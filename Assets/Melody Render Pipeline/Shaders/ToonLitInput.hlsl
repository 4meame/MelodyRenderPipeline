#ifndef MELODY_TOON_LIT_INPUT_INCLUDED
#define MELODY_TOON_LIT_INPUT_INCLUDED

#if defined(_SIMPLE_TOON)
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);

TEXTURE2D(_OcclusionMap);
SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_OutlineZOffsetMaskTex);
SAMPLER(sampler_OutlineZOffsetMaskTex);

TEXTURE2D(_FaceShadowSDF);
SAMPLER(sampler_FaceShadowSDF);

//Support per-instance material data, replace variable with an array reference WHEN NEEDED
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
	UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
	//emission
	UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _EmissionMulByBaseColor)
	//occlusion
	UNITY_DEFINE_INSTANCED_PROP(float, _OcclusionStrength)
	UNITY_DEFINE_INSTANCED_PROP(float4, _OcclusionMapChannelMask)
	UNITY_DEFINE_INSTANCED_PROP(float, _OcclusionRemapStart)
	UNITY_DEFINE_INSTANCED_PROP(float, _OcclusionRemapEnd)
	//lighting
	UNITY_DEFINE_INSTANCED_PROP(float3, _IndirectLightMinColor)
	UNITY_DEFINE_INSTANCED_PROP(float3, _IndirectLightMultiplier)
	UNITY_DEFINE_INSTANCED_PROP(float3, _DirectLightMultiplier)
	UNITY_DEFINE_INSTANCED_PROP(float, _CelShadeMidPoint)
	UNITY_DEFINE_INSTANCED_PROP(float, _CelShadeSoftness)
	//shadow mapping
	UNITY_DEFINE_INSTANCED_PROP(float, _ReceiveShadowMappingAmount)
	UNITY_DEFINE_INSTANCED_PROP(float, _ReceiveShadowMappingPosOffset)
	UNITY_DEFINE_INSTANCED_PROP(float3, _ShadowMapColor)
	//outline
	UNITY_DEFINE_INSTANCED_PROP(float, _OutlineWidth)
	UNITY_DEFINE_INSTANCED_PROP(float3, _OutlineColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _OutlineZOffset)
	UNITY_DEFINE_INSTANCED_PROP(float, _OutlineZOffsetMaskRemapStart)
	UNITY_DEFINE_INSTANCED_PROP(float, _OutlineZOffsetMaskRemapEnd)

	//is face
	UNITY_DEFINE_INSTANCED_PROP(float, _IsFace)
	UNITY_DEFINE_INSTANCED_PROP(float, _LerpMax)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

//a special uniform for applyShadowBiasFixToHClipPos() only, it is not a per material uniform
float3 _LightDirection;


float4 GetBase(float2 baseUV) {
	float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
	float4 base = baseMap * baseColor;
	return base;
}

float3 GetEmission(float2 baseUV) {
	float4 emissionMap = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, baseUV);
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float4 emissionColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionColor);
	float3 emission = emissionMap.rgb * emissionColor.rgb;
	return emission;
}

float GetOcclusion(float2 baseUV) {
	float occlusion = 1;
	float4 occlusionMap = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, baseUV);
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float4 occlusionMapChannelMask = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _OcclusionMapChannelMask);
	float occlusionValue = dot(occlusionMap, occlusionMapChannelMask);
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float occlusionStrength = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _OcclusionStrength);
	occlusionValue = lerp(1, occlusionValue, occlusionStrength);
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float occlusionRemapStart = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _OcclusionRemapStart);
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float occlusionRemapEnd = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _OcclusionRemapEnd);
	occlusionValue = invLerpClamp(occlusionRemapStart, occlusionRemapEnd, occlusionValue);
	occlusion = occlusionValue;
	return occlusion;
}

float GetOutlineWidth() {
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
float outlineWidth = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _OutlineWidth);
return outlineWidth;
}

float GetFaceSDF(float2 baseUV) {
	float4 faceSDF = SAMPLE_TEXTURE2D(_FaceShadowSDF, sampler_FaceShadowSDF, baseUV);
	return faceSDF.r;
}

#endif

