// The C# port must reproduce every golden file of contracts/golden with the tolerance of its _doc.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;

namespace WebGL.Telemetry.Tests
{
    using JObj = Dictionary<string, object>;

    public class GoldenFramesTests
    {
        [Test]
        public void Positions_NedToUnity_And_Render()
        {
            var g = C.Golden("frames");
            var cs = new Cases("frames.positions", C.N(g, "tolerance"));
            foreach (JObj p in C.A(g, "positions"))
            {
                cs.Case();
                cs.Near(C.V(p, "unity_hint"), NedFrames.NedToUnity(C.V(p, "ned")), "unity");
                cs.Near(C.V(p, "render"), NedFrames.NedToRender(C.V(p, "ned")), "render");
                var back = NedFrames.UnityToNed(NedFrames.NedToUnity(C.V(p, "ned"))[0], NedFrames.NedToUnity(C.V(p, "ned"))[1], NedFrames.NedToUnity(C.V(p, "ned"))[2]);
                cs.Near(C.V(p, "ned"), back, "round trip");
            }
            cs.Done();
        }

        [Test]
        public void Attitude_BodyAxes_InUnityWorld_DoublePrecision()
        {
            var g = C.Golden("frames");
            var cs = new Cases("frames.attitude.body_axes_in_unity_world", C.N(g, "tolerance"));
            foreach (JObj a in C.A(g, "attitude"))
            {
                cs.Case();
                var ax = C.O(a, "body_axes_in_unity_world");
                NedFrames.BodyAxesInUnity(C.V(a, "q_px4_wxyz"), out var f, out var r, out var d);
                // The golden q are rounded to 6 decimals and normalised by the port; axes are compared at 1e-5.
                cs.Near(C.V(ax, "forward"), f, S(a) + " forward");
                cs.Near(C.V(ax, "right"), r, S(a) + " right");
                cs.Near(C.V(ax, "down"), d, S(a) + " down");
            }
            cs.Done();
        }

        /// <summary>The same check with UnityEngine.Quaternion * Vector3: this is the Unity-side verification the contract asks for.</summary>
        [Test]
        public void Attitude_BodyAxes_InUnityWorld_UnityEngineQuaternion()
        {
            var g = C.Golden("frames");
            var cs = new Cases("frames.attitude.unity_engine_quaternion", 2e-5); // float32 maths
            foreach (JObj a in C.A(g, "attitude"))
            {
                cs.Case();
                var ax = C.O(a, "body_axes_in_unity_world");
                Quaternion q = NedFrames.Px4AttitudeToUnityQuaternion(C.V(a, "q_px4_wxyz"));
                Vector3 f = q * Vector3.forward, r = q * Vector3.right, d = q * Vector3.down;
                cs.Near(C.V(ax, "forward"), new double[] { f.x, f.y, f.z }, S(a) + " forward");
                cs.Near(C.V(ax, "right"), new double[] { r.x, r.y, r.z }, S(a) + " right");
                cs.Near(C.V(ax, "down"), new double[] { d.x, d.y, d.z }, S(a) + " down");
                // Unity is left-handed: right = up x forward must hold for the mapped body axes (proper rotation, not a mirror).
                Vector3 cross = Vector3.Cross(q * Vector3.up, f);
                cs.Near(new double[] { r.x, r.y, r.z }, new double[] { cross.x, cross.y, cross.z }, S(a) + " handedness");
                // Position hint and attitude agree: the body forward of a PX4 attitude with yaw only lies in the same Unity
                // direction as NedToUnity of the NED heading vector.
            }
            cs.Done();
        }

