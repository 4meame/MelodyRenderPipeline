#ifndef MELODY_STOCHASTIC_SSR_PASS_INCLUDED
#define MELODY_STOCHASTIC_SSR_PASS_INCLUDED

#include "../ShaderLibrary/ScreenSpaceTrace.hlsl"

TEXTURE2D(_SSR_SceneColor_RT);
TEXTURE2D(_SSR_Noise);
TEXTURE2D(_SSR_PreintegratedGF);
TEXTURE2D(_SSR_HierarchicalDepth_RT);
TEXTURE2D(_SSR_CombienReflection_RT);
TEXTURE2D(_SSR_RayCastRT);
TEXTURE2D(_SSR_RayMask_RT);
TEXTURE2D(_SSR_Spatial_RT);
TEXTURE2D(_SSR_TemporalPrev_RT);
TEXTURE2D(_SSR_TemporalCurr_RT);

float _SSR_BRDFBias;
float _SSR_ScreenFade;
float _SSR_Thickness;
int _SSR_RayStepSize;
int _SSR_NumRays;
int _SSR_TraceDistance;
float3 _SSR_CameraClipInfo;
float4 _SSR_ScreenSize;
float4 _SSR_RayCastSize;
float4 _SSR_NoiseSize;
float4 _SSR_Jitter;
float4 _SSR_RandomSeed;
float4 _SSR_ProjInfo;
//linear trace
int _SSR_NumSteps_Linear;
int _SSR_BackwardsRay;
int _SSR_CullBack;
int _SSR_TraceBehind;
//HiZ trace
int _SSR_NumSteps_HiZ;
int _SSR_HiZ_MaxLevel;
int _SSR_HiZ_StartLevel;
int _SSR_HiZ_StopLevel;
int _SSR_HiZ_PrevDepthLevel;
//denoise
int _SSR_NumResolver;
float _SSR_TemporalScale;
float _SSR_TemporalWeight;

float4x4 _SSR_ProjectionMatrix;
float4x4 _SSR_InverseProjectionMatrix;
float4x4 _SSR_ViewProjectionMatrix;
float4x4 _SSR_InverseViewProjectionMatrix;
float4x4 _SSR_WorldToCameraMatrix;
float4x4 _SSR_CameraToWorldMatrix;
float4x4 _SSR_LastFrameViewProjectionMatrix;
float4x4 _SSR_ProjectToPixelMatrix;

struct Varyings {
	float4 positionCS : SV_POSITION;
	float2 screenUV : VAR_SCREEN_UV;
};

//vertexID is the clockwise index of a triangle : 0,1,2
Varyings DefaultPassVertex(uint vertexID : SV_VertexID) {
	Varyings output;
	//make the [-1, 1] NDC, visible UV coordinates cover the 0-1 range
	output.positionCS = float4(vertexID <= 1 ? -1.0 : 3.0,
		vertexID == 1 ? 3.0 : -1.0,
		0.0, 1.0);
	output.screenUV = float2(vertexID <= 1 ? 0.0 : 2.0,
		vertexID == 1 ? 2.0 : 0.0);
	//some graphics APIs have the texture V coordinate start at the top while others have it start at the bottom
	if (_ProjectionParams.x < 0.0) {
		output.screenUV.y = 1.0 - output.screenUV.y;
	}
	return output;
}

float Luminance(float3 color) {
	return dot(color, float3(0.2126, 0.7152, 0.0722));
}

float3 SSR_BRDF(float3 viewDir, float3 lightDir, float3 normal, float roughness) {
	float3 halfVector = normalize(viewDir + lightDir);
	float ndoth = max(dot(normal, halfVector), 0);
	float ndotl = max(dot(normal, lightDir), 0);
	float ndotv = max(dot(normal, viewDir), 0);
	float D = D_GGX(ndoth, roughness);
	float G = Vis_SmithGGXCorrelated(ndotl, ndotv, roughness);
	return max(0, D * G);
}

