// Crest Ocean System

// Copyright 2020 Wave Harmonic Ltd

#ifndef CREST_OCEAN_NORMAL_MAPPING_INCLUDED
#define CREST_OCEAN_NORMAL_MAPPING_INCLUDED

#if _APPLYNORMALMAPPING_ON

half2 SampleNormalMaps
(
	in const WaveHarmonic::Crest::TiledTexture i_texture,
	in const float2 i_worldXZUndisplaced,
	in const float2 offset,
	in const float lodAlpha,
	in const CascadeParams cascadeData,
	in const PerCascadeInstanceData instanceData
)
{
	const float lodDataGridSize = cascadeData._texelWidth;
	float2 normalScrollSpeeds = instanceData._normalScrollSpeeds;

	const float2 v0 = float2(0.94, 0.34), v1 = float2(-0.85, -0.53);

	float2 worldXZUndisplaced = i_worldXZUndisplaced;

#if CREST_FLOATING_ORIGIN
	// Apply tiled floating origin offset. Always needed.
	worldXZUndisplaced -= i_texture.FloatingOriginOffset(cascadeData);
#endif

	worldXZUndisplaced -= offset;

	float nstretch = i_texture._scale * lodDataGridSize; // normals scaled with geometry
	const float spdmulL = normalScrollSpeeds[0];
	half2 norm =
		UnpackNormal(i_texture.Sample((v0 * _CrestTime * spdmulL + worldXZUndisplaced) / nstretch)).xy +
		UnpackNormal(i_texture.Sample((v1 * _CrestTime * spdmulL + worldXZUndisplaced) / nstretch)).xy;

	// blend in next higher scale of normals to obtain continuity
	const float farNormalsWeight = instanceData._farNormalsWeight;
	const half nblend = lodAlpha * farNormalsWeight;
	if (nblend > 0.001)
	{
		// next lod level
		nstretch *= 2.;
		const float spdmulH = normalScrollSpeeds[1];
		norm = lerp(norm,
			UnpackNormal(i_texture.Sample((v0 * _CrestTime * spdmulH + worldXZUndisplaced) / nstretch)).xy +
			UnpackNormal(i_texture.Sample((v1 * _CrestTime * spdmulH + worldXZUndisplaced) / nstretch)).xy,
			nblend);
	}

	// approximate combine of normals. would be better if normals applied in local frame.
	return _NormalsStrength * norm;
}

void ApplyNormalMapsWithFlow
(
	in const WaveHarmonic::Crest::TiledTexture i_texture,
	in const float2 worldXZUndisplaced,
	in const float2 flow,
	in const float lodAlpha,
	in const CascadeParams cascadeData,
	in const PerCascadeInstanceData instanceData,
	inout float3 io_n
)
{
	const float half_period = 1;
	const float period = half_period * 2;
	float sample1_offset = fmod(_CrestTime, period);
	float sample1_weight = sample1_offset / half_period;
	if (sample1_weight > 1.0) sample1_weight = 2.0 - sample1_weight;
	float sample2_offset = fmod(_CrestTime + half_period, period);
	float sample2_weight = 1.0 - sample1_weight;
	sample1_offset -= 0.5 * period;
	sample2_offset -= 0.5 * period;

	// In order to prevent flow from distorting the UVs too much,
	// we fade between two samples of normal maps so that for each
	// sample the UVs can be reset
	half2 io_n_1 = SampleNormalMaps(i_texture, worldXZUndisplaced, flow * sample1_offset, lodAlpha, cascadeData, instanceData);
	half2 io_n_2 = SampleNormalMaps(i_texture, worldXZUndisplaced, flow * sample2_offset, lodAlpha, cascadeData, instanceData);
	io_n.xz += sample1_weight * io_n_1;
	io_n.xz += sample2_weight * io_n_2;
}

#endif // _APPLYNORMALMAPPING_ON

#endif // CREST_OCEAN_NORMAL_MAPPING_INCLUDED
