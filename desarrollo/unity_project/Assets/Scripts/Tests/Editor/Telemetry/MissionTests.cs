// Lote 1c, tool 2: the mission planner (Telemetry/Logic/Mission.cs) against contracts/golden/mission.json, produced by the web's own src/shared/mission.ts
// (tools/contracts/golden_mission.ts): the limits, validateMission at its borders, parseMission, seven runner scripts on synthetic frames (controls, acks,
// deadlines -> the commands it sends) and the 4-waypoint square flown on a fresh simulator (the same loop as the generator: 100 Hz model, runner at 20 Hz).
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WebGL.Telemetry.Tests
{
    using JObj = Dictionary<string, object>;
    using JArr = List<object>;

    public class MissionTests
    {
        static double Round(double x, int n = 9) { double p = Math.Pow(10, n); return Math.Round(x * p, MidpointRounding.AwayFromZero) / p; }

        static MissionDoc Doc(JObj o)
        {
            var m = new MissionDoc { id = C.S(o, "id"), name = C.S(o, "name"), finish = C.S(o, "finish") };
            foreach (JObj w in C.A(o, "waypoints"))
            {
                // the generator writes a non-finite north as the string "NaN"
                double n = w["n"] is string ? double.NaN : C.N(w, "n");
                m.waypoints.Add(new Waypoint(n, C.N(w, "e"), C.N(w, "alt"), C.N(w, "speed"), w.ContainsKey("holdS") ? C.N(w, "holdS") : (double?)null));
            }
            return m;
        }

        static MissionLimits Limits(JObj o) => new MissionLimits(C.N(o, "radius"), C.N(o, "minAlt"), C.N(o, "maxAlt"));

        static MissionFrame? Frame(object raw)
        {
            if (raw == null) return null;
            var f = (JArr)raw;
            bool landed = (bool)f[3];
            return new MissionFrame((double)f[0], (double)f[1], -(double)f[2], landed, !landed);
        }

        static void CompareCommand(Cases cs, string what, JObj e, CommandMessage a, double tol)
        {
            cs.Eq(C.S(e, "name"), a.name, what + " name");
            foreach (string k in new[] { "north", "east", "altitude" })
            {
                double? got = k == "north" ? a.north : k == "east" ? a.east : a.altitude;
                if (!e.ContainsKey(k)) { cs.True(!got.HasValue, what + " " + k + " expected none, got " + got); continue; }
                if (!got.HasValue) { cs.True(false, what + " " + k + " expected " + C.N(e, k) + ", got none"); continue; }
                cs.Near(C.N(e, k), got.Value, what + " " + k, tol);
            }
        }

        [Test]
        public void Golden_Limits_MatchTheWeb()
        {
            var g = C.Golden("mission");
            var l = C.O(g, "limits");
            Assert.AreEqual(C.I(l, "maxWaypoints"), Mission.MaxWaypoints);
            Assert.AreEqual(C.N(l, "speedMin"), Mission.SpeedMin);
            Assert.AreEqual(C.N(l, "speedMax"), Mission.SpeedMax);
            Assert.AreEqual(C.N(l, "holdMax"), Mission.HoldMax);
            Assert.AreEqual(C.N(l, "arriveH"), Mission.ArriveH);
            Assert.AreEqual(C.N(l, "arriveV"), Mission.ArriveV);
            Assert.AreEqual(C.N(l, "rabbitPeriod"), Mission.RabbitPeriod);
            Assert.AreEqual(C.N(l, "leadDiv"), Mission.LeadDiv);
            Assert.AreEqual(C.N(l, "legFactor"), Mission.LegFactor);
            Assert.AreEqual(C.N(l, "legSlack"), Mission.LegSlack);
            Assert.AreEqual(C.N(l, "takeoffRate"), Mission.TakeoffRate);
            Assert.AreEqual(Mission.SpeedMax, SimLimits.MaxSpeed, "speedMax is the simulator's top speed");
            MissionLimits sim = Limits(C.O(g, "sim_limits_default")), def = Mission.SimLimitsOf(new FenceConfig().radius, new FenceConfig().ceiling);
            Assert.AreEqual(sim.radius, def.radius); Assert.AreEqual(sim.minAlt, def.minAlt); Assert.AreEqual(sim.maxAlt, def.maxAlt);
            MissionLimits sitl = Limits(C.O(g, "sitl_limits"));
            Assert.AreEqual(sitl.radius, Mission.SitlLimits.radius); Assert.AreEqual(sitl.minAlt, Mission.SitlLimits.minAlt); Assert.AreEqual(sitl.maxAlt, Mission.SitlLimits.maxAlt);
        }

        [Test]
        public void Golden_Validate_AtItsBorders()
        {
            var g = C.Golden("mission");
            var cs = new Cases("mission.validate");
            foreach (JObj c in C.A(g, "validate"))
            {
                cs.Case();
                string name = C.S(c, "name");
                List<MissionIssue> got = Mission.Validate(Doc(C.O(c, "mission")), Limits(C.O(c, "limits")));
                JArr exp = C.A(c, "issues");
                cs.Eq(exp.Count, got.Count, name + " issue count");
                for (int k = 0; k < exp.Count && k < got.Count; k++)
                {
                    var e = (JObj)exp[k];
                    cs.Eq(C.S(e, "code"), got[k].code, name + " code[" + k + "]");
                    cs.Eq(C.IsNull(e, "index") ? (int?)null : C.I(e, "index"), got[k].index, name + " index[" + k + "]");
                    cs.Eq(C.S(e, "detail"), got[k].detail, name + " detail[" + k + "]");
                }
            }
            cs.Done();
        }

        [Test]
        public void Golden_Storage_ParseAndWriteBack()
        {
            var g = C.Golden("mission");
            var cs = new Cases("mission.storage");
            foreach (JObj c in C.A(g, "storage"))
            {
                cs.Case();
                string name = C.S(c, "name");
                MissionDoc got = Mission.Parse(C.S(c, "raw"));
                if (C.IsNull(c, "out")) { cs.True(got == null, name + ": expected null"); continue; }
                if (got == null) { cs.True(false, name + ": expected a mission, got null"); continue; }
                MissionDoc exp = Doc(C.O(c, "out"));
                cs.Eq(exp.id, got.id, name + " id");
                cs.Eq(exp.name, got.name, name + " name");
                cs.Eq(exp.finish, got.finish, name + " finish");
                cs.Eq(exp.waypoints.Count, got.waypoints.Count, name + " waypoints");
                for (int k = 0; k < exp.waypoints.Count && k < got.waypoints.Count; k++)
                {
                    Waypoint a = exp.waypoints[k], b = got.waypoints[k];
                    cs.True(a.n == b.n && a.e == b.e && a.alt == b.alt && a.speed == b.speed && a.holdS == b.holdS, name + " waypoint " + k);
                }
                // what is written is read back the same (the export / import of the interface)
                MissionDoc again = Mission.Parse(Mission.ToJson(got));
                cs.True(again != null && Mission.ToJson(again) == Mission.ToJson(got), name + " round trip");
            }
            cs.Done();
            Assert.IsNull(Mission.Parse(null));
            Assert.IsNull(Mission.Parse(""));
        }

        [Test]
        public void Golden_Scripts_TheRunnerSendsTheSameCommands()
        {
            var g = C.Golden("mission");
            double tol = C.N(C.O(g, "tolerance"), "command");
            var per = new List<string>();
            foreach (JObj sc in C.A(g, "scripts"))
            {
                string name = C.S(sc, "name");
                var cs = new Cases("mission.script." + name, tol);
                MissionRun run = Mission.CreateRun(Doc(C.O(sc, "mission")));
                JArr steps = C.A(sc, "steps"), expect = C.A(sc, "expect");
                for (int k = 0; k < steps.Count; k++)
                {
                    cs.Case();
                    var s = (JObj)steps[k];
                    var e = (JObj)expect[k];
                    double t = C.N(s, "t");
                    MissionFrame? f = Frame(s["f"]);
                    var send = new List<CommandMessage>();
                    if (s.ContainsKey("control"))
                    {
                        string c = C.S(s, "control");
                        MissionControl action = c == "start" ? MissionControl.Start : c == "pause" ? MissionControl.Pause : c == "resume" ? MissionControl.Resume : c == "abort" ? MissionControl.Abort : MissionControl.AbortRtl;
                        MissionStepResult r0 = Mission.Control(run, action, f, t);
                        run = r0.run; send.AddRange(r0.send);
                    }
                    if (s.ContainsKey("ack"))
                    {
                        var a = C.O(s, "ack");
                        MissionStepResult r1 = Mission.Ack(run, C.S(a, "command"), C.B(a, "ok"), a.ContainsKey("detail") ? C.S(a, "detail") : null, t);
                        run = r1.run; send.AddRange(r1.send);
                    }
                    MissionStepResult r = Mission.Step(run, f, t);
                    run = r.run; send.AddRange(r.send);
                    string what = name + " t=" + t;
                    cs.Eq(C.S(e, "phase"), Mission.Name(run.phase), what + " phase");
                    cs.Eq(C.I(e, "i"), run.i, what + " i");
                    cs.Eq(C.IsNull(e, "reason") ? null : C.S(e, "reason"), run.reason, what + " reason");
                    JArr es = C.A(e, "send");
                    cs.Eq(es.Count, send.Count, what + " commands sent");
                    for (int j = 0; j < es.Count && j < send.Count; j++) CompareCommand(cs, what + " send[" + j + "]", (JObj)es[j], send[j], tol);
                }
                cs.Done();
                per.Add(name + " " + Cases.Summary["mission.script." + name]);
            }
            Debug.Log("[contract] mission scripts: " + string.Join(", ", per));
        }

        [Test]
        public void Golden_Integration_TheSquareOnTheSimulator()
        {
            var g = C.Golden("mission");
            var tolO = C.O(g, "tolerance");
            double tolCmd = C.N(tolO, "command"), tolPos = C.N(tolO, "position"), tolTime = C.N(tolO, "time"), tolSpeed = C.N(tolO, "speed");
            var ig = C.O(g, "integration");
            MissionDoc m = Doc(C.O(ig, "mission"));
            double simDt = C.N(ig, "sim_dt"), tick = C.N(ig, "tick");
            var cs = new Cases("mission.integration", tolPos);

            SimState s = FlightSim.Create(22);
            MissionRun run = Mission.CreateRun(m);
            var log = new List<(double t, CommandMessage c, bool ok)>();
            var legs = new List<double[]>(); // leg, start, end, length, arrivalError, altError, meanSpeed
            var samples = new List<double[]>();
            void Send(double t, List<CommandMessage> cmds)
            {
                foreach (CommandMessage c in cmds)
                {
                    SimReply rp = FlightSim.Command(s, c);
                    log.Add((Round(t, 6), c, rp.ok));
                    MissionStepResult a = Mission.Ack(run, c.name, rp.ok, rp.detail, t);
                    run = a.run;
                    if (a.send.Count > 0) Send(t, a.send);
                }
            }
            double legStart = 0, fromN = 0, fromE = 0;
            int steps = (int)Math.Round(120 / simDt), every = (int)Math.Round(tick / simDt);
            double? doneAt = null;
            for (int k = 0; k <= steps; k++)
            {
                double t = Round(k * simDt, 6);
                if (k % every == 0)
                {
                    TelemetryFrame f = FlightSim.Frame(s, Math.Round(t * 1000));
                    MissionRun before = run;
                    MissionStepResult r = k == 0 ? Mission.Control(run, MissionControl.Start, MissionFrame.Of(f), t) : Mission.Step(run, MissionFrame.Of(f), t);
                    run = r.run;
                    if (before.phase == MissionPhase.Leg && (run.phase != MissionPhase.Leg || run.i != before.i))
                    {
                        Waypoint w = m.waypoints[before.i];
                        double length = Math.Sqrt((w.n - fromN) * (w.n - fromN) + (w.e - fromE) * (w.e - fromE));
                        legs.Add(new[] { before.i + 1, Round(legStart, 6), t, Round(length, 6), Math.Sqrt((w.n - f.p[0]) * (w.n - f.p[0]) + (w.e - f.p[1]) * (w.e - f.p[1])), Math.Abs(-f.p[2] - w.alt), length / (t - legStart) });
                    }
                    if (run.phase == MissionPhase.Leg && (before.phase != MissionPhase.Leg || run.i != before.i)) { legStart = t; fromN = run.startN; fromE = run.startE; }
                    Send(t, r.send);
                    if (k % 100 == 0) samples.Add(new[] { t, f.p[0], f.p[1], -f.p[2] });
                    if (run.phase == MissionPhase.Done && !doneAt.HasValue) doneAt = t;
                    if (run.phase == MissionPhase.Failed || (doneAt.HasValue && t >= doneAt.Value + 2)) break;
                }
                FlightSim.Step(s, simDt);
            }

            cs.Case();
            cs.Eq(C.S(ig, "phase"), Mission.Name(run.phase), "final phase");
            cs.True(doneAt.HasValue, "the mission finishes");
            if (doneAt.HasValue) cs.Near(C.N(ig, "done_at"), doneAt.Value, "done_at", tolTime);

            JArr eLegs = C.A(ig, "legs");
            cs.Case();
            cs.Eq(eLegs.Count, legs.Count, "legs");
            for (int k = 0; k < eLegs.Count && k < legs.Count; k++)
            {
                cs.Case();
                var e = (JObj)eLegs[k];
                double[] a = legs[k];
                cs.Eq(C.I(e, "leg"), (int)a[0], "leg " + k + " number");
                cs.Near(C.N(e, "start"), a[1], "leg " + k + " start", tolTime);
                cs.Near(C.N(e, "end"), a[2], "leg " + k + " end", tolTime);
                cs.Near(C.N(e, "length"), a[3], "leg " + k + " length", tolPos);
                cs.Near(C.N(e, "arrivalError"), a[4], "leg " + k + " arrival error", tolPos);
                cs.Near(C.N(e, "altError"), a[5], "leg " + k + " altitude error", tolPos);
                cs.Near(C.N(e, "meanSpeed"), a[6], "leg " + k + " mean speed", tolSpeed);
                // the acceptance test of the specification: arrival under 1 m, 3 +- 0.5 m/s
                cs.True(a[4] < 1.0 && Math.Abs(a[6] - 3) <= 0.5, "leg " + k + " arrives under 1 m at 3 +- 0.5 m/s");
            }

            JArr eSamples = C.A(ig, "samples");
            cs.Case();
            cs.Eq(eSamples.Count, samples.Count, "samples");
            for (int k = 0; k < eSamples.Count && k < samples.Count; k++)
            {
                cs.Case();
                cs.Near(C.V(eSamples[k]), samples[k], "sample " + k, tolPos);
            }

            JArr eLog = C.A(ig, "log");
            cs.Case();
            cs.Eq(eLog.Count, log.Count, "commands in the log");
            for (int k = 0; k < eLog.Count && k < log.Count; k++)
            {
                cs.Case();
                var e = (JObj)eLog[k];
                cs.Near(C.N(e, "t"), log[k].t, "log " + k + " t", 1e-9);
                cs.Eq(C.B(e, "ok"), log[k].ok, "log " + k + " ok");
                CompareCommand(cs, "log " + k, C.O(e, "cmd"), log[k].c, Math.Max(tolCmd, 1e-6));
            }
            cs.Done();
            Assert.Less(doneAt.Value, 90, "the square is flown in under 90 s");
            Debug.Log("[contract] mission integration: done at " + doneAt.Value + " s, " + legs.Count + " legs, " + log.Count + " commands, " + Cases.Summary["mission.integration"]);
        }

        [Test]
        public void Progress_CountsWhatIsLeft()
        {
            var m = new MissionDoc();
            m.waypoints.Add(new Waypoint(10, 0, 5, 2, 4));
            m.waypoints.Add(new Waypoint(10, 10, 5, 5));
            MissionRun run = Mission.CreateRun(m);
            MissionProgress p = Mission.Progress(run, new MissionFrame(0, 0, 0, true, false));
            Assert.AreEqual(1, p.leg); Assert.AreEqual(2, p.legs);
            Assert.AreEqual(20, p.metres, 1e-9);
            Assert.AreEqual(10 / 2.0 + 4 + 10 / 5.0, p.seconds, 1e-9);
            run = Mission.Control(run, MissionControl.Start, new MissionFrame(0, 0, -5, false, true), 0).run;
            p = Mission.Progress(run, new MissionFrame(4, 0, -5, false, true));
            Assert.AreEqual(16, p.metres, 1e-9);
            Assert.AreEqual(new MissionProgress().legs, Mission.Progress(Mission.CreateRun(new MissionDoc()), null).legs);
        }
    }
}
