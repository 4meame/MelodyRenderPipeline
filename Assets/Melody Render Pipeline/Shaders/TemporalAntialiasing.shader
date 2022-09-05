Shader "Hidden/Melody RP/TemporalAntialiasing"
{
    SubShader
    {
        Cull Off
        ZTest Always
        Zwrite Off

        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "PostFXStackPasses.hlsl"
        #include "TemporalAntialiasingPass.hlsl"
        ENDHLSL

        Pass
        {
            Name "Adaptive TemporalAntialiasing"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment AdaptiveTemporalAntialiasing
            ENDHLSL
        }


        Pass
        {
            Name "Copy Depth"

            ColorMask 0
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment CopyDepthFragment
            ENDHLSL
        }

        Pass
        {
            Name "Copy Motion Vector"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment CopyMotionVectorFragment
            ENDHLSL
        }
    }
}
