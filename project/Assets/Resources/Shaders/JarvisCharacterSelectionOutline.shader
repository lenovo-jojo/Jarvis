Shader "Jarvis/Character Selection Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.42, 0.68, 0.82)
        _OutlineWidth ("Outline Width", Range(0.001, 0.04)) = 0.012
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "CharacterSelectionMask"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            ColorMask 0
            Stencil
            {
                Ref 37
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vertMask
            #pragma fragment fragMask

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct MaskAttributes
            {
                float4 positionOS : POSITION;
            };

            struct MaskVaryings
            {
                float4 positionHCS : SV_POSITION;
            };

            MaskVaryings vertMask(MaskAttributes input)
            {
                MaskVaryings output;
                output.positionHCS =
                    TransformObjectToHClip(
                        input.positionOS.xyz);
                return output;
            }

            half4 fragMask(MaskVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "CharacterSelectionOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil
            {
                Ref 37
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 expanded =
                    input.positionOS.xyz +
                    normalize(input.normalOS) *
                    _OutlineWidth;
                output.positionHCS =
                    TransformObjectToHClip(expanded);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