//get Hierarchical ZBuffer
float GetHierarchicalZBuffer(Varyings input) : SV_TARGET{
	float2 uv = input.screenUV;
	float4 minDepth = float4(
		_SSR_HierarchicalDepth_RT.SampleLevel(sampler_point_clamp, uv, _SSR_HiZ_PrevDepthLevel, int2(-1.0, -1.0)).r,
		_SSR_HierarchicalDepth_RT.SampleLevel(sampler_point_clamp, uv, _SSR_HiZ_PrevDepthLevel, int2(-1.0, 1.0)).r,
		_SSR_HierarchicalDepth_RT.SampleLevel(sampler_point_clamp, uv, _SSR_HiZ_PrevDepthLevel, int2(1.0, -1.0)).r,
		_SSR_HierarchicalDepth_RT.SampleLevel(sampler_point_clamp, uv, _SSR_HiZ_PrevDepthLevel, int2(1.0, 1.0)).r
		);
	//sample pixel surrounds and pick minnset depth
	return max(max(minDepth.r, minDepth.g), max(minDepth.b, minDepth.a));
}

//2D linear trace sampler(single spp: sample per pixel, pdf : probability distribution function)
void LinearTraceSingleSPP(Varyings input, out float4 RayHit_PDF : SV_TARGET0, out float4 Mask : SV_TARGET1) {
	float2 uv = input.screenUV;
	float sceneDepth = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_point_clamp, uv, 0);
	float roughness = SAMPLE_TEXTURE2D_LOD(_CameraSpecularTexture, sampler_linear_clamp, uv, 0).a;
	roughness = clamp(1 - roughness, 0.02, 1);
	float4 depthNormalTexture = SAMPLE_TEXTURE2D_LOD(_CameraDepthNormalTexture, sampler_point_clamp, uv, 0);
	float3 viewNormal = DecodeViewNormalStereo(depthNormalTexture);
	//const property
	float Ray_HitMask = 0.0;
	float Ray_NumMarch = 0.0;
	float2 Ray_HitUV = 0.0;
	float3 Ray_HitPoint = 0.0;
	//begin trace
	float4 screenTexelSize = float4(1 / _SSR_ScreenSize.x, 1 / _SSR_ScreenSize.y, _SSR_ScreenSize.x, _SSR_ScreenSize.y);
	float3 Ray_Origin_VS = GetPosition(_CameraDepthTexture, screenTexelSize, _SSR_ProjInfo, uv);
	float Ray_Bump = max(-0.01 * Ray_Origin_VS.z, 0.001);
	float2 hash = SAMPLE_TEXTURE2D_LOD(_SSR_Noise, sampler_point_repeat, float2((uv + _SSR_Jitter.zw) * _SSR_RayCastSize.xy / _SSR_NoiseSize.xy), 0).xy;
	float Jitter = hash.x + hash.y;
	hash.y = lerp(hash.y, 0.0, _SSR_BRDFBias);
	//calculate half vector by important sample
	float4 H = 0.0;
	if (roughness > 0.1) {
		H = TangentToWorld(ImportanceSampleGGX(hash, roughness), float4(viewNormal, 1.0));
	}
	else {
		H = float4(viewNormal, 1.0);
	}
	float3 Ray_Dir_VS = reflect(normalize(Ray_Origin_VS), H.xyz);
	//early exit if is opposite direction, or trace backward ray
	UNITY_BRANCH
	if (_SSR_BackwardsRay == 0 && Ray_Dir_VS.z > 0) {
		RayHit_PDF = 0;
		Mask = 0;
		return;
	}
	//ray trace
	bool hit = Linear2D_Trace(_CameraDepthTexture, Ray_Origin_VS + viewNormal * Ray_Bump, Ray_Dir_VS, _SSR_ProjectToPixelMatrix, _SSR_ScreenSize, Jitter, _SSR_NumSteps_Linear, _SSR_Thickness, _SSR_TraceDistance, Ray_HitUV, _SSR_RayStepSize, _SSR_TraceBehind == 1, Ray_HitPoint, Ray_NumMarch);
	Ray_HitUV /= _SSR_ScreenSize;
	UNITY_BRANCH
	if (hit) {
		Ray_HitMask = Square(1 - max(2 * float(Ray_NumMarch) / float(_SSR_NumSteps_Linear) - 1, 0));
		Ray_HitMask *= saturate(((_SSR_TraceDistance - dot(Ray_HitPoint - Ray_Origin_VS, Ray_Dir_VS))));
		//calculate backward ray mask
		if (_SSR_CullBack == 0) {
			float4 Ray_HitDepthNormal = SAMPLE_TEXTURE2D_LOD(_CameraDepthNormalTexture, sampler_point_clamp, Ray_HitUV, 0);
			float3 Ray_HitNormal_VS = DecodeViewNormalStereo(Ray_HitDepthNormal);
			float3 Ray_HitNormal_WS = mul(_SSR_CameraToWorldMatrix, float4(Ray_HitNormal_VS, 0)).xyz;
			float3 Ray_Dir_WS = mul(_SSR_CameraToWorldMatrix, float4(Ray_Dir_VS, 0)).xyz;
			if (dot(Ray_HitNormal_WS, Ray_Dir_WS) > 0)
				Ray_HitMask = 0;
		}
	}

	RayHit_PDF = float4(Ray_HitUV, SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_point_clamp, Ray_HitUV, 0).r, H.a);
	Mask = Square(Ray_HitMask * GetScreenFadeBord(Ray_HitUV, _SSR_ScreenFade));
}


