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
};

TEXTURE2D(_FlareTex);
SAMPLER(sampler_FlareTex);
#if defined(FLARE_OCCLUSION)
TEXTURE2D(_FlareOcclusionTex);
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

#if defined(FLARE_PREVIEW)
float4 _FlarePreviewData;
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
	return float2(v.x * cos0 - v.y * sin0, v.x * sin0 + v.y * cos0);
}

float GetLinearDepthValue(float2 uv) {
	//The Sample method accepts a UV coordinate (where the texture covers the [0, 1] range), does mipmap selection based on the UV derivatives, applies addressing modes (clamp, wrap, border) and does filtering (bilinear, trilinear, aniso)
	//The Load method accepts a texel coordinate in the [0, textureWidth - 1] x [0, textureHeight - 1] range, and the desired mip level, and simply loads a single texel. Coordinates outside the texture's range just return zero, and no filtering is done
	//When trying to map a texture 1:1 to the screen, it's convenient to combine Load with the SV_Position input semantic in a pixel shader, as they're in the same units.
#if defined(FLARE_PREVIEW)
	float depth = LOAD_TEXTURE2D_LOD(_CameraDepthTexture, uint2(uv * _FlarePreviewData.xy), 0).x;
#else
	float depth = LOAD_TEXTURE2D_LOD(_CameraDepthTexture, uint2(uv * _CameraBufferSize.zw), 0).x;
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
	for (uint i = 0; i < (uint)_OcclusionSampleCount; i++) {
		float2 direction = _OcclusionRadius * SampleDiskUniform(Hash(2 * i + 0), Hash(2 * i + 1));
		float2 position = _ScreenPos + direction;
		//[-1,1] -> [0,1]
		position.xy = position * 0.5 + 0.5;
		if (_ProjectionParams.x < 0.0) {
			position.y = 1.0f - position.y;
		}
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

//----------------------------------------Occlusion Shader------------------------------------------//

VaryingsLensFlare vertOcclusion(AttributesLensFlare input, uint instanceID : SV_INSTANCEID) {
	VaryingsLensFlare output;
//Single Pass Instanced
	UNITY_SETUP_INSTANCE_ID(input);
	float screenRatio = _FlareScreenRatio;
	float2 quadPos = 2.0f * GetQuadVertexPosition(input.vertexID).xy - 1.0f;
	float2 uv = GetQuadTexCoord(input.vertexID);
	uv.x = 1.0f - uv.x;
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

//----------------------------------------View Shader------------------------------------------//

VaryingsLensFlare vert(AttributesLensFlare input, uint instanceID : SV_INSTANCEID) {
	VaryingsLensFlare output;
#ifndef FLARE_PREVIEW
	//won't instance in preview
	UNITY_SETUP_INSTANCE_ID(input);
#endif
	float screenRatio = _FlareScreenRatio;
	//[0,1] -> [-1,1]
	float4 posPreScale = float4(2.0f, 2.0f, 1.0f, 1.0f) * GetQuadVertexPosition(input.vertexID) - float4(1.0f, 1.0f, 0.0f, 0.0);
	float2 uv = GetQuadTexCoord(input.vertexID);
	//mirror needs to filp x
	uv.x = 1.0f - uv.x;

	output.texcoord = uv;
	posPreScale.xy *= _FlareSize;
	float2 local = Rotate(posPreScale.xy, _LocalCos0, _LocalSin0);
	local.x *= screenRatio;
	output.positionCS.xy = local + _ScreenPos + _PositionTranslate;
	output.positionCS.z = 1.0f;
	output.positionCS.w = 1.0f;
#if defined(FLARE_OCCLUSION)
	float occlusion = GetOcclusion(screenRatio);
	if (_OcclusionOffscreen < 0.0f && // No lens flare off screen
		(any(_ScreenPos.xy < -1) || any(_ScreenPos.xy >= 1)))
		occlusion = 0.0f;
#else
	float occlusion = 1.0f;
#endif
	output.occlusion = occlusion;
	return output;
}

float InverseGradient(float x) {
	//DO NOT simplify as 1.0f - x
	return x * (1.0f - x) / (x + 1e-6f);
}

float4 ComputeCircle(float2 uv) {
	float2 v = (uv - 0.5f) * 2.0f;
	float x = length(v);
	float sdf = saturate((x - 1.0f) / ((_FlareEdgeOffset - 1.0f)));
#if defined(FLARE_INVERSE_SDF)
	sdf = saturate(sdf);
	sdf = InverseGradient(sdf);
#endif
	return pow(sdf, _FlareFalloff);
}

//modfied from ref: shadertoy.com/view/MtKcWW, shadertoy.com/view/3tGBDt
float4 ComputePolygon(float2 uv) {
	float2 p = uv * 2.0f - 1.0f;
	float r = _FlareSDFPolyRadius;
	float an = _FlareSDFPolyParam0;
	float he = _FlareSDFPolyParam1;
	float bn = an * floor((atan2(p.y, p.x) + 0.5f * an) / an);
	float cos0 = cos(bn);
	float sin0 = sin(bn);
	p = float2(cos0 * p.x + sin0 * p.y,
		-sin0 * p.x + cos0 * p.y);
	// side of polygon
	float sdf = length(p - float2(r, clamp(p.y, -he, he))) * sign(p.x - r) - _FlareSDFRoundness;
	sdf *= _FlareEdgeOffset;
#if defined(FLARE_INVERSE_SDF)
	sdf = saturate(-sdf);
	sdf = InverseGradient(sdf);
#else
	sdf = saturate(-sdf);
#endif
	return saturate(pow(sdf, _FlareFalloff));
}

float4 GetFlareShape(float2 uv) {
#if defined(FLARE_CIRCLE)
	return ComputeCircle(uv);
#elif defined(FLARE_POLYGON)
	return ComputePolygon(uv);
#else
	return SAMPLE_TEXTURE2D(_FlareTex, sampler_FlareTex, uv);
#endif
}

float4 frag(VaryingsLensFlare input) : SV_TARGET {
	float4 col = GetFlareShape(input.texcoord);
#if defined(FLARE_OCCLUSION)
	float occ = SAMPLE_TEXTURE2D_LOD(_FlareOcclusionTex, sampler_FlareOcclusionTex, float2(_FlareOcclusionIndex.x, 0.0f), 0).x;
	return col * _FlareColor * occ;
#else
	return col * _FlareColor * input.occlusion;
#endif
}

#endif
