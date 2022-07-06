#ifndef MELODY_BRDF_INCLUDED
#define MELODY_BRDF_INCLUDED

#define MIN_REFLECTIVITY 0.04

struct BRDF {
	float3 diffuse;
	float3 specular;
	float roughness;
//use this value to map various mip levels of enviroment 
	float perceptualRoughness;
	float fresnel;
};

float OneMinusReflectivity(float metallic) {
	float range = 1.0 - MIN_REFLECTIVITY;
	return range - metallic * range;
}

BRDF GetBRDF(Surface surface, bool applyAlphaToDiffuse = false) {
	BRDF brdf;
	float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
	brdf.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
	brdf.diffuse = surface.color * oneMinusReflectivity;
	if (applyAlphaToDiffuse) {
		brdf.diffuse *= surface.alpha;
	}
	//F0 term
	brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);
	brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);
	brdf.fresnel = saturate(surface.smoothness + 1.0 - oneMinusReflectivity);
	return brdf;
}

//Approximation of the CookTorrance BRDF
float SpecularStrength(Surface surface, BRDF brdf, Light light) {
	//half vector
	float3 h = SafeNormalize(light.direction + surface.viewDirection);
	float ndoth_2 = Square(saturate(dot(surface.normal, h)));
	float ldoth_2 = Square(saturate(dot(light.direction, h)));
	float r_2 = Square(brdf.roughness);
	float d_2 = Square(ndoth_2 * (r_2 - 1.0) + 1.00001);
	float normalization = brdf.roughness * 4.0 + 2.0;
	return r_2 / (d_2 * max(0.1, ldoth_2) * normalization);
}

float3 DirectBRDF(Surface surface, BRDF brdf, Light light) {
	//direct specular plus direct diffuse
	return SpecularStrength(surface, brdf, light) * brdf.specular + brdf.diffuse;
}

float3 IndirectBRDF(Surface surface, BRDF brdf, float3 diffuse, float3 specular) {
	//float3 reflection = specular * brdf.specular;
	float fresnelStrength = Pow4(1.0 - saturate(dot(surface.normal, surface.viewDirection))) * surface.fresnelStrength;
	float3 reflection = specular * lerp(brdf.specular, brdf.fresnel, fresnelStrength);
    //high roughness will halve the reflection while low roughness will not matter much
	reflection /= brdf.roughness * brdf.roughness + 1.0;
	//Indirect specular plus indirect diffuse
	return (reflection + diffuse * brdf.diffuse) * surface.occlusion;
}
#endif