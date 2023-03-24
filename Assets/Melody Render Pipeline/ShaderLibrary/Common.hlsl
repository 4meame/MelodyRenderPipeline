#ifndef MELODY_COMMON_INCLUDED
#define MELODY_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "UnityInput.hlsl"

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_I_V unity_MatrixInvV
#define UNITY_MATRIX_P OptimizeProjectionMatrix(glstate_matrix_projection)
#define UNITY_MATRIX_I_P ERROR_UNITY_MATRIX_I_P_IS_NOT_DEFINED
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_MATRIX_I_VP _InvViewProjMatrix
#define UNITY_MATRIX_MV mul(UNITY_MATRIX_V, UNITY_MATRIX_M)
#define UNITY_MATRIX_T_MV transpose(UNITY_MATRIX_MV)
#define UNITY_MATRIX_IT_MV transpose(mul(UNITY_MATRIX_I_M, UNITY_MATRIX_I_V))
#define UNITY_MATRIX_MVP mul(UNITY_MATRIX_VP, UNITY_MATRIX_M)
#define UNITY_PREV_MATRIX_M unity_MatrixPreviousM
#define UNITY_PREV_MATRIX_I_M unity_MatrixPreviousMI

#define Inv_PI 0.3183091
#define Two_PI 6.2831852
#define Inv_Two_PI 0.15915494

//Utility params
float _CurrentCameraFOV;

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

float2 Square(float2 x) {
	return x * x;
}

float3 Square(float3 x) {
	return x * x;
}

float4 Square(float4 x) {
	return x * x;
}

float pow2(float x) {
	return x * x;
}

float2 pow2(float2 x) {
	return x * x;
}

float3 pow2(float3 x) {
	return x * x;
}

float4 pow2(float4 x) {
	return x * x;
}

float pow3(float x) {
	return x * x * x;
}

float2 pow3(float2 x) {
	return x * x * x;
}

float3 pow3(float3 x) {
	return x * x * x;
}

float4 pow3(float4 x) {
	return x * x * x;
}

float pow4(float x) {
	float xx = x * x;
	return xx * xx;
}

float2 pow4(float2 x) {
	float2 xx = x * x;
	return xx * xx;
}

float3 pow4(float3 x) {
	float3 xx = x * x;
	return xx * xx;
}

float4 pow4(float4 x) {
	float4 xx = x * x;
	return xx * xx;
}

float pow5(float x) {
	float xx = x * x;
	return xx * xx * x;
}

float2 pow5(float2 x) {
	float2 xx = x * x;
	return xx * xx * x;
}

float3 pow5(float3 x) {
	float3 xx = x * x;
	return xx * xx * x;
}

