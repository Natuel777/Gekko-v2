// Shader de PRUEBA de la Fase 1: muestra la máscara de purificación sobre cualquier malla, sin armar Shader Graphs.
//  - Modo "ver máscara" = 1: verde = sano, violeta = corrupto, amarillo = frente de la onda.
//  - Modo "ver máscara" = 0: usa el color/textura base como "sano" y le aplica el look corrupto de PurifyLook.
// No es para el juego final: es para comprobar que cada región limpia solo su parte.
Shader "Gekko/Purification/Debug Mascara"
{
    Properties
    {
        [Toggle] _ShowMask ("Ver máscara (1) / ver look (0)", Float) = 1
        _BaseMap ("Textura base (sano)", 2D) = "white" {}
        _BaseColor ("Color base (sano)", Color) = (1, 1, 1, 1)

        [Header(Mascara)]
        _HealthyColor ("Color sano (modo máscara)", Color) = (0.30, 0.80, 0.40, 1)
        _CorruptColor ("Color corrupto (modo máscara)", Color) = (0.35, 0.12, 0.45, 1)
        _NoiseScale ("Escala del ruido", Float) = 0.3
        _NoiseAmount ("Irregularidad del borde (m)", Float) = 2.5

        [Header(Look corrupto)]
        _CorruptTint ("Tinte corrupto", Color) = (0.80, 0.65, 0.95, 1)
        _CorruptSaturation ("Saturación corrupta", Range(0, 1)) = 0.2
        _CorruptDarken ("Brillo corrupto", Range(0, 1)) = 0.45

        [Header(Borde de la onda)]
        [HDR] _EdgeColor ("Color del borde", Color) = (1.0, 0.8, 0.3, 1)
        _EdgeIntensity ("Intensidad del borde", Float) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _HealthyColor;
            half4 _CorruptColor;
            half4 _CorruptTint;
            half4 _EdgeColor;
            float _ShowMask;
            float _NoiseScale;
            float _NoiseAmount;
            half _CorruptSaturation;
            half _CorruptDarken;
            half _EdgeIntensity;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Gekko/Purification/Shaders/PurifyMask.hlsl"
            #include "Assets/_Gekko/Purification/Shaders/PurifyLook.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float mask, edge;
                PurifyMaskAuto_float(IN.positionWS, _NoiseScale, _NoiseAmount, mask, edge);

                // Luz simple (lambert + ambiente fijo): alcanza para leer la forma del piso.
                Light mainLight = GetMainLight();
                half lighting = saturate(dot(normalize(IN.normalWS), mainLight.direction)) * 0.7 + 0.3;

                float3 healthy = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb * _BaseColor.rgb;

                float3 lookColor, emission;
                PurifyLook_float(healthy, mask, edge, _CorruptTint.rgb, _CorruptSaturation, _CorruptDarken,
                                 _EdgeColor.rgb, _EdgeIntensity, lookColor, emission);

                float3 maskColor = lerp(_CorruptColor.rgb, _HealthyColor.rgb, mask);
                float3 color = lerp(lookColor, maskColor, _ShowMask) * lighting + emission;

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // Profundidad y normales: sin estos pases el SSAO y el depth prepass de URP no ven el objeto.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half Frag(Varyings IN) : SV_Target { return IN.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings Vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target { return half4(normalize(IN.normalWS), 0); }
            ENDHLSL
        }
    }
}
