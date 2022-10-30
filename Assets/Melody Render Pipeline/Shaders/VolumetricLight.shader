Shader "Hidden/Melody RP/VolumetricLight"
{
    SubShader
    {
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "../ShaderLibrary/Surface.hlsl"
        #include "../ShaderLibrary/Shadows.hlsl"
        #include "../ShaderLibrary/Light.hlsl"
        #include "VolumetricLightPass.hlsl"
        ENDHLSL

        //pass 0
        Pass
        {
            Name "Inside Point Light"
            ZTest Off
            Cull Front
            ZWrite Off
            Blend One One
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile _ _DIRECTION _SPOT _POINT
            #pragma multi_compile _ _RECEIVE_SHADOWS
            #pragma vertex DefaultPassVertex
            #pragma fragment fragPointInside
            ENDHLSL
        }

        //pass 1
        Pass
        {
            Name "Outside Point Light"
            ZTest Always
            Cull Back
            ZWrite Off
            Blend One One
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile _ _DIRECTION _SPOT _POINT
            #pragma multi_compile _ _RECEIVE_SHADOWS
            #pragma vertex DefaultPassVertex
            #pragma fragment fragPointOutside
            ENDHLSL
        }

        //pass 2
        Pass
        {
            Name "Inside Spot Light"
            ZTest Off
            Cull Front
            ZWrite Off
            Blend One One
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile _ _DIRECTION _SPOT _POINT
            #pragma multi_compile _ _RECEIVE_SHADOWS
            #pragma vertex DefaultPassVertex
            #pragma fragment fragSpotInside
            ENDHLSL
        }

        //pass 3
        Pass
        {
            Name "Outside Spot Light"
            ZTest Always
            Cull Off
            ZWrite Off
            Blend One One
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile _ _DIRECTION _SPOT _POINT
            #pragma multi_compile _ _RECEIVE_SHADOWS
            #pragma vertex DefaultPassVertex
            #pragma fragment fragSpotOutside
            ENDHLSL
        }

        //pass 4
        Pass
        {
            Name "Directional Light"
            ZTest Off
            Cull Off
            ZWrite Off
            Blend One One, One Zero
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile _ _DIRECTION _SPOT _POINT
            #pragma multi_compile _ _RECEIVE_SHADOWS
            #pragma vertex vertDir
            #pragma fragment fragDir

            float4 _FrustumCorners[4];

            struct appData {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint vertexID : SV_VertexID;
            };

            struct vertData {
                float4 positionCS : SV_POSITION;
                float2 uv : VAR_UV;
                float3 positionWS : VAR_POSITION_WS;
            };

            vertData vertDir(appData input) {
                vertData output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                //SV_VertexId doesn't work on OpenGL for some reason -> reconstruct id from uv
                output.positionWS = _FrustumCorners[input.uv.x + input.uv.y * 2];
                return output;
            }
            
            float4 fragDir(vertData input) : SV_TARGET{
                float2 uv = input.uv;
                //read depth and reconstruct world position
                float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_point_clamp, uv);
                float linearDepth = LinearEyeDepth(depth, _ZBufferParams);
                float3 rayStart = _WorldSpaceCameraPos;
                float3 rayDir = input.positionWS - _WorldSpaceCameraPos;
                rayDir *= linearDepth;
                float rayLength = length(rayDir);
                rayDir /= rayLength;
                rayLength = min(rayLength, _MaxRayLength);
                float4 color = RayMarch(input.positionCS.xy, rayStart, rayDir, rayLength);
                if (linearDepth > 0.999999) {
                    color.w = lerp(color.w, 1, SkyboxExtinction);
                }
                return color;
            }

            ENDHLSL
        }
    }
}
