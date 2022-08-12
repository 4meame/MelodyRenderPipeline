using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Melody Post FX Settings")]
public class PostFXSettings : ScriptableObject {
    [SerializeField]
    Shader shader = default;
    [System.NonSerialized]
    Material material;

    public Material Material {
        get {
            if(material == null && shader != null) {
                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave;
            }
            return material;
        }
    }

    [System.Serializable]
    public struct BloomSettings
    {
        [Range(0f, 16f)]
        public int maxIterations;
        [Min(1f)]
        public int downscaleLimit;
        public bool bicubicUpsampling;
        [Range(0f, 10f)]
        public float threshold;
        [Range(0f, 1f)]
        public float thresholdKnee;
        public bool fadeFireflies;
        public enum Mode { Additive, Scattering, SingleScatter}
        public Mode mode;
        [Min(0f)]
        public float intensity;
        [Range(0.01f, 0.99f)]
        public float scatter;
        public bool ignoreRenderScale;
    }

    [System.Serializable]
    public struct ColorAdjustmentSettings
    {
        public float postExposure;
        [Range(-100f, 100f)]
        public float constrat;
        [ColorUsage(false,true)]
        public Color colorFilter;
        [Range(-180f, 180f)]
        public float hueShift;
        [Range(-100f, 100f)]
        public float saturation;
    }

    [System.Serializable]
    public struct WhiteBalanceSettings
    {
        [Range(-100f, 100f)]
        public float Temperature;
        [Range(-100f, 100f)]
        public float Tint;
    }

    [System.Serializable]
    public struct SplitToningSettings
    {
        [ColorUsage(false)]
        public Color shadows;
        [ColorUsage(false)]
        public Color highlights;
        [Range(-100f, 100f)]
        public float balance;
    }

    [System.Serializable]
    public struct ChannelMixerSettings
    {
        public Vector3 red;
        public Vector3 green;
        public Vector3 blue;
    }

    [System.Serializable]
    public struct ShadowMidtonesHighlightsSettings
    {
        [ColorUsage(false,true)]
        public Color shadows;
        [ColorUsage(false, true)]
        public Color midtones;
        [ColorUsage(false, true)]
        public Color highlights;
        [Range(0f, 2f)]
        public float shadowsStart;
        [Range(0f, 2f)]
        public float shadowsEnd;
        [Range(0f, 2f)]
        public float highlightsStart;
        [Range(0f, 2f)]
        public float highlightsEnd;
    }

    [System.Serializable]
    public struct Posterize {
        [Range(2, 255)]
        public int colorLevel;
    }

    [System.Serializable]
    public struct ToneMappingSettings
    {
        //None is color grading only
        public enum Mode { None, Reinhard, Neutral, ACES }
        public Mode mode;
    }

    [System.Serializable]
    public struct OutlineSettings
    {
        public bool enable;
        [ColorUsage(true,true)]
        public Color color;
        [Range(0f, 4f)]
        public int outlineScale;
        [Range(0f, 1f)]
        public float ColorThreshold;
        [Range(0f, 6f)]
        public float depthThreshold;
        [Range(0f, 1f)]
        public float normalThreshold;
        [Range(0f, 1f)]
        public float depthNormalThreshold;
        [Range(0f, 18f)]
        public float depthNormalThresholdScale;
    }

    [System.Serializable]
    public struct LightShaftsSettings
    {
        public enum Mode { Occlusion, Bloom }
        public Mode mode;
        public bool enable;
        [Range(2, 8)]
        public int Downsample;
        public Vector4 lightShaftParameters;
        public Vector4 radialBlurParameters;
        [Range(0, 2)]
        public float density;
        [Range(0, 2)]
        public float weight;
        [Range(0, 2)]
        public float decay;
        [Range(0, 2)]
        public float exposure;
        public Color bloomTintAndThreshold;
    }

    [SerializeField]
    BloomSettings bloom = default;
    [SerializeField]
    ColorAdjustmentSettings colorAdjustment = new ColorAdjustmentSettings { colorFilter = Color.white };
    [SerializeField]
    WhiteBalanceSettings whiteBalance = default;
    [SerializeField]
    SplitToningSettings splitToning = new SplitToningSettings { shadows = Color.gray, highlights = Color.gray };
    [SerializeField]
    ChannelMixerSettings channelMixer = new ChannelMixerSettings { red = Vector3.right, green = Vector3.up, blue = Vector3.forward };
    [SerializeField]
    ShadowMidtonesHighlightsSettings shadowMidtonesHighlight = new ShadowMidtonesHighlightsSettings {
        shadows = Color.white,
        midtones = Color.white,
        highlights = Color.white,
        shadowsEnd = 0.3f,
        highlightsStart = 0.55f,
        highlightsEnd = 1f
    };
    [SerializeField]
    Posterize posterize = new Posterize { colorLevel = 2 };
    [SerializeField]
    ToneMappingSettings toneMapping = default;
    [SerializeField]
    OutlineSettings outlineSetting = new OutlineSettings { enable = false, color = Color.black, outlineScale = 1 };
    [SerializeField]
    LightShaftsSettings lightShaftsSetting = default;
    public BloomSettings Bloom => bloom;
    public ColorAdjustmentSettings ColorAdjustment => colorAdjustment;
    public WhiteBalanceSettings WhiteBalance => whiteBalance;
    public SplitToningSettings SplitToning => splitToning;
    public ChannelMixerSettings ChannelMixer => channelMixer;
    public ShadowMidtonesHighlightsSettings ShadowMidtonesHighlights => shadowMidtonesHighlight;
    public Posterize posterizes => posterize;
    public ToneMappingSettings ToneMapping => toneMapping;
    public OutlineSettings OutlineSetting => outlineSetting;
    public LightShaftsSettings LightShaftsSetting => lightShaftsSetting;
}
