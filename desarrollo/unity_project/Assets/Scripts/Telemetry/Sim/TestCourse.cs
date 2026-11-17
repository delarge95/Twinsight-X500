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
        /// <summary>Path of the bundled course under StreamingAssets (a copy of the web's contracts/courses/basic.json).</summary>
        public const string StreamingRelativePath = "Telemetry/courses/basic.json";
        /// <summary>Text of <see cref="Source"/> when the layout is the C# fallback (the JSON was not found or not valid).</summary>
        public const string BuiltinSource = "builtin";

        /// <summary>Course id: the key of its records (records.json) and of its ghost.</summary>
        public string Id = "basic";
        public string Name = "Circuito básico";
        /// <summary>Where this layout came from: the JSON file path, "json" (parsed from text) or <see cref="BuiltinSource"/>.</summary>
        public string Source = BuiltinSource;
        /// <summary>The test constants written in the JSON (`constants`); the run itself uses the CourseRun constants, so a JSON whose values differ is not loaded by Default().</summary>
        public double DroneRadius = CourseRun.DroneRadius, RingTube = CourseRun.RingTube, StartAltitude = CourseRun.StartAltitude, StartRadius = CourseRun.StartRadius;

        public double PadN, PadE, PadRadius = 1.5;
        public readonly List<CourseGate> Gates = new List<CourseGate>();
        public readonly List<CourseZone> Zones = new List<CourseZone>();
        public readonly List<CoursePylon> Pylons = new List<CoursePylon>();
        public readonly List<CourseBox> Wall = new List<CourseBox>();
        /// <summary>The ordered checkpoints of one lap. The last one is the finish (the zone over the pad «H»).</summary>
        public readonly List<Checkpoint> Checkpoints = new List<Checkpoint>();
        /// <summary>A line of waypoints of the intended lap (north, east, altitude); the verification flight follows it. Not shown in the scene.</summary>
        public readonly List<double[]> Route = new List<double[]>();

        /// <summary>
        /// The course of the app: the bundled JSON (StreamingAssets/Telemetry/courses/basic.json, the same file as the web's contracts/courses/basic.json) when it
        /// is there and valid, else the C# copy (<see cref="Builtin"/>). A test requires both to be the same layout.
        /// </summary>
        public static CourseLayout Default()
        {
            CourseLayout json = LoadBundled(out _);
            return json ?? Builtin();
        }

#if UNITY_STANDALONE || UNITY_EDITOR
        /// <summary>Path of the bundled course in the player / Editor.</summary>
        public static string BundledPath => System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "Telemetry", "courses", "basic.json");

        /// <summary>The bundled course; null (with `error`) when it is missing, not a version-1 course, or its test constants are not the ones the run uses.</summary>
        public static CourseLayout LoadBundled(out string error)
        {
            error = null;
            try
            {
                string p = BundledPath;
                if (!System.IO.File.Exists(p)) { error = "missing " + p; return null; }
                CourseLayout c = Parse(System.IO.File.ReadAllText(p));
                if (c == null) { error = "not a version 1 course: " + p; return null; }
                if (!c.ConstantsMatchTheRun()) { error = "constants differ from CourseRun: " + p; return null; }
                c.Source = p;
                return c;
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
                return null;
            }
        }
#else
        /// <summary>No file access on this platform (WebGL): the C# copy is used.</summary>
        public static CourseLayout LoadBundled(out string error) { error = "no bundled course on this platform"; return null; }
