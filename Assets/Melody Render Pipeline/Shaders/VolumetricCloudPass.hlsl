#ifndef MELODY_VOLUMETRIC_CLOUD_PASS_INCLUDED
#define MELODY_VOLUMETRIC_CLOUD_PASS_INCLUDED

//----------------textures-----------------//
TEXTURE3D(_ShapeNoiseTex);
SAMPLER(sampler_ShapeNoiseTex);
TEXTURE3D(_DetailNoiseTex);
SAMPLER(sampler_DetailNoiseTex);
TEXTURE2D(_CoverageTex);
SAMPLER(sampler_CoverageTex);
TEXTURE2D(_CurlNoiseTex);
SAMPLER(sampler_CurlNoiseTex);
TEXTURE2D(_BlueNoiseTex);
SAMPLER(sampler_BlueNoiseTex);
TEXTURE2D(_PreDepth);
SAMPLER(sampler_PreDepth);
//------------debug mode variables-------------//
int debugViewMode;
int debugGreyscale;
int debugShowAllChannels;
float debugNoiseSliceDepth;
float debugTileAmount;
float ViewerSize;
float4 debugChannelWeight;
//-----------shadred common variables------------//
float3 _CameraPosition;
float _MaxDistance;
//atmosphere
float3 _EarthCenter;
float _EarthRadius;
float _StartHeight;
float _EndHeight;
float _AtmosphereThickness;
//temporal sampling
TEXTURE2D(_CurrFrame);
SAMPLER(sampler_CurrFrame);
TEXTURE2D(_SubFrame);
SAMPLER(sampler_SubFrame);
TEXTURE2D(_PrevFrame);
SAMPLER(sampler_PrevFrame);
float _SubFrameNumber;
float _SubPixelSize;
float2 _SubFrameSize;
float2 _FrameSize;
float4x4 _PreviousProjection;
float4x4 _PreviousInverseProjection;
float4x4 _PreviousRotation;
float4x4 _PreviousInverseRotation;
float4x4 _Projection;
float4x4 _InverseProjection;
float4x4 _Rotation;
float4x4 _InverseRotation;
//------------cloud render variables-------------//
float3 _Random0;
float3 _Random1;
float3 _Random2;
float3 _Random3;
float3 _Random4;
float3 _Random5;
//coverage
float2 _CoverageOffset;
float2 _CoverageOffsetPerFrame;
float _CoverageScale;
float _HorizonCoverageStart;
float _HorizonCoverageEnd;
float4 _CloudHeightGradient1;
float4 _CloudHeightGradient2;
float4 _CloudHeightGradient3;
//define some specific use of the coverage map according to GPU-Pro 7
#define FLOAT4_COVERAGE( f)	f.r
//the chance that the clouds overhead will produce rain
#define FLOAT4_RAIN( f) f.g
//a value of 0.0 indicates stratus, 0.5 indicates stratocumulus, and 1.0 indicates cumulus clouds
#define FLOAT4_TYPE( f) f.b
//base modeling
float _BaseScale;
float3 _BaseOffset;
float _SampleThreshold;
float _BottomFade;
float _SampleScalar;
//detail modeling
float _ErosionEdgeSize;
float _Curl;
float _CurlScale;
float _DetailScale;
float3 _DetailOffset;
//lighting
float4 _MainLightPosition;
float4 _MainLightColor;
float _Density;
float _DarkOutlineScalar;
float _SunRayLength;
float _ConeRadius;
float _ForwardScatteringG;
float _BackwardScatteringG;
float3 _CloudBaseColor;
float3 _CloudTopColor;
float _LightScale;
float _AmbientScale;
//optimization
float _RayMinimumY;
float _MaxIterations;
float _LODDistance;
float _HorizonFadeScale;
float _HorizonFadeStartAlpha;

#define H 28000

