Shader "Hidden/WebGL/CRTGlitch"
{
    Properties
    {
        _Curvature ("CRT Curvature", Range(0, 0.15)) = 0.03
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.18
        _ScanlineCount ("Scanline Count", Float) = 720.0
        _ScanlineSpeed ("Scanline Speed", Float) = 1.2
        _ChromaticAberration ("Chromatic Aberration", Range(0, 0.03)) = 0.005
        _GlitchIntensity ("Glitch Intensity", Range(0, 0.08)) = 0.015
        _GlitchFrequency ("Glitch Frequency", Range(0, 1)) = 0.08
        _GlitchBands ("Glitch Bands", Float) = 48.0
        _VignetteStrength ("Vignette Strength", Range(0, 1)) = 0.25
        _VignettePower ("Vignette Power", Range(0.1, 1.0)) = 0.2
        _NoiseStrength ("Film Grain / Noise", Range(0, 0.1)) = 0.025
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "CRTGlitchPass"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Curvature;
            float _ScanlineIntensity;
            float _ScanlineCount;
            float _ScanlineSpeed;
            float _ChromaticAberration;
            float _GlitchIntensity;
            float _GlitchFrequency;
            float _GlitchBands;
            float _VignetteStrength;
            float _VignettePower;
            float _NoiseStrength;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // 1. Barrel / CRT Curvature distortion
                if (_Curvature > 0.001)
                {
                    float2 centered = uv - 0.5;
                    float r2 = dot(centered, centered);
                    uv = 0.5 + centered * (1.0 + r2 * _Curvature * 2.0);
                }

                // CRT Out of bounds black frame
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                {
                    return half4(0.02, 0.02, 0.03, 1.0);
                }

                // 2. Glitch horizontal displacements
                float timeBlock = floor(_Time.y * 12.0);
                float bandIndex = floor(uv.y * _GlitchBands);
                float glitchRand = Hash21(float2(bandIndex, timeBlock));
                
                float glitchOffset = 0.0;
                if (glitchRand < _GlitchFrequency && _GlitchIntensity > 0.0001)
                {
                    float offsetDir = (Hash11(timeBlock * 3.7 + bandIndex) - 0.5) * 2.0;
                    glitchOffset = offsetDir * _GlitchIntensity;
                }

                float2 uvGlitch = uv + float2(glitchOffset, 0.0);
                uvGlitch = saturate(uvGlitch);

                // 3. Chromatic Aberration (RGB Separation)
                float2 caDir = (uv - 0.5) * _ChromaticAberration + float2(glitchOffset * 0.4, 0.0);
                half r = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uvGlitch + caDir).r;
                half g = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uvGlitch).g;
                half b = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uvGlitch - caDir).b;

                half3 col = half3(r, g, b);

                // 4. Subtle Scanlines
                if (_ScanlineIntensity > 0.001)
                {
                    float scan = sin(uv.y * _ScanlineCount + _Time.y * _ScanlineSpeed);
                    scan = (scan + 1.0) * 0.5;
                    col *= lerp(1.0, 0.75 + 0.25 * scan, _ScanlineIntensity);
                }

                // 5. Film grain / phosphor noise
                if (_NoiseStrength > 0.001)
                {
                    float noise = Hash21(uv * 800.0 + frac(_Time.y * 50.0)) - 0.5;
                    col += noise * _NoiseStrength;
                }

                // 6. Smooth CRT Vignette
                if (_VignetteStrength > 0.001)
                {
                    float vig = 16.0 * uv.x * uv.y * (1.0 - uv.x) * (1.0 - uv.y);
                    vig = saturate(pow(vig, _VignettePower));
                    col = lerp(col * 0.2, col, lerp(1.0, vig, _VignetteStrength));
                }

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
