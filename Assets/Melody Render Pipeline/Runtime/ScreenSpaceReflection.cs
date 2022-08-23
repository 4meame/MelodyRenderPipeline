using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class ScreenSpaceReflection {
    const string bufferName = "ScreenSpaceReflection";
    CommandBuffer buffer = new CommandBuffer {
        name = bufferName
    };
    ScriptableRenderContext context;
    Camera camera;
    CameraBufferSettings.SSR settings;
    ComputeShader cs;
    bool useHDR;

    Vector2Int ssrBufferSize;
    int ssrResultId = Shader.PropertyToID("_SSR_Result");
    int ssrBlurId = Shader.PropertyToID("_SSR_Blur");

    public void Setup(ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, CameraBufferSettings.SSR settings, bool useHDR) {
        this.context = context;
        this.camera = camera;
        this.settings = settings;
        this.ssrBufferSize = bufferSize / settings.downSample;
        this.cs = settings.computeShader;
        this.useHDR = useHDR;
    }

    void Configure() {
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(ssrBufferSize.x, ssrBufferSize.y, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default, 0, 0);
        descriptor.sRGB = false;
        descriptor.enableRandomWrite = true;
        buffer.GetTemporaryRT(ssrResultId, descriptor);
        buffer.GetTemporaryRT(ssrBlurId, descriptor);
    }

    public void CleanUp() {
        buffer.ReleaseTemporaryRT(ssrResultId);
        buffer.ReleaseTemporaryRT(ssrBlurId);
        ExecuteBuffer();
    }

    public void Render() {
        buffer.BeginSample("SSR Resolve");
        if (settings.enabled) {
            Configure();
            if (settings.sSRType == CameraBufferSettings.SSR.SSRType.SSR) {
                //ray trace params
                buffer.SetComputeFloatParam(cs, "maxDistance", settings.maxDistance);
                buffer.SetComputeFloatParam(cs, "iterations", settings.iterations);
                buffer.SetComputeFloatParam(cs, "binarySearchIterations", settings.binarySearchIterations);
                buffer.SetComputeFloatParam(cs, "pixelStrideSize", settings.pixelStrideSize);
                buffer.SetComputeFloatParam(cs, "pixelStrideZCuttoff", settings.pixelStrideZCuttoff);
                buffer.SetComputeFloatParam(cs, "thickness", settings.thickness);
                //fade artifact params
                buffer.SetComputeFloatParam(cs, "screenEdgeFade", settings.screenEdgeFade);
                buffer.SetComputeFloatParam(cs, "eyeFadeStart", settings.eyeFadeStart);
                buffer.SetComputeFloatParam(cs, "eyeFadeEnd", settings.eyeFadeEnd);
                //custom camera matrixes
                Matrix4x4 trs = Matrix4x4.TRS(new Vector3(0.5f, 0.5f, 0.0f), Quaternion.identity, new Vector3(0.5f, 0.5f, 1.0f));
                Matrix4x4 scrScale = Matrix4x4.Scale(new Vector3(ssrBufferSize.x, ssrBufferSize.y, 1.0f));
                Matrix4x4 projection = camera.projectionMatrix;
                Matrix4x4 projMatrix = scrScale * trs * projection;
                buffer.SetComputeMatrixParam(cs, "_CameraProjection_SSR", projMatrix);
                buffer.SetComputeMatrixParam(cs, "_CameraInverseProjection_SSR", projection.inverse);
                //texture param
                buffer.SetComputeVectorParam(cs, "textureSize", new Vector2(ssrBufferSize.x, ssrBufferSize.y));
                int kernel_SSRResolve = cs.FindKernel("SSRResolve");
                buffer.SetComputeTextureParam(cs, kernel_SSRResolve, "ScreenSpaceReflectionRT", ssrResultId);
                buffer.DispatchCompute(cs, kernel_SSRResolve, ssrBufferSize.x / 8, ssrBufferSize.y / 8, 1);
                //SSR Blur Pass
                int kernel_SSRBlur = cs.FindKernel("SSRBlur");
                buffer.SetComputeFloatParam(cs, "blurOffset", settings.downSample);
                buffer.SetComputeTextureParam(cs, kernel_SSRBlur, "ScreenSpaceReflectionRT", ssrResultId);
                buffer.SetComputeTextureParam(cs, kernel_SSRBlur, "BlurRT", ssrBlurId);
                buffer.DispatchCompute(cs, kernel_SSRBlur, ssrBufferSize.x / 8, ssrBufferSize.y / 8, 1);
            }
            buffer.SetGlobalTexture("_SSR_Filtered", ssrBlurId);
            buffer.EnableShaderKeyword("_SSR_ON");
        } else {
            buffer.DisableShaderKeyword("_SSR_ON");
        }
        buffer.EndSample("SSR Resolve");
        ExecuteBuffer();
    }

    public void Debug(int sourceId) {
        if (settings.enabled && settings.debug) {
            buffer.Blit(ssrResultId, sourceId);
            ExecuteBuffer();
        }
    }

    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}
