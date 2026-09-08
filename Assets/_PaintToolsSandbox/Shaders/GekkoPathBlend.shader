Shader "Gekko/Path Blend"
{
    Properties
    {
        [Header(Capa base)]
        _BaseTex    ("Textura base", 2D) = "white" {}
        _BaseColor  ("Color base", Color) = (0.35, 0.55, 0.28, 1)
        _BaseTiling ("Tiling base", Float) = 0.25

        [Header(Capa camino)]
        _PathTex    ("Textura del camino", 2D) = "white" {}
        _PathColor  ("Color del camino", Color) = (0.86, 0.80, 0.55, 1)
        _PathTiling ("Tiling del camino", Float) = 0.25
        _TintStrength ("Fuerza del tinte pintado", Range(0, 1)) = 1

        [Header(Proyeccion)]
        [Toggle(_TRIPLANAR_ON)] _Triplanar ("Triplanar (para pisos inclinados)", Float) = 0
        _TriplanarSharpness ("Dureza de la mezcla triplanar", Range(1, 16)) = 5

        [Header(Borde)]
        _EdgeSharpness    ("Dureza del borde", Range(0, 1)) = 0.72
        _EdgeNoiseScale   ("Escala del ruido de borde", Float) = 2.5
        _EdgeNoiseStrength("Fuerza del ruido de borde", Range(0, 1)) = 0.35

        [Header(Iluminacion)]
        _ShadowTint ("Tinte en sombra", Color) = (0.34, 0.42, 0.55, 1)
        _LightWrap  ("Wrap de luz", Range(0, 1)) = 0.4
        _BandSmooth ("Suavidad de la banda", Range(0.01, 0.5)) = 0.25

        // Las setea PathCanvas por MaterialPropertyBlock, no se tocan a mano.
        [HideInInspector] _PathMask       ("Mascara", 2D) = "black" {}
        [HideInInspector] _PathCanvasMin  ("Min del canvas (xz)", Vector) = (0, 0, 0, 0)
        [HideInInspector] _PathCanvasSize ("Tamano del canvas (xz)", Vector) = (1, 1, 0, 0)
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
            float4 _BaseColor;
            float4 _PathColor;
            float4 _ShadowTint;
            float4 _PathCanvasMin;
            float4 _PathCanvasSize;
            float  _BaseTiling;
            float  _PathTiling;
            float  _TintStrength;
            float  _EdgeSharpness;
            float  _EdgeNoiseScale;
            float  _EdgeNoiseStrength;
            float  _LightWrap;
            float  _BandSmooth;
            float  _Triplanar;
            float  _TriplanarSharpness;
        CBUFFER_END

        float PathHash21(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        float PathNoise21(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);

            float a = PathHash21(i);
            float b = PathHash21(i + float2(1.0, 0.0));
            float c = PathHash21(i + float2(0.0, 1.0));
            float d = PathHash21(i + float2(1.0, 1.0));

            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex PathVertex
            #pragma fragment PathFragment
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            // Local y solo de fragment: un material plano no paga por las variantes
            // triplanar, y no se generan combinaciones con el resto de keywords.
            #pragma shader_feature_local_fragment _TRIPLANAR_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseTex);   SAMPLER(sampler_BaseTex);
            TEXTURE2D(_PathTex);   SAMPLER(sampler_PathTex);
            TEXTURE2D(_PathMask);  SAMPLER(sampler_PathMask);

            // Proyecta la textura sobre los tres ejes del mundo y mezcla segun la normal.
            // Es lo que evita que la textura se estire en una rampa o en un terreno con
            // relieve: con proyeccion cenital sola, los texeles se alargan en proporcion
            // inversa al coseno de la pendiente, y en una pared vertical se vuelven
            // rayas infinitas.
            float3 SampleTriplanar(TEXTURE2D_PARAM(tex, samp), float3 positionWS, float3 normalWS, float tiling)
            {
                float3 blend = pow(abs(normalWS), _TriplanarSharpness);
                blend /= max(blend.x + blend.y + blend.z, 1e-4);

                float3 planeX = SAMPLE_TEXTURE2D(tex, samp, positionWS.zy * tiling).rgb;
                float3 planeY = SAMPLE_TEXTURE2D(tex, samp, positionWS.xz * tiling).rgb;
                float3 planeZ = SAMPLE_TEXTURE2D(tex, samp, positionWS.xy * tiling).rgb;

                return planeX * blend.x + planeY * blend.y + planeZ * blend.z;
            }

            // Con el keyword apagado se paga 1 sample; con triplanar, 3. Por eso es
            // opt-in por material y no algo que se banque siempre.
            float3 SampleLayer(TEXTURE2D_PARAM(tex, samp), float3 positionWS, float3 normalWS, float tiling)
            {
            #if defined(_TRIPLANAR_ON)
                return SampleTriplanar(TEXTURE2D_ARGS(tex, samp), positionWS, normalWS, tiling);
            #else
                return SAMPLE_TEXTURE2D(tex, samp, positionWS.xz * tiling).rgb;
            #endif
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings PathVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.normalWS   = normals.normalWS;
                OUT.fogFactor  = ComputeFogFactor(positions.positionCS.z);
                return OUT;
            }

            half4 PathFragment(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // Las dos capas se muestrean por posicion de MUNDO, no por UV de la
                // malla. Por eso el sistema no depende ni de la cantidad de vertices ni
                // de que la malla tenga UVs limpias.
                float2 worldUV = IN.positionWS.xz;
                float3 N = normalize(IN.normalWS);

                float3 baseColor = SampleLayer(TEXTURE2D_ARGS(_BaseTex, sampler_BaseTex),
                                               IN.positionWS, N, _BaseTiling) * _BaseColor.rgb;
                float3 pathColor = SampleLayer(TEXTURE2D_ARGS(_PathTex, sampler_PathTex),
                                               IN.positionWS, N, _PathTiling) * _PathColor.rgb;

                // La MASCARA sigue siendo cenital aunque las capas sean triplanar, y es
                // a proposito: se pinto mirando desde arriba, igual que un splatmap de
                // terrain. En una pendiente se comprime por el coseno, que es la
                // proyeccion correcta de lo que se dibujo en planta.
                float2 canvasUV = (worldUV - _PathCanvasMin.xy) / max(_PathCanvasSize.xy, 1e-4);

                // Fuera del canvas no hay camino. Se resuelve sin branch.
                float2 insideAxis = step(0.0, canvasUV) * step(canvasUV, 1.0);
                float inside = insideAxis.x * insideAxis.y;

                float4 mask = SAMPLE_TEXTURE2D(_PathMask, sampler_PathMask, canvasUV);
                float coverage = mask.a * inside;

                // El ruido rompe el borde de la mascara. Es lo que permite pintar con una
                // textura de baja resolucion y que igual se vea un borde dibujado a mano
                // en vez de un degrade borroso.
                float noise = PathNoise21(worldUV * _EdgeNoiseScale);
                coverage = saturate(coverage + (noise - 0.5) * _EdgeNoiseStrength);

                float width = lerp(0.45, 0.02, _EdgeSharpness);
                coverage = smoothstep(0.5 - width, 0.5 + width, coverage);

                // Tinte pintado: el RGB de la mascara centrado en 0.5 = neutro.
                pathColor *= lerp(1.0, mask.rgb * 2.0, _TintStrength);

                float3 albedo = lerp(baseColor, pathColor, coverage);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float ndl = dot(N, mainLight.direction);
                float wrapped = saturate((ndl + _LightWrap) / (1.0 + _LightWrap));
                wrapped *= lerp(1.0, mainLight.shadowAttenuation, 0.85);

                // Banda suave en vez de lambert puro: mantiene el look plano y estilizado.
                float band = smoothstep(0.5 - _BandSmooth, 0.5 + _BandSmooth, wrapped);

                float3 lighting = lerp(_ShadowTint.rgb, mainLight.color, band);

            #if defined(_ADDITIONAL_LIGHTS)
                // La referencia tiene muchas luces de color chicas: sin esto, los focos
                // no pintan el piso.
                uint additionalCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < additionalCount; lightIndex++)
                {
                    Light light = GetAdditionalLight(lightIndex, IN.positionWS);
                    float additionalNdl = saturate((dot(N, light.direction) + _LightWrap) / (1.0 + _LightWrap));
                    lighting += light.color * (additionalNdl * light.distanceAttenuation * light.shadowAttenuation);
                }
            #endif

                float3 color = albedo * lighting;
                color += albedo * SampleSH(N) * 0.35;

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Estos tres pases van escritos a mano y NO con UsePass de URP Lit.
        // Con UsePass, cada pase heredado traia el CBUFFER UnityPerMaterial de Lit, de
        // otro tamano que el de este shader, y el SRP Batcher exige que TODOS los pases
        // de un SubShader declaren exactamente el mismo layout. Asi el shader reportaba
        // "UnityPerMaterial CBuffer inconsistent size inside a SubShader (DepthNormals)"
        // y quedaba fuera del batcher por completo.
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
            #pragma target 3.0
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
            #pragma target 3.0
            #pragma multi_compile_instancing

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

            half4 DepthNormalsFragment(DepthNormalsVaryings IN) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(IN.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
