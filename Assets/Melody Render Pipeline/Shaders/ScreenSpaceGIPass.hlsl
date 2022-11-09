#ifndef MELODY_SCREEN_SPACE_GI_PASS_INCLUDED
#define MELODY_SCREEN_SPACE_GI_PASS_INCLUDED

#include "../ShaderLibrary/ScreenSpaceTrace.hlsl"
#include "../ShaderLibrary/ImageBasedLighting.hlsl"

TEXTURE2D(_SSGI_SceneColor_RT);
TEXTURE2D(_SSGI_Noise);
TEXTURE2D(_SSGI_CombienScene_RT);
TEXTURE2D(_SSGI_RayCastRT);
TEXTURE2D(_SSGI_RayMask_RT);
TEXTURE2D(_SSGI_Spatial_RT);
TEXTURE2D(_SSGI_TemporalPrev_RT);
TEXTURE2D(_SSGI_TemporalCurr_RT);
Texture2D _SSGI_HierarchicalDepth_RT; SamplerState sampler_SSGI_HierarchicalDepth_RT;

float _SSGI_ScreenFade;
float _SSGI_Thickness;
float _SSGI_Intensity;
int _SSGI_NumRays;
float3 _SSGI_CameraClipInfo;
float4 _SSGI_ScreenSize;
float4 _SSGI_RayCastSize;
float4 _SSGI_NoiseSize;
float4 _SSGI_Jitter;
float4 _SSGI_RandomSeed;
float4 _SSGI_ProjInfo;
int _SSGI_TraceBehind;
int _SSGI_RayMask;
//linear trace
int _SSGI_NumSteps_Linear;
int _SSGI_RayStepSize;
int _SSGI_TraceDistance;
//HiZ trace
float _SSGI_Threshold_Hiz;
int _SSGI_NumSteps_HiZ;
int _SSGI_HiZ_MaxLevel;
int _SSGI_HiZ_StartLevel;
int _SSGI_HiZ_StopLevel;
int _SSGI_HiZ_PrevDepthLevel;
//denoise
int _SSGI_KernelSize;
float _SSGI_KernelRadius;
float _SSGI_TemporalScale;
float _SSGI_TemporalWeight;
//debug
int _DebugPass;

float4x4 _SSGI_ProjectionMatrix;
float4x4 _SSGI_InverseProjectionMatrix;
float4x4 _SSGI_ViewProjectionMatrix;
float4x4 _SSGI_InverseViewProjectionMatrix;
float4x4 _SSGI_WorldToCameraMatrix;
float4x4 _SSGI_CameraToWorldMatrix;
float4x4 _SSGI_LastFrameViewProjectionMatrix;
float4x4 _SSGI_ProjectToPixelMatrix;

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

float3 SSGI_BRDF(float3 viewDir, float3 lightDir, float3 normal, float roughness) {
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
		_SSGI_HierarchicalDepth_RT.SampleLevel(sampler_point_clamp, uv, _SSGI_HiZ_PrevDepthLevel, int2(-1.0, -1.0)).r,
		_SSGI_HierarchicalDepth_RT.SampleLevel(sampler_point_clamp, uv, _SSGI_HiZ_PrevDepthLevel, int2(-1.0, 1.0)).r,
		_SSGI_HierarchicalDepth_RT.SampleLevel(sampler_point_clamp, uv, _SSGI_HiZ_PrevDepthLevel, int2(1.0, -1.0)).r,
		_SSGI_HierarchicalDepth_RT.SampleLevel(sampler_point_clamp, uv, _SSGI_HiZ_PrevDepthLevel, int2(1.0, 1.0)).r
		);
	//sample pixel surrounds and pick minnset depth
	return max(max(minDepth.r, minDepth.g), max(minDepth.b, minDepth.a));
}

