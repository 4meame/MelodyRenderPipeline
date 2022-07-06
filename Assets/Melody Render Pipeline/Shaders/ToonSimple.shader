Shader "Melody RP/Toon/Simple"
{
    Properties
    {
        [HideInInspector] _MainTex("Texture for Lightmap", 2D) = "white" {}
        [HideInInspector] _Color("Color for Lightmap", Color) = (0.5, 0.5, 0.5, 1.0)
        _BaseMap("Base Map<Albedo>", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (0.5, 0.5, 0.5, 1.0)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5		
		[Header(Emission)]
		[NoScaleOffset]_EmissionMap("EmissionMap", 2D) = "white" {}
		[HDR] _EmissionColor("EmissionColor", Color) = (0,0,0)
		_EmissionMulByBaseColor("EmissionMulByBaseColor", Range(0,1)) = 0
		[Header(Occlusion)]
		[NoScaleOffset]_OcclusionMap("OcclusionMap", 2D) = "white" {}
		_OcclusionStrength("OcclusionStrength", Range(0.0, 1.0)) = 1.0
		_OcclusionMapChannelMask("OcclusionMapChannelMask", Vector) = (1,0,0,0)
		_OcclusionRemapStart("OcclusionRemapStart", Range(0,1)) = 0
		_OcclusionRemapEnd("OcclusionRemapEnd", Range(0,1)) = 1
		[Header(Lighting)]
		//can prevent completely black if lightprobe not baked
		_IndirectLightMinColor("IndirectLightMinColor", Color) = (0.1,0.1,0.1,1)
		_CelShadeMidPoint("CelShadeMidPoint", Range(-1,1)) = -0.5
		_CelShadeSoftness("CelShadeSoftness", Range(0,1)) = 0.05
		[Header(Shadow mapping)]
		_ReceiveShadowMappingAmount("ReceiveShadowMappingAmount", Range(0,1)) = 0.65
		_ReceiveShadowMappingPosOffset("ReceiveShadowMappingPosOffset", Float) = 0
		_ShadowMapColor("ShadowMapColor", Color) = (1,0.825,0.78)
		[NoScaleOffset]_FaceShadowSDF("Face Shadow SDF Mask", 2D) = "black" {}
		_LerpMax("Max Lerp Value", Range(0,1)) = 0.5
		[Header(Outline)]
		_OutlineWidth("OutlineWidth (World Space)", Range(0,4)) = 1
		_OutlineColor("OutlineColor", Color) = (0.5,0.5,0.5,1)
		_OutlineZOffset("OutlineZOffset (View Space)", Range(0,1)) = 0.0001
		[NoScaleOffset]_OutlineZOffsetMaskTex("OutlineZOffsetMask (black is apply ZOffset)", 2D) = "black" {}
		_OutlineZOffsetMaskRemapStart("OutlineZOffsetMaskRemapStart", Range(0,1)) = 0
		_OutlineZOffsetMaskRemapEnd("OutlineZOffsetMaskRemapEnd", Range(0,1)) = 1
		[Header(Toggle)]
		[Toggle]_UseOcclusion("UseOcclusion (on/off Occlusion completely)", Float) = 0
		[Toggle]_UseEmission("UseEmission (on/off Emission completely)", Float) = 0
		[Toggle(_IS_FACE)]_IsFace("Is Face", Float) = 0
		[Toggle(_CLIPPING)]_Clipping("Alpha Clipping", Float) = 0
		[Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows("Receive Shadows", Float) = 1
		[Header(Enum)]
		[KeywordEnum(On, Clip, Dither, Off)] _Shadows("Shadows", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)]_ZWrite("Z Write", Float) = 1
    }
    SubShader
    {       
        Pass
        {
            Name "MelodyLit"
            Tags { "LightMode" = "MelodyLit" }

            Blend [_SrcBlend][_DstBlend], One OneMinusSrcAlpha
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma shader_feature _CLIPPING
			#pragma shader_feature _RECEIVE_SHADOWS
			#pragma shader_feature _IS_FACE
			#pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            #pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7
			#pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            #pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
			#pragma multi_compile_instancing
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ _LIGHTS_PER_OBJECT
            #pragma vertex ToonLitPassVertex
            #pragma fragment ToonLitPassFragment
			#define _SIMPLE_TOON
            #include "ToonLitPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { 
            //IMPORTANT: don't write this line for any custom pass! else this outline pass will not be rendered by URP!
            "LightMode" = "MelodyUnlit" 
            //[Important CPU performance note]
            //If you need to add a custom pass to your shader (outline pass, planar shadow pass, XRay pass when blocked....),
            //(0) Add a new Pass{} to your shader
            //(1) Write "LightMode" = "YourCustomPassTag" inside new Pass's Tags{}
            //(2) Add a new custom RendererFeature(C#) to your renderer,
            //(3) write cmd.DrawRenderers() with ShaderPassName = "YourCustomPassTag"
            // 4) if done correctly, URP will render your new Pass{} for your shader, in a SRP-batcher friendly way (usually in 1 big SRP batch)
            }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front

            HLSLPROGRAM
            #pragma target 3.5
            #pragma shader_feature _CLIPPING
            #pragma multi_compile_instancing
            #pragma vertex ToonLitPassVertex
            #pragma fragment ToonLitPassFragment
            //because this is an Outline pass, define "ToonShaderIsOutline" to inject outline related code into both VertexShaderWork() and ShadeFinalColor()
            #define ToonShaderIsOutline
			#define _SIMPLE_TOON
            #include "ToonLitPass.hlsl"
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
			#pragma vertex ToonLitPassVertex
			#pragma fragment ToonLitShadowAlphaClipTest
			#define _SIMPLE_TOON
			#include "ToonLitPass.hlsl"
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
			#define _SIMPLE_TOON
            #include "ToonLitPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "MelodyShaderGUI"
}