#endif

        /// <summary>The `constants` of the JSON are the ones CourseRun uses (the run does not read them from the layout).</summary>
        public bool ConstantsMatchTheRun() => DroneRadius == CourseRun.DroneRadius && RingTube == CourseRun.RingTube && StartAltitude == CourseRun.StartAltitude && StartRadius == CourseRun.StartRadius;

        /// <summary>
        /// The layout from its JSON (contracts/courses/*.json), a port of the web's parseCourse (src/shared/course.ts): anything that is not a version-1 course
        /// gives null (nothing is guessed). The name defaults to the id.
        /// </summary>
        public static CourseLayout Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            object root;
            try { root = WebGL.Telemetry.Json.TelemetryJson.Parse(json); } catch { return null; }
            var d = root as Dictionary<string, object>;
            if (d == null) return null;
            if (!d.TryGetValue("version", out object ver) || !(ver is double) || (double)ver != 1) return null;
            if (!d.TryGetValue("id", out object idObj) || !(idObj is string id) || id.Length == 0) return null;
            var pad = Obj(d, "pad");
            var k = Obj(d, "constants");
            if (!AllNum(pad, "n", "e", "r") || !AllNum(k, "droneRadius", "ringTube", "startAltitude", "startRadius")) return null;
            var gates = List(d, "gates", "n", "e", "alt", "azimuthDeg", "r");
            var zones = List(d, "zones", "n", "e", "halfN", "halfE", "altMin", "altMax");
            var pylons = List(d, "pylons", "n", "e", "r", "h");
            var wall = List(d, "wall", "n0", "n1", "e0", "e1", "altMin", "altMax");
            if (gates == null || zones == null || pylons == null || wall == null) return null;
            if (!d.TryGetValue("checkpoints", out object cpo) || !(cpo is List<object> cps) || cps.Count == 0) return null;
            if (!d.TryGetValue("route", out object ro) || !(ro is List<object> route)) return null;

            string name = id;
            if (d.TryGetValue("name", out object nm) && nm is string nms) name = nms;
            var c = new CourseLayout
            {
                Id = id,
                Name = name,
                Source = "json",
                PadN = N(pad, "n"), PadE = N(pad, "e"), PadRadius = N(pad, "r"),
                DroneRadius = N(k, "droneRadius"), RingTube = N(k, "ringTube"), StartAltitude = N(k, "startAltitude"), StartRadius = N(k, "startRadius"),
            };
            foreach (var g in gates) c.Gates.Add(new CourseGate { N = N(g, "n"), E = N(g, "e"), Alt = N(g, "alt"), AzimuthDeg = N(g, "azimuthDeg"), Radius = N(g, "r") });
            foreach (var z in zones) c.Zones.Add(new CourseZone { N = N(z, "n"), E = N(z, "e"), HalfN = N(z, "halfN"), HalfE = N(z, "halfE"), AltMin = N(z, "altMin"), AltMax = N(z, "altMax") });
            foreach (var p in pylons) c.Pylons.Add(new CoursePylon { N = N(p, "n"), E = N(p, "e"), Radius = N(p, "r"), Height = N(p, "h") });
            foreach (var b in wall) c.Wall.Add(new CourseBox { N0 = N(b, "n0"), N1 = N(b, "n1"), E0 = N(b, "e0"), E1 = N(b, "e1"), AltMin = N(b, "altMin"), AltMax = N(b, "altMax") });
            foreach (object o in cps)
            {
                var cp = o as Dictionary<string, object>;
                if (cp == null || !cp.TryGetValue("kind", out object kindObj) || !(kindObj is string kind)) return null;
                int count = kind == "gate" ? c.Gates.Count : kind == "zone" ? c.Zones.Count : -1;
                if (count < 0 || !cp.TryGetValue("index", out object io) || !(io is double ix) || ix != Math.Floor(ix) || ix < 0 || ix >= count) return null;
                c.Checkpoints.Add(new Checkpoint(kind == "gate" ? CheckpointKind.Gate : CheckpointKind.Zone, (int)ix));
            }
            foreach (object o in route)
            {
                if (!(o is List<object> w) || w.Count != 3 || !(w[0] is double) || !(w[1] is double) || !(w[2] is double)) return null;
                c.Route.Add(new[] { (double)w[0], (double)w[1], (double)w[2] });
            }
            return c;
        }

        static Dictionary<string, object> Obj(Dictionary<string, object> d, string key) => d.TryGetValue(key, out object o) ? o as Dictionary<string, object> : null;
        static double N(Dictionary<string, object> o, string key) => (double)o[key];

        static bool AllNum(Dictionary<string, object> o, params string[] keys)
        {
            if (o == null) return false;
            foreach (string key in keys) if (!o.TryGetValue(key, out object v) || !(v is double x) || double.IsNaN(x) || double.IsInfinity(x)) return false;
            return true;
        }

        static List<Dictionary<string, object>> List(Dictionary<string, object> d, string key, params string[] keys)
        {
            if (!d.TryGetValue(key, out object o) || !(o is List<object> l)) return null;
            var r = new List<Dictionary<string, object>>(l.Count);
            foreach (object x in l)
            {
                var e = x as Dictionary<string, object>;
                if (!AllNum(e, keys)) return null;
                r.Add(e);
            }
            return r;
        }

        /// <summary>Every number of the layout in a fixed order and invariant text (id, name, pad, gates, zones, pylons, wall, checkpoints, route, constants): two layouts are the same course when their texts are equal.</summary>
        public string Canonical()
        {
            var sb = new System.Text.StringBuilder(2048);
            string F(double x) => x.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            sb.Append("id ").Append(Id).Append('\n').Append("name ").Append(Name).Append('\n');
            sb.Append("pad ").Append(F(PadN)).Append(' ').Append(F(PadE)).Append(' ').Append(F(PadRadius)).Append('\n');
            for (int i = 0; i < Gates.Count; i++) { var g = Gates[i]; sb.Append("gate ").Append(i).Append(' ').Append(F(g.N)).Append(' ').Append(F(g.E)).Append(' ').Append(F(g.Alt)).Append(' ').Append(F(g.AzimuthDeg)).Append(' ').Append(F(g.Radius)).Append('\n'); }
            for (int i = 0; i < Zones.Count; i++) { var z = Zones[i]; sb.Append("zone ").Append(i).Append(' ').Append(F(z.N)).Append(' ').Append(F(z.E)).Append(' ').Append(F(z.HalfN)).Append(' ').Append(F(z.HalfE)).Append(' ').Append(F(z.AltMin)).Append(' ').Append(F(z.AltMax)).Append('\n'); }
            for (int i = 0; i < Pylons.Count; i++) { var p = Pylons[i]; sb.Append("pylon ").Append(i).Append(' ').Append(F(p.N)).Append(' ').Append(F(p.E)).Append(' ').Append(F(p.Radius)).Append(' ').Append(F(p.Height)).Append('\n'); }
            for (int i = 0; i < Wall.Count; i++) { var b = Wall[i]; sb.Append("wall ").Append(i).Append(' ').Append(F(b.N0)).Append(' ').Append(F(b.N1)).Append(' ').Append(F(b.E0)).Append(' ').Append(F(b.E1)).Append(' ').Append(F(b.AltMin)).Append(' ').Append(F(b.AltMax)).Append('\n'); }
            for (int i = 0; i < Checkpoints.Count; i++) sb.Append("checkpoint ").Append(i).Append(' ').Append(Checkpoints[i].Kind == CheckpointKind.Gate ? "gate" : "zone").Append(' ').Append(Checkpoints[i].Index).Append('\n');
            for (int i = 0; i < Route.Count; i++) sb.Append("route ").Append(i).Append(' ').Append(F(Route[i][0])).Append(' ').Append(F(Route[i][1])).Append(' ').Append(F(Route[i][2])).Append('\n');
            sb.Append("constants ").Append(F(DroneRadius)).Append(' ').Append(F(RingTube)).Append(' ').Append(F(StartAltitude)).Append(' ').Append(F(StartRadius)).Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// The C# copy of the course (the fallback when the bundled JSON is missing). Every element sits well inside the 120 m geofence (the farthest point is
        /// about 78 m from the take-off point). It must stay equal to contracts/courses/basic.json (CourseLayoutJsonTests).
        /// </summary>
        public static CourseLayout Builtin()
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