struct Varyings {
	float4 positionCS : SV_POSITION;
	float2 screenUV : VAR_SCREEN_UV;
	float3 cameraRay : VAR_CAMERA_RAY;
};

float3 ScreenSpaceToViewSpace(float3 cameraRay, float depth) {
	return (cameraRay * depth);
}

//screen uv can present camera direction
float3 UVToCameraRay(float2 uv) {
	float4 cameraRay = float4(uv * 2.0 - 1.0, 1.0, 1.0);
	cameraRay = mul(_InverseProjection, cameraRay);
	cameraRay = cameraRay / cameraRay.w;
	return mul((float3x3)_InverseRotation, cameraRay.xyz);
}

//ReMap example: Converting a value of 0.66 from the range(0,1)to the range(0,2)resultsin 1.32 being returned.
float Remap(float original_value, float original_min, float original_max, float new_min, float new_max) {
	return new_min + (((original_value - original_min) / (original_max - original_min)) * (new_max - new_min));
}

//get the smooth value from gradient4
float GradientStep(float a, float4 gradient) {
	return smoothstep(gradient.x, gradient.y, a) - smoothstep(gradient.z, gradient.w, a);
}

//smooth the threshold value
float SmoothThreshold(float value, float threshold, float edgeSize) {
	return smoothstep(threshold, threshold + edgeSize, value);
}

float3 SmoothThreshold(float3 value, float threshold, float edgeSize) {
	value.r = smoothstep(threshold, threshold + edgeSize, value.r);
	value.g = smoothstep(threshold, threshold + edgeSize, value.g);
	value.b = smoothstep(threshold, threshold + edgeSize, value.b);
	return value;
}

//mix value with the smoothed noise
float MixNoise(float value, float noise, float a, float b, float height) {
	float s = smoothstep(a, b, height);
	value += noise * s;
	return value;
}

//trilinear interpolation?
float Lerp3(float v0, float v1, float v2, float a) {
	return a < 0.5 ? lerp(v0, v1, a * 2.0) : lerp(v1, v2, (a - 0.5) * 2.0);
}

float4 Lerp3(float4 v0, float4 v1, float4 v2, float a) {
	return float4(
		Lerp3(v0.x, v1.x, v2.x, a),
		Lerp3(v0.y, v1.y, v2.y, a),
		Lerp3(v0.z, v1.z, v2.z, a),
		Lerp3(v0.w, v1.w, v2.w, a)
		);
}

//calculate height percentage
float GetHeightPercentage(float3 ray, float3 sphereCenter) {
	float h = distance(ray, sphereCenter);
	return clamp(h - _EarthRadius - _StartHeight, 0, _AtmosphereThickness)/_AtmosphereThickness;
}

//a ray casting internal the sphere from origin to the radius ends
float3 InternalRaySphereIntersect(float sphereRadius, float3 origin, float3 direction) {
	float a0 = sphereRadius * sphereRadius - dot(origin, origin);
	float a1 = dot(origin, direction);
	float result = sqrt(a1 * a1 + a0) - a1;
	//normalized direction, normalized length result
	return origin + direction * result;
}

//returns (dstToBox, dstInsideBox). If ray misses box, dstInsideBox will be zero
//dstInsideBox is the distance from startPoint to endPoint
float2 RayBoxDst(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRaydir) {
	float3 t0 = (boundsMin - rayOrigin) * invRaydir;
	float3 t1 = (boundsMax - rayOrigin) * invRaydir;
	float3 tmin = min(t0, t1);
	float3 tmax = max(t0, t1);
	float dstA = max(max(tmin.x, tmin.y), tmin.z);
	float dstB = min(tmax.x, min(tmax.y, tmax.z));
	// CASE 1: ray intersects box from outside (0 <= dstA <= dstB)
	// dstA is dst to nearest intersection, dstB dst to far intersection
	// CASE 2: ray intersects box from inside (dstA < 0 < dstB)
	// dstA is the dst to intersection behind the ray, dstB is dst to forward intersection
	// CASE 3: ray misses box (dstA > dstB)
	float dstToBox = max(0, dstA);
	float dstInsideBox = max(0, dstB - dstToBox);
	return float2(dstToBox, dstInsideBox);
}

