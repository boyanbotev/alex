Shader "Alex/Neuron Contact Shadow"
{
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
            struct Attributes { float4 positionOS : POSITION; float3 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float strip = min(input.uv.x, 1 - input.uv.x);
                float radial = (1 - length(input.uv.xy * 2 - 1)) * 0.5;
                float edge = lerp(strip, radial, input.uv.z);
                return half4(0.04, 0.07, 0.12, smoothstep(0, 0.25, edge) * 0.18);
            }
            ENDHLSL
        }
    }
}
