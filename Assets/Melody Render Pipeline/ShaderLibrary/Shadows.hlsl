#ifndef MELODY_SHADOWS_INCLUDED
#define MELODY_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#if defined(_DIRECTIONAL_PCF3)
	#define DIRECTIONAL_FILTER_SAMPLES 4
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
	#define DIRECTIONAL_FILTER_SAMPLES 9
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
	#define DIRECTIONAL_FILTER_SAMPLES 16
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#if defined(_OTHER_PCF3)
	#define OTHER_FILTER_SAMPLES 4
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_OTHER_PCF5)
	#define OTHER_FILTER_SAMPLES 9
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_OTHER_PCF7)
	#define OTHER_FILTER_SAMPLES 16
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_SHADOWED_OTHER_LIGHT_COUNT 16
#define MAX_CASCADE_COUNT 4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas); TEXTURE2D_SHADOW(_OtherShadowAtlas);
//regular bilinear filtering donesn't make sense for depth data
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_ChickenShadows)
	float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
	float4x4 _OtherShadowMatrices[MAX_SHADOWED_OTHER_LIGHT_COUNT];
	int _CascadeCount;
	float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
	float4 _CascadeData[MAX_CASCADE_COUNT];
	float4 _OtherShadowTiles[MAX_SHADOWED_OTHER_LIGHT_COUNT];
	float4 _ShadowDistanceFade;
	float4 _ShadowAtlasSize;
CBUFFER_END


static const float3 pointShadowPlanes[6] = {
	float3(-1.0, 0.0, 0.0),
	float3(1.0, 0.0, 0.0),
	float3(0.0, -1.0, 0.0),
	float3(0.0, 1.0, 0.0),
	float3(0.0, 0.0, -1.0),
	float3(0.0, 0.0, 1.0)
};

struct ShadowMask {
	bool always;
	bool distance;
	float4 shadows;
};

struct ShadowData {
	//fade strength
	float strength;
	int cascadeIndex;
	//blend adjacent cascade soft shadow
	float cascadeBlend;
	//shadow mask data
	ShadowMask shadowMask;
};

struct DirectionalShadowData {
	//Shadow strength in light setting
	float strength;
	//number of tiles in atlas
	int tileIndex;
	float normalBias;
	int shadowMaskChannel;
};

struct OtherShadowData {
	//Shadow strength in light setting
	float strength;
	int tileIndex;
	int shadowMaskChannel;
	float3 lightPositionWS;
	float3 spotDirectionWS;
	bool isPoint;
	float3 lightDirectionWS;
};

//fade range shadow distance, (1-d/m)/f
float FadeShadowStrength(float distance, float scale, float fade) {
	return saturate((1.0 - distance * scale) * fade);
}

ShadowData GetShadowData(Surface surfaceWS) {
	ShadowData data;
	//data.strength = surfaceWS.depth <= _ShadowDistance ? 1.0 : 0.0;
	data.strength = FadeShadowStrength(surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);
	data.cascadeIndex = 0;
	data.cascadeBlend = 1;
	int i;
	for (i = 0; i < _CascadeCount; i++) {
		float4 sphere = _CascadeCullingSpheres[i];
		float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
		if (distanceSqr < sphere.w) {
			//cascade range fade
			float fade = FadeShadowStrength(distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z);
			if (i == _CascadeCount - 1) {
				//cascade fade formula : (1 - d2/r2) / (1 - (1 - f2)) 
				data.strength *= fade;
			} else {
				data.cascadeBlend = fade;
			}
			break;
		}
	}
	//if the shadow cascade of this pixel, set stength to zero, and ensure the global strength isn't incorrectly set to zero after the cascade loop
	if (i == _CascadeCount && _CascadeCount > 0) {
		data.strength = 0.0;
	}
	//dither adjacent cascade shadow(except the last cascade
#if defined(_CASCADE_BLEND_DITHER)
	else if (data.cascadeBlend < surfaceWS.dither) {
		i += 1;
	}	
#endif
#if !defined(_CASCADE_BLEND_SOFT)
	//disable multiple sampling
	data.cascadeBlend = 1.0;
#endif
	data.cascadeIndex = i;

	//Init shadow mask data, no-in-use by default
	data.shadowMask.always = false;
	data.shadowMask.distance = false;
	data.shadowMask.shadows = 1.0;

	return data;
}