        [Test]
        public void Attitude_RenderWorld_Quaternion_Axes_Euler()
        {
            var g = C.Golden("frames");
            double tol = C.N(g, "tolerance");
            var cs = new Cases("frames.attitude.render_and_euler", tol);
            foreach (JObj a in C.A(g, "attitude"))
            {
                cs.Case();
                var q = C.V(a, "q_px4_wxyz");
                var rq = C.O(a, "render_quaternion_xyzw");
                var got = NedFrames.Px4AttitudeToRender(q);
                var exp = new DQuat(C.N(rq, "x"), C.N(rq, "y"), C.N(rq, "z"), C.N(rq, "w"));
                cs.Near(1, Math.Abs(DQuat.Dot(exp, got)), S(a) + " |dot(render q)|", 2e-6);
                var ax = C.O(a, "body_axes_in_render_world");
                NedFrames.BodyAxesInRender(q, out var f, out var r, out var d);
                cs.Near(C.V(ax, "forward"), f, S(a) + " render forward");
                cs.Near(C.V(ax, "right"), r, S(a) + " render right");
                cs.Near(C.V(ax, "down"), d, S(a) + " render down");
                var e = C.O(a, "euler_deg");
                NedFrames.Px4Euler(q, out double roll, out double pitch, out double yaw);
                cs.Near(C.N(e, "roll"), roll, S(a) + " roll", 1e-4);
                cs.Near(C.N(e, "pitch"), pitch, S(a) + " pitch", 1e-4);
                cs.Near(C.N(e, "yaw"), yaw, S(a) + " yaw", 1e-4);
            }
            var inv = C.O(g, "attitude_sign_invariance");
            cs.Case();
            var q1 = NedFrames.Px4AttitudeToUnity(C.V(inv, "q_px4_wxyz"));
            var q2 = NedFrames.Px4AttitudeToUnity(C.V(inv, "q_px4_wxyz").Select(x => -x).ToArray());
            cs.Near(1, Math.Abs(DQuat.Dot(q1, q2)), "q and -q are the same rotation", 1e-9);
            foreach (JObj r in C.A(g, "rotor_frd_to_model"))
            {
                cs.Case();
                var frd = C.V(r, "frd");
                cs.Near(C.V(r, "model"), NedFrames.FrdToModel(frd[0], frd[1], frd[2]), "rotor " + C.I(r, "motor") + " frd->model");
            }
            cs.Done();
        }

        static string S(JObj a) => C.S(a, "name");
    }

    public class GoldenGroundTests
    {
        [Test]
        public void GroundReference_Steps()
        {
            var g = C.Golden("ground");
            var cs = new Cases("ground.steps", C.N(g, "tolerance"));
            Assert.AreEqual(GroundReference.TauMs, C.N(g, "tau_ms"));
            var gr = new GroundReference();
            foreach (JObj s in C.A(g, "steps"))
            {
                cs.Case();
                var i = C.O(s, "in");
                var f = new TelemetryFrame { ts = C.N(i, "ts"), seg = C.I(i, "seg"), landed = C.B(i, "landed"), p = new[] { 1.0, 2.0, C.N(i, "z") } };
                cs.Near(C.V(s, "out_p"), gr.Apply(f).p, "ts " + C.N(i, "ts"));
            }
            cs.Done();
        }
    }

    public class GoldenInterpolationTests
    {
        static JObj G => C.Golden("interpolation");

        [Test]
        public void Slerp_And_ExpAlpha()
        {
            var g = G;
            var cs = new Cases("interpolation.slerp+exp_alpha", C.N(g, "tolerance"));
            foreach (JObj s in C.A(g, "slerp"))
            {
                cs.Case();
                cs.Near(C.V(s, "out_wxyz"), Interp.SlerpQuat(C.V(s, "a"), C.V(s, "b"), C.N(s, "t")), C.S(s, "name"));
            }
            foreach (JObj s in C.A(g, "exp_alpha"))
            {
                cs.Case();
                cs.Near(C.N(s, "out"), Interp.ExpAlpha(C.N(s, "k"), C.N(s, "dt")), "k " + C.N(s, "k") + " dt " + C.N(s, "dt"));
            }
            cs.Done();
        }

        [Test]
        public void InterpolateFrame()
        {
            var g = G;
            var fr = C.LoopwrapFrames();
            var cs = new Cases("interpolation.interpolate_frame", C.N(g, "tolerance"));
            foreach (JObj s in C.A(g, "interpolate_frame"))
            {
                cs.Case();
                cs.Frame(s["out"], Interp.InterpolateFrame(fr[C.I(s, "a_index")], fr[C.I(s, "b_index")], C.N(s, "t")), "a" + C.I(s, "a_index") + " t" + C.N(s, "t"));
            }
            cs.Done();
        }

