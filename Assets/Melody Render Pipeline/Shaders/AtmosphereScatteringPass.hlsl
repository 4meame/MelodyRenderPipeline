#ifndef MELODY_ATMOSPHERE_SCATTERING_PASS_INCLUDED
#define MELODY_ATMOSPHERE_SCATTERING_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

#define PI 3.14159265359
float4 _MainLightPosition;
float4 _IncomingLight;
float _SunIntensity;
float _PlanetRadius;
float _AtmosphereHeight;
//Scale Height of raylie and mie scatter
float2 _DensityScaleHeight;
//Extinction factor of raylie and mie scatter
float3 _ExtinctionR;
float3 _ExtinctionM;
float _MieG;

TEXTURE2D(_ParticleDensityLUT);
SAMPLER(sampler_ParticleDensityLUT);

//d1 is if d < 0, ray hit the sphere in the opposite direction
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

//high Mie G value can be used to rendering sun like circle
float3 RenderSun(float cosAngle, float3 scatterM) {
	float g = 0.98;
	float g2 = g * g;
	float sun = pow(1 - g, 2.0) / (4 * PI * pow(1.0 + g2 - 2.0 * g * cosAngle, 1.5));
	sun *= 0.003;
	return scatterM * sun;
}

void ApplyPhaseFunction(inout float3 scatterR, inout float3 scatterM, float cosAngle) {
	//rayliegh phase
	float phase = (3.0 / 16.0 * PI) * (1 + (cosAngle * cosAngle));
	scatterR *= phase;
	//mie phase
	float g = _MieG;
	float2 g2 = g * g;
	phase = (1.0 / (4.0 * PI)) * ((3.0 * (1.0 - g2)) / (2.0 * (2.0 + g2))) * ((1 + cosAngle * cosAngle) / (pow((1 + g2 - 2 * g * cosAngle), 3.0 / 2.0)));
	scatterM *= phase;
}

//get a local point density by searching precomputed particle density texture
void GetAtmosphereDensity(float3 position, float3 planetCenter, float3 lightDir, out float2 localDensity, out float2 densityToAtmTop) {
	float height = length(position - planetCenter) - _PlanetRadius;
	//x : Raylei, y : Mie
	localDensity = exp(-height.xx / _DensityScaleHeight.xy);
	float cosAngle = dot(normalize(position - planetCenter), lightDir);
	//use cosAngle of the lightDir and height percentage of the point to get a local density
	densityToAtmTop = SAMPLE_TEXTURE2D_LOD(_ParticleDensityLUT, sampler_ParticleDensityLUT, float2(cosAngle * 0.5 + 0.5, height / _AtmosphereHeight), 0);
}

//compute a local point inscattering ,it will be used to intergrate path inscattering
void ComputeLocalInscattering(float2 localDensity, float2 densityPA, float2 densityCP, out float3 localInscatterR, out float3 localInscatterM) {
	//we can just add exponent calculation together
	float2 densityCPA = densityCP + densityPA;
	//transmittance
	float3 Tr = densityCPA.x * _ExtinctionR;
	float3 Tm = densityCPA.y * _ExtinctionM;
	//the absorption of extinction can be ignored
	float3 extinction = exp(-(Tr + Tm));
	//local inscattering equals to transmittance from C to A multi local height scatter(density)
	localInscatterR = localDensity.x * extinction;
	localInscatterM = localDensity.y * extinction;
}

float4 IntergrateInscattering(float3 rayStart, float3 rayDir, float rayLength, float3 planetCenter, float distanceScale, float3 lightDir, float sampleCount, out float4 extinction) {
	//ray march vector and the Delta s
	float3 step = rayDir * (rayLength / sampleCount);
	float stepSize = length(step) * distanceScale;
	//P - current integration point
	//C - camera position
	//A - top of the atmosphere
	float2 densityCP = 0;
	float3 scatterR = 0;
	float3 scatterM = 0;
	float2 densityAtP ;
	float2 densityPA ;
	float2 prevDensityAtP;
	float3 prevLocalInscatterR;
	float3 prevLocalInscatterM;
	GetAtmosphereDensity(rayStart, planetCenter, lightDir, prevDensityAtP, densityPA);
	ComputeLocalInscattering(prevDensityAtP, densityPA, densityCP, prevLocalInscatterR, prevLocalInscatterM);
	[loop]
	for (int i = 0; i < sampleCount; i++) {
		float3 p = rayStart + step * i;
		GetAtmosphereDensity(p, planetCenter, lightDir, densityAtP, densityPA);
		densityCP += (densityAtP + prevDensityAtP) * stepSize * 0.5;
		prevDensityAtP = densityAtP;
		float3 localInscatteringR;
		float3 localInscatteringM;
		ComputeLocalInscattering(densityAtP, densityPA, densityCP, localInscatteringR, localInscatteringM);
		scatterR += (localInscatteringR + prevLocalInscatterR) * stepSize * 0.5;
		scatterM += (localInscatteringM + prevLocalInscatterM) * stepSize * 0.5;
		prevLocalInscatterR = localInscatteringR;
		prevLocalInscatterM = localInscatteringM;
	}
	float m = scatterM;
	float cosAngle = dot(rayDir, -lightDir);
	ApplyPhaseFunction(scatterR, scatterM, cosAngle);
	//I = Isun *  β(0) * P(θ) * ∫(exp(-β(0) * (Dcp + Dpa))) * ρ(h)ds
	float3 lightInscatter = (scatterR * _ScatteringR + scatterM * _ScatteringM) * _IncomingLight.xyz;
	lightInscatter += RenderSun(m, cosAngle) * _SunIntensity;
	float3 lightExtinction = exp(-(densityCP.x * _ExtinctionR + densityCP.y * _ExtinctionM));
	extinction = float4(lightExtinction, 0);
	return float4(lightInscatter, 0);
}

#endif
