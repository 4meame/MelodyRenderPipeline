using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using static PostFXSettings;

public class DepthOfField : MonoBehaviour {
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
    //rendertexture id
    int pingNearId = Shader.PropertyToID("Ping Near");
    int pongNearId = Shader.PropertyToID("Pong Near");
    int nearAlphaId = Shader.PropertyToID("Near Alpha");
    int nearCoCId = Shader.PropertyToID("Near CoC");
    int dilatedNearCoCId = Shader.PropertyToID("Dilated Near CoC");
    int pingFarId = Shader.PropertyToID("Ping Far");
    int pongFarId = Shader.PropertyToID("Pong Far");
    int farCoCId = Shader.PropertyToID("Far CoC");
    int fullResCoCId = Shader.PropertyToID("Full Res CoC");
    int dilationPingPongId = Shader.PropertyToID("Dilation Ping Pong");
    int prevCoCHistoryId = Shader.PropertyToID("Prev CoC");
    int nextCoCHistoryId = Shader.PropertyToID("Next CoC");
    int resultId = Shader.PropertyToID("DOF Result");
    int sourceColorId = Shader.PropertyToID("Source Color");
    int colorMipId = Shader.PropertyToID("Color Mip Chain");
    int CoCMipId = Shader.PropertyToID("CoC Mip Chain");
    int minMaxCoCId = Shader.PropertyToID("CoC Min Max");
    int dilatedCoCId = Shader.PropertyToID("CoC Dilated");
    //shader property id
    int targetScaleId = Shader.PropertyToID("_TargetScale");
    int cocTargetScaleId = Shader.PropertyToID("_CoCTargetScale");
    int params1Id = Shader.PropertyToID("_Params1");
    int params2Id = Shader.PropertyToID("_Params2");
    int bokehKernelId = Shader.PropertyToID("_BokehKernel");
    int inputTextureId = Shader.PropertyToID("_InputTexture");
    int inputNearTextureId = Shader.PropertyToID("_InputNearTexture");
    int inputFarTextureId = Shader.PropertyToID("_InputFarTexture");
    int inputNearAlphaTextureId = Shader.PropertyToID("_InputNearAlphaTexture");
    int inputCoCTextureId = Shader.PropertyToID("_InputCoCTexture");
    int inputDilatedCoCTextureId = Shader.PropertyToID("_InputDilatedCoCTexture");
    int inputNearCoCTextureId = Shader.PropertyToID("_InputNearCoCTexture");
    int inputFarCoCTextureId = Shader.PropertyToID("_InputFarCoCTexture");
    int inputCoCHistoryTextureId = Shader.PropertyToID("_InputHistoryCoCTexture");
    int outputTextureId = Shader.PropertyToID("_OutputTexture");
    int outputNearTextureId = Shader.PropertyToID("_OutputNearTexture");
    int outputCoCTextureId = Shader.PropertyToID("_OutputCoCTexture");
    int outputAlphaTextureId = Shader.PropertyToID("_OutputAlphaTexture");
    int outputNearCoCTextureId = Shader.PropertyToID("_OutputNearCoCTexture");
    int outputFarCoCTextureId = Shader.PropertyToID("_OutputFarCoCTexture");
    int outputFarTextureId = Shader.PropertyToID("_OutputFarTexture");
    int outputMip1Id = Shader.PropertyToID("_OutputMip1");
    int outputMip2Id = Shader.PropertyToID("_OutputMip2");
    int outputMip3Id = Shader.PropertyToID("_OutputMip3");
    int outputMip4Id = Shader.PropertyToID("_OutputMip4");
    int indirectBufferId = Shader.PropertyToID("_IndirectBuffer");
    int nearBokehTileListId = Shader.PropertyToID("_NearTileList");
    int farBokehTileListId = Shader.PropertyToID("_FarTileList");
    int tileListId = Shader.PropertyToID("_TileList");

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
        public int dofMipColorKernel;
        public int dofMipCoCkernel;
        public ComputeShader dofDilateCS;
        public int dofDilateKernel;
        public ComputeShader dofTileMaxCS;
        public int dofTileMaxKernel;
        public ComputeShader dofClearIndirectCS;
        public int dofClearIndirectAvgsKernal;
        public ComputeShader dofGatherCS;
        public int dofGatherNearKernel;
        public int dofGatherFarKernel;
        public ComputeShader dofPreCombineCS;
        public int dofPreCombineKernel;
        public ComputeShader dofCombineCS;
        public int dofCombineKernel;
        public bool taaEnabled;
        //advanced DOF
        public bool useAdvanced;
        public ComputeShader dofAdvancedCS;
        public int advDofCoCKernal;
        public int dofCoCMinMaxKernel;
        public int minMaxCoCTileSize;
        public int dofCoCMinMaxDilateKernel;

