// Lote 1a, tool 4: the simulated wind (Telemetry/Sim/Rng.cs, Wind.cs and the wind of FlightSim) against contracts/golden/wind.json, produced by the web's own
// src/shared/rng.ts + wind.ts (tools/contracts/golden_wind.ts): mulberry32 (exact) and Box-Muller (1e-12), the ranges, the stored text, the compass, the calm
// mission equal to flightsim.json, the hover in a 5 m/s wind (acceptance numbers), 60 s of gusts and the five scripts.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace WebGL.Telemetry.Tests
{
    using JObj = Dictionary<string, object>;

    public class WindTests
    {
        static WindConfig Wc(JObj o) => new WindConfig { speed = C.N(o, "speed"), fromDeg = C.N(o, "fromDeg"), gustSigma = C.N(o, "gustSigma"), gustTau = C.N(o, "gustTau"), seed = C.N(o, "seed") };
        static SimConfig CfgWith(WindConfig w) { var c = new SimConfig(); c.wind = w.Clone(); return c; }

        [Test]
        public void Golden_Rng_Mulberry32_IsExact_AndBoxMullerWithinTolerance()
        {
            var g = C.Golden("wind");
            var rng = C.O(g, "rng");
            double seed = C.N(rng, "seed");
            double[] uniform = C.V(rng, "uniform"), normal = C.V(rng, "normal");
            var r = new Rng(seed);
            for (int i = 0; i < uniform.Length; i++) Assert.AreEqual(uniform[i], r.Next(), "uniform " + i); // tolerance.rng = 0: the same 53-bit double
            var n = new Rng(seed);
            double tol = C.N(C.O(g, "tolerance"), "normal");
            for (int i = 0; i < normal.Length; i++) Assert.AreEqual(normal[i], n.Normal(), tol, "normal " + i);
        }

        [Test]
        public void Rng_SeedBits_AreTheSameAsTheWeb()
        {
            // seed | 0 in JS keeps only the 32 low bits: 4294967295 is -1 there and 0xFFFFFFFF here
            Assert.AreEqual(0xFFFFFFFFu, new Rng(4294967295).state);
            Assert.AreEqual(42u, new Rng(42).state);
            // two uniforms per normal, always
            var a = new Rng(7); var b = new Rng(7);
            a.Normal();
            b.Next(); b.Next();
            Assert.AreEqual(b.Next(), a.Next());
        }

        [Test]
        public void Golden_Defaults_AndLimits()
        {
            var g = C.Golden("wind");
            var d = Wc(C.O(g, "defaults"));
            Assert.AreEqual(0, d.speed); Assert.AreEqual(0, d.fromDeg); Assert.AreEqual(0, d.gustSigma); Assert.AreEqual(2, d.gustTau); Assert.AreEqual(1, d.seed);
            var l = C.O(g, "limits");
            Assert.AreEqual(C.N(l, "speedMax"), Wind.SpeedMax);
            Assert.AreEqual(C.N(l, "fromMax"), Wind.FromMax);
            Assert.AreEqual(C.N(l, "gustMax"), Wind.GustMax);
            Assert.AreEqual(C.N(l, "tauMin"), Wind.TauMin);
            Assert.AreEqual(C.N(l, "tauMax"), Wind.TauMax);
            Assert.AreEqual(C.N(l, "seedMax"), Wind.SeedMax);
            Assert.AreEqual(C.N(l, "push"), Wind.Push);
        }

        [Test]
        public void Golden_Validate_AtItsBorders()
        {
            var g = C.Golden("wind");
            var cs = new Cases("wind.validate");
            foreach (JObj c in C.A(g, "validate"))
            {
                cs.Case();
                string name = C.S(c, "name");
                ConfigCheck r = Wind.Validate(Wc(C.O(c, "wind")));
                cs.Eq(C.S(c, "out"), r.ok ? "ok" : r.code, name + " out");
                cs.Eq(C.IsNull(c, "detail") ? null : C.S(c, "detail"), r.ok ? null : r.detail, name + " detail");
                cs.Eq(C.IsNull(c, "min") ? (double?)null : C.N(c, "min"), r.ok ? null : r.min, name + " min");
                cs.Eq(C.IsNull(c, "max") ? (double?)null : C.N(c, "max"), r.ok ? null : r.max, name + " max");
            }
            cs.Done();
        }

        [Test]
        public void Golden_Storage_AndCompass()
        {
            var g = C.Golden("wind");
            var cs = new Cases("wind.storage");
            foreach (JObj c in C.A(g, "storage"))
            {
                cs.Case();
                string name = C.S(c, "name");
                WindConfig got = Wind.Parse(C.IsNull(c, "raw") ? null : C.S(c, "raw"));
                WindConfig exp = Wc(C.O(c, "out"));
                cs.Eq(exp.speed, got.speed, name + " speed"); cs.Eq(exp.fromDeg, got.fromDeg, name + " fromDeg"); cs.Eq(exp.gustSigma, got.gustSigma, name + " gustSigma");
                cs.Eq(exp.gustTau, got.gustTau, name + " gustTau"); cs.Eq(exp.seed, got.seed, name + " seed");
            }
            cs.Done();
            var cc = new Cases("wind.compass");
            foreach (JObj c in C.A(g, "compass")) { cc.Case(); cc.Eq(C.I(c, "out"), Wind.CompassIndex(C.N(c, "deg")), "compass " + C.N(c, "deg")); }
            cc.Done();
            Assert.AreEqual(0.0, Wind.Parse(Wind.ToJson(new WindConfig { speed = 6, fromDeg = 315, gustSigma = 2, gustTau = 2, seed = 123456 })).speed - 6);
        }

        [Test]
        public void Golden_CalmMission_IsExactlyTheMissionOfFlightsim()
        {
            var g = C.Golden("wind");
            var fs = C.Golden("flightsim");
            JObj calm = C.A(g, "scenarios").Cast<JObj>().First(x => C.S(x, "name") == "calm_mission");
            JObj mission = C.A(fs, "scenarios").Cast<JObj>().First(x => C.S(x, "name") == "mission");
            // the web script's own claim: the rows are those of flightsim.json (compared as text, and re-run here with the wind configured but at zero)
            Assert.AreEqual(C.A(mission, "expect").Count, C.A(calm, "expect").Count);
            for (int i = 0; i < C.A(calm, "expect").Count; i++)
                if (C.A(calm, "expect")[i] is string a) Assert.AreEqual((string)C.A(mission, "expect")[i], a, "row " + i);
            var t = SimGolden.TolOf(g);
            var cs = new Cases("wind.calm", t.State);
            SimGolden.Play(cs, "calm_mission", calm, FlightSim.Create(22, CfgWith(Wc(C.O(calm, "wind")))), t);
            cs.Done();
        }

        static double Deg(double rad) => rad * 180 / Math.PI;

        /// <summary>The web's hold(): take off to 5 m, 10 s, then 40 s more of hover (dt 0.01).</summary>
        static SimState Hover(WindConfig w)
        {
            var s = FlightSim.Create(22, CfgWith(w));
            FlightSim.Command(s, ClientMessage.Takeoff(5));
            for (int i = 0; i < 1000; i++) FlightSim.Step(s, 0.01);
            for (int i = 0; i < 4000; i++) FlightSim.Step(s, 0.01);
            return s;
        }

        [Test]
        public void Golden_Hold_In5MsFromTheNorth_MeetsTheAcceptanceTargets()
        {
            var g = C.Golden("wind");
            var hold = C.O(g, "hold");
            var calmExp = C.O(hold, "calm");
            var northExp = C.O(hold, "north_5");
            var tg = C.O(hold, "targets");
            SimState calm = Hover(new WindConfig());
            SimState north = Hover(new WindConfig { speed = 5, fromDeg = 0 });
            // the numbers the model gives (the web rounds to 6 decimals)
            Assert.AreEqual(C.N(northExp, "south_m"), -north.p[0], 1e-6, "south m");
            Assert.AreEqual(C.N(northExp, "west_m"), -north.p[1], 1e-6, "west m");
            Assert.AreEqual(C.N(northExp, "pitch_deg"), Deg(north.pitch), 1e-5, "pitch deg");
            Assert.AreEqual(C.N(northExp, "roll_deg"), Deg(north.roll), 1e-5, "roll deg");
            Assert.AreEqual(C.N(northExp, "amps"), north.amps, 1e-5, "amps");
            Assert.AreEqual(C.N(calmExp, "amps"), calm.amps, 1e-5, "calm amps");
            // the acceptance targets of the specification (§4): deviation 0.62 +- 0.05 m downwind, nose into the wind 14 +- 1.5 deg, more current than without wind
            var south = C.O(tg, "south_m"); var pitch = C.O(tg, "pitch_deg_magnitude");
            Assert.AreEqual(C.N(south, "value"), -north.p[0], C.N(south, "tol"));
            Assert.AreEqual(C.N(pitch, "value"), Math.Abs(Deg(north.pitch)), C.N(pitch, "tol"));
            Assert.Less(north.pitch, 0, "nose down = into the wind (pitch negative)");
            Assert.Greater(north.amps, calm.amps);
            Debug.Log("[contract] wind hold: " + (-north.p[0]).ToString("F3") + " m south, pitch " + Deg(north.pitch).ToString("F2") + " deg, " + north.amps.ToString("F2") + " A (calm " + calm.amps.ToString("F2") + " A)");
        }

        [Test]
        public void Golden_Gusts_60s_WithAFixedSeed()
        {
            var g = C.Golden("wind");
            var gu = C.O(g, "gusts");
            double tol = C.N(C.O(g, "tolerance"), "gust");
            var s = FlightSim.Create(22, CfgWith(Wc(C.O(gu, "wind"))));
            FlightSim.Command(s, ClientMessage.Takeoff(5));
            var rows = C.A(gu, "rows");
            var cs = new Cases("wind.gusts", tol);
            int r = 0;
            for (int i = 1; i <= 6000; i++)
            {
                FlightSim.Step(s, 0.01);
                if (i % 100 != 0) continue;
                var row = (JObj)rows[r++];
                cs.Case();
                string at = "t=" + C.N(row, "t");
                cs.Near(C.N(row, "t"), s.t, at + " t");
                cs.Near(C.N(row, "gn"), s.wind.gn, at + " gn");
                cs.Near(C.N(row, "ge"), s.wind.ge, at + " ge");
                cs.Near(C.V(row, "p"), s.p, at + " p");
                cs.Near(C.V(row, "v"), s.v, at + " v");
                cs.Near(C.N(row, "pitch"), s.pitch, at + " pitch");
                cs.Near(C.N(row, "roll"), s.roll, at + " roll");
                cs.Near(C.N(row, "amps"), s.amps, at + " amps");
            }
            Assert.AreEqual(rows.Count, r, "every record was compared");
            cs.Done();
        }

        [Test]
        public void Golden_Scenarios_MatchTheWebModel()
        {
            var g = C.Golden("wind");
            var t = SimGolden.TolOf(g);
            var perScenario = new List<string>();
            foreach (JObj sc in C.A(g, "scenarios"))
            {
                string name = C.S(sc, "name");
                var cs = new Cases("wind." + name, t.State);
                var s = FlightSim.Create(22, CfgWith(Wc(C.O(sc, "wind"))));
                SimGolden.Play(cs, name, sc, s, t);
                cs.Case();
                var fw = C.O(sc, "final_cfg_wind");
                cs.Eq(C.N(fw, "speed"), s.cfg.wind.speed, name + " final speed"); cs.Eq(C.N(fw, "seed"), s.cfg.wind.seed, name + " final seed");
                cs.Eq(C.N(fw, "gustTau"), s.cfg.wind.gustTau, name + " final tau"); cs.Eq(C.N(fw, "gustSigma"), s.cfg.wind.gustSigma, name + " final sigma");
                cs.Near(C.V(sc, "final_gust"), new[] { s.wind.gn, s.wind.ge }, name + " final gust", 1e-9);
                FlightSim.WindNow(s, out double wn, out double we);
                cs.Near(C.V(sc, "final_wind_ned"), new[] { wn, we }, name + " final wind", 1e-9);
                try { cs.Done(); } catch (AssertionException) { perScenario.Add(name + " FAILED"); throw; }
                perScenario.Add(name + " " + Cases.Summary["wind." + name]);
            }
            Debug.Log("[contract] wind scenarios: " + string.Join(", ", perScenario));
        }

        [Test]
        public void TheSameSeed_GivesTheSameTrajectory()
        {
            SimState Fly(double seed)
            {
                var s = FlightSim.Create(22, CfgWith(new WindConfig { speed = 4, fromDeg = 45, gustSigma = 2, gustTau = 2, seed = seed }));
                FlightSim.Command(s, ClientMessage.Takeoff(6));
                for (int i = 0; i < 2000; i++) FlightSim.Step(s, 0.01);
                return s;
            }
            SimState a = Fly(99), b = Fly(99), c = Fly(100);
            CollectionAssert.AreEqual(a.p, b.p);
            CollectionAssert.AreEqual(a.v, b.v);
            Assert.AreNotEqual(a.p[0], c.p[0], "another seed, another gusts");
        }

        [Test]
        public void WindActsOnlyInFlight_AndANewSeedRestartsTheGusts()
        {
            var s = FlightSim.Create(22, CfgWith(new WindConfig { speed = 8, fromDeg = 90, gustSigma = 2, gustTau = 2, seed = 5 }));
            for (int i = 0; i < 500; i++) FlightSim.Step(s, 0.01);
            Assert.AreEqual(0, s.p[0], 0); Assert.AreEqual(0, s.p[1], 0); Assert.IsTrue(s.landed, "on the ground the wind moves nothing");
            Assert.AreNotEqual(0, s.wind.gn, "...but the gusts live on");
            Assert.IsTrue(FlightSim.SetConfig(s, new SimConfigPatch { wind = new WindPatch { speed = 3 } }).ok);
            Assert.AreNotEqual(0, s.wind.gn, "a change other than the seed keeps the gusts");
            Assert.IsTrue(FlightSim.SetConfig(s, new SimConfigPatch { wind = new WindPatch { seed = 6 } }).ok);
            Assert.AreEqual(0, s.wind.gn, "a new seed restarts the generator from calm");
        }
    }
}
