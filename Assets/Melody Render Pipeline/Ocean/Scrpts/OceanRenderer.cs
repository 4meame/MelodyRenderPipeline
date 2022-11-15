using System.Collections.Generic;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Crest.Internal;
using System.Linq;
#if UNITY_EDITOR
using UnityEngine.Rendering;
using UnityEditor;
#endif

#if !UNITY_2020_3_OR_NEWER
#error This version of Crest requires Unity 2020.3 or later.
#endif

namespace Crest
{
    /// <summary>
    /// The main script for the ocean system. Attach this to a GameObject to create an ocean. This script initializes the various data types and systems
    /// and moves/scales the ocean based on the viewpoint. It also hosts a number of global settings that can be tweaked here.
    /// </summary>
    [ExecuteDuringEditMode(ExecuteDuringEditModeAttribute.Include.None)]
    [SelectionBase]
    [AddComponentMenu(Internal.Constants.MENU_PREFIX_SCRIPTS + "Ocean Renderer")]
    [HelpURL(Constants.HELP_URL_GENERAL)]
    public partial class OceanRenderer : CustomMonoBehaviour
    {
        /// <summary>
        /// The version of this asset. Can be used to migrate across versions. This value should
        /// only be changed when the editor upgrades the version.
        /// </summary>
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _version = 0;
#pragma warning restore 414

        [Tooltip("Base wind speed in km/h. Controls wave conditions. Can be overridden on ShapeGerstner components."), Range(0, 150f, power: 2f)]
        public float _globalWindSpeed = 150f;

        [Tooltip("The viewpoint which drives the ocean detail. Defaults to the camera."), SerializeField]
        Transform _viewpoint;
        public Transform Viewpoint
        {
            get
            {
#if UNITY_EDITOR
                if (_followSceneCamera)
                {
                    var sceneViewCamera = EditorHelpers.EditorHelpers.GetActiveSceneViewCamera();
                    if (sceneViewCamera != null)
                    {
                        return sceneViewCamera.transform;
                    }
                }
#endif
                if (_viewpoint != null)
                {
                    return _viewpoint;
                }

                // Even with performance improvements, it is still good to cache whenever possible.
                var camera = ViewCamera;

                if (camera != null)
                {
                    return camera.transform;
                }

                return null;
            }
            set
            {
                _viewpoint = value;
            }
        }

        [Tooltip("The camera which drives the ocean data. Defaults to main camera."), SerializeField]
        Camera _camera;
        public Camera ViewCamera
        {
            get
            {
#if UNITY_EDITOR
                if (_followSceneCamera)
                {
                    var sceneViewCamera = EditorHelpers.EditorHelpers.GetActiveSceneViewCamera();
                    if (sceneViewCamera != null)
                    {
                        return sceneViewCamera;
                    }
                }
#endif

                if (_camera != null)
                {
                    return _camera;
                }

                // Unity has greatly improved performance of this operation in 2019.4.9.
                return Camera.main;
            }
            set
            {
                _camera = value;
            }
        }

        [Tooltip("The height where detail is focused is smoothed to avoid popping which is undesireable after a teleport. Threshold is in Unity units."), SerializeField]
        float _teleportThreshold = 10f;
        float _teleportTimerForHeightQueries = 0f;
        bool _isFirstFrameSinceEnabled = true;
        internal bool _hasTeleportedThisFrame = false;
        Vector3 _oldViewerPosition = Vector3.zero;

        public Transform Root { get; private set; }

        // does not respond to _timeProvider changing in inspector

        // Loosely a stack for time providers. The last TP in the list is the active one. When a TP gets
        // added to the stack, it is bumped to the top of the list. When a TP is removed, all instances
        // of it are removed from the stack. This is less rigid than a real stack which would be harder
        // to use as users have to keep a close eye on the order that things are pushed/popped.
        public List<ITimeProvider> _timeProviderStack = new List<ITimeProvider>();

        [Tooltip("Optional provider for time, can be used to hard-code time for automation, or provide server time. Defaults to local Unity time."), SerializeField]
        TimeProviderBase _timeProvider = null;
        public ITimeProvider TimeProvider
        {
            get => _timeProviderStack[_timeProviderStack.Count - 1];
        }

