Shader "JARVIS/Possum Springs Vertex Color"
{
    Properties
    {
        [MainTexture] _BaseMap ("texture", 2D) = "white" {}
        [MainColor] _BaseColor ("color", Color) = (1, 1, 1, 1)
        _Cutoff ("alpha cutoff", Range(0, 1)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }
        Cull Off
        ZWrite On

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : COLOR;
                half fogFactor : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.normalWS =
                    TransformObjectToWorldNormal(input.normalOS);
                output.uv =
                    TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                output.fogFactor =
                    ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        input.uv) *
                    _BaseColor *
                    input.color;
                clip(color.a - _Cutoff);

                Light mainLight = GetMainLight();
                half diffuse =
                    saturate(
                        dot(
                            normalize(input.normalWS),
                            mainLight.direction));
                color.rgb *=
                    (0.45h + diffuse * 0.55h) *
                    mainLight.color;
                color.rgb =
                    MixFog(
                        color.rgb,
                        input.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }
}
