Shader "Hidden/DrawAlbedoTexture" {
    Properties{
        _BaseMap("", 2D) = "white" {}
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
        uniform fixed _Cutoff;
        uniform fixed4 _BaseColor;

        fixed4 frag(v2f i) : SV_Target{
            fixed4 texcol = tex2D(_BaseMap, i.uv) * _BaseColor;
            clip(texcol.a * _BaseColor.a - _Cutoff);
            return texcol;
        }

        ENDCG
        }
    }
}
