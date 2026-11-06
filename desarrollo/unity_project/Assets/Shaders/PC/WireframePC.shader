// Fase 3B (G11), SOLO PC. Alambre limpio: grosor en pixeles con antialiasing, distancias a las aristas en
// espacio de pantalla (noperspective), atenuacion en zonas densas (triangulos de pocos pixeles no forman una
// mancha solida) y mezcla aditiva (no depende del orden de dibujo). Mismo recorte global CUT que el original.
Shader "TwinSight/WireframePC"
{
    Properties
    {
        _WireColor("Wire Color", Color) = (0.30, 0.80, 1.0, 1.0)
        _WirePixels("Wire thickness (px)", Range(0.5, 4)) = 0.9
        _AA("Edge softness (px)", Range(0.25, 2)) = 0.7
        _Intensity("Intensity (HDR)", Range(0.2, 4)) = 0.42
        _DenseMin("Visibility in dense areas", Range(0, 1)) = 0.12
        _DenseLow("Dense triangle size (px)", Float) = 2.5
        _DenseHigh("Open triangle size (px)", Float) = 11
        _FadeStart("Distance fade start (m)", Float) = 14
        _FadeLength("Distance fade length (m)", Float) = 30
        _FillAlpha("Faint fill", Range(0, 0.2)) = 0.008
    }

    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.universal" }
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "WireframePC"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma exclude_renderers gles gles3 glcore
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct v2g { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            struct g2f
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                noperspective float3 dist : TEXCOORD1;
                nointerpolation float triSize : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _WireColor;
                half _WirePixels;
                half _AA;
                half _Intensity;
                half _DenseMin;
                float _DenseLow;
                float _DenseHigh;
                float _FadeStart;
                float _FadeLength;
                half _FillAlpha;
            CBUFFER_END

            float4 _GlobalClipPlane;
            float _GlobalClipEnabled;
            float4 _GlobalClipPlane2;
            float _GlobalClipEnabled2;

            v2g vert(Attributes IN)
            {
                v2g o;
                o.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return o;
            }

            [maxvertexcount(3)]
            void geom(triangle v2g IN[3], inout TriangleStream<g2f> tri)
            {
                float2 halfPx = _ScreenParams.xy * 0.5;
                float2 p0 = IN[0].positionCS.xy / IN[0].positionCS.w * halfPx;
                float2 p1 = IN[1].positionCS.xy / IN[1].positionCS.w * halfPx;
                float2 p2 = IN[2].positionCS.xy / IN[2].positionCS.w * halfPx;
                float2 e0 = p2 - p1, e1 = p2 - p0, e2 = p1 - p0;
                float area = abs(e1.x * e2.y - e1.y * e2.x);
                float l0 = max(length(e0), 1e-4), l1 = max(length(e1), 1e-4), l2 = max(length(e2), 1e-4);
                float size = area / min(l0, min(l1, l2));   // altura mayor del triangulo, en pixeles

                g2f o;
                o.triSize = size;
                o.positionCS = IN[0].positionCS; o.positionWS = IN[0].positionWS; o.dist = float3(area / l0, 0, 0); tri.Append(o);
                o.positionCS = IN[1].positionCS; o.positionWS = IN[1].positionWS; o.dist = float3(0, area / l1, 0); tri.Append(o);
                o.positionCS = IN[2].positionCS; o.positionWS = IN[2].positionWS; o.dist = float3(0, 0, area / l2); tri.Append(o);
            }

            half4 frag(g2f IN) : SV_Target
            {
                if (_GlobalClipEnabled > 0.5 && dot(IN.positionWS, _GlobalClipPlane.xyz) + _GlobalClipPlane.w < 0) discard;
                if (_GlobalClipEnabled2 > 0.5 && dot(IN.positionWS, _GlobalClipPlane2.xyz) + _GlobalClipPlane2.w < 0) discard;

                float d = min(IN.dist.x, min(IN.dist.y, IN.dist.z));
                float halfW = _WirePixels * 0.5;
                float wire = 1.0 - smoothstep(halfW - _AA * 0.5, halfW + _AA, d);

                float dense = lerp(_DenseMin, 1.0, smoothstep(_DenseLow, _DenseHigh, IN.triSize));
                float dist = length(IN.positionWS - _WorldSpaceCameraPos);
                float fade = 1.0 - 0.65 * saturate((dist - _FadeStart) / max(_FadeLength, 1.0));

                float a = saturate(wire * dense * fade + _FillAlpha);
                return half4(_WireColor.rgb * _Intensity, a);
            }
            ENDHLSL
        }
    }
}
