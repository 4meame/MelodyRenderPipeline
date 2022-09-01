using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class TemporalAntialiasing {
    enum Pass {
        Resolve,
        CopyDepth,
        CopyMotionVector
    }
    const string bufferName = "Temporal Anti-aliasing";
    CommandBuffer buffer = new CommandBuffer {
        name = bufferName
    };
    ScriptableRenderContext context;
    Camera camera;
    Vector2Int bufferSize;
    CameraBufferSettings.TAA taa;
    Material taaMaterial;

    RenderTexture historyTex;
    RenderTexture historyMV;
    RenderTexture historyDepth;
    Vector2 jitter;

    int tempTextureId = Shader.PropertyToID("_TempTexture");
    public void Setup(ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, CameraBufferSettings.TAA taa) {
        this.context = context;
        this.camera = camera;
        this.bufferSize = bufferSize;
        this.taa = taa;
        taaMaterial = new Material(Shader.Find("Hidden/Melody RP/TemporalAntialiasing"));
    }

    public void Render(int sourceId) {
        if (taa.enabled) {
            BeginFrame();
            UpdateTextureSize(historyDepth);
            UpdateTextureSize(historyMV);
            EndFrame();
        }
    }

    void BeginFrame() {
        CreateHistoryTexture();
    }

    void EndFrame() {

    }

    void CreateHistoryTexture() {
        if (historyDepth == null) {
            historyDepth = new RenderTexture(bufferSize.x, bufferSize.y, 32, RenderTextureFormat.Depth, 0);
            historyDepth.bindTextureMS = false;
            historyDepth.Create();
        }
        if (historyMV == null) {
            historyMV = new RenderTexture(bufferSize.x, bufferSize.y, 0, RenderTextureFormat.RGFloat, 0);
            historyMV.bindTextureMS = false;
            historyMV.Create();
        }
    }

    void UpdateTextureSize(RenderTexture rt) {
        if (rt.width != bufferSize.x || rt.height != bufferSize.y)  {
            rt.Release();
            rt.width = bufferSize.x;
            rt.height = bufferSize.y;
            rt.Create();
        }
    }

    public void CopyLastFrameRT(int lastDepth, int lastMV, bool copyTextureSupported) {
        if (copyTextureSupported) {
            buffer.CopyTexture(lastDepth, historyDepth);
        } else {
            buffer.name = "Copy Depth";
            Draw(lastDepth, historyDepth, true);
            ExecuteBuffer();
        }
        if (copyTextureSupported) {
            buffer.CopyTexture(lastMV, historyMV);
        } else {
            buffer.name = "Copy Motion Vector";
            Draw(lastMV, historyMV);
            ExecuteBuffer();
        }
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, bool isDepth = false) {
        buffer.SetGlobalTexture(tempTextureId, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, taaMaterial, isDepth ? (int)Pass.CopyDepth : (int)Pass.CopyMotionVector, MeshTopology.Triangles, 3);
    }

    public void CleanUp() {
        
    }

    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}
