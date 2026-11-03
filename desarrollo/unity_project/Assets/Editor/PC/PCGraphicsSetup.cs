using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WebGL.Editor.PC
{
    /// <summary>
    /// Fase 1 de la build de PC: crea (de forma idempotente) los assets URP de PC, el perfil de
    /// post-procesado y los niveles de calidad PC_Low / PC_Medium / PC_High / PC_Ultra.
    ///
    /// Aislamiento de WebGL:
    ///  - Los niveles PC_* se excluyen de todas las plataformas salvo Standalone
    ///    (excludedTargetPlatforms) y el nivel "Low" original queda solo para el resto.
    ///  - URP_AutoFixed.asset y su renderer NO se tocan (los assets de PC son copias).
    ///  - El perfil de post-procesado va en el campo Volume Profile de cada asset URP de PC, no
    ///    en el Global Volume compartido de la escena.
    /// </summary>
    public static class PCGraphicsSetup
    {
        public const string Folder = "Assets/Settings/PC";
        public const string ProfilePath = Folder + "/PC_PostProcess.asset";
        public const string RendererPath = Folder + "/PC_Renderer.asset";
        private const string SourceAssetPath = "Assets/Settings/URP_AutoFixed.asset";
        private const string SourceRendererPath = "Assets/Settings/URP_HighFidelity_Renderer.asset";

        private static readonly string[] AllPlatforms =
        {
            "Android", "EmbeddedLinux", "GameCoreScarlett", "GameCoreXboxOne", "Kepler", "LinuxHeadlessSimulation",
            "Nintendo Switch", "Nintendo Switch 2", "PS4", "PS5", "QNX", "Server", "VisionOS", "WebGL",
            "Windows Store Apps", "XboxOne", "iPhone", "tvOS"
        };

        private struct Level
        {
            public string name;
            public bool hdr64;
            public int msaa;
            public float renderScale;
            public int shadowRes;
            public int cascades;
            public float shadowDistance;
            public Vector3 cascade4Split;
            public bool softShadows;
            public int softQuality;       // 1 low, 2 medium, 3 high
            public bool addShadows;
            public int addShadowRes;
            public int lutSize;
            public int mipLimit;
            public int aniso;
            public float lodBias;
            public int pixelLights;
        }

        private static readonly Level[] Levels =
        {
            new Level { name = "PC_Low",    hdr64 = false, msaa = 2, renderScale = 1.0f,  shadowRes = 1024, cascades = 1, shadowDistance = 20f, cascade4Split = new Vector3(0.067f, 0.2f, 0.467f), softShadows = false, softQuality = 1, addShadows = false, addShadowRes = 512,  lutSize = 32, mipLimit = 1, aniso = 1, lodBias = 1.0f, pixelLights = 2 },
            new Level { name = "PC_Medium", hdr64 = false, msaa = 2, renderScale = 1.0f,  shadowRes = 2048, cascades = 2, shadowDistance = 30f, cascade4Split = new Vector3(0.067f, 0.2f, 0.467f), softShadows = true,  softQuality = 2, addShadows = false, addShadowRes = 1024, lutSize = 32, mipLimit = 0, aniso = 2, lodBias = 1.0f, pixelLights = 4 },
            new Level { name = "PC_High",   hdr64 = false, msaa = 4, renderScale = 1.0f,  shadowRes = 2048, cascades = 4, shadowDistance = 35f, cascade4Split = new Vector3(0.08f, 0.22f, 0.5f),   softShadows = true,  softQuality = 3, addShadows = true,  addShadowRes = 1024, lutSize = 32, mipLimit = 0, aniso = 2, lodBias = 1.5f, pixelLights = 4 },
            new Level { name = "PC_Ultra",  hdr64 = true,  msaa = 4, renderScale = 1.25f, shadowRes = 4096, cascades = 4, shadowDistance = 40f, cascade4Split = new Vector3(0.08f, 0.22f, 0.5f),   softShadows = true,  softQuality = 3, addShadows = true,  addShadowRes = 2048, lutSize = 64, mipLimit = 0, aniso = 2, lodBias = 2.0f, pixelLights = 6 },
        };

        [MenuItem("TwinSight/PC/Build PC Graphics (fase 1)")]
        public static void BuildAll()
        {
            EnsureFolder();
            EnsureLayer();
            VolumeProfile profile = BuildPostProcessProfile();
            ScriptableRendererData renderer = EnsureRenderer();

            var assets = new List<UniversalRenderPipelineAsset>();
            foreach (Level level in Levels)
            {
                assets.Add(BuildUrpAsset(level, renderer, profile));
            }

            AssetDatabase.SaveAssets();
            ConfigureQualityLevels(assets);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PCGraphicsSetup] Listo: renderer, perfil, 4 assets URP y niveles de calidad PC_*.");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/Settings", "PC");
            }
        }

        private static void EnsureLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty slot = layers.GetArrayElementAtIndex(31);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = "PCReflectionOnly";
                tagManager.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ── Perfil de post-procesado (G1) ──

        public static VolumeProfile BuildPostProcessProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "PC_PostProcess";
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            var tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.active = true;
            // Neutral: ACES aplasta el degradado oscuro del fondo (28 -> 6 de 255) y satura los azules; ver PLAN_PC.md.
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var bloom = GetOrAdd<Bloom>(profile);
            bloom.active = true;
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.4f);
            bloom.scatter.Override(0.6f);
            bloom.clamp.Override(65472f);
            bloom.highQualityFiltering.Override(true);

            var color = GetOrAdd<ColorAdjustments>(profile);
            color.active = true;
            color.postExposure.Override(0.15f);
            color.contrast.Override(8f);

            var vignette = GetOrAdd<Vignette>(profile);
            vignette.active = true;
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.4f);
            vignette.rounded.Override(false);

            // DOF desactivado por defecto (estorba al inspeccionar); queda listo para activarse.
            var dof = GetOrAdd<DepthOfField>(profile);
            dof.active = false;
            dof.mode.Override(DepthOfFieldMode.Off);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing)) return existing;
            T created = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(created, profile);
            return created;
        }

        // ── Renderer propio de PC (copia; no se toca el compartido con WebGL) ──

        private static ScriptableRendererData EnsureRenderer()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
            if (existing != null) return existing;

            if (!AssetDatabase.CopyAsset(SourceRendererPath, RendererPath))
            {
                Debug.LogError("[PCGraphicsSetup] No se pudo copiar el renderer.");
                return AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(SourceRendererPath);
            }
            return AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
        }

        // ── Assets URP (G2/G4/G5) ──

        private static UniversalRenderPipelineAsset BuildUrpAsset(Level lv, ScriptableRendererData renderer, VolumeProfile profile)
        {
            string path = Folder + "/" + lv.name + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null)
            {
                if (!AssetDatabase.CopyAsset(SourceAssetPath, path))
                {
                    Debug.LogError("[PCGraphicsSetup] No se pudo copiar " + SourceAssetPath);
                    return null;
                }
                asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            }

            var so = new SerializedObject(asset);
            so.FindProperty("m_RendererDataList").arraySize = 1;
            so.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            so.FindProperty("m_DefaultRendererIndex").intValue = 0;

            so.FindProperty("m_SupportsHDR").boolValue = true;
            so.FindProperty("m_HDRColorBufferPrecision").intValue = lv.hdr64 ? 1 : 0;
            so.FindProperty("m_MSAA").intValue = lv.msaa;
            so.FindProperty("m_RenderScale").floatValue = lv.renderScale;
            so.FindProperty("m_UpscalingFilter").intValue = 0; // Auto
            so.FindProperty("m_RequireDepthTexture").boolValue = true;

            so.FindProperty("m_MainLightRenderingMode").intValue = 1;
            so.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            so.FindProperty("m_MainLightShadowmapResolution").intValue = lv.shadowRes;
            so.FindProperty("m_ShadowCascadeCount").intValue = lv.cascades;
            so.FindProperty("m_ShadowDistance").floatValue = lv.shadowDistance;
            so.FindProperty("m_Cascade2Split").floatValue = 0.25f;
            so.FindProperty("m_Cascade4Split").vector3Value = lv.cascade4Split;
            so.FindProperty("m_SoftShadowsSupported").boolValue = lv.softShadows;
            so.FindProperty("m_SoftShadowQuality").intValue = lv.softQuality;
            so.FindProperty("m_ShadowDepthBias").floatValue = 1f;
            so.FindProperty("m_ShadowNormalBias").floatValue = 1f;

            so.FindProperty("m_AdditionalLightsRenderingMode").intValue = 2; // Per Pixel
            so.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = lv.pixelLights;
            so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = lv.addShadows;
            so.FindProperty("m_AdditionalLightsShadowmapResolution").intValue = lv.addShadowRes;
            so.FindProperty("m_AdditionalLightsShadowResolutionTierLow").intValue = 512;
            so.FindProperty("m_AdditionalLightsShadowResolutionTierMedium").intValue = 1024;
            so.FindProperty("m_AdditionalLightsShadowResolutionTierHigh").intValue = lv.addShadowRes;

            so.FindProperty("m_ReflectionProbeBlending").boolValue = false;
            so.FindProperty("m_ReflectionProbeBoxProjection").boolValue = false;

            so.FindProperty("m_ColorGradingMode").intValue = 1; // HDR
            so.FindProperty("m_ColorGradingLutSize").intValue = lv.lutSize;
            so.FindProperty("m_UseFastSRGBLinearConversion").boolValue = false;

            // Post-procesado propio del nivel (no se usa el Global Volume compartido con WebGL).
            so.FindProperty("m_VolumeProfile").objectReferenceValue = profile;
            // Volume Update Mode = Every Frame (el asset de origen estaba en Via Scripting, que no actualiza el stack).
            so.FindProperty("m_VolumeFrameworkUpdateMode").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(asset);
            return asset;
        }

        // ── Niveles de calidad (G5) ──

        private static void ConfigureQualityLevels(List<UniversalRenderPipelineAsset> assets)
        {
            var qs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            SerializedProperty levels = qs.FindProperty("m_QualitySettings");

            // Nivel 0 ("Low", el de WebGL): solo se excluye de Standalone. Sin cambios para WebGL.
            SetExcluded(levels.GetArrayElementAtIndex(0), new[] { "Standalone" });

            for (int i = 0; i < Levels.Length; i++)
            {
                Level lv = Levels[i];
                int index = FindLevel(levels, lv.name);
                if (index < 0)
                {
                    levels.InsertArrayElementAtIndex(levels.arraySize - 1);
                    index = levels.arraySize - 1;
                    // El nuevo elemento copia al anterior: se sobrescribe todo lo relevante abajo.
                }

                SerializedProperty e = levels.GetArrayElementAtIndex(index);
                e.FindPropertyRelative("name").stringValue = lv.name;
                e.FindPropertyRelative("pixelLightCount").intValue = lv.pixelLights;
                e.FindPropertyRelative("shadows").intValue = 2;
                e.FindPropertyRelative("shadowResolution").intValue = 3;
                e.FindPropertyRelative("shadowProjection").intValue = 1;
                e.FindPropertyRelative("shadowCascades").intValue = lv.cascades >= 4 ? 4 : (lv.cascades >= 2 ? 2 : 1);
                e.FindPropertyRelative("shadowDistance").floatValue = lv.shadowDistance;
                e.FindPropertyRelative("globalTextureMipmapLimit").intValue = lv.mipLimit;
                e.FindPropertyRelative("anisotropicTextures").intValue = lv.aniso;
                e.FindPropertyRelative("antiAliasing").intValue = lv.msaa;
                e.FindPropertyRelative("realtimeReflectionProbes").boolValue = true;
                e.FindPropertyRelative("vSyncCount").intValue = 1;
                e.FindPropertyRelative("lodBias").floatValue = lv.lodBias;
                e.FindPropertyRelative("customRenderPipeline").objectReferenceValue = assets[i];
                SetExcluded(e, AllPlatforms);
            }

            // Plataformas: WebGL sigue con el nivel 0; Standalone arranca en PC_Ultra.
            SerializedProperty perPlatform = qs.FindProperty("m_PerPlatformDefaultQuality");
            int ultra = FindLevel(levels, "PC_Ultra");
            for (int i = 0; i < perPlatform.arraySize; i++)
            {
                SerializedProperty entry = perPlatform.GetArrayElementAtIndex(i);
                string key = entry.FindPropertyRelative("first").stringValue;
                if (key == "Standalone") entry.FindPropertyRelative("second").intValue = ultra;
            }

            qs.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int FindLevel(SerializedProperty levels, string name)
        {
            for (int i = 0; i < levels.arraySize; i++)
            {
                if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name) return i;
            }
            return -1;
        }

        private static void SetExcluded(SerializedProperty level, string[] platforms)
        {
            SerializedProperty list = level.FindPropertyRelative("excludedTargetPlatforms");
            list.arraySize = platforms.Length;
            for (int i = 0; i < platforms.Length; i++)
            {
                list.GetArrayElementAtIndex(i).stringValue = platforms[i];
            }
        }
    }
}
