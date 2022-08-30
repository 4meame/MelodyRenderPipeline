using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRender {
    ScriptableRenderContext context;
    Camera camera;
    const string bufferName = "Render Camera";
    CommandBuffer buffer = new CommandBuffer {
        name = bufferName
    };
    CullingResults cullingResults;
    static ShaderTagId unlitShaderTagId = new ShaderTagId("MelodyUnlit"),
                       forwardShaderTagId = new ShaderTagId("MelodyForward"),
                       deferredShaderTagId = new ShaderTagId("MelodyDeferred");

    Lighting lighting = new Lighting();
    AtmosphereScattering atmosphere = new AtmosphereScattering();
    VolumetricCloud cloud = new VolumetricCloud();
    ScreenSpaceAmbientOcclusion ssao = new ScreenSpaceAmbientOcclusion();
    ScreenSpaceReflection ssr = new ScreenSpaceReflection();
    MotionVectorRender motionVector = new MotionVectorRender();
    SSPlanarReflection sspr = new SSPlanarReflection();
    PostFXStack postFXStack = new PostFXStack();
    //intermediate frame buffer for the camera, to provide a source texture for the FX stack
    //static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");

    //let's separate color and depth in two different buffers
    static int colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment"),
               depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment"),
               depthTextureId = Shader.PropertyToID("_CameraDepthTexture"),
               colorTextureId = Shader.PropertyToID("_CameraColorTexture"),
               sourceTextureId = Shader.PropertyToID("_SourceTexture");

    bool useHDR;
    bool useDepthTexture;
    bool useColorTexture;
    bool useDepthNormalTexture;
    bool useDiffuseTexture;
    bool useSpecularTexture;
    bool useGBuffers;
    bool useIntermediateBuffer;

    #region Utility Params
    static int cameraFOVId = Shader.PropertyToID("_CurrentCameraFOV");
    #endregion
    #region Render Scale
    bool useScaledRendering;
    Vector2Int bufferSize;
    static int bufferSizeId = Shader.PropertyToID("_CameraBufferSize");
    public const float renderScaleMin = 0.1f, renderScaleMax = 2f;
    #endregion
    #region Depth Normal Buffer
    static ShaderTagId depthNormalTagId = new ShaderTagId("DepthNormal");
    static int depthNormalTextureId = Shader.PropertyToID("_CameraDepthNormalTexture");
    #endregion
    #region Diffuse Buffer
    static ShaderTagId diffuseTagId = new ShaderTagId("Diffuse");
    static int diffuseTextureId = Shader.PropertyToID("_CameraDiffuseTexture");
    #endregion
    #region Specular Buffer
    static ShaderTagId specularTagId = new ShaderTagId("Specular");
    static int specularTextureId = Shader.PropertyToID("_CameraSpecularTexture");
    #endregion
    //Deferred Render Buffers
    #region G Buffer
    const int GBufferLength = 4;
    static ShaderTagId GBuffersTagId = new ShaderTagId("GBuffer");
    static int GBuffer1Id = Shader.PropertyToID("_GBuffer1");
    static int GBuffer2Id = Shader.PropertyToID("_GBuffer2");
    static int GBuffer3Id = Shader.PropertyToID("_GBuffer3");
    RenderTargetIdentifier[] GBuffersID = new RenderTargetIdentifier[GBufferLength];
    RenderBufferLoadAction[] GBuffersLA = new RenderBufferLoadAction[GBufferLength];
    RenderBufferStoreAction[] GBuffersSA = new RenderBufferStoreAction[GBufferLength];
    #endregion
    static CameraSettings defaultCameraSettings = new CameraSettings();
    //WebGL 2.0 support
    static bool copyTextureSupported = false;

    static int srcBlendId = Shader.PropertyToID("_CameraSrcBlend"),
               dstBlendId = Shader.PropertyToID("_CameraDstBlend");
    public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useInstancing, bool useLightsPerObject, ShadowSettings shadowSettings, AtmosphereScatteringSettings atmosphereSettings, VolumetricCloudSettings cloudSettings, PostFXSettings postFXSettings, CameraBufferSettings cameraBufferSettings, int colorLUTResolution) {
        this.context = context;
        this.camera = camera;

        var crpCamera = camera.GetComponent<MelodyRenderPipelineCamera>();
        CameraSettings cameraSettings = crpCamera ? crpCamera.Settings : defaultCameraSettings;


        if (cameraSettings.overridePostFX) {
            postFXSettings = cameraSettings.postFXSettings;
        }

        #region Render Scale
        float renderScale = cameraSettings.GetRenderScale(cameraBufferSettings.renderScale);
        //slight deviations from 1 will have neither visual nor performance differences
        useScaledRendering = renderScale < 0.99f || renderScale > 1.01f;
        #endregion

        PrepareBuffer();
        PrepareForSceneWindow();

        if (!Cull(shadowSettings.maxDistance)) {
            return;
        }
        if (camera.cameraType == CameraType.Reflection)
        {
            useDepthTexture = cameraBufferSettings.copyDepthReflections;
            useColorTexture = cameraBufferSettings.copyColorReflections;
        } else {
            useDepthTexture = cameraBufferSettings.copyDepth && cameraSettings.copyDepth;
            useColorTexture = cameraBufferSettings.copyColor && cameraSettings.copyColor;
        }
        useDepthNormalTexture = cameraBufferSettings.useDepthNormal && cameraBufferSettings.renderingPath == CameraBufferSettings.RenderingPath.Forward;
        useDiffuseTexture = cameraBufferSettings.useDiffuse && cameraBufferSettings.renderingPath == CameraBufferSettings.RenderingPath.Forward;
        useSpecularTexture = cameraBufferSettings.useSpecular && cameraBufferSettings.renderingPath == CameraBufferSettings.RenderingPath.Forward;
        useGBuffers = cameraBufferSettings.renderingPath == CameraBufferSettings.RenderingPath.Deferred;
        useHDR = cameraBufferSettings.allowHDR && camera.allowHDR;

        #region Render Scale
        if (useScaledRendering) {
            //clamp scale in 0.1-2 range
            renderScale = Mathf.Clamp(renderScale, renderScaleMin, renderScaleMax);
            bufferSize.x = (int)(camera.pixelWidth * renderScale);
            bufferSize.y = (int)(camera.pixelHeight * renderScale);
        } else {
            bufferSize.x = camera.pixelWidth;
            bufferSize.y = camera.pixelHeight;
        }
        #endregion
        #region FXAA
        cameraBufferSettings.fxaa.enabled = cameraBufferSettings.fxaa.enabled && cameraSettings.allowFXAA;
        #endregion
        #region Volumetric Cloud
        var renderCloud = cloudSettings.enabled && cameraSettings.allowCloud;
        #endregion
        #region Atmosphere Scattering
        var atmosScatter = cameraSettings.allowAtmosFog;
        #endregion
        #region SSPR
        cameraBufferSettings.sspr.enabled = cameraBufferSettings.sspr.enabled && cameraSettings.allowSSPR;
        #endregion
        #region SSAO
        cameraBufferSettings.ssao.enabled = cameraBufferSettings.ssao.enabled && cameraSettings.allowSSAO;
        var renderSSAO = cameraBufferSettings.ssao.enabled;
        #endregion
        #region SSR
        cameraBufferSettings.ssr.enabled = cameraBufferSettings.ssr.enabled && cameraSettings.allowSSR;
        var renderSSR = cameraBufferSettings.ssr.enabled;
        #endregion

        //render shadows before setting up regular camera
        buffer.BeginSample(SampleName);
        #region Utility Params
        float cameraFOV = camera.fieldOfView;
        buffer.SetGlobalFloat(cameraFOVId, cameraFOV);
        #endregion
        #region Render Scale
        buffer.SetGlobalVector(bufferSizeId, new Vector4(1f / bufferSize.x, 1f / bufferSize.y, bufferSize.x, bufferSize.y));
        #endregion
        ExecuteBuffer();
        SetupGlobalTexture();
        if (!useGBuffers) {
            //I leave these three buffers for comparing change of performance with MRT GBuffers
            DrawDiffuse(useDiffuseTexture);
            DrawSpecular(useSpecularTexture);
            DrawDepthNormal(useDepthNormalTexture);
        }
        lighting.Setup(context, cullingResults, shadowSettings, useLightsPerObject);
        ssao.Setup(context, camera, bufferSize, cameraBufferSettings.ssao, useHDR);
        ssr.Setup(context, camera, bufferSize, cameraBufferSettings.ssr, useHDR);
        //motion vector objects
        motionVector.Setup(context, camera, cullingResults, bufferSize, cameraBufferSettings.taa);
        //sspr Objects
        sspr.Setup(context, camera, cullingResults, cameraBufferSettings.sspr, useHDR);
        atmosphere.Setup(context, camera, useHDR, atmosphereSettings);
        cloud.Setup(context, camera, cloudSettings, useHDR);
        postFXStack.Setup(context, camera, lighting, bufferSize, postFXSettings, useHDR, colorLUTResolution, cameraSettings.finalBlendMode, cameraBufferSettings.rescalingMode, cameraBufferSettings.fxaa, cameraSettings.keepAlpha);
        buffer.EndSample(SampleName);
        atmosphere.PrecomputeAll();
        atmosphere.UpdateAll();

        if (!useGBuffers) {
            SetupForward();
            DrawForwardGeometry(useDynamicBatching, useInstancing, useLightsPerObject);
            motionVector.Render();
            //for forward path we use screen-space post fx to render indirect illumination
            if (renderSSR) {
                ssr.Render();
                buffer.name = "Combine SSR";
                Draw(colorAttachmentId, colorTextureId);
                buffer.SetRenderTarget(colorAttachmentId);
                buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.CombineSSR, MeshTopology.Triangles, 3);
                ExecuteBuffer();
            }
            if (renderSSAO) {
                ssao.Render();
                buffer.name = "Combine SSAO";
                Draw(colorAttachmentId, colorTextureId);
                buffer.SetRenderTarget(colorAttachmentId);
                buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.CombineSSAO, MeshTopology.Triangles, 3);
                ExecuteBuffer();
            }
        } else {
            SetupDeferred();
            //draw GBuffers here
            DrawGBuffers(useDynamicBatching, useInstancing, useLightsPerObject);
            motionVector.Render();
            ssr.Render();
            ssao.Render();
            DrawDeferredGeometry(useDynamicBatching, useInstancing, useLightsPerObject);
        }
        //draw SSPR renders
        sspr.Render();

        if (renderCloud) {
            cloud.Render(colorAttachmentId);
        }
        if (atmosScatter) {
            var backGroundTextureId = Shader.PropertyToID("_BackGroundTexture");
            buffer.GetTemporaryRT(backGroundTextureId, bufferSize.x, bufferSize.y, 32, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            buffer.name = "Copy BackGround";
            Draw(colorAttachmentId, backGroundTextureId);
            ExecuteBuffer();
            atmosphere.RenderFog(colorAttachmentId);
        }
        DrawUnsupportedShaders();
        ssao.Debug(colorAttachmentId);
        ssr.Debug(colorAttachmentId);
        DrawGizmosBeforeFX();
        if (postFXStack.IsActive) {
            postFXStack.Render(colorAttachmentId);
        } else if (useIntermediateBuffer) {
            buffer.name = "Final Draw";
            DrawFinal(cameraSettings.finalBlendMode);
            ExecuteBuffer();
        }
        DrawGizmosAfterFX();
        CleanUp();
        Submit();

        motionVector.Refresh();
    }

    void DrawDepthNormal(bool useDepthNormalTexture) {
        if (useDepthNormalTexture) {
            buffer.name = "Draw DepthNormal";
            //set camera properties, including view and projection, so set the current rendertarget after that
            context.SetupCameraProperties(camera);
            buffer.GetTemporaryRT(depthNormalTextureId, bufferSize.x, bufferSize.y, 32, FilterMode.Point, RenderTextureFormat.ARGB64);
            buffer.SetRenderTarget(depthNormalTextureId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            buffer.ClearRenderTarget(true, true, Color.black);
            buffer.BeginSample(SampleName);
            ExecuteBuffer();
            //var depthNormalMaterial = CoreUtils.CreateEngineMaterial("Hidden/Internal-DepthNormalsTexture");
            var depthNormalMaterial = CoreUtils.CreateEngineMaterial("Hidden/DrawDepthNormalTexture");
            var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
            var drawingSettings = new DrawingSettings(depthNormalTagId, sortingSettings);
            drawingSettings.overrideMaterial = depthNormalMaterial;
            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

            sortingSettings.criteria = SortingCriteria.CommonTransparent;
            drawingSettings.sortingSettings = sortingSettings;
            filteringSettings.renderQueueRange = RenderQueueRange.transparent;
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

            buffer.EndSample(SampleName);
            ExecuteBuffer();
        }
    }

    void DrawDiffuse(bool useDiffuse) {
        if (useDiffuse) {
            buffer.name = "Draw Diffuse";
            //set camera properties, including view and projection, so set the current rendertarget after that
            context.SetupCameraProperties(camera);
            buffer.GetTemporaryRT(diffuseTextureId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            buffer.SetRenderTarget(diffuseTextureId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            buffer.ClearRenderTarget(true, true, Color.black);
            buffer.BeginSample(SampleName);
            ExecuteBuffer();
            var diffuseMaterial = CoreUtils.CreateEngineMaterial("Hidden/DrawDiffuseTexture");
            var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
            var drawingSettings = new DrawingSettings(diffuseTagId, sortingSettings);
            drawingSettings.overrideMaterial = diffuseMaterial;
            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
            //will not draw transparent for now
            buffer.EndSample(SampleName);
            ExecuteBuffer();
        }
    }

    void DrawSpecular(bool useSpecular) {
        if (useSpecular) {
            buffer.name = "Draw Specular";
            //set camera properties, including view and projection, so set the current rendertarget after that
            context.SetupCameraProperties(camera);
            buffer.GetTemporaryRT(specularTextureId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            buffer.SetRenderTarget(specularTextureId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            buffer.ClearRenderTarget(true, true, Color.black);
            buffer.BeginSample(SampleName);
            ExecuteBuffer();
            //shader should be refered to package
            var specularMaterial = CoreUtils.CreateEngineMaterial("Hidden/DrawSpecularTexture");
            var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
            var drawingSettings = new DrawingSettings(specularTagId, sortingSettings);
            drawingSettings.overrideMaterial = specularMaterial;
            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
            //will not draw transparent for now
            buffer.EndSample(SampleName);
            ExecuteBuffer();
        }
    }

    void DrawForwardGeometry(bool useDynamicBatching, bool useInstancing, bool useLightsPerObject) {
        //per object light will miss some lighting but sometimes it is not neccessary to calculate all light for one fragment
        PerObjectData lightsPerObjectFlags = useLightsPerObject ? PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None;
        var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings) { 
            enableDynamicBatching = useDynamicBatching, 
            enableInstancing = useInstancing,
            perObjectData = PerObjectData.Lightmaps | 
            PerObjectData.LightProbe | 
            PerObjectData.LightProbeProxyVolume |
            PerObjectData.ShadowMask | 
            PerObjectData.OcclusionProbe | 
            PerObjectData.OcclusionProbeProxyVolume |
            PerObjectData.ReflectionProbes | 
            lightsPerObjectFlags 
        };

        //set draw settings pass, index : 0, pass : ChickenUnlit; index : 1, pass: ChickenLit
        drawingSettings.SetShaderPassName(1, forwardShaderTagId);
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

        context.DrawSkybox(camera);
        if (useColorTexture || useDepthTexture) {
            CopyAttachments();
        }

        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    void DrawGBuffers(bool useDynamicBatching, bool useInstancing, bool useLightsPerObject) {
        //draw Deferred Object
        PerObjectData deferredLightsPerObjectFlags = useLightsPerObject ? PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None;
        var deferredSortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
        var deferredDrawingSettings = new DrawingSettings(deferredShaderTagId, deferredSortingSettings) {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useInstancing,
            perObjectData = PerObjectData.Lightmaps |
            PerObjectData.LightProbe |
            PerObjectData.LightProbeProxyVolume |
            PerObjectData.ShadowMask |
            PerObjectData.OcclusionProbe |
            PerObjectData.OcclusionProbeProxyVolume |
            PerObjectData.ReflectionProbes |
            deferredLightsPerObjectFlags
        };
        var deferredFilteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        context.DrawRenderers(cullingResults, ref deferredDrawingSettings, ref deferredFilteringSettings);

        context.DrawSkybox(camera);
        if (useColorTexture || useDepthTexture) {
            CopyAttachments();
        }
    }

    void DrawDeferredGeometry(bool useDynamicBatching, bool useInstancing, bool useLightsPerObject) {
        buffer.name = "Draw DeferredGeometry";
        context.SetupCameraProperties(camera);
        //NOTE : order makes sense
        buffer.SetRenderTarget(colorAttachmentId,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
            depthAttachmentId,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, true, Color.clear);
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
        //per object light will miss some lighting but sometimes it is not neccessary to calculate all light for one fragment
        PerObjectData lightsPerObjectFlags = useLightsPerObject ? PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None;
        var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings) {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useInstancing,
            perObjectData = PerObjectData.Lightmaps |
            PerObjectData.LightProbe |
            PerObjectData.LightProbeProxyVolume |
            PerObjectData.ShadowMask |
            PerObjectData.OcclusionProbe |
            PerObjectData.OcclusionProbeProxyVolume |
            PerObjectData.ReflectionProbes |
            lightsPerObjectFlags
        };

        //set draw settings pass, index : 0, pass : ChickenUnlit; index : 1, pass: ChickenLit
        drawingSettings.SetShaderPassName(1, deferredShaderTagId);
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

        context.DrawSkybox(camera);

        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        buffer.EndSample(SampleName);
        ExecuteBuffer();
    }

    void SetupForward() {
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;
        //Init before potentially getting the attachments
        //NOTE : because render scale also needs intermediate buffer, it will take a bit of extra work when not using post fx.
        useIntermediateBuffer = useColorTexture || useDepthTexture || postFXStack.IsActive || useScaledRendering;

        //before clearing the render target, store the temp render texture for post fx
        if (useIntermediateBuffer) {
            //always clear depth and color to be guaranteed to cover previous data, unless a sky box is used
            if(flags > CameraClearFlags.Color) {
                flags = CameraClearFlags.Color;
            }
            //use HDR or not
            //the reason why frameBuffer get darker when using HDR is linear color data that default HDR RT format stored, are incorrectly displayed in sRGB
            buffer.GetTemporaryRT(colorAttachmentId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            buffer.GetTemporaryRT(depthAttachmentId, bufferSize.x, bufferSize.y, 32, FilterMode.Point, RenderTextureFormat.Depth);
            //NOTE : order makes sense
            buffer.SetRenderTarget(colorAttachmentId,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                depthAttachmentId,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }

        buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, flags == CameraClearFlags.Color, flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
    }

    void SetupDeferred() {
        context.SetupCameraProperties(camera);
        useIntermediateBuffer = true;
        //init deferred render GBuffers
        buffer.GetTemporaryRT(colorAttachmentId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
        buffer.GetTemporaryRT(GBuffer1Id, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
        buffer.GetTemporaryRT(GBuffer2Id, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
        buffer.GetTemporaryRT(GBuffer3Id, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
        buffer.GetTemporaryRT(depthAttachmentId, bufferSize.x, bufferSize.y, 32, FilterMode.Point, RenderTextureFormat.Depth);
        //identifies a render texture for a command buffer.
        GBuffersID[0] = new RenderTargetIdentifier(colorAttachmentId);
        GBuffersID[1] = new RenderTargetIdentifier(GBuffer1Id);
        GBuffersID[2] = new RenderTargetIdentifier(GBuffer2Id);
        GBuffersID[3] = new RenderTargetIdentifier(GBuffer3Id);
        for (int i = 0; i < GBufferLength; i++) {
            GBuffersLA[i] = RenderBufferLoadAction.DontCare;
            GBuffersSA[i] = RenderBufferStoreAction.Store;
        }
        RenderTargetBinding renderTargetBinding = new RenderTargetBinding(GBuffersID,
            GBuffersLA, GBuffersSA,
            depthAttachmentId,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.SetGlobalTexture("_CameraDiffuseTexture", GBuffersID[1]);
        buffer.SetGlobalTexture("_CameraSpecularTexture", GBuffersID[2]);
        buffer.SetGlobalTexture("_CameraDepthNormalTexture", GBuffersID[3]);
        buffer.SetRenderTarget(renderTargetBinding);
        buffer.ClearRenderTarget(true, true, Color.clear);
        //buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, flags == CameraClearFlags.Color, flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);

        //disable keywords in case colorattachment will be affected
        buffer.DisableShaderKeyword("_SSAO_ON");
        buffer.DisableShaderKeyword("_SSR_ON");
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
    }

    void SetupGlobalTexture() {
        //Init set up "missing" GBuffers texture
        buffer.SetGlobalTexture("_CameraDiffuseTexture", missingTexture);
        buffer.SetGlobalTexture("_CameraSpecularTexture", missingTexture);
        buffer.SetGlobalTexture("_CameraDepthNormalTexture", missingTexture);
        //Init set up "missing" depth and color texture
        buffer.SetGlobalTexture("_CameraDepthTexture", missingTexture);
        buffer.SetGlobalTexture("_CameraColorTexture", missingTexture);
        //Init set up "missing" motion vector texture
        buffer.SetGlobalTexture("_CameraMotionVectorTexture", missingTexture);
    }

    void Submit() {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }

    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    bool Cull(float maxShadowDistance) {
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p)) {
            p.shadowDistance = Mathf.Min(camera.farClipPlane, maxShadowDistance);
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }

    void CleanUp() {
        lighting.CleanUp();
        if (useIntermediateBuffer) {
            buffer.ReleaseTemporaryRT(colorAttachmentId);
            buffer.ReleaseTemporaryRT(depthAttachmentId);
            if (useDepthTexture) {
                buffer.ReleaseTemporaryRT(depthTextureId);
            }
            if (useColorTexture) {
                buffer.ReleaseTemporaryRT(colorTextureId);
            }
        }
        if (useDepthNormalTexture) {
            buffer.ReleaseTemporaryRT(depthNormalTextureId);
        }
        if (useDiffuseTexture) {
            buffer.ReleaseTemporaryRT(diffuseTextureId);
        }
        if (useSpecularTexture) {
            buffer.ReleaseTemporaryRT(specularTextureId);
        }
        if (useGBuffers) {
            buffer.ReleaseTemporaryRT(GBuffer1Id);
            buffer.ReleaseTemporaryRT(GBuffer2Id);
            buffer.ReleaseTemporaryRT(GBuffer3Id);
        }
        motionVector.CleanUp();
        sspr.CleanUp();
        ssao.CleanUp();
        ssr.CleanUp();
        cloud.CleanUp();
    }

    //draw depth use "Copy Depth" pass(1)
    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, bool isDepth = false) {
        buffer.SetGlobalTexture(sourceTextureId, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, material, isDepth ? (int)Pass.CopyDepth : (int)Pass.Copy, MeshTopology.Triangles, 3);
    }

    //Copy buffer attachment
    void CopyAttachments() {
        string name = buffer.name;
        if (useColorTexture) {
                buffer.GetTemporaryRT(colorTextureId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
                if (copyTextureSupported) {
                    buffer.CopyTexture(colorAttachmentId, colorTextureId);
                } else {
                    buffer.name = "Copy Color";
                    Draw(colorAttachmentId, colorTextureId);
                    ExecuteBuffer();
                }
        }
        if (useDepthTexture) {

                buffer.GetTemporaryRT(depthTextureId, bufferSize.x, bufferSize.y, 32, FilterMode.Point, RenderTextureFormat.Depth);
                if (copyTextureSupported)
                {
                    buffer.CopyTexture(depthAttachmentId, depthTextureId);
                } else {
                    buffer.name = "Copy Depth";
                    Draw(depthAttachmentId, depthTextureId, true);
                    ExecuteBuffer();
                }
        }
        if (!copyTextureSupported) {
            //NOTE : because Draw changes the render target, we have to set render target back, loading color attachments again 
            buffer.SetRenderTarget(colorAttachmentId,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                depthAttachmentId,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            buffer.name = name;
            ExecuteBuffer();
        }
    }

    //duplicate Draw methods and manully set viewport after seting render target
    void DrawFinal(CameraSettings.FinalBlendMode finalBlendMode) {
        buffer.SetGlobalTexture(sourceTextureId, colorAttachmentId);
        buffer.SetGlobalFloat(srcBlendId, (float)finalBlendMode.source);
        buffer.SetGlobalFloat(dstBlendId, (float)finalBlendMode.destination);
        //SetRenderTarget will reset the viewport to cover the entire target
        buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, finalBlendMode.destination == BlendMode.Zero ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
        buffer.SetViewport(camera.pixelRect);
        buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.Copy, MeshTopology.Triangles, 3);
        //set blend mode back to one zero to not affect other actions
        buffer.SetGlobalFloat(srcBlendId, 1f);
        buffer.SetGlobalFloat(dstBlendId, 0f);
    }

    #region Camera Render Material and Missing Texture
    Material material;
    enum Pass {
        Copy,
        CopyDepth,
        CombineSSR,
        CombineSSAO
    }
    //make sure that invalid samples will produce consistent resutlts
    Texture2D missingTexture;
    public CameraRender(Shader shader) {
        material = CoreUtils.CreateEngineMaterial(shader);
        missingTexture = new Texture2D(1, 1) {
            hideFlags = HideFlags.HideAndDontSave,
            name = "Missing"
        };
        //well, black is better
        missingTexture.SetPixel(0, 0, Color.white * 0.5f);
        missingTexture.Apply(true, true);
    }
    public void Dispose() {
        CoreUtils.Destroy(material);
        CoreUtils.Destroy(missingTexture);
    }
    #endregion
}
