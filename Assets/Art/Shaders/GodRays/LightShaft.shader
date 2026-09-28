Shader "Gekko/LightShaft"
{
    // Haz de luz volumetrico falso (god ray). Se usa sobre un Quad estirado:
    //  - local Y = eje del haz (largo), local X = ancho
    //  - el quad rota alrededor de su eje para mirar siempre a la camara (billboard cilindrico)
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.85, 0.5, 1)
        _Intensity ("Intensity", Range(0, 4)) = 0.35
        _EdgeSoftness ("Edge Softness", Range(0.2, 4)) = 1.5
        _TopFade ("Top Fade", Range(0.01, 0.6)) = 0.2
        _BottomFade ("Bottom Fade", Range(0.05, 1)) = 0.7
        _NoiseScale ("Streak Scale", Float) = 6
        _NoiseSpeed ("Streak Speed", Float) = 0.15
        _NoiseStrength ("Streak Strength", Range(0, 1)) = 0.6
        _SoftDistance ("Depth Softness", Float) = 3
        _CamFadeStart ("Camera Fade Start", Float) = 4
        _CamFadeEnd ("Camera Fade End", Float) = 14
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

            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _EdgeSoftness;
                float _TopFade;
                float _BottomFade;
                float _NoiseScale;
                float _NoiseSpeed;
                float _NoiseStrength;
                float _SoftDistance;
                float _CamFadeStart;
                float _CamFadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float seed : TEXCOORD2;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float4x4 m = GetObjectToWorldMatrix();
                float3 origin = float3(m._m03, m._m13, m._m23);
                float3 axisFull = float3(m._m01, m._m11, m._m21);
                float len = length(axisFull);
                float3 axis = axisFull / max(len, 1e-5);
                float width = length(float3(m._m00, m._m10, m._m20));

                // punto sobre el eje del haz para este vertice, y vector "derecha" que mira a camara
                float3 center = origin + axis * (IN.positionOS.y * len);
                float3 viewDir = _WorldSpaceCameraPos - center;
                float3 right = cross(axis, viewDir);
                right = dot(right, right) < 1e-6 ? float3(1, 0, 0) : normalize(right);

                float3 positionWS = center + right * (IN.positionOS.x * width);

                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.uv = IN.positionOS.xy + 0.5; // el Quad de Unity va de -0.5 a 0.5
                OUT.seed = frac(sin(dot(origin.xz + origin.y, float2(12.9898, 78.233))) * 43758.5453);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = IN.uv.y; // 0 = abajo, 1 = arriba (lado del sol)
                float across = abs(IN.uv.x * 2.0 - 1.0);

                float edge = pow(smoothstep(0.0, 1.0, saturate(1.0 - across)), _EdgeSoftness);
                float topFade = 1.0 - smoothstep(1.0 - _TopFade, 1.0, t);
                float bottomFade = smoothstep(0.0, _BottomFade, t);

                // estrias que se desplazan lentamente + respiracion de cada haz
                float n = ValueNoise(float2(IN.uv.x * _NoiseScale + IN.seed * 50.0, t * 1.5 + _Time.y * _NoiseSpeed));
                float streak = lerp(1.0, n * 1.6, _NoiseStrength);
                float breathe = 0.75 + 0.25 * sin(_Time.y * 0.5 + IN.seed * 6.2831);

                // soft: se desvanece donde el haz toca geometria (arboles, suelo)
                float2 screenUV = IN.positionCS.xy / _ScaledScreenParams.xy;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float soft = saturate((sceneDepth - IN.positionCS.w) / max(_SoftDistance, 1e-3));

                // se desvanece cerca de la camara para evitar cortes bruscos
                float camDist = distance(_WorldSpaceCameraPos, IN.positionWS);
                float camFade = saturate((camDist - _CamFadeStart) / max(_CamFadeEnd - _CamFadeStart, 1e-3));

                float alpha = edge * topFade * bottomFade * streak * breathe * soft * camFade * _Intensity * _Color.a;
                return half4(_Color.rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
