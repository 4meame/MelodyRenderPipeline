using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class ScreenSpaceGlobalIllumination
{
    const string bufferName = "ScreenSpaceGlobalIllumination";
    CommandBuffer buffer = new CommandBuffer {
        name = bufferName
    };
    ScriptableRenderContext context;
    Camera camera;
    CameraBufferSettings.GI settings;
    bool useHDR;
    Vector2 bufferSize;
    bool copyTextureSupported;
    enum Pass {
        PrepareHiz,
        LinearTrace,
        HizTrace,
        SpatioBrdf,
        NormalBilateralX,
        NormalBilateralY,
        AdaptiveBilateralX,
        AdaptiveBilateralY,
        TemporalFilter,
        Combine
    }
    int m_SampleIndex = 0;
    const int k_SampleCount = 64;
    Material material;
    Vector2 cameraBufferSize;
    Vector2 randomSampler = Vector2.one;
    Matrix4x4 SSGI_ProjectionMatrix;
    Matrix4x4 SSGI_ViewProjectionMatrix;
    Matrix4x4 SSGI_Prev_ViewProjectionMatrix;
    Matrix4x4 SSGI_WorldToCameraMatrix;
    Matrix4x4 SSGI_CameraToWorldMatrix;
    RenderTexture[] SSGI_TraceMask_RT = new RenderTexture[2];
    RenderTargetIdentifier[] SSGI_TraceMask_ID = new RenderTargetIdentifier[2];
    RenderTexture SSGI_HierarchicalDepth_RT;
    RenderTexture SSGI_HierarchicalDepth_BackUp_RT;
    RenderTexture SSGI_SceneColor_RT;
    RenderTexture SSGI_CombineScene_RT;
    RenderTexture SSGI_Spatial_RT;
    RenderTexture SSGI_TemporalPrev_RT;
    RenderTexture SSGI_TemporalCurr_RT;
    static int SSGI_HierarchicalDepth_ID = Shader.PropertyToID("_SSGI_HierarchicalDepth_RT");
    static int SSGI_SceneColor_ID = Shader.PropertyToID("_SSGI_SceneColor_RT");
    static int SSGI_CombineScene_ID = Shader.PropertyToID("_SSGI_CombienScene_RT");
    static int SSGI_Trace_ID = Shader.PropertyToID("_SSGI_RayCastRT");
    static int SSGI_Mask_ID = Shader.PropertyToID("_SSGI_RayMask_RT");
    static int SSGI_Spatial_ID = Shader.PropertyToID("_SSGI_Spatial_RT");
    static int SSGI_TemporalPrev_ID = Shader.PropertyToID("_SSGI_TemporalPrev_RT");
    static int SSGI_TemporalCurr_ID = Shader.PropertyToID("_SSGI_TemporalCurr_RT");

    public void Setup(ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, CameraBufferSettings.GI settings, bool useHDR, bool copyTextureSupported) {
        this.context = context;
        this.camera = camera;
        this.settings = settings;
        this.bufferSize = bufferSize;
        this.useHDR = useHDR;
        this.copyTextureSupported = copyTextureSupported;
        if (material == null) {
            material = new Material(Shader.Find("Hidden/Melody RP/ScreenSpaceGlobalIllumination"));
        }
        if (camera.cameraType != CameraType.Game) {
            return;
        }
    }

    public void CleanUp() {
        ReleaseBuffer();
        ExecuteBuffer();
    }

    public void Render() {
        if(camera.cameraType != CameraType.Game) {
            return;
        }
        if (settings.enabled) {
            if (settings.giType == CameraBufferSettings.GI.GIType.SSGI) {
                randomSampler = GenerateRandomOffset();
                UpdateMatricesAndRenderTexture();
                //bilt scene depth
                buffer.Blit("_CameraDepthTexture", SSGI_HierarchicalDepth_RT);
                //set Hiz-depth RT
                for (int i = 0; i < settings.Hiz_MaxLevel; i++) {
                    buffer.SetGlobalInt("_SSGI_HiZ_PrevDepthLevel", i);
                    buffer.SetRenderTarget(SSGI_HierarchicalDepth_BackUp_RT, i + 1);
                    buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.PrepareHiz, MeshTopology.Triangles, 3);
                    buffer.CopyTexture(SSGI_HierarchicalDepth_BackUp_RT, 0, i + 1, SSGI_HierarchicalDepth_RT, 0, i + 1);
                }
                buffer.SetGlobalTexture(SSGI_HierarchicalDepth_ID, SSGI_HierarchicalDepth_RT);
                //set scene color RT
                buffer.SetGlobalTexture(SSGI_SceneColor_ID, SSGI_SceneColor_RT);
                CopyTexture("_CameraColorTexture", SSGI_SceneColor_RT);
                //ray casting
                buffer.SetGlobalTexture(SSGI_Trace_ID, SSGI_TraceMask_RT[0]);
                buffer.SetGlobalTexture(SSGI_Mask_ID, SSGI_TraceMask_RT[1]);
                if (settings.traceType == CameraBufferSettings.GI.TraceType.HiZTrace) {
                    buffer.SetRenderTarget(SSGI_TraceMask_ID, SSGI_TraceMask_RT[0]);
                    buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.HizTrace, MeshTopology.Triangles, 3);
                } else {
                    buffer.SetRenderTarget(SSGI_TraceMask_ID, SSGI_TraceMask_RT[0]);
                    buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.LinearTrace, MeshTopology.Triangles, 3);
                }
                //do temporal filter
                buffer.SetGlobalTexture(SSGI_TemporalPrev_ID, SSGI_TemporalPrev_RT);
                buffer.SetGlobalTexture(SSGI_TemporalCurr_ID, SSGI_TemporalCurr_RT);
                buffer.SetRenderTarget(SSGI_TemporalCurr_RT);
                buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.TemporalFilter, MeshTopology.Triangles, 3);
                CopyTexture(SSGI_TemporalCurr_RT, SSGI_TemporalPrev_RT);
                //do spatial filter
                if (settings.filterType == CameraBufferSettings.GI.FilterType.BrdfWeight) {
                    buffer.SetRenderTarget(SSGI_Spatial_RT);
                    buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.SpatioBrdf, MeshTopology.Triangles, 3);
                    buffer.SetGlobalTexture(SSGI_Spatial_ID, SSGI_Spatial_RT);
                } else if (settings.filterType == CameraBufferSettings.GI.FilterType.NormalBilateral) {
                    buffer.SetRenderTarget(SSGI_Spatial_RT);
                    buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.NormalBilateralX, MeshTopology.Triangles, 3);
                    buffer.SetGlobalTexture("_SSGI_TemporalPrev_RT", SSGI_Spatial_RT);
                    buffer.SetRenderTarget(SSGI_TraceMask_ID[0]);
                    buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.NormalBilateralY, MeshTopology.Triangles, 3);
                    buffer.SetGlobalTexture(SSGI_Spatial_ID, SSGI_TraceMask_ID[0]);
                } else if (settings.filterType == CameraBufferSettings.GI.FilterType.AdaptionBilateral) {
                    buffer.SetRenderTarget(SSGI_Spatial_RT);
                    buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.AdaptiveBilateralX, MeshTopology.Triangles, 3);
                    buffer.SetGlobalTexture("_SSGI_TemporalPrev_RT", SSGI_Spatial_RT);
                    buffer.SetRenderTarget(SSGI_TraceMask_ID[0]);
                    buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.AdaptiveBilateralY, MeshTopology.Triangles, 3);
                    buffer.SetGlobalTexture(SSGI_Spatial_ID, SSGI_TraceMask_ID[0]);
            }
                buffer.SetGlobalTexture("_SSGI_Filtered", SSGI_TraceMask_ID[0]);
            }
        } else {
            
        }
        ExecuteBuffer();
    }

    public void Combine(int sourceId) {
        if (camera.cameraType != CameraType.Game) {
            return;
        }
        if (settings.enabled && settings.giType == CameraBufferSettings.GI.GIType.SSGI) {
            switch (settings.debugType) {
                case CameraBufferSettings.GI.DebugType.Combine:
                    material.SetInt("_DebugPass", 0);
                    break;
                case CameraBufferSettings.GI.DebugType.Indirect:
                    material.SetInt("_DebugPass", 1);
                    break;
                case CameraBufferSettings.GI.DebugType.Occlusion:
                    material.SetInt("_DebugPass", 2);
                    break;
                case CameraBufferSettings.GI.DebugType.Mask:
                    material.SetInt("_DebugPass", 3);
                    break;
                case CameraBufferSettings.GI.DebugType.RayDepth:
                    material.SetInt("_DebugPass", 4);
                    break;
                default:
                    break;
            }
            buffer.SetGlobalTexture(SSGI_CombineScene_ID, SSGI_CombineScene_RT);
            CopyTexture(sourceId, SSGI_SceneColor_RT);
            buffer.SetRenderTarget(sourceId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.Combine, MeshTopology.Triangles, 3);
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
        material.SetTexture("_SSGI_Noise", settings.randomTexture);
        material.SetVector("_SSGI_ScreenSize", cameraBufferSize);
        material.SetVector("_SSGI_RayCastSize", cameraBufferSize / (int)settings.rayCastSize);
        material.SetVector("_SSGI_NoiseSize", new Vector2(1024, 1024));
        material.SetFloat("_SSGI_ScreenFade", settings.screenFade);
        material.SetFloat("_SSGI_Thickness", settings.THK);
        material.SetInt("_SSGI_RayStepSize", settings.Linear_StepSize);
        material.SetInt("_SSGI_TraceDistance", 512);
        material.SetInt("_SSGI_NumSteps_Linear", settings.Linear_RaySteps);
        material.SetInt("_SSGI_NumSteps_HiZ", settings.Hiz_RaySteps);
        material.SetInt("_SSGI_NumRays", settings.rayNums);
        material.SetInt("_SSGI_TraceBehind", settings.traceBehind ? 1 : 0);
        material.SetInt("_SSGI_RayMask", settings.rayMask ? 1 : 0);
        material.SetInt("_SSGI_HiZ_MaxLevel", settings.Hiz_MaxLevel);
        material.SetInt("_SSGI_HiZ_StartLevel", settings.Hiz_StartLevel);
        material.SetInt("_SSGI_HiZ_StopLevel", settings.Hiz_StopLevel);
        material.SetFloat("_SSGI_Intensity", settings.intensity);
        if (settings.deNoise) {
            material.SetInt("_SSGI_KernelSize", settings.SpatioKernel);
            material.SetFloat("_SSGI_KernelRadius", settings.SpatioRadius);
            material.SetFloat("_SSGI_TemporalScale", settings.TemporalScale);
            material.SetFloat("_SSGI_TemporalWeight", settings.TemporalWeight);
        }
        else {
            material.SetInt("_SSGI_KernelSize", 1);
            material.SetFloat("_SSGI_KernelRadius", 1f);
            material.SetFloat("_SSGI_TemporalScale", 0f);
            material.SetFloat("_SSGI_TemporalWeight", 0f);
        }
    }

    void UpdateMatricesAndRenderTexture() {
        Vector2 halfBufferSize = new Vector2(bufferSize.x / 2, bufferSize.y / 2);
        Vector2 currentBufferSize = new Vector2(bufferSize.x, bufferSize.y);
        if (currentBufferSize.x < 350 || currentBufferSize.y < 200) {
            //scene preview camera exit
            return;
        }
        if (cameraBufferSize != currentBufferSize) {
            cameraBufferSize = currentBufferSize;
            //SceneColor and HierarchicalDepth RT
            RenderTexture.ReleaseTemporary(SSGI_SceneColor_RT);
            SSGI_SceneColor_RT = RenderTexture.GetTemporary((int)cameraBufferSize.x, (int)cameraBufferSize.y, 0, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            //fix : "setting mipmap mode of already created render texture is not supported"
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor((int)cameraBufferSize.x, (int)cameraBufferSize.y, 0);
            descriptor.colorFormat = RenderTextureFormat.RHalf;
            descriptor.sRGB = false;
            descriptor.useMipMap = true;
            descriptor.autoGenerateMips = true;
            RenderTexture.ReleaseTemporary(SSGI_HierarchicalDepth_RT);
            SSGI_HierarchicalDepth_RT = RenderTexture.GetTemporary(descriptor);
            SSGI_HierarchicalDepth_RT.filterMode = FilterMode.Point;
            RenderTexture.ReleaseTemporary(SSGI_HierarchicalDepth_BackUp_RT);
            descriptor.autoGenerateMips = false;
            SSGI_HierarchicalDepth_BackUp_RT = RenderTexture.GetTemporary(descriptor);
            SSGI_HierarchicalDepth_BackUp_RT.filterMode = FilterMode.Point;
            //RayMarching and RayMask RT
            RenderTexture.ReleaseTemporary(SSGI_TraceMask_RT[0]);
            SSGI_TraceMask_RT[0] = RenderTexture.GetTemporary((int)cameraBufferSize.x / (int)settings.rayCastSize, (int)cameraBufferSize.y / (int)settings.rayCastSize, 0, RenderTextureFormat.ARGBHalf);
            SSGI_TraceMask_RT[0].filterMode = FilterMode.Point;
            SSGI_TraceMask_ID[0] = SSGI_TraceMask_RT[0].colorBuffer;
            RenderTexture.ReleaseTemporary(SSGI_TraceMask_RT[1]);
            SSGI_TraceMask_RT[1] = RenderTexture.GetTemporary((int)cameraBufferSize.x / (int)settings.rayCastSize, (int)cameraBufferSize.y / (int)settings.rayCastSize, 0, RenderTextureFormat.ARGBHalf);
            SSGI_TraceMask_RT[1].filterMode = FilterMode.Point;
            SSGI_TraceMask_ID[1] = SSGI_TraceMask_RT[1].colorBuffer;
            //Spatial RT
            RenderTexture.ReleaseTemporary(SSGI_Spatial_RT);
            SSGI_Spatial_RT = RenderTexture.GetTemporary((int)cameraBufferSize.x, (int)cameraBufferSize.y, 0, RenderTextureFormat.ARGBHalf);
            SSGI_Spatial_RT.filterMode = FilterMode.Bilinear;
            //Temporal RT
            RenderTexture.ReleaseTemporary(SSGI_TemporalPrev_RT);
            SSGI_TemporalPrev_RT = RenderTexture.GetTemporary((int)cameraBufferSize.x, (int)cameraBufferSize.y, 0, RenderTextureFormat.ARGBHalf);
            SSGI_TemporalPrev_RT.filterMode = FilterMode.Bilinear;
            RenderTexture.ReleaseTemporary(SSGI_TemporalCurr_RT);
            SSGI_TemporalCurr_RT = RenderTexture.GetTemporary((int)cameraBufferSize.x, (int)cameraBufferSize.y, 0, RenderTextureFormat.ARGBHalf);
            SSGI_TemporalCurr_RT.filterMode = FilterMode.Bilinear;
            //Combine RT
            RenderTexture.ReleaseTemporary(SSGI_CombineScene_RT);
            SSGI_CombineScene_RT = RenderTexture.GetTemporary((int)cameraBufferSize.x, (int)cameraBufferSize.y, 0, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            SSGI_CombineScene_RT.filterMode = FilterMode.Point;
            //update uniform variable
            UpdateUniformVariable();
        }

        //set martrices
        material.SetVector("_SSGI_Jitter", new Vector4(cameraBufferSize.x / 1024, cameraBufferSize.y / 1024, randomSampler.x, randomSampler.y));
        SSGI_WorldToCameraMatrix = camera.worldToCameraMatrix;
        SSGI_CameraToWorldMatrix = SSGI_WorldToCameraMatrix.inverse;
        SSGI_ProjectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
        SSGI_ViewProjectionMatrix = SSGI_ProjectionMatrix * SSGI_WorldToCameraMatrix;
        material.SetMatrix("_SSGI_ProjectionMatrix", SSGI_ProjectionMatrix);
        material.SetMatrix("_SSGI_InverseProjectionMatrix", SSGI_ProjectionMatrix.inverse);
        material.SetMatrix("_SSGI_ViewProjectionMatrix", SSGI_ViewProjectionMatrix);
        material.SetMatrix("_SSGI_InverseViewProjectionMatrix", SSGI_ViewProjectionMatrix.inverse);
        material.SetMatrix("_SSGI_WorldToCameraMatrix", SSGI_WorldToCameraMatrix);
        material.SetMatrix("_SSGI_CameraToWorldMatrix", SSGI_CameraToWorldMatrix);
        material.SetMatrix("_SSGI_LastFrameViewProjectionMatrix", SSGI_Prev_ViewProjectionMatrix);
        Matrix4x4 warpToScreenSpaceMatrix = Matrix4x4.identity;
        warpToScreenSpaceMatrix.m00 = halfBufferSize.x; warpToScreenSpaceMatrix.m03 = halfBufferSize.x;
        warpToScreenSpaceMatrix.m11 = halfBufferSize.y; warpToScreenSpaceMatrix.m13 = halfBufferSize.y;
        Matrix4x4 SSGI_ProjectToPixelMatrix = warpToScreenSpaceMatrix * SSGI_ProjectionMatrix;
        material.SetMatrix("_SSGI_ProjectToPixelMatrix", SSGI_ProjectToPixelMatrix);
        Vector4 SSGI_ProjInfo = new Vector4
        ((-2 / (cameraBufferSize.x * SSGI_ProjectionMatrix[0])),
        (-2 / (cameraBufferSize.y * SSGI_ProjectionMatrix[5])),
        ((1 - SSGI_ProjectionMatrix[2]) / SSGI_ProjectionMatrix[0]),
        ((1 + SSGI_ProjectionMatrix[6]) / SSGI_ProjectionMatrix[5]));
        material.SetVector("_SSGI_ProjInfo", SSGI_ProjInfo);
        Vector3 SSGI_ClipInfo = (float.IsPositiveInfinity(camera.farClipPlane)) ?
        new Vector3(camera.nearClipPlane, -1, 1) :
        new Vector3(camera.nearClipPlane * camera.farClipPlane, camera.nearClipPlane - camera.farClipPlane, camera.farClipPlane);
        material.SetVector("_SSGI_ClipInfo", SSGI_ClipInfo);
    }

    public void Refresh() {
        if (camera.cameraType != CameraType.Game) {
            return;
        }
        if (settings.giType == CameraBufferSettings.GI.GIType.SSGI) {
            SSGI_Prev_ViewProjectionMatrix = SSGI_ViewProjectionMatrix;
        }
    }

    void ReleaseBuffer() {
        RenderTexture.ReleaseTemporary(SSGI_HierarchicalDepth_RT);
        RenderTexture.ReleaseTemporary(SSGI_SceneColor_RT);
        RenderTexture.ReleaseTemporary(SSGI_TraceMask_RT[0]);
        RenderTexture.ReleaseTemporary(SSGI_TraceMask_RT[1]);
        RenderTexture.ReleaseTemporary(SSGI_Spatial_RT);
        RenderTexture.ReleaseTemporary(SSGI_TemporalPrev_RT);
        RenderTexture.ReleaseTemporary(SSGI_TemporalCurr_RT);
        RenderTexture.ReleaseTemporary(SSGI_CombineScene_RT);
        if (buffer != null) {
            buffer.Dispose();
        }
    }
}
