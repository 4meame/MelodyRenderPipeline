#ifndef MELODY_VOLUMETRIC_LIGHT_PASS_INCLUDED
#define MELODY_VOLUMETRIC_LIGHT_PASS_INCLUDED

int Index;
float4x4 _WorldViewProj;
float3 _CameraForward;
const float _MaxRayLength;
TEXTURE2D(_DepthTexture);
TEXTURE3D(_NoiseTexture);
SAMPLER(sampler_NoiseTexture);
TEXTURE2D(_DitherTexture);
SAMPLER(sampler_DitherTexture);
float4 LightDirection;
float4 LightPosition;
float4 LightColor;
float SampleCount;
float UseHeightFog;
float HeightScale;
float GroundHeight;
float Scattering;
float Extinction;
float SkyboxExtinction;
float MieG;
float UseNoise;
float NoiseScale;
float NoiseIntensity;
float NoiseOffset;
float2 NoiseVelocity;

#if defined(_DIRECTION)
	#define LightDirection _DirectionalLightDirections[Index]
	#define LightColor _DirectionalLightColors[Index]
	#define SampleCount _DirectionalLightSampleData[Index].x
	#define UseHeightFog _DirectionalLightSampleData[Index].y
	#define HeightScale _DirectionalLightSampleData[Index].z
	#define GroundHeight _DirectionalLightSampleData[Index].w
	#define Scattering  _DirectionalLightScatterData[Index].x
	#define Extinction _DirectionalLightScatterData[Index].y
	#define SkyboxExtinction _DirectionalLightScatterData[Index].z
	#define MieG _DirectionalLightScatterData[Index].w
	#define UseNoise _DirectionalLightNoiseData[Index].x
	#define NoiseScale _DirectionalLightNoiseData[Index].y
	#define NoiseIntensity _DirectionalLightNoiseData[Index].z
	#define NoiseOffset _DirectionalLightNoiseData[Index].w
	#define NoiseVelocity _DirectionalLightNoiseVelocity[Index].xy
#elif defined(_SPOT) || defined(_POINT)
	#define LightPosition _OtherLightPositions[Index]
	#define LightColor _OtherLightColors[Index]
	#define SampleCount _OtherLightSampleData[Index].x
	#define UseHeightFog _OtherLightSampleData[Index].y
	#define HeightScale _OtherLightSampleData[Index].z
	#define GroundHeight _OtherLightSampleData[Index].w
	#define Scattering _OtherLightScatterData[Index].x
	#define Extinction _OtherLightScatterData[Index].y
	#define SkyboxExtinction _OtherLightScatterData[Index].z
	#define MieG _OtherLightScatterData[Index].w
	#define UseNoise _OtherLightNoiseData[Index].x
	#define NoiseScale _OtherLightNoiseData[Index].y
	#define NoiseIntensity _OtherLightNoiseData[Index].z
	#define NoiseOffset _OtherLightNoiseData[Index].w
	#define NoiseVelocity _OtherLightNoiseVelocity[Index].xy
#endif

struct Attributes {
	float4 positionOS : POSITION;
};

struct Varyings {
	float4 positionCS : SV_POSITION;
	float4 screenUV : VAR_SCREEN_UV;
	float3 positionWS : VAR_POSITION_WS;
};

Varyings DefaultPassVertex(Attributes input) {
	Varyings output;
	output.positionCS = mul(_WorldViewProj, input.positionOS);
	output.screenUV = ComputeScreenPos(output.positionCS);
	output.positionWS = mul(unity_ObjectToWorld, input.positionOS);
	return output;
}

//height fog
void ApplyHeightFog(float3 posWS, inout float density) {
	if (UseHeightFog == 1) {
		density *= exp(-(posWS.y - GroundHeight) * HeightScale);
	}
}

//volume density
float GetDensity(float3 posWS) {
	float density = 1;
	if (UseNoise == 1) {
		float noise = SAMPLE_TEXTURE3D_LOD(_NoiseTexture, sampler_NoiseTexture, float4(frac(posWS * NoiseScale + float3(_Time.y * NoiseVelocity.x, 0, _Time.y * NoiseVelocity.y)), 0), 0);
		noise = saturate(noise - NoiseOffset) * NoiseIntensity;
		density = saturate(noise);
	}
	ApplyHeightFog(posWS, density);
	return density;
}

//mie scattering
float MieScattering(float cosAngle, float g) {
	float g2 = g * g;
	float phase = (1.0 / (4.0 * PI)) * (1.0 - g2) / (pow((1 + g2 - 2 * g * cosAngle), 3.0 / 2.0));
	return phase;
}

