﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class MelodyRenderPipeline : RenderPipeline {
    CameraRender renderer;
    bool useDynamicBatching;
    bool useInstancing;
    bool useLightsPerObject;
    ShadowSettings shadowSettings;
    AtmosphereScatteringSettings atmosphereSettings;
    VolumetricCloudSettings cloudSettings;
    VolumetricLightSettings fogSettings;
    PostFXSettings postFXSettings;
    CameraBufferSettings cameraBufferSettings;
    int colorLUTResolution;

    public MelodyRenderPipeline(bool useDynamicBatching, bool useInstancing, bool useSRPBatcher, bool useLightsPerObject, ShadowSettings shadowSettings, AtmosphereScatteringSettings atmosphereSettings, VolumetricCloudSettings cloudSettings, VolumetricLightSettings fogSettings, PostFXSettings postFXSettings, CameraBufferSettings cameraBufferSettings, int colorLUTResolution, Shader cameraRendererShader) {
        this.useDynamicBatching = useDynamicBatching;
        this.useInstancing = useInstancing;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        this.useLightsPerObject = useLightsPerObject;
        this.shadowSettings = shadowSettings;
        this.atmosphereSettings = atmosphereSettings;
        this.cloudSettings = cloudSettings;
        this.fogSettings = fogSettings;
        GraphicsSettings.lightsUseLinearIntensity = true;
        this.postFXSettings = postFXSettings;
        this.cameraBufferSettings = cameraBufferSettings;
        this.colorLUTResolution = colorLUTResolution;
        renderer = new CameraRender(cameraRendererShader);

        InitializeForEditor();
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
        foreach (Camera camera in cameras) {
            BeginCameraRendering(context, camera);
            renderer.Render(context, camera, useDynamicBatching, useInstancing, useLightsPerObject, shadowSettings, atmosphereSettings, cloudSettings, fogSettings, postFXSettings, cameraBufferSettings, colorLUTResolution);
            EndCameraRendering(context, camera);
        }
    }
}
