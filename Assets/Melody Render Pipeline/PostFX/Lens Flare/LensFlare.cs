using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System;

[ExecuteAlways]
public class LensFlare : MonoBehaviour {
    [SerializeField]
    LensFlareData m_LensFlareData = null;
    public LensFlareData lensFlareData {
        get {
            return m_LensFlareData;
        }
        set {
            m_LensFlareData = value;
            OnValidate();
        }
    }
    [Header("General")]
    [Min(0.0f)]
    public float intensity = 1.0f;
    [Min(0.0f)]
    public float scale = 1.0f;
    public bool attenuationByLightShape = true;
    public float maxAttenuationDistance = 100.0f;
    public AnimationCurve distanceAttenuationCurve = new AnimationCurve(new Keyframe(0.0f, 1.0f), new Keyframe(1.0f, 0.0f));
    public float maxAttenuationScale = 100.0f;
    public AnimationCurve scaleeAttenuationCurve = new AnimationCurve(new Keyframe(0.0f, 1.0f), new Keyframe(1.0f, 0.0f));
    //attenuation used radially, which allow for instance to enable flare only on the edge of the screen
    public AnimationCurve radialScreenAttenuationCurve = new AnimationCurve(new Keyframe(0.0f, 1.0f), new Keyframe(1.0f, 0.0f));
    [Header("Occlusion")]
    public bool useOcclusion = true;
    [Min(0.0f)]
    public float occlusionRadius = 0.5f;
    [Range(1, 64)]
    public int sampleCount = 32;
    //Z Occlusion Offset allow us to offset the plane where the disc of occlusion is place(closer to camera), value on world space.
    public float occlusionOffset = 0.05f;
    public bool allowOffScreen = false;
    //This is an arbitrary number, but must be kept constant so the occlusion radius for direct lights is consistent regardless of near / far clip plane configuration.
    static float sCelestialAngularRadius = 1.0f * Mathf.PI / 180.0f;

    //this is used for directional lights which require to have consistent occlusion radius regardless of the near/farplane configuration.
    public float CelestialProjectedOcclusionRadius(Camera mainCam) {
        float projectedRadius = (float)Math.Tan(sCelestialAngularRadius) * mainCam.farClipPlane;
        return occlusionRadius * projectedRadius;
    }

    void OnEnable() {
        if (lensFlareData) {
            LensFlareCommon.Instance.AddData(this);
        } else {
            LensFlareCommon.Instance.RemoveData(this);
        }
    }

    void OnDisable() {
        LensFlareCommon.Instance.RemoveData(this);
    }

    void OnValidate() {
        if (isActiveAndEnabled && lensFlareData != null) {
            LensFlareCommon.Instance.AddData(this);
        } else {
            LensFlareCommon.Instance.RemoveData(this);
        }
    }
}
