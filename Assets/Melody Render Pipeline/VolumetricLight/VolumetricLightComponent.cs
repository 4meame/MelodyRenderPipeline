using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
[RequireComponent(typeof(Light))]
public class VolumetricLightComponent : MonoBehaviour {
    [Range(1, 64)]
    public int sampleCount = 8;
    [Range(0.0f, 1.0f)]
    public float scatteringCoef = 0.5f;
    [Range(0.0f, 0.1f)]
    public float extinctionCoef = 0.01f;
    [Range(0.0f, 1.0f)]
    public float skyBackgroundExtinctionCoef = 0.9f;
    [Range(0.0f, 0.999f)]
    public float mieG = 0.1f;
    public bool HeightFog = false;
    [Range(0, 0.5f)]
    public float heightScale = 0.10f;
    public float groundHeight = 0;
    public bool useNoise = false;
    public float noiseScale = 0.015f;
    public float noiseIntensity = 1.0f;
    public float noiseIntensityOffset = 0.3f;
    public Vector2 noiseVelocity = new Vector2(3.0f, 3.0f);
    bool reversedZ = false;
    [HideInInspector]
    public Material material;
    [Min(1)]
    public float maxRayLength;

    void OnEnable() {
        material = new Material(Shader.Find("Hidden/Melody RP/VolumetricLight"));
    }

    void OnDisable() {
        DestroyImmediate(material);
    }
}
