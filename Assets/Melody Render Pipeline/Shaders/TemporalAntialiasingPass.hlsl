#ifndef MELODY_TEMPORAL_AITIALIASING_PASS_INCLUDED
#define MELODY_TEMPORAL_AITIALIASING_PASS_INCLUDED

TEXTURE2D(_SourceTex);
TEXTURE2D(_HistoryTex);
TEXTURE2D(_LastFrameDepthTexture);
TEXTURE2D(_LastFrameMotionVectorTexture);
TEXTURE2D(_TempTexture);

static const int2 _OffsetArray[8] = {
	int2(-1, -1),
	int2(0, -1),
	int2(1, -1),
	int2(-1, 0),
	int2(1, 1),
	int2(1, 0),
	int2(-1, 1),
	int2(0, -1)
};

float3 _TemporalClipBounding;
float2 _Jitter;
float4 _FinalBlendParams;
float _Sharpness;

float2 _LastJitter;
float4x4 _InvNonJitterVP;
float4x4 _InvLastVP;

float Luma4(float3 Color) {
	return (Color.g * 2) + (Color.r + Color.b);
}

float Luma(float3 Color) {
	return (Color.g * 0.5) + (Color.r + Color.b) * 0.25;
}

float3 RGBToYCoCg3(float3 RGB) {
	const float3x3 mat = float3x3(0.25, 0.5, 0.25, 0.5, 0, -0.5, -0.25, 0.5, -0.25);
	float3 col = mul(mat, RGB);
	return col;
}

float3 YCoCgToRGB3(float3 YCoCg) {
	const float3x3 mat = float3x3(1, 1, -1, 1, 0, 1, 1, -1, -1);
	return mul(mat, YCoCg);
}

float4 RGBToYCoCg4(float4 RGB) {
	return float4(RGBToYCoCg(RGB.xyz), RGB.w);
}

float4 YCoCgToRGB4(float4 YCoCg) {
	return float4(YCoCgToRGB(YCoCg.xyz), YCoCg.w);
}

#define TONE_BOUND 0.5
float3 Tonemap(float3 x) {
    float luma = Luma(x);
    [flatten]
	if (luma <= TONE_BOUND) { 
		return x; 
	}
	else { 
		return x * (TONE_BOUND * TONE_BOUND - luma) / (luma * (2 * TONE_BOUND - 1 - luma)); 
	}
}

float3 TonemapInvert(float3 x) {
    float luma = Luma(x);
    [flatten]
	if (luma <= TONE_BOUND) {
		return x;
	}
	else {
		return x * (TONE_BOUND * TONE_BOUND - (2 * TONE_BOUND - 1) * luma) / (luma * (1 - luma));
	}
}

float HdrWeight4(float3 Color, const float Exposure) {
	return 1.0 / (Luma4(Color) * Exposure + 4);
}

float3 ClipToAABB(float3 color, float3 minimum, float3 maximum) {
	//NOTE : only clips towards aabb center (but fast!)
	float3 center = 0.5 * (maximum + minimum);
	float3 extents = 0.5 * (maximum - minimum);
	//this is actually distance, however the keyword is reserved
	float3 offset = color.rgb - center;
	float3 ts = abs(extents / (offset + 0.0001));
	float t = saturate(Min3(ts.x, ts.y, ts.z));
	color.rgb = center + offset * t;
	return color;
}

float2 ReprojectedMotionVectorUV(float2 uv, out float outDepth) {
	float neighborhood;
	const float2 k = _CameraBufferSize.xy;
	uint i;
	outDepth = _CameraDepthTexture.Sample(sampler_point_clamp, uv).x;
	float3 result = float3(0, 0, outDepth);
	[unroll]
	for (i = 0; i < 8; ++i) {
		neighborhood = SAMPLE_DEPTH_OFFSET(_CameraDepthTexture, sampler_point_clamp, uv, _OffsetArray[i]);
		result = lerp(result, float3(_OffsetArray[i], neighborhood), COMPARE_DEPTH(neighborhood, result.z));
	}
	return uv + result.xy * k;
}

float2 Linear01Depth(float2 z) {
	return 1.0 / (_ZBufferParams.x * z + _ZBufferParams.y);
}