#if defined(_COMPLEX_TOON)
TEXTURE2D(_MainMap);
SAMPLER(sampler_MainMap);
float4 _MainMap_ST;
float4 _BaseColor;
float4 _Color;
float _Use_BaseAs1st;
float _Use_1stAs2nd;

float _Is_LightColor_Base;
TEXTURE2D(_1st_ShadeMap);
SAMPLER(sampler_1st_ShadeMap);
float4 _1st_ShadeMap_ST;
float4 _1st_ShadeColor;
float _Is_LightColor_1st_Shade;
TEXTURE2D(_2nd_ShadeMap);
SAMPLER(sampler_2nd_ShadeMap);
float4 _2nd_ShadeMap_ST;
float4 _2nd_ShadeColor;
float _Is_LightColor_2nd_Shade;
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
float4 _NormalMap_ST;
float _Is_NormalMapToBase;
float _Set_ShadowAttenutionToBase;
float _Tweak_ShadowAttenution;
float _BaseColor_Step;
float _BaseShade_Feather;
TEXTURE2D(_Set_1st_ShadePosition);
SAMPLER(sampler_Set_1st_ShadePosition);
float4 _Set_1st_ShadePosition_ST;
float _ShadeColor_Step;
float _ShadeColor_Feather;
TEXTURE2D(_Set_2nd_ShadePosition);
SAMPLER(sampler_Set_2nd_ShadePosition);
float4 _Set_2nd_ShadePosition_ST;

float4 _HighColor;
TEXTURE2D(_HighColor_Map);
SAMPLER(sampler_HighColor_Map);
float4 _HighColor_Map_ST;
float _Is_LightColor_HighColor;
float _Is_NormalMapToHighColor;
float _HighColor_Power;
float _Is_FeatherToHighColor;
float _Is_BlendAddToHighColor;
float _Is_UseTweakHighColorOnShadow;
float _Tweak_HighColorOnShadow;
TEXTURE2D(_Set_HighColorMask);
SAMPLER(sampler_Set_HighColorMask);
float4 _Set_HighColorMask_ST;
float _Tweak_HighColorStrength;

float _UseRimLight;
float4 _RimLightColor;
float _Is_LightColor_RimLight;
float _Is_NormalMapToRimLight;
float _RimLight_Power;
float _RimLight_Step;
float _RimLight_FeatherOff;
float _LightDirection_MaskOn;
float _Tweak_LightDirection_Strength;
float _Add_Antipodean_RimLight;
float4 _AntiRimLightColor;
float _Is_LightColor_AntiRimLight;
float _AntiRimLight_Power;
float _AntiRimLight_FeatherOff;
TEXTURE2D(_Set_RimLightMask);
SAMPLER(sampler_Set_RimLightMask);
float4 _Set_RimLightMask_ST;
float _Tweak_RimLightStrength;
//screen space rim light
float _Use_SS_RimLight;
float _SS_RimLightNormalBlend;
float _SS_RimLightLength;
float _SS_RimLightWidth;
float _SS_RimLight_Step;

float _UseMatCap;
TEXTURE2D(_MatCap_Map);
SAMPLER(sampler_MatCap_Map);
float4 _MatCap_Map_ST;
float4 _MatCapColor;
float _Is_LightColor_MatCap;
float _Is_BlendAddToMatCap;
float _Tweak_MatCapUV;
float _Rotate_MatCapUV;
float _Is_NormalMapToMatCap;
TEXTURE2D(_NormalMapForMatCap);
SAMPLER(sampler_NormalMapForMatCap);
float4 _NormalMapForMatCap_ST;
float _Rotate_NormalMapForMatCapUV;
float _Is_UseTweakMatCapOnShadow;
float _Tweak_MatCapOnShadow;
TEXTURE2D(_Set_MatCapMask);
SAMPLER(sampler_Set_MatCapMask);
float4 _Set_MatCapMask_ST;
float _Tweak_MatCapStrength;

float _Is_Ortho;
float _CameraRolling_Stabilizer;
float _BlurLevelMatCap;
float _Inverse_MatCapMask;
float _BumpScale;
float _BumpScaleMatCap;

