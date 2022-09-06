Shader "Hidden/Melody RP/Post FX Stack/Motion/FrameBlending"
{
    Properties
    {
        [HideInInspector] _MainTex("", 2D) = ""{}
        [HideInInspector]_VelocityTex("", 2D) = ""{}
        [HideInInspector]_NeighborMaxTex("", 2D) = ""{}
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
            Name "Velocity Setup"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment VelocitySetup
            ENDHLSL
        }

        Pass
        {
            Name "Velocity Setup"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment VelocitySetup
            ENDHLSL
        }
    }
}
