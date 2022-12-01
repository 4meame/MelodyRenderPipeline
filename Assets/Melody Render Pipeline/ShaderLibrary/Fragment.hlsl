﻿#ifndef MELODY_FRAGMENT_INCLUDED
#define MELODY_FRAGMENT_INCLUDED

TEXTURE2D(_CameraDepthTexture);
float4 _CameraDepthTexture_TexelSize;
TEXTURE2D(_TransparentDepthTexture);
float4 _TransparentDepthTexture_TexelSize;
TEXTURE2D(_CameraColorTexture);
float4 _CameraColorTexture_TexelSize;
TEXTURE2D(_CameraMotionVectorTexture);
float4 _CameraMotionVectorTexture_TexelSize;
TEXTURE2D(_CameraDepthNormalTexture);
//rgb : diffuse, a : occlusion
TEXTURE2D(_CameraDiffuseTexture);
//rgb : specular, a : smoothness
TEXTURE2D(_CameraSpecularTexture);
TEXTURE2D(_CameraReflectionsTexture);

//x : 1.0 / width(scaled) 
//y : 1.0 / height(scaled)
//z : width(scaled)
//w : height(scaled)
float4 _CameraBufferSize;

struct Fragment {
	//screen/window space position of the fragment, for example, it is (0.5, 0.5) for the texel in the bottom left corner of the screen
	float2 positionSS;
	float2 screenUV;
	float depth;
	float bufferDepth;
	float3 bufferNormal;
};

Fragment GetFragment(float4 positionCS) {
	Fragment f;
	f.positionSS = positionCS.xy;
	//screen params X Y is the width & height of screen
	//f.screenUV = f.positionSS / _ScreenParams.xy;
	f.screenUV = f.positionSS * _CameraBufferSize.xy;
	f.depth = IsOrthographicCamera() ? OrthographicDepthBufferToLinear(positionCS.z) : positionCS.w;
	//SAMPLE_DEPTH_TEXTURE_LOD return R channel
	f.bufferDepth = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, sampler_point_clamp, f.screenUV, 0);
	f.bufferDepth = IsOrthographicCamera() ? OrthographicDepthBufferToLinear(f.bufferDepth) : LinearEyeDepth(f.bufferDepth, _ZBufferParams);
	float4 depthNormal = SAMPLE_TEXTURE2D_LOD(_CameraDepthNormalTexture, sampler_point_clamp, f.screenUV, 0);
	f.bufferNormal = DecodeViewNormalStereo(depthNormal);
	f.bufferNormal = f.bufferNormal * 0.5 + 0.5;
#if defined(_USE_DEPTHNORMAL)
	f.bufferDepth = DecodeFloatRG(depthNormal.zw);
#endif
	return f;
}

float4 GetBufferColor(Fragment fragment, float2 uvOffset = float2(0.0, 0.0)) {
	float2 uv = fragment.screenUV + uvOffset;
	return SAMPLE_TEXTURE2D_LOD(_CameraColorTexture, sampler_linear_clamp, uv, 0);
}

#endif