//returns (dstToSphere, dstInSphere). If ray misses sphere, dstInSphere will be zero
//dstInSphere is the distance from startPoint to endPoint
float2 RaySphereDst(float3 sphereCenter, float sphereRadius, float3 origin, float3 direction) {
	float3 oc = origin - sphereCenter;
	float b = dot(direction, oc);
	float c = dot(oc, oc) - sphereRadius * sphereRadius;
	float t = b * b - c;
	// CASE 1: ray intersects sphere(t > 0)
	// dstA is dst to nearest intersection, dstB dst to far intersection
	// CASE 2: ray touches sphere(t = 0)
	// dstA is the dst to intersection behind the ray, dstB is dst to forward intersection
	// CASE 3: ray misses sphere (t < 0)
	float delta = sqrt(max(t, 0));
	float dstToSphere = max(-b - delta, 0);
	float dstInSphere = max(-b + delta - dstToSphere, 0);
	return float2(dstToSphere, dstInSphere);
}

float2 RayIntersectCloudDistance(float3 sphereCenter, float3 origin, float3 direction) {
	float2 cloudDstMin = RaySphereDst(sphereCenter, _StartHeight + _EarthRadius, origin, direction);
	float2 cloudDstMax = RaySphereDst(sphereCenter, _EndHeight + _EarthRadius, origin, direction);
	float dstToCloud = 0;
	float dstInCloud = 0;
	float d = distance(origin, sphereCenter);
	//on the ground
	if (d <= _StartHeight + _EarthRadius) {
		float3 startPos = origin + direction * cloudDstMin.y;
		if (startPos.y >= 0) {
			dstToCloud = cloudDstMin.y;
			dstInCloud = cloudDstMax.y - cloudDstMin.y;
		}
		return float2(dstToCloud, dstInCloud);
	}
	//in the cloud
	else if (d > _StartHeight + _EarthRadius && d <= _EndHeight + _EarthRadius) {
		dstToCloud = 0;
		dstInCloud = cloudDstMin.y > 0 ? cloudDstMin.x : cloudDstMax.y;
		return float2(dstToCloud, dstInCloud);
	}
	//outside the cloud
	else {
		dstToCloud = cloudDstMax.x;
		dstInCloud = cloudDstMin.y > 0 ? cloudDstMin.x - dstToCloud : cloudDstMax.y;
	}
	return float2(dstToCloud, dstInCloud);
}

//coverage is a xz plane
float4 SampleCoverage(float3 ray, float csRayHeight, float lod) {
	float2 unit = ray.xz * _CoverageScale;
	//make [-1,1] vector cover the [0,1] uv
	float2 uv = unit * 0.5 + 0.5;
	uv += _CoverageOffset + _CoverageOffsetPerFrame  * _Time.y;
	//view depth distance
	float depth = distance(ray, _CameraPosition) / _MaxDistance;
	float4 coverage = SAMPLE_TEXTURE2D_LOD(_CoverageTex, sampler_CoverageTex, float4(uv, 0, 0), 0);
	float4 coverageH = float4(1.0, 0.0, 0.0, 0.0);
	//make a circled horizontal cloud coverage by view distance
	float alpha = smoothstep(_HorizonCoverageStart, _HorizonCoverageEnd, depth);
	//tricked horizontal shadped coverage
	coverageH = float4(
		smoothstep(_HorizonCoverageStart, _HorizonCoverageEnd, depth),
		0.0,
		smoothstep(_HorizonCoverageEnd, _HorizonCoverageStart + (_HorizonCoverageEnd - _HorizonCoverageStart) * 0.5, depth),
		0.0
		);
	return lerp(coverage, coverageH, alpha);
}

