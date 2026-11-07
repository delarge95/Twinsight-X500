// Source abstraction. Every telemetry source (remote WebSocket today, WP-09's local replay tomorrow) produces the same
// SourceEvents; TelemetryClient drains them on the main thread. Sources may produce events on any thread.
// Pure C# (no network here).

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace WebGL.Telemetry
{
    /// <summary>Monotonic local clock in ms shared by sources (receive thread) and the session (main thread).</summary>
    public static class TelemetryTime
    {
        static readonly Stopwatch sw = Stopwatch.StartNew();
        public static double NowMs => sw.Elapsed.TotalMilliseconds;
    }

    /// <summary>Transport-level state of a source (web ConnState plus Idle).</summary>
    public enum TransportState
    {
        /// <summary>Not started or stopped by the user.</summary>
        Idle,
        /// <summary>Connecting, or failing quietly (first 3 failures of a link never open).</summary>
        Connecting,
        /// <summary>Link open (a local source is always open while running).</summary>
        Open,
        /// <summary>Real outage: retrying with backoff, shown as offline.</summary>
        Closed,
    }

    public enum SourceEventKind { State, Message, Log }

    public struct SourceEvent
    {
        public SourceEventKind Kind;
        public TransportState State;
        public int Failures;
        public bool EverOpened;
        public ServerMessage Message;
        /// <summary>Log text, or the reason of a state change (last error).</summary>
        public string Text;
        /// <summary>Log level for Kind == Log: 0 info, 1 warning, 2 error.</summary>
        public int Level;
        public double AtMs;
    }

    public interface ITelemetrySource : IDisposable
    {
        /// <summary>The classification the user chose (drives the honesty badge; not hello.mode).</summary>
        SourceClassification Classification { get; }
        /// <summary>True for sources inside the .exe (WP-09 local replay): label as "reproducción local", never "en vivo".</summary>
        bool IsLocal { get; }
        /// <summary>Human-readable endpoint (URL or file) for logs and the source popover.</summary>
        string Endpoint { get; }
        TransportState State { get; }

        void Start();
        void Stop();

        /// <summary>Thread-safe. False (with a reason) when the message is invalid, too long or the link is not open.</summary>
        bool Send(ClientMessage msg, out string reason);

        /// <summary>Main thread: moves pending events into `into`; returns how many.</summary>
        int Drain(List<SourceEvent> into, int max = int.MaxValue);

        /// <summary>Main thread, once per rendered frame (local sources generate frames here; remote ones do nothing).</summary>
        void Tick(double localNowMs);
    }

    /// <summary>Base class: thread-safe event queue + state bookkeeping. WP-09's local replay derives from it.</summary>
    public abstract class QueuedTelemetrySource : ITelemetrySource
    {
        readonly ConcurrentQueue<SourceEvent> queue = new ConcurrentQueue<SourceEvent>();
        volatile TransportState state = TransportState.Idle;
        /// <summary>Upper bound of queued events (protects memory if the main thread stalls); oldest messages are dropped.</summary>
        public int MaxQueued = 4096;
        int dropped;

        public abstract SourceClassification Classification { get; }
        public abstract bool IsLocal { get; }
        public abstract string Endpoint { get; }
        public TransportState State => state;

        public abstract void Start();
        public abstract void Stop();
        public abstract bool Send(ClientMessage msg, out string reason);
        public virtual void Tick(double localNowMs) { }

        public int Drain(List<SourceEvent> into, int max = int.MaxValue)
        {
            int n = 0;
            while (n < max && queue.TryDequeue(out var e)) { into.Add(e); n++; }
            int d = System.Threading.Interlocked.Exchange(ref dropped, 0);
            if (d > 0) into.Add(new SourceEvent { Kind = SourceEventKind.Log, Level = 1, Text = d + " telemetry event(s) dropped (main thread too slow)", AtMs = TelemetryTime.NowMs });
            return n;
        }

        protected void PostState(TransportState s, int failures, bool everOpened, string reason = null)
        {
            state = s;
            Enqueue(new SourceEvent { Kind = SourceEventKind.State, State = s, Failures = failures, EverOpened = everOpened, Text = reason, AtMs = TelemetryTime.NowMs });
        }

        protected void PostMessage(ServerMessage m)
        {
            if (m.ReceivedAtMs == 0) m.ReceivedAtMs = TelemetryTime.NowMs;
            Enqueue(new SourceEvent { Kind = SourceEventKind.Message, Message = m, AtMs = m.ReceivedAtMs });
        }

        protected void PostLog(int level, string text) =>
            Enqueue(new SourceEvent { Kind = SourceEventKind.Log, Level = level, Text = text, AtMs = TelemetryTime.NowMs });

        void Enqueue(SourceEvent e)
        {
            queue.Enqueue(e);
            while (queue.Count > MaxQueued && queue.TryDequeue(out _)) System.Threading.Interlocked.Increment(ref dropped);
        }

        public virtual void Dispose() => Stop();
    }

    /// <summary>
    /// Source fed by code: tests, diagnostics and any producer that already has parsed messages or JSON text.
    /// Thread-safe Push*. Sent client messages are recorded in Sent.
    /// </summary>
    public sealed class ManualTelemetrySource : QueuedTelemetrySource
    {
        readonly SourceClassification classification;
        readonly bool local;
        public readonly ConcurrentQueue<string> Sent = new ConcurrentQueue<string>();

        public ManualTelemetrySource(SourceClassification classification, bool isLocal = true)
        {
            this.classification = classification;
            local = isLocal;
        }

        public override SourceClassification Classification => classification;
        public override bool IsLocal => local;
        public override string Endpoint => "manual";

        public override void Start() => PostState(TransportState.Open, 0, true);
        public override void Stop() => PostState(TransportState.Idle, 0, true);

        public void SetState(TransportState s, int failures = 0, bool everOpened = true) => PostState(s, failures, everOpened);

        public void Push(ServerMessage m) => PostMessage(m);

        /// <summary>Parses (tolerant) and pushes; false + reason when the text is not a valid server message.</summary>
        public bool PushJson(string json, out string error)
        {
            if (!ProtocolParser.TryParseServer(json, out var m, out error)) return false;
            PostMessage(m);
            return true;
        }

        public override bool Send(ClientMessage msg, out string reason)
        {
            reason = msg.Validate();
            if (reason != null) return false;
            Sent.Enqueue(msg.ToJson());
            return true;
        }
    }

    /// <summary>Endpoints. No deployed worker exists yet (CONTEXTO §3): the replay default is wrangler dev.</summary>
    public static class TelemetryEndpoints
    {
        public const string ReplayDev = "ws://localhost:8787/ws";
        public const string SitlDefault = "ws://localhost:8765";

        /// <summary>Value of `-name value` on the command line, or null.</summary>
        public static string CommandLine(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        public static string Replay => CommandLine("-replay-url") ?? ReplayDev;
        public static string Sitl => CommandLine("-sitl-url") ?? SitlDefault;
    }
}
