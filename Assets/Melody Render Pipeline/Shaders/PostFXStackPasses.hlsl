#ifndef MELODY_POST_FX_PASSES_INCLUDED
#define MELODY_POST_FX_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

struct Varyings {
	float4 positionCS : SV_POSITION;
	float2 screenUV : VAR_SCREEN_UV;
};

TEXTURE2D(_PostFXSource);
SAMPLER(sampler_PostFXSource);
TEXTURE2D(_PostFXSource2);
SAMPLER(sampler_PostFXSource2);
TEXTURE2D(_ColorGradingLUT);
SAMPLER(sampler_ColorGradingLUT);
TEXTURE2D(_SSR_Result);
SAMPLER(sampler_SSR_Result);

float4 _PostFXSource_TexelSize;
bool _BloomBicubicUpsampling;
float4 _BloomThreshold;
float _BloomIntensity;
float4 _ColorAdjustment;
float4 _ColorFilter;
float4 _WhiteBalance;
float4 _SplitToningShadows;
float4 _SplitToningHighlights;
float4 _ChannelMixerRed;
float4 _ChannelMixerGreen;
float4 _ChannelMixerBlue;
float4 _SMHShadows;
float4 _SMHMidtones;
float4 _SMHHighlights;
float4 _SMHRange;
float _ColorRamp;
float _RampGamma;
float4 _ColorGradingLUTParams;
bool _ColorGradingLUTInLogC;
//use resccling
bool _CopyPoint;
bool _CopyBicubic;

float4 GetSource(float2 screenUV) {
	//buffer would never have mip maps
	if (_CopyPoint) {
		return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_point_clamp, screenUV, 0);
	}else {
		return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_PostFXSource, screenUV, 0);
	}
}

float4 GetSource2(float2 screenUV) {
	//buffer would never have mip maps
	if (_CopyPoint) {
		return SAMPLE_TEXTURE2D_LOD(_PostFXSource2, sampler_point_clamp, screenUV, 0);
	} else {
		return SAMPLE_TEXTURE2D_LOD(_PostFXSource2, sampler_PostFXSource2, screenUV, 0);
	}
}

float4 GetColor(float2 screenUV) {
	return SAMPLE_TEXTURE2D_LOD(_PostCameraColorTexture, sampler_linear_clamp, screenUV, 0);
}

float GetDepth(float2 screenUV) {
	return SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_point_clamp, screenUV, 0);
}

float4 GetNormal(float2 screenUV) {
	float4 depthNormal = SAMPLE_TEXTURE2D_LOD(_CameraDepthNormalTexture, sampler_point_clamp, screenUV, 0);
	float3 normal = DecodeViewNormalStereo(depthNormal);
	normal = normal * 0.5 + 0.5;
	return float4(normal, 1);
}

float4 GetDepthNormal(float2 screenUV) {
	float4 depthNormal = SAMPLE_TEXTURE2D_LOD(_CameraDepthNormalTexture, sampler_point_clamp, screenUV, 0);
	float3 normal = DecodeViewNormalStereo(depthNormal);
	normal = normal * 0.5 + 0.5;
	float depth = DecodeFloatRG(depthNormal.zw);
	return float4(normal, depth);
}

float3 GetDiffuse(float2 screenUV) {
	return SAMPLE_TEXTURE2D_LOD(_CameraDiffuseTexture, sampler_linear_clamp, screenUV, 0).rgb;
}

float3 GetSpecular(float2 screenUV) {
	return SAMPLE_TEXTURE2D_LOD(_CameraSpecularTexture, sampler_linear_clamp, screenUV, 0).rgb;
}

float4 GetSourceTexelSize() {
	return _PostFXSource_TexelSize;
}

//avoid blocky glow result by the bilinear filtering
float4 GetSourceBicubic(float2 screenUV) {
	return SampleTexture2DBicubic(TEXTURE2D_ARGS(_PostFXSource, sampler_PostFXSource), screenUV, _PostFXSource_TexelSize.zwxy, 1.0, 0.0);
}

//get pixel-like result by point sampler
float4 GetSourcePoint(float2 screenUV) {
	return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_point_clamp, screenUV, 0);
}

