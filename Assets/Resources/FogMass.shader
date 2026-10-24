Shader "Game/Fog Mass"
{
    Properties
    {
        [MainTexture] _BaseMap ("Cloud Artwork", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        _CreamColor ("Interior Cream", Color) = (0.91,0.89,0.83,1)
        _OutlineBand ("Artwork Edge Band", Range(0,1)) = 0.18
        _OutlineFade ("Artwork Fade Width", Range(0,1)) = 0.12
        [HideInInspector] _FogBounds ("World XZ Bounds", Vector) = (0,0,1,1)
        [HideInInspector] _FogSides ("Covered W E S N", Vector) = (0,0,0,0)
        [HideInInspector] _FogCorners ("Covered SW SE NW NE", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+50" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
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
                half4 _CreamColor;
                float _OutlineBand;
                float _OutlineFade;
                float4 _FogBounds;
                float4 _FogSides;
                float4 _FogCorners;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 worldXZ : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.worldXZ = world.xz;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = saturate((input.worldXZ - _FogBounds.xy) / max(_FogBounds.zw, 0.0001));
                float4 edges = float4(p.x, 1-p.x, p.y, 1-p.y);
                // Covered edges disappear; exposed edges retain the original cloud artwork.
                float4 exposed = lerp(edges, 2.0, _FogSides);
                float distance = min(min(exposed.x, exposed.y), min(exposed.z, exposed.w));
                // Retain the contour around concave corners as well as straight borders.
                float4 corners = float4(max(p.x,p.y), max(1-p.x,p.y),
                    max(p.x,1-p.y), max(1-p.x,1-p.y));
                corners = lerp(corners, 2.0, _FogCorners);
                distance = min(distance, min(min(corners.x,corners.y), min(corners.z,corners.w)));
                half interior = smoothstep(_OutlineBand, _OutlineBand + max(_OutlineFade, 0.0001), distance);
                half4 artwork = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                // The source cloud is solid within this inset. Fill shared tile gaps only
                // inside that safe core, independently of the adjustable artwork fade.
                // Exposed scallops and transparent corners retain their original alpha.
                half fill = smoothstep(0.24, 0.30, distance) * _CreamColor.a;
                half addedAlpha = fill * (1 - artwork.a);
                half alpha = artwork.a + addedAlpha;
                // Premultiplied colour prevents invisible texture RGB bleeding into cream.
                half3 colour = lerp(artwork.rgb, _CreamColor.rgb, interior) * artwork.a
                    + _CreamColor.rgb * addedAlpha;
                return half4(colour * _BaseColor.rgb * _BaseColor.a, alpha * _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
