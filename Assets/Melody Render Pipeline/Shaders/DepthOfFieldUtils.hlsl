#ifndef MELODY_DEPTH_OF_FIELD_UTILS_INCLUDED
#define MELODY_DEPTH_OF_FIELD_UTILS_INCLUDED

//input textures
TEXTURE2D(_InputTexture);
TEXTURE2D(_InputCoCTexture);
TEXTURE2D(_MinMaxTile);

#define FAST_INFOCUS_TILE 0
#define SLOW_INFOCUS_TILE 1
#define FAST_DEFOCUS_TILE 2
#define TILE_RES  8u
//a set of Defines to fine-tune the algorithm
#define ADAPTIVE_SAMPLING
#define STRATIFY
#define RING_OCCLUSION
#define PER_TILE_BG_FG_CLASSIFICATION
#define PHYSICAL_WEIGHTS
#define FORCE_POINT_SAMPLING

//random number generator
#define XOR_SHIFT   1
#define RNG_METHOD XOR_SHIFT
#define BN_RAND_BOUNCE 4
#define BN_RAND_OFFSET 5
#define RngStateType uint

//converts unsigned integer into float int range <0; 1) by using 23 most significant bits for mantissa
float UintToNormalizedFloat(uint x) {
    return asfloat(0x3f800000 | (x >> 9)) - 1.0f;
}

RngStateType InitRNG(uint2 launchIndex, uint frameIndex, uint sampleIndex, uint sampleCount) {
    frameIndex = frameIndex * sampleCount + sampleIndex;

    RngStateType seed = dot(launchIndex, uint2(1, 1280)) ^ JenkinsHash(frameIndex);
    return JenkinsHash(seed);
}

float RandomFloat01(inout RngStateType state, uint dimension) {
    return UintToNormalizedFloat(XorShift(state));
}

//gather utils
#define NumRings            _Params1.x
#define MaxCoCRadius        _Params1.y
#define MaxCoCMipLevel      _Params2.x
#define MaxColorMip         _Params2.y
#define OneOverResScale     _Params2.z
#define ResScale            _Params2.w
#define Anamorphism         _Params3.x
#define NGonFactor          _Params3.y
#define BladeCount          _Params3.z
#define Rotation            _Params3.w

//accumData contain each bucket which accumulates Color, CoC and weight, starting from largest gather one ring of samples at a time
struct AccumData {
    float3 color;
    float CoC;
    float weight;
    float alpha;
    float blendFactor;
};

struct DoFTile {
    float maxRadius;
    float layerBorder;
    int numSamples;
};

//data of per sample of rings
struct SampleData {
    float4 color;
    float CoC;
};

float GetSampleWeight(float cocRadius) {
#if defined(PHYSICAL_WEIGHTS)
    //√2/2
    float pixelRadius = 0.7071f;
    float radius = max(pixelRadius, abs(cocRadius));
    return pixelRadius * pixelRadius * rcp(radius * radius);
#else
    return 1.0f;
#endif
}

float GetCoCRadius(int2 positionSS) {
    float CoCRadius = LOAD_TEXTURE2D_LOD(_InputCoCTexture, ResScale * positionSS, 0).x;
    return CoCRadius * OneOverResScale;
}

float4 GetColorSample(float2 texelCoord, float lod) {
#if defined(FORCE_POINT_SAMPLING)
    float texelsToClamp = (1u << (uint)ceil(ResScale - 1.0)) + 1;
    float2 uv = min(ResScale * (texelCoord + 0.5) * _CameraBufferSize.xy, 1.0 - _CameraBufferSize.xy * texelsToClamp);
    return SAMPLE_TEXTURE2D_LOD(_InputTexture, sampler_point_clamp, uv, ResScale - 1.0);
#else
    float texelsToClamp = (1u << (uint)ceil(lod)) + 1;
    float2 uv = min(ResScale * (texelCoord + 0.5) * _CameraBufferSize.xy, 1.0 - _CameraBufferSize.xy * texelsToClamp);
    //trilinear sampling can introduce some "leaking" between in-focus and out-of-focus regions
    return SAMPLE_TEXTURE2D_LOD(_InputTexture, sampler_trilinear_clamp, uv, lod);
#endif
}

void LoadTileData(float2 texelCoord, SampleData centerSample, float rings, inout DoFTile tileData) {
    float4 cocRanges = LOAD_TEXTURE2D_LOD(_MinMaxTile, ResScale * texelCoord / TILE_RES, 0);
    //NOTE : for the far-field, we don't need to search further than the central CoC, if there is a larger CoC that overlaps the central pixel then it will have greater depth
    tileData.maxRadius = max(2 * abs(centerSample.CoC), -cocRanges.w) * OneOverResScale;
    //detect tiles than need more samples
    tileData.numSamples = rings;
    tileData.numSamples = tileData.maxRadius > 0 ? tileData.numSamples : 0;
#if defined(ADAPTIVE_SAMPLING)
    float minRadius = min(cocRanges.x, -cocRanges.z) * OneOverResScale;
    tileData.numSamples = (minRadius / tileData.maxRadius < 0.1) ? tileData.numSamples * 4 : tileData.numSamples;
#endif
    //by default split the fg and bg layers at 0
    tileData.layerBorder = 0;
#if defined(PER_TILE_BG_FG_CLASSIFICATION)
    if (cocRanges.w != 0 && cocRanges.y == 0) {
        //if there is no far field, then compute a splitting threshold that puts fg and bg in the near field
        //don't want any layers that span both the near and far field (CoC < 0 & CoC > 0)
        tileData.layerBorder = (cocRanges.z + cocRanges.w) / 2;
    }
#endif
}

