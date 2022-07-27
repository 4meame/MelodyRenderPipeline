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
                //NOTE : the larger radius of planet is , the further far plane of camera should be
                float3 rayDirection = normalize(TransformObjectToWorld(input.positionOS));
                float3 lightDirection = _MainLightPosition.xyz;
                //center could be any coordiante in the situation of universe system 
                float3 planetCenter = float3(0, -_PlanetRadius, 0);
                //calculate ray and atmosphere intersect
                float2 intersection = RaySphereIntersection(rayStart, rayDirection, planetCenter, _PlanetRadius + _AtmosphereHeight);
                float3 color = 0;
                if (intersection.y > 0) {
                    color = 1;
                }
                //return float4(color, 1);
                float rayLength = intersection.y;
                //ray should be end at the first intersect point if hit the planet
                intersection = RaySphereIntersection(rayStart, rayDirection, planetCenter, _PlanetRadius);
                if (intersection.x >= 0) {
                    rayLength = min(rayLength, intersection.x);
                }
                float4 extinction;
                float4 inscattering = IntergrateInscattering(rayStart, rayDirection, rayLength, planetCenter, 1, lightDirection, 64, extinction);
                return float4(inscattering.rgb, 1);
            }
            ENDHLSL
        }
    }
}
