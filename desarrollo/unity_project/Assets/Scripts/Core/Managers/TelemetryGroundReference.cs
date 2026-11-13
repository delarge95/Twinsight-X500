#if UNITY_STANDALONE
using UnityEngine;

namespace WebGL.Core.Managers
{
    /// <summary>
    /// PLAN_PC 28.2 (PC only): the world ground of a telemetry session. The studio has no world floor (its grid is painted on the skybox in screen
    /// space) and the camera follows the drone, so without a fixed reference neither the position nor the altitude of the flight would be visible.
    ///  - GROUND: a world-fixed, infinite plane drawn by one screen triangle (shader <c>TwinSight/GroundGridPC</c>, material
    ///    <c>Resources/PC/Environment/PC_GroundGrid</c>): the shader cuts the camera ray with the plane y = ground, so there is no geometry to clip, no
    ///    dependence on the camera's far plane and it looks the same from 0 to 60 m and beyond, from grazing to top-down, with the follow camera.
    ///    1 m / 10 m / 100 m lines in world space (derivative anti-aliasing), fade to the horizon colour, a ground tint that differs from the sky,
    ///    a clean horizon line with heading ticks, the soft shadow of the drone and the ring of its ground point.
    ///  - ALTITUDE CUE: a thin vertical line from the drone to its ground point (the label is a UI element: DesktopAltitudeLabel).
    ///  - FLY-TO TARGET: the ring and cross where a ground click sent the drone.
    /// Everything is on the Ignore Raycast layer with NO collider: it is not selectable (PLAN_PC 26: do not touch the part-selection raycast) and it is
    /// not part of the drone hierarchy. Colours are the tokens of design/tokens.json (accent, highlight, surface.panel). PC_Low drops the fine grid, the
    /// shadow and the heading ticks. <c>-ground-legacy</c> restores the old finite 29 m mesh grid (before/after captures).
    /// </summary>
    public sealed class TelemetryGroundReference : MonoBehaviour
    {
        // design tokens (design/tokens.json): color.accent, color.highlight, color.surface.panel
        static readonly Color Accent = new Color32(70, 175, 255, 255);
        static readonly Color Highlight = new Color32(191, 234, 255, 255);
        static readonly Color Panel = new Color32(23, 27, 34, 255);

        const int LegacyHalfCells = 14;
        const int LegacyMajorEvery = 5;
        public const string MaterialResource = "PC/Environment/PC_GroundGrid";

        static readonly int GroundColorId = Shader.PropertyToID("_GroundColor");
        static readonly int HorizonColorId = Shader.PropertyToID("_HorizonColor");
        static readonly int LineColorId = Shader.PropertyToID("_LineColor");
        static readonly int MajorColorId = Shader.PropertyToID("_MajorColor");
        static readonly int MeterId = Shader.PropertyToID("_Meter");
        static readonly int GroundYId = Shader.PropertyToID("_GroundY");
        static readonly int DroneShadowId = Shader.PropertyToID("_DroneShadow");
        static readonly int QualityId = Shader.PropertyToID("_Quality");
        static readonly int FadeStartId = Shader.PropertyToID("_FadeStart");
        static readonly int FadeEndId = Shader.PropertyToID("_FadeEnd");

        /// <summary>-ground-legacy: the old finite mesh grid instead of the world ground (before/after captures).</summary>
        public static bool Legacy => PCDebugFlags.Has("ground-legacy") || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-ground-legacy") >= 0;

        float cell = 16f;
        float groundY;
        bool visible = true;
        bool legacy;

        // the world ground (one screen triangle)
        MeshRenderer groundRenderer;
        MeshFilter groundFilter;
        Mesh groundMesh;
        Material groundMaterial;

        // the altitude cue
        GameObject cue;
        Mesh cueMesh;
        Material cueMaterial;
        readonly Vector3[] cueVerts = new Vector3[6];
        float altitudeMeters;
        Vector3 cueMid, cueTop, cueFoot;

        // the fly_to target
        GameObject marker;
        Mesh markerMesh;
        Material markerMaterial;

        // legacy grid
        MeshRenderer legacyRenderer;
        Material legacyMaterial;
        Mesh legacyMesh;

