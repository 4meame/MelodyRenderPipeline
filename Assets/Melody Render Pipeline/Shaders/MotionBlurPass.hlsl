#ifndef MELODY_MOTION_BLUR_PASS_INCLUDED
#define MELODY_MOTION_BLUR_PASS_INCLUDED

TEXTURE2D(_MotionBlurSource);
float4 _MotionBlurSource_TexelSize;
TEXTURE2D(_NeighborMaxTex);
float4 _NeighborMaxTex_TexelSize;
TEXTURE2D(_VelocityTex);
float4 _VelocityTex_TexelSize;

float _VelocityScale;
//tileMax filter parameters
int _TileMaxLoop;
float2 _TileMaxOffs;
//max blur radius(in pixels)
float _MaxBlurRadius;
float _RcpMaxBlurRadius;
//filter parameters/coefficients
float _LoopCount;

//history buffer for frame blending
TEXTURE2D(_History1LumaTex);
TEXTURE2D(_History2LumaTex);
TEXTURE2D(_History3LumaTex);
TEXTURE2D(_History4LumaTex);
TEXTURE2D(_History1ChromaTex);
TEXTURE2D(_History2ChromaTex);
TEXTURE2D(_History3ChromaTex);
TEXTURE2D(_History4ChromaTex);

float _History1Weight;
float _History2Weight;
float _History3Weight;
float _History4Weight;

struct Imag {
    float4 positionCS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
};

struct Multitex {
    float4 positionCS : SV_POSITION;
    float2 screenUV0 : VAR_SCREEN_UV0;
    float2 screenUV1 : VAR_SCREEN_UV1;
};

//vertexID is the clockwise index of a triangle : 0,1,2
Imag DefaultPassVertex(uint vertexID : SV_VertexID) {
    Imag output;
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

Multitex MultiTexPassVertex(uint vertexID : SV_VertexID) {
    Multitex output;
    output.positionCS = float4(vertexID <= 1 ? -1.0 : 3.0,
        vertexID == 1 ? 3.0 : -1.0,
        0.0, 1.0);
    output.screenUV0 = float2(vertexID <= 1 ? 0.0 : 2.0,
        vertexID == 1 ? 2.0 : 0.0);
    if (_ProjectionParams.x < 0.0) {
        output.screenUV0.y = 1.0 - output.screenUV0.y;
    }
    output.screenUV1 = output.screenUV0;
    return output;
}

//linearize depth value sampled from the camera depth texture.
float LinearizeDepth(float z) {
    float isOrtho = unity_OrthoParams.w;
    float isPers = 1 - unity_OrthoParams.w;
    z *= _ZBufferParams.x;
    return (1 - isOrtho * z) / (isPers * z + _ZBufferParams.y);
}

//returns the largest vector of v1 and v2.
float2 VMax(float2 v1, float2 v2) {
    return dot(v1, v1) < dot(v2, v2) ? v2 : v1;
}

float3 LinearToGammaSpace(float3 linRGB) {
    linRGB = max(linRGB, float3(0.h, 0.h, 0.h));
    // An almost-perfect approximation from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
    return max(1.055h * pow(linRGB, 0.416666667h) - 0.055h, 0.h);
}

float3 GammaToLinearSpace(float3 sRGB) {
    // Approximate version from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
    return sRGB * (sRGB * (sRGB * 0.305306011h + 0.682171111h) + 0.012522878h);
}


//fragment shader: Velocity texture setup
float4 VelocitySetup(Imag input) : SV_Target{
    //sample the motion vector.
    float2 v = SAMPLE_TEXTURE2D(_CameraMotionVectorTexture, sampler_point_clamp, input.screenUV).rg;
    //apply the exposure time and convert to the pixel space.
    v *= (_VelocityScale * 0.5) * _CameraMotionVectorTexture_TexelSize.zw;
    //clamp the vector with the maximum blur radius.
    v /= max(1, length(v) * _RcpMaxBlurRadius);
    //sample the depth of the pixel.
    float d = LinearizeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_point_clamp, input.screenUV));
    //pack into 10/10/10/2 format.
    return float4((v * _RcpMaxBlurRadius + 1) / 2, d, 0);
}

