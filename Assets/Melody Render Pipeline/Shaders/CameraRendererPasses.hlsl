#ifndef MELODY_CAMERA_RENDERER_PASSES_INCLUDED
#define MELODY_CAMERA_RENDERER_PASSES_INCLUDED

#include "../ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

TEXTURE2D(_SourceTexture);
TEXTURE2D(_SSR_Filtered);
TEXTURE2D(_SSAO_Filtered);

struct Varyings {
	float4 positionCS : SV_POSITION;
	float2 screenUV : VAR_SCREEN_UV;
};

//vertexID is the clockwise index of a triangle : 0,1,2
Varyings DefaultPassVertex(uint vertexID : SV_VertexID) {
	Varyings output;
//make the [-1, 1] NDC, visible UV coordinates cover the 0-1 range
	output.positionCS = float4(vertexID <= 1.0 ? -1.0 : 3.0,
							vertexID == 1.0 ? 3.0 : -1.0,
		0.0, 1.0);
	output.screenUV = float2(vertexID <= 1 ? 0.0 : 2.0,
							 vertexID == 1 ? 2.0 : 0.0);
//some graphics APIs have the texture V coordinate start at the top while others have it start at the bottom
	if (_ProjectionParams.x < 0.0) {
		output.screenUV.y = 1.0 - output.screenUV.y;
	}
	return output;
}


float4 CopyPassFragment(Varyings input) : SV_TARGET{
	return SAMPLE_TEXTURE2D_LOD(_SourceTexture, sampler_linear_clamp, input.screenUV, 0);
}

float CopyDepthPassFragment(Varyings input) : SV_DEPTH{
	return SAMPLE_DEPTH_TEXTURE_LOD(_SourceTexture, sampler_point_clamp, input.screenUV, 0);
}

float4 CombineSSRPassFragment(Varyings input) : SV_TARGET{
	float4 source = SAMPLE_TEXTURE2D_LOD(_CameraColorTexture, sampler_linear_clamp, input.screenUV, 0);
	float4 ssrResult = SAMPLE_TEXTURE2D(_SSR_Filtered, sampler_linear_clamp, input.screenUV);
	float roughness = SAMPLE_TEXTURE2D(_CameraSpecularTexture, sampler_linear_clamp, input.screenUV).a;
	//reflectAmount = reflectAmount.r * 0.299 + reflectAmount.g * 0.587 + reflectAmount.b * 0.144;
	source.rgb = lerp(ssrResult.rgb, source.rgb, ssrResult.a * roughness);
	return source;
}

float4 CombineSSAOPassFragment(Varyings input) : SV_TARGET{
	float4 source = SAMPLE_TEXTURE2D_LOD(_CameraColorTexture, sampler_linear_clamp, input.screenUV, 0);
	float4 diffuse = SAMPLE_TEXTURE2D_LOD(_CameraDiffuseTexture, sampler_linear_clamp, input.screenUV, 0);
	float3 ssaoResult = SAMPLE_TEXTURE2D(_SSAO_Filtered, sampler_linear_clamp, input.screenUV).rrr;
#if defined(_Multiple_Bounce_AO)
	float3 bounceSource = diffuse.rgb;
	ssaoResult = MultiBounce(ssaoResult.r, bounceSource);
#endif
	source.rgb *= ssaoResult;
	return source;
}
#endif