        public DepthOfFieldSettings.FocusMode focusMode;
        public DepthOfFieldSettings.Resolution resolution;
        public Vector2Int viewportSize;
        public Vector2Int threadGroup8;
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
        if (settings.focusMode == DepthOfFieldSettings.FocusMode.Manual) {
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
            parameters.dofMipColorKernel = parameters.dofMipGenCS.FindKernel("DOFMipColorMip0");
            parameters.dofMipCoCkernel = parameters.dofMipGenCS.FindKernel("DOFMipCoCMip0");
        } else {
            parameters.dofMipColorKernel = parameters.dofMipGenCS.FindKernel("DOFFarLayerMipColor");
            parameters.dofMipCoCkernel = parameters.dofMipGenCS.FindKernel("DOFFarLayerMipCoC");
        }
        parameters.dofDilateCS = settings.dofDilate;
        parameters.dofDilateKernel = parameters.dofDilateCS.FindKernel("DOFNearCoCDilate");
        parameters.dofTileMaxCS = settings.dofTileMax;
        parameters.dofTileMaxKernel = parameters.dofTileMaxCS.FindKernel("DOFCoCTileMax");
        parameters.dofClearIndirectCS = settings.dofClear;
        parameters.dofClearIndirectAvgsKernal = parameters.dofClearIndirectCS.FindKernel("ClearIndirect");
        parameters.dofGatherCS = settings.dofGather;
        parameters.dofGatherNearKernel = parameters.dofGatherCS.FindKernel("DOFGatherNear");
        parameters.dofGatherFarKernel = parameters.dofGatherCS.FindKernel("DOFGatherFar");
        parameters.dofPreCombineCS = settings.dofPreCombine;
        parameters.dofPreCombineKernel = parameters.dofPreCombineCS.FindKernel("DOFPreCombine");
        parameters.dofCombineCS = settings.dofCombine;
        parameters.dofCombineKernel = parameters.dofCombineCS.FindKernel("DOFCombine");
        //advanced dof compute
        parameters.dofAdvancedCS = settings.dofAdvanced;
        if (settings.focusMode == DepthOfFieldSettings.FocusMode.Manual) {
            parameters.advDofCoCKernal = parameters.dofAdvancedCS.FindKernel("CircleOfConfusionManual");
        } else {
            parameters.advDofCoCKernal = parameters.dofAdvancedCS.FindKernel("CircleOfConfusionPhysical");
        }
        parameters.dofCoCMinMaxKernel = parameters.dofAdvancedCS.FindKernel("CircleOfConfusionMinMax");
        parameters.dofCoCMinMaxDilateKernel = parameters.dofAdvancedCS.FindKernel("CircleOfConfusionDilate");
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
        parameters.minMaxCoCTileSize = 8;
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
        if (settings.focusMode == DepthOfFieldSettings.FocusMode.Manual) {
            parameters.focusMode = DepthOfFieldSettings.FocusMode.Manual;
        } else {
            parameters.focusMode = DepthOfFieldSettings.FocusMode.Physical;
        }
        if (settings.focusDistanceMode == DepthOfFieldSettings.FocusDistanceMode.Camera) {
            parameters.focusDistance = physcialCamera.focusDistance;
        } else {
            parameters.focusDistance = settings.focusDistance;
        }
        if (parameters.nearMaxBlur > 0f && parameters.nearFocusEnd > parameters.nearFocusStart) {
            parameters.nearLayerActive = true;
        }
        if (parameters.farMaxBlur > 0f && parameters.farFocusEnd > parameters.farFocusStart) {
            parameters.farLayerActive = true;
        }
        bool bothLayersActive = parameters.nearLayerActive && parameters.farLayerActive;
        parameters.taaEnabled = settings.taaEnabled;
        parameters.useAdvanced = settings.useAdvanced;
        //keywords, unity supports compute shader keywords from 2020.0.1
#if UNITY_2020_1_OR_NEWER
        parameters.dofCoCReprojectCS.shaderKeywords = null;
        parameters.dofPrefilterCS.shaderKeywords = null;
        parameters.dofTileMaxCS.shaderKeywords = null;
        parameters.dofGatherCS.shaderKeywords = null;
        parameters.dofCombineCS.shaderKeywords = null;

