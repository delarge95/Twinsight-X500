// Lote 1c, tool 6: guided training missions, a line-by-line port of the web's src/shared/training.ts (golden: contracts/golden/training.json). Pure C# (no UnityEngine).
// A lesson is a list of steps (contracts/training/missions.json). Every step is checked ONLY against telemetry frames (the position, the heading and the armed /
// landed flags that are on screen), never against the input that produced them. HONESTY: this is training on the SIMULATOR; the frames are simulated and it does not
// replace the official training of a pilot. Frame: metres from the take-off point (n north, e east), alt = height over the ground, yawDeg clockwise from north.

using System;
using System.Collections.Generic;
using System.Text;
using WebGL.Telemetry.Json;

namespace WebGL.Telemetry
{
    public enum StepType { Takeoff, Hold, Reach, Orbit, Yaw, Land, Course }

    /// <summary>One step of a lesson. Optional fields are null when absent (the defaults live in <see cref="Training"/>).</summary>
    public sealed class TrainingStep
    {
        public string id;
        public StepType type;
        public double alt, seconds, n, e, rMin, rMax, deg, radius;
        public double? tol, withinS, tolH, tolAlt, turns;
        public string courseId;
    }

    public sealed class Lesson
    {
        public string id;
        public List<TrainingStep> steps = new List<TrainingStep>();
    }

    /// <summary>What a step looks at: the position (n, e, alt), the heading and the flags of a telemetry frame.</summary>
    public struct TrainingFrame
    {
        public double n, e, alt, yawDeg;
        public bool armed, landed;
        public TrainingFrame(double n, double e, double alt, double yawDeg, bool armed, bool landed) { this.n = n; this.e = e; this.alt = alt; this.yawDeg = yawDeg; this.armed = armed; this.landed = landed; }
    }

    /// <summary>The state of a step in progress (plain data; <see cref="Training.StepCheck"/> never modifies the one it receives, it works on a copy).</summary>
    public sealed class StepCtx
    {
        /// <summary>Seconds since the step started.</summary>
        public double t;
        /// <summary>The best progress so far and the seconds since it last improved.</summary>
        public double best, idleS;
        /// <summary>Where the drone was at the first frame of the step (hold: the sphere's centre; orbit: the circle's centre).</summary>
        public double[] origin;
        /// <summary>Distance (reach, land) to the target at the first frame, and (land) the height.</summary>
        public double? d0, alt0;
        /// <summary>hold: the previous frame was inside the sphere.</summary>
        public bool wasIn;
        /// <summary>hold: seconds held inside the sphere. orbit: the unwrapped angle around the centre, rad (+ clockwise from above).</summary>
        public double acc;
        /// <summary>orbit: the angle of the previous sample while it was inside the ring, null when it was not.</summary>
        public double? prevTheta;
        /// <summary>course step: the lap in progress (shared by reference between copies: a step is never branched).</summary>
        public CourseRun course;

        public StepCtx Clone() => new StepCtx { t = t, best = best, idleS = idleS, origin = origin != null ? (double[])origin.Clone() : null, d0 = d0, alt0 = alt0, wasIn = wasIn, acc = acc, prevTheta = prevTheta, course = course };
    }

    public sealed class StepResult
    {
        public StepCtx ctx;
        public bool done;
        /// <summary>0..1. Monotonic (the best so far) for every step but hold, whose bar goes back to 0 when the drone leaves the sphere.</summary>
        public double progress;
        /// <summary>The id of the step (its hint text) once HintAfterS seconds (takeoff: withinS) went by without progress; null otherwise.</summary>
        public string hint;
    }

    /// <summary>What to mark in the world for the step in progress (n north, e east, alt up). kind: point | hold | band | pad.</summary>
    public sealed class TargetMark
    {
        public string kind;
        public double n, e, alt, r, h, rMin, rMax;
    }

    /// <summary>A lesson being flown. `step` is the number of steps completed (= the index of the step in progress; = total when done).</summary>
    public sealed class LessonRun
    {
        public string lessonId;
        public int total, step;
        public StepCtx ctx = new StepCtx();
        /// <summary>Seconds since the lesson started (retries included).</summary>
        public double elapsedS;
        public bool done;
        public double progress;
        public string hint;
        /// <summary>Started at the first step: only such a run can set a best time.</summary>
        public bool fromStart;

        public LessonRun Clone() => (LessonRun)MemberwiseClone();
    }

    public struct LessonEvent
    {
        /// <summary>"step" (index = the step just completed) or "done" (timeS).</summary>
        public string kind;
        public int index;
        public double timeS;
    }

