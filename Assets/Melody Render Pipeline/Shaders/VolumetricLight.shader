Shader "Hidden/Melody RP/VolumetricLight"
{
    SubShader
    {
        HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "../ShaderLibrary/Surface.hlsl"
		#include "../ShaderLibrary/Shadows.hlsl"
		#include "../ShaderLibrary/Light.hlsl"
        #include "VolumetricLightPass.hlsl"
        ENDHLSL

        //pass 0
        Pass
        {
            Name "Inside Point Light"
            ZTest Off
            Cull Front
            ZWrite Off
            Blend One One
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile _ _RECEIVE_SHADOWS
            #pragma vertex DefaultPassVertex
            #pragma fragment fragPointInside
            ENDHLSL
        }

        //pass 1
        Pass
        {
            Name "Outside Point Light"
            ZTest Always
            Cull Back
            ZWrite Off
            Blend One One
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile _ _RECEIVE_SHADOWS
            #pragma vertex DefaultPassVertex
            #pragma fragment fragPointOutside
            ENDHLSL
        }

        //pass 2
        Pass
        {
            Name "Inside Spot Light"
            ZTest Off
            Cull Front
            ZWrite Off
            Blend One One
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile _ _RECEIVE_SHADOWS
            #pragma vertex DefaultPassVertex
            #pragma fragment fragSpotInside
            ENDHLSL
        }

        //pass 3
        Pass
        {
            Name "Outside Spot Light"
            ZTest Always
            Cull Off
            ZWrite Off
            Blend One One
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile _ _RECEIVE_SHADOWS
            #pragma vertex DefaultPassVertex
            #pragma fragment fragSpotOutside
            ENDHLSL
        }
    }
}