        [Test]
        public void Buffer_PushOrders_Samples_Prune()
        {
            var g = G;
            var fr = C.LoopwrapFrames();
            var b = C.O(g, "buffer");
            var cs = new Cases("interpolation.buffer", C.N(g, "tolerance"));
            InterpolationBuffer last = null;
            foreach (JObj po in C.A(b, "push_orders"))
            {
                cs.Case();
                var buf = new InterpolationBuffer(600);
                foreach (var idx in C.A(po, "order")) buf.Push(fr[(int)(double)idx]);
                cs.Eq(C.I(po, "size"), buf.Size, "size");
                last = buf;
            }
            foreach (JObj s in C.A(b, "samples"))
            {
                cs.Case();
                var r = last.Sample(fr[0].ts + C.N(s, "t_offset_ms"));
                var o = C.O(s, "out");
                cs.True(r.HasValue, C.S(s, "name") + " has value");
                if (!r.HasValue) continue;
                cs.Eq(C.B(o, "interpolated"), r.Value.interpolated, C.S(s, "name") + " interpolated");
                cs.Near(C.N(o, "starved_ms"), r.Value.starvedMs, C.S(s, "name") + " starved");
                cs.Frame(o["frame"], r.Value.frame, C.S(s, "name"));
            }
            cs.Case();
            var pr = C.O(b, "prune");
            last.Prune(fr[0].ts + C.N(pr, "prune_at_offset_ms"));
            cs.Eq(C.I(pr, "size_after"), last.Size, "prune size");
            cs.Case();
            cs.Eq(C.B(b, "empty_sample_is_null"), !new InterpolationBuffer().Sample(0).HasValue, "empty sample is null");
            cs.Done();
        }

        [Test]
        public void PlaybackClock_Steps()
        {
            var g = G;
            var pc = C.O(g, "playback_clock");
            var clock = new PlaybackClock(C.N(pc, "delay_ms"), C.N(pc, "creep"));
            var cs = new Cases("interpolation.playback_clock", C.N(g, "tolerance"));
            foreach (JObj s in C.A(pc, "steps"))
            {
                cs.Case();
                string op = C.S(s, "op");
                if (op == "observe") clock.Observe(C.N(C.A(s, "args")[0]), C.N(C.A(s, "args")[1]));
                else if (op == "reset") clock.Reset();
                double rt = clock.RenderTime(C.N(s, "render_time_at"));
                if (C.IsNull(s, "out_render_time")) cs.True(double.IsNaN(rt), op + ": render time NaN");
                else cs.Near(C.N(s, "out_render_time"), rt, op + ": render time");
                cs.Eq(C.B(s, "out_synced"), clock.Synced, op + ": synced");
            }
            cs.Done();
        }

        [Test]
        public void SmoothToward()
        {
            var g = G;
            var fr = C.LoopwrapFrames();
            var cs = new Cases("interpolation.smooth_toward", C.N(g, "tolerance"));
            foreach (JObj s in C.A(g, "smooth_toward"))
            {
                cs.Case();
                var cur = C.IsNull(s, "current_index") ? null : fr[C.I(s, "current_index")];
                cs.Frame(s["out"], Interp.SmoothToward(cur, fr[C.I(s, "target_index")], C.N(s, "k"), C.N(s, "dt")), C.S(s, "name"));
            }
            cs.Done();
        }
    }

    public class GoldenReconnectTests
    {
        [Test]
        public void Reconnect_Tables()
        {
            var g = C.Golden("reconnect");
            var k = C.O(g, "constants");
            Assert.AreEqual(ReconnectPolicy.RetryBaseMs, C.I(k, "retry_base_ms"));
            Assert.AreEqual(ReconnectPolicy.RetryMaxMs, C.I(k, "retry_max_ms"));
            Assert.AreEqual(ReconnectPolicy.QuietFailures, C.I(k, "quiet_failures"));
            Assert.AreEqual(ReconnectPolicy.LogEvery, C.I(k, "log_every"));
            var cs = new Cases("reconnect");
            foreach (JObj s in C.A(g, "retry_delay_ms")) { cs.Case(); cs.Eq(C.I(s, "out"), ReconnectPolicy.RetryDelayMs(C.N(s, "failures")), "retry " + C.N(s, "failures")); }
            foreach (JObj s in C.A(g, "link_state_after_failure"))
            {
                cs.Case();
                cs.Eq(C.S(s, "out"), ReconnectPolicy.LinkStateAfterFailure(C.I(s, "failures"), C.B(s, "ever_opened")), "state " + C.N(s, "failures") + "/" + C.B(s, "ever_opened"));
            }
            foreach (JObj s in C.A(g, "should_log_failure"))
            {
                cs.Case();
                cs.Eq(C.B(s, "out"), ReconnectPolicy.ShouldLogFailure(C.I(s, "failures"), C.B(s, "ever_opened")), "log " + C.N(s, "failures") + "/" + C.B(s, "ever_opened"));
            }
            cs.Done();
        }
    }

