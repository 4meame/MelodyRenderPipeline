#ifndef MELODY_VOLUMETRIC_LIGHT_PASS_INCLUDED
#define MELODY_VOLUMETRIC_LIGHT_PASS_INCLUDED

int Index;
float4x4 _WorldViewProj;
float3 _CameraForward;
const float MaxRayLength = 499;
#if defined(DIRECTIONAL)
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
	float2 screenUV : VAR_SCREEN_UV;
	float3 positionWS : VAR_POSITION_WS;
};

Varyings DefaultPassVertex(Attributes input) {
	Varyings output;
	output.positionCS = mul(_WorldViewProj, input.positionOS);
	output.screenUV = ComputeScreenPos(output.positionCS);
	output.positionWS = mul(unity_ObjectToWorld, input.positionOS);
	return output;
}



float4 TestFragment(Varyings input) : SV_TARGET {
	float4 color = 1;
	return NoiseVelocity.xxxx;
}

#endif
