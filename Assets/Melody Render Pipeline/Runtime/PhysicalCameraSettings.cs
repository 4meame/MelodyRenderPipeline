using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.Rendering;
using UnityEngine;

[Serializable]
public class PhyscialCameraSettings {
	public enum SensorType { Custom }
	public enum GateFit { Vertical, Horizontal, Fill, Overscan, None}
	[Header("Camera Body")]
	public SensorType sensorType = SensorType.Custom;
	public Vector2 sensorSize = new Vector2(70.0f, 51.0f);
	public int ISO = 200;
	public float shutterSpeed = 0.005f;
	public GateFit gateFit = GateFit.Horizontal;
	[Header("Lens")]
	public float focalLength = 20.0f;
	public Vector2 shift = Vector2.zero;
	[Range(0, 32.0f)]
	public float fStop = 3.0f;
	public float focusDistance = 10.0f;
	[Header("Aperture Shape")]
	public int bladeCount = 8;
	public Vector2 curvature = new Vector2(2.0f, 11.0f);
	[Range(0, 1.0f)]
	public float barrelClipping = 0.25f;
	[Range(-1.0f, 1.0f)]
	public float anamorphism = 0.0f;
}
