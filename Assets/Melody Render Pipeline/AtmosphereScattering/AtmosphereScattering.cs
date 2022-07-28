using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static AtmosphereScatteringSettings;

public class AtmosphereScattering {
    enum Pass {
        PrecomputeDensity,
        PrecomputeSunColor
    }
    const string bufferName = "AtmosphereScattering";
    CommandBuffer buffer = new CommandBuffer {
        name = bufferName
    };
    ScriptableRenderContext context;
    Camera camera;
    bool useHDR;
    public AtmosphereScatteringSettings settings;

    Material material;
    Light sun;
    RenderTexture particleDensityLUT;
    RenderTexture sunColorLUT;
    Texture2D sunColorTexture;

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

    public void Setup(ScriptableRenderContext context, Camera camera, bool useHDR, AtmosphereScatteringSettings settings) {
        this.context = context;
        this.camera = camera;
        this.useHDR = useHDR;
        this.settings = settings;

        if (material == null) {
            var shader = Shader.Find("Hidden/Melody RP/AtmosphereScattering");
            material = new Material(shader);
        }
        if (sun == null) {
            sun = RenderSettings.sun;
        }
    }

    void UpdateMaterialParameters() {
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
    } 

    public void PrecomputeAll() {
        UpdateMaterialParameters();
        PrecomputeParticleDensity();
        PrecomputeSunColor();
        ExecuteBuffer();
    }

    public void CleanUp() {
        
    }

    void PrecomputeParticleDensity() {
        if (particleDensityLUT == null)
        {
            particleDensityLUT = new RenderTexture(settings.particleDensityLUTSize, settings.particleDensityLUTSize, 0, RenderTextureFormat.RGFloat, RenderTextureReadWrite.Linear);
            particleDensityLUT.name = "Particle Density LUT";
            particleDensityLUT.filterMode = FilterMode.Bilinear;
            particleDensityLUT.Create();
        }
        buffer.SetRenderTarget(particleDensityLUT);
        buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.PrecomputeDensity, MeshTopology.Triangles, 3);
        buffer.SetGlobalTexture(particleDensityLUTId, particleDensityLUT);
    }

    void PrecomputeSunColor() {
        if (sunColorLUT == null) {
            sunColorLUT = new RenderTexture(settings.sunColorLUTSize, settings.sunColorLUTSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            sunColorLUT.name = "Sun Color LUT";
            sunColorLUT.filterMode = FilterMode.Bilinear;
            sunColorLUT.Create();
        }
        buffer.SetRenderTarget(sunColorLUT);
        buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.PrecomputeSunColor, MeshTopology.Triangles, 3);
    }

    public void UpdateSunColor() {
        if (sunColorTexture == null) {
            sunColorTexture = new Texture2D(settings.sunColorLUTSize, settings.sunColorLUTSize, TextureFormat.RGBAHalf, false, true);
            sunColorTexture.name = "Sun Color Texture";
            sunColorTexture.Apply();
        }
        ReadRTpixelsBackToCPU(sunColorLUT, sunColorTexture);
        float cosAngle = Vector3.Dot(Vector3.up, -sun.transform.forward);
        float cosAngle01 = cosAngle * 0.5f + 0.5f;
        float height01 = settings.groundHeight / settings.atmosphereHeight;
        Color color = sunColorTexture.GetPixel((int)(cosAngle01 * sunColorTexture.width), (int)(height01 * sunColorTexture.height)).gamma;
        Vector3 c = new Vector3(color.r, color.g, color.b);
        float length = c.magnitude;
        c /= length;
        sun.color = new Color(Mathf.Max(c.x, 0.01f), Mathf.Max(c.y, 0.01f), Mathf.Max(c.z, 0.01f), 1);
        sun.intensity = Mathf.Max(length, 0.01f);
    }

    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void ReadRTpixelsBackToCPU(RenderTexture src, Texture2D dst) {
        RenderTexture currentActiveRT = RenderTexture.active;
        RenderTexture.active = src;
        dst.ReadPixels(new Rect(0, 0, dst.width, dst.height), 0, 0);
        RenderTexture.active = currentActiveRT;
    }
}
