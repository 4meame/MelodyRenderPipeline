using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static PostFXSettings;

public class PostFXStack {
    enum Pass {
        Copy,
        BloomHorizontal,
        BloomVertical,
        BloomCombineAdditive,
        BloomCombineScatter,
        BloomScatterFinal,
        BloomPrefilter,
        BloomPrefilterFireflies,
        ColorGradingNone,
        ColorGradingReinhard,
        ColorGradingNeutral,
        ColorGradingACES,
        FinalColorGrading,
        Rescale,
        FXAA,
        ColorGradingWithLuma,
        FXAAWithLuma,
        Outline,
        LightShaftsOcclusionPrefilter,
        LightShaftsBloomPrefilter,
        LightShaftsBlur,
        LightShaftsOcclusionBlend,
        LightShaftsBloomBlend
    }

    const string bufferName = "Post FX";
    CommandBuffer buffer = new CommandBuffer { name = bufferName };
    ScriptableRenderContext context;
    Camera camera;
    PostFXSettings settings;
    int fxSourceId = Shader.PropertyToID("_PostFXSource");
    int fxSource2Id = Shader.PropertyToID("_PostFXSource2");
    #region Bloom
    int bloomBucibicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling");
    int bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter");
    int bloomThresholdId = Shader.PropertyToID("_BloomThreshold");
    int bloomIntensityId = Shader.PropertyToID("_BloomIntensity");
    const int maxBloomPyramidLevels = 16;
    int bloomPyramidId;
    int bloomResultId = Shader.PropertyToID("_BloomResult");
    #endregion
    //HDR will change RT format from 8 * 4 bit to 16 * 4 bit, which is darker than LDR
    bool useHDR;
    //if no settings, post fx stack should be skipped
    public bool IsActive => settings != null;
    #region Color Grading
    int colorAdjustmentId = Shader.PropertyToID("_ColorAdjustment");
    int colorFilterId = Shader.PropertyToID("_ColorFilter");
    int whiteBalanceId = Shader.PropertyToID("_WhiteBalance");
    int splitToningShadowsId = Shader.PropertyToID("_SplitToningShadows");
    int splitToningHighlightsId = Shader.PropertyToID("_SplitToningHighlights");
    int channelMixerRedId = Shader.PropertyToID("_ChannelMixerRed");
    int channelMixerGreenId = Shader.PropertyToID("_ChannelMixerGreen");
    int channelMixerBlueId = Shader.PropertyToID("_ChannelMixerBlue");
    int smhShadowsId = Shader.PropertyToID("_SMHShadows");
    int smhMidtonesId = Shader.PropertyToID("_SMHMidtones");
    int smhHighlightsId = Shader.PropertyToID("_SMHHighlights");
    int smhRangeId = Shader.PropertyToID("_SMHRange");
    int colorRampId = Shader.PropertyToID("_ColorRamp");
    int rampGammaId = Shader.PropertyToID("_RampGamma");
    //lut
    int colorLUTResolution;
    int colorGradingLUTId = Shader.PropertyToID("_ColorGradingLUT");
    int colorGradingLUTParamsId = Shader.PropertyToID("_ColorGradingLUTParams");
    int colorGradingLUTInLogId = Shader.PropertyToID("_ColorGradingLUTInLogC");
    #endregion
    #region Multiple Cameras
    CameraSettings.FinalBlendMode finalBlendMode;
    int finalSrcBlendId = Shader.PropertyToID("_FinalSrcBlend");
    int finalDstBlendId = Shader.PropertyToID("_FinalDstBlend");
    #endregion
    #region Rander Scale
    Vector2Int bufferSize;
    CameraBufferSettings.RescalingMode rescalingMode;
    int finalResultId = Shader.PropertyToID("_FinalResult"),
        copyPointId = Shader.PropertyToID("_CopyPoint"),
        copyBicubicId = Shader.PropertyToID("_CopyBicubic");
    #endregion
    #region FXAA
    CameraBufferSettings.FXAA fxaa;
    int colorGradingResultId = Shader.PropertyToID("_ColorGradingResult");
    bool keepAlpha;
    int fxaaConfigId = Shader.PropertyToID("_FXAAConfig");
    const string fxaaQualityLowKeyword = "FXAA_QUALITY_LOW",
		         fxaaQualityMediumKeyword = "FXAA_QUALITY_MEDIUM";
    #endregion
    #region Outline
    int OutlineColor = Shader.PropertyToID("_OutlineColor");
    int OutlineParams = Shader.PropertyToID("_OutlineParams");
    int ThresholdParams = Shader.PropertyToID("_ThresholdParams");
    int ThresholdScale = Shader.PropertyToID("_DepthNormalThresholdScale");
    int outlineResultId = Shader.PropertyToID("_OutlineResult");
    #endregion
    #region LightShafts
    Lighting lighting;
    int lightShafts0Id = Shader.PropertyToID("_LightShafts0");
    int lightShafts1Id = Shader.PropertyToID("_LightShafts1");
    int lightShaftsResultId = Shader.PropertyToID("_LightShaftsResult");
    int lightSource = Shader.PropertyToID("_LightSource");
    int lightShaftParameters = Shader.PropertyToID("_LightShaftParameters");
    int radialBlurParameters = Shader.PropertyToID("_RadialBlurParameters");
    int lightShaftsDensity = Shader.PropertyToID("_ShaftsDensity");
    int lightShaftsWeight = Shader.PropertyToID("_ShaftsWeight");
    int lightShaftsDecay = Shader.PropertyToID("_ShaftsDecay");
    int lightShaftsExposure = Shader.PropertyToID("_ShaftsExposure");
    int bloomTintAndThreshold = Shader.PropertyToID("_BloomTintAndThreshold");
    #endregion
    #region Motion Blur
    ReconstructionFilter reconstructionFilter;
    FrameBlendingFilter frameBlendingFilter;
    int motionResultId = Shader.PropertyToID("_MotionResult");
    #endregion
    public void Setup(ScriptableRenderContext context, Camera camera, Lighting lighting, Vector2Int bufferSize, PostFXSettings settings, bool useHDR, int colorLUTResolution, CameraSettings.FinalBlendMode finalBlendMode, CameraBufferSettings.RescalingMode bicubicRescaling, CameraBufferSettings.FXAA fxaa, bool keepAlhpa) {
        this.context = context;
        this.camera = camera;
        this.lighting = lighting;
        this.bufferSize = bufferSize;
        //this.settings = settings;
        //apply to proper camera
        this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
        this.useHDR = useHDR;
        this.colorLUTResolution = colorLUTResolution;
        this.finalBlendMode = finalBlendMode;
        this.rescalingMode = bicubicRescaling;
        this.fxaa = fxaa;
        this.keepAlpha = keepAlhpa;
    }

