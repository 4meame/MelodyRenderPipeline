#ifndef MELODY_TOON_LIT_CORE_INCLUDED
#define MELODY_TOON_LIT_CORE_INCLUDED

//is a safe guard best practice in almost every .hlsl (need Unity2020 or up), doing this can make sure your .hlsl's user can include this .hlsl anywhere anytime without producing any multi include conflict
#pragma once

#if defined(_SIMPLE_TOON)
float3 ShadeGI(Surface surface) {
	//hide 3D felling by ignore all detail SH, seting normal to 0 can leave only the constant SH term
	surface.normal = 0;
	float3 averageSH = SampleLightProbe(surface);
	//can prevent result becomes completely black if lightprobe was not baked 
	averageSH = max(_IndirectLightMinColor, averageSH);
	//occlusion(maximum 50% darken for indirect to prevent result becomes completely black)
	float indirectOcclusion = lerp(1, surface.occlusion, 0.5);
	return averageSH * indirectOcclusion;
}

float3 ShadePerLight(Surface surface, Light light, float2 baseUV, bool isAdditionalLight){
	float NdotL = saturate(dot(surface.normal, light.direction));
	//clamp to prevent light over bright if point/spot light too close to vertex
	float distanceAttenuation = min(4, light.distanceAttenuation);
    //simplest 1 line cel shade, you can always replace this line by your own method
	float litOrShadowArea = smoothstep(_CelShadeMidPoint - _CelShadeSoftness, _CelShadeMidPoint + _CelShadeSoftness, NdotL);
	//occlusion
	litOrShadowArea *= surface.occlusion;
	//face ignore celshade since it is usually very ugly using NoL method
	litOrShadowArea = _IsFace ? lerp(0.5, 1, litOrShadowArea) : litOrShadowArea;

	//calculate face shadow position
	float isShadow = 0;
	float frontToleftDir = GetFaceSDF(baseUV);
	float frontTorightDir = GetFaceSDF(float2(1 - baseUV.x, baseUV.y));
	float2 left = normalize(TransformObjectToWorldDir(float3(1, 0, 0)).xz);
	float2 front = normalize(TransformObjectToWorldDir(float3(0, 0, 1)).xz);
	float threshold = 1 - clamp(0, 1, dot(front, light.direction) * 0.5 + 0.5);
	float sdfShadow = dot(light.direction, left) > 0 ? frontToleftDir : frontTorightDir;
	isShadow = step(sdfShadow, threshold);
	float bias = smoothstep(0, _LerpMax, abs(threshold - sdfShadow));
	litOrShadowArea = _IsFace ? bias : litOrShadowArea;

	//light's shadow map
	litOrShadowArea *= lerp(1, light.shadowAttenuation, _ReceiveShadowMappingAmount);
	float3 litOrShadowColor = lerp(_ShadowMapColor, 1, litOrShadowArea);
	float3 lightAttenuationRGB = litOrShadowColor * distanceAttenuation;
	//saturate() light.color to prevent over bright
    //additional light reduce intensity since it is additive
	return saturate(light.color) * lightAttenuationRGB * (isAdditionalLight ? 0.25 : 1);
}

float3 ShadeEmission(Surface surface) {
	//optional mul albedo
	float3 emissionResult = lerp(surface.emission, surface.emission * surface.color, _EmissionMulByBaseColor);
	return emissionResult;
}

float3 CompositeAllLightResults(float3 indirectResult, float3 mainLightResult, float3 additionalLightSumResult, float3 emissionResult, Surface surface) {
	//just a simple tutorial method
	//here we prevent light over bright, while still want to preserve light color's hue
	//pick the highest between indirect and direct light
	float3 rawLightSum = max(indirectResult, mainLightResult + additionalLightSumResult);
	return surface.color * rawLightSum + emissionResult;
}