//2D linear trace sampler
void GlobalIlluminationLinearTrace(Varyings input, out float4 SSGIColor_Occlusion : SV_TARGET0, out float4 Mask_Depth_HitUV : SV_TARGET1) {
	float2 uv = input.screenUV;
	float roughness = clamp(SAMPLE_TEXTURE2D_LOD(_CameraSpecularTexture, sampler_point_clamp, uv, 0).a, 0.02, 1);
	float4 depthNormalTexture = SAMPLE_TEXTURE2D_LOD(_CameraDepthNormalTexture, sampler_point_clamp, uv, 0);
	float3 viewNormal = DecodeViewNormalStereo(depthNormalTexture);
	//NOTE HERE : FOR w component, point : 1, direction : 0
	float3 worldNormal = normalize(mul(_SSGI_CameraToWorldMatrix, float4(viewNormal, 0))).xyz;
	float3x3 TangentBasis = GetTangentBasis(worldNormal);
	//const property
	float Ray_HitMask = 0.0;
	float Out_Fade = 0.0;
	float Out_Mask = 0.0;
	float Out_Occlusion = 0.0;
	float Out_RayDepth = 0.0;
	float2 Out_UV = 0;
	float4 Out_Color = 0;
	//begin trace
	float4 screenTexelSize = float4(1.0 / _SSGI_ScreenSize.x, 1.0 / _SSGI_ScreenSize.y, _SSGI_ScreenSize.x, _SSGI_ScreenSize.y);
	float3 Ray_Origin_VS = GetPosition(_CameraDepthTexture, screenTexelSize, _SSGI_ProjInfo, uv);
	float Ray_Bump = max(-0.01 * Ray_Origin_VS.z, 0.001);
	float2 blueNoise = SAMPLE_TEXTURE2D_LOD(_SSGI_Noise, sampler_point_repeat, float2((uv + _SSGI_Jitter.zw) * _SSGI_RayCastSize.xy / _SSGI_NoiseSize.xy), 0).xy;
	//loop all multi rays
	for (uint i = 0; i < (uint)_SSGI_NumRays; i++) {
		float2 hash = SAMPLE_TEXTURE2D_LOD(_SSGI_Noise, sampler_point_repeat, float2((uv + sin(1 + _SSGI_Jitter.zw)) * _SSGI_ScreenSize.xy / _SSGI_NoiseSize.xy), 0).xy;
		//calculate light dir by uniform sample disk
		float3 L;
		L.xy = UniformSampleDiskConcentric(hash);
		L.z = sqrt(1 - dot(L.xy, L.xy));
		float3 Ray_Dir_WS = mul(L, TangentBasis);
		float3 Ray_Dir_VS = mul((float3x3)(_SSGI_WorldToCameraMatrix), Ray_Dir_WS);
		//ray trace
		float Jitter = blueNoise.x + blueNoise.y;
		float Ray_NumMarch = 0.0;
		float2 Ray_HitUV = 0.0;
		float3 Ray_HitPoint = 0.0;
		bool hit = Linear2D_Trace(_CameraDepthTexture, Ray_Origin_VS + viewNormal * Ray_Bump, Ray_Dir_VS, _SSGI_ProjectToPixelMatrix, _SSGI_ScreenSize, Jitter, _SSGI_NumSteps_Linear, _SSGI_Thickness, _SSGI_TraceDistance, Ray_HitUV, _SSGI_RayStepSize, _SSGI_TraceBehind == 1, Ray_HitPoint, Ray_NumMarch);
		Ray_HitUV /= _SSGI_ScreenSize;
		UNITY_BRANCH
		if (hit) {
			Ray_HitMask = Square(1 - max(2 * float(Ray_NumMarch) / float(_SSGI_NumSteps_Linear) - 1, 0));
			Ray_HitMask *= saturate(((_SSGI_TraceDistance - dot(Ray_HitPoint - Ray_Origin_VS, Ray_Dir_VS))));
		}
		//calculate reflect color, last frame reflect color can be the light source for this frame
		float4 SampleColor = SAMPLE_TEXTURE2D_LOD(_SSGI_SceneColor_RT, sampler_linear_clamp, Ray_HitUV, 0);
		float4 SampleNormal = SAMPLE_TEXTURE2D_LOD(_CameraDepthNormalTexture, sampler_point_clamp, Ray_HitUV, 0);
		SampleNormal.xyz = DecodeViewNormalStereo(SampleNormal);
		SampleNormal.xyz = normalize(mul(_SSGI_CameraToWorldMatrix, float4(SampleNormal.xyz, 0))).xyz;
		float Occlusion = 1 - saturate(dot(Ray_Dir_WS, SampleNormal.xyz));
		SampleColor.rgb *= Occlusion;
		SampleColor.rgb /= 1 + Luminance(SampleColor.rgb);
		//accumulate sample result
		Out_Color += SampleColor;
		Out_Mask += Ray_HitMask * GetScreenFadeBord(Ray_HitUV, _SSGI_ScreenFade);
		Out_RayDepth += SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_point_clamp, Ray_HitUV, 0).r;
		Out_UV += Ray_HitUV;
		Out_Occlusion += Occlusion;
	}
	//output
	Out_Color /= _SSGI_NumRays;
	Out_Color.rgb /= 1 - Luminance(Out_Color.rgb);
	Out_Mask /= _SSGI_NumRays;
	Out_RayDepth /= _SSGI_NumRays;
	Out_UV /= _SSGI_NumRays;
	Out_Occlusion /= _SSGI_NumRays;

	if(_SSGI_RayMask == 1){
		SSGIColor_Occlusion = float4(Out_Color.rgb * saturate(Out_Mask * 2), Out_Occlusion);
	}
	else {
		SSGIColor_Occlusion = float4(Out_Color.rgb, Out_Occlusion);
	}
	Mask_Depth_HitUV = float4(Square(Out_Mask), Out_RayDepth, Out_UV);
}

