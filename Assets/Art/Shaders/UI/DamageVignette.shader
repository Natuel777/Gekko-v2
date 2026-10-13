Shader "Custom/UI/DamageVignette"
{
    Properties
    {
        // Unity's UI (Image/CanvasRenderer) espera que cualquier material de Graphic tenga
        // _MainTex, aunque no lo usemos; sin esto tira error en cada render del Canvas.
        _MainTex ("Texture (no usada)", 2D) = "white" {}
        _Color ("Color", Color) = (0.667, 0.196, 0.941, 0.85)
        _Intensity ("Intensidad (0-1)", Range(0, 1)) = 0
        _Radius ("Radio (donde empieza el borde)", Range(0, 1)) = 0.55
        _Softness ("Suavidad", Range(0.01, 1)) = 0.35
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
            half4 _Color;
            float _Intensity;
            float _Radius;
            float _Softness;

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
                float2 p = i.uv - 0.5;
                p.x *= aspect;
                float dist = length(p);

                float edge = smoothstep(_Radius, _Radius + _Softness, dist);
                half4 col = _Color;
                col.a *= edge * _Intensity;
                return col;
            }
            ENDHLSL
        }
    }
}
