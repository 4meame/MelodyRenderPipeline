#ifndef MELODY_LIT_SSPR_PASS_INCLUDED
#define MELODY_LIT_SSPR_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_MaskMap);
SAMPLER(sampler_MaskMap);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);
TEXTURE2D(_DetailMap);
SAMPLER(sampler_DetailMap);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_DetailNormalMap);
SAMPLER(sampler_DetailNormalMap);
TEXTURE2D(_SSPR_ColorResult);
SAMPLER(sampler_SSPR_LinearClamp);
TEXTURE2D(_SSPR_UVNoiseTex);
SAMPLER(sampler_SSPR_UVNoiseTex);
TEXTURE2D(_SSPR_ReflectionAreaTex);
SAMPLER(sampler_SSPR_ReflectionAreaTex);
TEXTURECUBE(_SSPR_ReflectionCube);
SAMPLER(sampler_SSPR_ReflectionCube);


//Support per-instance material data, replace variable with an array reference WHEN NEEDED
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
	UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
	UNITY_DEFINE_INSTANCED_PROP(float, _Occlusion)
	UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
	UNITY_DEFINE_INSTANCED_PROP(float, _Fresnel)
	UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
	UNITY_DEFINE_INSTANCED_PROP(float4, _DetailMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float, _DetailAlbedo)
	UNITY_DEFINE_INSTANCED_PROP(float, _DetailSmoothness)
	UNITY_DEFINE_INSTANCED_PROP(float, _DetailNormalScale)
	UNITY_DEFINE_INSTANCED_PROP(float, _NormalScale)
	UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
	UNITY_DEFINE_INSTANCED_PROP(float4, _SSPR_UVNoiseTex_ST)
	UNITY_DEFINE_INSTANCED_PROP(float, _SSPR_NoiseIntensity)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct Attributes {
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
	float3 normalOS : NORMAL;
	float4 tangentOS : TANGENT;
	//Support per-instance material data
	UNITY_VERTEX_INPUT_INSTANCE_ID
		//UV Coordinate for the light map
		GI_ATTRIBUTE_DATA
};

struct Varyings {
	float4 positionCS : SV_POSITION;
	//any unused identifier can be used here
	float3 positionWS : VAR_POSITION;
	float2 baseUV : VAR_BASE_UV;
	float2 detailUV : VAR_DETAIL_UV;
	float2 reflectUV : VAR_REFLECT_UV;
	float3 normalWS : VAR_NORMAL;
	float4 tangentWS : VAR_TANGENT;
	//Support per-instance material data
	UNITY_VERTEX_INPUT_INSTANCE_ID
		//UV Coordinate for the light map
		GI_VARYINGS_DATA
};

struct ReflectionInput
{
	float3 posWS;
	float2 screenUV;
	float2 screenSpaceNoise;
	float roughness;
	float SSPR_Usage;
};

float2 GetReflectionUVNoise(float2 uv) {
	//noise texture
	float2 noise = SAMPLE_TEXTURE2D(_SSPR_UVNoiseTex, sampler_SSPR_UVNoiseTex, uv).xy;
	noise = noise * 2 - 1;
	//hide missing data, only allow offset to valid location
	noise.y = -abs(noise).y;
	noise.x *= 0.25;
	float noiseIntensity = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SSPR_NoiseIntensity);
	noise *= noiseIntensity;
	return noise;
}

float GetReflectionArea(float2 uv) {
	return SAMPLE_TEXTURE2D(_SSPR_ReflectionAreaTex, sampler_SSPR_ReflectionAreaTex, uv).a;
}

float3 GetReflectionResult(ReflectionInput input) {
	//sample scene's reflection probe
	float3 viewWS = (input.posWS - _WorldSpaceCameraPos);
	viewWS = normalize(viewWS);
	//reflect at horizontal plane
	float3 reflectDirWS = viewWS * float3(1, -1, 1);
	float mip = PerceptualRoughnessToMipmapLevel(input.roughness);
	float4 enviroment = SAMPLE_TEXTURECUBE_LOD(_SSPR_ReflectionCube, sampler_SSPR_ReflectionCube, reflectDirWS, mip);
	float4 SSPRResult = 0;
	SSPRResult = SAMPLE_TEXTURE2D(_SSPR_ColorResult, sampler_SSPR_LinearClamp, input.screenUV + input.screenSpaceNoise);
	float3 finalReflection = lerp(enviroment.rgb, SSPRResult.rgb, SSPRResult.a * input.SSPR_Usage);
	return finalReflection;
}

