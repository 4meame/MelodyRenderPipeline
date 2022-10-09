#ifndef MELODY_DEPTH_OF_FIELD_COMMON_INCLUDED
#define MELODY_DEPTH_OF_FIELD_COMMON_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

//common parameters
float4 _Params1;
float4 _Params2;

//pack methods
uint PackKernelCoord(float2 coords) {
    return uint(f32tof16(coords.x) | f32tof16(coords.y) << 16);
}

float2 UnpackKernelCoord(StructuredBuffer<uint> kernel, uint id) {
    uint coord = kernel[id];
    return float2(f16tof32(coord), f16tof32(coord >> 16));
}

#endif