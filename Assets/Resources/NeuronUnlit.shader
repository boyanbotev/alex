Shader "Game/Neuron Frosted Unlit"
{
    Properties
    {
        [Header(Colour and Texture)]
        [MainTexture] _BaseMap ("Grain Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Colour", Color) = (0.7,0.85,1,1)
        _Brightness ("Brightness", Range(0,3)) = 1
        [Enum(Alpha,10,Additive,1)] _DstBlend ("Blending", Float) = 10
        _GrainScale ("Grain Scale", Float) = 0.7
        _GrainStrength ("Grain Strength", Range(0,1)) = 0.35
        _GrainGain ("Grain Brightness", Range(0,4)) = 2
        _GrainContrast ("Grain Contrast", Range(0,4)) = 1
        [Header(Frosted Edges)]
        _EdgeFrost ("Edge Frost", Range(0,0.25)) = 0.12
        _FrostScale ("Frost Frequency (per world unit)", Float) = 22.4
        _EdgeSoftness ("Edge Softness", Range(0.001,0.2)) = 0.025
        [Header(Directional Shading)]
        _Depth ("Rounded Shading", Range(0,1)) = 0.75
        _Roundness ("Cross Section Roundness", Range(0.05,2)) = 1
        _LightDirection ("Light Direction (World XYZ)", Vector) = (-0.5,0.8,-0.4,0)
        _Ambient ("Shaded Side Brightness", Range(0,1)) = 0.5
        _Diffuse ("Directional Light Strength", Range(0,2)) = 0.65
        _HighlightStrength ("Highlight Strength", Range(0,1)) = 0
        _HighlightSharpness ("Highlight Sharpness", Range(1,64)) = 20
        _HighlightColor ("Highlight Colour", Color) = (1,1,1,1)
        [Header(Geometry Requires Mesh Refresh or Play Restart)]
        _WidthScale ("Width Multiplier", Range(0.1,3)) = 1
        _CurveAmount ("Curve Amount", Range(0,0.1)) = 0.045
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha [_DstBlend], One OneMinusSrcAlpha
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
                half _Brightness;
                float _DstBlend;
                half _GrainGain;
                half _GrainContrast;
                float _FrostScale;
                float _EdgeSoftness;
                float _Roundness;
                float4 _LightDirection;
                half _Ambient;
                half _Diffuse;
                half _HighlightStrength;
                float _HighlightSharpness;
                half4 _HighlightColor;
                float _WidthScale;
                float _CurveAmount;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 uv : TEXCOORD0; float3 across : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 grainUV : TEXCOORD0; float3 profile : TEXCOORD1; float3 across : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.grainUV = world.xz;
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
                float2 uv = 1 - abs(frac(input.grainUV * _GrainScale * 0.5) * 2 - 1);
                uv = TRANSFORM_TEX(uv, _BaseMap);
                half3 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                half grain = dot(texel, half3(0.2126,0.7152,0.0722)) * _GrainGain;
                grain = saturate((grain - 0.5) * _GrainContrast + 0.5);
                half textureValue = lerp(1, grain, _GrainStrength);
                float stripEdge = min(input.profile.x, 1 - input.profile.x);
                float roundEdge = (1 - length(input.profile.xy * 2 - 1)) * 0.5;
                float edge = lerp(stripEdge, roundEdge, input.profile.z);
                // Shared world-space frost keeps the silhouette irregular without tile seams.
                float frost = FrostNoise(input.grainUV * max(_FrostScale, 0.001));
                edge -= frost * _EdgeFrost;
                float feather = max(fwidth(edge), _EdgeSoftness);
                half alpha = smoothstep(0, feather, edge) * _BaseColor.a;
                // A virtual rounded cross-section, lit consistently in world space.
                float2 radial = input.profile.xy * 2 - 1;
                float3 sideways = lerp(input.across * radial.x,
                    float3(radial.x, 0, radial.y), input.profile.z);
                float3 normal = normalize(sideways * _Roundness + float3(0, sqrt(saturate(1 - dot(sideways, sideways))), 0));
                float3 light = _LightDirection.xyz / max(length(_LightDirection.xyz), 0.001);
                half diffuse = saturate(dot(normal, light));
                half shading = lerp(1, _Ambient + diffuse * _Diffuse, _Depth);
                half3 highlight = _HighlightColor.rgb * pow(diffuse, _HighlightSharpness) * _HighlightStrength * _Depth;
                return half4((_BaseColor.rgb * shading + highlight) * textureValue * _Brightness, alpha);
            }
            ENDHLSL
        }
    }
}