//Hiz trace sampler
void GlobalIlluminationHierarchicalZ(Varyings input, out float4 SSGIColor_Occlusion : SV_TARGET0, out float4 Mask_Depth_HitUV : SV_TARGET1) {
	float2 uv = input.screenUV;
	float roughness = clamp(SAMPLE_TEXTURE2D_LOD(_CameraSpecularTexture, sampler_point_clamp, uv, 0).a, 0.02, 1);
	float sceneDepth = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_point_clamp, uv, 0);
	float4 depthNormalTexture = SAMPLE_TEXTURE2D_LOD(_CameraDepthNormalTexture, sampler_point_clamp, uv, 0);
	float3 viewNormal = DecodeViewNormalStereo(depthNormalTexture);
	//NOTE HERE : FOR w component, point : 1, direction : 0
	float3 worldNormal = normalize(mul(_SSGI_CameraToWorldMatrix, float4(viewNormal, 0))).xyz;
	float3x3 TangentBasis = GetTangentBasis(worldNormal);
	//const property
	float Out_Mask = 0.0;
	float Out_Occlusion = 0.0;
	float Out_RayDepth = 0.0;
	float2 Out_UV = 0;
	float4 Out_Color = 0;
	//begin trace
	float3 screenPos = GetScreenSpacePos(uv, sceneDepth);
	float4 screenTexelSize = float4(1.0 / _SSGI_ScreenSize.x, 1.0 / _SSGI_ScreenSize.y, _SSGI_ScreenSize.x, _SSGI_ScreenSize.y);
	float3 Ray_Origin_VS = GetPosition(_CameraDepthTexture, screenTexelSize, _SSGI_ProjInfo, uv);
	//loop all multi rays
	for (uint i = 0; i < (uint)_SSGI_NumRays; i++) {
		float2 hash = SAMPLE_TEXTURE2D_LOD(_SSGI_Noise, sampler_point_repeat, float2((uv + sin( i +_SSGI_Jitter.zw)) * _SSGI_ScreenSize.xy / _SSGI_NoiseSize.xy), 0).xy;
		//calculate light dir by uniform sample disk
		float3 L;
		L.xy = UniformSampleDiskConcentric(hash);
		L.z = sqrt(1 - dot(L.xy, L.xy));
		float3 Ray_Dir_WS = mul(L, TangentBasis);
		float3 Ray_Dir_VS = mul((float3x3)(_SSGI_WorldToCameraMatrix), Ray_Dir_WS);
		//hiz trace
		float3 Ray_Start = float3(uv, screenPos.z);
		float4 Ray_Proj = mul(_SSGI_ProjectionMatrix, float4(Ray_Origin_VS + Ray_Dir_VS, 1.0));
		float3 Ray_Dir = normalize((Ray_Proj.xyz / Ray_Proj.w) - screenPos);
		Ray_Dir.xy *= 0.5;
		float4 Ray_Hit_Data = HierarchicalZTrace(_SSGI_HiZ_MaxLevel, _SSGI_HiZ_StartLevel, _SSGI_HiZ_StopLevel, _SSGI_NumSteps_HiZ, _SSGI_Thickness, 1 / _SSGI_ScreenSize.xy, Ray_Start, Ray_Dir, _SSGI_HierarchicalDepth_RT, sampler_SSGI_HierarchicalDepth_RT);
		//calculate reflect color, last frame reflect color can be the light source for this frame
		float4 SampleColor = SAMPLE_TEXTURE2D_LOD(_SSGI_SceneColor_RT, sampler_linear_clamp, Ray_Hit_Data.xy, 0);
		float4 SampleNormal = SAMPLE_TEXTURE2D_LOD(_CameraDepthNormalTexture, sampler_point_clamp, Ray_Hit_Data.xy, 0);
		SampleNormal.xyz = DecodeViewNormalStereo(SampleNormal);
		SampleNormal.xyz = normalize(mul(_SSGI_CameraToWorldMatrix, float4(SampleNormal.xyz, 0))).xyz;
		float Occlusion = 1 - saturate(dot(Ray_Dir_WS, SampleNormal.xyz));
		SampleColor.rgb *= Occlusion;
		SampleColor.rgb /= 1 + Luminance(SampleColor.rgb);
		//accumulate sample result
		Out_Color += SampleColor;
		Out_Mask += Square(Ray_Hit_Data.w * GetScreenFadeBord(Ray_Hit_Data.xy, _SSGI_ScreenFade));
		Out_RayDepth += Ray_Hit_Data.z;
		Out_UV += Ray_Hit_Data.xy;
		Out_Occlusion += Occlusion;
	}
	//output
	Out_Color /= _SSGI_NumRays;
	Out_Color.rgb /= 1 - Luminance(Out_Color.rgb);
	Out_Mask /= _SSGI_NumRays;
	Out_RayDepth /= _SSGI_NumRays;
	Out_UV /= _SSGI_NumRays;
	Out_Occlusion /= _SSGI_NumRays;

	if (_SSGI_RayMask == 1) {
		SSGIColor_Occlusion = float4(Out_Color.rgb * saturate(Out_Mask * 2), Out_Occlusion);
	}
	else {
		SSGIColor_Occlusion = float4(Out_Color.rgb, Out_Occlusion);
	}
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