//raymarch
float4 RayMarch(float2 screenPos, float3 rayStart, float3 rayDir, float rayLength) {
	float2 interleavedPos = (fmod(floor(screenPos.xy), 8.0));
	//take care this
	float offset = SAMPLE_TEXTURE2D_LOD(_DitherTexture, sampler_DitherTexture, interleavedPos / 8.0 + float2(0.5 / 8.0, 0.5 / 8.0), 0).w;
	int stepCount = SampleCount;
	float stepSize = rayLength / stepCount;
	float3 step = rayDir * stepSize;
	float3 currentPosition = rayStart + offset * step;
	float4 result = 0;
	float cosAngle;
	float extinction = 0;
	float attenuation = 0;
#if defined(_DIRECTION)
	cosAngle = dot(-LightDirection.xyz, -rayDir);
#elif defined(_SPOT) || defined(_POINT)
	//we don't know about density between camera and light's volume, assume 0.5
	extinction = length(_WorldSpaceCameraPos - currentPosition) * Extinction * 0.5;
#endif
	Surface surfaceData;
	//init surface data to rely on pipeline bilut-in method for now
	surfaceData.position = 0;
	surfaceData.normal = float3(0, 1, 0);
	surfaceData.interpolatedNormal = float3(0, 1, 0);
	surfaceData.viewDirection = 0;
	surfaceData.depth = 0;
	surfaceData.color = 0;
	surfaceData.alpha = 0;
	surfaceData.metallic =0;
	surfaceData.occlusion = 0;
	surfaceData.smoothness = 0;
	surfaceData.dither = 0;
	surfaceData.fresnelStrength =0;
	ShadowData shadowData;
	[loop]
	for (int i = 0; i < stepCount; ++i) {
		surfaceData.position = currentPosition;
		shadowData = GetShadowData(surfaceData);
#if defined(_DIRECTION)
		Light light = GetDirectionalLight(Index, surfaceData, shadowData);
		attenuation = light.shadowAttenuation;
#elif defined(_SPOT) || defined(_POINT)
		Light light = GetOtherLight(Index, surfaceData, shadowData);
		attenuation = light.shadowAttenuation;
#endif
		float density = GetDensity(currentPosition);
		float scattering = Scattering * stepSize * density;
		extinction += Extinction * stepSize * density;
		float4 energy = attenuation * scattering * exp(-extinction);
#if defined(_SPOT) || defined(_POINT)
		//phase function for spot and point lights
		float3 toLight = normalize(currentPosition - LightPosition.xyz);
		cosAngle = dot(toLight, -rayDir);
		energy *= MieScattering(cosAngle, MieG);
#endif
		result += energy;
		currentPosition += step;
	}
	//phase function for spot and point lights
#if defined(_DIRECTION)
	result *= MieScattering(cosAngle, MieG);
#endif
	//apply light's color
	result *= LightColor;
	result = max(0, result);
#if defined(_DIRECTION)
	result.w = exp(-extinction);
#elif defined(_SPOT) || defined(_POINT)
	result.w = 0;
#endif
	return result;
}

float _Range;

float4 fragPointInside(Varyings input) : SV_TARGET{
	float2 uv = input.screenUV.xy / input.screenUV.w;
	//read depth and reconstruct world position
	float depth = SAMPLE_DEPTH_TEXTURE(_DepthTexture, sampler_point_clamp, uv);
	float3 rayStart = _WorldSpaceCameraPos;
	float3 rayEnd = input.positionWS;
	float3 rayDir = (rayEnd - rayStart);
	float rayLength = length(rayDir);
	rayDir /= rayLength;
	float linearDepth = LinearEyeDepth(depth, _ZBufferParams);
	float projectedDepth = linearDepth / dot(_CameraForward, rayDir);
	rayLength = min(rayLength, projectedDepth);
	return RayMarch(input.positionCS.xy, rayStart, rayDir, rayLength);
}

