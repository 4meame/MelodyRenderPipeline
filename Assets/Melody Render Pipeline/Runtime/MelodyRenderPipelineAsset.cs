using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Melody Render Pipeline")]
public class MelodyRenderPipelineAsset : RenderPipelineAsset {
    [SerializeField]
    bool useDynamicBachting = true, useInstancing = true, useSRPBatcher = true, useLightsPerObject = false;
    [SerializeField]
    ShadowSettings shadows = default;
    [SerializeField]
    AtmosphereScatteringSettings atmosphere = default;
    [SerializeField]
    VolumetricCloudSettings cloud = default;
    [SerializeField]
    PostFXSettings postFXSettings = default;
    [SerializeField]
    CameraBufferSettings cameraBufferSettings = new CameraBufferSettings { allowHDR = true, renderScale = 1.0f, renderingPath = CameraBufferSettings.RenderingPath.Forward,
        useDepthNormal = false, useDiffuse = false, useSpecular = false,
        fxaa = new CameraBufferSettings.FXAA { fixedThreshold = 0.0833f, relativeThreshold = 0.166f, subpixelBlending = 0.75f },
        ssr = new CameraBufferSettings.SSR { sSRType = CameraBufferSettings.SSR.SSRType.SSR, downSample = 1 },
        ssao = new CameraBufferSettings.SSAO { aOType = CameraBufferSettings.SSAO.AOType.GTAO, downSample = 1 }
    };
    public enum ColorLUTResolution { _16 = 16, _32 = 32, _64 = 64 }
    [SerializeField]
    ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;
    [SerializeField]
    Shader cameraRendererShader = default;
    //TODO: use global pipeline resources SO to collect all shaders,textures,material and so on
    //MelodyRenderPipelineResources pipelineResources;
    protected override RenderPipeline CreatePipeline() {
        return new MelodyRenderPipeline(useDynamicBachting, useInstancing, useSRPBatcher, useLightsPerObject, shadows, atmosphere, cloud, postFXSettings, cameraBufferSettings, (int)colorLUTResolution, cameraRendererShader);
    }
}
