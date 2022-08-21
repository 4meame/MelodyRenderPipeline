#ifndef MELODY_COMMON_INCLUDED
#define MELODY_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "UnityInput.hlsl"

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_MATRIX_P glstate_matrix_projection

//Utility params
float4 _Time;
float _CurrentCameraFOV;

//Utility Matrix
float4x4 _ClipToViewMatrix;
float4x4 _InvViewProjMatrix;

//The occlusion data can get instanced automatically, UnityInstancing only does this when SHADOWS_SHADOWMASK is defined
#if defined(_SHADOW_MASK_ALWAYS) || defined(_SHADOW_MASK_DISTANCE)
	#define SHADOWS_SHADOWMASK
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

float Square(float v) {
	return v * v;
}

float DistanceSquared(float3 p_a, float3 p_b) {
	return dot(p_a - p_b, p_a - p_b);
}

//In case of an orthographic camera its last component will be 1, otherwise it will be zero
bool IsOrthographicCamera() {
	return unity_OrthoParams.w;
}

//z component of the ortho camera is the raw value range 0-1, convert it to view-space depth by sacling it with near-far range then add near plane distance
float OrthographicDepthBufferToLinear(float rawDepth) {
#if UNITY_REVERSED_Z
	rawDepth = 1 - rawDepth;
#endif
	return (_ProjectionParams.z - _ProjectionParams.y) * rawDepth + _ProjectionParams.y;
}

float3 DecodeNormal(float4 sample, float scale) {
#if defined(UNITY_NO_DXT5nm)
	return UnpackNormalRGB(sample, scale);
#else
	return UnpackNormalmapRGorAG(sample, scale);
#endif
}

float3 NormalTangentToWorld(float3 normalTS, float3 normalWS, float4 tangentWS) {
	float3x3 tangentToWorld =
		CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
	return TransformTangentToWorld(normalTS, tangentToWorld);
}

float DecodeFloatRG(float2 enc) {
	float2 kDecodeDot = float2(1.0, 1 / 255.0);
	return dot(enc, kDecodeDot);
}

float3 DecodeViewNormalStereo(float4 enc4) {
	float kScale = 1.7777;
	float3 nn = enc4.xyz * float3(2 * kScale, 2 * kScale, 0) + float3(-kScale, -kScale, 1);
	float g = 2.0 / dot(nn.xyz, nn.xyz);
	float3 n;
	n.xy = g * nn.xy;
	n.z = g - 1;
	return n;
}

//Fragment data
SAMPLER(sampler_linear_clamp);
SAMPLER(sampler_linear_repeat);
SAMPLER(sampler_point_clamp);
SAMPLER(sampler_point_repeat);
#include "Fragment.hlsl"

//lod transition function
void ClipLOD(Fragment fragment, float fade) {
#if defined(LOD_FADE_CROSSFADE)
	float dither = InterleavedGradientNoise(fragment.positionSS, 0);
	clip(fade - dither);
#endif
}

//GTAO multiply bounce symbolic expression
float3 MultiBounce(float AO, float3 Albedo) {
	float3 A = 2.0404 * Albedo - 0.3324;
	float3 B = -4.7951 * Albedo + 0.6417;
	float3 C = 2.7552 * Albedo + 0.6903;
	return max(AO, ((AO * A + B) * AO + C) * AO);
}

#endif