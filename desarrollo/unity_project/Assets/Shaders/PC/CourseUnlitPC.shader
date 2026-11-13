// PLAN_PC 28.3, SOLO PC. Sombreado plano de las piezas del escenario de pruebas del simulador (aros, conos, muro, zonas, helipuerto):
// color de vertice x _Tint (el color por elemento: pendiente / siguiente / hecho / choque, lo pone un MaterialPropertyBlock) y una luz fija que da
// el aspecto de caras planas de baja poligonizacion. Sin texturas, sin sombras, sin sombreado de URP: barato. Dos materiales del mismo shader:
// solido (ZWrite) y translucido (zonas). Colores: tokens de design/tokens.json (los pone TelemetryTestCourse).
Shader "TwinSight/CourseUnlitPC"
{
    Properties
    {
        _Tint("Tint", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 0
        [Toggle] _ZWrite("ZWrite", Float) = 1
        _Shade("Face shading amount", Range(0, 1)) = 0.4
    }

    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.universal" }
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Course"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Shade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float3 normalWS : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                o.color = IN.color;
                return o;
            }

            half4 frag(Varyings IN, bool facing : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(IN.normalWS) * (facing ? 1.0 : -1.0);
                float l = saturate(dot(n, normalize(float3(0.35, 0.8, 0.45))));
                half shade = (half)(1.0 - _Shade + _Shade * (0.35 + 0.65 * l));
                half4 c = IN.color * _Tint;
                c.rgb *= shade;
                return c;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
