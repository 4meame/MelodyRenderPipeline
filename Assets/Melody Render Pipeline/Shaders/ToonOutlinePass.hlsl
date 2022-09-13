#ifndef MELODY_TOON_OUTLINE_PASS_INCLUDED
#define MELODY_TOON_OUTLINE_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

#include "ToonMap.hlsl"
#include "ToonUtilities.hlsl"

#if defined(_SIMPLE_TOON)

#endif

#if defined(_COMPLEX_TOON)

float4 _LightColor0;
float4 _BaseColor;
float _Unlit_Intensity;
float _Is_Filter_LightColor;
float _Is_LightColor_Outline;

float4 _Color;
TEXTURE2D(_MainMap);
SAMPLER(sampler_MainMap);
float4 _MainMap_ST;
float _Outline_Width;
float _Farthest_Distance;
float _Nearest_Distance;
TEXTURE2D(_Outline_Sampler);
SAMPLER(sampler_Outline_Sampler);
float4 _Outline_Sampler_ST;
float4 _Outline_Color;
float _Is_BlendBaseColor;
float _Offset_Z;

TEXTURE2D(_OutlineTex);
SAMPLER(sampler_OutlineTex);
float4 _OutlineTex_ST;
float _Is_OutlineTex;
//Baked Normal Texture for Outline
TEXTURE2D(_BakedNormal);
SAMPLER(sampler_BakedNormal);
float4 _BakedNormal_ST;
float _Is_BakedNormal;

TEXTURE2D(_ClippingMask);
SAMPLER(sampler_ClippingMask);
float4 _ClippingMask_ST;
float _Clipping_Level;
float _Inverse_Clipping;
float _IsBaseMapAlphaAsClippingMask;

struct VertexInput {
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
	float3 normalOS : NORMAL;
	float4 tangentOS : TANGENT;
	//Support per-instance material data
	UNITY_VERTEX_INPUT_INSTANCE_ID
	//UV Coordinate for the light map
	GI_ATTRIBUTE_DATA
};

struct VertexOutput {
	float4 positionCS : SV_POSITION;
	//any unused identifier can be used here
	float2 baseUV : VAR_BASE_UV;
	float3 normalWS : VAR_NORMAL;
	float3 tangentWS : VAR_TANGENT;
	float3 bitangentWS : VAR_BITANGENT;
	//Support per-instance material data
	UNITY_VERTEX_INPUT_INSTANCE_ID
	//UV Coordinate for the light map
	GI_VARYINGS_DATA
};

VertexOutput vert(VertexInput i) {
	VertexOutput o = (VertexOutput)0;
	o.baseUV = i.baseUV;
	float4 objPos = mul(UNITY_MATRIX_M, float4(0, 0, 0, 1));
	float4 _Outline_Sampler_var = SAMPLE_TEXTURE2D_LOD(_Outline_Sampler, sampler_Outline_Sampler, TRANSFORM_TEX(o.baseUV, _Outline_Sampler), 0);
	o.normalWS = TransformObjectToWorldNormal(i.normalOS);
	o.tangentWS = TransformObjectToWorldDir(i.tangentOS.xyz);
	o.bitangentWS= normalize(cross(o.normalWS, o.tangentWS) * i.tangentOS.w);
	float3x3 tangentTransform = float3x3(o.tangentWS, o.bitangentWS, o.normalWS);
	float4 _BakedNormal_var = SAMPLE_TEXTURE2D_LOD(_BakedNormal, sampler_BakedNormal, TRANSFORM_TEX(o.baseUV, _BakedNormal) * 2 - 1, 0);
	float3 _BakedNormalDir = normalize(mul(_BakedNormal_var.rgb, tangentTransform));
	float Set_Outline_Width = (_Outline_Width * 0.001 * smoothstep(_Farthest_Distance, _Nearest_Distance, distance(objPos.rgb, _WorldSpaceCameraPos)) * _Outline_Sampler_var.rgb).r;
	float4 _ClipCameraPos = mul(UNITY_MATRIX_VP, float4(_WorldSpaceCameraPos.xyz, 1));
#if defined(UNITY_REVERSED_Z)
	//vDX
	_Offset_Z = _Offset_Z * -0.01;
#else
	//OpenGL
	_Offset_Z = _Offset_Z * 0.01;
#endif
#ifdef _OUTLINE_NML
	o.positionCS = TransformObjectToHClip(lerp(float4(i.positionOS.xyz + i.normalOS * Set_Outline_Width, 1), float4(i.positionOS.xyz + _BakedNormalDir * Set_Outline_Width, 1), _Is_BakedNormal));
#elif _OUTLINE_POS
	Set_Outline_Width = Set_Outline_Width * 2;
	float signVar = dot(normalize(i.positionOS), normalize(i.normalOS)) < 0 ? -1 : 1;
	o.positionCS = TransformObjectToHClip(float4(i.positionOS.xyz + signVar * normalize(i.positionOS) * Set_Outline_Width, 1));
#endif
	o.positionCS.z = o.positionCS.z + _Offset_Z * _ClipCameraPos.z;
	return o;
}

float4 frag(VertexOutput i) : SV_Target {
    float3 defaultLightColor = saturate(max(float3(0.05, 0.05, 0.05) * _Unlit_Intensity, max(ShadeSH9(float3(0.0, 0.0, 0.0)), ShadeSH9(float3(0.0, -1.0, 0.0))) * _Unlit_Intensity));
	Light light = GetMainLight();
	float3 lightColor = max(defaultLightColor, light.color.rgb);
	float lightColorIntensity = (0.299 * lightColor.r + 0.587 * lightColor.g + 0.114 * lightColor.b);
	lightColor = lightColorIntensity < 1 ? lightColor : lightColor / lightColorIntensity;
	lightColor = lerp(float3(1.0,1.0,1.0), lightColor, _Is_LightColor_Outline);
	float4 _MainMap_var = SAMPLE_TEXTURE2D(_MainMap, sampler_MainMap, TRANSFORM_TEX(i.baseUV, _MainMap));
	float3 Set_BaseColor = _BaseColor.rgb * _MainMap_var.rgb;
	float3 _Is_BlendBaseColor_var = lerp(_Outline_Color.rgb * lightColor, (_Outline_Color.rgb * Set_BaseColor * Set_BaseColor * lightColor), _Is_BlendBaseColor);
	float3 _OutlineTex_var = SAMPLE_TEXTURE2D(_OutlineTex, sampler_OutlineTex, TRANSFORM_TEX(i.baseUV, _OutlineTex));
#if defined(_CLIPPING_OFF)
	float3 Set_Outline_Color = lerp(_Is_BlendBaseColor_var, _OutlineTex_var.rgb * _Outline_Color.rgb * lightColor, _Is_OutlineTex);
	return float4(Set_Outline_Color,1.0);
#elif defined(_CLIPPING_ON)
	float4 _ClippingMask_var = SAMPLE_TEXTURE2D(_ClippingMask, sampler_ClippingMask, TRANSFORM_TEX(i.baseUV, _ClippingMask));
	float Set_Clipping = saturate((lerp(_ClippingMask_var.a, (1.0 - _ClippingMask_var.a), _Inverse_Clipping) + lerp(_Clipping_Level, -_Clipping_Level, _Inverse_Clipping)));
	clip(Set_Clipping - 0.5);
	float4 Set_Outline_Color = lerp(float4(_Is_BlendBaseColor_var,Set_Clipping), float4((_OutlineTex_var.rgb * _Outline_Color.rgb * lightColor),Set_Clipping), _Is_OutlineTex);
	return Set_Outline_Color;
#endif
}

#endif

#endif
