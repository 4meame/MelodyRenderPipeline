// Crest Ocean System

// Copyright 2021 Wave Harmonic Ltd

Shader "Hidden/Crest/Underwater/Ocean Mask"
{
	Properties
	{
		// Needed so it can be scripted.
		_StencilRef("Stencil Reference", Int) = 0
	}

	SubShader
	{
		Pass
		{
			Name "Ocean Surface Mask"
			// We always disable culling when rendering ocean mask, as we only
			// use it for underwater rendering features.
			Cull Off

			Stencil
			{
				Ref [_StencilRef]
				Comp Equal
			}

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			// for VFACE
			#pragma target 3.0

			#pragma multi_compile_local _ CREST_WATER_VOLUME

			#include "../../../../ShaderLibrary/Common.hlsl"

			#include "../UnderwaterMaskShared.hlsl"
			ENDHLSL
		}

		Pass
		{
			Name "Ocean Horizon Mask"
			Cull Off
			ZWrite Off
			// Horizon must be rendered first or it will overwrite the mask with incorrect values. ZTest not needed.
			ZTest Always

			Stencil
			{
				Ref [_StencilRef]
				Comp Equal
			}

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#include "../../../../ShaderLibrary/Common.hlsl"

			#include "../UnderwaterMaskHorizonShared.hlsl"
			ENDHLSL
		}
	}
}
