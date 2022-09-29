Shader "Hidden/Melody RP/Post FX Stack/LensFlare/Preview"
{
    //NOTE: For UI as we don't have command buffer for UI we need to have one shader per usage instead of permutation like for rendering
    //keep the order as the same order of SRPLensFlareType
    SubShader
    {
        //Image
        Pass
        {
            Name "Image"
            Tags{ "LightMode" = "MelodyUnlit"  "RenderQueue" = "Transparent" }

            Blend One One
            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "../ShaderLibrary/Common.hlsl"
            #define FLARE_PREVIEW
            #include "LensflareCommon.hlsl"
            ENDHLSL
        }

        //Circle
        Pass
        {
            Name "Image"
            Tags{ "LightMode" = "MelodyUnlit"  "RenderQueue" = "Transparent" }

            Blend One One
            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "../ShaderLibrary/Common.hlsl"
            #define FLARE_PREVIEW
            #define FLARE_CIRCLE
            #include "LensflareCommon.hlsl"
            ENDHLSL
        }

        //Polygon
        Pass
        {
            Name "Image"
            Tags{ "LightMode" = "MelodyUnlit"  "RenderQueue" = "Transparent" }

            Blend One One
            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "../ShaderLibrary/Common.hlsl"
            #define FLARE_PREVIEW
            #include "LensflareCommon.hlsl"
            ENDHLSL
        }

        //Circle Inverse
        Pass
        {
            Name "Image"
            Tags{ "LightMode" = "MelodyUnlit"  "RenderQueue" = "Transparent" }

            Blend One One
            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "../ShaderLibrary/Common.hlsl"
            #define FLARE_PREVIEW
            #include "LensflareCommon.hlsl"
            ENDHLSL
        }

        //Polygon Inverse
        Pass
        {
            Name "Image"
            Tags{ "LightMode" = "MelodyUnlit"  "RenderQueue" = "Transparent" }

            Blend One One
            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "../ShaderLibrary/Common.hlsl"
            #define FLARE_PREVIEW
            #include "LensflareCommon.hlsl"
            ENDHLSL
        }
    }
}
