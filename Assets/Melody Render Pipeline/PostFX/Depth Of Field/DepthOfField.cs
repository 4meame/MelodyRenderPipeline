using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static PostFXSettings;

public class DepthOfField {
    const string bufferName = "Depth Of Field";
    CommandBuffer buffer = new CommandBuffer { name = bufferName };
    ScriptableRenderContext context;
    Camera camera;
    PhyscialCameraSettings physcialCamera;
    DepthOfFieldSettings settings;
    Vector2Int bufferSize;
    bool useHDR;

    ComputeBuffer nearBokehKernel;
    ComputeBuffer farBokehKernel;
    ComputeBuffer bokehIndirectCmd;
    ComputeBuffer nearBokehTileList;
    ComputeBuffer farBokehTileList;
    RenderTexture pingNear;
    RenderTexture pongNear;
    RenderTexture nearAlpha;
    RenderTexture nearCoC;
    RenderTexture dilatedNearCoC;
    RenderTexture pingFar;
    RenderTexture pongFar;
    RenderTexture farCoC;
    RenderTexture fullResCoC;
    RenderTexture[] mips = new RenderTexture[4];
    RenderTexture dilationPingPong;
    RenderTexture prevCoCHistroy;
    RenderTexture nextCoCHistory;
    int targetScaleId = Shader.PropertyToID("_TargetScale");
    int params1Id = Shader.PropertyToID("_Params1");
    int params2Id = Shader.PropertyToID("_Params2");
    int bokehKernelId = Shader.PropertyToID("_BokehKernel");

    public void Setup(ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, PostFXSettings settings, PhyscialCameraSettings physcialCamera, bool useHDR) {
        this.context = context;
        this.camera = camera;
        this.physcialCamera = physcialCamera;
        this.bufferSize = bufferSize;
        this.useHDR = useHDR;
        //apply to proper camera
        this.settings = camera.cameraType <= CameraType.SceneView ? (settings != null ? settings.depthOfFieldSettings : default) : default;
    }

    struct DepthOfFieldParameters {
        public ComputeShader dofKernelCS;
        public int dofKernelKernel;
        public ComputeShader dofCoCCS;
        public int dofCoCKernel;
        public ComputeShader dofCoCReprojectCS;
        public int dofCoCReprojectKernel;
        public ComputeShader dofPrefilterCS;
        public int dofPrefilterKernel;
        public ComputeShader dofMipGenCS;
        public int dofMipColorkernel;
        public int dofMipCoCkernel;
        public ComputeShader dofDilateCS;
        public int dofDilateKernel;
        public ComputeShader dofTileMaxCS;
        public int dofTileMaxKernel;
        public int dofClearIndirectAvgsKernal;
        public ComputeShader dofGatherCS;
        public int dofGatherNearKernel;
        public int dofGatherFarKernel;
        public ComputeShader dofCombineCS;
        public int dofCombineNearKernel;
        public int dofCombineFarKernel;
        //advanced DOF
        public bool useAdaneced;
        public ComputeShader dofAdvancedCS;
        public Vector2Int threadGroup8;

        public DepthOfFieldSettings.FocusMode focusMode;
        public DepthOfFieldSettings.Resolution resolution;
        public Vector2 viewportSize;
        public float focusDistance;
        public bool nearLayerActive;
        public bool farLayerActive;
        public float nearFocusStart;
        public float nearFocusEnd;
        public float farFocusStart;
        public float farFocusEnd;
        public int nearSampleCount;
        public float nearMaxBlur;
        public int farSampleCount;
        public float farMaxBlur;
        //physical camera params
        public float physicalCameraAperture;
        public Vector2 physicalCameraCurvature;
        public float physicalCameraBarrelClipping;
        public int physicalCameraBladeCount;
        public float physicalCameraAnamorphism;
    }

