// Lote 1b, tool 3: the pre-flight checklist (Telemetry/Logic/Preflight.cs) against contracts/golden/preflight.json, produced by the web's own src/shared/preflight.ts
// (tools/contracts/golden_preflight.ts): 14 cases (every threshold at its border, gps always "na", the replay with a synthetic fault, the simulator outside its
// fence, a late link), plus the take-off gate of the interface (asks with a fail, not with a warning).
using System.Collections.Generic;
using NUnit.Framework;

namespace WebGL.Telemetry.Tests
{
    using JObj = Dictionary<string, object>;
    using JArr = List<object>;

    public class PreflightTests
    {
        static PreflightInput Input(JObj o)
        {
            var i = new PreflightInput();
            if (!C.IsNull(o, "hello"))
            {
                var h = C.O(o, "hello");
                i.hello = new HelloMessage { rateHz = C.N(h, "rateHz"), batteryCells = C.I(h, "batteryCells"), provenance = C.Provenance(h["provenance"]) };
            }
            string src = C.S(o, "source");
            i.source = src == "sim" ? PreflightSource.Sim : src == "sitl" ? PreflightSource.Sitl : PreflightSource.Replay;
            i.own = o.ContainsKey("own") && o["own"] is bool b && b;
            var link = C.O(o, "link");
            string st = C.S(link, "state");
            i.link = st == "open" ? PreflightLink.Open : st == "connecting" ? PreflightLink.Connecting : PreflightLink.Closed;
            i.rxHz = C.N(link, "rxHz");
            i.starvedS = C.N(link, "starvedS");
            foreach (JObj a in C.A(o, "alerts")) { i.alerts++; if (C.S(a, "severity") == "critical") i.criticalAlerts++; }
            i.faults = C.A(o, "faults").Count;
            if (!C.IsNull(o, "fence")) { var f = C.O(o, "fence"); i.fence = new FenceConfig(C.N(f, "radius"), C.N(f, "ceiling"), 10); }
            var manual = new Dictionary<string, bool>();
            foreach (var kv in C.O(o, "manual")) manual[kv.Key] = (bool)kv.Value;
            i.manual = manual;
            if (!C.IsNull(o, "frame"))
            {
                var f = C.O(o, "frame");
                var bat = C.O(f, "bat");
                i.frame = new TelemetryFrame
                {
                    p = C.V(f, "p"), temp = C.V(f, "temp"), vib = C.N(f, "vib"), armed = C.B(f, "armed"), landed = C.B(f, "landed"),
                    bat = new BatteryState { v = C.N(bat, "v"), rem = C.N(bat, "rem") },
                };
            }
            return i;
        }

        static void Compare(Cases cs, string name, JObj expected, PreflightResult got, double tol)
        {
            JArr items = C.A(expected, "items");
            cs.Eq(items.Count, got.items.Count, name + " item count");
            for (int k = 0; k < items.Count && k < got.items.Count; k++)
            {
                var e = (JObj)items[k];
                PreflightItem a = got.items[k];
                string id = C.S(e, "id");
                cs.Eq(id, a.id, name + " id[" + k + "]");
                cs.Eq(C.S(e, "status"), Preflight.Name(a.status), name + " " + id + " status");
                cs.Eq(C.S(e, "origin"), Preflight.Name(a.origin), name + " " + id + " origin");
                cs.Eq(C.IsNull(e, "unit") ? null : C.S(e, "unit"), a.unit, name + " " + id + " unit");
                cs.Eq(C.IsNull(e, "detail") ? null : C.S(e, "detail"), a.detail, name + " " + id + " detail");
                if (C.IsNull(e, "value")) cs.True(!a.value.HasValue, name + " " + id + " value expected none, got " + a.value);
                else if (!a.value.HasValue) cs.True(false, name + " " + id + " value expected " + C.N(e, "value") + ", got none");
                else cs.Near(C.N(e, "value"), a.value.Value, name + " " + id + " value", tol);
            }
            var s = C.O(expected, "summary");
            cs.Eq(C.S(s, "verdict"), Preflight.Name(got.summary.verdict), name + " verdict");
            cs.Eq(C.I(s, "pass"), got.summary.pass, name + " pass");
            cs.Eq(C.I(s, "warn"), got.summary.warn, name + " warn");
            cs.Eq(C.I(s, "fail"), got.summary.fail, name + " fail");
            cs.Eq(C.I(s, "na"), got.summary.na, name + " na");
        }

