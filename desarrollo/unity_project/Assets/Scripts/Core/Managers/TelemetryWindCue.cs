#if UNITY_STANDALONE
using System.Collections.Generic;
using UnityEngine;
using WebGL.Telemetry;

namespace WebGL.Core.Managers
{
    /// <summary>
    /// Lote 1a, tool 4 (PC only): the wind of the FLIGHT SIMULATOR in the 3D scene: a thin neutral arrow beside the drone that points where the wind blows to, with a
    /// length proportional to its speed (mean plus gust, so a gusty wind makes it breathe). It runs beside the vehicle, centred on it, a little to its left looking
    /// downwind so it never hides the airframe (the web's WindCue). Only the simulator has it: in PX4 SITL the wind is Gazebo's and does not reach the client.
    /// No colliders, Ignore Raycast layer, shares the translucent material of the test course (shader TwinSight/CourseUnlitPC).
    /// </summary>
    public sealed class TelemetryWindCue : MonoBehaviour
    {
        /// <summary>Metres of arrow per m/s of wind (6 m/s = 0.9 m beside a drone that is about 0.5 m across).</summary>
        public const float MetresPerMs = 0.15f;
        const float Head = 0.12f, HeadRadius = 0.022f, ShaftRadius = 0.005f, Side = 0.45f, MinLength = 0.35f;
        static readonly Color Colour = new Color32(219, 228, 238, 255);
        static readonly int TintId = Shader.PropertyToID("_Tint");

        Material material;
        Mesh shaftMesh, headMesh;
        Transform shaft, head;
        Renderer shaftRenderer, headRenderer;
        MaterialPropertyBlock block;
        float k = 16f;

        public bool ArrowVisible { get; private set; }
        /// <summary>Length of the arrow in metres (verification harness).</summary>
        public float LengthMetres { get; private set; }
        /// <summary>World direction the arrow points to (unit, horizontal).</summary>
        public Vector3 Direction { get; private set; }
        public Vector3 TailWorld => transform.position;
        public bool HasColliders { get { foreach (var c in GetComponentsInChildren<Collider>(true)) if (c != null) return true; return false; } }

        public static TelemetryWindCue Create(float unitsPerMeter)
        {
            Material z = Resources.Load<Material>(TelemetryTestCourse.ZoneMaterial);
            if (z == null)
            {
                Debug.LogWarning("[WindCue] material " + TelemetryTestCourse.ZoneMaterial + " not found: no wind arrow");
                return null;
            }
            var go = new GameObject("_TelemetryWindCue") { hideFlags = HideFlags.HideInHierarchy, layer = 2 };
            var c = go.AddComponent<TelemetryWindCue>();
            c.k = Mathf.Max(0.01f, unitsPerMeter);
            c.material = new Material(z) { name = "WindCue" };
            c.block = new MaterialPropertyBlock();
            c.shaftMesh = BoxMesh();
            c.headMesh = ConeMesh();
            c.shaft = c.NewPart("Shaft", c.shaftMesh, out c.shaftRenderer);
            c.head = c.NewPart("Head", c.headMesh, out c.headRenderer);
            c.SetVisible(false);
            return c;
        }

        Transform NewPart(string name, Mesh mesh, out Renderer r)
        {
            var go = new GameObject(name) { layer = 2 };
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            r = mr;
            Paint(mr, name == "Shaft" ? 0.55f : 0.7f);
            return go.transform;
        }

        void Paint(Renderer r, float alpha)
        {
            Color c = Colour;
            c.a = alpha;
            r.GetPropertyBlock(block);
            block.SetColor(TintId, c);
            r.SetPropertyBlock(block);
        }

