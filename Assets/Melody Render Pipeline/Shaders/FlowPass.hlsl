#ifndef MELODY_FLOW_PASS_INCLUDED
#define MELODY_FLOW_PASS_INCLUDED

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

#endif