float3 ShadeAllLights(Surface surface, Light mainLight, ShadowData shadowData, float2 baseUV) {
	//indirect enviroment GI
	float3 IndirectLight = ShadeGI(surface);
#if defined(_RECEIVE_SHADOWS)
	//calculate offset positionSTS
	float3 shadowTestPosWS = surface.position + mainLight.direction * (_ReceiveShadowMappingPosOffset + _IsFace);
	//_ReceiveShadowMappingPosOffset will control the offset the shadow comparsion position, 
	//doing this is usually for hide ugly self shadow for shadow sensitive area like face
	DirectionalShadowData mainLightShadowData = GetDirectionalShadowData(_MainLightIndex, shadowData);
	surface.position = shadowTestPosWS;
	mainLight.shadowAttenuation = GetDirectionalShadowAttenuation(mainLightShadowData, shadowData, surface);
#endif 
	//main light result calculated by simple ShadePerLight Function
	float3 MainLightResult = ShadePerLight(surface, mainLight, baseUV, false);
	//additional light default by 0
	float3 otherLightSumResult = 0;
#if defined(_LIGHTS_PER_OBJECT)
	for (int i = 0; i < min(8, unity_LightData.y); i++) {
		int lightIndex = unity_LightIndices[(uint)i / 4][(uint)i % 4];
		Light light = GetOtherLight(lightIndex, surface, shadowData);
		otherLightSumResult += ShadePerLight(surface, light, baseUV, true);
	}
#else
	for (int i = 0; i < GetOtherLightCount(); i++) {
		Light light = GetOtherLight(i, surface, shadowData);
		otherLightSumResult += ShadePerLight(surface, light, baseUV, true);
	}
#endif
	//emission term
	float3 emissionResult = ShadeEmission(surface);
	return CompositeAllLightResults(IndirectLight, MainLightResult, otherLightSumResult, emissionResult, surface);
}

float3 ConvertSurfaceColorToOutlineColor(float3 originalSurfaceColor) {
	return originalSurfaceColor * _OutlineColor;
}
#endif

#if defined(_COMPLEX_TOON)
float3 ShadeGI() {
	float3 envLightColor = max(ShadeSH9(0), _GI_Min_Color);
	float3 envLightIntensity = 0.299 * envLightColor.r + 0.587 * envLightColor.g + 0.114 * envLightColor.b < 1 ? (0.299 * envLightColor.r + 0.587 * envLightColor.g + 0.114 * envLightColor.b) : 1;
	//smoothstep term prevents too bright env light
	float3 gi =  envLightColor * envLightIntensity * _GI_Intensity * smoothstep(1, 0, envLightIntensity / 2);
	return gi;
}

