using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Melody.Cloud;
using static VolumetricCloudSettings;

public class VolumetricCloud {
    enum Pass {
        Debug,
        PreDepth,
        Base,
        Combine,
        Copy,
        Final
    }
    const string bufferName = "VolemetricCloud";
    CommandBuffer buffer = new CommandBuffer {
        name = bufferName
    };
    ScriptableRenderContext context;
    Camera camera;
    VolumetricCloudSettings settings;
    CloudNoiseGenerator noise;
    CoveragePainter painter;
    Material cloudBaseMaterial;
    Material cloudCombineMaterial;
    Material cloudShadowMaterial;
    Material cloudDebugMaterial;
    bool useHDR;

    public SharedProperties cloudSharedProperties;
    public Vector2 coverageOffset;
    public Vector2 coverageOffsetPerFrame;
    private Vector3 baseOffset;
    private Vector3 detailOffset;

    int subFrameId = Shader.PropertyToID("_SubFrame");
    int prevFrameId = Shader.PropertyToID("_PrevFrame");
    int combFrameId = Shader.PropertyToID("_CombFrame");
    bool isFirstFrame;
    int preDepthId = Shader.PropertyToID("_PreDepth");

    Vector3[] randomVectors;
    Vector4 cloudGradientVector1;
    Vector4 cloudGradientVector2;
    Vector4 cloudGradientVector3;
    public bool IsActive => settings != null;

    public void Setup(ScriptableRenderContext context, Camera camera, VolumetricCloudSettings settings, bool useHDR) {
        this.context = context;
        this.camera = camera;
        this.settings = settings;
        noise = Object.FindObjectOfType<CloudNoiseGenerator>();
        painter = Object.FindObjectOfType<CoveragePainter>();
        this.useHDR = useHDR;

        if (cloudSharedProperties == null) {
            cloudSharedProperties = new SharedProperties();
        }
        UpdateGradientVectors();
        UpdateSharedFromPublicProperties();
        CreateRenderMaterials();
        CreateRenderTextures();
    }

