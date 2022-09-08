Shader "Hidden/Melody RP/Post FX Stack/Motion/FrameBlending"
{
    Properties
    {
        [HideInInspector] _MainTex("", 2D) = ""{}
        [HideInInspector]_History1LumaTex("", 2D) = ""{}
        [HideInInspector]_History2LumaTex("", 2D) = ""{}
        [HideInInspector]_History3LumaTex("", 2D) = ""{}
        [HideInInspector]_History4LumaTex("", 2D) = ""{}
        [HideInInspector]_History1ChromaTex("", 2D) = ""{}
        [HideInInspector]_History2ChromaTex("", 2D) = ""{}
        [HideInInspector]_History3ChromaTex("", 2D) = ""{}
        [HideInInspector]_History4ChromaTex("", 2D) = ""{}
    }

    SubShader
    {
        Cull Off
        ZTest Always
        Zwrite Off

        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "MotionBlurPass.hlsl"
        ENDHLSL

        Pass
        {
            Name "Frame compress"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment FrameCompress
            ENDHLSL
        }

        Pass
        {
            Name "Frame blending"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MultiTexPassVertex
            #pragma fragment FrameBlending
            ENDHLSL
        }
    }
}
