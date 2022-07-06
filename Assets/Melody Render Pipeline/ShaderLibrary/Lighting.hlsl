#ifndef MELODY_LIGHTING_INCLUDED
#define MELODY_LIGHTING_INCLUDED

float3 IncomingLight(Surface surface, Light light) {
	return saturate(dot(surface.normal, light.direction) * light.shadowAttenuation) * light.color;
}

float3 GetLighting(Surface surface, BRDF brdf, Light light) {
	return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}

float3 GetLighting(Surface surfaceWS, BRDF brdf, GI gi) {
	float3 color = 0.0;
	//init color with light baked global illumination diffuse, multiply the brdf.diffuse to get the reflectivity 
	//color = gi.diffuse * brdf.diffuse;
	color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular);
	ShadowData shadowData = GetShadowData(surfaceWS);
	shadowData.shadowMask = gi.shadowMask;
	//return gi.shadowMask.shadows.rgb;
	for (int i = 0; i < GetDirectionalLightCount(); i++) {
		Light light = GetDirectionalLight(i, surfaceWS, shadowData);
		color += GetLighting(surfaceWS, brdf, light);
	}
#if defined(_LIGHTS_PER_OBJECT)
	for (int j = 0; j < min(8, unity_LightData.y); j++) {
		int lightIndex = unity_LightIndices[(uint)j / 4][(uint)j % 4];
		Light light = GetOtherLight(lightIndex, surfaceWS, shadowData);
		color += GetLighting(surfaceWS, brdf, light);
	}
#else
	for (int j = 0; j < GetOtherLightCount(); j++) {
		Light light = GetOtherLight(j, surfaceWS, shadowData);
		color += GetLighting(surfaceWS, brdf, light);
	}
#endif
	return color;
}
#endif