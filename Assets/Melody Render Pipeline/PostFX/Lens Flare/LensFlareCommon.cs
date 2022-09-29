using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class LensFlareCommon {
    static LensFlareCommon m_Instance = null;
    static readonly object m_Padlock = new object();
    static List<LensFlare> m_Data = new List<LensFlare>();
    //max occlusion
    public static int maxLensFlareWithOcclusion = 128;
    //occlusion RT temporal filter
    public static int maxLensFlareWithOcclusionTemporalSample = 8;
    //1 : enable temporal merge, 0 : disable temporal merge
    public static int mergeNeeded = 1;
    public static RTHandle occlusionRT = null;
    static int frameIndex = 0;

    private LensFlareCommon() {

    }

    static public void Initialize() {
        if (occlusionRT == null && mergeNeeded > 0)
            //allocating occlusion RT
            occlusionRT = RTHandles.Alloc(width: maxLensFlareWithOcclusion, height: maxLensFlareWithOcclusionTemporalSample + 1 * mergeNeeded, colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat, enableRandomWrite: true, dimension: TextureDimension.Tex2D);
    }

    static public void Dispose() {
        if (occlusionRT != null) {
            RTHandles.Release(occlusionRT);
            occlusionRT = null;
        }
    }

    public static LensFlareCommon Instance {
        get {
            if (m_Instance == null) {
                lock (m_Padlock) {
                    if (m_Instance == null) {
                        m_Instance = new LensFlareCommon();
                    }
                }
            }
            return m_Instance;
        }
    }

    List<LensFlare> Data {
        get {
            return m_Data;
        }
    }

    public List<LensFlare> GetData() {
        return Data;
    }

    public bool IsEmpty() {
        return Data.Count == 0;
    }

    public void AddData(LensFlare data) {
        Debug.Assert(Instance == this, "LensFlareCommon can have only one instance");
        if (!m_Data.Contains(data)) {
            m_Data.Add(data);
        }
    }

    public void RemoveData(LensFlare data) {
        Debug.Assert(Instance == this, "LensFlareCommon can have only one instance");
        if (m_Data.Contains(data)) {
            m_Data.Remove(data);
        }
    }

    static public float ShapeAttenuationPointLight() {
        return 1.0f;
    }

    static public float ShapeAttenuationDirectionLight(Vector3 lightForward, Vector3 eyeToLight) {
        return Mathf.Max(Vector3.Dot(lightForward, eyeToLight), 0.0f);
    }

    static public float ShapeAttenuationSpotConeLight(Vector3 lightForward, Vector3 eyeToLight, float spotAngle, float innerSpotPercent01) {
        float outerDot = Mathf.Max(Mathf.Cos(0.5f * spotAngle * Mathf.Deg2Rad), 0.0f);
        float innerDot = Mathf.Max(Mathf.Cos(0.5f * spotAngle * Mathf.Deg2Rad * innerSpotPercent01), 0.0f);
        float dot = Mathf.Max(Vector3.Dot(lightForward, eyeToLight), 0.0f);
        return Mathf.Clamp01((dot - outerDot) / (innerDot - outerDot));
    }

    static public void ShapeAttenuationSpotCubeLight() {

    }

    static public void ShapeAttenuationPyramidCubeLight() {

    }

    static public void ShapeAttenuationAreaTubeLight() {

    }

    static public void ShapeAttenuationAreaRectangleLight() {

    }

    static public void ShapeAttenuationAreaDiscLight() {

    }

    static public Vector4 GetFlareData0(Vector2 screenPos, Vector2 translationScale, Vector2 rayOff0, Vector2 vLocalScreenRatio, float angleDeg, float position, float angularOffset, Vector2 positionOffset, bool autoRotate) {
        if (!SystemInfo.graphicsUVStartsAtTop) {
            angleDeg *= -1;
            positionOffset.y *= -1;
        }
        float globalCos0 = Mathf.Cos(-angularOffset * Mathf.Deg2Rad);
        float globalSin0 = Mathf.Sin(-angularOffset * Mathf.Deg2Rad);
        Vector2 rayOff = -translationScale * (screenPos + screenPos * (position - 1.0f));
        rayOff = new Vector2(globalCos0 * rayOff.x - globalSin0 * rayOff.y, globalSin0 * rayOff.x + globalCos0 * rayOff.y);
        float rotation = angleDeg;
        rotation += 180.0f;
        if (autoRotate) {
            Vector2 pos = (rayOff.normalized * vLocalScreenRatio) * translationScale;
            rotation += -Mathf.Rad2Deg * Mathf.Atan2(pos.y, pos.x);
        }
        rotation *= Mathf.Deg2Rad;
        float localCos0 = Mathf.Cos(-rotation);
        float localSin0 = Mathf.Sin(-rotation);
        return new Vector4(localCos0, localSin0, positionOffset.x + rayOff0.x * translationScale.x, -positionOffset.y + rayOff0.y * translationScale.y);
    }


}
