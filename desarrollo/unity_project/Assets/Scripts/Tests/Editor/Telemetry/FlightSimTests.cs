// E-5: the C# flight simulator (Telemetry/Sim) against contracts/golden/flightsim.json, produced by the web's own src/shared/flightsim.ts
// (tools/contracts/golden_flightsim.ts), plus the source (SimTelemetrySource): hello, acks, events, frames, never SITL.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace WebGL.Telemetry.Tests
{
    using JObj = Dictionary<string, object>;

    public class FlightSimTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static CommandMessage Cmd(JObj m)
        {
            var c = new CommandMessage { name = (string)m["name"] };
            if (m.ContainsKey("altitude")) c.altitude = C.N(m, "altitude");
            if (m.ContainsKey("north")) c.north = C.N(m, "north");
            if (m.ContainsKey("east")) c.east = C.N(m, "east");
            if (m.ContainsKey("degrees")) c.degrees = C.N(m, "degrees");
            if (m.ContainsKey("radius")) c.radius = C.N(m, "radius");
            if (m.ContainsKey("motor")) c.motor = (int)C.N(m, "motor");
            return c;
        }

        static string ModeName(SimMode m)
        {
            switch (m)
            {
                case SimMode.Idle: return "idle";
                case SimMode.Takeoff: return "takeoff";
                case SimMode.Hold: return "hold";
                case SimMode.Goto: return "goto";
                case SimMode.Manual: return "manual";
                case SimMode.Land: return "land";
                case SimMode.Rtl: return "rtl";
                default: return "orbit";
            }
        }

        static string PhaseName(RtlPhase p) => p == RtlPhase.Climb ? "climb" : p == RtlPhase.Return ? "return" : "land";

        /// <summary>The state numbers in the column order of the golden (null = "-").</summary>
        static double?[] StateNumbers(SimState s)
        {
            double? Fin(double x) => double.IsInfinity(x) || double.IsNaN(x) ? (double?)null : x;
            return new double?[]
            {
                s.t, s.p[0], s.p[1], s.p[2], s.v[0], s.v[1], s.v[2], s.yaw, s.roll, s.pitch, s.yawRate,
                s.target.n, s.target.e, s.target.alt, s.yawTarget,
                s.orbit?.cn, s.orbit?.ce, s.orbit?.r,
                s.manual.x, s.manual.y, s.manual.z, s.manual.r, Fin(s.manualAge), s.rem, s.amps,
                s.u[0], s.u[1], s.u[2], s.u[3], s.tempMotor[0], s.tempMotor[1], s.tempMotor[2], s.tempMotor[3], s.tempBattery, s.ambientC,
            };
        }

        static double?[] FrameNumbers(TelemetryFrame f)
        {
            var l = new List<double?> { f.ts };
            l.AddRange(f.q.Select(x => (double?)x));
            l.AddRange(f.p.Select(x => (double?)x));
            l.AddRange(f.v.Select(x => (double?)x));
            l.AddRange(f.u.Select(x => (double?)x));
            l.AddRange(f.rpm.Select(x => (double?)x));
            l.Add(f.bat.v); l.Add(f.bat.i); l.Add(f.bat.rem);
            l.AddRange(f.temp.Select(x => (double?)x));
            l.Add(f.vib); l.Add(f.armed ? 1 : 0); l.Add(f.landed ? 1 : 0); l.Add(f.nav);
            return l.ToArray();
        }

        static void CheckRow(Cases cs, string expected, SimState s, string label, double tolState, double tolFrame, double tolRpm, string[] stateCols, string[] frameCols)
        {
            string[] tok = expected.Split(' ');
            cs.Eq(tok[0], ModeName(s.mode), label + " mode");
            cs.Eq(tok[1], PhaseName(s.rtlPhase), label + " rtlPhase");
            cs.Eq(tok[2], s.armed ? "1" : "0", label + " armed");
            cs.Eq(tok[3], s.landed ? "1" : "0", label + " landed");
            cs.Eq(tok[4], s.pilot ? "1" : "0", label + " pilot");
            double?[] st = StateNumbers(s);
            Assert.AreEqual(stateCols.Length, st.Length, "state columns");
            for (int i = 0; i < st.Length; i++)
            {
                string e = tok[5 + i];
                if (e == "-") { cs.True(!st[i].HasValue, label + " " + stateCols[i] + " expected null, got " + st[i]); continue; }
                if (!st[i].HasValue) { cs.True(false, label + " " + stateCols[i] + " expected " + e + ", got null"); continue; }
                cs.Near(double.Parse(e, Inv), st[i].Value, label + " " + stateCols[i], tolState);
            }
            double?[] fr = FrameNumbers(FlightSim.Frame(s, Math.Round(s.t * 1000)));
            Assert.AreEqual(frameCols.Length, fr.Length, "frame columns");
            for (int i = 0; i < fr.Length; i++)
            {
                string e = tok[5 + st.Length + i];
                double tol = frameCols[i].StartsWith("rpm", StringComparison.Ordinal) ? tolRpm : tolFrame;
                if (e == "-") { cs.True(!fr[i].HasValue, label + " frame." + frameCols[i] + " expected null"); continue; }
                cs.Near(double.Parse(e, Inv), fr[i] ?? double.NaN, label + " frame." + frameCols[i], tol);
            }
        }

        [Test]
        public void Golden_Scenarios_MatchTheWebModel()
        {
            var g = C.Golden("flightsim");
            var tol = C.O(g, "tolerance");
            double tolState = C.N(tol, "state"), tolFrame = C.N(tol, "frame"), tolRpm = C.N(tol, "rpm");
            string[] stateCols = C.A(g, "state_columns").Cast<string>().ToArray();
            string[] frameCols = C.A(g, "frame_columns").Cast<string>().ToArray();
            var all = new Cases("flightsim", tolState);
            var perScenario = new List<string>();
            foreach (JObj sc in C.A(g, "scenarios"))
            {
                string name = C.S(sc, "name");
                var cs = new Cases("flightsim." + name, tolState);
                cs.Case();
                var s = FlightSim.Create(C.N(sc, "ambientC"));
                var expect = C.A(sc, "expect");
                int e = 0;
                cs.Case();
                CheckRow(cs, (string)expect[e++], s, name + "#0", tolState, tolFrame, tolRpm, stateCols, frameCols);
                foreach (JObj op in C.A(sc, "ops"))
                {
                    string kind = C.S(op, "op");
                    if (kind == "command")
                    {
                        var msg = C.O(op, "msg");
                        SimReply r = FlightSim.Command(s, Cmd(msg));
                        var ex = (JObj)expect[e++];
                        cs.Case();
                        cs.Eq(C.S(ex, "reply"), C.S(msg, "name"), name + " reply name");
                        cs.Eq(C.B(ex, "ok"), r.ok, name + " " + C.S(msg, "name") + " ok");
                        cs.Eq(C.IsNull(ex, "detail") ? null : C.S(ex, "detail"), r.detail, name + " " + C.S(msg, "name") + " detail");
                        cs.Eq(C.IsNull(ex, "event") ? null : C.S(ex, "event"), r.evt, name + " " + C.S(msg, "name") + " event");
                    }
                    else if (kind == "manual")
                    {
                        var st = C.O(op, "sticks");
                        FlightSim.Manual(s, new SimSticks(C.N(st, "x"), C.N(st, "y"), C.N(st, "z"), C.N(st, "r")));
                        cs.Case();
                        CheckRow(cs, (string)expect[e++], s, name + " manual", tolState, tolFrame, tolRpm, stateCols, frameCols);
                    }
                    else
                    {
                        double dt = C.N(op, "dt");
                        int steps = (int)Math.Round(C.N(op, "seconds") / dt);
                        int every = Math.Max(1, (int)Math.Round(C.N(op, "every") / dt));
                        SimSticks? sticks = null;
                        if (op.ContainsKey("sticks")) { var st = C.O(op, "sticks"); sticks = new SimSticks(C.N(st, "x"), C.N(st, "y"), C.N(st, "z"), C.N(st, "r")); }
                        for (int i = 1; i <= steps; i++)
                        {
                            if (sticks.HasValue) FlightSim.Manual(s, sticks.Value);
                            FlightSim.Step(s, dt);
                            if (i % every == 0 || i == steps)
                            {
                                cs.Case();
                                CheckRow(cs, (string)expect[e++], s, name + " t=" + s.t.ToString("0.###", Inv), tolState, tolFrame, tolRpm, stateCols, frameCols);
                            }
                        }
                    }
                }
                Assert.AreEqual(expect.Count, e, name + ": every expected record was consumed");
                try { cs.Done(); } catch (AssertionException) { perScenario.Add(name + " FAILED"); throw; }
                perScenario.Add(name + " " + Cases.Summary["flightsim." + name]);
            }
            all.Case();
            all.Done();
            Debug.Log("[contract] flightsim scenarios: " + string.Join(", ", perScenario));
        }


        [Test]
        public void Limits_AndCommandList_MatchTheWeb()
        {
            var g = C.Golden("flightsim");
            var l = C.O(g, "limits");
            Assert.AreEqual(C.N(l, "maxSpeed"), SimLimits.MaxSpeed);
            Assert.AreEqual(C.N(l, "maxClimb"), SimLimits.MaxClimb);
            Assert.AreEqual(C.N(l, "maxDescent"), SimLimits.MaxDescent);
            Assert.AreEqual(C.N(l, "landSpeed"), SimLimits.LandSpeed);
            Assert.AreEqual(C.N(l, "maxYawRate"), SimLimits.MaxYawRate);
            Assert.AreEqual(C.N(l, "velocityTau"), SimLimits.VelocityTau);
            Assert.AreEqual(C.N(l, "maxAccel"), SimLimits.MaxAccel);
            Assert.AreEqual(C.N(l, "tiltTau"), SimLimits.TiltTau);
            Assert.AreEqual(C.N(l, "dragTilt"), SimLimits.DragTilt);
            Assert.AreEqual(C.N(l, "maxTiltDeg"), SimLimits.MaxTiltDeg);
            Assert.AreEqual(120, C.N(l, "radius")); Assert.AreEqual(120, SimLimits.Radius);
            Assert.AreEqual(60, C.N(l, "ceiling")); Assert.AreEqual(60, SimLimits.Ceiling);
            Assert.AreEqual(C.N(l, "rtlAltitude"), SimLimits.RtlAltitude);
            Assert.AreEqual(C.N(l, "orbitSpeed"), SimLimits.OrbitSpeed);
            Assert.AreEqual(0.6, C.N(l, "manualTimeoutS")); Assert.AreEqual(0.6, SimLimits.ManualTimeoutS);
            Assert.AreEqual(C.N(l, "hoverThrottle"), SimLimits.HoverThrottle);
            Assert.AreEqual(C.N(l, "capacityAh"), SimLimits.CapacityAh);
            Assert.AreEqual(C.N(l, "hoverAmps"), SimLimits.HoverAmps);
            Assert.AreEqual(C.N(l, "avionicsAmps"), SimLimits.AvionicsAmps);
            Assert.AreEqual(C.N(l, "motorKv"), SimLimits.MotorKv);
            CollectionAssert.AreEqual(C.A(g, "commands").Cast<string>().ToArray(), FlightSim.Commands);
        }

        [Test]
        public void Takeoff_WithAFlatBattery_GetsANewOne()
        {
            var s = FlightSim.Create();
            s.rem = 0.1;
            SimReply r = FlightSim.Command(s, ClientMessage.Takeoff(5));
            Assert.IsTrue(r.ok);
            Assert.AreEqual("Despegue a 5 m · batería nueva", r.evt);
            Assert.AreEqual(1, s.rem);
        }

        [Test]
        public void FlyTo_WithANonFinitePoint_IsRefused()
        {
            var s = FlightSim.Create();
            FlightSim.Command(s, ClientMessage.Takeoff(5));
            for (int i = 0; i < 1000; i++) FlightSim.Step(s, 0.01);
            SimReply r = FlightSim.Command(s, ClientMessage.FlyTo(double.PositiveInfinity, 0, 5));
            Assert.IsFalse(r.ok);
            Assert.AreEqual("punto no válido", r.detail);
        }

        [Test]
        public void Step_ClampsALongStep_AndIgnoresAZeroOne()
        {
            var s = FlightSim.Create();
            FlightSim.Step(s, 0);
            Assert.AreEqual(0, s.t);
            FlightSim.Step(s, 10);
            Assert.AreEqual(0.05, s.t, 1e-12);
            FlightSim.Step(s, -1);
            Assert.AreEqual(0.05, s.t, 1e-12);
        }

        // ───────────────────────── the source ─────────────────────────

        sealed class Rig
        {
            public double Local = 1000, Wall = 1.8e12;
            public SimTelemetrySource Src;
            public TelemetryClient Client;

            public Rig()
            {
                Src = new SimTelemetrySource { LocalNowMs = () => Local, WallNowMs = () => Wall };
                Client = new TelemetryClient(Src) { Log = null };
                Client.Start();
            }

            public void Run(double seconds, double hz = 60)
            {
                int n = (int)Math.Round(seconds * hz);
                for (int i = 0; i < n; i++)
                {
                    Local += 1000.0 / hz;
                    Wall += 1000.0 / hz;
                    Client.Update(Local, 1.0 / hz);
                }
            }

            public bool Send(ClientMessage m) { bool ok = Client.Send(m, out _); Client.Update(Local, 0); return ok; }
        }

        [Test]
        public void Source_Hello_IsTheSimulatorNeverSitl()
        {
            var r = new Rig();
            r.Run(0.1);
            var h = r.Client.Hello;
            Assert.IsNotNull(h);
            Assert.AreEqual(TelemetryMode.Sitl, h.mode, "wire mode sitl enables the commands");
            StringAssert.DoesNotContain("SITL", h.source.label.ToUpperInvariant());
            StringAssert.Contains("no es PX4", h.source.label);
            foreach (string c in new[] { "takeoff", "land", "hold", "rtl", "fly_to", "orbit", "yaw", "set_altitude", "pilot" }) Assert.IsTrue(h.Accepts(c), c);
            Assert.IsFalse(h.Accepts("motor_off"));
            Assert.IsFalse(h.Accepts("inject"));
            Assert.IsTrue(r.Client.HasFrame);
            Assert.AreEqual(LinkState.Live, r.Client.State);
            var labels = HonestyLabels.Parse(C.Text("labels.json"));
            Assert.IsTrue(labels.TrySimText("title", "es", out string title));
            Assert.AreEqual("Simulador de vuelo", title);
            Assert.IsTrue(labels.TrySimText("detail", "es", out string detail));
            StringAssert.Contains("No es PX4 ni un dron real", detail);
        }

        [Test]
        public void Source_TakeoffFlyToOrbitRtlLand_ThroughTheClient()
        {
            var r = new Rig();
            r.Run(0.2);
            Assert.IsTrue(r.Send(ClientMessage.Takeoff(6)));
            Assert.IsTrue(r.Client.LastAck.ok);
            Assert.AreEqual("takeoff", r.Client.LastAck.command);
            r.Run(10);
            Assert.AreEqual(6, -r.Client.Displayed.p[2], 0.2);
            Assert.IsTrue(r.Client.Displayed.armed);
            Assert.IsTrue(r.Send(ClientMessage.FlyTo(20, -10, 8)));
            r.Run(15);
            Assert.AreEqual(20, r.Client.Displayed.p[0], 0.5);
            Assert.AreEqual(-10, r.Client.Displayed.p[1], 0.5);
            Assert.IsTrue(r.Send(ClientMessage.Orbit(8)));
            r.Run(10);
            Assert.AreEqual(21, r.Client.Displayed.nav);
            Assert.IsTrue(r.Send(ClientMessage.Rtl()));
            r.Run(45);
            Assert.IsTrue(r.Client.Displayed.landed, "rtl lands at home");
            Assert.IsTrue(r.Client.Events.Count >= 3, "the events of the commands reach the event list");
        }

        [Test]
        public void Source_RejectsFaultInjection_AndCommandsOnTheGround()
        {
            var r = new Rig();
            r.Run(0.2);
            Assert.IsFalse(r.Send(ClientMessage.Inject(FaultKind.Vibration)), "the client refuses a command the hello does not list");
            Assert.IsFalse(r.Send(ClientMessage.MotorOff(1)));
            Assert.IsTrue(r.Send(ClientMessage.Land()));
            Assert.IsFalse(r.Client.LastAck.ok);
            StringAssert.Contains("en tierra", r.Client.LastAck.detail);
        }

        [Test]
        public void Source_DeadMan_HoldsWhenTheSticksGoQuiet()
        {
            var r = new Rig();
            r.Run(0.2);
            r.Send(ClientMessage.Pilot());
            r.Send(ClientMessage.Takeoff(4));
            for (int i = 0; i < 60 * 6; i++) { r.Send(ClientMessage.Manual(0, 0, 500, 0)); r.Run(1.0 / 60); }
            for (int i = 0; i < 60 * 2; i++) { r.Send(ClientMessage.Manual(1000, 0, 500, 0)); r.Run(1.0 / 60); }
            Assert.AreEqual(SimMode.Manual, r.Src.Model.mode);
            Assert.Greater(r.Src.Model.v[0], 3);
            r.Run(0.7);
            Assert.AreEqual(SimMode.Hold, r.Src.Model.mode, "0.6 s without stick samples = hold");
            r.Run(5);
            Assert.Less(Math.Abs(r.Src.Model.v[0]), 0.2);
        }

        [Test]
        public void Source_Stop_ThenStart_StartsAFreshFlight()
        {
            var r = new Rig();
            r.Run(0.2);
            r.Send(ClientMessage.Takeoff(5));
            r.Run(3);
            Assert.IsTrue(r.Src.Model.armed);
            r.Src.Stop();
            Assert.AreEqual(TransportState.Idle, r.Src.State);
            r.Src.Start();
            Assert.IsFalse(r.Src.Model.armed);
            Assert.IsTrue(r.Src.Model.landed);
            Assert.AreEqual(1, r.Src.Model.rem);
        }
    }
}