float3 ShadeDirectionalLight(Surface surface, Fragment fragment, Light light, ShadowData shadowData, ToonMap toonMap) {
//--------------Setup Light Source--------------//
	float3 defaultLightDirection = 0;
	//default color, in case no light source in the scene
	float3 defaultLightColor = saturate(max(half3(0.05, 0.05, 0.05) * _Unlit_Intensity, max(ShadeSH9(half3(0.0, 0.0, 0.0)), ShadeSH9(half3(0.0, -1.0, 0.0))) * _Unlit_Intensity));
	//custom static light direction
	float3 customLightDirection = normalize(mul(unity_ObjectToWorld, float4(((float3(1.0, 0.0, 0.0) * _Offset_X_Axis_Static * 10) + (float3(0.0, 1.0, 0.0) * _Offset_Y_Axis_Static * 10) + (float3(0.0, 0.0, -1.0) * lerp(-1.0, 1.0, _Inverse_Z_Axis_Static))), 0)).xyz);
	float3 lightDirection = normalize(lerp(defaultLightDirection, light.direction.xyz, any(light.direction.xyz)));
	//if use static (virtural) light
	lightDirection = lerp(lightDirection, customLightDirection, _Is_Static_Light);
	float3 lightColor = max(defaultLightColor, light.color.rgb);

//-------------Setup Light Model Vector-----------//
	//half vector
	float3 halfDirection = normalize(surface.viewDirection + lightDirection);
	float lambert = dot(lerp(surface.interpolatedNormal, surface.normal, _Is_NormalMapToBase), lightDirection);
	//here the lerp is the same as "if", remap lambert term to [0,1]
	float halfLambert = lambert * 0.5 + 0.5;
	//here the lerp is the same as "if", remap blingPhong NdotH term to [0,1]
	float blingPhong = dot(halfDirection, lerp(surface.interpolatedNormal, surface.normal, _Is_NormalMapToHighColor)) * 0.5 + 0.5;
	float fresnel = (1.0 - dot(lerp(surface.interpolatedNormal, surface.normal, _Is_NormalMapToRimLight), surface.viewDirection));

//--------------Setup Remap Color--------------//
	float3 Set_LightColor = lightColor;
	float3 Set_BaseColor = lerp((_BaseColor.rgb * toonMap.mainMap.rgb), (Set_LightColor * (_BaseColor.rgb * toonMap.mainMap.rgb)), _Is_LightColor_Base);
	float3 Set_1st_ShadeColor = lerp((_1st_ShadeColor.rgb * toonMap.firstShadeMap.rgb), (Set_LightColor * (_1st_ShadeColor.rgb * toonMap.firstShadeMap.rgb)), _Is_LightColor_1st_Shade);
	float3 Set_2nd_ShadeColor = lerp((_2nd_ShadeColor.rgb * toonMap.secondShadeMap.rgb), (Set_LightColor * (_2nd_ShadeColor.rgb * toonMap.secondShadeMap.rgb)), _Is_LightColor_2nd_Shade);
	float3 Set_HighColor = lerp((_HighColor.rgb * toonMap.highColorMap.rgb), (Set_LightColor * (_HighColor.rgb * toonMap.highColorMap.rgb)), _Is_LightColor_HighColor);
	float3 Set_RimLightColor = lerp(_RimLightColor, (Set_LightColor * _RimLightColor), _Is_LightColor_RimLight);
	float3 Set_AntiRimLightColor = lerp(_AntiRimLightColor, (Set_LightColor * _AntiRimLightColor), _Is_LightColor_AntiRimLight);
	float3 Set_MatCapColor = lerp((_MatCapColor.rgb * toonMap.matCapMap.rgb), (Set_LightColor * (_MatCapColor.rgb * toonMap.matCapMap.rgb)), _Is_LightColor_MatCap);

//--------------Setup Remap Positions--------------//
	float3 Set_1st_ShadePosition = toonMap.firstShadePositionMap.rgb;
	float3 Set_2nd_ShadePosition = toonMap.secondShadePositionMap.rgb;

#if defined(_RECEIVE_SHADOWS)
	//calculate offset positionSTS
	float3 shadowTestPosWS = surface.position + light.direction;
	//_ReceiveShadowMappingPosOffset will control the offset the shadow comparsion position, 
	//doing this is usually for hide ugly self shadow for shadow sensitive area like face
	DirectionalShadowData lightShadowData = GetDirectionalShadowData(_MainLightIndex, shadowData);
	surface.position = shadowTestPosWS;
	light.shadowAttenuation = light.shadowAttenuation = GetDirectionalShadowAttenuation(lightShadowData, shadowData, surface);
#endif 
	float shadowAttenuation_var = max((0.5 * light.shadowAttenuation + 0.5) + _Tweak_ShadowAttenution, 0.001);
	float halfLambert_var = (lerp(halfLambert, halfLambert * saturate(shadowAttenuation_var), _Set_ShadowAttenutionToBase));
	//this value adjusts the boudnary level of the bright(base) part
	float baseLevel_var = _BaseColor_Step;
	//this value adjusts the boudnary feather of the bright(base) part
	float baseFeather_var = _BaseColor_Step - _BaseShade_Feather;
	//dark(shadow) part will get this value
	float Set_FinalDarkPart = saturate(LerpFormula(halfLambert_var, baseLevel_var, baseFeather_var, (1 - Set_1st_ShadePosition).r, 1.0));

//--------------Base Color Term--------------//
	//this value adjusts the boudnary level of the dark(shade) part
	float shadeLevel_var = _ShadeColor_Step;
	//this value adjusts the boudnary feather of the dark(shade) part
	float shadeFeather_var = _ShadeColor_Step - _ShadeColor_Feather;
	//this value leads to 2 detail dark parts
	float Set_SubDarkPart = saturate(LerpFormula(halfLambert, shadeLevel_var, shadeFeather_var, (1 - Set_2nd_ShadePosition).r, 1.0));
	float3 drakPart = lerp(Set_1st_ShadeColor, Set_2nd_ShadeColor, Set_SubDarkPart);
	float3 brightPart = Set_BaseColor;
	float3 Set_FinalBaseColor = lerp(brightPart, drakPart, Set_FinalDarkPart);

//--------------High color Term--------------//
	//this value adjusts or masks high color strength
	float highColorMaskMap_var = saturate(toonMap.highMaskMap.g + _Tweak_HighColorStrength);
	float featherHigh = pow(blingPhong, exp2(lerp(11, 1, _HighColor_Power)));
	float hardHigh = step(1.0 - blingPhong, pow(_HighColor_Power, 5));
	//leads to 2 specular style
	float styledHighColorMask_var = highColorMaskMap_var * lerp(hardHigh, featherHigh, _Is_FeatherToHighColor);
	float3 highColor_var = Set_HighColor * styledHighColorMask_var;
	//tweak high color in dark(shadow) part
	float3 tweakedHighColor_var = highColor_var * ((1.0 - Set_FinalDarkPart) + (Set_FinalDarkPart * _Tweak_HighColorOnShadow));
	//lerp leads to if tweak HighColor in shadow part
	float3 Set_FinalHighPart = lerp(highColor_var, tweakedHighColor_var, _Is_UseTweakHighColorOnShadow);
	//leads to 3 specular style: plus specular straight, plus high straight, minus base and plus high
	float3 Set_HighColorAddMode = lerp(saturate((Set_FinalBaseColor - styledHighColorMask_var)), Set_FinalBaseColor, lerp(_Is_BlendAddToHighColor, 1.0, _Is_FeatherToHighColor));

//--------------Rim color Term--------------//
	//this value adjusts or masks rim light strength
	float rimLightMaskMap_var = saturate(toonMap.rimMaskMap.g + _Tweak_RimLightStrength);
	float rimLightPower_var = pow(fresnel, exp2(lerp(3, 0, _RimLight_Power)));
	//fresnel term is already a feather-direcional result, use lerp formula to remap position
	float featherRimLight = saturate(LerpFormula(rimLightPower_var, _RimLight_Step, 1.0, 0.0, 1.0));
	float hardRimLight = saturate(step(_RimLight_Step, rimLightPower_var));
	float rimLight_var = lerp(featherRimLight, hardRimLight, _RimLight_FeatherOff);
	//use halfLambet(light direction) to hide the rim light on dark side
	float vertHalfLambert_var = dot(surface.interpolatedNormal, lightDirection) * 0.5 + 0.5;
	//rim level minus dark side of the half lambert
	float tweakedRimLight_var = saturate(rimLight_var - ((1 - vertHalfLambert_var) + _Tweak_LightDirection_Strength));
	float3 Set_RimPart = lerp(rimLight_var, tweakedRimLight_var, _LightDirection_MaskOn) * Set_RimLightColor;
	//antipodean-rim light, mask by opposite halfLambet(light direction)
	float antiRimLightPower_var = pow(fresnel, exp2(lerp(3, 0, _AntiRimLight_Power)));
	float featherAntiRimLight = saturate(LerpFormula(antiRimLightPower_var, _RimLight_Step, 1.0, 0.0, 1.0));
	float hardAntiRimLight = saturate(step(_RimLight_Step, antiRimLightPower_var));
	float antiRimLight_var = lerp(featherAntiRimLight, hardAntiRimLight, _AntiRimLight_FeatherOff);
	float3 tweakedAntiRimLight_var = saturate(antiRimLight_var - (vertHalfLambert_var + _Tweak_LightDirection_Strength));
	float3 Set_AntiRimPart = tweakedAntiRimLight_var * Set_AntiRimLightColor;

	//screen-space rim light
	float2 lightDirectionVS = normalize(mul((float3x3)UNITY_MATRIX_V, lightDirection).xy);
	float2 normalDirectionVS = normalize(mul((float3x3)UNITY_MATRIX_V, lerp(surface.interpolatedNormal, surface.normal, _SS_RimLightNormalBlend)).xy);
	float lambertVS = saturate(dot(lightDirectionVS, normalDirectionVS) + _SS_RimLightLength * 0.1);
	//offset ss uv along the direction
	float2 ssUV_var = fragment.screenUV + normalDirectionVS * lambertVS * _SS_RimLightWidth * 0.001;
	//use offset uv sample depth texture
	float4 depthNormalTex = SAMPLE_TEXTURE2D_LOD(_CameraDepthNormalTexture, sampler_point_clamp, ssUV_var, 0);
	float rimDepthOffset = DecodeFloatRG(depthNormalTex.zw);
	float rimDepthDiffer = (rimDepthOffset - fragment.bufferDepth) * 250;
	float ssRimIntensity = step(_SS_RimLight_Step, rimDepthDiffer);
	ssRimIntensity = lerp(0, ssRimIntensity, rimLightPower_var);
	float3 Set_ssRimPart = ssRimIntensity * Set_RimLightColor;

	Set_RimPart = lerp(Set_RimPart, Set_ssRimPart, _Use_SS_RimLight);
	float3 Set_FinalRimPart = lerp(Set_RimPart, Set_RimPart + Set_AntiRimPart, _Add_Antipodean_RimLight) * rimLightMaskMap_var;

//-----------------MatCap Term---------------//
	//this value adjusts or masks matcap strength
	float matCapMaskMap_var = saturate(lerp(toonMap.matCapMaskMap.g, (1.0 - toonMap.matCapMaskMap.g), _Inverse_MatCapMask) + _Tweak_MatCapStrength);
	//if tweak matcap in shadow area
	float3 tweakedMatCap = Set_MatCapColor * ((1 - Set_FinalDarkPart) + Set_FinalDarkPart * _Tweak_MatCapOnShadow);
	float3 matCapOnDarkPart = lerp(Set_FinalBaseColor * Set_FinalDarkPart * (1.0 - _Tweak_MatCapOnShadow), float3(0.0, 0.0, 0.0), _Is_BlendAddToMatCap);
	tweakedMatCap += matCapOnDarkPart;
	float3 Set_MatCap = lerp(Set_MatCapColor, tweakedMatCap, _Is_UseTweakMatCapOnShadow);
	//leads to 2 blend mode : add and multiple
	float3 matCapOnAddMode = Set_MatCap * matCapMaskMap_var;
	//weaken the multi on dark part(weaker dark side
	float matCapMaskMap_Multiply_var = lerp(1.0, (1.0 - (Set_FinalDarkPart) * (1.0 - _Tweak_MatCapOnShadow)), _Is_UseTweakMatCapOnShadow) * matCapMaskMap_var;
	float3 matCapOnMultiMode = Set_MatCap * matCapMaskMap_Multiply_var;

//-----------------Final Composition Term---------------//
	//base + high
	float3 Set_FinalHighColor = Set_FinalHighPart + Set_HighColorAddMode;
	//base + high + rim
	float3 Set_FinalRimColor = lerp(Set_FinalHighColor, Set_FinalHighColor + Set_FinalRimPart, _UseRimLight);
	float3 Set_MatCapOnAddMode = Set_FinalRimColor + matCapOnAddMode;
	float3 Set_MatCapOnMultiMode = Set_FinalHighColor * (1 - matCapMaskMap_Multiply_var) + Set_FinalHighPart * matCapOnMultiMode + lerp(0, Set_FinalRimPart, _UseRimLight);
	float3 Set_FinalMatCap = lerp(Set_MatCapOnMultiMode, Set_MatCapOnAddMode, _Is_BlendAddToMatCap);
	float3 Set_FinalColor = lerp(Set_FinalRimColor, Set_FinalMatCap, _UseMatCap);

	return saturate(Set_FinalColor);
}

