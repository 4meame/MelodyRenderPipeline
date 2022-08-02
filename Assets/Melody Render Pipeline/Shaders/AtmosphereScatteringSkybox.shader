Shader "Melody RP/Skybox/AtmosphereScattering"
{
    Properties
    {

    }
    SubShader
    {
        Pass
        {
            Tags {  "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma shader_feature _ATMOSPHERE_PRECOMPUTE
            #pragma vertex vert
            #pragma fragment frag
            #include "AtmosphereScatteringPass.hlsl"

            struct Attributes {
                float3 positionOS : POSITION;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 positionOS : VAR_OBJECT_SPACE;
            };

            Varyings vert(Attributes input) {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.positionOS = input.positionOS;
                return output;
            }

            float4 frag(Varyings input) : SV_TARGET{
                float3 rayStart = _WorldSpaceCameraPos.xyz;
                //NOTE : the larger radius of planet is , the further far plane of camera should be
                float3 rayDirection = normalize(TransformObjectToWorld(input.positionOS));
                float3 lightDirection = _MainLightPosition.xyz;
                //center could be any coordiante in the situation of universe system 
                float3 planetCenter = float3(0, -_PlanetRadius, 0);
#if defined(_ATMOSPHERE_PRECOMPUTE)
                float4 scatterR = 0;
                float4 scatterM = 0;
                float height = length(rayStart - planetCenter) - _PlanetRadius;
                float3 normal = normalize(rayStart - planetCenter);
                float viewZenith = dot(normal, rayDirection);
                float sunZenith = dot(normal, -lightDirection);
                float3 coords = float3(height / _AtmosphereHeight, viewZenith * 0.5 + 0.5, sunZenith * 0.5 + 0.5);
                coords.x = pow(height / _AtmosphereHeight, 0.5);
                float ch = -(sqrt(height * (2 * _PlanetRadius + height)) / (_PlanetRadius + height));
                if (viewZenith > ch) {
                    coords.y = 0.5 * pow((viewZenith - ch) / (1 - ch), 0.2) + 0.5;
                } else {
                    coords.y = 0.5 * pow((ch - viewZenith) / (ch + 1), 0.2);
                }
                coords.z = 0.5 * ((atan(max(sunZenith, -0.1975) * tan(1.26 * 1.1)) / 1.1) + (1 - 0.26));
                scatterR = SAMPLE_TEXTURE3D_LOD(_ScatterRaylieLUT, sampler_ScatterRaylieLUT, float4(coords, 0), 0);
                scatterM = SAMPLE_TEXTURE3D_LOD(_ScatterMieLUT, sampler_ScatterMieLUT, float4(coords, 0), 0);
                float3 m = scatterM.xyz;
                ApplyPhaseFunctionElek(scatterR.xyz, scatterM.xyz, dot(rayDirection, lightDirection.xyz));
                float3 inscattering = (scatterR.xyz * _ScatteringR + scatterM.xyz * _ScatteringM) * _IncomingLight.xyz;
                inscattering += RenderSun(m, dot(rayDirection, lightDirection.xyz)) * _SunIntensity;
                inscattering = max(0, inscattering);
                return float4(inscattering, 1);
#else
                //calculate ray and atmosphere intersect
                float2 intersection = RaySphereIntersection(rayStart, rayDirection, planetCenter, _PlanetRadius + _AtmosphereHeight);
                float rayLength = intersection.y;
                //ray should be end at the first intersect point if hit the planet
                intersection = RaySphereIntersection(rayStart, rayDirection, planetCenter, _PlanetRadius);
                if (intersection.x >= 0) {
                    rayLength = min(rayLength, intersection.x);
                }
                float lightSamples = _LightSamples;
                float4 extinction;
                float4 inscattering = IntergrateInscattering(rayStart, rayDirection, rayLength, planetCenter, 1, lightDirection, lightSamples, extinction);
                inscattering = max(0, inscattering);
                return float4(inscattering.rgb, 1);
#endif
            }
            ENDHLSL
        }
    }
}
