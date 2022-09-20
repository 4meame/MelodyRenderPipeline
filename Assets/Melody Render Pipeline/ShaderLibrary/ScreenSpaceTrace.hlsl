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

//refer to GPU Pro 5
//---------------------------Hierarchical Z trace--------------------------//
    //due to perspective, depth buffer valus are linear in screen space and then it is ok to do interpolate
float3 intersectDepthPlane(float3 rayOrigin, float3 rayDir, float marchSize) {
    //march size is a funtion of Hiz buffer
    return rayOrigin + rayDir * marchSize;
}

//returns the 2D integer index of the cell that contains the given 2D position within it
float2 cell(float2 ray, float2 cellCount) {
    return floor(ray * cellCount);
}

//cell count is just the resolution of the Hiz texture at the specific mip level 
float2 cellCount(float mipLevel, float2 bufferSize) {
    return bufferSize / (mipLevel == 0 ? 1 : exp2(mipLevel));
}

float3 intersectCellBoundary(float3 rayOrigin, float3 rayDir, float2 cellIndex, float2 cellCount, float2 crossStep, float2 crossOffset) {
    //by dividing the cell index by cell count, we can get the position of the boundaries between the current cell and the new cell
    float2 cellSize = 1.0 / cellCount;
    //crossStep is added to the current cell to get the next cell index, crossOffset is used to push the position just a tiny bit further to make sure the new position is not right on the boundary
    float2 planes = cellIndex / cellCount + cellSize * crossStep;
    //the delta between the new position and the origin is calculated. The delta is divided by xy component of d vector, after division, the x and y component in delta will have value between 0 to 1 which represents how far the delta position is from the origin of the ray
    float2 solutions = (planes - rayOrigin) / rayDir.xy;
    float3 intersectionPos = intersectDepthPlane(rayOrigin, rayDir, min(solutions.x, solutions.y));
    intersectionPos.xy += (solutions.x < solutions.y) ? float2(crossOffset.x, 0.0) : float2(0.0, crossOffset.y);
    return intersectionPos;
}

//if the new id is different from the old id ,we know we crossed a cell
bool crossedCellBoundary(float2 cellIdOne, float2 cellIdTwo) {
    return (int)cellIdOne.x != (int)cellIdTwo.x || (int)cellIdOne.y != (int)cellIdTwo.y;
}

float minimumDepthPlane(float2 ray, float mipLevel, float2 cellCount, Texture2D SceneDepth) {
    return -SceneDepth.Load(int3((ray * cellCount), mipLevel));
}

float4 HierarchicalZTrace(int HizMaxLevel, int HizStartLevel, int HizStopLevel, int numSteps, float thickness, bool traceBehind, float threshold, float2 bufferSize, float3 rayOrigin, float3 rayDir, Texture2D sceneDepth) {
    HizMaxLevel = clamp(HizMaxLevel, 0, 7);
    rayOrigin.z *= -1;
    rayDir.z *= -1;
    float mipLevel = HizStartLevel;
    float3 ray = rayOrigin;
    //get the cell cross direction and a small offset to enter the next cell when doing cell cross
    float2 crossStep = float2(rayDir.x >= 0.0 ? 1.0 : -1.0, rayDir.y >= 0.0 ? 1.0 : -1.0);
    float2 crossOffset = crossStep * 0.00001;
    crossStep = saturate(crossStep);
    float2 HizSize = cellCount(mipLevel, bufferSize);
    //cross to next cell so that we don't get a self-intersection immediately
    float2 rayCell = cell(ray.xy, HizSize);
    ray = intersectCellBoundary(ray, rayDir, rayCell, HizSize, crossStep, crossOffset);
    int iterations = 0;
    float mask = 1.0;
    while (mipLevel >= HizStopLevel && iterations < numSteps) {
        float3 tempRay = ray;
        //get the cell number of the current ray
        float2 currentCellCount = cellCount(mipLevel, bufferSize);
        float2 oldCellId = cell(ray.xy, currentCellCount);
        //get the minimum depth plane in which the current ray
        float minZ = minimumDepthPlane(ray.xy, mipLevel, currentCellCount, sceneDepth);
        if (rayDir.z > 0) {
            //compare min ray with current ray pos
            float minMinusRay = minZ - ray.z;
            tempRay = minMinusRay > 0 ? ray + (rayDir / rayDir.z) * minMinusRay : tempRay;
            float2 newCellId = cell(tempRay, currentCellCount);
            if (crossedCellBoundary(oldCellId, newCellId)) {
                //so intersect the boundary of that cell instead, and go up a level for taking a larger step next loop
                tempRay = intersectCellBoundary(ray, rayDir, oldCellId, currentCellCount, crossStep, crossOffset);
                mipLevel = min(HizMaxLevel, mipLevel + 2.0);
            } else {
                if (mipLevel == HizStartLevel && abs(minMinusRay) > threshold && traceBehind) {
                    tempRay = intersectCellBoundary(ray, rayDir, oldCellId, currentCellCount, crossStep, crossOffset);
                    mipLevel = HizStartLevel + 1;
                }
            }
        }
        else if (ray.z < minZ) {
            tempRay = intersectCellBoundary(ray, rayDir, oldCellId, currentCellCount, crossStep, crossOffset);
            mipLevel = min(HizMaxLevel, mipLevel + 2.0);
        }
        ray = tempRay;
        //go down a level in Hiz
        mipLevel--;
        iterations++;
        mask = (-LinearEyeDepth(-minZ, _ZBufferParams)) - (-LinearEyeDepth(-ray.z, _ZBufferParams)) < thickness && iterations > 0.0;
    }
    return float4(ray.xy, -ray.z, mask);
}

#endif