float3 ShadeEmission(Surface surface, ToonMap toonMap, float cameraDir, float cameraRoll, float2 baseUV, bool isMirror) {
	float4 emissionMap = toonMap.emissionMap;
	float3 emissionColor = emissionMap.rgb;
	float emissionMask = emissionMap.a;
	float3 Set_FinalEmission = 0;
#if defined(_EMISSIVE_SIMPLE)
	Set_FinalEmission = _Emissive_Color * emissionColor * emissionMask;
#elif defined(_EMISSIVE_ANIMATION)
	float3 normalVS = (mul(UNITY_MATRIX_V, float4(surface.interpolatedNormal, 0))).xyz;
	float3 normalVSBlendDetail = normalVS * float3(-1, -1, 1);
	float3 normalVSBlendBase = (mul(UNITY_MATRIX_V, float4(surface.viewDirection, 0)).xyz * float3(-1, -1, 1)) + float3(0, 0, 1);
	//normal blend method
	float3 blendNormalVS = normalVSBlendBase * dot(normalVSBlendBase, normalVSBlendDetail) / normalVSBlendBase.z - normalVSBlendDetail;
	float2 normalVSToUV = blendNormalVS.xy * 0.5 + 0.5;
	float2 viewSpaceUV = RotateUV(normalVSToUV, -(cameraDir * cameraRoll), float2(0.5, 0.5), 1.0);
	//if in mirror(reflect camera
	if (isMirror < 0) {
		viewSpaceUV.x = 1 - viewSpaceUV.x;
	}
	else {

	}
	float2 emissionUV = lerp(baseUV, viewSpaceUV, _Is_ViewCoord_Scroll);
	float Set_Time_var = _Time.y;
	float breathSpeed = _Base_Speed * Set_Time_var;
	//rotate and scroll UV with breath circle
	float breathAnim_var = lerp(breathSpeed, sin(breathSpeed), _Is_Breath_Anim);
	//uv scrools in horizontal and vertical
	float2 scrolledUV = emissionUV - float2(_Scroll_EmissiveU, _Scroll_EmissiveV) * breathAnim_var;
	float rotateSpeed = _Rotate_EmissiveUV * 3.141592654;
	float2 animUV_var = RotateUV(scrolledUV, rotateSpeed, float2(0.5, 0.5), breathAnim_var);
	float4 animEmission = SAMPLE_TEXTURE2D(_Emissive_Map, sampler_Emissive_Map, TRANSFORM_TEX(animUV_var, _Emissive_Map));

	//shift emission color
	float colorShiftSpeed_var = 1.0 - cos(Set_Time_var * _ColorShift_Speed);
	float viewShift_var = smoothstep(0.0, 1.0, max(0, dot(surface.normal, surface.viewDirection)));
	float4 colorShift_Color = lerp(_Emissive_Color, lerp(_Emissive_Color, _ColorShift, colorShiftSpeed_var), _Is_ColorShift);
	float4 viewShift_Color = lerp(_ViewShift, colorShift_Color, viewShift_var);
	float4 emissive_Color = lerp(colorShift_Color, viewShift_Color, _Is_ViewShift);

	Set_FinalEmission = emissive_Color.rgb * animEmission.rgb * emissionMask;
#endif
	return Set_FinalEmission;
}

