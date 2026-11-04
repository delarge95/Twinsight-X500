#if UNITY_STANDALONE
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WebGL.Core.Content;

namespace WebGL.Core.Managers
{
    /// <summary>
    /// Fase 3A (G7), SOLO PC: sustituye en runtime los materiales ClippableLit del dron por las
    /// variantes de PC (shader TwinSight/DroneLitPC, en Resources/PC/Materials). Los .mat que usa
    /// WebGL no se tocan. Mapea por material original y por nombre de renderer (categoria):
    ///   GRAY63 (160 fasteners) -> tornillos / tuercas / nylon; MAT_Plastic_Black -> helices / tubos
    ///   de carbono / plastico; carbon_fiber_01 y BAKE_PREVIEW.001 -> placas de carbono (clear-coat +
    ///   normal de trenzado); MAT_Plastic_Blue -> aluminio anodizado azul; etc.
    /// Despues pide a ViewModeManager reconstruir su cache para que "Realistic" restaure los
    /// materiales PC, y el modo CUT sigue recortando (DroneLitPC lee los mismos planos globales).
    /// </summary>
    public class PCMaterialSwapper : MonoBehaviour
    {
        private static PCMaterialSwapper _instance;
        private readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();
        private readonly HashSet<Renderer> _done = new HashSet<Renderer>();
        public static int SwappedCount { get; private set; }

        public static void Ensure()
        {
            if (_instance != null || !Application.isPlaying) return;
            var go = new GameObject("PC Material Swapper");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PCMaterialSwapper>();
        }

        private IEnumerator Start()
        {
            // Los fasteners y el resto del dron se construyen en los primeros segundos.
            float t = 0f;
            int stable = 0, last = -1;
            while (t < 40f)
            {
                yield return new WaitForSeconds(0.5f);
                t += 0.5f;
                var rs = DroneRenderResolver.CollectManagedRenderers();
                int n = rs.Count;
                if (n > 200 && n == last) stable++; else stable = 0;
                last = n;
                if (stable >= 3) break;
            }

            SwapAll();
            if (ViewModeManager.Instance != null && ViewModeManager.Instance.CurrentMode == ViewMode.Realistic)
            {
                ViewModeManager.Instance.RebuildCache();
            }
        }

        public void SwapAll()
        {
            var renderers = DroneRenderResolver.CollectManagedRenderers();
            int count = 0;
            foreach (Renderer r in renderers)
            {
                if (r == null || _done.Contains(r)) continue;
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material pc = Resolve(mats[i], r);
                    if (pc != null && pc != mats[i]) { mats[i] = pc; changed = true; }
                }
                if (changed)
                {
                    r.sharedMaterials = mats;
                    count++;
                    foreach (Material m in mats)
                    {
                        if (m != null && (m.name == "PC_ScrewZinc" || m.name == "PC_Nylon"))
                        {
                            PCScrewSmoother.Smooth(r);
                            break;
                        }
                    }
                }
                _done.Add(r);
            }
            SwappedCount += count;
            Debug.Log("[PCMaterialSwapper] Renderers con material PC: " + count + " de " + renderers.Count + " | tornillos redondeados: " + PCScrewSmoother.SmoothedRenderers + " (mallas: " + PCScrewSmoother.BuiltMeshes + ")");
        }

        private Material Load(string name)
        {
            if (!_cache.TryGetValue(name, out Material m))
            {
                m = Resources.Load<Material>("PC/Materials/" + name);
                _cache[name] = m;
            }
            return m;
        }