float SampleCloud(float3 ray, float rayDensity, float4 coverage, float csRayHeight, float lod) {
	//sample coord
	float4 uvw_shape = float4(ray * _BaseScale + _BaseOffset * _Time.y, 0.0);
	float4 shapeSample = SAMPLE_TEXTURE3D_LOD(_ShapeNoiseTex, sampler_ShapeNoiseTex, uvw_shape, 0);
	//use gradient describe the probablity of different cloud type
	float4 gradientScalar = float4(
		1.0,
		GradientStep(csRayHeight, _CloudHeightGradient1),
		GradientStep(csRayHeight, _CloudHeightGradient2),
		GradientStep(csRayHeight, _CloudHeightGradient3)
		); 
	shapeSample *= gradientScalar;
	//maybe use NUBIS's Remapping FBM method is better depending on _ShapeNoise
	float shape = saturate((shapeSample.r + shapeSample.g + shapeSample.b + shapeSample.a) / 4.0);
	float4 gradient = Lerp3(
		_CloudHeightGradient3,
		_CloudHeightGradient2,
		_CloudHeightGradient1,
		FLOAT4_TYPE(coverage)
		);
	shape *= GradientStep(csRayHeight, gradient);
	//erose the shape edge
	shape = SmoothThreshold(shape, _SampleThreshold, _ErosionEdgeSize);
	//make coverage
	shape = saturate(shape - (1.0 - FLOAT4_COVERAGE(coverage))) * FLOAT4_COVERAGE(coverage);
	//only lod 0 distance will calculate detial and sample detial noise
	if (shape > 0.0 && shape < 1.0 && lod == 0) {
		float4 uv_curl = float4(ray.xy * _BaseScale * _CurlScale, 0.0, 0.0);
		//make wind-like curl of cloud
		float3 curl = SAMPLE_TEXTURE2D_LOD(_CurlNoiseTex, sampler_CurlNoiseTex, uv_curl, 0) * 2.0 - 1.0;
		float4 uvw_detail = float4(ray * _BaseScale * _DetailScale, 0.0);
		uvw_detail.xyz += _DetailOffset * _Time.y;
		curl *= _Curl * csRayHeight;
		uvw_detail.xyz += curl;
		//just the same as shape
		float3 detail = SAMPLE_TEXTURE3D_LOD(_DetailNoiseTex, sampler_DetailNoiseTex, uvw_detail, 0);
		detail *= gradientScalar.gba;
		float detailValue = detail.r + detail.g + detail.b;
		detailValue /= 3.0;
		detailValue *= smoothstep(1.0, 0.0, shape) * 0.5;
		shape -= detailValue;
		shape = saturate(shape);
	}
	return shape * _SampleScalar * smoothstep(0.0, _BottomFade * 1.0, csRayHeight);
}

float HenyeyGreensteinPhase(float cosAngle, float g) {
	float g2 = g * g;
	return (1.0 - g2) / pow(1.0 + g2 - 2.0 * g * cosAngle, 1.5);
}

float BeerTerm(float densityAtSample) {
	return exp(-_Density * densityAtSample);
}

float PowderTerm(float densityAtSample, float cosTheta) {
	float powder = 1.0 - exp(-_Density * densityAtSample * 2.0);
	powder = saturate(powder * _DarkOutlineScalar * 2.0);
	return lerp(1.0, powder, smoothstep(0.5, -0.5, cosTheta));
}