    public void Render(int sourceId) {
        //buffer.Blit(sourceId, BuiltinRenderTextureType.CameraTarget);
        //Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);

        if (DoBloom(sourceId)) {
            DoColorGradingAndToneMappingAndFxaa(bloomResultId);
            buffer.ReleaseTemporaryRT(bloomResultId);
        } else {
            #region Motion Blur
            if (settings.motionBlurSettings.enable) {
                //updating current result to source
                sourceId = motionResultId;
            }
            #endregion
            #region Light Shafts
            if (settings.LightShaftsSetting.enable) {
                //updating outline result to source
                sourceId = lightShaftsResultId;
            }
            #endregion
            #region Outline
            if (settings.OutlineSetting.enable) {
                //updating outline result to source
                sourceId = outlineResultId;
            }
            #endregion
            DoColorGradingAndToneMappingAndFxaa(sourceId);
        }

        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass) {
        buffer.SetGlobalTexture(fxSourceId, from);
        //SetRenderTarget will reset the viewport to cover the entire target
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)pass, MeshTopology.Triangles, 3);
    }

    //duplicate Draw methods and manully set viewport after seting render target
    void DrawFinal(RenderTargetIdentifier from, Pass pass) {
        buffer.SetGlobalTexture(fxSourceId, from);
        buffer.SetGlobalFloat(finalSrcBlendId, (float)finalBlendMode.source);
        buffer.SetGlobalFloat(finalDstBlendId, (float)finalBlendMode.destination);
        //SetRenderTarget will reset the viewport to cover the entire target
        buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, finalBlendMode.destination == BlendMode.Zero ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
        buffer.SetViewport(camera.pixelRect);
        buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)pass, MeshTopology.Triangles, 3);
    }

    public PostFXStack() {
        #region Bloom
        bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
        //NOTE : double pyramidlevel to reserve texture identifiers, cause we need an additional step in the middle of each pyramid
        for (int i = 1; i < maxBloomPyramidLevels * 2; i++) {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
        #endregion
    }

    bool DoBloom(int sourceId) {
        #region Motion Blur
        if (settings.motionBlurSettings.enable) {
            DoMotionBlur(sourceId);
            //updating current result to source
            sourceId = motionResultId;
        }
        #endregion
        #region Light Shafts
        if (settings.LightShaftsSetting.enable) {
            DoLightShafts(sourceId);
            //updating current result to source
            sourceId = lightShaftsResultId;
        }
        #endregion
        #region Outline
        if (settings.OutlineSetting.enable) {
            DoOutline(sourceId);
            //updating outline result to source
            sourceId = outlineResultId;
        }
        #endregion
        BloomSettings bloom = settings.Bloom;
        int width, height;
        if (bloom.ignoreRenderScale)
        {
            width = camera.pixelWidth / 2;
            height = camera.pixelHeight / 2;
        } else {
            width = bufferSize.x / 2;
            height = bufferSize.y / 2;
        }
        //multiply 2 because half resolution then
        if (bloom.maxIterations == 0 || height < bloom.downscaleLimit * 2 || width < bloom.downscaleLimit * 2 || bloom.intensity <= 0f) {
            return false;
        }
        buffer.BeginSample("Bloom");
        #region Threshold knee function part
        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y = -threshold.x + threshold.x * bloom.thresholdKnee;
        threshold.z = 2f * threshold.x * bloom.thresholdKnee;
        threshold.w = 0.25f / (threshold.y + threshold.x + 0.00001f);
        #endregion
        RenderTextureFormat format = useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        #region Half resolution and prefliter bloom
        buffer.GetTemporaryRT(bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format);
        //Draw(sourceId, bloomPrefilterId, Pass.Copy);
        Draw(sourceId, bloomPrefilterId, bloom.fadeFireflies ? Pass.BloomPrefilterFireflies : Pass.BloomPrefilter);
        width /= 2;
        height /= 2;
        #endregion
        int fromId = bloomPrefilterId;
        int toId = bloomPyramidId + 1;
        int i;
        for (i = 0; i < bloom.maxIterations; i++) {
            if (height < bloom.downscaleLimit || width < bloom.downscaleLimit) {
                break;
            }
            //ask for the middle of each pyramid RT
            int midId = toId - 1;
            buffer.GetTemporaryRT(midId, width, height, 0, FilterMode.Bilinear, format);
            //ask for temp RT
            buffer.GetTemporaryRT(toId, width, height, 0, FilterMode.Bilinear, format);
            //draw Horizontal from fromId to midId
            Draw(fromId, midId, Pass.BloomHorizontal);
            //draw Vertical from midId to toId
            Draw(midId, toId, Pass.BloomVertical);
            //again, next loop toId is the next fromId
            fromId = toId;
            //toId += 1;
            toId += 2;
            width /= 2;
            height /= 2;
        }
        //release bloom prefilter
        buffer.ReleaseTemporaryRT(bloomPrefilterId);
        //draw the last RT to camera, and release all others
        //Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
        #region Bloom combine mode
        Pass combinePass;
        Pass finalPass;
        if (bloom.mode == BloomSettings.Mode.Additive) {
            combinePass = Pass.BloomCombineAdditive;
            finalPass = Pass.BloomCombineAdditive;
            buffer.SetGlobalFloat(bloomIntensityId, 1f);
        } else {
            combinePass = Pass.BloomCombineScatter;
            #region just my preference
            if (bloom.mode == BloomSettings.Mode.SingleScatter) {
                finalPass = combinePass;
            } else {
                finalPass = Pass.BloomScatterFinal;
            }
            #endregion
            //set scatter intensity
            buffer.SetGlobalFloat(bloomIntensityId, bloom.scatter);
        }
        #endregion
        //Only work if there are at least 2 iterations
        if (i > 1) {
            buffer.ReleaseTemporaryRT(fromId - 1);
            //NOTE : DO NOT KNOW WHY MINUS 5
            //toId -= 5;
            //NOTE : GET IT NOW, MINUS 2 because the last iteration toId has been plus addition 2, then mimus 3 because we will turn into last iteration(H -> HV -> H`-> H`V`
            toId = toId - 2 - 3;

            //release all other RT
            for (i -= 1; i > 0; i--) {
                //release all claimed RT
                //buffer.ReleaseTemporaryRT(bloomPyramidId + i);
                buffer.SetGlobalTexture(fxSource2Id, toId + 1);
                Draw(fromId, toId, combinePass);
                //release current draw loop's fromId, toId
                buffer.ReleaseTemporaryRT(fromId);
                buffer.ReleaseTemporaryRT(toId + 1);
                fromId = toId;
                toId -= 2;
            }
        } else {
            buffer.ReleaseTemporaryRT(bloomPyramidId);
        }
        //draw final result
        buffer.SetGlobalTexture(fxSource2Id, sourceId);
        //set bicubic enable
        buffer.SetGlobalFloat(bloomBucibicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f);
        //set bloom threshold vector
        buffer.SetGlobalVector(bloomThresholdId, threshold);
        //set bloom intensity
        buffer.SetGlobalFloat(bloomIntensityId, bloom.intensity);
        //get full camera resolution bloom final result RT for ther later post fx
        buffer.GetTemporaryRT(bloomResultId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, format);
        //Draw(fromId, BuiltinRenderTextureType.CameraTarget, finalPass);
        Draw(fromId, bloomResultId, finalPass);
        buffer.ReleaseTemporaryRT(fromId);
        buffer.EndSample("Bloom");
        return true;
    }

    void ConfigureColorAdjustment() {
        ColorAdjustmentSettings colorAdjustment = settings.ColorAdjustment;
        buffer.SetGlobalVector(colorAdjustmentId,new Vector4(
            Mathf.Pow(2f, colorAdjustment.postExposure),
            colorAdjustment.constrat * 0.01f + 1f,
            colorAdjustment.hueShift * (1 / 360f),
            colorAdjustment.saturation * 0.01f + 1f));
        buffer.SetGlobalColor(colorFilterId, colorAdjustment.colorFilter.linear);
    }

    void ConfigureWhiteBalance() {
        WhiteBalanceSettings whiteBalance = settings.WhiteBalance;
        buffer.SetGlobalVector(whiteBalanceId, ColorUtils.ColorBalanceToLMSCoeffs(whiteBalance.Temperature, whiteBalance.Tint));
    }

    void ConfigureSplitToning() {
        SplitToningSettings splitToning = settings.SplitToning;
        Color splitColor = splitToning.shadows;
        splitColor.a = splitToning.balance * 0.01f;
        buffer.SetGlobalColor(splitToningShadowsId, splitColor);
        buffer.SetGlobalColor(splitToningHighlightsId, splitToning.highlights);
    }

    void ConfigureChannelMixer() {
        ChannelMixerSettings channelMixer = settings.ChannelMixer;
        buffer.SetGlobalVector(channelMixerRedId, channelMixer.red);
        buffer.SetGlobalVector(channelMixerGreenId, channelMixer.green);
        buffer.SetGlobalVector(channelMixerBlueId, channelMixer.blue);
    }

    void ConfigureShadowsMidtonesHightlights() {
        ShadowMidtonesHighlightsSettings smh = settings.ShadowMidtonesHighlights;
        buffer.SetGlobalColor(smhShadowsId, smh.shadows);
        buffer.SetGlobalVector(smhMidtonesId, smh.midtones);
        buffer.SetGlobalVector(smhHighlightsId, smh.highlights);
        buffer.SetGlobalVector(smhRangeId, new Vector4(smh.shadowsStart, smh.shadowsEnd, smh.highlightsStart, smh.highlightsEnd));
    }

    void ConfigureColorGradePosterize() {
        Posterize posterize = settings.posterizes;
        buffer.SetGlobalFloat(colorRampId, posterize.colorRamp);
        buffer.SetGlobalFloat(rampGammaId, posterize.rampGamma);
    }

    void ConfigureFXAA() {
        if (fxaa.quality == CameraBufferSettings.FXAA.Quality.Low) {
            buffer.EnableShaderKeyword(fxaaQualityLowKeyword);
            buffer.DisableShaderKeyword(fxaaQualityMediumKeyword);
        } else if (fxaa.quality == CameraBufferSettings.FXAA.Quality.Medium) {
            buffer.DisableShaderKeyword(fxaaQualityLowKeyword);
            buffer.EnableShaderKeyword(fxaaQualityMediumKeyword);
        } else {
            buffer.DisableShaderKeyword(fxaaQualityLowKeyword);
            buffer.DisableShaderKeyword(fxaaQualityMediumKeyword);
        }
        buffer.SetGlobalVector(fxaaConfigId, new Vector4(fxaa.fixedThreshold, fxaa.relativeThreshold, fxaa.subpixelBlending));
    }

    void DoColorGradingAndToneMappingAndFxaa(int sourceId) {
        buffer.BeginSample("Color Grading");
        ConfigureColorAdjustment();
        ConfigureWhiteBalance(); 
        ConfigureSplitToning();
        ConfigureChannelMixer();
        ConfigureShadowsMidtonesHightlights();
        ConfigureColorGradePosterize();
        #region LUT
        // lut is 3D, but we render it to a 2D texture, so the width is square the height 
        int lutHeight = colorLUTResolution;
        int lutWidth = lutHeight * lutHeight;
        buffer.GetTemporaryRT(colorGradingLUTId, lutWidth, lutHeight, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
        buffer.SetGlobalVector(colorGradingLUTParamsId, new Vector4(lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight / (lutHeight - 1f)));
        #endregion
        ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
        Pass pass = Pass.ColorGradingNone + (int)mode;
        #region LUT
        buffer.SetGlobalFloat(colorGradingLUTInLogId, useHDR && pass != Pass.ColorGradingNone ? 1f : 0f);
        //draw final color in lut
        Draw(sourceId, colorGradingLUTId, pass);
        #endregion
        //We can not change final frame texture format(LDR
        #region LUT
        buffer.SetGlobalVector(colorGradingLUTParamsId, new Vector4(1f / lutWidth, 1f / lutHeight, lutHeight - 1f));
        #endregion
        #region FXAA
        buffer.SetGlobalFloat(finalSrcBlendId, 1f);
        buffer.SetGlobalFloat(finalDstBlendId, 0f);
        if (fxaa.enabled) {
            ConfigureFXAA();
            //store the color grading result immediately in a new LDR temporary texture
            buffer.GetTemporaryRT(colorGradingResultId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
            Draw(sourceId, colorGradingResultId, keepAlpha ? Pass.FinalColorGrading : Pass.ColorGradingWithLuma);
        }
        #endregion
        //Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.FinalColorGrading);
        if (bufferSize.x == camera.pixelWidth) {
            #region FXAA
            if (fxaa.enabled) {
                DrawFinal(colorGradingResultId, keepAlpha ? Pass.FXAA : Pass.FXAAWithLuma);
                buffer.ReleaseTemporaryRT(colorGradingResultId);
            }
            #endregion
            //no render scale, no FXAA，draw final
            else {
                DrawFinal(sourceId, Pass.FinalColorGrading);
            }
        } else {
            buffer.GetTemporaryRT(finalResultId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
            #region FXAA
            if (fxaa.enabled) {
                Draw(colorGradingResultId, finalResultId, keepAlpha ? Pass.FXAA : Pass.FXAAWithLuma);
            }
            #endregion
            else {
                //get a original buffer size intermediate RT
                Draw(sourceId, finalResultId, Pass.FinalColorGrading);
            }
            bool pointSampling = rescalingMode == CameraBufferSettings.RescalingMode.Point;
            bool bicubicSampling = rescalingMode == CameraBufferSettings.RescalingMode.Bicubic;
            buffer.SetGlobalFloat(copyPointId, pointSampling ? 1f : 0f);
            buffer.SetGlobalFloat(copyBicubicId, bicubicSampling ? 1f : 0f);
            DrawFinal(finalResultId, Pass.Rescale);
            buffer.ReleaseTemporaryRT(finalResultId);
        }
        buffer.ReleaseTemporaryRT(colorGradingLUTId);
        buffer.EndSample("Color Grading");
    }

    void DoOutline(int from) {
        buffer.BeginSample("Outline");
        Matrix4x4 clipToView = camera.projectionMatrix.inverse;
        buffer.SetGlobalMatrix("_ClipToViewMatrix", clipToView);
        buffer.SetGlobalColor(OutlineColor, settings.OutlineSetting.color);
        buffer.SetGlobalVector(OutlineParams, new Vector4(settings.OutlineSetting.outlineScale, 0, 0, 0));
        buffer.SetGlobalVector(ThresholdParams, new Vector4(settings.OutlineSetting.depthThreshold, settings.OutlineSetting.normalThreshold, settings.OutlineSetting.depthNormalThreshold, settings.OutlineSetting.ColorThreshold));
        buffer.SetGlobalFloat(ThresholdScale, settings.OutlineSetting.depthNormalThresholdScale);
        buffer.GetTemporaryRT(outlineResultId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
        Draw(from, outlineResultId, Pass.Outline);
        buffer.EndSample("Outline");
    }

    void DoLightShafts(int from) {
        buffer.BeginSample("Light Shafts");
        buffer.GetTemporaryRT(lightShafts0Id, bufferSize.x / settings.LightShaftsSetting.Downsample, bufferSize.y / settings.LightShaftsSetting.Downsample, 0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
        buffer.GetTemporaryRT(lightShafts1Id, bufferSize.x / settings.LightShaftsSetting.Downsample, bufferSize.y / settings.LightShaftsSetting.Downsample, 0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
        Vector3 lightDir = lighting.MainLightPosition;
        Vector4 lightScreenPos = camera.transform.position + lightDir * camera.farClipPlane;
        lightScreenPos = camera.WorldToViewportPoint(lightScreenPos);
        buffer.SetGlobalVector(lightSource, new Vector4(lightScreenPos.x, lightScreenPos.y, lightScreenPos.z, 0));
        buffer.SetGlobalVector(lightShaftParameters, settings.LightShaftsSetting.lightShaftParameters);
        buffer.SetGlobalVector(radialBlurParameters, settings.LightShaftsSetting.radialBlurParameters);
        buffer.SetGlobalFloat(lightShaftsDensity, settings.LightShaftsSetting.density);
        buffer.SetGlobalFloat(lightShaftsWeight, settings.LightShaftsSetting.weight);
        buffer.SetGlobalFloat(lightShaftsDecay, settings.LightShaftsSetting.decay);
        buffer.SetGlobalFloat(lightShaftsExposure, settings.LightShaftsSetting.exposure);
        buffer.SetGlobalVector(bloomTintAndThreshold, settings.LightShaftsSetting.bloomTintAndThreshold);
        //set the from rt as the scene color, do prefilter according to the settings
        buffer.SetGlobalTexture("_SceneColor", from);
        buffer.SetRenderTarget(lightShafts0Id, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        if (settings.LightShaftsSetting.mode == LightShaftsSettings.Mode.Occlusion) {
            buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)Pass.LightShaftsOcclusionPrefilter, MeshTopology.Triangles, 3);
        } else {
            buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)Pass.LightShaftsBloomPrefilter, MeshTopology.Triangles, 3);
        }
        //do radial blur for 3 times
        int tempId = lightShafts0Id;
        for (int i = 0; i < 3; i++) {
            buffer.SetGlobalTexture("_LightShafts0", lightShafts0Id);
            buffer.SetRenderTarget(lightShafts1Id, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)Pass.LightShaftsBlur, MeshTopology.Triangles, 3);
            lightShafts0Id = lightShafts1Id;
            lightShafts1Id = tempId;
            tempId = lightShafts0Id;
        }
        //blend with scene color according to the settings
        buffer.GetTemporaryRT(lightShaftsResultId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
        buffer.SetGlobalTexture("_LightShafts1", lightShafts1Id);
        buffer.SetRenderTarget(lightShaftsResultId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        if (settings.LightShaftsSetting.mode == LightShaftsSettings.Mode.Occlusion) {
            buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)Pass.LightShaftsOcclusionBlend, MeshTopology.Triangles, 3);
        } else {
            buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)Pass.LightShaftsBloomBlend, MeshTopology.Triangles, 3);
        }
        buffer.EndSample("Light Shafts");
    }

    void DoMotionBlur(int from) {
        if (reconstructionFilter == null) {
            reconstructionFilter = new ReconstructionFilter(buffer);
        }
        if (frameBlendingFilter == null) {
            frameBlendingFilter = new FrameBlendingFilter(buffer);
        }
        buffer.BeginSample("Motion Blur");
        RenderTextureFormat format = useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        buffer.GetTemporaryRT(motionResultId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, format);
        if (settings.motionBlurSettings.shutterAngle > 0 && settings.motionBlurSettings.frameBlending > 0) {
            //reconstruction and frame blending
            var tempId = Shader.PropertyToID("tempBuffer");
            buffer.GetTemporaryRT(tempId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, format);
            reconstructionFilter.ProcessImage(settings.motionBlurSettings.shutterAngle, settings.motionBlurSettings.sampleCount, from, tempId, bufferSize.x, bufferSize.y);
            frameBlendingFilter.BlendFrames(settings.motionBlurSettings.frameBlending, tempId, motionResultId);
            frameBlendingFilter.PushFrame(tempId, bufferSize.x, bufferSize.y);
            buffer.ReleaseTemporaryRT(tempId);
        } else if (settings.motionBlurSettings.shutterAngle > 0) {
            //reconstruction only
            reconstructionFilter.ProcessImage(settings.motionBlurSettings.shutterAngle, settings.motionBlurSettings.sampleCount, from, motionResultId, bufferSize.x, bufferSize.y);
        } else if (settings.motionBlurSettings.frameBlending > 0) {
            //frame blending only
            frameBlendingFilter.BlendFrames(settings.motionBlurSettings.frameBlending, from, motionResultId);
            frameBlendingFilter.PushFrame(from, bufferSize.x, bufferSize.y);
        } else {
            //nothing to do!
        }
        buffer.EndSample("Motion Blur");
    }
}
