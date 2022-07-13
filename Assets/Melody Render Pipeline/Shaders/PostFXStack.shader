Shader "Hidden/Melody RP/Post FX Stack"
{
    SubShader
    {
        Cull Off
        ZTest Always
        Zwrite Off

        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "PostFXStackPasses.hlsl"
        ENDHLSL

        Pass
        {
            Name "Copy"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment CopyPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Horizontal"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomHorizontalPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Vertical"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomVerticalPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Combine Additive"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomCombineAdditivePassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Combine Scatter"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomCombineScatterPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Combine Scatter Final"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomScatterFinalPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Prefilter"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomPrefilterPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Prefilter Fireflies"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomPrefilterFirefliesPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "ColorGrading None"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment ColorGradingNonePassFragment
            ENDHLSL
        }

        Pass
        {
            Name "ColorGrading Reinhard"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment ColorGradingReinhardPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "ColorGrading Neutral"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment ColorGradingNeutralPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "ColorGrading ACES"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment ColorGradingACESPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Final ColorGrading"

            Blend [_FinalSrcBlend] [_FinalDstBlend]

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment FinalColorGradingPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Render Rescale"

            Blend[_FinalSrcBlend][_FinalDstBlend]

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment FinalPassFragmentRescale
            ENDHLSL
        }

        Pass
        {
            Name "FXAA"

            Blend[_FinalSrcBlend][_FinalDstBlend]

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment FXAAPassFragment
            #include "FXAAPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Color Grading With Luma"

            Blend[_FinalSrcBlend][_FinalDstBlend]

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment ColorGradingWithLumaPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "FXAA With Luma"

            Blend[_FinalSrcBlend][_FinalDstBlend]

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment FXAAPassFragment
            #pragma multi_compile FXAA_ALPHA_CONTAINS _ FXAA_QUALITY_MEDIUM FXAA_QUALITY_LOW
            #define FXAA_ALPHA_CONTAINS_LUMA
            #include "FXAAPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Outline"

            Blend[_FinalSrcBlend][_FinalDstBlend]

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment OutlinePassFragment
            #include "OutlinePass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "LightShafts Prefilter"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment LightShaftsPrefilterPassFragment
            #include "LightShaftsPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "LightShafts Blur"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment LightShaftsBlurFragment
            #include "LightShaftsPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "LightShafts Blend"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment LightShaftsBlendFragment
            #include "LightShaftsPass.hlsl"
            ENDHLSL
        }
    }
}
