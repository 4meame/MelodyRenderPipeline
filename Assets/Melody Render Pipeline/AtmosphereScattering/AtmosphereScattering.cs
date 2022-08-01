using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static AtmosphereScatteringSettings;

public class AtmosphereScattering {
    enum Pass {
        PrecomputeDensity,
        PrecomputeSunColor,
        PrecomputeAmbient
    }
    const string bufferName = "AtmosphereScattering";
    CommandBuffer buffer = new CommandBuffer {
        name = bufferName
    };
    ScriptableRenderContext context;
    Camera camera;
    bool useHDR;
    AtmosphereScatteringSettings settings;

    ComputeShader cs;
    Material material;
    Light sun;
    RenderTexture particleDensityLUT;
    RenderTexture sunColorLUT;
    RenderTexture ambientLUT;
    RenderTexture scatterRaylieLUT;
    RenderTexture scatterMieLUT;
    RenderTexture inscatteringLUT;
    RenderTexture extinctionLUT;
    Texture2D sunColorTexture;
    Texture2D abmeintTexture;
    Texture2D randomVectorsLUT;
    Vector4[] frustumCorners = new Vector4[4];

    int particleDensityLUTId = Shader.PropertyToID("_ParticleDensityLUT");
    int scatterRaylieLUTId = Shader.PropertyToID("_ScatterRaylieLUT");
    int scatterMieLUTId = Shader.PropertyToID("_ScatterMieLUT");
    int inscatteringLUTId = Shader.PropertyToID("_InscatteringLUT");
    int extinctionLUTId = Shader.PropertyToID("_ExtinctionLUT");
    int planetRadiusId = Shader.PropertyToID("_PlanetRadius");
    int atmosphereHeightId = Shader.PropertyToID("_AtmosphereHeight");
    int densityScaleHeightId = Shader.PropertyToID("_DensityScaleHeight");
    int lightSamplesId = Shader.PropertyToID("_LightSamples");
    int distanceScaleId = Shader.PropertyToID("_DistanceScale");
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

        if (cs == null) {
            cs = settings.computeShader;
        }
        if (material == null) {
            var shader = Shader.Find("Hidden/Melody RP/AtmosphereScattering");
            material = new Material(shader);
        }
        if (sun == null) {
            sun = RenderSettings.sun;
        }
        //get four corners of camera furstum in world space
        frustumCorners[0] = camera.ViewportToWorldPoint(new Vector3(0, 0, camera.farClipPlane));
        frustumCorners[1] = camera.ViewportToWorldPoint(new Vector3(0, 1, camera.farClipPlane));
        frustumCorners[2] = camera.ViewportToWorldPoint(new Vector3(1, 1, camera.farClipPlane));
        frustumCorners[3] = camera.ViewportToWorldPoint(new Vector3(1, 0, camera.farClipPlane));

