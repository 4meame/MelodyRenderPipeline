#ifndef MELODY_LIT_PASS_INCLUDED
#define MELODY_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

#include "Lit-Input.hlsl"

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
	float3 normalWS : VAR_NORMAL;
	float4 tangentWS : VAR_TANGENT;
//Support per-instance material data
	UNITY_VERTEX_INPUT_INSTANCE_ID
//UV Coordinate for the light map
	GI_VARYINGS_DATA
};

Varyings LitPassVertex(Attributes input) {
	Varyings output;
//extract the index from the input and store it in a global static variable
	UNITY_SETUP_INSTANCE_ID(input);
//copy the index when it exists
    UNITY_TRANSFER_INSTANCE_ID(input, output);
//extract the UV Coordinate from the light map data
	TRANSFER_GI_DATA(input, output);
#if defined(_WAVE)
	//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float4 waveA = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _WaveA);
	float4 waveB = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _WaveB);
	float4 waveC = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _WaveC);
	float3 gridPoint = input.positionOS;
	float3 tangent = float3(1, 0, 0);
	float3 binormal = float3(0, 0, 1);
	float3 p = gridPoint;
	p += GerstnerWave(waveA, gridPoint, tangent, binormal);
	p += GerstnerWave(waveB, gridPoint, tangent, binormal);
	p += GerstnerWave(waveC, gridPoint, tangent, binormal);
	float3 normal = normalize(cross(binormal, tangent));
	input.positionOS = p;
	input.normalOS = normal;
#endif
	output.positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(output.positionWS);	
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	output.baseUV = input.baseUV * baseST.xy + baseST.zw;
	float4 detailST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DetailMap_ST);
	output.detailUV = input.baseUV * detailST.xy + detailST.zw;
	output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
	return output;
}

float4 LitPassFragment(Varyings input) : SV_TARGET {
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
	smoothness = lerp(smoothness, detailSmoothness < 0.0 ? 0.0 : 1.0, abs(detailSmoothness) * detailMask);
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
	
#if defined(_FLOW)
	//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	float ujump = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _UJump);
	float vjump = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _VJump);
	float tilling = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Tilling);
	float gridResolution = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _GridResolution);
	float speed = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Speed);
	float flowStrength = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FlowStrength);
	float flowOffset = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FlowOffset);
	float heightScale = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HeightScale);
	float heightScaleModulated = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HeightScaleModulated);
	float tilingModulated = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _TilingModulated);
#if defined(_FLOW_DISTORTION)
	//sample flow map 
	float4 flowMap = SAMPLE_TEXTURE2D(_FlowMap, sampler_FlowMap, input.baseUV);
	//calculate flow uv
	float2 flowVector = flowMap.rg * 2 - 1;
	flowVector *= flowStrength;
	float flowSpeed = length(flowVector);
	float noise = flowMap.a;
	float time = _Time.y * speed + noise;
	float2 jump = float2(ujump, vjump);
	float3 uvwA = FlowUV(input.baseUV, flowVector, jump, flowOffset, tilling, time, false);
	float3 uvwB = FlowUV(input.baseUV, flowVector, jump, flowOffset, tilling, time, true);
	float4 flowA = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvwA.xy) * uvwA.z;
	float4 flowB = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvwB.xy) * uvwB.z;
	base = (flowA + flowB) * baseColor;
	//stronger flow gets higher wave
	float finalHeightScale = flowSpeed * heightScaleModulated * heightScale;
	float3 dhA = UnpackDerivativeHeight(SAMPLE_TEXTURE2D(_DerivHeightMap, sampler_DerivHeightMap, uvwA.xy)) * uvwA.z * finalHeightScale;
	float3 dhB = UnpackDerivativeHeight(SAMPLE_TEXTURE2D(_DerivHeightMap, sampler_DerivHeightMap, uvwB.xy)) * uvwB.z * finalHeightScale;
	normal = normalize(float3(-(dhA.xy + dhB.xy), 1));
#elif defined(_FLOW_DIRECTION)
	//directional flow
	float time = _Time.y * speed;
	float3 dh = FlowGrid(input.baseUV, gridResolution, flowStrength, heightScaleModulated, heightScale, tilling, tilingModulated, time, false);
#if defined(_DUAL_GRID)
	dh = (dh + FlowGrid(input.baseUV, gridResolution, flowStrength, heightScaleModulated, heightScale, tilling, tilingModulated, time, true)) * 0.5;
#endif
	base.rgb = dh.z * dh.z * baseColor;
	normal = normalize(float3(-dh.xy, 1));
#endif
#endif

#if defined(_CLIPPING)
//access material property via UNITY_ACCESS_INSTANCED_PROP( , );
	clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
#endif
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
	return float4(color, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ZWrite) ? 1.0 : surface.alpha);
}
#endif
