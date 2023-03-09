#ifndef MELODY_GRASS_LIT_PASS_INCLUDED
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

TEXTURE2D(_ColorMap);
SAMPLER(sampler_ColorMap);
float4 _ColorMap_ST;
TEXTURE2D(_DistortionMap);
SAMPLER(sampler_DistortionMap);
float4 _DistortionMap_ST;
TEXTURE2D(_WaveNoise);
SAMPLER(sampler_WaveNoise);

CBUFFER_START(UnityPerMaterial)
StructuredBuffer<GrassData> _GrassData;
StructuredBuffer<uint> _IdOfVisibleGrass;

float4 _BaseColor;
float4 _HighColor;
float4 _ShadowColor;

float _DistributionX;
float _DistributionY;

float _Height;
float _Width;
float _Curvature;
float _CurvatureBase;

float _WindStrength;
float _WindSpeed;
float _WaveStrength;
float _WaveCycle;

float4 _ScatterFactor;
float _NormalDistribution;

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
	float hash : VAR_HASH;
	float3 wind : VAR_WIND;
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

float3 GetWindDirection(float3 grassUp, float3 windDirection, float windStrength) {
	float rad = (windStrength * PI / 2) * 0.9;
	float3 dir = normalize(windDirection - dot(windDirection, grassUp) * grassUp);
	float x, y;
	sincos(rad, x, y);
	dir = x * dir + y * grassUp;
	return dir - grassUp;
}

float GetWindStrength(float offset, float noise) {
	//TODO
	return saturate(_WindStrength * offset + _WaveStrength * sin(noise * _WaveCycle));
}

Varyings LitPassVertex(Attributes input, uint instanceID : SV_InstanceID) {
	Varyings output;
	uint id = _IdOfVisibleGrass[instanceID];
	GrassData data = _GrassData[id];
	output.baseUV = input.baseUV;
	output.worldUV = data.worldCoord;
	float3 localPosition = input.positionOS;
	//modify grass shape
	localPosition.y *= _Height;
	localPosition.x *= _Width;
	float t = _CurvatureBase > 0.5 ? localPosition.y : input.baseUV.y;
	localPosition.z += pow(t * _Curvature, 2) * t;
	float degrees = hash1(id);
	//random distribution on XY axis
	localPosition = RotateAroundXInDegrees(float4(localPosition, 1), degrees * _DistributionX);
	localPosition = RotateAroundYInDegrees(float4(localPosition, 1), degrees * _DistributionY);
	//wind and wave noise
	float4 distortion = SAMPLE_TEXTURE2D_LOD(_DistortionMap, sampler_DistortionMap, data.worldCoord * _DistortionMap_ST.xy + _DistortionMap_ST.zw - _Time.y * _WindSpeed + localPosition.y * 0.01, 0);
	float noise = SAMPLE_TEXTURE2D_LOD(_WaveNoise, sampler_WaveNoise, data.worldCoord, 0);
	float windStrength = GetWindStrength(length(distortion.xyz), noise);
	//bind root
	float3 wind = GetWindDirection(float3(0, 1, 0), normalize(distortion.xzy), windStrength) * localPosition.y;
	localPosition += wind;
	float3 worldPosition = localPosition + data.position;
	output.positionCS = TransformWorldToHClip(worldPosition);
	output.positionWS = worldPosition;
	output.normal = TransformObjectToWorldNormal(input.normal);
	output.color = float3(hash1(data.chunkID * 20 + 1024), hash1(hash1(data.chunkID) * 10 + 2048), hash1(data.chunkID * 4 + 4096));
	output.hash = hash1(id);
	output.wind = wind;
	return output;
}



float4 LitPassFragment(Varyings input) : SV_TARGET{
	//return float4(input.color, 1);
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
	float3 baseColor = lerp(_ShadowColor.rgb, _BaseColor.rgb, input.baseUV.y);
	float2 baseUV = input.baseUV;
	float2 worldUV = input.worldUV;
	float3 n = float3(0, 1, 0);//force shading normal equal to UP
	float3 randomNormal = _NormalDistribution * sin(input.hash * 78.9321) * float3(0, 0, 1) - 0.33 * input.wind;
	n = normalize(n + randomNormal);
	float3 l = light.direction;
	float3 v = surfaceData.viewDirection;
	float3 h = normalize(l + v);
	float3 h_sss = normalize(l + n * _ScatterFactor.x);
	float ndotl = saturate(dot(n, l));
	float ndoth = saturate(dot(n, h));
	//variance
	float4 variance = SAMPLE_TEXTURE2D(_ColorMap, sampler_ColorMap, worldUV * _ColorMap_ST.xy + _ColorMap_ST.zw);
	//radiance
	float3 diffuse = baseColor * variance.rgb * light.color * (ndotl * 0.5 + 0.5) * light.distanceAttenuation * light.shadowAttenuation;
	float3 specular = 0.09 * _HighColor.rgb * ndoth * ndoth * ndoth * light.color * light.distanceAttenuation * light.shadowAttenuation * baseUV.y;
	float3 sss = pow(saturate(dot(v, -h_sss)), _ScatterFactor.y) * _ScatterFactor.z * light.color * light.distanceAttenuation * light.shadowAttenuation;
	return float4(
		diffuse + specular + sss
		, 
		1);
}

float4 ShadowCasterPass(Varyings input) : SV_TARGET{
	return 1.0;
}
#endif
