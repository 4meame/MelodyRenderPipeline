using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Volumetric Cloud/Render Settings")]
public class VolumetricCloudSettings : ScriptableObject {
    [SerializeField]
    Shader shader = default;
    [System.NonSerialized]
    Material material;
    public Material Material {
        get {
            if(material == null && shader != null) {
                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave;
            }
            return material;
        }
    }

    public bool enabled;

    public enum SubPixelSize {
        Sub1x1,
        Sub2x2,
        Sub4x4,
        Sub8x8,
    }

    public enum RenderSize {
        CameraSizes,
        FixedSizes
    }

    [Header("Render")]
    public SubPixelSize subPixelSize = SubPixelSize.Sub1x1;
    public int maxIterations = 128;
    public RenderSize renderSize = RenderSize.CameraSizes;
    public int fixedWidth = 0;
    public int fixedHeight = 0;
    [Range(1, 8)]
    public int downsample = 2;
    [Header("Coverage")]
    [Range(0.0f, 1.0f)]
    public float coverageOffsetX;
    [Range(0.0f, 1.0f)]
    public float coverageOffsetY;
    [Range(0.0f, 1.0f)]
    public float horizonCoverageStart = 0.3f;
    [Range(0.0f, 1.0f)]
    public float horizonCoverageEnd = 0.4f;
    [Header("Base Modeling")]
    public float baseScale = 1.0f;
    public Gradient cloudGradient1;
    public Gradient cloudGradient2;
    public Gradient cloudGradient3;
    [Range(0.0f, 1.0f)]
    public float sampleThreshold = 0.05f;
    [Range(0.0f, 1.0f)]
    public float sampleScalar = 1.0f;
    [Range(0.0f, 1.0f)]
    public float bottomFade = 0.3f;
    [Header("Detail Modeling")]
    public float detailScale = 8.0f;
    [Range(0.0f, 1.0f)]
    public float erosionEdgeSize = 0.5f;
    [Range(0.0f, 1.0f)]
    public float cloudDistortion = 0.45f;
    public float cloudDistortionScale = 0.5f;
    [Header("Lighting")]
    public Color cloudBaseColor = new Color32(132, 170, 208, 255);
    public Color cloudTopColor = new Color32(255, 255, 255, 255);
    [Range(0.0f, 5.0f)]
    public float sunScale = 1.0f;
    [Range(0.0f, 5.0f)]
    public float ambientScale = 1.0f;
    [Range(0.0f, 1.0f)]
    public float sunRayLength = 0.08f;
    [Range(0.0f, 1.0f)]
    public float coneRadius = 0.08f;
    [Range(0.0f, 30.0f)]
    public float density = 1.0f;
    [Range(0.0f, 1.0f)]
    public float forwardScatteringG = 0.8f;
    [Range(0.0f, -1.0f)]
    public float backwardScatteringG = -0.5f;
    [Range(0.0f, 1.0f)]
    public float darkOutlineScalar = 1.0f;
    [Header("Animation")]
    [Range(-10.0f, 10.0f)]
    public float speed = 1.0f;
    public Vector2 coverageOffsetPerFrame;
    public Vector3 baseOffsetPerFrame;
    public Vector3 detailOffsetPerFrame;
    [Header("Atmosphere")]
    public Vector3 earthCenter = new Vector3(0, -6371000.0f, 0);
    public float earthRadius = 6371000.0f;
    public bool useCalculatedRadius = false;
    public bool showCalculatedRadius = false;
    public float horizonDistanceOfRadius = 0;
    public float atmosphereStartHeight = 1500.0f;
    public float atmosphereEndHeight = 8000.0f;
    public Vector3 cameraPositionScale = new Vector3(1.0f, 1.0f, 1.0f);
    [Header("Optimization")]
    [Range(0.0f, 1.0f)]
    public float lodDistance = 0.3f;
    [Range(-1.0f, 1.0f)]
    public float horizonLevel = 0.0f;
    [Range(0.0f, 1.0f)]
    public float horizonFade = 0.25f;
    [Range(0.0f, 1.0f)]
    public float horizonFadeStartAlpha = 0.9f;
    [Header("Resources")]
    public Texture3D shapeTexture;
    public Texture3D detailTexture;
    public Texture2D coverageTexture;
    public Texture2D curlTexture;
    public Texture2D blueNoiseTexture;
    public bool debugTexture;

}
