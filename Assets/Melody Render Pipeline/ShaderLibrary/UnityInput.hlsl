#ifndef MELODY_UNITY_INPUT_INCLUDED
#define MELODY_UNITY_INPUT_INCLUDED

#define UNITY_LIGHTMODEL_AMBIENT (glstate_lightmodel_ambient * 2)

CBUFFER_START(UnityPerCamera)
	//T(t = time since current level load) values from Unity
	float4 _Time; // (t/20, t, t*2, t*3)
	float4 _SinTime; // sin(t/8), sin(t/4), sin(t/2), sin(t)
	float4 _CosTime; // cos(t/8), cos(t/4), cos(t/2), cos(t)
	float4 unity_DeltaTime; // dt, 1/dt, smoothdt, 1/smoothdt
#if !defined(USING_STEREO_MATRICES)
	float3 _WorldSpaceCameraPos;
#endif
	// x = 1 or -1 (-1 if projection is flipped)
	// y = near plane
	// z = far plane
	// w = 1/far plane
	//manual flip the post fx buffer texture, cause some graphics APIs have the texture V coordinate start at the top while others have it start at the bottom
	float4 _ProjectionParams;
	// x = width
	// y = height
	// z = 1 + 1.0/width
	// w = 1 + 1.0/height
	float4 _ScreenParams;
	// Values used to linearize the Z buffer (http://www.humus.name/temp/Linearize%20depth.txt)
	// x = 1-far/near
	// y = far/near
	// z = x/far
	// w = y/far
	// or in case of a reversed depth buffer (UNITY_REVERSED_Z is 1)
	// x = -1+far/near
	// y = 1
	// z = x/far
	// w = 1/far
	float4 _ZBufferParams;
	// x = orthographic camera's width
	// y = orthographic camera's height
	// z = unused
	// w = 1.0 if camera is ortho, 0.0 if perspective
	//in case of an orthographic camera its last component will be 1, otherwise it will be zero
	float4 unity_OrthoParams;
CBUFFER_END

CBUFFER_START(UnityPerCameraRare)
    float4 unity_CameraWorldClipPlanes[6];
#if !defined(USING_STEREO_MATRICES)
	//projection matrices of the camera. Note that this might be different from projection matrix that is set right now, e.g. while rendering shadows the matrices below are still the projection of original camera.
	float4x4 unity_CameraProjection;
	float4x4 unity_CameraInvProjection;
	float4x4 unity_WorldToCamera;
	float4x4 unity_CameraToWorld;
#endif
CBUFFER_END

CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
	float4x4 unity_WorldToObject;
	float4 unity_LODFade;// x is the fade value ranging within [0,1]. y is x quantized into 16 levels
	real4 unity_WorldTransformParams;// w is usually 1.0, or -1.0 for odd-negative scale transforms
	//per object light
	//light Indices block feature
	//these are set internally by the engine upon request by RendererConfiguration
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
	//velocity, from HDRP shader library
	float4x4 unity_MatrixPreviousM;
	float4x4 unity_MatrixPreviousMI;
	//x : Use last frame positions (right now skinned meshes are the only objects that use this
	//y : Force No Motion
	//z : Z bias value
	//w : Camera only
	float4 unity_MotionVectorsParams;
CBUFFER_END

CBUFFER_START(UnityPerDrawRare)
    float4x4 glstate_matrix_transpose_modelview0;
CBUFFER_END

CBUFFER_START(UnityPerFrame)
	real4 glstate_lightmodel_ambient;
	real4 unity_AmbientSky;
	real4 unity_AmbientEquator;
	real4 unity_AmbientGround;
	real4 unity_IndirectSpecColor;
	float4 unity_FogParams;
	real4  unity_FogColor;

#if !defined(USING_STEREO_MATRICES)
	float4x4 glstate_matrix_projection;
	float4x4 unity_MatrixV;
	float4x4 unity_MatrixInvV;
	float4x4 unity_MatrixVP;
#endif
    real4 unity_ShadowColor;
CBUFFER_END

//TODO: all affine matrices should be 3x4.
//TODO: sort these vars by the frequency of use (descending), and put commonly used vars together.
//Note: please use UNITY_MATRIX_X macros instead of referencing matrix variables directly.
CBUFFER_START(UnityPerPass)
	float4x4 _PrevViewProjMatrix;
	float4x4 _ViewProjMatrix;
	float4x4 _NonJitteredViewProjMatrix;
	float4x4 _ViewMatrix;
	float4x4 _ProjMatrix;
	float4x4 _InvViewProjMatrix;
	float4x4 _InvViewMatrix;
	float4x4 _InvProjMatrix;
	float4   _InvProjParam;
	//{w, h, 1/w, 1/h}
	float4   _ScreenSize;
	//{(a, b, c) = N, d = -dot(N, P)} [L, R, T, B, N, F]
	float4   _FrustumPlanes[6];
CBUFFER_END

float4x4 OptimizeProjectionMatrix(float4x4 M)
{
	// Matrix format (x = non-constant value).
	// Orthographic Perspective  Combined(OR)
	// | x 0 0 x |  | x 0 x 0 |  | x 0 x x |
	// | 0 x 0 x |  | 0 x x 0 |  | 0 x x x |
	// | x x x x |  | x x x x |  | x x x x | <- oblique projection row
	// | 0 0 0 1 |  | 0 0 x 0 |  | 0 0 x x |
	// Notice that some values are always 0.
	// We can avoid loading and doing math with constants.
	M._21_41 = 0;
	M._12_42 = 0;
	return M;
}

#endif