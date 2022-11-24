// Crest Ocean System

// Copyright 2020 Wave Harmonic Ltd

Shader "Crest/Underwater Meniscus"
{
	Properties
	{
		_MeniscusWidth("Meniscus Width", Range(0.0, 100.0)) = 1.0
	}

	SubShader
	{
		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-99" }

		Pass
		{
			// Could turn this off, and it would allow the ocean surface to render through it
			ZWrite Off
			//Blend SrcAlpha OneMinusSrcAlpha
			Blend DstColor Zero

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#include "../../../ShaderLibrary/Common.hlsl"

			#include "../OceanGlobals.hlsl"
			#include "../OceanInputsDriven.hlsl"
			#include "../OceanHelpersNew.hlsl"
			#include "UnderwaterShared.hlsl"

			// @Hack: Work around to unity_CameraToWorld._13_23_33 not being set correctly in URP 7.4+
			float3 _CameraForward;

			#define MAX_OFFSET 5.0

			CBUFFER_START(UnderwaterAdditional)
			float _MeniscusWidth;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				half4 foam_screenPos : TEXCOORD1;
				half4 grabPos : TEXCOORD2;
				float3 worldPos : TEXCOORD3;

				UNITY_VERTEX_OUTPUT_STEREO
			};

			Varyings Vert(Attributes input)
			{
				Varyings o;

				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				// view coordinate frame for camera
				const float3 right = unity_CameraToWorld._11_21_31;
				const float3 up = unity_CameraToWorld._12_22_32;
				// @Hack: Work around to unity_CameraToWorld._13_23_33 not being set correctly in URP 7.4+
				const float3 forward = _CameraForward;

				const float3 nearPlaneCenter = _WorldSpaceCameraPos + forward * _ProjectionParams.y * 1.001;
				// Spread verts across the near plane.
				const float aspect = _ScreenParams.x / _ScreenParams.y;
				o.worldPos = nearPlaneCenter
					+ 2.6 * unity_CameraInvProjection._m11 * aspect * right * input.positionOS.x * _ProjectionParams.y
					+ up * input.positionOS.z * _ProjectionParams.y;

				if (abs(forward.y) < CREST_MAX_UPDOWN_AMOUNT)
				{
					o.worldPos += min(IntersectRayWithWaterSurface(o.worldPos, up, _CrestCascadeData[_LD_SliceIndex]), MAX_OFFSET) * up;

					const float offset = 0.001 * _ProjectionParams.y * _MeniscusWidth;
					if (input.positionOS.z > 0.49)
					{
						o.worldPos += offset * up;
					}
					else
					{
						o.worldPos -= offset * up;
					}
				}
				else
				{
					// kill completely if looking up/down
					o.worldPos *= 0.0;
				}

				o.positionCS = mul(UNITY_MATRIX_VP, float4(o.worldPos, 1.0));
				o.positionCS.z = o.positionCS.w;

				o.foam_screenPos.yzw = ComputeScreenPos(o.positionCS).xyw;
				o.grabPos = ComputeScreenPos(o.positionCS);
				o.foam_screenPos.x = 0.0;

				o.uv = input.uv;

				return o;
			}

			half4 Frag(Varyings input) : SV_Target
			{
				const half3 col = 1.3*half3(0.37, 0.4, 0.5);
				float alpha = abs(input.uv.y - 0.5);
				alpha = pow(smoothstep(0.5, 0.0, alpha), 0.5);
				return half4(lerp((half3)1.0, col, alpha), alpha);
			}
			ENDHLSL
		}
	}
}
