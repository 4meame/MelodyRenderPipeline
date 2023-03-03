Shader "Melody RP/Standard/Grass/Instanced"
{
    Properties
    {
        _BaseColor("Grass Base Color", Color) = (0.0, 1.0, 0.0, 1.0)
        _HighColor("Grass High Color", Color) = (1.0, 1.0, 0.0, 1.0)
        _ShadowColor("Grass Shadow Color", Color) = (0.0, 1.0, 1.0, 1.0)
        _NoiseTex("Noise Texture", 2D) = "white" {}
        _BendingX("Bending X", Range(0.0, 180)) = 45
        _BendingY("Bending Y", Range(0.0, 360)) = 360
        _WindTex("Wind Texture", 2D) = "white" {}
        _WindSpeed("Wind Speed", Float) = 1.0
        _WindFrequency("Wind Frequency", Float) = 1.0
        _WindStrength("Wind Strength", Float) = 1.0
        [Toggle(_PREMULTIPLY_ALPHA)]_PremulAlpha("Premultiply Alpha", Float) = 0
        [Toggle(_CLIPPING)]_Clipping("Alpha Clipping", Float) = 0
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows("Receive Shadows", Float) = 1
        [KeywordEnum(On, Clip, Dither, Off)] _Shadows("Shadows", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)]_ZWrite("Z Write", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)]_Cull("Cull Mode", Float) = 0
    }
    SubShader
    {
        Pass
        {
            Name "MelodyForward"
            Tags { "LightMode" = "MelodyForward" }

            Blend[_SrcBlend][_DstBlend], One OneMinusSrcAlpha
            ZWrite[_ZWrite]
            Cull[_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma shader_feature _CLIPPING
            #pragma shader_feature _PREMULTIPLY_ALPHA
            #pragma shader_feature _RECEIVE_SHADOWS
            #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            #pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7
            #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            #pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #include "GrassLitPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MelodyDeferred"
            Tags { "LightMode" = "MelodyDeferred" }

            Blend[_SrcBlend][_DstBlend]//, One OneMinusSrcAlpha
            ZWrite[_ZWrite]
            Cull[_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma shader_feature _CLIPPING
            #pragma shader_feature _PREMULTIPLY_ALPHA
            #pragma shader_feature _RECEIVE_SHADOWS
            #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            #pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7
            #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            #pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #include "GrassLitPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LitPassVertex
            #pragma fragment ShadowCasterPass
            #include "GrassLitPass.hlsl"
            ENDHLSL
        }
    }
}
