using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class LogHistogram {
    //EV range [2^-9, 2^9]
    public const int rangeMin = -9;
    public const int rangeMax = 9;
    const int bins = 128;
    CommandBuffer buffer;
    public ComputeShader cs;
    public ComputeBuffer data;

    public LogHistogram(CommandBuffer buffer, ComputeShader cs) {
        this.buffer = buffer;
        this.cs = cs;
    }

    public void Release() {
        if (data != null) {
            data.Release();
            data = null;
        }
    }

    public Vector4 GetHistogramScaleOffsetRes(int width, int height) {
        float diff = rangeMax - rangeMin;
        float scale = 1f / diff;
        float offset = -rangeMin * scale;
        return new Vector4(scale, offset, width, height);
    }

    public void GenerateHistorgram(int witdh, int height, int sourceId) {
        if(data == null) {
            data = new ComputeBuffer(bins, sizeof(uint));
        }
        uint threadX, threadY, threadZ;
        var scaleOffsetRes = GetHistogramScaleOffsetRes(witdh, height);
        //clear the buffer on every frame as we use it to accumulate luminance values on each frame
        int kernel = cs.FindKernel("EyeHistogramClear");
        buffer.SetComputeBufferParam(cs, kernel, "_HistogramBuffer", data);
        cs.GetKernelThreadGroupSizes(kernel, out threadX, out threadY, out threadZ);
        buffer.DispatchCompute(cs, kernel, Mathf.CeilToInt(bins / (float)threadX), 1, 1);
        //get a log histogram
        kernel = cs.FindKernel("EyeHistogram");
        buffer.SetComputeBufferParam(cs, kernel, "_HistogramBuffer", data);
        buffer.SetComputeTextureParam(cs, kernel, "_SourceTex", sourceId);
        buffer.SetComputeVectorParam(cs, "_ScaleOffsetRes", scaleOffsetRes);
        cs.GetKernelThreadGroupSizes(kernel, out threadX, out threadY, out threadZ);
        //half resolution
        buffer.DispatchCompute(cs, kernel, Mathf.CeilToInt(scaleOffsetRes.z / 2f / threadX), Mathf.CeilToInt(scaleOffsetRes.w / 2f / threadY), 1);
    }
}