float4 fragPointOutside(Varyings input) : SV_TARGET{
	float2 uv = input.screenUV.xy / input.screenUV.w;
	//read depth and reconstruct world position
	float depth = SAMPLE_DEPTH_TEXTURE(_DepthTexture, sampler_point_clamp, uv);
	float3 rayStart = _WorldSpaceCameraPos;
	float3 rayEnd = input.positionWS;
	float3 rayDir = (rayEnd - rayStart);
	float rayLength = length(rayDir);
	rayDir /= rayLength;
	float3 lightToCamera = _WorldSpaceCameraPos - LightPosition.xyz;
	float b = dot(rayDir, lightToCamera);
	float c = dot(lightToCamera, lightToCamera) - (_Range * _Range);
	float d = sqrt((b * b) - c);
	float start = -b - d;
	float end = -b + d;
	float linearDepth = LinearEyeDepth(depth, _ZBufferParams);
	float projectedDepth = linearDepth / dot(_CameraForward, rayDir);
	end = min(end, projectedDepth);
	rayStart = rayStart + rayDir * start;
	rayLength = end - start;
	return RayMarch(input.positionCS.xy, rayStart, rayDir, rayLength);
}

float _PlaneD;
float _CosAngle;
float4 _ConeApex;
float4 _ConeAxis;

//ray cone intersect
float2 RayConeIntersect(in float3 f3ConeApex, in float3 f3ConeAxis, in float fCosAngle, in float3 f3RayStart, in float3 f3RayDir) {
	float inf = 10000;
	f3RayStart -= f3ConeApex;
	float a = dot(f3RayDir, f3ConeAxis);
	float b = dot(f3RayDir, f3RayDir);
	float c = dot(f3RayStart, f3ConeAxis);
	float d = dot(f3RayStart, f3RayDir);
	float e = dot(f3RayStart, f3RayStart);
	fCosAngle *= fCosAngle;
	float A = a * a - b * fCosAngle;
	float B = 2 * (c * a - d * fCosAngle);
	float C = c * c - e * fCosAngle;
	float D = B * B - 4 * A * C;
	if (D > 0) {
		D = sqrt(D);
		float2 t = (-B + sign(A) * float2(-D, +D)) / (2 * A);
		bool2 b2IsCorrect = c + a * t > 0 && t > 0;
		t = t * b2IsCorrect + !b2IsCorrect * (inf);
		return t;
	}
	else {
		//no intersection
		return inf;
	}
}

//ray plane intersect
float RayPlaneIntersect(in float3 planeNormal, in float planeD, in float3 rayOrigin, in float3 rayDir) {
	float NdotD = dot(planeNormal, rayDir);
	float NdotO = dot(planeNormal, rayOrigin);
	float t = -(NdotO + planeD) / NdotD;
	if (t < 0)
		t = 100000;
	return t;
}

float4 fragSpotInside(Varyings input) : SV_TARGET{
	float2 uv = input.screenUV.xy / input.screenUV.w;
	//read depth and reconstruct world position
	float depth = SAMPLE_DEPTH_TEXTURE(_DepthTexture, sampler_point_clamp, uv);
	float3 rayStart = _WorldSpaceCameraPos;
	float3 rayEnd = input.positionWS;
	float3 rayDir = (rayEnd - rayStart);
	float rayLength = length(rayDir);
	rayDir /= rayLength;
	float linearDepth = LinearEyeDepth(depth, _ZBufferParams);
	float projectedDepth = linearDepth / dot(_CameraForward, rayDir);
	rayLength = min(rayLength, projectedDepth);
	return RayMarch(input.positionCS.xy, rayStart, rayDir, rayLength);
}

float4 fragSpotOutside(Varyings input) : SV_TARGET{
	float2 uv = input.screenUV.xy / input.screenUV.w;
	//read depth and reconstruct world position
	float depth = SAMPLE_DEPTH_TEXTURE(_DepthTexture, sampler_point_clamp, uv);
	float3 rayStart = _WorldSpaceCameraPos;
	float3 rayEnd = input.positionWS;
	float3 rayDir = (rayEnd - rayStart);
	float rayLength = length(rayDir);
	rayDir /= rayLength;
	//inside cone
	float3 r1 = rayEnd + rayDir * 0.001;
	//plane intersection
	float planeCoord = RayPlaneIntersect(_ConeAxis, _PlaneD, r1, rayDir);
	//ray cone intersection
	float2 lineCoords = RayConeIntersect(_ConeApex, _ConeAxis, _CosAngle, r1, rayDir);
	float linearDepth = LinearEyeDepth(depth, _ZBufferParams);
	float projectedDepth = linearDepth / dot(_CameraForward, rayDir);
	rayLength = min(rayLength, projectedDepth);
	float z = (projectedDepth - rayLength);
	rayLength = min(planeCoord, min(lineCoords.x, lineCoords.y));
	rayLength = min(rayLength, z);
	return RayMarch(input.positionCS.xy, rayEnd, rayDir, rayLength);
}

#endif
