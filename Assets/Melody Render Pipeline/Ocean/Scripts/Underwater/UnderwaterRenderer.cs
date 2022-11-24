// Crest Ocean System

// Copyright 2021 Wave Harmonic Ltd

namespace Crest
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Rendering;

    /// <summary>
    /// Underwater Renderer. If a camera needs to go underwater it needs to have this script attached. This adds
    /// fullscreen passes and should only be used if necessary. This effect disables itself when camera is not close to
    /// the water volume.
    ///
    /// For convenience, all shader material settings are copied from the main ocean shader.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu(Internal.Constants.MENU_PREFIX_SCRIPTS + "Underwater Renderer")]
    [HelpURL(Internal.Constants.HELP_URL_BASE_USER + "underwater.html" + Internal.Constants.HELP_URL_RP)]
    public partial class UnderwaterRenderer : MonoBehaviour
    {
        /// <summary>
        /// The version of this asset. Can be used to migrate across versions. This value should
        /// only be changed when the editor upgrades the version.
        /// </summary>
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _version = 0;
#pragma warning restore 414

        internal const string k_KeywordVolume = "CREST_WATER_VOLUME";
        internal const string k_KeywordVolume2D = "CREST_WATER_VOLUME_2D";
        internal const string k_KeywordVolumeHasBackFace = "CREST_WATER_VOLUME_HAS_BACKFACE";

        // The underlying value matches UnderwaterRenderer.EffectPass.
        // :UnderwaterRenderer.Mode
        public enum Mode
        {
            // Infinite water as a full-screen triangle.
            [InspectorNameAttribute("Full-Screen")]
            FullScreen,
            // Portal to infinite water rendered from front faces of geometry.
            Portal,
            // Volume of water rendered from front faces of geometry only. Back faces used for depth. Camera cannot see
            // the effect from within the volume.
            Volume,
            // Volume of water rendered using front faces, back faces and full-screen triangle. Camera can see effect
            // from within the volume.
            [InspectorNameAttribute("Volume (Fly-Through)")]
            VolumeFlyThrough,
        }

        [SerializeField]
        [Tooltip("Rendering mode of the underwater effect (and ocean). See the documentation for more details.")]
        internal Mode _mode;
        public static bool IsCullable
        {
            get
            {
                return Instance != null && Instance._mode == Mode.FullScreen;
            }
        }

        // This adds an offset to the cascade index when sampling ocean data, in effect smoothing/blurring it. Default
        // to shifting the maximum amount (shift from lod 0 to penultimate lod - dont use last lod as it cross-fades
        // data in/out), as more filtering was better in testing.
        [SerializeField, Range(0, LodDataMgr.MAX_LOD_COUNT - 2)]
        [Tooltip("How much to smooth ocean data such as water depth, light scattering, shadowing. Helps to smooth flickering that can occur under camera motion.")]
        internal int _filterOceanData = LodDataMgr.MAX_LOD_COUNT - 2;

        [SerializeField]
        [Tooltip("Add a meniscus to the boundary between water and air.")]
        internal bool _meniscus = true;
        public bool IsMeniscusEnabled => _meniscus;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Scales the depth fog density. Useful to reduce the intensity of the depth fog when underwater water only.")]
        float _depthFogDensityFactor = 1f;
        public static float DepthFogDensityFactor
        {
            get
            {
                if (Instance != null)
                {
                    return Instance._depthFogDensityFactor;
                }

                return 1f;
            }
        }

        [Header("Geometry")]

        [SerializeField, Predicated("_mode", inverted: false, Mode.FullScreen), DecoratedField]
        [Tooltip("Mesh to use to render the underwater effect.")]
        internal MeshFilter _volumeGeometry;

        [SerializeField, Predicated("_mode", inverted: true, Mode.Portal), DecoratedField]
        [Tooltip("If enabled, the back faces of the mesh will be used instead of the front faces.")]
        bool _invertCulling = false;


        [Header("Shader API")]

        [SerializeField]
        [Tooltip("Renders the underwater effect before the transparent pass (instead of after). So one can apply the underwater fog themselves to transparent objects. Cannot be changed at runtime.")]
        bool _enableShaderAPI = false;
        public bool EnableShaderAPI { get => _enableShaderAPI; set => _enableShaderAPI = value; }

        [SerializeField, Predicated("_enableShaderAPI")]
        internal LayerMask _transparentObjectLayers;


        [Header("Advanced")]

        [SerializeField]
        [Tooltip("Copying params each frame ensures underwater appearance stays consistent with ocean material params. Has a small overhead so should be disabled if not needed.")]
        internal bool _copyOceanMaterialParamsEachFrame = true;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Adjusts the far plane for horizon line calculation. Helps with horizon line issue.")]
        internal float _farPlaneMultiplier = 0.68f;

        [Space(10)]

        [SerializeField]
        internal DebugFields _debug = new DebugFields();
        [System.Serializable]
        public class DebugFields
        {
            public bool _viewOceanMask = false;
            public bool _disableOceanMask = false;
            public bool _viewStencil = false;
            public bool _disableHeightAboveWaterOptimization = false;
            public bool _disableArtifactCorrection = false;
        }

        internal Camera _camera;
        bool _firstRender = true;

        internal bool UseStencilBufferOnMask => _mode != Mode.FullScreen;
        internal bool UseStencilBufferOnEffect => _mode == Mode.VolumeFlyThrough;

#if UNITY_EDITOR
        List<Camera> _editorCameras = new List<Camera>();
#endif

        bool _currentEnableShaderAPI;

        // Use instance to denote whether this is active or not. Only one camera is supported.
        public static UnderwaterRenderer Instance { get; private set; }

        // SRP version needs access to this externally, hence public get
        public CommandBuffer BufUnderwaterMask { get; private set; }
        public CommandBuffer BufUnderwaterEffect { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InitStatics()
        {
            Instance = null;
        }

        public bool IsActive
        {
            get
            {
                if (OceanRenderer.Instance == null)
                {
                    return false;
                }

                // Only run optimisation in play mode otherwise shared height above water will cause rendering to be
                // skipped for other cameras which could be confusing. This issue will also be present in play mode but
                // game view camera is always dominant camera which is less confusing.
                if (Application.isPlaying && !_debug._disableHeightAboveWaterOptimization && _mode == Mode.FullScreen && OceanRenderer.Instance.ViewerHeightAboveWater > 2f)
                {
                    return false;
                }

                return true;
            }
        }

        void OnEnable()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }

#if UNITY_EDITOR
            if (!Validate(OceanRenderer.Instance, ValidatedHelper.DebugLog))
            {
                enabled = false;
                return;
            }
#endif

            // Setup here because it is the same across pipelines.
            if (_cameraFrustumPlanes == null)
            {
                _cameraFrustumPlanes = GeometryUtility.CalculateFrustumPlanes(_camera);
            }

            Instance = this;

            if (BufUnderwaterMask == null)
            {
                SetUpMaskCommandBuffers();
            }

            if (BufUnderwaterEffect == null)
            {
                SetUpEffectCommandBuffers();
            }

            CleanUpCommandBuffers();

            UnderwaterMaskPass.Enable(this);
            UnderwaterEffectPass.Enable(this);

        }

        void OnDisable()
        {
            CleanUpCommandBuffers();

            UnderwaterMaskPass.Disable();
            UnderwaterEffectPass.Disable();

            Instance = null;
        }

        void OnDestroy()
        {
            CleanUpCommandBuffers();

            UnderwaterEffectPass.CleanUp();
            DestroyImmediate(_underwaterEffectMaterial.material);
            UnderwaterMaskPass.CleanUp();
            DestroyImmediate(_oceanMaskMaterial.material);
        }

        void SetUpMaskCommandBuffers()
        {
            BufUnderwaterMask = new CommandBuffer();
            BufUnderwaterMask.name = "Underwater Mask";
        }

        void SetUpEffectCommandBuffers()
        {
            BufUnderwaterEffect = new CommandBuffer();
            BufUnderwaterEffect.name = "Underwater Effect";
        }

        void CleanUpCommandBuffers()
        {
            if (BufUnderwaterMask != null)
            {
                BufUnderwaterMask.Clear();
            }

            if (BufUnderwaterEffect != null)
            {
                BufUnderwaterEffect.Clear();
            }
        }
    }

