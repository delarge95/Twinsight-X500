// DroneLitPC - shader PBR de escritorio (fase 3A, G6). SOLO PARA PC.
//
// Extension de WebGL/ClippableLit (mismas propiedades, mismo recorte por planos globales
// _GlobalClipPlane/_GlobalClipPlane2 del modo CUT, mismo realce de seleccion por MaterialPropertyBlock)
// con funciones nuevas por keyword, apagadas por defecto:
//   _PC_METALLICMAP   mapa metallic (R) / smoothness (A)
//   _PC_ROUGHNESSMAP  mapa de rugosidad (canal R), p. ej. final_curve_roughness.png
//   _CLEARCOAT        segunda capa especular (clear-coat de URP)
//   _PC_DETAIL        normal de detalle con tiling
//   _PC_EMISSION      emision
//   _PC_TRANSLUCENT   translucidez simple en el borde (helices)
// Recibe SSAO (_SCREEN_SPACE_OCCLUSION), sombras de luces adicionales y sombras suaves de URP 17.
// No se compila para WebGL: exclude_renderers gles gles3 + ShaderVariantStripper.
Shader "TwinSight/DroneLitPC"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Metallic("Metallic", Range(0, 1)) = 0.0
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0

        [Header(Curvature and Occlusion)]
        _CurvatureMap("Curvature Map", 2D) = "gray" {}
        _CurvatureStrength("Curvature Edge Strength", Range(0, 2)) = 0.6
        _CurvatureCavityStrength("Curvature Cavity Strength", Range(0, 2)) = 0.5
        _OcclusionMap("Occlusion Map (AO)", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1.0

        [Header(PC Metallic and Roughness maps)]
        [Toggle(_PC_METALLICMAP)] _UseMetallicMap("Use Metallic/Smoothness map", Float) = 0
        _MetallicGlossMap("Metallic (R) Smoothness (A)", 2D) = "white" {}
        [Toggle(_PC_ROUGHNESSMAP)] _UseRoughnessMap("Use Roughness map", Float) = 0
        _RoughnessMap("Roughness (R)", 2D) = "white" {}
        _RoughnessMapStrength("Roughness Map Strength", Range(0, 1)) = 1.0
        _RoughnessMapScale("Roughness Map Scale", Range(0, 2)) = 1.0

        [Header(PC Clear Coat)]
        [Toggle(_CLEARCOAT)] _UseClearCoat("Clear Coat", Float) = 0
        _ClearCoat("Clear Coat Mask", Range(0, 1)) = 1.0
        _ClearCoatSmoothness("Clear Coat Smoothness", Range(0, 1)) = 0.9

        [Header(PC Detail Normal)]
        [Toggle(_PC_DETAIL)] _UseDetail("Detail Normal", Float) = 0
        [Normal] _DetailNormalMap("Detail Normal Map", 2D) = "bump" {}
        _DetailNormalScale("Detail Normal Scale", Range(0, 2)) = 1.0
        _DetailTiling("Detail Tiling", Float) = 16.0

        [Header(PC Emission)]
        [Toggle(_PC_EMISSION)] _UseEmission("Emission", Float) = 0
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionMap("Emission Map", 2D) = "white" {}

        [Header(PC Translucency)]
        [Toggle(_PC_TRANSLUCENT)] _UseTranslucent("Translucency", Float) = 0
        _TranslucencyColor("Translucency Color", Color) = (0.6, 0.65, 0.8, 1)
        _TranslucencyStrength("Translucency Strength", Range(0, 2)) = 0.4
        _TranslucencyPower("Translucency Edge Power", Range(0.5, 6)) = 2.0

        [Header(Selection Highlight)]
        _HighlightColor("Highlight Color", Color) = (0.42, 0.82, 1, 1)
        _HighlightIntensity("Highlight Intensity", Range(0, 1)) = 0.0
        _HighlightRimPower("Highlight Rim Power", Range(0.5, 8)) = 2.5

        [Header(Clipping)]
        [Toggle(_CLIP_ENABLED)] _ClipEnabled("Enable Clipping", Float) = 0
        _ClipPlane("Clip Plane", Vector) = (0, 1, 0, 0)
        _ClipColor("Clip Edge Color", Color) = (1, 0.5, 0, 1)
        _ClipEdgeWidth("Clip Edge Width", Range(0, 0.1)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles gles3
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _CLIP_ENABLED
            #pragma shader_feature_local_fragment _PC_METALLICMAP
            #pragma shader_feature_local_fragment _PC_ROUGHNESSMAP
            #pragma shader_feature_local_fragment _CLEARCOAT
            #pragma shader_feature_local_fragment _PC_DETAIL
            #pragma shader_feature_local_fragment _PC_EMISSION
            #pragma shader_feature_local_fragment _PC_TRANSLUCENT

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
            };

            TEXTURE2D(_BaseMap);          SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);          SAMPLER(sampler_BumpMap);
            TEXTURE2D(_CurvatureMap);     SAMPLER(sampler_CurvatureMap);
            TEXTURE2D(_OcclusionMap);     SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_RoughnessMap);     SAMPLER(sampler_RoughnessMap);
            TEXTURE2D(_DetailNormalMap);  SAMPLER(sampler_DetailNormalMap);
            TEXTURE2D(_EmissionMap);      SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Metallic;
                half _Smoothness;
                half _BumpScale;
                half _CurvatureStrength;
                half _CurvatureCavityStrength;
                half _OcclusionStrength;
                half _RoughnessMapStrength;
                half _RoughnessMapScale;
                half _ClearCoat;
                half _ClearCoatSmoothness;
                half _DetailNormalScale;
                half _DetailTiling;
                half4 _EmissionColor;
                half4 _TranslucencyColor;
                half _TranslucencyStrength;
                half _TranslucencyPower;
                half4 _HighlightColor;
                half _HighlightIntensity;
                half _HighlightRimPower;
                float4 _ClipPlane;
                half4 _ClipColor;
                half _ClipEdgeWidth;
            CBUFFER_END

            float4 _GlobalClipPlane;
            float _GlobalClipEnabled;
            float4 _GlobalClipPlane2;
            float _GlobalClipEnabled2;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.normalWS = normalInputs.normalWS;
                OUT.tangentWS = normalInputs.tangentWS;
                OUT.bitangentWS = normalInputs.bitangentWS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                if (_GlobalClipEnabled > 0.5)
                {
                    float gDist = dot(IN.positionWS, _GlobalClipPlane.xyz) + _GlobalClipPlane.w;
                    if (gDist < 0.0) discard;
                    if (gDist < _ClipEdgeWidth) return _ClipColor;
                }
                if (_GlobalClipEnabled2 > 0.5)
                {
                    float gDist2 = dot(IN.positionWS, _GlobalClipPlane2.xyz) + _GlobalClipPlane2.w;
                    if (gDist2 < 0.0) discard;
                    if (gDist2 < _ClipEdgeWidth) return _ClipColor;
                }
                #if defined(_CLIP_ENABLED)
                if (_GlobalClipEnabled < 0.5)
                {
                    float dist = dot(IN.positionWS, _ClipPlane.xyz) + _ClipPlane.w;
                    if (dist < 0.0) discard;
                    if (dist < _ClipEdgeWidth) return _ClipColor;
                }
                #endif

                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 albedo = baseMap * _BaseColor;

                half rawCurv = SAMPLE_TEXTURE2D(_CurvatureMap, sampler_CurvatureMap, IN.uv).r;
                half deltaCurv = (rawCurv - 0.5) * 2.0;
                half convex = saturate(deltaCurv) * _CurvatureStrength;
                half concave = saturate(-deltaCurv) * _CurvatureCavityStrength;
                albedo.rgb = albedo.rgb * (1.0 - concave * 0.45) + (convex * 0.45 * (1.0 - albedo.rgb * 0.3));

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv), _BumpScale);
                #if defined(_PC_DETAIL)
                    half3 detailTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, IN.uv * _DetailTiling), _DetailNormalScale);
                    normalTS = normalize(half3(normalTS.xy + detailTS.xy, normalTS.z * detailTS.z));
                #endif
                half3x3 TBN = half3x3(IN.tangentWS, IN.bitangentWS, IN.normalWS);
                half3 normalWS = TransformTangentToWorld(normalTS, TBN);
                normalWS = NormalizeNormalPerPixel(normalWS);

                half metallic = _Metallic;
                half smoothness = _Smoothness;
                #if defined(_PC_METALLICMAP)
                    half4 mg = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, IN.uv);
                    metallic = mg.r * _Metallic;
                    smoothness = mg.a * _Smoothness;
                #endif
                #if defined(_PC_ROUGHNESSMAP)
                    half rough = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, IN.uv).r;
                    smoothness = lerp(smoothness, saturate(1.0 - rough * _RoughnessMapScale), _RoughnessMapStrength);
                #endif

                half rawAO = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, IN.uv).g;
                half occlusion = lerp(1.0, rawAO, _OcclusionStrength);

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.positionCS = IN.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord = 0;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.shadowMask = half4(1.0, 1.0, 1.0, 1.0);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.metallic = metallic;
                surfaceData.smoothness = smoothness;
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = occlusion;
                surfaceData.alpha = 1.0;
                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 1;
                #if defined(_CLEARCOAT)
                    surfaceData.clearCoatMask = _ClearCoat;
                    surfaceData.clearCoatSmoothness = _ClearCoatSmoothness;
                #endif
                #if defined(_PC_EMISSION)
                    surfaceData.emission = _EmissionColor.rgb * SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv).rgb;
                #endif

                half highlightAmount = saturate(_HighlightIntensity);
                surfaceData.albedo *= (1.0 - highlightAmount * 0.30);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);

                #if defined(_PC_TRANSLUCENT)
                {
                    Light ml = GetMainLight(inputData.shadowCoord);
                    half ndv = saturate(dot(normalWS, inputData.viewDirectionWS));
                    half edge = pow(1.0 - ndv, _TranslucencyPower);
                    half back = saturate(dot(inputData.viewDirectionWS, -ml.direction));
                    half wrap = saturate(dot(-normalWS, ml.direction) * 0.5 + 0.5);
                    half t = edge * (0.30 + 0.70 * back) * (0.4 + 0.6 * wrap) * _TranslucencyStrength;
                    color.rgb += _TranslucencyColor.rgb * albedo.rgb * ml.color * ml.shadowAttenuation * t;
                }
                #endif

                if (highlightAmount > 0.001)
                {
                    float ndotv = saturate(dot(normalize(normalWS), normalize(inputData.viewDirectionWS)));
                    float rim = pow(1.0 - ndotv, max(_HighlightRimPower, 0.5));
                    color.rgb += _HighlightColor.rgb * highlightAmount * (0.35 + 0.85 * rim);
                }

                color.a = 1.0;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles gles3
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #pragma multi_compile_local _ _CLIP_ENABLED
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            float4 _ClipPlane;
            float4 _GlobalClipPlane;
            float _GlobalClipEnabled;
            float4 _GlobalClipPlane2;
            float _GlobalClipEnabled2;
            float3 _LightDirection;
            float3 _LightPosition;

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target
            {
                if (_GlobalClipEnabled > 0.5)
                {
                    if (dot(IN.positionWS, _GlobalClipPlane.xyz) + _GlobalClipPlane.w < 0.0) discard;
                }
                if (_GlobalClipEnabled2 > 0.5)
                {
                    if (dot(IN.positionWS, _GlobalClipPlane2.xyz) + _GlobalClipPlane2.w < 0.0) discard;
                }
                #if defined(_CLIP_ENABLED)
                if (_GlobalClipEnabled < 0.5)
                {
                    if (dot(IN.positionWS, _ClipPlane.xyz) + _ClipPlane.w < 0.0) discard;
                }
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles gles3
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #pragma multi_compile_local _ _CLIP_ENABLED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            float4 _ClipPlane;
            float4 _GlobalClipPlane;
            float _GlobalClipEnabled;
            float4 _GlobalClipPlane2;
            float _GlobalClipEnabled2;

            Varyings DepthVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            half4 DepthFrag(Varyings IN) : SV_Target
            {
                if (_GlobalClipEnabled > 0.5)
                {
                    if (dot(IN.positionWS, _GlobalClipPlane.xyz) + _GlobalClipPlane.w < 0.0) discard;
                }
                if (_GlobalClipEnabled2 > 0.5)
                {
                    if (dot(IN.positionWS, _GlobalClipPlane2.xyz) + _GlobalClipPlane2.w < 0.0) discard;
                }
                #if defined(_CLIP_ENABLED)
                if (_GlobalClipEnabled < 0.5)
                {
                    if (dot(IN.positionWS, _ClipPlane.xyz) + _ClipPlane.w < 0.0) discard;
                }
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles gles3
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag

            #pragma multi_compile_local _ _CLIP_ENABLED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            float4 _ClipPlane;
            float4 _GlobalClipPlane;
            float _GlobalClipEnabled;
            float4 _GlobalClipPlane2;
            float _GlobalClipEnabled2;

            Varyings DepthNormalsVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 DepthNormalsFrag(Varyings IN) : SV_Target
            {
                if (_GlobalClipEnabled > 0.5)
                {
                    if (dot(IN.positionWS, _GlobalClipPlane.xyz) + _GlobalClipPlane.w < 0.0) discard;
                }
                if (_GlobalClipEnabled2 > 0.5)
                {
                    if (dot(IN.positionWS, _GlobalClipPlane2.xyz) + _GlobalClipPlane2.w < 0.0) discard;
                }
                #if defined(_CLIP_ENABLED)
                if (_GlobalClipEnabled < 0.5)
                {
                    if (dot(IN.positionWS, _ClipPlane.xyz) + _ClipPlane.w < 0.0) discard;
                }
                #endif
                return half4(NormalizeNormalPerPixel(IN.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