float4 TemporalAntialiasingResolve(Varyings input) : SV_TARGET{
	float2 screenUV = (input.screenUV - _Jitter);
	float2 screenSize = _CameraBufferSize.zw;
	float depth;
	//get the closest pixel to the camera, ignore pixels possiblly been occluded
	float2 closest = ReprojectedMotionVectorUV(input.screenUV, depth);
	float2 velocity = SAMPLE_TEXTURE2D(_CameraMotionVectorTexture, sampler_point_clamp, closest).xy;
	//clamp temporal color
	float2 previousUV = input.screenUV - velocity;
	float4 middleCenter = SAMPLE_TEXTURE2D(_SourceTex, sampler_point_clamp, screenUV);
	if (previousUV.x > 1 || previousUV.y > 1 || previousUV.x < 0 || previousUV.y < 0) {
		return middleCenter;
	}
	//get color surrounding the center
	float4 topLeft = SAMPLE_TEXTURE2D_OFFSET(_SourceTex, sampler_point_clamp, screenUV, int2(-1, -1));
	float4 topCenter = SAMPLE_TEXTURE2D_OFFSET(_SourceTex, sampler_point_clamp, screenUV, int2(0, -1));
	float4 topRight = SAMPLE_TEXTURE2D_OFFSET(_SourceTex, sampler_point_clamp, screenUV, int2(1, -1));
	float4 middleLeft = SAMPLE_TEXTURE2D_OFFSET(_SourceTex, sampler_point_clamp, screenUV, int2(-1, 0));
	float4 middleRight = SAMPLE_TEXTURE2D_OFFSET(_SourceTex, sampler_point_clamp, screenUV, int2(1, 0));
	float4 bottomLeft = SAMPLE_TEXTURE2D_OFFSET(_SourceTex, sampler_point_clamp, screenUV, int2(-1, 1));
	float4 bottomCenter = SAMPLE_TEXTURE2D_OFFSET(_SourceTex, sampler_point_clamp, screenUV, int2(0, 1));
	float4 bottomRight = SAMPLE_TEXTURE2D_OFFSET(_SourceTex, sampler_point_clamp, screenUV, int2(1, 1));
	//then calculate the weight of Hdr
	float sampleWeights[9];
	sampleWeights[0] = HdrWeight4(topLeft.rgb, 10);
	sampleWeights[1] = HdrWeight4(topCenter.rgb, 10);
	sampleWeights[2] = HdrWeight4(topRight.rgb, 10);
	sampleWeights[3] = HdrWeight4(middleLeft.rgb, 10);
	sampleWeights[4] = HdrWeight4(middleCenter.rgb, 10);
	sampleWeights[5] = HdrWeight4(middleRight.rgb, 10);
	sampleWeights[6] = HdrWeight4(bottomLeft.rgb, 10);
	sampleWeights[7] = HdrWeight4(bottomCenter.rgb, 10);
	sampleWeights[8] = HdrWeight4(bottomRight.rgb, 10);
	float totalWeight = sampleWeights[0] + sampleWeights[1] + sampleWeights[2] + sampleWeights[3] + sampleWeights[4] + sampleWeights[5] + sampleWeights[6] + sampleWeights[7] + sampleWeights[8];
	//translate color from rgb to YCoCg to do neighborhood clamping 
	topLeft = RGBToYCoCg4(topLeft);
	topCenter = RGBToYCoCg4(topCenter);
	topRight = RGBToYCoCg4(topRight);
	middleLeft = RGBToYCoCg4(middleLeft);
	middleCenter = RGBToYCoCg4(middleCenter);
	middleRight = RGBToYCoCg4(middleRight);
	bottomLeft = RGBToYCoCg4(bottomLeft);
	bottomCenter = RGBToYCoCg4(bottomCenter);
	bottomRight = RGBToYCoCg4(bottomRight);
	float4 averageColor = (topLeft * sampleWeights[0] + topCenter * sampleWeights[1] + topRight * sampleWeights[2] + middleLeft * sampleWeights[3] + middleCenter * sampleWeights[4] + middleRight * sampleWeights[5] + bottomLeft * sampleWeights[6] + bottomCenter * sampleWeights[7] + bottomRight * sampleWeights[8]) / totalWeight;
	//the bigger velocity, the pixel more likely change last frame
	float velocityLength = length(velocity);
	float velocityWeight = saturate(velocityLength * _TemporalClipBounding.z);
	float AABBScale = lerp(_TemporalClipBounding.x, _TemporalClipBounding.y, velocityWeight);
	float4 m1 = topLeft + topCenter + topRight + middleLeft + middleCenter + middleRight + bottomLeft + bottomCenter + bottomRight;
	float4 m2 = topLeft * topLeft + topCenter * topCenter + topRight * topRight + middleLeft * middleLeft + middleCenter * middleCenter + middleRight * middleRight + bottomLeft * bottomLeft + bottomCenter * bottomCenter + bottomRight * bottomRight;
	//get stddev, bigger stddev means that feedback is far away average
	float4 mean = m1 / 9;
	float4 stddev = sqrt(m2 / 9 - mean * mean);
	float4 minColor = mean - AABBScale * stddev;
	float4 maxColor = mean + AABBScale * stddev;
	minColor = min(minColor, averageColor);
	maxColor = max(maxColor, averageColor);
	//resolve temporal
	float4 currentColor = YCoCgToRGB4(middleCenter);
	//color after weighting will be blur, sharp it
	float4 corners = (YCoCgToRGB4(topLeft + bottomRight + topRight + bottomLeft) - currentColor) * 2;
	currentColor += (currentColor - (corners * 0.166667)) * 2.718282 * _Sharpness;
	currentColor = clamp(currentColor, 0, 60.0);
	//history sample
	float2 prevDepthUV = previousUV + _Jitter - _LastJitter;
	float lastFrameDepth = SAMPLE_TEXTURE2D(_LastFrameDepthTexture, sampler_point_clamp, prevDepthUV).r;
	float2 lastFrameMV = SAMPLE_TEXTURE2D(_LastFrameMotionVectorTexture, sampler_point_clamp, prevDepthUV).xy;
	float lastFrameMVLength = dot(lastFrameMV, lastFrameMV);
	[unroll]
	for (int i = 0; i < 8; i++) {
		float2 currentMV = SAMPLE_TEXTURE2D(_LastFrameMotionVectorTexture, sampler_point_clamp, prevDepthUV + _OffsetArray[i]).xy;
		float currentMVLength = dot(currentMV, currentMV);
		lastFrameMVLength = max(currentMVLength, lastFrameMVLength);
	}
	float lastVelocityWeight = saturate(sqrt(lastFrameMVLength) * _TemporalClipBounding.z);
	float4 worldPos = mul(_InvNonJitterVP, float4(input.screenUV, depth, 1));
	float4 lastWorldPos = mul(_InvLastVP, float4(prevDepthUV, lastFrameDepth, 1));
	worldPos /= worldPos.w; 
	lastWorldPos /= lastWorldPos.w;
	worldPos -= lastWorldPos;
	//calculate adaptive blend factors
	float depthAdaptiveForce = 1 - saturate((dot(worldPos.xyz, worldPos.xyz) - 0.02) * 10);
	float4 previousColor = SAMPLE_TEXTURE2D(_HistoryTex, sampler_linear_clamp, previousUV);
	//whether current luminance is brighter than last
	float luminDiff = depthAdaptiveForce - previousColor.w;
	float tWeight = lerp(0.7, 0.9, saturate(tanh(luminDiff * 2) * 0.5 + 0.5));
	depthAdaptiveForce = lerp(depthAdaptiveForce, previousColor.w, tWeight);
	depthAdaptiveForce = lerp(depthAdaptiveForce, 1, velocityWeight);
	depthAdaptiveForce = lerp(depthAdaptiveForce, 1, lastVelocityWeight);
	float2 depth01 = Linear01Depth(float2(lastFrameDepth, depth));
	float finalDepthAdaptive = lerp(depthAdaptiveForce, 1, (depth01.x > 0.9999) || (depth01.y > 0.9999));
	previousColor.xyz = lerp(previousColor.xyz, YCoCgToRGB3(ClipToAABB(RGBToYCoCg3(previousColor.xyz), minColor.xyz, maxColor.xyz)), finalDepthAdaptive);
	//history blend
	float historyWeight = lerp(_FinalBlendParams.x, _FinalBlendParams.y, velocityWeight);
	currentColor.xyz = Tonemap(currentColor.xyz);
	previousColor.xyz = Tonemap(previousColor.xyz);
	float4 temporalColor = lerp(currentColor, previousColor, historyWeight);
	temporalColor.xyz = TonemapInvert(temporalColor.xyz);
	temporalColor.w = depthAdaptiveForce;
	return max(0, temporalColor);
}

float4 CopyMotionVectorFragment(Varyings input) : SV_TARGET {
	return SAMPLE_TEXTURE2D_LOD(_TempTexture, sampler_point_clamp, input.screenUV, 0);
}

float CopyDepthFragment(Varyings input) : SV_DEPTH {
	return SAMPLE_DEPTH_TEXTURE_LOD(_TempTexture, sampler_point_clamp, input.screenUV, 0);
}

#endif
