using System;
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    // PLAN_PC 28.3. Pure C# (no UnityEngine): the layout and the scoring of the "simulated test scenario" of the flight simulator.
    // All coordinates are the simulator's frame: metres NED from the take-off point (north, east) and altitude (up) over the ground.
    // HONESTY: the simulator has no physics. Nothing here changes how it flies; a "collision" is a simple volume test (a sphere of DroneRadius
    // around the drone position against cylinders, boxes and the tube of each ring), not a model of the real drone's shape or of a crash.

    /// <summary>A ring to fly through. The normal is the heading `AzimuthDeg` (0 = north, 90 = east); the flight must cross it along that heading.</summary>
    public sealed class CourseGate
    {
        public double N, E, Alt, AzimuthDeg, Radius;
    }

    /// <summary>A translucent box to cross (north/east half sizes and an altitude range).</summary>
    public sealed class CourseZone
    {
        public double N, E, HalfN, HalfE, AltMin, AltMax;
        public bool Contains(double n, double e, double alt) => Math.Abs(n - N) <= HalfN && Math.Abs(e - E) <= HalfE && alt >= AltMin && alt <= AltMax;
    }

    /// <summary>A cone / pylon to avoid (vertical cylinder from the ground).</summary>
    public sealed class CoursePylon
    {
        public double N, E, Radius, Height;
    }

    /// <summary>An axis-aligned solid (a slab of the wall).</summary>
    public sealed class CourseBox
    {
        public double N0, N1, E0, E1, AltMin, AltMax;
    }

    public enum CheckpointKind { Gate, Zone }

    public struct Checkpoint
    {
        public CheckpointKind Kind;
        public int Index;
        public Checkpoint(CheckpointKind kind, int index) { Kind = kind; Index = index; }
    }

    public sealed class CourseLayout
    {
        public double PadN, PadE, PadRadius = 1.5;
        public readonly List<CourseGate> Gates = new List<CourseGate>();
        public readonly List<CourseZone> Zones = new List<CourseZone>();
        public readonly List<CoursePylon> Pylons = new List<CoursePylon>();
        public readonly List<CourseBox> Wall = new List<CourseBox>();
        /// <summary>The ordered checkpoints of one lap. The last one is the finish (the zone over the pad «H»).</summary>
        public readonly List<Checkpoint> Checkpoints = new List<Checkpoint>();
        /// <summary>A line of waypoints of the intended lap (north, east, altitude); the verification flight follows it. Not shown in the scene.</summary>
        public readonly List<double[]> Route = new List<double[]>();

        /// <summary>Every element sits well inside the 120 m geofence (the farthest point is about 78 m from the take-off point).</summary>
        public static CourseLayout Default()
        {
            var c = new CourseLayout();
            // pad «H» at the take-off point
            c.PadN = 0; c.PadE = 0; c.PadRadius = 1.5;

            // 1. gate, 2. zone, then a slalom of three pylons, 3. gate
            c.Gates.Add(new CourseGate { N = 12, E = 0, Alt = 3.5, AzimuthDeg = 0, Radius = 2.6 });
            c.Zones.Add(new CourseZone { N = 24, E = 0, HalfN = 4, HalfE = 4, AltMin = 0, AltMax = 9 });
            c.Pylons.Add(new CoursePylon { N = 32, E = -2.5, Radius = 0.5, Height = 5 });
            c.Pylons.Add(new CoursePylon { N = 38, E = 2.5, Radius = 0.5, Height = 5 });
            c.Pylons.Add(new CoursePylon { N = 44, E = -2.5, Radius = 0.5, Height = 5 });
            c.Gates.Add(new CourseGate { N = 52, E = 0, Alt = 4.5, AzimuthDeg = 0, Radius = 2.6 });

            // a wall with a gap: the wall runs north-south at e = 20 (north 50 to 70); the gap is 5 m wide (north 57.5 to 62.5), 1 to 6 m high
            double e0 = 19.6, e1 = 20.4;
            c.Wall.Add(new CourseBox { N0 = 50, N1 = 57.5, E0 = e0, E1 = e1, AltMin = 0, AltMax = 9 });
            c.Wall.Add(new CourseBox { N0 = 62.5, N1 = 70, E0 = e0, E1 = e1, AltMin = 0, AltMax = 9 });
            c.Wall.Add(new CourseBox { N0 = 57.5, N1 = 62.5, E0 = e0, E1 = e1, AltMin = 6, AltMax = 9 });
            c.Wall.Add(new CourseBox { N0 = 57.5, N1 = 62.5, E0 = e0, E1 = e1, AltMin = 0, AltMax = 1 });

            // 4. gate after the wall (flown eastwards), 2nd zone, 5. gate (flown southwards)
            c.Gates.Add(new CourseGate { N = 60, E = 30, Alt = 3.5, AzimuthDeg = 90, Radius = 2.6 });
            c.Zones.Add(new CourseZone { N = 60, E = 44, HalfN = 4, HalfE = 4, AltMin = 0, AltMax = 9 });
            c.Gates.Add(new CourseGate { N = 40, E = 44, Alt = 5, AzimuthDeg = 180, Radius = 2.6 });
            // two pylons on the straight way back
            c.Pylons.Add(new CoursePylon { N = 21, E = 31, Radius = 0.5, Height = 5 });
            c.Pylons.Add(new CoursePylon { N = 12, E = 17.6, Radius = 0.5, Height = 5 });
            // finish: the zone over the pad
            c.Zones.Add(new CourseZone { N = 0, E = 0, HalfN = 3, HalfE = 3, AltMin = 0, AltMax = 4 });

            c.Checkpoints.Add(new Checkpoint(CheckpointKind.Gate, 0));
            c.Checkpoints.Add(new Checkpoint(CheckpointKind.Zone, 0));
            c.Checkpoints.Add(new Checkpoint(CheckpointKind.Gate, 1));
            c.Checkpoints.Add(new Checkpoint(CheckpointKind.Gate, 2));
            c.Checkpoints.Add(new Checkpoint(CheckpointKind.Zone, 1));
            c.Checkpoints.Add(new Checkpoint(CheckpointKind.Gate, 3));
            c.Checkpoints.Add(new Checkpoint(CheckpointKind.Zone, 2));

            double[][] route =
            {
                new[] { 0.0, 0, 3.5 }, new[] { 12.0, 0, 3.5 }, new[] { 24.0, 0, 3.5 }, new[] { 32.0, 2, 3.5 }, new[] { 38.0, -2, 3.5 }, new[] { 44.0, 2, 3.5 },
                new[] { 52.0, 0, 4.5 }, new[] { 60.0, 0, 3.5 }, new[] { 60.0, 12, 3.5 }, new[] { 60.0, 30, 3.5 }, new[] { 60.0, 44, 3.5 },
                new[] { 40.0, 44, 5.0 }, new[] { 30.0, 36, 3.5 }, new[] { 22.0, 27, 3.5 }, new[] { 12.0, 13, 3.5 }, new[] { 2.0, 1, 3.5 }
            };
            foreach (var w in route) c.Route.Add(w);
            return c;
        }

        /// <summary>Distance from the take-off point of the farthest element corner, in metres (the geofence is 120 m).</summary>
        public double FarthestFromOrigin()
        {
            double far = 0;
            void P(double n, double e) { far = Math.Max(far, Math.Sqrt(n * n + e * e)); }
            foreach (var g in Gates) P(g.N, g.E);
            foreach (var z in Zones) { P(z.N + z.HalfN, z.E + z.HalfE); P(z.N - z.HalfN, z.E - z.HalfE); P(z.N + z.HalfN, z.E - z.HalfE); P(z.N - z.HalfN, z.E + z.HalfE); }
            foreach (var p in Pylons) P(p.N, p.E);
            foreach (var b in Wall) { P(b.N0, b.E0); P(b.N1, b.E1); P(b.N0, b.E1); P(b.N1, b.E0); }
            return far;
        }
    }

    public enum CourseState { Waiting, Running, Finished }

    public enum CourseEventKind { Started, Checkpoint, Collision, Finished }

    public struct CourseEvent
    {
        public CourseEventKind Kind;
        /// <summary>Checkpoint reached: its 1-based number. Collision: the obstacle id ("pylon:2", "wall:0", "gate:1").</summary>
        public int Number;
        public string Obstacle;
        public double Time;
    }

    /// <summary>
    /// One lap: waits for the take-off (altitude over 0.5 m), then runs the timer, scores the ordered checkpoints and counts collisions until the
    /// last checkpoint. `Step` takes the drone position of every frame (the simulator's own state, no input from anywhere else).
    /// </summary>
    public sealed class CourseRun
    {
        public const double DroneRadius = 0.45;     // m, a sphere around the position: «volumen simple», not the real shape
        public const double RingTube = 0.12;         // m, the visible tube of a ring
        public const double StartAltitude = 0.5;     // m
        public const double StartRadius = 6.0;       // m from the pad

        public readonly CourseLayout Layout;
        public CourseState State { get; private set; } = CourseState.Waiting;
        public double Elapsed { get; private set; }
        public int Reached { get; private set; }
        public int Collisions { get; private set; }
        public int Total => Layout.Checkpoints.Count;
        public double FinishTime { get; private set; }
        public readonly List<CourseEvent> Events = new List<CourseEvent>();

        private bool _havePrev;
        private bool _sawGround;
        private double _pn, _pe, _pa;
        private readonly HashSet<string> _touching = new HashSet<string>();

        public CourseRun(CourseLayout layout) { Layout = layout; }

        public void Reset()
        {
            State = CourseState.Waiting;
            Elapsed = 0;
            Reached = 0;
            Collisions = 0;
            FinishTime = 0;
            _havePrev = false;
            _sawGround = false;
            _touching.Clear();
            Events.Clear();
        }

        /// <summary>The checkpoint to reach now (null once finished).</summary>
        public Checkpoint? Next => Reached < Total ? Layout.Checkpoints[Reached] : (Checkpoint?)null;

        /// <summary>Advances the run by `dt` seconds with the drone at (n, e, alt). Events are appended to <see cref="Events"/> (the consumer clears them).</summary>
        public void Step(double dt, double n, double e, double alt)
        {
            if (State == CourseState.Finished) { _havePrev = false; return; }
            if (State == CourseState.Waiting)
            {
                // the lap starts with a take-off from the pad (the drone was on the ground and climbs over 0.5 m within 6 m of «H»), or, after a reset in
                // the air, with the first checkpoint (so a reset never starts a lap by itself)
                if (alt <= 0.3) _sawGround = true;
                double dPad = Math.Sqrt((n - Layout.PadN) * (n - Layout.PadN) + (e - Layout.PadE) * (e - Layout.PadE));
                bool takeoff = _sawGround && alt > StartAltitude && dPad <= StartRadius;
                bool firstCheckpoint = false;
                if (!takeoff && Reached == 0 && Total > 0 && _havePrev)
                {
                    Checkpoint c0 = Layout.Checkpoints[0];
                    firstCheckpoint = c0.Kind == CheckpointKind.Zone ? Layout.Zones[c0.Index].Contains(n, e, alt) : CrossesGate(Layout.Gates[c0.Index], _pn, _pe, _pa, n, e, alt);
                }
                if (takeoff || firstCheckpoint)
                {
                    State = CourseState.Running;
                    Events.Add(new CourseEvent { Kind = CourseEventKind.Started, Time = 0 });
                }
                if (State == CourseState.Waiting) { _pn = n; _pe = e; _pa = alt; _havePrev = true; return; }
            }

            if (dt > 0 && dt < 1) Elapsed += dt;

            // collisions: edge-triggered per obstacle (one count per contact, not one per frame)
            for (int i = 0; i < Layout.Pylons.Count; i++) Touch("pylon:" + i, HitsPylon(Layout.Pylons[i], n, e, alt));
            for (int i = 0; i < Layout.Wall.Count; i++) Touch("wall:" + i, HitsBox(Layout.Wall[i], n, e, alt));
            for (int i = 0; i < Layout.Gates.Count; i++) Touch("gate:" + i, HitsRing(Layout.Gates[i], n, e, alt));

            // the ordered checkpoint
            if (Reached < Total)
            {
                Checkpoint cp = Layout.Checkpoints[Reached];
                bool ok = false;
                if (cp.Kind == CheckpointKind.Zone) ok = Layout.Zones[cp.Index].Contains(n, e, alt);
                else if (_havePrev) ok = CrossesGate(Layout.Gates[cp.Index], _pn, _pe, _pa, n, e, alt);
                if (ok)
                {
                    Reached++;
                    Events.Add(new CourseEvent { Kind = CourseEventKind.Checkpoint, Number = Reached, Time = Elapsed });
                    if (Reached >= Total)
                    {
                        State = CourseState.Finished;
                        FinishTime = Elapsed;
                        Events.Add(new CourseEvent { Kind = CourseEventKind.Finished, Number = Reached, Time = Elapsed });
                    }
                }
            }

            _pn = n; _pe = e; _pa = alt; _havePrev = true;
        }

        private void Touch(string id, bool hit)
        {
            if (hit)
            {
                if (_touching.Add(id))
                {
                    Collisions++;
                    Events.Add(new CourseEvent { Kind = CourseEventKind.Collision, Obstacle = id, Time = Elapsed });
                }
            }
            else if (_touching.Count > 0) _touching.Remove(id);
        }

        // ── the simple volume tests (static: the unit tests call them directly) ──

        public static bool HitsPylon(CoursePylon p, double n, double e, double alt)
        {
            double dn = n - p.N, de = e - p.E;
            double r = p.Radius + DroneRadius;
            return dn * dn + de * de <= r * r && alt <= p.Height + DroneRadius && alt >= -DroneRadius;
        }

        public static bool HitsBox(CourseBox b, double n, double e, double alt)
        {
            double dn = Math.Max(Math.Max(b.N0 - n, 0), n - b.N1);
            double de = Math.Max(Math.Max(b.E0 - e, 0), e - b.E1);
            double da = Math.Max(Math.Max(b.AltMin - alt, 0), alt - b.AltMax);
            return dn * dn + de * de + da * da <= DroneRadius * DroneRadius;
        }

        /// <summary>The drone touches the tube of the ring (not the opening): the distance to the ring's circle is below tube + drone radius.</summary>
        public static bool HitsRing(CourseGate g, double n, double e, double alt)
        {
            Frame(g, n, e, alt, out double axial, out double radial);
            double d = Math.Sqrt((radial - g.Radius) * (radial - g.Radius) + axial * axial);
            return d <= RingTube + DroneRadius;
        }

        /// <summary>The segment prev -> now crosses the ring's plane along its heading and inside the opening (clear of the tube).</summary>
        public static bool CrossesGate(CourseGate g, double pn, double pe, double pa, double n, double e, double alt)
        {
            Frame(g, pn, pe, pa, out double a0, out _);
            Frame(g, n, e, alt, out double a1, out _);
            if (!(a0 < 0 && a1 >= 0)) return false;
            double s = a0 / (a0 - a1);
            double cn = pn + (n - pn) * s, ce = pe + (e - pe) * s, ca = pa + (alt - pa) * s;
            Frame(g, cn, ce, ca, out _, out double radial);
            return radial <= g.Radius - RingTube;
        }

        /// <summary>Position relative to the ring: axial = along its heading (positive = past the ring), radial = distance from its axis (in its plane).</summary>
        private static void Frame(CourseGate g, double n, double e, double alt, out double axial, out double radial)
        {
            double az = g.AzimuthDeg * Math.PI / 180.0;
            double nn = Math.Cos(az), ne = Math.Sin(az);          // heading (north, east)
            double dn = n - g.N, de = e - g.E, da = alt - g.Alt;
            axial = dn * nn + de * ne;
            double u = dn * -ne + de * nn;                           // horizontal offset in the ring's plane
            radial = Math.Sqrt(u * u + da * da);
        }
    }
}
