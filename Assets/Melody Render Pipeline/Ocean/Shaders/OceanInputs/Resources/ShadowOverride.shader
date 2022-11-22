// Crest Ocean System

// Copyright 2020 Wave Harmonic Ltd

// Renders the geometry to the shadow texture and sets shadow data to provided value.

Shader "Crest/Inputs/Shadows/Override Shadows"
{
	Properties
	{
		_ShadowValue("Shadow Value", Range(0.0, 1.0)) = 1.0
	}

	SubShader
	{
		Tags { "Queue" = "Geometry" }

		Pass
		{
			Blend Off
			ZWrite Off
			ColorMask RG

			CGPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#include "UnityCG.cginc"

			half _ShadowValue;

			struct Attributes
			{
				float3 positionOS : POSITION;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
			};

			Varyings Vert(Attributes input)
			{
				Varyings o;
				o.positionCS = UnityObjectToClipPos(input.positionOS);
				return o;
			}

			half4 Frag(Varyings input) : SV_Target
			{
				return _ShadowValue;
			}
			ENDCG
		}
	}
}