//spatio filter weight by brdf
float4 BrdfWeightFilter(Varyings input) : SV_TARGET {
	float2 uv = input.screenUV;
	//sample buffers' properties
	float roughness = clamp(SAMPLE_TEXTURE2D_LOD(_CameraSpecularTexture, sampler_point_clamp, uv, 0).a, 0.02, 1);
	float sceneDepth = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_point_clamp, uv, 0);
	float4 depthNormalTexture = SAMPLE_TEXTURE2D_LOD(_CameraDepthNormalTexture, sampler_point_clamp, uv, 0);
	float3 viewNormal = DecodeViewNormalStereo(depthNormalTexture);
	//get screen pos
	float3 screenPos = GetScreenSpacePos(uv, sceneDepth);
	float3 viewPos = GetViewSpacePos(screenPos, _SSGI_InverseProjectionMatrix);
	//make offset rotate matrix
	float2 blueNoise = SAMPLE_TEXTURE2D_LOD(_SSGI_Noise, sampler_point_repeat, float2((uv + _SSGI_Jitter.zw) * _SSGI_RayCastSize.xy / _SSGI_NoiseSize.xy), 0) * 2 - 1;
	float2x2 offsetRotationMatrix = float2x2(blueNoise.x, blueNoise.y, -blueNoise.y, -blueNoise.x);

	float NumWeight, Weight;
	float2 Offset_UV, Neighbor_UV;
	float3 Hit_ViewPos;
	float4 SampleColor, FilterColor;
	for (int i = 0; i < _SSGI_KernelSize; i++) {
		Offset_UV = mul(offsetRotationMatrix, offset2[i] * (1 / _SSGI_ScreenSize.xy));
		Neighbor_UV = uv + Offset_UV;
		//_SSGI_RayCastRT stores rg : hit uv b : depth
		float4 Hit_Mask_Depth_UV = SAMPLE_TEXTURE2D_LOD(_SSGI_RayMask_RT, sampler_point_clamp, Neighbor_UV, 0);
		float3 Hit_ViewPos = GetViewSpacePos(float3(Hit_Mask_Depth_UV.ba, Hit_Mask_Depth_UV.g), _SSGI_InverseProjectionMatrix);
		//spatio sampler
		Weight = SSGI_BRDF(normalize(-viewPos), normalize(Hit_ViewPos - viewPos), viewNormal, roughness);
		SampleColor.rgb = SAMPLE_TEXTURE2D_LOD(_SSGI_RayCastRT, sampler_linear_clamp, Neighbor_UV, 0).rgb;
		SampleColor.a = SAMPLE_TEXTURE2D_LOD(_SSGI_RayMask_RT, sampler_point_clamp, Neighbor_UV, 0).r;
		//calculate weight
		FilterColor += SampleColor * Weight;
		NumWeight += Weight;
	}
	FilterColor /= NumWeight;
	FilterColor = max(1e-5, FilterColor);
	return FilterColor;
}

