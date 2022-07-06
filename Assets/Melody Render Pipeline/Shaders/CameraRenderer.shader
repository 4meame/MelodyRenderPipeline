Shader "Hidden/Melody RP/Camera Renderer"
{
    SubShader
    {
        Cull Off
        ZTest Always
        Zwrite Off

        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "CameraRendererPasses.hlsl"
        ENDHLSL

        Pass
        {
            Name "Copy"

            Blend [_CameraSrcBlend] [_CameraDstBlend]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment CopyPassFragment
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
            #pragma fragment CopyDepthPassFragment
            ENDHLSL
        }
    }
}
