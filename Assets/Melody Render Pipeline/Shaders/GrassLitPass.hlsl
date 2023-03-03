﻿#ifndef MELODY_GRASS_LIT_PASS_INCLUDED
#define MELODY_GRASS_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

struct GrassData {
	float3 position;
	int chunkID;
	float2 worldCoord;
};

TEXTURE2D(_NoiseTex);
SAMPLER(sampler_NoiseTex);
float4 _NoiseTex_ST;
TEXTURE2D(_WindTex);
SAMPLER(sampler_WindTex);
float4 _WindTex_ST;

CBUFFER_START(UnityPerMaterial)
StructuredBuffer<GrassData> _GrassData;
StructuredBuffer<uint> _IdOfVisibleGrass;

float4 _BaseColor;
float4 _HighColor;
float4 _ShadowColor;

float _BendingX;
float _BendingY;

float _WindFrequency;
float _WindStrength;
float _WindSpeed;

CBUFFER_END

struct Attributes {
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
	float3 normal : NORMAL;
};

struct Varyings {
	float4 positionCS : SV_POSITION;
	float3 positionWS : VAR_WORLD_POS;
	float2 baseUV : VAR_BASE_UV;
	float2 worldUV : VAR_WORLD_UV;
	float3 normal : VAR_NORMAL;
	float3 color : VAR_COLOR;
};

float hash1(uint n) {
	//integer hash copied from Hugo Elias
	n = (n << 13U) ^ n;
	n = n * (n * n * 15731U + 789221U) + 1376312589U;
	return float(n & uint(0x7fffffffU)) / float(0x7fffffff);
}

float3 hash3(uint n) {
	//integer hash copied from Hugo Elias
	n = (n << 13U) ^ n;
	n = n * (n * n * 15731U + 789221U) + 1376312589U;
	int3 k = n * float3(n, n * 16807U, n * 48271U);
	return float3(k & 0x7fffffffU.xxx) / float(0x7fffffff);
}

float4 RotateAroundYInDegrees(float4 vertex, float degrees) {
	float alpha = degrees * PI / 180.0;
	float sina, cosa;
	sincos(alpha, sina, cosa);
	float2x2 m = float2x2(cosa, -sina, sina, cosa);
	return float4(mul(m, vertex.xz), vertex.yw).xzyw;
}

float4 RotateAroundXInDegrees(float4 vertex, float degrees) {
	float alpha = degrees * PI / 180.0;
	float sina, cosa;
	sincos(alpha, sina, cosa);
	float2x2 m = float2x2(cosa, -sina, sina, cosa);
	return float4(mul(m, vertex.yz), vertex.xw).zxyw;
}

Varyings LitPassVertex(Attributes input, uint instanceID : SV_InstanceID) {
	Varyings output;
	uint id = _IdOfVisibleGrass[instanceID];
	GrassData data = _GrassData[id];
	output.baseUV = input.baseUV;
	output.worldUV = data.worldCoord;
	float3 localPosition = input.positionOS;
	float degrees = hash1(id);
	//random rotation
	localPosition = RotateAroundYInDegrees(float4(localPosition, 1), degrees * _BendingY);
	//random bending
	localPosition = RotateAroundXInDegrees(float4(localPosition, 1), degrees * _BendingX);
	//wind
	float2 windSample = SAMPLE_TEXTURE2D_LOD(_WindTex, sampler_WindTex, data.worldCoord * _WindTex_ST.xy + _WindTex_ST.zw + _Time.y * _WindSpeed + localPosition.y * 0.01, 0);
	windSample = windSample * 2 - 1;
	float3 wind = normalize(float3(windSample.x, windSample.y, 0)) * _WindStrength;
	//bind root
	localPosition += wind * input.baseUV.y;
	float3 worldPosition = localPosition + data.position;
	output.positionCS = TransformWorldToHClip(worldPosition);
	output.positionWS = worldPosition;
	output.normal = input.normal;
	output.color = float3(hash1(data.chunkID * 20 + 1024), hash1(hash1(data.chunkID) * 10 + 2048), hash1(data.chunkID * 4 + 4096));
	output.color = wind;
	return output;
}



float4 LitPassFragment(Varyings input) : SV_TARGET{
	Surface surfaceData;
	//init surface data to rely on pipeline bilut-in method for now
	surfaceData.position = input.positionWS.xyz;
	surfaceData.normal = input.normal;
	surfaceData.interpolatedNormal = input.normal;
	surfaceData.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
	surfaceData.depth = -TransformWorldToView(input.positionWS).z;
	surfaceData.color = 1.0;
	surfaceData.alpha = 1.0;
	surfaceData.metallic = 0.0;
	surfaceData.occlusion = 1.0;
	surfaceData.smoothness = 0.0;
	surfaceData.dither = 0;
	surfaceData.fresnelStrength = 0.0;
	ShadowData shadowData;
	shadowData = GetShadowData(surfaceData);
	Light light = GetMainLight(surfaceData, shadowData);
	//useful properties as follow
	float3 baseColor = _BaseColor.rgb;
	float2 baseUV = input.baseUV;
	float2 worldUV = input.worldUV;
	float3 n = float3(0, 1, 0);//force shading normal equal to UP
	float3 l = light.direction;
	float3 h = normalize(light.direction + (surfaceData.viewDirection));
	float ndotl = saturate(dot(n, l));
	float ndoth = saturate(dot(n, h));
	//variance
	float4 variance = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, worldUV * _NoiseTex_ST.xy + _NoiseTex_ST.zw);
	//radiance
	float3 diffuse = baseColor * variance * light.color * (ndotl * 0.5 + 0.5) * light.distanceAttenuation * light.shadowAttenuation;
	float3 specular = 0.09 * ndoth * ndoth * ndoth * light.color * light.distanceAttenuation * light.shadowAttenuation * baseUV.y;
	return float4(
		diffuse + specular
		, 
		1);
}

float4 ShadowCasterPass(Varyings input) : SV_TARGET{
	return 1.0;
}
#endif
