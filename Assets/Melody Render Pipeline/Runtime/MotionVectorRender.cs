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

    private Matrix4x4 nonJitteredVP;
    private Matrix4x4 previousVP;
    private Matrix4x4 camNonJitteredVP;
    private Matrix4x4 camPreviousVP;
    static Mesh fullscreenMesh = null;

    public void Setup(ScriptableRenderContext context, Camera camera, CullingResults cullingResults, Vector2Int bufferSize, CameraBufferSettings.TAA taa) {
        this.context = context;
        this.camera = camera;
        this.cullingResults = cullingResults;
        this.bufferSize = bufferSize;
        this.taa = taa;
        if (motionVectorMaterial == null) {
            motionVectorMaterial = new Material(Shader.Find("Hidden/Melody RP/DrawMotionVector"));
        }
    }

    public void Render(int sourceId, int motionVectorTextureId, int depthAttachmentId) {
        if (taa.motionVectorEnabled) {
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
            //from hdrp
            var proj = camera.projectionMatrix;
            var view = camera.worldToCameraMatrix;
            var gpuNonJitteredProj = GL.GetGPUProjectionMatrix(proj, true);
            nonJitteredVP = gpuNonJitteredProj * view;
            buffer.SetGlobalMatrix("_PrevViewProjMatrix", previousVP);
            buffer.SetGlobalMatrix("_NonJitteredViewProjMatrix", nonJitteredVP);
            buffer.GetTemporaryRT(motionVectorTextureId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, RenderTextureFormat.RGFloat);
            buffer.SetRenderTarget(motionVectorTextureId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, depthAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            buffer.ClearRenderTarget(true, true, Color.clear);
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
            camNonJitteredVP = camera.nonJitteredProjectionMatrix * camera.worldToCameraMatrix;
            buffer.SetGlobalMatrix("_CamPrevViewProjMatrix", camPreviousVP);
            buffer.SetGlobalMatrix("_CamNonJitteredViewProjMatrix", camNonJitteredVP);
            buffer.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            //draw full screen quad to make Camera motion
            buffer.DrawMesh(FullscreenMesh, Matrix4x4.identity, motionVectorMaterial, 0, 1, null);
            buffer.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
            buffer.EndSample("Draw Camera Motion");
            ExecuteBuffer();

            //set render target back
            buffer.SetGlobalTexture("_CameraMotionVectorTexture", motionVectorTextureId);
            buffer.SetRenderTarget(sourceId);
            ExecuteBuffer();
        }
    }

    public void CleanUp(int destinationId) {
        buffer.ReleaseTemporaryRT(destinationId);
    }

    public void Refresh() {
        if (taa.motionVectorEnabled) {
            previousVP = nonJitteredVP;
            camPreviousVP = camNonJitteredVP;
        }
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