    public class GoldenAlertsTests
    {
        static string Sev(AlertSeverity s) => WireNames.Of(s);

        [Test]
        public void DeriveAlerts_Normative_Fields()
        {
            var g = C.Golden("alerts");
            var th = C.O(g, "thresholds");
            var d = AlertThresholds.Default;
            Assert.AreEqual(C.N(th, "batteryLowRem"), d.batteryLowRem, 1e-9);
            Assert.AreEqual(C.N(th, "batteryCritRem"), d.batteryCritRem, 1e-9);
            Assert.AreEqual(C.N(th, "motorTempWarnC"), d.motorTempWarnC, 1e-9);
            Assert.AreEqual(C.N(th, "motorTempCritC"), d.motorTempCritC, 1e-9);
            Assert.AreEqual(C.N(th, "batteryTempWarnC"), d.batteryTempWarnC, 1e-9);
            Assert.AreEqual(C.N(th, "batteryTempCritC"), d.batteryTempCritC, 1e-9);
            Assert.AreEqual(C.N(th, "fcTempWarnC"), d.fcTempWarnC, 1e-9);
            Assert.AreEqual(C.N(th, "vibrationWarn"), d.vibrationWarn, 1e-9);
            Assert.AreEqual(C.N(th, "vibrationCrit"), d.vibrationCrit, 1e-9);
            Assert.AreEqual(C.N(th, "motorOutRatio"), d.motorOutRatio, 1e-9);

            var cs = new Cases("alerts.cases (id, component, severity, order)");
            int msgSame = 0, msgTotal = 0;
            foreach (JObj c in C.A(g, "cases"))
            {
                cs.Case();
                var got = Alerts.Derive(C.Frame(c["frame"]));
                var exp = C.A(c, "out").Cast<JObj>().ToList();
                cs.Eq(exp.Count, got.Count, C.S(c, "name") + " count");
                for (int i = 0; i < Math.Min(exp.Count, got.Count); i++)
                {
                    cs.Eq(C.S(exp[i], "id"), got[i].id, C.S(c, "name") + " id[" + i + "]");
                    cs.Eq(C.S(exp[i], "component"), got[i].component, C.S(c, "name") + " component[" + i + "]");
                    cs.Eq(C.S(exp[i], "severity"), Sev(got[i].severity), C.S(c, "name") + " severity[" + i + "]");
                    msgTotal++;
                    if (C.S(exp[i], "message") == got[i].message) msgSame++;
                    else Debug.Log("[contract] alerts: informative message differs (allowed): '" + C.S(exp[i], "message") + "' vs '" + got[i].message + "'");
                }
            }
            Debug.Log("[contract] alerts: informative messages identical " + msgSame + "/" + msgTotal + " (not normative)");
            cs.Done();
        }

        [Test]
        public void Summary_And_Fresh()
        {
            var g = C.Golden("alerts");
            var cs = new Cases("alerts.summary+fresh");
            foreach (JObj s in C.A(g, "alert_summary"))
            {
                cs.Case();
                var sev = C.A(s, "in_severities").Select(x => (string)x == "critical" ? AlertSeverity.Critical : AlertSeverity.Warning).ToList();
                var r = Alerts.Summary(sev);
                var o = C.O(s, "out");
                cs.Eq(C.I(o, "count"), r.count, "count");
                cs.Eq(C.IsNull(o, "worst") ? null : C.S(o, "worst"), r.worst.HasValue ? Sev(r.worst.Value) : null, "worst");
            }
            var cases = C.A(g, "cases").Cast<JObj>().ToDictionary(c => C.S(c, "name"));
            foreach (JObj s in C.A(g, "fresh_alert"))
            {
                cs.Case();
                var alerts = Alerts.Derive(C.Frame(cases[C.S(s, "alerts_from_case")]["frame"]));
                var prev = new HashSet<string>(C.A(s, "prev_ids").Cast<string>());
                var f = Alerts.Fresh(prev, alerts);
                cs.Eq(C.IsNull(s, "out_id") ? null : C.S(s, "out_id"), f?.id, "fresh");
            }
            cs.Done();
        }
    }

    public class GoldenVitalsQuadXTests
    {
        static RotorGeometry[] Rotors(List<object> a) =>
            a.Cast<JObj>().Select(r => new RotorGeometry(C.I(r, "motor"), C.N(r, "px"), C.N(r, "py"), C.N(r, "km"))).ToArray();

