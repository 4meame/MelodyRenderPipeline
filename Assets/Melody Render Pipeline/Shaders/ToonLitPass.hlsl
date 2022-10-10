#ifndef MELODY_TOON_LIT_PASS_INCLUDED
#define MELODY_TOON_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

#include "ToonMap.hlsl"
#include "ToonUtilities.hlsl"
#include "ToonLitInput.hlsl"
#include "ToonLitCore.hlsl"

#if defined(_SIMPLE_TOON)
struct Attributes {
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
	float3 normalOS : NORMAL;
	float4 tangentOS : TANGENT;
	//Support per-instance material data
	UNITY_VERTEX_INPUT_INSTANCE_ID
	//UV Coordinate for the light map
	GI_ATTRIBUTE_DATA
};

struct Varyings {
	float4 positionCS : SV_POSITION;
	//any unused identifier can be used here
	float3 positionWS : VAR_POSITION;
	float2 baseUV : VAR_BASE_UV;
	float3 positionVS : VAR_POSITION_VS;
	float3 normalWS : VAR_NORMAL;
	float3 normalVS : VAR_NORMAL_VS;
	float4 tangentWS : VAR_TANGENT;
	//Support per-instance material data
	UNITY_VERTEX_INPUT_INSTANCE_ID
	//UV Coordinate for the light map
	GI_VARYINGS_DATA
};

Varyings ToonLitPassVertex(Attributes input) {
	Varyings output;
//extract the index from the input and store it in a global static variable
	UNITY_SETUP_INSTANCE_ID(input);
//copy the index when it exists
    UNITY_TRANSFER_INSTANCE_ID(input, output);
//extract the UV Coordinate from the light map data
	TRANSFER_GI_DATA(input, output);
	output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
	output.normalVS = TransformWorldToView(output.normalWS);

	output.positionWS = TransformObjectToWorld(input.positionOS);
	output.positionVS = TransformWorldToView(output.positionWS);
#if defined(ToonShaderIsOutline)
	float outlineWidth = GetOutlineWidth();
	output.positionWS = TransformPositionWSToOutlinePositionWS(output.positionWS, output.positionVS.z, output.normalWS, outlineWidth);
#endif
	output.positionCS = TransformWorldToHClip(output.positionWS);
	//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	output.baseUV = input.baseUV * baseST.xy + baseST.zw;

#if defined(ToonShaderIsOutline)
	//[Read ZOffset mask texture]
	//we can't use SAMPLE_TEXTURE2D() in vertex shader because ddx & ddy is unknown before rasterization, 
	//so use SAMPLE_TEXTURE2D_LOD() with an explict mip level 0, put explict mip level 0 inside the 4th component of param uv)
	float outlineZOffsetMaskTexExplictMipLevel = 0;
	//we assume it is a Black/White texture
	float outlineZOffsetMask = SAMPLE_TEXTURE2D_LOD(_OutlineZOffsetMaskTex, sampler_OutlineZOffsetMaskTex, input.baseUV, outlineZOffsetMaskTexExplictMipLevel).r;
	//[Remap ZOffset texture value]
	//flip texture read value so default black area = apply ZOffset, because usually outline mask texture are using this format(black = hide outline)
	outlineZOffsetMask = 1 - outlineZOffsetMask;
	//allow user to flip value or remap
	outlineZOffsetMask = invLerpClamp(_OutlineZOffsetMaskRemapStart, _OutlineZOffsetMaskRemapEnd, outlineZOffsetMask);
	//[Apply ZOffset, Use remapped value as ZOffset mask]
	output.positionCS = NiloGetNewClipPosWithZOffset(output.positionCS, _OutlineZOffset * outlineZOffsetMask + 0.03 * _IsFace);
#endif

#if defined(ToonShaderApplyShadowBiasFix)
	float4 positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, _LightDirection));

#if UNITY_REVERSED_Z
	positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
	positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif
	output.positionCS = positionCS;
#endif

	return output;
}

