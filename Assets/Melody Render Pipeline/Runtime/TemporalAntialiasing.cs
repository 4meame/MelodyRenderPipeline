using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class TemporalAntialiasing : MonoBehaviour {
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
    bool useHDR;
    bool copyTextureSupported;
    bool isFirstFrame = true;
    Material taaMaterial;

    RenderTexture[] temporalBuffer;
    static int indexWrite = 0;
    RenderTexture historyMV;
    RenderTexture historyDepth;
    Vector2 jitter;
    int sampleIndex = 0;
    const int k_SampleCount = 8;

    int tempTextureId = Shader.PropertyToID("_TempTexture");
    int colorTextureId = Shader.PropertyToID("_CameraColorTexture");
    //for reprojection
    private Matrix4x4 nonJitteredVP;
    private Matrix4x4 previousVP;
    public void Setup(ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, CameraBufferSettings.TAA taa, bool useHDR, bool copyTextureSupported) {
        this.context = context;
        this.camera = camera;
        this.bufferSize = bufferSize;
        this.taa = taa;
        this.useHDR = useHDR;
        this.copyTextureSupported = copyTextureSupported;
        taaMaterial = new Material(Shader.Find("Hidden/Melody RP/TemporalAntialiasing"));
    }

    public void Render(int sourceId) {
        if (taa.enabled) {       
            if(camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection) {
                return;
            }
            buffer.name = "Temporal Antialiasing";
            buffer.BeginSample("Copy Current Frame");
            buffer.GetTemporaryRT(colorTextureId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            if (copyTextureSupported) {
                buffer.CopyTexture(sourceId, colorTextureId);
            } else {
                Draw(sourceId, colorTextureId);
                ExecuteBuffer();
            }
            buffer.EndSample("Copy Current Frame");
            ExecuteBuffer();

            BeginFrame();
            UpdateTextureSize(historyDepth);
            UpdateTextureSize(historyMV);
            EnsureArray(ref temporalBuffer, 2);
            EnsureRenderTarget(ref temporalBuffer[0], bufferSize.x, bufferSize.y, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default, FilterMode.Bilinear, 0);
            EnsureRenderTarget(ref temporalBuffer[1], bufferSize.x, bufferSize.y, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default, FilterMode.Bilinear, 1);
            //TAA Start
            int indexRead = indexWrite;
            indexWrite = (++indexWrite) % 2;
            buffer.SetGlobalVector("_LastJitter", jitter);
            camera.ResetProjectionMatrix();
            ConfigureJitteredProjectionMatrix(camera, ref jitter);
            buffer.SetGlobalVector("_Jitter", jitter);
            const float kMotionAmplification_Blending = 100f * 60f;
            const float kMotionAmplification_Bounding = 100f * 30f;
            buffer.SetGlobalFloat("_Sharpness", taa.sharpness);
            buffer.SetGlobalVector("_TemporalClipBounding", new Vector4(taa.staticAABBScale, taa.motionAABBScale, kMotionAmplification_Bounding, 0f));
            buffer.SetGlobalVector("_FinalBlendParams", new Vector4(taa.staticBlending, taa.motionBlending, kMotionAmplification_Blending, 0f));
            buffer.SetGlobalTexture("_LastFrameDepthTexture", historyDepth);
            buffer.SetGlobalTexture("_LastFrameMotionVectorTexture", historyMV);
            buffer.SetGlobalTexture("_HistoryTex", temporalBuffer[indexRead]);
            nonJitteredVP = camera.nonJitteredProjectionMatrix * camera.worldToCameraMatrix;
            buffer.SetGlobalMatrix("_InvNonJitterVP", nonJitteredVP.inverse);
            buffer.SetGlobalMatrix("_InvLastVP", previousVP.inverse);
            buffer.BeginSample("Antialiasing Resolve");


            buffer.SetGlobalTexture("_SourceTex", colorTextureId);
            buffer.Blit(colorTextureId, temporalBuffer[indexWrite], taaMaterial, (int)Pass.Resolve);
            buffer.Blit(temporalBuffer[indexWrite], sourceId);

            EndFrame();
            buffer.EndSample("Antialiasing Resolve");
            ExecuteBuffer();
        }
    }

    void BeginFrame() {
        CreateHistoryTexture();
    }

    void EndFrame() {

    }

    public void Refresh() {
        previousVP = nonJitteredVP;
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

    public void CopyLastFrameRT(int lastDepth, int lastMV) {
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

    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public void CleanUp() {
        buffer.ReleaseTemporaryRT(tempTextureId);
    }

    void EnsureArray<T>(ref T[] array, int size, T initialValue = default(T)) {
        if (array == null || array.Length != size) {
            array = new T[size];
            for (int i = 0; i != size; i++)
                array[i] = initialValue;
        }
    }

    bool EnsureRenderTarget(ref RenderTexture rt, int width, int height, RenderTextureFormat format, FilterMode filterMode, int index, int depthBits = 0, int antiAliasing = 1) {
        if (rt != null && (rt.width != width || rt.height != height || rt.format != format || rt.filterMode != filterMode || rt.antiAliasing != antiAliasing)) {
            RenderTexture.ReleaseTemporary(rt);
            rt = null;
        }
        if (rt == null) {
            rt = RenderTexture.GetTemporary(width, height, depthBits, format, RenderTextureReadWrite.Default, antiAliasing);
            rt.filterMode = filterMode;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.name = "temporalBuffer" + index;
            //new target
            return true;
        }
        //same target
        return false;
    }

    Vector2 GenerateRandomOffset() {
        var offset = new Vector2(
                HaltonSeq.Get((sampleIndex & 1023) + 1, 2) - 0.5f,
                HaltonSeq.Get((sampleIndex & 1023) + 1, 3) - 0.5f
            );
        if (++sampleIndex >= k_SampleCount)
            sampleIndex = 0;
        return offset;
    }

    public Matrix4x4 GetJitteredProjectionMatrix(Camera camera, ref Vector2 jitter) {
        Matrix4x4 cameraProj;
        jitter = GenerateRandomOffset();
        jitter *= taa.jitterScale;
        cameraProj = camera.orthographic
            ? GetJitteredOrthographicProjectionMatrix(camera, jitter)
            : GetJitteredPerspectiveProjectionMatrix(camera, jitter);
        jitter = new Vector2(jitter.x / camera.pixelWidth, jitter.y / camera.pixelHeight);
        return cameraProj;
    }

    public static Matrix4x4 GetJitteredPerspectiveProjectionMatrix(Camera camera, Vector2 offset) {
        float near = camera.nearClipPlane;
        float far = camera.farClipPlane;
        float vertical = Mathf.Tan(0.5f * Mathf.Deg2Rad * camera.fieldOfView) * near;
        float horizontal = vertical * camera.aspect;
        offset.x *= horizontal / (0.5f * camera.pixelWidth);
        offset.y *= vertical / (0.5f * camera.pixelHeight);
        var matrix = camera.projectionMatrix;
        matrix[0, 2] += offset.x / horizontal;
        matrix[1, 2] += offset.y / vertical;
        return matrix;
    }

    public static Matrix4x4 GetJitteredOrthographicProjectionMatrix(Camera camera, Vector2 offset) {
        float vertical = camera.orthographicSize;
        float horizontal = vertical * camera.aspect;
        offset.x *= horizontal / (0.5f * camera.pixelWidth);
        offset.y *= vertical / (0.5f * camera.pixelHeight);
        float left = offset.x - horizontal;
        float right = offset.x + horizontal;
        float top = offset.y + vertical;
        float bottom = offset.y - vertical;
        return Matrix4x4.Ortho(left, right, bottom, top, camera.nearClipPlane, camera.farClipPlane);
    }

    public void ConfigureJitteredProjectionMatrix(Camera camera, ref Vector2 jitter) {
        camera.nonJitteredProjectionMatrix = camera.projectionMatrix;
        camera.projectionMatrix = GetJitteredProjectionMatrix(camera, ref jitter);
        camera.useJitteredProjectionMatrixForTransparentRendering = false;
    }

    public void PreRenderFrame(Camera camera) {
        buffer.SetGlobalVector("_LastJitter", jitter);
        camera.ResetProjectionMatrix();
        ConfigureJitteredProjectionMatrix(camera, ref jitter);
        buffer.SetGlobalVector("_Jitter", jitter);
    }


    public static class HaltonSeq {
        public static float Get(int index, int radix) {
            float result = 0f;
            float fraction = 1f / (float)radix;
            while (index > 0) {
                result += (float)(index % radix) * fraction;

                index /= radix;
                fraction /= (float)radix;
            }
            return result;
        }
    }

    private void OnDisable() {
        Shader.SetGlobalVector("_Jitter", Vector4.zero);
    }
}