float Luma4(float3 Color) {
	return (Color.g * 2) + (Color.r + Color.b);
}

float HdrWeight4(float3 Color, float Exposure) {
	return rcp(Luma4(Color) * Exposure + 4);
}

float4 TemporalFilter(Varyings input) : SV_TARGET {
	float2 uv = input.screenUV;
	//get reprojection velocity
	float2 velocity = SAMPLE_TEXTURE2D_LOD(_CameraMotionVectorTexture, sampler_point_clamp, uv, 0).rg;
	//AABB clipping
	float SSGI_Variance = 0;
	float4 SSGI_CurrColor = 0;
	float4 SSGI_MinColor, SSGI_MaxColor;
	float4 SampleColors[9];
	for (uint i = 0; i < 9; i++) {
		SampleColors[i] = SAMPLE_TEXTURE2D_LOD(_SSGI_RayCastRT, sampler_linear_clamp, uv + (offset1[i] / _SSGI_ScreenSize.xy), 0);
	}
	float SampleWeights[9];
	for (uint j = 0; j < 9; j++) {
		SampleWeights[j] = HdrWeight4(SampleColors[j].rgb, 10);
	}
	float TotalWeight = 0;
	for (uint k = 0; k < 9; k++) {
		TotalWeight += SampleWeights[k];
	}
	SampleColors[4] = (SampleColors[0] * SampleWeights[0] + SampleColors[1] * SampleWeights[1] + SampleColors[2] * SampleWeights[2] + SampleColors[3] * SampleWeights[3] + SampleColors[4] * SampleWeights[4] + SampleColors[5] * SampleWeights[5] + SampleColors[6] * SampleWeights[6] + SampleColors[7] * SampleWeights[7] + SampleColors[8] * SampleWeights[8]) / TotalWeight;
	float4 m1 = 0.0;
	float4 m2 = 0.0;
	for (uint x = 0; x < 9; x++) {
		m1 += SampleColors[x];
		m2 += SampleColors[x] * SampleColors[x];
	}
	float4 mean = m1 / 9.0;
	float4 stddev = sqrt((m2 / 9.0) - pow2(mean));
	SSGI_MinColor = mean - _SSGI_TemporalScale * stddev;
	SSGI_MaxColor = mean + _SSGI_TemporalScale * stddev;
	SSGI_CurrColor = SampleColors[4];
	SSGI_MinColor = min(SSGI_MinColor, SSGI_CurrColor);
	SSGI_MaxColor = max(SSGI_MaxColor, SSGI_CurrColor);
	float4 TotalVariance = 0;
	for (uint n = 0; n < 9; n++) {
		TotalVariance += pow2(Luminance(SampleColors[n]) - Luminance(mean));
	}
	SSGI_Variance = saturate((TotalVariance / 9) * 256);
	SSGI_Variance *= SSGI_CurrColor.a;
	//clamp temporal color
	float4 SSGI_PrevColor = SAMPLE_TEXTURE2D_LOD(_SSGI_TemporalPrev_RT, sampler_linear_clamp, uv - velocity, 0);
	SSGI_PrevColor = clamp(SSGI_PrevColor, SSGI_MinColor, SSGI_MaxColor);
	//combine
	float Temporal_BlendWeight = saturate(_SSGI_TemporalWeight * (1 - length(velocity) * 2));
	float4 FilterColor = lerp(SSGI_CurrColor, SSGI_PrevColor, Temporal_BlendWeight);
	return FilterColor;
}