    public sealed class LessonRecord
    {
        public int step;
        public bool done;
        public double? bestS;
    }

    public static class Training
    {
        /// <summary>Seconds without progress after which a step raises its hint.</summary>
        public const double HintAfterS = 20;
        /// <summary>A step has made progress when its progress beats the best so far by more than this (0..1).</summary>
        public const double ProgressEps = 0.002;
        public const double TakeoffTol = 0.5, TakeoffWithinS = 20, HoldTolH = 1, HoldTolAlt = 0.5, ReachTol = 1.5, OrbitTurns = 1, YawTol = 10;

        // ── data ──

        static bool IsNum(object x) => x is double d && !double.IsNaN(d) && !double.IsInfinity(d);
        static bool Pos(object x) => IsNum(x) && (double)x > 0;
        static bool Opt(Dictionary<string, object> s, string key, Func<object, bool> ok) => !s.TryGetValue(key, out object v) || ok(v);
        static double? OptNum(Dictionary<string, object> s, string key) => s.TryGetValue(key, out object v) ? (double?)(double)v : null;

        static TrainingStep ParseStep(object raw)
        {
            var s = raw as Dictionary<string, object>;
            if (s == null || !s.TryGetValue("id", out object idObj) || !(idObj is string id) || id.Length == 0) return null;
            if (!s.TryGetValue("type", out object tObj) || !(tObj is string type)) return null;
            var st = new TrainingStep { id = id };
            switch (type)
            {
                case "takeoff":
                    if (!(s.TryGetValue("alt", out object a) && Pos(a)) || !Opt(s, "tol", Pos) || !Opt(s, "withinS", Pos)) return null;
                    st.type = StepType.Takeoff; st.alt = (double)a; st.tol = OptNum(s, "tol"); st.withinS = OptNum(s, "withinS");
                    return st;
                case "hold":
                    if (!(s.TryGetValue("seconds", out object sec) && Pos(sec)) || !Opt(s, "tolH", Pos) || !Opt(s, "tolAlt", Pos)) return null;
                    st.type = StepType.Hold; st.seconds = (double)sec; st.tolH = OptNum(s, "tolH"); st.tolAlt = OptNum(s, "tolAlt");
                    return st;
                case "reach":
                    if (!(s.TryGetValue("n", out object rn) && IsNum(rn)) || !(s.TryGetValue("e", out object re) && IsNum(re)) || !(s.TryGetValue("alt", out object ra) && IsNum(ra)) || !Opt(s, "tol", Pos)) return null;
                    st.type = StepType.Reach; st.n = (double)rn; st.e = (double)re; st.alt = (double)ra; st.tol = OptNum(s, "tol");
                    return st;
                case "orbit":
                    if (!(s.TryGetValue("rMin", out object mn) && Pos(mn)) || !(s.TryGetValue("rMax", out object mx) && Pos(mx)) || (double)mx <= (double)mn || !Opt(s, "turns", Pos)) return null;
                    st.type = StepType.Orbit; st.rMin = (double)mn; st.rMax = (double)mx; st.turns = OptNum(s, "turns");
                    return st;
                case "yaw":
                    if (!(s.TryGetValue("deg", out object dg) && IsNum(dg)) || !Opt(s, "tol", Pos)) return null;
                    st.type = StepType.Yaw; st.deg = (double)dg; st.tol = OptNum(s, "tol");
                    return st;
                case "land":
                    if (!(s.TryGetValue("n", out object ln) && IsNum(ln)) || !(s.TryGetValue("e", out object le) && IsNum(le)) || !(s.TryGetValue("radius", out object lr) && Pos(lr))) return null;
                    st.type = StepType.Land; st.n = (double)ln; st.e = (double)le; st.radius = (double)lr;
                    return st;
                case "course":
                    if (!s.TryGetValue("courseId", out object cid) || !(cid is string cs) || cs.Length == 0) return null;
                    st.type = StepType.Course; st.courseId = cs;
                    return st;
                default:
                    return null;
            }
        }

