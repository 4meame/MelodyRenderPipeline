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
        ENDHLSL

        Pass
        {
            Name "TemporalAntialiasing"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment TemporalAntialiasingResolve
            #include "TemporalAntialiasingPass.hlsl"
            ENDHLSL
        }
    }
}
