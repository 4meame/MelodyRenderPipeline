Shader "Hidden/Melody RP/AtmosphereScattering" {
	SubShader
	{
		HLSLINCLUDE
		#include "AtmosphereScatteringPass.hlsl"
		ENDHLSL

		Pass
		{
			Name "Precompute Particle Density"

			Cull Off
			ZTest Off
			Zwrite Off
			Blend Off

			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex DefaultPassVertex
			#pragma fragment particleDensityLUT

			struct Varyings {
			float4 positionCS : SV_POSITION;
			float2 uv : VAR_UV;
			};

			//vertexID is the clockwise index of a triangle : 0,1,2
			Varyings DefaultPassVertex(uint vertexID : SV_VertexID) {
				Varyings output;
				//make the [-1, 1] NDC, visible UV coordinates cover the 0-1 range
				output.positionCS = float4(vertexID <= 1 ? -1.0 : 3.0,
					vertexID == 1 ? 3.0 : -1.0,
					0.0, 1.0);
				output.uv = float2(vertexID <= 1 ? 0.0 : 2.0,
					vertexID == 1 ? 2.0 : 0.0);
				//some graphics APIs have the texture V coordinate start at the top while others have it start at the bottom
				if (_ProjectionParams.x < 0.0) {
					output.uv.y = 1.0 - output.uv.y;
				}
				return output;
			}

			float2 particleDensityLUT(Varyings input) : SV_TARGET {
				float cosAngle = input.uv.x * 2.0 - 1.0;
				float sinAngle = sqrt(saturate(1 - cosAngle * cosAngle));
				float startHeight = lerp(0.0, _AtmosphereHeight, input.uv.y);
				float3 rayStart = float3(0, startHeight, 0);
				float3 rayDir = float3(sinAngle, cosAngle, 0);
				return PrecomputeParticleDensity(rayStart, rayDir);
			}
			
			ENDHLSL
		}

		Pass
		{
			Name "Precompute Sun Color"

			Cull Off
			ZTest Off
			Zwrite Off
			Blend Off

			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex DefaultPassVertex
			#pragma fragment SunColorLut

			struct Varyings {
			float4 positionCS : SV_POSITION;
			float2 uv : VAR_UV;
			};

			//vertexID is the clockwise index of a triangle : 0,1,2
			Varyings DefaultPassVertex(uint vertexID : SV_VertexID) {
				Varyings output;
				//make the [-1, 1] NDC, visible UV coordinates cover the 0-1 range
				output.positionCS = float4(vertexID <= 1 ? -1.0 : 3.0,
					vertexID == 1 ? 3.0 : -1.0,
					0.0, 1.0);
				output.uv = float2(vertexID <= 1 ? 0.0 : 2.0,
					vertexID == 1 ? 2.0 : 0.0);
				//some graphics APIs have the texture V coordinate start at the top while others have it start at the bottom
				if (_ProjectionParams.x < 0.0) {
					output.uv.y = 1.0 - output.uv.y;
				}
				return output;
			}

			float4 SunColorLut(Varyings input) : SV_TARGET{
				float cosAngle = input.uv.x * 2.0 - 1.0;
				float sinAngle = sqrt(saturate(1 - cosAngle * cosAngle));
				float startHeight = lerp(0.0, _AtmosphereHeight, input.uv.y);
				float3 rayStart = float3(0, startHeight, 0);
				float3 rayDir = float3(sinAngle, cosAngle, 0);
				return PrecomputeSunColor(rayStart, rayDir);
			}

			ENDHLSL
		}

		Pass
		{
			Name "Precompute Ambient"

			Cull Off
			ZTest Off
			Zwrite Off
			Blend Off

			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex DefaultPassVertex
			#pragma fragment AmbientLut

			struct Varyings {
			float4 positionCS : SV_POSITION;
			float2 uv : VAR_UV;
			};

			//vertexID is the clockwise index of a triangle : 0,1,2
			Varyings DefaultPassVertex(uint vertexID : SV_VertexID) {
				Varyings output;
				//make the [-1, 1] NDC, visible UV coordinates cover the 0-1 range
				output.positionCS = float4(vertexID <= 1 ? -1.0 : 3.0,
					vertexID == 1 ? 3.0 : -1.0,
					0.0, 1.0);
				output.uv = float2(vertexID <= 1 ? 0.0 : 2.0,
					vertexID == 1 ? 2.0 : 0.0);
				//some graphics APIs have the texture V coordinate start at the top while others have it start at the bottom
				if (_ProjectionParams.x < 0.0) {
					output.uv.y = 1.0 - output.uv.y;
				}
				return output;
			}

			float4 AmbientLut(Varyings input) : SV_TARGET{
				float cosAngle = input.uv.x * 2.0 - 1.0;
				float sinAngle = sqrt(saturate(1 - cosAngle * cosAngle));
				float3 lightDir = float3(sinAngle, cosAngle, 0);
				return PrecomputeAmbient(lightDir);
			}

			ENDHLSL
		}
	}
}