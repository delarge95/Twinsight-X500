// Lote 1c, tool 5: the course as data (StreamingAssets/Telemetry/courses/basic.json = the web's contracts/courses/basic.json), its scoring, records and ghost
// (Telemetry/Sim/CourseRecords.cs) against contracts/golden/course.json (produced by the web's src/shared/course.ts).
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace WebGL.Telemetry.Tests
{
    using JObj = Dictionary<string, object>;
    using JArr = List<object>;

    public class CourseRecordsTests
    {
        const double Tol = 1e-9;

        static string BundledText() => File.ReadAllText(Path.Combine(Application.dataPath, "StreamingAssets", "Telemetry", "courses", "basic.json"));

        [Test]
        public void Constants_MatchTheWeb()
        {
            var k = C.O(C.Golden("course"), "constants");
            Assert.AreEqual(C.N(k, "penalty_s"), CourseRecords.PenaltyS);
            Assert.AreEqual((int)C.N(k, "max_attempts"), CourseRecords.MaxAttempts);
            Assert.AreEqual((int)C.N(k, "ghost_hz"), CourseRecords.GhostHz);
            Assert.AreEqual(C.N(k, "drone_radius"), CourseRun.DroneRadius);
            Assert.AreEqual(C.N(k, "ring_tube"), CourseRun.RingTube);
            Assert.AreEqual(C.N(k, "start_altitude"), CourseRun.StartAltitude);
            Assert.AreEqual(C.N(k, "start_radius"), CourseRun.StartRadius);
        }

        [Test]
        public void BundledJson_IsTheSameLayoutAsTheCSharpCopy()
        {
            CourseLayout json = CourseLayout.Parse(BundledText());
            Assert.IsNotNull(json, "basic.json must parse as a version-1 course");
            Assert.IsTrue(json.ConstantsMatchTheRun());
            Assert.AreEqual(CourseLayout.Builtin().Canonical(), json.Canonical());
            Assert.AreEqual("basic", json.Id);
        }

        [Test]
        public void BundledJson_MatchesTheWebLayoutChecks()
        {
            var lc = C.O(C.Golden("course"), "layout_checks");
            var n = C.O(lc, "counts");
            CourseLayout L = CourseLayout.Parse(BundledText());
            Assert.AreEqual(C.I(n, "gates"), L.Gates.Count);
            Assert.AreEqual(C.I(n, "zones"), L.Zones.Count);
            Assert.AreEqual(C.I(n, "pylons"), L.Pylons.Count);
            Assert.AreEqual(C.I(n, "wall"), L.Wall.Count);
            Assert.AreEqual(C.I(n, "checkpoints"), L.Checkpoints.Count);
            Assert.AreEqual(C.I(n, "route"), L.Route.Count);
            var last = C.O(lc, "last_checkpoint");
            Assert.AreEqual(C.S(last, "kind") == "gate" ? CheckpointKind.Gate : CheckpointKind.Zone, L.Checkpoints[L.Checkpoints.Count - 1].Kind);
            Assert.AreEqual(C.I(last, "index"), L.Checkpoints[L.Checkpoints.Count - 1].Index);
        }

        [Test]
        public void Parse_RejectsWhatIsNotAVersion1Course()
        {
            Assert.IsNull(CourseLayout.Parse(null));
            Assert.IsNull(CourseLayout.Parse("{"));
            Assert.IsNull(CourseLayout.Parse("{\"version\":2,\"id\":\"x\"}"));
        }

        [Test]
        public void Default_IsTheSameLayoutAsTheCSharpCopy()
        {
            Assert.AreEqual(CourseLayout.Builtin().Canonical(), CourseLayout.Default().Canonical());
        }

        [Test]
        public void Golden_Score()
        {
            foreach (object o in C.A(C.Golden("course"), "score"))
            {
                var c = C.O(o);
                var r = C.O(c, "run");
                var exp = C.O(c, "out");
                CourseResult s = CourseRecords.Score(C.S(r, "state") == "finished", C.N(r, "finish_time"), C.N(r, "elapsed"), C.I(r, "collisions"));
                Assert.AreEqual(C.N(exp, "timeS"), s.TimeS, Tol, C.S(c, "name"));
                Assert.AreEqual(C.N(exp, "penalisedS"), s.PenalisedS, Tol, C.S(c, "name"));
                Assert.AreEqual(C.I(exp, "collisions"), s.Collisions, C.S(c, "name"));
            }
        }

        static void AssertRecords(JObj expected, CourseRecordSet got, string name)
        {
            var courses = C.O(expected, "courses");
            Assert.AreEqual(courses.Count, got.Courses.Count, name);
            foreach (var kv in courses)
            {
                var e = C.O(kv.Value);
                CourseRecord r = got.Of(kv.Key);
                Assert.IsNotNull(r, name + " " + kv.Key);
                if (e["bestClean"] == null) Assert.IsNull(r.BestClean, name); else Assert.AreEqual(C.N(e, "bestClean"), r.BestClean.Value, Tol, name);
                if (e["bestPenalised"] == null) Assert.IsNull(r.BestPenalised, name); else Assert.AreEqual(C.N(e, "bestPenalised"), r.BestPenalised.Value, Tol, name);
                JArr att = C.A(e, "attempts");
                Assert.AreEqual(att.Count, r.Attempts.Count, name);
                for (int i = 0; i < att.Count; i++)
                {
                    var a = C.O(att[i]);
                    Assert.AreEqual(C.N(a, "at"), r.Attempts[i].At, Tol, name);
                    Assert.AreEqual(C.N(a, "timeS"), r.Attempts[i].TimeS, Tol, name);
                    Assert.AreEqual(C.N(a, "penalisedS"), r.Attempts[i].PenalisedS, Tol, name);
                    Assert.AreEqual(C.I(a, "collisions"), r.Attempts[i].Collisions, name);
                }
            }
        }

        [Test]
        public void Golden_UpdateRecords_StepByStep()
        {
            var steps = C.A(C.O(C.Golden("course"), "records"), "steps");
            CourseRecordSet rec = CourseRecords.Empty();
            foreach (object o in steps)
            {
                var s = C.O(o);
                var r = C.O(s, "result");
                var res = new CourseResult { TimeS = C.N(r, "timeS"), PenalisedS = C.N(r, "penalisedS"), Collisions = C.I(r, "collisions") };
                CourseRecordUpdate u = CourseRecords.Update(rec, C.S(s, "courseId"), res, C.N(s, "now"));
                Assert.AreEqual(C.B(s, "new_best_clean"), u.NewBestClean, C.S(s, "name"));
                Assert.AreEqual(C.B(s, "new_best_penalised"), u.NewBestPenalised, C.S(s, "name"));
                AssertRecords(C.O(s, "records"), u.Records, C.S(s, "name"));
                rec = u.Records;
                AssertRecords(C.O(s, "records"), CourseRecords.Parse(CourseRecords.ToJson(rec)), C.S(s, "name") + " (stored)");
            }
        }

        [Test]
        public void Golden_Records_Storage()
        {
            foreach (object o in C.A(C.O(C.Golden("course"), "records"), "storage"))
            {
                var c = C.O(o);
                AssertRecords(C.O(c, "out"), CourseRecords.Parse(c["raw"] as string), C.S(c, "name"));
            }
        }

        [Test]
        public void Golden_Ghost_PoseAndStorage()
        {
            var g = C.O(C.Golden("course"), "ghost");
            foreach (object o in C.A(g, "storage"))
            {
                var c = C.O(o);
                Assert.AreEqual(C.B(c, "ok"), CourseRecords.ParseGhost(c["raw"] as string) != null, C.S(c, "name"));
            }
            var wrap = C.O(g, "yaw_wraps_the_short_way");
            var trace = new List<double[]>();
            foreach (object s in C.A(wrap, "trace")) trace.Add(C.V(s));
            foreach (object o in C.A(wrap, "poses"))
            {
                var c = C.O(o);
                double[] p = CourseRecords.GhostPose(trace, C.N(c, "t")), e = C.V(c, "out");
                for (int i = 0; i < 4; i++) Assert.AreEqual(e[i], p[i], 1e-6, "t=" + C.N(c, "t") + " i=" + i);
            }
            Assert.IsNull(CourseRecords.GhostPose(new List<double[]>(), 1));
        }

        [Test]
        public void Ghost_PushKeepsOneSampleEveryTenthOfASecond()
        {
            var t = new List<double[]>();
            Assert.IsTrue(CourseRecords.PushGhost(t, 0, 1, 2, 3, 0));
            Assert.IsFalse(CourseRecords.PushGhost(t, 0.05, 1, 2, 3, 0));
            Assert.IsTrue(CourseRecords.PushGhost(t, 0.1, 1, 2, 3, 0));
            Assert.AreEqual(2, t.Count);
            Assert.IsNotNull(CourseRecords.ParseGhost(CourseRecords.GhostToJson(t)));
        }
    }
}
