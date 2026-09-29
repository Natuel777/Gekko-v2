Shader "Gekko/SoftParticle"
{
    // Particula circular suave y procedural (sin textura). Sirve para polvo/polen (aditivo)
    // y para bruma baja (alpha blend) cambiando _SrcBlend/_DstBlend en el material.
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _Falloff ("Falloff", Range(0.2, 6)) = 1.5
        _SoftDistance ("Depth Softness", Float) = 2
        _CamFadeStart ("Camera Fade Start", Float) = 1
        _CamFadeEnd ("Camera Fade End", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Falloff;
                float _SoftDistance;
                float _CamFadeStart;
                float _CamFadeEnd;
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
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.color = IN.color;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float d = length(IN.uv * 2.0 - 1.0);
                float shape = pow(smoothstep(0.0, 1.0, saturate(1.0 - d)), _Falloff);

                float camDist = distance(_WorldSpaceCameraPos, IN.positionWS);
                float camFade = saturate((camDist - _CamFadeStart) / max(_CamFadeEnd - _CamFadeStart, 1e-3));

                // El sprite es un quad pero la forma es un circulo (shape): las 4
                // esquinas (~21% del area) ya dan alpha 0 sin tocar la textura de
                // depth. Con decenas de particulas grandes superpuestas (la niebla
                // llega a blades de 24 unidades) esa lectura de depth es lo mas caro
                // del shader, asi que la saltamos donde ya sabemos que no se ve.
                half4 col = IN.color * _Color;
                float cheapAlpha = col.a * shape * camFade;
                clip(cheapAlpha - 0.003);

                float2 screenUV = IN.positionCS.xy / _ScaledScreenParams.xy;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float soft = saturate((sceneDepth - IN.positionCS.w) / max(_SoftDistance, 1e-3));

                col.a = saturate(cheapAlpha * soft);
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
