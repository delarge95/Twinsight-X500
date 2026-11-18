// P2 of the MVP: the battery reaction of the simulator (Telemetry/Sim/BatteryReact.cs + FlightSim.StepBatteryReact) against contracts/golden/batteryreact.json,
// produced by the web's own src/shared/batteryfail.ts and flightsim.ts (tools/contracts/golden_batteryreact.ts): defaults, limits, texts, validation, the stored
// switch and the 10 scenarios (warning, return, emergency, 0 % in the return, 0 % landing, reaction off, switch in flight, custom thresholds, cut motor and
// natural drain), with the tolerances of the golden.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace WebGL.Telemetry.Tests
{
    using JObj = Dictionary<string, object>;

    public class BatteryReactTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static BatteryReactConfig Cfg(JObj o) => new BatteryReactConfig { react = C.B(o, "react"), lowRem = C.N(o, "lowRem"), critRem = C.N(o, "critRem"), emptyRem = C.N(o, "emptyRem") };

        static string Sev(EventSeverity s) => s == EventSeverity.Info ? "info" : s == EventSeverity.Warning ? "warning" : "critical";

        [Test]
        public void Golden_Defaults_Limits_AndParams()
        {
            var g = C.Golden("batteryreact");
            var d = C.O(g, "defaults");
            var def = BatteryReact.Default();
            Assert.AreEqual(C.B(d, "react"), def.react);
            Assert.AreEqual(C.N(d, "lowRem"), def.lowRem);
            Assert.AreEqual(C.N(d, "critRem"), def.critRem);
            Assert.AreEqual(C.N(d, "emptyRem"), def.emptyRem);
            var l = C.O(g, "limits");
            Assert.AreEqual(C.N(l, "emptyMax"), BatteryReact.EmptyMax);
            Assert.AreEqual(C.N(l, "critMax"), BatteryReact.CritMax);
            Assert.AreEqual(C.N(l, "lowMax"), BatteryReact.LowMax);
            var p = C.O(g, "params");
            Assert.AreEqual(C.N(p, "emergencyHoldS"), BatteryReact.EmergencyHoldS);
            Assert.AreEqual(C.N(p, "disarmDelayS"), BatteryReact.DisarmDelayS);
            CollectionAssert.AreEqual(C.A(g, "emergency_allowed").Cast<string>().ToArray(), BatteryReact.EmergencyAllowed);
            Assert.AreEqual(C.S(g, "emergency_detail"), BatteryReact.EmergencyDetail);
            // A fresh simulator reacts (the golden is created with the defaults).
            Assert.IsTrue(FlightSim.Create().cfg.battery.react);
        }

        [Test]
        public void Golden_Texts()
        {
            var tx = C.O(C.Golden("batteryreact"), "texts");
            var cs = new Cases("batteryreact.texts");
            foreach (JObj c in C.A(tx, "low")) { cs.Case(); cs.Eq(C.S(c, "text"), BatteryReact.Text.Low(C.N(c, "rem")), "low " + C.N(c, "rem")); }
            foreach (JObj c in C.A(tx, "crit")) { cs.Case(); cs.Eq(C.S(c, "text"), BatteryReact.Text.Crit(C.N(c, "rem")), "crit " + C.N(c, "rem")); }
            foreach (JObj c in C.A(tx, "critNoAction")) { cs.Case(); cs.Eq(C.S(c, "text"), BatteryReact.Text.CritNoAction(C.N(c, "rem")), "critNoAction"); }
            cs.Case(); cs.Eq(C.S(tx, "empty"), BatteryReact.Text.Empty(BatteryReact.EmergencyHoldS), "empty");
            cs.Case(); cs.Eq(C.S(tx, "emptyLanding"), BatteryReact.Text.EmptyLanding, "emptyLanding");
            cs.Case(); cs.Eq(C.S(tx, "emptyNoAction"), BatteryReact.Text.EmptyNoAction, "emptyNoAction");
            cs.Case(); cs.Eq(C.S(tx, "landing"), BatteryReact.Text.Landing, "landing");
            cs.Case(); cs.Eq(C.S(tx, "disarmed"), BatteryReact.Text.Disarmed, "disarmed");
            cs.Done();
        }

        [Test]
        public void Golden_Validate_AtItsBorders()
        {
            var cs = new Cases("batteryreact.validate");
            foreach (JObj c in C.A(C.Golden("batteryreact"), "validate"))
            {
                cs.Case();
                string name = C.S(c, "name");
                var b = C.O(c, "battery");
                // "react not a boolean" is a JS-only case: in C# the type already forbids it.
                if (!(b["react"] is bool)) { cs.Eq("batteryReact", C.S(c, "out"), name + " out"); continue; }
                var cfg = new BatteryReactConfig
                {
                    react = C.B(b, "react"),
                    lowRem = b["lowRem"] == null ? double.NaN : C.N(b, "lowRem"),
                    critRem = b["critRem"] == null ? double.NaN : C.N(b, "critRem"),
                    emptyRem = b["emptyRem"] == null ? double.NaN : C.N(b, "emptyRem"),
                };
                ConfigCheck r = BatteryReact.Validate(cfg);
                cs.Eq(C.S(c, "out"), r.ok ? "ok" : r.code, name + " out");
                cs.Eq(C.IsNull(c, "detail") ? null : C.S(c, "detail"), r.ok ? null : r.detail, name + " detail");
                cs.Eq(C.IsNull(c, "min") ? (double?)null : C.N(c, "min"), r.ok ? null : r.min, name + " min");
                cs.Eq(C.IsNull(c, "max") ? (double?)null : C.N(c, "max"), r.ok ? null : r.max, name + " max");
            }
            cs.Done();
        }

        [Test]
        public void Golden_Storage_OnlyZeroTurnsItOff()
        {
            var cs = new Cases("batteryreact.storage");
            foreach (JObj c in C.A(C.Golden("batteryreact"), "storage"))
            {
                cs.Case();
                cs.Eq(C.B(c, "react"), BatteryReact.ParseReact(C.IsNull(c, "raw") ? null : C.S(c, "raw")), C.S(c, "name"));
            }
            cs.Done();
        }

        [Test]
        public void Golden_Scenarios_MatchTheWebModel()
        {
            var g = C.Golden("batteryreact");
            var t = SimGolden.TolOf(g);
            var tol = C.O(g, "tolerance");
            double tolReact = C.N(tol, "react"), tolEvent = C.N(tol, "event_t");
            var perScenario = new List<string>();
            var scenarios = C.A(g, "scenarios");
            Assert.AreEqual(10, scenarios.Count, "the 10 scenarios of the golden");
            foreach (JObj sc in scenarios)
            {
                string name = C.S(sc, "name");
                var cs = new Cases("batteryreact." + name, t.State);
                var cfg = new SimConfig { battery = Cfg(C.O(sc, "battery")) };
                var s = FlightSim.Create(22, cfg);
                Play(cs, name, sc, s, t, tolReact, tolEvent);
                var fin = C.O(sc, "final_cfg_battery");
                cs.Case();
                cs.Eq(C.B(fin, "react"), s.cfg.battery.react, name + " final react");
                cs.Near(C.N(fin, "lowRem"), s.cfg.battery.lowRem, name + " final lowRem", 0);
                cs.Near(C.N(fin, "critRem"), s.cfg.battery.critRem, name + " final critRem", 0);
                cs.Near(C.N(fin, "emptyRem"), s.cfg.battery.emptyRem, name + " final emptyRem", 0);
                try { cs.Done(); } catch (AssertionException) { perScenario.Add(name + " FAILED"); throw; }
                perScenario.Add(name + " " + Cases.Summary["batteryreact." + name]);
            }
            Debug.Log("[contract] batteryreact scenarios: " + string.Join(", ", perScenario));
        }

        /// <summary>The runner of SimGolden.Play plus the `rem` op, the event records (taken after every step, before its row) and the four react columns.</summary>
        static void Play(Cases cs, string name, JObj sc, SimState s, SimGolden.Tol t, double tolReact, double tolEvent)
        {
            var expect = C.A(sc, "expect");
            int e = 0;
            cs.Case();
            CheckRow(cs, (string)expect[e++], s, name + "#0", t, tolReact);
            foreach (JObj op in C.A(sc, "ops"))
            {
                string kind = C.S(op, "op");
                if (kind == "command")
                {
                    var msg = C.O(op, "msg");
                    SimReply r = FlightSim.Command(s, SimGolden.Cmd(msg));
                    var ex = (JObj)expect[e++];
                    cs.Case();
                    cs.Eq(C.S(ex, "reply"), C.S(msg, "name"), name + " reply name");
                    cs.Eq(C.B(ex, "ok"), r.ok, name + " " + C.S(msg, "name") + " ok");
                    cs.Eq(C.IsNull(ex, "detail") ? null : C.S(ex, "detail"), r.detail, name + " " + C.S(msg, "name") + " detail");
                    cs.Eq(C.IsNull(ex, "event") ? null : C.S(ex, "event"), r.evt, name + " " + C.S(msg, "name") + " event");
                }
                else if (kind == "config")
                {
                    ConfigCheck r = FlightSim.SetConfig(s, SimGolden.Patch(C.O(op, "cfg")));
                    var ex = (JObj)expect[e++];
                    cs.Case();
                    cs.Eq("config", C.S(ex, "reply"), name + " reply name");
                    cs.Eq(C.B(ex, "ok"), r.ok, name + " config ok " + r.detail);
                    cs.Eq(C.IsNull(ex, "detail") ? null : C.S(ex, "detail"), r.ok ? null : r.detail, name + " config detail");
                    cs.Eq(C.IsNull(ex, "code") ? null : C.S(ex, "code"), r.ok ? null : r.code, name + " config code");
                }
                else if (kind == "rem")
                {
                    s.rem = C.N(op, "value");
                    cs.Case();
                    CheckRow(cs, (string)expect[e++], s, name + " rem", t, tolReact);
                }
                else if (kind == "manual")
                {
                    var st = C.O(op, "sticks");
                    FlightSim.Manual(s, new SimSticks(C.N(st, "x"), C.N(st, "y"), C.N(st, "z"), C.N(st, "r")));
                    cs.Case();
                    CheckRow(cs, (string)expect[e++], s, name + " manual", t, tolReact);
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
                        foreach (SimEvent ev in FlightSim.TakeEvents(s))
                        {
                            cs.Case();
                            if (e >= expect.Count || !(expect[e] is JObj ee) || !ee.ContainsKey("event")) { cs.True(false, name + " t=" + s.t.ToString("0.###", Inv) + " unexpected event " + ev.text); continue; }
                            e++;
                            cs.Eq(C.S(ee, "event"), Sev(ev.severity), name + " event severity");
                            cs.Eq(C.S(ee, "text"), ev.text, name + " event text");
                            cs.Near(C.N(ee, "t"), s.t, name + " event t", tolEvent);
                        }
                        if (i % every == 0 || i == steps)
                        {
                            cs.Case();
                            CheckRow(cs, (string)expect[e++], s, name + " t=" + s.t.ToString("0.###", Inv), t, tolReact);
                        }
                    }
                }
            }
            Assert.AreEqual(expect.Count, e, name + ": every expected record was consumed");
        }

        static void CheckRow(Cases cs, string expected, SimState s, string label, SimGolden.Tol t, double tolReact)
        {
            SimGolden.CheckRow(cs, expected, s, label, t);
            string[] tok = expected.Split(' ');
            int nState = SimGolden.StateNumbers(s).Length;
            int nFrame = SimGolden.FrameNumbers(FlightSim.Frame(s, 0)).Length;
            int k = 5 + nState + nFrame;
            BatteryReactState b = s.batReact;
            cs.Eq(tok[k], b.stage.ToString(Inv), label + " stage");
            cs.Eq(tok[k + 1], b.emergency ? "1" : "0", label + " emergency");
            Opt(cs, tok[k + 2], b.holdLeft, label + " holdLeft", tolReact);
            Opt(cs, tok[k + 3], b.disarmIn, label + " disarmIn", tolReact);
        }

        static void Opt(Cases cs, string expected, double? actual, string label, double tol)
        {
            if (expected == "-") { cs.True(!actual.HasValue, label + " expected null, got " + actual); return; }
            if (!actual.HasValue) { cs.True(false, label + " expected " + expected + ", got null"); return; }
            cs.Near(double.Parse(expected, Inv), actual.Value, label, tol);
        }

        [Test]
        public void Config_IsClonedAndOnByDefault()
        {
            var a = new SimConfig();
            Assert.IsTrue(a.battery.react);
            var b = a.Clone();
            b.battery.react = false;
            Assert.IsTrue(a.battery.react, "the clone shares nothing with the original");
        }
    }
}
