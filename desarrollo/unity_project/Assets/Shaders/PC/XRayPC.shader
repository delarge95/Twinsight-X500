// Fase 3B (G11), SOLO PC. Rayos X con mezcla aditiva: el resultado no depende del orden de las piezas
// transparentes (sin capas que tapan a otras) y el borde brilla en HDR (bloom del perfil de PC).
// Mismas propiedades y mismo recorte global CUT que WebGL/XRay.
Shader "TwinSight/XRayPC"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (0.3, 0.7, 1.0, 0.3)
        _FresnelPower("Fresnel Power", Range(0.1, 6)) = 2.2
        _FresnelIntensity("Fresnel Intensity", Range(0, 2)) = 1.0
        _RimColor("Rim Color", Color) = (0.5, 0.8, 1.0, 1.0)
        _InnerAlpha("Inner Alpha", Range(0, 1)) = 0.1
        _OuterAlpha("Outer Alpha", Range(0, 1)) = 0.8
        _Intensity("Intensity (HDR)", Range(0.1, 3)) = 0.75
        _BehindColor("Behind Color", Color) = (0.2, 0.5, 0.8, 0.5)
        _BehindIntensity("Behind Intensity", Range(0, 1)) = 0.5
    }

    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.universal" }
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 normalWS : TEXCOORD0;
            float3 viewDirWS : TEXCOORD1;
            float3 positionWS : TEXCOORD2;
        };
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _FresnelPower;
            half _FresnelIntensity;
            half4 _RimColor;
            half _InnerAlpha;
            half _OuterAlpha;
            half _Intensity;
            half4 _BehindColor;
            half _BehindIntensity;
        CBUFFER_END
        float4 _GlobalClipPlane;
        float _GlobalClipEnabled;
        float4 _GlobalClipPlane2;
        float _GlobalClipEnabled2;

        Varyings vert(Attributes IN)
        {
            Varyings o;
            o.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
            o.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
            o.normalWS = TransformObjectToWorldNormal(IN.normalOS);
            o.viewDirWS = GetWorldSpaceNormalizeViewDir(o.positionWS);
            return o;
        }

        void ClipGlobal(float3 positionWS)
        {
            if (_GlobalClipEnabled > 0.5 && dot(positionWS, _GlobalClipPlane.xyz) + _GlobalClipPlane.w < 0) discard;
            if (_GlobalClipEnabled2 > 0.5 && dot(positionWS, _GlobalClipPlane2.xyz) + _GlobalClipPlane2.w < 0) discard;
        }
        ENDHLSL

        // Piezas detras de otras (Z-fail): solo un contorno tenue
        Pass
        {
            Name "XRay Behind"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest Greater
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragBehind
            half4 fragBehind(Varyings IN) : SV_Target
            {
                ClipGlobal(IN.positionWS);
                half fres = pow(1.0 - saturate(dot(normalize(IN.normalWS), normalize(IN.viewDirWS))), _FresnelPower);
                return half4(_BehindColor.rgb * _Intensity, _BehindColor.a * _BehindIntensity * fres * 0.8);
            }
            ENDHLSL
        }

        // Piezas visibles: relleno tenue + borde fresnel brillante
        Pass
        {
            Name "XRay Front"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragFront
            half4 fragFront(Varyings IN) : SV_Target
            {
                ClipGlobal(IN.positionWS);
                half fres = saturate(pow(1.0 - saturate(dot(normalize(IN.normalWS), normalize(IN.viewDirWS))), _FresnelPower) * _FresnelIntensity);
                half3 col = lerp(_BaseColor.rgb, _RimColor.rgb, fres) * _Intensity;
                half a = lerp(_InnerAlpha, _OuterAlpha, fres);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
