Shader "Hidden/Melody RP/VolumetricLight/Filter"
{
    Properties
    {
        _MainTex("Texture", any) = "" {}
    }

    SubShader
    {
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        //--------------------------------------------------------------------------------------------
        //downsample, bilateral blur and upsample config
        //--------------------------------------------------------------------------------------------        
        //method used to downsample depth buffer: 0 = min; 1 = max; 2 = min/max in chessboard pattern
        #define DOWNSAMPLE_DEPTH_MODE 2
        #define UPSAMPLE_DEPTH_THRESHOLD 1.5f
        #define BLUR_DEPTH_FACTOR 0.5
        #define GAUSS_BLUR_DEVIATION 1.5        
        #define FULL_RES_BLUR_KERNEL_SIZE 7
        #define HALF_RES_BLUR_KERNEL_SIZE 5

        TEXTURE2D(_HalfResDepthTexture);
        TEXTURE2D(_HalfResColorTexture);
        TEXTURE2D(_MainTex);
        float4 _HalfResDepthTexture_TexelSize;
        float4 _MainTex_ST;
        
        struct Attributes {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
        };
        
        struct Varyings {
            float2 uv : TEXCOORD0;
            float4 vertex : SV_POSITION;
        };
        
        struct DownSample {
            float2 uv00 : TEXCOORD0;
            float2 uv01 : TEXCOORD1;
            float2 uv10 : TEXCOORD2;
            float2 uv11 : TEXCOORD3;
            float4 vertex : SV_POSITION;
        };
        
        struct UpSample {
            float2 uv : TEXCOORD0;
            float2 uv00 : TEXCOORD1;
            float2 uv01 : TEXCOORD2;
            float2 uv10 : TEXCOORD3;
            float2 uv11 : TEXCOORD4;
            float4 vertex : SV_POSITION;
        };
        
        Varyings vert(Attributes i) {
            Varyings o;
            o.vertex = TransformObjectToHClip(i.vertex);
            o.uv = i.uv;
            return o;
        }

        //vert depth downsample
        DownSample vertDepthDownSample(Attributes i, float2 texelSize) {
            DownSample o;
            o.vertex = TransformObjectToHClip(i.vertex);
            o.uv00 = i.uv - 0.5 * texelSize.xy;
            o.uv10 = o.uv00 + float2(texelSize.x, 0);
            o.uv01 = o.uv00 + float2(0, texelSize.y);
            o.uv11 = o.uv00 + texelSize.xy;
            return o;
        }

        //vert upsample
        UpSample vertUpSample(Attributes i, float2 texelSize) {
            UpSample o;
            o.vertex = TransformObjectToHClip(i.vertex);
            o.uv = i.uv;
            o.uv00 = i.uv - 0.5 * texelSize.xy;
            o.uv10 = o.uv00 + float2(texelSize.x, 0);
            o.uv01 = o.uv00 + float2(0, texelSize.y);
            o.uv11 = o.uv00 + texelSize.xy;
            return o;
        }

        //bilaternal upsample
        float4 BilateralUpSample(UpSample input, Texture2D hiDepth, Texture2D loDepth, Texture2D loColor) {
            const float threshold = UPSAMPLE_DEPTH_THRESHOLD;
            float4 highResDepth = LinearEyeDepth(SAMPLE_TEXTURE2D(hiDepth, sampler_point_clamp, input.uv), _ZBufferParams).xxxx;
            float4 lowResDepth;
            lowResDepth[0] = LinearEyeDepth(SAMPLE_TEXTURE2D(loDepth, sampler_point_clamp, input.uv00), _ZBufferParams);
            lowResDepth[1] = LinearEyeDepth(SAMPLE_TEXTURE2D(loDepth, sampler_point_clamp, input.uv10), _ZBufferParams);
            lowResDepth[2] = LinearEyeDepth(SAMPLE_TEXTURE2D(loDepth, sampler_point_clamp, input.uv01), _ZBufferParams);
            lowResDepth[3] = LinearEyeDepth(SAMPLE_TEXTURE2D(loDepth, sampler_point_clamp, input.uv11), _ZBufferParams);
            float4 depthDiff = abs(lowResDepth - highResDepth);
            float accumDiff = dot(depthDiff, float4(1, 1, 1, 1));
            [branch]
            //small error, not an edge -> use bilinear filter
            if (accumDiff < threshold) {
                return SAMPLE_TEXTURE2D(loColor, sampler_linear_clamp, input.uv);
            }
            //find nearest sample
            float minDepthDiff = depthDiff[0];
            float2 nearestUv = input.uv00;
            if (depthDiff[1] < minDepthDiff) {
                nearestUv = input.uv10;
                minDepthDiff = depthDiff[1];
            }
            if (depthDiff[2] < minDepthDiff) {
                nearestUv = input.uv01;
                minDepthDiff = depthDiff[2];
            }
            if (depthDiff[3] < minDepthDiff) {
                nearestUv = input.uv11;
                minDepthDiff = depthDiff[3];
            }
            return SAMPLE_TEXTURE2D(loColor, sampler_point_clamp, input.uv);
        }

        //downsample depth
        float DownSampleDepth(DownSample input, Texture2D depthTexture) {
            float4 depth;
            depth.x = SAMPLE_TEXTURE2D(depthTexture, sampler_point_clamp, input.uv00).x;
            depth.y = SAMPLE_TEXTURE2D(depthTexture, sampler_point_clamp, input.uv01).x;
            depth.z = SAMPLE_TEXTURE2D(depthTexture, sampler_point_clamp, input.uv10).x;
            depth.w = SAMPLE_TEXTURE2D(depthTexture, sampler_point_clamp, input.uv11).x;
#if DOWNSAMPLE_DEPTH_MODE == 0
            //min  depth
            return min(min(depth.x, depth.y), min(depth.z, depth.w));
#elif DOWNSAMPLE_DEPTH_MODE == 1
            //max  depth
            return max(max(depth.x, depth.y), max(depth.z, depth.w));
#elif DOWNSAMPLE_DEPTH_MODE == 2
            //min/max depth in chessboard pattern
            float minDepth = min(min(depth.x, depth.y), min(depth.z, depth.w));
            float maxDepth = max(max(depth.x, depth.y), max(depth.z, depth.w));
            //chessboard pattern
            int2 position = input.vertex.xy % 2;
            int index = position.x + position.y;
            return index == 1 ? minDepth : maxDepth;
#endif
        }

        //gaussianWeight
        float GaussianWeight(float offset, float deviation) {
            float weight = 1.0f / sqrt(2.0f * PI * deviation * deviation);
            weight *= exp(-(offset * offset) / (2.0f * deviation * deviation));
            return weight;
        }

        float4 BilateralBlur(Varyings input, int2 direction, Texture2D depth, const int kernelRadius, float2 pixelSize) {
            //make it really strong
            const float deviation = kernelRadius / GAUSS_BLUR_DEVIATION;
            float2 uv = input.uv;
            float4 centerColor = SAMPLE_TEXTURE2D(_MainTex, sampler_linear_clamp, uv);
            float3 color = centerColor.xyz;
            float centerDepth = LinearEyeDepth(SAMPLE_TEXTURE2D(depth, sampler_point_clamp, uv), _ZBufferParams).x;
            float weightSum = 0;
            //gaussian weight is computed from constants only -> will be computed in compile time
            float weight = GaussianWeight(0, deviation);
            color *= weight;
            weightSum += weight;
            [unroll] 
            for (int i = -kernelRadius; i < 0; i += 1) {
                float2 offset = (direction * i);
                float3 sampleColor = SAMPLE_TEXTURE2D_OFFSET(_MainTex, sampler_linear_clamp, uv, offset);
                float sampleDepth = LinearEyeDepth(SAMPLE_DEPTH_OFFSET(depth, sampler_point_clamp, uv, offset), _ZBufferParams);
                float depthDiff = abs(centerDepth - sampleDepth);
                float dFactor = depthDiff * BLUR_DEPTH_FACTOR;
                float w = exp(-(dFactor * dFactor));
                //gaussian weight is computed from constants only -> will be computed in compile time
                weight = GaussianWeight(i, deviation) * w;
                color += weight * sampleColor;
                weightSum += weight;
            }
            [unroll]
            for (i = 1; i <= kernelRadius; i += 1) {
                float2 offset = (direction * i);
                float3 sampleColor = SAMPLE_TEXTURE2D_OFFSET(_MainTex, sampler_linear_clamp, uv, offset);
                float sampleDepth = LinearEyeDepth(SAMPLE_DEPTH_OFFSET(depth, sampler_point_clamp, uv, offset), _ZBufferParams);
                float depthDiff = abs(centerDepth - sampleDepth);
                float dFactor = depthDiff * BLUR_DEPTH_FACTOR;
                float w = exp(-(dFactor * dFactor));
                //gaussian weight is computed from constants only -> will be computed in compile time
                weight = GaussianWeight(i, deviation) * w;
                color += weight * sampleColor;
                weightSum += weight;
            }
            color /= weightSum;
            return float4(color, centerColor.w);
        }
        ENDHLSL

        //pass 0 - horizontal blur (hires)
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            Name "horizontal blur (hires)"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment horizontalFrag

            float4 horizontalFrag(Varyings input) : SV_Target
            {
                return BilateralBlur(input, int2(1, 0), _TransparentDepthTexture, FULL_RES_BLUR_KERNEL_SIZE, _TransparentDepthTexture_TexelSize.xy);
            }

            ENDHLSL
        }

        //pass 1 - vertical blur (hires)
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            Name "vertical blur (hires)"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment verticalFrag

            float4 verticalFrag(Varyings input) : SV_Target
            {
                return BilateralBlur(input, int2(0, 1), _TransparentDepthTexture, FULL_RES_BLUR_KERNEL_SIZE, _TransparentDepthTexture_TexelSize.xy);
            }

            ENDHLSL
        }

        //pass 2 - horizontal blur (lores)
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            Name "horizontal blur (lores)"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment horizontalFrag

            float4 horizontalFrag(Varyings input) : SV_Target
            {
                return BilateralBlur(input, int2(1, 0), _HalfResDepthTexture, HALF_RES_BLUR_KERNEL_SIZE, _HalfResDepthTexture_TexelSize.xy);
            }

            ENDHLSL
        }

        //pass 3 - vertical blur (lores)
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            Name "vertical blur (lores)"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment verticalFrag

            float4 verticalFrag(Varyings input) : SV_Target
            {
                return BilateralBlur(input, int2(0, 1), _HalfResDepthTexture, HALF_RES_BLUR_KERNEL_SIZE, _HalfResDepthTexture_TexelSize.xy);
            }

            ENDHLSL
        }

        
		//pass 4 - downsample depth to half
		Pass
		{
            Cull Off
            ZWrite Off
            ZTest Always
            Name "downsample depth to half"
            HLSLPROGRAM
            #pragma target 3.5
			#pragma vertex vertHalfDepth
			#pragma fragment frag

			DownSample vertHalfDepth(Attributes i)
			{
                return vertDepthDownSample(i, _TransparentDepthTexture_TexelSize);
			}

			float frag(DownSample input) : SV_Target
			{
                return DownSampleDepth(input, _TransparentDepthTexture);
			}

            ENDHLSL
		}

        //pass 5 - bilateral upsample
		Pass
		{
            Blend One Zero
            Cull Off
            ZWrite Off
            ZTest Always
            Name "bilateral upsample"
            HLSLPROGRAM
            #pragma target 3.5
			#pragma vertex vertUpsampleToFull
			#pragma fragment frag		

			UpSample vertUpsampleToFull(Attributes i)
			{
                return vertUpSample(i, _HalfResDepthTexture_TexelSize);
			}

			float4 frag(UpSample input) : SV_TARGET
			{
				return BilateralUpSample(input, _TransparentDepthTexture, _HalfResDepthTexture, _HalfResColorTexture);
			}

            ENDHLSL
		}

        //pass 6 - combine add
		Pass
		{
            Blend One Zero
            Cull Off
            ZWrite Off
            ZTest Always
            Name "combine"
            HLSLPROGRAM
            #pragma target 3.5
			#pragma vertex vertShader
			#pragma fragment fragShader		
            #pragma multi_compile _ _DIRECTION _SPOT _POINT
            TEXTURE2D(_Main);
            TEXTURE2D(_Source);
            float4 _Main_ST;

            Varyings vertShader(Attributes i)
            {
                Varyings o;
                o.vertex = TransformObjectToHClip(i.vertex);
                o.uv = TRANSFORM_TEX(i.uv, _Main);
                return o;
            }

            float4 fragShader(Varyings input) : SV_TARGET
            {
                float4 main = SAMPLE_TEXTURE2D(_Main, sampler_linear_clamp, input.uv);
                //some graphics APIs have the texture V coordinate start at the top while others have it start at the bottom
                if (_ProjectionParams.x < 0.0) {
                    input.uv.y = 1.0 - input.uv.y;
                }
                float4 source = SAMPLE_TEXTURE2D(_Source, sampler_linear_clamp, input.uv);
#if defined(_DIRECTION)
                source *= main.w;
#endif
                source.xyz += main.xyz;
                return float4(source.xyz, 1);
            }

            ENDHLSL
		}

        //pass 7 - copy
		Pass
		{
            Name "copy"
            HLSLPROGRAM
            #pragma target 3.5
			#pragma vertex vertShader
			#pragma fragment fragShader		

            Varyings vertShader(Attributes i)
            {
                Varyings o;
                o.vertex = TransformObjectToHClip(i.vertex);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                return o;
            }

            float4 fragShader(Varyings input) : SV_TARGET
            {
                //some graphics APIs have the texture V coordinate start at the top while others have it start at the bottom
                if (_ProjectionParams.x < 0.0) {
                    input.uv.y = 1.0 - input.uv.y;
                }
                float4 main = SAMPLE_TEXTURE2D(_MainTex, sampler_linear_clamp, input.uv);
                return main;
            }

            ENDHLSL
		}
    }
}
