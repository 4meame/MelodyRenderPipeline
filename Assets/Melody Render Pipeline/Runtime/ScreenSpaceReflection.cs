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
    Vector2 bufferSize;
    bool copyTextureSupported;
    //normal ssr
    Vector2 ssrBufferSize;
    int ssrResultId = Shader.PropertyToID("_SSR_Result");
    int ssrBlurId = Shader.PropertyToID("_SSR_Blur");
    //stochastic ssr
    enum Pass {
        PrepareHiz,
        LinearTrace,
        LinearMultiTrace,
        HizTrace,
        HizMultiTrace,
        SpatioFilter,
        SpatioMultiFilter,
        TemporalFilter,
        TemporalMultiFilter,
        Combine,
        CombineMulti
    }
    int m_SampleIndex = 0;
    const int k_SampleCount = 64;
    Material material;
    Vector2 cameraBufferSize;
    Vector2 randomSampler = Vector2.one;
    Matrix4x4 SSR_ProjectionMatrix;
    Matrix4x4 SSR_ViewProjectionMatrix;
    Matrix4x4 SSR_Prev_ViewProjectionMatrix;
    Matrix4x4 SSR_WorldToCameraMatrix;
    Matrix4x4 SSR_CameraToWorldMatrix;
    RenderTexture[] SSR_TraceMask_RT = new RenderTexture[2];
    RenderTargetIdentifier[] SSR_TraceMask_ID = new RenderTargetIdentifier[2];
    RenderTexture SSR_HierarchicalDepth_RT;
    RenderTexture SSR_HierarchicalDepth_BackUp_RT;
    RenderTexture SSR_SceneColor_RT;
    RenderTexture SSR_CombineScene_RT;
    RenderTexture SSR_Spatial_RT;
    RenderTexture SSR_TemporalPrev_RT;
    RenderTexture SSR_TemporalCurr_RT;
    static int SSR_HierarchicalDepth_ID = Shader.PropertyToID("_SSR_HierarchicalDepth_RT");
    static int SSR_SceneColor_ID = Shader.PropertyToID("_SSR_SceneColor_RT");
    static int SSR_CombineScene_ID = Shader.PropertyToID("_SSR_CombienReflection_RT");
    static int SSR_Trace_ID = Shader.PropertyToID("_SSR_RayCastRT");
    static int SSR_Mask_ID = Shader.PropertyToID("_SSR_RayMask_RT");
    static int SSR_Spatial_ID = Shader.PropertyToID("_SSR_Spatial_RT");
    static int SSR_TemporalPrev_ID = Shader.PropertyToID("_SSR_TemporalPrev_RT");
    static int SSR_TemporalCurr_ID = Shader.PropertyToID("_SSR_TemporalCurr_RT");

    public void Setup(ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, CameraBufferSettings.SSR settings, bool useHDR, bool copyTextureSupported) {
        this.context = context;
        this.camera = camera;
        this.settings = settings;
        this.bufferSize = bufferSize;
        this.cs = settings.computeShader;
        this.useHDR = useHDR;
        this.copyTextureSupported = copyTextureSupported;
        if (material == null) {
            material = new Material(Shader.Find("Hidden/Melody RP/StochasticSSR"));
        }
        if (camera.cameraType != CameraType.Game) {
            return;
        }
    }

    void Configure() {
        ssrBufferSize = bufferSize / settings.downSample;
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor((int)ssrBufferSize.x, (int)ssrBufferSize.y, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default, 0, 0);
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
        if(camera.cameraType != CameraType.Game) {
            return;
        }
        if (settings.enabled) {
            if (settings.sSRType == CameraBufferSettings.SSR.SSRType.SSR) {
                buffer.EnableShaderKeyword("_SSR_ON");
                Configure();
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
                buffer.DispatchCompute(cs, kernel_SSRResolve, (int)ssrBufferSize.x / 8, (int)ssrBufferSize.y / 8, 1);
                //SSR Blur Pass
                int kernel_SSRBlur = cs.FindKernel("SSRBlur");
                buffer.SetComputeFloatParam(cs, "blurOffset", settings.downSample);
                buffer.SetComputeTextureParam(cs, kernel_SSRBlur, "ScreenSpaceReflectionRT", ssrResultId);
                buffer.SetComputeTextureParam(cs, kernel_SSRBlur, "BlurRT", ssrBlurId);
                buffer.DispatchCompute(cs, kernel_SSRBlur, (int)ssrBufferSize.x / 8, (int)ssrBufferSize.y / 8, 1);
                buffer.SetGlobalTexture("_SSR_Filtered", ssrBlurId);
                ExecuteBuffer();
            }
            if(settings.sSRType == CameraBufferSettings.SSR.SSRType.StochasticSSR) {
                buffer.DisableShaderKeyword("_SSR_ON");
                randomSampler = GenerateRandomOffset();
                UpdateMatricesAndRenderTexture();
                //bilt scene depth
                buffer.Blit("_CameraDepthTexture", SSR_HierarchicalDepth_RT);
                //set Hiz-depth RT
                for (int i = 0; i < settings.Hiz_MaxLevel; i++) {
                    buffer.SetGlobalInt("_SSR_HiZ_PrevDepthLevel", i);
                    buffer.SetRenderTarget(SSR_HierarchicalDepth_BackUp_RT, i + 1);
                    buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.PrepareHiz, MeshTopology.Triangles, 3);
                    buffer.CopyTexture(SSR_HierarchicalDepth_BackUp_RT, 0, i + 1, SSR_HierarchicalDepth_RT, 0, i + 1);
                }
                buffer.SetGlobalTexture(SSR_HierarchicalDepth_ID, SSR_HierarchicalDepth_RT);
                //set scene color RT
                buffer.SetGlobalTexture(SSR_SceneColor_ID, SSR_SceneColor_RT);
                CopyTexture("_CameraColorTexture", SSR_SceneColor_RT);
                //ray casting
                buffer.SetGlobalTexture(SSR_Trace_ID, SSR_TraceMask_RT[0]);
                buffer.SetGlobalTexture(SSR_Mask_ID, SSR_TraceMask_RT[1]);
                if (settings.traceMethod == CameraBufferSettings.SSR.TraceMethod.HiZTrace) {
                    buffer.SetRenderTarget(SSR_TraceMask_ID, SSR_TraceMask_RT[0]);
                    buffer.DrawProcedural(Matrix4x4.identity, material, (settings.rayNums > 1) ? (int)Pass.HizMultiTrace : (int)Pass.HizTrace, MeshTopology.Triangles, 3);
                } else {
                    buffer.SetRenderTarget(SSR_TraceMask_ID, SSR_TraceMask_RT[0]);
                    buffer.DrawProcedural(Matrix4x4.identity, material, (settings.rayNums > 1) ? (int)Pass.LinearMultiTrace : (int)Pass.LinearTrace, MeshTopology.Triangles, 3);
                }
                //do spatial filter
                buffer.SetGlobalTexture(SSR_Spatial_ID, SSR_Spatial_RT);
                buffer.SetRenderTarget(SSR_Spatial_RT);
                buffer.DrawProcedural(Matrix4x4.identity, material, (settings.rayNums > 1) ? (int)Pass.SpatioMultiFilter : (int)Pass.SpatioFilter, MeshTopology.Triangles, 3);
                CopyTexture(SSR_Spatial_RT, SSR_TemporalCurr_RT);
                //do temporal filter
                buffer.SetGlobalTexture(SSR_TemporalPrev_ID, SSR_TemporalPrev_RT);
                buffer.SetGlobalTexture(SSR_TemporalCurr_ID, SSR_TemporalCurr_RT);
                buffer.SetRenderTarget(SSR_TemporalCurr_RT);
                buffer.DrawProcedural(Matrix4x4.identity, material, (settings.rayNums > 1) ? (int)Pass.TemporalMultiFilter : (int)Pass.TemporalFilter, MeshTopology.Triangles, 3);
                CopyTexture(SSR_TemporalCurr_RT, SSR_TemporalPrev_RT);
                ExecuteBuffer();
            }
        } else {
            buffer.DisableShaderKeyword("_SSR_ON");
        }
    }

    public void Combine(int sourceId) {
        if (camera.cameraType != CameraType.Game) {
            return;
        }
        if (settings.enabled && settings.sSRType == CameraBufferSettings.SSR.SSRType.SSR && settings.debugMode == CameraBufferSettings.SSR.DebugMode.Reflection) {        
            buffer.Blit(ssrResultId, sourceId);
            ExecuteBuffer();
        }
        if (settings.enabled && settings.sSRType == CameraBufferSettings.SSR.SSRType.StochasticSSR) {
            switch (settings.debugMode) {
                case CameraBufferSettings.SSR.DebugMode.Combine:
                    material.SetInt("_DebugPass", 0);
                    break;
                case CameraBufferSettings.SSR.DebugMode.CombineNoCubemap:
                    material.SetInt("_DebugPass", 1);
                    break;
                case CameraBufferSettings.SSR.DebugMode.Reflection:
                    material.SetInt("_DebugPass", 2);
                    break;
                case CameraBufferSettings.SSR.DebugMode.CubeMap:
                    material.SetInt("_DebugPass", 3);
                    break;
                case CameraBufferSettings.SSR.DebugMode.ReflectionAndCubemap:
                    material.SetInt("_DebugPass", 4);
                    break;
                case CameraBufferSettings.SSR.DebugMode.Mask:
                    material.SetInt("_DebugPass", 5);
                    break;
                case CameraBufferSettings.SSR.DebugMode.PDF:
                    material.SetInt("_DebugPass", 6);
                    break;
                case CameraBufferSettings.SSR.DebugMode.Jitter:
                    material.SetInt("_DebugPass", 7);
                    break;
                case CameraBufferSettings.SSR.DebugMode.RO:
                    material.SetInt("_DebugPass", 8);
                    break;
                case CameraBufferSettings.SSR.DebugMode.Motion:
                    material.SetInt("_DebugPass", 9);
                    break;
                default:
                    break;
            }
            buffer.SetGlobalTexture(SSR_CombineScene_ID, SSR_CombineScene_RT);
            CopyTexture(sourceId, SSR_SceneColor_RT);
            buffer.SetRenderTarget(sourceId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            buffer.DrawProcedural(Matrix4x4.identity, material, (settings.rayNums > 1) ? (int)Pass.CombineMulti : (int)Pass.Combine, MeshTopology.Triangles, 3);
            ExecuteBuffer();
        }
    }

    void CopyTexture(RenderTargetIdentifier source, RenderTargetIdentifier destination) {
        if (copyTextureSupported) {
            buffer.CopyTexture(source, destination);
        } else {
            buffer.Blit(source, destination);
        }
    }

    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    float GetHaltonValue(int index, int radix) {
        float result = 0f;
        float fraction = 1f / radix;
        while (index > 0) {
            result += (index % radix) * fraction;
            index /= radix;
            fraction /= radix;
        }
        return result;
    }

    Vector2 GenerateRandomOffset() {
        var offset = new Vector2(GetHaltonValue(m_SampleIndex & 1023, 2), GetHaltonValue(m_SampleIndex & 1023, 3));
        if (m_SampleIndex++ >= k_SampleCount)
            m_SampleIndex = 0;
        return offset;
    }

    void UpdateUniformVariable() {
        material.SetTexture("_SSR_PreintegratedGF", settings.PreintegratedGF);
        material.SetTexture("_SSR_Noise", settings.BlueNoise);
        material.SetVector("_SSR_ScreenSize", cameraBufferSize);
        material.SetVector("_SSR_RayCastSize", cameraBufferSize / (int)settings.rayCastSize);
        material.SetVector("_SSR_NoiseSize", new Vector2(1024, 1024));
        material.SetFloat("_SSR_BRDFBias", settings.BRDFBias);
        material.SetFloat("_SSR_ScreenFade", settings.screenFade);
        material.SetFloat("_SSR_Thickness", settings.THK);
        material.SetInt("_SSR_RayStepSize", settings.Linear_StepSize);
        material.SetInt("_SSR_TraceDistance", 512);
        material.SetInt("_SSR_NumSteps_Linear", settings.Linear_RaySteps);
        material.SetInt("_SSR_NumSteps_HiZ", settings.Hiz_RaySteps);
        material.SetInt("_SSR_NumRays", settings.rayNums);
        material.SetInt("_SSR_BackwardsRay", settings.traceTowardRay ? 1 : 0);
        material.SetInt("_SSR_CullBack", settings.traceTowardRay ? 1 : 0);
        material.SetInt("_SSR_TraceBehind", settings.traceBehind ? 1 : 0);
        material.SetInt("_SSR_ReflectionOcclusion", settings.ReflectionOcclusion ? 1 : 0);
        material.SetInt("_SSR_HiZ_MaxLevel", settings.Hiz_MaxLevel);
        material.SetInt("_SSR_HiZ_StartLevel", settings.Hiz_StartLevel);
        material.SetInt("_SSR_HiZ_StopLevel", settings.Hiz_StopLevel);
        material.SetFloat("_SSR_Threshold_Hiz", settings.Hiz_Threshold);
        if (settings.deNoise) {
            material.SetInt("_SSR_NumResolver", settings.SpatioSampler);
            material.SetFloat("_SSR_TemporalScale", settings.TemporalScale);
            material.SetFloat("_SSR_TemporalWeight", settings.TemporalWeight);
        }
        else {
            material.SetInt("_SSR_NumResolver", 1);
            material.SetFloat("_SSR_TemporalScale", 0);
            material.SetFloat("_SSR_TemporalWeight", 0);
        }
    }

    void UpdateMatricesAndRenderTexture() {
        Vector2 halfBufferSize = new Vector2(bufferSize.x / 2, bufferSize.y / 2);
        Vector2 currentBufferSize = new Vector2(bufferSize.x, bufferSize.y);
        if (cameraBufferSize != currentBufferSize) {
            cameraBufferSize = currentBufferSize;
            //SceneColor and HierarchicalDepth RT
            RenderTexture.ReleaseTemporary(SSR_SceneColor_RT);
            SSR_SceneColor_RT = RenderTexture.GetTemporary((int)cameraBufferSize.x, (int)cameraBufferSize.y, 0, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            //fix : "setting mipmap mode of already created render texture is not supported"
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor((int)cameraBufferSize.x, (int)cameraBufferSize.y, 0);
            descriptor.colorFormat = RenderTextureFormat.RHalf;
            descriptor.sRGB = false;
            descriptor.useMipMap = true;
            descriptor.autoGenerateMips = true;
            RenderTexture.ReleaseTemporary(SSR_HierarchicalDepth_RT);
            SSR_HierarchicalDepth_RT = RenderTexture.GetTemporary(descriptor);
            SSR_HierarchicalDepth_RT.filterMode = FilterMode.Point;
            RenderTexture.ReleaseTemporary(SSR_HierarchicalDepth_BackUp_RT);
            descriptor.autoGenerateMips = false;
            SSR_HierarchicalDepth_BackUp_RT = RenderTexture.GetTemporary(descriptor);
            SSR_HierarchicalDepth_BackUp_RT.filterMode = FilterMode.Point;
            //RayMarching and RayMask RT
            RenderTexture.ReleaseTemporary(SSR_TraceMask_RT[0]);
            SSR_TraceMask_RT[0] = RenderTexture.GetTemporary((int)cameraBufferSize.x / (int)settings.rayCastSize, (int)cameraBufferSize.y / (int)settings.rayCastSize, 0, RenderTextureFormat.ARGBHalf);
            SSR_TraceMask_RT[0].filterMode = FilterMode.Point;
            SSR_TraceMask_ID[0] = SSR_TraceMask_RT[0].colorBuffer;
            RenderTexture.ReleaseTemporary(SSR_TraceMask_RT[1]);
            SSR_TraceMask_RT[1] = RenderTexture.GetTemporary((int)cameraBufferSize.x / (int)settings.rayCastSize, (int)cameraBufferSize.y / (int)settings.rayCastSize, 0, RenderTextureFormat.ARGBHalf);
            SSR_TraceMask_RT[1].filterMode = FilterMode.Point;
            SSR_TraceMask_ID[1] = SSR_TraceMask_RT[1].colorBuffer;
            //Spatial RT
            RenderTexture.ReleaseTemporary(SSR_Spatial_RT);
            SSR_Spatial_RT = RenderTexture.GetTemporary((int)cameraBufferSize.x, (int)cameraBufferSize.y, 0, RenderTextureFormat.ARGBHalf);
            SSR_Spatial_RT.filterMode = FilterMode.Bilinear;
            //Temporal RT
            RenderTexture.ReleaseTemporary(SSR_TemporalPrev_RT);
            SSR_TemporalPrev_RT = RenderTexture.GetTemporary((int)cameraBufferSize.x, (int)cameraBufferSize.y, 0, RenderTextureFormat.ARGBHalf);
            SSR_TemporalPrev_RT.filterMode = FilterMode.Bilinear;
            RenderTexture.ReleaseTemporary(SSR_TemporalCurr_RT);
            SSR_TemporalCurr_RT = RenderTexture.GetTemporary((int)cameraBufferSize.x, (int)cameraBufferSize.y, 0, RenderTextureFormat.ARGBHalf);
            SSR_TemporalCurr_RT.filterMode = FilterMode.Bilinear;
            //Combine RT
            RenderTexture.ReleaseTemporary(SSR_CombineScene_RT);
            SSR_CombineScene_RT = RenderTexture.GetTemporary((int)cameraBufferSize.x, (int)cameraBufferSize.y, 0, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            SSR_CombineScene_RT.filterMode = FilterMode.Point;
            //update uniform variable
            UpdateUniformVariable();
        }

        //set martrices
        material.SetVector("_SSR_Jitter", new Vector4(cameraBufferSize.x / 1024, cameraBufferSize.y / 1024, randomSampler.x, randomSampler.y));
        SSR_WorldToCameraMatrix = camera.worldToCameraMatrix;
        SSR_CameraToWorldMatrix = SSR_WorldToCameraMatrix.inverse;
        SSR_ProjectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
        SSR_ViewProjectionMatrix = SSR_ProjectionMatrix * SSR_WorldToCameraMatrix;
        material.SetMatrix("_SSR_ProjectionMatrix", SSR_ProjectionMatrix);
        material.SetMatrix("_SSR_InverseProjectionMatrix", SSR_ProjectionMatrix.inverse);
        material.SetMatrix("_SSR_ViewProjectionMatrix", SSR_ViewProjectionMatrix);
        material.SetMatrix("_SSR_InverseViewProjectionMatrix", SSR_ViewProjectionMatrix.inverse);
        material.SetMatrix("_SSR_WorldToCameraMatrix", SSR_WorldToCameraMatrix);
        material.SetMatrix("_SSR_CameraToWorldMatrix", SSR_CameraToWorldMatrix);
        material.SetMatrix("_SSR_LastFrameViewProjectionMatrix", SSR_Prev_ViewProjectionMatrix);
        Matrix4x4 warpToScreenSpaceMatrix = Matrix4x4.identity;
        warpToScreenSpaceMatrix.m00 = halfBufferSize.x; warpToScreenSpaceMatrix.m03 = halfBufferSize.x;
        warpToScreenSpaceMatrix.m11 = halfBufferSize.y; warpToScreenSpaceMatrix.m13 = halfBufferSize.y;
        Matrix4x4 SSR_ProjectToPixelMatrix = warpToScreenSpaceMatrix * SSR_ProjectionMatrix;
        material.SetMatrix("_SSR_ProjectToPixelMatrix", SSR_ProjectToPixelMatrix);
        Vector4 SSR_ProjInfo = new Vector4
        ((-2 / (cameraBufferSize.x * SSR_ProjectionMatrix[0])),
        (-2 / (cameraBufferSize.y * SSR_ProjectionMatrix[5])),
        ((1 - SSR_ProjectionMatrix[2]) / SSR_ProjectionMatrix[0]),
        ((1 + SSR_ProjectionMatrix[6]) / SSR_ProjectionMatrix[5]));
        material.SetVector("_SSR_ProjInfo", SSR_ProjInfo);
        Vector3 SSR_ClipInfo = (float.IsPositiveInfinity(camera.farClipPlane)) ?
        new Vector3(camera.nearClipPlane, -1, 1) :
        new Vector3(camera.nearClipPlane * camera.farClipPlane, camera.nearClipPlane - camera.farClipPlane, camera.farClipPlane);
        material.SetVector("_SSR_ClipInfo", SSR_ClipInfo);
    }

    public void Refresh() {
        if (camera.cameraType != CameraType.Game) {
            return;
        }
        if (settings.sSRType == CameraBufferSettings.SSR.SSRType.StochasticSSR) {
            SSR_Prev_ViewProjectionMatrix = SSR_ViewProjectionMatrix;
        }
    }

    void ReleaseBuffer() {
        RenderTexture.ReleaseTemporary(SSR_HierarchicalDepth_RT);
        RenderTexture.ReleaseTemporary(SSR_SceneColor_RT);
        RenderTexture.ReleaseTemporary(SSR_TraceMask_RT[0]);
        RenderTexture.ReleaseTemporary(SSR_TraceMask_RT[1]);
        RenderTexture.ReleaseTemporary(SSR_Spatial_RT);
        RenderTexture.ReleaseTemporary(SSR_TemporalPrev_RT);
        RenderTexture.ReleaseTemporary(SSR_TemporalCurr_RT);
        RenderTexture.ReleaseTemporary(SSR_CombineScene_RT);
        if (buffer != null) {
            buffer.Dispose();
        }
    }
}
