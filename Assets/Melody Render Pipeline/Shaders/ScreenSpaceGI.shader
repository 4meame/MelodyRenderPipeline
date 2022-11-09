Shader "Hidden/Melody RP/ScreenSpaceGlobalIllumination"
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
        #include "ScreenSpaceGIPass.hlsl"
        ENDHLSL

        Pass
        {
            Name "Prepare Hierarchical Z"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment GetHierarchicalZBuffer
            ENDHLSL
        }


        Pass 
        {
            Name "Linear Trace GI"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment GlobalIlluminationLinearTrace
            ENDHLSL
        }

        Pass
        {
            Name "Hiz Trace GI"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment GlobalIlluminationHierarchicalZ
            ENDHLSL
        }

        Pass 
        {
            Name "Spatio Brdf"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BrdfWeightFilter
            ENDHLSL
        }

            Pass
        {
            Name "Spatio Normal Bilateral X"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment NormalBilateralFilterX
            ENDHLSL
        }

        Pass
        {
            Name "Spatio Normal Bilateral Y"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment NormalBilateralFilterY
            ENDHLSL
        }

        Pass
        {
            Name "Spatio Adaptive Bilateral X"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment AdaptiveBilateralFilterX
            ENDHLSL
        }

        Pass
        {
            Name "Spatio Adaptive Bilateral Y"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment AdaptiveBilateralFilterY
            ENDHLSL
        }

        Pass
        {
            Name "Temporal Filter"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment TemporalFilter
            ENDHLSL
        }

        Pass
        {
            Name "Combine Scene"

            //note: DO NOT use HLSLINCLUDE
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment CombineGlobalIllumination
            ENDHLSL
        }
    }
}