//fragment shader: TileMax filter (2 pixel width with normalization)
float4 TileMax1(Imag input) : SV_Target {
    float4 d = _MotionBlurSource_TexelSize.xyxy * float4(-0.5, -0.5, 0.5, 0.5);
    float2 v1 = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_point_clamp, input.screenUV + d.xy).rg;
    float2 v2 = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_point_clamp, input.screenUV + d.zy).rg;
    float2 v3 = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_point_clamp, input.screenUV + d.xw).rg;
    float2 v4 = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_point_clamp, input.screenUV + d.zw).rg;
    v1 = (v1 * 2 - 1) * _MaxBlurRadius;
    v2 = (v2 * 2 - 1) * _MaxBlurRadius;
    v3 = (v3 * 2 - 1) * _MaxBlurRadius;
    v4 = (v4 * 2 - 1) * _MaxBlurRadius;
    return float4(VMax(VMax(VMax(v1, v2), v3), v4), 0, 0);
}

//fragment shader: TileMax filter (2 pixel width)
float4 TileMax2(Imag input) : SV_Target{
    float4 d = _MotionBlurSource_TexelSize.xyxy * float4(-0.5, -0.5, 0.5, 0.5);
    float2 v1 = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_point_clamp, input.screenUV + d.xy).rg;
    float2 v2 = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_point_clamp, input.screenUV + d.zy).rg;
    float2 v3 = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_point_clamp, input.screenUV + d.xw).rg;
    float2 v4 = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_point_clamp, input.screenUV + d.zw).rg;
    return float4(VMax(VMax(VMax(v1, v2), v3), v4), 0, 0);
}

//fragment shader: TileMax filter (variable width)
float4 TileMaxV(Imag input) : SV_Target{
    float2 uv0 = input.screenUV + _MotionBlurSource_TexelSize.xy * _TileMaxOffs.xy;
    float2 du = float2(_MotionBlurSource_TexelSize.x, 0);
    float2 dv = float2(0, _MotionBlurSource_TexelSize.y);
    float2 vo = 0;
    [loop]
    for (int ix = 0; ix < _TileMaxLoop; ix++) {
        [loop]
        for (int iy = 0; iy < _TileMaxLoop; iy++) {
            float2 uv = uv0 + du * ix + dv * iy;
            vo = VMax(vo, SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_point_clamp, uv).rg);
        }
    }
    return float4(vo, 0, 0);
}

//fragment shader: NeighborMax filter
float4 NeighborMax(Imag input) : SV_Target{
    //center weight tweak
    const float cw = 1.01f;
    float4 d = _MotionBlurSource_TexelSize.xyxy * float4(1, 1, -1, 0);
    float2 v1 = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_point_clamp, input.screenUV - d.xy).rg;
    float2 v2 = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_point_clamp, input.screenUV - d.wy).rg;
    float2 v3 = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_point_clamp, input.screenUV - d.zy).rg;
    float2 v4 = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_point_clamp, input.screenUV - d.xw).rg;
    float2 v5 = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_point_clamp, input.screenUV).rg * cw;
    float2 v6 = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_point_clamp, input.screenUV + d.xw).rg;
    float2 v7 = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_point_clamp, input.screenUV + d.zy).rg;
    float2 v8 = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_point_clamp, input.screenUV + d.wy).rg;
    float2 v9 = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_point_clamp, input.screenUV + d.xy).rg;
    float2 va = VMax(v1, VMax(v2, v3));
    float2 vb = VMax(v4, VMax(v5, v6));
    float2 vc = VMax(v7, VMax(v8, v9));
    return float4(VMax(va, VMax(vb, vc)) / cw, 0, 0);
}

//returns true or false with a given interval.
bool Interval(float phase, float interval) {
    return frac(phase / interval) > 0.499;
}

