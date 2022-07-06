Shader "Hidden/Melody RP/VolumetricCloud/TextureBrush"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
    }
        SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Assets/Melody Render Pipeline/ShaderLibrary/Common.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BrushTexture);
            SAMPLER(sampler_BrushTexture);
            float _BrushTextureAlpha;
            float _CoverageOpacity;
            float _TypeOpacity;
            float _ShouldDrawCoverage;
            float _ShouldDrawType;
            float _ShouldBlendValues;

            struct Attributes {
                float4 vertex : POSITION;
                float2 uv1 : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings {
                float4 vertex : SV_POSITION;
                float2 uv1 : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            Varyings vert(Attributes input) {
                Varyings output;
                output.vertex = TransformObjectToHClip(input.vertex);
                output.uv1 = input.uv1;
                output.uv2 = input.uv2;
                return output;
            }

            float4 frag(Varyings input) : SV_Target {
                float4 background = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv2);
                float brush = lerp(1.0, SAMPLE_TEXTURE2D(_BrushTexture, sampler_BrushTexture, input.uv1).r, _BrushTextureAlpha);
                float a = 1.0 - min(1.0, length(input.uv1 * 2.0 - 1.0));
                a *= brush;

                float4 result = background;
                UNITY_BRANCH
                if (_ShouldBlendValues == 1.0) {
                    result.r = saturate(result.r + a * _CoverageOpacity * _ShouldDrawCoverage);
                    result.b = saturate(result.b + a * _TypeOpacity * _ShouldDrawType);
                }
                else {
                result.r = lerp(result.r, _CoverageOpacity, a * _ShouldDrawCoverage);
                result.b = lerp(result.b, _TypeOpacity, a * _ShouldDrawType);
                }

                return result;
            }
            ENDHLSL
        }
    }
}