float3 SampleLight(float3 origin, float originDensity, float pixelAlpha, float3 cosAngle, float rayDistance, float3 RandomUnitSphere[6]) {
	const float iterations = 6.0;
	float3 rayStep = _MainLightPosition * (_SunRayLength / iterations);
	float3 ray = origin + rayStep;
	float heightFraction = 0.0;
	float lod = 0;
	float density = 0.0;
	float4 coverage;
	float3 randomOffset = float3(0.0, 0.0, 0.0);
	float coneRadius = 0.0;
	const float coneStep = _ConeRadius / iterations;
	float energy = 0.0;
	//optical thickness 
	float thickness = 0.0;
	for (float i = 0.0; i < iterations; i++) {
		randomOffset = RandomUnitSphere[i] * coneRadius;
		ray += rayStep;
		heightFraction = GetHeightPercentage(ray, _EarthCenter);
		coverage = SampleCoverage(ray + randomOffset, heightFraction, lod);
		density = SampleCloud(ray + randomOffset, originDensity, coverage, heightFraction, lod);
		density *= float(heightFraction <= 1.0);
		thickness += density;
		coneRadius += coneStep;
	}
	float far = 8.0;
	ray += rayStep * far;
	heightFraction = GetHeightPercentage(ray, _EarthCenter);
	coverage = SampleCoverage(ray, heightFraction, lod);
	density = SampleCloud(ray, originDensity, coverage, heightFraction, lod);
	density *= float(heightFraction <= 1.0);
	thickness += density;
	float forwardP = HenyeyGreensteinPhase(cosAngle, _ForwardScatteringG);
	float backwardsP = HenyeyGreensteinPhase(cosAngle, _BackwardScatteringG);
	float P = (forwardP + backwardsP) / 2.0;
	return _MainLightColor.rgb * BeerTerm(thickness) * PowderTerm(originDensity, cosAngle) * P;
}

float3 SampleAmbientLight(float heightFraction, float depth) {
	return lerp(_CloudBaseColor, _CloudTopColor, heightFraction);
}

float4 debugDrawNoise(float2 uv) {
	float4 channels = 0;
	float3 samplePos = float3(uv.x, uv.y, debugNoiseSliceDepth);
	if (debugViewMode == 1) {
		channels = SAMPLE_TEXTURE3D_LOD(_ShapeNoiseTex, sampler_ShapeNoiseTex, float4(samplePos, 0), 0);
	}
	else if (debugViewMode == 2) {
		channels = SAMPLE_TEXTURE3D_LOD(_DetailNoiseTex, sampler_DetailNoiseTex, float4(samplePos, 0), 0);
	}
	else if (debugViewMode == 3) {
		samplePos.xy += _CoverageOffset;
		channels = SAMPLE_TEXTURE2D_LOD(_CoverageTex, sampler_CoverageTex, float4(samplePos, 0), 0);
	}

	if (debugShowAllChannels) {
		return channels;
	}
	else {
		float4 maskedChannels = channels * debugChannelWeight;
		if (debugGreyscale || debugChannelWeight.w == 1) {
			return dot(maskedChannels, 1);
		}
		else {
			return maskedChannels;
		}
	}
}

float3 FilmicTonemap(float3 x) {
	const float A = 0.15;
	const float B = 0.50;
	const float C = 0.10;
	const float D = 0.20;
	const float E = 0.02;
	const float F = 0.30;
	return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F;
}

//vertexID is the clockwise index of a triangle : 0,1,2
Varyings DefaultPassVertex(uint vertexID : SV_VertexID) {
	Varyings output;
	//make the [-1, 1] NDC, visible UV coordinates cover the 0-1 range
	output.positionCS = float4(vertexID <= 1 ? -1.0 : 3.0,
		vertexID == 1 ? 3.0 : -1.0,
		0.0, 1.0);
	output.screenUV = float2(vertexID <= 1 ? 0.0 : 2.0,
		vertexID == 1 ? 2.0 : 0.0);
	//some graphics APIs have the texture V coordinate start at the top while others have it start at the bottom
	if (_ProjectionParams.x < 0.0) {
		output.screenUV.y = 1.0 - output.screenUV.y;
	}
	output.cameraRay = UVToCameraRay(output.screenUV);
	return output;
}

