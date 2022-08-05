#ifndef MELODY_UNITY_INPUT_INCLUDED
#define MELODY_UNITY_INPUT_INCLUDED

CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
	float4x4 unity_WorldToObject;
	float4 unity_LODFade;
	real4 unity_WorldTransformParams;
	//per object light
	real4 unity_LightData;
	real4 unity_LightIndices[2];
	//light map ST factors
	float4 unity_LightmapST;
	float4 unity_DynamicLightmapST;
	//light probe
	float4 unity_SHAr;
	float4 unity_SHAg;
	float4 unity_SHAb;
	float4 unity_SHBr;
	float4 unity_SHBg;
	float4 unity_SHBb;
	float4 unity_SHC;
	//light probe proxy volume
	float4 unity_ProbeVolumeParams;
	float4x4 unity_ProbeVolumeWorldToObject;
	float4 unity_ProbeVolumeSizeInv;
	float4 unity_ProbeVolumeMin;
	//unity also baked shadow mask data in the light probe
	float4 unity_ProbesOcclusion;
	float4 unity_SpecCube0_HDR;
CBUFFER_END
	float4x4 unity_MatrixV;
	float4x4 unity_MatrixVP;
	float4x4 glstate_matrix_projection;
	float4x4 unity_CameraProjection;
	float3 _WorldSpaceCameraPos;
	//manual flip the post fx buffer texture, cause some graphics APIs have the texture V coordinate start at the top while others have it start at the bottom
	float4 _ProjectionParams;
	//In case of an orthographic camera its last component will be 1, otherwise it will be zero
	float4 unity_OrthoParams;
	float4 _ScreenParams;
	float4 _ZBufferParams;
#endif