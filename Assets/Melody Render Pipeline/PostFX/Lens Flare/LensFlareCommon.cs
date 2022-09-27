using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LensFlareCommon {
    static LensFlareCommon m_Instance = null;
    static readonly object m_Padlock = new object();

    private LensFlareCommon() {

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

    public void AddData(LensFlare data)
    {

    }

    public void RemoveData(LensFlare data)
    {

    }

    static public Vector4 GetFlareData0(Vector2 screenPos, Vector2 translationScale, Vector2 rayOff0, Vector2 vLocalScreenRatio, float angleDeg, float position, float angularOffset, Vector2 positionOffset, bool autoRotate) {
        return Vector4.zero;
    }
}