TEXTURE2D(_Emissive_Map);
SAMPLER(sampler_Emissive_Map);
float4 _Emissive_Map_ST;
float4 _Emissive_Color;
float _Is_ViewCoord_Scroll;
float _Rotate_EmissiveUV;
float _Base_Speed;
float _Scroll_EmissiveU;
float _Scroll_EmissiveV;
float _Is_Breath_Anim;
float4 _ColorShift;
float4 _ViewShift;
float _ColorShift_Speed;
float _Is_ColorShift;
float _Is_ViewShift;

float _Unlit_Intensity;
float _Is_Filter_HighCutPointLightColor;
//for VR chat filtering scenelights
//float _Is_Filter_LightColor;
float _StepOffset;
float _Is_Static_Light;
float _Offset_X_Axis_Static;
float _Offset_Y_Axis_Static;
float _Inverse_Z_Axis_Static;

float4 _GI_Min_Color;
float _GI_Intensity;

TEXTURE2D(_ClippingMask);
SAMPLER(sampler_ClippingMask);
float4 _ClippingMask_ST;
float _Clipping_Level;
float _Inverse_Clipping;
float _ZWrite;

ToonMap GetToonMap(float2 baseUV) {
	ToonMap toonMap;
	toonMap.mainMap = SAMPLE_TEXTURE2D(_MainMap, sampler_MainMap, TRANSFORM_TEX(baseUV, _MainMap));
	toonMap.firstShadeMap = SAMPLE_TEXTURE2D(_1st_ShadeMap, sampler_1st_ShadeMap, TRANSFORM_TEX(baseUV, _1st_ShadeMap));
	toonMap.secondShadeMap = SAMPLE_TEXTURE2D(_2nd_ShadeMap, sampler_2nd_ShadeMap, TRANSFORM_TEX(baseUV, _2nd_ShadeMap));
	toonMap.normalMap = DecodeNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, TRANSFORM_TEX(baseUV, _NormalMap)), _BumpScale);
	toonMap.firstShadePositionMap = SAMPLE_TEXTURE2D(_Set_1st_ShadePosition, sampler_Set_1st_ShadePosition, TRANSFORM_TEX(baseUV, _Set_1st_ShadePosition));
	toonMap.secondShadePositionMap = SAMPLE_TEXTURE2D(_Set_2nd_ShadePosition, sampler_Set_2nd_ShadePosition, TRANSFORM_TEX(baseUV, _Set_2nd_ShadePosition));
	toonMap.highColorMap = SAMPLE_TEXTURE2D(_HighColor_Map, sampler_HighColor_Map, TRANSFORM_TEX(baseUV, _HighColor_Map));
	toonMap.highMaskMap = SAMPLE_TEXTURE2D(_Set_HighColorMask, sampler_Set_HighColorMask, TRANSFORM_TEX(baseUV, _Set_HighColorMask));
	toonMap.rimMaskMap = SAMPLE_TEXTURE2D(_Set_RimLightMask, sampler_Set_RimLightMask, TRANSFORM_TEX(baseUV, _Set_RimLightMask));
	toonMap.normalForMatCap = DecodeNormal(SAMPLE_TEXTURE2D(_NormalMapForMatCap, sampler_NormalMapForMatCap, TRANSFORM_TEX(baseUV, _NormalMapForMatCap)), _BumpScaleMatCap);
	toonMap.matCapMap = SAMPLE_TEXTURE2D_LOD(_MatCap_Map, sampler_MatCap_Map, TRANSFORM_TEX(baseUV, _MatCap_Map), _BlurLevelMatCap);
	toonMap.matCapMaskMap = SAMPLE_TEXTURE2D(_Set_MatCapMask, sampler_Set_MatCapMask, TRANSFORM_TEX(baseUV, _Set_MatCapMask));
	toonMap.emissionMap = SAMPLE_TEXTURE2D(_Emissive_Map, sampler_Emissive_Map, TRANSFORM_TEX(baseUV, _Emissive_Map));
	toonMap.clipMap = SAMPLE_TEXTURE2D(_ClippingMask, sampler_ClippingMask, TRANSFORM_TEX(baseUV, _ClippingMask));
	return toonMap;
}

#endif

#endif
