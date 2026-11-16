// Shared runner of the simulator golden scripts (contracts/golden/flightsim.json, geofence.json, wind.json): a scenario is a list of ops run on a fresh model
// (`command`, `manual`, `run` and, for the Lote 1a tools, `config`), and `expect` lists the records in order (a row of tokens, or a reply). The format is the
// one of the web's tools/contracts/golden_flightsim.ts.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace WebGL.Telemetry.Tests
{
    using JObj = Dictionary<string, object>;

    static class SimGolden
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static CommandMessage Cmd(JObj m)
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

        public static string ModeName(SimMode m)
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

        public static string PhaseName(RtlPhase p) => p == RtlPhase.Climb ? "climb" : p == RtlPhase.Return ? "return" : "land";

        /// <summary>The state numbers in the column order of the golden (null = "-").</summary>
        public static double?[] StateNumbers(SimState s)
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

        public static double?[] FrameNumbers(TelemetryFrame f)
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

        public sealed class Tol
        {
            public double State, Frame, Rpm;
            public string[] StateCols, FrameCols;
        }

        public static Tol TolOf(JObj g)
        {
            var tol = C.O(g, "tolerance");
            return new Tol
            {
                State = C.N(tol, "state"), Frame = C.N(tol, "frame"), Rpm = C.N(tol, "rpm"),
                StateCols = C.A(g, "state_columns").Cast<string>().ToArray(), FrameCols = C.A(g, "frame_columns").Cast<string>().ToArray(),
            };
        }

        public static void CheckRow(Cases cs, string expected, SimState s, string label, Tol t)
        {
            string[] tok = expected.Split(' ');
            cs.Eq(tok[0], ModeName(s.mode), label + " mode");
            cs.Eq(tok[1], PhaseName(s.rtlPhase), label + " rtlPhase");
            cs.Eq(tok[2], s.armed ? "1" : "0", label + " armed");
            cs.Eq(tok[3], s.landed ? "1" : "0", label + " landed");
            cs.Eq(tok[4], s.pilot ? "1" : "0", label + " pilot");
            double?[] st = StateNumbers(s);
            NUnit.Framework.Assert.AreEqual(t.StateCols.Length, st.Length, "state columns");
            for (int i = 0; i < st.Length; i++)
            {
                string e = tok[5 + i];
                if (e == "-") { cs.True(!st[i].HasValue, label + " " + t.StateCols[i] + " expected null, got " + st[i]); continue; }
                if (!st[i].HasValue) { cs.True(false, label + " " + t.StateCols[i] + " expected " + e + ", got null"); continue; }
                cs.Near(double.Parse(e, Inv), st[i].Value, label + " " + t.StateCols[i], t.State);
            }
            double?[] fr = FrameNumbers(FlightSim.Frame(s, Math.Round(s.t * 1000)));
            NUnit.Framework.Assert.AreEqual(t.FrameCols.Length, fr.Length, "frame columns");
            for (int i = 0; i < fr.Length; i++)
            {
                string e = tok[5 + st.Length + i];
                double tol = t.FrameCols[i].StartsWith("rpm", StringComparison.Ordinal) ? t.Rpm : t.Frame;
                if (e == "-") { cs.True(!fr[i].HasValue, label + " frame." + t.FrameCols[i] + " expected null"); continue; }
                cs.Near(double.Parse(e, Inv), fr[i] ?? double.NaN, label + " frame." + t.FrameCols[i], tol);
            }
        }

        /// <summary>The partial configuration of a `config` op (cfg: { radius?, ceiling?, rtlAltitude?, wind?: {...}, faultComp? }).</summary>
        public static SimConfigPatch Patch(JObj cfg)
        {
            var p = new SimConfigPatch();
            if (cfg.ContainsKey("radius")) p.radius = C.N(cfg, "radius");
            if (cfg.ContainsKey("ceiling")) p.ceiling = C.N(cfg, "ceiling");
            if (cfg.ContainsKey("rtlAltitude")) p.rtlAltitude = C.N(cfg, "rtlAltitude");
            if (cfg.ContainsKey("wind"))
            {
                var w = C.O(cfg, "wind");
                p.wind = new WindPatch();
                if (w.ContainsKey("speed")) p.wind.speed = C.N(w, "speed");
                if (w.ContainsKey("fromDeg")) p.wind.fromDeg = C.N(w, "fromDeg");
                if (w.ContainsKey("gustSigma")) p.wind.gustSigma = C.N(w, "gustSigma");
                if (w.ContainsKey("gustTau")) p.wind.gustTau = C.N(w, "gustTau");
                if (w.ContainsKey("seed")) p.wind.seed = C.N(w, "seed");
            }
            if (cfg.ContainsKey("faultComp")) p.faultComp = C.B(cfg, "faultComp");
            return p;
        }

        /// <summary>Plays one scenario (`ops` and `expect`) on `s`. `onStep` is called after every Step with whether a row was recorded.</summary>
        public static void Play(Cases cs, string name, JObj sc, SimState s, Tol t, Action<SimState, bool> onStep = null)
        {
            var expect = C.A(sc, "expect");
            int e = 0;
            cs.Case();
            CheckRow(cs, (string)expect[e++], s, name + "#0", t);
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
                else if (kind == "config")
                {
                    ConfigCheck r = FlightSim.SetConfig(s, Patch(C.O(op, "cfg")));
                    var ex = (JObj)expect[e++];
                    cs.Case();
                    cs.Eq("config", C.S(ex, "reply"), name + " reply name");
                    cs.Eq(C.B(ex, "ok"), r.ok, name + " config ok " + r.detail);
                    cs.Eq(C.IsNull(ex, "detail") ? null : C.S(ex, "detail"), r.ok ? null : r.detail, name + " config detail");
                    cs.Eq(C.IsNull(ex, "code") ? null : C.S(ex, "code"), r.ok ? null : r.code, name + " config code");
                }
                else if (kind == "manual")
                {
                    var st = C.O(op, "sticks");
                    FlightSim.Manual(s, new SimSticks(C.N(st, "x"), C.N(st, "y"), C.N(st, "z"), C.N(st, "r")));
                    cs.Case();
                    CheckRow(cs, (string)expect[e++], s, name + " manual", t);
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
                        bool rec = i % every == 0 || i == steps;
                        if (rec)
                        {
                            cs.Case();
                            CheckRow(cs, (string)expect[e++], s, name + " t=" + s.t.ToString("0.###", Inv), t);
                        }
                        onStep?.Invoke(s, rec);
                    }
                }
            }
            NUnit.Framework.Assert.AreEqual(expect.Count, e, name + ": every expected record was consumed");
        }
    }
}