        /// <summary>World height of the ground plane (the underside of the landing gear at rest).</summary>
        public float GroundY => groundY;
        public bool HasTarget => marker != null && marker.activeSelf;
        public bool IsLegacy => legacy;
        public bool Visible
        {
            get => visible;
            set
            {
                if (visible == value) return;
                visible = value;
                if (groundRenderer != null) groundRenderer.enabled = value;
                if (legacyRenderer != null) legacyRenderer.enabled = value;
                if (cue != null) cue.SetActive(value && altitudeMeters > 0.3f);
            }
        }

        /// <summary>Drone altitude over the ground in metres (the cue line's length).</summary>
        public float AltitudeMeters => altitudeMeters;
        /// <summary>The ends of the cue line in world space (the UI label goes on the visible part of it): the drone and its ground point.</summary>
        public Vector3 CueTop => cueTop;
        public Vector3 CueFoot => cueFoot;
        public Vector3 CueMidpoint => cueMid;
        /// <summary>True when the cue line is drawn (the environment is shown and the drone is off the ground).</summary>
        public bool CueVisible => !legacy && visible && cue != null && cue.activeSelf;
        public Material GroundMaterial => groundMaterial;

        public static TelemetryGroundReference Create(float unitsPerMeter, float groundY)
        {
            var go = new GameObject("_TelemetryGroundReference") { hideFlags = HideFlags.HideInHierarchy, layer = 2 };
            var g = go.AddComponent<TelemetryGroundReference>();
            g.cell = Mathf.Max(0.01f, unitsPerMeter);
            g.groundY = groundY;
            g.Build();
            return g;
        }

        void Build()
        {
            Material src = Legacy ? null : Resources.Load<Material>(MaterialResource);
            if (src == null)
            {
                if (!Legacy) Debug.LogWarning("[TelemetryGround] " + MaterialResource + " not found: using the legacy finite grid");
                legacy = true;
                BuildLegacy();
                return;
            }

            groundMaterial = new Material(src) { name = "TelemetryGround" };
            groundMaterial.SetFloat(MeterId, cell);
            groundMaterial.SetFloat(GroundYId, groundY);
            groundMaterial.SetColor(LineColorId, Accent);
            groundMaterial.SetColor(MajorColorId, Highlight);
            Color ground = Color.Lerp(Panel, Accent, 0.17f);
            Color horizon = Color.Lerp(Panel, Accent, 0.34f);
            groundMaterial.SetColor(GroundColorId, ground);
            groundMaterial.SetColor(HorizonColorId, horizon);
            groundMaterial.SetFloat(FadeStartId, 60f);
            groundMaterial.SetFloat(FadeEndId, 1800f);
            groundMaterial.SetFloat("_ShadowStrength", 0.7f);
            groundMaterial.SetVector(DroneShadowId, new Vector4(0f, groundY, 0f, 1.5f * cell));
            ApplyQuality();

            groundMesh = new Mesh { name = "TelemetryGroundScreenQuad" };
            groundMesh.SetVertices(new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0) });
            groundMesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            groundMesh.bounds = new Bounds(Vector3.zero, new Vector3(1e7f, 1e7f, 1e7f)); // never culled: the shader positions it at the far plane
            groundFilter = gameObject.AddComponent<MeshFilter>();
            groundFilter.sharedMesh = groundMesh;
            groundRenderer = gameObject.AddComponent<MeshRenderer>();
            groundRenderer.sharedMaterial = groundMaterial;
            groundRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            groundRenderer.receiveShadows = false;
            groundRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            groundRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            groundRenderer.allowOcclusionWhenDynamic = false;

