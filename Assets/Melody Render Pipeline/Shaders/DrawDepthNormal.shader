Shader "Hidden/DrawDepthNormalTexture" {
    Properties{
        _BaseMap("", 2D) = "white" {}
        _Cutoff("", Float) = 0.5
        _BaseColor("", Color) = (1,1,1,1)
        [NoScaleOffset]_NormalMap("Normals", 2D) = "bump" {}
        _NormalScale("Normal Scale", Range(0, 1)) = 1
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
            float3 normal : TEXCOORD2;
            float4 tangent : TEXCOORD3;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        uniform float4 _BaseMap_ST;

        float GetOddNegativeScale() {
            return unity_WorldTransformParams.w >= 0.0 ? 1.0 : -1.0;
        }

        float3x3 CreateTangentToWorld(float3 normal, float3 tangent, float flipSign) {
            float sgn = flipSign * GetOddNegativeScale();
            float3 bitangent = cross(normal, tangent) * sgn;
            return float3x3(tangent, bitangent, normal);
        }

        float3 TransformTangentToWorld(float3 normalTS, float3x3 tangentToWorld, bool doNormalize = false) {
            float3 result = mul(normalTS, tangentToWorld);
            if (doNormalize)
                return normalize(result);
            return result;
        }

        float3 NormalTangentToWorld(float3 normalTS, float3 normalWS, float4 tangentWS) {
            float3x3 tangentToWorld =
                CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
            return TransformTangentToWorld(normalTS, tangentToWorld);
        }

        v2f vert(appdata_tan v) {
            v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex);
            //o.nz.xyz = COMPUTE_VIEW_NORMAL;
            o.nz.xyz = 0;
            o.nz.w = COMPUTE_DEPTH_01;
            o.uv = TRANSFORM_TEX(v.texcoord, _BaseMap);
            o.normal = UnityObjectToWorldNormal(v.normal);
            o.tangent = float4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w);
            return o;
        }

        uniform sampler2D _NormalMap;
        uniform fixed _NormalScale;

        fixed4 frag(v2f i) : SV_Target{
            float3 normal = UnpackNormalWithScale(tex2D(_NormalMap, i.uv), _NormalScale);
            normal = NormalTangentToWorld(normal, normalize(i.normal), normalize(i.tangent));
            return EncodeDepthNormal(i.nz.w, normal);
        }

        ENDCG
        }
    }

    SubShader
    {
    Tags { "RenderType" = "TransparentCutout" }
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
            float3 normal : TEXCOORD2;
            float4 tangent : TEXCOORD3;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        uniform float4 _BaseMap_ST;

        float GetOddNegativeScale() {
            return unity_WorldTransformParams.w >= 0.0 ? 1.0 : -1.0;
        }

        float3x3 CreateTangentToWorld(float3 normal, float3 tangent, float flipSign) {
            float sgn = flipSign * GetOddNegativeScale();
            float3 bitangent = cross(normal, tangent) * sgn;
            return float3x3(tangent, bitangent, normal);
        }

        float3 TransformTangentToWorld(float3 normalTS, float3x3 tangentToWorld, bool doNormalize = false) {
            float3 result = mul(normalTS, tangentToWorld);
            if (doNormalize)
                return normalize(result);
            return result;
        }

        float3 NormalTangentToWorld(float3 normalTS, float3 normalWS, float4 tangentWS) {
            float3x3 tangentToWorld =
                CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
            return TransformTangentToWorld(normalTS, tangentToWorld);
        }

        v2f vert(appdata_tan v) {
            v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex);
            //o.nz.xyz = COMPUTE_VIEW_NORMAL;
            o.nz.xyz = 0;
            o.nz.w = COMPUTE_DEPTH_01;
            o.uv = TRANSFORM_TEX(v.texcoord, _BaseMap);
            o.normal = UnityObjectToWorldNormal(v.normal);
            o.tangent = float4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w);
            return o;
        }

        uniform sampler2D _BaseMap;
        uniform fixed _Cutoff;
        uniform fixed4 _BaseColor;
        uniform sampler2D _NormalMap;
        uniform fixed _NormalScale;

        fixed4 frag(v2f i) : SV_Target{
            fixed4 texcol = tex2D(_BaseMap, i.uv) * _BaseColor;
            clip(texcol.a* _BaseColor.a - _Cutoff);
            float3 normal = UnpackNormalWithScale(tex2D(_NormalMap, i.uv), _NormalScale);
            normal = NormalTangentToWorld(normal, normalize(i.normal), normalize(i.tangent));
            return EncodeDepthNormal(i.nz.w, normal);
        }

        ENDCG
        }
    }
}
