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
    bool useHDR;
    CameraBufferSettings.SSAO settings;
    Vector2Int aoBufferSize;
    Vector2Int blurBufferSize;
    ComputeShader cs;

    int ambientOcclusionId = Shader.PropertyToID("AmbientOcclusionRT");
    int upSampleId = Shader.PropertyToID("UpSampleRT");
    int filtered0Id = Shader.PropertyToID("Filtered0");
    int filtered1Id = Shader.PropertyToID("Filtered1");
    int debugResultId = Shader.PropertyToID("debugResult");
    public void Setup(ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, CameraBufferSettings.SSAO settings, bool useHDR) {
        this.context = context;
        this.camera = camera;
        this.useHDR = useHDR;
        this.aoBufferSize = bufferSize / settings.downSample;
        this.blurBufferSize = bufferSize;
        this.settings = settings;
        this.cs = settings.computeShader;
    }

    void Configure() {
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(aoBufferSize.x, aoBufferSize.y, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default, 0, 0);
        descriptor.sRGB = false;
        descriptor.enableRandomWrite = true;
        buffer.GetTemporaryRT(ambientOcclusionId, descriptor);
        descriptor.width = blurBufferSize.x;
        descriptor.height = blurBufferSize.y;
        buffer.GetTemporaryRT(upSampleId, descriptor);
        buffer.GetTemporaryRT(filtered0Id, descriptor);
        buffer.GetTemporaryRT(filtered1Id, descriptor);
        buffer.GetTemporaryRT(debugResultId, descriptor);
    }

    public void Render() {
        buffer.BeginSample("SSAO Resolve");
        if (settings.enabled) {
            Configure();
            buffer.SetComputeIntParam(cs, "sampleCount", settings.sampleCount);
            buffer.SetComputeFloatParam(cs, "aoRadius", settings.aoRadius);
            buffer.SetComputeIntParam(cs, "downSample", settings.downSample);
            if (settings.aOType == CameraBufferSettings.SSAO.AOType.pureDepthAO) {
                buffer.SetComputeFloatParam(cs, "threshold", settings.pureDepthAOParameters.x);
                buffer.SetComputeFloatParam(cs, "area", settings.pureDepthAOParameters.y);
                buffer.SetComputeFloatParam(cs, "strength", settings.pureDepthAOParameters.z);
                buffer.SetComputeFloatParam(cs, "correction", settings.pureDepthAOParameters.w);
                int kernel_SSAOResolve = cs.FindKernel("PureDepthSSAO");
                buffer.SetComputeTextureParam(cs, kernel_SSAOResolve, "RandomTexture", settings.randomTexture);
                buffer.SetComputeTextureParam(cs, kernel_SSAOResolve, "AmbientOcclusionRT", ambientOcclusionId);
                buffer.DispatchCompute(cs, kernel_SSAOResolve, aoBufferSize.x / 8, aoBufferSize.y / 8, 1);
            }
            else if (settings.aOType == CameraBufferSettings.SSAO.AOType.SSAO) {
                buffer.SetComputeFloatParam(cs, "bias", settings.SSAOParameters.x);
                buffer.SetComputeFloatParam(cs, "contrast", settings.SSAOParameters.y);
                buffer.SetComputeFloatParam(cs, "magnitude", settings.SSAOParameters.z);
                buffer.SetComputeFloatParam(cs, "power", settings.SSAOParameters.w);
                Matrix4x4 projection = camera.projectionMatrix;
                buffer.SetComputeMatrixParam(cs, "_CameraProjection_SSAO", projection);
                buffer.SetComputeMatrixParam(cs, "_CameraInverseProjection_SSAO", projection.inverse);
                int kernel_SSAOResolve = cs.FindKernel("SSAO");
                buffer.SetComputeTextureParam(cs, kernel_SSAOResolve, "RandomTexture", settings.randomTexture);
                buffer.SetComputeTextureParam(cs, kernel_SSAOResolve, "AmbientOcclusionRT", ambientOcclusionId);
                buffer.DispatchCompute(cs, kernel_SSAOResolve, aoBufferSize.x / 8, aoBufferSize.y / 8, 1);
            } else if (settings.aOType == CameraBufferSettings.SSAO.AOType.HBAO) {
                Matrix4x4 projection = camera.projectionMatrix;
                buffer.SetComputeMatrixParam(cs, "_CameraProjection_SSAO", projection);
                buffer.SetComputeMatrixParam(cs, "_CameraInverseProjection_SSAO", projection.inverse);
                buffer.SetComputeFloatParam(cs, "_CameraNearPlane", camera.nearClipPlane);
                buffer.SetComputeIntParam(cs, "numDirection", settings.numDirection);
                buffer.SetComputeIntParam(cs, "maxRadiusPixel", settings.maxRadiusPixel);
                buffer.SetComputeFloatParam(cs, "tanBias", settings.tanBias);
                buffer.SetComputeFloatParam(cs, "hbaoStrength", settings.hbaoStrength);
                int kernel_SSAOResolve = cs.FindKernel("HBAO");
                buffer.SetComputeTextureParam(cs, kernel_SSAOResolve, "RandomTexture", settings.randomTexture);
                buffer.SetComputeTextureParam(cs, kernel_SSAOResolve, "AmbientOcclusionRT", ambientOcclusionId);
                buffer.DispatchCompute(cs, kernel_SSAOResolve, aoBufferSize.x / 8, aoBufferSize.y / 8, 1);
            } else if(settings.aOType == CameraBufferSettings.SSAO.AOType.GTAO) {
                buffer.SetComputeVectorParam(cs, "fadeParams", settings.fadeParams);
                buffer.SetComputeIntParam(cs, "numSlice", settings.numSlice);
                buffer.SetComputeFloatParam(cs, "thickness", settings.thickness);
                buffer.SetComputeFloatParam(cs, "gtaoStrength", settings.gtaoStrength);
                float fovRad = camera.fieldOfView * Mathf.Deg2Rad;
                float projScale = aoBufferSize.y / (Mathf.Tan(fovRad * 0.5f) * 2) * 0.5f;
                buffer.SetComputeFloatParam(cs, "_HalfProjScale", projScale);
                Matrix4x4 projection = camera.projectionMatrix;
                buffer.SetComputeMatrixParam(cs, "_CameraProjection_SSAO", projection);
                buffer.SetComputeMatrixParam(cs, "_CameraInverseProjection_SSAO", projection.inverse);
                int kernel_SSAOResolve = cs.FindKernel("GTAO");
                buffer.SetComputeTextureParam(cs, kernel_SSAOResolve, "AmbientOcclusionRT", ambientOcclusionId);
                buffer.DispatchCompute(cs, kernel_SSAOResolve, aoBufferSize.x / 8, aoBufferSize.y / 8, 1);
                if (settings.multipleBounce) {
                    buffer.EnableShaderKeyword("_Multiple_Bounce_AO");
                } else {
                    buffer.DisableShaderKeyword("_Multiple_Bounce_AO");
                }
            }

            if(settings.filterType == CameraBufferSettings.SSAO.FilterType.NormalBilateral) {
                int kernel_SSAOFilter0 = cs.FindKernel("NormalBilateralFilter0");
                buffer.SetComputeVectorParam(cs, "filterRadius", new Vector2(settings.filterRadius, 0));
                buffer.SetComputeFloatParam(cs, "filterFactor", settings.filterFactor);
                buffer.SetComputeTextureParam(cs, kernel_SSAOFilter0, "aoResult", ambientOcclusionId);
                buffer.SetComputeTextureParam(cs, kernel_SSAOFilter0, "Filtered0", filtered0Id);
                buffer.DispatchCompute(cs, kernel_SSAOFilter0, blurBufferSize.x / 8, blurBufferSize.y / 8, 1);
                int kernel_SSAOFilter1 = cs.FindKernel("NormalBilateralFilter1");
                buffer.SetComputeVectorParam(cs, "filterRadius", new Vector2(0, settings.filterRadius));
                buffer.SetComputeFloatParam(cs, "filterFactor", settings.filterFactor);
                buffer.SetComputeTextureParam(cs, kernel_SSAOFilter1, "aoResult", filtered0Id);
                buffer.SetComputeTextureParam(cs, kernel_SSAOFilter1, "Filtered1", filtered1Id);
                buffer.DispatchCompute(cs, kernel_SSAOFilter1, blurBufferSize.x / 8, blurBufferSize.y / 8, 1);
            } else if (settings.filterType == CameraBufferSettings.SSAO.FilterType.AdaptionBilateral) {
                int kernel_SSAOUpSample = cs.FindKernel("AdaptionBilateralUpSample");
                buffer.SetComputeVectorParam(cs, "filterRadius", new Vector2(settings.filterRadius, settings.filterRadius));
                buffer.SetComputeTextureParam(cs, kernel_SSAOUpSample, "aoResult", ambientOcclusionId);
                buffer.SetComputeTextureParam(cs, kernel_SSAOUpSample, "UpSampleRT", upSampleId);
                buffer.DispatchCompute(cs, kernel_SSAOUpSample, blurBufferSize.x / 8, blurBufferSize.y / 8, 1);
                int kernel_SSAOFilter0 = cs.FindKernel("AdaptionBilateralFilter0");
                buffer.SetComputeVectorParam(cs, "filterRadius", new Vector2(settings.filterRadius, 0));
                buffer.SetComputeFloatParam(cs, "filterFactor", settings.filterFactor);
                buffer.SetComputeIntParam(cs, "kernelSize", settings.kernelSize);
                buffer.SetComputeTextureParam(cs, kernel_SSAOFilter0, "aoResult", upSampleId);
                buffer.SetComputeTextureParam(cs, kernel_SSAOFilter0, "Filtered0", filtered0Id);
                buffer.DispatchCompute(cs, kernel_SSAOFilter0, blurBufferSize.x / 8, blurBufferSize.y / 8, 1);
                int kernel_SSAOFilter1 = cs.FindKernel("AdaptionBilateralFilter1");
                buffer.SetComputeVectorParam(cs, "filterRadius", new Vector2(0, settings.filterRadius));
                buffer.SetComputeFloatParam(cs, "filterFactor", settings.filterFactor);
                buffer.SetComputeIntParam(cs, "kernelSize", settings.kernelSize);
                buffer.SetComputeTextureParam(cs, kernel_SSAOFilter1, "aoResult", filtered0Id);
                buffer.SetComputeTextureParam(cs, kernel_SSAOFilter1, "Filtered1", filtered1Id);
                buffer.DispatchCompute(cs, kernel_SSAOFilter1, blurBufferSize.x / 8, blurBufferSize.y / 8, 1);
            }
            buffer.SetGlobalTexture("_SSAO_Blur", filtered1Id);
            buffer.EnableShaderKeyword("_SSAO_ON");
        } else {
            buffer.DisableShaderKeyword("_SSAO_ON");
        }
        buffer.EndSample("SSAO Resolve");
        ExecuteBuffer();
    }

    public void Debug(int sourceId) {
        if (settings.enabled && settings.debugType == CameraBufferSettings.SSAO.DebugType.AO) {
            int kernel_Debug = cs.FindKernel("DebugAO");
            buffer.SetComputeTextureParam(cs, kernel_Debug, "Filtered1", filtered1Id);
            buffer.SetComputeTextureParam(cs, kernel_Debug, "debugResult", debugResultId);
            buffer.SetComputeIntParam(cs, "multipleBounce", settings.multipleBounce ? 1 : 0);
            buffer.DispatchCompute(cs, kernel_Debug, blurBufferSize.x / 8, blurBufferSize.y / 8, 1);
            buffer.Blit(debugResultId, sourceId);
            ExecuteBuffer();
        } else if (settings.enabled && settings.debugType == CameraBufferSettings.SSAO.DebugType.RO) {
            int kernel_Debug = cs.FindKernel("DebugRO");
            buffer.SetComputeTextureParam(cs, kernel_Debug, "Filtered1", filtered1Id);
            buffer.SetComputeTextureParam(cs, kernel_Debug, "debugResult", debugResultId);
            buffer.DispatchCompute(cs, kernel_Debug, blurBufferSize.x / 8, blurBufferSize.y / 8, 1);
            buffer.Blit(debugResultId, sourceId);
            ExecuteBuffer();
        }
    }

    public void CleanUp() {
        buffer.ReleaseTemporaryRT(ambientOcclusionId);
        buffer.ReleaseTemporaryRT(filtered0Id);
        buffer.ReleaseTemporaryRT(filtered1Id);
        buffer.ReleaseTemporaryRT(debugResultId);
        ExecuteBuffer();
    }

    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}
