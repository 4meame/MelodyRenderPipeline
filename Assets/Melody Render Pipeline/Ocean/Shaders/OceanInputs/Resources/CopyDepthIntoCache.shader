// Crest Ocean System

// Copyright 2020 Wave Harmonic Ltd

Shader "Crest/Copy Depth Buffer Into Cache"
{
	SubShader
	{
		Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

		Pass
		{
			Name "CopyDepthBufferIntoCache"
			ZTest Always ZWrite Off Blend Off

			HLSLPROGRAM
			// Required to compile gles 2.0 with standard srp library
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma vertex vert
			#pragma fragment frag

			#include "../../../../ShaderLibrary/Common.hlsl"

			#include "../../OceanGlobals.hlsl"

			CBUFFER_START(CrestPerOceanInput)
			float4 _HeightNearHeightFar;
			float4 _CustomZBufferParams;
			CBUFFER_END

			sampler2D _CamDepthBuffer;

			struct Attributes
			{
				float3 positionOS   : POSITION;
				float2 uv           : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionCS   : SV_POSITION;
				float2 uv           : TEXCOORD0;
			};

			Varyings vert(Attributes input)
			{
				Varyings output;
				output.uv = input.uv;
				output.positionCS = TransformObjectToHClip(input.positionOS);
				return output;
			}

			float CustomLinear01Depth(float z)
			{
				z *= _CustomZBufferParams.x;
				return (1.0 - z) / _CustomZBufferParams.y;
			}

			float4 frag(Varyings input) : SV_Target
			{
				float deviceDepth = tex2D(_CamDepthBuffer, input.uv).x;
				float linear01Z = CustomLinear01Depth(deviceDepth);

				float altitude;
#if UNITY_REVERSED_Z
				altitude = lerp(_HeightNearHeightFar.y, _HeightNearHeightFar.x, linear01Z);
#else
				altitude = lerp(_HeightNearHeightFar.x, _HeightNearHeightFar.y, linear01Z);
#endif

				return float4(altitude, 0.0, 0.0, 1.0);
			}

			ENDHLSL
		}
	}
}