float3 ApplyBloomThreshold(float3 color) {
	float brightness = Max3(color.r, color.g, color.b);
	float soft = brightness + _BloomThreshold.y;
	soft = clamp(soft, 0.0, _BloomThreshold.z);
	soft = soft * soft * _BloomThreshold.w;
	float weight = max(soft, brightness - _BloomThreshold.x);
	weight /= max(brightness, 0.00001);
	return color * weight;
}

float Luminance(float3 color, bool useACES) {
	return useACES ? AcesLuminance(color) : Luminance(color);
}

//postexposure must be applied after all other post fx and before other color grading
float3 ColorGradePostExposure(float3 color) {
	return color * _ColorAdjustment.x;
}

//whiteBalance must be applied in LMS space after postexposure and before other color grading
float3 ColorGradeWhiteBalance(float3 color) {
	color = LinearToLMS(color);
	color *= _WhiteBalance.rgb;
	return LMSToLinear(color);
}

//ACEScc is a logarithmic subset of ACES color space. The mid gray value is 0.4135884.
float3 ColorGradeContrast(float3 color, bool useACES) {
	//for the best result this conversion is done in log c space
	color = useACES ? ACES_to_ACEScc(unity_to_ACES(color)) : LinearToLogC(color);
	color = (color - ACEScc_MIDGRAY)* _ColorAdjustment.y + ACEScc_MIDGRAY;
	//ACEScg is a linear subset of ACES color space
	return useACES ? ACES_to_ACEScg(ACEScc_to_ACES(color)) : LogCToLinear(color);
}

//just simply multiply color
float3 ColorGradeColorFilter(float3 color) {
	return color * _ColorFilter.rgb;
}

//match Adobe products, made after the color filter, after negative values have been eliminated
float3 ColorGradeSplitToning(float3 color, bool useACES) {
	color = PositivePow(color, 1.0 / 2.2);
	//limit the tins to the seperate regoions by luminance
	float t = saturate(Luminance(saturate(color), useACES) + _SplitToningShadows.w);
	float3 shadows = lerp(0.5, _SplitToningShadows.rgb, 1 - t);
	float3 highlights = lerp(0.5, _SplitToningHighlights.rgb, t);
	color = SoftLight(color, shadows);
	color = SoftLight(color, highlights);
	return PositivePow(color, 2.2);
}

float3 ColorGradeChannelMixer(float3 color) {
	return mul(
		float3x3(_ChannelMixerRed.rgb, _ChannelMixerGreen.rgb, _ChannelMixerBlue.rgb),
		color
	);
}

float3 ColorGradeShadowsMidtonesHighlighes(float3 color, bool useACES) {
	float luminance = Luminance(color, useACES);
	float shadowsWeight = 1.0 - smoothstep(_SMHRange.x, _SMHRange.y, luminance);
	float highlightsWeight = smoothstep(_SMHRange.z, _SMHRange.w, luminance);
	float midtonesWeight = 1.0 - shadowsWeight - highlightsWeight;
	return color * _SMHShadows.rgb * shadowsWeight + color * _SMHHighlights.rgb * highlightsWeight + color * _SMHMidtones.rgb * midtonesWeight;
}

float3 ColorGradeHueShift(float3 color) {
	color = RgbToHsv(color);
	float hue = color.x + _ColorAdjustment.z;
	color.x = RotateHue(hue, 0.0, 1.0);
	return HsvToRgb(color);
}

float3 ColorGradeSaturation(float3 color, bool useACES) {
	float luminance = Luminance(color, useACES);
	return (color - luminance) * _ColorAdjustment.w + luminance;
}

//NOTE : core idea is decrease picture's bit depth(32/24 bit ----> 4 bit)
float3 ColorGradePosterize(float3 color) {
	color = pow(color, float3(_RampGamma, _RampGamma, _RampGamma));
	color = color * _ColorRamp;
	color = floor(color);
	color = color / _ColorRamp;
	color = pow(color, 1.0 / _RampGamma);
	return color;
}

