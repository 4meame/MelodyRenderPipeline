// Crest Ocean System

// Copyright 2020 Wave Harmonic Ltd

Shader "Hidden/Crest/Simulation/Update Shadow"
{
	Properties
	{
		[Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows("Receive Shadows", Float) = 1
	}

	SubShader
	{
		Pass
		{

			Name "Update Ocean Shadow"

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			// #pragma enable_d3d11_debug_symbols

			#pragma multi_compile _ _RECEIVE_SHADOWS
			#pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
			#pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7
			#pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
			#pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE

			#include "../../../ShaderLibrary/Common.hlsl"
			#include "../../../ShaderLibrary/Surface.hlsl"
			#include "../../../ShaderLibrary/Shadows.hlsl"
			#include "../../../ShaderLibrary/Light.hlsl"

			#define CREST_SAMPLE_SHADOW_HARD

			#include "../ShaderLibrary/UpdateShadow.hlsl"

			half CrestSampleShadows(const float4 i_positionWS)
			{
				Surface surfaceData;
				//init surface data to rely on pipeline bilut-in method for now
				surfaceData.position = i_positionWS.xyz;
				surfaceData.normal = float3(0, 1, 0);
				surfaceData.interpolatedNormal = float3(0, 1, 0);
				surfaceData.viewDirection = 0;
				surfaceData.depth = -TransformWorldToView(i_positionWS.xyz).z;
				surfaceData.color = 0;
				surfaceData.alpha = 0;
				surfaceData.metallic = 0;
				surfaceData.occlusion = 0;
				surfaceData.smoothness = 0;
				surfaceData.dither = 0;
				surfaceData.fresnelStrength = 0;
				ShadowData shadowData;
				shadowData = GetShadowData(surfaceData);
				Light mainLight = GetMainLight(surfaceData, shadowData);
				return mainLight.shadowAttenuation;
			}

			Varyings Vert(Attributes input)
			{
				Varyings o;

				o.positionCS = TransformObjectToHClip(input.positionOS);

				// World position from [0,1] quad.
				o.positionWS.xyz = float3(input.positionOS.x - 0.5, 0.0, input.positionOS.y - 0.5) * _Scale * 4.0 + _CenterPos;
				o.positionWS.y = _OceanCenterPosWorld.y;

				return o;
			}

			ENDHLSL
		}
	}
}
