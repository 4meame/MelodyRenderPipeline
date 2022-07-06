#ifndef MELODY_GI_INCLUDED
#define MELODY_GI_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"

#if defined(LIGHTMAP_ON)
	#define GI_ATTRIBUTE_DATA float2 lightMapUV : TEXCOORD1;
	#define GI_VARYINGS_DATA float2 lightMapUV : VAR_LIGHT_MAP_UV;
	#define TRANSFER_GI_DATA(input, output) output.lightMapUV = input.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
	#define GI_FRAGMENT_DATA(input) input.lightMapUV
#else
	#define GI_ATTRIBUTE_DATA
	#define GI_VARYINGS_DATA
	#define TRANSFER_GI_DATA(input, output)
	#define GI_FRAGMENT_DATA(input) 0.0
#endif

TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);
TEXTURE2D(unity_ShadowMask);
SAMPLER(samplerunity_ShadowMask);
TEXTURE3D_FLOAT(unity_ProbeVolumeSH);
SAMPLER(samplerunity_ProbeVolumeSH);
TEXTURECUBE(unity_SpecCube0);
SAMPLER(samplerunity_SpecCube0);

struct GI {
//indirect light comes from all direction, written in lightMap, used for diffuse only(specular via reflection probes.
	float3 diffuse;
//specular via reflection probes
	float3 specular;
//shadow mask is also a part of baked lighting, no-in-use by default
	ShadowMask shadowMask;
};

float3 SampleLightMap(float2 lightMapUV) {
#if defined(LIGHTMAP_ON)
//This method has lots of arguments...
//TEXTURE2D_ARGS(textureName, samplerName) --> TEXTURE2D(textureName), SAMPLER(samplerName)
// float4(1.0, 1.0, 0.0, 0.0) --> scale and translation
	return SampleSingleLightmap(TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap),
		lightMapUV,
		float4(1.0, 1.0, 0.0, 0.0),
#if defined(UNITY_LIGHTMAP_FULL_HDR)
		false,
#else
		true,
#endif
		float4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0, 0.0)
		);
#else
	return 0.0;
#endif
}

//this is SampleSH term
float3 SampleLightProbe(Surface surfaceWS) {
#if defined(LIGHTMAP_ON)
	return 0.0;
#else
//unity_ProbeVolumeParams.x stores that whether an LPPV or interpolated light probe is used
	if (unity_ProbeVolumeParams.x) {
		return SampleProbeVolumeSH4(
			TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
			surfaceWS.position, surfaceWS.normal,
			unity_ProbeVolumeWorldToObject,
			unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
			unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
		);
	} else {
		float4 coefficients[7];
		coefficients[0] = unity_SHAr;
		coefficients[1] = unity_SHAg;
		coefficients[2] = unity_SHAb;
		coefficients[3] = unity_SHBr;
		coefficients[4] = unity_SHBg;
		coefficients[5] = unity_SHBb;
		coefficients[6] = unity_SHC;
		return max(0.0, SampleSH9(coefficients, surfaceWS.normal));
	}
#endif
}

float4 SampleBakedShadows(float2 lightMapUV, Surface surfaceWS) {
#if defined(LIGHTMAP_ON)
	return SAMPLE_TEXTURE2D(unity_ShadowMask, samplerunity_ShadowMask, lightMapUV);
#else
	if (unity_ProbeVolumeParams.x) {
		return SampleProbeOcclusion(
			TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
			surfaceWS.position, unity_ProbeVolumeWorldToObject,
			unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
			unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
		);
	} else {
		return unity_ProbesOcclusion;
	};
#endif
}

float3 SampleEnvironment(Surface surfaceWS, BRDF brdf) {
	float3 uvw = reflect(-surfaceWS.viewDirection, surfaceWS.normal);
	float mip = PerceptualRoughnessToMipmapLevel(brdf.perceptualRoughness);
	float4 environment = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, uvw, mip);
	//return enviroment.rgb;
	return DecodeHDREnvironment(environment, unity_SpecCube0_HDR);
}

GI GetGI(float2 lightMapUV, Surface surfaceWS, BRDF brdf) {
	GI gi;
	gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(surfaceWS);
	gi.specular = SampleEnvironment(surfaceWS, brdf);
	gi.shadowMask.always = false;
	gi.shadowMask.distance = false;
	gi.shadowMask.shadows = 1.0;
#if defined(_SHADOW_MASK_ALWAYS)
	gi.shadowMask.always = true;
	gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surfaceWS);
#elif defined(_SHADOW_MASK_DISTANCE)
	gi.shadowMask.distance = true;
	gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surfaceWS);
#endif
#if defined(_SSPR)
	gi.diffuse = 1.0;
	gi.specular = 0.5;
#endif
	return gi;
}

#endif