float4 DebugFragment(Varyings input) : SV_TARGET {
	if (debugViewMode != 0) {
		float width = _ScreenParams.x;
		float height = _ScreenParams.y;
		float minDim = min(width, height);
		float x = input.screenUV.x * width;
		float y = (1 - input.screenUV.y) * height;
		if (x < minDim * ViewerSize && y < minDim * ViewerSize)
		{
			return debugDrawNoise(float2((x / (minDim * ViewerSize) * debugTileAmount), y / (minDim * ViewerSize) * debugTileAmount));
		}
	}
      return SAMPLE_TEXTURE2D_LOD(_CurrFrame, sampler_CurrFrame, input.screenUV, 0);
}

//TODO : Downsample and apply Erosion Operator
float4 PreDepthFragment(Varyings input) : SV_TARGET{
	float4 color = 1;
	float3  rayDirection = normalize(input.cameraRay);
	float dstToScene = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_point_clamp, input.screenUV, 0);
	dstToScene = LinearEyeDepth(dstToScene, _ZBufferParams);
	float2 risInfo = RayIntersectCloudDistance(_EarthCenter, _CameraPosition, rayDirection);
	//if ray hit scene object, return
	if (dstToScene < risInfo.x || risInfo.y < 0) {
		color = 0;
	}
	return color;
}

float4 BaseFragment(Varyings input) : SV_TARGET{
	float4 color = float4(0,0,0,0);
	float3  rayDirection = normalize(input.cameraRay);
	float i = 0;
	if (rayDirection.y > _RayMinimumY) {
		//ray intersect sphere info
		float2 risInfo = RayIntersectCloudDistance(_EarthCenter, _CameraPosition, rayDirection);
		float3 ray = _CameraPosition + rayDirection * risInfo.x;
		//float stepSize = _RayStepLength;
		float stepSize = min(risInfo.y, 4 * H)/ _MaxIterations;
		//fix step size of the in-cloud situation, and a litte lerp with usual step size
		if (distance(_CameraPosition, _EarthCenter) - _EarthRadius >= _StartHeight && distance(_CameraPosition, _EarthCenter) - _EarthRadius <= _EndHeight) {
			stepSize = lerp(stepSize, 2 * _AtmosphereThickness / _MaxIterations, clamp(risInfo.y/ _MaxDistance,0.0,0.1));
		}
		//ray steps vector
		float3 rayStep = rayDirection * stepSize;
		float rayStepScalar = 1.0;
		//white-noise like random sample start offset
		const float3 RandomUnitSphere[6] = { _Random0, _Random1, _Random2, _Random3, _Random4, _Random5 };
		float travelling = 0;
		//persentage of the raystep, use it divide lod 0 and lod 1
		float normalizedDepth = 0.0;
		//persentage of the Y along atmosphere from 0 to 1
		float heightFraction = 0.0;
		float density = 0.0;
		//threshold that switch to cheap sample
		float zeroThreshold = 4.0;
		int zeroCount = 0;
		float cosAngle = dot(rayDirection, _MainLightPosition);
		while (true) {
			//early exit if reaching max iterations or full opacity or atmosphere or hit scene object
			if (i >= _MaxIterations || color.a >= 1.0 || heightFraction >= 1.0 || travelling >= risInfo.y) {
				break;
			}
			heightFraction = GetHeightPercentage(ray, _EarthCenter);
			normalizedDepth = distance(_CameraPosition, ray) / _MaxDistance;
			float lod = step(_LODDistance, normalizedDepth);
			float4 coverage = SampleCoverage(ray, heightFraction, lod);
			density = SampleCloud(ray, color.a, coverage, heightFraction, lod);
			float4 particle = float4(density, density, density, density);

			if (density > 0) {
				if (rayStepScalar > 1.0) {
					ray -= rayStep * rayStepScalar;
					i -= rayStepScalar;
					heightFraction = GetHeightPercentage(ray, _EarthCenter);
					normalizedDepth = distance(_CameraPosition, ray) / _MaxDistance;
					lod = step(_LODDistance, normalizedDepth);
					coverage = SampleCoverage(ray, heightFraction, lod);
					density = SampleCloud(ray, color.a, coverage, heightFraction, lod);
					float4 particle = float4(density, density, density, density);
				}

				float3 ambientLight = SampleAmbientLight(heightFraction, normalizedDepth);
				float3 sunLight = SampleLight(ray, particle.a, color.a, cosAngle, normalizedDepth, RandomUnitSphere);
				sunLight *= _LightScale;
				ambientLight *= _AmbientScale;
				particle.rgb = sunLight + ambientLight;
				particle.rgb *= particle.a;
				color = (1.0 - color.a) * particle + color;
			}
			i += rayStepScalar;
			ray += rayStep * rayStepScalar;
			travelling += stepSize * rayStepScalar;
			heightFraction = GetHeightPercentage(ray, _EarthCenter);
		}
		float fade = smoothstep(
			_RayMinimumY,
			_RayMinimumY + (1.0 - _RayMinimumY) * _HorizonFadeScale,
			rayDirection.y
			);
		color *= _HorizonFadeStartAlpha + fade * (1 - _HorizonFadeStartAlpha);
	}

	//filmic tonemapping: http://filmicgames.com/archives/75
	const float W = 11.2;
	float3 texColor = color.rgb;
	texColor *= 1.5;
	float ExposureBias = 2.0f;
	float3 curr = FilmicTonemap(ExposureBias * texColor);
	float3 whiteScale = 1.0f / FilmicTonemap(W);
	color.rgb = curr * whiteScale;

	return color;
}

