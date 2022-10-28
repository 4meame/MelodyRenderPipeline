#ifndef MELODY_VOLUMETRIC_LIGHT_PASS_INCLUDED
#define MELODY_VOLUMETRIC_LIGHT_PASS_INCLUDED

int Index;
float _Range;
float4x4 _WorldViewProj;
float3 _CameraForward;
const float MaxRayLength = 499;
TEXTURE3D(_NoiseTexture);
SAMPLER(sampler_NoiseTexture);
TEXTURE2D(_DitherTexture);
SAMPLER(sampler_DitherTexture);
#if defined(DIRECTIONAL)
#define LightDirection _DirectionalLightDirections[Index]
#define LightColor _DirectionalLightColors[Index]
#define SampleCount _DirectionLightSampleData[Index].x
#define HeightFog _DirectionLightSampleData[Index].y
#define HeightScale _DirectionLightSampleData[Index].z
#define GroundHeight _DirectionLightSampleData[Index].w
#define Scattering  _DirectionLightScatterData[Index].x
#define Extinction _DirectionLightScatterData[Index].y
#define SkyboxExtinction _DirectionLightScatterData[Index].z
#define MieG _DirectionLightScatterData[Index].w
#define UseNoise _DirectionLightNoiseData[Index].x
#define NoiseScale _DirectionLightNoiseData[Index].y
#define NoiseIntensity _DirectionLightNoiseData[Index].z
#define NoiseOffset _DirectionLightNoiseData[Index].w
#define NoiseVelocity _DirectionLightNoiseVelocity[Index].xy
#else
#define LightDirection _OtherLightDirections[Index]
#define LightPosition _OtherLightPositions[Index]
#define LightColor _OtherLightColors[Index]
#define SampleCount _OtherLightSampleData[Index].x
#define UseHeightFog _OtherLightSampleData[Index].y
#define HeightScale _OtherLightSampleData[Index].z
#define GroundHeight _OtherLightSampleData[Index].w
#define Scattering _OtherLightScatterData[Index].x
#define Extinction _OtherLightScatterData[Index].y
#define SkyboxExtinction _OtherLightScatterData[Index].z
#define MieG _OtherLightScatterData[Index].w
#define UseNoise _OtherLightNoiseData[Index].x
#define NoiseScale _OtherLightNoiseData[Index].y
#define NoiseIntensity _OtherLightNoiseData[Index].z
#define NoiseOffset _OtherLightNoiseData[Index].w
#define NoiseVelocity _OtherLightNoiseVelocity[Index].xy
#endif

struct Attributes {
	float4 positionOS : POSITION;
};

struct Varyings {
	float4 positionCS : SV_POSITION;
	float4 screenUV : VAR_SCREEN_UV;
	float3 positionWS : VAR_POSITION_WS;
};

Varyings DefaultPassVertex(Attributes input) {
	Varyings output;
	output.positionCS = mul(_WorldViewProj, input.positionOS);
	output.screenUV = ComputeScreenPos(output.positionCS);
	output.positionWS = mul(unity_ObjectToWorld, input.positionOS);
	return output;
}

//height fog
void ApplyHeightFog(float3 posWS, inout float density) {
	if (UseHeightFog == 1) {
		density *= exp(-(posWS.y + GroundHeight) * HeightScale);
	}
}

//volume density
float GetDensity(float3 posWS) {
	float density = 1;
	if (UseNoise == 1) {
		float noise = SAMPLE_TEXTURE3D_LOD(_NoiseTexture, sampler_NoiseTexture, float4(posWS * NoiseScale + float3(_Time.y * NoiseVelocity.x, 0, _Time.y * NoiseVelocity.y), 0), 0);
		noise = saturate(noise - NoiseOffset) * NoiseIntensity;
		density = saturate(noise);
	}
	ApplyHeightFog(posWS, density);
	return density;
}

//mie scattering
float MieScattering(float cosAngle, float g) {
	float g2 = g * g;
	float phase = (1.0 / (4.0 * PI)) * (1.0 - g2) / (pow((1 + g2 - 2 * g * cosAngle), 3.0 / 2.0));
	return phase;
}