        static Mesh BoxMesh()
        {
            // a unit box along +Z from 0 to 1, 1 x 1 in the other two axes, centred on them
            var v = new List<Vector3>(); var n = new List<Vector3>(); var c = new List<Color32>(); var t = new List<int>();
            Vector3[] p = { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(-.5f, -.5f, 1), new Vector3(.5f, -.5f, 1), new Vector3(.5f, .5f, 1), new Vector3(-.5f, .5f, 1) };
            int[][] faces = { new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 4, 7, 3 }, new[] { 1, 2, 6, 5 }, new[] { 3, 7, 6, 2 }, new[] { 0, 1, 5, 4 } };
            foreach (int[] f in faces)
            {
                int i = v.Count;
                Vector3 nn = Vector3.Cross(p[f[1]] - p[f[0]], p[f[2]] - p[f[0]]).normalized;
                for (int q = 0; q < 4; q++) { v.Add(p[f[q]]); n.Add(nn); c.Add(Color.white); }
                t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }
            var m = new Mesh { name = "WindShaft" };
            m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        static Mesh ConeMesh()
        {
            // a cone along +Z: base radius 1 at z = 0, tip at z = 1
            const int seg = 12;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var c = new List<Color32>(); var t = new List<int>();
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Vector3 b0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0), b1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0), tip = new Vector3(0, 0, 1);
                Vector3 nn = Vector3.Cross(b1 - b0, tip - b0).normalized;
                int k0 = v.Count;
                v.Add(b0); v.Add(b1); v.Add(tip); n.Add(nn); n.Add(nn); n.Add(nn); c.Add(Color.white); c.Add(Color.white); c.Add(Color.white);
                t.Add(k0); t.Add(k0 + 1); t.Add(k0 + 2);
                int k1 = v.Count; // the base
                v.Add(b0); v.Add(Vector3.zero); v.Add(b1); n.Add(-Vector3.forward); n.Add(-Vector3.forward); n.Add(-Vector3.forward); c.Add(Color.white); c.Add(Color.white); c.Add(Color.white);
                t.Add(k1); t.Add(k1 + 1); t.Add(k1 + 2);
            }
            var m = new Mesh { name = "WindHead" };
            m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        void SetVisible(bool on)
        {
            ArrowVisible = on;
            if (shaftRenderer != null) shaftRenderer.enabled = on;
            if (headRenderer != null) headRenderer.enabled = on;
        }

        /// <summary>
        /// Places the arrow beside the drone: `dronePos` is the world position of the body, `windN` / `windE` the wind in force (NED m/s, where it blows TO).
        /// Hidden below 0.05 m/s.
        /// </summary>
        public void Place(Vector3 dronePos, double windN, double windE)
        {
            double speed = System.Math.Sqrt(windN * windN + windE * windE);
            if (speed < 0.05) { if (ArrowVisible) SetVisible(false); LengthMetres = 0f; return; }
            if (!ArrowVisible) SetVisible(true);
            float len = Mathf.Max(MinLength, (float)speed * MetresPerMs);
            LengthMetres = len;
            // NED to Unity world: x = east, z = north
            Vector3 dir = new Vector3((float)(windE / speed), 0f, (float)(windN / speed));
            Direction = dir;
            Vector3 left = new Vector3(-dir.z, 0f, dir.x); // left of the wind looking downwind, seen from above (east right, north up)
            Vector3 centre = dronePos + Vector3.up * (0.1f * k) + left * (Side * k);
            transform.position = centre - dir * (len * 0.5f * k);
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            float shaftLen = Mathf.Max(0.02f, len - Head) * k;
            shaft.localPosition = Vector3.zero;
            shaft.localScale = new Vector3(ShaftRadius * 2f * k, ShaftRadius * 2f * k, shaftLen);
            head.localPosition = new Vector3(0f, 0f, shaftLen);
            head.localScale = new Vector3(HeadRadius * k, HeadRadius * k, Head * k);
        }

        void OnDestroy()
        {
            if (shaftMesh != null) Destroy(shaftMesh);
            if (headMesh != null) Destroy(headMesh);
            if (material != null) Destroy(material);
        }
    }
}
#endif
