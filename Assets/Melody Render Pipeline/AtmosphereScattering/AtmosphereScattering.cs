using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static AtmosphereScatteringSettings;

public class AtmosphereScattering {
    enum Pass {
        PrecomputeDensity,
    }
    const string bufferName = "AtmosphereScattering";
    CommandBuffer buffer = new CommandBuffer {
        name = bufferName
    };
    ScriptableRenderContext context;
    Camera camera;
    public AtmosphereScatteringSettings settings;

    Material material;
    RenderTexture particleDensityLUT;

    int particleDensityLUTId = Shader.PropertyToID("_ParticleDensityLUT");
    int planetRadiusId = Shader.PropertyToID("_PlanetRadius");
    int atmosphereHeightId = Shader.PropertyToID("_AtmosphereHeight");
    int densityScaleHeightId = Shader.PropertyToID("_DensityScaleHeight");
    int sunIntensityId = Shader.PropertyToID("_SunIntensity");
    int incomingLightId = Shader.PropertyToID("_IncomingLight");
    int extinctionRId = Shader.PropertyToID("_ExtinctionR");
    int extinctionMId = Shader.PropertyToID("_ExtinctionM");
    int scatteringRId = Shader.PropertyToID("_ScatteringR");
    int scatteringMId = Shader.PropertyToID("_ScatteringM");
    int mieGId = Shader.PropertyToID("_MieG");

    public void Setup(ScriptableRenderContext context, Camera camera, AtmosphereScatteringSettings settings) {
        this.context = context;
        this.camera = camera;
        this.settings = settings;

        if (material == null) {
            var shader = Shader.Find("Hidden/Melody RP/AtmosphereScattering");
            material = new Material(shader);
        }
    }

    public void Precompute() {
        if (particleDensityLUT == null) {
            particleDensityLUT = new RenderTexture(settings.particleDensityLUTSize, settings.particleDensityLUTSize, 0, RenderTextureFormat.RGFloat, RenderTextureReadWrite.Linear);
            particleDensityLUT.name = "Particle Density LUT";
            particleDensityLUT.filterMode = FilterMode.Bilinear;
            particleDensityLUT.Create();
        }
        buffer.SetGlobalFloat(planetRadiusId, settings.planetRadius);
        buffer.SetGlobalFloat(atmosphereHeightId, settings.atmosphereHeight);
        buffer.SetGlobalVector(densityScaleHeightId, settings.densityScaleHeight);
        buffer.SetGlobalVector(incomingLightId, settings.incomingLight);
        buffer.SetGlobalFloat(sunIntensityId, 0.3f);
        buffer.SetGlobalVector(extinctionRId, settings.rayleighCoefficients * 0.000001f * settings.rayleighExtinctionScale);
        buffer.SetGlobalVector(extinctionMId, settings.mieCoefficients * 0.000001f * settings.mieExtinctionScale);
        buffer.SetGlobalVector(scatteringRId, settings.rayleighCoefficients * 0.000001f * settings.rayleighInscatterScale);
        buffer.SetGlobalVector(scatteringMId, settings.mieCoefficients * 0.000001f * settings.mieInscatterScale);
        buffer.SetGlobalFloat(mieGId, settings.mieG);
        buffer.SetRenderTarget(particleDensityLUT);
        buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.PrecomputeDensity, MeshTopology.Triangles, 3);
        buffer.SetGlobalTexture(particleDensityLUTId, particleDensityLUT);
        ExecuteBuffer();
    }

    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}
