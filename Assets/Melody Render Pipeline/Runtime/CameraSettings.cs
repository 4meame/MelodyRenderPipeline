using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.Rendering;
using UnityEngine;

[Serializable]
public class CameraSettings {
	[Serializable]
	public struct FinalBlendMode {
		public BlendMode source, destination;
	}

	public enum RenderScaleMode { Inherit, Multiply, Override }

	public bool copyDepth = true;
	public bool copyColor = true;
	public FinalBlendMode finalBlendMode = new FinalBlendMode { source = BlendMode.One, destination = BlendMode.Zero };
	public bool overridePostFX = false;
	public PostFXSettings postFXSettings = default;
	public bool allowFXAA = true;
    public bool allowSSPR = false;
	public bool allowSSR = false;
	public bool allowCloud = true;
	public bool updateAtmosScatter = true;
	public RenderScaleMode renderScaleMode = RenderScaleMode.Inherit;
	[Range(0.1f, 2f)]
	public float renderScale = 1f;
	public float GetRenderScale(float scale) {
		return renderScaleMode == RenderScaleMode.Inherit ? scale :
			   renderScaleMode == RenderScaleMode.Override ? renderScale :
			   scale * renderScale;
	}
	public bool keepAlpha = false;
}
