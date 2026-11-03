using System;
using System.Collections.Generic;
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
    ///
    /// PC (UNITY_STANDALONE): cada tier selecciona un nivel de calidad propio (PC_Low,
    /// PC_Medium, PC_High, PC_Ultra) con su asset URP (HDR, MSAA, sombras en cascada,
    /// perfil de post-procesado). Las variables avanzadas solo pisan al asset si el usuario
    /// las ha tocado. WebGL conserva el comportamiento anterior sin cambios.
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

#if UNITY_STANDALONE
        // -- PC (Standalone) --
        private const string AdvHdrKey = "adv_hdr";
        public const float PcMaxRenderScale = 2.0f;
        public const float PcMaxShadowDistance = 100f;

        private struct PcBaseline
        {
            public float renderScale;
            public int msaa;
            public float shadowDistance;
            public bool hdr;
        }

        private static readonly Dictionary<UniversalRenderPipelineAsset, PcBaseline> PcBaselines = new Dictionary<UniversalRenderPipelineAsset, PcBaseline>();
        private static UniversalRenderPipelineAsset _pcAsset;
        private static bool _pcCliQuality;
#endif

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
#if UNITY_STANDALONE
            // Argumento de linea de comandos opcional: -quality <PC_Low|PC_Medium|PC_High|PC_Ultra|low|medium|high|ultra|auto>
            if (TryGetCommandLineTier(out QualityTier cliTier))
            {
                _pcCliQuality = true;
                ApplyTier(cliTier, savePref: false);
                PCStudioReflections.Ensure();
                return;
            }
#endif
            ApplyTier(SavedTier, savePref: false);
#if UNITY_STANDALONE
            PCStudioReflections.Ensure();
#endif
        }

        public static float GetTierRenderScaleDefault(QualityTier tier) =>
#if UNITY_STANDALONE
            TryGetPcBaseline(out PcBaseline pcBase) ? pcBase.renderScale :
#endif
            tier switch
        {
            QualityTier.Low => 0.6f,
            QualityTier.Medium => 0.8f,
            _ => 1.0f
        };

        public static bool GetTierShadowsDefault(QualityTier tier) =>
#if UNITY_STANDALONE
            TryGetPcBaseline(out PcBaseline pcBase) ? pcBase.shadowDistance > 0f :
#endif
            tier >= QualityTier.High;

        public static float GetTierShadowDistanceDefault(QualityTier tier) =>
#if UNITY_STANDALONE
            TryGetPcBaseline(out PcBaseline pcBase) ? pcBase.shadowDistance :
#endif
            (tier >= QualityTier.Ultra ? 25f : 12f);

        public static int GetTierMsaaDefault(QualityTier tier) =>
#if UNITY_STANDALONE
            TryGetPcBaseline(out PcBaseline pcBase) ? pcBase.msaa :
#endif
            (tier >= QualityTier.Ultra ? 4 : 2);

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
#if UNITY_STANDALONE
            PlayerPrefs.SetFloat(AdvRenderScaleKey, Mathf.Clamp(value, 0.5f, PcMaxRenderScale));
#else
            PlayerPrefs.SetFloat(AdvRenderScaleKey, Mathf.Clamp(value, 0.5f, 1.5f));