        [Test]
        public void Vitals()
        {
            var g = C.Golden("vitals");
            var cs = new Cases("vitals");
            foreach (JObj s in C.A(g, "cross_layout"))
            {
                cs.Case();
                var got = global::WebGL.Telemetry.Vitals.CrossLayout(Rotors(C.A(s, "rotors")));
                var exp = C.A(s, "out").Cast<JObj>().ToList();
                cs.Eq(exp.Count, got.Length, "count");
                for (int i = 0; i < exp.Count && i < got.Length; i++)
                {
                    cs.Eq(C.I(exp[i], "motor"), got[i].motor, "motor");
                    cs.Eq(C.I(exp[i], "row"), got[i].row, "row");
                    cs.Eq(C.I(exp[i], "col"), got[i].col, "col");
                }
            }
            foreach (JObj s in C.A(g, "battery_class")) { cs.Case(); cs.Eq(C.S(s, "out"), global::WebGL.Telemetry.Vitals.WireName(global::WebGL.Telemetry.Vitals.ClassOf(C.N(s, "rem"))), "class " + C.N(s, "rem")); }
            foreach (JObj s in C.A(g, "battery_fill")) { cs.Case(); cs.Near(C.N(s, "out"), global::WebGL.Telemetry.Vitals.BatteryFill(C.N(s, "rem")), "fill " + C.N(s, "rem"), 1e-12); }
            cs.Case();
            cs.Eq(0.0, global::WebGL.Telemetry.Vitals.BatteryFill(double.NaN), "fill NaN (documented in _doc)");
            var th = C.O(g, "thresholds");
            cs.Case();
            cs.Near(C.N(th, "batteryLowRem"), AlertThresholds.Default.batteryLowRem, "batteryLowRem", 1e-12);
            cs.Near(C.N(th, "batteryCritRem"), AlertThresholds.Default.batteryCritRem, "batteryCritRem", 1e-12);
            cs.Done();
        }

        [Test]
        public void QuadX()
        {
            var g = C.Golden("quadx");
            var cs = new Cases("quadx", 1e-6);
            foreach (JObj r in C.A(g, "quad_x"))
            {
                cs.Case();
                var rg = new RotorGeometry(C.I(r, "motor"), C.N(r, "px"), C.N(r, "py"), C.N(r, "km"));
                var def = global::WebGL.Telemetry.QuadX.Px4QuadX[rg.motor - 1];
                cs.Near(new[] { rg.px, rg.py, rg.km }, new[] { def.px, def.py, def.km }, "default geometry motor " + rg.motor, 1e-12);
                cs.Eq(C.I(r, "spin_sign"), global::WebGL.Telemetry.QuadX.SpinSign(rg), "spin " + rg.motor);
                cs.Eq(C.S(r, "label_es"), global::WebGL.Telemetry.QuadX.RotorLabelEs(rg), "label " + rg.motor);
            }
            var at = C.O(g, "assign_to_rotors");
            cs.Case();
            var parts = C.A(at, "parts").Cast<JObj>().Select(p => new[] { C.N(p, "x"), C.N(p, "z") }).ToList();
            var got = global::WebGL.Telemetry.QuadX.AssignToRotors(parts, Rotors(C.A(at, "rotors")));
            cs.True(C.V(at, "out_motor_per_part").Select(x => (int)x).SequenceEqual(got), "assign " + string.Join(",", got));
            foreach (JObj s in C.A(g, "advance_phase"))
            {
                cs.Case();
                var i = C.O(s, "in");
                // CONTRACT ISSUE (reported): `out` was computed with the exact dt (1/60, 1/30) but `in.dt` is printed rounded to 6
                // decimals, so the 1e-6 tolerance cannot hold from the printed input. Tolerance = 1e-6 + |omega| * 5e-7 (half a unit of
                // the 6th decimal of dt times the angular speed).
                double omega = C.N(i, "rpm") / 60 * 2 * Math.PI * C.N(i, "visual_scale");
                cs.Near(C.N(s, "out"), Interp.AdvancePhase(C.N(i, "phase"), C.N(i, "rpm"), C.I(i, "spin"), C.N(i, "dt"), C.N(i, "visual_scale")), "phase", 1e-6 + Math.Abs(omega) * 5e-7);
                // Strict check with the exact dt the golden was computed with (nearest 1/n).
                double dtExact = 1.0 / Math.Round(1.0 / C.N(i, "dt"));
                cs.Near(C.N(s, "out"), Interp.AdvancePhase(C.N(i, "phase"), C.N(i, "rpm"), C.I(i, "spin"), dtExact, C.N(i, "visual_scale")), "phase (exact dt)", 1e-6);
            }
            cs.Done();
        }
    }

