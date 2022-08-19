using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[Serializable]
public struct CameraBufferSettings {
	public bool allowHDR;
	public bool copyDepth, copyDepthReflections;
	public bool copyColor, copyColorReflections;
	public bool useDepthNormal;
    public bool usePostGeometryColor;
	[Range(0.1f, 2f)]
	public float renderScale;
	public enum RescalingMode { Linear, Point, Bicubic }
	public RescalingMode rescalingMode;

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
        [Header("Trace")]
        [Range(1, 8)]
        public int downsample;
        public float maxDistance;
        [Range(1,300)]
        public int iterations;
        [Range(1, 30)]
        public int binarySearchIterations;
        [Range(1, 50)]
        public int pixelStrideSize;
        public float pixelStrideZCuttoff;
        public float thickness;
        [Header("Fade")]
        [Range(0, 0.999f)]
        public float screenEdgeFade;
        [Range(0, 1f)]
        public float eyeFadeStart;
        [Range(0, 1)]
        public float eyeFadeEnd; 
        public bool debug;
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
            DepthBilateral,
        }
        public AOType aOType;
        public FilterType filterType;
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
        public float filterFactor;
        [Range(0, 16)]
        public int kernelSize;
        [Header("Pure Depth AO")]
        public Vector4 pureDepthAOParameters;
        [Header("SSAO")]
        public Vector4 SSAOParameters;
        [Header("HBAO")]
        [Range(1, 32)]
        public int numDirections;
        [Range(1, 100)]
        public int maxRadiusPixels;
        [Range(-1, 1)]
        public float tanBias;
        [Range(0, 5)]
        public float hbaoStrength;
        [Header("GTAO")]
        public float thickness;

        public bool debug;
    }
    public SSAO ssao;
}
