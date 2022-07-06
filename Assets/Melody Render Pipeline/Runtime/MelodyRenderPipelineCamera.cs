using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public class MelodyRenderPipelineCamera : MonoBehaviour {
    [SerializeField]
    CameraSettings settings = default;

    //?? ---> settings == null ? settings = new CameraSettings() : settings
    public CameraSettings Settings => settings ?? (settings = new CameraSettings());
}
