// Lote 1a, tool 10: «motor cortado» of the simulator (Telemetry/Sim/MotorFail.cs and its coupling in FlightSim) against contracts/golden/motorfail.json, produced by the web's
// own src/shared/motorfail.ts + flightsim.ts (tools/contracts/golden_motorfail.ts): the table of constants, the four motors (thrusts, torques, signs), the three scripts of the
// rigid body (state every 0.1 s, 1e-6), the five scripts coupled in the simulator (cut, restore, impact), and the restore condition. Plus the acceptance invariants of the spec
// (section 10): loses > 1 m in 1.5 s, yawRate < 0 and growing, impact before 4 s; with compensation roll and pitch < 1 deg, drift < 2 m, descent 2.09 +- 0.1 m/s; restore at 6 m is stable in < 3 s.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace WebGL.Telemetry.Tests
{
    using JObj = Dictionary<string, object>;

    public class MotorFailTests
    {
        const double Dt = 1.0 / MotorFailParams.RateHz;
        const int Every = 12;

        static MotorFailSnapshot Hover(double alt, double[] v = null) => new MotorFailSnapshot { p = new double[] { 0, 0, -alt }, v = v ?? new double[3], roll = 0, pitch = 0, yaw = 0, yawRate = 0 };

        static MotorFailState Run(MotorFailSnapshot snap, int motor, bool comp, int steps, List<MotorFailState> states = null, List<double> times = null)
        {
            var s = MotorFail.Start(snap, motor, comp);
            states?.Add(Copy(s));
            int n = 0;
            while (n < steps && !s.landed)
            {
                MotorFail.Step(s, Dt);
                n++;
                if (n % Every == 0 || s.landed) states?.Add(Copy(s));
            }
            if (times != null) times.Add(n);
            return s;
        }

        static MotorFailState Copy(MotorFailState s) => new MotorFailState
        {
            t = s.t, q = (double[])s.q.Clone(), w = (double[])s.w.Clone(), p = (double[])s.p.Clone(), v = (double[])s.v.Clone(), motor = s.motor, comp = s.comp, landed = s.landed, impact = s.impact,
        };

        static void SameRotation(Cases cs, double[] exp, double[] got, string what, double tol)
        {
            // q and -q are the same rotation
            double dPlus = 0, dMinus = 0;
            for (int i = 0; i < 4; i++) { dPlus = Math.Max(dPlus, Math.Abs(exp[i] - got[i])); dMinus = Math.Max(dMinus, Math.Abs(exp[i] + got[i])); }
            cs.True(Math.Min(dPlus, dMinus) <= tol, what + ": q differs by " + Math.Min(dPlus, dMinus));
        }

        [Test]
        public void Golden_Params_AreTheWebTable()
        {
            var g = C.Golden("motorfail");
            var p = C.O(g, "params");
            Assert.AreEqual(C.N(p, "mass"), MotorFailParams.Mass);
            Assert.AreEqual(C.N(p, "g"), MotorFailParams.Gravity);
            Assert.AreEqual(C.N(p, "thrust_max"), MotorFailParams.ThrustMax);
            Assert.AreEqual(C.N(p, "thrust_hover"), MotorFailParams.ThrustHover, 1e-8);
            Assert.AreEqual(C.N(p, "hover_throttle"), MotorFailParams.HoverThrottle);
            Assert.AreEqual(0.62, SimLimits.HoverThrottle, "the hover command measured in the real flight, one source");
            Assert.AreEqual(C.N(p, "ixx"), MotorFailParams.Ixx);
            Assert.AreEqual(C.N(p, "iyy"), MotorFailParams.Iyy);
            Assert.AreEqual(C.N(p, "izz"), MotorFailParams.Izz);
            Assert.AreEqual(C.N(p, "yaw_drag_quad"), MotorFailParams.YawDragQuad);
            Assert.AreEqual(C.N(p, "tilt_damping"), MotorFailParams.TiltDamping);
            Assert.AreEqual(C.N(p, "linear_drag"), MotorFailParams.LinearDrag);
            Assert.AreEqual(C.N(p, "max_tilt_deg"), MotorFailParams.MaxTiltDeg);
            Assert.AreEqual(C.N(p, "comp_level_tau"), MotorFailParams.CompLevelTau);
            Assert.AreEqual(C.N(p, "restore_min_altitude"), MotorFailParams.RestoreMinAltitude);
            Assert.AreEqual(C.N(p, "impact_speed"), MotorFailParams.ImpactSpeed);
            Assert.AreEqual(C.N(p, "rate_hz"), MotorFailParams.RateHz);
            var rotors = C.A(p, "rotors");
            for (int i = 0; i < 4; i++)
            {
                var r = (JObj)rotors[i];
                Assert.AreEqual(C.I(r, "motor"), MotorFailParams.Rotors[i].motor);
                Assert.AreEqual(C.N(r, "px"), MotorFailParams.Rotors[i].px);
                Assert.AreEqual(C.N(r, "py"), MotorFailParams.Rotors[i].py);
                Assert.AreEqual(C.N(r, "km"), MotorFailParams.Rotors[i].km);
            }
            var opp = C.O(p, "opposite");
            for (int m = 1; m <= 4; m++) Assert.AreEqual(C.I(opp, m.ToString()), MotorFailParams.Opposite(m));
        }

        [Test]
        public void Golden_TheFourMotors_ThrustsTorquesAndYawSign()
        {
            var g = C.Golden("motorfail");
            double tol = C.N(g, "tolerance");
            var cs = new Cases("motorfail.motors", tol);
            foreach (JObj m in C.A(g, "motors"))
            {
                cs.Case();
                int motor = C.I(m, "motor");
                string at = "motor " + motor;
                cs.Near(C.V(m, "u"), MotorFail.Command(motor, false), at + " u");
                cs.Near(C.V(m, "u_comp"), MotorFail.Command(motor, true), at + " u_comp");
                MotorFail.Torques(motor, false, out double[] th, out _, out double[] tau);
                MotorFail.Torques(motor, true, out double[] thc, out _, out double[] tauc);
                cs.Near(C.V(m, "thrust_n"), th, at + " thrust", 1e-7);
                cs.Near(C.V(m, "tau_nm"), tau, at + " tau", 1e-7);
                cs.Near(C.V(m, "thrust_comp_n"), thc, at + " thrust comp", 1e-7);
                cs.Near(C.V(m, "tau_comp_nm"), tauc, at + " tau comp", 1e-7);
                cs.Eq(C.I(m, "yaw_rate_sign"), (int)Math.Sign(tau[2]), at + " yaw sign");
                cs.Near(C.N(m, "yaw_rate_terminal_rad_s"), MotorFail.YawTerminal(tau[2]), at + " yaw terminal", 1e-7);
                cs.Near(C.N(m, "yaw_rate_terminal_comp_rad_s"), MotorFail.YawTerminal(tauc[2]), at + " yaw terminal comp", 1e-7);
                var s = Run(Hover(50), motor, false, 24);
                var a = C.O(m, "after_0_2_s");
                cs.Near(C.V(a, "w"), s.w, at + " w after 0.2 s");
                double[] arm = MotorFail.Rotate(s.q, new[] { MotorFailParams.Rotors[motor - 1].px, MotorFailParams.Rotors[motor - 1].py, 0 });
                cs.Near(C.N(a, "cut_rotor_arm_down_m"), arm[2], at + " arm down", 1e-7);
                cs.Near(C.N(a, "tilt_deg"), MotorFail.Tilt(s.q) * 180 / Math.PI, at + " tilt", 1e-6);
                // the invariants of the spec (d): with the motor out the body turns the way km says (motor 1 out: counter-clockwise seen from above = yawRate < 0)
                cs.True(Math.Sign(s.w[2]) == -Math.Sign(MotorFailParams.Rotors[motor - 1].km), at + ": yaw sign follows km");
            }
            cs.Done();
        }

        [Test]
        public void Golden_Scripts_OfTheRigidBody()
        {
            var g = C.Golden("motorfail");
            double tol = C.N(g, "tolerance");
            var perScript = new List<string>();
            int idx = 0;
            foreach (JObj sc in C.A(g, "scripts"))
            {
                string name = C.S(sc, "name");
                var cs = new Cases("motorfail.script" + idx++, tol);
                var snapO = C.O(sc, "snapshot");
                var snap = new MotorFailSnapshot { p = C.V(snapO, "p"), v = C.V(snapO, "v"), roll = C.N(snapO, "roll"), pitch = C.N(snapO, "pitch"), yaw = C.N(snapO, "yaw"), yawRate = C.N(snapO, "yawRate") };
                var states = new List<MotorFailState>();
                var steps = new List<double>();
                MotorFailState last = Run(snap, C.I(sc, "motor"), C.B(sc, "comp"), C.I(sc, "steps"), states, steps);
                var exp = C.A(sc, "states");
                cs.Case();
                cs.Eq(exp.Count, states.Count, name + " number of records");
                cs.Eq(C.I(sc, "steps"), (int)steps[0], name + " steps");
                for (int i = 0; i < Math.Min(exp.Count, states.Count); i++)
                {
                    var e = (JObj)exp[i];
                    var s = states[i];
                    cs.Case();
                    string at = name + " #" + i;
                    cs.Near(C.N(e, "t"), s.t, at + " t", 1e-7);
                    SameRotation(cs, C.V(e, "q"), s.q, at, tol);
                    cs.Near(C.V(e, "w"), s.w, at + " w");
                    cs.Near(C.V(e, "p"), s.p, at + " p");
                    cs.Near(C.V(e, "v"), s.v, at + " v");
                    cs.Eq(C.B(e, "landed"), s.landed, at + " landed");
                }
                cs.Case();
                if (C.IsNull(sc, "impact")) cs.True(last.impact == null, name + " no impact");
                else
                {
                    var im = C.O(sc, "impact");
                    cs.True(last.impact != null, name + " impact expected");
                    if (last.impact != null)
                    {
                        cs.Near(C.N(im, "speed"), last.impact.speed, name + " impact speed", 1e-7);
                        cs.Eq(C.B(im, "hard"), last.impact.hard, name + " hard");
                        cs.Eq(C.S(im, "severity") == "critical", last.impact.critical, name + " severity");
                        cs.Eq(C.S(im, "text"), last.impact.text, name + " text");
                    }
                }
                cs.Near(C.V(sc, "u_before_impact"), MotorFail.Command(C.I(sc, "motor"), C.B(sc, "comp")), name + " u before");
                cs.Near(C.V(sc, "u_after"), MotorFail.U(last), name + " u after");
                if (sc.ContainsKey("checks") && sc["checks"] != null)
                {
                    var ck = C.O(sc, "checks");
                    MotorFail.Euler(last.q, out double roll, out double pitch, out _);
                    cs.Near(C.N(ck, "descent_m_s"), last.v[2], name + " descent", 1e-7);
                    cs.Near(C.N(ck, "drift_m"), Math.Sqrt(last.p[0] * last.p[0] + last.p[1] * last.p[1]), name + " drift", 1e-7);
                    cs.Near(C.N(ck, "roll_deg"), roll * 180 / Math.PI, name + " roll", 1e-6);
                    cs.Near(C.N(ck, "pitch_deg"), pitch * 180 / Math.PI, name + " pitch", 1e-6);
                    // the spec (c): with compensation roll and pitch stay under 1 deg, drift under 2 m, the descent converges on 2.09 +- 0.1 m/s
                    cs.True(Math.Abs(roll) < Math.PI / 180 && Math.Abs(pitch) < Math.PI / 180, name + " roll and pitch < 1 deg");
                    cs.True(Math.Sqrt(last.p[0] * last.p[0] + last.p[1] * last.p[1]) < 2, name + " drift < 2 m");
                    cs.True(Math.Abs(last.v[2] - 2.09) < 0.1, name + " descent 2.09 +- 0.1 m/s (" + last.v[2].ToString("F3") + ")");
                }
                try { cs.Done(); } catch (AssertionException) { perScript.Add(name + " FAILED"); throw; }
                perScript.Add(name + " " + Cases.Summary["motorfail.script" + (idx - 1)]);
            }
            Debug.Log("[contract] motorfail scripts: " + string.Join(", ", perScript));
        }

        [Test]
        public void Invariants_Spec10_NoCompensation_MotorOneAt10m()
        {
            var states = new List<MotorFailState>();
            Run(Hover(10), 1, false, 6 * 120, states);
            double Alt(MotorFailState s) => -s.p[2];
            MotorFailState At(double t) => states.OrderBy(s => Math.Abs(s.t - t)).First();
            Assert.Greater(10 - Alt(At(1.5)), 1.0, "loses more than 1 m in 1.5 s");
            Assert.Less(At(0.5).w[2], 0, "yawRate < 0 (motor 1 out turns the body counter-clockwise from above)");
            // |yawRate| grows during the first 2 s (every 0.1 s), until the quadratic drag balances it
            for (int i = 1; i < states.Count && states[i].t <= 2.0 + 1e-9; i++)
                Assert.GreaterOrEqual(Math.Abs(states[i].w[2]), Math.Abs(states[i - 1].w[2]) - 1e-9, "t = " + states[i].t);
            Assert.Less(At(2.0).w[2], 0, "and it stays negative"); 
            Assert.IsTrue(states.Last().landed, "it hit the ground");
            Assert.Less(states.Last().t, 4.0, "impact before 4 s");
            // tilt towards the cut rotor: the arm of motor 1 (front right) goes down
            var s02 = At(0.2);
            double[] arm = MotorFail.Rotate(s02.q, new[] { MotorFailParams.Rotors[0].px, MotorFailParams.Rotors[0].py, 0 });
            Assert.Greater(arm[2], 0, "the cut rotor drops (z down positive)");
        }

        [Test]
        public void Golden_Restore_Condition()
        {
            var g = C.Golden("motorfail");
            var rs = C.O(g, "restore");
            var at6 = C.O(rs, "at_6_m");
            var snapO = C.O(at6, "snapshot");
            var snap = new MotorFailSnapshot { p = C.V(snapO, "p"), v = C.V(snapO, "v") };
            var e = Run(snap, C.I(at6, "motor"), C.B(at6, "comp"), C.I(at6, "steps"));
            MotorFailRestore r = MotorFail.Restore(e);
            Assert.IsNotNull(r);
            var o = C.O(at6, "out");
            var cs = new Cases("motorfail.restore", 1e-6);
            cs.Case();
            cs.Near(C.V(o, "p"), r.p, "p"); cs.Near(C.V(o, "v"), r.v, "v");
            cs.Near(C.N(o, "roll"), r.roll, "roll"); cs.Near(C.N(o, "pitch"), r.pitch, "pitch"); cs.Near(C.N(o, "yaw"), r.yaw, "yaw"); cs.Near(C.N(o, "yawRate"), r.yawRate, "yawRate");
            var t = C.O(o, "target");
            cs.Near(C.N(t, "n"), r.target.n, "target n"); cs.Near(C.N(t, "e"), r.target.e, "target e"); cs.Near(C.N(t, "alt"), r.target.alt, "target alt");
            double speed = Math.Sqrt(r.v[0] * r.v[0] + r.v[1] * r.v[1] + r.v[2] * r.v[2]);
            var sb = C.O(at6, "settle_bound");
            cs.Near(C.N(sb, "speed_m_s"), speed, "speed"); cs.Near(C.N(sb, "speed_after_3_s_m_s"), speed * Math.Exp(-3 / 0.55), "speed after 3 s");
            cs.True(speed * Math.Exp(-3 / 0.55) < C.N(sb, "limit_m_s"), "the kinematic model settles under 0.05 m/s in 3 s");
            // below the minimum altitude and after an impact: nothing to restore
            var low = C.O(rs, "below_min_altitude");
            var lo = C.O(low, "snapshot");
            var l = Run(new MotorFailSnapshot { p = C.V(lo, "p"), v = C.V(lo, "v") }, C.I(low, "motor"), C.B(low, "comp"), C.I(low, "steps"));
            cs.Near(C.N(low, "altitude_m"), -l.p[2], "altitude of the low script");
            cs.True(MotorFail.Restore(l) == null, "no restore at 0.5 m or less");
            var b = Run(Hover(10), 1, false, 6 * 120);
            cs.True(MotorFail.Restore(b) == null, "no restore after the impact");
            cs.Done();
        }

        sealed class IntegratedResult
        {
            public SimState S;
            public SimReply Cut;
            public int Steps;
            public List<(double t, string p, double[] v, double roll, double pitch, double yaw, double yawRate, double[] u, string mode, bool armed, bool landed, int cut, double[] pos)> States = new List<(double, string, double[], double, double, double, double, double[], string, bool, bool, int, double[])>();
            public List<(EventSeverity sev, string text, double t)> Events = new List<(EventSeverity, string, double)>();
            public int RestoreStep = -1; public double RestoreT, RestoreAlt, RestoreDescent; public SimReply RestoreReply;
            public double MinAltAfterRestore = double.NaN, Settled = double.NaN;
        }

        static (double t, string p, double[] v, double roll, double pitch, double yaw, double yawRate, double[] u, string mode, bool armed, bool landed, int cut, double[] pos) Sample(SimState s, double t) =>
            (t, null, (double[])s.v.Clone(), s.roll, s.pitch, s.yaw, s.yawRate, (double[])s.u.Clone(), SimGolden.ModeName(s.mode), s.armed, s.landed, s.fail != null ? s.fail.motor : 0, (double[])s.p.Clone());

        /// <summary>The web's runIntegrated: take off to `alt`, 12 s of hover, motor_off, restore at the first step with altitude &lt;= restoreAt (negative = -restoreAt s after the cut), `after` s more.</summary>
        static IntegratedResult RunIntegrated(double alt, int motor, bool comp, double? restoreAt, double after)
        {
            var s = FlightSim.Create();
            FlightSim.SetConfig(s, new SimConfigPatch { faultComp = comp });
            FlightSim.Command(s, ClientMessage.Takeoff(alt));
            for (int i = 0; i < 12 * 120; i++) FlightSim.Step(s, Dt);
            var r = new IntegratedResult { S = s, Cut = FlightSim.Command(s, ClientMessage.MotorOff(motor)) };
            r.States.Add(Sample(s, 0));
            int n = 0, end = int.MaxValue;
            double minAlt = double.PositiveInfinity;
            double? settled = null;
            bool restored = false;
            while (n < end && n < 30 * 120 && !s.landed)
            {
                FlightSim.Step(s, Dt);
                n++;
                bool mark = s.landed;
                foreach (SimEvent e in FlightSim.TakeEvents(s)) r.Events.Add((e.severity, e.text, Math.Round(n * Dt * 1e8) / 1e8));
                if (restored)
                {
                    minAlt = Math.Min(minAlt, -s.p[2]);
                    bool still = Math.Sqrt(s.v[0] * s.v[0] + s.v[1] * s.v[1] + s.v[2] * s.v[2]) < 0.3 && Math.Abs(s.roll) < 0.035 && Math.Abs(s.pitch) < 0.035 && Math.Abs(s.yawRate) < 0.05;
                    if (!still) settled = null;
                    else if (settled == null) settled = (n - r.RestoreStep) * Dt;
                }
                else if (restoreAt.HasValue && !s.landed && (restoreAt.Value < 0 ? n >= (int)Math.Round(-restoreAt.Value * 120) : -s.p[2] <= restoreAt.Value))
                {
                    r.RestoreStep = n;
                    r.RestoreT = n * Dt;
                    r.RestoreAlt = -s.p[2];
                    r.RestoreDescent = s.v[2];
                    r.RestoreReply = FlightSim.Command(s, ClientMessage.Restore());
                    end = n + (int)Math.Round(after * 120);
                    mark = true;
                    restored = true;
                }
                if (n % Every == 0 || mark) r.States.Add(Sample(s, n * Dt));
            }
            r.Steps = n;
            if (restored) { r.MinAltAfterRestore = minAlt; r.Settled = settled ?? double.NaN; }
            return r;
        }

        [Test]
        public void Golden_Integrated_InTheSimulator()
        {
            var g = C.Golden("motorfail");
            double tol = C.N(g, "tolerance");
            var stable = C.O(g, "stable");
            Assert.AreEqual(0.3, C.N(stable, "speed_m_s")); Assert.AreEqual(0.035, C.N(stable, "tilt_rad")); Assert.AreEqual(0.05, C.N(stable, "yaw_rate_rad_s"));
            var perScript = new List<string>();
            int idx = 0;
            foreach (JObj sc in C.A(g, "integrated"))
            {
                string name = C.S(sc, "name");
                var cs = new Cases("motorfail.integrated" + idx++, tol);
                double? restoreAt = C.IsNull(sc, "restore_at_m") ? (double?)null : C.N(sc, "restore_at_m");
                var r = RunIntegrated(C.N(sc, "takeoff_altitude_m"), C.I(sc, "motor"), C.B(sc, "comp"), restoreAt, C.N(sc, "after_s"));
                cs.Case();
                var cut = C.O(sc, "cut_reply");
                cs.Eq(C.B(cut, "ok"), r.Cut.ok, name + " cut ok"); cs.Eq(C.S(cut, "event"), r.Cut.evt, name + " cut event");
                cs.Eq(C.I(sc, "steps"), r.Steps, name + " steps");
                if (C.IsNull(sc, "restore")) cs.True(r.RestoreStep < 0, name + " not restored");
                else
                {
                    var re = C.O(sc, "restore");
                    cs.Eq(C.I(re, "step"), r.RestoreStep, name + " restore step");
                    cs.Near(C.N(re, "altitude_m"), r.RestoreAlt, name + " restore altitude");
                    cs.Near(C.N(re, "descent_m_s"), r.RestoreDescent, name + " restore descent");
                    var rr = C.O(re, "reply");
                    cs.Eq(C.B(rr, "ok"), r.RestoreReply.ok, name + " restore ok"); cs.Eq(C.S(rr, "event"), r.RestoreReply.evt, name + " restore event");
                    cs.Near(C.N(sc, "min_altitude_after_restore_m"), r.MinAltAfterRestore, name + " min altitude after restore", 1e-6);
                    if (C.IsNull(sc, "settled_after_s")) cs.True(double.IsNaN(r.Settled), name + " never settles");
                    else cs.Near(C.N(sc, "settled_after_s"), r.Settled, name + " settled after", 1e-6);
                }
                var ev = C.A(sc, "events");
                cs.Eq(ev.Count, r.Events.Count, name + " events");
                for (int i = 0; i < Math.Min(ev.Count, r.Events.Count); i++)
                {
                    var e = (JObj)ev[i];
                    cs.Eq(C.S(e, "text"), r.Events[i].text, name + " event text");
                    cs.Eq(C.S(e, "severity") == "critical" ? EventSeverity.Critical : EventSeverity.Warning, r.Events[i].sev, name + " event severity");
                    cs.Near(C.N(e, "t"), r.Events[i].t, name + " event t", 1e-7);
                }
                var fin = C.O(sc, "final");
                cs.Eq(C.S(fin, "mode"), SimGolden.ModeName(r.S.mode), name + " final mode"); cs.Eq(C.B(fin, "armed"), r.S.armed, name + " final armed"); cs.Eq(C.B(fin, "landed"), r.S.landed, name + " final landed");
                var exp = C.A(sc, "states");
                cs.Eq(exp.Count, r.States.Count, name + " records");
                for (int i = 0; i < Math.Min(exp.Count, r.States.Count); i++)
                {
                    var e = (JObj)exp[i];
                    var s = r.States[i];
                    cs.Case();
                    string at = name + " #" + i;
                    cs.Near(C.N(e, "t"), s.t, at + " t", 1e-7);
                    cs.Near(C.V(e, "p"), s.pos, at + " p"); cs.Near(C.V(e, "v"), s.v, at + " v");
                    cs.Near(C.N(e, "roll"), s.roll, at + " roll"); cs.Near(C.N(e, "pitch"), s.pitch, at + " pitch"); cs.Near(C.N(e, "yaw"), s.yaw, at + " yaw"); cs.Near(C.N(e, "yawRate"), s.yawRate, at + " yawRate");
                    cs.Near(C.V(e, "u"), s.u, at + " u");
                    cs.Eq(C.S(e, "mode"), s.mode, at + " mode"); cs.Eq(C.B(e, "armed"), s.armed, at + " armed"); cs.Eq(C.B(e, "landed"), s.landed, at + " landed"); cs.Eq(C.I(e, "cut"), s.cut, at + " cut");
                }
                try { cs.Done(); } catch (AssertionException) { perScript.Add(name + " FAILED"); throw; }
                perScript.Add(name + " " + Cases.Summary["motorfail.integrated" + (idx - 1)]);
            }
            Debug.Log("[contract] motorfail integrated: " + string.Join(", ", perScript));
        }

        [Test]
        public void Integrated_Restore_At6m_IsInHoldAndStableUnder3s()
        {
            var r = RunIntegrated(6, 1, false, -0.4, 4);
            Assert.IsTrue(r.RestoreReply.ok);
            Assert.AreEqual(SimMode.Hold, r.S.mode);
            Assert.IsTrue(r.S.armed && !r.S.landed);
            Assert.Less(r.Settled, 3.0, "stable (speed < 0.3 m/s, tilt < 2 deg) in under 3 s");
        }

        [Test]
        public void SimulatorCommands_WhileACutIsActive()
        {
            var s = FlightSim.Create();
            FlightSim.Command(s, ClientMessage.Takeoff(10));
            for (int i = 0; i < 12 * 120; i++) FlightSim.Step(s, Dt);
            Assert.IsTrue(FlightSim.Command(s, ClientMessage.MotorOff(2)).ok);
            SimReply r = FlightSim.Command(s, ClientMessage.FlyTo(10, 10, 10));
            Assert.IsFalse(r.ok);
            Assert.AreEqual("motor cortado: el simulador no puede maniobrar", r.detail);
            Assert.IsTrue(FlightSim.Command(s, ClientMessage.Hold()).ok, "hold only confirms");
            Assert.AreEqual(SimMode.Hold, s.mode);
            SimReply wrong = FlightSim.Command(s, ClientMessage.Restore(3));
            Assert.IsFalse(wrong.ok);
            Assert.AreEqual("el motor 3 no está cortado (el cortado es el 2)", wrong.detail);
            double rollBefore = s.roll;
            FlightSim.Manual(s, new SimSticks(1000, 0, 500, 0));
            FlightSim.Step(s, Dt);
            Assert.AreNotEqual(SimMode.Manual, s.mode, "the sticks are ignored");
            Assert.AreEqual(0, s.u[1], "u of the cut motor");
            Assert.AreEqual(0, Math.Round(s.u[1] * 920 * 1)); // rpm of a cut motor is 0 (u = 0)
            TelemetryFrame f = FlightSim.Frame(s, 1000);
            Assert.AreEqual(0, f.u[1]); Assert.AreEqual(0, f.rpm[1]); Assert.Greater(f.rpm[0], 0);
        }

        [Test]
        public void ADroneOnTheGround_OnlyMarksTheCut_AndRefusesToTakeOffUntilRestore()
        {
            var s = FlightSim.Create();
            SimReply r = FlightSim.Command(s, ClientMessage.MotorOff(1));
            Assert.IsTrue(r.ok);
            Assert.AreEqual("Motor 1 cortado (simulado) · en tierra", r.evt);
            SimReply t = FlightSim.Command(s, ClientMessage.Takeoff(5));
            Assert.IsFalse(t.ok);
            Assert.AreEqual(FlightSim.MotorCutDetail, t.detail);
            SimReply rs = FlightSim.Command(s, ClientMessage.Restore());
            Assert.IsTrue(rs.ok);
            Assert.AreEqual("Motor 1 restaurado (simulado)", rs.evt);
            Assert.IsTrue(FlightSim.Command(s, ClientMessage.Takeoff(5)).ok);
            Assert.AreEqual("no hay ningún motor cortado", FlightSim.Command(s, ClientMessage.Restore()).detail);
        }

        [Test]
        public void InvalidMotor_IsRefused()
        {
            var s = FlightSim.Create();
            SimReply r = FlightSim.Command(s, new CommandMessage { name = "motor_off", motor = 5 });
            Assert.IsFalse(r.ok);
            Assert.AreEqual("motor no válido: debe ser 1, 2, 3 o 4", r.detail);
        }

        [Test]
        public void TheCompensationSwitch_ActsOnACutMotorLive()
        {
            var s = FlightSim.Create();
            FlightSim.Command(s, ClientMessage.Takeoff(20));
            for (int i = 0; i < 12 * 120; i++) FlightSim.Step(s, Dt);
            FlightSim.Command(s, ClientMessage.MotorOff(1));
            Assert.IsFalse(s.fail.comp);
            Assert.IsTrue(FlightSim.SetConfig(s, new SimConfigPatch { faultComp = true }).ok);
            Assert.IsTrue(s.fail.comp, "the didactic comparison is live");
            Assert.IsTrue(s.cfg.faultComp);
        }

        [Test]
        public void ImpactEvents_ReachTheClientThroughTheSource()
        {
            var src = new SimTelemetrySource { LocalNowMs = () => 1000, WallNowMs = () => 1.8e12 };
            var client = new TelemetryClient(src) { Log = null };
            client.Start();
            double local = 1000;
            void Run(double sec) { for (int i = 0; i < (int)(sec * 60); i++) { local += 1000.0 / 60; src.LocalNowMs = () => local; client.Update(local, 1.0 / 60); } }
            Run(0.2);
            Assert.IsTrue(client.Hello.Accepts("motor_off") && client.Hello.Accepts("restore"), "the simulator announces both orders");
            client.Send(ClientMessage.Takeoff(8), out _); client.Update(local, 0);
            Run(14);
            Assert.IsTrue(client.Send(ClientMessage.MotorOff(1), out _)); client.Update(local, 0);
            Run(4);
            Assert.IsTrue(client.Displayed.landed);
            bool impact = false, cut = false;
            for (int i = 0; i < client.Events.Count; i++)
            {
                string t = client.Events[i].text;
                if (t.StartsWith("Impacto simulado a ", StringComparison.Ordinal)) impact = true;
                if (t.Contains("Motor 1 cortado (simulado)")) cut = true;
            }
            Assert.IsTrue(impact, "the impact reaches the event list");
            Assert.IsTrue(cut);
        }
    }
}
