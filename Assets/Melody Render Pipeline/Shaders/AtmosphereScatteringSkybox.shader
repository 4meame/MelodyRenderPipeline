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
                float3 rayDirection = normalize(TransformObjectToWorld(input.positionOS));
                float3 lightDirection = _MainLightPosition.xyz;
                //camera position is the center of the world in the skybox
                float3 planetCenter = _WorldSpaceCameraPos.xyz;
                planetCenter = float3(0, -_PlanetRadius, 0);
                //calculate ray and atmosphere intersect
                float2 intersection = RaySphereIntersection(rayStart, rayDirection, planetCenter, _PlanetRadius + _AtmosphereHeight);
                float rayLength = intersection.y;
                //ray should be end at the first intersect point if hit the planet
                intersection = RaySphereIntersection(rayStart, rayDirection, planetCenter, _PlanetRadius);
                if (intersection.x > 0) {
                    rayLength = min(rayLength, intersection.x);
                }
                float4 extinction;
                float4 inscattering = IntergrateInscattering();
                return 1;
            }
            ENDHLSL
        }
    }
}
