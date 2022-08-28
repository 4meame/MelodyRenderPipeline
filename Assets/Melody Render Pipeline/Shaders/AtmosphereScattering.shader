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

		Pass
		{
			Name "Aerial Perspective"

			Cull Off
			ZTest Always
			Zwrite Off
			Blend One Zero

			HLSLPROGRAM
			#pragma target 3.5
			#pragma shader_feature _ATMOSPHERE_PRECOMPUTE
			#pragma shader_feature _ _DEBUG_EXTINCTION _DEBUG_INSCATTERING
			#pragma vertex DefaultPassVertex
			#pragma fragment AerialPerspectiveFog

			struct Varyings {
			float4 positionCS : SV_POSITION;
			float2 uv : VAR_UV;
			};

			float4x4 _InvProj;
			float4x4 _InvRot;
			TEXTURE2D(_BackGroundTexture);

			//screen uv can present camera direction
			float3 UVToCameraRay(float2 uv) {
				float4 cameraRay = float4(uv * 2.0 - 1.0, 1.0, 1.0);
				cameraRay = mul(_InvProj, cameraRay);
				cameraRay = cameraRay / cameraRay.w;
				return mul((float3x3)_InvRot, cameraRay.xyz);
			}

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

			float4 AerialPerspectiveFog(Varyings input) : SV_TARGET{
				float2 screenUV = input.uv;
				float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_point_clamp, screenUV);
				float linearDepth01 = Linear01Depth(depth, _ZBufferParams);
				float3 rayStart = _WorldSpaceCameraPos;
				float3 rayDirection = UVToCameraRay(screenUV);
				//get correct ray length
				rayDirection *= linearDepth01;
				float rayLength = length(rayDirection);
				rayDirection /= rayLength;

				float3 planetCenter = float3(0, -_PlanetRadius, 0);
				float2 intersection = RaySphereIntersection(rayStart, rayDirection, planetCenter, _PlanetRadius + _AtmosphereHeight);
				if (linearDepth01 > 0.99999) {
					rayLength = 1e20;
				}
				rayLength = min(intersection.y, rayLength);
				//ray should be end at the first intersect point if hit the planet
				intersection = RaySphereIntersection(rayStart, rayDirection, planetCenter, _PlanetRadius);
				if (intersection.x >= 0) {
					rayLength = min(rayLength, intersection.x);
				}
				float lightSamples = 16;
				float3 lightDirection = _MainLightPosition.xyz;
#if defined(_ATMOSPHERE_PRECOMPUTE)
				float4 extinction = SAMPLE_TEXTURE3D_LOD(_ExtinctionLUT, sampler_ExtinctionLUT, float4(screenUV.x, screenUV.y, linearDepth01, 0), 0);
				float4 inscattering = SAMPLE_TEXTURE3D_LOD(_InscatteringLUT, sampler_InscatteringLUT, float4(screenUV.x, screenUV.y, linearDepth01, 0), 0);
#else
				float4 extinction;
				float4 inscattering = IntergrateInscattering(rayStart, rayDirection, rayLength, planetCenter, _DistanceScale, lightDirection, lightSamples, extinction);
#endif
				float4 background = SAMPLE_TEXTURE2D(_BackGroundTexture, sampler_linear_clamp, screenUV);
				if (linearDepth01 > 0.99999) {
					inscattering = 0;
					extinction = 1;
				}
				float4 color = background * extinction + inscattering;
#if defined(_DEBUG_EXTINCTION)
				return float4(extinction.rgb, 1);
#elif defined(_DEBUG_INSCATTERING)
				return float4(inscattering.rgb, 1);
#endif
				return float4(color.rgb, 1);
			}

			ENDHLSL
		}
	}
}