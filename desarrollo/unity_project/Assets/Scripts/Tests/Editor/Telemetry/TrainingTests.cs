// Lote 1c, tool 6: the guided-training logic (Telemetry/Logic/Training.cs) against contracts/golden/training.json, produced by the web's src/shared/training.ts: every
// step type on synthetic frames (probes and scripts with the context carried), what each step marks in the world, whole lessons (events, retries), the stored
// progress and where a lesson resumes. The lessons come from the bundled missions.json (a copy of the web's contracts/training/missions.json).
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace WebGL.Telemetry.Tests
{
    using JObj = Dictionary<string, object>;
    using JArr = List<object>;

    public class TrainingTests
    {
        const double Tol = 1e-9;

        static string MissionsText() => File.ReadAllText(Path.Combine(Application.dataPath, "StreamingAssets", "Telemetry", "training", "missions.json"));

        static List<Lesson> Lessons()
        {
            List<Lesson> l = Training.ParseLessons(MissionsText());
            Assert.IsNotNull(l, "missions.json must parse");
            return l;
        }

        static Lesson LessonById(string id) => Lessons().Find(x => x.id == id);

        static CourseLayout Courses(string id) => CourseLayout.Default();

        static TrainingFrame F(object raw, out double dt)
        {
            double[] a = C.V(raw);
            dt = a[0];
            return new TrainingFrame(a[1], a[2], a[3], a[4], a[5] > 0.5, a[6] > 0.5);
        }

        static TrainingStep Step(JObj s)
        {
            // one step through the same parser as the file, as a lesson of one step
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"version\":1,\"lessons\":[{\"id\":\"x\",\"steps\":[").Append(WebGL.Telemetry.Json.TelemetryJson.Write(s)).Append("]}]}");
            List<Lesson> l = Training.ParseLessons(sb.ToString());
            Assert.IsNotNull(l, "step does not parse: " + sb);
            return l[0].steps[0];
        }

        static void AssertHint(object expected, string got, string what)
        {
            if (expected == null) Assert.IsNull(got, what + " hint"); else Assert.AreEqual((string)expected, got, what + " hint");
        }

        [Test]
        public void Golden_Constants_MatchTheWeb()
        {
            var k = C.O(C.Golden("training"), "constants");
            Assert.AreEqual(C.N(k, "hint_after_s"), Training.HintAfterS);
            Assert.AreEqual(C.N(k, "progress_eps"), Training.ProgressEps);
            var d = C.O(k, "defaults");
            Assert.AreEqual(C.N(d, "takeoffTol"), Training.TakeoffTol);
            Assert.AreEqual(C.N(d, "takeoffWithinS"), Training.TakeoffWithinS);
            Assert.AreEqual(C.N(d, "holdTolH"), Training.HoldTolH);
            Assert.AreEqual(C.N(d, "holdTolAlt"), Training.HoldTolAlt);
            Assert.AreEqual(C.N(d, "reachTol"), Training.ReachTol);
            Assert.AreEqual(C.N(d, "orbitTurns"), Training.OrbitTurns);
            Assert.AreEqual(C.N(d, "yawTol"), Training.YawTol);
        }

        [Test]
        public void Lessons_AreTheOnesOfTheGolden()
        {
            JArr exp = C.A(C.Golden("training"), "lessons");
            List<Lesson> got = Lessons();
            Assert.AreEqual(exp.Count, got.Count);
            for (int i = 0; i < exp.Count; i++)
            {
                var e = C.O(exp[i]);
                Assert.AreEqual(C.S(e, "id"), got[i].id);
                JArr steps = C.A(e, "steps");
                Assert.AreEqual(steps.Count, got[i].steps.Count, got[i].id);
                for (int j = 0; j < steps.Count; j++)
                {
                    Assert.AreEqual(C.S(C.O(steps[j]), "id"), got[i].steps[j].id);
                    Assert.AreEqual(C.S(C.O(steps[j]), "type"), got[i].steps[j].type.ToString().ToLowerInvariant());
                }
            }
        }

        [Test]
        public void ParseLessons_RejectsWhatIsNotAVersion1File()
        {
            Assert.IsNull(Training.ParseLessons(null));
            Assert.IsNull(Training.ParseLessons("{"));
            Assert.IsNull(Training.ParseLessons("{\"version\":2,\"lessons\":[]}"));
            Assert.IsNull(Training.ParseLessons("{\"version\":1,\"lessons\":[{\"id\":\"a\",\"steps\":[{\"id\":\"s\",\"type\":\"nope\"}]}]}"));
            Assert.IsNull(Training.ParseLessons("{\"version\":1,\"lessons\":[{\"id\":\"a\",\"steps\":[{\"id\":\"s\",\"type\":\"takeoff\",\"alt\":3},{\"id\":\"s\",\"type\":\"takeoff\",\"alt\":3}]}]}"), "duplicate step ids");
        }

        [Test]
        public void Golden_Probes()
        {
            foreach (object o in C.A(C.Golden("training"), "probes"))
            {
                var c = C.O(o);
                string name = C.S(c, "name");
                TrainingFrame f = F(c["frame"], out double dt);
                StepResult r = Training.StepCheck(Step(C.O(c, "step")), new StepCtx(), f, dt, Courses);
                Assert.AreEqual(C.B(c, "done"), r.done, name);
                Assert.AreEqual(C.N(c, "progress"), r.progress, Tol, name);
                AssertHint(c["hint"], r.hint, name);
            }
        }

        [Test]
        public void Golden_Scripts_WithTheContextCarried()
        {
            foreach (object o in C.A(C.Golden("training"), "scripts"))
            {
                var c = C.O(o);
                string name = C.S(c, "name");
                TrainingStep step = Step(C.O(c, "step"));
                JArr frames = C.A(c, "frames"), expect = C.A(c, "expect");
                int doneAt = C.IsNull(c, "done_at") ? -1 : C.I(c, "done_at");
                var byIndex = new Dictionary<int, JObj>();
                foreach (object e in expect) byIndex[C.I(C.O(e), "i")] = C.O(e);
                StepCtx ctx = new StepCtx();
                for (int i = 0; i < frames.Count; i++)
                {
                    TrainingFrame f = F(frames[i], out double dt);
                    StepResult r = Training.StepCheck(step, ctx, f, dt, Courses);
                    ctx = r.ctx;
                    if (byIndex.TryGetValue(i, out JObj e))
                    {
                        Assert.AreEqual(C.B(e, "done"), r.done, name + " i=" + i);
                        Assert.AreEqual(C.N(e, "progress"), r.progress, Tol, name + " i=" + i);
                        AssertHint(e["hint"], r.hint, name + " i=" + i);
                    }
                    if (r.done) { Assert.AreEqual(doneAt, i, name + " done_at"); break; }
                    Assert.AreNotEqual(i, doneAt, name + " should have finished at " + i);
                }
                Assert.AreEqual(C.N(c, "final_ctx_t"), ctx.t, 1e-6, name + " final_ctx_t");
            }
        }

        [Test]
        public void Golden_Targets()
        {
            foreach (object o in C.A(C.Golden("training"), "targets"))
            {
                var c = C.O(o);
                string name = C.S(c, "name");
                var ctx = new StepCtx();
                if (!C.IsNull(c, "origin")) ctx.origin = C.V(c["origin"]);
                TargetMark m = Training.StepTarget(Step(C.O(c, "step")), ctx);
                if (C.IsNull(c, "mark")) { Assert.IsNull(m, name); continue; }
                var e = C.O(c, "mark");
                Assert.IsNotNull(m, name);
                Assert.AreEqual(C.S(e, "kind"), m.kind, name);
                foreach (var kv in e)
                {
                    if (kv.Key == "kind") continue;
                    double got = kv.Key == "n" ? m.n : kv.Key == "e" ? m.e : kv.Key == "alt" ? m.alt : kv.Key == "r" ? m.r : kv.Key == "h" ? m.h : kv.Key == "rMin" ? m.rMin : kv.Key == "rMax" ? m.rMax : double.NaN;
                    Assert.AreEqual((double)kv.Value, got, Tol, name + " " + kv.Key);
                }
            }
        }

        [Test]
        public void Golden_LessonRuns()
        {
            foreach (object o in C.A(C.Golden("training"), "lesson_runs"))
            {
                var c = C.O(o);
                string name = C.S(c, "name");
                Lesson lesson = LessonById(C.S(c, "lesson"));
                Assert.IsNotNull(lesson, name);
                var retries = new HashSet<int>();
                foreach (object r in C.A(c, "retry_before_frames")) retries.Add((int)C.N(r));
                var trace = new Dictionary<int, JObj>();
                foreach (object t in C.A(c, "trace")) trace[C.I(C.O(t), "i")] = C.O(t);
                LessonRun run = Training.StartLesson(lesson, C.I(c, "from"));
                JArr frames = C.A(c, "frames");
                for (int i = 0; i < frames.Count; i++)
                {
                    if (retries.Contains(i)) run = Training.RetryStep(run);
                    TrainingFrame f = F(frames[i], out double dt);
                    var events = new List<LessonEvent>();
                    run = Training.LessonStep(lesson, run, f, dt, events, Courses);
                    if (!trace.TryGetValue(i, out JObj t)) continue;
                    string w = name + " i=" + i;
                    JArr ev = C.A(t, "events");
                    Assert.AreEqual(ev.Count, events.Count, w + " events");
                    for (int k = 0; k < ev.Count; k++)
                    {
                        var e = C.O(ev[k]);
                        Assert.AreEqual(C.S(e, "kind"), events[k].kind, w);
                        if (events[k].kind == "step") Assert.AreEqual(C.I(e, "index"), events[k].index, w);
                        else Assert.AreEqual(C.N(e, "timeS"), events[k].timeS, Tol, w);
                    }
                    var rr = C.O(t, "run");
                    Assert.AreEqual(C.I(rr, "step"), run.step, w);
                    Assert.AreEqual(C.N(rr, "progress"), run.progress, Tol, w);
                    AssertHint(rr["hint"], run.hint, w);
                    Assert.AreEqual(C.N(rr, "elapsed_s"), run.elapsedS, Tol, w);
                    Assert.AreEqual(C.N(rr, "fraction"), Training.LessonFraction(run), Tol, w);
                    Assert.AreEqual(C.B(rr, "from_start"), run.fromStart, w);
                    Assert.AreEqual(C.B(rr, "done"), run.done, w);
                }
            }
        }

        static void AssertProgress(JObj expected, Dictionary<string, LessonRecord> got, string name)
        {
            Assert.AreEqual(expected.Count, got.Count, name);
            foreach (var kv in expected)
            {
                var e = C.O(kv.Value);
                Assert.IsTrue(got.TryGetValue(kv.Key, out LessonRecord r), name + " " + kv.Key);
                Assert.AreEqual(C.I(e, "step"), r.step, name);
                Assert.AreEqual(C.B(e, "done"), r.done, name);
                if (e["bestS"] == null) Assert.IsFalse(r.bestS.HasValue, name); else Assert.AreEqual(C.N(e, "bestS"), r.bestS.Value, Tol, name);
            }
        }

        [Test]
        public void Golden_Progress_Storage_RecordFlow_Resume()
        {
            var p = C.O(C.Golden("training"), "progress");
            foreach (object o in C.A(p, "storage"))
            {
                var c = C.O(o);
                AssertProgress(C.O(c, "out"), Training.ParseProgress(c["raw"] as string), C.S(c, "name"));
            }

            var flow = new Dictionary<string, LessonRecord>();
            foreach (object o in C.A(p, "record_flow"))
            {
                var c = C.O(o);
                Lesson lesson = LessonById(C.S(c, "lesson"));
                bool done = C.B(c, "done");
                var run = new LessonRun { lessonId = lesson.id, total = lesson.steps.Count, step = C.I(c, "step"), done = done, elapsedS = C.N(c, "elapsed_s"), fromStart = C.B(c, "from_start") };
                flow = Training.RecordRun(flow, run);
                AssertProgress(C.O(c, "progress"), flow, C.S(c, "name"));
                AssertProgress(C.O(c, "progress"), Training.ParseProgress(Training.ProgressToJson(flow)), C.S(c, "name") + " (stored)");
            }

            foreach (object o in C.A(p, "resume_from"))
            {
                var c = C.O(o);
                LessonRecord rec = null;
                if (!C.IsNull(c, "rec"))
                {
                    var r = C.O(c, "rec");
                    rec = new LessonRecord { step = C.I(r, "step"), done = C.B(r, "done"), bestS = r["bestS"] == null ? (double?)null : C.N(r, "bestS") };
                }
                Assert.AreEqual(C.I(c, "out"), Training.ResumeFrom(LessonById(C.S(c, "lesson")), rec), C.S(c, "name"));
            }
        }
    }
}
