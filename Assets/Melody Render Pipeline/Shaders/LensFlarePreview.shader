Shader "Hidden/Melody RP/Post FX Stack/LensFlare/Preview"
{
    //NOTE: For UI as we don't have command buffer for UI we need to have one shader per usage instead of permutation like for rendering
    //keep the order as the same order of SRPLensFlareType
    SubShader
    {
        Blend One One
        ZWrite Off
        Cull Off
        ZTest Always

        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "LensflareCommon.hlsl"
        ENDHLSL

        //Image
        Pass
        {
            Name "Image"
            Tags{ "LightMode" = "MelodyUnlit"  "RenderQueue" = "Transparent" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #define FLARE_PREVIEW
            ENDHLSL
        }

        //Circle
        Pass
        {
            Name "Circle"
            Tags{ "LightMode" = "MelodyUnlit"  "RenderQueue" = "Transparent" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #define FLARE_PREVIEW
            #define FLARE_CIRCLE
            ENDHLSL
        }

        //Polygon
        Pass
        {
            Name "Polygon"
            Tags{ "LightMode" = "MelodyUnlit"  "RenderQueue" = "Transparent" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #define FLARE_PREVIEW
            #define FLARE_POLYGON
            ENDHLSL
        }

        //Circle Inverse
        Pass
        {
            Name "Circle Inverse"
            Tags{ "LightMode" = "MelodyUnlit"  "RenderQueue" = "Transparent" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #define FLARE_PREVIEW
            #define FLARE_CIRCLE
            #define FLARE_INVERSE_SDF
            ENDHLSL
        }

        //Polygon Inverse
        Pass
        {
            Name "Polygon Inverse"
            Tags{ "LightMode" = "MelodyUnlit"  "RenderQueue" = "Transparent" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #define FLARE_PREVIEW
            #define FLARE_POLYGON
            #define FLARE_INVERSE_SDF
            ENDHLSL
        }
    }
}
