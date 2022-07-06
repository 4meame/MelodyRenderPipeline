using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class SSPlanarReflection {
    const string bufferName = "SSPlanarReflection";
    CommandBuffer buffer = new CommandBuffer {
        name = bufferName
    };
    ScriptableRenderContext context;
    Camera camera;
    CameraBufferSettings.SSPR settings;
    CullingResults cullingResults;
    ComputeShader cs;
    bool useHDR;

    int ColorResultId = Shader.PropertyToID("_SSPR_ColorResult");
    int PackedDataId = Shader.PropertyToID("_SSPR_PackedData");
    static ShaderTagId SSPRTagId = new ShaderTagId("SSPlanarReflection");
    //must match compute shader's [numthread(x)]
    const int SHADER_NUMTHREAD_X = 8;
    //must match compute shader's [numthread(y)]
    const int SHADER_NUMTHREAD_Y = 8;

    public void Setup(ScriptableRenderContext context, Camera camera, CullingResults cullingResults, CameraBufferSettings.SSPR settings, bool useHDR) {
        this.context = context;
        this.camera = camera;
        this.cullingResults = cullingResults;
        this.settings = settings;
        this.cs = settings.computeShader;
        this.useHDR = useHDR;
    }

    int GetRTHeight() {
        return Mathf.CeilToInt((float)settings.reflectionTextureSize / (float)SHADER_NUMTHREAD_Y) * SHADER_NUMTHREAD_Y;
    }
    int GetRTWidth() {
        float aspect = (float)Screen.width / Screen.height;
        return Mathf.CeilToInt(GetRTHeight() * aspect / (float)SHADER_NUMTHREAD_X) * SHADER_NUMTHREAD_X;
    }

    void Configure() {
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(GetRTWidth(), GetRTHeight(), useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default, 0, 0);
        descriptor.sRGB = false;
        descriptor.enableRandomWrite = true;
        buffer.GetTemporaryRT(ColorResultId, descriptor);
        //PackedData use RInt format
        descriptor.colorFormat = RenderTextureFormat.RInt;
        buffer.GetTemporaryRT(PackedDataId, descriptor);
    }

    public void CleanUp() {
        buffer.ReleaseTemporaryRT(ColorResultId);
        ExecuteBuffer();
    }

    public void Render() {
        //divide by shader's numthreads.x
        int dispatchThreadGroupXCount = GetRTWidth() / SHADER_NUMTHREAD_X;
        //divide by shader's numthreads.y
        int dispatchThreadGroupYCount = GetRTHeight() / SHADER_NUMTHREAD_Y;
        //divide by shader's numthreads.z
        int dispatchThreadGroupZCount = 1;

        if (settings.enabled)
        {
            Configure();

            buffer.BeginSample("SSPR Resolve");
            buffer.SetComputeVectorParam(cs, Shader.PropertyToID("_RTSize"), new Vector2(GetRTWidth(), GetRTHeight()));
            buffer.SetComputeFloatParam(cs, Shader.PropertyToID("_HorizontalPlaneHeightWS"), settings.HorizontalReflectionPlaneHeightWS);
            buffer.SetComputeFloatParam(cs, Shader.PropertyToID("_FadeOutScreenBorderWidthVerticle"), settings.FadeOutScreenBorderWidthVerticle);
            buffer.SetComputeFloatParam(cs, Shader.PropertyToID("_FadeOutScreenBorderWidthHorizontal"), settings.FadeOutScreenBorderWidthHorizontal);
            buffer.SetComputeVectorParam(cs, Shader.PropertyToID("_CameraDirection"), camera.transform.forward);
            buffer.SetComputeVectorParam(cs, Shader.PropertyToID("_CameraPosition"), camera.transform.position);
            buffer.SetComputeFloatParam(cs, Shader.PropertyToID("_ScreenLRStretchIntensity"), settings.ScreenLRStretchIntensity);
            buffer.SetComputeFloatParam(cs, Shader.PropertyToID("_ScreenLRStretchThreshold"), settings.ScreenLRStretchThreshold);
            buffer.SetComputeVectorParam(cs, Shader.PropertyToID("_FinalTintColor"), settings.TintColor);
            //send VP matrix
            Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
            Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
            Matrix4x4 viewProjMatrix = projMatrix * viewMatrix;
            Matrix4x4 invViewProjMatrix = Matrix4x4.Inverse(viewProjMatrix);
            buffer.SetComputeMatrixParam(cs, "_VPMatrix", viewProjMatrix);
            buffer.SetComputeMatrixParam(cs, "_I_VPMatrix", invViewProjMatrix);

            //kernel NonMobilePathClear
            int kernel_NonMobilePathClear = cs.FindKernel("NonMobilePathClear");
            buffer.SetComputeTextureParam(cs, kernel_NonMobilePathClear, "HashRT", PackedDataId);
            buffer.SetComputeTextureParam(cs, kernel_NonMobilePathClear, "ColorRT", ColorResultId);
            buffer.DispatchCompute(cs, kernel_NonMobilePathClear, dispatchThreadGroupXCount, dispatchThreadGroupYCount, dispatchThreadGroupZCount);

            //kernel NonMobilePathRenderHashRT
            int kernel_NonMobilePathRenderHashRT = cs.FindKernel("NonMobilePathRenderHashRT");
            buffer.SetComputeTextureParam(cs, kernel_NonMobilePathRenderHashRT, "HashRT", PackedDataId);
            buffer.SetComputeTextureParam(cs, kernel_NonMobilePathRenderHashRT, "_CameraDepthTexture", new RenderTargetIdentifier("_CameraDepthTexture"));

            buffer.DispatchCompute(cs, kernel_NonMobilePathRenderHashRT, dispatchThreadGroupXCount, dispatchThreadGroupYCount, dispatchThreadGroupZCount);

            //resolve to ColorRT
            int kernel_NonMobilePathResolveColorRT = cs.FindKernel("NonMobilePathResolveColorRT");
            buffer.SetComputeTextureParam(cs, kernel_NonMobilePathResolveColorRT, "_CameraColorTexture", new RenderTargetIdentifier("_CameraColorTexture"));
            buffer.SetComputeTextureParam(cs, kernel_NonMobilePathResolveColorRT, "ColorRT", ColorResultId);
            buffer.SetComputeTextureParam(cs, kernel_NonMobilePathResolveColorRT, "HashRT", PackedDataId);
            buffer.DispatchCompute(cs, kernel_NonMobilePathResolveColorRT, dispatchThreadGroupXCount, dispatchThreadGroupYCount, dispatchThreadGroupZCount);

            //optional shared pass to improve result only: fill RT hole
            if (settings.ApplyFillHoleFix) {
                int kernel_FillHoles = cs.FindKernel("FillHoles");
                buffer.SetComputeTextureParam(cs, kernel_FillHoles, "ColorRT", ColorResultId);
                buffer.SetComputeTextureParam(cs, kernel_FillHoles, "PackedDataRT", PackedDataId);
                buffer.DispatchCompute(cs, kernel_FillHoles, Mathf.CeilToInt(dispatchThreadGroupXCount / 2f), Mathf.CeilToInt(dispatchThreadGroupYCount / 2f), dispatchThreadGroupZCount);
            }

            buffer.SetGlobalTexture(ColorResultId, new RenderTargetIdentifier(ColorResultId));
            buffer.EndSample("SSPR Resolve");
            buffer.EnableShaderKeyword("_SSPR");
        } else {
            buffer.DisableShaderKeyword("_SSPR");
        }
        ExecuteBuffer();

        var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
        var drawingSettings = new DrawingSettings(SSPRTagId, sortingSettings);
        var filteringSettings = new FilteringSettings(RenderQueueRange.all);
        context.DrawRenderers(cullingResults,ref drawingSettings,ref filteringSettings);
    }

    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}