//2D linear trace sampler(mutilple spp: samples per pixel, pdf : probability distribution function)
void LinearTraceMultiSPP(Varyings input, out float4 SSRColor_PDF : SV_TARGET0, out float4 Mask_Depth_HitUV : SV_TARGET1) {
	float2 uv = input.screenUV;
	float sceneDepth = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_point_clamp, uv, 0);
	float roughness = SAMPLE_TEXTURE2D_LOD(_CameraSpecularTexture, sampler_linear_clamp, uv, 0).a;
	roughness = clamp(1 - roughness, 0.02, 1);
	float4 depthNormalTexture = SAMPLE_TEXTURE2D_LOD(_CameraDepthNormalTexture, sampler_point_clamp, uv, 0);
	float3 viewNormal = DecodeViewNormalStereo(depthNormalTexture);
	//const property
	float Ray_HitMask = 0.0;
	float Out_Fade = 0.0;
	float Out_Mask = 0.0;
	float Out_PDF = 0.0;
	float Out_RayDepth = 0.0;
	float2 Out_UV = 0;
	float4 Out_Color = 0;
	//begin trace
	float4 screenTexelSize = float4(1 / _SSR_ScreenSize.x, 1 / _SSR_ScreenSize.y, _SSR_ScreenSize.x, _SSR_ScreenSize.y);
	float3 Ray_Origin_VS = GetPosition(_CameraDepthTexture, screenTexelSize, _SSR_ProjInfo, uv);
	float Ray_Bump = max(-0.01 * Ray_Origin_VS.z, 0.001);
	float2 blueNoise = SAMPLE_TEXTURE2D_LOD(_SSR_Noise, sampler_point_repeat, float2((uv + _SSR_Jitter.zw) * _SSR_RayCastSize.xy / _SSR_NoiseSize.xy), 0).xy;
	//loop all multi rays
	for (uint i = 0; i < (uint)_SSR_NumRays; i++) {
		float2 hash = SAMPLE_TEXTURE2D_LOD(_SSR_Noise, sampler_point_repeat, float2((uv + _SSR_Jitter.zw) * _SSR_RayCastSize.xy / _SSR_NoiseSize.xy), 0).xy;
		hash.y = lerp(hash.y, 0.0, _SSR_BRDFBias);
		//calculate half vector by important sample
		float4 H = 0.0;
		if (roughness > 0.1) {
			H = TangentToWorld(ImportanceSampleGGX(hash, roughness), float4(viewNormal, 1.0));
		}
		else {
			H = float4(viewNormal, 1.0);
		}
		float3 Ray_Dir_VS = reflect(normalize(Ray_Origin_VS), H.xyz);
		//early exit if is opposite direction, or trace backward ray
		UNITY_BRANCH
		if (_SSR_BackwardsRay == 0 && Ray_Dir_VS.z > 0) {
			SSRColor_PDF = 0;
			Mask_Depth_HitUV = 0;
			return;
		}
		//ray trace
		float Jitter = blueNoise.x + blueNoise.y;
		float Ray_NumMarch = 0.0;
		float2 Ray_HitUV = 0.0;
		float3 Ray_HitPoint = 0.0;
		bool hit = Linear2D_Trace(_CameraDepthTexture, Ray_Origin_VS + viewNormal * Ray_Bump, Ray_Dir_VS, _SSR_ProjectToPixelMatrix, _SSR_ScreenSize, Jitter, _SSR_NumSteps_Linear, _SSR_Thickness, _SSR_TraceDistance, Ray_HitUV, _SSR_RayStepSize, _SSR_TraceBehind == 1, Ray_HitPoint, Ray_NumMarch);
		Ray_HitUV /= _SSR_ScreenSize;
		UNITY_BRANCH
		if (hit) {
			Ray_HitMask = Square(1 - max(2 * float(Ray_NumMarch) / float(_SSR_NumSteps_Linear) - 1, 0));
			Ray_HitMask *= saturate(((_SSR_TraceDistance - dot(Ray_HitPoint - Ray_Origin_VS, Ray_Dir_VS))));
			//calculate backward ray mask
			if (_SSR_CullBack == 0) {
				float4 Ray_HitDepthNormal = SAMPLE_TEXTURE2D_LOD(_CameraDepthNormalTexture, sampler_point_clamp, Ray_HitUV, 0);
				float3 Ray_HitNormal_VS = DecodeViewNormalStereo(Ray_HitDepthNormal);
				float3 Ray_HitNormal_WS = mul(_SSR_CameraToWorldMatrix, float4(Ray_HitNormal_VS, 0)).xyz;
				float3 Ray_Dir_WS = mul(_SSR_CameraToWorldMatrix, float4(Ray_Dir_VS, 0)).xyz;
				if (dot(Ray_HitNormal_WS, Ray_Dir_WS) > 0)
					Ray_HitMask = 0;
			}
		}
		//calculate reflect color
		float4 SampleColor = SAMPLE_TEXTURE2D_LOD(_SSR_SceneColor_RT, sampler_linear_clamp, Ray_HitUV, 0);
		SampleColor.rgb /= 1 + Luminance(SampleColor.rgb);
		//accumulate sample result
		Out_Color += SampleColor;
		Out_Mask += Ray_HitMask * GetScreenFadeBord(Ray_HitUV, _SSR_ScreenFade);
		Out_RayDepth += SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_point_clamp, Ray_HitUV, 0).r;
		Out_UV += Ray_HitUV;
		Out_PDF += H.a;
	}
	//output
	Out_Color /= _SSR_NumRays;
	Out_Color.rgb /= 1 - Luminance(Out_Color.rgb);
	Out_Mask /= _SSR_NumRays;
	Out_RayDepth /= _SSR_NumRays;
	Out_UV /= _SSR_NumRays;
	Out_PDF /= _SSR_NumRays;

	SSRColor_PDF = float4(Out_Color.rgb, Out_PDF);
	Mask_Depth_HitUV = float4(Square(Out_Mask), Out_RayDepth, Out_UV);
}


static const int2 offset[9] = { 
	int2(-2.0, -2.0), int2(0.0, -2.0), int2(2.0, -2.0), 
	int2(-2.0, 0.0), int2(0.0, 0.0), int2(2.0, 0.0), 
	int2(-2.0, 2.0), int2(0.0, 2.0), int2(2.0, 2.0) 
};


#endif
