using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public enum LensFlareType
{
    Image,
    Circle,
    Polygon
}

[System.Serializable]
public enum LensFlareBlendMode
{
    Additive,
    Screen,
    Premutiply,
    Lerp
}

//defined how we spread the flare element when count > 1
[System.Serializable]
public enum LensFlareDistribution
{
    Uniform,
    Curve,
    Random
}



public class LensFlareDataElement {
    public LensFlareDataElement() {
        visible = true;
        localIntensity = 1.0f;
        position = 0.0f;
        positionOffset = new Vector2(0.0f, 0.0f);
        angularOffset = 0.0f;
        translationScale = new Vector2(1.0f, 1.0f);
        lensFlareTexture = null;
        uniformScale = 1.0f;
        sizeXY = Vector2.one;
        allowMultipleElement = false;
        count = 5;
        rotation = 0.0f;
        tint = new Color(1.0f, 1.0f, 1.0f, 0.5f);
        blendMode = LensFlareBlendMode.Additive;
        autoRotate = false;
        isFoldOpened = true;
        flareType = LensFlareType.Circle;
        distribution = LensFlareDistribution.Uniform;
        lengthSpread = 1f;
        colorGradient = new Gradient();
        colorGradient.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) });
        positionCurve = new AnimationCurve(new Keyframe(0.0f, 0.0f, 1.0f, 1.0f), new Keyframe(1.0f, 1.0f, 1.0f, -1.0f));
        scaleCurve = new AnimationCurve(new Keyframe(0.0f, 1.0f), new Keyframe(1.0f, 1.0f));
        uniformAngleCurve = new AnimationCurve(new Keyframe(0.0f, 0.0f), new Keyframe(1.0f, 0.0f));
        //random
        seed = 0;
        intensityVariation = 0.75f;
        positionVariation = new Vector2(1.0f, 0.0f);
        scaleVariation = 1.0f;
        rotationVariation = 180.0f;
        //distortion
        enableRadialDistortion = false;
        targetSizeDistortion = Vector2.one;
        distortionCurve = new AnimationCurve(new Keyframe(0.0f, 0.0f, 1.0f, 1.0f), new Keyframe(1.0f, 1.0f, 1.0f, -1.0f));
        distortionRelativeToCenter = false;
        //parameters for procedural
        fallOff = 1.0f;
        edgeOffset = 0.1f;
        sdfRoundness = 0.0f;
        sideCount = 6;
        inverseSDF = false;
    }

    public bool visible;
    public float position;
    public Vector2 positionOffset;
    public float angularOffset;
    public Vector2 translationScale;
    [Min(0), SerializeField, FormerlySerializedAs("localIntensity")]
    float m_LocalIntensity;
    public float localIntensity {
        get => m_LocalIntensity;
        set => m_LocalIntensity = Mathf.Max(0, value);
    }
    public Texture lensFlareTexture;
    public float uniformScale;
    public Vector2 sizeXY;
    public bool allowMultipleElement;
    [Min(1), SerializeField, FormerlySerializedAs("count")]
    int m_Count;
    public int count {
        get => m_Count;
        set => m_Count = Mathf.Max(1, value);
    }
    public bool preserveAspectRatio;
    public float rotation;
    public Color tint;
    public LensFlareBlendMode blendMode;
    public bool autoRotate;
    public LensFlareType flareType;
    public bool modulateByLightColor;
    public LensFlareDistribution distribution;
    public float lengthSpread;
    public AnimationCurve positionCurve;
    public AnimationCurve scaleCurve;
    public int seed;
    public Gradient colorGradient;
    [Range(0, 1), SerializeField, FormerlySerializedAs("intensityVariation")]
    float m_IntensityVariation;
    public float intensityVariation {
        get => m_IntensityVariation;
        set => m_IntensityVariation = Mathf.Max(0, value);
    }
    public Vector2 positionVariation;
    public float scaleVariation;
    public float rotationVariation;
    public bool enableRadialDistortion;
    //target size used on the edge of the screen
    public Vector2 targetSizeDistortion;
    public AnimationCurve distortionCurve;
    //if true the distortion is relative to center of the screen otherwise relative to lensFlare source screen position
    public bool distortionRelativeToCenter;
    [Range(0, 1), SerializeField, FormerlySerializedAs("fallOff")]
    float m_FallOff;
    //fall of the gradient used for the procedural flare
    public float fallOff {
        get => m_FallOff;
        set => m_FallOff = Mathf.Clamp01(value);
    }
    [Range(0, 1), SerializeField, FormerlySerializedAs("edgeOffset")]
    float m_EdgeOffset;
    //gradient offset used for the procedural flare
    public float edgeOffset {
        get => m_EdgeOffset;
        set => m_EdgeOffset = Mathf.Clamp01(value);
    }
    [Min(3), SerializeField, FormerlySerializedAs("sideCount")]
    int m_SideCount;
    //side count of the regular polygon generated
    public int sideCount {
        get => m_SideCount;
        set => m_SideCount = Mathf.Max(3, value);
    }

    [Range(0, 1), SerializeField, FormerlySerializedAs("sdfRoundness")]
    float m_SdfRoundness;
    //roundness of the polygon flare (0: Sharp Polygon, 1: Circle).
    public float sdfRoundness {
        get => m_SdfRoundness;
        set => m_SdfRoundness = Mathf.Clamp01(value);
    }
    public bool inverseSDF;
    public float uniformAngle;
    public AnimationCurve uniformAngleCurve;
    [SerializeField]
    bool isFoldOpened;
}

[CreateAssetMenu(menuName = "Rendering/Lens Flare")]
public class LensFlareData : ScriptableObject {
    public LensFlareData() {
        elements = null;
    }
    public LensFlareDataElement[] elements;
}
