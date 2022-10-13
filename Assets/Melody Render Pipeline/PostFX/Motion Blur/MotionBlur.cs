﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static PostFXSettings;

public class MotionBlur {
    const string bufferName = "Motion Blur";
    CommandBuffer buffer = new CommandBuffer { name = bufferName };
    ScriptableRenderContext context;
    Camera camera;
    MotionBlurSettings settings;
    Vector2Int bufferSize;
    bool useHDR;

    ReconstructionFilter reconstructionFilter;
    FrameBlendingFilter frameBlendingFilter;
    RenderTexture tempBlendindBuffer;
    int motionResultId = Shader.PropertyToID("_MotionResult");

    public void Setup(ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, PostFXSettings settings, bool useHDR) {
        this.context = context;
        this.camera = camera;
        this.bufferSize = bufferSize;
        //apply to proper camera
        this.settings = camera.cameraType <= CameraType.SceneView ? (settings ? settings.motionBlurSettings : default) : default;
        this.useHDR = useHDR;
        if (reconstructionFilter == null) {
            reconstructionFilter = new ReconstructionFilter(buffer);
        }
        if (frameBlendingFilter == null) {
            frameBlendingFilter = new FrameBlendingFilter(buffer);
        }
    }

    public void DoMotionBlur(int from) {
        if(settings.mode == MotionBlurSettings.Mode.None) {
            return;
        }
        RenderTextureFormat format = useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        buffer.GetTemporaryRT(motionResultId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, format);
        if (settings.shutterAngle > 0 && settings.frameBlending > 0) {
            //reconstruction and frame blending
            if(tempBlendindBuffer == null) {
                tempBlendindBuffer = RenderTexture.GetTemporary(bufferSize.x, bufferSize.y, 0, format);
                tempBlendindBuffer.name = "tempBuffer";
                tempBlendindBuffer.filterMode = FilterMode.Bilinear;
            }
            reconstructionFilter.ProcessImage(settings.shutterAngle, settings.sampleCount, from, tempBlendindBuffer, bufferSize.x, bufferSize.y);
            frameBlendingFilter.BlendFrames(settings.frameBlending, tempBlendindBuffer, motionResultId);
            frameBlendingFilter.PushFrame(tempBlendindBuffer, bufferSize.x, bufferSize.y);
        }
        else if (settings.shutterAngle > 0) {
            //reconstruction only
            reconstructionFilter.ProcessImage(settings.shutterAngle, settings.sampleCount, from, motionResultId, bufferSize.x, bufferSize.y);
        }
        else if (settings.frameBlending > 0) {
            //frame blending only
            frameBlendingFilter.BlendFrames(settings.frameBlending, from, motionResultId);
            frameBlendingFilter.PushFrame(from, bufferSize.x, bufferSize.y);
        }
        else {
            //nothing to do!
        }
        ExecuteBuffer();
    }

    public void Combine(int sourceId) {
        buffer.Blit(motionResultId, sourceId);
        ExecuteBuffer();
    }

    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}