    public void Render(int sourceId) {

        UpdateAnimatedProperties();

        buffer.SetRenderTarget(preDepthId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, cloudBaseMaterial, (int)Pass.PreDepth, MeshTopology.Triangles, 3);
        buffer.SetGlobalTexture("_PreDepth", preDepthId);

        cloudSharedProperties.BeginFrame(camera);
        if (cloudSharedProperties.sizesChangedSinceLastFrame) {
            //just reset subframe nubmer at begin
            cloudSharedProperties.subPixelSize = SubPixelSizeToInt(settings.subPixelSize);
            ReleaseRenderTextures();
            CreateRenderTextures();
            //just reset the first Temporal frame mark here
            isFirstFrame = true;
        }
        Vector3 pos = Vector3.Scale(camera.transform.position - Vector3.one, settings.cameraPositionScale);
        cloudSharedProperties.cameraPosition = pos;
        //use base material to render the first frame with sub pixel and jitter camera, store in the subFrame RT
        cloudSharedProperties.ApplyToMaterial(cloudBaseMaterial, true);
        UpdateMaterialsPublicProperties();
        buffer.SetRenderTarget(subFrameId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, cloudBaseMaterial, (int)Pass.Base, MeshTopology.Triangles, 3);
        //copy the first subFrame to the previous frame
        if (isFirstFrame) {
            buffer.SetGlobalTexture("_CurrFrame", subFrameId);
            buffer.SetRenderTarget(prevFrameId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            buffer.DrawProcedural(Matrix4x4.identity, cloudBaseMaterial, (int)Pass.Copy, MeshTopology.Triangles, 3);
            //VERY IMPORTANT!!! DO NOT change it if camera hasn't been changed
            isFirstFrame = false;
        }
        //Temporal Reprojection Operation
        buffer.SetGlobalTexture("_SubFrame", subFrameId);
        buffer.SetGlobalTexture("_PrevFrame", prevFrameId);
        //combine subFrame and prevFrame with no jitter
        cloudSharedProperties.ApplyToMaterial(cloudCombineMaterial);
        buffer.SetRenderTarget(combFrameId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, cloudCombineMaterial, (int)Pass.Combine, MeshTopology.Triangles, 3);
        //set combine frame to the previous frame
        buffer.SetGlobalTexture("_CurrFrame", combFrameId);
        buffer.SetRenderTarget(prevFrameId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, cloudCombineMaterial, (int)Pass.Copy, MeshTopology.Triangles, 3);
        //this temporal render is end
        cloudSharedProperties.EndFrame();
        //render result to colorattachment
        buffer.SetGlobalTexture("_CurrFrame", prevFrameId);
        buffer.SetRenderTarget(sourceId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, cloudCombineMaterial, (int)Pass.Final, MeshTopology.Triangles, 3);

        if (settings.debugTexture) {
            if (noise == null || !noise.isActiveAndEnabled || painter == null || !painter.isActiveAndEnabled) {
                return;
            }
            noise.UpdateWorleyNoise();
            cloudDebugMaterial.SetTexture("_ShapeNoiseTex", noise.shapeTexture);
            cloudDebugMaterial.SetTexture("_DetailNoiseTex", noise.detailTexture);
            cloudDebugMaterial.SetTexture("_CoverageTex", painter.coverageTexture);
            //set Debug variables
            int debugModeIndex = 0;
            if (noise.displayEnable) {
                debugModeIndex = (noise.activeType == CloudNoiseGenerator.CloudNoiseType.Shape) ? 1 : 2;
            } 
            else if(painter.displayEnable) {
                debugModeIndex = 3;
                cloudDebugMaterial.SetVector("_CoverageOffset", coverageOffset);
            }
            cloudDebugMaterial.SetInt("debugViewMode", debugModeIndex);
            cloudDebugMaterial.SetFloat("debugNoiseSliceDepth", noise.displaySliceDepth);
            cloudDebugMaterial.SetFloat("debugTileAmount", noise.displayTileAmount);
            cloudDebugMaterial.SetFloat("ViewerSize", noise.displaySize);
            cloudDebugMaterial.SetVector("debugChannelWeight", noise.ChannelMask);
            cloudDebugMaterial.SetInt("debugGreyscale", (noise.displayGreyScale) ? 1 : 0);
            cloudDebugMaterial.SetInt("debugShowAllChannels", (noise.displayAllChannel) ? 1 : 0);
            buffer.SetRenderTarget(sourceId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            buffer.DrawProcedural(Matrix4x4.identity, cloudDebugMaterial, (int)Pass.Debug, MeshTopology.Triangles, 3);
        }
        ExecuteBuffer();

    }

    public void CleanUp() {
        ReleaseRenderTextures();
    }

    void CreateRenderMaterials() {
        if (randomVectors == null || randomVectors.Length < 1) {
            randomVectors = new Vector3[] { Random.onUnitSphere,
                    Random.onUnitSphere,
                    Random.onUnitSphere,
                    Random.onUnitSphere,
                    Random.onUnitSphere,
                    Random.onUnitSphere};
        }

        if (cloudBaseMaterial == null) {
            cloudBaseMaterial = new Material(Shader.Find("Hidden/Melody RP/VolumetricCloud"));
            cloudBaseMaterial.hideFlags = HideFlags.HideAndDontSave;
        }
        if (cloudCombineMaterial == null) {
            cloudCombineMaterial = new Material(Shader.Find("Hidden/Melody RP/VolumetricCloud"));
            cloudCombineMaterial.hideFlags = HideFlags.HideAndDontSave;
        }
        if (cloudShadowMaterial == null) {
            cloudShadowMaterial = new Material(Shader.Find("Hidden/Melody RP/VolumetricCloud"));
            cloudShadowMaterial.hideFlags = HideFlags.HideAndDontSave;
        }
        if (cloudDebugMaterial == null) {
            cloudDebugMaterial = new Material(Shader.Find("Hidden/Melody RP/VolumetricCloud"));
            cloudDebugMaterial.hideFlags = HideFlags.HideAndDontSave;
        }
    }

    void CreateRenderTextures() {
        RenderTextureFormat format = useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        buffer.GetTemporaryRT(subFrameId, cloudSharedProperties.subFrameWidth, cloudSharedProperties.subFrameHeight, 0, FilterMode.Bilinear, format, RenderTextureReadWrite.Linear);
        buffer.GetTemporaryRT(prevFrameId, cloudSharedProperties.frameWidth, cloudSharedProperties.frameHeight, 0, FilterMode.Bilinear, format, RenderTextureReadWrite.Linear);
        buffer.GetTemporaryRT(combFrameId, cloudSharedProperties.frameWidth, cloudSharedProperties.frameHeight, 0, FilterMode.Bilinear, format, RenderTextureReadWrite.Linear);
        buffer.GetTemporaryRT(preDepthId, camera.pixelWidth, camera.pixelHeight, 32, FilterMode.Bilinear, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
    }

    void ReleaseRenderTextures() {
        buffer.ReleaseTemporaryRT(subFrameId);
        buffer.ReleaseTemporaryRT(prevFrameId);
        buffer.ReleaseTemporaryRT(combFrameId);
        buffer.ReleaseTemporaryRT(preDepthId);
    }

    public void UpdateSharedFromPublicProperties() {
        Vector3 earthCenter = settings.earthCenter;
        float earthRadius = settings.earthRadius;
        coverageOffset.Set(settings.coverageOffsetX, settings.coverageOffsetY);
        if (settings.useCalculatedRadius) {
            earthRadius = cloudSharedProperties.CalculatePlanetRadius(settings.atmosphereStartHeight, settings.horizonDistanceOfRadius);
            earthCenter = new Vector3(earthCenter.x, earthCenter.y - earthRadius, earthCenter.z);
            if(settings.showCalculatedRadius) {
                Debug.Log(earthRadius);
            }
        }
        cloudSharedProperties.earthCenter = earthCenter;
        cloudSharedProperties.earthRadius = earthRadius;
        cloudSharedProperties.atmosphereStartHeight = settings.atmosphereStartHeight;
        cloudSharedProperties.atmosphereEndHeight = settings.atmosphereEndHeight;
        cloudSharedProperties.cameraPosition = new Vector3(0.0f, earthRadius, 0.0f);
        //VERY IMPORTANT!!! DO NOT reset this value per frame
        //cloudSharedProperties.subPixelSize = SubPixelSizeToInt(settings.subPixelSize);
        cloudSharedProperties.downsample = settings.downsample;
        cloudSharedProperties.useFixedSizes = settings.renderSize == RenderSize.FixedSizes;
        cloudSharedProperties.fixedWidth = settings.fixedWidth;
        cloudSharedProperties.fixedHeight = settings.fixedHeight;
    }
    
    int SubPixelSizeToInt(SubPixelSize size) {
        int value = 2;
        switch (size) {
            case SubPixelSize.Sub1x1: value = 1; break;
            case SubPixelSize.Sub2x2: value = 2; break;
            case SubPixelSize.Sub4x4: value = 4; break;
            case SubPixelSize.Sub8x8: value = 8; break;
        }
        return value;
    }

    void UpdateGradientVectors() {
        cloudGradientVector1 = CloudHeightGradient(settings.cloudGradient1);
        cloudGradientVector2 = CloudHeightGradient(settings.cloudGradient2);
        cloudGradientVector3 = CloudHeightGradient(settings.cloudGradient3);
    }

    Vector4 CloudHeightGradient(Gradient gradient) {
        int l = gradient.colorKeys.Length;
        float a = l > 0 ? gradient.colorKeys[0].time : 0.0f;
        float b = l > 1 ? gradient.colorKeys[1].time : a;
        float c = l > 2 ? gradient.colorKeys[2].time : b;
        float d = l > 3 ? gradient.colorKeys[3].time : c;
        return new Vector4(a, b, c, d);
    }

    public void UpdateAnimatedProperties() {
        coverageOffsetPerFrame = settings.coverageOffsetPerFrame * settings.speed;
        baseOffset = settings.baseOffsetPerFrame * settings.speed;
        detailOffset = settings.detailOffsetPerFrame * settings.speed;
    }

    private void UpdateMaterialsPublicProperties() {
        if (cloudBaseMaterial && camera) {
            if (settings.debugTexture && noise && painter) {
                cloudBaseMaterial.SetTexture("_ShapeNoiseTex", noise.shapeTexture);
                cloudBaseMaterial.SetTexture("_DetailNoiseTex", noise.detailTexture);
                cloudBaseMaterial.SetTexture("_CoverageTex", painter.coverageTexture);
            } else {
                cloudBaseMaterial.SetTexture("_ShapeNoiseTex", settings.shapeTexture);
                cloudBaseMaterial.SetTexture("_DetailNoiseTex", settings.detailTexture);
                cloudBaseMaterial.SetTexture("_CoverageTex", settings.coverageTexture);
            }
            cloudBaseMaterial.SetTexture("_CurlNoiseTex", settings.curlTexture);
            cloudBaseMaterial.SetTexture("_BlueNoiseTex", settings.blueNoiseTexture);
            //coverage
            cloudBaseMaterial.SetVector("_CoverageOffset", coverageOffset);
            cloudBaseMaterial.SetVector("_CoverageOffsetPerFrame", coverageOffsetPerFrame);
            cloudBaseMaterial.SetFloat("_CoverageScale", 1.0f / cloudSharedProperties.maxDistance);
            cloudBaseMaterial.SetFloat("_HorizonCoverageStart", settings.horizonCoverageStart);
            cloudBaseMaterial.SetFloat("_HorizonCoverageEnd", settings.horizonCoverageEnd);
            cloudBaseMaterial.SetVector("_CloudHeightGradient1", cloudGradientVector1);
            cloudBaseMaterial.SetVector("_CloudHeightGradient2", cloudGradientVector2);
            cloudBaseMaterial.SetVector("_CloudHeightGradient3", cloudGradientVector3);
            //shape
            cloudBaseMaterial.SetFloat("_BaseScale", 1.0f / settings.atmosphereEndHeight * settings.baseScale);
            cloudBaseMaterial.SetVector("_BaseOffset", baseOffset);
            cloudBaseMaterial.SetFloat("_SampleThreshold", settings.sampleThreshold);
            cloudBaseMaterial.SetFloat("_SampleScalar", settings.sampleScalar);
            cloudBaseMaterial.SetFloat("_BottomFade", settings.bottomFade);
            //detail
            cloudBaseMaterial.SetFloat("_ErosionEdgeSize", settings.erosionEdgeSize);
            cloudBaseMaterial.SetFloat("_Curl", settings.cloudDistortion);
            cloudBaseMaterial.SetFloat("_CurlScale", settings.cloudDistortionScale);
            cloudBaseMaterial.SetFloat("_DetailScale", settings.detailScale);
            cloudBaseMaterial.SetVector("_DetailOffset", detailOffset);
            //atmosphere
            float atmosphereThickness = settings.atmosphereEndHeight - settings.atmosphereStartHeight;
            //Optimization
            cloudBaseMaterial.SetFloat("_RayMinimumY", settings.horizonLevel);
            cloudBaseMaterial.SetFloat("_MaxIterations", settings.maxIterations);
            cloudBaseMaterial.SetFloat("_LODDistance", settings.lodDistance);
            cloudBaseMaterial.SetFloat("_HorizonFadeScale", settings.horizonFade);
            cloudBaseMaterial.SetFloat("_HorizonFadeStartAlpha", settings.horizonFadeStartAlpha);
            //lighting
            cloudBaseMaterial.SetFloat("_Density", settings.density);
            cloudBaseMaterial.SetFloat("_DarkOutlineScalar", settings.darkOutlineScalar);
            cloudBaseMaterial.SetFloat("_SunRayLength", atmosphereThickness * settings.sunRayLength);
            cloudBaseMaterial.SetFloat("_ConeRadius", atmosphereThickness * settings.coneRadius);
            cloudBaseMaterial.SetFloat("_ForwardScatteringG", settings.forwardScatteringG);
            cloudBaseMaterial.SetFloat("_BackwardScatteringG", settings.backwardScatteringG);
            cloudBaseMaterial.SetVector("_CloudBaseColor", settings.cloudBaseColor);
            cloudBaseMaterial.SetVector("_CloudTopColor", settings.cloudTopColor);
            cloudBaseMaterial.SetFloat("_LightScale", settings.sunScale);
            cloudBaseMaterial.SetFloat("_AmbientScale", settings.ambientScale);
        }
    }

    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}
