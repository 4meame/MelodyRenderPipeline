using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Atmosphere Scattering/Render Settings")]
public class AtmosphereScatteringSettings : ScriptableObject {
    public enum Mode {
        Common,
        Precompute
    }
    public enum DebugMode {
        None,
        Inscattering,
        Extinction
    }
    public bool updateEveryFrame;
    public ComputeShader computeShader;
    public Mode mode = Mode.Common;
    public DebugMode debugMode = DebugMode.None;
    [Header("Planet Settings")]
    public float planetRadius = 6371000.0f;
    public float atmosphereHeight = 80000.0f;
    public float groundHeight = 500.0f;
    public Vector2 densityScaleHeight = new Vector2(7944.0f, 1200.0f);
    public Vector3 rayleighCoefficients = new Vector3(5.5f, 13.0f, 22.4f);
    public Vector3 mieCoefficients = new Vector3(21.0f, 21.0f, 21.0f);
    public float mieG = 0.625f;
    [Header("Sample Settings")]
    [ColorUsage(false, true)]
    public Color incomingLight = new Color(4, 4, 4, 4);
    public float sunIntensity = 0.3f;
    [Range(1, 128)]
    public int lightSamples = 64;
    public float distanceScale = 30.0f;
    public float rayleighInscatterScale = 1.0f;
    public float rayleighExtinctionScale = 1.0f;
    public float mieInscatterScale = 1.0f;
    public float mieExtinctionScale = 1.0f;
    [Header("Lighting")]
    [Min(0)]
    public float directIntensity = 1.0f;
    [Min(0)]
    public float ambientIntensity = 1.0f;
    [Header("Texture Settings")]
    public int particleDensityLUTSize = 1024;
    public int sunColorLUTSize = 256;
    public int ambientLUTSize = 128;
    public Vector3 atmosphereScatterLUTSize = new Vector3(32, 128, 32);
    public Vector3 inscatterExtinctionLUTSize = new Vector3(8, 8, 64);
}
