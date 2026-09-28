Shader "Gekko/Path Blend"
{
    Properties
    {
        [Header(Capa base)]
        [NoScaleOffset] _BaseTex ("Textura base", 2D) = "white" {}
        _BaseColor  ("Color base", Color) = (0.35, 0.55, 0.28, 1)
        _BaseTiling ("Tiling base", Float) = 0.25

        // Hasta 4 texturas pintables sobre la base. Cada una es un canal de la
        // splat mask (R=Tex1, G=Tex2, B=Tex3, A=Tex4): el pincel pinta "cuanto de
        // esta textura hay" en vez de un tinte, y lo que sobra (1 - suma de las 4)
        // es la base. El toggle de cada slot controla si el shader la muestrea o
        // no: apagado no cuesta nada, ni el sampler se declara.
        [Header(Textura pintable 1)]
        [NoScaleOffset] _Tex1 ("Textura 1", 2D) = "white" {}
        _Tex1Color  ("Color 1", Color) = (0.86, 0.80, 0.55, 1)
        _Tex1Tiling ("Tiling 1", Float) = 0.25

        [Header(Textura pintable 2 (opcional))]
        [Toggle(_TEX2_ON)] _Tex2Enabled ("Textura 2 activa", Float) = 0
        [NoScaleOffset] _Tex2 ("Textura 2", 2D) = "white" {}
        _Tex2Color  ("Color 2", Color) = (1, 1, 1, 1)
        _Tex2Tiling ("Tiling 2", Float) = 0.25

        [Header(Textura pintable 3 (opcional))]
        [Toggle(_TEX3_ON)] _Tex3Enabled ("Textura 3 activa", Float) = 0
        [NoScaleOffset] _Tex3 ("Textura 3", 2D) = "white" {}
        _Tex3Color  ("Color 3", Color) = (1, 1, 1, 1)
        _Tex3Tiling ("Tiling 3", Float) = 0.25

        [Header(Textura pintable 4 (opcional))]
        [Toggle(_TEX4_ON)] _Tex4Enabled ("Textura 4 activa", Float) = 0
        [NoScaleOffset] _Tex4 ("Textura 4", 2D) = "white" {}
        _Tex4Color  ("Color 4", Color) = (1, 1, 1, 1)
        _Tex4Tiling ("Tiling 4", Float) = 0.25

        [Header(Tinte pintado)]
        _TintStrength ("Fuerza del tinte pintado", Range(0, 1)) = 1

        [Header(Proyeccion)]
        [Toggle(_TRIPLANAR_ON)] _Triplanar ("Triplanar (para pisos inclinados)", Float) = 1
        _TriplanarSharpness ("Dureza de la mezcla triplanar", Range(1, 16)) = 5

        [Header(Normal maps opcionales)]
        [Toggle(_NORMALMAP_ON)] _NormalMapping ("Usar normal maps (base + textura 1)", Float) = 0
        [NoScaleOffset] [Normal] _BaseNormalMap ("Normal base", 2D) = "bump" {}
        _BaseNormalStrength ("Fuerza normal base", Range(0, 2)) = 1
        [NoScaleOffset] [Normal] _Tex1NormalMap ("Normal textura 1", 2D) = "bump" {}
        _Tex1NormalStrength ("Fuerza normal textura 1", Range(0, 2)) = 1

        [Header(Borde)]
        _EdgeSharpness    ("Dureza del borde", Range(0, 1)) = 0.72
        _EdgeNoiseScale   ("Escala del ruido de borde", Float) = 2.5
        _EdgeNoiseStrength("Fuerza del ruido de borde", Range(0, 1)) = 0.35

        [Header(Iluminacion)]
        _ShadowTint ("Tinte en sombra", Color) = (0.34, 0.42, 0.55, 1)
        _LightWrap  ("Wrap de luz", Range(0, 1)) = 0.4
        _BandSmooth ("Suavidad de la banda", Range(0.01, 0.5)) = 0.25

        // Las setea PathCanvas por MaterialPropertyBlock, no se tocan a mano.
        // _SplatMask: RGBA = peso de Tex1/Tex2/Tex3/Tex4 (splatmap, no color).
        // _TintMask: RGB = tinte pintado (neutro = mitad), A = fuerza de ese tinte.
        // Son dos mascaras separadas a proposito: la primera elige QUE textura se
        // ve, la segunda opcionalmente la recolorea encima, sin pisarse entre si.
        [HideInInspector] _SplatMask      ("Splat mask", 2D) = "black" {}
        [HideInInspector] _TintMask       ("Tint mask", 2D) = "black" {}
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
            float4 _Tex1Color;
            float4 _Tex2Color;
            float4 _Tex3Color;
            float4 _Tex4Color;
            float4 _ShadowTint;
            float4 _PathCanvasMin;
            float4 _PathCanvasSize;
            float  _BaseTiling;
            float  _Tex1Tiling;
            float  _Tex2Tiling;
            float  _Tex3Tiling;
            float  _Tex4Tiling;
            float  _Tex2Enabled;
            float  _Tex3Enabled;
            float  _Tex4Enabled;
            float  _TintStrength;
            float  _EdgeSharpness;
            float  _EdgeNoiseScale;
            float  _EdgeNoiseStrength;
            float  _LightWrap;
            float  _BandSmooth;
            float  _Triplanar;
            float  _TriplanarSharpness;
            float  _NormalMapping;
            float  _BaseNormalStrength;
            float  _Tex1NormalStrength;
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

            // Sin esto el piso queda ciego a los Decal Projector: el renderer feature
            // pinta los decals en el DBuffer ANTES de este pase, pero si el shader no
            // declara estas variantes ni lo lee, esa informacion se descarta entera.
            // Es exactamente lo que le pasaba al material de caminos.
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3

            // Local y solo de fragment: un material plano no paga por las variantes
            // triplanar, y no se generan combinaciones con el resto de keywords.
            #pragma shader_feature_local_fragment _TRIPLANAR_ON

            // Normal maps: totalmente opcional. Sin este keyword ni se declaran los
            // samplers extra, asi que un material sin normal maps no paga nada.
            #pragma shader_feature_local_fragment _NORMALMAP_ON

            // Texturas pintables 2/3/4: cada una es opt-in por separado. Un material
            // con solo Textura 1 (el caso comun) no declara samplers ni paga samples
            // de las otras tres.
            #pragma shader_feature_local_fragment _TEX2_ON
            #pragma shader_feature_local_fragment _TEX3_ON
            #pragma shader_feature_local_fragment _TEX4_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

            TEXTURE2D(_BaseTex);    SAMPLER(sampler_BaseTex);
            TEXTURE2D(_Tex1);       SAMPLER(sampler_Tex1);
            TEXTURE2D(_SplatMask);  SAMPLER(sampler_SplatMask);
            TEXTURE2D(_TintMask);   SAMPLER(sampler_TintMask);

        #if defined(_TEX2_ON)
            TEXTURE2D(_Tex2); SAMPLER(sampler_Tex2);
        #endif
        #if defined(_TEX3_ON)
            TEXTURE2D(_Tex3); SAMPLER(sampler_Tex3);
        #endif
        #if defined(_TEX4_ON)
            TEXTURE2D(_Tex4); SAMPLER(sampler_Tex4);
        #endif

        #if defined(_NORMALMAP_ON)
            TEXTURE2D(_BaseNormalMap); SAMPLER(sampler_BaseNormalMap);
            TEXTURE2D(_Tex1NormalMap); SAMPLER(sampler_Tex1NormalMap);
        #endif

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

        #if defined(_NORMALMAP_ON)
            // Blend triplanar de normal maps ("whiteout blend", Ben Golus): cada plano
            // aporta su propia normal de tangente, se le suma la normal base del plano
            // correspondiente en vez de multiplicarla por una matriz TBN (no tenemos
            // tangentes de malla, y no hacen falta), y se mezcla con el mismo peso que
            // el albedo. Sin esto los normal maps se verian bien de frente y quebrados
            // en las costuras entre los 3 ejes.
            float3 SampleNormalTriplanar(TEXTURE2D_PARAM(tex, samp), float3 positionWS, float3 normalWS, float tiling, float strength)
            {
                float3 blend = pow(abs(normalWS), _TriplanarSharpness);
                blend /= max(blend.x + blend.y + blend.z, 1e-4);

                float3 tnX = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, positionWS.zy * tiling), strength);
                float3 tnY = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, positionWS.xz * tiling), strength);
                float3 tnZ = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, positionWS.xy * tiling), strength);

                tnX = float3(tnX.xy + normalWS.zy, abs(tnX.z) * normalWS.x);
                tnY = float3(tnY.xy + normalWS.xz, abs(tnY.z) * normalWS.y);
                tnZ = float3(tnZ.xy + normalWS.xy, abs(tnZ.z) * normalWS.z);

                return normalize(tnX.zyx * blend.x + tnY.xzy * blend.y + tnZ.xyz * blend.z);
            }

            // Sin triplanar, un solo plano cenital: igual que el albedo, se asume tangente
            // = eje X del mundo y bitangente = eje Z, así que no hace falta una base TBN
            // real. Es la misma simplificacion que ya usa SampleLayer para el color.
            float3 SampleNormalCenital(TEXTURE2D_PARAM(tex, samp), float3 positionWS, float tiling, float strength)
            {
                float3 tn = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, positionWS.xz * tiling), strength);
                return normalize(float3(tn.x, tn.z, tn.y));
            }

            float3 SampleLayerNormal(TEXTURE2D_PARAM(tex, samp), float3 positionWS, float3 normalWS, float tiling, float strength)
            {
            #if defined(_TRIPLANAR_ON)
                return SampleNormalTriplanar(TEXTURE2D_ARGS(tex, samp), positionWS, normalWS, tiling, strength);
            #else
                return SampleNormalCenital(TEXTURE2D_ARGS(tex, samp), positionWS, tiling, strength);
            #endif
            }
        #endif

            // Reparte el peso que le "sobra" a las texturas apagadas hacia la base, sin
            // branch: un canal de un slot desactivado se fuerza a 0 antes de sumar, asi
            // que ese peso queda disponible para _baseWeight_ como si nunca se hubiera
            // pintado ahi. Evita que pintar y despues destildar una textura dejes un
            // agujero negro en el piso.
            float4 MaskEnabledChannels(float4 raw)
            {
            #if !defined(_TEX2_ON)
                raw.g = 0.0;
            #endif
            #if !defined(_TEX3_ON)
                raw.b = 0.0;
            #endif
            #if !defined(_TEX4_ON)
                raw.a = 0.0;
            #endif
                return raw;
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

                // Todas las capas se muestrean por posicion de MUNDO, no por UV de la
                // malla. Por eso el sistema no depende ni de la cantidad de vertices ni
                // de que la malla tenga UVs limpias.
                float2 worldUV = IN.positionWS.xz;
                float3 N = normalize(IN.normalWS);

                // Las MASCARAS siguen siendo cenitales aunque las capas sean triplanar,
                // y es a proposito: se pintan mirando desde arriba, igual que un
                // splatmap de terrain. En una pendiente se comprimen por el coseno, que
                // es la proyeccion correcta de lo que se dibujo en planta.
                float2 canvasUV = (worldUV - _PathCanvasMin.xy) / max(_PathCanvasSize.xy, 1e-4);

                // Fuera del canvas no hay nada pintado. Se resuelve sin branch.
                float2 insideAxis = step(0.0, canvasUV) * step(canvasUV, 1.0);
                float inside = insideAxis.x * insideAxis.y;

                // _SplatMask: RGBA = cuanto de Tex1/Tex2/Tex3/Tex4 hay en este pixel.
                // No es tinte, es "que textura se ve" — el equivalente pintable a un
                // splatmap de terreno con hasta 4 capas.
                float4 rawWeights = MaskEnabledChannels(SAMPLE_TEXTURE2D(_SplatMask, sampler_SplatMask, canvasUV) * inside);
                float rawTotal = min(rawWeights.r + rawWeights.g + rawWeights.b + rawWeights.a, 1.0);

                // El mismo truco de borde estilizado que antes (ruido + corte duro),
                // ahora sobre el TOTAL pintado en vez de un unico canal de cobertura.
                // Es lo que permite pintar con una mascara de baja resolucion y que
                // igual se vea un borde dibujado a mano en vez de un degrade borroso.
                float noise = PathNoise21(worldUV * _EdgeNoiseScale);
                float sharpTotal = saturate(rawTotal + (noise - 0.5) * _EdgeNoiseStrength);
                float width = lerp(0.45, 0.02, _EdgeSharpness);
                sharpTotal = smoothstep(0.5 - width, 0.5 + width, sharpTotal);

                // Reescala los 4 pesos crudos para que sigan sumando el total ya
                // afilado, manteniendo la proporcion relativa entre texturas.
                float rescale = sharpTotal / max(rawTotal, 1e-4);
                float4 weights = rawWeights * rescale;
                float baseWeight = saturate(1.0 - (weights.r + weights.g + weights.b + weights.a));

                float3 baseColor = SampleLayer(TEXTURE2D_ARGS(_BaseTex, sampler_BaseTex),
                                                IN.positionWS, N, _BaseTiling) * _BaseColor.rgb;
                float3 tex1Color = SampleLayer(TEXTURE2D_ARGS(_Tex1, sampler_Tex1),
                                                IN.positionWS, N, _Tex1Tiling) * _Tex1Color.rgb;

                float3 albedo = baseColor * baseWeight + tex1Color * weights.r;

            #if defined(_TEX2_ON)
                float3 tex2Color = SampleLayer(TEXTURE2D_ARGS(_Tex2, sampler_Tex2),
                                                IN.positionWS, N, _Tex2Tiling) * _Tex2Color.rgb;
                albedo += tex2Color * weights.g;
            #endif
            #if defined(_TEX3_ON)
                float3 tex3Color = SampleLayer(TEXTURE2D_ARGS(_Tex3, sampler_Tex3),
                                                IN.positionWS, N, _Tex3Tiling) * _Tex3Color.rgb;
                albedo += tex3Color * weights.b;
            #endif
            #if defined(_TEX4_ON)
                float3 tex4Color = SampleLayer(TEXTURE2D_ARGS(_Tex4, sampler_Tex4),
                                                IN.positionWS, N, _Tex4Tiling) * _Tex4Color.rgb;
                albedo += tex4Color * weights.a;
            #endif

                // _TintMask: mascara APARTE del splat, para no competir por los mismos
                // canales. RGB = tinte (centrado en 0.5 = neutro), A = cuanto de ese
                // tinte se aplica ahi. Se pinta encima de lo que sea que ya se mezclo
                // arriba (base o cualquiera de las 4 texturas).
                float4 tintSample = SAMPLE_TEXTURE2D(_TintMask, sampler_TintMask, canvasUV);
                float tintAmount = tintSample.a * inside;
                albedo *= lerp(1.0, tintSample.rgb * 2.0, tintAmount * _TintStrength);

                // Normal maps opcionales: se mezclan con el mismo peso que el albedo de
                // Textura 1 (todavia no cubre Tex2/3/4, para no triplicar samples).
            #if defined(_NORMALMAP_ON)
                float3 baseNormalWS = SampleLayerNormal(TEXTURE2D_ARGS(_BaseNormalMap, sampler_BaseNormalMap),
                                                         IN.positionWS, N, _BaseTiling, _BaseNormalStrength);
                float3 tex1NormalWS = SampleLayerNormal(TEXTURE2D_ARGS(_Tex1NormalMap, sampler_Tex1NormalMap),
                                                         IN.positionWS, N, _Tex1Tiling, _Tex1NormalStrength);
                N = normalize(lerp(baseNormalWS, tex1NormalWS, weights.r));
            #endif

                // Prioridad al decal: se aplica DESPUES de mezclar base+camino, asi que
                // un Decal Projector (flores, huellas, lo que sea) se ve arriba del
                // camino pintado en vez de quedar tapado por el. Se toca tambien la
                // normal para que la iluminacion de abajo reaccione al decal.
            #if defined(_DBUFFER)
                half3 decalAlbedo = albedo;
                half3 decalNormalWS = N;
                ApplyDecalToBaseColorAndNormal(IN.positionCS, decalAlbedo, decalNormalWS);
                albedo = decalAlbedo;
                N = normalize(decalNormalWS);
            #endif

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

            // Con Decal Layers activo, el proyector descarta el pixel si el layer de la
            // superficie (escrito en este pase, SV_Target1) no coincide con el suyo. Sin
            // esta variante el piso deja el layer en 0 y el decal se recorta entero.
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
