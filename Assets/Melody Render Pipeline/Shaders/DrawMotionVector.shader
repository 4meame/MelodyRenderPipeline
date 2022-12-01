Shader "Hidden/Melody RP/DrawMotionVector" {
    Properties{

    }

    SubShader {
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"

        bool _HasLastPositionData;

        struct MotionVertexInput {
            float4 positionOS : POSITION;
            float3 oldPos : TEXCOORD4;
        };

        struct MotionVectorData {
            float4 positionCS : SV_POSITION;
            float4 transferPos : TEXCOORD0;
            float4 transferPosOld : TEXCOORD1;
        };

        MotionVectorData VertMotionVectors(MotionVertexInput input) {
            MotionVectorData output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
#if defined(UNITY_REVERSED_Z)
            output.positionCS.z -= unity_MotionVectorsParams.z * output.positionCS.w;
#else
            output.positionCS.z += unity_MotionVectorsParams.z * output.positionCS.w;
#endif
            output.transferPos = mul(_NonJitteredViewProjMatrix, mul(unity_ObjectToWorld, input.positionOS));
            output.transferPosOld = mul(_PrevViewProjMatrix, mul(unity_MatrixPreviousM, _HasLastPositionData ? float4(input.oldPos, 1) : input.positionOS));
            return output;
        }

        float4 FragMotionVectors(MotionVectorData input) : SV_TARGET {
            float3 hPos = (input.transferPos.xyz / input.transferPos.w);
            float3 hPosOld = (input.transferPosOld.xyz / input.transferPosOld.w);
            //v is the viewport position at this pixel in the range 0 to 1.
            float2 vPos = (hPos.xy + 1.0f) / 2.0f;
            float2 vPosOld = (hPosOld.xy + 1.0f) / 2.0f;
            //some graphics APIs have the texture V coordinate start at the top while others have it start at the bottom, -1 means proj matrix has filpped
            if (_ProjectionParams.x < 0) {
                vPos.y = 1.0 - vPos.y;
                vPosOld.y = 1.0 - vPosOld.y;
            }
            half2 uvDiff = vPos - vPosOld;
            bool _ForceNoMotion = unity_MotionVectorsParams.y == 0.0;
            return lerp(half4(uvDiff, 0, 1), 0, (half)_ForceNoMotion);
        }

        struct CameraMotionVertexInput {
            float4 positionOS : POSITION;
            float3 normal : NORMAL;
        };

        struct CameraMotionVector {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 ray : TEXCOORD1;
        };

        CameraMotionVector VertMotionVectorsCamera(CameraMotionVertexInput input) {
            CameraMotionVector output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = ComputeScreenPos(output.positionCS);
            // we know we are rendering a quad, and the normal passed from C# is the raw ray
            output.ray = input.normal;
            return output;
        }

        float2 CalculateMotion(float rawDepth, float3 inRay) {
            float depth = Linear01Depth(rawDepth, _ZBufferParams);
            float3 ray = inRay * (_ProjectionParams.z / inRay.z);
            float3 vPos = ray * depth;
            float4 worldPos = mul(unity_CameraToWorld, float4(vPos, 1.0));

            float4 prevClipPos = mul(_PrevViewProjMatrix, worldPos);
            float4 curClipPos = mul(_NonJitteredViewProjMatrix, worldPos);
            float2 prevHPos = prevClipPos.xy / prevClipPos.w;
            float2 curHPos = curClipPos.xy / curClipPos.w;
            //v is the viewport position at this pixel in the range 0 to 1.
            float2 vPosPrev = (prevHPos.xy + 1.0f) / 2.0f;
            float2 vPosCur = (curHPos.xy + 1.0f) / 2.0f;
            //some graphics APIs have the texture V coordinate start at the top while others have it start at the bottom, -1 means proj matrix has filpped
            if (_ProjectionParams.x < 0) {
                vPosPrev.y = 1.0 - vPosPrev.y;
                vPosCur.y = 1.0 - vPosCur.y;
            }
            return vPosCur - vPosPrev;
        }

        float4 FragMotionVectorsCamera(CameraMotionVector input) : SV_Target {
            float depth = SAMPLE_TEXTURE2D_LOD(_TransparentDepthTexture, sampler_point_clamp, input.uv, 0);
            float2 motion = CalculateMotion(depth, input.ray);
            return float4(motion, 0, min(motion.x + motion.y, 1));
        }
        ENDHLSL

        //0 - Motion vectors
        Pass
        {
            Tags { "LightMode" = "MelodyUnlit" }

            ZTest LEqual
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex VertMotionVectors
            #pragma fragment FragMotionVectors
            ENDHLSL
        }

        //1 - Camera motion vectors
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Always
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex VertMotionVectorsCamera
            #pragma fragment FragMotionVectorsCamera
            ENDHLSL
        }
    }
}
