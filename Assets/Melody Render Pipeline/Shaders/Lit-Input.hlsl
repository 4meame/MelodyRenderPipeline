#ifndef MELODY_LIT_INPUT_PASS_INCLUDED
#define MELODY_LIT_INPUT_PASS_INCLUDED

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
//screen space
TEXTURE2D(_SSR_Filtered);
SAMPLER(sampler_SSR_Filtered);
TEXTURE2D(_SSAO_Filtered);
SAMPLER(sampler_SSAO_Filtered);

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
#if defined(_FLOW)
	UNITY_DEFINE_INSTANCED_PROP(float, _UJump)
	UNITY_DEFINE_INSTANCED_PROP(float, _VJump)
	UNITY_DEFINE_INSTANCED_PROP(float, _Tilling)
	UNITY_DEFINE_INSTANCED_PROP(float, _GridResolution)
	UNITY_DEFINE_INSTANCED_PROP(float, _Speed)
	UNITY_DEFINE_INSTANCED_PROP(float, _FlowStrength)
	UNITY_DEFINE_INSTANCED_PROP(float, _FlowOffset)
	UNITY_DEFINE_INSTANCED_PROP(float, _HeightScale)
	UNITY_DEFINE_INSTANCED_PROP(float, _HeightScaleModulated)
	UNITY_DEFINE_INSTANCED_PROP(float, _TilingModulated)
#endif
#if defined(_WAVE)
	UNITY_DEFINE_INSTANCED_PROP(float4, _WaveA)
	UNITY_DEFINE_INSTANCED_PROP(float4, _WaveB)
	UNITY_DEFINE_INSTANCED_PROP(float4, _WaveC)
#endif
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

#endif
