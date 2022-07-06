Shader "Hidden/Melody RP/VolumetricCloud"
{
    SubShader
    {
        Cull Off
        ZTest Always
        Zwrite Off

        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "VolumetricCloudPass.hlsl"
        ENDHLSL

        Pass
        {
            Name "Texture Debug"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment DebugFragment
            ENDHLSL
        }

		Pass {
			Name "Pre Depth "

			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex DefaultPassVertex
			#pragma fragment PreDepthFragment
			ENDHLSL
		}

		Pass {
			Name "Cloud Rendering "

			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex DefaultPassVertex
			#pragma fragment BaseFragment
			ENDHLSL
		}

		Pass {
			Name "Temporal Sampling"

			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex DefaultPassVertex
			#pragma fragment CombineFragment
			ENDHLSL
		}

		//NOTE : copy operations of temporal sampling is different final bilt so JUST use Blend one one 
		Pass {
			Name "Copy"

			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex DefaultPassVertex
			#pragma fragment CopyFragment
			ENDHLSL
		}

		Pass {
			Name "Final"

			Blend One OneMinusSrcAlpha

			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex DefaultPassVertex
			#pragma fragment FinalFragment
			ENDHLSL
		}
    }
}
