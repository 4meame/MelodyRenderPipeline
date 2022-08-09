using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ScreenSpaceAmbientOcclusion {
    const string bufferName = "ScreenSpaceAmbientOcclusion";
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };
    ScriptableRenderContext context;
    Camera camera;
    CameraBufferSettings.SSAO settings;
    Vector2Int bufferSize;
    ComputeShader cs;

    int ambientOcclusionId = Shader.PropertyToID("AmbientOcclusionRT");
    int filteredResultId = Shader.PropertyToID("FilteredResult");
    public void Setup(ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, CameraBufferSettings.SSAO settings) {
        this.context = context;
        this.camera = camera;
        this.bufferSize = bufferSize;
        this.settings = settings;
        this.cs = settings.computeShader;
    }

    void Configure() {
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(bufferSize.x, bufferSize.y, RenderTextureFormat.RGB111110Float, 0, 0);
        descriptor.sRGB = false;
        descriptor.enableRandomWrite = true;
        buffer.GetTemporaryRT(ambientOcclusionId, descriptor);
        buffer.GetTemporaryRT(filteredResultId, descriptor);
    }

    public void Render() {
        buffer.BeginSample("SSAO Resolve");
        //divide by shader's numthreads.x
        int dispatchThreadGroupXCount = bufferSize.x / 8;
        //divide by shader's numthreads.y
        int dispatchThreadGroupYCount = bufferSize.y / 8;
        //divide by shader's numthreads.z
        int dispatchThreadGroupZCount = 1;
        if (settings.enabled) {
            Configure();
            buffer.SetComputeIntParam(cs, "sampleCount", settings.sampleCount);
            buffer.SetComputeFloatParam(cs, "aoRadius", settings.aoRadius);
            buffer.SetComputeFloatParam(cs, "threshold", settings.pureDepthAOParameters.x);
            buffer.SetComputeFloatParam(cs, "area", settings.pureDepthAOParameters.y);
            buffer.SetComputeFloatParam(cs, "strength", settings.pureDepthAOParameters.z);
            buffer.SetComputeFloatParam(cs, "correction", settings.pureDepthAOParameters.w);
            Matrix4x4 projection = camera.projectionMatrix;
            buffer.SetComputeMatrixParam(cs, "_CameraProjection", projection);
            buffer.SetComputeMatrixParam(cs, "_CameraInverseProjection", projection.inverse);
            int kernel_SSAOResolve = cs.FindKernel("PureDepthSSAO");
            buffer.SetComputeTextureParam(cs, kernel_SSAOResolve, "RandomTexture", settings.randomTexture);
            buffer.SetComputeTextureParam(cs, kernel_SSAOResolve, "AmbientOcclusionRT", ambientOcclusionId);
            buffer.DispatchCompute(cs, kernel_SSAOResolve, dispatchThreadGroupXCount, dispatchThreadGroupYCount, dispatchThreadGroupZCount);

            int kernel_SSAOFilter = cs.FindKernel("BilateralFilter");
            buffer.SetComputeVectorParam(cs, "filterRadius", new Vector2(settings.filterRadius, 0));
            buffer.SetComputeFloatParam(cs, "filterFactor", settings.filterFactor);
            buffer.SetComputeTextureParam(cs, kernel_SSAOFilter, "aoResult", ambientOcclusionId);
            buffer.SetComputeTextureParam(cs, kernel_SSAOFilter, "FilteredResult", filteredResultId);
            buffer.DispatchCompute(cs, kernel_SSAOFilter, dispatchThreadGroupXCount, dispatchThreadGroupYCount, dispatchThreadGroupZCount);
            buffer.SetComputeVectorParam(cs, "filterRadius", new Vector2(0, settings.filterRadius));
            buffer.SetComputeFloatParam(cs, "filterFactor", settings.filterFactor);
            buffer.SetComputeTextureParam(cs, kernel_SSAOFilter, "aoResult", filteredResultId);
            buffer.SetComputeTextureParam(cs, kernel_SSAOFilter, "FilteredResult", ambientOcclusionId);
            buffer.DispatchCompute(cs, kernel_SSAOFilter, dispatchThreadGroupXCount, dispatchThreadGroupYCount, dispatchThreadGroupZCount);
            buffer.SetGlobalTexture("_SSAO_Blur", ambientOcclusionId);
            buffer.EnableShaderKeyword("_SSAO_ON");
        } else {
            buffer.DisableShaderKeyword("_SSAO_ON");
        }
        buffer.EndSample("SSAO Resolve");
        ExecuteBuffer();
    }

    public void CleanUp() {
        buffer.ReleaseTemporaryRT(ambientOcclusionId);
        ExecuteBuffer();
    }

    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}