float2 PointOnNGon(float phi) {
	//transform to rotated ngon
	//from "Cry Engine 3 Graphics Gem"
	float n = BladeCount;
	float nt = cos(PI / n);
	float dt = cos(phi - (Two_PI / n) * floor((n * phi + PI) / Two_PI));
	float rNGon = PositivePow(nt / dt, NGonFactor);
	float u = rNGon * cos(phi - Rotation);
	float v = rNGon * sin(phi - Rotation);
	u *= 1.0 - Anamorphism;
	v *= 1.0 + Anamorphism;
	return float2(u, v);
}

void ResolveColorAndAlpha(inout float4 outColor, inout float outAlpha, float4 defaultValue) {
    outColor.xyz = outColor.w > 0 ? outColor.xyz / outColor.w : defaultValue.xyz;
#if defined(ENABLE_ALPHA)
    outAlpha = outColor.w > 0 ? outAlpha / outColor.w : defaultValue.w;
#endif
}

//accumlate sample data to a accumData struct
void AccumulateSampleToRing(SampleData sampleData, float weight, inout AccumData accumData) {
    accumData.color += sampleData.color.xyz * weight;
    accumData.CoC += abs(sampleData.CoC) * weight;
    accumData.weight += weight;
#if defined(ENABLE_ALPHA)
    accumData.alpha += sampleData.color.w * weight;
#endif
}

void AccumulateCenterSample(SampleData centerSample, inout AccumData accumData) {
    float centerWeight = GetSampleWeight(centerSample.CoC);
    accumData.color = accumData.color * (1 - centerWeight) + centerWeight * centerSample.color.xyz;
    accumData.weight = accumData.weight * (1 - centerWeight) + centerWeight;
#if defined(ENABLE_ALPHA)
    accumData.alpha = accumData.alpha * (1 - centerWeight) + centerWeight * centerSample.color.w;
#endif
}

//accumlate data of 2 samples of a ring
void AccumulateRingData(SampleData sampleData[2], SampleData centerSample, float sampleRadius, float borderRadius, float layerBorder, const bool isForeground, inout AccumData accumData, inout AccumData ringAccum) {
    [unroll]
    for (int k = 0; k < 2; k++) {
        //saturate allows a small overlap between the layers, this helps conceal any continuity artifacts due to differences in sorting
        float w = saturate(sampleData[k].CoC - layerBorder);
        float layerWeight = isForeground ? 1.0 - w : w;
        float CoC = abs(sampleData[k].CoC);
        float sampleWeight = GetSampleWeight(CoC);
        float visibility = step(0.0, CoC - sampleRadius);
        //check if the sample belongs to the current bucket
        float borderWeight = saturate(CoC - borderRadius);
#ifndef RING_OCCLUSION
        //maybe artifacts in NGon
        borderWeight = 0;
#endif
        float weight = layerWeight * visibility * sampleWeight;
        AccumulateSampleToRing(sampleData[k], borderWeight * weight, accumData);
        AccumulateSampleToRing(sampleData[k], (1.0 - borderWeight) * weight, ringAccum);
    }
}

//bucketData is previous accumeulated data and ringData is the current bucketData
void AccumulateBucketData(float numSamples, const bool isNearField, AccumData ringData, inout AccumData bucketData) {
    if (ringData.weight == 0) {
        // nothing to accumulate
        return;
    }
    float currAvgCoC = ringData.weight > 0 ? ringData.CoC * rcp(ringData.weight) : 0;
    float prevAvgCoC = bucketData.weight > 0 ? bucketData.CoC * rcp(bucketData.weight) : 0;
    float occlusionCoC = saturate(prevAvgCoC - currAvgCoC);
    float normCoC = ringData.CoC * rcp(ringData.weight);
    float ringOpacity = saturate(ringData.weight * rcp(GetSampleWeight(normCoC)) * rcp(numSamples));
    //near-field is the region where CoC > 0. In this case sorting is reversed
    if (isNearField) {
        const float occlusionWeight = 0.5;
        occlusionCoC = occlusionWeight * (1 - saturate(currAvgCoC - prevAvgCoC));
        // front-to-back blending
        float blend = 1.0;
#if defined(RING_OCCLUSION)
        blend = bucketData.blendFactor;
#endif
        bucketData.color += blend * ringData.color;
        bucketData.CoC += blend * ringData.CoC;
        bucketData.weight += blend * ringData.weight;
        bucketData.alpha += blend * ringData.alpha;
        bucketData.blendFactor *= saturate(1 - 2 * ringOpacity * occlusionCoC);
    } else {
        // back-to-front blending
        float blend = 0.0;
#if defined(RING_OCCLUSION)
        blend = (bucketData.weight > 0.0) ? ringOpacity * occlusionCoC : 1.0;
#endif
        bucketData.color = bucketData.color * (1.0 - blend) + ringData.color;
        bucketData.CoC = bucketData.CoC * (1.0 - blend) + ringData.CoC;
        bucketData.weight = bucketData.weight * (1.0 - blend) + ringData.CoC;
        bucketData.alpha = bucketData.alpha * (1.0 - blend) + ringData.alpha;
    }
}

void DoFGatherRings(PositionInputs posInputs, DoFTile tileData, SampleData centerSample, out float4 color, out float alpha) {
    AccumData bgAccumData, fgAccumData;
    ZERO_INITIALIZE(AccumData, bgAccumData);
    ZERO_INITIALIZE(AccumData, fgAccumData);
    //layers in the near field are using front-to-back accumulation (so start with a dest alpha of 1, ignored if in the far field)
    fgAccumData.blendFactor = 1;
    bgAccumData.blendFactor = 1;

}

#endif