float4 ToonLitPassFragment(Varyings input) : SV_TARGET {
	UNITY_SETUP_INSTANCE_ID(input);
//Init fragment data
	Fragment fragment;
	fragment = GetFragment(input.positionCS);
//Unity Lod Group, unity_LODFade.x is factor of one and reduces to zero
	//ClipLOD(input.positionCS.xy, unity_LODFade.x);
	ClipLOD(fragment, unity_LODFade.x);

	float4 base = GetBase(input.baseUV);
	float3 emission = GetEmission(input.baseUV);
	float occlusion = GetOcclusion(input.baseUV);

#if defined(_CLIPPING)
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
#endif
	//Init surface data
	Surface surface;
	surface.color = base.rgb;
	surface.alpha = base.a;
	surface.emission = emission;
	surface.occlusion = occlusion;
	surface.position = input.positionWS;
	surface.interpolatedNormal = normalize(input.normalWS);
	surface.normal = normalize(input.normalWS);
	surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
	surface.depth = -TransformWorldToView(input.positionWS).z;
	//Init shadow data
	ShadowData shadowData = GetShadowData(surface);
	//Init main light data
	Light light = GetMainLight();

	float3 color = ShadeAllLights(surface, light, shadowData, input.baseUV);

#if defined(ToonShaderIsOutline)
	color = ConvertSurfaceColorToOutlineColor(color);
#endif

	return float4(color, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ZWrite) ? 1.0 : base.a);
}

void ToonLitShadowAlphaClipTest(Varyings input) {
	UNITY_SETUP_INSTANCE_ID(input);
	//Init fragment data
	Fragment fragment;
	fragment = GetFragment(input.positionCS);
//Unity Lod Group, unity_LODFade.x is factor of one and reduces to zero
		//ClipLOD(input.positionCS.xy, unity_LODFade.x);
	ClipLOD(fragment, unity_LODFade.x);
	float4 base = GetBase(input.baseUV);
#if defined(_SHADOWS_CLIP)
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
#elif defined(_SHADOWS_DITHER)
	float dither = InterleavedGradientNoise(fragment.positionSS, 0);
	clip(base.a - dither);
#endif
}
#endif

#if defined(_COMPLEX_TOON)
struct Attributes {
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
	float3 normalOS : NORMAL;
	float4 tangentOS : TANGENT;
	//Support per-instance material data
	UNITY_VERTEX_INPUT_INSTANCE_ID
		//UV Coordinate for the light map
		GI_ATTRIBUTE_DATA
};

struct Varyings {
	float4 positionCS : SV_POSITION;
	//any unused identifier can be used here
	float3 positionWS : VAR_POSITION;
	float2 baseUV : VAR_BASE_UV;
	float3 positionVS : VAR_POSITION_VS;
	float3 normalWS : VAR_NORMAL;
	float3 normalVS : VAR_NORMAL_VS;
	float3 tangentWS : VAR_TANGENT;
	float3 bitangentWS : VAR_BITANGENT;
	float mirrorFlag : VAR_MIRRORFLAG;
	//Support per-instance material data
	UNITY_VERTEX_INPUT_INSTANCE_ID
	//UV Coordinate for the light map
	GI_VARYINGS_DATA
};

Varyings ToonLitPassVertex(Attributes input) {
	Varyings output;
	//extract the index from the input and store it in a global static variable
	UNITY_SETUP_INSTANCE_ID(input);
	//copy the index when it exists
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	//extract the UV Coordinate from the light map data
	TRANSFER_GI_DATA(input, output);
	output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	output.tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
	output.bitangentWS = normalize(cross(output.normalWS, output.tangentWS) * input.tangentOS.w);
	output.normalVS = TransformWorldToView(output.normalWS);
	output.positionWS = TransformObjectToWorld(input.positionOS);
	output.positionVS = TransformWorldToView(output.positionWS);
	output.positionCS = TransformWorldToHClip(output.positionWS);
	output.baseUV = input.baseUV;
	//output.mirrorFlag = -1 is in mirror ( camera reflection
	float3 crossFwd = cross(UNITY_MATRIX_V[0], UNITY_MATRIX_V[1]);
	output.mirrorFlag = dot(crossFwd, UNITY_MATRIX_V[2]) < 0 ? 1 : -1;
	return output;
}

