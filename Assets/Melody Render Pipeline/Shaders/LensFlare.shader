Shader "Hidden/Melody RP/Post FX Stack/LensFlare/Common"
{
    SubShader
    {
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "LensflareCommon.hlsl"
        ENDHLSL

       //Additive
       Pass
       {
           Name "LensFlareAdditive"
           Tags{ "LightMode" = "MelodyFoward"  "RenderQueue" = "Transparent" }
           Blend One One
           ZWrite Off
           Cull Off
           ZTest Always

           HLSLPROGRAM
           #pragma target 3.5
           #pragma vertex vert
           #pragma fragment frag
           #pragma multi_compile_fragment _ FLARE_CIRCLE FLARE_POLYGON
           #pragma multi_compile_fragment _ FLARE_INVERSE_SDF
           #pragma multi_compile _ FLARE_OCCLUSION
           ENDHLSL
       }
        //Screen
        Pass
        {
            Name "LensFlareScreen"
            Tags{ "LightMode" = "MelodyFoward"  "RenderQueue" = "Transparent" }
            Blend One OneMinusSrcColor
            BlendOp Max
            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ FLARE_CIRCLE FLARE_POLYGON
            #pragma multi_compile_fragment _ FLARE_INVERSE_SDF
            #pragma multi_compile _ FLARE_OCCLUSION
            ENDHLSL
        }

        //Premultiply
        Pass
        {
            Name "LensFlarePremultiply"
            Tags{ "LightMode" = "MelodyFoward"  "RenderQueue" = "Transparent" }
            Blend One OneMinusSrcAlpha
            ColorMask RGB
            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ FLARE_CIRCLE FLARE_POLYGON
            #pragma multi_compile_fragment _ FLARE_INVERSE_SDF
            #pragma multi_compile _ FLARE_OCCLUSION
            ENDHLSL
        }

        //Lerp
        Pass
        {
            Name "LensFlareLerp"
            Tags{ "LightMode" = "MelodyFoward"  "RenderQueue" = "Transparent" }
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ FLARE_CIRCLE FLARE_POLYGON
            #pragma multi_compile_fragment _ FLARE_INVERSE_SDF
            #pragma multi_compile _ FLARE_OCCLUSION
            ENDHLSL
        }

        //OcclusionOnly
        Pass
        {
            Name "LensFlareOcclusion"
            Blend Off
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertOcclusion
            #pragma fragment fragOcclusion
            ENDHLSL
        }
    }
}
