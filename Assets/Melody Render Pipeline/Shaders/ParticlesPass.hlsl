#ifndef MELODY_PARTICLES_PASS_INCLUDED
#define MELODY_PARTICLES_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

//CBUFFER_START(UnityPerMaterial)
//	float4 _BaseColor;
//CBUFFER_END

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);
TEXTURE2D(_DistortionMap);
SAMPLER(sampler_DistortionMap);

//Support per-instance material data, replace variable with an array reference WHEN NEEDED
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
	UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
	UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeDistance)
	UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeRange)
	UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesDistance)
	UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesRange)
	UNITY_DEFINE_INSTANCED_PROP(float, _DistortionStrength)
	UNITY_DEFINE_INSTANCED_PROP(float, _DistortionBlend)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

float3 flipbookUVB;

struct Attributes {
	float3 positionOS : POSITION;
#if defined(_FLIPBOOK_BLENDING)
	//in flipbook blending,both uv pairs are provided bia texcoord0
	float4 baseUV : TEXCOORD0;
	float flipbookBlend : TEXCOORD1;
#else
	float2 baseUV : TEXCOORD0;
#endif
	float4 color : COLOR;
//Support per-instance material data
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
	float4 positionCS_SS : SV_POSITION;
	float2 baseUV : VAR_BASE_UV;
#if defined(_FLIPBOOK_BLENDING)
	float3 flipbookUVB : VAR_FLIPBOOK;
#endif
#if defined(_VERTEX_COLORS)
	float4 color : VAR_COLOR;
#endif
//Support per-instance material data
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

float2 GetDistortion(float2 baseUV, float3 flipbook) {
	float4 rawMap = SAMPLE_TEXTURE2D(_DistortionMap, sampler_DistortionMap, baseUV);
#if defined(_FLIPBOOK_BLENDING)
	rawMap = lerp(rawMap, SAMPLE_TEXTURE2D(_DistortionMap, sampler_DistortionMap, flipbook.xy), flipbook.z);
#endif
	return DecodeNormal(rawMap, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DistortionStrength)).xy;
}

Varyings UnlitParticlesPassVertex(Attributes input) {
	Varyings output;
//extract the index from the input and store it in a global static variable
	UNITY_SETUP_INSTANCE_ID(input);
//copy the index when it exists
    UNITY_TRANSFER_INSTANCE_ID(input, output);
	float3 positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS_SS = TransformWorldToHClip(positionWS);
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	output.baseUV.xy = input.baseUV.xy * baseST.xy + baseST.zw;
#if defined(_FLIPBOOK_BLENDING)
	output.flipbookUVB.xy = input.baseUV.zw * baseST.xy + baseST.zw;
	output.flipbookUVB.z = input.flipbookBlend;
#endif
#if defined(_VERTEX_COLORS)
	output.color = input.color;
#endif
	return output;
}

float4 UnlitParticlesPassFragment(Varyings input) : SV_TARGET {
	UNITY_SETUP_INSTANCE_ID(input);
//Init fragment data
	Fragment fragment;
	fragment = GetFragment(input.positionCS_SS);

    float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
#if defined(_FLIPBOOK_BLENDING)
	flipbookUVB = input.flipbookUVB;
	baseMap = lerp(baseMap, SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.flipbookUVB.xy), input.flipbookUVB.z);
#endif
#if defined(_NEAR_FADE)
	float nearAttenuation = (fragment.depth - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _NearFadeDistance)) / UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _NearFadeRange);
	baseMap.a *= saturate(nearAttenuation);
#endif
#if defined(_SOFT_PARTICLES)
	float depthDelta = fragment.bufferDepth - fragment.depth;
	float softAttenuation = (depthDelta - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SoftParticlesDistance)) / UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SoftParticlesRange);
	baseMap.a *= saturate(softAttenuation);
#endif
	//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
	float4 base = baseMap * baseColor;
#if defined(_VERTEX_COLORS)
	base *= input.color;
#endif
#if defined(_CLIPPING)
	//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
#endif	
#if defined(_DISTORTION)
	float2 distortion = GetDistortion(input.baseUV, flipbookUVB) * base.a;
	base.rgb = lerp(GetBufferColor(fragment, distortion).rgb, base.rgb, saturate(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DistortionBlend)));
#endif
	//emission color
	float4 emissionMap = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.baseUV);
	float4 emissionColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionColor);
	float3 emission = emissionMap.rgb * emissionColor.rgb;
	base.rgb += emission;

	//objects that write depth should always produce an alpha of 1
	return float4(base.rgb, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ZWrite) ? 1.0 : base.a);
}
#endif