        if (settings.mode == Mode.Precompute) {
            buffer.EnableShaderKeyword("_ATMOSPHERE_PRECOMPUTE");
        }
        else{
            buffer.DisableShaderKeyword("_ATMOSPHERE_PRECOMPUTE");
        }
    }

    void UpdateShaderParameters() {
        buffer.SetGlobalFloat(planetRadiusId, settings.planetRadius);
        buffer.SetGlobalFloat(atmosphereHeightId, settings.atmosphereHeight);
        buffer.SetGlobalFloat(lightSamplesId, settings.lightSamples);
        buffer.SetGlobalFloat(distanceScaleId, settings.distanceScale);
        buffer.SetGlobalVector(densityScaleHeightId, settings.densityScaleHeight);
        buffer.SetGlobalVector(incomingLightId, settings.incomingLight);
        buffer.SetGlobalFloat(sunIntensityId, settings.sunIntensity);
        buffer.SetGlobalVector(extinctionRId, settings.rayleighCoefficients * 0.000001f * settings.rayleighExtinctionScale);
        buffer.SetGlobalVector(extinctionMId, settings.mieCoefficients * 0.000001f * settings.mieExtinctionScale);
        buffer.SetGlobalVector(scatteringRId, settings.rayleighCoefficients * 0.000001f * settings.rayleighInscatterScale);
        buffer.SetGlobalVector(scatteringMId, settings.mieCoefficients * 0.000001f * settings.mieInscatterScale);
        buffer.SetGlobalFloat(mieGId, settings.mieG);
        buffer.SetGlobalVector("_BottomLeftCorner", frustumCorners[0]);
        buffer.SetGlobalVector("_TopLeftCorner", frustumCorners[1]);
        buffer.SetGlobalVector("_TopRightCorner", frustumCorners[2]);
        buffer.SetGlobalVector("_BottomRightCorner", frustumCorners[3]);
    }

    public void PrecomputeAll() {
        if (settings.updateEveryFrame) {
            UpdateShaderParameters();
            PrecomputeParticleDensity();
            PrecomputeAtmosphereSky();
            PrecomputeSunColor();
            InitRandomVectors();
            PrecomputeAmbient();
            ExecuteBuffer();
        }
    }

    public void UpdateAll() {
        if (settings.updateEveryFrame) {
            UpdateSunColor();
            UpdateAmbient();
        }
    }

    public void CleanUp() {
        
    }

    void InitRandomVectors() {
        if (randomVectorsLUT == null) {
            randomVectorsLUT = new Texture2D(256, 1, TextureFormat.RGBAHalf, false, true);
            randomVectorsLUT.name = "Random Vectors LUT";
            Color[] colors = new Color[256];
            for (int i = 0; i < colors.Length; i++) {
                Vector3 vector = Random.onUnitSphere;
                colors[i] = new Color(vector.x, vector.y, vector.z, 1);
            }
            randomVectorsLUT.SetPixels(colors);
            randomVectorsLUT.Apply();
        }
    }

    void PrecomputeParticleDensity() {
        if (particleDensityLUT == null) {
            particleDensityLUT = new RenderTexture(settings.particleDensityLUTSize, settings.particleDensityLUTSize, 0, RenderTextureFormat.RGFloat, RenderTextureReadWrite.Linear);
            particleDensityLUT.name = "Particle Density LUT";
            particleDensityLUT.filterMode = FilterMode.Bilinear;
            //computer sahder need RT with enableRandomWrite
            particleDensityLUT.enableRandomWrite = true;
            particleDensityLUT.Create();
        }
        if (settings.mode == Mode.Common) {
            buffer.SetRenderTarget(particleDensityLUT);
            buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.PrecomputeDensity, MeshTopology.Triangles, 3);
            buffer.SetGlobalTexture(particleDensityLUTId, particleDensityLUT);
        }
        else if(settings.mode == Mode.Precompute) {
            int index = cs.FindKernel("ParticleDensityLUT");
            buffer.SetComputeTextureParam(cs, index, "_CSParticleDensityLUT", particleDensityLUT);
            buffer.SetRenderTarget(particleDensityLUT);
            buffer.DispatchCompute(cs, index, particleDensityLUT.width / 8, particleDensityLUT.height / 8, 1);
            buffer.SetGlobalTexture(particleDensityLUTId, particleDensityLUT);
        }
    }

    void PrecomputeSunColor() {
        if (sunColorLUT == null) {
            sunColorLUT = new RenderTexture(settings.sunColorLUTSize, settings.sunColorLUTSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            sunColorLUT.name = "Sun Color LUT";
            sunColorLUT.filterMode = FilterMode.Bilinear;
            //computer sahder need RT with enableRandomWrite
            sunColorLUT.enableRandomWrite = true;
            sunColorLUT.Create();
        }
        if (settings.mode == Mode.Common) {
            buffer.SetRenderTarget(sunColorLUT);
            buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.PrecomputeSunColor, MeshTopology.Triangles, 3);
        }
        else if (settings.mode == Mode.Precompute) {
            int index = cs.FindKernel("SunColorLUT");
            buffer.SetComputeTextureParam(cs, index, "_SunColorLUT", sunColorLUT);
            buffer.SetRenderTarget(sunColorLUT);
            buffer.DispatchCompute(cs, index, sunColorLUT.width / 8, sunColorLUT.height / 8, 1);
        }
    }

    void PrecomputeAmbient() {
        if (ambientLUT == null) {
            ambientLUT = new RenderTexture(settings.ambientLUTSize, 1, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            ambientLUT.name = "Ambient LUT";
            ambientLUT.filterMode = FilterMode.Bilinear;
            //computer sahder need RT with enableRandomWrite
            ambientLUT.enableRandomWrite = true;
            ambientLUT.Create();
        }
        if (settings.mode == Mode.Common) {
            buffer.SetGlobalTexture("_RandomVectors", randomVectorsLUT);
            buffer.SetRenderTarget(ambientLUT);
            buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.PrecomputeAmbient, MeshTopology.Triangles, 3);
        }
        else if(settings.mode == Mode.Precompute) {
            buffer.SetGlobalTexture("_RandomVectors", randomVectorsLUT);
            int index = cs.FindKernel("AmbientLUT");
            buffer.SetComputeTextureParam(cs, index, "_AmbientLUT", ambientLUT);
            buffer.SetRenderTarget(ambientLUT);
            buffer.DispatchCompute(cs, index, ambientLUT.width / 64, 1, 1);
        }
    }

    void PrecomputeAtmosphereSky() {
        if (settings.mode == Mode.Precompute) {
            if (scatterRaylieLUT == null) {
                scatterRaylieLUT = new RenderTexture((int)settings.atmosphereScatterLUTSize.x, (int)settings.atmosphereScatterLUTSize.y, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                scatterRaylieLUT.volumeDepth = (int)settings.atmosphereScatterLUTSize.z;
                scatterRaylieLUT.name = "Scatter Raylie LUT";
                //do not miss texture dimension
                scatterRaylieLUT.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
                //computer sahder need RT with enableRandomWrite
                scatterRaylieLUT.enableRandomWrite = true;
                scatterRaylieLUT.Create();
            }
            if (scatterMieLUT == null) {
                scatterMieLUT = new RenderTexture((int)settings.atmosphereScatterLUTSize.x, (int)settings.atmosphereScatterLUTSize.y, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                scatterMieLUT.volumeDepth = (int)settings.atmosphereScatterLUTSize.z;
                scatterMieLUT.name = "Scatter Mie LUT";
                //do not miss texture dimension
                scatterMieLUT.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
                //computer sahder need RT with enableRandomWrite
                scatterMieLUT.enableRandomWrite = true;
                scatterMieLUT.Create();
            }
            int index = cs.FindKernel("AtmosphereSkyLUT");
            buffer.SetComputeTextureParam(cs, index, scatterRaylieLUTId, scatterRaylieLUT);
            buffer.SetComputeTextureParam(cs, index, scatterMieLUTId, scatterMieLUT);
            buffer.SetRenderTarget(scatterRaylieLUT);
            buffer.DispatchCompute(cs, index, scatterRaylieLUT.width / 8, scatterRaylieLUT.height / 8, scatterRaylieLUT.volumeDepth / 8);
            buffer.SetGlobalTexture(scatterRaylieLUTId, scatterRaylieLUT);
            buffer.SetGlobalTexture(scatterMieLUTId, scatterMieLUT);
        }
    }

    void UpdateSunColor() {
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

    void UpdateAmbient() {
        if (abmeintTexture == null) {
            abmeintTexture = new Texture2D(settings.ambientLUTSize, 1, TextureFormat.RGBAHalf, false, true);
            abmeintTexture.name = "Ambient Texture";
            abmeintTexture.Apply();
        }
        ReadRTpixelsBackToCPU(ambientLUT, abmeintTexture);
        float cosAngle = Vector3.Dot(Vector3.up, -sun.transform.forward);
        float cosAngle01 = cosAngle * 0.5f + 0.5f;
        Color color = abmeintTexture.GetPixel((int)(cosAngle01 * abmeintTexture.width), 0).gamma;
        RenderSettings.ambientLight = color;
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