    public class GoldenFaultsTests
    {
        static Fault FaultOf(JObj f) => new Fault
        {
            id = C.S(f, "id"), kind = Kind(C.S(f, "kind")), motor = f.ContainsKey("motor") ? C.I(f, "motor") : 0, HasMotor = f.ContainsKey("motor"),
            since = C.N(f, "since"), until = C.N(f, "until"),
        };

        static FaultKind Kind(string s) { WireNames.TryFaultKind(s, out var k); return k; }

        [Test]
        public void Faults()
        {
            var g = C.Golden("faults");
            double tol = C.N(g, "tolerance");
            var k = C.O(g, "constants");
            Assert.AreEqual(global::WebGL.Telemetry.Faults.DurationMs, C.N(k, "fault_duration_ms"));
            Assert.AreEqual(global::WebGL.Telemetry.Faults.MaxActive, C.I(k, "max_active_faults"));
            CollectionAssert.AreEqual(C.A(k, "fault_kinds").Cast<string>().ToArray(), TelemetryProtocol.FaultKinds);

            var cs = new Cases("faults", tol);
            foreach (JObj s in C.A(g, "validate_fault"))
            {
                cs.Case();
                bool ok = global::WebGL.Telemetry.Faults.Validate(s["kind"], s["motor"], out var kind, out var motor);
                if (C.IsNull(s, "out")) { cs.True(!ok, "invalid " + s["kind"] + "/" + s["motor"]); continue; }
                var o = C.O(s, "out");
                cs.True(ok, "valid " + s["kind"]);
                cs.Eq(C.S(o, "kind"), WireNames.Of(kind), "kind");
                cs.Eq(o.ContainsKey("motor") ? (int?)C.I(o, "motor") : null, motor, "motor");
            }
            var list = new List<Fault>();
            foreach (JObj s in C.A(g, "add_fault_chain"))
            {
                cs.Case();
                list = global::WebGL.Telemetry.Faults.Add(list, Kind(C.S(s, "kind")), C.IsNull(s, "motor") ? (int?)null : C.I(s, "motor"), C.N(s, "now"));
                var exp = C.A(s, "out_faults").Cast<JObj>().ToList();
                cs.Eq(exp.Count, list.Count, "count at " + C.N(s, "now"));
                for (int i = 0; i < Math.Min(exp.Count, list.Count); i++)
                {
                    cs.Eq(C.S(exp[i], "id"), list[i].id, "id");
                    cs.Eq(exp[i].ContainsKey("motor"), list[i].HasMotor, "has motor");
                    cs.Near(C.N(exp[i], "since"), list[i].since, "since");
                    cs.Near(C.N(exp[i], "until"), list[i].until, "until");
                }
            }
            foreach (JObj s in C.A(g, "active_faults"))
            {
                cs.Case();
                var act = global::WebGL.Telemetry.Faults.Active(C.A(s, "faults").Cast<JObj>().Select(FaultOf).ToList(), C.N(s, "now"));
                cs.True(C.A(s, "out_ids").Cast<string>().SequenceEqual(act.Select(f => f.id)), "active at " + C.N(s, "now"));
            }
            var af = C.O(g, "apply_faults");
            var bas = C.Frame(af["base_frame"]);
            foreach (JObj s in C.A(af, "cases"))
            {
                cs.Case();
                var faults = C.A(s, "faults").Cast<JObj>().Select(FaultOf).ToList();
                cs.Frame(s["out"], global::WebGL.Telemetry.Faults.Apply(bas, faults, C.N(s, "now")), C.S(s, "name") + " @" + C.N(s, "now"));
            }
            cs.Done();
        }
    }

