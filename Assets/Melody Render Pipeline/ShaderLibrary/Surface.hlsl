#ifndef MELODY_SURFACE_INCLUDED
#define MELODY_SURFACE_INCLUDED

struct Surface {
	float3 position;
	float3 normal;
	float3 interpolatedNormal;
	float3 viewDirection;
	float depth;
	float3 color;
	float3 emission;
	float alpha;
	float metallic;
	float occlusion;
	float smoothness;
	//soft shadow dither blur value
	float dither;
	float fresnelStrength;
};

#endif