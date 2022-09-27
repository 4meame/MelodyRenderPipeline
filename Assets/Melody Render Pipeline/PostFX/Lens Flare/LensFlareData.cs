using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

[System.Serializable]
public enum LensFlareDistribution
{
    Uniform,
    Curve,
    Random
}


public class LensFlareDataElement {

}

[CreateAssetMenu(menuName = "Rendering/LensFlare")]
public class LensFlareData : ScriptableObject {
    public LensFlareData() {
        elements = null;
    }
    public LensFlareDataElement[] elements;
}
