#ifndef MELODY_ATMOSPHERE_SCATTERING_PASS_INCLUDED
#define MELODY_ATMOSPHERE_SCATTERING_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

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
//Scattering factor of raylie and mie scatter
float3 _ScatteringR;
float3 _ScatteringM;
float _MieG;

float _LightSamples;
float _DistanceScale;

TEXTURE2D(_ParticleDensityLUT);
SAMPLER(sampler_ParticleDensityLUT);
TEXTURE2D(_RandomVectors);
SAMPLER(sampler_RandomVectors);

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
float3 RenderSun(float3 scatterM, float cosAngle) {
	float g = 0.98;
	float g2 = g * g;
	float sun = pow(1 - g, 2.0) / (4 * PI * pow(1.0 + g2 - 2.0 * g * cosAngle, 1.5));
	sun *= 0.003;
	return scatterM * sun;
}

void ApplyPhaseFunction(inout float3 scatterR, inout float3 scatterM, float cosAngle) {
	//rayliegh phase
	float phase = (3.0 / (16.0 * PI)) * (1 + (cosAngle * cosAngle));
	scatterR *= phase;
	//mie phase
	float g = _MieG;
	float g2 = g * g;
	phase = (1.0 / (4.0 * PI)) * ((3.0 * (1.0 - g2)) / (2.0 * (2.0 + g2))) * ((1 + cosAngle * cosAngle) / (pow((1 + g2 - 2 * g * cosAngle), 3.0 / 2.0)));
	scatterM *= phase;
}

void ApplyPhaseFunctionElek(inout float3 scatterR, inout float3 scatterM, float cosAngle) {
	//rayliegh phase
	float phase = (8.0 / 10.0) / (4 * PI) * ((7.0 / 5.0) + 0.5 * cosAngle);
	scatterR *= phase;
	//mie phase
	float g = _MieG;
	float g2 = g * g;
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
	float3 step = rayDir * (rayLength * distanceScale / sampleCount);
	float stepSize = length(step);
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
	for (float i = 1.0; i < sampleCount; i += 1) {
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
	float3 m = scatterM;
	float cosAngle = dot(rayDir, lightDir);
	ApplyPhaseFunction(scatterR, scatterM, cosAngle);
	//I = Isun *  β(0) * P(θ) * ∫(exp(-β(0) * (Dcp + Dpa))) * ρ(h)ds
	float3 lightInscatter = (scatterR * _ScatteringR + scatterM * _ScatteringM) * _IncomingLight.xyz;
	lightInscatter += RenderSun(m, cosAngle) * _SunIntensity;
	float3 lightExtinction = exp(-(densityCP.x * _ExtinctionR + densityCP.y * _ExtinctionM));
	extinction = float4(lightExtinction, 0);
	return float4(lightInscatter, 0);
}

//compute density of the all height and zenith angle situation, so we can store density of all light direction and height  
float2 PrecomputeParticleDensity(float3 rayStart, float3 rayDir) {
	float3 planetCenter = float3(0, -_PlanetRadius, 0);
	float sampleCount = 256;
	float2 intersection = RaySphereIntersection(rayStart, rayDir, planetCenter, _PlanetRadius);
	//write very high density if intersect planet
	if (intersection.x > 0) {
		return 1e+20;
	}
	intersection = RaySphereIntersection(rayStart, rayDir, planetCenter, _PlanetRadius + _AtmosphereHeight);
	float3 rayEnd = rayStart + rayDir * intersection.y;
	//compute density along the ray
	float3 step = (rayEnd - rayStart) / sampleCount;
	float stepSize = length(step);
	float2 density = 0;
	for (float i = 0.5; i < sampleCount; i += 1) {
		float3 p = rayStart + i * step;
		//why abs ?  Due to spherical symmetry ?
		float height = abs(length(p - planetCenter) - _PlanetRadius);
		float2 localDensity = exp(-height.xx / _DensityScaleHeight);
		density += localDensity * stepSize;
	}
	return density;
}

//compute the color of sun reachs planet ground
float4 PrecomputeSunColor(float3 rayStart, float3 rayDir) {
	float3 planetCenter = float3(0, -_PlanetRadius, 0);
	float2 localDensity;
	float2 densityToAtmosphereTop;
	GetAtmosphereDensity(rayStart, planetCenter, rayDir, localDensity, densityToAtmosphereTop);
	float4 color;
	//transmittance from sun to ground object
	float3 Tr = densityToAtmosphereTop.x * _ExtinctionR;
	float3 Tm = densityToAtmosphereTop.y * _ExtinctionM;
	//the absorption of extinction can be ignored
	float3 extinction = exp(-(Tr + Tm));
	color.xyz = _IncomingLight.xyz * extinction;
	color.w = 1;
	return color;
}

//compute abment color : ∫2PI (N * ω) Inscatter(ω), use Monte Carlo method
float4 PrecomputeAmbient(float3 lightDir) {
	float startHeight = 0;
	float3 rayStart = float3(0, startHeight, 0);
	float3 planetCenter = float3(0, -_PlanetRadius, 0);
	float sampleCount = 256;
	float4 color;
	[loop]
	for (int i = 0; i < sampleCount; i++) {
		float3 rayDir = SAMPLE_TEXTURE2D_LOD(_RandomVectors, sampler_RandomVectors, float2((i / sampleCount), 0.5), 0);
		//in case the random vector is invert
		rayDir.y = abs(rayDir.y);
		float2 intersection = RaySphereIntersection(rayStart, rayDir, planetCenter, _PlanetRadius + _AtmosphereHeight);
		float rayLength = intersection.y;
		//ray should be end at the first intersect point if hit the planet
		intersection = RaySphereIntersection(rayStart, rayDir, planetCenter, _PlanetRadius);
		if (intersection.x >= 0) {
			rayLength = min(rayLength, intersection.x);
		}
		float4 extinction;
		float4 inscattering = IntergrateInscattering(rayStart, rayDir, rayLength, planetCenter, 1, lightDir, _LightSamples, extinction);
		//n dot ω
		color += inscattering * dot(rayDir, float3(0, 1, 0));
	}
	return color * 2 * PI / sampleCount;
}
#endif
