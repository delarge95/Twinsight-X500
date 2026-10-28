using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using System;

namespace WebGL.Core.Rendering
{
    public class CRTGlitchFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            public Material crtGlitchMaterial;
            [Range(0f, 0.15f)] public float curvature = 0.02f;
            [Range(0f, 1f)] public float scanlineIntensity = 0.14f;
            public float scanlineCount = 720f;
            public float scanlineSpeed = 1.0f;
            [Range(0f, 0.03f)] public float chromaticAberration = 0.004f;
            [Range(0f, 0.08f)] public float glitchIntensity = 0.012f;
            [Range(0f, 1f)] public float glitchFrequency = 0.06f;
            public float glitchBands = 48f;
            [Range(0f, 1f)] public float vignetteStrength = 0.2f;
            [Range(0.1f, 1f)] public float vignettePower = 0.22f;
            [Range(0f, 0.1f)] public float noiseStrength = 0.015f;
        }

        public Settings settings = new Settings();
        private CRTGlitchPass _glitchPass;

        // Static toggles for runtime / UI control
        public static bool GlobalEnabled { get; set; } = false;
        public static float OverrideGlitchIntensity { get; set; } = -1f;
        public static float OverrideChromaticAberration { get; set; } = -1f;

        public override void Create()
        {
            _glitchPass = new CRTGlitchPass(settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!GlobalEnabled) return;
            if (settings.crtGlitchMaterial == null) return;
            if (renderingData.cameraData.cameraType != CameraType.Game) return;
            renderer.EnqueuePass(_glitchPass);
        }

        private class CRTGlitchPass : ScriptableRenderPass
        {
            private readonly Settings _settings;
            private bool _renderGraphErrorLogged;

            private static readonly int CurvatureId = Shader.PropertyToID("_Curvature");
            private static readonly int ScanlineIntensityId = Shader.PropertyToID("_ScanlineIntensity");
            private static readonly int ScanlineCountId = Shader.PropertyToID("_ScanlineCount");
            private static readonly int ScanlineSpeedId = Shader.PropertyToID("_ScanlineSpeed");
            private static readonly int ChromaticAberrationId = Shader.PropertyToID("_ChromaticAberration");
            private static readonly int GlitchIntensityId = Shader.PropertyToID("_GlitchIntensity");
            private static readonly int GlitchFrequencyId = Shader.PropertyToID("_GlitchFrequency");
            private static readonly int GlitchBandsId = Shader.PropertyToID("_GlitchBands");
            private static readonly int VignetteStrengthId = Shader.PropertyToID("_VignetteStrength");
            private static readonly int VignettePowerId = Shader.PropertyToID("_VignettePower");
            private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");

            public CRTGlitchPass(Settings settings)
            {
                _settings = settings;
                renderPassEvent = settings.renderPassEvent;
                ConfigureInput(ScriptableRenderPassInput.Color);
            }

            private void UpdateMaterialProperties(Material mat)
            {
                mat.SetFloat(CurvatureId, _settings.curvature);
                mat.SetFloat(ScanlineIntensityId, _settings.scanlineIntensity);
                mat.SetFloat(ScanlineCountId, _settings.scanlineCount);
                mat.SetFloat(ScanlineSpeedId, _settings.scanlineSpeed);
                
                float ca = OverrideChromaticAberration >= 0f ? OverrideChromaticAberration : _settings.chromaticAberration;
                mat.SetFloat(ChromaticAberrationId, ca);

                float glitch = OverrideGlitchIntensity >= 0f ? OverrideGlitchIntensity : _settings.glitchIntensity;
                mat.SetFloat(GlitchIntensityId, glitch);

                mat.SetFloat(GlitchFrequencyId, _settings.glitchFrequency);
                mat.SetFloat(GlitchBandsId, _settings.glitchBands);
                mat.SetFloat(VignetteStrengthId, _settings.vignetteStrength);
                mat.SetFloat(VignettePowerId, _settings.vignettePower);
                mat.SetFloat(NoiseStrengthId, _settings.noiseStrength);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var mat = _settings.crtGlitchMaterial;
                if (mat == null) return;

                UpdateMaterialProperties(mat);

                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraColorHandle = resourceData.activeColorTexture;

                if (!cameraColorHandle.IsValid()) return;

                TextureDesc descriptor;
                try
                {
                    descriptor = renderGraph.GetTextureDesc(cameraColorHandle);
                }
                catch (Exception ex)
                {
                    if (!_renderGraphErrorLogged)
                    {
                        _renderGraphErrorLogged = true;
                        Debug.LogWarning($"[CRTGlitchFeature] Skipping pass: {ex.Message}");
                    }
                    return;
                }

                if (descriptor.width <= 0 || descriptor.height <= 0) return;

                descriptor.name = "_CRTGlitchTemp";
                descriptor.clearBuffer = false;
                var tempTexture = renderGraph.CreateTexture(descriptor);

                try
                {
                    // 1. Source -> Temp with CRTGlitch shader
                    RenderGraphUtils.BlitMaterialParameters blitToTemp =
                        new(cameraColorHandle, tempTexture, mat, 0);
                    renderGraph.AddBlitPass(blitToTemp, "CRT Glitch Blit");

                    // 2. Temp -> Source copy back
                    RenderGraphUtils.BlitMaterialParameters blitBack =
                        new(tempTexture, cameraColorHandle, Blitter.GetBlitMaterial(TextureDimension.Tex2D), 0);
                    renderGraph.AddBlitPass(blitBack, "CRT Glitch Copy Back");
                }
                catch (Exception ex)
                {
                    if (!_renderGraphErrorLogged)
                    {
                        _renderGraphErrorLogged = true;
                        Debug.LogWarning($"[CRTGlitchFeature] RenderGraph pass failed and skipped: {ex.Message}");
                    }
                }
            }
        }
    }
}