    DepthOfFieldParameters PrepareDOFParameters() {
        DepthOfFieldParameters parameters = new DepthOfFieldParameters();
        //prepare compute shader info
        parameters.dofKernelCS = settings.dofKernel;
        parameters.dofKernelKernel = parameters.dofKernelCS.FindKernel("DOFBokehKernel");
        parameters.dofCoCCS = settings.dofCoc;
        if(settings.focusMode == DepthOfFieldSettings.FocusMode.Manual) {
            parameters.dofCoCKernel = parameters.dofCoCCS.FindKernel("DOFCoCManual");
        } else {
            parameters.dofCoCKernel = parameters.dofCoCCS.FindKernel("DOFCoCPhysical");
        }
        parameters.dofCoCReprojectCS = settings.dofReproj;
        parameters.dofCoCReprojectKernel = parameters.dofCoCReprojectCS.FindKernel("DOFCoCReProj");
        parameters.dofPrefilterCS = settings.dofPrefitler;
        parameters.dofPrefilterKernel = parameters.dofPrefilterCS.FindKernel("DOFPrefilter");
        parameters.dofMipGenCS = settings.dofMipGen;
        if (settings.useAdvanced) {
        } else {
            parameters.dofMipColorkernel = parameters.dofMipGenCS.FindKernel("DOFFarLayerMipColor");
            parameters.dofMipCoCkernel = parameters.dofMipGenCS.FindKernel("DOFFarLayerMipCoC");
        }
        parameters.dofDilateCS = settings.dofDilate;
        parameters.dofDilateKernel = parameters.dofDilateCS.FindKernel("DOFNearCoCDilate");
        parameters.dofTileMaxCS = settings.dofTileMax;
        parameters.dofTileMaxKernel = parameters.dofTileMaxCS.FindKernel("DOFCoCTileMax");
        parameters.dofClearIndirectAvgsKernal = parameters.dofTileMaxCS.FindKernel("ClearIndirect");
        parameters.dofGatherCS = settings.dofGather;
        parameters.dofGatherNearKernel = parameters.dofGatherCS.FindKernel("DOFGatherNear");
        parameters.dofGatherFarKernel = parameters.dofGatherCS.FindKernel("DOFGatherFar");
        parameters.dofCombineCS = settings.dofCombine;
        parameters.dofCombineNearKernel = parameters.dofCombineCS.FindKernel("DOFCombineNear");
        parameters.dofCombineFarKernel = parameters.dofCombineCS.FindKernel("DOFPreCombineFar");
        //compute rt info
        parameters.viewportSize = bufferSize;
        parameters.resolution = settings.resolution;
        float scale = settings.useAdvanced ? 1f : 1f / (float)parameters.resolution;
        float resolutionScale = (bufferSize.y / 1080f) * (scale * 2f);
        int targetWidth = Mathf.RoundToInt(bufferSize.x * scale);
        int targetHeight = Mathf.RoundToInt(bufferSize.y * scale);
        int threadGroup8X = (targetWidth + 7) / 8;
        int threadGroup8Y = (targetHeight + 7) / 8;
        parameters.threadGroup8 = new Vector2Int(threadGroup8X, threadGroup8Y);
        //sample varibles
        int farSamples = Mathf.CeilToInt(settings.farBlurSampleCount * resolutionScale);
        int nearSamples = Mathf.CeilToInt(settings.nearBlurSampleCount * resolutionScale);
        parameters.nearMaxBlur = settings.nearBlurMaxRadius;
        parameters.farMaxBlur = settings.farBlurMaxRadius;
        //want at least 3 samples for both far and near
        parameters.nearSampleCount = Mathf.Max(3, nearSamples);
        parameters.farSampleCount = Mathf.Max(3, farSamples);
        parameters.nearFocusStart = settings.nearRangeStart;
        parameters.nearFocusEnd = settings.nearRangeEnd;
        parameters.farFocusStart = settings.farRangeStart;
        parameters.farFocusEnd = settings.farRangeEnd;
        //physical parameters
        parameters.physicalCameraAperture = physcialCamera.fStop;
        parameters.physicalCameraCurvature = physcialCamera.curvature;
        parameters.physicalCameraBarrelClipping = physcialCamera.barrelClipping;
        parameters.physicalCameraBladeCount = physcialCamera.bladeCount;
        parameters.physicalCameraAnamorphism = physcialCamera.anamorphism;
        if(settings.focusDistanceMode == DepthOfFieldSettings.FocusDistanceMode.Camera) {
            parameters.focusDistance = physcialCamera.focusDistance;
        } else {
            parameters.focusDistance = settings.focusDistance;
        }

        bool nearLayerActive = parameters.nearLayerActive;
        bool farLayerActive = parameters.farLayerActive;
        bool bothLayersActive = nearLayerActive && farLayerActive;
        parameters.useAdaneced = settings.useAdvanced;
        //keywords, unity supports compute shader keywords from 2020.0.1
#if UNITY_2020_1_OR_NEWER
        parameters.dofCoCReprojectCS.shaderKeywords = null;
        parameters.dofPrefilterCS.shaderKeywords = null;
        parameters.dofTileMaxCS.shaderKeywords = null;
        parameters.dofGatherCS.shaderKeywords = null;
        parameters.dofCombineCS.shaderKeywords = null;

        if (parameters.resolution == DepthOfFieldSettings.Resolution.Full) {
            parameters.dofPrefilterCS.EnableKeyword("FULL_RES");
        }
        if (bothLayersActive || nearLayerActive) {
            parameters.dofPrefilterCS.EnableKeyword("NEAR");
            parameters.dofTileMaxCS.EnableKeyword("NEAR");
            parameters.dofCombineCS.EnableKeyword("NEAR");
        }
        if (bothLayersActive || !nearLayerActive) {
            parameters.dofPrefilterCS.EnableKeyword("FAR");
            parameters.dofTileMaxCS.EnableKeyword("FAR");
            parameters.dofCombineCS.EnableKeyword("FAR");
        }

        if (settings.useAdvanced) {
            parameters.dofCoCReprojectCS.EnableKeyword("MAX_BLENDING");
            //fix the resolution to half. This only affects the out-of-focus regions (and there is no visible benefit at computing those at higher res). Tiles with pixels near the focus plane always run at full res
            parameters.resolution = DepthOfFieldSettings.Resolution.Half;
        }
#endif
        return parameters;
    }

