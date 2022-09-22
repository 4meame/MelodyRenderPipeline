using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class AutoExposure {
    const int numAutoExposureTexture = 2;
    CommandBuffer buffer;
    ComputeShader cs;
    RenderTexture[] autoExposurePool;
    int autoExposurePingPong;
    RenderTexture currentAutoExposure;
    bool resetHistory;

    public AutoExposure(CommandBuffer buffer, ComputeShader cs) {
        this.buffer = buffer;
        this.cs = cs;
        autoExposurePool = new RenderTexture[numAutoExposureTexture];
        autoExposurePingPong = 0;
        resetHistory = true;
    }

    void CheckTexture(int id) {
        if (autoExposurePool[id] == null || !autoExposurePool[id].IsCreated()) {
            autoExposurePool[id] = new RenderTexture(1, 1, 0, RenderTextureFormat.RFloat) { enableRandomWrite = true };
            autoExposurePool[id].Create();
        }
    }

    //exposureParams x: lowPercent, y: highPercent, z: minEV, w: maxEV
    public void AutoExposureLookUp(Vector4 exposureParams, Vector4 adaptationParams, Vector4 scaleOffsetRes, ComputeBuffer data, bool isFixed) {
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

}