//positionSTS : Shadow Texture Space
//z component > the sample depth return 1 , means that position is closer to the light, no shadow
//z component < the sample depth return 0 , means that position is far to the light, shadow
float SampleDirectionalShadowAtlas(float3 positionSTS) {
	return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

//PCF: sample shadow atlas multiple times
float FilterDirectionalShadow(float3 positionSTS) {
#if defined(DIRECTIONAL_FILTER_SETUP)
	float weights[DIRECTIONAL_FILTER_SAMPLES];
	float2 positions[DIRECTIONAL_FILTER_SAMPLES];
	//y,w components are texel size, x,z components are texture size
	float4 size = _ShadowAtlasSize.yyxx;
	DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
	float shadow = 0;
	for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++) {
		shadow += weights[i] * SampleDirectionalShadowAtlas(float3(positions[i], positionSTS.z));
	}
	return shadow;
#else
	return SampleDirectionalShadowAtlas(positionSTS);
#endif
}

float GetCascadeShadow(DirectionalShadowData directionalData, ShadowData globalShadowData, Surface surfaceWS) {
	float3 normalbias = surfaceWS.interpolatedNormal * (_CascadeData[globalShadowData.cascadeIndex].y * directionalData.normalBias);
	float3 positionSTS = mul(_DirectionalShadowMatrices[directionalData.tileIndex], float4(surfaceWS.position + normalbias, 1.0)).xyz;
	float shadow = FilterDirectionalShadow(positionSTS);
	if (globalShadowData.cascadeBlend < 1.0) {
		//lerp current shadow with the next cascade
		normalbias = surfaceWS.interpolatedNormal * (_CascadeData[globalShadowData.cascadeIndex + 1].y * directionalData.normalBias);
		positionSTS = mul(_DirectionalShadowMatrices[directionalData.tileIndex + 1], float4(surfaceWS.position + normalbias, 1.0)).xyz;
		shadow = lerp(FilterDirectionalShadow(positionSTS), shadow, globalShadowData.cascadeBlend);
	}
	return shadow;
}

//get shadow mask baked shadow data, supporting multiple lights
float GetBakedShadow(ShadowMask shadowMask, int channel) {
	float shadow = 1.0;
	if (shadowMask.always || shadowMask.distance) {
		if (channel >= 0) {
			shadow = shadowMask.shadows[channel];
		}
	}
	return shadow;
}

//if no realtime shadow rendered(no shadow or culled, supporting multiple lights
float GetBakedShadow(ShadowMask shadowMask, float strength, int channel) {
	if (shadowMask.always || shadowMask.distance) {
		return lerp(1.0, GetBakedShadow(shadowMask, channel), strength);
	}
	return 1.0;
}

//simply mix realtime shadow and baked shadow with strength parameter, supporting multiple lights
float MixBakedAndRealtimeShadows(ShadowData shadowData, float shadow, float strength, int shadowMaskChannel) {
	float baked = GetBakedShadow(shadowData.shadowMask, shadowMaskChannel);
	if (shadowData.shadowMask.distance) {
		//lerp baked and realtime with shadow fade strength
		shadow = lerp(baked, shadow, shadowData.strength);
		//after that lerp with global light shadow strength
		return lerp(1.0, shadow, strength);
	}
	if (shadowData.shadowMask.always) {
		shadow = lerp(1.0, shadow, shadowData.strength);
		shadow = min(baked, shadow);
		return lerp(1.0, shadow, strength);
	}
	//realtime shadow, lerp it just with both strength and fade
	return lerp(1.0, shadow, strength * shadowData.strength);
}

