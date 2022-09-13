#ifndef MELODY_SCREEN_SPACE_TRACE_INCLUDED
#define MELODY_SCREEN_SPACE_TRACE_INCLUDED

inline float GetScreenFadeBord(float2 pos, float value) {
    float borderDist = min(1 - max(pos.x, pos.y), min(pos.x, pos.y));
    return saturate(borderDist > value ? 1 : borderDist / value);
}

inline float3 ReconstructCSPosition(float4 _MainTex_TexelSize, float4 _ProjInfo, float2 S, float z) {
    float linEyeZ = -LinearEyeDepth(z, _ZBufferParams);
    return float3((((S.xy * _MainTex_TexelSize.zw)) * _ProjInfo.xy + _ProjInfo.zw) * linEyeZ, linEyeZ);
}

inline float3 GetPosition(TEXTURE2D(depth), float4 _MainTex_TexelSize, float4 _ProjInfo, float2 ssP) {
    float3 P;
    P.z = SAMPLE_DEPTH_TEXTURE(depth, sampler_point_clamp, ssP.xy).r;
    P = ReconstructCSPosition(_MainTex_TexelSize, _ProjInfo, float2(ssP), P.z);
    return P;
}

//---------------------------2D linear trace--------------------------//
inline float distanceSquared(float2 A, float2 B) {
    A -= B;
    return dot(A, A);
}

inline float distanceSquared(float3 A, float3 B) {
    A -= B;
    return dot(A, A);
}

void swap(inout float v0, inout float v1) {
    float temp = v0;
    v0 = v1;
    v1 = temp;
}

bool intersectsDepthBuffer(float rayZMin, float rayZMax, float sceneZ, float layerThickness) {
    return (rayZMax >= sceneZ - layerThickness) && (rayZMin <= sceneZ);
}

void rayIterations(TEXTURE2D(forntDepth), 
    in bool traceBehind_Old, 
    in bool traceBehind, 
    inout float2 P, 
    inout float stepDirection, 
    inout float end, 
    inout int stepCount, 
    inout int maxSteps, 
    inout bool intersecting,
    inout float sceneZ, 
    inout float2 dP, 
    inout float3 Q, 
    inout float3 dQ, 
    inout float k, 
    inout float dk,
    inout float rayZMin, 
    inout float rayZMax, 
    inout float prevZMaxEstimate, 
    inout bool permute, 
    inout float2 hitPixel,
    float2 invSize, 
    inout float layerThickness) {
    bool stop = intersecting;
    for (; (P.x * stepDirection) <= end && stepCount < maxSteps && !stop; P += dP, Q.z += dQ.z, k += dk, stepCount += 1)
    {
        rayZMin = prevZMaxEstimate;
        rayZMax = (dQ.z * 0.5 + Q.z) / (dk * 0.5 + k);
        prevZMaxEstimate = rayZMax;

        if (rayZMin > rayZMax) {
            swap(rayZMin, rayZMax);
        }

        hitPixel = permute ? P.yx : P;
        sceneZ = SAMPLE_TEXTURE2D_LOD(forntDepth, sampler_point_clamp, float2(hitPixel * invSize), 0).r;
        sceneZ = -LinearEyeDepth(sceneZ, _ZBufferParams);
        bool isBehind = (rayZMin <= sceneZ);

        if (traceBehind_Old == 1) {
            intersecting = isBehind && (rayZMax >= sceneZ - layerThickness);
        }
        else {
            intersecting = (rayZMax >= sceneZ - layerThickness);
        }

        stop = traceBehind ? intersecting : isBehind;
    }
    P -= dP, Q.z -= dQ.z, k -= dk;
}

bool Linear2D_Trace(TEXTURE2D(forntDepth),
    float3 csOrigin,
    float3 csDirection,
    float4x4 projectMatrix,
    float2 csZBufferSize,
    float jitter,
    int maxSteps,
    float layerThickness,
    float traceDistance,
    in out float2 hitPixel,
    int stepSize,
    bool traceBehind,
    in out float3 csHitPoint,
    in out float stepCount) {

    float2 invSize = float2(1 / csZBufferSize.x, 1 / csZBufferSize.y);
    hitPixel = float2(-1, -1);

    float nearPlaneZ = -0.01;
    float rayLength = ((csOrigin.z + csDirection.z * traceDistance) > nearPlaneZ) ? ((nearPlaneZ - csOrigin.z) / csDirection.z) : traceDistance;
    float3 csEndPoint = csDirection * rayLength + csOrigin;
    float4 H0 = mul(projectMatrix, float4(csOrigin, 1));
    float4 H1 = mul(projectMatrix, float4(csEndPoint, 1));
    float k0 = 1 / H0.w;
    float k1 = 1 / H1.w;
    float2 P0 = H0.xy * k0;
    float2 P1 = H1.xy * k1;
    float3 Q0 = csOrigin * k0;
    float3 Q1 = csEndPoint * k1;

    float yMax = csZBufferSize.y - 0.5;
    float yMin = 0.5;
    float xMax = csZBufferSize.x - 0.5;
    float xMin = 0.5;
    float alpha = 0;

    if (P1.y > yMax || P1.y < yMin) {
        float yClip = (P1.y > yMax) ? yMax : yMin;
        float yAlpha = (P1.y - yClip) / (P1.y - P0.y);
        alpha = yAlpha;
    }
    if (P1.x > xMax || P1.x < xMin) {
        float xClip = (P1.x > xMax) ? xMax : xMin;
        float xAlpha = (P1.x - xClip) / (P1.x - P0.x);
        alpha = max(alpha, xAlpha);
    }

    P1 = lerp(P1, P0, alpha);
    k1 = lerp(k1, k0, alpha);
    Q1 = lerp(Q1, Q0, alpha);

    P1 = (distanceSquared(P0, P1) < 0.0001) ? P0 + float2(0.01, 0.01) : P1;
    float2 delta = P1 - P0;
    bool permute = false;

    if (abs(delta.x) < abs(delta.y)) {
        permute = true;
        delta = delta.yx;
        P1 = P1.yx;
        P0 = P0.yx;
    }

    float stepDirection = sign(delta.x);
    float invdx = stepDirection / delta.x;
    float2 dP = float2(stepDirection, invdx * delta.y);
    float3 dQ = (Q1 - Q0) * invdx;
    float dk = (k1 - k0) * invdx;

    dP *= stepSize;
    dQ *= stepSize;
    dk *= stepSize;
    P0 += dP * jitter;
    Q0 += dQ * jitter;
    k0 += dk * jitter;

    float3 Q = Q0;
    float k = k0;
    float prevZMaxEstimate = csOrigin.z;
    stepCount = 0;
    float rayZMax = prevZMaxEstimate, rayZMin = prevZMaxEstimate;
    float sceneZ = 100000;
    float end = P1.x * stepDirection;
    bool intersecting = intersectsDepthBuffer(rayZMin, rayZMax, sceneZ, layerThickness);
    float2 P = P0;
    int originalStepCount = 0;

    bool traceBehind_Old = true;
    rayIterations(forntDepth, traceBehind_Old, traceBehind, P, stepDirection, end, originalStepCount, maxSteps, intersecting, sceneZ, dP, Q, dQ, k, dk, rayZMin, rayZMax, prevZMaxEstimate, permute, hitPixel, invSize, layerThickness);

    stepCount = originalStepCount;
    Q.xy += dQ.xy * stepCount;
    csHitPoint = Q * (1 / k);
    return intersecting;
}

#endif