//normal weight bilateral
float4 GetSource(float2 screenUV) {
	return SAMPLE_TEXTURE2D_LOD(_SSGI_TemporalPrev_RT, sampler_linear_clamp, screenUV, 0);
}

float GetDepth(float2 screenUV) {
	return SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, sampler_point_clamp, screenUV, 0).r;
}

float3 GetNormal(float2 screenUV) {
	float4 depthNormal = SAMPLE_TEXTURE2D_LOD(_CameraDepthNormalTexture, sampler_point_clamp, screenUV, 0);
	float3 normal = DecodeViewNormalStereo(depthNormal);
	return normal;
}

float CompareNormal(float3 normal0, float3 normal1) {
	return smoothstep(0.95, 1.0, dot(normal0, normal1));
}

float2 GetValidUV(float2 uv, float2 delta) {
	float2 deltaUV = uv + delta;
	if (deltaUV.x > 1 || deltaUV.y > 1 || deltaUV.x < 0 || deltaUV.y < 0) {
		return uv;
	}
	else {
		return deltaUV;
	}
}

float4 NormalBilateralFilterX(Varyings input) : SV_TARGET{
	float2 delta = _SSGI_KernelRadius * float2(1.0 / _SSGI_ScreenSize.x, 0);
	float2 uv = input.screenUV;
	float2 uv0a = GetValidUV(uv, -delta);
	float2 uv0b = GetValidUV(uv, +delta);
	float2 uv1a = GetValidUV(uv, -2.0 * delta);
	float2 uv1b = GetValidUV(uv, +2.0 * delta);
	float2 uv2a = GetValidUV(uv, -3.0 * delta);
	float2 uv2b = GetValidUV(uv, +3.0 * delta);
	//get normal
	float3 normal = GetNormal(uv);
	float3 normal0a = GetNormal(uv0a);
	float3 normal0b = GetNormal(uv0b);
	float3 normal1a = GetNormal(uv1a);
	float3 normal1b = GetNormal(uv1b);
	float3 normal2a = GetNormal(uv2a);
	float3 normal2b = GetNormal(uv2b);
	//get source
	float4 source = GetSource(uv);
	float4 source0a = GetSource(uv0a);
	float4 source0b = GetSource(uv0b);
	float4 source1a = GetSource(uv1a);
	float4 source1b = GetSource(uv1b);
	float4 source2a = GetSource(uv2a);
	float4 source2b = GetSource(uv2b);
	//calculate weight
	float w = 0.37004005286;
	float w0a = CompareNormal(normal, normal0a) * 0.31718061674;
	float w0b = CompareNormal(normal, normal0b) * 0.31718061674;
	float w1a = CompareNormal(normal, normal1a) * 0.19823788546;
	float w1b = CompareNormal(normal, normal1b) * 0.19823788546;
	float w2a = CompareNormal(normal, normal2a) * 0.11453744493;
	float w2b = CompareNormal(normal, normal2b) * 0.11453744493;
	float4 result = w * source;
	result += w0a * source0a;
	result += w0b * source0b;
	result += w1a * source1a;
	result += w1b * source1b;
	result += w2a * source2a;
	result += w2b * source2b;
	result = result / (w + w0a + w0b + w1a + w1b + w2a + w2b);
	return result;
}

