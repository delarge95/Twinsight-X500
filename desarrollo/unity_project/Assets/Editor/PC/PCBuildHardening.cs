#if UNITY_EDITOR
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TwinSight.EditorTools.PC
{
    /// <summary>
    /// PC only (PLAN_PC §33). A Standalone player whose URP Blit shaders are missing fails in
    /// UniversalRenderPipeline..ctor -> Blitter.Initialize (smeared / black screen, 12 000+ exceptions).
    ///  1. Pre-build: refuses to build when the URP runtime shaders (coreBlitPS, coreBlitColorAndDepthPS) are null
    ///     and tells the recipe (PLAN_PC 12.3: reimport the shader folders, wait, build again).
    ///  2. During the Standalone build the URP Blit / utility shaders are added to Always Included Shaders so they
    ///     survive stripping; they are removed afterwards, so WebGL builds do not change.
    ///  3. Menu "TwinSight/PC/Verificar build (Player.log)" runs tools/pc_postbuild_gate.ps1 on the newest build.
    /// </summary>
    public class PCBuildHardening : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        static readonly string[] BlitShaders =
        {
            "Hidden/Universal Render Pipeline/Blit", "Hidden/Universal/BlitFullPrecision", "Hidden/Universal/BlitHDROverlay",
            "Hidden/Universal/CoreBlit", "Hidden/Universal/CoreBlitColorAndDepth", "Hidden/Universal Render Pipeline/CopyDepth",
            "Hidden/Universal Render Pipeline/Sampling",
        };
        const string AddedKey = "TwinSight.PCBuildHardening.Added";

        static bool IsPcBuild(BuildTarget t) => t == BuildTarget.StandaloneWindows64 || t == BuildTarget.StandaloneWindows;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!IsPcBuild(report.summary.platform)) return;

            var rs = GraphicsSettings.GetRenderPipelineSettings<UniversalRenderPipelineRuntimeShaders>();
            if (rs == null || rs.coreBlitPS == null || rs.coreBlitColorAndDepthPS == null)
                throw new BuildFailedException("PC: los shaders Blit de URP (coreBlitPS / coreBlitColorAndDepthPS) son null; el player saldria roto. " +
                    "Receta PLAN_PC 12.3: con Standalone activo reimportar (ForceUpdate) Assets/Shaders y Packages/com.unity.render-pipelines.universal/Shaders, esperar la compilacion y volver a construir.");

            var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset"));
            SerializedProperty list = so.FindProperty("m_AlwaysIncludedShaders");
            var existing = new HashSet<Object>();
            for (int i = 0; i < list.arraySize; i++) existing.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);

            var added = new List<string>();
            foreach (string n in BlitShaders)
            {
                Shader sh = Shader.Find(n);
                if (sh == null || existing.Contains(sh)) continue;
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh;
                added.Add(AssetDatabase.GetAssetPath(sh));
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            SessionState.SetString(AddedKey, string.Join("|", added));
            UnityEngine.Debug.Log("[PCBuildHardening] shaders Blit anadidos a Always Included para la build PC: " + added.Count);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!IsPcBuild(report.summary.platform)) return;
            Restore();
        }

        [InitializeOnLoadMethod]
        static void RestoreAfterReload() { if (!BuildPipeline.isBuildingPlayer) EditorApplication.delayCall += Restore; }

        static void Restore()
        {
            string marker = SessionState.GetString(AddedKey, "");
            if (string.IsNullOrEmpty(marker)) return;
            SessionState.EraseString(AddedKey);
            var paths = new HashSet<string>(marker.Split('|'));
            var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset"));
            SerializedProperty list = so.FindProperty("m_AlwaysIncludedShaders");
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                Object o = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (o != null && paths.Contains(AssetDatabase.GetAssetPath(o)))
                {
                    list.GetArrayElementAtIndex(i).objectReferenceValue = null;   // first call clears the slot, second removes it
                    list.DeleteArrayElementAtIndex(i);
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();   // writes ProjectSettings/GraphicsSettings.asset back without the temporary entries
        }

        [MenuItem("TwinSight/PC/Verificar build (Player.log)")]
        static void Gate()
        {
            string dir = EditorUtility.OpenFolderPanel("Carpeta de la build PC", "E:/TwinSight_builds/release", "");
            if (string.IsNullOrEmpty(dir)) return;
            string script = Path.GetFullPath(Path.Combine(Application.dataPath, "../../tools/pc_postbuild_gate.ps1"));
            var psi = new ProcessStartInfo("powershell", $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -BuildDir \"{dir}\"")
            { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
            using (Process p = Process.Start(psi))
            {
                string o = p.StandardOutput.ReadToEnd(); p.WaitForExit();
                if (p.ExitCode == 0) UnityEngine.Debug.Log("[PC gate] OK\n" + o); else UnityEngine.Debug.LogError("[PC gate] FALLA\n" + o);
            }
        }
    }
}
#endif
