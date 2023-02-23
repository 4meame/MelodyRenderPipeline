#ifndef MELODY_GRASS_LIT_PASS_INCLUDED
#define MELODY_GRASS_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

StructuredBuffer<float3> _DataBuffer;

struct Attributes {
	float3 positionOS : POSITION;
};

struct Varyings {
	float4 positionCS : SV_POSITION;
};

Varyings LitPassVertex(Attributes input, uint instanceID : SV_InstanceID) {
	Varyings output;
	float3 localPosition = input.positionOS;
	float3 worldPosition = localPosition + _DataBuffer[instanceID];
	output.positionCS = TransformWorldToHClip(worldPosition);
	return output;
}

float4 LitPassFragment(Varyings input) : SV_TARGET{
	return 0.5;
}
#endif
