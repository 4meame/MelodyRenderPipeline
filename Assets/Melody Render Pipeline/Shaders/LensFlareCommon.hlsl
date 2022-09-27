#ifndef MELODY_LENS_FLARE_COMMON_INCLUDED
#define MELODY_LENS_FLARE_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Sampling/Sampling.hlsl"

struct AttributesLensFlare {
	uint vertexID : SV_VERTEXID;
#ifndef FLARE_PREVIEW
	UNITY_VERTEX_INPUT_INSTANCE_ID
#endif
};

struct VaryingsLensFlare {
	float4 positionCS : SV_POSITION;
	float2 texcoord : VAR_TEXCOORD;
	float occlusion : VAR_OCCLUSION;
#ifndef FLARE_PREVIEW
	UNITY_VERTEX_OUTPUT_STEREO
#endif
};

TEXTURE2D(_FlareTex);
SAMPLER(sampler_FlareTex);
#if defined(MRP_FLARE) && defined(FLARE_OCCLUSION)
TEXTURE2D_X(_FlareOcclusionTex);
SAMPLER(sampler_FlareOcclusionTex);
#endif

float4 _FlareColorValue;
//x: localCos0, y: localSin0, zw: PositionOffsetXY
float4 _FlareData0;
//x: OcclusionRadius, y: OcclusionSampleCount, z: ScreenPosZ, w: ScreenRatio
float4 _FlareData1;
//xy: ScreenPos, zw: FlareSize
float4 _FlareData2;
//x: Allow Offscreen, y: Edge Offset, z: Falloff, w: invSideCount
float4 _FlareData3;
//x: SDF Roundness, y: Poly Radius, z: PolyParam0, w: PolyParam1
float4 _FlareData4;

#ifdef FLARE_PREVIEW
float4 _FlarePreviewData;
#define _ScreenSize         _FlarePreviewData.xy;
#define _FlareScreenRatio   _FlarePreviewData.z;
#endif

//occlusion index calculated in compute shader
float4 _FlareOcclusionIndex;

#define _FlareColor             _FlareColorValue

#define _LocalCos0              _FlareData0.x
#define _LocalSin0              _FlareData0.y
#define _PositionTranslate      _FlareData0.zw

#define _OcclusionRadius        _FlareData1.x
#define _OcclusionSampleCount   _FlareData1.y
#define _ScreenPosZ             _FlareData1.z

#ifndef _FlareScreenRatio
#define _FlareScreenRatio       _FlareData1.w
#endif

#define _ScreenPos              _FlareData2.xy
#define _FlareSize              _FlareData2.zw

#define _OcclusionOffscreen     _FlareData3.x
#define _FlareEdgeOffset        _FlareData3.y
#define _FlareFalloff           _FlareData3.z
#define _FlareShapeInvSide      _FlareData3.z

#define _FlareSDFRoundness      _FlareData4.x
#define _FlareSDFPolyRadius     _FlareData4.y
#define _FlareSDFPolyParam0     _FlareData4.z
#define _FlareSDFPolyParam1     _FlareData4.w

float2 Rotate(float2 v, float cos0, float sin0) {
	return float2(v.x * cos0 - v.y * sin0,
		v.x * sin0 + v.y * cos0);
}

float GetLinearDepthValue(float2 uv) {
#if defined(MRP_FLARE) || defined(FLARE_PREVIEW)
	float depth = LOAD_TEXTURE2D_X_LOD(_CameraDepthTexture, uint2(uv * _ScreenSize.xy), 0).x;
#else
	float depth = LOAD_TEXTURE2D_X_LOD(_CameraDepthTexture, uint2(uv * GetScaledScreenParams().xy), 0).x;
#endif
	return LinearEyeDepth(depth, _ZBufferParams);
}

float GetOcclusion(float ratio) {
	if (_OcclusionSampleCount == 0.0) {
		return 1.0f;
	}
	float contribute = 0.0f;
	float sample_contribute = 1.0f / _OcclusionSampleCount;
	float2 ratioScale = float2(1.0f / ratio, 1.0);
	for (unit i = 0; i < (uint)_OcclusionSampleCount; i++) {
		float2 direction = _OcclusionRadius * SampleDiskUniform(Hash(2 * i + 0), Hash(2 * i + 1));
		float2 position = _ScreenPos + direction;
		//[-1,1] -> [0,1]
		position.xy = position * 0.5 + 0.5;
#ifdef UNITY_UV_STARTS_AT_TOP
		position.y = 1.0f - position.y;
#endif
		if (all(position >= 0) && all(position <= 1)) {
			float depth0 = GetLinearDepthValue(position);
#if defined(UNITY_REVERSED_Z)
			if (depth0 > _ScreenPosZ)
#else
			if (depth0 < _ScreenPosZ)
#endif
				contribute += sample_contribute;
		}
		else if (_OcclusionOffscreen > 0.0f) {
			contribute += sample_contribute;
		}
	}
	return contribute;
}

VaryingsLensFlare vertOcclusion(AttributesLensFlare input, uint instanceID : SV_INSTANCEID) {
	VaryingsLensFlare output;
//Single Pass Instanced
	UNITY_SETUP_INSTANCE_ID(input);
	float screenRatio = _FlareScreenRatio;
	float2 quadPos = 2.0f * GetQuadVertexPosition(input.vertexID).xy - 1.0f;
	float2 uv = GetQuadTexCoord(input.vertexID);
	output.positionCS.xy = quadPos;
	output.texcoord.xy = uv;
	output.positionCS.z = 1.0f;
	output.positionCS.w = 1.0f;
	float occlusion = GetOcclusion(screenRatio);
	if (_OcclusionOffscreen < 0.0f && // No lens flare off screen
		(any(_ScreenPos.xy < -1) || any(_ScreenPos.xy >= 1)))
		occlusion = 0.0f;
	output.occlusion = occlusion;
	return output;
}

float4 fragOcclusion(VaryingsLensFlare input) : SV_TARGET {
	return float4(input.occlusion.xxx, 1.0f);
}


#endif
