using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[Serializable]
public struct CameraBufferSettings {
	public bool allowHDR;
    [Range(0.1f, 2f)]
    public float renderScale;
    public enum RescalingMode { Linear, Point, Bicubic }
    public RescalingMode rescalingMode;
    public enum RenderingPath { Forward, Deferred }
    public RenderingPath renderingPath;
    public bool copyDepth;
    public bool copyDepthReflections;
    public bool copyColor;
    public bool copyColorReflections;
    [Header("Invalid on deferred")]
    public bool useDepthNormal;
    public bool useDiffuse;
    public bool useSpecular;
	[Serializable]
	public struct FXAA {
		public bool enabled;
		//0.0833 - upper limit (default, the start of visible unfiltered edges)
		//0.0625 - high quality (faster)
		//0.0312 - visible limit (slower)
		[Range(0.0312f, 0.0833f)]
		public float fixedThreshold;
		//0.333 - too little (faster)
		//0.250 - low quality
		//0.166 - default
		//0.125 - high quality 
		//0.063 - overkill (slower)
		[Range(0.063f, 0.333f)]
		public float relativeThreshold;
		//1.00 - upper limit (softer)
		//0.75 - default amount of filtering
		//0.50 - lower limit (sharper, less sub-pixel aliasing removal)
		//0.25 - almost off
		//0.00 - completely off
		[Range(0f, 1f)]
		public float subpixelBlending;
		public enum Quality { Low, Medium, High }
		public Quality quality;
	}
	public FXAA fxaa;

    [Serializable]
    public struct TAA {
        public enum Mode {
            None,
            Common,
            Adaptive
        }
        public Mode mode;
        public bool motionVectorEnabled;
        [Header("Adaptive TAA")]
        [Range(0.0f, 1.0f)]
        public float jitterScale;
        [Range(0.0f, 3.0f)]
        public float sharpness;
        [Range(0.0f, 0.99f)]
        public float staticBlending;
        [Range(0.0f, 0.99f)]
        public float motionBlending;
        [Range(0.05f, 6.0f)]
        public float staticAABBScale;
        [Range(0.05f, 6.0f)]
        public float motionAABBScale;
        [Header("Common TAA")]
        [Range(0.0f, 1.0f)]
        public float feedBack;
    }
    public TAA taa;

    [Serializable]
    public struct SSPR {
        public enum TextureSize {
            _256 = 256,
            _512 = 512,
            _1024 = 1024,
            _2048 = 2048,
        }

        public bool enabled;
        public ComputeShader computeShader;
        [Header("Reflect")]
        public TextureSize reflectionTextureSize;
        public bool ApplyFillHoleFix;

        public float HorizontalReflectionPlaneHeightWS;
        [Header("Fade")]
        [Range(0.01f, 1f)]
        public float FadeOutScreenBorderWidthVerticle;
        [Range(0.01f, 1f)]
        public float FadeOutScreenBorderWidthHorizontal;
        [Range(0, 8f)]
        public float ScreenLRStretchIntensity;
        [Range(-1f, 1f)]
        public float ScreenLRStretchThreshold;
        [ColorUsage(true, true)]
        public Color TintColor;
    }
    public SSPR sspr;

    [Serializable]
    public struct SSR {
        public bool enabled;
        public ComputeShader computeShader;
        public enum SSRType {
            SSR,
            StochasticSSR
        }
        public enum DebugMode {
            Combine,
            CombineNoCubemap,
            Reflection,
            CubeMap,
            ReflectionAndCubemap,
            Mask,
            PDF,
            Jitter,
            RO,
            Motion
        };
        public enum TraceMethod {
            HiZTrace = 0,
            LinearTrace = 1
        };
        public enum RenderSize {
            Full = 1,
            Half = 2
        };
        public SSRType sSRType;
        [Header("SSR")]
        [Range(1, 8)]
        public int downSample;
        public float maxDistance;
        [Range(1,300)]
        public int iterations;
        [Range(1, 30)]
        public int binarySearchIterations;
        [Range(1, 50)]
        public int pixelStrideSize;
        public float pixelStrideZCuttoff;
        public float thickness;
        [Range(0, 0.999f)]
        public float screenEdgeFade;
        [Range(0, 1f)]
        public float eyeFadeStart;
        [Range(0, 1)]
        public float eyeFadeEnd;
        [Header("StochasticSSR")]
        public TraceMethod traceMethod;
        public RenderSize rayCastSize;
        [Range(1, 4)]
        public int rayNums;
        [Range(0, 1)]
        public float BRDFBias;
        [Range(0.05f, 5f)]
        public float THK;
        [Range(0, 1)]
        public float screenFade;
        public bool traceBehind;
        public bool traceTowardRay;
        public bool ReflectionOcclusion;
        [Range(32, 512)]
        public int Hiz_RaySteps;
        [Range(0, 0.001f)]
        public float Hiz_Threshold;
        [Range(4, 10)]
        public int Hiz_MaxLevel;
        [Range(0, 2)]
        public int Hiz_StartLevel;
        [Range(0, 2)]
        public int Hiz_StopLevel;
        [Range(64, 512)]
        public int Linear_RaySteps;
        [Range(5, 20)]
        public int Linear_StepSize;
        public Texture2D BlueNoise;
        public Texture2D PreintegratedGF;
        [Range(1, 9)]
        public int SpatioSampler;
        [Range(0, 0.99f)]
        public float TemporalWeight;
        [Range(1, 6f)]
        public float TemporalScale;
        public bool deNoise;
        public DebugMode debugMode;
    }
    public SSR ssr;

