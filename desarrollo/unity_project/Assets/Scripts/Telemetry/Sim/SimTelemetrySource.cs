// E-5: the flight SIMULATOR as a telemetry source (web: SimClient in src/web/telemetry/local.ts). It speaks the same protocol as the SITL
// bridge: a `hello` with wire mode `sitl` (so the client shows the drone at once, without the replay buffer, and the command panels enable),
// then frames from the kinematic model of FlightSim, and acks for the commands. Pure C# (no UnityEngine): the clocks are injectable.
//
// HONESTY (never negotiable): this is NOT PX4 SITL, NOT a real drone and NOT a digital shadow. The badge and the origin text come from
// HonestyLabels.TrySimText ("Simulador de vuelo" / "No es PX4 ni un dron real: todos sus datos son simulados"), the provenance of every
// series is shown as "simulado", and nothing here calls itself SITL: the wire mode `sitl` is only the protocol dialect that enables commands.
// The parameters of the model are plausible, not calibrated against the X500 (F-2 is a later package).

using System;
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    public sealed class SimTelemetrySource : QueuedTelemetrySource
    {
        /// <summary>Fixed sub-step of the model (the web steps it at 120 Hz).</summary>
        public const double StepS = 1.0 / 120.0;
        /// <summary>Frames are posted at most at this rate (hello.rateHz).</summary>
        public const double FrameHz = 60;
        public const double MaxCarryS = 0.25;
        public const double AmbientC = 22;

        /// <summary>Same text as the web SIM_HELLO, with "in the app" instead of "in the browser" (the source is the desktop app).</summary>
        public const string SourceLabel = "Simulador de vuelo de la aplicación · modelo cinemático (no es PX4)";

        /// <summary>Wall clock in Unix ms (frame ts). Tests inject a fake one.</summary>
        public Func<double> WallNowMs = ReplaySchedule.WallClockMs;
        public Func<double> LocalNowMs = () => TelemetryTime.NowMs;

        readonly object gate = new object();
        SimState sim = FlightSim.Create(AmbientC);
        /// <summary>The local configuration a (re)started flight begins with (geofence, wind...); null = the defaults. Set it before Start.</summary>
        public SimConfig InitialConfig;
        bool running;
        readonly List<SimEvent> pendingEvents = new List<SimEvent>();
        double carry, lastTickLocal = double.NaN, sinceFrame;

        /// <summary>The model (tests and the on-screen readout read it; mutate it only through Send).</summary>
        public SimState Model { get { lock (gate) return sim; } }
        public bool IsRunning => running;

        /// <summary>Changes the local configuration of the running model (FlightSim.SetConfig): a refusal says why and changes nothing.</summary>
        public ConfigCheck SetConfig(SimConfigPatch patch)
        {
            lock (gate) return FlightSim.SetConfig(sim, patch);
        }

        // The classification the client/UI uses for honesty rules (measured -> simulated). The UI never shows it as SITL: DesktopTelemetry
        // checks IsSimulator first and takes the badge from HonestyLabels.TrySimText.
        public override SourceClassification Classification => SourceClassification.Sitl;
        public override bool IsLocal => true;
        public override string Endpoint => "flight simulator (kinematic model, not PX4)";

        public HelloMessage BuildHello(double serverTimeMs)
        {
            return new HelloMessage
            {
                mode = TelemetryMode.Sitl,
                rotors = QuadX.Px4QuadX,
                rateHz = FrameHz,
                provenance = new Provenance
                {
                    rpm = "estimated", motorTemp = "estimated", batteryTemp = "estimated", fcTemp = "unavailable",
                    batteryCurrent = "estimated", HasBatteryCurrent = true,
                },
                batteryCells = 4,
                ambientC = AmbientC,
                source = new SourceInfo { label = SourceLabel },
                commands = (string[])FlightSim.Commands.Clone(),
                serverTime = serverTimeMs,
            };
        }

        public override void Start()
        {
            if (running) return;
            double wall = WallNowMs(), local = LocalNowMs();
            lock (gate)
            {
                sim = FlightSim.Create(AmbientC, InitialConfig);
                carry = 0;
                sinceFrame = 0;
            }
            running = true;
            lastTickLocal = local;
            PostState(TransportState.Open, 0, true);
            PostMessage(Stamp(BuildHello(wall), local));
            PostMessage(Stamp(new FaultsMessage { faults = Array.Empty<Fault>() }, local));
            PostFrame(wall, local);
        }

        public override void Stop()
        {
            if (!running) return;
            running = false;
            PostState(TransportState.Idle, 0, true);
        }

        /// <summary>The model is stepped here, once per rendered frame, at a fixed 120 Hz sub-step; frames go out at 60 Hz.</summary>
        public override void Tick(double localNowMs)
        {
            if (!running) return;
            double dt = double.IsNaN(lastTickLocal) ? 0 : Math.Max(0, (localNowMs - lastTickLocal) / 1000.0);
            lastTickLocal = localNowMs;
            lock (gate)
            {
                carry = Math.Min(carry + dt, MaxCarryS);
                while (carry >= StepS)
                {
                    FlightSim.Step(sim, StepS);
                    carry -= StepS;
                    if (sim.events.Count > 0) pendingEvents.AddRange(FlightSim.TakeEvents(sim)); // impact / forced landing of a cut motor
                }
                sinceFrame += dt;
            }
            if (pendingEvents.Count > 0)
            {
                foreach (SimEvent e in pendingEvents)
                    PostMessage(Stamp(new EventMessage { severity = e.severity, text = e.text, ts = WallNowMs() }, localNowMs));
                pendingEvents.Clear();
            }
            if (sinceFrame >= 1.0 / FrameHz - 1e-6)
            {
                sinceFrame = 0;
                PostFrame(WallNowMs(), localNowMs);
            }
        }

        void PostFrame(double wall, double local)
        {
            TelemetryFrame f;
            lock (gate) f = FlightSim.Frame(sim, wall);
            PostMessage(Stamp(new FramesMessage { frames = new[] { f } }, local));
        }

        public override bool Send(ClientMessage msg, out string reason)
        {
            reason = msg.Validate();
            if (reason != null) return false;
            if (msg.ToJson().Length > msg.MaxChars) { reason = "message longer than " + msg.MaxChars + " characters"; return false; }
            if (!running) { reason = "link not open"; return false; }
            double local = LocalNowMs();
            if (msg is ManualMessage m)
            {
                lock (gate) FlightSim.Manual(sim, new SimSticks(m.x, m.y, m.z, m.r));
                return true;
            }
            if (!(msg is CommandMessage c))
            {
                // inject / clear: PX4 fault injection does not exist here
                string name = msg.Type.ToString().ToLowerInvariant();
                PostMessage(Stamp(new AckMessage { command = name, ok = false, detail = FlightSim.NotAvailable, HasDetail = true }, local));
                return true;
            }
            SimReply reply;
            lock (gate) reply = FlightSim.Command(sim, c);
            PostMessage(Stamp(new AckMessage { command = c.name, ok = reply.ok, detail = reply.detail, HasDetail = reply.detail != null }, local));
            if (reply.evt != null)
                PostMessage(Stamp(new EventMessage { severity = EventSeverity.Info, text = reply.evt, ts = WallNowMs() }, local));
            return true;
        }

        static ServerMessage Stamp(ServerMessage m, double localMs) { m.ReceivedAtMs = localMs; return m; }
    }
}