        if (parameters.resolution == DepthOfFieldSettings.Resolution.Full) {
            parameters.dofPrefilterCS.EnableKeyword("FULL_RES");
            parameters.dofCombineCS.EnableKeyword("FULL_RES");
        }
        if (bothLayersActive || parameters.nearLayerActive) {
            parameters.dofPrefilterCS.EnableKeyword("NEAR");
            parameters.dofTileMaxCS.EnableKeyword("NEAR");
            parameters.dofCombineCS.EnableKeyword("NEAR");
        }
        if (bothLayersActive || parameters.farLayerActive) {
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

    void DepthOfFieldPass(in DepthOfFieldParameters dofParameters, CommandBuffer buffer, int sourceId, int resultId,
        ComputeBuffer nearBokehKernel, ComputeBuffer farBokehKernel,
        RenderTargetIdentifier pingNear, RenderTargetIdentifier pongNear,
        RenderTargetIdentifier nearAlpha, RenderTargetIdentifier nearCoC, RenderTargetIdentifier dilatedNearCoC,
        RenderTargetIdentifier pingFar, RenderTargetIdentifier pongFar,
        RenderTargetIdentifier farCoC,
        RenderTargetIdentifier fullResCoC,
        RenderTargetIdentifier dilationPingPong,
        RenderTargetIdentifier prevCoCHistory, RenderTargetIdentifier nextCoCHistory
        ) {
        bool nearLayerActive = dofParameters.nearLayerActive;
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
        buffer.SetGlobalVector(targetScaleId, new Vector4((float)dofParameters.resolution, scale, 0f, 0f));
        int farSamples = dofParameters.farSampleCount;
        int nearSamples = dofParameters.nearSampleCount;
        //scale sample radius with scale
        float farMaxBlur = dofParameters.farMaxBlur * resolutionScale;
        float nearMaxBlur = dofParameters.nearMaxBlur * resolutionScale;
        //init cs and kernel
        ComputeShader cs;
        int kernel;

        //generate bokeh kernel
        //given that we allow full customization of near & far planes we'll need a separate kernel for each layer
        buffer.BeginSample("DepthOfFieldKernel");
        cs = dofParameters.dofKernelCS;
        kernel = dofParameters.dofKernelKernel;
        if (nearLayerActive) {
            buffer.SetComputeVectorParam(cs, params1Id, new Vector4(nearSamples, ngonFactor, bladeCount, rotation));
            buffer.SetComputeVectorParam(cs, params2Id, new Vector4(anamorphism, 0f, 0f, 0f));
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

        //compute CoC in full resolution for temporal reprojtion and combine
        //CoC is stored in a R16 ranged [-1, 1] RT as it makes RT management easier and temporal re-projection cheaper; later transformed into individual targets for near & far layers
        buffer.BeginSample("DepthOfFieldCoC");
        cs = dofParameters.dofCoCCS;
        kernel = dofParameters.dofCoCKernel;
        if (dofParameters.focusMode == DepthOfFieldSettings.FocusMode.Physical) {
            //the sensor scale is used to convert the CoC size from mm to screen pixels
            float sensorScale;
            //"A Lens and Aperture Camera Model for Synthetic Image Generation" [Potmesil81]
            float F = physcialCamera.focalLength / 1000f;
            float A = physcialCamera.focalLength / dofParameters.physicalCameraAperture;
            float P = dofParameters.focusDistance;
            if (physcialCamera.gateFit == Camera.GateFitMode.Horizontal) {
                sensorScale = (0.5f / physcialCamera.sensorSize.x) * dofParameters.viewportSize.x;
            } else {
                sensorScale = (0.5f / physcialCamera.sensorSize.y) * dofParameters.viewportSize.y;
            }
            float maxCoC = sensorScale * (A * F) / Mathf.Max((P - F), 1e-6f);
            buffer.SetComputeVectorParam(cs, params1Id, new Vector4(P, maxCoC, 0f, 0f));
        }
        else {
            float nearEnd = dofParameters.nearFocusEnd;
            float nearStart = Mathf.Min(dofParameters.nearFocusStart, nearEnd - 1e-5f);
            float farStart = Mathf.Max(dofParameters.farFocusStart, nearEnd);
            float farEnd = Mathf.Max(dofParameters.farFocusEnd, farStart + 1e-5f);
            buffer.SetComputeVectorParam(cs, params1Id, new Vector4(nearStart, nearEnd, farStart, farEnd));
        }
        buffer.SetComputeTextureParam(cs, kernel, outputCoCTextureId, fullResCoC);
        buffer.DispatchCompute(cs, kernel, (dofParameters.viewportSize.x + 7) / 8, (dofParameters.viewportSize.y + 7) / 8, 1);
        if (dofParameters.taaEnabled) {
            cs = dofParameters.dofCoCReprojectCS;
            kernel = dofParameters.dofCoCReprojectKernel;
            buffer.SetComputeVectorParam(cs, params1Id, new Vector4(0.9f, 1f, 1f, 0f));
            buffer.SetComputeTextureParam(cs, kernel, inputCoCTextureId, fullResCoC);
            buffer.SetComputeTextureParam(cs, kernel, inputCoCHistoryTextureId, prevCoCHistory);
            buffer.SetComputeTextureParam(cs, kernel, outputCoCTextureId, nextCoCHistory);
            buffer.DispatchCompute(cs, kernel, (dofParameters.viewportSize.x + 7) / 8, (dofParameters.viewportSize.y + 7) / 8, 1);
            //TODO: ping-pong buffer  
            fullResCoC = nextCoCHistory;
        }
        buffer.EndSample("DepthOfFieldCoC");

        //downsample and prefilter CoC and layers
        //only need to pre-multiply the CoC for the far layer; if only near is being rendered we can use the downsampled color target as-is
        buffer.BeginSample("DepthOfFieldPrefilter");
        cs = dofParameters.dofPrefilterCS;
        kernel = dofParameters.dofPrefilterKernel;
        buffer.SetComputeTextureParam(cs, kernel, inputTextureId, sourceId);
        buffer.SetComputeTextureParam(cs, kernel, inputCoCTextureId, fullResCoC);
        buffer.SetComputeVectorParam(cs, cocTargetScaleId, new Vector4(1f, 1f, 0f, 0f));
        if (nearLayerActive) {
            buffer.SetComputeTextureParam(cs, kernel, outputNearCoCTextureId, nearCoC);
            buffer.SetComputeTextureParam(cs, kernel, outputNearTextureId, pingNear);
        }
        if (farLayerActive) {
            buffer.SetComputeTextureParam(cs, kernel, outputFarCoCTextureId, farCoC);
            buffer.SetComputeTextureParam(cs, kernel, outputFarTextureId, pingFar);
        }
        buffer.DispatchCompute(cs, kernel, dofParameters.threadGroup8.x, dofParameters.threadGroup8.y, 1);
        buffer.EndSample("DepthOfFieldPrefilter");

        if (dofParameters.farLayerActive) {
            //mips generation far layer
            //only do this for the far layer because the near layer can't really usevery wide radi
            buffer.BeginSample("DepthOfFieldMipsGen");
            int tx = ((targetWidth >> 1) + 7) / 8;
            int ty = ((targetHeight >> 1) + 7) / 8;
            cs = dofParameters.dofMipGenCS;
            kernel = dofParameters.dofMipColorKernel;
            buffer.SetComputeTextureParam(cs, kernel, inputTextureId, pingFar, 0);
            buffer.SetComputeTextureParam(cs, kernel, outputMip1Id, pingFar, 1);
            buffer.SetComputeTextureParam(cs, kernel, outputMip2Id, pingFar, 2);
            buffer.SetComputeTextureParam(cs, kernel, outputMip3Id, pingFar, 3);
            buffer.SetComputeTextureParam(cs, kernel, outputMip4Id, pingFar, 4);
            buffer.DispatchCompute(cs, kernel, tx, ty, 1);
            kernel = dofParameters.dofMipCoCkernel;
            buffer.SetComputeTextureParam(cs, kernel, inputTextureId, farCoC, 0);
            buffer.SetComputeTextureParam(cs, kernel, outputMip1Id, farCoC, 1);
            buffer.SetComputeTextureParam(cs, kernel, outputMip2Id, farCoC, 2);
            buffer.SetComputeTextureParam(cs, kernel, outputMip3Id, farCoC, 3);
            buffer.SetComputeTextureParam(cs, kernel, outputMip4Id, farCoC, 4);
            buffer.DispatchCompute(cs, kernel, tx, ty, 1);
            buffer.EndSample("DepthOfFieldMipsGen");
        }

        if (dofParameters.nearLayerActive) {
            //dilate the near layer
            buffer.BeginSample("DepthOfFieldDilate");
            cs = dofParameters.dofDilateCS;
            kernel = dofParameters.dofDilateKernel;
            buffer.SetComputeVectorParam(cs, params1Id, new Vector4(targetWidth - 1, targetHeight - 1, 0f, 0f));
            int passCount = GetDoFDilationPassCount(scale, nearMaxBlur);
            buffer.SetComputeTextureParam(cs, kernel, inputCoCTextureId, nearCoC);
            buffer.SetComputeTextureParam(cs, kernel, outputCoCTextureId, dilatedNearCoC);
            buffer.DispatchCompute(cs, kernel, dofParameters.threadGroup8.x, dofParameters.threadGroup8.y, 1);
            if (passCount > 1) {
                //ping-pong       
                var src = dilatedNearCoC;
                var dst = dilationPingPong;
                for (int i = 0; i < passCount; i++) {
                    buffer.SetComputeTextureParam(cs, kernel, inputCoCTextureId, src);
                    buffer.SetComputeTextureParam(cs, kernel, outputCoCTextureId, dst);
                    buffer.DispatchCompute(cs, kernel, dofParameters.threadGroup8.x, dofParameters.threadGroup8.y, 1);
                    Swap(ref src, ref dst);
                }
                dilatedNearCoC = src;
            }
            buffer.EndSample("DepthOfFieldDilate");
        }

        //tile-max classification
        buffer.BeginSample("DepthOfFieldTileMax");
        cs = dofParameters.dofClearIndirectCS;
        kernel = dofParameters.dofClearIndirectAvgsKernal;
        buffer.SetComputeBufferParam(cs, kernel, indirectBufferId, bokehIndirectCmd);
        buffer.DispatchCompute(cs, kernel, 1, 1, 1);
        //build the tile list & indirect command buffer
        cs = dofParameters.dofTileMaxCS;
        kernel = dofParameters.dofTileMaxKernel;
        buffer.SetComputeVectorParam(cs, params1Id, new Vector4(targetWidth - 1, targetHeight - 1, 0f, 0f));
        buffer.SetComputeBufferParam(cs, kernel, indirectBufferId, bokehIndirectCmd);
        if (nearLayerActive) {
            buffer.SetComputeTextureParam(cs, kernel, inputNearCoCTextureId, dilatedNearCoC);
            buffer.SetComputeBufferParam(cs, kernel, nearBokehTileListId, nearBokehTileList);
        }
        if (farLayerActive) {
            buffer.SetComputeTextureParam(cs, kernel, inputFarCoCTextureId, farCoC);
            buffer.SetComputeBufferParam(cs, kernel, farBokehTileListId, farBokehTileList);
        }
        buffer.DispatchCompute(cs, kernel, dofParameters.threadGroup8.x, dofParameters.threadGroup8.y, 1);
        buffer.EndSample("DepthOfFieldTileMax");

        if (dofParameters.farLayerActive) {
            //bokeh blur the far layer
            buffer.BeginSample("DepthOfFieldGatherFar");
            //need to clear dest as we recycle render targets and tiles won't write to all pixels thus leaving previous-frame info
            buffer.SetRenderTarget(pongFar);
            buffer.ClearRenderTarget(false, true, Color.clear);
            cs = dofParameters.dofGatherCS;
            kernel = dofParameters.dofGatherFarKernel;
            buffer.SetComputeVectorParam(cs, params1Id, new Vector4(farSamples, farMaxBlur * scale, barrelClipping, farMaxBlur));
            buffer.SetComputeVectorParam(cs, params2Id, new Vector4(GetDoFResolutionMaxMip(dofParameters), 0f, 0f, 0f));
            buffer.SetComputeTextureParam(cs, kernel, inputTextureId, pingFar);
            buffer.SetComputeTextureParam(cs, kernel, inputCoCTextureId, farCoC);
            buffer.SetComputeTextureParam(cs, kernel, outputTextureId, pongFar);
            buffer.SetComputeBufferParam(cs, kernel, bokehKernelId, farBokehKernel);
            buffer.SetComputeBufferParam(cs, kernel, tileListId, farBokehTileList);
            buffer.DispatchCompute(cs, kernel, bokehIndirectCmd, indirectFarOffset);
            buffer.EndSample("DepthOfFieldGatherFar");
        }

        if (dofParameters.nearLayerActive) {
            //if the far layer was active, use it as a source for the near blur to avoid out-of-focus artifacts (e.g. near blur in front of far blur)
            if (dofParameters.farLayerActive) {
                buffer.BeginSample("DepthOfFieldPreCombineFar");
                cs = dofParameters.dofPreCombineCS;
                kernel = dofParameters.dofPreCombineKernel;
                buffer.SetComputeTextureParam(cs, kernel, inputTextureId, pingNear);
                buffer.SetComputeTextureParam(cs, kernel, inputFarTextureId, pongFar);
                buffer.SetComputeTextureParam(cs, kernel, inputCoCTextureId, farCoC);
                buffer.SetComputeTextureParam(cs, kernel, outputTextureId, pongNear);
                buffer.DispatchCompute(cs, kernel, dofParameters.threadGroup8.x, dofParameters.threadGroup8.y, 1);
                buffer.EndSample("DepthOfFieldPreCombineFar");
            }
        }

        //bokeh blur the near layer
        buffer.BeginSample("DepthOfFieldGatherNear");
        if (!dofParameters.farLayerActive) {
            buffer.SetRenderTarget(pongNear);
            buffer.ClearRenderTarget(false, true, Color.clear);
        }
        buffer.SetRenderTarget(nearAlpha);
        buffer.ClearRenderTarget(false, true, Color.clear);
        cs = dofParameters.dofGatherCS;
        kernel = dofParameters.dofGatherNearKernel;
        buffer.SetComputeVectorParam(cs, params1Id, new Vector4(nearSamples, nearMaxBlur * scale, barrelClipping, nearMaxBlur));
        buffer.SetComputeTextureParam(cs, kernel, inputTextureId, pingNear);
        buffer.SetComputeTextureParam(cs, kernel, inputCoCTextureId, nearCoC);
        buffer.SetComputeTextureParam(cs, kernel, inputDilatedCoCTextureId, dilatedNearCoC);
        buffer.SetComputeTextureParam(cs, kernel, outputTextureId, pongNear);
        buffer.SetComputeTextureParam(cs, kernel, outputAlphaTextureId, nearAlpha);
        buffer.SetComputeBufferParam(cs, kernel, bokehKernelId, nearBokehKernel);
        buffer.SetComputeBufferParam(cs, kernel, tileListId, nearBokehTileList);
        buffer.DispatchCompute(cs, kernel, bokehIndirectCmd, indirectNearOffset);
        buffer.EndSample("DepthOfFieldGatherNear");

        //combine blurred layers
        buffer.BeginSample("DepthOfFieldCombine");
        cs = dofParameters.dofCombineCS;
        kernel = dofParameters.dofCombineKernel;
        if (nearLayerActive) {
            buffer.SetComputeTextureParam(cs, kernel, inputNearTextureId, pongNear);
            buffer.SetComputeTextureParam(cs, kernel, inputNearAlphaTextureId, nearAlpha);
        }
        if (farLayerActive) {
            buffer.SetComputeTextureParam(cs, kernel, inputFarTextureId, pongFar);
            buffer.SetComputeTextureParam(cs, kernel, inputCoCTextureId, fullResCoC);
        }
        buffer.SetComputeTextureParam(cs, kernel, inputTextureId, sourceId);
        buffer.SetComputeTextureParam(cs, kernel, outputTextureId, resultId);
        buffer.DispatchCompute(cs, kernel, (dofParameters.viewportSize.x + 7) / 8, (dofParameters.viewportSize.y + 7) / 8, 1);
        buffer.EndSample("DepthOfFieldCombine");
    }

    void DepthOfFieldAdvancedPass(in DepthOfFieldParameters dofParameters, CommandBuffer buffer, int sourceId, int resultId,
        RenderTargetIdentifier fullResCoC,
        RenderTargetIdentifier prevCoCHistory, RenderTargetIdentifier nextCoCHistory,
        RenderTargetIdentifier minMaxCoC, RenderTargetIdentifier dilatedCoC,
        RenderTargetIdentifier sourceColor, RenderTargetIdentifier sourceMip, RenderTargetIdentifier CoCMip
        ) {
        //currently Physically Based DoF is performed at "full" resolution (ie does not utilize DepthOfFieldResolution)
        //to produce similar results when switching between various resolutions, or dynamic resolution, must incorporate resolution independence, fitted with a 1920x1080 reference resolution.
        var scale = dofParameters.viewportSize / new Vector2(1920f, 1080f);
        float resolutionScale = Mathf.Min(scale.x, scale.y) * 2f;
        float farMaxBlur = resolutionScale * dofParameters.farMaxBlur;
        float nearMaxBlur = resolutionScale * dofParameters.nearMaxBlur;
        bool usePhysicalCamera = dofParameters.focusMode == DepthOfFieldSettings.FocusMode.Physical;
        //map the old "max radius" parameters to a bigger range when driving the dof from physical camera settings, so we can work on more challenging scenes, [0, 16] --> [0, 64], ? really
        float radiusMultiplier = usePhysicalCamera ? 4.0f : 1.0f;
        Vector2 cocLimit = new Vector2(Mathf.Max(radiusMultiplier * farMaxBlur, 0.01f), Mathf.Max(radiusMultiplier * nearMaxBlur, 0.01f));
        float maxCoc = Mathf.Max(cocLimit.x, cocLimit.y);
        ComputeShader cs;
        int kernel;
        //calculate CoC
        buffer.BeginSample("DepthOfFieldCoC");
        cs = dofParameters.dofAdvancedCS;
        kernel = dofParameters.advDofCoCKernal;
        int tx, ty;
        if(dofParameters.focusMode == DepthOfFieldSettings.FocusMode.Physical) {
            //the sensor scale is used to convert the CoC size from mm to screen pixels
            float sensorScale;
            //"A Lens and Aperture Camera Model for Synthetic Image Generation" [Potmesil81]
            float F = physcialCamera.focalLength / 1000f;
            float A = physcialCamera.focalLength / dofParameters.physicalCameraAperture;
            float P = dofParameters.focusDistance;
            if (physcialCamera.gateFit == Camera.GateFitMode.Horizontal) {
                sensorScale = (0.5f / physcialCamera.sensorSize.x) * dofParameters.viewportSize.x;
            } else {
                sensorScale = (0.5f / physcialCamera.sensorSize.y) * dofParameters.viewportSize.y;
            }
            float maxFaxCoC = sensorScale * (A * F) / Mathf.Max((P - F), 1e-6f);
            //scale and bias factors for directyly computing CoC size with a single mad
            float cocBias = maxFaxCoC * (1f - P / camera.farClipPlane);
            float cocScale = maxFaxCoC * P * (camera.farClipPlane - camera.nearClipPlane) / (camera.farClipPlane * camera.nearClipPlane);
            buffer.SetComputeFloatParam(cs, "_FarMaxRadius", cocLimit.x);
            buffer.SetComputeFloatParam(cs, "_NearMaxRadius", cocLimit.y);
            buffer.SetComputeFloatParam(cs, "_CoCBias", cocBias);
            buffer.SetComputeFloatParam(cs, "_CoCScale", cocScale);
        } else {
            float nearEnd = dofParameters.nearFocusEnd;
            float nearStart = Mathf.Min(dofParameters.nearFocusStart, nearEnd - 1e-5f);
            float farStart = Mathf.Max(dofParameters.farFocusStart, nearEnd);
            float farEnd = Mathf.Max(dofParameters.farFocusEnd, farStart + 1e-5f);
            buffer.SetComputeFloatParam(cs, "_FarStart", farStart);
            buffer.SetComputeFloatParam(cs, "_NearEnd", nearEnd);
            buffer.SetComputeFloatParam(cs, "_FarRange", 1.0f / (farEnd - farStart));
            buffer.SetComputeFloatParam(cs, "_NearRange", 1.0f / (nearStart - nearEnd));
            buffer.SetComputeFloatParam(cs, "_FarMaxRadius", cocLimit.x);
            buffer.SetComputeFloatParam(cs, "_NearMaxRadius", cocLimit.y);
        }
        buffer.SetComputeTextureParam(cs, kernel, "_FullResCoCTexture", fullResCoC);
        buffer.DispatchCompute(cs, kernel, (dofParameters.viewportSize.x + 7) / 8, (dofParameters.viewportSize.y + 7) / 8, 1);
        if (dofParameters.taaEnabled) {
            cs = dofParameters.dofCoCReprojectCS;
            kernel = dofParameters.dofCoCReprojectKernel;
            buffer.SetComputeVectorParam(cs, params1Id, new Vector4(0.9f, 1f, 1f, 0f));
            buffer.SetComputeTextureParam(cs, kernel, inputCoCTextureId, fullResCoC);
            buffer.SetComputeTextureParam(cs, kernel, inputCoCHistoryTextureId, prevCoCHistory);
            buffer.SetComputeTextureParam(cs, kernel, outputCoCTextureId, nextCoCHistory);
            buffer.DispatchCompute(cs, kernel, (dofParameters.viewportSize.x + 7) / 8, (dofParameters.viewportSize.y + 7) / 8, 1);
            //TODO: ping-pong buffer  
            fullResCoC = nextCoCHistory;
        }
        buffer.EndSample("DepthOfFieldCoC");

        buffer.BeginSample("DepthOfFieldMipChain");
        //color mip chain
        cs = dofParameters.dofMipGenCS;
        kernel = dofParameters.dofMipColorKernel;
        buffer.CopyTexture(sourceId, sourceColor);
        buffer.SetComputeTextureParam(cs, kernel, "_InputTexture", sourceColor, 0);
        buffer.SetComputeTextureParam(cs, kernel, "_OutputTexture", sourceMip, 0);
        buffer.SetComputeTextureParam(cs, kernel, "_OutputMip1", sourceMip, 1);
        buffer.SetComputeTextureParam(cs, kernel, "_OutputMip2", sourceMip, 2);
        buffer.SetComputeTextureParam(cs, kernel, "_OutputMip3", sourceMip, 3);
        buffer.SetComputeTextureParam(cs, kernel, "_OutputMip4", sourceMip, 4);
        tx = ((dofParameters.viewportSize.x >> 1) + 7) / 8;
        ty = ((dofParameters.viewportSize.y >> 1) + 7) / 8;
        buffer.DispatchCompute(cs, kernel, tx, ty, 1);
        //coc mip chain
        kernel = dofParameters.dofMipCoCkernel;
        buffer.SetComputeTextureParam(cs, kernel, "_InputTexture", fullResCoC, 0);
        buffer.SetComputeTextureParam(cs, kernel, "_OutputTexture", CoCMip, 0);
        buffer.SetComputeTextureParam(cs, kernel, "_OutputMip1", CoCMip, 1);
        buffer.SetComputeTextureParam(cs, kernel, "_OutputMip2", CoCMip, 2);
        buffer.SetComputeTextureParam(cs, kernel, "_OutputMip3", CoCMip, 3);
        buffer.SetComputeTextureParam(cs, kernel, "_OutputMip4", CoCMip, 4);
        buffer.DispatchCompute(cs, kernel, tx, ty, 1);
        buffer.EndSample("DepthOfFieldMipChain");

        //coc flatten
        buffer.BeginSample("DepthOfFieldFlatten");
        cs = dofParameters.dofAdvancedCS;
        kernel = dofParameters.dofCoCMinMaxKernel;
        int tileSize = dofParameters.minMaxCoCTileSize;
        tx = ((dofParameters.viewportSize.x / tileSize) + 7) / 8;
        ty = ((dofParameters.viewportSize.y / tileSize) + 7) / 8;
        buffer.SetComputeTextureParam(cs, kernel, "_RawCoCTexture", fullResCoC, 0);
        buffer.SetComputeTextureParam(cs, kernel, "_MinMaxCoCTexture", minMaxCoC, 0);
        buffer.DispatchCompute(cs, kernel, tx, ty, 1);
        buffer.EndSample("DepthOfFieldFlatten");

        //coc dilate
        buffer.BeginSample("DepthOfFieldDilate");
        cs = dofParameters.dofAdvancedCS;
        kernel = dofParameters.dofCoCMinMaxDilateKernel;
        int iterations = (int)Mathf.Max(Mathf.Ceil(cocLimit.y / dofParameters.minMaxCoCTileSize), 1.0f);
        for (int pass = 0; pass < iterations + 1; ++pass) {
            buffer.SetComputeTextureParam(cs, kernel, "_RawCoCTexture", minMaxCoC, 0);
            buffer.SetComputeTextureParam(cs, kernel, "_DilateCoCTexture", dilatedCoC, 0);
            buffer.DispatchCompute(cs, kernel, tx, ty, 1);
            Swap(ref minMaxCoC, ref dilatedCoC);
        }
        buffer.EndSample("DepthOfFieldDilate");

        //coc gather

    }

    public void DoDepthOfField(int sourceId) {
        if (settings.focusMode == DepthOfFieldSettings.FocusMode.None || camera.cameraType == CameraType.SceneView) {
            return;
        }

        DepthOfFieldParameters dofParameters = PrepareDOFParameters();
        InitRenderTextureAndComputeBuffer(buffer, dofParameters);
        if (dofParameters.useAdvanced) {
            //get history coc
            buffer.CopyTexture(nextCoCHistoryId, prevCoCHistoryId);
            DepthOfFieldAdvancedPass(dofParameters, buffer, sourceId, resultId, fullResCoCId, prevCoCHistoryId, nextCoCHistoryId, minMaxCoCId, dilatedCoCId ,sourceColorId, colorMipId, CoCMipId);
        } else {
            //rest compute buffer
            nearBokehTileList.SetCounterValue(0u);
            farBokehTileList.SetCounterValue(0u);
            //get history coc
            buffer.CopyTexture(nextCoCHistoryId, prevCoCHistoryId);
            DepthOfFieldPass(dofParameters, buffer, sourceId, resultId, nearBokehKernel, farBokehKernel, pingNearId, pongNearId, nearAlphaId, nearCoCId, dilatedNearCoCId, pingFarId, pongFarId, farCoCId, fullResCoCId, dilationPingPongId, prevCoCHistoryId, nextCoCHistoryId);

        }
        buffer.Blit(resultId, sourceId);
        ExecuteBuffer();
    }

    void InitRenderTextureAndComputeBuffer(CommandBuffer buffer, DepthOfFieldParameters parameters) {
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

        GraphicsFormat format = GraphicsFormat.B10G11R11_UFloatPack32;
        GraphicsFormat cocFormat = GraphicsFormat.R16_SFloat;
        GraphicsFormat cocMinMaxFormat = GraphicsFormat.R16G16B16A16_SFloat;
        GetDoFResolutionScale(parameters, out float scale, out float resolutionScale);
        var screenScale = new Vector2(scale, scale);
        if (parameters.useAdvanced) {
            //coc rt
            GetTemporaryRenderTexture(buffer, fullResCoCId, bufferSize, 0, cocFormat, true, false, "Full res CoC");
            GetTemporaryRenderTexture(buffer, prevCoCHistoryId, bufferSize, 0, cocFormat, false, false, "Prev CoC");
            GetTemporaryRenderTexture(buffer, nextCoCHistoryId, bufferSize, 0, cocFormat, true, false, "Next CoC");
            GetTemporaryRenderTexture(buffer, minMaxCoCId, (bufferSize + new Vector2Int(7, 7)) / 8, 0, cocMinMaxFormat, true, false, "CoC Min Max");
            GetTemporaryRenderTexture(buffer, dilatedCoCId, (bufferSize + new Vector2Int(7, 7)) / 8, 0, cocMinMaxFormat, true, false, "CoC Dilated");
            //mip chain rt
            GetTemporaryRenderTexture(buffer, sourceColorId, bufferSize, 0, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default, true, false, "Source Color");
            GetTemporaryRenderTexture(buffer, colorMipId, bufferSize, 0, format, true, true, "Color Mip Chain");
            GetTemporaryRenderTexture(buffer, CoCMipId, bufferSize, 0, cocFormat, true, true, "CoC Mip Chain");
        }
        else{
            //near plane rt
            if (parameters.nearLayerActive) {
                GetTemporaryRenderTexture(buffer, pingNearId, bufferSize * screenScale, 0, format, true, false, "Ping Near");
                GetTemporaryRenderTexture(buffer, pongNearId, bufferSize * screenScale, 0, format, true, false, "Pong Near");
                GetTemporaryRenderTexture(buffer, nearCoCId, bufferSize * screenScale, 0, cocFormat, true, false,"Near CoC");
                GetTemporaryRenderTexture(buffer, nearAlphaId, bufferSize * screenScale, 0, cocFormat, true, false, "Near Alpha");
                GetTemporaryRenderTexture(buffer, dilatedNearCoCId, bufferSize * screenScale, 0, cocFormat, true, false, "Dilated Near CoC");
            } else {
                buffer.ReleaseTemporaryRT(pingNearId);
                buffer.ReleaseTemporaryRT(pongNearId);
                buffer.ReleaseTemporaryRT(nearCoCId);
                buffer.ReleaseTemporaryRT(nearAlphaId);
                buffer.ReleaseTemporaryRT(dilatedNearCoCId);
            }
            //far plane rt
            if (parameters.farLayerActive) {
                GetTemporaryRenderTexture(buffer, pingFarId, bufferSize * screenScale, 0, format, true, true, "Ping Far");
                GetTemporaryRenderTexture(buffer, pongFarId, bufferSize * screenScale, 0, format, true, false, "Pong Far");
                GetTemporaryRenderTexture(buffer, farCoCId, bufferSize * screenScale, 0, cocFormat, true, true, "Far CoC");
            } else {
                buffer.ReleaseTemporaryRT(pingFarId);
                buffer.ReleaseTemporaryRT(pongFarId);
                buffer.ReleaseTemporaryRT(farCoCId);
            }
            //coc rt
            GetTemporaryRenderTexture(buffer, fullResCoCId, bufferSize, 0, cocFormat, true, false, "Full res CoC");
            GetTemporaryRenderTexture(buffer, prevCoCHistoryId, bufferSize, 0, cocFormat, false, false, "Prev CoC");
            GetTemporaryRenderTexture(buffer, nextCoCHistoryId, bufferSize, 0, cocFormat, true, false, "Next CoC");

            float actualNearMaxBlur = parameters.nearMaxBlur * resolutionScale;
            int passCount = GetDoFDilationPassCount(scale, actualNearMaxBlur);
            if(passCount > 1) {
                GetTemporaryRenderTexture(buffer, dilationPingPongId, bufferSize * screenScale, 0, cocFormat, true, false, "Dilation ping pong CoC");
            }
            else {
                buffer.ReleaseTemporaryRT(dilationPingPongId);
            }
            GetTemporaryRenderTexture(buffer, resultId, bufferSize, 0, format, true, false, "DOF Dest");
        }
    }

    void OnDisable() {
        ReleaseComputeBuffer();
    }

    void ReleaseComputeBuffer() {
        if (nearBokehKernel != null) {
            nearBokehKernel.Release();
        }
        if (farBokehKernel != null) {
            farBokehKernel.Release();
        }
        if (bokehIndirectCmd != null) {
            bokehIndirectCmd.Release();
        }
        if (nearBokehTileList != null) {
            nearBokehTileList.Release();
        }
        if (farBokehTileList != null) {
            farBokehTileList.Release();
        }
    }

    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void GetTemporaryRenderTexture(CommandBuffer buffer, int id, Vector2 rtSize, int depth, GraphicsFormat format, bool enableRW, bool useMip, string name) {
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor((int)rtSize.x, (int)rtSize.y, format, depth);
        descriptor.dimension = TextureDimension.Tex2D;
        descriptor.enableRandomWrite = enableRW;
        descriptor.useMipMap = useMip;
        buffer.GetTemporaryRT(id, descriptor);
    }

    void GetTemporaryRenderTexture(CommandBuffer buffer, int id, Vector2 rtSize, int depth, RenderTextureFormat format, bool enableRW, bool useMip, string name) {
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor((int)rtSize.x, (int)rtSize.y, format, depth);
        descriptor.dimension = TextureDimension.Tex2D;
        descriptor.enableRandomWrite = enableRW;
        descriptor.useMipMap = useMip;
        buffer.GetTemporaryRT(id, descriptor);
    }

    static void Swap<T>(ref T a, ref T b) {
        var tmp = a;
        a = b;
        b = tmp;
    }
}