#endif
            ApplyGraphicsSettings();
        }

        public static void SetAdvancedShadows(bool enabled)
        {
            PlayerPrefs.SetInt(AdvShadowsKey, enabled ? 1 : 0);
            ApplyGraphicsSettings();
        }

        public static void SetAdvancedShadowDistance(float value)
        {
#if UNITY_STANDALONE
            PlayerPrefs.SetFloat(AdvShadowDistanceKey, Mathf.Clamp(value, 0f, PcMaxShadowDistance));
#else
            PlayerPrefs.SetFloat(AdvShadowDistanceKey, Mathf.Clamp(value, 0f, 40f));
#endif
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

#if UNITY_STANDALONE
        /// <summary>HDR del asset URP activo (solo PC). Por defecto el valor del nivel de calidad.</summary>
        public static bool GetAdvancedHdr(bool tierDefault)
        {
            return PlayerPrefs.GetInt(AdvHdrKey, tierDefault ? 1 : 0) == 1;
        }

        public static void SetAdvancedHdr(bool enabled)
        {
            PlayerPrefs.SetInt(AdvHdrKey, enabled ? 1 : 0);
            ApplyGraphicsSettings();
        }
#endif

        public static void ClearAdvancedOverrides()
        {
#if UNITY_STANDALONE
            PlayerPrefs.DeleteKey(AdvHdrKey);
#endif
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

#if UNITY_STANDALONE
            ApplyPcGraphicsSettings();
            return;
#else
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
#endif
        }

#if UNITY_STANDALONE
        // ---------------------------------------------------------------
        // PC (Standalone): niveles PC_* con asset URP propio
        // ---------------------------------------------------------------

        private static string PcLevelNameForTier(QualityTier tier) => tier switch
        {
            QualityTier.Low => "PC_Low",
            QualityTier.Medium => "PC_Medium",
            QualityTier.High => "PC_High",
            _ => "PC_Ultra"
        };

        private static bool TryGetCommandLineTier(out QualityTier tier)
        {
            tier = QualityTier.Auto;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], "-quality", StringComparison.OrdinalIgnoreCase)) continue;
                string value = args[i + 1].Trim().ToLowerInvariant();
                if (value.StartsWith("pc_")) value = value.Substring(3);
                switch (value)
                {
                    case "low": tier = QualityTier.Low; return true;
                    case "medium": tier = QualityTier.Medium; return true;
                    case "high": tier = QualityTier.High; return true;
                    case "ultra": tier = QualityTier.Ultra; return true;
                    case "auto": tier = QualityTier.Auto; return true;
                }
            }
            return false;
        }

        private static bool TryGetPcBaseline(out PcBaseline baseline)
        {
            baseline = default;
            return _pcAsset != null && PcBaselines.TryGetValue(_pcAsset, out baseline);
        }

        private static void SelectPcQualityLevel(QualityTier tier)
        {
            string wanted = PcLevelNameForTier(tier);
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == wanted)
                {
                    QualitySettings.SetQualityLevel(i, true);
                    return;
                }
            }
            Debug.LogWarning("[RenderQuality] Nivel de calidad '" + wanted + "' no encontrado; se mantiene el actual.");
        }

        private static void ApplyPcGraphicsSettings()
        {
            SelectPcQualityLevel(_resolvedTier);

            UniversalRenderPipelineAsset urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            }
            if (urp == null)
            {
                return;
            }
            _pcAsset = urp;

            // Valores del asset del nivel, capturados una sola vez (antes de cualquier override).
            if (!PcBaselines.TryGetValue(urp, out PcBaseline baseline))
            {
                baseline = new PcBaseline
                {
                    renderScale = urp.renderScale,
                    msaa = urp.msaaSampleCount,
                    shadowDistance = urp.shadowDistance,
                    hdr = urp.supportsHDR
                };
                PcBaselines[urp] = baseline;
            }

            float renderScale = baseline.renderScale;
            int msaa = baseline.msaa;
            float shadowDistance = baseline.shadowDistance;
            bool hdr = baseline.hdr;
            int textureLimit = QualitySettings.globalTextureMipmapLimit;

            // Con -quality se ignoran los overrides guardados (mediciones reproducibles).
            if (!_pcCliQuality)
            {
                if (PlayerPrefs.HasKey(AdvRenderScaleKey)) renderScale = PlayerPrefs.GetFloat(AdvRenderScaleKey);
                if (PlayerPrefs.HasKey(AdvMsaaKey)) msaa = PlayerPrefs.GetInt(AdvMsaaKey);
                if (PlayerPrefs.HasKey(AdvHdrKey)) hdr = PlayerPrefs.GetInt(AdvHdrKey) == 1;
                if (PlayerPrefs.HasKey(AdvShadowDistanceKey)) shadowDistance = PlayerPrefs.GetFloat(AdvShadowDistanceKey);
                if (PlayerPrefs.HasKey(AdvShadowsKey) && PlayerPrefs.GetInt(AdvShadowsKey) == 0) shadowDistance = 0f;
                if (PlayerPrefs.HasKey(AdvTextureLimitKey)) textureLimit = PlayerPrefs.GetInt(AdvTextureLimitKey);
            }

            urp.renderScale = Mathf.Clamp(renderScale, 0.5f, PcMaxRenderScale);
            urp.msaaSampleCount = Mathf.Clamp(msaa, 1, 8);
            urp.supportsHDR = hdr;
            urp.shadowDistance = Mathf.Clamp(shadowDistance, 0f, PcMaxShadowDistance);
            QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(textureLimit, 0, 3);

            ApplyPcCameraAntiAliasing(_resolvedTier);

            // Argumento opcional "-vsync 0|1|2" (mediciones de rendimiento sin tope de 60 FPS).
            string[] cliArgs = Environment.GetCommandLineArgs();
            for (int i = 0; i < cliArgs.Length - 1; i++)
            {
                if (string.Equals(cliArgs[i], "-vsync", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(cliArgs[i + 1], out int cliVsync))
                {
                    QualitySettings.vSyncCount = Mathf.Clamp(cliVsync, 0, 2);
                }
            }

            Debug.Log("[RenderQuality] PC tier=" + _resolvedTier +
                      " nivel=" + QualitySettings.names[QualitySettings.GetQualityLevel()] +
                      " asset=" + urp.name +
                      " renderScale=" + urp.renderScale.ToString("0.00") +
                      " msaa=" + urp.msaaSampleCount +
                      " hdr=" + urp.supportsHDR +
                      " sombras=" + urp.shadowDistance.ToString("0") + "m" +
                      " cascadas=" + urp.shadowCascadeCount +
                      " vsync=" + QualitySettings.vSyncCount);
        }

        /// <summary>
        /// AA de camara por tier (se suma al MSAA del asset). Ultra/High: SMAA (no emborrona
        /// el detalle fino y funciona con MSAA); Medium: FXAA; Low: solo MSAA bajo.
        /// </summary>
        private static void ApplyPcCameraAntiAliasing(QualityTier tier)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data == null) return;

            // La Main Camera de la escena tiene "Render Shadows" desactivado (heredado de WebGL):
            // en PC se activa para que las sombras de los assets PC_* se dibujen de verdad.
            data.renderShadows = true;
            data.renderPostProcessing = true;
            data.dithering = true; // evita bandas en los degradados oscuros del fondo con HDR

            switch (tier)
            {
                case QualityTier.Ultra:
                    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    data.antialiasingQuality = AntialiasingQuality.High;
                    break;
                case QualityTier.High:
                    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    data.antialiasingQuality = AntialiasingQuality.Medium;
                    break;
                case QualityTier.Medium:
                    data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                    break;
                default:
                    data.antialiasing = AntialiasingMode.None;
                    break;
            }
        }
#endif

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