        // Put a time provider at the top of the stack
        public void PushTimeProvider(ITimeProvider tp)
        {
            Debug.Assert(tp != null, "Crest: Null time provider pushed");

            // Remove any instances of it already in the stack
            PopTimeProvider(tp);

            // Add it to the top
            _timeProviderStack.Add(tp);
        }

        // Remove a time provider from the stack
        public void PopTimeProvider(ITimeProvider tp)
        {
            Debug.Assert(tp != null, "Crest: Null time provider popped");

            _timeProviderStack.RemoveAll(candidate => candidate == tp);
        }

        public float CurrentTime => TimeProvider.CurrentTime;
        public float DeltaTime => TimeProvider.DeltaTime;
        public float DeltaTimeDynamics => TimeProvider.DeltaTimeDynamics;

        [Tooltip("The primary directional light. Required if shadowing is enabled.")]
        public Light _primaryLight;
        [Tooltip("If Primary Light is not set, search the scene for all directional lights and pick the brightest to use as the sun light.")]
        [SerializeField, Predicated("_primaryLight", true), DecoratedField]
        bool _searchForPrimaryLightOnStartup = true;

        [Header("Ocean Params")]
        [SerializeField, Tooltip("Material to use for the ocean surface")]
        internal Material _material = null;
        public Material OceanMaterial { get => _material; set => _material = value; }

        [Tooltip("Use prefab for water tiles. The only requirements are that the prefab must contain a MeshRenderer at the root and not a MeshFilter or OceanChunkRenderer. MR values will be overwritten where necessary and the prefabs are linked in edit mode.")]
        public GameObject _waterTilePrefab;

        [System.Obsolete("Use the _layer field instead."), HideInInspector, SerializeField]
        string _layerName = "";
        [System.Obsolete("Use the Layer property instead.")]
        public string LayerName => _layerName;

        [HelpBox("The <i>Layer</i> property needs to migrate the deprecated <i>Layer Name</i> property before it can be used. Please see the bottom of this component for a fix button.", HelpBoxAttribute.MessageType.Warning, HelpBoxAttribute.Visibility.PropertyDisabled, order = 1)]
        [Tooltip("The ocean tile renderers will have this layer.")]
        [SerializeField, Predicated("_layerName", inverted: true), Layer]
        int _layer = 4; // Water
        public int Layer => _layer;

        [SerializeField, Delayed, Tooltip("Multiplier for physics gravity."), Range(0f, 10f)]
        float _gravityMultiplier = 1f;
        public float Gravity => _gravityMultiplier * Physics.gravity.magnitude;

        [Tooltip("Whether 'Water Body' components will cull the ocean tiles. Disable if you want to use the 'Water Body' 'Material Override' feature and still have an ocean.")]
        public bool _waterBodyCulling = true;

        [Header("Detail Params")]
        [Delayed, Tooltip("The smallest scale the ocean can be."), SerializeField]
        float _minScale = 8f;

        [Delayed, Tooltip("The largest scale the ocean can be (-1 for unlimited)."), SerializeField]
        float _maxScale = 256f;

        [Tooltip("Drops the height for maximum ocean detail based on waves. This means if there are big waves, max detail level is reached at a lower height, which can help visual range when there are very large waves and camera is at sea level."), SerializeField, Range(0f, 1f)]
        float _dropDetailHeightBasedOnWaves = 0.2f;

        [SerializeField, Delayed, Tooltip("Resolution of ocean LOD data. Use even numbers like 256 or 384.")]
        int _lodDataResolution = 384;
        public int LodDataResolution => _lodDataResolution;

        [SerializeField, Delayed, Tooltip("How much of the water shape gets tessellated by geometry. If set to e.g. 4, every geometry quad will span 4x4 LOD data texels. Use power of 2 values like 1, 2, 4...")]
        int _geometryDownSampleFactor = 2;

        [SerializeField, Tooltip("Number of ocean tile scales/LODs to generate."), Range(2, LodDataMgr.MAX_LOD_COUNT)]
        int _lodCount = 7;

        [Tooltip("Applied to the extents' far vertices to make the larger. Increase if the extents do not reach the horizon or you see the underwater effect at the horizon.")]
        [SerializeField, Delayed]
        internal float _extentsSizeMultiplier = 100f;

        [Header("Simulation Params")]
        [Embedded]
        public SimSettingsAnimatedWaves _simSettingsAnimatedWaves;

