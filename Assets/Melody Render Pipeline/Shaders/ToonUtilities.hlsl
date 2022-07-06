#ifndef MELODY_TOON_UTILTIES_INCLUDED
#define MELODY_TOON_UTILTIES_INCLUDED

//just a very simple but low method to get came pov, also can use commandBuffer.SetGlobalFloat("_CurrentCameraFOV",cameraFOV) to get that
float GetCameraFOV() {
	//https://answers.unity.com/questions/770838/how-can-i-extract-the-fov-information-from-the-pro.html
	//float t = unity_CameraProjection._m11;
	//float t = UNITY_MATRIX_P._m11;
	//float Rad2Deg = 180 / 3.1415;
	//float fov = atan(1.0f / t) * 2.0 * Rad2Deg;
	return _CurrentCameraFOV;
}

float ApplyOutlineDistanceFadeOut(float inputMulFix) {
	//make outline "fadeout" if character is too small in camera's view
	return saturate(inputMulFix);
}

float GetOutlineCameraFovAndDistanceFixMultiplier(float positionVS_Z) {
	float cameraMulFix;
	if (unity_OrthoParams.w == 0) {
		//perspective camera
		//keep outline similar width on screen accoss all camera distance       
		cameraMulFix = abs(positionVS_Z);
		//can replace to a tonemap function if a smooth stop is needed
		cameraMulFix = ApplyOutlineDistanceFadeOut(cameraMulFix);
		//keep outline similar width on screen accoss all camera fov
		cameraMulFix *= GetCameraFOV();
	} else {
		//orthographic camera
		float orthoSize = abs(unity_OrthoParams.y);
		orthoSize = ApplyOutlineDistanceFadeOut(orthoSize);
		//50 is a magic number to match perspective camera's outline width
		cameraMulFix = orthoSize * 50; 
	}
	//mul a const to make return result = default normal expand amount WS
	return cameraMulFix * 0.0003;
}

float4 NiloGetNewClipPosWithZOffset(float4 originalPositionCS, float viewSpaceZOffsetAmount) {
	if (unity_OrthoParams.w == 0) {
		//perspective camera
		float2 ProjM_ZRow_ZW = UNITY_MATRIX_P[2].zw;
		//push imaginary vertex
		float modifiedPositionVS_Z = -originalPositionCS.w + -viewSpaceZOffsetAmount;
		float modifiedPositionCS_Z = modifiedPositionVS_Z * ProjM_ZRow_ZW[0] + ProjM_ZRow_ZW[1];
		//overwrite positionCS.z
		originalPositionCS.z = modifiedPositionCS_Z * originalPositionCS.w / (-modifiedPositionVS_Z);
		return originalPositionCS;
	} else {
		//orthographic camera
		//push imaginary vertex and overwrite positionCS.z
		originalPositionCS.z += -viewSpaceZOffsetAmount / _ProjectionParams.z;
		return originalPositionCS;
	}
}

//just like smoothstep(), but linear, not clamped
float invLerp(float from, float to, float value) {
	return (value - from) / (to - from);
}

float invLerpClamp(float from, float to, float value) {
	return saturate(invLerp(from, to, value));
}

//full control remap, but slower
float remap(float origFrom, float origTo, float targetFrom, float targetTo, float value) {
	float rel = invLerp(origFrom, origTo, value);
	return lerp(targetFrom, targetTo, rel);
}

float3 TransformPositionWSToOutlinePositionWS(float3 positionWS, float positionVS_Z, float3 normalWS, float outlineWidth) {
	//you can replace it to your own method! Here we will write a simple world space method for tutorial reason, it is not the best method
	float outlineExpandAmount = outlineWidth * GetOutlineCameraFovAndDistanceFixMultiplier(positionVS_Z);
	return positionWS + normalWS * outlineExpandAmount;
}

float2 TransformPositionCSToOutlinePositionCS(float4 positionCS, float3 normalCS, float outlineWidth) {
	float outlineExpandAmount = outlineWidth * positionCS.w * 2.0;
	//x,y components in clip space correspond to the vertex's horizontal and vertical placement
	//by perspective division, larger w values cause psotions move closer to center of the screen thus appear smaller and farther way
	return positionCS.xy + normalize(normalCS.xy) / _ScreenParams.xy * outlineExpandAmount;
}

float2 RotateUV(float2 _uv, float _radian, float2 _piv, float _time) {
	float RotateUV_ang = _radian;
	float RotateUV_cos = cos(_time * RotateUV_ang);
	float RotateUV_sin = sin(_time * RotateUV_ang);
	return (mul(_uv - _piv, float2x2(RotateUV_cos, -RotateUV_sin, RotateUV_sin, RotateUV_cos)) + _piv);
}

//just a simple version to sample lightprobes
float3 ShadeSH9(float3 normalWS) {
	float4 coefficients[7];
	coefficients[0] = unity_SHAr;
	coefficients[1] = unity_SHAg;
	coefficients[2] = unity_SHAb;
	coefficients[3] = unity_SHBr;
	coefficients[4] = unity_SHBg;
	coefficients[5] = unity_SHBb;
	coefficients[6] = unity_SHC;
	return max(0.0, SampleSH9(coefficients, normalWS));
}

//linear lerp
float LerpFormula(float x, float x1, float x2, float y1, float y2) {
	return y1 + (x - x1) * (y2 - y1) / (x2 - x1);
}

float4 TransformHClipToViewPortPos(float4 positionCS)
{
	float4 o = positionCS * 0.5f;
	o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
	o.zw = positionCS.zw;
	return o / o.w;
}

#endif
