using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRender
{
    ScriptableRenderContext context;
    Camera camera;
    const string bufferName = "Render Camera";
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };
    CullingResults cullingResults;
    static ShaderTagId unlitShaderTagId = new ShaderTagId("MelodyUnlit"),
                       litShaderTagId = new ShaderTagId("MelodyLit");                    

    Lighting lighting = new Lighting();
    VolumetricCloud cloud = new VolumetricCloud();
    SSPlanarReflection sspr = new SSPlanarReflection();
    ScreenSpaceReflection ssr = new ScreenSpaceReflection();
    PostFXStack postFXStack = new PostFXStack();
    //intermediate frame buffer for the camera, to provide a source texture for the FX stack
    //static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");

    //let's separate color and depth in two different buffers
    static int colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment"),
               depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment"),
               depthTextureId = Shader.PropertyToID("_CameraDepthTexture"),
               colorTextureId = Shader.PropertyToID("_CameraColorTexture"),
               postColorTextureId = Shader.PropertyToID("_PostCameraColorTexture"),
               sourceTextureId = Shader.PropertyToID("_SourceTexture");

    bool useHDR;
    bool useDepthTexture;
    bool useColorTexture;
    bool useDepthNormalTexture;
    bool useIntermediateBuffer;

    #region Utility Params
    static int timeSinceLevelLoad = Shader.PropertyToID("_Time");
    static int cameraFOVId = Shader.PropertyToID("_CurrentCameraFOV");
    #endregion
    #region Utility Matrix
    static int cilpToViewMatrix = Shader.PropertyToID("_ClipToViewMatrix");
    static int invViewProjMatrix = Shader.PropertyToID("_InvViewProjMatrix");
    #endregion
    #region Render Scale
    bool useScaledRendering;
    Vector2Int bufferSize;
    static int bufferSizeId = Shader.PropertyToID("_CameraBufferSize");
    public const float renderScaleMin = 0.1f, renderScaleMax = 2f;
    #endregion
    #region Depth Normal
    static ShaderTagId depthNormalTagId = new ShaderTagId("DepthNormal");
    static int depthNormalTextureId = Shader.PropertyToID("_CameraDepthNormalTexture");
    #endregion
    static CameraSettings defaultCameraSettings = new CameraSettings();
    //WebGL 2.0 support
    static bool copyTextureSupported = false;

    static int srcBlendId = Shader.PropertyToID("_CameraSrcBlend"),
               dstBlendId = Shader.PropertyToID("_CameraDstBlend");
    public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useInstancing, bool useLightsPerObject, ShadowSettings shadowSettings, VolumetricCloudSettings cloudSettings, PostFXSettings postFXSettings, CameraBufferSettings cameraBufferSettings, int colorLUTResolution) {
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
        useDepthNormalTexture = cameraBufferSettings.useDepthNormal;

        useHDR = cameraBufferSettings.allowHDR && camera.allowHDR;

        #region Render Scale
        if (useScaledRendering)
        {
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
        #region SSPR
        cameraBufferSettings.sspr.enabled = cameraBufferSettings.sspr.enabled && cameraSettings.allowSSPR;
        #endregion
        #region SSR
        cameraBufferSettings.ssr.enabled = cameraBufferSettings.ssr.enabled && cameraSettings.allowSSR;
        #endregion

        //render shadows before setting up regular camera
        buffer.BeginSample(SampleName);
        #region Utility Params
        float time = Time.timeSinceLevelLoad;
        buffer.SetGlobalVector(timeSinceLevelLoad, new Vector4(time / 20.0f, time, time * time, time * time * time));
        float cameraFOV = camera.fieldOfView;
        buffer.SetGlobalFloat(cameraFOVId, cameraFOV);
        #endregion
        #region Utility Matrix
        //project matrix is different from GL and DX, use this to uniform it
        Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
        Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
        Matrix4x4 viewProjMatrix = projMatrix * viewMatrix;
        Matrix4x4 invViewProj = viewProjMatrix.inverse;
        buffer.SetGlobalMatrix(invViewProjMatrix, invViewProj);
        Matrix4x4 clipToView = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true).inverse;
        buffer.SetGlobalMatrix(cilpToViewMatrix, clipToView);
        #endregion
        #region Render Scale
        buffer.SetGlobalVector(bufferSizeId, new Vector4(1f / bufferSize.x, 1f / bufferSize.y, bufferSize.x, bufferSize.y));
        #endregion
        ExecuteBuffer();
        DrawDepthNormal(useDepthNormalTexture);
        lighting.Setup(context, cullingResults, shadowSettings, useLightsPerObject);
        sspr.Setup(context, camera, cullingResults, cameraBufferSettings.sspr, useHDR);
        ssr.Setup(context, camera, cullingResults, cameraBufferSettings.ssr, useHDR, useDynamicBatching, useInstancing, useLightsPerObject);
        cloud.Setup(context, camera, cloudSettings, useHDR);
        postFXStack.Setup(context, camera, lighting, bufferSize, postFXSettings, useHDR, colorLUTResolution, cameraSettings.finalBlendMode, cameraBufferSettings.bicubicRescaling, cameraBufferSettings.fxaa, cameraSettings.keepAlpha);
        buffer.EndSample(SampleName);
        Setup();
        DrawVisibleGeometry(useDynamicBatching, useInstancing, useLightsPerObject);
        sspr.Render();
        ssr.Render();
        if (renderCloud) {
            cloud.Render(colorAttachmentId);
        }
        #region Fix post-Geometry rendering probelms
        if (cameraBufferSettings.usePostGeometryColor) {
            buffer.GetTemporaryRT(postColorTextureId, bufferSize.x, bufferSize.y, 32, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            if (copyTextureSupported) {
                buffer.CopyTexture(colorAttachmentId, postColorTextureId);
            } else {
                buffer.name = "Copy Post-Geometry Color";
                Draw(colorAttachmentId, postColorTextureId);
                ExecuteBuffer();
            }
        }
        #endregion
        DrawUnsupportedShaders();
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
            var depthNormalMaterial = CoreUtils.CreateEngineMaterial("Hidden/Internal-DepthNormalsTexture");
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

    void DrawVisibleGeometry(bool useDynamicBatching, bool useInstancing, bool useLightsPerObject) {
        //per object light will miss some lighting but sometimes it is not neccessary to calculate all light for one fragment
        PerObjectData lightsPerObjectFlags = useLightsPerObject ? PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None;
        var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings) { enableDynamicBatching = useDynamicBatching, enableInstancing = useInstancing,
                                                  perObjectData = PerObjectData.Lightmaps | PerObjectData.LightProbe | PerObjectData.LightProbeProxyVolume | 
                                                                  PerObjectData.ShadowMask | PerObjectData.OcclusionProbe | PerObjectData.OcclusionProbeProxyVolume |
                                                                  PerObjectData.ReflectionProbes | lightsPerObjectFlags };

        //set draw settings pass, index : 0, pass : ChickenUnlit; index : 1, pass: ChickenLit
        drawingSettings.SetShaderPassName(1, litShaderTagId);
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

    void Setup() {
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;
        //Init before potentially getting the attachments
        //NOTE : because render scale also needs intermediate buffer, it will take a bit of extra work when not using post fx.
        useIntermediateBuffer = useColorTexture || useDepthTexture || postFXStack.IsActive || useScaledRendering || cloud.IsActive;

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
        //Init set up a "missing" depth texture
        buffer.SetGlobalTexture(depthTextureId, missingTexture);
        ExecuteBuffer();
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
        sspr.CleanUp();
        ssr.CleanUp();
        cloud.CleanUp();
    }

    //draw depth use "Copy Depth" pass(1)
    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, bool isDepth = false) {
        buffer.SetGlobalTexture(sourceTextureId, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, material, isDepth ? 1 : 0, MeshTopology.Triangles, 3);
    }

    //Copy buffer attachment
    void CopyAttachments() {
        string name = buffer.name;
        if (useColorTexture)
        {
            buffer.GetTemporaryRT(colorTextureId, bufferSize.x, bufferSize.y, 32, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
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
            if (copyTextureSupported) {
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
        buffer.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
        //set blend mode back to one zero to not affect other actions
        buffer.SetGlobalFloat(srcBlendId, 1f);
        buffer.SetGlobalFloat(dstBlendId, 0f);
    }

    #region Camera Render Material and Missing Texture
    Material material;
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
