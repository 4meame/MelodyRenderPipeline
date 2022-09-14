#ifndef MELODY_STOCHASTIC_SSR_PASS_INCLUDED
#define MELODY_STOCHASTIC_SSR_PASS_INCLUDED

#include "../ShaderLibrary/ScreenSpaceTrace.hlsl"
#include "../ShaderLibrary/ImageBasedLighting.hlsl"

TEXTURE2D(_SSR_SceneColor_RT);
TEXTURE2D(_SSR_Noise);
TEXTURE2D(_SSR_PreintegratedGF);
//store RO
TEXTURE2D(_SSAO_Filtered);
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
//debug
int _DebugPass;

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
	float roughness = clamp(SAMPLE_TEXTURE2D_LOD(_CameraSpecularTexture, sampler_point_clamp, uv, 0).a, 0.02, 1);
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
	float roughness = clamp(SAMPLE_TEXTURE2D_LOD(_CameraSpecularTexture, sampler_point_clamp, uv, 0).a, 0.02, 1);
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
		//calculate reflect color, last frame reflect color can be the light source for this frame
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

static const int2 offset1[9] = {
	int2(-1.0, -1.0), int2(0.0, -1.0), int2(1.0, -1.0),
	int2(-1.0, 0.0), int2(0.0, 0.0), int2(1.0, 0.0),
	int2(-1.0, 1.0), int2(0.0, 2.0), int2(1.0, 1.0)
};

static const int2 offset2[9] = { 
	int2(-2.0, -2.0), int2(0.0, -2.0), int2(2.0, -2.0), 
	int2(-2.0, 0.0), int2(0.0, 0.0), int2(2.0, 0.0), 
	int2(-2.0, 2.0), int2(0.0, 2.0), int2(2.0, 2.0) 
};

float4 SpatioFilterSingleSPP(Varyings input) : SV_TARGET {
	float2 uv = input.screenUV;
	//sample buffers' properties
	float roughness = clamp(SAMPLE_TEXTURE2D_LOD(_CameraSpecularTexture, sampler_point_clamp, uv, 0).a, 0.02, 1);
	float sceneDepth = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_point_clamp, uv, 0);
	float4 depthNormalTexture = SAMPLE_TEXTURE2D_LOD(_CameraDepthNormalTexture, sampler_point_clamp, uv, 0);
	float3 viewNormal = DecodeViewNormalStereo(depthNormalTexture);
	//get screen pos
	float3 screenPos = GetScreenSpacePos(uv, sceneDepth);
	float3 viewPos = GetViewSpacePos(screenPos, _SSR_InverseProjectionMatrix);
	//make offset rotate matrix to calculate neighbor uv
	float2 blueNoise = SAMPLE_TEXTURE2D_LOD(_SSR_Noise, sampler_point_repeat, float2((uv + _SSR_Jitter.zw) * _SSR_RayCastSize.xy / _SSR_NoiseSize.xy), 0) * 2 - 1;
	float2x2 offsetRotationMatrix = float2x2(blueNoise.x, blueNoise.y, -blueNoise.y, -blueNoise.x);

	float NumWeight, Weight;
	float2 Offset_UV, Neighbor_UV;
	float4 SampleColor, ReflecttionColor;
	for (int i = 0; i < _SSR_NumResolver; i++) {
		Offset_UV = mul(offsetRotationMatrix, offset2[i] * (1 / _SSR_ScreenSize.xy));
		Neighbor_UV = uv + Offset_UV;
		//_SSR_RayCastRT stores rg : hit uv b : depth a : pdf
		float4 HitUV_PDF = SAMPLE_TEXTURE2D_LOD(_SSR_RayCastRT, sampler_point_clamp, Neighbor_UV, 0);
		float3 Hit_ViewPos = GetViewSpacePos(float3(HitUV_PDF.rg, HitUV_PDF.b), _SSR_InverseProjectionMatrix);
		// spatio sampler : 
		// We assume that the hit point of the neighbor's ray is also visible for our ray, and we blindly pretend
		// that the current pixel shot that ray. To do that, we treat the hit point as a tiny light source. To calculate
		// a lighting contribution from it, we evaluate the BRDF. Finally, we need to account for the probability of getting
		// this specific position of the "light source", and that is approximately 1/PDF, where PDF comes from the neighbor.
		// Finally, the weight is BRDF/PDF. BRDF uses the local pixel's normal and roughness, but PDF comes from the neighbor.
		Weight = SSR_BRDF(normalize(-viewPos), normalize(Hit_ViewPos - viewPos), viewNormal, roughness) / max(1e-5, HitUV_PDF.a);
		SampleColor.rgb = SAMPLE_TEXTURE2D_LOD(_SSR_SceneColor_RT, sampler_linear_clamp, HitUV_PDF.rg, 0).rgb;
		SampleColor.rgb /= 1 + Luminance(SampleColor.rgb);
		SampleColor.a = SAMPLE_TEXTURE2D_LOD(_SSR_RayMask_RT, sampler_point_clamp, Neighbor_UV, 0).r;
		//calculate weight
		ReflecttionColor += SampleColor * Weight;
		NumWeight += Weight;
	}
	ReflecttionColor /= NumWeight;
	ReflecttionColor.rgb /= 1 - Luminance(ReflecttionColor.rgb);
	ReflecttionColor = max(1e-5, ReflecttionColor);
	return ReflecttionColor;
}