float4 NormalBilateralFilterY(Varyings input) : SV_TARGET{
	float2 delta = _SSGI_KernelRadius * float2(0, 1.0 / _SSGI_ScreenSize.y);
	float2 uv = input.screenUV;
	float2 uv0a = GetValidUV(uv, -delta);
	float2 uv0b = GetValidUV(uv, +delta);
	float2 uv1a = GetValidUV(uv, -2.0 * delta);
	float2 uv1b = GetValidUV(uv, +2.0 * delta);
	float2 uv2a = GetValidUV(uv, -3.0 * delta);
	float2 uv2b = GetValidUV(uv, +3.0 * delta);
	//get normal
	float3 normal = GetNormal(uv);
	float3 normal0a = GetNormal(uv0a);
	float3 normal0b = GetNormal(uv0b);
	float3 normal1a = GetNormal(uv1a);
	float3 normal1b = GetNormal(uv1b);
	float3 normal2a = GetNormal(uv2a);
	float3 normal2b = GetNormal(uv2b);
	//get source
	float4 source = GetSource(uv);
	float4 source0a = GetSource(uv0a);
	float4 source0b = GetSource(uv0b);
	float4 source1a = GetSource(uv1a);
	float4 source1b = GetSource(uv1b);
	float4 source2a = GetSource(uv2a);
	float4 source2b = GetSource(uv2b);
	//calculate weight
	float w = 0.37004005286;
	float w0a = CompareNormal(normal, normal0a) * 0.31718061674;
	float w0b = CompareNormal(normal, normal0b) * 0.31718061674;
	float w1a = CompareNormal(normal, normal1a) * 0.19823788546;
	float w1b = CompareNormal(normal, normal1b) * 0.19823788546;
	float w2a = CompareNormal(normal, normal2a) * 0.11453744493;
	float w2b = CompareNormal(normal, normal2b) * 0.11453744493;
	float4 result = w * source;
	result += w0a * source0a;
	result += w0b * source0b;
	result += w1a * source1a;
	result += w1b * source1b;
	result += w2a * source2a;
	result += w2b * source2b;
	result = result / (w + w0a + w0b + w1a + w1b + w2a + w2b);
	return result;
}

//adaption bilateral filter from battlefield3 of frostbite
void GetGI_Depth(TEXTURE2D(_SourceTexture), float2 uv, inout float3 Gi, inout float Depth) {
	Gi = SAMPLE_TEXTURE2D_LOD(_SourceTexture, sampler_linear_clamp, uv, 0).xyz;
	Depth = LinearEyeDepth(SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_point_clamp, uv, 0).r, _ZBufferParams);
}

float CrossBilateralWeight(float r, float d, float d0) {
	float blurSigma = (float)_SSGI_KernelSize * 0.5;
	float blurFalloff = 1 / (2 * blurSigma * blurSigma);
	float dz = (d0 - d) * _ProjectionParams.z * 0.25;
	return exp2(-r * r * blurFalloff - dz * dz);
}

