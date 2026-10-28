using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WebGL.Core.Managers
{
    public enum QualityTier
    {
        Auto = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Ultra = 4
    }

    /// <summary>
    /// Sistema de calidad grafica por tiers (Auto/Low/Medium/High/Ultra).
    /// NO cambia el pipeline asset en runtime (cambiar URP assets no es fiable en WebGL):
    /// ajusta las propiedades del asset activo (render scale, MSAA, sombras, luces
    /// adicionales, HDR) y el limite de mipmaps de texturas por tier.
    /// La configuracion avanzada permite ajustar cada variable individualmente.
    /// </summary>
    public static class RenderQualityController
    {
        private const string TierPrefKey = "quality_tier";
        private const string LegacyModePrefKey = "render_quality_mode";

        // Advanced override pref keys
        private const string AdvRenderScaleKey = "adv_render_scale";
        private const string AdvShadowsKey = "adv_shadows";
        private const string AdvShadowDistanceKey = "adv_shadow_distance";
        private const string AdvMsaaKey = "adv_msaa";
        private const string AdvTextureLimitKey = "adv_texture_limit";
        private const string AdvSoftboxesKey = "adv_softboxes";
        private const string AdvCompatLightingKey = "adv_compat_lighting";

        private static QualityTier _resolvedTier;

        public static event System.Action<QualityTier> OnTierChanged;

        public static QualityTier SavedTier => (QualityTier)PlayerPrefs.GetInt(TierPrefKey, (int)MigrateLegacyModePref());
        public static QualityTier ResolvedTier => _resolvedTier;
        public static bool SoftboxesEnabled => GetAdvancedBool(AdvSoftboxesKey, _resolvedTier >= QualityTier.High);

        /// <summary>
        /// Iluminacion Lambert simple en ClippableLit (opcion de diagnostico / bajo consumo).
        /// </summary>
        public static bool CompatibilityLightingEnabled =>
            GetAdvancedBool(AdvCompatLightingKey, false);

        public static void SetCompatibilityLighting(bool enabled)
        {
            PlayerPrefs.SetInt(AdvCompatLightingKey, enabled ? 1 : 0);
            ApplyGraphicsSettings();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            ApplyTier(SavedTier, savePref: false);
        }

        public static float GetTierRenderScaleDefault(QualityTier tier) => tier switch
        {
            QualityTier.Low => 0.6f,
            QualityTier.Medium => 0.8f,
            _ => 1.0f
        };

        public static bool GetTierShadowsDefault(QualityTier tier) => tier >= QualityTier.High;

        public static float GetTierShadowDistanceDefault(QualityTier tier) => tier >= QualityTier.Ultra ? 25f : 12f;

        public static int GetTierMsaaDefault(QualityTier tier) => tier >= QualityTier.Ultra ? 4 : 2;

        public static int GetTierTextureLimitDefault(QualityTier tier) => tier switch
        {
            QualityTier.Low => 2,
            QualityTier.Medium => 1,
            _ => 0
        };

        public static bool GetTierSoftboxesDefault(QualityTier tier) => tier >= QualityTier.High;

        public static void SetTier(QualityTier tier)
        {
            ClearAdvancedOverrides();
            ApplyTier(tier, savePref: true);
        }

        // ── Advanced per-variable overrides ──

        public static float GetAdvancedRenderScale(float tierDefault)
        {
            return PlayerPrefs.GetFloat(AdvRenderScaleKey, tierDefault);
        }

        public static bool GetAdvancedShadows(bool tierDefault)
        {
            return PlayerPrefs.GetInt(AdvShadowsKey, tierDefault ? 1 : 0) == 1;
        }

        public static float GetAdvancedShadowDistance(float tierDefault)
        {
            return PlayerPrefs.GetFloat(AdvShadowDistanceKey, tierDefault);
        }

        public static int GetAdvancedMsaa(int tierDefault)
        {
            return PlayerPrefs.GetInt(AdvMsaaKey, tierDefault);
        }

        public static int GetAdvancedTextureLimit(int tierDefault)
        {
            return PlayerPrefs.GetInt(AdvTextureLimitKey, tierDefault);
        }

        public static bool GetAdvancedBool(string key, bool tierDefault)
        {
            return PlayerPrefs.GetInt(key, tierDefault ? 1 : 0) == 1;
        }

        public static void SetAdvancedRenderScale(float value)
        {
            PlayerPrefs.SetFloat(AdvRenderScaleKey, Mathf.Clamp(value, 0.5f, 1.5f));
            ApplyGraphicsSettings();
        }

        public static void SetAdvancedShadows(bool enabled)
        {
            PlayerPrefs.SetInt(AdvShadowsKey, enabled ? 1 : 0);
            ApplyGraphicsSettings();
        }

        public static void SetAdvancedShadowDistance(float value)
        {
            PlayerPrefs.SetFloat(AdvShadowDistanceKey, Mathf.Clamp(value, 0f, 40f));
            ApplyGraphicsSettings();
        }

        public static void SetAdvancedMsaa(int samples)
        {
            PlayerPrefs.SetInt(AdvMsaaKey, Mathf.Clamp(samples, 0, 8));
            ApplyGraphicsSettings();
        }

        public static void SetAdvancedTextureLimit(int limit)
        {
            PlayerPrefs.SetInt(AdvTextureLimitKey, Mathf.Clamp(limit, 0, 3));
            ApplyGraphicsSettings();
        }

        public static void SetAdvancedSoftboxes(bool enabled)
        {
            PlayerPrefs.SetInt(AdvSoftboxesKey, enabled ? 1 : 0);
            ApplyGraphicsSettings();
        }

        public static void ClearAdvancedOverrides()
        {
            PlayerPrefs.DeleteKey(AdvRenderScaleKey);
            PlayerPrefs.DeleteKey(AdvShadowsKey);
            PlayerPrefs.DeleteKey(AdvShadowDistanceKey);
            PlayerPrefs.DeleteKey(AdvMsaaKey);
            PlayerPrefs.DeleteKey(AdvTextureLimitKey);
            PlayerPrefs.DeleteKey(AdvSoftboxesKey);
            PlayerPrefs.DeleteKey(AdvCompatLightingKey);
            PlayerPrefs.Save();
        }

        // ── Application ──

        private static void ApplyTier(QualityTier tier, bool savePref)
        {
            QualityTier resolved = tier == QualityTier.Auto ? ResolveAutoTier() : tier;

            if (savePref)
            {
                PlayerPrefs.SetInt(TierPrefKey, (int)tier);
                PlayerPrefs.Save();
            }

            _resolvedTier = resolved;
            ApplyGraphicsSettings();

            OnTierChanged?.Invoke(resolved);
        }

        private static void ApplyGraphicsSettings()
        {
            // Modo compatibilidad de iluminacion para ClippableLit (WebGL por defecto).
            // Se establece SIEMPRE, independientemente del asset activo.
            Shader.SetGlobalFloat("_CompatibilityMode", CompatibilityLightingEnabled ? 1f : 0f);

            UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            }
            if (urp == null)
            {
                return;
            }

            // En el player WebGL los setters persisten solo en memoria, pero un asset
            // "sucio" (render scale alto + HDR + MSAA 4, p.ej. 4K) agota los framebuffers
            // y deja la escena sin dibujar. Clamps estrictos para web.
            bool isWebGLPlayer = Application.platform == RuntimePlatform.WebGLPlayer;
            float maxRenderScale = isWebGLPlayer ? 1.0f : 1.5f;
            int maxMsaa = isWebGLPlayer ? 2 : 8;

            bool tierShadowsDefault = _resolvedTier >= QualityTier.High;
            float tierRenderScaleDefault = _resolvedTier switch
            {
                QualityTier.Low => 0.6f,
                QualityTier.Medium => 0.8f,
                _ => 1.0f
            };
            int tierMsaaDefault = _resolvedTier >= QualityTier.Ultra ? 4 : 2;
            float tierShadowDistanceDefault = _resolvedTier >= QualityTier.Ultra ? 25f : 12f;
            int tierTextureLimitDefault = _resolvedTier switch
            {
                QualityTier.Low => 2,
                QualityTier.Medium => 1,
                _ => 0
            };

            urp.renderScale = Mathf.Clamp(GetAdvancedRenderScale(tierRenderScaleDefault), 0.5f, maxRenderScale);
            urp.msaaSampleCount = Mathf.Clamp(GetAdvancedMsaa(tierMsaaDefault), 0, maxMsaa);

            bool shadows = GetAdvancedShadows(tierShadowsDefault);
            float shadowDistance = shadows ? GetAdvancedShadowDistance(tierShadowDistanceDefault) : 0f;
            urp.shadowDistance = Mathf.Clamp(shadowDistance, 0f, 40f);

            // NUNCA activar HDR en runtime: en WebGL rompe la cadena de render.

            QualitySettings.globalTextureMipmapLimit = GetAdvancedTextureLimit(tierTextureLimitDefault);
        }

        private static QualityTier ResolveAutoTier()
        {
            // En WebGL los navegadores (Safari sobre todo) no reportan VRAM fiable
            // y sombras/luces adicionales son la ruta de render mas fragil:
            // Auto arranca en Medium (sin sombras, sin softboxes) como base segura.
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                return SystemInfo.deviceType == DeviceType.Handheld
                    ? QualityTier.Low
                    : QualityTier.Medium;
            }

            if (SystemInfo.deviceType == DeviceType.Handheld)
            {
                return SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize <= 2048
                    ? QualityTier.Low
                    : QualityTier.Medium;
            }

            int desktopVramMb = SystemInfo.graphicsMemorySize;
            if (desktopVramMb > 0 && desktopVramMb <= 2048) return QualityTier.Low;
            if (desktopVramMb > 0 && desktopVramMb <= 4096) return QualityTier.Medium;
            if (desktopVramMb > 0 && desktopVramMb <= 8192) return QualityTier.High;
            return QualityTier.Ultra;
        }

        private static QualityTier MigrateLegacyModePref()
        {
            if (PlayerPrefs.HasKey(LegacyModePrefKey))
            {
                int legacy = PlayerPrefs.GetInt(LegacyModePrefKey, 0);
                PlayerPrefs.DeleteKey(LegacyModePrefKey);
                PlayerPrefs.SetInt(TierPrefKey, legacy == 1 ? (int)QualityTier.Low : (int)QualityTier.High);
                PlayerPrefs.Save();
            }
            return QualityTier.Auto;
        }
    }
}