//spatio filter weight by brdf
float4 SpatioFilterMultiSPP(Varyings input) : SV_TARGET {
	float2 uv = input.screenUV;
	//sample buffers' properties
	float roughness = clamp(SAMPLE_TEXTURE2D_LOD(_CameraSpecularTexture, sampler_point_clamp, uv, 0).a, 0.02, 1);
	float sceneDepth = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_point_clamp, uv, 0);
	float4 depthNormalTexture = SAMPLE_TEXTURE2D_LOD(_CameraDepthNormalTexture, sampler_point_clamp, uv, 0);
	float3 viewNormal = DecodeViewNormalStereo(depthNormalTexture);
	//get screen pos
	float3 screenPos = GetScreenSpacePos(uv, sceneDepth);
	float3 viewPos = GetViewSpacePos(screenPos, _SSR_InverseProjectionMatrix);
	//make offset rotate matrix
	float2 blueNoise = SAMPLE_TEXTURE2D_LOD(_SSR_Noise, sampler_point_repeat, float2((uv + _SSR_Jitter.zw) * _SSR_RayCastSize.xy / _SSR_NoiseSize.xy), 0) * 2 - 1;
	float2x2 offsetRotationMatrix = float2x2(blueNoise.x, blueNoise.y, -blueNoise.y, -blueNoise.x);

	float NumWeight, Weight;
	float2 Offset_UV, Neighbor_UV;
	float3 Hit_ViewPos;
	float4 SampleColor, ReflecttionColor;
	for (int i = 0; i < _SSR_NumResolver; i++) {
		Offset_UV = mul(offsetRotationMatrix, offset2[i] * (1 / _SSR_ScreenSize.xy));
		Neighbor_UV = uv + Offset_UV;
		//_SSR_RayCastRT stores rg : hit uv b : depth a : pdf
		float PDF = SAMPLE_TEXTURE2D_LOD(_SSR_RayCastRT, sampler_point_clamp, Neighbor_UV, 0).a;
		float4 Hit_Mask_Depth_UV = SAMPLE_TEXTURE2D_LOD(_SSR_RayMask_RT, sampler_point_clamp, Neighbor_UV, 0);
		float3 Hit_ViewPos = GetViewSpacePos(float3(Hit_Mask_Depth_UV.ba, Hit_Mask_Depth_UV.g), _SSR_InverseProjectionMatrix);
		//spatio sampler
		Weight = SSR_BRDF(normalize(-viewPos), normalize(Hit_ViewPos - viewPos), viewNormal, roughness) / max(1e-5, PDF);
		SampleColor.rgb = SAMPLE_TEXTURE2D_LOD(_SSR_RayCastRT, sampler_linear_clamp, Neighbor_UV, 0).rgb;
		SampleColor.a = SAMPLE_TEXTURE2D_LOD(_SSR_RayMask_RT, sampler_point_clamp, Neighbor_UV, 0).r;
		//calculate weight
		ReflecttionColor += SampleColor * Weight;
		NumWeight += Weight;
	}
	ReflecttionColor /= NumWeight;
	ReflecttionColor = max(1e-5, ReflecttionColor);
	return ReflecttionColor;
}

