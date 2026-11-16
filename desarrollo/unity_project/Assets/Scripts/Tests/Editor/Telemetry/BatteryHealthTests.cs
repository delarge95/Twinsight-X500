// Lote 1b, tool 8: battery health (Telemetry/Logic/BatteryHealth.cs) against contracts/golden/battery.json, produced by the web's own src/shared/batteryhealth.ts
// (tools/contracts/golden_battery.ts): the estimator on synthetic series, the slope and the time left, 600 s of the simulator with load steps (R-hat = 0.012 ohm
// within 0.001), the 300 real samples of the TWF (value pinned by the golden) and the origin of each value.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace WebGL.Telemetry.Tests
{
    using JObj = Dictionary<string, object>;
    using JArr = List<object>;

    public class BatteryHealthTests
    {
        const double Dt = 1.0 / BatteryHealth.SampleHz;

        static double Round(double x, int n) { double k = Math.Pow(10, n); return JsMath.Round(x * k) / k; }

        static void NearOrNull(Cases cs, JObj e, string key, double? actual, string what, double tol)
        {
            if (C.IsNull(e, key)) { cs.True(!actual.HasValue, what + " " + key + ": expected none, got " + actual); return; }
            if (!actual.HasValue) { cs.True(false, what + " " + key + ": expected " + C.N(e, key) + ", got none"); return; }
            cs.Near(C.N(e, key), actual.Value, what + " " + key, tol);
        }

        /// <summary>The numbers of a report against the golden's view (seconds are kept with 6 decimals there).</summary>
        static void CompareReport(Cases cs, JObj e, BatteryReport h, string what, double tol)
        {
            NearOrNull(cs, e, "r_hat", h.rHat, what, tol);
            NearOrNull(cs, e, "r_cell", h.rCell, what, tol);
            cs.Eq(C.I(e, "quality"), h.quality, what + " quality");
            NearOrNull(cs, e, "drop_v", h.dropV, what, tol);
            NearOrNull(cs, e, "rest_v", h.restV, what, tol);
            NearOrNull(cs, e, "rest_cell", h.restCell, what, tol);
            NearOrNull(cs, e, "slope", h.slope, what, tol);
            NearOrNull(cs, e, "by_slope_s", h.bySlopeS, what, SecondsTol(e, "by_slope_s", tol));
            NearOrNull(cs, e, "by_current_s", h.byCurrentS, what, SecondsTol(e, "by_current_s", tol));
        }

        /// <summary>
        /// A time is `usable / -slope`: the relative error of the slope carries over, so a time of hundreds of seconds is compared with the golden's tolerance
        /// RELATIVE to its size (never tighter than the tolerance itself).
        /// </summary>
        static double SecondsTol(JObj e, string key, double tol) => C.IsNull(e, key) ? tol : Math.Max(tol, Math.Abs(C.N(e, key)) * tol);

        [Test]
        public void Golden_Limits_MatchTheWeb()
        {
            var l = C.O(C.Golden("battery"), "limits");
            Assert.AreEqual(C.N(l, "min_delta_a"), BatteryHealth.MinDeltaA);
            Assert.AreEqual(C.N(l, "max_dt_s"), BatteryHealth.MaxDtS);
            Assert.AreEqual(C.N(l, "window_s"), BatteryHealth.WindowS);
            Assert.AreEqual(C.N(l, "r_max_ohm"), BatteryHealth.RMaxOhm);
            Assert.AreEqual(C.N(l, "median_of"), BatteryHealth.MedianOf);
            Assert.AreEqual(C.N(l, "min_accepted"), BatteryHealth.MinAccepted);
            Assert.AreEqual(C.N(l, "slope_window_s"), BatteryHealth.SlopeWindowS);
            Assert.AreEqual(C.N(l, "slope_min_samples"), BatteryHealth.SlopeMinSamples);
            Assert.AreEqual(C.N(l, "slope_min_span_s"), BatteryHealth.SlopeMinSpanS);
            Assert.AreEqual(C.N(l, "slope_max_per_s"), BatteryHealth.SlopeMaxPerS);
            Assert.AreEqual(C.N(l, "reserve_rem"), BatteryHealth.ReserveRem);
            Assert.AreEqual(AlertThresholds.Default.batteryLowRem, BatteryHealth.ReserveRem, "the reserve is the low-battery threshold");
            Assert.AreEqual(C.N(l, "sim_capacity_ah"), BatteryHealth.SimCapacityAh);
            Assert.AreEqual(SimLimits.CapacityAh, BatteryHealth.SimCapacityAh, "the capacity is the simulator's");
            Assert.AreEqual(C.N(l, "ring_length"), BatteryHealth.RingLength);
            Assert.AreEqual(C.N(l, "sample_hz"), BatteryHealth.SampleHz);
        }

        [Test]
        public void Golden_Estimator_OnSyntheticSeries()
        {
            var g = C.Golden("battery");
            double tol = C.N(g, "tolerance");
            var cs = new Cases("battery.estimator", tol);
            foreach (JObj c in C.A(g, "estimator"))
            {
                cs.Case();
                string name = C.S(c, "name");
                JArr profile = C.A(c, "profile");
                double end = C.V(profile[profile.Count - 1])[0];
                int n = (int)JsMath.Round(end / Dt);
                double v0 = C.N(c, "v0"), r = C.N(c, "r"), rem = C.N(c, "rem");
                var st = new BatteryEstimator();
                for (int k = 1; k <= n; k++)
                {
                    double t = k * Dt;
                    double amps = C.V(profile[profile.Count - 1])[1];
                    foreach (object seg in profile) { double[] s = C.V(seg); if (t <= s[0] + 1e-9) { amps = s[1]; break; } }
                    BatteryHealth.Push(st, new BatterySample(t, Round(v0 - r * amps, 6), amps, rem));
                }
                cs.Eq(C.I(c, "samples"), st.samples.Count, name + " samples");
                cs.Eq(C.I(c, "accepted"), st.accepted.Count, name + " accepted");
                cs.Near(C.V(c, "accepted_values"), st.accepted.ToArray(), name + " accepted values", 1e-9);
                var last = C.O(c, "last");
                cs.Near(C.N(last, "v"), st.Last.v, name + " last v");
                cs.Near(C.N(last, "i"), st.Last.i, name + " last i");
                CompareReport(cs, c, BatteryHealth.Health(st, C.I(c, "cells"), C.B(c, "flying")), name, tol);
            }
            cs.Done();
        }

        [Test]
        public void Golden_TimeLeft_SlopeAndDashes()
        {
            var g = C.Golden("battery");
            double tol = C.N(g, "tolerance");
            var cs = new Cases("battery.time_left", tol);
            foreach (JObj c in C.A(g, "time_left"))
            {
                cs.Case();
                string name = C.S(c, "name");
                int n = C.I(c, "n");
                double rem0 = C.N(c, "rem0"), slope = C.N(c, "slope"), noise = C.N(c, "noise"), amps = C.N(c, "amps");
                var series = new List<BatterySample>();
                for (int k = 1; k <= n; k++)
                {
                    double t = k * Dt;
                    series.Add(new BatterySample(t, 15, amps, Round(rem0 + slope * t + noise * Math.Sin(0.7 * k), 9)));
                }
                cs.Near(C.N(c, "rem_last"), series[n - 1].rem, name + " rem_last", 1e-9);
                BatteryTimeLeft tl = BatteryHealth.TimeLeft(series, C.B(c, "flying"), C.IsNull(c, "capacity_ah") ? (double?)null : C.N(c, "capacity_ah"));
                var o = C.O(c, "out");
                NearOrNull(cs, o, "slope", tl.slope, name, tol);
                NearOrNull(cs, o, "by_slope_s", tl.bySlopeS, name, SecondsTol(o, "by_slope_s", tol));
                NearOrNull(cs, o, "by_current_s", tl.byCurrentS, name, SecondsTol(o, "by_current_s", tol));
            }
            cs.Done();
        }

        [Test]
        public void Golden_Simulator_600s_RHatIs12mOhm()
        {
            var g = C.Golden("battery");
            double tol = C.N(g, "tolerance");
            var sp = C.O(g, "simulator");
            int substeps = C.I(sp, "substeps_per_sample");
            double subDt = C.N(sp, "substep_dt");
            int total = (int)JsMath.Round(C.N(sp, "seconds") / Dt);
            int every = (int)JsMath.Round(C.N(sp, "checkpoint_every_s") / Dt);
            JArr checkpoints = C.A(sp, "checkpoints"), full = C.A(sp, "health_at_checkpoints");
            var cs = new Cases("battery.simulator", tol);
            var sim = FlightSim.Create();
            var st = new BatteryEstimator();
            int k = 0, cp = 0;
            double iMin = double.PositiveInfinity, iMax = double.NegativeInfinity;

            void SampleOnce()
            {
                for (int i = 0; i < substeps; i++) FlightSim.Step(sim, subDt);
                k++;
                TelemetryFrame f = FlightSim.Frame(sim, k * 250);
                BatteryHealth.Push(st, new BatterySample(k * Dt, f.bat.v, f.bat.i, f.bat.rem));
                if (k % every != 0) return;
                cs.Case();
                BatteryReport h = BatteryHealth.Health(st, 4, f.armed && !f.landed, BatteryHealth.SimCapacityAh);
                var e = (JObj)checkpoints[cp];
                cs.Near(C.N(e, "t"), k * Dt, "checkpoint " + cp + " t");
                NearOrNull(cs, e, "r_hat", h.rHat, "checkpoint " + cp, tol);
                cs.Eq(C.I(e, "quality"), h.quality, "checkpoint " + cp + " quality");
                CompareReport(cs, (JObj)full[cp], h, "health " + cp, tol);
                cp++;
            }

            foreach (JObj op in C.A(sp, "ops"))
            {
                if (k >= total) break;
                if (C.S(op, "op") == "command") FlightSim.Command(sim, SimGolden.Cmd(C.O(op, "msg")));
                else
                {
                    int n = Math.Min((int)JsMath.Round(C.N(op, "seconds") / Dt), total - k);
                    for (int i = 0; i < n; i++) SampleOnce();
                }
            }
            while (k < total) SampleOnce();
            foreach (BatterySample s in st.samples) { iMin = Math.Min(iMin, s.i); iMax = Math.Max(iMax, s.i); }

            cs.Case();
            cs.Eq(checkpoints.Count, cp, "checkpoints seen");
            var fin = C.O(sp, "final");
            var last = C.O(fin, "last");
            cs.Near(C.N(fin, "t"), st.Last.t, "final t");
            cs.Near(C.N(last, "v"), st.Last.v, "final v");
            cs.Near(C.N(last, "i"), st.Last.i, "final i");
            cs.Near(C.N(last, "rem"), st.Last.rem, "final rem");
            bool flying = sim.armed && !sim.landed;
            cs.Eq(C.B(fin, "flying"), flying, "final flying");
            BatteryReport hf = BatteryHealth.Health(st, 4, flying, BatteryHealth.SimCapacityAh);
            CompareReport(cs, fin, hf, "final", tol);
            var range = C.O(sp, "current_range_a");
            cs.Near(C.N(range, "min"), iMin, "current min", 1e-3);
            cs.Near(C.N(range, "max"), iMax, "current max", 1e-3);
            cs.Done();

            // the check of the specification: the simulator's voltage is 4 * (3.5 + 0.7 * rem) - 0.012 * I, so R-hat must give 0.012 ohm
            Assert.IsTrue(hf.rHat.HasValue);
            Assert.AreEqual(C.N(sp, "expected_r_ohm"), hf.rHat.Value, C.N(sp, "expected_tolerance_ohm"), "R-hat of the simulator");
            Assert.AreEqual(0.012, C.N(sp, "expected_r_ohm"));
            Debug.Log("[contract] battery.simulator: R-hat " + hf.rHat.Value.ToString("0.000000") + " ohm after 600 s (expected 0.012 +- 0.001), time left " + hf.bySlopeS + " s by slope, " + hf.byCurrentS + " s by current");
        }

        [Test]
        public void Golden_RealFlight_300Samples()
        {
            string path = GoldenReplayTests.FindFlightTwf();
            if (path == null) Assert.Inconclusive("x500v2_flight.twf not found");
            var g = C.Golden("battery");
            double tol = C.N(g, "tolerance");
            var rf = C.O(g, "real_flight");
            var sch = new ReplaySchedule(TwfFile.Decode(File.ReadAllBytes(path)));
            int start = C.I(rf, "start_g"), step = C.I(rf, "step_g"), count = C.I(rf, "count"), every = C.I(rf, "checkpoint_every");
            JArr checkpoints = C.A(rf, "checkpoints");
            var cs = new Cases("battery.real_flight", tol);
            var st = new BatteryEstimator();
            bool flying = false;
            int cp = 0;
            double iMin = double.PositiveInfinity, iMax = double.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                TelemetryFrame f = sch.Frame(start + i * step);
                BatteryHealth.Push(st, new BatterySample((i + 1) * Dt, f.bat.v, f.bat.i, f.bat.rem));
                iMin = Math.Min(iMin, f.bat.i); iMax = Math.Max(iMax, f.bat.i);
                flying = f.armed && !f.landed;
                if ((i + 1) % every != 0) continue;
                cs.Case();
                BatteryReport h = BatteryHealth.Health(st, 4, flying);
                var e = (JObj)checkpoints[cp];
                cs.Eq(C.I(e, "n"), i + 1, "checkpoint " + cp + " n");
                NearOrNull(cs, e, "r_hat", h.rHat, "checkpoint " + cp, tol);
                cs.Eq(C.I(e, "quality"), h.quality, "checkpoint " + cp + " quality");
                cp++;
            }
            cs.Case();
            cs.Eq(checkpoints.Count, cp, "checkpoints seen");
            cs.Eq(C.B(rf, "flying_at_end"), flying, "flying at the end");
            var fin = C.O(rf, "final");
            var last = C.O(fin, "last");
            cs.Near(C.N(last, "v"), st.Last.v, "final v", 1e-5);
            cs.Near(C.N(last, "i"), st.Last.i, "final i", 1e-5);
            cs.Near(C.N(last, "rem"), st.Last.rem, "final rem", 1e-5);
            BatteryReport hf = BatteryHealth.Health(st, 4, flying);
            CompareReport(cs, fin, hf, "final", tol);
            cs.True(!hf.byCurrentS.HasValue, "the replay does not know the capacity: no time by current");
            var range = C.O(rf, "current_range_a");
            cs.Near(C.N(range, "min"), iMin, "current min", 1e-3);
            cs.Near(C.N(range, "max"), iMax, "current max", 1e-3);
            cs.Done();
        }

        static string Name(ReadingOrigin o) => o == ReadingOrigin.Measured ? "measured" : o == ReadingOrigin.Estimated ? "estimated" : o == ReadingOrigin.Simulated ? "simulated" : o.ToString();

        [Test]
        public void Golden_Origins_OfEveryValue()
        {
            var g = C.Golden("battery");
            var cs = new Cases("battery.origins");
            foreach (JObj c in C.A(g, "origins"))
            {
                cs.Case();
                string src = C.S(c, "source");
                BatterySource s = src == "sim" ? BatterySource.Sim : src == "sitl" ? BatterySource.Sitl : BatterySource.Replay;
                string cur = C.IsNull(c, "current") ? null : C.S(c, "current");
                bool own = C.B(c, "own");
                BatteryOrigins o = BatteryHealth.Origins(s, cur, own);
                var e = C.O(c, "out");
                string what = src + "/" + (cur ?? "-") + "/" + own;
                cs.Eq(C.S(e, "v"), Name(o.v), what + " v");
                cs.Eq(C.S(e, "i"), Name(o.i), what + " i");
                cs.Eq(C.S(e, "rem"), Name(o.rem), what + " rem");
                cs.Eq(C.S(e, "derived"), Name(o.derived), what + " derived");
                cs.Eq(C.B(e, "onSimData"), o.onSimData, what + " onSimData");
                cs.Eq("estimated", Name(o.derived), what + ": R-hat, the sag, the rest voltage and the time are ALWAYS an estimate");
            }
            cs.Done();
        }

        [Test]
        public void Dashes_OnTheGround_WithARisingCharge_AndWithFewPairs()
        {
            var st = new BatteryEstimator();
            for (int k = 1; k <= 160; k++) BatteryHealth.Push(st, new BatterySample(k * Dt, 15.5, 18, 0.9 - 0.002 * k * Dt));
            BatteryReport ground = BatteryHealth.Health(st, 4, false, 5);
            Assert.IsFalse(ground.bySlopeS.HasValue, "on the ground: no time left");
            Assert.IsFalse(ground.byCurrentS.HasValue);
            Assert.IsFalse(ground.rHat.HasValue, "a constant current gives no pair: calculating");
            Assert.AreEqual(0, ground.quality);
            Assert.IsFalse(ground.dropV.HasValue);
            Assert.IsFalse(ground.restV.HasValue);
            Assert.IsTrue(BatteryHealth.Health(st, 4, true).bySlopeS.HasValue, "flying with the charge falling: a time");
            var up = new BatteryEstimator();
            for (int k = 1; k <= 160; k++) BatteryHealth.Push(up, new BatterySample(k * Dt, 15.5, 18, 0.5 + 0.001 * k * Dt));
            Assert.IsFalse(BatteryHealth.Health(up, 4, true).bySlopeS.HasValue, "a slope >= 0: no time");
        }

        [Test]
        public void Push_IgnoresTimeGoingBack_AndKeepsTheRing()
        {
            var st = new BatteryEstimator();
            Assert.IsTrue(BatteryHealth.Push(st, new BatterySample(1, 16, 5, 0.9)));
            Assert.IsFalse(BatteryHealth.Push(st, new BatterySample(1, 16, 5, 0.9)), "the same time");
            Assert.IsFalse(BatteryHealth.Push(st, new BatterySample(0.5, 16, 5, 0.9)), "an older time");
            Assert.IsFalse(BatteryHealth.Push(st, new BatterySample(2, double.NaN, 5, 0.9)), "not a number");
            for (int k = 0; k < 400; k++) BatteryHealth.Push(st, new BatterySample(2 + k * Dt, 16, 5, 0.9));
            Assert.AreEqual(BatteryHealth.RingLength, st.samples.Count);
            Assert.AreEqual(2 + 399 * Dt, st.Last.t, 1e-9);
        }

        [Test]
        public void Cloud_PutsTheLineThroughTheNewestSample()
        {
            var st = new BatteryEstimator();
            for (int k = 1; k <= 80; k++)
            {
                double amps = (k / 16) % 2 == 0 ? 2 : 20;
                BatteryHealth.Push(st, new BatterySample(k * Dt, 16 - 0.015 * amps, amps, 0.8));
            }
            double? r = BatteryHealth.Resistance(st);
            Assert.AreEqual(0.015, r.Value, 1e-9);
            ViCloud c = BatteryHealth.Cloud(st.samples, r, 240, 110);
            Assert.AreEqual(80, c.count);
            Assert.IsTrue(c.hasLine);
            Assert.AreEqual(18, c.iMax - c.iMin, 1e-9, "the span of the data (wider than the 6 A minimum)");
            Assert.AreEqual(0.5, c.vMax - c.vMin, 1e-9, "the 0.5 V minimum span");
            // every sample lies on V = V0 - R * I, so every point is on the segment
            for (int k = 0; k < c.count; k++)
            {
                double yOnLine = c.lineY0 + (c.lineY1 - c.lineY0) * (c.x[k] - c.lineX0) / (c.lineX1 - c.lineX0);
                Assert.AreEqual(yOnLine, c.y[k], 1e-6);
                Assert.That(c.x[k] >= -1e-9 && c.x[k] <= 240 + 1e-9 && c.y[k] >= -1e-9 && c.y[k] <= 110 + 1e-9);
            }
            Assert.Greater(c.lineY1, c.lineY0, "more current, less voltage: the line falls to the right (y grows downwards)");
            ViCloud none = BatteryHealth.Cloud(st.samples, null, 240, 110, c);
            Assert.AreSame(c, none);
            Assert.IsFalse(none.hasLine, "while calculating there is no line");
            Assert.AreEqual(0, BatteryHealth.Cloud(new List<BatterySample>(), null, 240, 110).count);
        }
    }
}
