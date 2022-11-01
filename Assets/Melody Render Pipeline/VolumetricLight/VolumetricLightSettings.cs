using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class VolumetricLightSettings {
    public bool enabled;
    public enum Resolution {
        Half,
        Full
    }
    public Resolution resolution = Resolution.Full;
}