            BuildCue();
            RenderQualityController.OnTierChanged += OnTier;
        }

        void OnTier(QualityTier _) => ApplyQuality();

        /// <summary>PC_Low simplifies: no fine 1 m grid, no drone shadow and ring, no heading ticks (2 grid levels and the fade only).</summary>
        void ApplyQuality()
        {
            if (groundMaterial == null) return;
            bool low = RenderQualityController.ResolvedTier == QualityTier.Low;
            groundMaterial.SetFloat(QualityId, low ? 0f : 1f);
        }

        void BuildCue()
        {
            cueMesh = new Mesh { name = "TelemetryAltitudeCue" };
            cueMesh.MarkDynamic();
            for (int i = 0; i < cueVerts.Length; i++) cueVerts[i] = Vector3.zero;
            var cols = new Color[6];
            Color c = new Color(Accent.r, Accent.g, Accent.b, 0.9f);
            for (int i = 0; i < 6; i++) cols[i] = c;
            cols[1] = new Color(Accent.r, Accent.g, Accent.b, 0.35f); // the line fades towards the ground end
            cueMesh.SetVertices(cueVerts);
            cueMesh.SetColors(cols);
            cueMesh.SetIndices(new[] { 0, 1, 2, 3, 4, 5 }, MeshTopology.Lines, 0);
            cueMesh.bounds = new Bounds(Vector3.zero, new Vector3(1e6f, 1e6f, 1e6f));
            cue = new GameObject("_TelemetryAltitudeCue") { hideFlags = HideFlags.HideInHierarchy, layer = 2 };
            cue.AddComponent<MeshFilter>().sharedMesh = cueMesh;
            var shader = Shader.Find("Sprites/Default");
            cueMaterial = new Material(shader != null ? shader : Shader.Find("Hidden/Internal-Colored")) { name = "TelemetryAltitudeCue", renderQueue = 3000 };
            var mr = cue.AddComponent<MeshRenderer>();
            mr.sharedMaterial = cueMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            cue.SetActive(false);
        }

        void BuildLegacy()
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var cols = new System.Collections.Generic.List<Color>();
            var idx = new System.Collections.Generic.List<int>();
            float fadeR = LegacyHalfCells * cell;
            void Line(float ax, float az, float bx, float bz, bool major)
            {
                Color baseCol = major ? new Color(0.62f, 0.72f, 0.86f, 0.55f) : new Color(0.62f, 0.72f, 0.86f, 0.22f);
                const int seg = LegacyHalfCells * 2;
                for (int s = 0; s < seg; s++)
                {
                    float t0 = s / (float)seg, t1 = (s + 1) / (float)seg;
                    Vector3 p0 = new Vector3(Mathf.Lerp(ax, bx, t0), 0, Mathf.Lerp(az, bz, t0));
                    Vector3 p1 = new Vector3(Mathf.Lerp(ax, bx, t1), 0, Mathf.Lerp(az, bz, t1));
                    Color c0 = baseCol, c1 = baseCol;
                    c0.a *= Mathf.Clamp01(1f - new Vector2(p0.x, p0.z).magnitude / fadeR);
                    c1.a *= Mathf.Clamp01(1f - new Vector2(p1.x, p1.z).magnitude / fadeR);
                    idx.Add(verts.Count); verts.Add(p0); cols.Add(c0);
                    idx.Add(verts.Count); verts.Add(p1); cols.Add(c1);
                }
            }
            for (int i = -LegacyHalfCells; i <= LegacyHalfCells; i++)
            {
                bool major = i % LegacyMajorEvery == 0;
                Line(i * cell, -fadeR, i * cell, fadeR, major);
                Line(-fadeR, i * cell, fadeR, i * cell, major);
            }
            legacyMesh = new Mesh { name = "TelemetryGroundGrid" };
            legacyMesh.SetVertices(verts);
            legacyMesh.SetColors(cols);
            legacyMesh.SetIndices(idx, MeshTopology.Lines, 0);
            legacyMesh.bounds = new Bounds(Vector3.zero, new Vector3(fadeR * 2f, 1f, fadeR * 2f));
            gameObject.AddComponent<MeshFilter>().sharedMesh = legacyMesh;
            var shader = Shader.Find("Sprites/Default");
            legacyMaterial = new Material(shader != null ? shader : Shader.Find("Hidden/Internal-Colored")) { name = "TelemetryGroundGrid", renderQueue = 2990 };
            legacyRenderer = gameObject.AddComponent<MeshRenderer>();
            legacyRenderer.sharedMaterial = legacyMaterial;
            legacyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            legacyRenderer.receiveShadows = false;
        }

        /// <summary>E-6: the fly_to target marker (a ring and a cross on the ground, 2.4 m wide); null hides it.</summary>
        public void SetTarget(Vector3? world)
        {
            if (!world.HasValue) { if (marker != null) marker.SetActive(false); return; }
            if (marker == null) BuildMarker();
            marker.SetActive(true);
            marker.transform.position = new Vector3(world.Value.x, groundY + 0.02f * cell, world.Value.z);
        }

        void BuildMarker()
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var idx = new System.Collections.Generic.List<int>();
            var cols = new System.Collections.Generic.List<Color>();
            Color c = new Color(0.27f, 0.69f, 1f, 0.95f);
            void Seg(Vector3 a, Vector3 b) { idx.Add(verts.Count); verts.Add(a); cols.Add(c); idx.Add(verts.Count); verts.Add(b); cols.Add(c); }
            float r = 1.2f * cell; // 1.2 m: it has to read from the far camera of a flight
            const int n = 32;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                Seg(new Vector3(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r), new Vector3(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r));
                Seg(new Vector3(Mathf.Cos(a0) * r * 0.55f, 0, Mathf.Sin(a0) * r * 0.55f), new Vector3(Mathf.Cos(a1) * r * 0.55f, 0, Mathf.Sin(a1) * r * 0.55f));
            }
            Seg(new Vector3(-r * 1.4f, 0, 0), new Vector3(r * 1.4f, 0, 0));
            Seg(new Vector3(0, 0, -r * 1.4f), new Vector3(0, 0, r * 1.4f));
            markerMesh = new Mesh { name = "TelemetryFlyTarget" };
            markerMesh.SetVertices(verts);
            markerMesh.SetColors(cols);
            markerMesh.SetIndices(idx, MeshTopology.Lines, 0);
            markerMesh.bounds = new Bounds(Vector3.zero, new Vector3(r * 4f, 1f, r * 4f));
            marker = new GameObject("_TelemetryFlyTarget") { hideFlags = HideFlags.HideInHierarchy, layer = 2 };
            // not a child of anything that moves: the target stays where it was clicked
            marker.AddComponent<MeshFilter>().sharedMesh = markerMesh;
            var shader = Shader.Find("Sprites/Default");
            markerMaterial = new Material(shader != null ? shader : Shader.Find("Hidden/Internal-Colored")) { name = "TelemetryFlyTarget", renderQueue = 3000 };
            var mr = marker.AddComponent<MeshRenderer>();
            mr.sharedMaterial = markerMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            marker.SetActive(false);
        }

        /// <summary>
        /// Every frame: the drone's world position. Feeds the shader (shadow and ring under it) and the altitude cue. The ground itself never moves:
        /// it is world-fixed and infinite, so the lines stay still while the drone and the camera move.
        /// </summary>
        public void Follow(Vector3 bodyPosition, float flightAltitudeMeters)
        {
            altitudeMeters = Mathf.Max(0f, flightAltitudeMeters); // the simulator's own altitude reading (the same number as the Flight tab)
            if (legacy)
            {
                float x = Mathf.Round(bodyPosition.x / cell) * cell;
                float z = Mathf.Round(bodyPosition.z / cell) * cell;
                transform.position = new Vector3(x, groundY, z);
                return;
            }

            if (groundMaterial != null) groundMaterial.SetVector(DroneShadowId, new Vector4(bodyPosition.x, bodyPosition.y, bodyPosition.z, 1.5f * cell));

            bool on = visible && altitudeMeters > 0.3f;
            if (cue != null)
            {
                if (cue.activeSelf != on) cue.SetActive(on);
                if (on)
                {
                    float arm = 0.35f * cell;
                    Vector3 top = bodyPosition;
                    Vector3 foot = new Vector3(bodyPosition.x, groundY + 0.01f * cell, bodyPosition.z);
                    cueVerts[0] = top; cueVerts[1] = foot;
                    cueVerts[2] = foot + new Vector3(-arm, 0, 0); cueVerts[3] = foot + new Vector3(arm, 0, 0);
                    cueVerts[4] = foot + new Vector3(0, 0, -arm); cueVerts[5] = foot + new Vector3(0, 0, arm);
                    cueMesh.SetVertices(cueVerts);
                    cueMid = Vector3.Lerp(top, foot, 0.5f);
                    cueTop = top; cueFoot = foot;
                }
            }
        }

        void OnDestroy()
        {
            RenderQualityController.OnTierChanged -= OnTier;
            if (groundMesh != null) Destroy(groundMesh);
            if (groundMaterial != null) Destroy(groundMaterial);
            if (legacyMesh != null) Destroy(legacyMesh);
            if (legacyMaterial != null) Destroy(legacyMaterial);
            if (cue != null) Destroy(cue);
            if (cueMesh != null) Destroy(cueMesh);
            if (cueMaterial != null) Destroy(cueMaterial);
            if (marker != null) Destroy(marker);
            if (markerMesh != null) Destroy(markerMesh);
            if (markerMaterial != null) Destroy(markerMaterial);
        }
    }
}
#endif
