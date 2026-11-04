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
                }
                _done.Add(r);
            }
            SwappedCount += count;
            Debug.Log("[PCMaterialSwapper] Renderers con material PC: " + count + " de " + renderers.Count);
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
#endif
