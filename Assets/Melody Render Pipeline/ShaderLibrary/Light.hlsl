#ifndef MELODY_LIGHT_INCLUDED
#define MELODY_LIGHT_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];
#define MAX_OTHER_LIGHT_COUNT 64

CBUFFER_START(_ChickenLight)
int _DirectionLightCount;
float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
//volume
float4 _DirectionLightSampleData[MAX_DIRECTIONAL_LIGHT_COUNT];
float4 _DirectionLightScatterData[MAX_DIRECTIONAL_LIGHT_COUNT];
float4 _DirectionLightNoiseData[MAX_DIRECTIONAL_LIGHT_COUNT];
float4 _DirectionLightNoiseVelocity[MAX_DIRECTIONAL_LIGHT_COUNT];

int _OtherLightCount;
float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];
float4 _OtherLightDirections[MAX_OTHER_LIGHT_COUNT];
float4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];
float4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];
//volume
float4 _OtherLightSampleData[MAX_OTHER_LIGHT_COUNT];
float4 _OtherLightScatterData[MAX_OTHER_LIGHT_COUNT];
float4 _OtherLightNoiseData[MAX_OTHER_LIGHT_COUNT];
float4 _OtherLightNoiseVelocity[MAX_OTHER_LIGHT_COUNT];

int _MainLightIndex;
float4 _MainLightPosition;
float4 _MainLightColor;
CBUFFER_END

struct Light {
	float3 color;
	float3 direction;
	float shadowAttenuation;
	float distanceAttenuation;
};

int GetDirectionalLightCount() {
	return min(_DirectionLightCount, MAX_DIRECTIONAL_LIGHT_COUNT);
}

int GetOtherLightCount() {
	return _OtherLightCount;
}

DirectionalShadowData GetDirectionalShadowData(int Lightindex, ShadowData shadowData) {
	DirectionalShadowData data;
	//data.strength = _DirectionalLightShadowData[Lightindex].x * shadowData.strength;
	//no longer immediately combine both light shadow strength and shadow fade strength, calculate by step according to shadow mask in use or not
	data.strength = _DirectionalLightShadowData[Lightindex].x;
	data.tileIndex = _DirectionalLightShadowData[Lightindex].y + shadowData.cascadeIndex;
	data.normalBias = _DirectionalLightShadowData[Lightindex].z; 
	data.shadowMaskChannel = _DirectionalLightShadowData[Lightindex].w;
	return data;
}

OtherShadowData GetOtherShadowData(int Lightindex) {
	OtherShadowData data;
	data.strength = _OtherLightShadowData[Lightindex].x;
	data.tileIndex = _OtherLightShadowData[Lightindex].y;
	data.shadowMaskChannel = _OtherLightShadowData[Lightindex].w;
	data.lightPositionWS = 0.0;
	data.spotDirectionWS = 0.0;
	data.isPoint = _OtherLightShadowData[Lightindex].z == 1.0;
	data.lightDirectionWS = 0.0;
	return data;
}

Light GetDirectionalLight(int index, Surface surfaceWS, ShadowData shadowData) {
	Light light;
	light.color = _DirectionalLightColors[index].rgb;
	light.direction = _DirectionalLightDirections[index].xyz;
	//NEEDING PER OBJECT LIGHT, unity_LightData.z is 1 when not culled by the culling mask, otherwise 0
	light.distanceAttenuation = unity_LightData.z;
#if defined(LIGHTMAP_ON) || defined(_MIXED_LIGHTING_SUBTRACTIVE)
	//unity_ProbesOcclusion.x is the mixed light probe occlusion data
	light.distanceAttenuation *= unity_ProbesOcclusion.x;
#endif
	DirectionalShadowData directionalShadowData = GetDirectionalShadowData(index, shadowData);
	light.shadowAttenuation = GetDirectionalShadowAttenuation(directionalShadowData, shadowData, surfaceWS);
	//test for culling spheres
	// light.shadowAttenuation = shadowData.cascadeIndex * 0.25;
	return light;
}

Light GetOtherLight(int index, Surface surfaceWS, ShadowData shadowData) {
	Light light;
	light.color = _OtherLightColors[index].rgb;
	float3 position = _OtherLightPositions[index].xyz;
	float3 ray = position - surfaceWS.position;
	light.direction = normalize(ray);
	//square the distance
	float distanceSqr = max(dot(ray, ray), 0.00001);
	//calculate intensity range attenuation just the same with URP 
	float rangeAttenuation = Square(saturate(1.0 - Square(distanceSqr * _OtherLightPositions[index].w)));
	//spot light is a point light that is enclosed by an occluding sphere with a hole on it, the size of the hole determines the size of the light cone
	//formula is the same with URP : saturate(da + b)2
	float4 spotAngles = _OtherLightSpotAngles[index];
	float3 spotDirection = _OtherLightDirections[index].xyz;
	float spotAttenuation = Square(saturate(dot(spotDirection, light.direction) * spotAngles.x + spotAngles.y));
	//inverse-square law
	//light.shadowAttenuation = spotAttenuation * rangeAttenuation / distanceSqr;
	light.distanceAttenuation = spotAttenuation * rangeAttenuation / distanceSqr;
	OtherShadowData otherShadowData = GetOtherShadowData(index);
	otherShadowData.lightPositionWS = position;
	otherShadowData.spotDirectionWS = spotDirection;
	otherShadowData.lightDirectionWS = light.direction;
	light.shadowAttenuation = light.distanceAttenuation * GetOtherShadowAttenuation(otherShadowData, shadowData, surfaceWS);
	return light;
}

Light GetMainLight() {
	Light light;
	light.direction = _MainLightPosition.xyz;
	//NEEDING PER OBJECT LIGHT, unity_LightData.z is 1 when not culled by the culling mask, otherwise 0
	light.distanceAttenuation = unity_LightData.z;
#if defined(LIGHTMAP_ON) || defined(_MIXED_LIGHTING_SUBTRACTIVE)
	//unity_ProbesOcclusion.x is the mixed light probe occlusion data
	light.distanceAttenuation *= unity_ProbesOcclusion.x;
#endif
	light.shadowAttenuation = 1.0;
	light.color = _MainLightColor.rgb;

	return light;
}

Light GetMainLight(Surface surfaceWS, ShadowData shadowData) {
	Light light = GetMainLight();
	DirectionalShadowData mainLightShadowData = GetDirectionalShadowData(_MainLightIndex, shadowData);
	light.shadowAttenuation = GetDirectionalShadowAttenuation(mainLightShadowData, shadowData, surfaceWS);
	return light;
}
#endif