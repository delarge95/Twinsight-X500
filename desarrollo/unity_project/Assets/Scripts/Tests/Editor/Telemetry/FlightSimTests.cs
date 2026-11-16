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

        [Test]
        public void Golden_Scenarios_MatchTheWebModel()
        {
            var g = C.Golden("flightsim");
            var t = SimGolden.TolOf(g);
            var all = new Cases("flightsim", t.State);
            var perScenario = new List<string>();
            foreach (JObj sc in C.A(g, "scenarios"))
            {
                string name = C.S(sc, "name");
                if (name == "replies") continue; // TEMP(Lote 1a, tool 10): motor_off / restore are accepted from the motor-cut commit on
                var cs = new Cases("flightsim." + name, t.State);
                var s = FlightSim.Create(C.N(sc, "ambientC"));
                SimGolden.Play(cs, name, sc, s, t);
                try { cs.Done(); } catch (NUnit.Framework.AssertionException) { perScenario.Add(name + " FAILED"); throw; }
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
            CollectionAssert.AreEqual(C.A(g, "commands").Cast<string>().Take(10).ToArray(), FlightSim.Commands); // TEMP(Lote 1a, tool 10): + motor_off, restore
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
