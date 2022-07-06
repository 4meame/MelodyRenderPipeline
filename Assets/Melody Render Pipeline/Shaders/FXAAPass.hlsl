#ifndef MELODY_FAXX_PASS_INCLUDED
#define MELODY_FAXX_PASS_INCLUDED

#if defined(FXAA_QUALITY_LOW)
	#define EXTRA_EDGE_STEPS 3
	#define EDGE_STEP_SIZES 1.0, 1.0, 1.0
	#define LAST_EDGE_STEP_GUESS 1.0
#elif defined(FXAA_QUALITY_MEDIUM)
	#define EXTRA_EDGE_STEPS 8
	#define EDGE_STEP_SIZES 1.5, 2.0, 2.0, 2.0, 2.0, 2.0, 2.0, 4.0
	#define LAST_EDGE_STEP_GUESS 8.0
#else
	#define EXTRA_EDGE_STEPS 12
	#define EDGE_STEP_SIZES 1.0, 1.0, 1.0, 1.0, 1.0, 1.5, 1.5, 1.5, 1.5, 2.0, 2.0, 4.0
	#define LAST_EDGE_STEP_GUESS 8.0
#endif

static const float edgeStepSizes[EXTRA_EDGE_STEPS] = { EDGE_STEP_SIZES };
float4 _FXAAConfig;

struct LumaNeighborhood {
	float m;
	float n;
	float s;
	float w;
	float e;
	float highest, lowest, range;
	float nw, ne, sw, se;
};

//only concerning the perceived brightness to get hard transitions between different colors
float GetLuma(float2 uv, float uOffset = 0.0, float vOffset = 0.0) {
	uv += float2(uOffset, vOffset) * GetSourceTexelSize();
	//dark colors are more sensitive for our eyes, so sqrt the results to balance brightness and darkness
#if defined(FXAA_ALPHA_CONTAINS_LUMA)
	return GetSource(uv).a;
	//a cheap way to substitute luma2, can avoid dot product and square root operations
#else
	return GetSource(uv).g;
#endif
}

LumaNeighborhood GetLumaNeighborhood(float2 uv) {
	LumaNeighborhood luma;
	luma.m = GetLuma(uv);
	luma.n = GetLuma(uv, 0.0, 1.0);
	luma.s = GetLuma(uv, 0.0, -1.0);
	luma.w = GetLuma(uv, -1.0, 0.0);
	luma.e = GetLuma(uv, 1.0, 0.0);
	luma.highest = max(max(max(max(luma.m, luma.n), luma.e), luma.s), luma.w);
	luma.lowest = min(min(min(min(luma.m, luma.n), luma.e), luma.s), luma.w);
	luma.range = luma.highest - luma.lowest;
	//improve filter quality
	luma.nw = GetLuma(uv, -1.0, 1.0);
	luma.ne = GetLuma(uv, 1.0, 1.0);
	luma.sw = GetLuma(uv, -1.0, -1.0);
	luma.se = GetLuma(uv, 1.0, -1.0);
	return luma;
}

bool CanSkipFXAA(LumaNeighborhood luma) {
	// take 2 factors into account
	return luma.range < max(_FXAAConfig.x, _FXAAConfig.y * luma.highest);
}

float GetSubpixelBlendFactor(LumaNeighborhood luma) {
	float filter = 2.0 * (luma.n + luma.s + luma.e + luma.e);
	filter += luma.nw + luma.ne + luma.sw + luma.se;
	//low-pass filter
	filter *= 1.0 / 12.0;
	//high-pass filter(do not know it is necessary
	filter = abs(filter - luma.m);
	////normalized filter
	//filter = filter / luma.range;
	//highest value doesn't have store diagonal samples, saturate it avoid negative value
	filter = saturate(filter / luma.range);
	//smoothed filter
	filter = smoothstep(0, 1, filter);
	return filter * filter * _FXAAConfig.z;
}

//determin the blend direction by calculating the contrast gradient: difference of the horizontal or vertical
bool IsHorizontalEdge(LumaNeighborhood luma) {
	//take diagonal samples into account
	float horizontal = 2.0 * abs(luma.m + luma.s - 2.0 * luma.m) + abs(luma.ne + luma.se - 2.0 * luma.e) + abs(luma.nw + luma.sw - 2.0 * luma.w);
	float vertical = 2.0 * abs(luma.w + luma.e - 2.0 * luma.m) + abs(luma.nw + luma.ne - 2.0 * luma.n) + abs(luma.sw + luma.se - 2.0 * luma.s);
	return horizontal >= vertical;
}

struct FXAAEdge {
	bool isHorizontal;
	//size of a blend pixel step
	float pixelStep;
	//gradient of luma and the gradient luma value to identify the edge
	float lumaGradient, otherLuma;
};

