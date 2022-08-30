using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class MotionVectorRender {
    const string bufferName = "Render Motion Vector";
    CommandBuffer buffer = new CommandBuffer {
        name = bufferName
    };
    static ShaderTagId motionTagId = new ShaderTagId("Motion");
    ScriptableRenderContext context;
    Camera camera;
    CullingResults cullingResults;
    Vector2Int bufferSize;
    CameraBufferSettings.TAA taa;
    Material motionVectorMaterial;
    //For camera motion vector
    private Matrix4x4 nonJitteredVP;
    private Matrix4x4 previousVP;
    static Mesh fullscreenMesh = null;

    static int motionVectorTextureId = Shader.PropertyToID("_CameraMotionVectorTexture");

    public void Setup(ScriptableRenderContext context, Camera camera, CullingResults cullingResults, Vector2Int bufferSize, CameraBufferSettings.TAA taa) {
        this.context = context;
        this.camera = camera;
        this.cullingResults = cullingResults;
        this.bufferSize = bufferSize;
        this.taa = taa;
        motionVectorMaterial = new Material(Shader.Find("Hidden/Melody RP/DrawMotionVector"));
    }

    public void Render() {
        if (taa.enabled) {
            context.SetupCameraProperties(camera);
            camera.depthTextureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;
            var sortingSettings = new SortingSettings(camera);
            DrawingSettings drawSettings = new DrawingSettings(motionTagId, sortingSettings) {
                perObjectData = PerObjectData.MotionVectors,
                overrideMaterial = motionVectorMaterial,
                overrideMaterialPassIndex = 0
            };
            FilteringSettings filterSettings = new FilteringSettings(RenderQueueRange.all) {
                excludeMotionVectorObjects = false
            };
            buffer.GetTemporaryRT(motionVectorTextureId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, RenderTextureFormat.RGFloat);
            buffer.SetRenderTarget(motionVectorTextureId);
            buffer.ClearRenderTarget(true, true, Color.black);
            buffer.BeginSample("Draw Objects Motion");
            ExecuteBuffer();
            //opaque motion objects
            sortingSettings.criteria = SortingCriteria.CommonOpaque;
            drawSettings.sortingSettings = sortingSettings;
            filterSettings.renderQueueRange = RenderQueueRange.opaque;
            context.DrawRenderers(cullingResults, ref drawSettings, ref filterSettings);
            buffer.EndSample("Draw Objects Motion");

            //camera motion vector
            buffer.BeginSample("Draw Camera Motion");
            nonJitteredVP = camera.nonJitteredProjectionMatrix * camera.worldToCameraMatrix;
            buffer.SetGlobalMatrix("_CamPrevViewProjMatrix", previousVP);
            buffer.SetGlobalMatrix("_CamNonJitteredViewProjMatrix", nonJitteredVP);
            buffer.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            //draw full screen quad to make Camera motion
            buffer.DrawMesh(FullscreenMesh, Matrix4x4.identity, motionVectorMaterial, 0, 1, null);
            buffer.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
            buffer.EndSample("Draw Camera Motion");
            ExecuteBuffer();

            buffer.SetGlobalTexture("_CameraMotionVectorTexture", motionVectorTextureId);
            ExecuteBuffer();
        }
    }

    public void CleanUp() {
        buffer.ReleaseTemporaryRT(motionVectorTextureId);
    }

    public void Refresh() {
        //for camera motion vector
        previousVP = nonJitteredVP;
    }

    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public static Mesh FullscreenMesh {
        get {
            if (fullscreenMesh != null) {
                return fullscreenMesh;
            }
            float topV = 1.0f;
            float bottomV = 0.0f;
            fullscreenMesh = new Mesh { name = "Fullscreen Quad" };
            fullscreenMesh.SetVertices(new List<Vector3> {
                new Vector3(-1.0f, -1.0f, 0.0f),
                new Vector3(-1.0f,  1.0f, 0.0f),
                new Vector3(1.0f, -1.0f, 0.0f),
                new Vector3(1.0f,  1.0f, 0.0f)
            });
            fullscreenMesh.SetUVs(0, new List<Vector2> {
                new Vector2(0.0f, bottomV),
                new Vector2(0.0f, topV),
                new Vector2(1.0f, bottomV),
                new Vector2(1.0f, topV)
            });
            fullscreenMesh.SetIndices(new[] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0, false);
            fullscreenMesh.UploadMeshData(true);
            return fullscreenMesh;
        }
    }
}
