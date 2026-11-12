// WP-09: the local replay source (P-3). Same data the 24/7 worker streams, produced inside the .exe from the bundled TWF1 flight
// log (StreamingAssets/Telemetry/x500v2_flight.twf, sha256 in golden/replay.json) so the app shows the digital shadow with no network.
//  - Mirrors src/worker/index.ts: hello on start, then faults, then the last PrimeMs of frames; a tick every TickMs sends the frames
//    with play time in (lastSent, now] (ReplaySchedule.FramesBetween, wall clock + fixed epoch: the loop is seamless, there is no pause
//    and no seek) with the active synthetic faults applied (Faults.Apply). inject / clear go through Faults.Validate / Add with the same
//    acks and the same 1.5 s command cooldown; any other command gets the worker negative ack.
//  - Classification is Replay and IsLocal is true: the badge text is HonestyLabels.TryLocalReplayText ("Sombra digital · reproduccion
//    local"), never "en vivo" and never a digital twin of a physical drone.
// Pure C# (the disk loader is desktop-only). Clocks are injectable for tests.

using System;
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    public sealed class LocalReplayTelemetrySource : QueuedTelemetrySource
    {
        /// <summary>Worker constants (src/worker/index.ts).</summary>
        public const double TickMs = 100;
        public const double PrimeMs = 600;
        public const double CommandCooldownMs = 1500;
        public const string StreamingRelativePath = "Telemetry/x500v2_flight.twf";
        /// <summary>The worker hello `source.label` is a literal there; a test compares it with fixtures/hello.replay.json.</summary>
        public const string SourceLabel = "Log real de PX4 Flight Review · Holybro X500 V2";

        public readonly TwfFile File;
        public readonly ReplaySchedule Schedule;
        /// <summary>Wall clock in Unix ms (frames are scheduled on it). Tests inject a fake one.</summary>
        public Func<double> WallNowMs = ReplaySchedule.WallClockMs;
        /// <summary>Local monotonic clock, the one TelemetryClient.Update receives.</summary>
        public Func<double> LocalNowMs = () => TelemetryTime.NowMs;

        List<Fault> faults = new List<Fault>();
        bool running;
        double lastSentWall, lastTickLocal = double.NaN, lastCommandWall = double.NegativeInfinity;

        public LocalReplayTelemetrySource(TwfFile file, double epochMs = ReplaySchedule.ReplayEpochMs)
        {
            File = file ?? throw new ArgumentNullException(nameof(file));
            Schedule = new ReplaySchedule(file, epochMs);
        }

        public LocalReplayTelemetrySource(byte[] twfBytes) : this(TwfFile.Decode(twfBytes)) { }

        public override SourceClassification Classification => SourceClassification.Replay;
        public override bool IsLocal => true;
        public override string Endpoint => "local replay (" + StreamingRelativePath + ")";
        public bool IsRunning => running;
        public IReadOnlyList<Fault> ActiveFaults => faults;

        /// <summary>The hello the worker would send for this file (mode replay; serverTime = the wall clock now).</summary>
        public HelloMessage BuildHello(double serverTimeMs)
        {
            var h = File.Header;
            double ambient = 20;
            if (h.Raw != null && h.Raw.TryGetValue("thermal", out var th) && th is Dictionary<string, object> td &&
                td.TryGetValue("ambient_c", out var a) && a is double ad) ambient = ad;
            var src = new SourceInfo
            {
                label = SourceLabel,
                vehicle = (h.vehicleHardware ?? "") + " · airframe " + h.vehicleSysAutostart, HasVehicle = true,
                durationS = h.duration_s, HasDurationS = h.HasDuration,
            };
            if (!string.IsNullOrEmpty(h.sourceUrl)) { src.url = h.sourceUrl; src.HasUrl = true; }
            if (!string.IsNullOrEmpty(h.vehiclePx4Version)) { src.px4 = h.vehiclePx4Version; src.HasPx4 = true; }
            return new HelloMessage
            {
                mode = TelemetryMode.Replay,
                rotors = h.rotors ?? Array.Empty<RotorGeometry>(),
                rateHz = h.rate_hz,
                provenance = new Provenance
                {
                    rpm = "estimated", motorTemp = "estimated", batteryTemp = "estimated", fcTemp = "measured",
                    batteryCurrent = "measured", HasBatteryCurrent = true,
                },
                batteryCells = h.vehicleBatCells > 0 ? h.vehicleBatCells : 4,
                ambientC = ambient,
                source = src,
                commands = new[] { "inject", "clear" },
                serverTime = serverTimeMs,
            };
        }

        public override void Start()
        {
            if (running) return;
            running = true;
            faults = new List<Fault>();
            double wall = WallNowMs(), local = LocalNowMs();
            PostState(TransportState.Open, 0, true);
            PostMessage(Stamp(BuildHello(wall), local));
            PostMessage(Stamp(new FaultsMessage { faults = Faults.Active(faults, wall).ToArray() }, local));
            PostFrames(wall - PrimeMs, wall, local);
            lastSentWall = wall;
            lastTickLocal = local;
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
            int before = faults.Count;
            faults = Faults.Active(faults, wall); // expired ones leave (web: activeFaults)
            if (faults.Count != before) PostMessage(Stamp(new FaultsMessage { faults = faults.ToArray() }, localNowMs));
            PostFrames(lastSentWall, wall, localNowMs);
            lastSentWall = wall;
        }

        void PostFrames(double fromWall, double toWall, double local)
        {
            var live = Faults.Active(faults, toWall);
            var frames = Schedule.FramesBetween(fromWall, toWall);
            if (frames.Count == 0) return;
            var arr = new TelemetryFrame[frames.Count];
            for (int i = 0; i < arr.Length; i++) arr[i] = Faults.Apply(frames[i], live);
            PostMessage(Stamp(new FramesMessage { frames = arr }, local));
        }

        public override bool Send(ClientMessage msg, out string reason)
        {
            reason = msg.Validate();
            if (reason != null) return false;
            if (msg.ToJson().Length > TelemetryProtocol.MaxClientMessageChars) { reason = "message longer than 512 characters"; return false; }
            if (!running) { reason = "link not open"; return false; }
            double wall = WallNowMs(), local = LocalNowMs();
            string name = msg is CommandMessage c ? c.name : msg.Type.ToString().ToLowerInvariant();
            if (wall - lastCommandWall < CommandCooldownMs)
            {
                Ack(name, false, "demasiado rápido, espera un segundo", local);
                return true;
            }
            lastCommandWall = wall;
            if (msg is InjectMessage inj)
            {
                if (!Faults.Validate(WireNames.Of(inj.kind), inj.motor.HasValue ? (object)(double)inj.motor.Value : null, out var kind, out var motor))
                {
                    Ack("inject", false, "falla no válida", local);
                    return true;
                }
                faults = Faults.Add(faults, kind, motor, wall);
            }
            else if (msg is ClearMessage) faults = new List<Fault>();
            else
            {
                Ack(name, false, "la repetición 24/7 no acepta comandos de vuelo; usa el modo SITL", local);
                return true;
            }
            Ack(name, true, null, local);
            PostMessage(Stamp(new FaultsMessage { faults = Faults.Active(faults, wall).ToArray() }, local));
            return true;
        }

        void Ack(string command, bool ok, string detail, double local) =>
            PostMessage(Stamp(new AckMessage { command = command, ok = ok, detail = detail, HasDetail = detail != null }, local));

        static ServerMessage Stamp(ServerMessage m, double localMs) { m.ReceivedAtMs = localMs; return m; }

#if UNITY_STANDALONE || UNITY_EDITOR
        /// <summary>Path of the bundled flight log in the player / Editor.</summary>
        public static string BundledPath => System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, StreamingRelativePath);

        /// <summary>Loads the bundled TWF; null (with `error`) when it is missing or corrupt. Never invents data.</summary>
        public static LocalReplayTelemetrySource LoadBundled(out string error)
        {
            error = null;
            try
            {
                string p = BundledPath;
                if (!System.IO.File.Exists(p)) { error = "missing " + p; return null; }
                return new LocalReplayTelemetrySource(System.IO.File.ReadAllBytes(p));
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
                return null;
            }
        }
#endif
    }
}