FXAAEdge GetFXAAEdge(LumaNeighborhood luma) {
	FXAAEdge edge;
	edge.isHorizontal = IsHorizontalEdge(luma);
	//determine whether we should blend in the positive or negative direction
	float lumaP, lumaN;
	if (edge.isHorizontal) {
		edge.pixelStep = GetSourceTexelSize().y;
		lumaP = luma.n;
		lumaN = luma.s;
	}
	else {
		edge.pixelStep = GetSourceTexelSize().x;
		lumaP = luma.e;
		lumaN = luma.w;
	}
	float gradientP = abs(lumaP - luma.m);
	float gradientN = abs(lumaN - luma.m);
	if (gradientP < gradientN) {
		edge.pixelStep = -edge.pixelStep;
		edge.lumaGradient = gradientN;
		edge.otherLuma = lumaN;
	}
	else {
		edge.lumaGradient = gradientP;
		edge.otherLuma = lumaP;
	}
	return edge;
}

//search through edge pixels for the end of the edge
float GetEdgeBlendFactor(LumaNeighborhood luma, FXAAEdge edge, float2 uv) {
	float2 edgeUV = uv;
	float2 uvStep = 0.0;
	if (edge.isHorizontal) {
		//edge.pixelStep equals to texel size
		edgeUV.y += 0.5 * edge.pixelStep;
		uvStep.x += GetSourceTexelSize().x;
	}
	else {
		edgeUV.x += 0.5 * edge.pixelStep;
		uvStep.y += GetSourceTexelSize().y;
	}
	float edgeLuma = 0.5 * (luma.m + edge.otherLuma);
	float gradientThreshold = 0.25 * edge.lumaGradient;

	//start by a single step at point to positive direction
	float2 uvP = edgeUV + uvStep;
	float lumaDeltaP = GetLuma(uvP) - edgeLuma;
	bool atEndP = abs(lumaDeltaP) >= gradientThreshold;
	for (int i = 0; i < EXTRA_EDGE_STEPS && !atEndP; i++) {
		uvP += edgeStepSizes[i] * uvStep;
		//calculate the delta of the luma of the step point and the edge average value
		lumaDeltaP = GetLuma(uvP) - edgeLuma;
		//if it is larger than the threshold, it is the pixel of the another edge
		atEndP = abs(lumaDeltaP) >= gradientThreshold;
	}
	//if we do not find the end point, guess it is the next point
	if (!atEndP) {
		uvP += LAST_EDGE_STEP_GUESS * uvStep;
	}

	//start by a single step at point to negative direction
	float2 uvN = edgeUV - uvStep;
	float lumaDeltaN = GetLuma(uvN) - edgeLuma;
	bool atEndN = abs(lumaDeltaN) >= gradientThreshold;
	for (int i = 0; i < EXTRA_EDGE_STEPS && !atEndN; i++) {
		uvN -= edgeStepSizes[i] * uvStep;
		//calculate the delta of the luma of the step point and the edge average value
		lumaDeltaN = GetLuma(uvN) - edgeLuma;
		//if it is larger than the threshold, it is the pixel of the another edge
		atEndN = abs(lumaDeltaN) >= gradientThreshold;
	}
	//if we do not find the end point, guess it is the next point
	if (!atEndN) {
		uvN -= LAST_EDGE_STEP_GUESS * uvStep;
	}

	float distanceToEndN;
	float distanceToEndP;
	if (edge.isHorizontal) {
		distanceToEndP = uvP.x - uv.x;
		distanceToEndN = uv.x - uvN.x;
	}
	else {
		distanceToEndP = uvP.y - uv.y;
		distanceToEndN = uv.y - uvN.y;
	}
	//fine the nearest end of the edge
	float distanceToNearestEnd;
	bool deltaSign;
	if (distanceToEndP <= distanceToEndN) {
		distanceToNearestEnd = distanceToEndP;
		deltaSign = lumaDeltaP >= 0;
	}
	else {
		distanceToNearestEnd = distanceToEndN;
		deltaSign = lumaDeltaN >= 0;
	}

	//blend on a single side
	if (deltaSign == (luma.m - edgeLuma >= 0)) {
		//clip both the small luma pixel while edge luma gradient to low and the big luma pixel while edge luma gradient to high
		return 0;
	}
	else {
		return 0.5 - distanceToNearestEnd / (distanceToEndP + distanceToEndN);
	}
}

float4 FXAAPassFragment(Varyings input) : SV_TARGET{
	LumaNeighborhood luma = GetLumaNeighborhood(input.screenUV);
	if (CanSkipFXAA(luma)) {
		////test skipped pixel
		//return 0.0;
		return GetSource(input.screenUV);
	}
	
	FXAAEdge edge = GetFXAAEdge(luma);
	////test edge pixel
	//return edge.pixelStep > 0 ? float4(0.0, 1.0, 0.0, 1.0) : 1.0;

	//subpixel blend factor, just use it already can get a simple AA result
	float subPixelBlendFactor = GetSubpixelBlendFactor(luma);

	//edge blend factor, specularly used for slanting region
	float edgeBlendFactor = GetEdgeBlendFactor(luma, edge, input.screenUV);

	float blendFactor = max(subPixelBlendFactor, edgeBlendFactor);
	float2 blendUV = input.screenUV;
	if (edge.isHorizontal) {
		blendUV.y += blendFactor * edge.pixelStep;
	} 
	else {
		blendUV.x += blendFactor * edge.pixelStep;
	}

	return GetSource(blendUV);
}

#endif
