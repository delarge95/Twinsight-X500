#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WebGL.Core.Managers
{
    /// <summary>
    /// Fase 3B.1, SOLO PC. Interruptores de diagnostico por linea de comandos para medir FPS y aislar el aspecto del
    /// carbono en el .exe: <c>-pcdbg flag1,flag2</c>. Sin el argumento no hace nada.
    /// Flags: noalbedobump, noaniso, noclearcoat, noscrews, notree, mip1 (texturas a la mitad), nossao, rs1 (render scale 1),
    /// probe0 (probe y reflexion global a 0), probeup0 (sin reflejo, solo luz), nocarbonspec (carbono sin especular).
    /// </summary>
    public static class PCDebugFlags
    {
        private static HashSet<string> _flags;

        public static bool Has(string flag)
        {
            if (_flags == null)
            {
                _flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string[] a = Environment.GetCommandLineArgs();
                for (int i = 0; i < a.Length - 1; i++)
                {
                    if (!a[i].Equals("-pcdbg", StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (string f in a[i + 1].Split(',')) _flags.Add(f.Trim());
                }

                if (_flags.Count > 0) Debug.Log("[PCDebug] flags: " + string.Join(",", _flags));
            }

            return _flags.Contains(flag);
        }

        public static void ApplyMaterialFlags(IEnumerable<Material> pcMaterials)
        {
            foreach (Material m in pcMaterials)
            {
                if (m == null) continue;
                if (Has("noalbedobump")) m.DisableKeyword("_PC_ALBEDOBUMP");
                if (Has("noaniso")) m.DisableKeyword("_PC_ANISO");
                if (Has("noclearcoat")) m.DisableKeyword("_CLEARCOAT");
            }
        }

        public static void ApplyGlobalFlags()
        {
            if (Has("mip1")) QualitySettings.globalTextureMipmapLimit = 1;
            if (Has("rs1") && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.renderScale = 1f;
            if (Has("nossao"))
            {
                foreach (var f in Resources.FindObjectsOfTypeAll<ScreenSpaceAmbientOcclusion>()) if (f != null) f.SetActive(false);
            }
        }
    }
}
#endif
