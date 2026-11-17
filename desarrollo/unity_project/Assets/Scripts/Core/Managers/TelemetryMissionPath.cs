#if UNITY_STANDALONE
using System.Collections.Generic;
using UnityEngine;
using WebGL.Telemetry;

namespace WebGL.Core.Managers
{
    /// <summary>
    /// Lote 1c, tool 2 (PC only): the route of the app's mission in the 3D scene (the web's MissionPath): a thin line through the waypoints at their altitudes, a
    /// fainter stem from each one down to the ground and a small sphere per waypoint (the one being flown to is larger). Blue, the interaction accent. It has no
    /// colliders and sits on the Ignore Raycast layer, so a click on the ground passes through it; it shares the translucent material of the test course.
    /// </summary>
    public sealed class TelemetryMissionPath : MonoBehaviour
    {
        const float LineM = 0.035f, StemM = 0.02f, DotM = 0.14f, DotCurM = 0.22f;
        static readonly Color Accent = new Color32(70, 175, 255, 255);
        static readonly int TintId = Shader.PropertyToID("_Tint");

        Material material;
        MaterialPropertyBlock block;
        Mesh cube, sphere;
        Vector3 origin;
        float groundY, k = 16f;
        readonly List<Transform> lines = new List<Transform>(), stems = new List<Transform>(), dots = new List<Transform>();

        /// <summary>Waypoints drawn and the one drawn larger (-1 = none): read by the verification harness.</summary>
        public int Count { get; private set; }
        public int Current { get; private set; } = -1;
        public bool HasColliders { get { foreach (var c in GetComponentsInChildren<Collider>(true)) if (c != null) return true; return false; } }
        public Vector3 DotWorld(int i) => i >= 0 && i < Count ? dots[i].position : Vector3.zero;
        public float DotScaleMetres(int i) => i >= 0 && i < Count ? dots[i].localScale.x / (2f * k) : 0f;

        public static TelemetryMissionPath Create(Vector3 flightOrigin, float groundY, float unitsPerMeter)
        {
            Material z = Resources.Load<Material>(TelemetryTestCourse.ZoneMaterial);
            if (z == null)
            {
                Debug.LogWarning("[MissionPath] material " + TelemetryTestCourse.ZoneMaterial + " not found: no mission route in the scene");
                return null;
            }
            var go = new GameObject("_TelemetryMissionPath") { hideFlags = HideFlags.HideInHierarchy, layer = 2 };
            var c = go.AddComponent<TelemetryMissionPath>();
            c.origin = flightOrigin;
            c.groundY = groundY;
            c.k = Mathf.Max(0.01f, unitsPerMeter);
            c.material = new Material(z) { name = "MissionPath" };
            c.block = new MaterialPropertyBlock();
            c.cube = PrimitiveMesh(PrimitiveType.Cube);
            c.sphere = PrimitiveMesh(PrimitiveType.Sphere);
            return c;
        }

        static Mesh PrimitiveMesh(PrimitiveType type)
        {
            GameObject tmp = GameObject.CreatePrimitive(type);
            Mesh m = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
            return m;
        }

        Transform Part(List<Transform> pool, int i, Mesh mesh, float alpha)
        {
            while (pool.Count <= i)
            {
                var go = new GameObject("p") { layer = 2 };
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                pool.Add(go.transform);
            }
            Transform t = pool[i];
            t.gameObject.SetActive(true);
            var r = t.GetComponent<Renderer>();
            Color c = Accent;
            c.a = alpha;
            r.GetPropertyBlock(block);
            block.SetColor(TintId, c);
            r.SetPropertyBlock(block);
            return t;
        }

        static void Hide(List<Transform> pool, int from) { for (int i = from; i < pool.Count; i++) pool[i].gameObject.SetActive(false); }

        void Stretch(Transform t, Vector3 a, Vector3 b, float thickness)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            t.position = (a + b) * 0.5f;
            if (len > 1e-5f) t.rotation = Quaternion.LookRotation(d / len, Mathf.Abs(d.y / len) > 0.99f ? Vector3.forward : Vector3.up);
            t.localScale = new Vector3(thickness * k, thickness * k, Mathf.Max(len, 1e-4f));
        }

        /// <summary>World position of a waypoint: local NED north / east of the take-off point and height above it.</summary>
        public Vector3 WorldOf(double n, double e, double alt) => origin + new Vector3((float)e, (float)alt, (float)n) * k;

        /// <summary>Redraws the route. `current` = the waypoint being flown to while the mission runs or is paused (-1 = none).</summary>
        public void Set(IReadOnlyList<Waypoint> wps, int current)
        {
            int n = wps != null ? wps.Count : 0;
            Count = n;
            Current = current >= 0 && current < n ? current : -1;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = WorldOf(wps[i].n, wps[i].e, wps[i].alt);
                bool cur = i == Current;
                Transform dot = Part(dots, i, sphere, cur ? 0.55f : 0.5f);
                dot.position = p;
                dot.localScale = Vector3.one * (2f * (cur ? DotCurM : DotM) * k);
                Stretch(Part(stems, i, cube, 0.22f), new Vector3(p.x, groundY + 0.02f * k, p.z), p, StemM);
                if (i > 0) Stretch(Part(lines, i - 1, cube, 0.75f), WorldOf(wps[i - 1].n, wps[i - 1].e, wps[i - 1].alt), p, LineM);
            }
            Hide(dots, n);
            Hide(stems, n);
            Hide(lines, Mathf.Max(0, n - 1));
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
#endif