        [Tooltip("Water depth information used for shallow water, shoreline foam, wave attenuation, among others."), SerializeField]
        bool _createSeaFloorDepthData = true;
        public bool CreateSeaFloorDepthData => _createSeaFloorDepthData;
        [Predicated("_createSeaFloorDepthData"), Embedded]
        public SimSettingsSeaFloorDepth _simSettingsSeaFloorDepth;

        [Tooltip("Simulation of foam created in choppy water and dissipating over time."), SerializeField]
        bool _createFoamSim = true;
        public bool CreateFoamSim => _createFoamSim;
        [Predicated("_createFoamSim"), Embedded]
        public SimSettingsFoam _simSettingsFoam;

        [Tooltip("Dynamic waves generated from interactions with objects such as boats."), SerializeField]
        bool _createDynamicWaveSim = false;
        public bool CreateDynamicWaveSim => _createDynamicWaveSim;
        [Predicated("_createDynamicWaveSim"), Embedded]
        public SimSettingsWave _simSettingsDynamicWaves;
        public SimSettingsWave SimSettingsDynamicWaves { get => _simSettingsDynamicWaves; set => _simSettingsDynamicWaves = value; }

        [Tooltip("Horizontal motion of water body, akin to water currents."), SerializeField]
        bool _createFlowSim = false;
        public bool CreateFlowSim => _createFlowSim;
        [Predicated("_createFlowSim"), Embedded]
        public SimSettingsFlow _simSettingsFlow;

        [Tooltip("Shadow information used for lighting water."), SerializeField]
        bool _createShadowData = false;
        public bool CreateShadowData => _createShadowData;
        [Predicated("_createShadowData"), Embedded]
        public SimSettingsShadow _simSettingsShadow;

        [Tooltip("Clip surface information for clipping the ocean surface."), SerializeField]
        bool _createClipSurfaceData = false;
        public bool CreateClipSurfaceData => _createClipSurfaceData;

        [Predicated("_createClipSurfaceData"), Embedded]
        public SimSettingsClipSurface _simSettingsClipSurface;

        public enum DefaultClippingState
        {
            NothingClipped,
            EverythingClipped,
        }
        [Tooltip("Whether to clip nothing by default (and clip inputs remove patches of surface), or to clip everything by default (and clip inputs add patches of surface).")]
        [Predicated("_createClipSurfaceData"), DecoratedField]
        public DefaultClippingState _defaultClippingState = DefaultClippingState.NothingClipped;

        [Tooltip("Albedo - a colour layer composited onto the water surface."), SerializeField]
        bool _createAlbedoData = false;
        public bool CreateAlbedoData => _createAlbedoData;
        [Predicated("_createAlbedoData"), Embedded]
        public SimSettingsAlbedo _settingsAlbedo;

        [Header("Advanced")]
        [SerializeField]
        [Tooltip("How Crest should handle self-intersections of the ocean surface caused by choppy waves which can cause a flipped underwater effect. Automatic will disable the fix if portals/volumes are used which is the recommend setting.")]
        SurfaceSelfIntersectionFixMode _surfaceSelfIntersectionFixMode = SurfaceSelfIntersectionFixMode.Automatic;
        public enum SurfaceSelfIntersectionFixMode
        {
            Off,
            On,
            Automatic,
        }

        [SerializeField, Range(UNDERWATER_CULL_LIMIT_MINIMUM, UNDERWATER_CULL_LIMIT_MAXIMUM)]
        [Tooltip("Proportion of visibility below which ocean will be culled underwater. The larger the number, the closer to the camera the ocean tiles will be culled.")]
        public float _underwaterCullLimit = 0.001f;
        internal const float UNDERWATER_CULL_LIMIT_MINIMUM = 0.000001f;
        internal const float UNDERWATER_CULL_LIMIT_MAXIMUM = 0.01f;

        [Header("Edit Mode Params")]
        [SerializeField]
#pragma warning disable 414
        internal bool _showOceanProxyPlane = false;
#pragma warning restore 414
#if UNITY_EDITOR
        GameObject _proxyPlane;
        const string kProxyShader = "Hidden/Crest/OceanProxy";
#endif

        [Tooltip("Sets the update rate of the ocean system when in edit mode. Can be reduced to save power."), Range(0f, 60f), SerializeField]
#pragma warning disable 414
        float _editModeFPS = 30f;
#pragma warning restore 414

