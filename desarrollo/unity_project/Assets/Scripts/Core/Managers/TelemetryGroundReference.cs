#if UNITY_STANDALONE
using UnityEngine;

namespace WebGL.Core.Managers
{
    /// <summary>
    /// WP-10 (PC only): a faint ground grid, shown ONLY while a telemetry session runs. The studio has no world floor (its grid is
    /// painted on the skybox in screen space) and the camera follows the drone, so without a fixed reference neither the position
    /// nor the altitude of the flight would be visible. Cells are 1 m (model units per metre), every 5th line is stronger, and the
    /// ground sits at the height of the landing gear at rest (flight altitude 0 = on the ground). A plain line mesh with the
    /// built-in Sprites/Default material (always in the player), on the Ignore Raycast layer: it is not selectable and it is not
    /// part of the drone hierarchy, so isolation / explode / filters never touch it.
    /// </summary>
    public sealed class TelemetryGroundReference : MonoBehaviour
    {
        const int HalfCells = 14;
        const int MajorEvery = 5;

        float cell = 16f;
        float groundY;
        MeshRenderer meshRenderer;
        Material material;
        Mesh mesh;

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
            var verts = new System.Collections.Generic.List<Vector3>();
            var cols = new System.Collections.Generic.List<Color>();
            var idx = new System.Collections.Generic.List<int>();
            float fadeR = HalfCells * cell;
            void Line(float ax, float az, float bx, float bz, bool major)
            {
                Color baseCol = major ? new Color(0.62f, 0.72f, 0.86f, 0.55f) : new Color(0.62f, 0.72f, 0.86f, 0.22f);
                const int seg = HalfCells * 2;
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
            for (int i = -HalfCells; i <= HalfCells; i++)
            {
                bool major = i % MajorEvery == 0;
                Line(i * cell, -fadeR, i * cell, fadeR, major);
                Line(-fadeR, i * cell, fadeR, i * cell, major);
            }
            mesh = new Mesh { name = "TelemetryGroundGrid" };
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetIndices(idx, MeshTopology.Lines, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(fadeR * 2f, 1f, fadeR * 2f));
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var shader = Shader.Find("Sprites/Default");
            material = new Material(shader != null ? shader : Shader.Find("Hidden/Internal-Colored")) { name = "TelemetryGroundGrid", renderQueue = 2990 };
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        /// <summary>Centres the grid under `bodyPosition` snapped to whole cells (lines stay still while the drone moves).</summary>
        public void Follow(Vector3 bodyPosition)
        {
            float x = Mathf.Round(bodyPosition.x / cell) * cell;
            float z = Mathf.Round(bodyPosition.z / cell) * cell;
            transform.position = new Vector3(x, groundY, z);
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }
    }
}
#endif