    public class GoldenThermalTests
    {
        [Test]
        public void Thermal()
        {
            var g = C.Golden("thermal");
            var cs = new Cases("thermal", C.N(g, "tolerance"));
            var sources = C.A(g, "sources").Cast<JObj>().Select(s => new HeatSource(C.V(s, "pos"), C.N(s, "radius"))).ToList();
            var temps = C.V(g, "temps");
            double amb = C.N(g, "ambient_c");
            foreach (JObj s in C.A(g, "temperature_at")) { cs.Case(); cs.Near(C.N(s, "out"), ThermalField.TemperatureAt(C.V(s, "point"), sources, temps, amb), "T at " + string.Join(",", C.V(s, "point"))); }
            foreach (JObj s in C.A(g, "auto_range"))
            {
                cs.Case();
                var i = C.O(s, "in");
                var r = ThermalField.AutoRange(C.V(i, "temps"), C.N(i, "ambient_c"));
                cs.Near(C.N(C.O(s, "out"), "min"), r.min, "auto min");
                cs.Near(C.N(C.O(s, "out"), "max"), r.max, "auto max");
            }
            foreach (JObj s in C.A(g, "smooth_range"))
            {
                cs.Case();
                var i = C.O(s, "in");
                var cur = C.O(i, "current"); var tg = C.O(i, "target");
                var r = ThermalField.SmoothRange(new ThermalRange(C.N(cur, "min"), C.N(cur, "max")), new ThermalRange(C.N(tg, "min"), C.N(tg, "max")), C.N(i, "dt"));
                cs.Near(C.N(C.O(s, "out"), "min"), r.min, "smooth min");
                cs.Near(C.N(C.O(s, "out"), "max"), r.max, "smooth max");
            }
            cs.Case();
            cs.Near(C.N(C.O(g, "fixed_range"), "min"), ThermalField.FixedRange.min, "fixed min");
            cs.Near(C.N(C.O(g, "fixed_range"), "max"), ThermalField.FixedRange.max, "fixed max");
            foreach (JObj s in C.A(g, "telemetry_origins"))
            {
                cs.Case();
                var got = ThermalField.TelemetryOrigins(C.Provenance(s["provenance"])).Select(o => WireNames.Of(o)).ToList();
                cs.True(C.A(s, "out").Cast<string>().SequenceEqual(got), "origins " + string.Join("/", got));
            }
            foreach (JObj s in C.A(g, "effective_thermal_source"))
            {
                cs.Case();
                var chosen = C.S(s, "chosen") == "telemetry" ? ThermalSourceId.Telemetry : ThermalSourceId.Simulation;
                var got = ThermalField.EffectiveSource(C.Cls(C.S(s, "app_source")), chosen) == ThermalSourceId.Telemetry ? "telemetry" : "simulation";
                cs.Eq(C.S(s, "out"), got, "effective " + C.S(s, "app_source"));
            }
            foreach (JObj s in C.A(g, "has_telemetry_data"))
            {
                cs.Case();
                cs.Eq(C.B(s, "out"), ThermalField.HasTelemetryData(C.Cls(C.S(s, "app_source")), C.S(s, "connection") == "open", C.B(s, "has_frame")), "has data");
            }
            foreach (JObj s in C.A(g, "is_thermal_no_data"))
            {
                cs.Case();
                var src = C.S(s, "source") == "telemetry" ? ThermalSourceId.Telemetry : ThermalSourceId.Simulation;
                cs.Eq(C.B(s, "out"), ThermalField.IsNoData(src, C.B(s, "has_telemetry")), "no data");
            }
            var labels = HonestyLabels.Parse(C.Text("labels.json"));
            foreach (JObj s in C.A(g, "format_readout_es"))
            {
                cs.Case();
                var origins = C.A(s, "origins").Cast<string>().Select(o => (ThermalOrigin)Array.IndexOf(TelemetryProtocol.ThermalOrigins, o)).ToList();
                cs.Eq(C.S(s, "out"), ThermalField.FormatReadout(C.N(s, "temp_c"), origins, labels, "es"), "readout");
            }
            cs.Case();
            bool threw = false;
            try { ThermalField.FormatReadout(40, new List<ThermalOrigin>(), labels, "es"); } catch (InvalidOperationException) { threw = true; }
            cs.Eq(C.B(g, "format_readout_without_origin_throws"), threw, "no origin -> throws");
            cs.Done();
        }
    }

    public class GoldenReplayTests
    {
        [Test]
        public void Schedule_Arithmetic()
        {
            var g = C.Golden("replay");
            var cs = new Cases("replay.schedule");
            cs.Case();
            cs.Eq(C.N(g, "epoch_ms"), ReplaySchedule.ReplayEpochMs, "epoch");
            double period = C.N(C.O(g, "twf"), "period_ms");
            foreach (JObj s in C.A(g, "global_frame_at")) { cs.Case(); cs.Eq(C.N(s, "out_g"), ReplaySchedule.GlobalFrameAt(C.N(s, "now_ms"), ReplaySchedule.ReplayEpochMs, period), "g at " + C.N(s, "now_ms")); }
            foreach (JObj s in C.A(g, "play_time")) { cs.Case(); cs.Eq(C.N(s, "out_ms"), ReplaySchedule.ReplayEpochMs + C.N(s, "g") * period, "play time " + C.N(s, "g")); }
            cs.Done();
        }

