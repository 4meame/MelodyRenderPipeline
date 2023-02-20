#ifndef MELODY_GRASS_LIT_PASS_INCLUDED
#define MELODY_GRASS_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

StructuredBuffer<float4> positionBuffer;

struct Attributes {
	float3 positionOS : POSITION;
};

struct Varyings {
	float4 positionCS : SV_POSITION;
};

Varyings LitPassVertex(Attributes input, uint instanceID : SV_InstanceID) {
	Varyings output;
	float4 data = positionBuffer[instanceID];
	float3 localPosition = input.positionOS.xyz * data.w;
	float3 worldPosition = data.xyz + localPosition;
	output.positionCS = mul(UNITY_MATRIX_VP, float4(worldPosition, 1.0f));
	return output;
}

float4 LitPassFragment(Varyings input) : SV_TARGET{
	return 0.5;
}
#endif
