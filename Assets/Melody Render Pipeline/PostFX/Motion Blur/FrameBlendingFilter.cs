﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class FrameBlendingFilter {
    enum Pass
    {
        Compress,
        Blending
    }
    CommandBuffer buffer;
    Material material;
    Frame[] frameList;
    int lastFrameCount;
    int sourceId = Shader.PropertyToID("_MotionBlurSource");

    public FrameBlendingFilter(CommandBuffer buffer) {
        this.buffer = buffer;
        var shader = Shader.Find("Hidden/Melody RP/Post FX Stack/Motion/FrameBlending");
        material = new Material(shader);
        material.hideFlags = HideFlags.DontSave;
        frameList = new Frame[4];
    }

    public void Release() {
        foreach (var frame in frameList) {
            frame.Release();
        }
        frameList = null;
    }

    public void PushFrame(RenderTargetIdentifier source, int width, int height) {
        //push only when actual update (do nothing while pausing)
        var frameCount = Time.frameCount;
        if (frameCount == lastFrameCount) { 
            return; 
        }
        //update the frame record.
        var index = frameCount % frameList.Length;
        frameList[index].MakeRecord(buffer, source, width, height, material);
        lastFrameCount = frameCount;
    }

    public void BlendFrames(float strength, RenderTargetIdentifier source, RenderTargetIdentifier destination) {
        var t = Time.time;

        var f1 = GetFrameRelative(-1);
        var f2 = GetFrameRelative(-2);
        var f3 = GetFrameRelative(-3);
        var f4 = GetFrameRelative(-4);

        material.SetTexture("_History1LumaTex", f1.lumaTexture);
        material.SetTexture("_History2LumaTex", f2.lumaTexture);
        material.SetTexture("_History3LumaTex", f3.lumaTexture);
        material.SetTexture("_History4LumaTex", f4.lumaTexture);

        material.SetTexture("_History1ChromaTex", f1.chromaTexture);
        material.SetTexture("_History2ChromaTex", f2.chromaTexture);
        material.SetTexture("_History3ChromaTex", f3.chromaTexture);
        material.SetTexture("_History4ChromaTex", f4.chromaTexture);

        material.SetFloat("_History1Weight", f1.CalculateWeight(strength, t));
        material.SetFloat("_History2Weight", f2.CalculateWeight(strength, t));
        material.SetFloat("_History3Weight", f3.CalculateWeight(strength, t));
        material.SetFloat("_History4Weight", f4.CalculateWeight(strength, t));

        Draw(source, destination, Pass.Blending);
    }

    struct Frame {
        public RenderTexture lumaTexture;
        public RenderTexture chromaTexture;
        public float time;
        RenderTargetIdentifier[] identifier;

        public float CalculateWeight(float strength, float currentTime) {
            if (time == 0) { 
                return 0; 
            }
            var coeff = Mathf.Lerp(90.0f, 10.0f, strength);
            return Mathf.Exp((time - currentTime) * coeff);
        }

        public void Release() {
            if (lumaTexture != null) {
                RenderTexture.ReleaseTemporary(lumaTexture);
            }
            if (chromaTexture != null) { 
                RenderTexture.ReleaseTemporary(chromaTexture); 
            }
            lumaTexture = null;
            chromaTexture = null;
        }

        public void MakeRecord(CommandBuffer buffer, RenderTargetIdentifier source, int width, int height, Material material) {
            Release();
            lumaTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.R16, RenderTextureReadWrite.Linear);
            chromaTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.R16, RenderTextureReadWrite.Linear);
            lumaTexture.filterMode = FilterMode.Point;
            chromaTexture.filterMode = FilterMode.Point;
            if (identifier == null) {
                identifier = new RenderTargetIdentifier[2];
            }
            identifier[0] = lumaTexture.colorBuffer;
            identifier[1] = chromaTexture.colorBuffer;

            buffer.SetGlobalTexture("_MotionBlurSource", source);
            buffer.SetRenderTarget(identifier, lumaTexture.depthBuffer);
            buffer.DrawProcedural(Matrix4x4.identity, material, (int)Pass.Compress, MeshTopology.Triangles, 3);
            time = Time.time;
        }
    }

    //retrieve a frame record with relative indexing, use a negative index to refer to previous frames.
    Frame GetFrameRelative(int offset) {
        var index = (Time.frameCount + frameList.Length + offset) % frameList.Length;
        return frameList[index];
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass) {
        buffer.SetGlobalTexture(sourceId, from);
        //SetRenderTarget will reset the viewport to cover the entire target
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, material, (int)pass, MeshTopology.Triangles, 3);
    }
}
