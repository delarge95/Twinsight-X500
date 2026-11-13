// PLAN_PC 28.3: the layout and the scoring of the simulated test course (pure C#). A scripted flight through the route finishes the lap with no
// collision; a flight into a pylon / the wall / the tube of a ring counts one collision per contact; a ring only counts when it is crossed along its
// heading and inside the opening; checkpoints are ordered; the timer starts with the take-off; everything lies inside the 120 m geofence.
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace WebGL.Telemetry.Tests
{
    public class TestCourseTests
    {
        const double Dt = 1.0 / 30.0;

        /// <summary>Flies the route as a straight polyline at 6 m/s (the simulator's top speed) from the pad; returns the run.</summary>
        static CourseRun FlyRoute(CourseLayout layout, Action<CourseRun, double[]> perStep = null)
        {
            var run = new CourseRun(layout);
            double n = 0, e = 0, a = 0;
            // take-off to the first altitude
            for (int i = 0; i < 40; i++) { a += 0.1; run.Step(Dt, n, e, a); }
            foreach (var w in layout.Route)
            {
                double dn = w[0] - n, de = w[1] - e, da = w[2] - a;
                double dist = Math.Sqrt(dn * dn + de * de + da * da);
                int steps = Math.Max(1, (int)Math.Ceiling(dist / (6.0 * Dt)));
                for (int s = 1; s <= steps; s++)
                {
                    double f = s / (double)steps;
                    double[] p = { n + dn * f, e + de * f, a + da * f };
                    run.Step(Dt, p[0], p[1], p[2]);
                    perStep?.Invoke(run, p);
                }
                n = w[0]; e = w[1]; a = w[2];
            }
            return run;
        }

        [Test]
        public void TheDefaultLayout_HasTheRequiredElements_InsideTheGeofence()
        {
            var l = CourseLayout.Default();
            Assert.AreEqual(4, l.Gates.Count, "3-4 rings");
            Assert.AreEqual(3, l.Zones.Count, "2-3 zones");
            Assert.AreEqual(5, l.Pylons.Count, "cones / pylons (a slalom of three and two more)");
            Assert.AreEqual(4, l.Wall.Count, "a wall with a gap: two slabs, a lintel and a sill");
            Assert.AreEqual(7, l.Checkpoints.Count);
            Assert.Less(l.FarthestFromOrigin(), SimLimits.Radius, "everything is inside the 120 m geofence");
            foreach (var g in l.Gates) Assert.Less(g.Alt + g.Radius, SimLimits.Ceiling);
            // the last checkpoint is the zone over the pad
            Checkpoint last = l.Checkpoints[l.Checkpoints.Count - 1];
            Assert.AreEqual(CheckpointKind.Zone, last.Kind);
            Assert.AreEqual(l.PadN, l.Zones[last.Index].N, 1e-9);
            Assert.AreEqual(l.PadE, l.Zones[last.Index].E, 1e-9);
        }

        [Test]
        public void TheScriptedRoute_FinishesTheLap_InOrder_WithNoCollision()
        {
            var layout = CourseLayout.Default();
            var order = new List<int>();
            var run = FlyRoute(layout, (r, p) => { foreach (var ev in r.Events) if (ev.Kind == CourseEventKind.Checkpoint) order.Add(ev.Number); r.Events.Clear(); });
            Assert.AreEqual(CourseState.Finished, run.State);
            Assert.AreEqual(layout.Checkpoints.Count, run.Reached);
            Assert.AreEqual(0, run.Collisions, "the clean route clears every pylon, the wall gap and the ring tubes");
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7 }, order, "checkpoints come in order");
            Assert.Greater(run.FinishTime, 20);
            Assert.Less(run.FinishTime, 120, "a lap is about a minute");
        }

        [Test]
        public void TheTimer_StartsWithTheTakeoff_AndStopsAtTheFinish()
        {
            var layout = CourseLayout.Default();
            var run = new CourseRun(layout);
            for (int i = 0; i < 100; i++) run.Step(Dt, 0, 0, 0.2);
            Assert.AreEqual(CourseState.Waiting, run.State);
            Assert.AreEqual(0, run.Elapsed, 1e-12, "on the pad the timer does not run");
            run.Step(Dt, 0, 0, 0.3);
            run.Step(Dt, 0, 0, 0.8);
            Assert.AreEqual(CourseState.Running, run.State);
            Assert.AreEqual(CourseEventKind.Started, run.Events[0].Kind);
            double t0 = run.Elapsed;
            for (int i = 0; i < 30; i++) run.Step(Dt, 0, 0, 1.0);
            Assert.AreEqual(t0 + 30 * Dt, run.Elapsed, 1e-9);
        }

        [Test]
        public void AGate_CountsOnlyWhenCrossedAlongItsHeading_InsideTheOpening()
        {
            var g = new CourseGate { N = 10, E = 0, Alt = 3, AzimuthDeg = 0, Radius = 2.6 };
            Assert.IsTrue(CourseRun.CrossesGate(g, 9, 0, 3, 11, 0, 3), "through the middle, northwards");
            Assert.IsFalse(CourseRun.CrossesGate(g, 11, 0, 3, 9, 0, 3), "the wrong way");
            Assert.IsFalse(CourseRun.CrossesGate(g, 9, 4, 3, 11, 4, 3), "beside the ring");
            Assert.IsFalse(CourseRun.CrossesGate(g, 9, 0, 6.5, 11, 0, 6.5), "above the ring");
            Assert.IsFalse(CourseRun.CrossesGate(g, 9, 2.55, 3, 11, 2.55, 3), "across the tube itself is not the opening");
            var east = new CourseGate { N = 0, E = 10, Alt = 3, AzimuthDeg = 90, Radius = 2.6 };
            Assert.IsTrue(CourseRun.CrossesGate(east, 0, 9, 3, 0, 11, 3), "a ring facing east is crossed eastwards");
            Assert.IsFalse(CourseRun.CrossesGate(east, 9, 0, 3, 11, 0, 3), "and not northwards");
            var south = new CourseGate { N = 10, E = 0, Alt = 3, AzimuthDeg = 180, Radius = 2.6 };
            Assert.IsTrue(CourseRun.CrossesGate(south, 11, 0, 3, 9, 0, 3), "a ring facing south is crossed southwards");
        }

        [Test]
        public void Checkpoints_AreOrdered_APassOutOfOrderDoesNotCount()
        {
            var layout = CourseLayout.Default();
            var run = new CourseRun(layout);
            for (int i = 0; i < 5; i++) run.Step(Dt, 0, 0, 0.1);
            run.Step(Dt, 0, 0, 1);                                // take off from the pad
            run.Step(Dt, 0, 30, 4.5);                             // aside
            run.Step(Dt, 51, 30, 4.5);
            run.Step(Dt, 51, 0, 4.5);
            // fly through the SECOND gate first (52, 0): not the next checkpoint (the first gate is)
            run.Step(Dt, 53, 0, 4.5);
            Assert.AreEqual(0, run.Reached, "the second ring is not the next checkpoint");
            run.Step(Dt, 5, 20, 3.5);
            run.Step(Dt, 11, 0, 3.5);
            run.Step(Dt, 13, 0, 3.5);
            Assert.AreEqual(1, run.Reached, "the first ring counts");
            run.Step(Dt, 24, 0, 3.5);
            Assert.AreEqual(2, run.Reached, "then the zone");
        }

        [Test]
        public void ACollision_IsCountedOncePerContact_AndAgainAfterLeaving()
        {
            var layout = CourseLayout.Default();
            var run = new CourseRun(layout);
            run.Step(Dt, 0, 0, 1);
            run.Events.Clear();
            var p = layout.Pylons[0];                              // (32, -2.5), r 0.5, h 5
            for (int i = 0; i < 20; i++) run.Step(Dt, p.N, p.E + 0.3, 3);   // inside the pylon for 20 frames
            Assert.AreEqual(1, run.Collisions, "one contact = one count, not one per frame");
            Assert.AreEqual("pylon:0", run.Events.Find(ev => ev.Kind == CourseEventKind.Collision).Obstacle);
            for (int i = 0; i < 5; i++) run.Step(Dt, p.N, p.E + 6, 3);       // away
            for (int i = 0; i < 5; i++) run.Step(Dt, p.N, p.E - 0.2, 2);     // and back in
            Assert.AreEqual(2, run.Collisions, "a new contact counts again");
        }

        [Test]
        public void TheSimpleVolumeTests_AreWhatTheyClaim()
        {
            var pylon = new CoursePylon { N = 0, E = 0, Radius = 0.5, Height = 5 };
            Assert.IsTrue(CourseRun.HitsPylon(pylon, 0.9, 0, 2), "within radius + drone radius");
            Assert.IsFalse(CourseRun.HitsPylon(pylon, 1.1, 0, 2));
            Assert.IsFalse(CourseRun.HitsPylon(pylon, 0, 0, 6), "flying over the top");
            Assert.IsTrue(CourseRun.HitsPylon(pylon, 0, 0, 5.2), "the top of the pylon plus the drone radius");

            var slab = new CourseBox { N0 = 0, N1 = 5, E0 = 0, E1 = 1, AltMin = 0, AltMax = 9 };
            Assert.IsTrue(CourseRun.HitsBox(slab, 2, 0.5, 4), "inside");
            Assert.IsTrue(CourseRun.HitsBox(slab, 2, 1.3, 4), "touching within the drone radius");
            Assert.IsFalse(CourseRun.HitsBox(slab, 2, 1.6, 4));

            var ring = new CourseGate { N = 0, E = 0, Alt = 3, AzimuthDeg = 0, Radius = 2.6 };
            Assert.IsFalse(CourseRun.HitsRing(ring, 0, 0, 3), "the opening is free");
            Assert.IsTrue(CourseRun.HitsRing(ring, 0, 2.6, 3), "the tube");
            Assert.IsTrue(CourseRun.HitsRing(ring, 0.2, 2.9, 3), "just outside the tube, within the drone radius");
            Assert.IsFalse(CourseRun.HitsRing(ring, 3, 0, 3), "away along the axis");
        }

        [Test]
        public void TheWallGap_IsFlyable_AndTheWallIsNot()
        {
            var layout = CourseLayout.Default();
            bool Hit(double n, double e, double a) { foreach (var b in layout.Wall) if (CourseRun.HitsBox(b, n, e, a)) return true; return false; }
            Assert.IsFalse(Hit(60, 20, 3.5), "the middle of the gap");
            Assert.IsTrue(Hit(54, 20, 3.5), "through a slab");
            Assert.IsTrue(Hit(60, 20, 7.5), "through the lintel");
            Assert.IsTrue(Hit(60, 20, 0.3), "through the sill");
        }

        [Test]
        public void AResetInTheAir_DoesNotStartALap_UntilTheFirstCheckpoint()
        {
            var layout = CourseLayout.Default();
            var run = new CourseRun(layout);
            for (int i = 0; i < 10; i++) run.Step(Dt, 0, 0, 0.1);
            run.Step(Dt, 0, 0, 3.5);
            Assert.AreEqual(CourseState.Running, run.State);
            run.Reset();
            for (int i = 0; i < 50; i++) run.Step(Dt, 1, 1, 3.5);       // hovering over the pad in the air
            Assert.AreEqual(CourseState.Waiting, run.State, "a reset in the air does not start the lap by itself");
            run.Step(Dt, 11, 0, 3.5);
            run.Step(Dt, 13, 0, 3.5);                                     // through the first ring
            Assert.AreEqual(CourseState.Running, run.State);
            Assert.AreEqual(1, run.Reached, "the ring that started the lap also counts");
        }

        [Test]
        public void ATakeoffFarFromThePad_DoesNotStartTheLap()
        {
            var run = new CourseRun(CourseLayout.Default());
            for (int i = 0; i < 10; i++) run.Step(Dt, 40, 40, 0.1);
            run.Step(Dt, 40, 40, 3);
            Assert.AreEqual(CourseState.Waiting, run.State, "the take-off must be from the pad «H»");
        }

        [Test]
        public void Reset_StartsAFreshLap()
        {
            var layout = CourseLayout.Default();
            var run = FlyRoute(layout, (r, p) => r.Events.Clear());
            Assert.AreEqual(CourseState.Finished, run.State);
            run.Reset();
            Assert.AreEqual(CourseState.Waiting, run.State);
            Assert.AreEqual(0, run.Reached);
            Assert.AreEqual(0, run.Collisions);
            Assert.AreEqual(0, run.Elapsed, 1e-12);
            Assert.IsEmpty(run.Events);
        }
    }
}
