#if UNITY_STANDALONE
using System.Collections.Generic;
using UnityEngine;
using WebGL.Telemetry;

namespace WebGL.Core.Managers
{
    /// <summary>
    /// Lote 1a, tool 9 (PC only): the geofence of the FLIGHT SIMULATOR in the 3D scene: a ring on the ground at the radius, a translucent open cylinder up to the
    /// ceiling and a thin ring on its lid (the same three pieces and colours as the web's FenceRing). A neutral grey-blue and almost invisible while the vehicle is
    /// clear of it, amber at less than 5 m from the wall or the lid and red when it touches them (<see cref="Geofence.Status"/>). Only the simulator has it: the
    /// geofence of PX4 (GF_*) does not reach the client, so SITL and the replay never draw one. Everything about it is simulated.
    /// It has NO colliders and sits on the Ignore Raycast layer (it never takes part in the part-selection raycast); it shares the translucent material of the
    /// test course (shader TwinSight/CourseUnlitPC, vertex colour x _Tint).
    /// </summary>
    public sealed class TelemetrySimFence : MonoBehaviour
    {
        // design tokens (design/tokens.json): text.secondary, state.warn, state.crit
        static readonly Color Ok = new Color32(170, 180, 193, 255);
        static readonly Color Warn = new Color32(245, 185, 66, 255);
        static readonly Color Crit = new Color32(255, 77, 77, 255);
        static readonly int TintId = Shader.PropertyToID("_Tint");
        // opacity of [ground ring, wall, lid] per status: the wall only shows when the vehicle is close to it (web OPACITY)
        static readonly float[][] Opacity =
        {
            new[] { 0.28f, 0.012f, 0.16f },
            new[] { 0.70f, 0.070f, 0.55f },
            new[] { 0.90f, 0.120f, 0.70f },
        };
        const int Segments = 160;

        Material material;
        Renderer ring, wall, lid;
        Mesh ringMesh, wallMesh, lidMesh;
        MaterialPropertyBlock block;
        Vector3 origin;
        float k = 16f, groundY;
        double radius, ceiling;
        FenceStatus status = FenceStatus.Ok;
        bool statusSet;

        public double Radius => radius;
        public double Ceiling => ceiling;
        public FenceStatus Status => status;
        public bool HasColliders { get { foreach (var c in GetComponentsInChildren<Collider>(true)) if (c != null) return true; return false; } }
        /// <summary>The colour the ground ring has now (verification harness).</summary>
        public Color RingColor { get; private set; }
        public float RingOpacity { get; private set; }

        public static TelemetrySimFence Create(Vector3 flightOrigin, float groundY, float unitsPerMeter, double radius, double ceiling)
        {
            Material z = Resources.Load<Material>(TelemetryTestCourse.ZoneMaterial);
            if (z == null)
            {
                Debug.LogWarning("[SimFence] material " + TelemetryTestCourse.ZoneMaterial + " not found: no fence");
                return null;
            }
            var go = new GameObject("_TelemetrySimFence") { hideFlags = HideFlags.HideInHierarchy, layer = 2 };
            var f = go.AddComponent<TelemetrySimFence>();
            f.material = new Material(z) { name = "SimFence" };
            f.block = new MaterialPropertyBlock();
            f.origin = flightOrigin;
            f.groundY = groundY;
            f.k = Mathf.Max(0.01f, unitsPerMeter);
            f.ring = f.NewPart("Ground", out f.ringMesh);
            f.wall = f.NewPart("Wall", out f.wallMesh);
            f.lid = f.NewPart("Lid", out f.lidMesh);
            f.SetSize(radius, ceiling);
            return f;
        }

        Renderer NewPart(string name, out Mesh mesh)
        {
            var go = new GameObject(name) { layer = 2 };
            go.transform.SetParent(transform, false);
            mesh = new Mesh { name = "SimFence" + name };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return mr;
        }

        /// <summary>A new size rebuilds the meshes (the status is re-read on the next frame).</summary>
        public void SetSize(double newRadius, double newCeiling)
        {
            if (radius == newRadius && ceiling == newCeiling && ringMesh.vertexCount > 0) return;
            radius = newRadius;
            ceiling = newCeiling;
            float r = (float)radius * k, h = (float)ceiling * k;
            float width = Mathf.Max(0.12f, (float)radius * 0.003f) * k;
            Vector3 c = new Vector3(origin.x, groundY + 0.03f * k, origin.z);
            Build(ringMesh, c, r - width, r, 0f);
            Build(lidMesh, new Vector3(origin.x, groundY + h, origin.z), r - width * 0.6f, r, 0f);
            // open cylinder from the ground to the lid
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var col = new List<Color32>();
            var t = new List<int>();
            for (int i = 0; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                float x = origin.x + Mathf.Sin(a) * r, z = origin.z + Mathf.Cos(a) * r;
                v.Add(new Vector3(x, groundY, z)); v.Add(new Vector3(x, groundY + h, z));
                Vector3 nn = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                n.Add(nn); n.Add(nn);
                col.Add(Color.white); col.Add(Color.white);
                if (i < Segments) { int b = i * 2; t.Add(b); t.Add(b + 1); t.Add(b + 3); t.Add(b); t.Add(b + 3); t.Add(b + 2); }
            }
            wallMesh.Clear();
            wallMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
            wallMesh.SetVertices(v); wallMesh.SetNormals(n); wallMesh.SetColors(col); wallMesh.SetTriangles(t, 0);
            wallMesh.RecalculateBounds();
            statusSet = false;
        }

        static void Build(Mesh m, Vector3 centre, float r0, float r1, float y)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var col = new List<Color32>();
            var t = new List<int>();
            for (int i = 0; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                float s = Mathf.Sin(a), c = Mathf.Cos(a);
                v.Add(centre + new Vector3(s * r0, y, c * r0)); v.Add(centre + new Vector3(s * r1, y, c * r1));
                n.Add(Vector3.up); n.Add(Vector3.up);
                col.Add(Color.white); col.Add(Color.white);
                if (i < Segments) { int b = i * 2; t.Add(b); t.Add(b + 1); t.Add(b + 3); t.Add(b); t.Add(b + 3); t.Add(b + 2); }
            }
            m.Clear();
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(v); m.SetNormals(n); m.SetColors(col); m.SetTriangles(t, 0);
            m.RecalculateBounds();
        }

        /// <summary>The vehicle's NED position decides the colour (ok / near / hit); call it every frame.</summary>
        public void SetPosition(double[] p)
        {
            FenceStatus s = Geofence.Status(radius, ceiling, p);
            if (statusSet && s == status) return;
            status = s;
            statusSet = true;
            Color c = s == FenceStatus.Hit ? Crit : s == FenceStatus.Near ? Warn : Ok;
            float[] o = Opacity[(int)s];
            Paint(ring, c, o[0]);
            Paint(wall, c, o[1]);
            Paint(lid, c, o[2]);
            RingColor = c;
            RingOpacity = o[0];
        }

        void Paint(Renderer r, Color c, float alpha)
        {
            if (r == null) return;
            c.a = alpha;
            r.GetPropertyBlock(block);
            block.SetColor(TintId, c);
            r.SetPropertyBlock(block);
        }

        void OnDestroy()
        {
            if (ringMesh != null) Destroy(ringMesh);
            if (wallMesh != null) Destroy(wallMesh);
            if (lidMesh != null) Destroy(lidMesh);
            if (material != null) Destroy(material);
        }
    }
}
#endif
