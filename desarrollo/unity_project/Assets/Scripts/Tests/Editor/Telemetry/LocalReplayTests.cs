// WP-09: local replay source tests. EditMode, fake clocks, no scene.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace WebGL.Telemetry.Tests
{
    using JObj = Dictionary<string, object>;

    public class LocalReplayTests
    {
        const double LocalStart = 1000;

        sealed class Clock
        {
            public double Local = LocalStart;
            public double Wall;
            public void Advance(double ms) { Local += ms; Wall += ms; }
        }

        static byte[] FlightBytes()
        {
            string path = GoldenReplayTests.FindFlightTwf();
            if (path == null) Assert.Inconclusive("x500v2_flight.twf not found");
            return System.IO.File.ReadAllBytes(path);
        }

        static (LocalReplayTelemetrySource src, TelemetryClient client, Clock clock) Make(double wallAtStart)
        {
            var clock = new Clock { Wall = wallAtStart };
            var src = new LocalReplayTelemetrySource(FlightBytes()) { WallNowMs = () => clock.Wall, LocalNowMs = () => clock.Local };
            var client = new TelemetryClient(src) { Log = null };
            return (src, client, clock);
        }

        /// <summary>Runs `ms` of simulated time at 60 Hz, returning every drained frame message through the client history.</summary>
        static void Run(TelemetryClient c, Clock clock, double ms)
        {
            for (double t = 0; t < ms; t += 1000.0 / 60)
            {
                clock.Advance(1000.0 / 60);
                c.Update(clock.Local, 1.0 / 60);
            }
        }

        [Test]
        public void Bundled_File_Loads_And_Is_Labelled_Local_Replay()
        {
            var src = LocalReplayTelemetrySource.LoadBundled(out string err);
            Assert.IsNotNull(src, err);
            Assert.IsTrue(src.IsLocal);
            Assert.AreEqual(SourceClassification.Replay, src.Classification);
            Assert.AreEqual(9492, src.Schedule.FrameCount);
            Assert.IsTrue(HonestyLabels.Parse(C.Text("labels.json")).TryLocalReplayText("es", out string t));
            Assert.AreEqual("Sombra digital · reproducción local", t);
            StringAssert.DoesNotContain("vivo", t);
        }

        [Test]
        public void Hello_Matches_The_Worker_Fixture_Except_ServerTime()
        {
            var (src, _, _) = Make(ReplaySchedule.ReplayEpochMs + 120000);
            Assert.IsTrue(ProtocolParser.TryParseServer(C.Text("fixtures/hello.replay.json"), out var m, out string err), err);
            var want = (HelloMessage)m;
            var got = src.BuildHello(want.serverTime);
            Assert.AreEqual(want.mode, got.mode);
            Assert.AreEqual(want.rateHz, got.rateHz);
            Assert.AreEqual(want.batteryCells, got.batteryCells);
            Assert.AreEqual(want.ambientC, got.ambientC, 1e-9);
            Assert.AreEqual(want.rotors.Length, got.rotors.Length);
            for (int i = 0; i < want.rotors.Length; i++)
            {
                Assert.AreEqual(want.rotors[i].motor, got.rotors[i].motor);
                Assert.AreEqual(want.rotors[i].px, got.rotors[i].px, 1e-9);
                Assert.AreEqual(want.rotors[i].py, got.rotors[i].py, 1e-9);
                Assert.AreEqual(want.rotors[i].km, got.rotors[i].km, 1e-9);
            }
            Assert.AreEqual(want.provenance.rpm, got.provenance.rpm);
            Assert.AreEqual(want.provenance.motorTemp, got.provenance.motorTemp);
            Assert.AreEqual(want.provenance.batteryTemp, got.provenance.batteryTemp);
            Assert.AreEqual(want.provenance.fcTemp, got.provenance.fcTemp);
            Assert.AreEqual(want.provenance.BatteryCurrentOrDefault, got.provenance.BatteryCurrentOrDefault);
            Assert.AreEqual(want.source.label, got.source.label);
            Assert.AreEqual(want.source.url, got.source.url);
            Assert.AreEqual(want.source.vehicle, got.source.vehicle);
            Assert.AreEqual(want.source.px4, got.source.px4);
            Assert.AreEqual(want.source.durationS, got.source.durationS, 1e-9);
            CollectionAssert.AreEqual(want.commands, got.commands);
        }

        [Test]
        public void Session_Streams_Contiguous_Frames_At_20Hz_And_Is_Live()
        {
            var (src, c, clock) = Make(ReplaySchedule.ReplayEpochMs + 120000);
            c.Start();
            c.Update(clock.Local, 0);
            Assert.IsNotNull(c.Hello, "hello is the first message");
            Assert.AreEqual(TelemetryMode.Replay, c.Hello.mode);
            Assert.AreEqual(12, c.History.Count, "priming: the last 600 ms = 12 frames at 20 Hz");
            Run(c, clock, 3000);
            Assert.AreEqual(LinkState.Live, c.State);
            Assert.IsNotNull(c.Displayed);
            Assert.AreEqual(SourceClassification.Replay, c.Classification);
            var h = c.History;
            Assert.GreaterOrEqual(h.Count, 70);
            for (int i = 1; i < h.Count; i++) Assert.AreEqual(50, h[i].ts - h[i - 1].ts, 1e-6, "frame " + i + ": contiguous 50 ms steps, no gap and no repeat");
            Assert.AreEqual(12 + 60, h.Count, 2, "12 primed + 3 s at 20 Hz");
            src.Stop();
        }

        [Test]
        public void Loop_Wraps_Seamlessly_Like_The_Worker()
        {
            double loopMs = 9492 * 50.0;
            var (src, c, clock) = Make(ReplaySchedule.ReplayEpochMs + loopMs - 1500);
            c.Start();
            c.Update(clock.Local, 0);
            Run(c, clock, 3000);
            var h = c.History;
            Assert.AreEqual(0, h[0].seg);
            Assert.AreEqual(1, h[h.Count - 1].seg, "the loop id (seg) advanced");
            for (int i = 1; i < h.Count; i++) Assert.AreEqual(50, h[i].ts - h[i - 1].ts, 1e-6, "no gap across the wrap");
            // Row 0 follows row N-1: the position the web draws is the ground-corrected one, the raw replay frame is the file row.
            int wrap = Enumerable.Range(1, h.Count - 1).First(i => h[i].seg != h[i - 1].seg);
            Assert.AreEqual(src.Schedule.Frame(9492).lt, src.Schedule.Frame(0).lt, 1e-9);
            Assert.IsTrue(wrap > 0);
        }

        [Test]
        public void Inject_Clear_Cooldown_And_Rejected_Commands_Like_The_Worker()
        {
            var (src, c, clock) = Make(ReplaySchedule.ReplayEpochMs + 60000);
            c.Start();
            c.Update(clock.Local, 0);
            AckMessage last = null;
            c.AckReceived += a => last = a;

            Assert.IsFalse(c.Send(ClientMessage.Inject(FaultKind.MotorFailure, null), out string reason), "motor_failure needs a motor");
            Assert.IsNotNull(reason);

            Assert.IsTrue(c.Send(ClientMessage.Inject(FaultKind.MotorFailure, 3), out _));
            clock.Advance(20); c.Update(clock.Local, 0.02);
            Assert.IsTrue(last.ok);
            Assert.AreEqual("inject", last.command);
            Assert.AreEqual(1, c.Faults.Count);
            Assert.AreEqual("motor_failure:3", c.Faults[0].id);
            Assert.AreEqual(30000, c.Faults[0].until - c.Faults[0].since, 1e-6);

            // Within 1.5 s: the worker answers "demasiado rapido".
            clock.Advance(500);
            Assert.IsTrue(c.Send(ClientMessage.Inject(FaultKind.Vibration), out _));
            c.Update(clock.Local, 0.02);
            Assert.IsFalse(last.ok);
            Assert.AreEqual("demasiado rápido, espera un segundo", last.detail);
            Assert.AreEqual(1, c.Faults.Count);

            // Flight commands are not accepted by the replay (the hello does not list them, the client refuses before sending).
            Assert.IsFalse(c.Send(ClientMessage.Takeoff(5), out string noFly));
            StringAssert.Contains("does not accept", noFly);
            clock.Advance(2000);
            Assert.IsTrue(src.Send(ClientMessage.Takeoff(5), out _), "the source itself answers with the worker negative ack");
            c.Update(clock.Local, 0.02);
            Assert.IsFalse(last.ok);
            Assert.AreEqual("takeoff", last.command);
            Assert.AreEqual("la repetición 24/7 no acepta comandos de vuelo; usa el modo SITL", last.detail);

            clock.Advance(2000);
            Assert.IsTrue(c.Send(ClientMessage.Clear(), out _));
            c.Update(clock.Local, 0.02);
            Assert.IsTrue(last.ok);
            Assert.AreEqual("clear", last.command);
            Assert.AreEqual(0, c.Faults.Count);
        }

        [Test]
        public void Synthetic_Motor_Failure_Stops_The_Rotor_And_Raises_The_Alert()
        {
            // Second 120 of the flight: flying with the motors above 1000 rpm.
            var (src, c, clock) = Make(ReplaySchedule.ReplayEpochMs + 120000);
            c.Start();
            c.Update(clock.Local, 0);
            Run(c, clock, 800);
            Assert.IsTrue(c.Displayed.armed && !c.Displayed.landed, "precondition: flying");
            Assert.IsFalse(c.Alerts.Any(a => a.component == "motor3"), "no alert before the fault");
            Assert.IsTrue(c.Send(ClientMessage.Inject(FaultKind.MotorFailure, 3), out _));
            Run(c, clock, 4000);
            var f = c.Displayed;
            Assert.Less(f.rpm[2], 300, "rotor 3 spun down (ramp tau 0.8 s)");
            Assert.AreEqual(0, f.u[2], 1e-9);
            Assert.Greater(f.rpm[0], 1000, "the others keep flying");
            Assert.IsTrue(c.Alerts.Any(a => a.component == "motor3" && a.severity == AlertSeverity.Critical), "critical motor-out alert on motor3");
            Assert.GreaterOrEqual(c.AlertSummary.count, 1);
        }

        [Test]
        public void Stop_Is_Idle_And_Restart_Resets_Faults()
        {
            var (src, c, clock) = Make(ReplaySchedule.ReplayEpochMs + 120000);
            c.Start(); c.Update(clock.Local, 0);
            c.Send(ClientMessage.Inject(FaultKind.Vibration), out _);
            clock.Advance(50); c.Update(clock.Local, 0.05);
            Assert.AreEqual(1, c.Faults.Count);
            c.Stop(); clock.Advance(50); c.Update(clock.Local, 0.05);
            Assert.AreEqual(TransportState.Idle, c.Transport);
            Assert.AreEqual(LinkState.Disconnected, c.State);
            c.Start(); clock.Advance(50); c.Update(clock.Local, 0.05);
            Assert.AreEqual(0, c.Faults.Count, "a new session starts without synthetic faults");
            Assert.IsNotNull(c.Hello);
        }
    }
}