//interleaved gradient function from Jimenez 2014 http://goo.gl/eomGso
float GradientNoise(float2 uv) {
    uv = floor((uv + _Time.y) * _CameraBufferSize.zw);
    float f = dot(float2(0.06711056f, 0.00583715f), uv);
    return frac(52.9829189f * frac(f));
}

//jitter function for tile lookup
float2 JitterTile(float2 uv) {
    float rx, ry;
    //output sin and cosine
    sincos(GradientNoise(uv + float2(2, 0)) * PI * 2, ry, rx);
    return float2(rx, ry) * _NeighborMaxTex_TexelSize.xy / 4;
}

float3 SampleVelocity(float2 uv) {
    float3 v = SAMPLE_TEXTURE2D_LOD(_VelocityTex, sampler_point_clamp, uv, 0).xyz;
    return  float3((v.xy * 2 - 1) * _MaxBlurRadius, v.z);
}

//fragment shader: Reconstruction
float4 Reconstruction(Multitex input) : SV_Target {
    //color sample at center point
    float4 c_p = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_linear_clamp, input.screenUV0);
    //velocity/depth sample at center point
    float3 vd_p = SampleVelocity(input.screenUV1);
    float l_v_p = max(length(vd_p.xy), 0.5);
    float rcp_d_p = 1 / vd_p.z;
    //neightborMax vector sample at center point
    float2 v_max = SAMPLE_TEXTURE2D(_NeighborMaxTex, sampler_point_clamp, input.screenUV1 + JitterTile(input.screenUV1));
    float l_v_max = length(v_max);
    float rcp_l_v_max = 1 / l_v_max;
    //earlt exit if neightborMax is too small
    if (l_v_max < 2) {
        return c_p;
    }
    //use v as a secondary sampling direction except when it's too small compared to V_max. This vector is rescaled to be the length of V_max.
    float2 v_alt = (l_v_p * 2 > l_v_max) ? vd_p.xy * (l_v_max / l_v_p) : v_max;
    //determine the sample count
    float sc = floor(min(_LoopCount, l_v_max / 2));
    //loop variables (starts from the outermost sample)
    float dt = 1 / sc;
    float t_offs = (GradientNoise(input.screenUV0) - 0.5) * dt;
    float t = 1 - dt / 2;
    float count = 0;
    //background velocity
    //this is used for tracking the maximum velocity in the background layer.
    float l_v_bg = max(l_v_p, 1);
    //color accumlation
    float4 acc = 0;
    [loop]
    while(t > dt / 4) {
        //sampling direction(switch per 2 samples)
        float2 v_s = Interval(count, 4) ? v_alt : v_max;
        //sampling position(inverted per every sample)
        float t_s = (Interval(count, 2) ? -t : t) + t_offs;
        //distance to sample position
        float l_t = l_v_max * abs(t_s);
        //uv for sample position
        float2 uv0 = input.screenUV0 + v_s * t_s * _MotionBlurSource_TexelSize.xy;
        float2 uv1 = input.screenUV1 + v_s * t_s * _VelocityTex_TexelSize.xy;
        //color sample
        float3 c = SAMPLE_TEXTURE2D_LOD(_MotionBlurSource, sampler_linear_clamp, uv0, 0);
        //velocity/depth sample
        float3 vd = SampleVelocity(uv1);
        //background/Foreground separation mask
        float fg = saturate((vd_p.z - vd.z) * 20 * rcp_d_p);
        //length of the velocity vector
        float l_v = lerp(l_v_bg, length(vd.xy), fg);
        //sample weight
        //(Distance test) * (Spreading out by motion) * (Triangular window)
        float w = saturate(l_v - l_t) / l_v * (1.2 - t);
        //color accumlate, w can be adjusted customlize
        acc += float4(c, 1) * w;
        //update the background velocity.
        l_v_bg = max(l_v_bg, l_v);
        //advance to the next sample.
        t = Interval(count, 2) ? t - dt : t;
        count += 1;
    }
    acc += float4(c_p.rgb, 1) * (1.2 / (l_v_bg * sc * 2));
    return float4(acc.rgb / acc.a, c_p.a);
}