    void GetDoFResolutionScale(in DepthOfFieldParameters dofParameters, out float scale, out float resolutionScale) {
        //the DoF sampling is performed in normalized space in the shader, so we don't need any scaling for half/quarter resoltion
        scale = 1f / (float)dofParameters.resolution;
        resolutionScale = (dofParameters.viewportSize.y / 1080f) * 2f;
    }

    float GetDoFResolutionMaxMip(in DepthOfFieldParameters dofParameters) {
        //for low sample counts & resolution scales, the DoF result looks very different from base resolutions, try to enforce a maximum mip to clamp to depending on the the resolution scale
        switch (dofParameters.resolution) {
            case DepthOfFieldSettings.Resolution.Full:
                return 4.0f;
            case DepthOfFieldSettings.Resolution.Half:
                return 3.0f;
            default:
                return 2.0f;
        }
    }

    int GetDoFDilationPassCount(in float dofScale, in float nearMaxBlur) {
        return Mathf.CeilToInt((nearMaxBlur * dofScale + 2) / 4f);
    }

    void DepthOfFieldPass(in DepthOfFieldParameters dofParameters, CommandBuffer buffer, ComputeBuffer nearBokehKernel, ComputeBuffer farBokehKernel) {
        bool nearLayerAcitve = dofParameters.nearLayerActive;
        bool farLayerActive = dofParameters.farLayerActive;
        const uint indirectNearOffset = 0u * sizeof(uint);
        const uint indirectFarOffset = 3u * sizeof(uint);
        //data prepare
        int bladeCount = dofParameters.physicalCameraBladeCount;
        float rotation = (dofParameters.physicalCameraAperture - PhyscialCameraSettings.MinAperture) / (PhyscialCameraSettings.MaxAperture - PhyscialCameraSettings.MinAperture);
        //TODO: crude approximation, make it correct
        rotation *= (360f / bladeCount) * Mathf.Deg2Rad;
        float ngonFactor = 1f;
        if (dofParameters.physicalCameraCurvature.y - dofParameters.physicalCameraCurvature.x > 0f) {
            ngonFactor = (dofParameters.physicalCameraAperture - dofParameters.physicalCameraCurvature.x) / (dofParameters.physicalCameraCurvature.y - dofParameters.physicalCameraCurvature.x);
        }
        ngonFactor = Mathf.Clamp01(ngonFactor);
        ngonFactor = Mathf.Lerp(ngonFactor, 0f, Mathf.Abs(dofParameters.physicalCameraAnamorphism));
        //magic numbers
        float anamorphism = dofParameters.physicalCameraAnamorphism / 4f;
        float barrelClipping = dofParameters.physicalCameraBarrelClipping / 3f;
        //get dof rt scale
        GetDoFResolutionScale(dofParameters, out float scale, out float resolutionScale);
        var screenScale = new Vector2(scale, scale);
        //rt width and height
        int targetWidth = Mathf.RoundToInt(dofParameters.viewportSize.x * scale);
        int targetHeight = Mathf.RoundToInt(dofParameters.viewportSize.y * scale);
        buffer.SetGlobalVector(targetScaleId, new Vector4((float)dofParameters.resolution, scale, 0.0f, 0.0f));
        int farSamples = dofParameters.farSampleCount;
        int nearSamples = dofParameters.nearSampleCount;
        //scale sample radius with scale
        float farMaxBlur = dofParameters.farMaxBlur * resolutionScale;
        float nearMaxBlur = dofParameters.nearMaxBlur * resolutionScale;
        //init cs and kernel
        ComputeShader cs;
        int kernel;
        //generate bokeh kernel
        buffer.BeginSample("DepthOfFieldKernel");
        cs = dofParameters.dofKernelCS;
        kernel = dofParameters.dofKernelKernel;
        if (nearLayerAcitve) {
            buffer.SetComputeVectorParam(cs, params1Id, new Vector4(nearSamples, ngonFactor, bladeCount, rotation));
            buffer.SetComputeVectorParam(cs,params2Id, new Vector4(anamorphism, 0f, 0f, 0f));
            buffer.SetComputeBufferParam(cs, kernel, bokehKernelId, nearBokehKernel);
            buffer.DispatchCompute(cs, kernel, Mathf.CeilToInt((nearSamples * nearSamples) / 64f), 1, 1);
        }
        if (farLayerActive) {
            buffer.SetComputeVectorParam(cs, params1Id, new Vector4(farSamples, ngonFactor, bladeCount, rotation));
            buffer.SetComputeVectorParam(cs, params2Id, new Vector4(anamorphism, 0f, 0f, 0f));
            buffer.SetComputeBufferParam(cs, kernel, bokehKernelId, farBokehKernel);
            buffer.DispatchCompute(cs, kernel, Mathf.CeilToInt((farSamples * farSamples) / 64f), 1, 1);
        }
        buffer.EndSample("DepthOfFieldKernel");
    }

