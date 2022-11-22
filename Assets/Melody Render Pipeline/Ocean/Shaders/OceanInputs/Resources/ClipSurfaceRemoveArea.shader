// Crest Ocean System

// Copyright 2020 Wave Harmonic Ltd

// Renders the geometry to the clip surface texture.

Shader "Crest/Inputs/Clip Surface/Remove Area"
{
	SubShader
	{
		Tags { "Queue" = "Geometry" }

		Pass
		{
			Blend Off
			ZWrite Off
			ColorMask R

			CGPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#include "UnityCG.cginc"

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
				return 1.0;
			}
			ENDCG
		}
	}
}
