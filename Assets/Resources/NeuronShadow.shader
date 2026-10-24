Shader "Game/Neuron Contact Shadow"
{
    Properties
    {
        _ShadowColor ("Shadow Colour and Opacity", Color) = (0.04,0.07,0.12,0.18)
        _ShadowOffset ("World Offset XYZ (clamped to 0.1)", Vector) = (0.025,-0.015,-0.025,0)
        _ShadowSoftness ("Shadow Softness", Range(0.01,0.5)) = 0.25
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-1" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
                float4 _ShadowOffset;
                float _ShadowSoftness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                world += clamp(_ShadowOffset.xyz, -0.1, 0.1);
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float strip = min(input.uv.x, 1 - input.uv.x);
                float radial = (1 - length(input.uv.xy * 2 - 1)) * 0.5;
                float edge = lerp(strip, radial, input.uv.z);
                return half4(_ShadowColor.rgb, smoothstep(0, _ShadowSoftness, edge) * _ShadowColor.a);
            }
            ENDHLSL
        }
    }
}
