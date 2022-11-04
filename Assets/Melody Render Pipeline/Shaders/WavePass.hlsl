#ifndef MELODY_WAVE_PASS_INCLUDED
#define MELODY_WAVE_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

float3 GerstnerWave(float4 wave, float3 p, inout float3 tangent, inout float3 binormal) {
	float2 direction = wave.xy;
	float steepness = wave.z;
	float waveLength = wave.w;
	float k = 2 * PI / waveLength;
	//gravity
	float c = sqrt(9.8 / k);
	float2 d = normalize(direction);
	float f = k * (dot(d, p.xz) - c * _Time.y);
	float a = steepness / k;
	//derivative of the surface posotion
	tangent += float3(- d.x * d.x * (steepness * sin(f)), d.x * (steepness * cos(f)), -d.x * d.y * (steepness * sin(f)));
	binormal += float3(-d.x * d.y * (steepness * sin(f)), d.y * (steepness * cos(f)), - d.y * d.y * (steepness * sin(f)));
	return float3(d.x * (a * cos(f)), a * sin(f), d.y * (a * cos(f)));
}

#endif