float3 ColorGrade(float3 color, bool useACES = false) {
	color = min(color, 60.0);
	color = ColorGradePostExposure(color);
	color = ColorGradeWhiteBalance(color);
	color = ColorGradeContrast(color, useACES);
	color = ColorGradeColorFilter(color);
	color = ColorGradePosterize(color);
	//sometimes color components will be negative after contrast
	color = max(color, 0.0);
	color = ColorGradeSplitToning(color, useACES);
	color = ColorGradeChannelMixer(color);
	//negative weight will get negative result
	color = max(color, 0.0);
	color = ColorGradeShadowsMidtonesHighlighes(color, useACES);
	color = ColorGradeHueShift(color);
	color = ColorGradeSaturation(color, useACES);
	//saturate adjustment also may get negative result
	return max(useACES ? ACEScg_to_ACES(color) : color, 0.0);
}

float3 GetColorGradedLUT(float2 uv, bool useACES = false) {
	float3 color = GetLutStripValue(uv, _ColorGradingLUTParams);
	return ColorGrade(_ColorGradingLUTInLogC ? LogCToLinear(color) : color, useACES);
}

float3 ApplyColorGradingLUT(float3 color) {
	return ApplyLut2D(
		TEXTURE2D_ARGS(_ColorGradingLUT, sampler_ColorGradingLUT), saturate(_ColorGradingLUTInLogC ? LinearToLogC(color) : color), _ColorGradingLUTParams.xyz);
}


//vertexID is the clockwise index of a triangle : 0,1,2
Varyings DefaultPassVertex(uint vertexID : SV_VertexID) {
	Varyings output;
//make the [-1, 1] NDC, visible UV coordinates cover the 0-1 range
	output.positionCS = float4(vertexID <= 1 ? -1.0 : 3.0,
							vertexID == 1 ? 3.0 : -1.0,
		0.0, 1.0);
	output.screenUV = float2(vertexID <= 1 ? 0.0 : 2.0,
							 vertexID == 1 ? 2.0 : 0.0);
//some graphics APIs have the texture V coordinate start at the top while others have it start at the bottom
	if (_ProjectionParams.x < 0.0) {
		output.screenUV.y = 1.0 - output.screenUV.y;
	}
	return output;
}


float4 CopyPassFragment(Varyings input) : SV_TARGET{
	return GetSource(input.screenUV);
}

float4 BloomHorizontalPassFragment(Varyings input) : SV_TARGET{
	float3 color = 0.0;
	float offsets[] = { 
		-4.0, -3.0, -2.0, -1.0, 
		0.0, 1.0, 2.0, 3.0, 4.0 
	};
//The weights are derived from Pascal's triangle
	float weights[] = {
		0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
		0.19459459, 0.12162162, 0.05405405, 0.01621622
	};
	for (int i = 0; i < 9; i++)	{
		float offset = offsets[i] * 2.0 * GetSourceTexelSize().x;
		color += GetSource(input.screenUV + float2(offset, 0.0)).rgb * weights[i];
	}
	return float4(color, 1.0);
}

float4 BloomVerticalPassFragment(Varyings input) : SV_TARGET{
	float3 color = 0.0;
	float offsets[] = {
		-4.0, -3.0, -2.0, -1.0,
		0.0, 1.0, 2.0, 3.0, 4.0
	};
//The weights are derived from Pascal's triangle
	float weights[] = {
		0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
		0.19459459, 0.12162162, 0.05405405, 0.01621622
	};
	for (int i = 0; i < 9; i++) {
		float offset = offsets[i] * GetSourceTexelSize().y;
		color += GetSource(input.screenUV + float2(0.0, offset)).rgb * weights[i];
	}
	return float4(color, 1.0);
}

//just add upsample source to origin source
float4 BloomCombineAdditivePassFragment(Varyings input) : SV_TARGET{
	float3 lowRes;
	if (_BloomBicubicUpsampling) {
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	} else {
		lowRes = GetSource(input.screenUV).rgb;
	}
	//preserve original high resolution texture's alpha
	float4 highRes = GetSource2(input.screenUV);
	return float4(lowRes * _BloomIntensity + highRes.rgb, highRes.a);
}

