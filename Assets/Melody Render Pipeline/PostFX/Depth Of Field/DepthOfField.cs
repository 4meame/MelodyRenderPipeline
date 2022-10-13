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

    RenderTexture pingNear;
    RenderTexture pongNear;
    RenderTexture nearAlpha;
    RenderTexture nearCoC;
    RenderTexture dilatedNearCoC;
    RenderTexture pingFar;
    RenderTexture pongFar;
    RenderTexture farCoC;
    RenderTexture fullResCoC;
    RenderTexture[] mips;
    RenderTexture dilationPingPong;
    RenderTexture prevCoCHistroy;
    RenderTexture nextCoCHistory;
    int targetScaleId = Shader.PropertyToID("_TargetScale");

    public void Setup(ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, PostFXSettings settings, PhyscialCameraSettings physcialCamera) {
        this.context = context;
        this.camera = camera;
        this.physcialCamera = physcialCamera;
        this.bufferSize = bufferSize;
        //apply to proper camera
        this.settings = camera.cameraType <= CameraType.SceneView ? (settings ? settings.depthOfFieldSettings : default) : default;
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
        //keywords



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

    void DepthOfFieldPass(in DepthOfFieldParameters dofParameters, CommandBuffer buffer) {
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
        buffer.BeginSample("DepthOfFieldKernel");
        cs = dofParameters.dofKernelCS;
        kernel = dofParameters.dofKernelKernel;

        buffer.EndSample("DepthOfFieldKernel");
    }

    public void DoDepthOfField() {

    }
}