    [Serializable]
    public struct SSAO {
        public enum AOType {
            pureDepthAO,
            SSAO,
            HBAO,
            GTAO,
        }
        public enum FilterType {
            NormalBilateral,
            AdaptionBilateral,
        }
        public enum DebugType {
            Common,
            AO,
            RO
        }
        public AOType aOType;
        public FilterType filterType;
        public DebugType debugType;
        public bool enabled;
        public ComputeShader computeShader;
        public Texture2D randomTexture;
        [Range(1, 4)]
        public int downSample;
        [Range(1, 32)]
        public int sampleCount;
        [Range(0, 32)]
        public float aoRadius;
        [Range(0, 16)]
        public float filterRadius;
        [Range(0, 1)]
        public float filterFactor;
        [Range(0, 16)]
        public int kernelSize;
        [Header("Pure Depth AO")]
        public Vector4 pureDepthAOParameters;
        [Header("SSAO")]
        public Vector4 SSAOParameters;
        [Header("HBAO")]
        [Range(1, 32)]
        public int numDirection;
        [Range(1, 100)]
        public int maxRadiusPixel;
        [Range(-1, 1)]
        public float tanBias;
        [Range(0, 5)]
        public float hbaoStrength;
        [Header("GTAO")]
        public Vector4 fadeParams;
        [Range(1, 8)]
        public int numSlice;
        [Range(-2, 2)]
        public float thickness;
        [Range(0, 9)]
        public float gtaoStrength;
        public bool multipleBounce;

    }
    public SSAO ssao;

    [Serializable]
    public struct GI
    {
        public enum GIType
        {
            SSGI,
        }
        public enum RenderSize
        {
            Full = 1,
            Half = 2
        };
        public enum TraceType
        {
            HiZTrace = 0,
            LinearTrace = 1
        };
        public enum FilterType
        {
            BrdfWeight,
            NormalBilateral,
            AdaptionBilateral,
        }
        public enum DebugType
        {
            Combine,
            Indirect,
            Occlusion
        }
        public GIType giType;
        public bool enabled;
        [Header("SSGI")]
        public RenderSize rayCastSize;
        public FilterType filterType;
        [Range(1, 16)]
        public int rayNums;
        [Range(0, 5f)]
        public float THK;
        [Range(0, 0.5f)]
        public float screenFade;
        public bool traceBehind;
        public bool rayMask;
        public TraceType traceType;
        [Range(32, 512)]
        public int Hiz_RaySteps;
        [Range(0, 0.001f)]
        public float Hiz_Threshold;
        [Range(4, 10)]
        public int Hiz_MaxLevel;
        [Range(0, 2)]
        public int Hiz_StartLevel;
        [Range(0, 2)]
        public int Hiz_StopLevel;
        [Range(64, 512)]
        public int Linear_RaySteps;
        [Range(5, 20)]
        public int Linear_StepSize;
        public Texture2D randomTexture;
        [Range(1, 128)]
        public float intensity;
        [Range(1, 18)]
        public int SpatioKernel;
        [Range(0.1f, 6)]
        public float SpatioRadius;
        [Range(0, 0.99f)]
        public float TemporalWeight;
        [Range(1, 6f)]
        public float TemporalScale;
        public bool deNoise;
        public DebugType debugType;
    }
    public GI gi;
}