//temporal filter by reproject
float4 TemporalFilterSingelSSP(Varyings input) : SV_TARGET{
	float2 uv = input.screenUV;
	float hitDepth = SAMPLE_TEXTURE2D_LOD(_SSR_RayCastRT, sampler_point_clamp, uv, 0).b;
	float4 depthNormalTexture = SAMPLE_TEXTURE2D_LOD(_CameraDepthNormalTexture, sampler_point_clamp, uv, 0);
	float3 viewNormal = DecodeViewNormalStereo(depthNormalTexture);
	float3 worldNormal = mul(_SSR_CameraToWorldMatrix, float4(viewNormal, 0)).xyz;
	//get reprojection velocity
	float2 depthVelocity = SAMPLE_TEXTURE2D_LOD(_CameraMotionVectorTexture, sampler_point_clamp, uv, 0).rg;
	float2 rayVelocity = GetMotionVector(hitDepth, uv, _SSR_InverseViewProjectionMatrix, _SSR_LastFrameViewProjectionMatrix, _SSR_ViewProjectionMatrix);
	float velocityWeight = saturate(dot(worldNormal, half3(0, 1, 0)));
	float2 velocity = lerp(depthVelocity, rayVelocity, velocityWeight);
	//AABB clipping
	float SSR_Variance = 0;
	float4 SSR_CurrColor = 0;
	float4 SSR_MinColor, SSR_MaxColor;
	float4 SampleColors[9];
	for (uint i = 0; i < 9; i++) {
		SampleColors[i] = SAMPLE_TEXTURE2D_LOD(_SSR_Spatial_RT, sampler_linear_clamp, uv + (offset1[i] / _SSR_ScreenSize.xy), 0);
	}
	float4 m1 = 0.0;
	float4 m2 = 0.0;
	for (uint x = 0; x < 9; x++) {
		m1 += SampleColors[x];
		m2 += SampleColors[x] * SampleColors[x];
	}
	float4 mean = m1 / 9.0;
	float4 stddev = sqrt((m2 / 9.0) - pow2(mean));
	SSR_MinColor = mean - _SSR_TemporalScale * stddev;
	SSR_MaxColor = mean + _SSR_TemporalScale * stddev;
	SSR_CurrColor = SampleColors[4];
	SSR_MinColor = min(SSR_MinColor, SSR_CurrColor);
	SSR_MaxColor = max(SSR_MaxColor, SSR_CurrColor);
	float4 TotalVariance = 0;
	for (uint n = 0; n < 9; n++) {
		TotalVariance += pow2(Luminance(SampleColors[n]) - Luminance(mean));
	}
	SSR_Variance = saturate((TotalVariance / 9) * 256);
	SSR_Variance *= SSR_CurrColor.a;
	//clamp temporal color
	float4 SSR_PrevColor = SAMPLE_TEXTURE2D_LOD(_SSR_TemporalPrev_RT, sampler_linear_clamp, uv - velocity, 0);
	SSR_PrevColor = clamp(SSR_PrevColor, SSR_MinColor, SSR_MaxColor);
	//combine
	float Temporal_BlendWeight = saturate(_SSR_TemporalWeight * (1 - length(velocity) * 8));
	float4 ReflectionColor = lerp(SSR_CurrColor, SSR_PrevColor, Temporal_BlendWeight);
	return ReflectionColor;
}

