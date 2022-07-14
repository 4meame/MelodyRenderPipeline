#ifndef MELODY_LIGHT_SHAFTS_PASS_INCLUDED
#define MELODY_LIGHT_SHAFTS_PASS_INCLUDED

#define NUM_SAMPLES 24

TEXTURE2D(_LightShafts0);
SAMPLER(sampler_LightShafts0);
TEXTURE2D(_LightShafts1);
SAMPLER(sampler_LightShafts1);
//current source frame
TEXTURE2D(_SceneColor);
SAMPLER(sampler_SceneColor);
//cloud current frame for occlusion
TEXTURE2D(_CurrFrame);
SAMPLER(sampler_CurrFrame);

float4 _LightShaftParameters;
float4 _RadialBlurParameters;
float4 _BloomTintAndThreshold;
float _ShaftsDensity;
float _ShaftsWeight;
float _ShaftsDecay;
float _ShaftsExposure;
float4 _LightSource;

float4 LightShaftsOcclusionPrefilterPassFragment(Varyings input) : SV_TARGET{
	float4 sceneColor = SAMPLE_TEXTURE2D_LOD(_SceneColor, sampler_SceneColor, input.screenUV, 0);
	float sceneDepth = GetDepth(input.screenUV);
	sceneDepth = Linear01Depth(sceneDepth, _ZBufferParams);
	//setup a mask that is 1 at the edges of the screen and 0 at the center
	float edgeMask = 1.0f - input.screenUV.x * (1.0f - input.screenUV.x) * input.screenUV.y * (1.0f - input.screenUV.y) * 8.0f;
	edgeMask = edgeMask * edgeMask * edgeMask * edgeMask;
	float invOcclusionDepthRange = _LightShaftParameters.x;
	//filter the occlusion mask instead of the depths
	float occlusionMask = saturate(sceneDepth * invOcclusionDepthRange);
	occlusionMask = max(occlusionMask, edgeMask * .8f);
	return float4(occlusionMask.xxx, 1);
}

float4 LightShaftsBloomPrefilterPassFragment(Varyings input) : SV_TARGET{
	float4 sceneColor = SAMPLE_TEXTURE2D_LOD(_SceneColor, sampler_SceneColor, input.screenUV, 0);
	float sceneDepth = GetDepth(input.screenUV);
	sceneDepth = Linear01Depth(sceneDepth, _ZBufferParams);
	//setup a mask that is 1 at the edges of the screen and 0 at the center
	float edgeMask = 1.0f - input.screenUV.x * (1.0f - input.screenUV.x) * input.screenUV.y * (1.0f - input.screenUV.y) * 8.0f;
	edgeMask = edgeMask * edgeMask * edgeMask * edgeMask;
	float invOcclusionDepthRange = _LightShaftParameters.x;
	//only bloom colors over bloomThreshold
	float luminance = max(dot(sceneColor, half3(.3f, .59f, .11f)), 6.10352e-5);
	float adjustedLuminance = max(luminance - _BloomTintAndThreshold.a, 0.0f);
	float3 bloomColor = _LightShaftParameters.y * sceneColor / luminance * adjustedLuminance * 2.0f;
	//only allow bloom from pixels whose depth are in the far half of OcclusionDepthRange
	float bloomDistanceMask = saturate((sceneDepth - 0.5f / invOcclusionDepthRange) * invOcclusionDepthRange);
	//setup a mask that is 0 at light source and increases to 1 over distance
	float screenRatio = _CameraBufferSize.z / _CameraBufferSize.w;
	float blurOriginDistanceMask = 1.0f - saturate(length(_LightSource.xy - input.screenUV) * screenRatio * 2.0f);
	//calculate bloom color with masks applied
	bloomColor = saturate(bloomColor * _BloomTintAndThreshold.rgb * bloomDistanceMask * (1.0f - edgeMask) * blurOriginDistanceMask * blurOriginDistanceMask);
	return float4(bloomColor, 1);
}

float4 LightShaftsBlurFragment(Varyings input) : SV_TARGET{
	float3 blurredValues = 0.0;
	float passScale = pow(0.4 * NUM_SAMPLES, _RadialBlurParameters.y);
	//vectors from pixel to light source
	float2 blurVector = _LightSource.xy - input.screenUV;
	blurVector *= min(_RadialBlurParameters.z * passScale, 1);
	//divide by number of samples and scale by control factor.
	float2 delta = blurVector / NUM_SAMPLES * _ShaftsDensity;
	//set up illumination decay factor.
	float illuminationDecay = 1.0f;
	for (int i = 0; i < (_LightSource.z < 0 ? 0 : NUM_SAMPLES); i++) {
		float2 sampleUV = input.screenUV + delta * i;
		float3 sampleValue = SAMPLE_TEXTURE2D_LOD(_LightShafts0, sampler_LightShafts0, sampleUV, 0);
		//apply sample attenuation scale/decay factors.
		sampleValue *= illuminationDecay * (_ShaftsWeight / NUM_SAMPLES);
		//accumulate combined color.
		blurredValues += sampleValue;
		//update exponential decay factor.
		illuminationDecay *= _ShaftsDecay;
	}
	return float4(blurredValues * _ShaftsExposure, 1);
}

float4 LightShaftsOcclusionBlendFragment(Varyings input) : SV_TARGET {
	float4 godsRayBlur = SAMPLE_TEXTURE2D_LOD(_LightShafts1, sampler_LightShafts1, input.screenUV, 0);
	float4 sceneColor = SAMPLE_TEXTURE2D_LOD(_SceneColor, sampler_SceneColor, input.screenUV, 0);
	return float4(sceneColor.rgb * godsRayBlur.x, 1);
}

float4 LightShaftsBloomBlendFragment(Varyings input) : SV_TARGET{
	float4 godsRayBlur = SAMPLE_TEXTURE2D_LOD(_LightShafts1, sampler_LightShafts1, input.screenUV, 0);
	float4 sceneColor = SAMPLE_TEXTURE2D_LOD(_SceneColor, sampler_SceneColor, input.screenUV, 0);
	return float4(sceneColor.rgb + godsRayBlur.rgb, 1);
}

#endif
