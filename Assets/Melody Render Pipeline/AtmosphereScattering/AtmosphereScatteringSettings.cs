using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Atmosphere Scattering/Render Settings")]
public class AtmosphereScatteringSettings : ScriptableObject {
    public float planetRadius = 6371000.0f;
    public float atmosphereHeight = 80000.0f;
    public float groundHeight = 500.0f;
    public Vector2 densityScaleHeight = new Vector2(7944.0f, 1200.0f);
    [ColorUsage(false,true)]
    public Color incomingLight = new Color(4, 4, 4, 4);
    public Vector3 rayleighCoefficients = new Vector3(5.5f, 13.0f, 22.4f);
    public float rayleighInscatterScale = 1.0f;
    public float rayleighExtinctionScale = 1.0f;
    public Vector3 mieCoefficients = new Vector3(21.0f, 21.0f, 21.0f);
    public float mieInscatterScale = 1.0f;
    public float mieExtinctionScale = 1.0f;
    public float mieG = 0.625f;
    [Header("Texture")]
    public int particleDensityLUTSize = 1024;
    public int sunColorLUTSize = 512;
}
