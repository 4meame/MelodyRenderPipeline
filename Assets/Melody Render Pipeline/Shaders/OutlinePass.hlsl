#ifndef MELODY_OUTLINE_PASS_INCLUDED
#define MELODY_OUTLINE_PASS_INCLUDED

float4x4 _ClipToViewMatrix;
float4 _OutlineColor;
float4 _OutlineParams;
float4 _ThresholdParams;
float _DepthNormalThresholdScale;

struct UVNeighborhood {
	float2 c;
	float2 bl;
	float2 tr;
	float2 br;
	float2 tl;
};

struct ColorNeighborhood {
	float4 c;
	float4 bl;
	float4 tr;
	float4 br;
	float4 tl;
};

struct DepthNeighborhood {
	float c;
	float bl;
	float tr;
	float br;
	float tl;
};

struct NormalNeighborhood {
	float3 c;
	float3 bl;
	float3 tr;
	float3 br;
	float3 tl;
};

UVNeighborhood GetUVNeighborhood(float2 uv) {
	UVNeighborhood UV;
	float halfScaleFloor = floor(_OutlineParams.x * 0.5);
	float halfScaleCeil = ceil(_OutlineParams.x * 0.5);
	UV.c = uv;
	UV.bl = uv - GetSourceTexelSize() * halfScaleFloor;
	UV.tr = uv + GetSourceTexelSize() * halfScaleFloor;
	UV.br = uv + GetSourceTexelSize() * float2(halfScaleCeil, -halfScaleFloor);
	UV.tl = uv + GetSourceTexelSize() * float2(-halfScaleCeil, halfScaleFloor);
	return UV;
}

ColorNeighborhood GetColorNeighborhood(UVNeighborhood uv) {
	ColorNeighborhood color;
	color.c = GetSource(uv.c);
	color.bl = GetSource(uv.bl);
	color.tr = GetSource(uv.tr);
	color.br = GetSource(uv.br);
	color.tl = GetSource(uv.tl);
	return color;
}

DepthNeighborhood GetDepthNeighborhood(UVNeighborhood uv) {
	DepthNeighborhood depth;
	depth.c = GetDepth(uv.c);
	depth.bl = GetDepth(uv.bl);
	depth.tr = GetDepth(uv.tr);
	depth.br = GetDepth(uv.br);
	depth.tl = GetDepth(uv.tl);
	return depth;
}

NormalNeighborhood GetNormalNeighborhood(UVNeighborhood uv) {
	NormalNeighborhood normal;
	normal.c = GetNormal(uv.c);
	normal.bl = GetNormal(uv.bl);
	normal.tr = GetNormal(uv.tr);
	normal.br = GetNormal(uv.br);
	normal.tl = GetNormal(uv.tl);
	return normal;
}

float4 OutlinePassFragment(Varyings input) : SV_TARGET{
	UVNeighborhood uv = GetUVNeighborhood(input.screenUV);
	ColorNeighborhood color = GetColorNeighborhood(uv);
	DepthNeighborhood depth = GetDepthNeighborhood(uv);
	NormalNeighborhood normal = GetNormalNeighborhood(uv);

	//------color edge-------
	float3 colorDifference0 = color.bl - color.tr;
	float3 colorlDifference1 = color.br - color.tl;
	float edgeColor = sqrt(dot(colorDifference0, colorDifference0) + dot(colorlDifference1, colorlDifference1));
	edgeColor = edgeColor > _ThresholdParams.w ? 1 : 0;
	//------------------------

	//------view threshold-----
	float4 positionCS = float4((input.screenUV - 0.5) * 2.0 * float2(1, -1), 0, 1);
	float3 viewDir = normalize(mul(_ClipToViewMatrix, positionCS).xyz);
	float3 viewNormal = normal.c * 2 - 1;
	float NdotV = 1 - dot(-viewDir.xyz, viewNormal);
	float normalThreshold = saturate((NdotV - _ThresholdParams.z) / (1 - _ThresholdParams.z)) * _DepthNormalThresholdScale + 1;
	//-------------------------

	//------depth edge-------
	float depthDifference0 = abs(depth.bl - depth.tr);
	float depthDifference1 = abs(depth.br - depth.tl);
	float edgeDepth = sqrt(pow(depthDifference0, 2) + pow(depthDifference1, 2)) * 100;
	//depth buffer is no-linear, means that the diff of 2 parts near the camera is not equal but larger than the far from the camera, so multi depth value for correction
	float depthThreshold = _ThresholdParams.x * depth.c * normalThreshold;
	edgeDepth = edgeDepth > depthThreshold ? 1 : 0;
	//------------------------

	//------normal edge-------
	float3 normalDifference0 = normal.bl - normal.tr;
	float3 normalDifference1 = normal.br - normal.tl;
	float edgeNormal = sqrt(dot(normalDifference0, normalDifference0) + dot(normalDifference1, normalDifference1));
	edgeNormal = edgeNormal > _ThresholdParams.y ? 1 : 0;
	//------------------------

	//return float4(NdotV.xxx, 1);
	//return float4(color.c.xyz, 1);
	//return float4(depth.c.xxx,1);
	//return float4(normal.c.xyz,1);
	//return float4(edgeColor.xxx, 1);
	//return float4(edgeDepth.xxx, 1);
	//return float4(edgeNormal.xxx, 1);

	float edge = max(edgeColor, max(edgeDepth, edgeNormal));
	float4 result = ((1 - edge) * color.c) + (edge * lerp(color.c, _OutlineColor, _OutlineColor.a));
	return result;
}

#endif
