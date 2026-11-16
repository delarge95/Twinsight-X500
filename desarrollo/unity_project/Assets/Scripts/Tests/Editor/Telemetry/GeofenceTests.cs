// Lote 1a, tool 9: the geofence editor of the simulator (Telemetry/Sim/Geofence.cs + SimState.cfg) against contracts/golden/geofence.json, produced by the web's own
// src/shared/geofence.ts (tools/contracts/golden_geofence.ts): validateFence at its borders, fenceStatus / margins, the stored text and three scripts.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace WebGL.Telemetry.Tests
{
    using JObj = Dictionary<string, object>;

    public class GeofenceTests
    {
        static FenceConfig Cfg(JObj o) => new FenceConfig(C.N(o, "radius"), C.N(o, "ceiling"), C.N(o, "rtlAltitude"));

        [Test]
        public void Golden_Limits_AndDefaults_MatchTheWeb()
        {
            var g = C.Golden("geofence");
            var l = C.O(g, "limits");
            Assert.AreEqual(C.N(l, "radiusMin"), Geofence.RadiusMin);
            Assert.AreEqual(C.N(l, "radiusMax"), Geofence.RadiusMax);
            Assert.AreEqual(C.N(l, "radiusStep"), Geofence.RadiusStep);
            Assert.AreEqual(C.N(l, "ceilingMin"), Geofence.CeilingMin);
            Assert.AreEqual(C.N(l, "ceilingMax"), Geofence.CeilingMax);
            Assert.AreEqual(C.N(l, "ceilingStep"), Geofence.CeilingStep);
            Assert.AreEqual(C.N(l, "radiusMargin"), Geofence.RadiusMargin);
            Assert.AreEqual(C.N(l, "ceilingMargin"), Geofence.CeilingMargin);
            Assert.AreEqual(C.N(l, "rtlMin"), Geofence.RtlMin);
            Assert.AreEqual(C.N(l, "nearM"), Geofence.NearM);
            Assert.AreEqual(C.N(l, "hitM"), Geofence.HitM);
            var d = C.O(g, "defaults");
            var def = Geofence.Default();
            Assert.AreEqual(C.N(d, "radius"), def.radius);
            Assert.AreEqual(C.N(d, "ceiling"), def.ceiling);
            Assert.AreEqual(C.N(d, "rtlAltitude"), def.rtlAltitude);
        }

        [Test]
        public void Golden_Validate_AtItsBorders()
        {
            var g = C.Golden("geofence");
            var cs = new Cases("geofence.validate");
            foreach (JObj c in C.A(g, "validate"))
            {
                cs.Case();
                string name = C.S(c, "name");
                var st = C.O(c, "state");
                var p = C.V(st, "p");
                ConfigCheck r = Geofence.Validate(Cfg(C.O(c, "cfg")), p, C.B(st, "armed"), C.B(st, "landed"));
                cs.Eq(C.S(c, "out"), r.ok ? "ok" : r.code, name + " out");
                cs.Eq(C.IsNull(c, "detail") ? null : C.S(c, "detail"), r.ok ? null : r.detail, name + " detail");
                cs.Eq(C.IsNull(c, "min") ? (double?)null : C.N(c, "min"), r.ok ? null : r.min, name + " min");
                cs.Eq(C.IsNull(c, "max") ? (double?)null : C.N(c, "max"), r.ok ? null : r.max, name + " max");
            }
            cs.Done();
        }

        [Test]
        public void Golden_Status_AndMargins()
        {
            var g = C.Golden("geofence");
            double tol = C.N(C.O(g, "tolerance"), "margin");
            var cs = new Cases("geofence.status", tol);
            foreach (JObj c in C.A(g, "status"))
            {
                cs.Case();
                string name = C.S(c, "name");
                var f = C.O(c, "cfg");
                double[] p = C.V(c, "p");
                Geofence.Margins(C.N(f, "radius"), C.N(f, "ceiling"), p, out double h, out double v, out double min);
                cs.Near(C.N(c, "horizontal"), h, name + " horizontal");
                cs.Near(C.N(c, "vertical"), v, name + " vertical");
                cs.Near(C.N(c, "min"), min, name + " min");
                cs.Eq(C.S(c, "out"), Geofence.StatusName(Geofence.Status(C.N(f, "radius"), C.N(f, "ceiling"), p)), name + " status");
            }
            cs.Done();
        }

        [Test]
        public void Golden_Storage_ParsesWhatTheAppKeeps()
        {
            var g = C.Golden("geofence");
            var cs = new Cases("geofence.storage");
            foreach (JObj c in C.A(g, "storage"))
            {
                cs.Case();
                string name = C.S(c, "name");
                FenceConfig got = Geofence.Parse(C.IsNull(c, "raw") ? null : C.S(c, "raw"));
                FenceConfig exp = Cfg(C.O(c, "out"));
                cs.Eq(exp.radius, got.radius, name + " radius");
                cs.Eq(exp.ceiling, got.ceiling, name + " ceiling");
                cs.Eq(exp.rtlAltitude, got.rtlAltitude, name + " rtl");
            }
            cs.Done();
        }

        [Test]
        public void Storage_RoundTrips()
        {
            var c = new FenceConfig(80, 30, 10);
            FenceConfig back = Geofence.Parse(Geofence.ToJson(c));
            Assert.AreEqual(80, back.radius);
            Assert.AreEqual(30, back.ceiling);
            Assert.AreEqual(10, back.rtlAltitude);
        }

        [Test]
        public void Golden_Scenarios_MatchTheWebModel()
        {
            var g = C.Golden("geofence");
            var t = SimGolden.TolOf(g);
            var perScenario = new List<string>();
            foreach (JObj sc in C.A(g, "scenarios"))
            {
                string name = C.S(sc, "name");
                var cs = new Cases("geofence." + name, t.State);
                var s = FlightSim.Create(22);
                SimGolden.Play(cs, name, sc, s, t);
                var fin = C.O(sc, "final_cfg");
                cs.Case();
                cs.Eq(C.N(fin, "radius"), s.cfg.radius, name + " final radius");
                cs.Eq(C.N(fin, "ceiling"), s.cfg.ceiling, name + " final ceiling");
                cs.Eq(C.N(fin, "rtlAltitude"), s.cfg.rtlAltitude, name + " final rtl");
                try { cs.Done(); } catch (AssertionException) { perScenario.Add(name + " FAILED"); throw; }
                perScenario.Add(name + " " + Cases.Summary["geofence." + name]);
            }
            Debug.Log("[contract] geofence scenarios: " + string.Join(", ", perScenario));
        }

        [Test]
        public void SetConfig_InFlight_NeverTeleports()
        {
            var s = FlightSim.Create();
            FlightSim.Command(s, ClientMessage.Takeoff(10));
            for (int i = 0; i < 1000; i++) FlightSim.Step(s, 0.01);
            FlightSim.Command(s, ClientMessage.FlyTo(30, 40, 10));
            for (int i = 0; i < 2000; i++) FlightSim.Step(s, 0.01);
            double n = s.p[0], e = s.p[1];
            ConfigCheck r = FlightSim.SetConfig(s, new SimConfigPatch { radius = 50 });
            Assert.IsFalse(r.ok);
            Assert.AreEqual("radius_below_position", r.code);
            Assert.AreEqual(120, s.cfg.radius, "nothing changed");
            Assert.AreEqual(n, s.p[0]);
            Assert.AreEqual(e, s.p[1]);
            Assert.IsTrue(FlightSim.SetConfig(s, new SimConfigPatch { radius = 55 }).ok);
            Assert.AreEqual(55, s.cfg.radius);
        }

        [Test]
        public void Defaults_GiveTheModelOfBefore()
        {
            var s = FlightSim.Create();
            Assert.AreEqual(SimLimits.Radius, s.cfg.radius);
            Assert.AreEqual(SimLimits.Ceiling, s.cfg.ceiling);
            Assert.AreEqual(SimLimits.RtlAltitude, s.cfg.rtlAltitude);
        }
    }
}