float4 ToonLitPassFragment(Varyings input) : SV_TARGET{
	UNITY_SETUP_INSTANCE_ID(input);
//Init fragment data
	Fragment fragment;
	fragment = GetFragment(input.positionCS);
//Init toon map data
	ToonMap toonMap;
	toonMap = GetToonMap(input.baseUV);
//Unity Lod Group, unity_LODFade.x is factor of one and reduces to zero
	//ClipLOD(input.positionCS.xy, unity_LODFade.x);
	ClipLOD(fragment, unity_LODFade.x);

#if defined(_CLIPPING_ON)
	//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float Set_Clipping = saturate((lerp(toonMap.clipMap.a, (1.0 - toonMap.clipMap.a), _Inverse_Clipping) + lerp(_Clipping_Level, -_Clipping_Level, _Inverse_Clipping)));
	clip(Set_Clipping - 0.5);
#endif

	float3x3 tangentTransform = float3x3(input.tangentWS, input.bitangentWS, input.normalWS);
	//transform tangent space normal map to world space
	float3 normalDirection = normalize(mul(toonMap.normalMap, tangentTransform));
	float3 viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);

//--------------------------MatCap Rotation UV-------------------------//
//---------copied from UCTS2 do not know the detail about them---------//
	float3 _Camera_Right = UNITY_MATRIX_V[0].xyz;
	float3 _Camera_Front = UNITY_MATRIX_V[2].xyz;
	//UNITY_MATRIX_V[0].xyz == world space camera Right unit vector
	//UNITY_MATRIX_V[1].xyz == world space camera Up unit vector
	//UNITY_MATRIX_V[2].xyz == -1 * world space camera Forward unit vector
	float3 _Up_Unit = float3(0, 1, 0);
	float3 _Right_Axis = cross(_Camera_Front, _Up_Unit);
	//if in mirror(reflect camera
	if (input.mirrorFlag < 0) {
		_Right_Axis = -1 * _Right_Axis;
		_Rotate_MatCapUV = -1 * _Rotate_MatCapUV;
	}
	else {

	}
	float _Camera_Right_Magnitude = sqrt(_Camera_Right.x * _Camera_Right.x + _Camera_Right.y * _Camera_Right.y + _Camera_Right.z * _Camera_Right.z);
	float _Right_Axis_Magnitude = sqrt(_Right_Axis.x * _Right_Axis.x + _Right_Axis.y * _Right_Axis.y + _Right_Axis.z * _Right_Axis.z);
	float _Camera_Roll_Cos = dot(_Right_Axis, _Camera_Right) / (_Right_Axis_Magnitude * _Camera_Right_Magnitude);
	float _Camera_Roll = acos(clamp(_Camera_Roll_Cos, -1, 1));
	float _Camera_Dir = _Camera_Right.y < 0 ? -1 : 1;
	float _Rot_MatCapUV_var_ang = (_Rotate_MatCapUV * 3.141592654) - _Camera_Dir * _Camera_Roll * _CameraRolling_Stabilizer;
	float2 _Rot_MatCapNmUV_var = RotateUV(input.baseUV, (_Rotate_NormalMapForMatCapUV * 3.141592654), float2(0.5, 0.5), 1.0);
	//recalculate rotated UV of the normal for matCap
	toonMap.normalForMatCap = DecodeNormal(SAMPLE_TEXTURE2D(_NormalMapForMatCap, sampler_NormalMapForMatCap, TRANSFORM_TEX(_Rot_MatCapNmUV_var, _NormalMapForMatCap)), _BumpScaleMatCap);
	float3 viewNormal = (mul(UNITY_MATRIX_V, float4(lerp(normalDirection, mul(toonMap.normalForMatCap.rgb, tangentTransform).rgb, _Is_NormalMapToMatCap), 0))).rgb;
	float3 NormalBlend_MatcapUV_Detail = viewNormal.rgb * float3(-1, -1, 1);
	float3 NormalBlend_MatcapUV_Base = (mul(UNITY_MATRIX_V, float4(viewDirection, 0)).rgb * float3(-1, -1, 1)) + float3(0, 0, 1);
	float3 noSknewViewNormal = NormalBlend_MatcapUV_Base * dot(NormalBlend_MatcapUV_Base, NormalBlend_MatcapUV_Detail) / NormalBlend_MatcapUV_Base.b - NormalBlend_MatcapUV_Detail;
	float2 _ViewNormalAsMatCapUV = (lerp(noSknewViewNormal, viewNormal, _Is_Ortho).rg * 0.5) + 0.5;
	float2 _Rot_MatCapUV_var = RotateUV((0.0 + ((_ViewNormalAsMatCapUV - (0.0 + _Tweak_MatCapUV)) * (1.0 - 0.0)) / ((1.0 - _Tweak_MatCapUV) - (0.0 + _Tweak_MatCapUV))), _Rot_MatCapUV_var_ang, float2(0.5, 0.5), 1.0);
	//if in mirror(reflect camera
	if (input.mirrorFlag < 0) {
		_Rot_MatCapUV_var.x = 1 - _Rot_MatCapUV_var.x;
	}
	else {

	}
	//recalculate rotated UV of matCap
	toonMap.matCapMap = SAMPLE_TEXTURE2D_LOD(_MatCap_Map, sampler_MatCap_Map, TRANSFORM_TEX(_Rot_MatCapUV_var, _MatCap_Map), _BlurLevelMatCap);
