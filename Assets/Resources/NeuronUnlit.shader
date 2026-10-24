Shader "Alex/Neuron Frosted Unlit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Grain Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Colour", Color) = (0.7,0.85,1,1)
        _GrainScale ("Grain Scale", Float) = 0.7
        _GrainStrength ("Grain Strength", Range(0,1)) = 0.35
        _EdgeFrost ("Edge Frost", Range(0,0.25)) = 0.12
        _Depth ("Rounded Shading", Range(0,1)) = 0.75
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
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
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _GrainScale;
                half _GrainStrength;
                float _EdgeFrost;
                half _Depth;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 uv : TEXCOORD0; float3 across : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 grainUV : TEXCOORD0; float3 profile : TEXCOORD1; float3 across : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.grainUV = world.xz * _GrainScale;
                output.profile = input.uv;
                output.across = input.across;
                return output;
            }
            float FrostNoise(float2 position)
            {
                float2 cell = floor(position);
                float2 fraction = frac(position);
                fraction = fraction * fraction * (3 - 2 * fraction);
                float4 hashes = frac(sin(float4(dot(cell, float2(127.1,311.7)),
                    dot(cell + float2(1,0), float2(127.1,311.7)),
                    dot(cell + float2(0,1), float2(127.1,311.7)),
                    dot(cell + 1, float2(127.1,311.7)))) * 43758.5453);
                return lerp(lerp(hashes.x, hashes.y, fraction.x),
                    lerp(hashes.z, hashes.w, fraction.x), fraction.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Mirror a patch of fine tissue detail, avoiding the silhouette and yellow centre.
                float2 uv = 1 - abs(frac(input.grainUV * 0.5) * 2 - 1);
                uv = TRANSFORM_TEX(uv, _BaseMap);
                half3 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                half grain = saturate(dot(texel, half3(0.2126,0.7152,0.0722)) * 2);
                half textureValue = lerp(1, grain, _GrainStrength);
                float stripEdge = min(input.profile.x, 1 - input.profile.x);
                float roundEdge = (1 - length(input.profile.xy * 2 - 1)) * 0.5;
                float edge = lerp(stripEdge, roundEdge, input.profile.z);
                // Shared world-space frost keeps the silhouette irregular without tile seams.
                float frost = FrostNoise(input.grainUV * 32);
                edge -= frost * _EdgeFrost;
                float feather = max(fwidth(edge), 0.025);
                half alpha = smoothstep(0, feather, edge) * _BaseColor.a;
                // A virtual rounded cross-section, lit consistently in world space.
                float2 radial = input.profile.xy * 2 - 1;
                float3 sideways = lerp(input.across * radial.x,
                    float3(radial.x, 0, radial.y), input.profile.z);
                float3 normal = normalize(sideways + float3(0, sqrt(saturate(1 - dot(sideways, sideways))), 0));
                half diffuse = saturate(dot(normal, normalize(float3(-0.5, 0.8, -0.4))));
                half shading = lerp(1, 0.5 + diffuse * 0.65, _Depth);
                return half4(_BaseColor.rgb * textureValue * shading, alpha);
            }
            ENDHLSL
        }
    }
}