float3 ShadeOtherLight(Surface surface, Light light, ToonMap toonMap) {
//--------------Setup Light Source--------------//
	float3 lightDirection = light.direction;
	float3 lightColor = light.color;
	float attenuation = light.shadowAttenuation;

//-------------Setup Light Model Vector-----------//
	//half vector
	float3 halfDirection = normalize(surface.viewDirection + lightDirection);
	//here the lerp is the same as "if", remap halfLambert term to [0,1]
	float halfLambert = dot(lerp(surface.interpolatedNormal, surface.normal, _Is_NormalMapToBase), lightDirection) * 0.5 + 0.5;
	//here the lerp is the same as "if", remap blingPhong NdotH term to [0,1]
	float blingPhong = dot(halfDirection, lerp(surface.interpolatedNormal, surface.normal, _Is_NormalMapToHighColor)) * 0.5 + 0.5;

//--------------Setup Remap Position--------------//
	//this value adjusts the boudnary level of the bright(base) part
	float baseLevel_var = saturate(_BaseColor_Step + _StepOffset);
	//this value adjusts the boudnary feather of the bright(base) part
	float baseFeather_var = baseLevel_var - _BaseShade_Feather;
	float halfLambert_var = (lerp(halfLambert, halfLambert * (1 + _Tweak_ShadowAttenution), _Set_ShadowAttenutionToBase));
	float3 Set_1st_ShadePosition = toonMap.firstShadePositionMap.rgb;
	float3 Set_2nd_ShadePosition = toonMap.secondShadePositionMap.rgb;
	//dark(shadow) part will get this value
	float Set_FinalDarkPart = saturate(LerpFormula(halfLambert_var, baseLevel_var, baseFeather_var, (1 - Set_1st_ShadePosition).r, 1.0));

//--------------Setup Remap Color--------------//
	float3 attenuationLightColor = lightColor * halfLambert * attenuation;
	float lightIntensity = (0.299 * lightColor.r + 0.587 * lightColor.g + 0.114 * lightColor.b) * attenuation;
	//filtering the high intensity zone of point lights
	float3 Set_LightColor = lerp(attenuationLightColor, min(attenuationLightColor, lightColor.rgb * attenuation * _BaseColor_Step), _Is_Filter_HighCutPointLightColor);
	float3 Set_BaseColor = lerp((_BaseColor.rgb * toonMap.mainMap.rgb * lightIntensity), (Set_LightColor * (_BaseColor.rgb * toonMap.mainMap.rgb)), _Is_LightColor_Base);
	float3 Set_1st_ShadeColor = lerp((_1st_ShadeColor.rgb * toonMap.firstShadeMap.rgb), (Set_LightColor * (_1st_ShadeColor.rgb * toonMap.firstShadeMap.rgb)), _Is_LightColor_1st_Shade);
	float3 Set_2nd_ShadeColor = lerp((_2nd_ShadeColor.rgb * toonMap.secondShadeMap.rgb), (Set_LightColor * (_2nd_ShadeColor.rgb * toonMap.secondShadeMap.rgb)), _Is_LightColor_2nd_Shade);
	float3 Set_HighColor = lerp((_HighColor.rgb * toonMap.highColorMap.rgb), (Set_LightColor * (_HighColor.rgb * toonMap.highColorMap.rgb)), _Is_LightColor_HighColor);

//--------------Base Color Term--------------//
	//this value adjusts the boudnary level of the dark(shade) part
	float shadeLevel_var = saturate(_ShadeColor_Step + _StepOffset);
	//this value adjusts the boudnary feather of the dark(shade) part
	float shadeFeather_var = shadeLevel_var - _ShadeColor_Feather;
	//this value leads to 2 detail dark parts
	float Set_SubDarkPart = saturate(LerpFormula(halfLambert, shadeLevel_var, shadeFeather_var, (1 - Set_2nd_ShadePosition).r, 1.0));
	float3 drakPart = lerp(Set_1st_ShadeColor, Set_2nd_ShadeColor, Set_SubDarkPart);
	float3 brightPart = Set_BaseColor;
	float3 Set_FinalBaseColor = lerp(brightPart, drakPart, Set_FinalDarkPart);

//--------------High Color Term--------------//
	//this value adjusts or masks high color strength
	float highColorMaskMap_var = saturate(toonMap.highMaskMap.g + _Tweak_HighColorStrength);
	float featherHigh = pow(blingPhong, exp2(lerp(11, 1, _HighColor_Power)));
	float hardHigh = step(1.0 - blingPhong, pow(_HighColor_Power, 5));
	//leads to 2 specular style
	float styledHighColorMask_var = highColorMaskMap_var * lerp(hardHigh, featherHigh, _Is_FeatherToHighColor);
	float3 highColor_var = Set_HighColor * styledHighColorMask_var;
	//tweak high color in dark(shadow) part
	float3 tweakedHighColor_var = highColor_var * ((1.0 - Set_FinalDarkPart) + (Set_FinalDarkPart * _Tweak_HighColorOnShadow));
	//lerp leads to if tweak HighColor in shadow part
	float3 Set_FinalHighPart = lerp(highColor_var, tweakedHighColor_var, _Is_UseTweakHighColorOnShadow);
	Set_FinalHighPart = lerp(Set_FinalHighPart, 0, _Is_Filter_HighCutPointLightColor);

	return saturate(Set_FinalBaseColor + Set_FinalHighPart);
}

#endif
#endif
