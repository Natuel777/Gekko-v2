// Agua estilizada para rios hechos con SplineRiver (Assets/_RiverSandbox/Runtime/SplineRiver.cs).
//
// Pensado para la malla que genera SplineRiver:
//   uv0 = (offset lateral en metros desde el centro, distancia recorrida en metros)
//   uv1 = (lateral normalizado 0..1, multiplicador de velocidad de corriente)
//   tangente = direccion de la corriente (a lo largo de la spline)
// Las ondas interactivas (_RipplePos/_RippleParams) y la velocidad (_RiverFlowSpeed) las empuja
// SplineRiver por MaterialPropertyBlock cada frame; no se tocan desde el material.
//
// Requiere en el URP Asset: Depth Texture (color por profundidad, espuma, causticas) y
// Opaque Texture (refraccion). Si Opaque Texture esta apagado, destildar "Refraction".
Shader "Gekko/River Stylized"
{
    Properties
    {
        [Header(Color)]
        _ShallowColor("Color poco profundo (A = opacidad)", Color) = (0.36, 0.93, 0.86, 0.12)
        _DeepColor("Color profundo (A = opacidad)", Color) = (0.02, 0.36, 0.66, 0.9)
        _DepthMaxDistance("Distancia de profundidad maxima", Float) = 2.0
        _ShallowTint("Tinte sobre el fondo", Range(0, 1)) = 0.5
        _ReflectionColor("Color de reflejo (fresnel)", Color) = (0.78, 0.94, 1, 1)
        _FresnelPower("Fresnel Power", Range(0.5, 8)) = 4
        _FresnelStrength("Fresnel Strength", Range(0, 1)) = 0.3

        [Header(Superficie y corriente)]
        _NoiseScale("Escala del ruido", Float) = 0.55
        _StreakStretch("Estirado a lo largo de la corriente", Range(0.1, 1)) = 0.35
        _SurfaceFoamCutoff("Corte de vetas blancas", Range(0, 1)) = 0.9
        _SurfaceFoamStrength("Intensidad de vetas blancas", Range(0, 1)) = 0.45
        _FlowPeriod("Periodo de flujo (s)", Float) = 3
        _NormalStrength("Intensidad de normales", Range(0, 2)) = 0.7
        _WaveAmplitude("Amplitud de ola (vertices)", Float) = 0.025
        _WaveFrequency("Frecuencia de ola", Float) = 0.8

        [Header(Refraccion)]
        [Toggle(_REFRACTION_ON)] _Refraction("Refraccion (usa Opaque Texture)", Float) = 1
        _RefractionStrength("Intensidad de refraccion", Range(0, 0.1)) = 0.02

        [Header(Espuma de orilla)]
        _FoamColor("Color de espuma", Color) = (1, 1, 1, 1)
        _FoamDistance("Ancho de espuma", Float) = 0.3
        _FoamNoiseScale("Escala del ruido de espuma", Float) = 2.2
        _FoamLines("Lineas de orilla", Range(0, 1)) = 0.55
        _FoamLineSpacing("Separacion de lineas", Float) = 0.45
        _FoamLineSpeed("Velocidad de lineas", Float) = 0.35

        [Header(Causticas)]
        _CausticsColor("Color de causticas", Color) = (1, 1, 0.95, 1)
        _CausticsStrength("Intensidad de causticas", Range(0, 2)) = 0.7
        _CausticsScale("Escala de causticas", Float) = 0.8
        _CausticsSpeed("Velocidad de causticas", Float) = 0.6
        _CausticsDepthFade("Profundidad donde desaparecen", Float) = 2.2

        [Header(Brillos)]
        _SpecularSize("Tamano del brillo", Range(0.9, 1)) = 0.985
        _SpecularStrength("Intensidad del brillo", Range(0, 2)) = 0.9
        _SparkleScale("Escala de destellos", Float) = 5
        _SparkleThreshold("Umbral de destellos", Range(0.5, 1)) = 0.96
        _SparkleStrength("Intensidad de destellos", Range(0, 2)) = 0.7

        [Header(Ondas interactivas)]
        _RippleFoam("Espuma de ondas", Range(0, 1)) = 0.9
        _RippleNormal("Distorsion de ondas", Range(0, 3)) = 1.2

        [Header(Cascada (tramos en pendiente))]
        _WaterfallDepthTone("Tono de cascada (0 = como agua poco profunda, 1 = como agua profunda)", Range(0, 1)) = 0.3
        _WaterfallStreakScale("Escala de vetas", Float) = 1.3
        _WaterfallStreakCutoff("Corte de vetas blancas", Range(0, 1)) = 0.66
        _WaterfallEdgeFoam("Espuma en los bordes", Range(0, 0.5)) = 0.16
        _WaterfallLipFoam("Espuma arriba y al pie", Range(0, 1)) = 1

        [Header(Modo estanque (mallas comunes, sin SplineRiver))]
        [Toggle(_POOL_MODE)] _PoolMode("Modo estanque", Float) = 0
        _PoolDepth("Profundidad simulada (m)", Float) = 1.2
        _PoolFlow("Corriente del estanque (X, Z en m/s)", Vector) = (0.05, 0.2, 0, 0)

        [Header(Purificacion (zonas corruptas del PurifyManager))]
        [HDR] _PurifyCorruptTint("Tinte corrupto", Color) = (0.8, 0.65, 0.95, 1)
        _PurifyCorruptSaturation("Saturacion corrupta", Range(0, 1)) = 0.15
        _PurifyCorruptDarken("Brillo corrupto", Range(0, 1.5)) = 0.35
        _PurifyCorruptMurk("Agua corrupta turbia (no deja ver el fondo)", Range(0, 1)) = 0.6
        [HDR] _PurifyEdgeColor("Color del borde de la onda", Color) = (1, 0.8, 0.3, 1)
        _PurifyEdgeIntensity("Intensidad del borde de la onda", Float) = 2
        _PurifyNoiseScale("Escala del ruido de la onda", Float) = 0.3
        _PurifyNoiseAmount("Irregularidad del borde (m)", Float) = 2.5

        [Header(Iluminacion)]
        _ShadowStrength("Intensidad de sombra", Range(0, 1)) = 0.35
        _EdgeFade("Suavizado del borde", Float) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "RiverForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fog
            #pragma shader_feature_local _REFRACTION_ON
            #pragma shader_feature_local _POOL_MODE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            // Sistema de purificacion del proyecto: mismas globales (las sube PurifyManager) y mismo look que el resto.
            #include "Assets/_Gekko/Purification/Shaders/PurifyLook.hlsl"

            #define RIVER_MAX_RIPPLES 16

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthMaxDistance;
                half _ShallowTint;
                half4 _ReflectionColor;
                half _FresnelPower;
                half _FresnelStrength;

                float _NoiseScale;
                float _StreakStretch;
                half _SurfaceFoamCutoff;
                half _SurfaceFoamStrength;
                float _FlowPeriod;
                half _NormalStrength;
                float _WaveAmplitude;
                float _WaveFrequency;

                half _RefractionStrength;

                half4 _FoamColor;
                float _FoamDistance;
                float _FoamNoiseScale;
                half _FoamLines;
                float _FoamLineSpacing;
                float _FoamLineSpeed;

                half4 _CausticsColor;
                half _CausticsStrength;
                float _CausticsScale;
                float _CausticsSpeed;
                float _CausticsDepthFade;

                half _SpecularSize;
                half _SpecularStrength;
                float _SparkleScale;
                half _SparkleThreshold;
                half _SparkleStrength;

                half _RippleFoam;
                half _RippleNormal;

                half _WaterfallDepthTone;
                float _WaterfallStreakScale;
                half _WaterfallStreakCutoff;
                half _WaterfallEdgeFoam;
                half _WaterfallLipFoam;

                float _PoolDepth;
                float4 _PoolFlow;

                half4 _PurifyCorruptTint;
                half _PurifyCorruptSaturation;
                half _PurifyCorruptDarken;
                half _PurifyCorruptMurk;
                half4 _PurifyEdgeColor;
                half _PurifyEdgeIntensity;
                float _PurifyNoiseScale;
                float _PurifyNoiseAmount;

                half _ShadowStrength;
                float _EdgeFade;
            CBUFFER_END

            // Version corrupta de un color del agua (mismo PurifyCorruptColor que piso, hojas y M_Water). mask: 1 sano, 0 corrupto.
            half3 PurifyWater(half3 c, float mask)
            {
                return lerp(PurifyCorruptColor(c, _PurifyCorruptTint.rgb, _PurifyCorruptSaturation, _PurifyCorruptDarken), c, saturate(mask));
            }

            // Fuera del CBUFFER: los escribe SplineRiver por MaterialPropertyBlock.
            float _RiverFlowSpeed;
            float _RiverLoopLength; // largo de la vuelta en metros (0 = rio abierto)
            float4 _RipplePos[RIVER_MAX_RIPPLES];    // xyz = posicion en mundo, w = edad (s)
            float4 _RippleParams[RIVER_MAX_RIPPLES]; // x = fuerza (0 = inactiva), y = velocidad, z = vida, w = ancho

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2; // x = cascada (0..1)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 flowWS : TEXCOORD2;
                float4 uv : TEXCOORD3; // xy = uv0, zw = uv1
                float eyeDepth : TEXCOORD4;
                float fogFactor : TEXCOORD5;
                float steep : TEXCOORD6;
            };

            // ---------- ruido ----------
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 Hash22(float2 p)
            {
                float3 a = frac(p.xyx * float3(123.34, 234.34, 345.65));
                a += dot(a, a + 34.45);
                return frac(float2(a.x * a.y, a.y * a.z));
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm2(float2 p)
            {
                return ValueNoise(p) * 0.65 + ValueNoise(p * 2.13 + 17.1) * 0.35;
            }

            // Distancia al punto de celda mas cercano (Voronoi F1), con celdas animadas.
            float VoronoiF1(float2 p, float time)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float md = 8.0;
                [unroll] for (int y = -1; y <= 1; y++)
                {
                    [unroll] for (int x = -1; x <= 1; x++)
                    {
                        float2 g = float2(x, y);
                        float2 o = Hash22(i + g);
                        o = 0.5 + 0.5 * sin(time + 6.2831 * o);
                        float2 r = g + o - f;
                        md = min(md, dot(r, r));
                    }
                }
                return sqrt(md);
            }

            float WrapIndex(float i, float period)
            {
                return period > 0.0 ? i - period * floor(i / period) : i;
            }

            // Value noise periodico en Y (period = cantidad de celdas; 0 = sin repetir).
            float ValueNoisePeriodicY(float2 p, float period)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float y0 = WrapIndex(i.y, period);
                float y1 = WrapIndex(i.y + 1.0, period);
                float a = Hash21(float2(i.x, y0));
                float b = Hash21(float2(i.x + 1.0, y0));
                float c = Hash21(float2(i.x, y1));
                float d = Hash21(float2(i.x + 1.0, y1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Altura de la superficie en "espacio rio" (metros lateral, metros a lo largo).
            // En un rio cerrado (_RiverLoopLength > 0) la escala a lo largo se ajusta para que entre
            // un numero entero de celdas en la vuelta y el ruido se repita: asi no hay costura en la
            // union, donde la distancia vuelve de TotalLength a 0.
            float SurfaceHeight(float2 riverUV)
            {
                float2 scale = float2(_NoiseScale, _NoiseScale * _StreakStretch);
                float period = 0.0;
                if (_RiverLoopLength > 0.0)
                {
                    period = max(1.0, round(_RiverLoopLength * scale.y));
                    scale.y = period / _RiverLoopLength;
                }

                float2 q = riverUV * scale;
                return ValueNoisePeriodicY(q, period) * 0.65
                     + ValueNoisePeriodicY(q * float2(2.13, 2.0) + 17.0, period * 2.0) * 0.35;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);

                // Ola suave; se apaga en los bordes para no despegarse del cauce.
                float edge = sin(saturate(IN.uv1.x) * PI);
                float wave = sin(positionWS.x * _WaveFrequency + _Time.y * 1.3)
                           * cos(positionWS.z * _WaveFrequency * 0.8 + _Time.y * 1.1);
                positionWS.y += wave * _WaveAmplitude * edge;

                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.flowWS = TransformObjectToWorldDir(IN.tangentOS.xyz);
                OUT.uv = float4(IN.uv0, IN.uv1);
                OUT.eyeDepth = -TransformWorldToView(positionWS).z;
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                OUT.steep = IN.uv2.x;
            #if _POOL_MODE
                // Malla comun (estanque/lago): coordenadas de mundo en metros, sin cascada.
                OUT.uv = float4(positionWS.xz, 0.5, 1.0);
                OUT.flowWS = float3(0.0, 0.0, 1.0);
                OUT.steep = 0.0;
            #endif
                return OUT;
            }

            float RawToDevice(float raw)
            {
            #if UNITY_REVERSED_Z
                return raw;
            #else
                return lerp(UNITY_NEAR_CLIP_VALUE, 1, raw);
            #endif
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 F = normalize(IN.flowWS - N * dot(IN.flowWS, N));
                float3 R = cross(N, F);
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                float time = _Time.y;

                // ---------- flujo en dos fases (evita que el patron se estire con el tiempo) ----------
                float speed = _RiverFlowSpeed * IN.uv.w;
                float period = max(_FlowPeriod, 0.1);
                float phaseT = time / period;
                float p0 = frac(phaseT);
                float p1 = frac(phaseT + 0.5);
                float wA = 1.0 - abs(1.0 - 2.0 * p0);
                float wB = 1.0 - wA;
            #if _POOL_MODE
                float2 flowOffset = _PoolFlow.xy * period;
            #else
                float2 flowOffset = float2(0.0, speed * period);
            #endif
                float2 uvA = IN.uv.xy - flowOffset * p0;
                float2 uvB = IN.uv.xy - flowOffset * p1 + float2(3.7, 5.1);

                float hA = SurfaceHeight(uvA);
                float hB = SurfaceHeight(uvB);
                float h = hA * wA + hB * wB;

                const float e = 0.2;
                float dx = ((SurfaceHeight(uvA + float2(e, 0)) - hA) * wA + (SurfaceHeight(uvB + float2(e, 0)) - hB) * wB) / e;
                float dy = ((SurfaceHeight(uvA + float2(0, e)) - hA) * wA + (SurfaceHeight(uvB + float2(0, e)) - hB) * wB) / e;
                float3 normalWS = normalize(N - (R * dx + F * dy) * _NormalStrength * 0.35);

                // ---------- ondas interactivas ----------
                float rippleAmount = 0.0;
                float2 rippleTilt = 0.0;
                [unroll] for (int i = 0; i < RIVER_MAX_RIPPLES; i++)
                {
                    float4 rp = _RipplePos[i];
                    float4 pr = _RippleParams[i];
                    float2 d2 = IN.positionWS.xz - rp.xz;
                    float d = length(d2) + 1e-4;
                    float age = rp.w;
                    float radius = age * pr.y;
                    float life = saturate(1.0 - age / max(pr.z, 1e-3));
                    float k = (d - radius) / max(pr.w, 1e-3);
                    float ring = saturate(1.0 - abs(k));
                    float k2 = (d - radius * 0.55) / max(pr.w * 0.8, 1e-3);
                    float ring2 = saturate(1.0 - abs(k2)) * 0.5;
                    float amount = (ring * ring * (3.0 - 2.0 * ring) + ring2 * ring2) * life * pr.x;
                    rippleAmount += amount;
                    rippleTilt += (d2 / d) * clamp(-k, -1.0, 1.0) * amount;
                }
                normalWS = normalize(normalWS + float3(rippleTilt.x, 0.0, rippleTilt.y) * _RippleNormal);

                // ---------- profundidad de la escena ----------
                float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                float rawDepth = RawToDevice(SampleSceneDepth(screenUV));
                float sceneEye = LinearEyeDepth(rawDepth, _ZBufferParams);
                float viewDepthDiff = max(0.0, sceneEye - IN.eyeDepth);
            #if _POOL_MODE
                // En los estanques el piso esta a ras del agua: con la profundidad real todo seria "orilla" (blanco y
                // transparente). Se usa una profundidad simulada para color, espuma y transparencia.
                viewDepthDiff = max(viewDepthDiff, _PoolDepth);
            #endif

                float2 sampleUV = screenUV;
            #if _REFRACTION_ON
                float2 distortedUV = screenUV + normalWS.xz * _RefractionStrength * saturate(viewDepthDiff);
                float distortedEye = LinearEyeDepth(RawToDevice(SampleSceneDepth(distortedUV)), _ZBufferParams);
                // Si lo distorsionado queda delante del agua (un objeto que sobresale), no distorsionar.
                sampleUV = distortedEye > IN.eyeDepth ? distortedUV : screenUV;
            #endif
                float sampleRaw = RawToDevice(SampleSceneDepth(sampleUV));
                float3 bedWS = ComputeWorldSpacePosition(sampleUV, sampleRaw, UNITY_MATRIX_I_VP);
                float waterDepth = max(0.0, IN.positionWS.y - bedWS.y);
            #if _POOL_MODE
                waterDepth = _PoolDepth;
            #endif

                // ---------- purificacion (PurifyManager / PurifyRegion del proyecto) ----------
                // mask 1 = sano, 0 = corrupto; edge = frente de la onda. Sin PurifyManager en la escena todo queda sano.
                // Se corrompe solo lo que aporta el agua (sus colores, espuma, causticas, reflejo): el fondo que se ve a
                // traves ya lo corrompe su propio shader (Path Blend, SG_PurifyLit...), si no se oscureceria dos veces.
                float purifyMask = 1.0;
                float purifyEdge = 0.0;
                if (_PurifySphereCount > 0.5)
                {
                    float purified;
                    PurifyMaskFull_float(IN.positionWS, PurifyNoise(IN.positionWS.xz, _PurifyNoiseScale), _PurifyNoiseAmount,
                                         purifyMask, purifyEdge, purified);
                }
                half3 shallowColor = PurifyWater(_ShallowColor.rgb, purifyMask);
                half3 deepColor = PurifyWater(_DeepColor.rgb, purifyMask);
                half3 foamColor = PurifyWater(_FoamColor.rgb, purifyMask);
                half3 reflectionColor = PurifyWater(_ReflectionColor.rgb, purifyMask);
                half3 causticsTint = PurifyWater(_CausticsColor.rgb, purifyMask);

                // ---------- color por profundidad ----------
                float depth01 = saturate(waterDepth / max(_DepthMaxDistance, 1e-3));
                float depthCurve = 1.0 - (1.0 - depth01) * (1.0 - depth01);
                half3 waterColor = lerp(shallowColor, deepColor, depthCurve);
                half opacity = lerp(_ShallowColor.a, _DeepColor.a, depthCurve);
                // El agua corrupta es turbia: deja ver menos el fondo.
                opacity = lerp(max(opacity, _PurifyCorruptMurk), opacity, purifyMask);

                // ---------- luz principal ----------
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half shadow = lerp(1.0 - _ShadowStrength, 1.0, mainLight.shadowAttenuation);
                half3 lightColor = lerp(half3(1, 1, 1), mainLight.color, 0.5);

                // ---------- causticas sobre el fondo ----------
                float2 cp = bedWS.xz * _CausticsScale;
                float ct = time * _CausticsSpeed;
                float v1 = VoronoiF1(cp + float2(ct * 0.25, ct * 0.15), ct * 2.0);
                float v2 = VoronoiF1(cp * 1.35 - float2(ct * 0.2, ct * 0.3) + 5.2, ct * 2.3);
                float caustics = smoothstep(0.3, 0.7, min(v1, v2));
                float causticsMask = saturate(1.0 - waterDepth / max(_CausticsDepthFade, 1e-3)) * saturate(waterDepth * 6.0);
                half3 causticsColor = caustics * causticsMask * _CausticsStrength * causticsTint * mainLight.color * lerp(0.35, 1.0, purifyMask) * mainLight.shadowAttenuation;

                half3 color;
            #if _REFRACTION_ON
                half3 sceneColor = SampleSceneColor(sampleUV);
                half3 tinted = sceneColor * lerp(half3(1, 1, 1), shallowColor * 1.3, _ShallowTint) + causticsColor;
                color = lerp(tinted, waterColor * lightColor * shadow, opacity);
            #else
                color = waterColor * lightColor * shadow + causticsColor * (1.0 - opacity);
            #endif

                // ---------- fresnel ----------
                half fresnel = pow(1.0 - saturate(dot(normalWS, V)), _FresnelPower) * _FresnelStrength;
                color = lerp(color, reflectionColor * lightColor, fresnel);

                // ---------- espuma ----------
                float foamNoise = ValueNoise(IN.positionWS.xz * _FoamNoiseScale + float2(time * 0.15, -time * 0.1));
                float edgeMask = saturate(1.0 - viewDepthDiff / max(_FoamDistance, 1e-3));
                float foamCut = 0.15 + foamNoise * 0.7;
                float shoreFoam = smoothstep(foamCut - 0.04, foamCut + 0.04, edgeMask);

                float linePhase = frac(viewDepthDiff / max(_FoamLineSpacing, 1e-3) - time * _FoamLineSpeed);
                float lines = (1.0 - smoothstep(0.08, 0.16, linePhase))
                            * saturate(1.0 - viewDepthDiff / max(_FoamLineSpacing * 2.5, 1e-3))
                            * step(0.35, foamNoise) * _FoamLines;

                // De lejos el detalle fino se vuelve "nieve": se apaga con la distancia.
                float distanceFade = saturate(1.0 - (IN.eyeDepth - 15.0) / 30.0);
                float streaks = smoothstep(_SurfaceFoamCutoff, _SurfaceFoamCutoff + 0.04, h) * _SurfaceFoamStrength * distanceFade;
                float rippleFoam = smoothstep(0.12, 0.3, rippleAmount - h * 0.15) * _RippleFoam;

                float foam = saturate(shoreFoam + lines + streaks + rippleFoam) * _FoamColor.a;
                color = lerp(color, foamColor * lightColor * shadow, foam);

                // ---------- cascada (tramos en pendiente: SplineRiver pasa la pendiente en uv2.x) ----------
                // Azul intenso con bandas claras y vetas blancas muy estiradas que caen con la corriente (las fases
                // uvA/uvB ya traen la velocidad, que en la cascada es mayor), bordes laterales blancos y espuma en
                // el labio de arriba y al pie (donde la pendiente cambia).
                float steep = saturate(IN.steep);
                if (steep > 0.001)
                {
                    float2 fallScale = float2(_WaterfallStreakScale, _WaterfallStreakScale * 0.12);
                    float fallNoise = ValueNoise(uvA * fallScale) * wA + ValueNoise(uvB * fallScale + 7.3) * wB;
                    float bands = smoothstep(0.35, 0.65, fallNoise);
                    // Mismos colores que el agua del rio (Shallow/Deep del material): la cascada no es "otra agua".
                    half3 fallBase = lerp(shallowColor, deepColor, _WaterfallDepthTone);
                    half3 fallLight = lerp(shallowColor, PurifyWater(half3(1, 1, 1), purifyMask), 0.25);
                    half3 fallColor = lerp(fallBase, fallLight, bands) * lightColor * shadow;

                    float fallStreaks = smoothstep(_WaterfallStreakCutoff, _WaterfallStreakCutoff + 0.05, fallNoise);
                    float side = abs(IN.uv.z * 2.0 - 1.0) + (fallNoise - 0.5) * 0.15;
                    float edgeFoam = smoothstep(1.0 - _WaterfallEdgeFoam, 1.0 - _WaterfallEdgeFoam * 0.4, side);
                    float lip = steep * (1.0 - steep) * 4.0;
                    lip = smoothstep(0.35, 0.85, lip + (fallNoise - 0.5) * 0.5) * _WaterfallLipFoam;
                    float fallFoam = saturate(fallStreaks + edgeFoam + lip);

                    fallColor = lerp(fallColor, foamColor * lightColor * shadow, fallFoam);
                    color = lerp(color, fallColor, steep);
                    foam = lerp(foam, fallFoam, steep);
                }

                // ---------- brillos ----------
                float3 H = normalize(mainLight.direction + V);
                float NdotH = saturate(dot(normalWS, H));
                float specular = smoothstep(_SpecularSize, lerp(_SpecularSize, 1.0, 0.25), NdotH) * _SpecularStrength;

                float s1 = ValueNoise(uvA * _SparkleScale + float2(0.0, time * 0.7));
                float s2 = ValueNoise(uvB * _SparkleScale * 1.37 - float2(time * 0.5, 0.0));
                float sparkle = smoothstep(_SparkleThreshold, _SparkleThreshold + 0.03, s1 * s2 * 1.6)
                              * saturate(NdotH * 2.0 - 0.6) * _SparkleStrength * distanceFade;

                // En lo corrupto el agua brilla menos.
                color += (specular + sparkle) * mainLight.color * mainLight.shadowAttenuation * (1.0 - foam) * lerp(0.3, 1.0, purifyMask);

                // Frente de la onda de purificacion (mismo brillo que el resto de los materiales).
                color += purifyEdge * _PurifyEdgeColor.rgb * _PurifyEdgeIntensity;

                // ---------- alpha ----------
                half edgeAlpha = saturate(viewDepthDiff / max(_EdgeFade, 1e-4));
            #if _REFRACTION_ON
                half alpha = edgeAlpha;
            #else
                half alpha = saturate(max(opacity, foam)) * edgeAlpha;
            #endif
                // La cascada es casi opaca (no se ve "a traves" de una caida de agua).
                alpha = lerp(alpha, max(alpha, 0.95), saturate(IN.steep));

                color = MixFog(color, IN.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
