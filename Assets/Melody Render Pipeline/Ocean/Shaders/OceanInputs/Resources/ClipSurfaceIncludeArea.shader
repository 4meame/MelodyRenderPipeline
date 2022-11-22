// Crest Ocean System

// Copyright 2020 Wave Harmonic Ltd

// Renders the geometry to the clip surface data and sets the value to 'include'.

Shader "Crest/Inputs/Clip Surface/Include Area"
{
	SubShader
	{
		Tags { "Queue" = "Geometry-10" }

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
				return 0.0;
			}
			ENDCG
		}
	}
}
