using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Volumetric Cloud/Noise Settings")]
public class CloudNoiseSettings : ScriptableObject
{
    public int seed;
    [Header("Worley")]
    [Range(1, 50)]
    public int numDivisionsA = 5;
    [Range(1, 50)]
    public int numDivisionsB = 10;
    [Range(1, 50)]
    public int numDivisionsC = 15;

    public float persistence = 0.5f;
    public int tile = 1;
    public bool invert = true;
    public bool blendPerlin = false;
    [Header("Perlin")]
    public int octave = 6;
    public float frequency = 2.0f;
    public float amplitude = 0.5f;
    public float lacunarity = 0.5f;
    public Vector3 offset = new Vector3(0f, 0f, 0f);
}
