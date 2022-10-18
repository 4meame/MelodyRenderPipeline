using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.Rendering;
using UnityEngine;

[Serializable]
public class PhyscialCameraSettings {
	public const float MinAperture = 0.7f;
	public const float MaxAperture = 32f;
	public const int MinBladeCount = 3;
	public const int MaxBladeCount = 11;
	public enum SensorType { Custom }
	[Header("Camera Body")]
	public SensorType sensorType = SensorType.Custom;
	public Vector2 sensorSize = new Vector2(70.0f, 51.0f);
	public int ISO = 200;
	public float shutterSpeed = 0.005f;
	public Camera.GateFitMode gateFit = Camera.GateFitMode.Horizontal;
	[Header("Lens")]
	[Min(0.1117f)]
	public float focalLength = 20.0f;
	public Vector2 shift = Vector2.zero;
	[Range(MinAperture, MaxAperture)]
	public float fStop = 3.0f;
	[Min(0.1f)]
	public float focusDistance = 10.0f;
	[Header("Aperture Shape")]
	[Range(MinBladeCount, MaxBladeCount)]
	public int bladeCount = 8;
	[Min(MinAperture)]
	public Vector2 curvature = new Vector2( 2.0f, 11.0f);
	[Range(0, 1.0f)]
	public float barrelClipping = 0.25f;
	[Range(-1.0f, 1.0f)]
	public float anamorphism = 0.0f;
}
