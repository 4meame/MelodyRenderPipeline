Shader "Melody RP/Standard/ForwardLit"
{
    Properties
    {
        [HideInInspector] _MainTex("Texture for Lightmap", 2D) = "white" {}
        [HideInInspector] _Color("Color for Lightmap", Color) = (0.5, 0.5, 0.5, 1.0)
        _BaseMap("Base Map<Albedo>", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (0.5, 0.5, 0.5, 1.0)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
		[NoScaleOffset]_MaskMap("Mask Map<MODS>", 2D) = "white" {}
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
		_Occlusion("Occlusion", Range(0.0, 1.0)) = 1.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _Fresnel("Fresnel", Range(0, 1)) = 1
        [NoScaleOffset]_EmissionMap("Emission Map<HDR>", 2D) = "white" {}
        [HDR]_EmissionColor("Emission", Color) = (0.0, 0.0, 0.0, 0.0)
		_DetailMap("Details", 2D) = "linearGrey" {}
		[NoScaleOffset]_DetailNormalMap("Detail Normals", 2D) = "bump" {}
		_DetailAlbedo("Detail Albedo", Range(0, 1)) = 1
		_DetailSmoothness("Detail Smoothness", Range(0, 1)) = 1
		_DetailNormalScale("Detail Normal Scale", Range(0, 1)) = 1
		[NoScaleOffset]_NormalMap("Normals", 2D) = "bump" {}
		_NormalScale("Normal Scale", Range(0, 1)) = 1
        [Toggle(_PREMULTIPLY_ALPHA)]_PremulAlpha("Premultiply Alpha", Float) = 0
        [Toggle(_CLIPPING)]_Clipping("Alpha Clipping", Float) = 0
		[Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows("Receive Shadows", Float) = 1
		[KeywordEnum(On, Clip, Dither, Off)] _Shadows("Shadows", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)]_ZWrite("Z Write", Float) = 1
    }
    SubShader
    {       
        Pass
        {
            Name "MelodyForward"
            Tags { "LightMode" = "MelodyForward" }

            Blend [_SrcBlend][_DstBlend], One OneMinusSrcAlpha
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma shader_feature _CLIPPING
            #pragma shader_feature _PREMULTIPLY_ALPHA
			#pragma shader_feature _RECEIVE_SHADOWS
			#pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            #pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7
			#pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            #pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
			#pragma multi_compile_instancing
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ _LIGHTS_PER_OBJECT
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #include "LitPass.hlsl"
            ENDHLSL
        }

		Pass
		{
            Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }

			ColorMask 0

			HLSLPROGRAM
			#pragma target 3.5
			#pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
			#pragma multi_compile_instancing
			#pragma vertex ShadowCasterPassVertex
			#pragma fragment ShadowCasterPassFragment
			#include "ShadowCasterPass.hlsl"
			ENDHLSL
		}

        Pass 
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MetaPassVertex
            #pragma fragment MetaPassFragment
            #include "MetaPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormal"
            Tags { "LightMode" = "DepthNormal" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonLitPassVertex
            #pragma fragment ToonLitShadowAlphaClipTest
            #define _COMPLEX_TOON
            #include "ToonLitPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "MelodyShaderGUI"
}
