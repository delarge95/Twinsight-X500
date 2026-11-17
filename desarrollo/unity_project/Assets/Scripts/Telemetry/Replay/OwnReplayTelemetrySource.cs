// Lote 1c, tool 7: "Mis sesiones" - the replay of a flight recorded by this app (web: src/web/telemetry/ownreplay.ts). It goes through the same ReplaySchedule as the
// real log, with the epoch at the moment of loading (it starts at 0 and loops). v1 has no pause and no seek. It takes no commands and no synthetic faults, and every
// reading is simulated: there is no physical drone behind it, so it is NEVER called "sombra digital" (its badge is classification.replay.own.sim|sitl).
// Pure C# (the disk loading is the caller's).

using System;
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    public sealed class OwnReplayTelemetrySource : QueuedTelemetrySource
    {
        public const double TickMs = 100;
        public readonly TwfFile File;
        /// <summary>"sim" or "sitl": what the recording was made with.</summary>
        public readonly string Kind;
        /// <summary>The name of the file (for the endpoint text only).</summary>
        public readonly string Name;
        /// <summary>The schedule of this playback: its epoch is the moment <see cref="Start"/> ran, so the session begins at 0 and loops.</summary>
        public ReplaySchedule Schedule { get; private set; }
        public Func<double> WallNowMs = ReplaySchedule.WallClockMs;
        public Func<double> LocalNowMs = () => TelemetryTime.NowMs;

        bool running;
        double lastSentWall, lastTickLocal = double.NaN;

        public OwnReplayTelemetrySource(TwfFile file, string kind, string name = null)
        {
            File = file ?? throw new ArgumentNullException(nameof(file));
            if (kind != TwfWriter.KindSim && kind != TwfWriter.KindSitl) throw new ArgumentException("kind must be sim or sitl", nameof(kind));
            Kind = kind;
            Name = name ?? "session";
            Schedule = new ReplaySchedule(file, WallNowMs());
        }

        /// <summary>Opens the bytes as an own session; null (with the reason) when they are not one.</summary>
        public static OwnReplayTelemetrySource FromBytes(byte[] bytes, string name, out TwfWriter.OwnFail fail)
        {
            fail = TwfWriter.ParseOwn(bytes, out TwfFile file, out string kind);
            return fail == TwfWriter.OwnFail.None ? new OwnReplayTelemetrySource(file, kind, name) : null;
        }

        /// <summary>A replay, but not of a real flight: the app's own classification of it (the badge text comes from classification.replay.own.*).</summary>
        public override SourceClassification Classification => SourceClassification.Replay;
        public override bool IsLocal => true;
        public override string Endpoint => "own session (" + Name + ")";
        public bool IsRunning => running;
        public double DurationS => File.Header.frame_count / File.Header.rate_hz;

        /// <summary>The hello of a replayed session. The channels are model values (the simulator or SITL), not sensor readings. No commands.</summary>
        public HelloMessage BuildHello(double serverTimeMs)
        {
            TwfHeader h = File.Header;
            double ambient = 22;
            if (h.Raw != null && h.Raw.TryGetValue("thermal", out var th) && th is Dictionary<string, object> td && td.TryGetValue("ambient_c", out var a) && a is double ad) ambient = ad;
            return new HelloMessage
            {
                mode = TelemetryMode.Replay,
                rotors = h.rotors ?? Array.Empty<RotorGeometry>(),
                rateHz = h.rate_hz,
                provenance = new Provenance { rpm = "estimated", motorTemp = "estimated", batteryTemp = "estimated", fcTemp = "unavailable", batteryCurrent = "estimated", HasBatteryCurrent = true },
                batteryCells = h.vehicleBatCells > 0 ? h.vehicleBatCells : 4,
                ambientC = ambient,
                source = new SourceInfo
                {
                    // the web's provisional text, with "de la app" where the web says "del navegador"
                    label = Kind == TwfWriter.KindSim ? "Sesión propia grabada en el simulador de la app (no es un dron real)" : "Sesión propia grabada con PX4 SITL (no es un dron real)",
                    durationS = DurationS, HasDurationS = true,
                },
                commands = Array.Empty<string>(),
                serverTime = serverTimeMs,
            };
        }

        public override void Start()
        {
            if (running) return;
            running = true;
            double wall = WallNowMs(), local = LocalNowMs();
            Schedule = new ReplaySchedule(File, wall); // epoch = the moment of loading: frame 0 plays now
            PostState(TransportState.Open, 0, true);
            PostMessage(Stamp(BuildHello(wall), local));
            PostMessage(Stamp(new FaultsMessage { faults = new Fault[0] }, local));
            lastSentWall = wall - 1;
            lastTickLocal = local;
            PostFrames(lastSentWall, wall, local);
            lastSentWall = wall;
        }

        public override void Stop()
        {
            if (!running) return;
            running = false;
            PostState(TransportState.Idle, 0, true);
        }

        public override void Tick(double localNowMs)
        {
            if (!running) return;
            if (!double.IsNaN(lastTickLocal) && localNowMs - lastTickLocal < TickMs) return;
            lastTickLocal = localNowMs;
            double wall = WallNowMs();
            PostFrames(lastSentWall, wall, localNowMs);
            lastSentWall = wall;
        }

        void PostFrames(double fromWall, double toWall, double local)
        {
            List<TelemetryFrame> frames = Schedule.FramesBetween(fromWall, toWall);
            if (frames.Count == 0) return;
            PostMessage(Stamp(new FramesMessage { frames = frames.ToArray() }, local));
        }

        /// <summary>A recording cannot react: every order and every synthetic fault is refused with its reason (the stick stream is dropped silently).</summary>
        public override bool Send(ClientMessage msg, out string reason)
        {
            reason = msg.Validate();
            if (reason != null) return false;
            if (!running) { reason = "link not open"; return false; }
            if (msg is ManualMessage) return true;
            string name = msg is CommandMessage c ? c.name : msg.Type.ToString().ToLowerInvariant();
            PostMessage(Stamp(new AckMessage { command = name, ok = false, detail = "la sesión propia no acepta comandos ni fallas", HasDetail = true }, LocalNowMs()));
            return true;
        }

        static ServerMessage Stamp(ServerMessage m, double localMs) { m.ReceivedAtMs = localMs; return m; }
    }
}
