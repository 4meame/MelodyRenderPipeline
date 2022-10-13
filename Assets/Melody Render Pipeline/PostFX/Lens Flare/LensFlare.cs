using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static PostFXSettings;

public class LensFlare {
    const string bufferName = "Lens Flare";
    CommandBuffer buffer = new CommandBuffer { name = bufferName };
    ScriptableRenderContext context;
    Camera camera;
    LensFlareSettings settings;
    Vector2Int bufferSize;

    int flareOcclusionTex = Shader.PropertyToID("_FlareOcclusionTex");
    int lensFlareOcclusion = Shader.PropertyToID("_LensFlareOcclusion");
    int flareTex = Shader.PropertyToID("_FlareTex");
    int flareColorValue = Shader.PropertyToID("_FlareColorValue");
    int flareData0 = Shader.PropertyToID("_FlareData0");
    int flareData1 = Shader.PropertyToID("_FlareData1");
    int flareData2 = Shader.PropertyToID("_FlareData2");
    int flareData3 = Shader.PropertyToID("_FlareData3");
    int flareData4 = Shader.PropertyToID("_FlareData4");
    int flareOcclusionIndex = Shader.PropertyToID("_FlareOcclusionIndex");

    public void Setup(ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, PostFXSettings settings) {
        this.context = context;
        this.camera = camera;
        this.bufferSize = bufferSize;
        //apply to proper camera
        this.settings = camera.cameraType <= CameraType.SceneView ? (settings ? settings.lensFlareSettings : default) : default;
    }

    public void DoLensFlare(int sourceId) {
        if(settings.mode == LensFlareSettings.Mode.None) {
            return;
        }
        if (settings.mode != LensFlareSettings.Mode.None && !LensFlareCommon.Instance.IsEmpty()) {
            LensFlareParameters parameters = PrepareLensFlareParameters();
            float width = LensFlareCommon.maxLensFlareWithOcclusion;
            float height = LensFlareCommon.maxLensFlareWithOcclusionTemporalSample;
            bool forceCameraOrigin = true;
            Vector3 cameraPosWS = camera.transform.position;
            //from hdrp
            var proj = camera.projectionMatrix;
            var view = camera.worldToCameraMatrix;
            var gpuNonJitteredProj = GL.GetGPUProjectionMatrix(proj, true);
            Matrix4x4 viewProjMatrix = gpuNonJitteredProj * view;
            bool taaEnable = settings.antiAliasing;
            LensFlareCommon.ComputeOcclusion(parameters.lensFlareMaterial, parameters.lensFlares, camera, width, height, forceCameraOrigin, cameraPosWS, viewProjMatrix, buffer, taaEnable, flareOcclusionTex, flareOcclusionIndex, flareTex, flareColorValue, flareData0, flareData1, flareData2, flareData3, flareData4);
            if (taaEnable) {
                buffer.SetComputeTextureParam(parameters.lensFlareMergeOcclusion, parameters.mergeOcclusionKernel, lensFlareOcclusion, LensFlareCommon.occlusionRT);
                buffer.DispatchCompute(parameters.lensFlareMergeOcclusion, parameters.mergeOcclusionKernel, 16, 1, 1);
            }
            width = bufferSize.x;
            height = bufferSize.y;
            LensFlareCommon.DoLensFlareCommon(parameters.lensFlareMaterial, parameters.lensFlares, camera, width, height, forceCameraOrigin, cameraPosWS, viewProjMatrix, buffer, sourceId, (a, b, c) => { return GetLensFlareLightAttenuation(a, b, c); }, taaEnable, flareOcclusionTex, flareOcclusionIndex, flareTex, flareColorValue, flareData0, flareData1, flareData2, flareData3, flareData4);
            ExecuteBuffer();
        }
    }

    struct LensFlareParameters {
        public Material lensFlareMaterial;
        public ComputeShader lensFlareMergeOcclusion;
        public int mergeOcclusionKernel;
        public LensFlareCommon lensFlares;
    }

    LensFlareParameters PrepareLensFlareParameters() {
        LensFlareParameters parameters;
        parameters.lensFlares = LensFlareCommon.Instance;
        parameters.lensFlareMaterial = CoreUtils.CreateEngineMaterial(settings.lensFlareShader);
        parameters.lensFlareMergeOcclusion = settings.mergeOcclusion;
        parameters.mergeOcclusionKernel = settings.mergeOcclusion.FindKernel("MergeOcclusion");
        return parameters;
    }

    float GetLensFlareLightAttenuation(Light light, Camera camera, Vector3 eyeToLight) {
        if (light.TryGetComponent<Light>(out var lightData)) {
            switch (lightData.type) {
                case LightType.Spot:
                    return LensFlareCommon.ShapeAttenuationSpotConeLight(lightData.transform.forward, eyeToLight, light.spotAngle, lightData.innerSpotAngle / 100f);
                case LightType.Directional:
                    return LensFlareCommon.ShapeAttenuationDirectionLight(lightData.transform.forward, eyeToLight);
                case LightType.Point:
                    return LensFlareCommon.ShapeAttenuationPointLight();
                case LightType.Area:
                    break;
            }
        }
        return 1.0f;
    }

    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}
