Shader "Gekko/Grass"
{
    Properties
    {
        [Header(Gradiente de la brizna)]
        _BottomColor    ("Color de la base", Color) = (0.11, 0.32, 0.18, 1)
        _TopColor       ("Color de la punta", Color) = (0.55, 0.85, 0.45, 1)
        _AmbientOcclusion ("Oscurecer la base", Range(0, 1)) = 0.45

        [Header(Variacion de color)]
        _VariationColor    ("Color de los manchones", Color) = (0.35, 0.78, 0.62, 1)
        _VariationScale    ("Escala de los manchones", Float) = 0.04
        _VariationStrength ("Fuerza de los manchones", Range(0, 1)) = 0.45
        _TintRandom        ("Variacion por brizna", Range(0, 0.5)) = 0.15

        [Header(Viento)]
        _WindDirection ("Direccion del viento", Vector) = (1, 0, 0.35, 0)
        _WindScale     ("Escala del viento", Float) = 0.12
        _WindSpeed     ("Velocidad del viento", Float) = 0.6
        _WindStrength  ("Fuerza del viento", Float) = 0.25
        _SwaySpeed     ("Velocidad del vaiven", Float) = 2.0
        _SwayStrength  ("Fuerza del vaiven", Float) = 0.04

        [Header(Interaccion)]
        _PushStrength ("Empuje lateral", Float) = 1.0
        _PushDown     ("Aplastado hacia abajo", Float) = 0.5

        [Header(Iluminacion)]
        _ShadowTint   ("Tinte en sombra", Color) = (0.35, 0.45, 0.55, 1)
        _LightWrap    ("Wrap de luz", Range(0, 1)) = 0.5
        _Translucency ("Translucidez a contraluz", Range(0, 2)) = 0.6
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

        // Tope de interactores. Tiene que coincidir con
        // GrassInteractionManager.MaxInteractors del lado de C#.
        #define MAX_GRASS_INTERACTORS 8

        // Globales, NO por material: los setea GrassInteractionManager una vez por
        // frame con Shader.SetGlobalVectorArray, asi sirven para todos los materiales
        // de pasto a la vez. Van fuera del CBUFFER UnityPerMaterial a proposito.
        float4 _GrassInteractors[MAX_GRASS_INTERACTORS];
        float  _GrassInteractorCount;

        CBUFFER_START(UnityPerMaterial)
            float4 _BottomColor;
            float4 _TopColor;
            float4 _VariationColor;
            float4 _WindDirection;
            float4 _ShadowTint;
            float  _AmbientOcclusion;
            float  _VariationScale;
            float  _VariationStrength;
            float  _TintRandom;
            float  _WindScale;
            float  _WindSpeed;
            float  _WindStrength;
            float  _SwaySpeed;
            float  _SwayStrength;
            float  _PushStrength;
            float  _PushDown;
            float  _LightWrap;
            float  _Translucency;
        CBUFFER_END

        float GrassHash21(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        float GrassNoise21(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);

            float a = GrassHash21(i);
            float b = GrassHash21(i + float2(1.0, 0.0));
            float c = GrassHash21(i + float2(0.0, 1.0));
            float d = GrassHash21(i + float2(1.0, 1.0));

            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        /// Desplaza un vertice de pasto por viento e interactores.
        /// height: 0 en la base, 1 en la punta. randomPhase: variacion por brizna.
        /// Todo el doblado se pondera por height*height, asi la base queda clavada al
        /// suelo y el movimiento se concentra en la punta.
        float3 GrassDisplace(float3 positionWS, float height, float randomPhase)
        {
            float bend = height * height;
            if (bend <= 0.0)
            {
                return positionWS;
            }

            float3 windDir = float3(_WindDirection.x, 0.0, _WindDirection.z);
            windDir = normalize(windDir + float3(1e-5, 0.0, 1e-5));

            // Onda grande que recorre el campo: es lo que hace las "olas" de pasto.
            float2 windUV = positionWS.xz * _WindScale - windDir.xz * (_Time.y * _WindSpeed);
            float gust = GrassNoise21(windUV) * 2.0 - 1.0;

            // Vaiven rapido y desfasado por brizna, para que no se muevan todas igual.
            float sway = sin(_Time.y * _SwaySpeed + randomPhase * 6.2831853);

            positionWS += windDir * (gust * _WindStrength * bend);
            positionWS.xz += float2(sway, sway * 0.6) * (_SwayStrength * bend);

            // Interactores: personajes que pisan el pasto. El loop esta acotado por el
            // contador real, no por MAX, para no pagar las 8 iteraciones siempre.
            int count = (int)min(_GrassInteractorCount, (float)MAX_GRASS_INTERACTORS);
            for (int i = 0; i < count; i++)
            {
                float4 interactor = _GrassInteractors[i];

                float3 delta = positionWS - interactor.xyz;
                delta.y = 0.0;

                float radius = max(interactor.w, 1e-4);
                float distance = length(delta);
                float falloff = saturate(1.0 - distance / radius);
                falloff *= falloff;

                if (falloff > 0.0)
                {
                    float3 away = delta / max(distance, 1e-4);
                    positionWS += away * (falloff * _PushStrength * bend);
                    positionWS.y -= falloff * _PushDown * bend;
                }
            }

            return positionWS;
        }
        ENDHLSL

        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // Las briznas son planas: se ven de los dos lados.
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex GrassVertex
            #pragma fragment GrassFragment
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
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float4 color      : TEXCOORD3;
                float  fogFactor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings GrassVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                positionWS = GrassDisplace(positionWS, IN.uv.y, IN.color.r);

                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.positionWS = positionWS;
                // La normal es la del SUELO, compartida por toda la brizna. Es el truco
                // que da el sombreado suave y "pintado" en vez de briznas facetadas.
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv         = IN.uv;
                OUT.color      = IN.color;
                OUT.fogFactor  = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 GrassFragment(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float height = IN.uv.y;

                // 1) Gradiente a lo largo de la brizna.
                float3 color = lerp(_BottomColor.rgb, _TopColor.rgb, height);

                // 2) Manchones de color a escala del mundo: es lo que evita que un
                //    campo grande se lea como una alfombra de un solo verde.
                float patch = GrassNoise21(IN.positionWS.xz * _VariationScale);
                color = lerp(color, _VariationColor.rgb, patch * _VariationStrength);

                // 3) Variacion por brizna.
                color *= lerp(1.0 - _TintRandom, 1.0 + _TintRandom, IN.color.r);

                // 4) Oclusion en la base: da la sensacion de densidad sin geometria extra.
                color *= lerp(1.0 - _AmbientOcclusion, 1.0, height);

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float ndl = dot(N, mainLight.direction);
                float wrapped = saturate((ndl + _LightWrap) / (1.0 + _LightWrap));
                wrapped *= lerp(1.0, mainLight.shadowAttenuation, 0.8);

                color *= lerp(_ShadowTint.rgb, float3(1.0, 1.0, 1.0), wrapped);
                color *= mainLight.color;
                color += SampleSH(N) * 0.3;

                // Translucidez: cuando la luz viene de atras, la punta se enciende.
                float backlight = saturate(dot(-mainLight.direction, V));
                color += _TopColor.rgb * (pow(backlight, 4.0) * _Translucency * height);

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0);
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
            Cull Off

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
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
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

                // El mismo desplazamiento que el pase de color: si no, la sombra se
                // quedaria quieta mientras el pasto se mueve.
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                positionWS = GrassDisplace(positionWS, IN.uv.y, IN.color.r);

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

        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma target 3.0
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
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

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                positionWS = GrassDisplace(positionWS, IN.uv.y, IN.color.r);

                OUT.positionCS = TransformWorldToHClip(positionWS);
                return OUT;
            }

            half4 DepthFragment(DepthVaryings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