//--------------------------MatCap Rotation UV-------------------------//

	//Init surface data
	Surface surface;
	surface.color = 1;
	surface.alpha = 1;
	surface.emission = 0;
	surface.position = input.positionWS;
	surface.normal = normalDirection;
	surface.interpolatedNormal = normalize(input.normalWS);
	surface.viewDirection = viewDirection;
	surface.depth = -TransformWorldToView(input.positionWS).z;
	//Init shadow data
	ShadowData shadowData = GetShadowData(surface);
	//Init main light data
	Light mainLight = GetMainLight();

	float3 mainLightResult = ShadeDirectionalLight(surface, fragment, mainLight, shadowData, toonMap);
	float3 emission = ShadeEmission(surface, toonMap, _Camera_Dir, _Camera_Roll, input.baseUV, input.mirrorFlag);
	float3 gi = ShadeGI();

	float3 otherLightResult = 0;
	for (int i = 0; i < GetOtherLightCount(); i++) {
		Light otherLight = GetOtherLight(i, surface, shadowData);
		otherLightResult += ShadeOtherLight(surface, otherLight, toonMap);
	}

	float3 finalResult = mainLightResult + otherLightResult + gi + emission;
	return float4(finalResult, _ZWrite ? 1.0 : _BaseColor.a);
}

void ToonLitShadowAlphaClipTest(Varyings input) {
	UNITY_SETUP_INSTANCE_ID(input);
	//Init fragment data
	Fragment fragment;
	fragment = GetFragment(input.positionCS);
	//Init toon map data
	ToonMap toonMap;
	toonMap = GetToonMap(input.baseUV);
	//Unity Lod Group, unity_LODFade.x is factor of one and reduces to zero
		//ClipLOD(input.positionCS.xy, unity_LODFade.x);
	ClipLOD(fragment, unity_LODFade.x);

	//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float Set_Clipping = saturate((lerp(toonMap.clipMap.a, (1.0 - toonMap.clipMap.a), _Inverse_Clipping) + lerp(_Clipping_Level, -_Clipping_Level, _Inverse_Clipping)));
#if defined(_SHADOWS_CLIP)
	clip(Set_Clipping - 0.5);
#elif defined(_SHADOWS_DITHER)
	//float dither = InterleavedGradientNoise(input.positionCS.xy, 0);
	float dither = InterleavedGradientNoise(fragment.positionSS, 0);
	clip(Set_Clipping - dither);
#endif
}
#endif

#endif