        /// <summary>The lessons from their JSON (contracts/training/missions.json). Anything that is not a version 1 file with unique ids gives null.</summary>
        public static List<Lesson> ParseLessons(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            object root;
            try { root = TelemetryJson.Parse(json); } catch { return null; }
            var d = root as Dictionary<string, object>;
            if (d == null || !d.TryGetValue("version", out object v) || !(v is double dv) || dv != 1) return null;
            if (!d.TryGetValue("lessons", out object lo) || !(lo is List<object> ls) || ls.Count == 0) return null;
            var lessonIds = new HashSet<string>();
            var stepIds = new HashSet<string>();
            var lessons = new List<Lesson>();
            foreach (object lraw in ls)
            {
                var l = lraw as Dictionary<string, object>;
                if (l == null || !l.TryGetValue("id", out object idObj) || !(idObj is string id) || id.Length == 0 || !lessonIds.Add(id)) return null;
                if (!l.TryGetValue("steps", out object so) || !(so is List<object> steps) || steps.Count == 0) return null;
                var lesson = new Lesson { id = id };
                foreach (object r in steps)
                {
                    TrainingStep s = ParseStep(r);
                    if (s == null || !stepIds.Add(s.id)) return null;
                    lesson.steps.Add(s);
                }
                lessons.Add(lesson);
            }
            return lessons;
        }

        // ── one step ──

        /// <summary>The telemetry frame as the steps see it (NED -> n, e, up; the heading from the attitude quaternion).</summary>
        public static TrainingFrame FrameOf(TelemetryFrame f) => new TrainingFrame(f.p[0], f.p[1], -f.p[2], Minimap.YawFromQuat(f.q) * 180 / Math.PI, f.armed, f.landed);

        static double Clamp01(double x) => Math.Min(1, Math.Max(0, x));
        static double WrapPi(double a) => Math.Atan2(Math.Sin(a), Math.Cos(a));
        /// <summary>Degrees wrapped to [-180, 180) (exact for whole numbers).</summary>
        static double WrapDeg(double a) => ((((a + 180) % 360) + 360) % 360) - 180;
        static bool Flying(TrainingFrame f) => f.armed && !f.landed;
        static double Hypot(double a, double b) => Math.Sqrt(a * a + b * b);
        static double Hypot(double a, double b, double c) => Math.Sqrt(a * a + b * b + c * c);

        /// <summary>
        /// Checks one step against a frame. `dt` is the time since the previous frame of this step (the first call may pass 0); a negative or non-finite value counts as 0.
        /// The caller keeps the returned ctx and passes it with the next frame; start a step with `new StepCtx()`. `courses` resolves a course by id (the `course` step).
        /// </summary>
        public static StepResult StepCheck(TrainingStep step, StepCtx ctxIn, TrainingFrame f, double dtIn, Func<string, CourseLayout> courses = null)
        {
            double dt = !double.IsNaN(dtIn) && !double.IsInfinity(dtIn) && dtIn > 0 ? dtIn : 0;
            StepCtx c = ctxIn.Clone();
            c.t += dt;
            bool done = false;
            double raw = 0;
            double hintS = HintAfterS;
            bool monotonic = true;
            switch (step.type)
            {
                case StepType.Takeoff:
                {
                    double err = Math.Abs(f.alt - step.alt);
                    done = Flying(f) && err < (step.tol ?? TakeoffTol);
                    raw = Flying(f) ? Clamp01(1 - err / step.alt) : 0;
                    hintS = step.withinS ?? TakeoffWithinS;
                    break;
                }
                case StepType.Hold:
                {
                    monotonic = false;
                    if (c.origin == null) c.origin = new[] { f.n, f.e, f.alt };
                    bool inside = Flying(f) && Hypot(f.n - c.origin[0], f.e - c.origin[1]) <= (step.tolH ?? HoldTolH) && Math.Abs(f.alt - c.origin[2]) <= (step.tolAlt ?? HoldTolAlt);
                    if (!inside) c.acc = 0;
                    else if (c.wasIn) c.acc += dt;
                    c.wasIn = inside;
                    done = c.acc >= step.seconds;
                    raw = Clamp01(c.acc / step.seconds);
                    break;
                }
                case StepType.Reach:
                {
                    double tol = step.tol ?? ReachTol;
                    double dist = Hypot(f.n - step.n, f.e - step.e, f.alt - step.alt);
                    if (c.d0 == null) c.d0 = dist;
                    done = dist <= tol;
                    raw = c.d0.Value <= tol ? 1 : Clamp01(1 - (dist - tol) / (c.d0.Value - tol));
                    break;
                }
                case StepType.Orbit:
                {
                    if (c.origin == null) c.origin = new[] { f.n, f.e, f.alt };
                    double dn = f.n - c.origin[0], de = f.e - c.origin[1];
                    double r = Hypot(dn, de);
                    if (r >= step.rMin && r <= step.rMax)
                    {
                        double theta = Math.Atan2(de, dn); // clockwise from north, the heading convention
                        if (c.prevTheta != null) c.acc += WrapPi(theta - c.prevTheta.Value);
                        c.prevTheta = theta;
                    }
                    else
                    {
                        c.acc = 0;
                        c.prevTheta = null;
                    }
                    double full = 2 * Math.PI * (step.turns ?? OrbitTurns);
                    done = Math.Abs(c.acc) >= full;
                    raw = Clamp01(Math.Abs(c.acc) / full);
                    break;
                }
                case StepType.Yaw:
                {
                    double err = Math.Abs(WrapDeg(f.yawDeg - step.deg));
                    done = err <= (step.tol ?? YawTol);
                    raw = Clamp01(1 - err / 180);
                    break;
                }
                case StepType.Land:
                {
                    double dist = Hypot(f.n - step.n, f.e - step.e);
                    if (c.d0 == null) c.d0 = dist;
                    if (c.alt0 == null) c.alt0 = Math.Max(f.alt, 0.5);
                    done = f.landed && !f.armed && dist <= step.radius;
                    double close = c.d0.Value <= step.radius ? 1 : Clamp01(1 - (dist - step.radius) / (c.d0.Value - step.radius));
                    raw = 0.5 * close + 0.5 * Clamp01(1 - f.alt / c.alt0.Value);
                    break;
                }
                case StepType.Course:
                {
                    CourseLayout layout = courses != null ? courses(step.courseId) : null;
                    if (layout == null) break; // no such course: nothing to check
                    if (c.course == null) c.course = new CourseRun(layout);
                    c.course.Step(dt, f.n, f.e, f.alt);
                    done = c.course.State == CourseState.Finished;
                    raw = Clamp01(c.course.Reached / (double)layout.Checkpoints.Count);
                    break;
                }
            }
            if (done) raw = 1;
            else raw = Math.Min(raw, 0.99); // 1 means done: a landed-but-armed frame is not a finished landing
            if (raw > c.best + ProgressEps || (done && c.best < 1))
            {
                c.best = raw;
                c.idleS = 0;
            }
            else c.idleS += dt;
            double progress = monotonic ? Math.Max(c.best, raw) : raw;
            var res = new StepResult { ctx = c, done = done, progress = progress };
            if (!done && c.idleS >= hintS) res.hint = step.id;
            return res;
        }

