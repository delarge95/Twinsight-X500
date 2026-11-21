// PLAN_PC 42 (C): «Geocerca» and «Viento» are on/off switches like «Suelo», «Escenario» and «Batería». Pure model checks (FlightSim); the strip, the blue
// «on» state and the gear that opens the fine settings are checked in the player by `-fix2-capture`.
using NUnit.Framework;

namespace WebGL.Telemetry.Tests
{
    public class SimSwitchesTests
    {
        static SimState Flying(SimConfig cfg = null)
        {
            var s = FlightSim.Create(22, cfg);
            FlightSim.Command(s, ClientMessage.Takeoff(10));
            for (int i = 0; i < 1000; i++) FlightSim.Step(s, 0.01);
            return s;
        }

        [Test]
        public void The_Fence_Is_On_By_Default_And_Its_Envelope_Is_The_Configured_One()
        {
            var c = new SimConfig();
            Assert.IsTrue(c.fenceOn);
            Assert.AreEqual(c.radius, c.FenceRadius);
            Assert.AreEqual(c.ceiling, c.FenceCeiling);
            Assert.IsTrue(c.Clone().fenceOn);
        }

        [Test]
        public void Fence_Off_Lets_The_Drone_Fly_Past_The_Radius_Up_To_The_Widest_Envelope()
        {
            var s = Flying();
            Assert.IsTrue(FlightSim.SetConfig(s, new SimConfigPatch { radius = 50 }).ok);
            FlightSim.Command(s, ClientMessage.FlyTo(80, 0, 10));
            for (int i = 0; i < 3000; i++) FlightSim.Step(s, 0.01);
            Assert.LessOrEqual(s.p[0], 50.01, "on: clamped to the fence");
            Assert.IsTrue(FlightSim.SetConfig(s, new SimConfigPatch { fenceOn = false }).ok);
            FlightSim.Command(s, ClientMessage.FlyTo(80, 0, 10));
            for (int i = 0; i < 3000; i++) FlightSim.Step(s, 0.01);
            Assert.Greater(s.p[0], 60, "off: the 50 m fence no longer acts");
            Assert.LessOrEqual(s.p[0], Geofence.RadiusMax + 0.01);
        }

        [Test]
        public void Turning_The_Fence_On_Outside_It_In_Flight_Is_Refused_With_Its_Reason()
        {
            var s = Flying(new SimConfig { radius = 50, fenceOn = false });
            FlightSim.Command(s, ClientMessage.FlyTo(80, 0, 10));
            for (int i = 0; i < 3000; i++) FlightSim.Step(s, 0.01);
            ConfigCheck r = FlightSim.SetConfig(s, new SimConfigPatch { fenceOn = true });
            Assert.IsFalse(r.ok);
            Assert.AreEqual("radius_below_position", r.code);
            Assert.IsFalse(s.cfg.fenceOn, "nothing changes on a refusal");
            // with the fence off, another change (wind) is not refused because of the stale radius
            Assert.IsTrue(FlightSim.SetConfig(s, new SimConfigPatch { wind = new WindPatch { speed = 3 } }).ok);
        }

#if UNITY_STANDALONE
        [Test]
        public void Wind_Off_Is_Calm_And_Keeps_The_Seed_And_Direction()
        {
            var stored = new WindConfig { speed = 6, fromDeg = 90, gustSigma = 1.5, seed = 7 };
            WindConfig off = SimSettings.Effective(stored, false);
            Assert.AreEqual(0, off.speed);
            Assert.AreEqual(0, off.gustSigma);
            Assert.AreEqual(90, off.fromDeg);
            Assert.AreEqual(7, off.seed);
            WindConfig on = SimSettings.Effective(stored, true);
            Assert.AreEqual(6, on.speed);
            Assert.AreEqual(1.5, on.gustSigma);
            Assert.AreEqual(6, stored.speed, "the stored values are not touched");
        }
#endif
    }
}
