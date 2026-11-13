#if UNITY_STANDALONE
using System.Collections.Generic;
using UnityEngine;
using WebGL.Telemetry;

namespace WebGL.Core.Managers
{
    /// <summary>
    /// PLAN_PC 28.3 (PC only): the 3D scene of the «Escenario de prueba simulado» of the flight simulator: a pad «H», four rings, translucent zones, five
    /// pylons (a slalom and two more), and a wall with a gap. Low-poly procedural meshes, flat shaded (shader <c>TwinSight/CourseUnlitPC</c>), tinted with
    /// design tokens (accent = pending, highlight = next, ok = done, warn = pylons, crit = a hit). The layout and the scoring are pure C# (CourseLayout /
    /// CourseRun, in the Telemetry assembly); this class only draws them in the flight's frame and colours them.
    /// It has NO colliders and sits on the Ignore Raycast layer: it never takes part in the part-selection raycast (PLAN_PC 26) and it does not touch the
    /// simulator in any way. The drone is never stopped by it: a «collision» is only counted.
    /// </summary>
    public sealed class TelemetryTestCourse : MonoBehaviour
    {
        public const string SolidMaterial = "PC/Environment/PC_CourseSolid";
        public const string ZoneMaterial = "PC/Environment/PC_CourseZone";

        // design tokens (design/tokens.json): accent, highlight, surface.panel, state.ok / warn / crit
        static readonly Color Accent = new Color32(70, 175, 255, 255);
        static readonly Color Highlight = new Color32(191, 234, 255, 255);
        static readonly Color Panel = new Color32(23, 27, 34, 255);
        static readonly Color Ok = new Color32(63, 191, 138, 255);
        static readonly Color Warn = new Color32(245, 185, 66, 255);
        static readonly Color Crit = new Color32(255, 77, 77, 255);
        static readonly Color Slate = Color.Lerp(Panel, Highlight, 0.30f);
        static readonly int TintId = Shader.PropertyToID("_Tint");

        sealed class Item
        {
            public string Key;
            public Renderer[] Renderers;
            public float[] Alphas;        // alpha of each renderer's tint at rest (fills are faint, outlines strong)
            public Color Base;
            public int Checkpoint;        // 1-based number of the checkpoint this item is (0 = none)
            public float FlashUntil;
            public bool Zone;
            public bool Fill;
        }

        CourseLayout layout;
        float k = 16f;
        float groundY;
        Vector3 origin;
        Material solid, zone;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Item> items = new List<Item>();
        readonly Dictionary<string, Item> byKey = new Dictionary<string, Item>();
        MaterialPropertyBlock block;
        int reached;
        bool finished;
        bool dirty = true;
        float lastPulse = -1f;

        public int ItemCount => items.Count;
        public int GateCount => layout != null ? layout.Gates.Count : 0;
        public CourseLayout Layout => layout;
        public bool HasColliders { get { foreach (var c in GetComponentsInChildren<Collider>(true)) if (c != null) return true; return false; } }

        /// <summary>World position of a flight-frame point (metres north, east, altitude).</summary>
        public Vector3 World(double n, double e, double alt) => origin + new Vector3((float)e * k, (float)alt * k, (float)n * k);
        Vector3 OnGround(double n, double e, float lift = 0f) => new Vector3(origin.x + (float)e * k, groundY + (0.02f + lift) * k, origin.z + (float)n * k);

        public static TelemetryTestCourse Create(CourseLayout layout, Vector3 flightOrigin, float groundY, float unitsPerMeter)
        {
            Material s = Resources.Load<Material>(SolidMaterial), z = Resources.Load<Material>(ZoneMaterial);
            if (s == null || z == null)
            {
                Debug.LogWarning("[TestCourse] materials " + SolidMaterial + " / " + ZoneMaterial + " not found: no course");
                return null;
            }

            var go = new GameObject("_TelemetryTestCourse") { hideFlags = HideFlags.HideInHierarchy, layer = 2 };
            var c = go.AddComponent<TelemetryTestCourse>();
            c.layout = layout;
            c.k = Mathf.Max(0.01f, unitsPerMeter);
            c.groundY = groundY;
            c.origin = flightOrigin;
            c.solid = new Material(s) { name = "TestCourseSolid" };
            c.zone = new Material(z) { name = "TestCourseZone" };
            c.block = new MaterialPropertyBlock();
            c.Build();
            return c;
        }

