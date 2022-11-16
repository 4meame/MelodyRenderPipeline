using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;


public class ChannelCombineTool: EditorWindow {



    [MenuItem("Tools/ChannelCombineTool")]
    public static void Open() {
        GetWindow<ChannelCombineTool>("ChannelCombineTool");
    }

    private Texture m_Channel_R;
    private Texture m_Channel_G;
    private Texture m_Channel_B;
    private Texture m_Channel_A;

    private Texture m_TargetTex;

    private Material m_ChangeMat;
    private string m_OutputPath = Path.Combine(Environment.CurrentDirectory, "Assets", "Textures", "MODS.png");
    private TextureFormat m_OutputFormat = TextureFormat.RGBA32;

    private XX m_R2;
    private XX m_G2;
    private XX m_B2;
    private XX m_A2;
    private enum XX {
        r,
        g,
        b,
        a
    }
    private void OnGUI() {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("R",GUILayout.Width(30));
        m_Channel_R = (Texture)EditorGUILayout.ObjectField(m_Channel_R, typeof(Texture), true);
        m_R2 = (XX)EditorGUILayout.EnumPopup(m_R2);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("G", GUILayout.Width(30));
        m_Channel_G = (Texture)EditorGUILayout.ObjectField(m_Channel_G, typeof(Texture), true);
        m_G2 = (XX)EditorGUILayout.EnumPopup(m_G2);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("B", GUILayout.Width(30));
        m_Channel_B = (Texture)EditorGUILayout.ObjectField(m_Channel_B, typeof(Texture), true);
        m_B2 = (XX)EditorGUILayout.EnumPopup(m_B2);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("A", GUILayout.Width(30));
        m_Channel_A = (Texture)EditorGUILayout.ObjectField(m_Channel_A, typeof(Texture), true);
        m_A2 = (XX)EditorGUILayout.EnumPopup(m_A2);
        EditorGUILayout.EndHorizontal();

        m_TargetTex = (Texture)EditorGUILayout.ObjectField(m_TargetTex, typeof(Texture), true);

        if (GUILayout.Button("Generate Shader")) {
            var shader =
                "Shader \"Unlit/ChangeChannel\"                                 \n" +
                "{                                                              \n" +
                "   Properties                                                  \n" +
                "   {                                                           \n" +
                "       _Channel_R(\"Texture\", 2D) = \"black\" { }             \n" +
                "       _Channel_G(\"Texture\", 2D) = \"black\" { }             \n" +
                "       _Channel_B(\"Texture\", 2D) = \"black\" { }             \n" +
                "       _Channel_A(\"Texture\", 2D) = \"black\" { }             \n" +
                "   }                                                           \n" +
                "   SubShader                                                   \n" +
                "   {                                                           \n" +
                "       Tags { \"RenderType\"=\"Opaque\" }                      \n" +
                "       LOD 100                                                 \n" +
                "       Pass                                                    \n" +
                "       {                                                       \n" +
                "           CGPROGRAM                                           \n" +
                "           #pragma vertex vert                                 \n" +
                "           #pragma fragment frag                               \n" +
                "           # include \"UnityCG.cginc\"                         \n" +
                "           struct appdata {                                    \n" +
                "           float4 vertex : POSITION;                           \n" +
                "           float2 uv : TEXCOORD0;                              \n" +
                "           };                                                  \n" +
                "           struct v2f                                          \n" +
                "           {                                                   \n" +
                "               float2 uv : TEXCOORD0;                          \n" +
                "               UNITY_FOG_COORDS(1)                             \n" +
                "               float4 vertex : SV_POSITION;                    \n" +
                "           };                                                  \n" +
                "           sampler2D _Channel_R;                               \n" +
                "           sampler2D _Channel_G;                               \n" +
                "           sampler2D _Channel_B;                               \n" +
                "           sampler2D _Channel_A;                               \n" +
                "           float4 _Channel_R_ST;                               \n" +
                "           float4 _Channel_G_ST;                               \n" +
                "           float4 _Channel_B_ST;                               \n" +
                "           float4 _Channel_A_ST;                               \n" +
                "           v2f vert(appdata v)                                 \n" +
                "           {                                                   \n" +
                "               v2f o;                                          \n" +
                "               o.vertex = UnityObjectToClipPos(v.vertex);      \n" +
                "               o.uv = TRANSFORM_TEX(v.uv, _Channel_R);         \n" +
                "               return o;                                       \n" +
                "           }                                                   \n" +
                "           float4 frag(v2f i) : SV_Target                      \n" +
                "           {                                                   \n" +
                "               float r = tex2D(_Channel_R, i.uv).x;            \n" +
                "               float g = tex2D(_Channel_G, i.uv).x;            \n" +
                "               float b = tex2D(_Channel_B, i.uv).x;            \n" +
                "               float a = tex2D(_Channel_A, i.uv).x;            \n" +
                $"               return float4({m_R2},{m_G2},{m_B2},{m_A2});    \n" +
                "           }                                                   \n" +
                "           ENDCG                                               \n" +
                "       }                                                       \n" +
                "   }                                                           \n" +
                "}                                                              \n" ;

            var path = Path.Combine(Environment.CurrentDirectory,"Assets", "Shaders", "ChangeChannel.shader");
            if (File.Exists(path)) {
                File.Delete(path);
            }
            File.WriteAllText(path, shader);
            AssetDatabase.Refresh();
        }

        if (GUILayout.Button("Preview")) {
            if (m_ChangeMat == null) {
                m_ChangeMat = new Material(Shader.Find("Unlit/ChangeChannel"));
            }

            if (m_TargetTex != null) {
                RenderTexture.ReleaseTemporary(m_TargetTex as RenderTexture);
            }

            m_ChangeMat.SetTexture("_Channel_R", m_Channel_R);
            m_ChangeMat.SetTexture("_Channel_G", m_Channel_G);
            m_ChangeMat.SetTexture("_Channel_B", m_Channel_B);
            m_ChangeMat.SetTexture("_Channel_A", m_Channel_A);
            m_TargetTex = RenderTexture.GetTemporary(m_Channel_R.width, m_Channel_R.height);
            Graphics.Blit(m_Channel_R, m_TargetTex as RenderTexture, m_ChangeMat,0);

        }


        EditorGUILayout.BeginHorizontal();
        m_OutputPath = EditorGUILayout.TextField(m_OutputPath);
        m_OutputFormat = (TextureFormat)EditorGUILayout.EnumPopup(m_OutputFormat);
        if (GUILayout.Button("Save")) {

            if (m_TargetTex != null) {
                RenderTexture.active = m_TargetTex as RenderTexture;
                Texture2D saveTex = new Texture2D(m_Channel_R.width, m_Channel_R.height, m_OutputFormat, true);
                saveTex.ReadPixels(new Rect(0, 0, saveTex.width, saveTex.height), 0, 0);
                saveTex.Apply();
        
                if (File.Exists(m_OutputPath)) {
                    File.Delete(m_OutputPath);
                }
                File.WriteAllBytes(m_OutputPath, saveTex.EncodeToPNG());
            }
            AssetDatabase.Refresh();

        }
        EditorGUILayout.EndHorizontal();

    }



}