float4 TemporalFilterMultiSSP(Varyings input) : SV_TARGET {
	float2 uv = input.screenUV;
	float hitDepth = SAMPLE_TEXTURE2D_LOD(_SSR_RayMask_RT, sampler_point_clamp, uv, 0).g;
	float4 depthNormalTexture = SAMPLE_TEXTURE2D_LOD(_CameraDepthNormalTexture, sampler_point_clamp, uv, 0);
	float3 viewNormal = DecodeViewNormalStereo(depthNormalTexture);
	float3 worldNormal = mul(_SSR_CameraToWorldMatrix, float4(viewNormal, 0)).xyz;
	//get reprojection velocity
	float2 depthVelocity = SAMPLE_TEXTURE2D_LOD(_CameraMotionVectorTexture, sampler_point_clamp, uv, 0).rg;
	float2 rayVelocity = GetMotionVector(hitDepth, uv, _SSR_InverseViewProjectionMatrix, _SSR_LastFrameViewProjectionMatrix, _SSR_ViewProjectionMatrix);
	float velocityWeight = saturate(dot(worldNormal, half3(0, 1, 0)));
	float2 velocity = lerp(depthVelocity, rayVelocity, velocityWeight);
	//AABB clipping
	float SSR_Variance = 0;
	float4 SSR_CurrColor = 0;
	float4 SSR_MinColor, SSR_MaxColor;
	float4 SampleColors[9];
	for (uint i = 0; i < 9; i++) {
		SampleColors[i] = SAMPLE_TEXTURE2D_LOD(_SSR_Spatial_RT, sampler_linear_clamp, uv + (offset1[i] / _SSR_ScreenSize.xy), 0);
	}
	float4 m1 = 0.0;
	float4 m2 = 0.0;
	for (uint x = 0; x < 9; x++) {
		m1 += SampleColors[x];
		m2 += SampleColors[x] * SampleColors[x];
	}
	float4 mean = m1 / 9.0;
	float4 stddev = sqrt((m2 / 9.0) - pow2(mean));
	SSR_MinColor = mean - _SSR_TemporalScale * stddev;
	SSR_MaxColor = mean + _SSR_TemporalScale * stddev;
	SSR_CurrColor = SampleColors[4];
	SSR_MinColor = min(SSR_MinColor, SSR_CurrColor);
	SSR_MaxColor = max(SSR_MaxColor, SSR_CurrColor);
	float4 TotalVariance = 0;
	for (uint n = 0; n < 9; n++) {
		TotalVariance += pow2(Luminance(SampleColors[n]) - Luminance(mean));
	}
	SSR_Variance = saturate((TotalVariance / 9) * 256);
	SSR_Variance *= SSR_CurrColor.a;
	//clamp temporal color
	float4 SSR_PrevColor = SAMPLE_TEXTURE2D_LOD(_SSR_TemporalPrev_RT, sampler_linear_clamp, uv - velocity, 0);
	SSR_PrevColor = clamp(SSR_PrevColor, SSR_MinColor, SSR_MaxColor);
	//combine
	float Temporal_BlendWeight = saturate(_SSR_TemporalWeight * (1 - length(velocity) * 8));
	float4 ReflectionColor = lerp(SSR_CurrColor, SSR_PrevColor, Temporal_BlendWeight);
	return ReflectionColor;
}

