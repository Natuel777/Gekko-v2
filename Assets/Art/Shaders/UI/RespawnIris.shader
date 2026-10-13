Shader "Custom/UI/RespawnIris"
{
    Properties
    {
        // Unity's UI (Image/CanvasRenderer) espera que cualquier material de Graphic tenga
        // _MainTex, aunque no lo usemos; sin esto tira error en cada render del Canvas.
        _MainTex ("Texture (no usada)", 2D) = "white" {}
        _Center ("Centro (UV de pantalla)", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radio (0 = cerrado, ~1.5 = abierto)", Float) = 1.5
        _Softness ("Suavidad del borde", Range(0.0001, 0.2)) = 0.01
        _RimColor ("Color del borde", Color) = (0.498, 0.965, 1, 1)
        _RimWidth ("Ancho del borde", Range(0.0001, 0.05)) = 0.008
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _Center;
            float _Radius;
            float _Softness;
            half4 _RimColor;
            float _RimWidth;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float2 p = i.uv - _Center.xy;
                p.x *= aspect;
                float dist = length(p);

                // 1 = adentro del agujero (transparente), 0 = afuera (negro opaco).
                float insideHole = smoothstep(_Radius, _Radius - _Softness, dist);
                half alpha = 1.0 - insideHole;

                // Anillo turquesa justo en el borde del agujero.
                float rim = 1.0 - saturate(abs(dist - _Radius) / max(_RimWidth, 0.0001));
                rim = smoothstep(0.0, 1.0, rim);

                half3 col = lerp(half3(0, 0, 0), _RimColor.rgb, rim);
                alpha = max(alpha, rim * _RimColor.a);

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
