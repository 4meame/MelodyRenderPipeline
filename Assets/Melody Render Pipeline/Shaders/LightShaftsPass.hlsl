#ifndef MELODY_LIGHT_SHAFTS_PASS_INCLUDED
#define MELODY_LIGHT_SHAFTS_PASS_INCLUDED

#define NUM_SAMPLES 12

TEXTURE2D(_LightShafts0);
SAMPLER(sampler_LightShafts0);
TEXTURE2D(_LightShafts1);
SAMPLER(sampler_LightShafts1);
//cloud current frame for occlusion
TEXTURE2D(_CurrFrame);
SAMPLER(sampler_CurrFrame);

float4 _LightShaftParameters;
float4 _LightSource;

float4 LightShaftsPrefilterPassFragment(Varyings input) : SV_TARGET {
	float4 sceneColor = GetColor(input.screenUV);
	float sceneDepth = GetDepth(input.screenUV);
	float cloudDepth = SAMPLE_TEXTURE2D_LOD(_CurrFrame, sampler_CurrFrame, input.screenUV, 0).a;
	sceneDepth = Linear01Depth(sceneDepth, _ZBufferParams);
	//setup a mask that is 1 at the edges of the screen and 0 at the center
	float edgeMask = 1.0f - input.screenUV.x * (1.0f - input.screenUV.x) * input.screenUV.y * (1.0f - input.screenUV.y) * 8.0f;
	edgeMask = edgeMask * edgeMask * edgeMask * edgeMask;
	float invOcclusionDepthRange = _LightShaftParameters.x;
	//filter the occlusion mask instead of the depths
	float OcclusionMask = saturate(sceneDepth * invOcclusionDepthRange);
	OcclusionMask = max(OcclusionMask, edgeMask);
	return float4(_LightSource.xy - input.screenUV, 0, 1);
	return float4(OcclusionMask.xxx, 1);
}

float4 LightShaftsBlurFragment(Varyings input) : SV_TARGET{
	float3 blurredValues = 0.0;
	float passScale = pow(0.4 * NUM_SAMPLES, _LightShaftParameters.y);
	//vectors from pixel to light source
	float2 blurVector = _LightSource.xy - input.screenUV;
	blurVector *= min(_LightShaftParameters.z * passScale, 1);
	float2 delta = blurVector / NUM_SAMPLES;
	for (int i = 0; i < NUM_SAMPLES; i++)
	{
		float2 sampleUV = input.screenUV + delta * i;
		sampleUV = clamp(sampleUV, 0, 1);
		float3 sampleValue = SAMPLE_TEXTURE2D_LOD(_LightShafts0, sampler_LightShafts0, sampleUV, 0);
		blurredValues += sampleValue;
	}
	blurredValues /= NUM_SAMPLES;

	return float4(blurredValues, 1);
}

float4 LightShaftsBlendFragment(Varyings input) : SV_TARGET {
	float godsRayOcclusion = SAMPLE_TEXTURE2D_LOD(_LightShafts0,sampler_LightShafts0, input.screenUV, 0).x;
	float4 godsRayBlur = SAMPLE_TEXTURE2D_LOD(_LightShafts1, sampler_LightShafts1, input.screenUV, 0);
	float4 sceneColor = GetSource(input.screenUV);
	//LightShaftParameters.w is OcclusionMaskDarkness, use that to control what an occlusion value of 0 maps to
	float finalOcclusion = lerp(_LightShaftParameters.w, 1, godsRayOcclusion * godsRayOcclusion);
	// Setup a mask based on where the blur origin is
	float blurOriginDistanceMask = saturate(length(_LightSource.xy - input.screenUV) * 1.2f);
	// Fade out occlusion over distance away from the blur origin
	finalOcclusion = lerp(finalOcclusion, 1, blurOriginDistanceMask);
	// Fade to no darkening based on distance from the light for point lights
	//finalOcclusion = lerp(finalOcclusion, 1, DistanceFade * DistanceFade * DistanceFade);

	return float4(sceneColor.rgb + godsRayBlur.rgb, 1);
}

#endif
