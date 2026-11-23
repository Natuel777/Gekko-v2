// Transición "pintada" entre dos materiales URP/Lit (A = corrupto, B = sano), de abajo hacia arriba.
//
// Lo usa TurretPurifiedMaterialSwap: al purificarse la torreta crea una instancia de este shader, copia en ella las
// texturas y valores de los dos materiales Lit, y anima _PaintHeight (altura en MUNDO) desde el pie hasta la punta.
// Debajo de esa altura se ve B, arriba A, con un frente irregular (ruido 3D) y una franja brillante del mismo color
// que el frente de la onda de purificación del suelo. Al terminar, _Pulse hace un destello y el script devuelve el
// material Lit sano, así que este shader solo se dibuja durante la transición.
//
// La altura es en mundo (no por malla) para que todas las piezas de la torreta se pinten como una sola.
// Para mallas animadas (la planta carnívora) se puede usar la posición de reposo guardada en TEXCOORD3 (_UseRestPosition = 1):
// así la pintura queda pegada al cuerpo aunque se doble o muerda. La guarda PlantPaintPurify al arrancar.
// Los pases de sombra/profundidad van escritos a mano (no UsePass) para que el CBUFFER sea el mismo en todos y el
// shader entre en el SRP Batcher (mismo motivo que GekkoPathBlend).
Shader "Gekko/Purification/Paint Transition"
{
    Properties
    {
        [Header(Material A (corrupto))]
        _BaseMapA          ("Albedo A", 2D) = "white" {}
        _BaseColorA        ("Color A", Color) = (1, 1, 1, 1)
        [Normal] _BumpMapA ("Normal A", 2D) = "bump" {}
        _BumpScaleA        ("Fuerza normal A", Float) = 1
        _MetallicGlossMapA ("Metallic (R) Smoothness (A) A", 2D) = "white" {}
        _UseMetalMapA      ("Usa mapa metallic A", Float) = 0
        _MetallicA         ("Metallic A", Range(0, 1)) = 0
        _SmoothnessA       ("Smoothness A", Range(0, 1)) = 0.5
        _EmissionMapA      ("Emission A", 2D) = "white" {}
        [HDR] _EmissionColorA ("Color emission A", Color) = (0, 0, 0, 1)

        [Header(Material B (sano))]
        _BaseMapB          ("Albedo B", 2D) = "white" {}
        _BaseColorB        ("Color B", Color) = (1, 1, 1, 1)
        [Normal] _BumpMapB ("Normal B", 2D) = "bump" {}
        _BumpScaleB        ("Fuerza normal B", Float) = 1
        _MetallicGlossMapB ("Metallic (R) Smoothness (A) B", 2D) = "white" {}
        _UseMetalMapB      ("Usa mapa metallic B", Float) = 0
        _MetallicB         ("Metallic B", Range(0, 1)) = 0
        _SmoothnessB       ("Smoothness B", Range(0, 1)) = 0.5
        _EmissionMapB      ("Emission B", 2D) = "white" {}
        [HDR] _EmissionColorB ("Color emission B", Color) = (0, 0, 0, 1)
        [Normal] _DetailNormalMapB ("Detail normal B", 2D) = "bump" {}
        _DetailNormalScaleB ("Fuerza detail normal B", Float) = 0

        [Header(Pintado)]
        _PaintHeight       ("Altura del frente (mundo)", Float) = -10000
        _PaintNoiseScale   ("Escala del ruido (por metro)", Float) = 5
        _PaintNoiseAmount  ("Irregularidad del frente (m)", Float) = 0.14
        _PaintSoftness     ("Suavidad del corte (m)", Float) = 0.025
        _UseRestPosition   ("Usar posición de reposo (TEXCOORD3)", Float) = 0

        [Header(Borde)]
        [HDR] _EdgeColor   ("Color del borde", Color) = (1, 0.8, 0.3, 1)
        _EdgeIntensity     ("Intensidad del borde", Float) = 2
        _EdgeWidth         ("Ancho del borde (m)", Float) = 0.1

        [Header(Pulso final)]
        _Pulse             ("Pulso (0..1)", Range(0, 1)) = 0
        _PulseIntensity    ("Intensidad del pulso", Float) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMapA_ST;
            float4 _BaseColorA;
            float4 _EmissionColorA;
            float  _BumpScaleA;
            float  _UseMetalMapA;
            float  _MetallicA;
            float  _SmoothnessA;

            float4 _BaseMapB_ST;
            float4 _BaseColorB;
            float4 _EmissionColorB;
            float4 _DetailNormalMapB_ST;
            float  _BumpScaleB;
            float  _UseMetalMapB;
            float  _MetallicB;
            float  _SmoothnessB;
            float  _DetailNormalScaleB;

            float  _PaintHeight;
            float  _PaintNoiseScale;
            float  _PaintNoiseAmount;
            float  _PaintSoftness;
            float  _UseRestPosition;

            float4 _EdgeColor;
            float  _EdgeIntensity;
            float  _EdgeWidth;

            float  _Pulse;
            float  _PulseIntensity;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex PaintVertex
            #pragma fragment PaintFragment
            #pragma target 3.5

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMapA);          SAMPLER(sampler_BaseMapA);
            TEXTURE2D(_BumpMapA);
            TEXTURE2D(_MetallicGlossMapA);
            TEXTURE2D(_EmissionMapA);
            TEXTURE2D(_BaseMapB);
            TEXTURE2D(_BumpMapB);
            TEXTURE2D(_MetallicGlossMapB);
            TEXTURE2D(_EmissionMapB);
            TEXTURE2D(_DetailNormalMapB);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float4 restPos    : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 tangentWS  : TEXCOORD3;
                float  fogFactor  : TEXCOORD4;
                float3 restPosWS  : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ---------- Ruido 3D (en mundo) para el frente irregular ----------
            float PaintHash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float PaintValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = PaintHash31(i);
                float n100 = PaintHash31(i + float3(1, 0, 0));
                float n010 = PaintHash31(i + float3(0, 1, 0));
                float n110 = PaintHash31(i + float3(1, 1, 0));
                float n001 = PaintHash31(i + float3(0, 0, 1));
                float n101 = PaintHash31(i + float3(1, 0, 1));
                float n011 = PaintHash31(i + float3(0, 1, 1));
                float n111 = PaintHash31(i + float3(1, 1, 1));

                float x00 = lerp(n000, n100, f.x);
                float x10 = lerp(n010, n110, f.x);
                float x01 = lerp(n001, n101, f.x);
                float x11 = lerp(n011, n111, f.x);

                return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
            }

            // 0..1, tres octavas: manchas grandes con bordes finos, como pintura que se extiende.
            float PaintNoise(float3 p)
            {
                float n = PaintValueNoise(p) * 0.5
                        + PaintValueNoise(p * 2.03) * 0.25
                        + PaintValueNoise(p * 4.07) * 0.125;
                return n / 0.875;
            }

            Varyings PaintVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = nrm.normalWS;
                OUT.tangentWS = float4(nrm.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
                OUT.uv = IN.uv;
                OUT.restPosWS = IN.restPos.xyz;
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 PaintFragment(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // ---- Cuánto está pintado este píxel (1 = B sano, 0 = A corrupto) ----
                float3 paintPos = _UseRestPosition > 0.5 ? IN.restPosWS : IN.positionWS;
                float noise = PaintNoise(paintPos * _PaintNoiseScale);
                float y = paintPos.y + (noise - 0.5) * 2.0 * _PaintNoiseAmount;
                float dist = _PaintHeight - y; // > 0 = debajo del frente (ya pintado)
                float painted = saturate(dist / max(_PaintSoftness, 0.0001) + 0.5);

                // Franja brillante centrada en el frente.
                float edge = saturate(1.0 - abs(dist) / max(_EdgeWidth, 0.0001));
                edge *= edge;

                // ---- Material A ----
                float2 uvA = IN.uv * _BaseMapA_ST.xy + _BaseMapA_ST.zw;
                half4 albedoA = SAMPLE_TEXTURE2D(_BaseMapA, sampler_BaseMapA, uvA) * _BaseColorA;
                half3 normalA = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMapA, sampler_BaseMapA, uvA), _BumpScaleA);
                half4 mgA = SAMPLE_TEXTURE2D(_MetallicGlossMapA, sampler_BaseMapA, uvA);
                half metallicA = lerp(_MetallicA, mgA.r, _UseMetalMapA);
                half smoothA = lerp(_SmoothnessA, mgA.a * _SmoothnessA, _UseMetalMapA);
                half3 emissionA = SAMPLE_TEXTURE2D(_EmissionMapA, sampler_BaseMapA, uvA).rgb * _EmissionColorA.rgb;

                // ---- Material B ----
                float2 uvB = IN.uv * _BaseMapB_ST.xy + _BaseMapB_ST.zw;
                half4 albedoB = SAMPLE_TEXTURE2D(_BaseMapB, sampler_BaseMapA, uvB) * _BaseColorB;
                half3 normalB = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMapB, sampler_BaseMapA, uvB), _BumpScaleB);
                if (_DetailNormalScaleB > 0.0)
                {
                    float2 uvD = IN.uv * _DetailNormalMapB_ST.xy + _DetailNormalMapB_ST.zw;
                    half3 detail = normalize(UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMapB, sampler_BaseMapA, uvD), _DetailNormalScaleB));
                    normalB = BlendNormalRNM(normalB, detail);
                }
                half4 mgB = SAMPLE_TEXTURE2D(_MetallicGlossMapB, sampler_BaseMapA, uvB);
                half metallicB = lerp(_MetallicB, mgB.r, _UseMetalMapB);
                half smoothB = lerp(_SmoothnessB, mgB.a * _SmoothnessB, _UseMetalMapB);
                half3 emissionB = SAMPLE_TEXTURE2D(_EmissionMapB, sampler_BaseMapA, uvB).rgb * _EmissionColorB.rgb;

                // ---- Mezcla ----
                half3 normalTS = normalize(lerp(normalA, normalB, painted));

                float3 bitangent = IN.tangentWS.w * cross(IN.normalWS, IN.tangentWS.xyz);
                half3x3 tbn = half3x3(IN.tangentWS.xyz, bitangent, IN.normalWS);
                float3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tbn));
                float3 viewWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = lerp(albedoA.rgb, albedoB.rgb, painted);
                surface.metallic = lerp(metallicA, metallicB, painted);
                surface.smoothness = lerp(smoothA, smoothB, painted);
                surface.normalTS = normalTS;
                surface.occlusion = 1.0;
                surface.alpha = 1.0;

                // Brillo del frente + pulso final (más fuerte en los bordes de la silueta, tipo rim).
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewWS)), 2.0);
                half3 glow = _EdgeColor.rgb * (edge * _EdgeIntensity + _Pulse * _PulseIntensity * (0.35 + 0.65 * fresnel));
                surface.emission = lerp(emissionA, emissionB, painted) + glow;

                InputData input = (InputData)0;
                input.positionWS = IN.positionWS;
                input.positionCS = IN.positionCS;
                input.normalWS = normalWS;
                input.viewDirectionWS = viewWS;
                input.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                input.fogCoord = IN.fogFactor;
                input.bakedGI = SampleSH(normalWS);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);
                input.tangentToWorld = tbn;

                half4 color = UniversalFragmentPBR(input, surface);
                color.rgb = MixFog(color.rgb, IN.fogFactor);
                return half4(color.rgb, 1.0);
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

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVertex(ShadowAttributes IN)
            {
                ShadowVaryings OUT = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 ShadowFragment(ShadowVaryings IN) : SV_Target
            {
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

            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma target 3.5
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthVertex(DepthAttributes IN)
            {
                DepthVaryings OUT = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 DepthFragment(DepthVaryings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct DepthNormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthNormalsVaryings DepthNormalsVertex(DepthNormalsAttributes IN)
            {
                DepthNormalsVaryings OUT = (DepthNormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            void DepthNormalsFragment(DepthNormalsVaryings IN
                , out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out float4 outRenderingLayers : SV_Target1
            #endif
            )
            {
                outNormalWS = half4(NormalizeNormalPerPixel(IN.normalWS), 0.0);
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()), 0, 0, 0);
            #endif
            }
            ENDHLSL
        }
    }

    Fallback Off
}
