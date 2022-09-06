#ifndef MELODY_MOTION_BLUR_PASS_INCLUDED
#define MELODY_MOTION_BLUR_PASS_INCLUDED

TEXTURE2D(_MainTex);
float4 _MainTex_TexelSize;
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
half _LoopCount;

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

//returns the largest vector of v1 and v2.
float2 VMax(float2 v1, float2 v2) {
    return dot(v1, v1) < dot(v2, v2) ? v2 : v1;
}

//Velocity texture setup
float4 VelocitySetup(Imag input) : SV_Target{
    //sample the motion vector.
    float2 v = SAMPLE_TEXTURE2D(_CameraMotionVectorTexture, sampler_linear_clamp, input.screenUV).rg;
    //apply the exposure time and convert to the pixel space.
    v *= (_VelocityScale * 0.5) * _CameraMotionVectorTexture_TexelSize.zw;
    //clamp the vector with the maximum blur radius.
    v /= max(1, length(v) * _RcpMaxBlurRadius);
    //sample the depth of the pixel.
    float d = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_point_clamp, input.screenUV), _ZBufferParams);
    //pack into 10/10/10/2 format.
    return float4((v * _RcpMaxBlurRadius + 1) / 2, d, 0);
}

//returns true or false with a given interval.
bool Interval(half phase, half interval) {
    return frac(phase / interval) > 0.499;
}

//interleaved gradient function from Jimenez 2014 http://goo.gl/eomGso
float GradientNoise(float2 uv) {
    uv = floor((uv + _Time.y) * _ScreenParams.xy);
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
    float3 v = SAMPLE_TEXTURE2D(_CameraMotionVectorTexture, sampler_linear_clamp, uv).xyz;
    return  float3((v.xy * 2 - 1) * _MaxBlurRadius, v.z);
}



#endif