float4 CombineReflectionColor(Varyings input) : SV_TARGET {
	float2 uv = input.screenUV;
	//sample buffers' properties
	float4 specular = SAMPLE_TEXTURE2D_LOD(_CameraSpecularTexture, sampler_point_clamp, uv, 0);
	float roughness = clamp(specular, 0.02, 1.0);
	float4 depthNormalTexture = SAMPLE_TEXTURE2D_LOD(_CameraDepthNormalTexture, sampler_point_clamp, uv, 0);
	float3 viewNormal = DecodeViewNormalStereo(depthNormalTexture);
	//NOTE HERE : FOR w component, point : 1, direction : 0
	float3 worldNormal = normalize(mul(_SSR_CameraToWorldMatrix, float4(viewNormal, 0))).xyz;
	float sceneDepth = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_point_clamp, uv, 0);
	//get screen pos and view direction
	float3 screenPos = GetScreenSpacePos(uv, sceneDepth);
	float3 worldPos = GetViewSpacePos(screenPos, _SSR_InverseViewProjectionMatrix);
	float3 viewDir = normalize(worldPos - _WorldSpaceCameraPos);
	float ndotv = saturate(dot(worldNormal, -viewDir));
	//preintegrated DGF lut
	float3 Enviorfilter_GFD = SAMPLE_TEXTURE2D_LOD(_SSR_PreintegratedGF, sampler_linear_clamp, float2(roughness, ndotv), 0).rgb;
	float3 ReflectionGF = lerp(saturate(50.0 * specular.g) * Enviorfilter_GFD.ggg, Enviorfilter_GFD.rrr, specular.rgb);
	float4 PreintegratedGF =  float4(ReflectionGF, Enviorfilter_GFD.b);
	//sample ro if we have
	float ReflectionOcclusion = saturate(SAMPLE_TEXTURE2D_LOD(_SSAO_Filtered, sampler_linear_clamp, uv, 0).g);
	//ReflectionOcclusion = ReflectionOcclusion == 0.5 ? 1 : ReflectionOcclusion;
	ReflectionOcclusion = 1;
	float4 SceneColor = SAMPLE_TEXTURE2D_LOD(_SSR_SceneColor_RT, sampler_linear_clamp, uv, 0);
	float4 SSRColor = SAMPLE_TEXTURE2D_LOD(_SSR_TemporalCurr_RT, sampler_linear_clamp, uv, 0);
	float4 CubemapColor = SAMPLE_TEXTURE2D_LOD(_CameraReflectionsTexture, sampler_linear_clamp, uv, 0) * ReflectionOcclusion;
	//combine reflection and cubemap and add it to the scene color
	SceneColor.rgb = max(1e-5, SceneColor.rgb - CubemapColor.rgb);
	float SSRMask = Square(SSRColor.a);
	float4 ReflectionColor = (CubemapColor * (1 - SSRMask)) + (SSRColor * PreintegratedGF * SSRMask * ReflectionOcclusion);
	
	if (_DebugPass == 0)
		SceneColor.rgb += ReflectionColor;
	else if (_DebugPass == 1)
		SceneColor.rgb += (SSRColor * PreintegratedGF * SSRMask * ReflectionOcclusion);
	else if (_DebugPass == 2)
		SceneColor.rgb = SSRColor.rgb * SSRMask;
	else if (_DebugPass == 3)
		SceneColor.rgb = CubemapColor.rgb;
	else if (_DebugPass == 4)
		SceneColor.rgb = ReflectionColor;
	else if (_DebugPass == 5)
		SceneColor.rgb = SSRMask;
	else if (_DebugPass == 6) {
		float4 H = 0.0;
		float2 jitter = SAMPLE_TEXTURE2D_LOD(_SSR_Noise, sampler_point_repeat, float2((uv + _SSR_Jitter.zw) * _SSR_RayCastSize.xy / _SSR_NoiseSize.xy), 0).xy;
		jitter.y = lerp(jitter.y, 0.0, _SSR_BRDFBias);
		if (roughness > 0.1) {
			H = TangentToWorld(ImportanceSampleGGX(jitter, roughness), float4(viewNormal, 1.0));
		}
		else {
			H = float4(viewNormal, 1.0);
		}
		SceneColor.rgb = H.rgb;
	}
	else if (_DebugPass == 7) {
		float2 jitter = SAMPLE_TEXTURE2D_LOD(_SSR_Noise, sampler_point_repeat, float2((uv + _SSR_Jitter.zw) * _SSR_RayCastSize.xy / _SSR_NoiseSize.xy), 0).xy;
		SceneColor.rg = jitter;
		SceneColor.b = 0;
	}

	return SceneColor;
}

#endif
