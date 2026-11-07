// Main-thread side of the telemetry link (web: src/web/telemetry/client.ts). One class for every source.
//   TelemetryClient.Update() drains the source queue, applies hello/frames/faults/event/ack exactly as the web does, and
//   computes what to draw: the displayed frame (replay: 250 ms behind with SLERP; SITL: exponential approach k = 18),
//   alerts, the alert summary, the newest alert to announce, and the link state for the chip.
// No UI here (WP-10/WP-11 read the public properties). Pure C# except the optional Debug logging.

using System;
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    /// <summary>What the status chip shows.</summary>
    public enum LinkState
    {
        /// <summary>No source or stopped by the user.</summary>
        Disconnected,
        /// <summary>Connecting (includes the first quiet failures of a link that never opened).</summary>
        Connecting,
        /// <summary>Open and data fresh.</summary>
        Live,
        /// <summary>Open but no fresh data for more than StaleAfterMs (web "datos atrasados").</summary>
        Stale,
        /// <summary>Real outage (after the quiet window) while retrying with backoff.</summary>
        Error,
    }

    /// <summary>Fixed-capacity ring buffer, newest last. Main thread only.</summary>
    public sealed class RingBuffer<T>
    {
        readonly T[] items;
        int start, count;

        public RingBuffer(int capacity) { items = new T[Math.Max(1, capacity)]; }
        public int Count => count;
        public int Capacity => items.Length;

        public void Add(T x)
        {
            if (count < items.Length) items[(start + count++) % items.Length] = x;
            else { items[start] = x; start = (start + 1) % items.Length; }
        }

        /// <summary>i = 0 is the oldest.</summary>
        public T this[int i] => i < 0 || i >= count ? throw new ArgumentOutOfRangeException(nameof(i)) : items[(start + i) % items.Length];
        public T Newest => count > 0 ? this[count - 1] : default;

        public void Clear() { start = 0; count = 0; Array.Clear(items, 0, items.Length); }

        /// <summary>Newest first (as the web's events list).</summary>
        public List<T> NewestFirst(int max = int.MaxValue)
        {
            var r = new List<T>(Math.Min(max, count));
            for (int i = count - 1; i >= 0 && r.Count < max; i--) r.Add(this[i]);
            return r;
        }
    }

    public sealed class TelemetryClient : IDisposable
    {
        // Web constants (client.ts): InterpolationBuffer(600), PlaybackClock(250), SITL_STIFFNESS 18, events kept 30, stale 500 ms.
        public const int BufferCapacity = 600;
        public const double RenderDelayMs = 250;
        public const double SitlStiffness = 18;
        public const int MaxEvents = 30;
        public const double StaleAfterMs = 500;
        /// <summary>Raw (ground-corrected) frames kept for plots / diagnostics.</summary>
        public const int HistoryCapacity = 1200;

        public readonly InterpolationBuffer Buffer = new InterpolationBuffer(BufferCapacity);
        public readonly PlaybackClock Clock = new PlaybackClock(RenderDelayMs);
        readonly GroundReference ground = new GroundReference();
        public readonly RingBuffer<EventMessage> Events = new RingBuffer<EventMessage>(MaxEvents);
        public readonly RingBuffer<TelemetryFrame> History = new RingBuffer<TelemetryFrame>(HistoryCapacity);

        public ITelemetrySource Source { get; private set; }
        public HelloMessage Hello { get; private set; }
        public IReadOnlyList<Fault> Faults => faults;
        public AckMessage LastAck { get; private set; }
        public TransportState Transport { get; private set; } = TransportState.Idle;
        public int Failures { get; private set; }
        public string LastError { get; private set; }
        /// <summary>Measured inbound frame rate (Hz), refreshed every 2 s.</summary>
        public double RxHz { get; private set; }

        /// <summary>The frame to draw (ground-corrected, interpolated). null until data arrives.</summary>
        public TelemetryFrame Displayed { get; private set; }
        /// <summary>Replay: ms the render time is past the newest sample.</summary>
        public double StarvedMs { get; private set; }
        public IReadOnlyList<Alert> Alerts => alerts;
        public AlertSummary AlertSummary { get; private set; }
        /// <summary>Set for one Update when a new alert appears (the 7 s toast of the web); null otherwise.</summary>
        public Alert FreshAlert { get; private set; }
        /// <summary>Version warning when a future hello carries a different protocol major (null when fine or absent).</summary>
        public string ProtocolWarning { get; private set; }

        /// <summary>Classification to show (from the chosen source, never from hello.mode; Inspector when there is no source).</summary>
        public SourceClassification Classification => Source?.Classification ?? SourceClassification.Inspector;
        public bool HasFrame => Displayed != null;

        public LinkState State { get; private set; } = LinkState.Disconnected;

        /// <summary>Raised on the main thread.</summary>
        public event Action<HelloMessage> HelloReceived;
        public event Action<Alert> AlertRaised;
        public event Action<LinkState> StateChanged;
        public event Action<AckMessage> AckReceived;
        /// <summary>Log sink (defaults to UnityEngine.Debug). Set to null to silence.</summary>
        public Action<int, string> Log = DefaultLog;

        readonly List<Fault> faults = new List<Fault>();
        List<Alert> alerts = new List<Alert>();
        readonly HashSet<string> prevAlertIds = new HashSet<string>();
        readonly List<SourceEvent> drained = new List<SourceEvent>();
        double lastFrameRxMs = double.NaN, openedAtMs = double.NaN, lastUpdateMs = double.NaN;
        int rxCount;
        double rxWindowStart = double.NaN;

        public TelemetryClient(ITelemetrySource source = null) { if (source != null) SetSource(source); }

        /// <summary>Replaces the source (stops the previous one) and clears every per-session state.</summary>
        public void SetSource(ITelemetrySource source, bool start = false)
        {
            Source?.Dispose();
            Source = source;
            ResetSession();
            Hello = null;
            faults.Clear();
            Transport = TransportState.Idle;
            Failures = 0;
            UpdateState(TelemetryTime.NowMs);
            if (start) Start();
        }

        public void Start() => Source?.Start();
        public void Stop() => Source?.Stop();

        /// <summary>Sends a client message (validated, max 512 chars). False with a reason when not sent.</summary>
        public bool Send(ClientMessage msg, out string reason)
        {
            if (Source == null) { reason = "no source"; return false; }
            if (Hello != null && msg.Type != ClientMessageType.Manual && !AcceptedBy(Hello, msg)) { reason = "the source does not accept " + Describe(msg); return false; }
            return Source.Send(msg, out reason);
        }

        static bool AcceptedBy(HelloMessage h, ClientMessage m)
        {
            switch (m)
            {
                case InjectMessage _: return h.Accepts("inject");
                case ClearMessage _: return h.Accepts("clear");
                case CommandMessage c: return h.Accepts(c.name);
                default: return true;
            }
        }

        static string Describe(ClientMessage m) => m is CommandMessage c ? c.name : m.Type.ToString().ToLowerInvariant();

        /// <summary>Once per rendered frame on the main thread. Returns the frame to draw (may be null).</summary>
        public TelemetryFrame Update(double localNowMs, double dtSeconds)
        {
            FreshAlert = null;
            lastUpdateMs = localNowMs;
            if (Source != null)
            {
                Source.Tick(localNowMs);
                drained.Clear();
                Source.Drain(drained);
                foreach (var e in drained) Handle(e);
            }
            Advance(localNowMs, dtSeconds);
            EvaluateAlerts();
            UpdateState(localNowMs);
            return Displayed;
        }

        /// <summary>Convenience overload with TelemetryTime.NowMs.</summary>
        public TelemetryFrame Update(double dtSeconds) => Update(TelemetryTime.NowMs, dtSeconds);

        // ------------------------------------------------------------------------------------------------ handling

        void Handle(SourceEvent e)
        {
            switch (e.Kind)
            {
                case SourceEventKind.State:
                    Transport = e.State;
                    Failures = e.Failures;
                    if (e.Text != null) LastError = e.Text;
                    if (e.State == TransportState.Open) { openedAtMs = e.AtMs; LastError = null; }
                    if (e.State != TransportState.Open) lastFrameRxMs = double.NaN;
                    if ((e.State == TransportState.Connecting || e.State == TransportState.Closed) && e.Failures > 0 && ReconnectPolicy.ShouldLogFailure(e.Failures, e.EverOpened))
                        Log?.Invoke(1, "[Telemetry] " + Source?.Endpoint + ": " + e.Failures + " failed attempt(s); next retry in " +
                            (ReconnectPolicy.RetryDelayMs(e.Failures) / 1000.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) +
                            " s (grows up to 15 s)" + (e.Text != null ? " - " + e.Text : ""));
                    break;
                case SourceEventKind.Log:
                    Log?.Invoke(e.Level, "[Telemetry] " + e.Text);
                    break;
                case SourceEventKind.Message:
                    HandleMessage(e.Message);
                    break;
            }
        }

        void HandleMessage(ServerMessage msg)
        {
            switch (msg)
            {
                case HelloMessage h:
                    // A new hello is a new session: clear buffer, clock and ground reference.
                    ResetSession();
                    Hello = h;
                    Events.Clear();
                    ProtocolWarning = null;
                    if (h.HasProtocolVersion && MajorOf(h.protocolVersion) != TelemetryProtocol.VersionMajor)
                    {
                        ProtocolWarning = "server protocol " + h.protocolVersion + " != client " + TelemetryProtocol.Version;
                        Log?.Invoke(1, "[Telemetry] " + ProtocolWarning);
                    }
                    if (Source != null && Source.Classification != SourceClassification.Inspector && WireNames.Of(h.mode) != WireNames.Of(Source.Classification))
                        Log?.Invoke(1, "[Telemetry] hello.mode '" + WireNames.Of(h.mode) + "' differs from the chosen source '" + WireNames.Of(Source.Classification) + "'; the badge keeps the chosen source");
                    HelloReceived?.Invoke(h);
                    break;
                case FramesMessage fm:
                {
                    double now = msg.ReceivedAtMs;
                    foreach (var f in fm.frames)
                    {
                        var g = ground.Apply(f);
                        Buffer.Push(g);
                        History.Add(g);
                    }
                    if (fm.frames.Length > 0)
                    {
                        Clock.Observe(fm.frames[fm.frames.Length - 1].ts, now);
                        lastFrameRxMs = now;
                    }
                    if (double.IsNaN(rxWindowStart)) rxWindowStart = now;
                    rxCount += fm.frames.Length;
                    if (now - rxWindowStart > 2000)
                    {
                        RxHz = rxCount * 1000.0 / (now - rxWindowStart);
                        rxCount = 0;
                        rxWindowStart = now;
                    }
                    break;
                }
                case FaultsMessage f:
                    faults.Clear();
                    faults.AddRange(f.faults);
                    break;
                case EventMessage ev:
                    Events.Add(ev);
                    break;
                case AckMessage a:
                    LastAck = a;
                    if (!a.ok && a.HasDetail)
                        Events.Add(new EventMessage { severity = EventSeverity.Warning, text = a.command + ": " + a.detail, ts = ReplaySchedule.WallClockMs(), ReceivedAtMs = a.ReceivedAtMs });
                    AckReceived?.Invoke(a);
                    break;
            }
        }

        static int MajorOf(string v)
        {
            if (string.IsNullOrEmpty(v)) return -1;
            int dot = v.IndexOf('.');
            return int.TryParse(dot < 0 ? v : v.Substring(0, dot), out int m) ? m : -1;
        }

        void ResetSession()
        {
            Buffer.Clear();
            Clock.Reset();
            ground.Reset();
            History.Clear();
            Displayed = null;
            StarvedMs = 0;
            alerts = new List<Alert>();
            prevAlertIds.Clear();
            AlertSummary = new AlertSummary();
            lastFrameRxMs = double.NaN;
            rxCount = 0;
            rxWindowStart = double.NaN;
            RxHz = 0;
        }

        // ------------------------------------------------------------------------------------------------ display

        void Advance(double localNow, double dt)
        {
            if (Hello != null && Hello.mode == TelemetryMode.Sitl)
            {
                var target = Buffer.Newest;
                if (target == null) return;
                Displayed = Interp.SmoothToward(Displayed, target, SitlStiffness, dt);
                StarvedMs = 0;
                return;
            }
            double t = Clock.RenderTime(localNow);
            if (double.IsNaN(t)) return;
            var r = Buffer.Sample(t);
            if (!r.HasValue) return;
            Buffer.Prune(t - 2000);
            StarvedMs = r.Value.starvedMs;
            Displayed = r.Value.frame;
        }

        void EvaluateAlerts()
        {
            if (Displayed == null)
            {
                if (alerts.Count > 0) { alerts = new List<Alert>(); prevAlertIds.Clear(); AlertSummary = new AlertSummary(); }
                return;
            }
            var next = WebGL.Telemetry.Alerts.Derive(Displayed);
            var fresh = WebGL.Telemetry.Alerts.Fresh(prevAlertIds, next);
            prevAlertIds.Clear();
            foreach (var a in next) prevAlertIds.Add(a.id);
            alerts = next;
            AlertSummary = WebGL.Telemetry.Alerts.Summary(next);
            if (fresh != null)
            {
                FreshAlert = fresh;
                AlertRaised?.Invoke(fresh);
            }
        }

        void UpdateState(double now)
        {
            LinkState s;
            switch (Transport)
            {
                case TransportState.Connecting: s = LinkState.Connecting; break;
                case TransportState.Closed: s = LinkState.Error; break;
                case TransportState.Open:
                {
                    double age = double.IsNaN(lastFrameRxMs) ? (double.IsNaN(openedAtMs) ? 0 : now - openedAtMs) : now - lastFrameRxMs;
                    s = age > StaleAfterMs || StarvedMs > StaleAfterMs ? LinkState.Stale : LinkState.Live;
                    break;
                }
                default: s = LinkState.Disconnected; break;
            }
            if (Source == null) s = LinkState.Disconnected;
            if (s != State)
            {
                State = s;
                StateChanged?.Invoke(s);
            }
        }

        /// <summary>ms since the last frame arrived (NaN when none in this session).</summary>
        public double LastFrameAgeMs => double.IsNaN(lastFrameRxMs) || double.IsNaN(lastUpdateMs) ? double.NaN : lastUpdateMs - lastFrameRxMs;

        public void Dispose()
        {
            Source?.Dispose();
            Source = null;
        }

        static void DefaultLog(int level, string text)
        {
            if (level >= 2) UnityEngine.Debug.LogError(text);
            else if (level == 1) UnityEngine.Debug.LogWarning(text);
            else UnityEngine.Debug.Log(text);
        }
    }
}
