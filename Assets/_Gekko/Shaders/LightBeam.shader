// Haz de luz vertical (aditivo) que marca los GrapplePoint y Gancho de lejos (ver GrappleHighlight).
// La malla es un quad de 0..1 en Y y -0.5..0.5 en X: en el vértice se orienta hacia la cámara girando solo alrededor del eje
// vertical (billboard cilíndrico), así de cualquier lado se ve como una columna. Se desvanece hacia arriba y en los costados,
// y desaparece cuando la cámara está cerca (_NearFade) para no tapar la vista al engancharse.
Shader "Gekko/FX/Light Beam"
{
    Properties
    {
        [HDR] _Color     ("Color", Color) = (1, 0.8, 0.3, 1)
        _Intensity       ("Intensidad", Float) = 1
        _Height          ("Alto (m)", Float) = 6
        _Width           ("Ancho (m)", Float) = 0.6
        _NearFadeStart   ("Empieza a desaparecer a (m)", Float) = 6
        _NearFadeEnd     ("Desaparece del todo a (m)", Float) = 2.5
        _Flicker         ("Ondulación", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Beam"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex BeamVertex
            #pragma fragment BeamFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Intensity;
                float  _Height;
                float  _Width;
                float  _NearFadeStart;
                float  _NearFadeEnd;
                float  _Flicker;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float  nearFade   : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings BeamVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 baseWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 toCam = _WorldSpaceCameraPos - baseWS;
                toCam.y = 0;
                float3 right = normalize(cross(float3(0, 1, 0), toCam + float3(0.0001, 0, 0)));

                float3 positionWS = baseWS + right * (IN.positionOS.x * _Width) + float3(0, IN.positionOS.y * _Height, 0);
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.uv = IN.uv;

                float dist = distance(_WorldSpaceCameraPos, baseWS);
                OUT.nearFade = saturate((dist - _NearFadeEnd) / max(0.01, _NearFadeStart - _NearFadeEnd));
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 BeamFragment(Varyings IN) : SV_Target
            {
                // Centro brillante y bordes suaves; arranca fuerte abajo y se apaga hacia arriba.
                float across = 1.0 - abs(IN.uv.x * 2.0 - 1.0);
                across = across * across;
                float along = pow(saturate(1.0 - IN.uv.y), 1.6) * saturate(IN.uv.y * 12.0);

                // Ondulación lenta que sube, para que no se vea como un cartel quieto.
                float wave = 1.0 - _Flicker + _Flicker * (0.5 + 0.5 * sin(IN.uv.y * 9.0 - _Time.y * 2.2));

                float a = across * along * wave * IN.nearFade * _Intensity;
                half3 color = _Color.rgb * a;
                color = MixFogColor(color, half3(0, 0, 0), IN.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
