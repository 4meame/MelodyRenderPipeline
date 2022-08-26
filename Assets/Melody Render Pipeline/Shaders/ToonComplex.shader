Shader "Melody RP/Toon/Complex"
{
    Properties
    {
        [Header(Main)]
        _MainMap("MainMap(Bright Part)", 2D) = "white" {}
		_BaseColor("MainColor", Color) = (1,1,1,1)
		[Toggle(_)] _Is_LightColor_Base("Is LightColor On Main", Float) = 1
        [Header(1st Shade)]
		_1st_ShadeMap("1st ShadeMap(Dark Part)", 2D) = "white" {}
		[Toggle(_)] _Use_BaseAs1st("Use MainMap as 1st ShadeMap", Float) = 0
		_1st_ShadeColor("1st ShadeColor", Color) = (1,1,1,1)
		[Toggle(_)] _Is_LightColor_1st_Shade("Is LightColor On 1st Shade", Float) = 1
        [Header(2nd Shade)]
		_2nd_ShadeMap("2nd ShadeMap(Dark Part)", 2D) = "white" {}
		[Toggle(_)] _Use_1stAs2nd("Use 1st ShadeMap as 2nd ShadeMap", Float) = 0
		_2nd_ShadeColor("2nd ShadeColor", Color) = (1,1,1,1)
		[Toggle(_)] _Is_LightColor_2nd_Shade("Is LightColor On 2nd Shade", Float) = 1
        [Header(Normal)]
        _NormalMap("NormalMap", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0, 1)) = 1
        [Toggle(_)] _Is_NormalMapToBase("Is NormalMap To Base", Float) = 0
		[Header(Boundary of brightness and darkness )]
		_Set_1st_ShadePosition("Set 1st ShadePosition", 2D) = "white" {}
		_Set_2nd_ShadePosition("Set 2nd ShadePosition", 2D) = "white" {}
		_BaseColor_Step("BaseColor Step", Range(0, 1)) = 0.5
		_BaseShade_Feather("Base/Shade Feather", Range(0.0001, 1)) = 0.0001
		_ShadeColor_Step("ShadeColor Step", Range(0, 1)) = 0
		_ShadeColor_Feather("1st/2nd Shades Feather", Range(0.0001, 1)) = 0.0001
		[Toggle(_)] _Set_ShadowAttenutionToBase("Set Shadow Attenution To Base", Float) = 1
		_Tweak_ShadowAttenution("Tweak Shadow Attenution", Range(-0.5, 0.5)) = 0
		_StepOffset("Point Light Attenution Offset", Range(-0.5, 0.5)) = 0
		[Header(Specular)]
		_HighColor_Map("HighColor Map", 2D) = "white" {}
		_HighColor("HighColor", Color) = (0,0,0,1)
		_Set_HighColorMask("HighColor MaskMap", 2D) = "white" {}
		_Tweak_HighColorStrength("Tweak High Color Mask Strength", Range(-1, 1)) = 0
		[Toggle(_)] _Is_LightColor_HighColor("Is LightColor On HighColor", Float) = 1
		[Toggle(_)] _Is_NormalMapToHighColor("Is NormalMap To HighColor", Float) = 0
		[Toggle(_)] _Is_FeatherToHighColor("Is Feather Style", Float) = 0
		[Toggle(_)] _Is_BlendAddToHighColor("Is Blend Add To HighColor", Float) = 0
		_HighColor_Power("HighColor Power", Range(0, 1)) = 0
		[Toggle(_)] _Is_UseTweakHighColorOnShadow("Tweak High Color On Dark Part", Float) = 0
		_Tweak_HighColorOnShadow("Tweak High Color On Dark(Shadow) Part", Range(0, 1)) = 0
		[Toggle(_)] _Is_Filter_HighCutPointLightColor("Filter Point light High Intensity)", Float) = 1
        [Header(Rim)]
		[Toggle(_)] _UseRimLight("Use RimLight", Float) = 0
		_Set_RimLightMask("Set RimLight Mask", 2D) = "white" {}
		_Tweak_RimLightStrength("Tweak RimLight Mask Strength", Range(-1, 1)) = 0
		[Toggle(_)] _LightDirection_MaskOn("Hide Rim On Dark Part", Float) = 0
		_Tweak_LightDirection_Strength("Tweak On Dark Part Strength", Range(0, 0.5)) = 0
		[Toggle(_)] _Is_LightColor_RimLight("Is Light Color On RimLight", Float) = 1
		[Toggle(_)] _Is_NormalMapToRimLight("Is NormalMap To RimLight", Float) = 0
		[Toggle(_)] _Is_LightColor_Ap_RimLight("Is LightColor on Anti RimLight", Float) = 1
		_RimLightColor("Rim Light Color", Color) = (1,1,1,1)
		_RimLight_Step("RimLight Step", Range(0.0001, 1)) = 0.0001
		_RimLight_Power("RimLight_Power", Range(0, 1)) = 0.1
		[Toggle(_)] _RimLight_FeatherOff("RimLight Feather Off", Float) = 0
		[Toggle(_)] _Add_Antipodean_RimLight("Add Antipodean RimLight", Float) = 0
		_AntiRimLightColor("Anti RimLight Color", Color) = (1,1,1,1)
		_AntiRimLight_Power("Anti RimLightPower", Range(0, 1)) = 0.1
		[Toggle(_)] _AntiRimLight_FeatherOff("Anti RimLight Feather Off", Float) = 0
		 _Use_SS_RimLight("Screen Space RimLight Strength",  Range(0, 1)) = 0
		_SS_RimLightLength("Screen Space RimLight Length", Float) = 0
		_SS_RimLightWidth("Screen Space RimLight Width", Float) = 0
		_SS_RimLight_Step("Screen Space RimLight Step", Range(0.01, 1)) = 0
		_SS_RimLightNormalBlend("Smooth Normal Blend Strength", Range(0, 1)) = 0
		[Header(MatCap)]
		[Toggle(_)] _UseMatCap("MatCap", Float) = 0
        _MatCap_Map("MatCap Map", 2D) = "black" {}
		_MatCapColor("MatCap Color", Color) = (1,1,1,1)
		_BlurLevelMatCap("Blur Level of MatCap", Range(0, 10)) = 0
		_Tweak_MatCapUV("Tweak MatCap UV", Range(-0.5, 0.5)) = 0
		_Rotate_MatCapUV("Rotate MatCap UV", Range(-1, 1)) = 0
		_Set_MatCapMask("Set MatCap Mask", 2D) = "white" {}
		[Toggle(_)] _Inverse_MatCapMask("Inverse MatCap Mask", Float) = 0
		_Tweak_MatCapStrength("Tweak MatCap Mask Strength", Range(-1, 1)) = 0
		[Toggle(_)] _Is_LightColor_MatCap("Is Light Color On MatCap", Float) = 1
		[Toggle(_)] _Is_BlendAddToMatCap("Is Blend Add To MatCap", Float) = 1
		_NormalMapForMatCap("NormalMap For MatCap", 2D) = "bump" {}
		[Toggle(_)] _Is_NormalMapToMatCap("Is NormalMap To MatCap", Float) = 0
		_BumpScaleMatCap("Scale for NormalMap For MatCap", Range(0, 1)) = 1
		_Rotate_NormalMapForMatCapUV("Rotate NormalMap For MatCap UV", Range(-1, 1)) = 0
		[Toggle(_)] _Is_UseTweakMatCapOnShadow("Tweak MatCap On Dark Part", Float) = 0
		_Tweak_MatCapOnShadow("Tweak MatCap On Dark Part Strength", Range(0, 1)) = 0
		[Toggle(_)] _Is_Ortho("Orthographic Projection for MatCap", Float) = 0
		[Toggle(_)] _CameraRolling_Stabilizer("Activate CameraRolling Stabilizer", Float) = 0
		[Header(Emission)]
		[KeywordEnum(SIMPLE,ANIMATION)] _EMISSIVE("EMISSION MODE", Float) = 0
		_Emissive_Map("Emission Map", 2D) = "white" {}
		[HDR]_Emissive_Color("Emission Color", Color) = (0,0,0,1)
		[Toggle(_)] _Is_ViewCoord_Scroll("Is View Space Anim", Float) = 0
		_Base_Speed("Anim Speed", Float) = 0
		_Scroll_EmissiveU("Move Horizontal", Range(-1, 1)) = 0
		_Scroll_EmissiveV("Move Vertical", Range(-1, 1)) = 0
		_Rotate_EmissiveUV("Move Rotate ", Float) = 0
		[Toggle(_)] _Is_ViewShift("Use ViewShift", Float) = 0
		[HDR]_ViewShift("ViewSift Color", Color) = (0,0,0,1)
		[Toggle(_)] _Is_ColorShift("Use ColorShift", Float) = 0
		[HDR]_ColorShift("ColorSift Color", Color) = (0,0,0,1)
		_ColorShift_Speed("ColorShift Speed", Float) = 0
		[Toggle(_)] _Is_Breath_Anim("Is Breath Circle Anim", Float) = 0
		[Header(GI)]
		_GI_Min_Color("GI Min Color", Color) = (0,0,0,1)
		_GI_Intensity("GI Intensity", Range(0, 2)) = 0
		_Unlit_Intensity("Unlit Intensity", Range(0.001, 4)) = 1
		[Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows("Receive Shadows", Float) = 1
		[Toggle()] _Is_Static_Light("Is Static Light", Float) = 0
		_Offset_X_Axis_Static("Static Offset X Axis", Float) = 0
		_Offset_Y_Axis_Static("Static Offset Y Axis", Float) = 0
		_Offset_Z_Axis_Static("Static Offset Z Axis", Float) = 0
		[Header(Outline)]
		[KeywordEnum(NML, POS)] _OUTLINE("Outline Mode", Float) = 0
		_Outline_Width("Outline Width", Float) = 0
		_Farthest_Distance("Farthest Distance", Float) = 100
		_Nearest_Distance("Nearest Distance", Float) = 0.5
		_Outline_Sampler("Outline Sampler", 2D) = "white" {}
		_Outline_Color("Outline Color", Color) = (0.5, 0.5, 0.5, 1)
		[Toggle(_)] _Is_BlendBaseColor("Is Blend Base Color", Float) = 0
		[Toggle(_)] _Is_LightColor_Outline("Is Light Color On Outline", Float) = 1
		[Toggle(_)] _Is_OutlineTex("Use OutlineTex", Float) = 0
		_OutlineTex("Outline Map", 2D) = "white" {}
		_Offset_Z("Offset Camera Z", Float) = 0
		[Toggle(_)] _Is_BakedNormal("Use BakedNormal", Float) = 0
		_BakedNormal("Baked Normal for Outline", 2D) = "white" {}
		[Header(Cliping)]
		[KeywordEnum(ON,OFF)] _CLIPPING("Clipping Mode", Float) = 0
		_ClippingMask("Clipping Map", 2D) = "white" {}
		_Clipping_Level("Clipping_Level", Range(0, 1)) = 0
		[Toggle(_)] _Inverse_Clipping("Inverse_Clipping", Float) = 0
		[Header(Enum)]
		[KeywordEnum(On, Clip, Dither, Off)] _Shadows("Shadows", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)]_ZWrite("Z Write", Float) = 1
		[Enum(OFF, 0, FRONT, 1, BACK, 2)] _CullMode("Cull Mode", int) = 2
    }
    SubShader
    {       
        Pass
        {
            Name "MelodyForward"
            Tags { "LightMode" = "MelodyForward" }

			Cull[_CullMode]
            Blend [_SrcBlend][_DstBlend], One OneMinusSrcAlpha
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma target 3.5
			#pragma shader_feature _RECEIVE_SHADOWS
			#pragma multi_compile _EMISSIVE_SIMPLE _EMISSIVE_ANIMATION
	        #pragma multi_compile _CLIPPING_ON _CLIPPING_OFF
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
			#define _COMPLEX_TOON
			#define _USE_DEPTHNORMAL
            #include "ToonLitPass.hlsl"
            ENDHLSL
        }

		Pass
		{
			Name "Outline"
			Tags { "LightMode" = "MelodyUnlit" }

			Cull Front
			HLSLPROGRAM
			#pragma target 3.5
			#pragma multi_compile _CLIPPING_ON _CLIPPING_OFF
			#pragma multi_compile _OUTLINE_NML _OUTLINE_POS
			#pragma multi_compile_instancing
			#pragma vertex vert
			#pragma fragment frag
			#define _COMPLEX_TOON
			#include "ToonOutlinePass.hlsl"
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
			#define _COMPLEX_TOON
			#include "ToonLitPass.hlsl"
			ENDHLSL
		}

        Pass
        {
            Name "GBuffer"
            Tags { "LightMode" = "GBuffer" }

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
