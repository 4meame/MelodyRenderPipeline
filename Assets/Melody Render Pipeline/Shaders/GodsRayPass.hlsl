#ifndef MELODY_GODSRAY_PASS_INCLUDED
#define MELODY_GODSRAY_PASS_INCLUDED



float4 GodsRayPassFragment(Varyings input) : SV_TARGET{
	float4 sceneColor = GetColor(input.screenUV);
	float sceneDepth = GetDepth(input.screenUV);
	//calculate linear depth
	sceneDepth = IsOrthographicCamera() ? OrthographicDepthBufferToLinear(sceneDepth) : LinearEyeDepth(sceneDepth, _ZBufferParams);
	//setup a mask that is 1 at the edges of the screen and 0 at the center
	float EdgeMask = 1.0f - input.screenUV.x * (1.0f - input.screenUV.x) * input.screenUV.y * (1.0f - input.screenUV.y) * 8.0f;
	EdgeMask = EdgeMask * EdgeMask * EdgeMask * EdgeMask;

	return float4(sceneDepth.xxx, 1);
}

#endif
