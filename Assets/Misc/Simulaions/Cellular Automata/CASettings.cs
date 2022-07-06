using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ComputeShaderUtility;

[CreateAssetMenu(menuName = "Simulation/Cellular Automata Settings")]
public class CASettings : ScriptableObject
{
    public const int numSensors = 8;
    public int stepsPerFrame = 1;
    public Vector2 noiseOffset;
    public Sensor[] sensors;

    public void RandomizeConditions(int seed)
    {
        System.Random prng = new System.Random(seed);
        if (sensors == null || sensors.Length != numSensors)
        {
            sensors = new Sensor[numSensors];
        }

        for (int i = 0; i < sensors.Length; i++)
        {

            sensors[i].radiusMinMax = RandomRadii(prng);
            sensors[i].aliveMinMax = CalculateMinMaxPair(prng);
            sensors[i].deadMinMax = CalculateMinMaxPair(prng);
        }
    }

    static Vector2Int RandomRadii(System.Random prng)
    {
        const int maxPossibleRadius = 10;
        int radiusA = prng.Next(0, maxPossibleRadius);
        int radiusB = prng.Next(0, maxPossibleRadius);
        int minRadius = (radiusA < radiusB) ? radiusA : radiusB;
        int maxRadius = (radiusA > radiusB) ? radiusA : radiusB;
        return new Vector2Int(minRadius, maxRadius);
    }

    static Vector2 CalculateMinMaxPair(System.Random prng)
    {
        float a = (float)prng.NextDouble();
        float b = (float)prng.NextDouble();

        if (a > b)
        {
            (a, b) = (b, a);
        }

        return new Vector2(a, b);
    }
}

[System.Serializable]
public struct Sensor
{
    public Vector2Int radiusMinMax;
    public Vector2 aliveMinMax;
    public Vector2 deadMinMax;
}