Shader "Hidden/Melody RP/Post FX Stack/Motion/Reconstruction"
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
            Name "Velocity setup"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment VelocitySetup
            ENDHLSL
        }

        Pass
        {
            Name "TileMax filter with normalization"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment TileMax1
            ENDHLSL
        }

        Pass
        {
            Name "TileMax filter"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment TileMax2
            ENDHLSL
        }

        Pass
        {
            Name "TileMax filter virable"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment TileMaxV
            ENDHLSL
        }

        Pass
        {
            Name "NeighborMax filter"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment NeighborMax
            ENDHLSL
        }

        Pass
        {
            Name "Reconstruction"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MultiTexPassVertex
            #pragma fragment Reconstruction
            ENDHLSL
        }
    }
}