        // ── the objective on the scene and the map ──

        /// <summary>What to mark in the world for the step in progress. `course` and `yaw` mark nothing: the course draws itself. null = nothing.</summary>
        public static TargetMark StepTarget(TrainingStep step, StepCtx ctx)
        {
            switch (step.type)
            {
                case StepType.Takeoff: return new TargetMark { kind = "point", n = 0, e = 0, alt = step.alt, r = step.tol ?? TakeoffTol };
                case StepType.Reach: return new TargetMark { kind = "point", n = step.n, e = step.e, alt = step.alt, r = step.tol ?? ReachTol };
                case StepType.Hold:
                    return ctx.origin != null ? new TargetMark { kind = "hold", n = ctx.origin[0], e = ctx.origin[1], alt = ctx.origin[2], r = step.tolH ?? HoldTolH, h = step.tolAlt ?? HoldTolAlt } : null;
                case StepType.Orbit:
                    return ctx.origin != null ? new TargetMark { kind = "band", n = ctx.origin[0], e = ctx.origin[1], alt = ctx.origin[2], rMin = step.rMin, rMax = step.rMax } : null;
                case StepType.Land: return new TargetMark { kind = "pad", n = step.n, e = step.e, r = step.radius };
                default: return null;
            }
        }

        // ── one lesson ──

        /// <summary>A run of `lesson` at its step `from` (the number of steps already done; 0 = the beginning).</summary>
        public static LessonRun StartLesson(Lesson lesson, int from = 0)
        {
            int step = Math.Min(Math.Max(0, from), lesson.steps.Count - 1);
            return new LessonRun { lessonId = lesson.id, total = lesson.steps.Count, step = step, ctx = new StepCtx(), elapsedS = 0, done = false, progress = 0, hint = null, fromStart = step == 0 };
        }

        /// <summary>Advances the lesson with one frame. A step that completes on this frame starts on the next one (its first frame captures its starting point).</summary>
        public static LessonRun LessonStep(Lesson lesson, LessonRun run, TrainingFrame f, double dt, List<LessonEvent> events, Func<string, CourseLayout> courses = null)
        {
            if (run.done) return run;
            double d = !double.IsNaN(dt) && !double.IsInfinity(dt) && dt > 0 ? dt : 0;
            double elapsedS = run.elapsedS + d;
            StepResult r = StepCheck(lesson.steps[run.step], run.ctx, f, d, courses);
            LessonRun next = run.Clone();
            next.elapsedS = elapsedS;
            if (!r.done)
            {
                next.ctx = r.ctx; next.progress = r.progress; next.hint = r.hint;
                return next;
            }
            events?.Add(new LessonEvent { kind = "step", index = run.step });
            next.step = run.step + 1;
            next.ctx = new StepCtx();
            next.hint = null;
            if (next.step >= run.total)
            {
                events?.Add(new LessonEvent { kind = "done", timeS = elapsedS });
                next.done = true; next.progress = 1;
            }
            else next.progress = 0;
            return next;
        }

