// Crest Ocean System

// Copyright 2020 Wave Harmonic Ltd

// This script originated from the unity standard assets. It has been modified heavily to be camera-centric (as opposed to
// geometry-centric) and assumes a single main camera which simplifies the code.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crest
{
    
    internal static class PreparedReflections
    {
        private static volatile RenderTexture _currentreflectiontexture = null;
        private static volatile int _referenceCameraInstanceId = -1;
        private static volatile KeyValuePair<int, RenderTexture>[] _collection = new KeyValuePair<int, RenderTexture>[0];

        public static RenderTexture GetRenderTexture(int camerainstanceid)
        {
            if (camerainstanceid == _referenceCameraInstanceId)
                return _currentreflectiontexture;

            // Prevent crash if somebody change collection now in over thread, useless in unity now
            var currentcollection = _collection;
            for (int i = 0; i < currentcollection.Length; i++)
            {
                if (currentcollection[i].Key == camerainstanceid)
                {
                    var texture = currentcollection[i].Value;
                    _currentreflectiontexture = texture;
                    _referenceCameraInstanceId = camerainstanceid;
                    return texture;
                }
            }
            return null;
        }

        // Remove element if exists
        public static void Remove(int camerainstanceid)
        {
            if (!GetRenderTexture(camerainstanceid)) return;
            _collection = _collection.Where(e => e.Key != camerainstanceid).ToArray(); //rebuild array without element
            _currentreflectiontexture = null;
            _referenceCameraInstanceId = -1;
        }

        public static void Register(int instanceId, RenderTexture reflectionTexture)
        {
            var currentcollection = _collection;
            for (var i = 0; i < currentcollection.Length; i++)
            {
                if (currentcollection[i].Key == instanceId)
                {
                    currentcollection[i] = new KeyValuePair<int, RenderTexture>(instanceId, reflectionTexture);
                    return;
                }
            }
            // Rebuild with new element if not found
            _collection = currentcollection
                .Append(new KeyValuePair<int, RenderTexture>(instanceId, reflectionTexture)).ToArray();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InitStatics()
        {
            // Init here from 2019.3 onwards
            _currentreflectiontexture = null;
            _referenceCameraInstanceId = -1;
            _collection = new KeyValuePair<int, RenderTexture>[0];
        }
    }

    /// <summary>
    /// Attach to a camera to generate a reflection texture which can be sampled in the ocean shader.
    /// </summary>
    [AddComponentMenu(Internal.Constants.MENU_PREFIX_SCRIPTS + "Ocean Planar Reflections")]
    public class OceanPlanarReflection : MonoBehaviour
    {
        /// <summary>
        /// The version of this asset. Can be used to migrate across versions. This value should
        /// only be changed when the editor upgrades the version.
        /// </summary>
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _version = 0;
#pragma warning restore 414

        [SerializeField] LayerMask _reflectionLayers = 1;
        [SerializeField] bool _disableOcclusionCulling = true;
        [SerializeField] int _textureSize = 256;
        [SerializeField] float _clipPlaneOffset = 0.07f;
        [SerializeField] bool _physcialCamera = false;
        [SerializeField] bool _hdr = true;
        [SerializeField] bool _stencil = false;
        [SerializeField] bool _hideCameraGameobject = true;
        bool _allowMSAA = false;           //allow MSAA on reflection camera
        [SerializeField] float _farClipPlane = 1000;             //far clip plane for reflection camera on all layers
        [SerializeField] CameraClearFlags _clearFlags = CameraClearFlags.Color;

        /// <summary>
        /// Refresh reflection every x frames(1-every frame)
        /// </summary>
        [SerializeField] int RefreshPerFrames = 1;

        /// <summary>
        /// To relax OceanPlanarReflection refresh to different frames need to set different values for each script
        /// </summary>
        [SerializeField] int _frameRefreshOffset = 0;

        RenderTexture _reflectionTexture;

        Camera _camViewpoint;
        Skybox _camViewpointSkybox;
        Camera _camReflections;
        Skybox _camReflectionsSkybox;

        private long _lastRefreshOnFrame = -1;

        const int CULL_DISTANCE_COUNT = 32;
        float[] _cullDistances = new float[CULL_DISTANCE_COUNT];

        CommandBuffer commandBuffer;
        CullingResults cullingResults;
        static ShaderTagId unlitShaderTagId,
                           forwardShaderTagId,
                           deferredShaderTagId;
        static int colorId = Shader.PropertyToID("_CameraColorAttachmentA"),
                   depthId = Shader.PropertyToID("_CameraDepthAttachmentA");

        private void OnEnable()
        {

            if (commandBuffer == null)
            {
                commandBuffer = new CommandBuffer();
                commandBuffer.name = "Planar Reflection";
            }

            RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        }

        private void Start()
        {
            if (OceanRenderer.Instance == null)
            {
                enabled = false;
                return;
            }

            _camViewpoint = GetComponent<Camera>();

            if(commandBuffer == null)
            {
                commandBuffer = new CommandBuffer();
                commandBuffer.name = "Planar Reflection";
            }

            if (!_camViewpoint)
            {
                Debug.LogWarning("Crest: Disabling planar reflections as no camera found on gameobject to generate reflection from.", this);
                enabled = false;
                return;
            }
            _camViewpointSkybox = _camViewpoint?.GetComponent<Skybox>();

            unlitShaderTagId = new ShaderTagId("MelodyUnlit");
            forwardShaderTagId = new ShaderTagId("MelodyForward");
            deferredShaderTagId = new ShaderTagId("MelodyDeferred");

            // This is anyway called in OnPreRender, but was required here as there was a black reflection
            // for a frame without this earlier setup call.
            CreateWaterObjects(_camViewpoint);

#if UNITY_EDITOR
            if (!OceanRenderer.Instance.OceanMaterial.IsKeywordEnabled("_PLANARREFLECTIONS_ON"))
            {
                Debug.LogWarning("Crest: Planar reflections are not enabled on the current ocean material and will not be visible.", this);
            }
#endif
        }

        public void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _camViewpoint)
                return;


            if (!RequestRefresh(Time.renderedFrameCount))
                return; // Skip if not need to refresh on this frame

            if (OceanRenderer.Instance == null)
            {
                return;
            }

            CreateWaterObjects(_camViewpoint);

            if (!_camReflections)
            {
                return;
            }

            // Find out the reflection plane: position and normal in world space
            Vector3 planePos = OceanRenderer.Instance.Root.position;
            Vector3 planeNormal = Vector3.up;

            UpdateCameraModes(_camViewpoint);

            // Reflect camera around reflection plane
            float d = -Vector3.Dot(planeNormal, planePos) - _clipPlaneOffset;
            Vector4 reflectionPlane = new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, d);

            Matrix4x4 reflection = Matrix4x4.zero;
            CalculateReflectionMatrix(ref reflection, reflectionPlane);
            Vector3 newpos = reflection.MultiplyPoint(_camViewpoint.transform.position);
            _camReflections.worldToCameraMatrix = _camViewpoint.worldToCameraMatrix * reflection;

            // Setup oblique projection matrix so that near plane is our reflection
            // plane. This way we clip everything below/above it for free.
            Vector4 clipPlane = CameraSpacePlane(_camReflections, planePos, planeNormal, 1.0f);
            _camReflections.projectionMatrix = _camViewpoint.CalculateObliqueMatrix(clipPlane);

            // Set custom culling matrix from the current camera
            _camReflections.cullingMatrix = _camViewpoint.projectionMatrix * _camViewpoint.worldToCameraMatrix;

            _camReflections.targetTexture = _reflectionTexture;

            // Invert culling because view is mirrored
            bool oldCulling = GL.invertCulling;
            GL.invertCulling = !oldCulling;

            _camReflections.transform.position = newpos;
            Vector3 euler = _camViewpoint.transform.eulerAngles;
            _camReflections.transform.eulerAngles = new Vector3(-euler.x, euler.y, euler.z);
            _camReflections.cullingMatrix = _camReflections.projectionMatrix * _camReflections.worldToCameraMatrix;

            ForceDistanceCulling(_farClipPlane);

            RenderSingleCamera(context, _camReflections, colorId, depthId, false, false, false, _hdr);

            GL.invertCulling = oldCulling;

            // Remember this frame as last refreshed
            Refreshed(Time.renderedFrameCount);
        }

        bool RequestRefresh(long frame)
        {
            if (_lastRefreshOnFrame <= 0 || RefreshPerFrames < 2)
            {
                //not refreshed before or refresh every frame, not check frame counter
                return true;
            }
            return Math.Abs(_frameRefreshOffset) % RefreshPerFrames == frame % RefreshPerFrames;
        }

        void Refreshed(long currentframe)
        {
            _lastRefreshOnFrame = currentframe;
        }

        /// <summary>
        /// Limit render distance for reflection camera for first 32 layers
        /// </summary>
        /// <param name="farClipPlane">reflection far clip distance</param>
        private void ForceDistanceCulling(float farClipPlane)
        {
            if (_cullDistances == null || _cullDistances.Length != CULL_DISTANCE_COUNT)
                _cullDistances = new float[CULL_DISTANCE_COUNT];
            for (var i = 0; i < _cullDistances.Length; i++)
            {
                // The culling distance
                _cullDistances[i] = farClipPlane;
            }
            _camReflections.layerCullDistances = _cullDistances;
            _camReflections.layerCullSpherical = true;
        }

        void UpdateCameraModes(Camera currentCamera)
        {
            if (_physcialCamera)
            {
                _camReflections.usePhysicalProperties = true;
                //TODO: only support vertical fit for now
                _camReflections.gateFit = currentCamera.gateFit;
                _camReflections.sensorSize = currentCamera.sensorSize;
                _camReflections.lensShift = currentCamera.lensShift;
                _camReflections.focalLength = currentCamera.focalLength;
            }

            // Set water camera to clear the same way as current camera
            _camReflections.renderingPath = _camViewpoint.renderingPath;
            _camReflections.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camReflections.clearFlags = _clearFlags;

            if (_clearFlags == CameraClearFlags.Skybox)
            {
                if (!_camViewpointSkybox || !_camViewpointSkybox.material)
                {
                    _camReflectionsSkybox.enabled = false;
                }
                else
                {
                    _camReflectionsSkybox.enabled = true;
                    _camReflectionsSkybox.material = _camViewpointSkybox.material;
                }
            }

            // Update other values to match current camera.
            // Even if we are supplying custom camera&projection matrices,
            // some of values are used elsewhere (e.g. skybox uses far plane).

            _camReflections.farClipPlane = _camViewpoint.farClipPlane;
            _camReflections.nearClipPlane = _camViewpoint.nearClipPlane;
            _camReflections.orthographic = _camViewpoint.orthographic;
            _camReflections.fieldOfView = _camViewpoint.fieldOfView;
            _camReflections.orthographicSize = _camViewpoint.orthographicSize;
            _camReflections.allowMSAA = _allowMSAA;
            _camReflections.aspect = _camViewpoint.aspect;
            _camReflections.useOcclusionCulling = !_disableOcclusionCulling && _camViewpoint.useOcclusionCulling;
        }

        // On-demand create any objects we need for water
        void CreateWaterObjects(Camera currentCamera)
        {
            // Reflection render texture
            if (!_reflectionTexture || _reflectionTexture.width != _textureSize)
            {
                if (_reflectionTexture)
                {
                    DestroyImmediate(_reflectionTexture);
                }

                var format = _hdr ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
                Debug.Assert(SystemInfo.SupportsRenderTextureFormat(format), "Crest: The graphics device does not support the render texture format " + format.ToString());
                _reflectionTexture = new RenderTexture(_textureSize, _textureSize, _stencil ? 24 : 16, format)
                {
                    name = "__WaterReflection" + GetHashCode(),
                    isPowerOfTwo = true,
                };
                _reflectionTexture.Create();
                PreparedReflections.Register(currentCamera.GetHashCode(), _reflectionTexture);
            }

            // Camera for reflection
            if (!_camReflections)
            {
                GameObject go = new GameObject("Water Reflect Cam");
                _camReflections = go.AddComponent<Camera>();
                _camReflections.enabled = false;
                _camReflections.transform.position = transform.position;
                _camReflections.transform.rotation = transform.rotation;
                _camReflections.cullingMask = _reflectionLayers;
                _camReflectionsSkybox = _camReflections.gameObject.AddComponent<Skybox>();
                _camReflections.cameraType = CameraType.Reflection;

                if (_hideCameraGameobject)
                {
                    go.hideFlags = HideFlags.HideAndDontSave;
                }
            }
        }

        bool Cull(ScriptableRenderContext context, Camera camera, float maxShadowDistance)
        {
            if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
            {
                p.shadowDistance = Mathf.Min(camera.farClipPlane, maxShadowDistance);
                cullingResults = context.Cull(ref p);
                return true;
            }
            return false;
        }

        void RenderSingleCamera(ScriptableRenderContext context, Camera camera, int colorID, int depthId, bool useDynamicBatching, bool useInstancing, bool useLightsPerObject, bool useHDR)
        {
            if (!Cull(context,camera, 30f))
            {
                return;
            }
            string SampleName = commandBuffer.name;
            commandBuffer.BeginSample(SampleName);
            context.SetupCameraProperties(camera);
            CameraClearFlags flags = camera.clearFlags;
            //always clear depth and color to be guaranteed to cover previous data, unless a sky box is used
            if (flags > CameraClearFlags.Color)
            {
                flags = CameraClearFlags.Color;
            }
            //use HDR or not
            //the reason why frameBuffer get darker when using HDR is linear color data that default HDR RT format stored, are incorrectly displayed in sRGB
            commandBuffer.GetTemporaryRT(colorID, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            commandBuffer.GetTemporaryRT(depthId, camera.pixelWidth, camera.pixelHeight, 32, FilterMode.Point, RenderTextureFormat.Depth);
            //NOTE : order makes sense
            commandBuffer.SetRenderTarget(colorID,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                depthId,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            commandBuffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, flags == CameraClearFlags.Color, flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
            context.ExecuteCommandBuffer(commandBuffer);
            commandBuffer.Clear();
            //per object light will miss some lighting but sometimes it is not neccessary to calculate all light for one fragment
            PerObjectData lightsPerObjectFlags = useLightsPerObject ? PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None;
            var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
            var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
            {
                enableDynamicBatching = useDynamicBatching,
                enableInstancing = useInstancing,
                perObjectData = PerObjectData.Lightmaps |
                PerObjectData.LightProbe |
                PerObjectData.LightProbeProxyVolume |
                PerObjectData.ShadowMask |
                PerObjectData.OcclusionProbe |
                PerObjectData.OcclusionProbeProxyVolume |
                PerObjectData.ReflectionProbes |
                lightsPerObjectFlags
            };
            //set draw settings pass, index : 0, pass : MelodyUnlit; index : 1, pass: MelodyUnlit
            //if (useDeferLighting)
            {
                drawingSettings.SetShaderPassName(1, deferredShaderTagId);
            }
            //else
            //{
            //    drawingSettings.SetShaderPassName(1, forwardShaderTagId);
            //}
            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

            sortingSettings.criteria = SortingCriteria.CommonTransparent;
            drawingSettings.sortingSettings = sortingSettings;
            filteringSettings.renderQueueRange = RenderQueueRange.transparent;
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
            commandBuffer.EndSample(SampleName);
            context.ExecuteCommandBuffer(commandBuffer);
            commandBuffer.Clear();
            commandBuffer.BeginSample(SampleName);
            commandBuffer.Blit(colorID, camera.targetTexture);
            //commandBuffer.ReleaseTemporaryRT(colorID);
            //commandBuffer.ReleaseTemporaryRT(depthId);
            commandBuffer.EndSample(SampleName);
            context.ExecuteCommandBuffer(commandBuffer);
            commandBuffer.Clear();
            context.Submit();
        }

        // Given position/normal of the plane, calculates plane in camera space.
        Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
        {
            Vector3 offsetPos = pos + normal * _clipPlaneOffset;
            Matrix4x4 m = cam.worldToCameraMatrix;
            Vector3 cpos = m.MultiplyPoint(offsetPos);
            Vector3 cnormal = m.MultiplyVector(normal).normalized * sideSign;
            return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        }

        // Calculates reflection matrix around the given plane
        static void CalculateReflectionMatrix(ref Matrix4x4 reflectionMat, Vector4 plane)
        {
            reflectionMat.m00 = (1F - 2F * plane[0] * plane[0]);
            reflectionMat.m01 = (-2F * plane[0] * plane[1]);
            reflectionMat.m02 = (-2F * plane[0] * plane[2]);
            reflectionMat.m03 = (-2F * plane[3] * plane[0]);

            reflectionMat.m10 = (-2F * plane[1] * plane[0]);
            reflectionMat.m11 = (1F - 2F * plane[1] * plane[1]);
            reflectionMat.m12 = (-2F * plane[1] * plane[2]);
            reflectionMat.m13 = (-2F * plane[3] * plane[1]);

            reflectionMat.m20 = (-2F * plane[2] * plane[0]);
            reflectionMat.m21 = (-2F * plane[2] * plane[1]);
            reflectionMat.m22 = (1F - 2F * plane[2] * plane[2]);
            reflectionMat.m23 = (-2F * plane[3] * plane[2]);

            reflectionMat.m30 = 0F;
            reflectionMat.m31 = 0F;
            reflectionMat.m32 = 0F;
            reflectionMat.m33 = 1F;
        }

        private void OnDisable()
        {
            if (_camViewpoint != null)
            {
                PreparedReflections.Remove(_camViewpoint.GetHashCode());
            }

            // Cleanup all the objects we possibly have created
            if (_reflectionTexture)
            {
                Destroy(_reflectionTexture);
                _reflectionTexture = null;
            }
            if (_camReflections)
            {
                Destroy(_camReflections.gameObject);
                _camReflections = null;
            }

            if (commandBuffer != null)
            {
                commandBuffer.Release();
                commandBuffer = null;
            }

            RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        }
    }
}