        private Material Resolve(Material original, Renderer r)
        {
            if (original == null) return null;
            string mn = original.name.Replace(" (Instance)", "");
            string rn = r.name.ToLowerInvariant();
            if (original.shader != null && original.shader.name == "TwinSight/DroneLitPC") return original;
            if (original.shader == null || original.shader.name != "WebGL/ClippableLit") return null;

            string target;
            switch (mn)
            {
                case "GRAY63":
                    if (rn.Contains("nylon")) target = "PC_Nylon";
                    else if (rn.Contains("nut")) target = "PC_NutZinc";
                    else target = "PC_ScrewZinc";
                    break;
                case "MAT_Steel": target = "PC_Steel"; break;
                case "carbon_fiber_01":
                case "BAKE_PREVIEW.001": target = "PC_CarbonPlate"; break;
                case "MAT_Plastic_Black":
                    if (rn.StartsWith("x500v2_prop")) target = "PC_Prop";
                    else if (rn.Contains("carbon-fiber-tube")) target = "PC_CarbonTube";
                    else target = "PC_PlasticBlack";
                    break;
                case "MAT_Plastic_Blue": target = "PC_AnodizedBlue"; break;
                case "MAT_Alum_Black": target = "PC_AnodizedBlack"; break;
                case "MAT_Electronics":
                case "BAKE_PREVIEW.003": target = "PC_PCB"; break;
                case "MAT_Rubber":
                case "MAT_Rubber_black": target = "PC_Rubber"; break;
                case "BAKE_PREVIEW.007": target = "PC_Motor"; break;
                default:
                    target = mn.StartsWith("BAKE_PREVIEW.") ? "PC_" + mn.Replace("BAKE_PREVIEW", "Housing") : null;
                    break;
            }
            return target != null ? Load(target) : null;
        }
    }
}

namespace WebGL.Core.Managers
{
    /// <summary>
    /// Fase 3B, SOLO PC: las cabezas de los tornillos son cilindros de 12 lados (la malla de baja resolucion del FBX):
    /// las normales de la pared ya son radiales, pero la SILUETA sigue siendo un dodecagono y se ve facetada de cerca.
    /// Copia la malla (el FBX y WebGL no cambian), detecta el eje y el anillo exterior del cilindro y subdivide dos
    /// veces (12 -> 24 -> 48 lados) empujando los puntos medios de las aristas del anillo exterior a la circunferencia.
    /// La tapa (plana, con el hueco hexagonal y la UV de Hex_AO) y el resto de vertices conservan posicion y UV; los
    /// puntos medios se calculan igual desde cada lado de la arista, asi que no se abren grietas entre tapa y pared.
    /// Se omite si el anillo exterior tiene menos de 10 puntos (tuercas hexagonales, nylon de 6 lados).
    /// </summary>
    internal static class PCScrewSmoother
    {
        private static readonly Dictionary<Mesh, Mesh> Cache = new Dictionary<Mesh, Mesh>();
        public static int SmoothedRenderers { get; private set; }
        public static int BuiltMeshes => Cache.Count;

        public static void Smooth(Renderer r)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            Mesh src = mf.sharedMesh;
            if (!Cache.TryGetValue(src, out Mesh round))
            {
                round = Build(src);
                Cache[src] = round;
            }

            if (round != null)
            {
                mf.sharedMesh = round;
                SmoothedRenderers++;
            }
        }