        /// <summary>Starts the current step again (a fresh context: the drone's position then is the new starting point). The lesson's clock keeps running.</summary>
        public static LessonRun RetryStep(LessonRun run)
        {
            if (run.done) return run;
            LessonRun n = run.Clone();
            n.ctx = new StepCtx(); n.progress = 0; n.hint = null;
            return n;
        }

        /// <summary>0..1 over the whole lesson: the completed steps plus the progress of the current one.</summary>
        public static double LessonFraction(LessonRun run) => run.done ? 1 : (run.step + run.progress) / run.total;

        // ── persistence: persistentDataPath/twinsight/training.json (web: twinsight.training.v1) = { [lessonId]: { step, done, bestS } } ──

        /// <summary>The progress from its stored text. Anything invalid gives an empty record (nothing is half-read); unknown fields are dropped.</summary>
        public static Dictionary<string, LessonRecord> ParseProgress(string raw)
        {
            var empty = new Dictionary<string, LessonRecord>();
            if (string.IsNullOrEmpty(raw)) return empty;
            object root;
            try { root = TelemetryJson.Parse(raw); } catch { return empty; }
            var d = root as Dictionary<string, object>;
            if (d == null) return empty;
            var outp = new Dictionary<string, LessonRecord>(StringComparer.Ordinal);
            foreach (var kv in d)
            {
                var r = kv.Value as Dictionary<string, object>;
                if (r == null) return empty;
                if (!r.TryGetValue("step", out object so) || !(so is double sd) || sd != Math.Floor(sd) || sd < 0 || sd > int.MaxValue) return empty;
                if (!r.TryGetValue("done", out object dn) || !(dn is bool db)) return empty;
                if (!r.TryGetValue("bestS", out object bo)) return empty;
                double? best;
                if (bo == null) best = null;
                else if (bo is double bd && !double.IsNaN(bd) && !double.IsInfinity(bd) && bd >= 0) best = bd;
                else return empty;
                outp[kv.Key] = new LessonRecord { step = (int)sd, done = db, bestS = best };
            }
            return outp;
        }

        /// <summary>The progress as JSON, as the web's JSON.stringify writes it.</summary>
        public static string ProgressToJson(Dictionary<string, LessonRecord> progress)
        {
            var sb = new StringBuilder(128);
            sb.Append('{');
            bool first = true;
            foreach (var kv in progress)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(TelemetryJson.Str(kv.Key)).Append(":{\"step\":").Append(kv.Value.step.ToString(System.Globalization.CultureInfo.InvariantCulture))
                  .Append(",\"done\":").Append(kv.Value.done ? "true" : "false").Append(",\"bestS\":").Append(kv.Value.bestS.HasValue ? TelemetryJson.Num(kv.Value.bestS.Value) : "null").Append('}');
            }
            return sb.Append('}').ToString();
        }

        /// <summary>The record after a step was completed (`run.step` steps done) or the lesson was finished. The input is not modified.</summary>
        public static Dictionary<string, LessonRecord> RecordRun(Dictionary<string, LessonRecord> progress, LessonRun run)
        {
            var outp = new Dictionary<string, LessonRecord>(progress, StringComparer.Ordinal);
            progress.TryGetValue(run.lessonId, out LessonRecord old);
            if (run.done)
            {
                double? bestS = run.fromStart && (old == null || !old.bestS.HasValue || run.elapsedS < old.bestS.Value) ? (double?)run.elapsedS : (old != null ? old.bestS : null);
                outp[run.lessonId] = new LessonRecord { step = run.total, done = true, bestS = bestS };
                return outp;
            }
            // mid-lesson progress: never loses the "done" mark or the best time of an earlier pass
            outp[run.lessonId] = new LessonRecord { step = run.step, done = old != null && old.done, bestS = old != null ? old.bestS : null };
            return outp;
        }

        /// <summary>Where a lesson starts from the record: the saved step, or the beginning when it was finished (or the saved step is out of range).</summary>
        public static int ResumeFrom(Lesson lesson, LessonRecord rec)
        {
            if (rec == null || rec.done || rec.step >= lesson.steps.Count) return 0;
            return rec.step;
        }
    }
}