float4 pow5(float4 x) {
	float4 xx = x * x;
	return xx * xx * x;
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

float4 ComputeScreenPos(float4 positionCS) {
	float4 o = positionCS * 0.5f;
	o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
	o.zw = positionCS.zw;
	return o;
}

float3 GetScreenSpacePos(float2 uv, float depth) {
	return float3(uv.xy * 2 - 1, depth.r);
}

float3 GetWorldSpacePos(float3 screenPos, float4x4 inverseVPMatrix) {
	float4 worldPos = mul(inverseVPMatrix, float4(screenPos, 1));
	return worldPos.xyz / worldPos.w;
}

float3 GetViewSpacePos(float3 screenPos, float4x4 inversePMatrix) {
	float4 worldPos = mul(inversePMatrix, float4(screenPos, 1));
	return worldPos.xyz / worldPos.w;
}

float2 GetMotionVector(half SceneDepth, half2 inUV, half4x4 inverseVPMatrix, half4x4 prevVPMatrix, half4x4 VPMatrix) {
	float3 screenPos = GetScreenSpacePos(inUV, SceneDepth);
	float4 worldPos = half4(GetWorldSpacePos(screenPos, inverseVPMatrix), 1);
	float4 prevClipPos = mul(prevVPMatrix, worldPos);
	float4 curClipPos = mul(VPMatrix, worldPos);
	float2 prevHPos = prevClipPos.xy / prevClipPos.w;
	float2 curHPos = curClipPos.xy / curClipPos.w;
	float2 vPosPrev = (prevHPos.xy + 1) / 2;
	float2 vPosCur = (curHPos.xy + 1) / 2;
	return vPosCur - vPosPrev;
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

inline float2 EncodeFloatRG(float v) {
	float2 kEncodeMul = float2(1.0, 255.0);
	float kEncodeBit = 1.0 / 255.0;
	float2 enc = kEncodeMul * v;
	enc = frac(enc);
	enc.x -= enc.y * kEncodeBit;
	return enc;
}

inline float2 EncodeViewNormalStereo(float3 n) {
	float kScale = 1.7777;
	float2 enc;
	enc = n.xy / (n.z + 1);
	enc /= kScale;
	enc = enc * 0.5 + 0.5;
	return enc;
}

inline float4 EncodeDepthNormal(float depth, float3 normal) {
	float4 enc;
	enc.xy = EncodeViewNormalStereo(normal);
	enc.zw = EncodeFloatRG(depth);
	return enc;
}

inline float3 UnpackDerivativeHeight(float4 textureData) {
	float3 dh = textureData.agb;
	dh.xy = dh.xy * 2 - 1;
	return dh;
}


//Fragment data
SAMPLER(sampler_trilinear_clamp);
SAMPLER(sampler_trilinear_repeat);
SAMPLER(sampler_linear_clamp);
SAMPLER(sampler_linear_repeat);
SAMPLER(sampler_point_clamp);
SAMPLER(sampler_point_repeat);
#include "Fragment.hlsl"

//hi-z depth texture
Texture2D _HierarchicalDepthTexture; SamplerState sampler_HierarchicalDepthTexture;
int _HizMipLevel;

#define SAMPLE_DEPTH_OFFSET(x,y,z,a) (x.Sample(y,z,a).r )
#define SAMPLE_TEXTURE2D_OFFSET(x,y,z,a) (x.Sample(y,z,a))

#if defined(UNITY_REVERSED_Z)
#define COMPARE_DEPTH(a, b) step(b, a)
#else
#define COMPARE_DEPTH(a, b) step(a, b)
#endif

//lod transition function
void ClipLOD(Fragment fragment, float fade) {
#if defined(LOD_FADE_CROSSFADE)
	float dither = InterleavedGradientNoise(fragment.positionSS, 0);
	clip(fade - dither);
#endif
}

inline float CharlieL(float x, float r) {
	r = saturate(r);
	r = 1 - (1 - r) * (1 - r);
	float a = lerp(25.3245, 21.5473, r);
	float b = lerp(3.32435, 3.82987, r);
	float c = lerp(0.16801, 0.19823, r);
	float d = lerp(-1.27393, -1.97760, r);
	float e = lerp(-4.85967, -4.32054, r);
	return a / (1 + b * pow(x, c)) + d * x + e;
}

//GTAO multiply bounce symbolic expression
float3 MultiBounce(float AO, float3 Albedo) {
	float3 A = 2.0404 * Albedo - 0.3324;
	float3 B = -4.7951 * Albedo + 0.6417;
	float3 C = 2.7552 * Albedo + 0.6903;
	return max(AO, ((AO * A + B) * AO + C) * AO);
}

//TAA public params
float2 _Jitter;

// Stereo-related bits
#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)

#define SLICE_ARRAY_INDEX   unity_StereoEyeIndex

#define TEXTURE2D_X(textureName)                                        TEXTURE2D_ARRAY(textureName)
#define TEXTURE2D_X_PARAM(textureName, samplerName)                     TEXTURE2D_ARRAY_PARAM(textureName, samplerName)
#define TEXTURE2D_X_ARGS(textureName, samplerName)                      TEXTURE2D_ARRAY_ARGS(textureName, samplerName)
#define TEXTURE2D_X_HALF(textureName)                                   TEXTURE2D_ARRAY_HALF(textureName)
#define TEXTURE2D_X_FLOAT(textureName)                                  TEXTURE2D_ARRAY_FLOAT(textureName)

#define LOAD_TEXTURE2D_X(textureName, unCoord2)                         LOAD_TEXTURE2D_ARRAY(textureName, unCoord2, SLICE_ARRAY_INDEX)
#define LOAD_TEXTURE2D_X_LOD(textureName, unCoord2, lod)                LOAD_TEXTURE2D_ARRAY_LOD(textureName, unCoord2, SLICE_ARRAY_INDEX, lod)
#define SAMPLE_TEXTURE2D_X(textureName, samplerName, coord2)            SAMPLE_TEXTURE2D_ARRAY(textureName, samplerName, coord2, SLICE_ARRAY_INDEX)
#define SAMPLE_TEXTURE2D_X_LOD(textureName, samplerName, coord2, lod)   SAMPLE_TEXTURE2D_ARRAY_LOD(textureName, samplerName, coord2, SLICE_ARRAY_INDEX, lod)
#define GATHER_TEXTURE2D_X(textureName, samplerName, coord2)            GATHER_TEXTURE2D_ARRAY(textureName, samplerName, coord2, SLICE_ARRAY_INDEX)
#define GATHER_RED_TEXTURE2D_X(textureName, samplerName, coord2)        GATHER_RED_TEXTURE2D(textureName, samplerName, float3(coord2, SLICE_ARRAY_INDEX))
#define GATHER_GREEN_TEXTURE2D_X(textureName, samplerName, coord2)      GATHER_GREEN_TEXTURE2D(textureName, samplerName, float3(coord2, SLICE_ARRAY_INDEX))
#define GATHER_BLUE_TEXTURE2D_X(textureName, samplerName, coord2)       GATHER_BLUE_TEXTURE2D(textureName, samplerName, float3(coord2, SLICE_ARRAY_INDEX))

#else
#define SLICE_ARRAY_INDEX       0

#define TEXTURE2D_X(textureName)                                        TEXTURE2D(textureName)
#define TEXTURE2D_X_PARAM(textureName, samplerName)                     TEXTURE2D_PARAM(textureName, samplerName)
#define TEXTURE2D_X_ARGS(textureName, samplerName)                      TEXTURE2D_ARGS(textureName, samplerName)
#define TEXTURE2D_X_HALF(textureName)                                   TEXTURE2D_HALF(textureName)
#define TEXTURE2D_X_FLOAT(textureName)                                  TEXTURE2D_FLOAT(textureName)

#define LOAD_TEXTURE2D_X(textureName, unCoord2)                         LOAD_TEXTURE2D(textureName, unCoord2)
#define LOAD_TEXTURE2D_X_LOD(textureName, unCoord2, lod)                LOAD_TEXTURE2D_LOD(textureName, unCoord2, lod)
#define SAMPLE_TEXTURE2D_X(textureName, samplerName, coord2)            SAMPLE_TEXTURE2D(textureName, samplerName, coord2)
#define SAMPLE_TEXTURE2D_X_LOD(textureName, samplerName, coord2, lod)   SAMPLE_TEXTURE2D_LOD(textureName, samplerName, coord2, lod)
#define GATHER_TEXTURE2D_X(textureName, samplerName, coord2)            GATHER_TEXTURE2D(textureName, samplerName, coord2)
#define GATHER_RED_TEXTURE2D_X(textureName, samplerName, coord2)        GATHER_RED_TEXTURE2D(textureName, samplerName, coord2)
#define GATHER_GREEN_TEXTURE2D_X(textureName, samplerName, coord2)      GATHER_GREEN_TEXTURE2D(textureName, samplerName, coord2)
#define GATHER_BLUE_TEXTURE2D_X(textureName, samplerName, coord2)       GATHER_BLUE_TEXTURE2D(textureName, samplerName, coord2)
#endif

#endif