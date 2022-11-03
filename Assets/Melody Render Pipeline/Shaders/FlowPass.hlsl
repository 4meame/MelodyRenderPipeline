#ifndef MELODY_FLOW_PASS_INCLUDED
#define MELODY_FLOW_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

//flow
TEXTURE2D(_FlowMap);
SAMPLER(sampler_FlowMap);
TEXTURE2D(_DerivHeightMap);
SAMPLER(sampler_DerivHeightMap);

float3 FlowUV(float2 uv, float2 flowVector, float2 jump, float flowOffset, float tilling, float time, bool flowB) {
	//shifting the phase of B by half its period
	float phaseOffset = flowB ? 0.5 : 0;
	//avoid anim factor going too high as time gone, so use "frac"
	float progress = frac(time + phaseOffset);
	float3 uvw;
	uvw.xy = uv - flowVector * (progress + flowOffset);
	uvw.xy *= tilling;
	uvw.xy += phaseOffset;
	uvw.xy += (time - progress) * jump;
	//add a blend weight to the output to fade transistion
	uvw.z = 1 - abs(1 - 2 * progress);
	return uvw;
}

float2 DirectionalFlowUV(float2 uv, float2 flowVector, float flowSpeed, float tilling, float time, out float2x2 rotation) {
	float2 dir = normalize(flowVector.xy);
	rotation = float2x2(dir.y, dir.x, -dir.x, dir.y);
	uv = mul(float2x2(dir.y, -dir.x, dir.x, dir.y), uv);
	uv.y -= time * flowSpeed;
	return uv * tilling;
}

float3 FlowCell(float2 uv, float2 offset, float gridResolution, float flowStrength, float heightScaleModulated, float heightScale, float tilling, float tilingModulated, float time, bool gridB) {
	float2 shift = 1 - offset;
	shift *= 0.5;
	offset *= 0.5;
	if (gridB) {
		shift -= 0.25;
		offset += 0.25;
	}
	float2x2 derivRotation;
	float2 uvTiled = (floor(uv * gridResolution + offset) + shift) / gridResolution;
	float3 flow = SAMPLE_TEXTURE2D(_FlowMap, sampler_FlowMap, uvTiled);
	float2 flowVector = flow.xy * 2 - 1;
	flowVector *= flowStrength;
	float flowSpeed = length(flowVector);
	tilling += flowSpeed * tilingModulated;
	float2 uvFlow = DirectionalFlowUV(uv + offset, flowVector, flowSpeed, tilling, time, derivRotation);
	float3 dh = UnpackDerivativeHeight(SAMPLE_TEXTURE2D(_DerivHeightMap, sampler_DerivHeightMap, uvFlow));
	dh.xy = mul(derivRotation, dh.xy);
	dh *= flowSpeed * heightScaleModulated + heightScale;
	return dh;
}

float3 FlowGrid(float2 uv, float gridResolution, float flowStrength, float heightScaleModulated, float heightScale ,float tilling, float tilingModulated, float time, bool gridB) {
	float3 dhA = FlowCell(uv, float2(0, 0), gridResolution, flowStrength, heightScaleModulated, heightScale, tilling, tilingModulated, time, gridB);
	float3 dhB = FlowCell(uv, float2(1, 0), gridResolution, flowStrength, heightScaleModulated, heightScale, tilling, tilingModulated, time, gridB);
	float3 dhC = FlowCell(uv, float2(0, 1), gridResolution, flowStrength, heightScaleModulated, heightScale, tilling, tilingModulated, time, gridB);
	float3 dhD = FlowCell(uv, float2(1, 1), gridResolution, flowStrength, heightScaleModulated, heightScale, tilling, tilingModulated, time, gridB);
	float2 t = uv * gridResolution;
	if (gridB) {
		t += 0.25;
	}
	t = abs(2 * frac(t) - 1);
	float wA = (1 - t.x) * (1 - t.y);
	float wB = t.x * (1 - t.y);
	float wC = (1 - t.x) * t.y;
	float wD = t.x * t.y;
	return wA * dhA + wB * dhB + wC * dhC + wD * dhD;
}

#endif
