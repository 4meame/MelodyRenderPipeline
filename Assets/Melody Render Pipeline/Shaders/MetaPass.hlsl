﻿#ifndef MELODY_META_PASS_INCLUDED
#define MELODY_META_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"

//NOTE : MUST write down all the props whether they are NEEDED
//Support per-instance material data, replace variable with an array reference WHEN NEEDED
#include "Lit-Input.hlsl"

//unity arguement x: diffuse, y: emission ?..
bool4 unity_MetaFragmentControl;
float unity_OneOverOutputBoost;
float unity_MaxOutputValue;

struct Attributes {
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
	float2 lightMapUV : TEXCOORD1;
};

struct Varyings {
	float4 positionCS : SV_POSITION;
	float2 baseUV : VAR_BASE_UV;
};

Varyings MetaPassVertex(Attributes input) {
	Varyings output;
//object space positions are stored in light map
	input.positionOS.xy = input.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
	input.positionOS.z = input.positionOS.z > 0.0 ? FLT_MIN : 0.0;
//OS:
	output.positionCS = TransformWorldToHClip(input.positionOS);
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	output.baseUV = input.baseUV * baseST.xy + baseST.zw;
	return output;
}

float4 MetaPassFragment(Varyings input) : SV_TARGET{
	float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
	float4 base = baseMap * baseColor;
	Surface surface;
	ZERO_INITIALIZE(Surface, surface);
	surface.color = base.rgb;
	surface.metallic = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
	surface.smoothness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
	BRDF brdf = GetBRDF(surface);
//if meta equals to zero, there is no indirect light;
	float4 meta = 0.0;
//Calculate indirect light of diffuse
	if (unity_MetaFragmentControl.x) {
		meta = float4(brdf.diffuse, 1.0);
//highly specular but rough materials also pass along some indirect light
		meta.rgb += brdf.specular * brdf.roughness * 0.5;
		meta.rgb = min(PositivePow(meta.rgb, unity_OneOverOutputBoost), unity_MaxOutputValue);
	}
//Calculate indirect light of emission
	else if (unity_MetaFragmentControl.y) {
		float4 emissionMap = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.baseUV);
		float4 emissionColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionColor);
		float3 emission = emissionMap.rgb * emissionColor.rgb;
		meta = float4(emission, 1.0);
	}
	return meta;
}

#endif
