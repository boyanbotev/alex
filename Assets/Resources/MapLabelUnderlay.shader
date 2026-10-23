Shader "Game/Map Label Underlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Size ("World Size", Vector) = (1, 1, 0, 0)
        _CornerRadius ("Corner Radius", Float) = 0.035
        _EdgeSoftness ("Edge Softness", Float) = 0.015
    }
    SubShader
    {
        Tags { "Queue"="Overlay-1" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };
            float4 _Size;
            float _CornerRadius;
            float _EdgeSoftness;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                float2 halfSize = _Size.xy * 0.5;
                float radius = clamp(_CornerRadius, 0, min(halfSize.x, halfSize.y));
                float2 q = abs((input.uv - 0.5) * _Size.xy) - halfSize + radius;
                float distance = length(max(q, 0)) + min(max(q.x, q.y), 0) - radius;
                // Fade inward, keeping the underlay inside its existing footprint.
                float softness = max(_EdgeSoftness, max(fwidth(distance), 0.00001));
                fixed4 color = input.color;
                color.a *= 1 - smoothstep(-softness, 0, distance);
                return color;
            }
            ENDCG
        }
    }
}