        [Tooltip("Move ocean with Scene view camera if Scene window is focused."), SerializeField, Predicated("_showOceanProxyPlane", true), DecoratedField]
#pragma warning disable 414
        bool _followSceneCamera = true;
#pragma warning restore 414

        [Tooltip("Whether height queries are enabled in edit mode."), SerializeField]
#pragma warning disable 414
        bool _heightQueries = true;
#pragma warning restore 414

        [Header("Server Settings")]
        [Tooltip("Emulate batch mode which models running without a display (but with a GPU available). Equivalent to running standalone build with -batchmode argument."), SerializeField]
        bool _forceBatchMode = false;
        [Tooltip("Emulate running on a client without a GPU. Equivalent to running standalone with -nographics argument."), SerializeField]
        bool _forceNoGPU = false;

        [Header("Debug Params")]

        [Tooltip("Attach debug gui that adds some controls and allows to visualise the ocean data."), SerializeField]
        bool _attachDebugGUI = false;
        [Tooltip("Move ocean with viewpoint.")]
        bool _followViewpoint = true;
        [Tooltip("Set the ocean surface tiles hidden by default to clean up the hierarchy.")]
        public bool _hideOceanTileGameObjects = true;
        [HideInInspector, Tooltip("Whether to generate ocean geometry tiles uniformly (with overlaps).")]
        public bool _uniformTiles = false;
        [HideInInspector, Tooltip("Disable generating a wide strip of triangles at the outer edge to extend ocean to edge of view frustum.")]
        public bool _disableSkirt = false;

        /// <summary>
        /// Current ocean scale (changes with viewer altitude).
        /// </summary>
        public float Scale { get; private set; }
        public float CalcLodScale(float lodIndex) => Scale * Mathf.Pow(2f, lodIndex);
        public float CalcGridSize(int lodIndex) => CalcLodScale(lodIndex) / LodDataResolution;

        /// <summary>
        /// The ocean changes scale when viewer changes altitude, this gives the interpolation param between scales.
        /// </summary>
        public float ViewerAltitudeLevelAlpha { get; private set; }

        /// <summary>
        /// Sea level is given by y coordinate of GameObject with OceanRenderer script.
        /// </summary>
        public float SeaLevel => Root.position.y;

        [HideInInspector] public LodTransform _lodTransform;
        [HideInInspector] public LodDataMgrAnimWaves _lodDataAnimWaves;
        [HideInInspector] public LodDataMgrSeaFloorDepth _lodDataSeaDepths;
        [HideInInspector] public LodDataMgrClipSurface _lodDataClipSurface;
        [HideInInspector] public LodDataMgrDynWaves _lodDataDynWaves;
        [HideInInspector] public LodDataMgrFlow _lodDataFlow;
        [HideInInspector] public LodDataMgrFoam _lodDataFoam;
        [HideInInspector] public LodDataMgrShadow _lodDataShadow;
        [HideInInspector] public LodDataMgrAlbedo _lodDataAlbedo;

        /// <summary>
        /// The number of LODs/scales that the ocean is currently using.
        /// </summary>
        public int CurrentLodCount => _lodTransform != null ? _lodTransform.LodCount : _lodCount;

        /// <summary>
        /// Vertical offset of camera vs water surface.
        /// </summary>
        public float ViewerHeightAboveWater { get; private set; }

        /// <summary>
        /// Depth Fog Density with factor applied for underwater.
        /// </summary>
        public Vector3 UnderwaterDepthFogDensity { get; private set; }

        List<LodDataMgr> _lodDatas = new List<LodDataMgr>();
        List<OceanChunkRenderer> _oceanChunkRenderers = new List<OceanChunkRenderer>();
        public List<OceanChunkRenderer> Tiles => _oceanChunkRenderers;

        /// <summary>
        /// Smoothly varying version of viewer height to combat sudden changes in water level that are possible
        /// when there are local bodies of water
        /// </summary>
        float _viewerHeightAboveWaterSmooth = 0f;

        SampleHeightHelper _sampleHeightHelper = new SampleHeightHelper();

        public static OceanRenderer Instance { get; private set; }

        /// <summary>
        /// Is runtime environment without graphics card
        /// </summary>
        public static bool RunningWithoutGPU
        {
            get
            {
                var noGPU = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
                var emulateNoGPU = (Instance != null ? Instance._forceNoGPU : false);
                return noGPU || emulateNoGPU;
            }
        }

