#ifndef MELODY_SHADOW_CASTER_PASS_INCLUDED
#define MELODY_SHADOW_CASTER_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

//NOTE : MUST write down all the props whether they are NEEDED
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
	//flow
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
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct Attributes {
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
//Support per-instance material data
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
	float4 positionCS : SV_POSITION;
//any unused identifier can be used here
	float2 baseUV : VAR_BASE_UV;
//Support per-instance material data
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

bool _ShadowPancaking;

Varyings ShadowCasterPassVertex(Attributes input) {
	Varyings output;
//extract the index from the input and store it in a global static variable
	UNITY_SETUP_INSTANCE_ID(input);
//copy the index when it exists
    UNITY_TRANSFER_INSTANCE_ID(input, output);
	float3 positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(positionWS);
	if (_ShadowPancaking) {
		//flatten shadow casters that lie front of the near plane, decrease shadow pancaking artifacts
#if UNITY_REVERSED_Z
		output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
		output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif
	}
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	output.baseUV = input.baseUV * baseST.xy + baseST.zw;
	return output;
}

void ShadowCasterPassFragment(Varyings input) {
	UNITY_SETUP_INSTANCE_ID(input);
//Init fragment data
	Fragment fragment;
	fragment = GetFragment(input.positionCS);
//Unity Lod Group, unity_LODFade.x is factor of one and reduces to zero
	ClipLOD(fragment, unity_LODFade.x);

    float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
	float4 base = baseMap * baseColor;
	//base.rgb = abs(length(input.normalWS) - 1.0) * 10.0;
#if defined(_SHADOWS_CLIP)
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
#elif defined(_SHADOWS_DITHER)
	//float dither = InterleavedGradientNoise(input.positionCS.xy, 0);
	float dither = InterleavedGradientNoise(fragment.positionSS, 0);
	clip(base.a - dither);
#endif
}
#endif
