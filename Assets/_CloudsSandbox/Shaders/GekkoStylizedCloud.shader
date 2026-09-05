Shader "Gekko/Stylized Cloud"
{
    Properties
    {
        [Header(Colores)]
        [HDR] _TopColor      ("Color iluminado", Color) = (1, 1, 1, 1)
        [HDR] _ShadeColor    ("Color en sombra", Color) = (0.62, 0.68, 0.85, 1)
        [HDR] _BottomColor   ("Color de la base", Color) = (0.45, 0.52, 0.72, 1)
        [HDR] _RimColor      ("Color del borde", Color) = (1, 0.95, 0.85, 1)

        [Header(Rolling local)]
        _NoiseScale     ("Escala del ruido", Float) = 1.6
        _RollDirection  ("Direccion del roll (local)", Vector) = (1, 0.15, 0.3, 0)
        _RollSpeed      ("Velocidad del roll", Float) = 0.08
        _Displacement   ("Deformacion de vertices", Range(0, 1)) = 0.18
        _DetailScale    ("Multiplicador de detalle", Float) = 2.5

        [Header(Silueta)]
        _Solidity       ("Solidez", Range(0, 2)) = 1.15
        _NoiseInfluence ("Ruido en la silueta", Range(0, 2)) = 0.75
        _Cutoff         ("Umbral de recorte", Range(0, 1)) = 0.35
        _EdgeSoftness   ("Suavidad del borde", Range(0.001, 0.5)) = 0.14

        [Header(Sombreado)]
        _LightWrap      ("Wrap de luz", Range(0, 1)) = 0.65
        _ShadeThreshold ("Umbral de sombra", Range(0, 1)) = 0.5
        _ShadeSmooth    ("Suavidad de sombra", Range(0.001, 0.5)) = 0.18
        _NoiseShading   ("Ruido en el sombreado", Range(0, 1)) = 0.35
        _HeightScale    ("Escala del degrade vertical", Float) = 1.0
        _HeightOffset   ("Offset del degrade vertical", Float) = 0.45
        _RimPower       ("Potencia del borde", Range(0.5, 8)) = 3.0
        _RimStrength    ("Fuerza del borde", Range(0, 2)) = 0.55
        _LightTint      ("Tintado por la luz", Range(0, 1)) = 0.5

        [Header(Render)]
        [Enum(Off, 0, On, 1)] _ZWrite ("Escribir profundidad", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"  = "UniversalPipeline"
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "GekkoCloudNoise.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _TopColor;
            float4 _ShadeColor;
            float4 _BottomColor;
            float4 _RimColor;
            float4 _RollDirection;
            float  _NoiseScale;
            float  _RollSpeed;
            float  _Displacement;
            float  _DetailScale;
            float  _Solidity;
            float  _NoiseInfluence;
            float  _Cutoff;
            float  _EdgeSoftness;
            float  _LightWrap;
            float  _ShadeThreshold;
            float  _ShadeSmooth;
            float  _NoiseShading;
            float  _HeightScale;
            float  _HeightOffset;
            float  _RimPower;
            float  _RimStrength;
            float  _LightTint;
            float  _ZWrite;
            float  _Cull;
        CBUFFER_END

        // Desplaza el vertice a lo largo de su normal con el FBM local que rueda
        // en el tiempo. Se usa igual en el pase de color y en el de sombras, para
        // que la silueta proyectada coincida con la de la nube.
        float3 GekkoCloudDisplacedPositionOS(float3 positionOS, float3 normalOS)
        {
            float3 offset = GekkoRollOffset(_RollDirection.xyz, _RollSpeed, _Time.y);
            float  n = GekkoFbm(positionOS * _NoiseScale + offset);
            return positionOS + normalOS * ((n - 0.5) * _Displacement);
        }
        ENDHLSL

        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex CloudVertex
            #pragma fragment CloudFragment
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings CloudVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 normalOS = normalize(IN.normalOS);
                float3 displacedOS = GekkoCloudDisplacedPositionOS(IN.positionOS.xyz, normalOS);

                VertexPositionInputs positions = GetVertexPositionInputs(displacedOS);
                VertexNormalInputs normals = GetVertexNormalInputs(normalOS);

                OUT.positionCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                // Se guarda la posicion SIN desplazar para muestrear el ruido en el
                // fragment: si no, el patron se arrastraria con su propia deformacion.
                OUT.positionOS = IN.positionOS.xyz;
                OUT.normalWS   = normals.normalWS;
                OUT.fogFactor  = ComputeFogFactor(positions.positionCS.z);
                return OUT;
            }

            half4 CloudFragment(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

                float3 offset = GekkoRollOffset(_RollDirection.xyz, _RollSpeed, _Time.y);
                float  noise = GekkoFbm(IN.positionOS * _NoiseScale * _DetailScale + offset);

                // Cuanto mas de frente mira la superficie, mas "espesor" hay detras.
                // Es la aproximacion barata de volumen que rompe la silueta.
                float facing = saturate(dot(N, V));
                float mask = saturate(facing * _Solidity + (noise - 0.5) * _NoiseInfluence);
                float alpha = smoothstep(_Cutoff - _EdgeSoftness, _Cutoff + _EdgeSoftness, mask);
                clip(alpha - 0.003);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float ndl = dot(N, mainLight.direction);
                float wrapped = saturate((ndl + _LightWrap) / (1.0 + _LightWrap));
                wrapped *= lerp(1.0, noise + 0.5, _NoiseShading);
                wrapped *= lerp(1.0, mainLight.shadowAttenuation, 0.75);

                float band = smoothstep(_ShadeThreshold - _ShadeSmooth,
                                        _ShadeThreshold + _ShadeSmooth,
                                        wrapped);

                float3 color = lerp(_ShadeColor.rgb, _TopColor.rgb, band);

                // Degrade vertical en espacio local: la panza de la nube queda densa.
                float heightT = saturate(IN.positionOS.y * _HeightScale + _HeightOffset);
                color = lerp(_BottomColor.rgb, color, heightT);

                float rim = pow(saturate(1.0 - facing), _RimPower) * _RimStrength;
                color += _RimColor.rgb * rim;

                color *= lerp(float3(1.0, 1.0, 1.0), mainLight.color, _LightTint);
                color += SampleSH(N) * 0.25;

                color = MixFog(color, IN.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma target 3.0
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

                float3 normalOS = normalize(IN.normalOS);
                float3 displacedOS = GekkoCloudDisplacedPositionOS(IN.positionOS.xyz, normalOS);

                float3 positionWS = TransformObjectToWorld(displacedOS);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);

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
    }

    Fallback Off
}
