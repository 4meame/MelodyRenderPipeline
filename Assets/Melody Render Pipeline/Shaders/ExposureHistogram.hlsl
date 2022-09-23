//DO NOT forget to update 'LogHistogram.cs' if you change these values
#define HISTOGRAM_BINS          256
#define HISTOGRAM_TEXELS        HISTOGRAM_BINS / 4
#define HISTOGRAM_THREAD_X				16
#define HISTOGRAM_THREAD_Y				16
#define HISTOGRAM_REDUCTION_THREAD_X    HISTOGRAM_THREAD_X
#define HISTOGRAM_REDUCTION_THREAD_Y    HISTOGRAM_BINS / HISTOGRAM_THREAD_Y
#define HISTOGRAM_REDUCTION_BINS HISTOGRAM_REDUCTION_THREAD_X * HISTOGRAM_REDUCTION_THREAD_Y

//metering focus mask
int _MeteringMask;

float GetHistogramFromLuminance(float value, float2 scaleOffset) {
	return saturate(log2(value) * scaleOffset.x + scaleOffset.y);
}

float GetLuminanceFromHistogramBin(float bin, float2 scaleOffset) {
	return exp2((bin - scaleOffset.y) / scaleOffset.x);
}

float GetBinValue(StructuredBuffer<uint> buffer, uint index, float maxHistogramValue) {
	return float(buffer[index]) * maxHistogramValue;
}

float FindMaxHistogramValue(StructuredBuffer<uint> buffer) {
    uint maxValue = 0u;
    for (uint i = 0; i < HISTOGRAM_BINS; i++) {
        uint h = buffer[i];
        maxValue = max(maxValue, h);
    }
    return float(maxValue);
}

void FilterLuminance(StructuredBuffer<uint> buffer, uint i, float maxHistogramValue, float2 scaleOffset, inout float4 filter) {
    float binValue = GetBinValue(buffer, i, maxHistogramValue);
    //filter dark areas
    float offset = min(filter.z, binValue);
    binValue -= offset;
    filter.zw -= offset.xx;
    //filter highlights
    binValue = min(filter.w, binValue);
    filter.w -= binValue;
    //luminance at the bin
    float luminance = GetLuminanceFromHistogramBin(float(i) / float(HISTOGRAM_BINS), scaleOffset);
    filter.xy += float2(luminance * binValue, binValue);
}

float GetAverageLuminance(StructuredBuffer<uint> buffer, float4 params, float maxHistogramValue, float2 scaleOffset) {
    //sum of all bins
    uint i;
    float totalSum = 0.0;
    UNITY_UNROLL
        for (i = 0; i < HISTOGRAM_BINS; i++)
            totalSum += GetBinValue(buffer, i, maxHistogramValue);
    //skip darker and lighter parts of the histogram to stabilize the auto exposure
    //x: filtered sum
    //y: accumulator
    //zw: fractions
    float4 filter = float4(0.0, 0.0, totalSum * params.xy);
    UNITY_UNROLL
        for (i = 0; i < HISTOGRAM_BINS; i++)
            FilterLuminance(buffer, i, maxHistogramValue, scaleOffset, filter);
    //clamp to user brightness range
    return clamp(filter.x / max(filter.y, 0.00001), params.z, params.w);
}