using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Simulation/Reaction-Diffusion")]
public class RDSettings : ScriptableObject
{
	public enum DisplayMode
	{
		AB,
		Greyscale,
		Delta1,
		Delta2
	}

	public int numStepsPerFrame = 1;

	public DisplayMode displayMode;
	public Texture2D initMap;

	[Header("Behaviour")]
	[Range(0, 0.1f)]
	public float feedRate;
	[Range(0, 0.1f)]
	public float removeRate;
	[Range(0, 1f)]
	public float diffuseRateA;
	[Range(0, 1f)]
	public float diffuseRateB;
	[Range(2, 8)]
	public int diffuseRadius;


	public void SendToShader(ComputeShader compute)
	{
		compute.SetFloat(nameof(feedRate), feedRate);
		compute.SetFloat(nameof(removeRate), removeRate);
		compute.SetFloat(nameof(diffuseRateA), diffuseRateA);
		compute.SetFloat(nameof(diffuseRateB), diffuseRateB);
		compute.SetInt(nameof(diffuseRadius), diffuseRadius);

		// Processing
		compute.SetInt("displayMode", (int)displayMode);
	}
}