        private static Mesh Build(Mesh src)
        {
            if (!src.isReadable || src.subMeshCount != 1) { Debug.Log("[PCScrewSmoother] " + src.name + ": no legible o con varios submeshes"); return null; }
            var v = new List<Vector3>(src.vertices);
            var n = new List<Vector3>(src.normals);
            var uv = new List<Vector2>(src.uv);
            var tan = new List<Vector4>(src.tangents);
            var tris = new List<int>(src.triangles);
            if (v.Count == 0 || n.Count != v.Count || uv.Count != v.Count || tris.Count < 3) return null;

            // eje del cilindro: la direccion con mas normales perfectamente alineadas (las tapas)
            float[] axisScore = new float[3];
            foreach (Vector3 nn in n)
            {
                for (int k = 0; k < 3; k++) if (Mathf.Abs(nn[k]) > 0.99f) axisScore[k] += 1f;
            }

            int axis = axisScore[0] >= axisScore[1] && axisScore[0] >= axisScore[2] ? 0 : (axisScore[1] >= axisScore[2] ? 1 : 2);
            if (axisScore[axis] < 4f) { Debug.Log("[PCScrewSmoother] " + src.name + ": sin eje (" + axisScore[axis] + ")"); return null; }

            Bounds b = src.bounds;
            Vector3 c = b.center;
            Vector3 Radial(Vector3 p) { Vector3 d = p - c; d[axis] = 0f; return d; }

            float R = 0f;
            foreach (Vector3 p in v) R = Mathf.Max(R, Radial(p).magnitude);
            if (R < 1e-9f) return null;

            // puntos del anillo exterior (posiciones unicas)
            var ring = new HashSet<long>();
            foreach (Vector3 p in v)
            {
                if (Radial(p).magnitude >= 0.97f * R) ring.Add(Key(p));
            }

            // en un 12-gono hay 12 posiciones por anillo y 2 anillos (>= 20 con la pared): hexagonos (6 x 2) se omiten
            if (ring.Count < 16) { Debug.Log("[PCScrewSmoother] " + src.name + ": anillo de " + ring.Count + " puntos, omitida"); return null; }

            for (int pass = 0; pass < 2; pass++)
            {
                var mid = new Dictionary<long, int>();
                int Mid(int a, int bIdx)
                {
                    long key = a < bIdx ? ((long)a << 32) | (uint)bIdx : ((long)bIdx << 32) | (uint)a;
                    if (mid.TryGetValue(key, out int found)) return found;
                    Vector3 pa = v[a], pb = v[bIdx];
                    Vector3 pm = (pa + pb) * 0.5f;
                    Vector3 nm = (n[a] + n[bIdx]);
                    bool bothRing = Radial(pa).magnitude >= 0.97f * R && Radial(pb).magnitude >= 0.97f * R
                        && Vector3.Angle(Radial(pa), Radial(pb)) < 60f;
                    if (bothRing)
                    {
                        Vector3 rad = Radial(pm);
                        if (rad.sqrMagnitude > 1e-18f)
                        {
                            float axisVal = pm[axis];
                            pm = c + rad.normalized * R;
                            pm[axis] = axisVal;
                            // pared lateral (normales casi perpendiculares al eje): la normal es radial en el punto nuevo
                            if (Mathf.Abs(n[a][axis]) < 0.3f && Mathf.Abs(n[bIdx][axis]) < 0.3f) nm = rad.normalized;
                        }
                    }

                    Vector4 ta = tan.Count == v.Count ? tan[a] : Vector4.zero, tb = tan.Count == v.Count ? tan[bIdx] : Vector4.zero;
                    Vector4 tm = (ta + tb) * 0.5f;
                    Vector3 t3 = new Vector3(tm.x, tm.y, tm.z);
                    if (t3.sqrMagnitude > 1e-12f) { t3.Normalize(); tm = new Vector4(t3.x, t3.y, t3.z, ta.w); }

                    int idx = v.Count;
                    v.Add(pm);
                    n.Add(nm.sqrMagnitude > 1e-12f ? nm.normalized : n[a]);
                    uv.Add((uv[a] + uv[bIdx]) * 0.5f);
                    if (tan.Count == idx) tan.Add(tm);
                    mid[key] = idx;
                    return idx;
                }

                var next = new List<int>(tris.Count * 4);
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b2 = tris[i + 1], c2 = tris[i + 2];
                    int ab = Mid(a, b2), bc = Mid(b2, c2), ca = Mid(c2, a);
                    next.AddRange(new[] { a, ab, ca, ab, b2, bc, ca, bc, c2, ab, bc, ca });
                }

                tris = next;
            }

            var m = new Mesh { name = src.name + "_round", indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            if (tan.Count == v.Count) m.SetTangents(tan);
            m.SetTriangles(tris, 0);
            m.bounds = src.bounds;
            Debug.Log("[PCScrewSmoother] " + src.name + ": eje " + axis + ", anillo " + ring.Count + ", vertices " + src.vertexCount + " -> " + v.Count + ", triangulos " + (src.triangles.Length / 3) + " -> " + (tris.Count / 3));
            return m;
        }

        private static long Key(Vector3 p)
        {
            const float q = 1000000f;
            long x = Mathf.RoundToInt(p.x * q), y = Mathf.RoundToInt(p.y * q), z = Mathf.RoundToInt(p.z * q);
            return (x * 73856093L) ^ (y * 19349663L) ^ (z * 83492791L);
        }
    }
}
#endif