        [Test]
        public void Golden_Thresholds_AndItemOrder_MatchTheWeb()
        {
            var g = C.Golden("preflight");
            var t = C.O(g, "thresholds");
            Assert.AreEqual(C.N(t, "rem_pass"), Preflight.RemPass);
            Assert.AreEqual(C.N(t, "rem_warn"), Preflight.RemWarn);
            Assert.AreEqual(C.N(t, "cell_pass_v"), Preflight.CellPass);
            Assert.AreEqual(C.N(t, "cell_warn_v"), Preflight.CellWarn);
            Assert.AreEqual(C.N(t, "link_rate_ratio"), Preflight.LinkRateRatio);
            Assert.AreEqual(C.N(t, "starved_warn_s"), Preflight.StarvedWarnS);
            Assert.AreEqual(C.N(t, "fence_margin_m"), Preflight.FenceMarginM);
            var th = AlertThresholds.Default;
            Assert.AreEqual(C.N(t, "motor_temp_warn_c"), th.motorTempWarnC);
            Assert.AreEqual(C.N(t, "motor_temp_crit_c"), th.motorTempCritC);
            Assert.AreEqual(C.N(t, "battery_temp_warn_c"), th.batteryTempWarnC);
            Assert.AreEqual(C.N(t, "battery_temp_crit_c"), th.batteryTempCritC);
            Assert.AreEqual(C.N(t, "vibration_warn"), th.vibrationWarn);
            Assert.AreEqual(C.N(t, "vibration_crit"), th.vibrationCrit);
            JArr ids = C.A(g, "item_ids");
            Assert.AreEqual(ids.Count, Preflight.ItemIds.Length);
            for (int i = 0; i < ids.Count; i++) Assert.AreEqual((string)ids[i], Preflight.ItemIds[i]);
        }

        [Test]
        public void Golden_Cases_EveryThresholdAtItsBorder()
        {
            var g = C.Golden("preflight");
            double tol = C.N(g, "tolerance");
            var cs = new Cases("preflight.cases", tol);
            int inputs = 0;
            foreach (JObj c in C.A(g, "cases"))
            {
                cs.Case();
                string name = C.S(c, "name");
                foreach (string suffix in new[] { "", "_b", "_c" })
                {
                    if (!c.ContainsKey("input" + suffix)) continue;
                    inputs++;
                    PreflightResult r = Preflight.Evaluate(Input(C.O(c, "input" + suffix)));
                    Compare(cs, name + suffix, C.O(c, "out" + suffix), r, tol);
                    // gps is "na" always: the wire carries no GPS
                    PreflightItem gps = r.Find("gps");
                    cs.True(gps != null && gps.status == PreflightStatus.Na && gps.origin == PreflightOrigin.None && !gps.value.HasValue, name + suffix + " gps is na");
                }
            }
            cs.Done();
            Assert.AreEqual(14, C.A(g, "cases").Count, "14 cases in the golden");
            Assert.GreaterOrEqual(inputs, 17, "with the other side of the borders");
        }

        [Test]
        public void TakeoffGate_AsksWithAFail_NotWithAWarning()
        {
            var g = C.Golden("preflight");
            foreach (JObj c in C.A(g, "cases"))
            {
                PreflightResult r = Preflight.Evaluate(Input(C.O(c, "input")));
                Assert.AreEqual(r.summary.fail > 0, Preflight.TakeoffAsks(r), C.S(c, "name"));
                Assert.AreEqual(r.summary.fail, Preflight.FailingIds(r).Count, C.S(c, "name"));
                if (r.summary.verdict == PreflightVerdict.Warn) Assert.IsFalse(Preflight.TakeoffAsks(r), C.S(c, "name") + ": a warning does not ask");
            }
            Assert.IsFalse(Preflight.TakeoffAsks(null), "no checks: the take-off is sent straight away");
        }

        [Test]
        public void Gps_IsNa_InEverySource_WithOrWithoutAFrame()
        {
            foreach (PreflightSource s in new[] { PreflightSource.Replay, PreflightSource.Sim, PreflightSource.Sitl })
            foreach (bool withFrame in new[] { false, true })
            {
                var i = new PreflightInput { source = s, link = PreflightLink.Open, rxHz = 20, frame = withFrame ? new TelemetryFrame { landed = true, bat = new BatteryState { v = 16, rem = 0.9 } } : null };
                PreflightItem gps = Preflight.Evaluate(i).Find("gps");
                Assert.AreEqual(PreflightStatus.Na, gps.status);
                Assert.AreEqual(PreflightOrigin.None, gps.origin);
            }
        }

        [Test]
        public void Simulator_Flying_FailsTheStateRow_AndReplayHasNoBoxes()
        {
            var s = FlightSim.Create();
            FlightSim.Command(s, ClientMessage.Takeoff(5));
            for (int i = 0; i < 1200; i++) FlightSim.Step(s, 1.0 / 120);
            var input = new PreflightInput
            {
                source = PreflightSource.Sim, link = PreflightLink.Open, rxHz = 60, hello = new HelloMessage { rateHz = 60, batteryCells = 4 },
                frame = FlightSim.Frame(s, 0), fence = s.cfg,
            };
            PreflightResult r = Preflight.Evaluate(input);
            Assert.AreEqual(PreflightStatus.Fail, r.Find("armed").status);
            Assert.AreEqual("flying", r.Find("armed").detail);
            Assert.AreEqual(PreflightOrigin.Simulated, r.Find("bat.rem").origin, "in the simulator everything is simulated");
            Assert.AreEqual(PreflightStatus.Warn, r.Find("manual.props").status, "an unticked box is a warning");
            input.source = PreflightSource.Replay;
            r = Preflight.Evaluate(input);
            foreach (string id in Preflight.ManualIds) Assert.AreEqual(PreflightStatus.Na, r.Find(id).status, "the replay has nobody at the field to tick " + id);
            Assert.AreEqual(PreflightStatus.Na, r.Find("fence").status, "the fence only exists in the simulator");
        }
    }
}
