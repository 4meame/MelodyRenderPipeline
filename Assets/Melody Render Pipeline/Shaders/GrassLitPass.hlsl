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
	int visable;
};

StructuredBuffer<GrassData> _GrassData;

struct Attributes {
	float3 positionOS : POSITION;
};

struct Varyings {
	float4 positionCS : SV_POSITION;
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

Varyings LitPassVertex(Attributes input, uint instanceID : SV_InstanceID) {
	Varyings output;
	GrassData data = _GrassData[instanceID];
	float3 localPosition = input.positionOS;
	float3 worldPosition = localPosition + data.position;
	output.positionCS = TransformWorldToHClip(worldPosition);
	output.color = float3(hash1(data.chunkID * 20 + 1024), hash1(hash1(data.chunkID) * 10 + 2048), hash1(data.chunkID * 4 + 4096));
	if (data.visable == 0) {
		output.color = 0;
	}
	return output;
}

float4 LitPassFragment(Varyings input) : SV_TARGET{
	return float4(input.color, 1);
}
#endif
