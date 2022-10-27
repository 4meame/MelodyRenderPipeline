using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

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
    Material material;
    bool useHDR;
    Mesh pointLightMesh;
    Mesh spotLightMesh;
    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, Camera camera) {
        this.context = context;
        this.cullingResults = cullingResults;
        this.camera = camera;
        material = new Material(Shader.Find("Hidden/Melody RP/VolumetricLight"));
        if(pointLightMesh == null) {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.hideFlags = HideFlags.HideAndDontSave;
            pointLightMesh = go.GetComponent<MeshFilter>().sharedMesh;
        }
        if (spotLightMesh == null) {
            spotLightMesh = CreateSpotLightMesh();
        }
    }

    public void PreRenderVolumetric(bool useVolumetric) {
        if (!useVolumetric || camera.cameraType == CameraType.Preview) {
            return;
        }
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
        int dirLightCount = 0, otherLightCount = 0;
        int i;
        //use very low value for near clip plane to simplify cone/frustum intersection
        Matrix4x4 proj = Matrix4x4.Perspective(camera.fieldOfView, camera.aspect, 0.01f, camera.farClipPlane);
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
        if (component == null || !component.isActiveAndEnabled) {
            return;
        }
        int pass = 0;
        if (!IsCameraInPointLightBounds(visibleLight.light)) {
            pass = 0;
        }
        //material.SetPass(pass);
        float scale = light.range * 2.0f;
        Matrix4x4 world = Matrix4x4.TRS(light.transform.position, light.transform.rotation, new Vector3(scale, scale, scale));
        material.SetMatrix("_WorldViewProj", viewProj * world);
        material.SetMatrix("_WorldView", camera.worldToCameraMatrix * world);
        material.SetInt("Index", index);
        Debug.Log(index);
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
}
