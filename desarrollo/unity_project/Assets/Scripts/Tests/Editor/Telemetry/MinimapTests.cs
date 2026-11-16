// Lote 1b, tool 1: the top-down map logic (Telemetry/Logic/Minimap.cs) against contracts/golden/minimap.json, produced by the web's own src/shared/minimap.ts
// (tools/contracts/golden_minimap.ts): yaw of 8 quaternions, the trail with its segment changes and its ring, the fitted and followed views, the scale bars and
// their labels, and world-to-screen in both orientations.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace WebGL.Telemetry.Tests
{
    using JObj = Dictionary<string, object>;
    using JArr = List<object>;

    public class MinimapTests
    {
        static double Tol(JObj g) => C.N(g, "tolerance");

        static List<double> Flat(JArr pts)
        {
            var l = new List<double>();
            foreach (object p in pts) { double[] v = C.V(p); l.Add(v[0]); l.Add(v[1]); }
            return l;
        }

        static MapFence Fence(JObj c)
        {
            if (C.IsNull(c, "fence")) return null;
            var f = C.O(c, "fence");
            return new MapFence(C.N(f, "radius"), f.ContainsKey("n") ? C.N(f, "n") : 0, f.ContainsKey("e") ? C.N(f, "e") : 0);
        }

        static MapView View(JObj v) => new MapView(C.N(v, "scale"), C.N(v, "centerN"), C.N(v, "centerE"));

        [Test]
        public void Golden_Constants_MatchTheWeb()
        {
            var k = C.O(C.Golden("minimap"), "constants");
            Assert.AreEqual(C.N(k, "min_scale"), Minimap.MinScale);
            Assert.AreEqual(C.N(k, "trail_max"), Minimap.TrailMax);
            Assert.AreEqual(C.N(k, "trail_min_step"), Minimap.TrailMinStep);
            Assert.AreEqual(C.N(k, "follow_min_scale"), Minimap.FollowMinScale);
        }

        [Test]
        public void Golden_Yaw_OfEightQuaternions()
        {
            var g = C.Golden("minimap");
            var cs = new Cases("minimap.yaw", Tol(g));
            foreach (JObj c in C.A(g, "yaw"))
            {
                cs.Case();
                // the file keeps the quaternion rounded to 9 decimals: the yaw of a tilted attitude moves by a few 1e-9 with it, so the input rounding is allowed for
                cs.Near(C.N(c, "out"), Minimap.YawFromQuat(C.V(c, "q")), C.S(c, "name"), 1e-8);
            }
            cs.Done();
            Assert.AreEqual(8, C.A(g, "yaw").Count);
        }

        [Test]
        public void Golden_Trail_SegmentChanges_Ring_AndNewHello()
        {
            var g = C.Golden("minimap");
            var tr = C.O(g, "trail");
            var cs = new Cases("minimap.trail", Tol(g));
            var trail = new MapTrail();
            JArr pushes = C.A(tr, "pushes"), steps = C.A(tr, "steps");
            int segChanges = 0;
            int? lastSeg = null;
            for (int i = 0; i < pushes.Count; i++)
            {
                cs.Case();
                var p = (JObj)pushes[i];
                var e = (JObj)steps[i];
                int seg = C.I(p, "seg");
                if (lastSeg.HasValue && lastSeg.Value != seg) segChanges++;
                lastSeg = seg;
                trail.Push(C.V(p, "p"), seg);
                cs.Eq(C.I(e, "seg"), trail.Seg ?? -1, "push " + i + " seg");
                cs.Eq(C.I(e, "length"), trail.Count, "push " + i + " length");
                double[] last = C.V(e, "last");
                cs.Near(last[0], trail.N(trail.Count - 1), "push " + i + " last n");
                cs.Near(last[1], trail.E(trail.Count - 1), "push " + i + " last e");
            }
            Assert.AreEqual(3, segChanges, "the golden trail has 3 segment changes");
            var fin = C.O(tr, "final");
            cs.Case();
            cs.Eq(C.I(fin, "seg"), trail.Seg ?? -1, "final seg");
            SamePoints(cs, C.A(fin, "points"), trail, "final");

            // the ring: the oldest points above `max` are dropped
            var ring = C.O(tr, "ring");
            int max = C.I(ring, "max");
            var rt = new MapTrail();
            JArr rp = C.A(ring, "pushes"), rs = C.A(ring, "steps");
            for (int i = 0; i < rp.Count; i++)
            {
                cs.Case();
                var p = (JObj)rp[i];
                rt.Push(C.V(p, "p"), C.I(p, "seg"), Minimap.TrailMinStep, max);
                SamePoints(cs, (JArr)rs[i], rt, "ring " + i);
            }

            // a new hello = an empty trail (the caller does it), then the next push starts it again
            var nh = C.O(tr, "new_hello");
            var ht = new MapTrail();
            foreach (object p in C.A(nh, "before")) ht.Push(C.V(p), C.I(nh, "seg"));
            cs.Case();
            SamePoints(cs, C.A(nh, "before"), ht, "new_hello before");
            ht.Clear();
            cs.Eq(0, ht.Count, "cleared");
            cs.True(!ht.Seg.HasValue, "cleared seg");
            foreach (object p in C.A(nh, "after")) ht.Push(C.V(p), C.I(nh, "seg"));
            SamePoints(cs, C.A(nh, "after"), ht, "new_hello after");
            cs.Done();
        }

        static void SamePoints(Cases cs, JArr expected, MapTrail t, string what)
        {
            cs.Eq(expected.Count, t.Count, what + " count");
            for (int i = 0; i < Math.Min(expected.Count, t.Count); i++)
            {
                double[] e = C.V(expected[i]);
                cs.Near(e[0], t.N(i), what + "[" + i + "] n");
                cs.Near(e[1], t.E(i), what + "[" + i + "] e");
            }
        }

        [Test]
        public void Golden_Fit_AndFollow()
        {
            var g = C.Golden("minimap");
            var cs = new Cases("minimap.fit", Tol(g));
            foreach (JObj c in C.A(g, "fit"))
            {
                cs.Case();
                string name = C.S(c, "name");
                MapView v = Minimap.FitView(Flat(C.A(c, "points")), Fence(c), C.N(c, "sizePx"), C.N(c, "marginPx"));
                var o = C.O(c, "out");
                cs.Near(C.N(o, "scale"), v.scale, name + " scale");
                cs.Near(C.N(o, "centerN"), v.centerN, name + " centerN");
                cs.Near(C.N(o, "centerE"), v.centerE, name + " centerE");
            }
            cs.Done();
            var cf = new Cases("minimap.follow", Tol(g));
            foreach (JObj c in C.A(g, "follow"))
            {
                cf.Case();
                MapView v = Minimap.FollowView(C.N(c, "n"), C.N(c, "e"), C.N(c, "fitScale"));
                var o = C.O(c, "out");
                cf.Near(C.N(o, "scale"), v.scale, "follow scale");
                cf.Near(C.N(o, "centerN"), v.centerN, "follow centerN");
                cf.Near(C.N(o, "centerE"), v.centerE, "follow centerE");
            }
            cf.Done();
        }

        [Test]
        public void Golden_ScaleBars_AndLabels()
        {
            var g = C.Golden("minimap");
            var cs = new Cases("minimap.scale_bar", Tol(g));
            foreach (JObj c in C.A(g, "scale_bar"))
            {
                cs.Case();
                double scale = C.N(c, "scale"), maxPx = C.N(c, "maxPx");
                Minimap.ScaleBar(scale, maxPx, out double metres, out double px);
                var o = C.O(c, "out");
                cs.Near(C.N(o, "metres"), metres, "bar(" + scale + ", " + maxPx + ") metres");
                cs.Near(C.N(o, "px"), px, "bar(" + scale + ", " + maxPx + ") px");
            }
            cs.Done();
            var cl = new Cases("minimap.scale_label");
            foreach (JObj c in C.A(g, "scale_label"))
            {
                cl.Case();
                cl.Eq(C.S(c, "out"), Minimap.ScaleLabel(C.N(c, "metres")), "label " + C.N(c, "metres"));
            }
            cl.Done();
        }

        [Test]
        public void ScaleLabel_OfAComputedBar_IsClean()
        {
            // 0.5 m is 5 * 10^-1 in floating point: the label must still read "0.5 m"
            Minimap.ScaleBar(0.0125, 80, out double metres, out _);
            Assert.AreEqual("1 m", Minimap.ScaleLabel(metres));
            Minimap.ScaleBar(0.01, 56, out metres, out _);
            Assert.AreEqual("0.5 m", Minimap.ScaleLabel(metres));
        }

        [Test]
        public void Golden_ToScreen_AndAngles()
        {
            var g = C.Golden("minimap");
            var cs = new Cases("minimap.to_screen", Tol(g));
            // the generator (tools/contracts/golden_minimap.ts) projects these five world points with each of five yaws, north up and heading up; the file keeps
            // the yaw rounded to 9 decimals, so the exact value is taken from the same list (and checked against the rounded one)
            JArr src = new JArr { new JArr { 0.0, 0.0 }, new JArr { 30.0, 40.0 }, new JArr { 15.0, 20.0 }, new JArr { -5.0, 10.0 }, new JArr { 40.0, -3.0 } };
            double[] yaws = { 0, Math.PI / 2, Math.PI, -Math.PI / 4, 2.5 };
            int index = 0;
            foreach (JObj c in C.A(g, "to_screen"))
            {
                cs.Case();
                bool up = C.B(c, "headingUp");
                double yaw = yaws[index / 2];
                cs.Near(C.N(c, "yaw"), yaw, "yaw of case " + index);
                cs.Eq(index % 2 == 1, up, "headingUp of case " + index);
                index++;
                MapView view = View(C.O(c, "view"));
                JArr outs = C.A(c, "out");
                Assert.AreEqual(src.Count, outs.Count);
                for (int i = 0; i < src.Count; i++)
                {
                    double[] w = C.V(src[i]);
                    double[] e = C.V(outs[i]);
                    Minimap.ToScreen(w[0], w[1], view, up, yaw, out double x, out double y);
                    cs.Near(e[0], x, "to_screen up=" + up + " yaw=" + yaw + " [" + i + "] x");
                    cs.Near(e[1], y, "to_screen up=" + up + " yaw=" + yaw + " [" + i + "] y");
                }
                var a = C.O(c, "angles");
                Minimap.ViewAngles(up, yaw, out double arrow, out double north);
                cs.Near(C.N(a, "arrow"), arrow, "arrow");
                cs.Near(C.N(a, "north"), north, "north");
            }
            cs.Done();
        }

        [Test]
        public void Spec_FlyTo_30_40_Is50mAndTheBarShows10Or20m()
        {
            // acceptance (b) of the specification: with the simulator, take off and fly_to (30, 40): the arrow is 50 m from the origin and the bar shows 10 or 20 m
            var s = FlightSim.Create();
            FlightSim.Command(s, ClientMessage.Takeoff(10));
            for (int i = 0; i < 1200; i++) FlightSim.Step(s, 1.0 / 120);
            FlightSim.Command(s, ClientMessage.FlyTo(30, 40, 10));
            var trail = new MapTrail();
            for (int i = 0; i < 120 * 40; i++)
            {
                FlightSim.Step(s, 1.0 / 120);
                if (i % 12 == 0) trail.Push(s.p, 0);
            }
            Assert.AreEqual(50, Math.Sqrt(s.p[0] * s.p[0] + s.p[1] * s.p[1]), 0.05);
            var pts = new List<double> { 0, 0 };
            for (int i = 0; i < trail.Count; i++) { pts.Add(trail.N(i)); pts.Add(trail.E(i)); }
            foreach (double size in new[] { 220.0, 360.0 })
            {
                MapView v = Minimap.FitView(pts, null, size, 22);
                Minimap.ScaleBar(v.scale, 80, out double metres, out double px);
                Assert.That(metres == 10 || metres == 20, "bar of the " + size + " px map: " + metres + " m");
                Assert.LessOrEqual(px, 80 + 1e-6);
                Minimap.ToScreen(s.p[0], s.p[1], v, false, 0, out double x, out double y);
                Minimap.ToScreen(0, 0, v, false, 0, out double ox, out double oy);
                Assert.AreEqual(50, Math.Sqrt((x - ox) * (x - ox) + (y - oy) * (y - oy)) * v.scale, 0.05, "the arrow is 50 m from H on screen");
            }
        }

        [Test]
        public void Spec_HeadingUp_TurnsTheMap_NotTheArrow()
        {
            // acceptance (c): heading up, a 90 degree turn rotates the map (the origin and north move on screen), the arrow stays pointing up
            var view = new MapView(0.5, 0, 0);
            Minimap.ViewAngles(true, 0, out double a0, out double n0);
            Minimap.ViewAngles(true, Math.PI / 2, out double a1, out double n1);
            Assert.AreEqual(0, a0); Assert.AreEqual(0, a1);
            Assert.AreEqual(0, n0, 1e-12); Assert.AreEqual(-Math.PI / 2, n1, 1e-12);
            Minimap.ToScreen(10, 0, view, true, 0, out double x0, out double y0);
            Minimap.ToScreen(10, 0, view, true, Math.PI / 2, out double x1, out double y1);
            Assert.AreEqual(0, x0, 1e-9); Assert.AreEqual(-20, y0, 1e-9);   // north ahead: up
            Assert.AreEqual(-20, x1, 1e-9); Assert.AreEqual(0, y1, 1e-9);   // nose east: north is to the left
            // north up: the arrow turns, the map does not
            Minimap.ViewAngles(false, Math.PI / 2, out double a2, out double n2);
            Assert.AreEqual(Math.PI / 2, a2, 1e-12); Assert.AreEqual(0, n2);
        }

        [Test]
        public void Spec_ReplayLoopWrap_EmptiesTheTrail()
        {
            // acceptance (d): in the replay the trail empties when the loop wraps (a new segment)
            var t = new MapTrail();
            for (int i = 0; i < 50; i++) t.Push(new double[] { i, 0, -5 }, 4);
            Assert.AreEqual(50, t.Count);
            Assert.IsTrue(t.Push(new double[] { 3, 3, -5 }, 5));
            Assert.AreEqual(1, t.Count);
            Assert.AreEqual(5, t.Seg);
            Assert.IsFalse(t.Push(new double[] { 3.1, 3, -5 }, 5), "less than half a metre: nothing added, the caller skips the redraw");
        }
    }
}
