Shader "Hidden/DrawDiffuseTexture" {
    Properties{
        _BaseMap("", 2D) = "white" {}
        [NoScaleOffset]_MaskMap("Mask Map<MODS>", 2D) = "white" {}
        _Occlusion("Occlusion", Range(0.0, 1.0)) = 1.0
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _Cutoff("", Float) = 0.5
        _BaseColor("", Color) = (1,1,1,1)
    }

    SubShader {
        Tags { "RenderType" = "Opaque" }
        Pass 
        {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"

        struct v2f {
            float4 pos : SV_POSITION;
            float4 nz : TEXCOORD0;
            float2 uv : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        uniform float4 _BaseMap_ST;

        v2f vert(appdata_base v) {
            v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex);
            o.nz.xyz = COMPUTE_VIEW_NORMAL;
            o.nz.w = COMPUTE_DEPTH_01;
            o.uv = TRANSFORM_TEX(v.texcoord, _BaseMap);
            return o;
        }

        uniform sampler2D _BaseMap;
        uniform sampler2D _MaskMap;
        uniform fixed _Occlusion;
        uniform fixed _Metallic;
        uniform fixed _Smoothness;
        uniform fixed _Cutoff;
        uniform fixed4 _BaseColor;
        #define MIN_REFLECTIVITY 0.04

        float OneMinusReflectivity(float metallic) {
            float range = 1.0 - MIN_REFLECTIVITY;
            return range - metallic * range;
        }

        fixed4 frag(v2f i) : SV_Target{
            fixed4 texcol = tex2D(_BaseMap, i.uv);
            fixed4 maskMap = tex2D(_MaskMap, i.uv);
            fixed occlusion = maskMap.g * _Occlusion;
            fixed metallic = maskMap.r * _Metallic;
            fixed oneMinusReflectivity = OneMinusReflectivity(metallic);
            fixed3 diffuse = texcol.rgb * _BaseColor.rgb * oneMinusReflectivity;
            clip(texcol.a * _BaseColor.a - _Cutoff);
            return fixed4(diffuse, occlusion);
        }

        ENDCG
        }
    }
}
