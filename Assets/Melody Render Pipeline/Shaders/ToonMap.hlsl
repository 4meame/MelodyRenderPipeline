#ifndef MELODY_TOON_MAP_INCLUDED
#define MELODY_TOON_MAP_INCLUDED

struct ToonMap {
	float4 mainMap;
	float4 firstShadeMap;
	float4 secondShadeMap;
	float3 normalMap;
	float4 firstShadePositionMap;
	float4 secondShadePositionMap;
	//high is toon specular area
	float4 highColorMap;
	float4 highMaskMap;
	float4 rimMaskMap;
	float4 matCapMap;
	float3 normalForMatCap;
	float4 matCapMaskMap;
	float4 emissionMap;
	float4 clipMap;
};

#endif