#if UNITY_EDITOR
    // Edit Mode.
    public partial class UnderwaterRenderer
    {
        /// <summary>
        /// Whether the effect is active for the editor only camera (eg scene view). You can check game preview cameras,
        /// but do not check game cameras.
        /// </summary>
        internal bool IsActiveForEditorCamera(Camera camera)
        {
            // Skip rendering altogether if proxy plane is being used.
            if (OceanRenderer.Instance == null || (!Application.isPlaying && OceanRenderer.Instance._showOceanProxyPlane))
            {
                // These two clears will only run for built-in renderer as they'll be null for SRPs.
                _oceanMaskCommandBuffer?.Clear();
                _underwaterEffectCommandBuffer?.Clear();
                return false;
            }

            // Only use for scene and game preview cameras.
            if (camera.cameraType != CameraType.SceneView && !Helpers.IsPreviewOfGameCamera(camera))
            {
                return false;
            }

            return true;
        }

        internal static bool IsFogEnabledForEditorCamera(Camera camera)
        {
            // Check if scene view has disabled fog rendering.
            if (camera.cameraType == CameraType.SceneView)
            {
                var sceneView = EditorHelpers.EditorHelpers.GetSceneViewFromSceneCamera(camera);
                // Skip rendering if fog is disabled or for some reason we could not find the scene view.
                if (sceneView == null || !sceneView.sceneViewState.fogEnabled)
                {
                    return false;
                }
            }

            return true;
        }
    }

    // Validation.
    public partial class UnderwaterRenderer : IValidated
    {
        public bool Validate(OceanRenderer ocean, ValidatedHelper.ShowMessage showMessage)
        {
            var isValid = true;

            if (_mode != Mode.FullScreen && _volumeGeometry == null)
            {
                showMessage
                (
                    $"<i>{_mode}</i> mode requires a <i>Mesh Filter</i> be set to <i>Volume Geometry</i>.",
                    "Change <i>Mode</i> to <i>FullScreen</i>.",
                    ValidatedHelper.MessageType.Error, this,
                    (SerializedObject so) =>
                    {
                        so.FindProperty("_mode").enumValueIndex = (int)Mode.FullScreen;
                    }
                );

                isValid = false;
            }

            if (ocean != null && ocean.OceanMaterial != null)
            {
                var material = ocean.OceanMaterial;

                if (!material.IsKeywordEnabled("_UNDERWATER_ON"))
                {
                    showMessage
                    (
                        $"<i>Underwater</i> is not enabled on material <i>{material.name}</i>. " +
                        "The underside of the ocean surface will not be rendered correctly.",
                        $"Enable <i>Underwater</i> on <i>{material.name}</i>.",
                        ValidatedHelper.MessageType.Warning, material,
                        (material) => ValidatedHelper.FixSetMaterialOptionEnabled(material, "_UNDERWATER_ON", "_Underwater", enabled: true)
                    );
                }

                if (material.GetFloat("_CullMode") == (int)CullMode.Back)
                {
                    showMessage
                    (
                        $"<i>Cull Mode</i> is set to <i>Back</i> on material <i>{material.name}</i>. " +
                        "The underside of the ocean surface will not be rendered.",
                        $"Set <i>Cull Mode</i> to <i>Off</i> (or <i>Front</i>) on <i>{material.name}</i>.",
                        ValidatedHelper.MessageType.Warning, material,
                        (material) =>
                        {
                            ValidatedHelper.FixSetMaterialIntProperty(material, "Cull Mode", "_CullMode", (int)CullMode.Off);
                        }
                    );
                }

                if (_mode != Mode.FullScreen && showMessage != ValidatedHelper.DebugLog)
                {
                    showMessage
                    (
                        $"The <i>{_mode}</i> mode is incompatible with SSAO. " +
                        "We cannot detect whether SSAO is enabled or not so this is general advice and can be ignored.",
                        "",
                        ValidatedHelper.MessageType.Info, this
                    );
                }


                if (_enableShaderAPI && ocean.OceanMaterial.IsKeywordEnabled("_SUBSURFACESHALLOWCOLOUR_ON"))
                {
                    showMessage
                    (
                        "<i>Enable Shader API</i> does not support the <i>Scatter Colour Shallow</i> option",
                        $"Disable <i>Scatter Colour Shallow</i> on the ocean material <i>{material.name}</i>.",
                        ValidatedHelper.MessageType.Error, material
                    );
                }
            }

            return isValid;
        }
    }

    [CustomEditor(typeof(UnderwaterRenderer)), CanEditMultipleObjects]
    public class UnderwaterRendererEditor : ValidatedEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Scene view rendering can be enabled/disabled with the scene view fog toggle in the scene view command bar.", MessageType.Info);
            EditorGUILayout.Space();

            base.OnInspectorGUI();
        }
    }
#endif
}
