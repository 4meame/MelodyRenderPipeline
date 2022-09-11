Shader "Hidden/Melody RP/StochasticSSR"
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
        #include "../ShaderLibrary/BRDF.hlsl"
        #include "StochasticSSRPass.hlsl"
        ENDHLSL

        Pass
        {
            Name ""

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment LinearTraceSingleSPP
            ENDHLSL
        }

    }
}