//mrt output struct for the compressor
struct CompressorOutput {
    float4 luma : SV_Target0;
    float4 chroma : SV_Target1;
};

// Frame compression fragment shader
CompressorOutput FrameCompress(Imag input) {
    //screen width
    float sw = _CameraBufferSize.z;
    //pixel width
    float pw = _CameraBufferSize.x;
    //rgb to YCbCr convertion matrix
    const float3 kY = float3(0.299, 0.587, 0.114);
    const float3 kCB = float3(-0.168736, -0.331264, 0.5);
    const float3 kCR = float3(0.5, -0.418688, -0.081312);
    //0: even column, 1: odd column
    float odd = frac(input.screenUV.x * sw * 0.5) > 0.5;
    //calculate UV for chroma componetns.
    //it's between the even and odd columns.
    float2 uv_c = input.screenUV.xy;
    uv_c.x = (floor(uv_c.x * sw * 0.5) * 2 + 1) * pw;
    // Sample the source texture.
    float3 rgb_y = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_linear_clamp, input.screenUV).rgb;
    float3 rgb_c = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_linear_clamp, uv_c).rgb;
#if !UNITY_COLORSPACE_GAMMA
    rgb_y = LinearToGammaSpace(rgb_y);
    rgb_c = LinearToGammaSpace(rgb_c);
#endif
    // Convertion and subsampling
    CompressorOutput output;
    output.luma = dot(kY, rgb_y);
    output.chroma = dot(lerp(kCB, kCR, odd), rgb_c) + 0.5;
    return output;
}

// Sample luma-chroma textures and convert to RGB
float3 DecodeHistory(float2 uvLuma, float2 uvCb, float2 uvCr, TEXTURE2D(lumaTex), TEXTURE2D(chromaTex)) {
    float y = SAMPLE_TEXTURE2D(lumaTex, sampler_linear_clamp, uvLuma).r;
    float cb = SAMPLE_TEXTURE2D(chromaTex, sampler_linear_clamp, uvCb).r - 0.5;
    float cr = SAMPLE_TEXTURE2D(chromaTex, sampler_linear_clamp, uvCr).r - 0.5;
    return y + float3(1.402 * cr, -0.34414 * cb - 0.71414 * cr, 1.772 * cb);
}

float4 FrameBlending(Multitex input) : SV_Target {
    //texture width
    float sw = _MotionBlurSource_TexelSize.z;
    //texel width
    float pw = _MotionBlurSource_TexelSize.x;
    //uv for luma
    float2 uvLuma = input.screenUV1;
    //uv for Cb (even columns)
    float2 uvCb = input.screenUV1;
    uvCb.x = (floor(uvCb.x * sw * 0.5) * 2 + 0.5) * pw;
    //uv for Cr (even columns)
    float2 uvCr = uvCb;
    uvCr.x += pw;
    //sample from the source image
    float4 src = SAMPLE_TEXTURE2D(_MotionBlurSource, sampler_linear_clamp, input.screenUV0);
    //sampling and blending
    #if UNITY_COLORSPACE_GAMMA
    float3 acc = src.rgb;
    #else
    float3 acc = LinearToGammaSpace(src.rgb);
    #endif
    acc += DecodeHistory(uvLuma, uvCb, uvCr, _History1LumaTex, _History1ChromaTex) * _History1Weight;
    acc += DecodeHistory(uvLuma, uvCb, uvCr, _History2LumaTex, _History2ChromaTex) * _History2Weight;
    acc += DecodeHistory(uvLuma, uvCb, uvCr, _History3LumaTex, _History3ChromaTex) * _History3Weight;
    acc += DecodeHistory(uvLuma, uvCb, uvCr, _History4LumaTex, _History4ChromaTex) * _History4Weight;
    acc /= 1 + _History1Weight + _History2Weight + _History3Weight + _History4Weight;
    #if !UNITY_COLORSPACE_GAMMA
    acc = GammaToLinearSpace(acc);
    #endif
    return float4(acc, src.a);
}

#endif