//it does not make image brighter cause it only shows a cropped portion of the original after guassian filtering
float4 BloomCombineScatterPassFragment(Varyings input) : SV_TARGET{
	float3 lowRes;
	if (_BloomBicubicUpsampling) {
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else {
	  lowRes = GetSource(input.screenUV).rgb;
	}
	float4 highRes = GetSource2(input.screenUV);
	return float4(lerp(highRes.rgb, lowRes, _BloomIntensity), highRes.a);
}

//adds light energy offset to combine scatter pass
float4 BloomScatterFinalPassFragment(Varyings input) : SV_TARGET{
	float3 lowRes;
	if (_BloomBicubicUpsampling) {
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else {
		lowRes = GetSource(input.screenUV).rgb;
	}
	float4 highRes = GetSource2(input.screenUV);
//add light preflited to make energy nearly balance
	lowRes += highRes.rgb - ApplyBloomThreshold(highRes.rgb);
	return float4(lerp(highRes.rgb, lowRes, _BloomIntensity), highRes.a);
}

//it applys a threshold curve that makes colors have different sense
float4 BloomPrefilterPassFragment(Varyings input) : SV_TARGET{
	float3 color = ApplyBloomThreshold(GetSource(input.screenUV).rgb);
	return float4(color, 1.0);
}

//cause regions are about the size of a pixel or smaller can extremly change relative size and fade in and out during the movement
float4 BloomPrefilterFirefliesPassFragment(Varyings input) : SV_TARGET{
	float3 color = 0.0;
	float2 offsets[] = {
		float2(-1.0, 1.0), float2(0.0, 1.0), float2(1.0, 1.0),
		float2(-1.0, 0.0), float2(0.0, 0.0), float2(1.0, 0.0),
		float2(-1.0, -1.0), float2(0.0, -1.0), float2(1.0, -1.0)
	};
	float weightSum = 0.0;
	for (int i = 0; i < 9; i++) {
		//multiply 2 because filter resolution is half
		float3 c = GetSource(input.screenUV + offsets[i] * GetSourceTexelSize().xy * 2.0).rgb;
		c = ApplyBloomThreshold(c);
		//color += c;
		// NOT ENOUGH, STILL fireflies cause it JUST spread out a larger area

		//a sample weight is l/(1 + l)
		//weighed average instead, based on the color's luminance
		float w = 1.0 / (Luminance(c) + 1.0);
		color += c * w;
		weightSum += w;
	}
	//color /= 9.0;
	color /= weightSum;
	return float4(color, 1.0);
}

float4 ColorGradingReinhardPassFragment(Varyings input) : SV_TARGET{
	//float4 color = GetSource(input.screenUV);
	//color.rgb = ColorGrade(color.rgb);
	float3 color = GetColorGradedLUT(input.screenUV);
	//Tone Mapping method that makes lighter color darker while darker color will not be influenced so much , so "c = 1/(1 + c)" is fine
	color /= color + 1.0;
	return float4(color, 1.0);
}

float4 ColorGradingNeutralPassFragment(Varyings input) : SV_TARGET{
	//float4 color = GetSource(input.screenUV);
	//color.rgb = ColorGrade(color.rgb);
	float3 color = GetColorGradedLUT(input.screenUV);
	//tone mapping
	color.rgb = NeutralTonemap(color.rgb);
	return float4(color, 1.0);
}

float4 ColorGradingACESPassFragment(Varyings input) : SV_TARGET{
	//float4 color = GetSource(input.screenUV);
	//color.rgb = ColorGrade(color.rgb, true);
	float3 color = GetColorGradedLUT(input.screenUV, true);
	//tone mapping
	color.rgb = AcesTonemap(color.rgb);
	return float4(color.rgb, 1.0);
}

float4 ColorGradingNonePassFragment(Varyings input) : SV_TARGET{
	//float4 color = GetSource(input.screenUV);
	//color.rgb = ColorGrade(color.rgb);
	//return color;
	float3 color = GetColorGradedLUT(input.screenUV);
	return float4(color, 1.0);
}

float4 FinalColorGradingPassFragment(Varyings input) : SV_TARGET{
	float4 color = GetSource(input.screenUV);
	color.rgb = ApplyColorGradingLUT(color.rgb);
	return color;
}

float4 FinalPassFragmentRescale(Varyings input) : SV_TARGET{
	if (_CopyBicubic) {
		return GetSourceBicubic(input.screenUV);
	} else {
		return GetSource(input.screenUV);
	}
}

float4 ColorGradingWithLumaPassFragment(Varyings input) : SV_TARGET{
	float4 color = GetSource(input.screenUV);
	color.rgb = ApplyColorGradingLUT(color.rgb);
	color.a = sqrt(Luminance(color.rgb));
	return color;
}
#endif
