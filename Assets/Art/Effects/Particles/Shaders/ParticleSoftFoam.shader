// Partícula unlit alpha-blended con Depth Fade (soft particles) para la espuma de cascadas.
// - Depth Fade: desvanece la partícula donde toca geometría opaca (usa _CameraDepthTexture,
//   requiere "Depth Texture" activado en el URP Asset).
// - Water Height Fade: el agua del río es transparente (ZWrite Off) y NO aparece en el depth
//   texture, así que también se puede desvanecer por altura de mundo respecto al nivel del agua.
Shader "Gekko/Particles/Soft Foam"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [HDR] _Color ("Tint", Color) = (1,1,1,1)

        [Header(Depth Fade)]
        [Toggle(_DEPTH_FADE)] _DepthFadeEnabled ("Enable Depth Fade", Float) = 1
        _DepthFadeDistance ("Fade Distance", Range(0.01, 5)) = 0.8
        _DepthFadePower ("Fade Curve (Power)", Range(0.25, 4)) = 1.5

        [Header(Water Height Fade)]
        [Toggle(_WATER_FADE)] _WaterFadeEnabled ("Enable Water Height Fade", Float) = 0
        _WaterHeight ("Water Height (World Y)", Float) = 0
        _WaterFadeDistance ("Fade Distance", Range(0.01, 5)) = 0.6

        [Header(Camera Fade)]
        [Toggle(_CAMERA_FADE)] _CameraFadeEnabled ("Enable Camera Near Fade", Float) = 0
        _CameraFadeNear ("Near", Float) = 0.5
        _CameraFadeFar ("Far", Float) = 2

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _DEPTH_FADE
            #pragma shader_feature_local _WATER_FADE
            #pragma shader_feature_local _CAMERA_FADE
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _DepthFadeDistance;
                float _DepthFadePower;
                float _WaterHeight;
                float _WaterFadeDistance;
                float _CameraFadeNear;
                float _CameraFadeFar;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.screenPos = ComputeScreenPos(pos.positionCS);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color * i.color;

                // Profundidad lineal (en unidades de mundo) del fragmento de la partícula.
                float fragEye = LinearEyeDepth(i.positionWS, GetWorldToViewMatrix());

                #if defined(_DEPTH_FADE)
                {
                    float2 uv = i.screenPos.xy / i.screenPos.w;
                    float rawDepth = SampleSceneDepth(uv);
                    float sceneEye = (unity_OrthoParams.w == 0)
                        ? LinearEyeDepth(rawDepth, _ZBufferParams)
                        : lerp(_ProjectionParams.y, _ProjectionParams.z,
                               #if UNITY_REVERSED_Z
                                   1.0 - rawDepth
                               #else
                                   rawDepth
                               #endif
                          );
                    float fade = saturate((sceneEye - fragEye) / _DepthFadeDistance);
                    col.a *= pow(fade, _DepthFadePower);
                }
                #endif

                #if defined(_WATER_FADE)
                {
                    float fade = saturate((i.positionWS.y - _WaterHeight) / _WaterFadeDistance);
                    col.a *= smoothstep(0.0, 1.0, fade);
                }
                #endif

                #if defined(_CAMERA_FADE)
                {
                    col.a *= saturate((fragEye - _CameraFadeNear) / max(_CameraFadeFar - _CameraFadeNear, 0.0001));
                }
                #endif

                col.rgb = MixFog(col.rgb, i.fogFactor);
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