    public void DoDepthOfField() {
        if(settings.focusMode == DepthOfFieldSettings.FocusMode.None) {
            return;
        }
        DepthOfFieldParameters dofParameters = PrepareDOFParameters();
        InitRenderTextureAndComputeBuffer(dofParameters);

    }

    void InitRenderTextureAndComputeBuffer(DepthOfFieldParameters parameters) {
        if (nearBokehKernel == null) {
            nearBokehKernel = new ComputeBuffer(parameters.nearSampleCount * parameters.nearSampleCount, sizeof(uint));
            nearBokehKernel.name = "Near Bokeh Kernel";
        }
        if (farBokehKernel == null) {
            farBokehKernel = new ComputeBuffer(parameters.farSampleCount * parameters.farSampleCount, sizeof(uint));
            farBokehKernel.name = "Far Bokeh Kernel";
        }
        if (bokehIndirectCmd == null) {
            bokehIndirectCmd = new ComputeBuffer(3 * 2, sizeof(uint), ComputeBufferType.IndirectArguments);
            bokehIndirectCmd.name = "Bokeh Indirect Cmd";
        }
        if (nearBokehTileList == null) {
            nearBokehTileList = new ComputeBuffer(parameters.threadGroup8.x * parameters.threadGroup8.y, sizeof(uint), ComputeBufferType.Append);
            nearBokehTileList.name = "Near Bokeh Tile List";
        }
        if (farBokehTileList == null) {
            farBokehTileList = new ComputeBuffer(parameters.threadGroup8.x * parameters.threadGroup8.y, sizeof(uint), ComputeBufferType.Append);
            farBokehTileList.name = "Far Bokeh Tile List";
        }

        RenderTextureFormat format = useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        RenderTextureFormat cocFormat = RenderTextureFormat.R16;
        GetDoFResolutionScale(parameters, out float scale, out float resolutionScale);
        var screenScale = new Vector2(scale, scale);
        if (parameters.useAdaneced) {

        }
        else{
            //near plane rt
            if (parameters.nearLayerActive) {
                RenderTexture.ReleaseTemporary(pingNear);
                pingNear = GetTemporaryRenderTexture(bufferSize * screenScale, 0, format, true, "Ping Near");
                RenderTexture.ReleaseTemporary(pongNear);
                pongNear = GetTemporaryRenderTexture(bufferSize * screenScale, 0, format, true, "Pong Near");
                RenderTexture.ReleaseTemporary(nearCoC);
                nearCoC = GetTemporaryRenderTexture(bufferSize * screenScale, 0, cocFormat, true, "Near CoC");
                RenderTexture.ReleaseTemporary(nearAlpha);
                nearAlpha = GetTemporaryRenderTexture(bufferSize * screenScale, 0, cocFormat, true, "Near Alpha");
                RenderTexture.ReleaseTemporary(dilatedNearCoC);
                dilatedNearCoC = GetTemporaryRenderTexture(bufferSize * screenScale, 0, cocFormat, true, "Near Alpha");
            } else {
                pingNear = null;
                pongNear = null;
                nearCoC = null;
                nearAlpha = null;
                dilatedNearCoC = null;
            }
            //far plane rt
            if (parameters.nearLayerActive) {
                RenderTexture.ReleaseTemporary(pingFar);
                pingFar = GetTemporaryRenderTexture(bufferSize * screenScale, 0, format, true, "Ping Far");
                pingFar.useMipMap = true;
                RenderTexture.ReleaseTemporary(pongFar);
                pongFar = GetTemporaryRenderTexture(bufferSize * screenScale, 0, format, true, "Pong Far");
                RenderTexture.ReleaseTemporary(farCoC);
                farCoC = GetTemporaryRenderTexture(bufferSize * screenScale, 0, cocFormat, true, "Far CoC");
                farCoC.useMipMap = true;
            } else {
                pingFar = null;
                pongFar = null;
                farCoC = null;
            }
            //coc rt
            float actualNearMaxBlur = parameters.nearMaxBlur * resolutionScale;
            int passCount = GetDoFDilationPassCount(scale, actualNearMaxBlur);
            dilationPingPong = null;
            if(passCount > 1) {
                RenderTexture.ReleaseTemporary(dilationPingPong);
                dilationPingPong = GetTemporaryRenderTexture(bufferSize * screenScale, 0, cocFormat, true, "Dilation ping pong CoC");
            }
            //mip gen rt
            var mipScale = scale;
            for (int i = 0; i < 4; ++i) {
                mipScale *= 0.5f;
                var size = new Vector2(Mathf.RoundToInt(bufferSize.x * mipScale), Mathf.RoundToInt(bufferSize.y * mipScale));
                RenderTexture.ReleaseTemporary(mips[i]);
                mips[i] = GetTemporaryRenderTexture(size, 0, format, true, "CoC Mip");
            }
        }
    }

    RenderTexture GetTemporaryRenderTexture(Vector2 rtSize,int depth, RenderTextureFormat format, bool enableRW, string name) {
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor((int)rtSize.x, (int)rtSize.y, format, depth);
        descriptor.enableRandomWrite = enableRW;
        RenderTexture rt = RenderTexture.GetTemporary(descriptor);
        rt.name = name;
        return rt;
    }

}
