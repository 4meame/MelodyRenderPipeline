using UnityEngine;

public abstract class SimulationBase : MonoBehaviour
{
	public CASettings settings;

	protected Material material;
	bool displayNeedsUpdate = true;

	protected abstract void Init();
	protected abstract void RunSimulation();
	protected abstract void UpdateDisplay();
	protected abstract void ReleaseBuffers();
	protected virtual void HandleInput() { }


	void Awake()
	{
		material = transform.GetComponentInChildren<MeshRenderer>().material;
		Init();
	}

	void FixedUpdate()
	{
		for (int i = 0; i < settings.stepsPerFrame; i++)
		{
			RunSimulation();
			displayNeedsUpdate = true;
		}
	}

	void Update()
	{
		HandleInput();

		if (displayNeedsUpdate)
		{
			UpdateDisplay();
			displayNeedsUpdate = false;
		}

		if (Input.GetKeyDown(KeyCode.Escape))
		{
			Application.Quit();
		}

	}

	void OnDestroy()
	{
		ReleaseBuffers();
	}

}
