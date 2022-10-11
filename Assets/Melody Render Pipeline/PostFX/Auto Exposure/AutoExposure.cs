using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static PostFXSettings;

public class AutoExposure {
    const int numAutoExposureTexture = 2;
    const string bufferName = "Auto Exposure";
    CommandBuffer buffer = new CommandBuffer { name = bufferName };
    ScriptableRenderContext context;
    Camera camera;
    PostFXSettings settings;
    Vector2Int bufferSize;
    ComputeShader cs;
    RenderTexture[] autoExposurePool;
    int autoExposurePingPong;
    RenderTexture currentAutoExposure;
    bool resetHistory;

    LogHistogram logHistogram;

    public AutoExposure() {
        autoExposurePool = new RenderTexture[numAutoExposureTexture];
        autoExposurePingPong = 0;
        resetHistory = true;
    }

    public void Setup(ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, PostFXSettings settings) {
        this.context = context;
        this.camera = camera;
        this.bufferSize = bufferSize;
        //apply to proper camera
        this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
        cs = settings.autoExposureSettings.autoExposure;
        if (logHistogram == null) {
            logHistogram = new LogHistogram(buffer, settings.autoExposureSettings.logHistogram);
        }
    }

    void CheckTexture(int id) {
        if (autoExposurePool[id] == null || !autoExposurePool[id].IsCreated()) {
            autoExposurePool[id] = new RenderTexture(1, 1, 0, RenderTextureFormat.RFloat) { enableRandomWrite = true };
            autoExposurePool[id].Create();
        }
    }

    //exposureParams x: lowPercent, y: highPercent, z: minEV, w: maxEV
    void AutoExposureLookUp(Vector4 exposureParams, Vector4 adaptationParams, Vector4 scaleOffsetRes, ComputeBuffer data, bool isFixed) {
        CheckTexture(0);
        CheckTexture(1);
        bool firstFrame = resetHistory || !Application.isPlaying;
        string adaptation = null;
        if(firstFrame || isFixed) {
            adaptation = "AutoExposureAvgLuminance_fixed";
        } else {
            adaptation = "AutoExposureAvgLuminance_progressive";
        }

        int kernel = cs.FindKernel(adaptation);
        buffer.SetComputeBufferParam(cs, kernel, "_HistogramBuffer", data);
        buffer.SetComputeVectorParam(cs, "_Params1", new Vector4(exposureParams.x * 0.01f, exposureParams.y * 0.01f, Mathf.Pow(2, exposureParams.z), Mathf.Pow(2, exposureParams.w)));
        buffer.SetComputeVectorParam(cs, "_Params2", adaptationParams);
        buffer.SetComputeVectorParam(cs, "_ScaleOffsetRes", scaleOffsetRes);
        if (firstFrame) {
            //don't want eye adaptation when not in play mode because the GameView isn't animated, thus making it harder to tweak. Just use the final audo exposure value.
            currentAutoExposure = autoExposurePool[0];
            buffer.SetComputeTextureParam(cs, kernel, "_DestinationTex", currentAutoExposure);
            buffer.DispatchCompute(cs, kernel, 1, 1, 1);
            //copy current exposure to the other pingpong target to avoid adapting from black
            buffer.Blit(autoExposurePool[0], autoExposurePool[1]);
            resetHistory = false;
        } else {
            int pp = autoExposurePingPong;
            var src = autoExposurePool[++pp % 2];
            var dst = autoExposurePool[++pp % 2];
            buffer.SetComputeTextureParam(cs, kernel, "_SourceTex", src);
            buffer.SetComputeTextureParam(cs, kernel, "_DestinationTex", dst);
            buffer.DispatchCompute(cs, kernel, 1, 1, 1);
            autoExposurePingPong = ++pp % 2;
            currentAutoExposure = dst;
        }
        buffer.SetGlobalTexture("_AutoExposureLUT", currentAutoExposure);
    }

    public void DoAutoExposure(int from) {
        if(settings == null) {
            return;
        }
        if (settings.autoExposureSettings.metering == AutoExposureSettings.MeteringMode.None) {
            //set buffer keyword
            buffer.SetGlobalFloat("_AutoExposure", 0);
            return;
        }
        buffer.SetGlobalFloat("_AutoExposure", 1);

        switch (settings.autoExposureSettings.meteringMask) {
            case AutoExposureSettings.MeteringMask.None:
                buffer.SetGlobalInt("_MeteringMask", 0);
                break;
            case AutoExposureSettings.MeteringMask.Vignette:
                buffer.SetGlobalInt("_MeteringMask", 1);
                break;
            case AutoExposureSettings.MeteringMask.Custom:
                buffer.SetGlobalInt("_MeteringMask", 2);
                break;
            default:
                break;
        }

        logHistogram.GenerateHistorgram(bufferSize.x, bufferSize.y, from);
        //make sure filtering values are correct to avoid apocalyptic consequences
        float lowPercent = settings.autoExposureSettings.lowPercent;
        float highPercent = settings.autoExposureSettings.highPercent;
        const float minDelta = 1e-2f;
        highPercent = Mathf.Clamp(highPercent, 1f + minDelta, 99f);
        lowPercent = Mathf.Clamp(lowPercent, 1f, highPercent - minDelta);
        //clamp min/max adaptation values as well
        float minLum = settings.autoExposureSettings.minEV;
        float maxLum = settings.autoExposureSettings.maxEV;
        Vector4 exposureParams = new Vector4(lowPercent, highPercent, minLum, maxLum);
        Vector4 adaptationParams = new Vector4(settings.autoExposureSettings.speedDown, settings.autoExposureSettings.speedUp, settings.autoExposureSettings.compensation, Time.deltaTime);
        Vector4 scaleOffsetRes = logHistogram.GetHistogramScaleOffsetRes(bufferSize.x, bufferSize.y);
        AutoExposureLookUp(exposureParams, adaptationParams, scaleOffsetRes, logHistogram.data, settings.autoExposureSettings.adaptation == AutoExposureSettings.AdaptationMode.Fixed ? true : false);
        ExecuteBuffer();
    }

    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}
