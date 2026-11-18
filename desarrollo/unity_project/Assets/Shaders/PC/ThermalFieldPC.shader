// MVP P3 (SOLO PC, nunca en WebGL). Campo termico cuantitativo sobre el dron: la MISMA formula que src/shared/thermal.ts (temperatureAt) y que el fragment shader de la web
// (src/web/scene/materials.ts createThermalMaterial):
//     T(p) = T_amb + max_i( (T_i - T_amb) * exp(-|p - c_i|^2 / r_i^2) )
// c_i son los centros de los componentes en el espacio LOCAL de la raiz del dron (piezas en reposo), r_i su dispersion, i = motor 1..4, bateria, controladora (6 fuentes).
// Paleta inferno (6 paradas como en GLSL), isotermas cada 5 C, sombreado ligero y un halo tenue para que la silueta se lea. Los valores los fija ThermalFieldPC.cs cada
// fotograma (temperaturas de la telemetria, ambiente del hello, rango automatico suavizado). Con _TF_NoData > 0.5 (telemetria sin datos) pinta un gris neutro: no se
// inventan temperaturas. Mismo recorte global CUT que el resto de shaders PC.
Shader "TwinSight/ThermalFieldPC"
{
    Properties
    {
        [HideInInspector] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.universal" }
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ThermalField"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            // Values the controller sets once per frame on the shared material (outside the CBUFFER: the arrays are not per object).
            float4x4 _TF_RootInv;      // world -> local space of the drone root
            float4 _TF_Src[6];         // xyz = centre (root-local), w = radius (root-local units)
            float _TF_Temps[6];        // C
            float _TF_Ambient;         // C
            float4 _TF_Range;          // x = min, y = max (C), z = palette floor
            float _TF_NoData;
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
                return o;
            }

            // inferno (matplotlib), the 6 stops of thermal.ts
            float3 Inferno(float x)
            {
                float t = saturate(x) * 5.0;
                if (t < 1.0) return lerp(float3(0.001, 0.0, 0.014), float3(0.258, 0.039, 0.406), t);
                if (t < 2.0) return lerp(float3(0.258, 0.039, 0.406), float3(0.578, 0.148, 0.404), t - 1.0);
                if (t < 3.0) return lerp(float3(0.578, 0.148, 0.404), float3(0.865, 0.317, 0.226), t - 2.0);
                if (t < 4.0) return lerp(float3(0.865, 0.317, 0.226), float3(0.988, 0.645, 0.04), t - 3.0);
                if (t < 5.0) return lerp(float3(0.988, 0.645, 0.04), float3(0.988, 0.998, 0.645), t - 4.0);
                return float3(0.988, 0.998, 0.645);
            }

            half4 frag(Varyings IN, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                if (_GlobalClipEnabled > 0.5 && dot(IN.positionWS, _GlobalClipPlane.xyz) + _GlobalClipPlane.w < 0) discard;
                if (_GlobalClipEnabled2 > 0.5 && dot(IN.positionWS, _GlobalClipPlane2.xyz) + _GlobalClipPlane2.w < 0) discard;

                float3 n = normalize(IN.normalWS);
                if (!frontFace) n = -n;
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float shade = 0.55 + 0.45 * max(dot(n, normalize(float3(0.4, 1.0, 0.3))), 0.0);
                float rim = pow(1.0 - abs(dot(n, viewDir)), 3.0);

                float3 col;
                if (_TF_NoData > 0.5)
                {
                    // telemetry source without data: a neutral, lit grey-blue (web createNoDataThermalMaterial), never a colour scale
                    col = float3(0.545, 0.608, 0.690) * (0.35 + 0.65 * max(dot(n, normalize(float3(0.4, 1.0, 0.3))), 0.0));
                }
                else
                {
                    float3 p = mul(_TF_RootInv, float4(IN.positionWS, 1.0)).xyz;
                    // Same formula as temperatureAt() in src/shared/thermal.ts.
                    float best = 0.0;
                    [unroll] for (int i = 0; i < 6; i++)
                    {
                        float3 d = p - _TF_Src[i].xyz;
                        float g = exp(-dot(d, d) / (_TF_Src[i].w * _TF_Src[i].w));
                        best = max(best, (_TF_Temps[i] - _TF_Ambient) * g);
                    }
                    float T = _TF_Ambient + best;
                    float span = max(_TF_Range.y - _TF_Range.x, 1e-3);
                    col = Inferno(_TF_Range.z + (1.0 - _TF_Range.z) * saturate((T - _TF_Range.x) / span));
                    // isotherms every 5 C
                    float iso = abs(frac(T / 5.0 + 0.5) - 0.5) / max(fwidth(T / 5.0), 1e-5);
                    col = lerp(col * 0.55, col, smoothstep(0.0, 1.2, iso));
                    col = col * shade + float3(0.18, 0.22, 0.30) * rim + 0.03;
                }
                #ifndef UNITY_COLORSPACE_GAMMA
                col = SRGBToLinear(saturate(col));
                #endif
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