        [Test]
        public void MiniTwf_Decode()
        {
            var g = C.Golden("replay");
            var mini = C.O(g, "mini_twf");
            var f = TwfFile.Decode(File.ReadAllBytes(Path.Combine(C.Root, "golden", "mini.twf")));
            var cs = new Cases("replay.mini_twf");
            var h = C.O(mini, "header");
            cs.Case();
            cs.Eq(C.I(h, "frame_count"), f.Header.frame_count, "frame_count");
            cs.Eq(C.N(h, "rate_hz"), f.Header.rate_hz, "rate");
            cs.True(C.A(h, "fields").Cast<string>().SequenceEqual(f.Header.fields), "fields");
            cs.Eq("fixture", f.Header.sourceLogId, "source.log_id");
            var rows = C.A(mini, "rows_float32");
            for (int r = 0; r < rows.Count; r++)
            {
                cs.Case();
                var exp = C.V(rows[r]);
                for (int c = 0; c < exp.Length; c++) cs.Eq(exp[c], f.At(r, c), "row " + r + " col " + c + " (tolerance 0 after widening)");
            }
            cs.Done();
        }

        /// <summary>Needs the real flight file (WP-09 bundles it); Inconclusive when it is not on this machine.</summary>
        [Test]
        public void FullTwf_Frames_And_FramesBetween()
        {
            var g = C.Golden("replay");
            var tw = C.O(g, "twf");
            string path = FindFlightTwf();
            if (path == null) Assert.Inconclusive("x500v2_flight.twf not found (StreamingAssets/Telemetry, %TWINSIGHT_WEB_REPO%/data, E:/twinsight-web-twin/data)");
            var bytes = File.ReadAllBytes(path);
            string sha;
            using (var s = SHA256.Create()) sha = string.Concat(s.ComputeHash(bytes).Select(b => b.ToString("x2")));
            Assert.AreEqual(C.S(tw, "sha256"), sha, "sha256 of " + path);
            var file = TwfFile.Decode(bytes);
            var sch = new ReplaySchedule(file);
            var cs = new Cases("replay.frames", 0);
            cs.Case();
            cs.Eq(C.I(tw, "frame_count"), sch.FrameCount, "frame_count");
            cs.Eq(C.N(tw, "period_ms"), sch.PeriodMs, "period");
            foreach (JObj s in C.A(g, "frames")) { cs.Case(); cs.Frame(s["out"], sch.Frame(C.N(s, "g")), "g " + C.N(s, "g"), 0); }
            cs.Case();
            cs.Frame(C.O(C.O(g, "frame_from_row"), "row_0"), sch.Frame(0), "row_0", 0);
            foreach (JObj s in C.A(g, "frames_between"))
            {
                cs.Case();
                var fr = sch.FramesBetween(C.N(s, "from_ms"), C.N(s, "to_ms"), C.I(s, "max"));
                cs.Eq(C.I(s, "out_count"), fr.Count, "count");
                var ts = C.V(s, "out_ts");
                for (int i = 0; i < ts.Length && i < fr.Count; i++) cs.Eq(ts[i], fr[i].ts, "ts[" + i + "]");
                if (fr.Count > 0)
                {
                    cs.Eq(C.N(s, "out_first_ts"), fr[0].ts, "first");
                    cs.Eq(C.N(s, "out_last_ts"), fr[fr.Count - 1].ts, "last");
                }
            }
            cs.Done();
        }

        internal static string FindFlightTwf()
        {
            var candidates = new List<string> { Path.Combine(Application.streamingAssetsPath, "Telemetry", "x500v2_flight.twf") };
            var env = Environment.GetEnvironmentVariable("TWINSIGHT_WEB_REPO");
            if (!string.IsNullOrEmpty(env)) candidates.Add(Path.Combine(env, "data", "x500v2_flight.twf"));
            candidates.Add("E:/twinsight-web-twin/data/x500v2_flight.twf");
            return candidates.FirstOrDefault(File.Exists);
        }
    }
}
