using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using ComputeShaderUtility;

public class CASimulation : MonoBehaviour
{
    const int simKernel = 0;
    const int displayKernel = 1;
    public int width = 1920;
    int height = 1080;

    public CASettings settings;
    public ComputeShader simCompute;

    [Header("Info")]
    public FilterMode filterMode = FilterMode.Point;
    public int frameCounter;

    RenderTexture simulationMap;
    RenderTexture nextSimulationMap;
    RenderTexture displayTexture;
    ComputeBuffer sensorBuffer;
    bool displayNeedsUpdate = true;
    Material material;

    private void Start()
    {
        {
            material = transform.GetComponentInChildren<MeshRenderer>().material;
            Init();
        }
    }

    private void FixedUpdate()
    {
        for (int i = 0; i < settings.stepsPerFrame; i++)
        {
            RunSimulation();
            displayNeedsUpdate = true;
        }
    }

    private void Update()
    {
        if (displayNeedsUpdate)
        {
            ComputeHelper.Dispatch(simCompute, width, height, 1, displayKernel);
            displayNeedsUpdate = false;
        }
    }


    private void OnDestroy()
    {
        ComputeHelper.Release(sensorBuffer);
        ComputeHelper.Release(simulationMap, nextSimulationMap, displayTexture);
    }

    void Init()
    {
        height = Mathf.RoundToInt(width * 9 / 16f);
        const int targetFPS = 60;
        Time.fixedDeltaTime = 1.0f / targetFPS;

        GraphicsFormat displayFormat = GraphicsFormat.R32G32B32A32_SFloat;

        ComputeHelper.CreateRenderTexture(ref simulationMap, width, height, filterMode, displayFormat, "Sim Texture");
        ComputeHelper.CreateRenderTexture(ref nextSimulationMap, width, height, filterMode, displayFormat, "Next Sim Texture");
        ComputeHelper.CreateRenderTexture(ref displayTexture, width, height, filterMode, displayFormat, "Display Texture");

        ComputeHelper.SetRenderTexture(simulationMap, simCompute, "SimMap", simKernel, displayKernel);
        ComputeHelper.SetRenderTexture(nextSimulationMap, simCompute, "NextSimMap", simKernel);
        ComputeHelper.SetRenderTexture(displayTexture, simCompute, "DisplayMap", displayKernel);

        simCompute.SetInt("width", width);
        simCompute.SetInt("height", height);
        simCompute.SetVector("noiseOffset", settings.noiseOffset);

        material.SetTexture(Shader.PropertyToID("_BaseMap"), displayTexture);
        frameCounter = 0;
    }


    void RunSimulation()
    {
        simCompute.SetInt("frameCount", frameCounter);

        ComputeHelper.CreateStructuredBuffer<Sensor>(ref sensorBuffer, settings.sensors);
        simCompute.SetBuffer(simKernel, "Sensors", sensorBuffer);

        // Run
        ComputeHelper.Dispatch(simCompute, width, height, 1, simKernel);
        ComputeHelper.CopyRenderTexture(nextSimulationMap, simulationMap);

        frameCounter++;
    }

    public void Reset()
    {
        frameCounter = 0;
    }
}