        /// <summary>
        /// Is runtime environment without graphics card
        /// </summary>
        public static bool RunningHeadless => Application.isBatchMode || (Instance != null ? Instance._forceBatchMode : false);

        // We are computing these values to be optimal based on the base mesh vertex density.
        float _lodAlphaBlackPointFade;
        float _lodAlphaBlackPointWhitePointFade;

        bool _canSkipCulling = false;

        public static readonly int sp_oceanCenterPosWorld = Shader.PropertyToID("_OceanCenterPosWorld");
        public static readonly int sp_crestTime = Shader.PropertyToID("_CrestTime");
        public static readonly int sp_perCascadeInstanceData = Shader.PropertyToID("_CrestPerCascadeInstanceData");
        public static readonly int sp_CrestPerCascadeInstanceDataSource = Shader.PropertyToID("_CrestPerCascadeInstanceDataSource");
        public static readonly int sp_cascadeData = Shader.PropertyToID("_CrestCascadeData");
        public static readonly int sp_CrestCascadeDataSource = Shader.PropertyToID("_CrestCascadeDataSource");
        public static readonly int sp_CrestLodChange = Shader.PropertyToID("_CrestLodChange");
        readonly static int sp_meshScaleLerp = Shader.PropertyToID("_MeshScaleLerp");
        readonly static int sp_sliceCount = Shader.PropertyToID("_SliceCount");
        readonly static int sp_clipByDefault = Shader.PropertyToID("_CrestClipByDefault");
        readonly static int sp_lodAlphaBlackPointFade = Shader.PropertyToID("_CrestLodAlphaBlackPointFade");
        readonly static int sp_lodAlphaBlackPointWhitePointFade = Shader.PropertyToID("_CrestLodAlphaBlackPointWhitePointFade");
        readonly static int sp_CrestDepthTextureOffset = Shader.PropertyToID("_CrestDepthTextureOffset");
        public static readonly int sp_CrestForceUnderwater = Shader.PropertyToID("_CrestForceUnderwater");

        public static class ShaderIDs
        {
            // Shader properties.
            public static readonly int s_DepthFogDensity = Shader.PropertyToID("_DepthFogDensity");
            public static readonly int s_Diffuse = Shader.PropertyToID("_Diffuse");
            public static readonly int s_DiffuseGrazing = Shader.PropertyToID("_DiffuseGrazing");
            public static readonly int s_DiffuseShadow = Shader.PropertyToID("_DiffuseShadow");
            public static readonly int s_SubSurfaceColour = Shader.PropertyToID("_SubSurfaceColour");
            public static readonly int s_SubSurfaceSun = Shader.PropertyToID("_SubSurfaceSun");
            public static readonly int s_SubSurfaceBase = Shader.PropertyToID("_SubSurfaceBase");
            public static readonly int s_SubSurfaceSunFallOff = Shader.PropertyToID("_SubSurfaceSunFallOff");
        }

#if UNITY_EDITOR
        static float _lastUpdateEditorTime = -1f;
        public static float LastUpdateEditorTime => _lastUpdateEditorTime;
        static int _editorFrames = 0;
#endif

        BuildCommandBuffer _commandbufferBuilder;

        // This must exactly match struct with same name in HLSL
        // :CascadeParams
        public struct CascadeParams
        {
            public Vector2 _posSnapped;
            public float _scale;
            public float _textureRes;
            public float _oneOverTextureRes;
            public float _texelWidth;
            public float _weight;
            public float _maxWavelength;
        }

        public ComputeBuffer _bufCascadeDataTgt;
        public ComputeBuffer _bufCascadeDataSrc;

        // This must exactly match struct with same name in HLSL
        // :PerCascadeInstanceData
        public struct PerCascadeInstanceData
        {
            public float _meshScaleLerp;
            public float _farNormalsWeight;
            public float _geoGridWidth;
            public Vector2 _normalScrollSpeeds;
            // Align to 32 bytes
            public Vector3 __padding;
        }

        public ComputeBuffer _bufPerCascadeInstanceData;
        public ComputeBuffer _bufPerCascadeInstanceDataSource;

        BufferedData<CascadeParams[]> _cascadeParams;
        BufferedData<PerCascadeInstanceData[]> _perCascadeInstanceData;
        public int BufferSize { get; private set; }

    }
}