float GetDirectionalShadowAttenuation(DirectionalShadowData directionalData, ShadowData globalShadowData, Surface surfaceWS) {
#if !defined(_RECEIVE_SHADOWS)
	return 1.0;
#endif
	float shadow;
	if (directionalData.strength * globalShadowData.strength <= 0.0) {
		shadow = GetBakedShadow(globalShadowData.shadowMask, directionalData.strength, directionalData.shadowMaskChannel);
	} else {
		shadow = GetCascadeShadow(directionalData, globalShadowData, surfaceWS);
		shadow = MixBakedAndRealtimeShadows(globalShadowData, shadow, directionalData.strength, directionalData.shadowMaskChannel);
	}
	return shadow;
}

//positionSTS : Shadow Texture Space
//z component > the sample depth return 1 , means that position is closer to the light, no shadow
//z component < the sample depth return 0 , means that position is far to the light, shadow
float SampleOtherShadowAtlas(float3 positionSTS, float3 bounds) {
	positionSTS.xy = clamp(positionSTS.xy, bounds.xy, bounds.xy + bounds.z);
	return SAMPLE_TEXTURE2D_SHADOW(_OtherShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

//PCF: sample shadow atlas multiple times
float FilterOtherShadow(float3 positionSTS, float3 bounds) {
#if defined(OTHER_FILTER_SETUP)
	real weights[OTHER_FILTER_SAMPLES];
	real2 positions[OTHER_FILTER_SAMPLES];
	//y,w components are texel size, x,z components are texture size
	float4 size = _ShadowAtlasSize.wwzz;
	OTHER_FILTER_SETUP(size, positionSTS.xy, weights, positions);
	float shadow = 0;
	for (int i = 0; i < OTHER_FILTER_SAMPLES; i++) {
		shadow += weights[i] * SampleOtherShadowAtlas(float3(positions[i], positionSTS.z), bounds);
	}
	return shadow;
#else
	return SampleOtherShadowAtlas(positionSTS, bounds);
#endif
}

float GetOtherShadow(OtherShadowData otherShadowData, ShadowData globalShadowData, Surface surfaceWS) {
	float tileIndex = otherShadowData.tileIndex;
	float3 lightPlane = otherShadowData.spotDirectionWS;
	if (otherShadowData.isPoint) {
		//use ID function to find the face offset, passing the negated dir
		float faceOffset = CubeMapFaceID(-otherShadowData.lightDirectionWS);
		tileIndex += faceOffset;
		lightPlane = pointShadowPlanes[faceOffset];
	}
	float4 tileData = _OtherShadowTiles[tileIndex];
	float3 surfaceToLight = otherShadowData.lightPositionWS - surfaceWS.position;
	//dot can present the distance to spot light range
	float distanceToLightPlane = dot(surfaceToLight, lightPlane);
	//w component is fixed bias, scale it by the distance from the lighting position to the spot light
	float3 normalBias = surfaceWS.normal * (distanceToLightPlane * tileData.w);
	float4 positionSTS = mul(_OtherShadowMatrices[tileIndex], float4(surfaceWS.position + normalBias, 1.0));
	return FilterOtherShadow(positionSTS.xyz / positionSTS.w, tileData.xyz);
}

float GetOtherShadowAttenuation(OtherShadowData otherShadowData, ShadowData globalShadowData, Surface surfaceWS) {
#if !defined(_RECEIVE_SHADOWS)
	return 1.0;
#endif
	float shadow;
	if (otherShadowData.strength * globalShadowData.strength <= 0.0) {
		shadow = GetBakedShadow(globalShadowData.shadowMask, abs(otherShadowData.strength), otherShadowData.shadowMaskChannel);
	} else {
		shadow = GetOtherShadow(otherShadowData, globalShadowData, surfaceWS);
		shadow = MixBakedAndRealtimeShadows(globalShadowData, shadow, otherShadowData.strength, otherShadowData.shadowMaskChannel);
	}
	return shadow;
}
#endif