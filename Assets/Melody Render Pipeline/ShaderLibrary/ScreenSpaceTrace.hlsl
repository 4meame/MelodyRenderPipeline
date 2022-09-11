#ifndef MELODY_SCREEN_SPACE_TRACE_INCLUDED
#define MELODY_SCREEN_SPACE_TRACE_INCLUDED

inline half GetScreenFadeBord(half2 pos, half value) {
    half borderDist = min(1 - max(pos.x, pos.y), min(pos.x, pos.y));
    return saturate(borderDist > value ? 1 : borderDist / value);
}

inline half3 ReconstructCSPosition(half4 _MainTex_TexelSize, half4 _ProjInfo, half2 S, half z) {
    half linEyeZ = -LinearEyeDepth(z, _ZBufferParams);
    return half3((((S.xy * _MainTex_TexelSize.zw)) * _ProjInfo.xy + _ProjInfo.zw) * linEyeZ, linEyeZ);
}

inline half3 GetPosition(TEXTURE2D(depth), half4 _MainTex_TexelSize, half4 _ProjInfo, half2 ssP) {
    half3 P;
    P.z = SAMPLE_DEPTH_TEXTURE(depth, sampler_point_clamp, ssP.xy).r;
    P = ReconstructCSPosition(_MainTex_TexelSize, _ProjInfo, half2(ssP), P.z);
    return P;
}

//2D linear trace
inline half distanceSquared(half2 A, half2 B) {
    A -= B;
    return dot(A, A);
}

inline half distanceSquared(half3 A, half3 B) {
    A -= B;
    return dot(A, A);
}

void swap(inout half v0, inout half v1) {
    half temp = v0;
    v0 = v1;
    v1 = temp;
}

bool intersectsDepthBuffer(half rayZMin, half rayZMax, half sceneZ, half layerThickness) {
    return (rayZMax >= sceneZ - layerThickness) && (rayZMin <= sceneZ);
}

void rayIterations(TEXTURE2D(forntDepth), 
    in bool traceBehind_Old, 
    in bool traceBehind, 
    inout half2 P, 
    inout half stepDirection, 
    inout half end, 
    inout int stepCount, 
    inout int maxSteps, 
    inout bool intersecting,
    inout half sceneZ, 
    inout half2 dP, 
    inout half3 Q, 
    inout half3 dQ, 
    inout half k, 
    inout half dk,
    inout half rayZMin, 
    inout half rayZMax, 
    inout half prevZMaxEstimate, 
    inout bool permute, 
    inout half2 hitPixel,
    half2 invSize, 
    inout half layerThickness) {
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
        sceneZ = SAMPLE_TEXTURE2D_LOD(forntDepth, sampler_point_clamp, half2(hitPixel * invSize), 0).r;
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
    half3 csOrigin,
    half3 csDirection,
    half4x4 projectMatrix,
    half2 csZBufferSize,
    half jitter,
    int maxSteps,
    half layerThickness,
    half traceDistance,
    in out half2 hitPixel,
    int stepSize,
    bool traceBehind,
    in out half3 csHitPoint,
    in out half stepCount) {

    half2 invSize = half2(1 / csZBufferSize.x, 1 / csZBufferSize.y);
    hitPixel = half2(-1, -1);

    half nearPlaneZ = -0.01;
    half rayLength = ((csOrigin.z + csDirection.z * traceDistance) > nearPlaneZ) ? ((nearPlaneZ - csOrigin.z) / csDirection.z) : traceDistance;
    half3 csEndPoint = csDirection * rayLength + csOrigin;
    half4 H0 = mul(projectMatrix, half4(csOrigin, 1));
    half4 H1 = mul(projectMatrix, half4(csEndPoint, 1));
    half k0 = 1 / H0.w;
    half k1 = 1 / H1.w;
    half2 P0 = H0.xy * k0;
    half2 P1 = H1.xy * k1;
    half3 Q0 = csOrigin * k0;
    half3 Q1 = csEndPoint * k1;

    half yMax = csZBufferSize.y - 0.5;
    half yMin = 0.5;
    half xMax = csZBufferSize.x - 0.5;
    half xMin = 0.5;
    half alpha = 0;

    if (P1.y > yMax || P1.y < yMin) {
        half yClip = (P1.y > yMax) ? yMax : yMin;
        half yAlpha = (P1.y - yClip) / (P1.y - P0.y);
        alpha = yAlpha;
    }
    if (P1.x > xMax || P1.x < xMin) {
        half xClip = (P1.x > xMax) ? xMax : xMin;
        half xAlpha = (P1.x - xClip) / (P1.x - P0.x);
        alpha = max(alpha, xAlpha);
    }

    P1 = lerp(P1, P0, alpha);
    k1 = lerp(k1, k0, alpha);
    Q1 = lerp(Q1, Q0, alpha);

    P1 = (distanceSquared(P0, P1) < 0.0001) ? P0 + half2(0.01, 0.01) : P1;
    half2 delta = P1 - P0;
    bool permute = false;

    if (abs(delta.x) < abs(delta.y)) {
        permute = true;
        delta = delta.yx;
        P1 = P1.yx;
        P0 = P0.yx;
    }

    half stepDirection = sign(delta.x);
    half invdx = stepDirection / delta.x;
    half2 dP = half2(stepDirection, invdx * delta.y);
    half3 dQ = (Q1 - Q0) * invdx;
    half dk = (k1 - k0) * invdx;

    dP *= stepSize;
    dQ *= stepSize;
    dk *= stepSize;
    P0 += dP * jitter;
    Q0 += dQ * jitter;
    k0 += dk * jitter;

    half3 Q = Q0;
    half k = k0;
    half prevZMaxEstimate = csOrigin.z;
    stepCount = 0;
    half rayZMax = prevZMaxEstimate, rayZMin = prevZMaxEstimate;
    half sceneZ = 100000;
    half end = P1.x * stepDirection;
    bool intersecting = intersectsDepthBuffer(rayZMin, rayZMax, sceneZ, layerThickness);
    half2 P = P0;
    int originalStepCount = 0;

    bool traceBehind_Old = true;
    rayIterations(forntDepth, traceBehind_Old, traceBehind, P, stepDirection, end, originalStepCount, maxSteps, intersecting, sceneZ, dP, Q, dQ, k, dk, rayZMin, rayZMax, prevZMaxEstimate, permute, hitPixel, invSize, layerThickness);

    stepCount = originalStepCount;
    Q.xy += dQ.xy * stepCount;
    csHitPoint = Q * (1 / k);
    return intersecting;
}

#endif