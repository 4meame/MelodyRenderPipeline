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
        public int colorRamp;
        [Range(0, 3)]
        public float rampGamma;
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

    [System.Serializable]
    public struct MotionBlurSettings {
        public enum Mode { None, Manual, Physical }
        public Mode mode;
        [Range(0, 360)]
        public float shutterAngle;
        [Range(0, 64)]
        public int sampleCount;
        [Range(0, 1)]
        public float frameBlending;
    }

    [System.Serializable]
    public struct AutoExposureSettings {
        public ComputeShader autoExposure;
        public ComputeShader logHistogram;
        public enum MeteringMode { None, Auto, Curve, Physical }
        public MeteringMode metering;
        public enum MeteringMask { None, Vignette, Custom }
        public MeteringMask meteringMask;
        public Texture2D mask;
        [Range(-10, 10)]
        public float minEV;
        [Range(-10, 10)]
        public float maxEV;
        [Range(1,99)]
        public float lowPercent;
        [Range(1, 99)]
        public float highPercent;
        public float compensation;
        public enum AdaptationMode { Fixed, Progressive }
        public AdaptationMode adaptation;
        [Min(0)]
        public float speedUp;
        [Min(0)]
        public float speedDown;
    }

    [System.Serializable]
    public struct DepthOfFieldSettings {
        public ComputeShader dofKernel;
        public ComputeShader dofCoc;
        public ComputeShader dofReproj;
        public ComputeShader dofPrefitler;
        public ComputeShader dofMipGen;
        public ComputeShader dofTileMax;
        public ComputeShader dofClear;
        public ComputeShader dofDilate;
        public ComputeShader dofGather;
        public ComputeShader dofPreCombine;
        public ComputeShader dofCombine;
        public enum FocusMode { None, Manual, Physical }
        public FocusMode focusMode;
        public enum FocusDistanceMode { Post, Camera }
        public FocusDistanceMode focusDistanceMode;
        public enum Resolution { Full = 1, Half = 2 }
        public Resolution resolution;
        public bool taaEnabled;
        [Min(0.1f)]
        public float focusDistance;
        [Min(0)]
        public float nearRangeStart;
        [Min(0)]
        public float nearRangeEnd;
        [Min(0)]
        public float farRangeStart;
        [Min(0)]
        public float farRangeEnd;
        [Range(3, 8)]
        public int nearBlurSampleCount;
        [Range(0, 8f)]
        public float nearBlurMaxRadius;
        [Range(3, 16f)]
        public int farBlurSampleCount;
        [Range(0, 16f)]
        public float farBlurMaxRadius;
        public ComputeShader dofAdvanced;
        public bool useAdvanced;
    }

    [System.Serializable]
    public struct LensFlareSettings {
        public Shader lensFlareShader;
        public ComputeShader mergeOcclusion;
        public enum Mode { None, Manual, Physical }
        public Mode mode;
        public bool antiAliasing;
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
    Posterize posterize = new Posterize { colorRamp = 256, rampGamma = 0.3f };
    [SerializeField]
    ToneMappingSettings toneMapping = default;
    [SerializeField]
    OutlineSettings outlineSetting = new OutlineSettings { enable = false, color = Color.black, outlineScale = 1 };
    [SerializeField]
    LightShaftsSettings lightShaftsSetting = default;
    [SerializeField]
    MotionBlurSettings motionBlurSetting = default;
    [SerializeField]
    AutoExposureSettings autoExposureSetting = default;
    [SerializeField]
    DepthOfFieldSettings depthOfFieldSetting = default;
    [SerializeField]
    LensFlareSettings lensFlareSetting = default;
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
    public MotionBlurSettings motionBlurSettings => motionBlurSetting;
    public AutoExposureSettings autoExposureSettings => autoExposureSetting;
    public DepthOfFieldSettings depthOfFieldSettings => depthOfFieldSetting;
    public LensFlareSettings lensFlareSettings => lensFlareSetting;
}