void ProcessSample(float4 Gi_Depth, float r, float d0, inout float3 totalGi, inout float totalW) {
	float w = CrossBilateralWeight(r, d0, Gi_Depth.w);
	totalW += w;
	totalGi += w * Gi_Depth.xyz;
}

void ProcessRadius(TEXTURE2D(_SourceTexture), float2 uv0, float2 deltaUV, float d0, inout float3 totalGi, inout float totalW) {
	float r = 1.0;
	float z = 0.0;
	float2 uv = 0.0;
	float3 Gi = 0.0;
	for (; r <= _SSGI_KernelSize / 2; r += 1) {
		uv = uv0 + r * deltaUV;
		GetGI_Depth(_SourceTexture, uv, Gi, z);
		ProcessSample(float4(Gi, z), r, d0, totalGi, totalW);
	}
	for (; r <= _SSGI_KernelSize; r += 2) {
		uv = uv0 + (r + 0.5) * deltaUV;
		GetGI_Depth(_SourceTexture, uv, Gi, z);
		ProcessSample(float4(Gi, z), r, d0, totalGi, totalW);
	}
}

float4 AdaptiveBilateralFilterX(Varyings input) : SV_TARGET{
	float2 uv = input.screenUV;
	float2 deltaUV = _SSGI_KernelRadius * float2(1.0 / _SSGI_ScreenSize.x, 0);
	float depth;
	float3 totalGi;
	GetGI_Depth(_SSGI_TemporalPrev_RT, uv, totalGi, depth);
	float totalW = 1;
	ProcessRadius(_SSGI_TemporalPrev_RT, uv, -deltaUV, depth, totalGi, totalW);
	ProcessRadius(_SSGI_TemporalPrev_RT, uv, deltaUV, depth, totalGi, totalW);
	totalGi /= totalW;
	return float4(totalGi, depth);
}

float4 AdaptiveBilateralFilterY(Varyings input) : SV_TARGET{
	float2 uv = input.screenUV;
	float2 deltaUV = _SSGI_KernelRadius * float2(0, 1.0 / _SSGI_ScreenSize.y);
	float depth;
	float3 totalGi;
	GetGI_Depth(_SSGI_TemporalPrev_RT, uv, totalGi, depth);
	float totalW = 1;
	ProcessRadius(_SSGI_TemporalPrev_RT, uv, -deltaUV, depth, totalGi, totalW);
	ProcessRadius(_SSGI_TemporalPrev_RT, uv, deltaUV, depth, totalGi, totalW);
	totalGi /= totalW;
	return float4(totalGi, depth);
}

float4 CombineGlobalIllumination(Varyings input) : SV_TARGET{
	float2 uv = input.screenUV;
	float4 BaseColor = SAMPLE_TEXTURE2D_LOD(_CameraDiffuseTexture, sampler_linear_clamp, uv, 0);
	float4 SceneColor = SAMPLE_TEXTURE2D_LOD(_SSGI_SceneColor_RT, sampler_linear_clamp, uv, 0);
	float4 RayCast = SAMPLE_TEXTURE2D_LOD(_SSGI_Spatial_RT, sampler_linear_clamp, uv, 0);
	float4 RayMask = SAMPLE_TEXTURE2D_LOD(_SSGI_RayMask_RT, sampler_linear_clamp, uv, 0);
	if (_DebugPass == 0)
		SceneColor.rgb += BaseColor.rgb * RayCast.rgb * _SSGI_Intensity;
	else if (_DebugPass == 1)
		SceneColor.rgb = RayCast.rgb * _SSGI_Intensity;
	else if (_DebugPass == 2)
		SceneColor.rgb = RayCast.aaa;
	else if (_DebugPass == 3)
		SceneColor.rgb = RayMask.xxx;
	else if (_DebugPass == 4)
		SceneColor.rgb = RayMask.yyy;
	return SceneColor;
}
#endif
