#ifndef MELODY_VOLUMETRIC_LIGHT_PASS_INCLUDED
#define MELODY_VOLUMETRIC_LIGHT_PASS_INCLUDED

//x : sample
float4 _DirectionLightSampleData[MAX_DIRECTIONAL_LIGHT_COUNT];
float4 _DirectionLightScatterData[MAX_DIRECTIONAL_LIGHT_COUNT];
float4 _DirectionLightNoiseData[MAX_DIRECTIONAL_LIGHT_COUNT];
float4 _DirectionLightNoiseVelocity[MAX_DIRECTIONAL_LIGHT_COUNT];
float4 _OtherLightSampleData[MAX_OTHER_LIGHT_COUNT];
float4 _OtherLightScatterData[MAX_OTHER_LIGHT_COUNT];
float4 _OtherLightNoiseData[MAX_OTHER_LIGHT_COUNT];
float4 _OtherLightNoiseVelocity[MAX_OTHER_LIGHT_COUNT];

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
	output.positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(output.positionWS);
	output.screenUV = ComputeScreenPos(output.positionCS);
	return output;
}

float4 TestFragment(Varyings input) : SV_TARGET {
	float4 color = 1;
	return color;
}

#endif
