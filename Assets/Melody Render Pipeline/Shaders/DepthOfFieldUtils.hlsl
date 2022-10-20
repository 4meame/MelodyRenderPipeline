#ifndef MELODY_DEPTH_OF_FIELD_UTILS_INCLUDED
#define MELODY_DEPTH_OF_FIELD_UTILS_INCLUDED

// Input textures
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

// Converts unsigned integer into float int range <0; 1) by using 23 most significant bits for mantissa
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

struct AccumData {
    float4 color;
    float alpha;
    float destAlpha;
    float CoC;
};

struct DoFTile {
    float maxRadius;
    float layerBorder;
    int numSamples;
};

struct SampleData {
    float4 color;
    float CoC;
};

float GetSampleWeight(float cocRadius) {
#ifdef PHYSICAL_WEIGHTS
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
    //NOTE : for the far-field, we don't need to search further than than the central CoC, if there is a larger CoC that overlaps the central pixel then it will have greater depth
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
        tileData.layerBorder = (/*cocRanges.z*/ 0 + cocRanges.w) / 2;
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

#endif