Varyings SSPRPassVertex(Attributes input) {
	Varyings output;
//extract the index from the input and store it in a global static variable
	UNITY_SETUP_INSTANCE_ID(input);
	//copy the index when it exists
	UNITY_TRANSFER_INSTANCE_ID(input, output);
//extract the UV Coordinate from the light map data
	TRANSFER_GI_DATA(input, output);
	output.positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(output.positionWS);
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	output.baseUV = input.baseUV * baseST.xy + baseST.zw;
	float4 detailST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DetailMap_ST);
	output.detailUV = input.baseUV * detailST.xy + detailST.zw;
	float4 reflectST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SSPR_UVNoiseTex_ST);
	float reflectSpeed = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SSPR_NoiseIntensity);
	output.reflectUV = input.baseUV * reflectST.xy + reflectST.zw + _Time.y * reflectSpeed / 4.0;
	output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
	return output;
}

float4 SSPRPassFragment(Varyings input) : SV_TARGET{
	UNITY_SETUP_INSTANCE_ID(input);
	//Init fragment data
	Fragment fragment;
	fragment = GetFragment(input.positionCS);
//Unity Lod Group, unity_LODFade.x is factor of one and reduces to zero
	//ClipLOD(input.positionCS.xy, unity_LODFade.x);
	ClipLOD(fragment, unity_LODFade.x);

	//sample detail and MODS map
	float4 detailMap = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, input.detailUV);
	float4 maskMap = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.baseUV);
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float detailColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DetailAlbedo);
	float detailSmoothness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DetailSmoothness);
	//values of 0.5 are neutral.Higher values should increase or brighten, while lower values should decrease or darken
	detailMap = detailMap * 2.0 - 1.0;
	float detailMask = maskMap.b;

	float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
	detailColor = detailMap.r * detailColor;
	baseMap.rgb = lerp(sqrt(baseMap.rgb), detailColor < 0.0 ? 0.0 : 1.0, abs(detailColor)* detailMask);
	//approximate gamma by interpolating the square root of the albedo, and squaring
	baseMap.rgb *= baseMap.rgb;
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
	float4 base = baseMap * baseColor;
	//base.rgb = abs(length(input.normalWS) - 1.0) * 10.0;

//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float metallic = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
	metallic *= maskMap.r;
	//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float occlusion = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Occlusion);
	occlusion *= maskMap.g;
	//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float smoothness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
	detailSmoothness = detailMap.b * detailSmoothness;
	smoothness = lerp(smoothness, detailSmoothness < 0.0 ? 0.0 : 1.0, abs(detailSmoothness)* detailMask);
	smoothness *= maskMap.a;

	//get tangent space normal
	float4 normalMap = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.baseUV);
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float normalScale = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _NormalScale);
	float3 normal = DecodeNormal(normalMap, normalScale);
	float4 detailNormalMap = SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, input.detailUV);
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float detailNormalScale = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DetailNormalScale);
	float3 detailNormal = DecodeNormal(detailNormalMap, detailNormalScale) * detailMask;
	//UDN Blending
	normal = normalize(float3(normal.xy + detailNormal.xy, normal.z));


	Surface surface;
	surface.position = input.positionWS;
	surface.normal = NormalTangentToWorld(normal, normalize(input.normalWS), normalize(input.tangentWS));
	surface.interpolatedNormal = input.normalWS;
	surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
	surface.depth = -TransformWorldToView(input.positionWS).z;
	surface.color = base.rgb;
	surface.alpha = base.a;
	surface.metallic = metallic;
	//only work on indirect environmental lighting
	surface.occlusion = occlusion;
	surface.smoothness = smoothness;
	//surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);
	surface.dither = InterleavedGradientNoise(fragment.positionSS, 0);
	surface.fresnelStrength = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Fresnel);
#if defined(_PREMULTIPLY_ALPHA)
	BRDF brdf = GetBRDF(surface, true);
#else
	BRDF brdf = GetBRDF(surface);
#endif
	GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);
	float3 color = GetLighting(surface, brdf, gi);
	//emission color
	float4 emissionMap = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.baseUV);
	float4 emissionColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionColor);
	float3 emission = emissionMap.rgb * emissionColor.rgb;
	color += emission;
	//objects that write depth should always produce an alpha of 1

	float relectionArea = GetReflectionArea(input.baseUV);
	float2 _SSPR_Noise = GetReflectionUVNoise(input.reflectUV);
	ReflectionInput reflectionInput;
	reflectionInput.posWS = input.positionWS;
	reflectionInput.screenUV = fragment.screenUV;
	reflectionInput.screenSpaceNoise = _SSPR_Noise;
	reflectionInput.roughness = brdf.roughness;
	reflectionInput.SSPR_Usage = baseColor.a;
	float3 reflectionResult = GetReflectionResult(reflectionInput);	

#if defined(_CLIPPING)
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	clip(relectionArea - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
#endif
	//objects that write depth should always produce an alpha of 1
	return float4(lerp(color.rgb, reflectionResult, base.a), UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ZWrite) ? 1.0 : base.a);
}

#endif
