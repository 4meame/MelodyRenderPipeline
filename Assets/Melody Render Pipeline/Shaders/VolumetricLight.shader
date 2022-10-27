Shader "Hidden/Melody RP/VolumetricLight"
{
    SubShader
    {
        Cull Off
        ZTest Always
        Zwrite Off

        HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "../ShaderLibrary/Surface.hlsl"
		#include "../ShaderLibrary/Shadows.hlsl"
		#include "../ShaderLibrary/Light.hlsl"
        #include "VolumetricLightPass.hlsl"
        ENDHLSL

        Pass
        {
            Name "Test"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment TestFragment
            ENDHLSL
        }

    }
}
