// PLAN_PC 28.2, SOLO PC. Suelo del mundo para las sesiones de telemetria (simulador, repeticion, SITL): UN triangulo de pantalla completa en el plano
// lejano que, por pixel, corta el rayo de la camara con el plano y = _GroundY. Por eso el suelo es infinito, no tiene geometria que recortar a ninguna
// altura ni angulo (0 a 60 m y mas, de rasante a cenital) y no depende del plano lejano de la camara.
//  - Rejilla en ESPACIO DEL MUNDO: 1 m (fina), 10 m (media) y 100 m (mayor), con antialiasing por derivadas (fwidth) y filtrado: las lineas que ya
//    no caben en unos pocos pixeles se difuminan hacia su cobertura media en vez de dar muaré.
//  - Desvanecimiento con la distancia hasta el color del horizonte (_HorizonColor) y, por encima del horizonte, una bruma fina del mismo color mas
//    una linea de horizonte limpia y marcas de rumbo cada 30 grados (el norte, +Z, mas alta).
//  - Sombra blanda del dron (mancha) y anillo del punto del suelo bajo el dron; vista desde debajo del suelo: velo tenue.
// Se dibuja despues del cielo (cola Transparent-100), sin escribir profundidad y con ZTest LEqual contra el plano lejano: el dron y el resto de la
// escena lo tapan. Colores: tokens de design/tokens.json (los pone TelemetryGroundReference); no hay colores propios.
Shader "TwinSight/GroundGridPC"
{
    Properties
    {
        _GroundColor("Ground Color", Color) = (0.11, 0.14, 0.19, 1)
        _HorizonColor("Horizon Color", Color) = (0.25, 0.31, 0.40, 1)
        _LineColor("Line Color", Color) = (0.27, 0.69, 1.0, 1)
        _MajorColor("Major Line Color", Color) = (0.75, 0.92, 1.0, 1)
        _Meter("Units per metre", Float) = 16.13
        _GroundY("Ground height (world)", Float) = 0
        _FadeStart("Fade start (m)", Float) = 90
        _FadeEnd("Fade end (m)", Float) = 700
        _DroneShadow("Drone xyz (world), w = radius (units)", Vector) = (0, 0, 0, 16)
        _ShadowStrength("Shadow strength", Range(0, 1)) = 0.55
        _Quality("Quality (0 low, 1 normal)", Float) = 1
    }

    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.universal" }
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-100" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Ground"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GroundColor;
                half4 _HorizonColor;
                half4 _LineColor;
                half4 _MajorColor;
                float _Meter;
                float _GroundY;
                float _FadeStart;
                float _FadeEnd;
                float4 _DroneShadow;
                half _ShadowStrength;
                half _Quality;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 rayWS : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                float2 ndc = IN.positionOS.xy; // the quad corners are (+-1, +-1, 0)
                o.positionCS = float4(ndc, UNITY_RAW_FAR_CLIP_VALUE, 1.0);
                float4 w = mul(UNITY_MATRIX_I_VP, float4(ndc, UNITY_RAW_FAR_CLIP_VALUE, 1.0));
                o.rayWS = w.xyz / w.w - _WorldSpaceCameraPos;
                return o;
            }

            // coverage (0..1) of the lines of a grid with period `period` (metres) at the metre-space position p, with the pixel footprint fw (metres)
            // widthPx: line width in pixels. Lines that no longer fit (cell < ~4 px) fade to their average coverage: no moire.
            float GridCoverage(float2 p, float2 fw, float period, float widthPx)
            {
                float2 q = p / period;
                float2 f = max(fw / period, 1e-6);          // pixel footprint in cells
                float2 d = abs(frac(q + 0.5) - 0.5);        // distance to the nearest line, in cells
                float2 half_w = f * (0.5 * widthPx);
                float2 aa = saturate((half_w + f * 0.5 - d) / f); // 1 on the line, 0 one pixel away
                float line_ = max(aa.x, aa.y);
                float avg = saturate(widthPx * (f.x + f.y));      // average coverage of the two line families when the cell is tiny
                float fade = smoothstep(0.12, 0.32, max(f.x, f.y));
                return lerp(line_, avg, fade) * (1.0 - smoothstep(0.30, 0.55, max(f.x, f.y)));
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.rayWS);
                float h = _WorldSpaceCameraPos.y - _GroundY;       // camera height over the ground (world units)
                float m = max(_Meter, 1e-3);

                // ------------------------------------------------ ray vs the plane
                float t = -h / (abs(dir.y) < 1e-5 ? (dir.y < 0 ? -1e-5 : 1e-5) : dir.y);
                bool hit = t > 0.0;
                bool below = h < 0.0;                              // the camera is under the ground (orbit angle below the horizon)

                // derivatives BEFORE any divergent branch (they are undefined inside one)
                float distW = clamp(abs(t), 0.0, 1e7);
                float3 P = _WorldSpaceCameraPos + dir * distW;
                float2 p = (P.xz) / m;                                              // metres
                float2 fw = max(fwidth(p), 1e-6);
                float fwEl = max(fwidth(dir.y), 1e-5);
                float2 hdir = dir.xz;
                float2 fwHdir = fwidth(hdir);

                if (!hit)
                {
                    // sky side: a fine haze of the horizon colour above the horizon, the horizon line and the heading ticks
                    float el = below ? -dir.y : dir.y;             // elevation over the horizon (0 on the line)
                    if (el < 0.0) return half4(0, 0, 0, 0);
                    float haze = exp(-el / 0.05) * 0.9;
                    float lineA = 1.0 - smoothstep(0.0, fwEl * 1.5, el);
                    half3 rgb = lerp(_HorizonColor.rgb, _MajorColor.rgb, lineA * 0.55);
                    float a = max(haze, lineA * 0.85);
                    if (_Quality > 0.5)
                    {
                        // heading ticks every 30 degrees over the horizon, the north (+Z) taller and in the highlight colour
                        float hl = max(length(hdir), 1e-4);
                        float az = atan2(hdir.x, hdir.y);                           // 0 = north (+Z), +90 = east (+X)
                        float step_ = 0.5235988;                                    // 30 degrees
                        float u = az / step_;
                        float dAz = max(length(fwHdir) / hl, 1e-5) / step_;
                        float dist = abs(frac(u + 0.5) - 0.5);
                        float tick = 1.0 - smoothstep(dAz * 0.6, dAz * 1.6, dist);
                        float isNorth = 1.0 - smoothstep(0.0, 0.6, abs(az) / step_);
                        float reach = lerp(0.014, 0.030, isNorth);
                        tick *= smoothstep(reach, reach * 0.4, el) * smoothstep(fwEl * 0.8, fwEl * 2.0, el);
                        a = max(a, tick * 0.8);
                        rgb = lerp(rgb, _MajorColor.rgb, saturate(tick) * (0.4 + 0.6 * isNorth));
                    }
                    return half4(rgb, a);
                }

                // ------------------------------------------------ ground side
                float distM = distW / m;

                float fog = smoothstep(_FadeStart, _FadeEnd, distM);
                float rim = fog;

                half3 rgb = _GroundColor.rgb;
                float cov1 = 0, cov10 = 0, cov100 = 0;
                if (_Quality > 0.5) cov1 = GridCoverage(p, fw, 1.0, 1.0);
                cov10 = GridCoverage(p, fw, 10.0, 1.3);
                cov100 = GridCoverage(p, fw, 100.0, 1.8);
                // lines: fine < medium < major; the fine ones fade out sooner with the distance
                rgb = lerp(rgb, _LineColor.rgb, cov1 * 0.20 * (1.0 - saturate(distM / 160.0)));
                rgb = lerp(rgb, _LineColor.rgb, cov10 * 0.42);
                rgb = lerp(rgb, _MajorColor.rgb, cov100 * 0.80);

                // the soft shadow of the drone (a blob that widens and fades with the altitude) and the ring of its ground point
                float alt = max(_DroneShadow.y - _GroundY, 0.0) / m;                // metres
                float2 dd = P.xz - _DroneShadow.xz;
                float r = length(dd);
                if (_Quality > 0.5 && _ShadowStrength > 0.001)
                {
                    float R = _DroneShadow.w * (1.0 + alt / 22.0);
                    float k = _ShadowStrength * (1.0 / (1.0 + alt / 28.0));
                    float blob = (1.0 - smoothstep(R * 0.15, R, r)) * k;
                    rgb *= (1.0 - blob);
                    float ringR = _DroneShadow.w * 1.9;
                    float fwR = max(fwidth(r), 1e-4);
                    float ring = (1.0 - smoothstep(fwR * 0.7, fwR * 1.7, abs(r - ringR))) * smoothstep(0.4, 1.2, alt);
                    rgb = lerp(rgb, _MajorColor.rgb, ring * 0.9);
                }

                // distance fade to the horizon colour
                rgb = lerp(rgb, _HorizonColor.rgb, rim);
                float alpha = 1.0;
                if (below) alpha = 0.38;                                            // seen from under the ground: a faint veil, not a wall
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