float4 CombineFragment(Varyings input) : SV_TARGET{
	float2 uv = floor(input.screenUV * _FrameSize);
	float2 uv2 = (floor(input.screenUV * _SubFrameSize) + 0.5) / _SubFrameSize;
	float x = fmod(uv.x, _SubPixelSize);
	float y = fmod(uv.y, _SubPixelSize);
	float frame = y * _SubPixelSize + x;
	float4 cloud;
	if (frame == _SubFrameNumber) {
		cloud = SAMPLE_TEXTURE2D_LOD(_SubFrame, sampler_SubFrame, uv2, 0);
	}
	else {
		float4 prevPos = float4(input.screenUV * 2.0 - 1.0, 1.0, 1.0);
		prevPos = mul(_InverseProjection, prevPos);
		prevPos = prevPos / prevPos.w;
		prevPos.xyz = mul((float3x3)_InverseRotation, prevPos.xyz);
		prevPos.xyz = mul((float3x3)_PreviousRotation, prevPos.xyz);
		float4 reProj = mul(_Projection, prevPos);
		reProj /= reProj.w;
		reProj.xy = reProj.xy * 0.5 + 0.5;

		if (reProj.y < 0.0 || reProj.y > 1.0 || reProj.x < 0.0 || reProj.x > 1.0) {
			cloud = SAMPLE_TEXTURE2D_LOD(_SubFrame, sampler_SubFrame, input.screenUV, 0);
		}
		else {
			cloud = SAMPLE_TEXTURE2D_LOD(_PrevFrame, sampler_SubFrame, reProj.xy, 0);
		}
	}
	return cloud;
}

float4 CopyFragment(Varyings input) : SV_TARGET{
	return SAMPLE_TEXTURE2D_LOD(_CurrFrame, sampler_CurrFrame, input.screenUV, 0);
}

float4 FinalFragment(Varyings input) : SV_TARGET{
	//Final bilt by Pre-Detective depth mask, it is a very expensive method
	return SAMPLE_TEXTURE2D_LOD(_CurrFrame, sampler_CurrFrame, input.screenUV, 0) * SAMPLE_TEXTURE2D_LOD(_PreDepth, sampler_PreDepth, input.screenUV, 0);
}

#endif
