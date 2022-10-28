using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using System;

public class VolumetricLight {
    const string bufferName = "VolemetricLight";
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };
    const int maxDirLightCount = 4;
    const int maxOtherLightCount = 64;
    ScriptableRenderContext context;
    CullingResults cullingResults;
    Camera camera;
    Vector2 bufferSize;
    bool useHDR;
    ShadowSettings shadowSettings;
    Material globalMaterial;
    Mesh pointLightMesh;
    Mesh spotLightMesh;
    RenderTexture volumeLightPreTexture;
    Texture2D ditheringTexture;
    Texture3D noiseTexture;
    Vector2 cameraBufferSize;
    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, Camera camera, Vector2 bufferSize, bool useHDR, ShadowSettings shadowSettings) {
        this.context = context;
        this.cullingResults = cullingResults;
        this.camera = camera;
        this.bufferSize = bufferSize;
        this.useHDR = useHDR;
        this.shadowSettings = shadowSettings;
        if(pointLightMesh == null) {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.hideFlags = HideFlags.HideAndDontSave;
            pointLightMesh = go.GetComponent<MeshFilter>().sharedMesh;
        }
        if (spotLightMesh == null) {
            spotLightMesh = CreateSpotLightMesh();
        }
        UpdateRenderTexture();
        GenerateDitherTexture();
        buffer.SetGlobalTexture("_DitherTexture", ditheringTexture);
        LoadNoise3dTexture();
        buffer.SetGlobalTexture("_NoiseTexture", noiseTexture);
    }

    void UpdateRenderTexture() {
        Vector2 halfBufferSize = new Vector2(bufferSize.x / 2, bufferSize.y / 2);
        Vector2 currentBufferSize = new Vector2(bufferSize.x, bufferSize.y);
        if (cameraBufferSize != currentBufferSize) {
            cameraBufferSize = currentBufferSize;
            volumeLightPreTexture = RenderTexture.GetTemporary((int)cameraBufferSize.x, (int)cameraBufferSize.y, 0, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            volumeLightPreTexture.filterMode = FilterMode.Bilinear;
            volumeLightPreTexture.name = "Volumetric Pre Light";
        }
    }

    public void PreRenderVolumetric(bool useVolumetric) {
        if (!useVolumetric || camera.cameraType == CameraType.Preview) {
            return;
        }
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
        int dirLightCount = 0, otherLightCount = 0;
        int i;
        Matrix4x4 proj = camera.projectionMatrix;
        proj = GL.GetGPUProjectionMatrix(proj, true);
        Matrix4x4 viewProj = proj * camera.worldToCameraMatrix;
        for (i = 0; i < visibleLights.Length; i++) {
            VisibleLight visibleLight = visibleLights[i];
            switch (visibleLight.lightType) {
                case LightType.Point:
                    if (otherLightCount < maxOtherLightCount) {
                        SetUpPointVolume(otherLightCount++, visibleLight, viewProj);
                    }
                    break;
                case LightType.Directional:
                    if (dirLightCount < maxDirLightCount) {
                        SetUpDirectionalVolume(dirLightCount++, visibleLight);
                    }
                    break;
                case LightType.Spot:
                    if (otherLightCount < maxOtherLightCount) {
                        SetUpSpotVolume(visibleLight);
                    }
                    break;
            }
        }
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void SetUpPointVolume(int index, VisibleLight visibleLight, Matrix4x4 viewProj) {
        VolumetricLightComponent component = visibleLight.light.GetComponent<VolumetricLightComponent>();
        Light light = visibleLight.light;
        Material material = new Material(Shader.Find("Hidden/Melody RP/VolumetricLight"));
        if (component == null || !component.isActiveAndEnabled) {
            return;
        }
        int pass = 0;
        if (!IsCameraInPointLightBounds(visibleLight.light)) {
            pass = 1;
        }
        material.SetPass(pass);
        float scale = light.range * 2.0f;
        Matrix4x4 world = Matrix4x4.TRS(light.transform.position, light.transform.rotation, new Vector3(scale, scale, scale));
        material.SetMatrix("_WorldViewProj", viewProj * world);
        material.SetMatrix("_WorldView", camera.worldToCameraMatrix * world);
        material.SetVector("_CameraForward", camera.transform.forward);
        material.SetInt("Index", index);
        material.SetFloat("_Range", light.range);
        bool forceShadowsOff = false;
        if ((light.transform.position - camera.transform.position).magnitude >= shadowSettings.maxDistance)
            forceShadowsOff = true;
        if (light.shadows != LightShadows.None && !forceShadowsOff) {
            material.EnableKeyword("_RECEIVE_SHADOWS");
        } else {
            material.DisableKeyword("_RECEIVE_SHADOWS");
        }
        //buffer.SetRenderTarget(volumeLightPreTexture);
        buffer.DrawMesh(pointLightMesh, world, material, 0, pass);
    }

    void SetUpDirectionalVolume(int index, VisibleLight visibleLight) {
        VolumetricLightComponent component = visibleLight.light.GetComponent<VolumetricLightComponent>();
        if (component == null || !component.isActiveAndEnabled) {
            return;
        }

    }

    void SetUpSpotVolume(VisibleLight visibleLight) {
        VolumetricLightComponent component = visibleLight.light.GetComponent<VolumetricLightComponent>();
        if (component == null || !component.isActiveAndEnabled) {
            return;
        }

    }

    bool IsCameraInPointLightBounds(Light light)
    {
        float distanceSqr = (light.transform.position - camera.transform.position).sqrMagnitude;
        float extendedRange = light.range + 1;
        if (distanceSqr < (extendedRange * extendedRange))
            return true;
        return false;
    }

    Mesh CreateSpotLightMesh() {
        //copy & pasted from other project, the geometry is too complex, should be simplified
        Mesh mesh = new Mesh();
        const int segmentCount = 16;
        Vector3[] vertices = new Vector3[2 + segmentCount * 3];
        Color32[] colors = new Color32[2 + segmentCount * 3];
        vertices[0] = new Vector3(0, 0, 0);
        vertices[1] = new Vector3(0, 0, 1);
        float angle = 0;
        float step = Mathf.PI * 2.0f / segmentCount;
        float ratio = 0.9f;
        for (int i = 0; i < segmentCount; ++i) {
            vertices[i + 2] = new Vector3(-Mathf.Cos(angle) * ratio, Mathf.Sin(angle) * ratio, ratio);
            colors[i + 2] = new Color32(255, 255, 255, 255);
            vertices[i + 2 + segmentCount] = new Vector3(-Mathf.Cos(angle), Mathf.Sin(angle), 1);
            colors[i + 2 + segmentCount] = new Color32(255, 255, 255, 0);
            vertices[i + 2 + segmentCount * 2] = new Vector3(-Mathf.Cos(angle) * ratio, Mathf.Sin(angle) * ratio, 1);
            colors[i + 2 + segmentCount * 2] = new Color32(255, 255, 255, 255);
            angle += step;
        }
        mesh.vertices = vertices;
        mesh.colors32 = colors;
        int[] indices = new int[segmentCount * 3 * 2 + segmentCount * 6 * 2];
        int index = 0;
        for (int i = 2; i < segmentCount + 1; ++i) {
            indices[index++] = 0;
            indices[index++] = i;
            indices[index++] = i + 1;
        }
        indices[index++] = 0;
        indices[index++] = segmentCount + 1;
        indices[index++] = 2;
        for (int i = 2; i < segmentCount + 1; ++i) {
            indices[index++] = i;
            indices[index++] = i + segmentCount;
            indices[index++] = i + 1;

            indices[index++] = i + 1;
            indices[index++] = i + segmentCount;
            indices[index++] = i + segmentCount + 1;
        }
        indices[index++] = 2;
        indices[index++] = 1 + segmentCount;
        indices[index++] = 2 + segmentCount;
        indices[index++] = 2 + segmentCount;
        indices[index++] = 1 + segmentCount;
        indices[index++] = 1 + segmentCount + segmentCount;
        //------------
        for (int i = 2 + segmentCount; i < segmentCount + 1 + segmentCount; ++i) {
            indices[index++] = i;
            indices[index++] = i + segmentCount;
            indices[index++] = i + 1;
            indices[index++] = i + 1;
            indices[index++] = i + segmentCount;
            indices[index++] = i + segmentCount + 1;
        }
        indices[index++] = 2 + segmentCount;
        indices[index++] = 1 + segmentCount * 2;
        indices[index++] = 2 + segmentCount * 2;
        indices[index++] = 2 + segmentCount * 2;
        indices[index++] = 1 + segmentCount * 2;
        indices[index++] = 1 + segmentCount * 3;
        ////-------------------------------------
        for (int i = 2 + segmentCount * 2; i < segmentCount * 3 + 1; ++i) {
            indices[index++] = 1;
            indices[index++] = i + 1;
            indices[index++] = i;
        }
        indices[index++] = 1;
        indices[index++] = 2 + segmentCount * 2;
        indices[index++] = segmentCount * 3 + 1;
        mesh.triangles = indices;
        mesh.RecalculateBounds();
        return mesh;
    }

    void GenerateDitherTexture() {
        if (ditheringTexture != null) {
            return;
        }

        int size = 8;
        ditheringTexture = new Texture2D(size, size, TextureFormat.Alpha8, false, true);
        ditheringTexture.filterMode = FilterMode.Point;
        Color32[] c = new Color32[size * size];

        byte b;
        int i = 0;
        b = (byte)(1.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(49.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(13.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(61.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(4.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(52.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(16.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(64.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(33.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(17.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(45.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(29.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(36.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(20.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(48.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(32.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(9.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(57.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(5.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(53.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(12.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(60.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(8.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(56.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(41.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(25.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(37.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(21.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(44.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(28.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(40.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(24.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(3.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(51.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(15.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(63.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(2.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(50.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(14.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(62.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(35.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(19.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(47.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(31.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(34.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(18.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(46.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(30.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(11.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(59.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(7.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(55.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(10.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(58.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(6.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(54.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(43.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(27.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(39.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(23.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(42.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(26.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(38.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(22.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        ditheringTexture.SetPixels32(c);
        ditheringTexture.Apply();
    }

    void LoadNoise3dTexture() {
        if (noiseTexture != null) {
            return;
        }
        //basic dds loader for 3d texture - !not very robust!
        TextAsset data = Resources.Load("NoiseVolume") as TextAsset;
        byte[] bytes = data.bytes;
        uint height = BitConverter.ToUInt32(data.bytes, 12);
        uint width = BitConverter.ToUInt32(data.bytes, 16);
        uint pitch = BitConverter.ToUInt32(data.bytes, 20);
        uint depth = BitConverter.ToUInt32(data.bytes, 24);
        uint formatFlags = BitConverter.ToUInt32(data.bytes, 20 * 4);
        //uint fourCC = BitConverter.ToUInt32(data.bytes, 21 * 4);
        uint bitdepth = BitConverter.ToUInt32(data.bytes, 22 * 4);
        if (bitdepth == 0)
            bitdepth = pitch / width * 8;
        //doesn't work with TextureFormat.Alpha8 for some reason
        noiseTexture = new Texture3D((int)width, (int)height, (int)depth, TextureFormat.RGBA32, false);
        noiseTexture.name = "3D Noise";
        Color[] c = new Color[width * height * depth];
        uint index = 128;
        if (data.bytes[21 * 4] == 'D' && data.bytes[21 * 4 + 1] == 'X' && data.bytes[21 * 4 + 2] == '1' && data.bytes[21 * 4 + 3] == '0' && (formatFlags & 0x4) != 0) {
            uint format = BitConverter.ToUInt32(data.bytes, (int)index);
            if (format >= 60 && format <= 65)
                bitdepth = 8;
            else if (format >= 48 && format <= 52)
                bitdepth = 16;
            else if (format >= 27 && format <= 32)
                bitdepth = 32;
            index += 20;
        }
        uint byteDepth = bitdepth / 8;
        pitch = (width * bitdepth + 7) / 8;
        for (int d = 0; d < depth; ++d) {
            //index = 128;
            for (int h = 0; h < height; ++h) {
                for (int w = 0; w < width; ++w) {
                    float v = (bytes[index + w * byteDepth] / 255.0f);
                    c[w + h * width + d * width * height] = new Color(v, v, v, v);
                }

                index += pitch;
            }
        }
        noiseTexture.SetPixels(c);
        noiseTexture.Apply();
    }
}
