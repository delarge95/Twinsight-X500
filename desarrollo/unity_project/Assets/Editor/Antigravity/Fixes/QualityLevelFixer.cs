using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace WebGL.Editor.Fixes
{
    /// <summary>
    /// FIX 2: Set WebGL to use proper Quality Level.
    /// Ensures all quality levels use the correct URP asset.
    /// </summary>
    public static class QualityLevelFixer
    {
        internal static bool IsPcLevel(string levelName)
        {
            return !string.IsNullOrEmpty(levelName) && levelName.StartsWith("PC_");
        }

        [MenuItem("WebGL/Fixes/2. Set WebGL Quality Level")]
        public static int Fix()
        {
            string[] candidates =
            {
                URPAssetFixer.FixedAssetPath,
                "Assets/Settings/URP_HighFidelity.asset",
                "Assets/Settings/High_PipelineAsset.asset"
            };

            RenderPipelineAsset bestAsset = null;
            foreach (var path in candidates)
            {
                var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
                if (asset != null) { bestAsset = asset; break; }
            }

            if (bestAsset == null)
            {
                Debug.LogWarning($"{WebGLBuildFixer.LOG_PREFIX} FIX 2: No URP asset found for quality levels");
                return 0;
            }

            var allNames = QualitySettings.names;
            int currentLevel = QualitySettings.GetQualityLevel();

            // Los niveles PC_* (fase 1 de la build de escritorio) tienen su propio asset URP:
            // este fixer es solo para los niveles de WebGL y nunca debe tocarlos.
            if (!IsPcLevel(allNames[currentLevel]))
            {
                QualitySettings.renderPipeline = bestAsset;
                Debug.Log($"{WebGLBuildFixer.LOG_PREFIX} <color=green>FIX 2:</color> Set current quality level pipeline to '{bestAsset.name}'");
            }

            int touched = 0;
            for (int i = 0; i < allNames.Length; i++)
            {
                if (IsPcLevel(allNames[i])) continue;
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = bestAsset;
                touched++;
            }
            QualitySettings.SetQualityLevel(currentLevel, false);
            Debug.Log($"{WebGLBuildFixer.LOG_PREFIX} FIX 2: Applied URP asset to {touched} WebGL quality level(s) ({allNames.Length - touched} PC level(s) left untouched)");

            return 1;
        }
    }
}