        // ───────────────────────── geometry ─────────────────────────

        sealed class MB
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Color32> C = new List<Color32>();
            public readonly List<int> T = new List<int>();

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Color32 col)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c);
                N.Add(n); N.Add(n); N.Add(n);
                C.Add(col); C.Add(col); C.Add(col);
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 col) { Tri(a, b, c, col); Tri(a, c, d, col); }

            public void Box(Vector3 min, Vector3 max, Color32 col)
            {
                Vector3 p000 = new Vector3(min.x, min.y, min.z), p100 = new Vector3(max.x, min.y, min.z), p110 = new Vector3(max.x, max.y, min.z), p010 = new Vector3(min.x, max.y, min.z);
                Vector3 p001 = new Vector3(min.x, min.y, max.z), p101 = new Vector3(max.x, min.y, max.z), p111 = new Vector3(max.x, max.y, max.z), p011 = new Vector3(min.x, max.y, max.z);
                Quad(p000, p010, p110, p100, col); Quad(p101, p111, p011, p001, col);
                Quad(p001, p011, p010, p000, col); Quad(p100, p110, p111, p101, col);
                Quad(p010, p011, p111, p110, col); Quad(p000, p100, p101, p001, col);
            }

            /// <summary>A cone frustum standing on y = 0 (radius r0 at the base, r1 on top).</summary>
            public void Frustum(Vector3 baseCentre, float r0, float r1, float h, int seg, Color32 col)
            {
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                    Vector3 b0 = baseCentre + new Vector3(Mathf.Cos(a0) * r0, 0, Mathf.Sin(a0) * r0), b1 = baseCentre + new Vector3(Mathf.Cos(a1) * r0, 0, Mathf.Sin(a1) * r0);
                    Vector3 t0 = baseCentre + new Vector3(Mathf.Cos(a0) * r1, h, Mathf.Sin(a0) * r1), t1 = baseCentre + new Vector3(Mathf.Cos(a1) * r1, h, Mathf.Sin(a1) * r1);
                    Quad(b0, t0, t1, b1, col);
                    Tri(baseCentre + new Vector3(0, h, 0), t1, t0, col);
                }
            }

            /// <summary>A flat disc in the XZ plane at height y.</summary>
            public void Disc(Vector3 centre, float r, int seg, Color32 col)
            {
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                    Tri(centre, centre + new Vector3(Mathf.Sin(a0) * r, 0, Mathf.Cos(a0) * r), centre + new Vector3(Mathf.Sin(a1) * r, 0, Mathf.Cos(a1) * r), col);
                }
            }

            /// <summary>A flat ring in the XZ plane.</summary>
            public void Annulus(Vector3 centre, float r0, float r1, int seg, Color32 col)
            {
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                    Vector3 i0 = centre + new Vector3(Mathf.Sin(a0) * r0, 0, Mathf.Cos(a0) * r0), i1 = centre + new Vector3(Mathf.Sin(a1) * r0, 0, Mathf.Cos(a1) * r0);
                    Vector3 o0 = centre + new Vector3(Mathf.Sin(a0) * r1, 0, Mathf.Cos(a0) * r1), o1 = centre + new Vector3(Mathf.Sin(a1) * r1, 0, Mathf.Cos(a1) * r1);
                    Quad(i0, o0, o1, i1, col);
                }
            }

            /// <summary>A torus in the XY plane (axis +Z): ring radius R, tube radius r.</summary>
            public void Torus(float R, float r, int segR, int segr, Color32 col)
            {
                for (int i = 0; i < segR; i++)
                {
                    float a0 = i * Mathf.PI * 2f / segR, a1 = (i + 1) * Mathf.PI * 2f / segR;
                    for (int j = 0; j < segr; j++)
                    {
                        float b0 = j * Mathf.PI * 2f / segr, b1 = (j + 1) * Mathf.PI * 2f / segr;
                        Vector3 P(float a, float b) { float rr = R + r * Mathf.Cos(b); return new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, r * Mathf.Sin(b)); }
                        Quad(P(a0, b0), P(a1, b0), P(a1, b1), P(a0, b1), col);
                    }
                }
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.indexFormat = V.Count > 60000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                m.SetVertices(V);
                m.SetNormals(N);
                m.SetColors(C);
                m.SetTriangles(T, 0);
                m.RecalculateBounds();
                return m;
            }
        }

        Renderer AddMesh(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name) { layer = 2 };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            meshes.Add(mesh);
            return mr;
        }

        Item NewItem(string key, int checkpoint, Color baseColor, bool isZone = false)
        {
            var it = new Item { Key = key, Checkpoint = checkpoint, Base = baseColor, Zone = isZone };
            items.Add(it);
            byKey[key] = it;
            return it;
        }

        static Color32 White => new Color32(255, 255, 255, 255);

        void Build()
        {
            var root = transform;

            // pad «H»: a disc, its rim and the letter (vertex colours; tint stays white)
            {
                var mb = new MB();
                float r = (float)layout.PadRadius * k;
                Vector3 c = OnGround(layout.PadN, layout.PadE);
                Color32 disc = Color.Lerp(Panel, Accent, 0.30f), rim = Highlight, letter = Highlight;
                mb.Disc(c, r, 28, disc);
                mb.Annulus(c + new Vector3(0, 0.004f * k, 0), r * 0.92f, r, 28, rim);
                float bar = r * 0.16f, hh = r * 0.62f, hw = r * 0.34f;
                Vector3 up = new Vector3(0, 0.006f * k, 0);
                mb.Quad(c + up + new Vector3(-hw - bar, 0, -hh), c + up + new Vector3(-hw - bar, 0, hh), c + up + new Vector3(-hw, 0, hh), c + up + new Vector3(-hw, 0, -hh), letter);
                mb.Quad(c + up + new Vector3(hw, 0, -hh), c + up + new Vector3(hw, 0, hh), c + up + new Vector3(hw + bar, 0, hh), c + up + new Vector3(hw + bar, 0, -hh), letter);
                mb.Quad(c + up + new Vector3(-hw, 0, -bar * 0.5f), c + up + new Vector3(-hw, 0, bar * 0.5f), c + up + new Vector3(hw, 0, bar * 0.5f), c + up + new Vector3(hw, 0, -bar * 0.5f), letter);
                var it = NewItem("pad", 0, Color.white);
                it.Renderers = new[] { AddMesh(root, "Pad", mb.ToMesh("CoursePad"), solid, Vector3.zero, Quaternion.identity) };
                it.Alphas = new[] { 1f };
            }

            // rings: torus + a pole from the ground to the bottom of the ring
            for (int i = 0; i < layout.Gates.Count; i++)
            {
                CourseGate g = layout.Gates[i];
                float az = (float)g.AzimuthDeg * Mathf.Deg2Rad;
                Vector3 heading = new Vector3(Mathf.Sin(az), 0, Mathf.Cos(az));
                Vector3 centre = World(g.N, g.E, g.Alt);
                var tb = new MB();
                tb.Torus((float)g.Radius * k, (float)CourseRun.RingTube * k * 1.3f, 20, 6, White);
                var it = NewItem("gate:" + i, CheckpointNumber(CheckpointKind.Gate, i), Accent);
                var ring = AddMesh(root, "Gate" + i, tb.ToMesh("CourseRing" + i), solid, centre, Quaternion.LookRotation(heading, Vector3.up));
                var pb = new MB();
                float pw = 0.06f * k;
                float top = centre.y - (float)g.Radius * k;
                Vector3 foot = OnGround(g.N, g.E);
                pb.Box(new Vector3(foot.x - pw, foot.y, foot.z - pw), new Vector3(foot.x + pw, top, foot.z + pw), White);
                var pole = AddMesh(root, "GatePole" + i, pb.ToMesh("CoursePole" + i), solid, Vector3.zero, Quaternion.identity);
                it.Renderers = new[] { ring, pole };
                it.Alphas = new[] { 0.95f, 0.55f };
            }

            // zones: a faint fill and a bright outline
            for (int i = 0; i < layout.Zones.Count; i++)
            {
                CourseZone z = layout.Zones[i];
                Vector3 centre = World(z.N, (z.E), (z.AltMin + z.AltMax) * 0.5);
                Vector3 half = new Vector3((float)z.HalfE * k, (float)(z.AltMax - z.AltMin) * 0.5f * k, (float)z.HalfN * k);
                var fb = new MB();
                fb.Box(-half, half, White);
                var lb = new MB();
                AddEdges(lb, half);
                var lm = new Mesh { name = "CourseZoneEdges" + i };
                lm.SetVertices(lb.V);
                lm.SetNormals(lb.N);
                lm.SetColors(lb.C);
                lm.SetIndices(lb.T, MeshTopology.Lines, 0);
                lm.RecalculateBounds();
                var it = NewItem("zone:" + i, CheckpointNumber(CheckpointKind.Zone, i), Accent, true);
                var fill = AddMesh(root, "Zone" + i, fb.ToMesh("CourseZone" + i), zone, centre, Quaternion.identity);
                var edges = AddMesh(root, "ZoneEdges" + i, lm, solid, centre, Quaternion.identity);
                it.Renderers = new[] { fill, edges };
                it.Alphas = new[] { 0.10f, 0.7f };
            }

            // pylons
            for (int i = 0; i < layout.Pylons.Count; i++)
            {
                CoursePylon p = layout.Pylons[i];
                var mb = new MB();
                mb.Frustum(OnGround(p.N, p.E), (float)p.Radius * k, (float)p.Radius * k * 0.35f, (float)p.Height * k, 10, White);
                var it = NewItem("pylon:" + i, 0, Warn);
                it.Renderers = new[] { AddMesh(root, "Pylon" + i, mb.ToMesh("CoursePylon" + i), solid, Vector3.zero, Quaternion.identity) };
                it.Alphas = new[] { 1f };
            }

            // wall slabs
            for (int i = 0; i < layout.Wall.Count; i++)
            {
                CourseBox b = layout.Wall[i];
                var mb = new MB();
                Vector3 mn = new Vector3(origin.x + (float)b.E0 * k, origin.y + (float)b.AltMin * k, origin.z + (float)b.N0 * k);
                Vector3 mx = new Vector3(origin.x + (float)b.E1 * k, origin.y + (float)b.AltMax * k, origin.z + (float)b.N1 * k);
                if (b.AltMin <= 0.001) mn.y = groundY + 0.02f * k;
                mb.Box(mn, mx, White);
                var it = NewItem("wall:" + i, 0, Slate);
                it.Renderers = new[] { AddMesh(root, "Wall" + i, mb.ToMesh("CourseWall" + i), solid, Vector3.zero, Quaternion.identity) };
                it.Alphas = new[] { 1f };
            }

            dirty = true;
        }

        static void AddEdges(MB b, Vector3 h)
        {
            Vector3[] c = new Vector3[8];
            for (int i = 0; i < 8; i++) c[i] = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
            int[,] e = { { 0, 1 }, { 2, 3 }, { 4, 5 }, { 6, 7 }, { 0, 2 }, { 1, 3 }, { 4, 6 }, { 5, 7 }, { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 } };
            for (int i = 0; i < 12; i++)
            {
                int n = b.V.Count;
                b.V.Add(c[e[i, 0]]); b.V.Add(c[e[i, 1]]);
                b.N.Add(Vector3.up); b.N.Add(Vector3.up);
                b.C.Add(White); b.C.Add(White);
                b.T.Add(n); b.T.Add(n + 1);
            }
        }

        int CheckpointNumber(CheckpointKind kind, int index)
        {
            for (int i = 0; i < layout.Checkpoints.Count; i++) if (layout.Checkpoints[i].Kind == kind && layout.Checkpoints[i].Index == index) return i + 1;
            return 0;
        }

        // ───────────────────────── state ─────────────────────────

        /// <summary>The run's progress: how many checkpoints are done (the next one is highlighted) and whether the lap ended.</summary>
        public void SetProgress(int checkpointsReached, bool lapFinished)
        {
            if (checkpointsReached != reached || lapFinished != finished) { reached = checkpointsReached; finished = lapFinished; dirty = true; }
        }

        /// <summary>A brief flash of the touched obstacle ("pylon:1", "wall:0", "gate:2").</summary>
        public void Flash(string key)
        {
            if (key != null && byKey.TryGetValue(key, out Item it)) { it.FlashUntil = Time.unscaledTime + 0.5f; dirty = true; }
        }

        /// <summary>Colour state of an item (verification harness): "pending", "next", "done", "hit".</summary>
        public string StateOf(string key)
        {
            if (!byKey.TryGetValue(key, out Item it)) return null;
            if (Time.unscaledTime < it.FlashUntil) return "hit";
            if (it.Checkpoint <= 0) return "static";
            if (reached >= it.Checkpoint) return "done";
            return reached == it.Checkpoint - 1 && !finished ? "next" : "pending";
        }

        void Update()
        {
            float now = Time.unscaledTime;
            bool anyFlash = false;
            for (int i = 0; i < items.Count; i++) if (items[i].FlashUntil > 0f) { anyFlash = true; break; }
            // the next checkpoint breathes (a slow 1.4 Hz pulse); the step is coarse so the work stays tiny
            float pulse = Mathf.Round((0.5f + 0.5f * Mathf.Sin(now * 8.8f)) * 8f) / 8f;
            if (!dirty && !anyFlash && Mathf.Approximately(pulse, lastPulse)) return;
            lastPulse = pulse;
            dirty = false;
            for (int i = 0; i < items.Count; i++)
            {
                Item it = items[i];
                if (it.Renderers == null) continue;
                bool flashing = now < it.FlashUntil;
                if (it.FlashUntil > 0f && !flashing) { it.FlashUntil = 0f; }
                Color col = it.Base;
                float mul = 1f;
                if (it.Key == "pad") { col = Color.white; }
                else if (flashing) { col = Crit; mul = 1f; }
                else if (it.Checkpoint > 0)
                {
                    if (reached >= it.Checkpoint) { col = Ok; mul = it.Zone ? 1.2f : 1f; }
                    else if (reached == it.Checkpoint - 1 && !finished) { col = Color.Lerp(Accent, Highlight, 0.45f + 0.55f * pulse); mul = it.Zone ? 1.6f + 1.4f * pulse : 1f; }
                }
                for (int r = 0; r < it.Renderers.Length; r++)
                {
                    Renderer rend = it.Renderers[r];
                    if (rend == null) continue;
                    Color tint = col;
                    tint.a = Mathf.Clamp01(it.Alphas[r] * mul * (flashing && it.Zone ? 3f : 1f));
                    rend.GetPropertyBlock(block);
                    block.SetColor(TintId, tint);
                    rend.SetPropertyBlock(block);
                }
            }
        }

        void OnDestroy()
        {
            foreach (var m in meshes) if (m != null) Destroy(m);
            if (solid != null) Destroy(solid);
            if (zone != null) Destroy(zone);
        }
    }
}
#endif
