// PLAN_PC 41: plano visual del CORTE. Casi transparente: un tinte muy tenue y un contorno fino (el borde del cuadrado), sin palabras clave
// (el material que se creaba en tiempo de ejecucion con "Universal Render Pipeline/Unlit" perdia la variante transparente en la build y salia OPACO:
// un plano azul solido que tapaba el dron). Cull Off, ZWrite Off, mezcla alfa normal.
Shader "WebGL/CutPlane"
{
    Properties
    {
        _BaseColor("Color", Color) = (0.27, 0.69, 1, 1)
        _Fill("Fill alpha", Range(0, 1)) = 0.012
        _Edge("Edge alpha", Range(0, 1)) = 0.55
        _EdgeWidth("Edge width (of the half side)", Range(0, 0.2)) = 0.012
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "CutPlane"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Fill;
                half _Edge;
                half _EdgeWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 d = abs(IN.uv - 0.5) * 2.0;           // 0 at the centre, 1 at the edge
                float edge = step(1.0 - _EdgeWidth, max(d.x, d.y));
                half a = lerp(_Fill, _Edge, edge);
                return half4(_BaseColor.rgb, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
