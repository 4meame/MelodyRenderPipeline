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
    CullingResults cullingResults;
    CameraBufferSettings.SSR settings;
    ComputeShader cs;
    bool useHDR;
    int ssrResultId = Shader.PropertyToID("_SSR_Result");
    int ssrBlurId = Shader.PropertyToID("_SSR_Blur");
    //must match compute shader's [numthread(x)]
    const int SHADER_NUMTHREAD_X = 8;
    //must match compute shader's [numthread(y)]
    const int SHADER_NUMTHREAD_Y = 8;

    public void Setup(ScriptableRenderContext context, Camera camera, CullingResults cullingResults, CameraBufferSettings.SSR settings, bool useHDR, bool useDynamicBatching, bool useInstancing, bool useLightsPerObject) {
        this.context = context;
        this.camera = camera;
        this.cullingResults = cullingResults;
        this.settings = settings;
        this.cs = settings.computeShader;
        this.useHDR = useHDR;
    }

    int GetRTHeight() {
        return Mathf.CeilToInt((float)camera.pixelHeight / (float)SHADER_NUMTHREAD_Y) * SHADER_NUMTHREAD_Y / settings.downsample;
    }
    int GetRTWidth() {
        float aspect = (float)Screen.width / Screen.height;
        return Mathf.CeilToInt(GetRTHeight() * aspect / (float)SHADER_NUMTHREAD_X) * SHADER_NUMTHREAD_X;
    }

    void Configure() {
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(GetRTWidth(), GetRTHeight(), useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default, 0, 0);
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
        //divide by shader's numthreads.x
        int dispatchThreadGroupXCount = GetRTWidth() / SHADER_NUMTHREAD_X;
        //divide by shader's numthreads.y
        int dispatchThreadGroupYCount = GetRTHeight() / SHADER_NUMTHREAD_Y;
        //divide by shader's numthreads.z
        int dispatchThreadGroupZCount = 1;
        if (settings.enabled) {
            Configure();
            buffer.BeginSample("SSR Resolve");

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
            Matrix4x4 scrScale = Matrix4x4.Scale(new Vector3(GetRTWidth(), GetRTHeight(), 1.0f));
            Matrix4x4 projection = camera.projectionMatrix;
            Matrix4x4 projMatrix = scrScale * trs * projection;
            buffer.SetComputeMatrixParam(cs, "_CameraProjection", projMatrix);
            buffer.SetComputeMatrixParam(cs, "_CameraInverseProjection", projection.inverse);
            //texture param
            buffer.SetComputeVectorParam(cs, "textureSize", new Vector2(GetRTWidth(), GetRTHeight()));
            int kernel_SSRResolve = cs.FindKernel("SSRResolve");
            buffer.SetComputeTextureParam(cs, kernel_SSRResolve, "ScreenSpaceReflectionRT", ssrResultId);
            buffer.DispatchCompute(cs, kernel_SSRResolve, dispatchThreadGroupXCount, dispatchThreadGroupYCount, dispatchThreadGroupZCount);
            //SSR Blur Pass
            int kernel_SSRBlur = cs.FindKernel("SSRBlur");
            buffer.SetComputeFloatParam(cs, "blurOffset", settings.downsample);
            buffer.SetComputeTextureParam(cs, kernel_SSRBlur, "ScreenSpaceReflectionRT", ssrResultId);
            buffer.SetComputeTextureParam(cs, kernel_SSRBlur, "BlurRT", ssrBlurId);
            buffer.DispatchCompute(cs, kernel_SSRBlur, dispatchThreadGroupXCount, dispatchThreadGroupYCount, dispatchThreadGroupZCount);
            buffer.SetGlobalTexture(ssrBlurId, new RenderTargetIdentifier(ssrBlurId));
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
