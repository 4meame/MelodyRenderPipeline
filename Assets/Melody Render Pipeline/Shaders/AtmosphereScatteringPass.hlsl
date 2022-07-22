#ifndef MELODY_ATMOSPHERE_SCATTERING_PASS_INCLUDED
#define MELODY_ATMOSPHERE_SCATTERING_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

#define PI 3.14159265359
float4 _MainLightPosition;
float _PlanetRadius;
float _AtmosphereHeight;


//d1 is if d<0, ray hit the sphere in the opposite direction
float2 RaySphereIntersection(float3 rayOrigin, float3 rayDir, float3 sphereCenter, float sphereRadius) {
	rayOrigin -= sphereCenter;
	float a = dot(rayDir, rayDir);
	float b = 2.0 * dot(rayOrigin, rayDir);
	float c = dot(rayOrigin, rayOrigin) - (sphereRadius * sphereRadius);
	float d = b * b - 4 * a * c;
	if (d < 0) {
		return -1;
	} else {
		d = sqrt(d);
		return float2(-b - d, -b + d) / (2 * a);
	}
}

float4 IntergrateInscattering() {
	return 0;
}

#endif
