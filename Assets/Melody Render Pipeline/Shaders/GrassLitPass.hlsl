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

StructuredBuffer<GrassData> _GrassData;
StructuredBuffer<uint> _IdOfVisibleGrass;

TEXTURE2D(_NoiseTex);
SAMPLER(sampler_NoiseTex);

struct Attributes {
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
};

struct Varyings {
	float4 positionCS : SV_POSITION;
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

Varyings LitPassVertex(Attributes input, uint instanceID : SV_InstanceID) {
	Varyings output;
	uint id = _IdOfVisibleGrass[instanceID];
	GrassData data = _GrassData[id];
	float3 localPosition = input.positionOS;
	//random rotation
	float degrees = hash1(id);
	localPosition = RotateAroundYInDegrees(float4(localPosition, 1), 180 * degrees);
	float3 worldPosition = localPosition + data.position;
	output.positionCS = TransformWorldToHClip(worldPosition);
	output.baseUV = input.baseUV;
	output.worldUV = data.worldCoord;
	output.normal = float3(0, 1, 0);
	output.color = float3(hash1(data.chunkID * 20 + 1024), hash1(hash1(data.chunkID) * 10 + 2048), hash1(data.chunkID * 4 + 4096));
	return output;
}

float4 LitPassFragment(Varyings input) : SV_TARGET{
	return float4(
		float3(0.21, 0.88, 0.16) * SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.worldUV).x 
		* 
		dot(input.normal, GetMainLight().direction).x * GetMainLight().color
		, 
		1);
}

float4 ShadowCasterPass(Varyings input) : SV_TARGET{
	return 1.0;
}
#endif
