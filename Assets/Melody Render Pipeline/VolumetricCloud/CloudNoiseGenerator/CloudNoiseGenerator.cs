using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class CloudNoiseGenerator : MonoBehaviour {
    const int computeThreadGroupSize = 8;
    public const string detailNoiseName = "DetalNoise";
    public const string shapeNoiseName = "ShapeNoise";
    public enum CloudNoiseType { Shape, Detail }
    public enum TextureChannel { R, G, B, A }
    public ComputeShader noiseCompute;
    public ComputeShader copy;
    [Header("Editor Settings")]
    public CloudNoiseType activeType;
    public TextureChannel activeChannel;
    public bool autoUpdate;
    //log computing time
    public bool logComputeTime;
    [Header("Noise Settings")]
    public int detailResolution = 32;
    public int shapeResolution = 128;
    public CloudNoiseSettings[] detailNoise;
    public CloudNoiseSettings[] shapeNoise;
    [Header("Display Settings")]
    public bool displayEnable;
    public bool displayGreyScale;
    public bool displayAllChannel;
    [Range(0, 1)]
    public float displaySliceDepth;
    [Range(1, 5)]
    public float displayTileAmount = 1;
    [Range(0, 1)]
    public float displaySize = 1;

    List<ComputeBuffer> buffersToRelease;
    bool updateNoise;
    [HideInInspector]
    public bool showSettingsEditor = true;
    [SerializeField, HideInInspector]
    public RenderTexture shapeTexture;
    [SerializeField, HideInInspector]
    public RenderTexture detailTexture;

    public void UpdateWorleyNoise() {
        ValidateParameters();
        CreateTexture(ref detailTexture, detailResolution, detailNoiseName);
        CreateTexture(ref shapeTexture, shapeResolution, shapeNoiseName);
        if(updateNoise && noiseCompute) {
            //--------------NOTE HERE---------------
            var timer = System.Diagnostics.Stopwatch.StartNew();

            updateNoise = false;
            CloudNoiseSettings activeSettings = ActiveSettings;
            if (activeSettings == null) {
                return;
            }
            buffersToRelease = new List<ComputeBuffer>();
            int activeTextureResolution = ActiveTexture.width;
            //set current active setting
            noiseCompute.SetFloat("persistence", activeSettings.persistence);
            noiseCompute.SetInt("resolution", activeTextureResolution);
            noiseCompute.SetVector("channelMask", ChannelMask);
            //set noise gen kernel data
            noiseCompute.SetTexture(0, "Result", ActiveTexture);
            //keep track of min max value(using int to support atomic operation)
            var minMaxBuffer = CreateBuffer(new int[] { int.MaxValue, 0 }, sizeof(int), "minMax", 0);
            UpdateNoiseSettings(ActiveSettings);
            //really ?
            noiseCompute.SetTexture(0, "Result", ActiveTexture);
            //dispatch noise gen kernel
            int numthreadGroups = Mathf.CeilToInt(activeTextureResolution / (float)computeThreadGroupSize);
            noiseCompute.Dispatch(0, numthreadGroups, numthreadGroups, numthreadGroups);
            //set normalization kernel data
            noiseCompute.SetBuffer(1, "minMax", minMaxBuffer);
            noiseCompute.SetTexture(1, "Result", ActiveTexture);
            //dispatch normalization kernel
            noiseCompute.Dispatch(1, numthreadGroups, numthreadGroups, numthreadGroups);
            if (logComputeTime) {
                //get minmax data just to force main thread to wait until compute shaders are finished.
                //this allows us to measure the execution time.
                var minMax = new int[2];
                minMaxBuffer.GetData(minMax);
                Debug.Log($"Noise Generation: {timer.ElapsedMilliseconds}ms");
            }
            //release buffers
            foreach (var buffer in buffersToRelease) {
                buffer.Release();
            }
        }
    }

    //create buffer with data, set in compute shader, also add to list of buffers to be released in one function
    ComputeBuffer CreateBuffer(System.Array data, int stride, string bufferName, int kernel = 0) {
        var buffer = new ComputeBuffer(data.Length, stride, ComputeBufferType.Structured);
        buffersToRelease.Add(buffer);
        buffer.SetData(data);
        noiseCompute.SetBuffer(kernel, bufferName, buffer);
        return buffer;
    }

    void CreateTexture(ref RenderTexture renderTexture, int resolution, string name) {
        var format = GraphicsFormat.R16G16B16A16_SNorm;
        if (renderTexture == null || !renderTexture.IsCreated() || renderTexture.width != resolution || renderTexture.height != resolution || renderTexture.volumeDepth != resolution || renderTexture.graphicsFormat != format) {
            if (renderTexture != null) {
                renderTexture.Release();
            }
            renderTexture = new RenderTexture(resolution, resolution, 0);
            renderTexture.volumeDepth = resolution;
            renderTexture.graphicsFormat = format;
            renderTexture.enableRandomWrite = true;
            renderTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            renderTexture.name = name;
            renderTexture.Create();
            Load(name, renderTexture);
        }
        renderTexture.wrapMode = TextureWrapMode.Repeat;
        renderTexture.filterMode = FilterMode.Bilinear;
    }

    void CreateWorleyPointsBuffer(System.Random prng, int pointsNumPerAxis, string bufferName) {
        var points = new Vector3[pointsNumPerAxis * pointsNumPerAxis * pointsNumPerAxis];
        float cellSize = 1f / pointsNumPerAxis;
        for (int i = 0; i < pointsNumPerAxis; i++) {
            for (int j = 0; j < pointsNumPerAxis; j++) {
                for (int k = 0; k < pointsNumPerAxis; k++) {
                    float randomX = (float)prng.NextDouble();
                    float randomY = (float)prng.NextDouble();
                    float randomZ = (float)prng.NextDouble();
                    //random offset multiple size of the cell making it unit
                    Vector3 randomOffset = new Vector3(randomX, randomY, randomZ) * cellSize;
                    //corner of the unit cell
                    Vector3 cellOrigin = new Vector3(i, j, k) * cellSize;
                    int index = i + pointsNumPerAxis * (j + k * pointsNumPerAxis);
                    points[index] = cellOrigin + randomOffset;
                }
            }
        }
        CreateBuffer(points, sizeof(float) * 3, bufferName);
    }

    void UpdateNoiseSettings(CloudNoiseSettings settings) {
        var prng = new System.Random(settings.seed);
        CreateWorleyPointsBuffer(prng, settings.numDivisionsA, "pointsA");
        CreateWorleyPointsBuffer(prng, settings.numDivisionsB, "pointsB");
        CreateWorleyPointsBuffer(prng, settings.numDivisionsC, "pointsC");
        noiseCompute.SetInt("numCellsA", settings.numDivisionsA);
        noiseCompute.SetInt("numCellsB", settings.numDivisionsB);
        noiseCompute.SetInt("numCellsC", settings.numDivisionsC);
        noiseCompute.SetBool("invertNoise", settings.invert);
        noiseCompute.SetBool("blendPerlin", settings.blendPerlin);
        noiseCompute.SetInt("tile", settings.tile);
        noiseCompute.SetInt("octaves", settings.octave);
        noiseCompute.SetFloat("amplitude", settings.amplitude);
        noiseCompute.SetFloat("frequency", settings.frequency);
        noiseCompute.SetFloat("lacunarity", settings.lacunarity);
        noiseCompute.SetVector("offset", settings.offset);
    }

    public RenderTexture ActiveTexture {
        get {
            return (activeType == CloudNoiseType.Shape) ? shapeTexture : detailTexture;
        }
    }

    public CloudNoiseSettings ActiveSettings {
        get {
            CloudNoiseSettings[] settings = (activeType == CloudNoiseType.Shape) ? shapeNoise : detailNoise;
            //R、G、B、A
            int activeChannelIndex = (int)activeChannel;
            if (activeChannelIndex >= settings.Length) {
                return null;
            }
            return settings[activeChannelIndex];
        }
    }

    public Vector4 ChannelMask {
        get {
            Vector4 channelWeight = new Vector4(
                (activeChannel == TextureChannel.R) ? 1 : 0,
                (activeChannel == TextureChannel.G) ? 1 : 0,
                (activeChannel == TextureChannel.B) ? 1 : 0,
                (activeChannel == TextureChannel.A) ? 1 : 0
                );
            return channelWeight;
        }
    }

    public void Load(string saveName, RenderTexture target) {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        //--------------NOTE HERE---------------
        saveName = sceneName + "_" + saveName;
        Texture3D savedTex = (Texture3D)Resources.Load(saveName);
        if (savedTex != null && savedTex.width == target.width) {
            copy.SetTexture(0, "tex", savedTex);
            copy.SetTexture(0, "renderTex", target);
            int numThreadGroups = Mathf.CeilToInt(savedTex.width / 8f);
            copy.Dispatch(0, numThreadGroups, numThreadGroups, numThreadGroups);
        }
    }

    void ValidateParameters() {
        detailResolution = Mathf.Max(1, detailResolution);
        shapeResolution = Mathf.Max(1, shapeResolution);
    }

    public void ManualUpdate() {
        updateNoise = true;
        UpdateWorleyNoise();
    }

    public void ActiveNoiseSettingsChanged() {
        if (autoUpdate) {
            updateNoise = true;
        }
    }
}
