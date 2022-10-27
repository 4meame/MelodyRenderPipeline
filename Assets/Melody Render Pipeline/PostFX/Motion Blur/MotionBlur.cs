using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static PostFXSettings;

public class MotionBlur {
    const string bufferName = "Motion Blur";
    CommandBuffer buffer = new CommandBuffer { name = bufferName };
    ScriptableRenderContext context;
    Camera camera;
    PhyscialCameraSettings physcialCamera;
    MotionBlurSettings settings;
    Vector2Int bufferSize;
    bool useHDR;

    ReconstructionFilter reconstructionFilter;
    FrameBlendingFilter frameBlendingFilter;
    RenderTexture tempBlendindBuffer;
    int motionResultId = Shader.PropertyToID("_MotionResult");

    public void Setup(ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, PostFXSettings settings, PhyscialCameraSettings physcialCamera, bool useHDR) {
        this.context = context;
        this.camera = camera;
        this.physcialCamera = physcialCamera;
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
        float shutterAngle = 0;
        float frameBlending = 0;
        switch (settings.mode) {
            case MotionBlurSettings.Mode.None:
                return;
            case MotionBlurSettings.Mode.Manual:
                shutterAngle = settings.shutterAngle;
                frameBlending = settings.frameBlending;
                break;
            case MotionBlurSettings.Mode.Physical:
                //A = S * F * 360
                shutterAngle = (1.0f / physcialCamera.shutterSpeed) * 360 * 59.98f;
                shutterAngle = Mathf.Clamp(shutterAngle, 0f, 360f);
                frameBlending = settings.frameBlending;
                break;
            default:
                break;
        }

        RenderTextureFormat format = useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        buffer.GetTemporaryRT(motionResultId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, format);
        if (shutterAngle > 0 && frameBlending > 0) {
            //reconstruction and frame blending
            if(tempBlendindBuffer == null) {
                tempBlendindBuffer = RenderTexture.GetTemporary(bufferSize.x, bufferSize.y, 0, format);
                tempBlendindBuffer.name = "tempBuffer";
                tempBlendindBuffer.filterMode = FilterMode.Bilinear;
            }
            reconstructionFilter.ProcessImage(shutterAngle, settings.sampleCount, from, tempBlendindBuffer, bufferSize.x, bufferSize.y);
            frameBlendingFilter.BlendFrames(frameBlending, tempBlendindBuffer, motionResultId);
            frameBlendingFilter.PushFrame(tempBlendindBuffer, bufferSize.x, bufferSize.y);
        }
        else if (shutterAngle > 0) {
            //reconstruction only
            reconstructionFilter.ProcessImage(shutterAngle, settings.sampleCount, from, motionResultId, bufferSize.x, bufferSize.y);
        }
        else if (settings.frameBlending > 0) {
            //frame blending only
            frameBlendingFilter.BlendFrames(frameBlending, from, motionResultId);
            frameBlendingFilter.PushFrame(from, bufferSize.x, bufferSize.y);
        }
        else {
            //nothing to do!
        }
        ExecuteBuffer();
    }

    public void Combine(int sourceId) {
        if (settings.mode == MotionBlurSettings.Mode.None)
            return;
        buffer.Blit(motionResultId, sourceId);
        ExecuteBuffer();
    }

    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}
