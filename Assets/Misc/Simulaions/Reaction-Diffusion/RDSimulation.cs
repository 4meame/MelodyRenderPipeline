using UnityEngine;
using UnityEngine.Experimental.Rendering;
using ComputeShaderUtility;

public class RDSimulation : MonoBehaviour
{
	public int width = 1920;
	public int height = 1080;
	public ComputeShader compute;

	public RDSettings settings;

	public FilterMode filterMode = FilterMode.Point;
	public GraphicsFormat format = ComputeHelper.defaultGraphicsFormat;

	protected RenderTexture map;
	protected RenderTexture newMap;
	protected RenderTexture displayMap;
	bool needsDisplayUpdate = true;


	void Start()
	{
		Time.fixedDeltaTime = 1 / 60.0f;
		Init();

		var material = transform.GetComponentInChildren<MeshRenderer>().material;
		material.SetTexture(Shader.PropertyToID("_BaseMap"), displayMap);
	}


	void Init()
	{

		// Create render textures
		ComputeHelper.CreateRenderTexture(ref map, width, height, filterMode, format, "Map");
		ComputeHelper.CreateRenderTexture(ref newMap, width, height, filterMode, format, "New Map");
		ComputeHelper.CreateRenderTexture(ref displayMap, width, height, filterMode, format, "Display Map");

		compute.SetInt("width", width);
		compute.SetInt("height", height);

		compute.SetTexture(0, "Map", map);
		compute.SetTexture(0, "InitMap", settings.initMap);
		ComputeHelper.Dispatch(compute, width, height, 1, kernelIndex: 0);
	}



	void RunSimulation()
	{
		compute.SetFloat("deltaTime", Time.fixedDeltaTime);
		compute.SetTexture(1, "Map", map);
		compute.SetTexture(1, "NewMap", newMap);

		settings.SendToShader(compute);

		// Run
		ComputeHelper.Dispatch(compute, width, height, 1, kernelIndex: 1);

		ComputeHelper.CopyRenderTexture(newMap, map);

	}

	void Display()
	{

		compute.SetTexture(2, "Map", map);
		compute.SetTexture(2, "DisplayMap", displayMap);
		ComputeHelper.Dispatch(compute, width, height, 1, kernelIndex: 2);

	}


	void FixedUpdate()
	{
		for (int i = 0; i < settings.numStepsPerFrame; i++)
		{
			RunSimulation();
		}
		needsDisplayUpdate = true;
	}


	void Update()
	{
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			Application.Quit();
		}

		if (needsDisplayUpdate)
		{
			Display();
			needsDisplayUpdate = false;
		}

	}

	void OnDestroy()
	{
		ComputeHelper.Release(map, displayMap, newMap);
	}

}