//raymarch
float4 RayMarch(float2 screenPos, float3 rayStart, float3 rayDir, float rayLength) {
	float2 interleavedPos = (fmod(floor(screenPos.xy), 8.0));
	//take care this
	float offset = SAMPLE_TEXTURE2D_LOD(_DitherTexture, sampler_DitherTexture, interleavedPos / 8.0 + float2(0.5 / 8.0, 0.5 / 8.0), 0).w;
	int stepCount = SampleCount;
	float stepSize = rayLength / stepCount;
	float3 step = rayDir * stepSize;
	float3 currentPosition = rayStart + offset * rayDir;
	float4 result = 0;
	float cosAngle;
#if defined(DIRECTIONAL)
	float extinction = 0;
	cosAngle = dot(LightDirection.xyz, -rayDir);
#else
	//we don't know about density between camera and light's volume, assume 0.5
	float extinction = length(_WorldSpaceCameraPos - currentPosition) * Extinction * 0.5;
#endif
	Surface surfaceData;
	//init surface data to rely on pipeline bilut-in method for now
	surfaceData.position = 0;
	surfaceData.normal = float3(0, 1, 0);
	surfaceData.interpolatedNormal = float3(0, 1, 0);
	surfaceData.viewDirection = 0;
	surfaceData.depth = 0;
	surfaceData.color = 0;
	surfaceData.alpha = 0;
	surfaceData.metallic =0;
	surfaceData.occlusion = 0;
	surfaceData.smoothness = 0;
	surfaceData.dither = 0;
	surfaceData.fresnelStrength =0;
	ShadowData shadowData;
	[loop]
	for (int i = 0; i < stepCount; ++i) {
		surfaceData.position = currentPosition;
		shadowData = GetShadowData(surfaceData);
#if defined(DIRECTIONAL)
		Light light = GetDirectionalLight(Index, surfaceData, shadowData);
		float attenuation = light.shadowAttenuation;
#else
		Light light = GetOtherLight(Index, surfaceData, shadowData);
		float attenuation = light.shadowAttenuation;
#endif
		float density = GetDensity(currentPosition);
		float scattering = Scattering * stepSize * density;
		extinction += Extinction * stepSize * density;
		float4 energy = attenuation * scattering * exp(-extinction);
#if !defined(DIRECTIONAL)
		//phase function for spot and point lights
		float3 toLight = normalize(currentPosition - LightPosition.xyz);
		cosAngle = dot(toLight, -rayDir);
		energy *= MieScattering(cosAngle, MieG);
#endif
		result += energy;
		currentPosition += step;
	}
	//phase function for spot and point lights
#if defined(DIRECTIONAL)
	result *= MieScattering(cosAngle, MieG);
#endif
	//apply light's color
	result *= LightColor;
	result = max(0, result);
#if defined(DIRECTIONAL)
	result.w = exp(-extinction);
#else
	result.w = 0;
#endif
	return result;
}

//ray cone intersect


float4 fragPointInside(Varyings input) : SV_TARGET{
	float2 uv = input.screenUV.xy / input.screenUV.w;
	//read depth and reconstruct world position
	float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_point_clamp, uv);
	float3 rayStart = _WorldSpaceCameraPos;
	float3 rayEnd = input.positionWS;
	float3 rayDir = (rayEnd - rayStart);
	float rayLength = length(rayDir);
	rayDir /= rayLength;
	float linearDepth = LinearEyeDepth(depth, _ZBufferParams);
	float projectedDepth = linearDepth / dot(_CameraForward, rayDir);
	rayLength = min(rayLength, projectedDepth);
	return RayMarch(input.positionCS.xy, rayStart, rayDir, rayLength);
}

float4 fragPointOutside(Varyings input) : SV_TARGET{
	float2 uv = input.screenUV.xy / input.screenUV.w;
	//read depth and reconstruct world position
	float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_point_clamp, uv);
	float3 rayStart = _WorldSpaceCameraPos;
	float3 rayEnd = input.positionWS;
	float3 rayDir = (rayEnd - rayStart);
	float rayLength = length(rayDir);
	rayDir /= rayLength;
	float3 lightToCamera = _WorldSpaceCameraPos - LightPosition.xyz;
	float b = dot(rayDir, lightToCamera);
	float c = dot(lightToCamera, lightToCamera) - (_Range * _Range);
	float d = sqrt((b * b) - c);
	float start = -b - d;
	float end = -b + d;
	float linearDepth = LinearEyeDepth(depth, _ZBufferParams);
	float projectedDepth = linearDepth / dot(_CameraForward, rayDir);
	end = min(end, projectedDepth);
	rayStart = rayStart + rayDir * start;
	rayLength = end - start;
	return RayMarch(input.positionCS.xy, rayStart, rayDir, rayLength);
}


#endif
