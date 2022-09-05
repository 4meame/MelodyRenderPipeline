using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ReconstructionFilter {
    // The maximum length of motion blur, given as a percentage
    // of the screen height. Larger values may introduce artifacts.
    const float maxBlurRadius = 5;
    public Material material;

    public ReconstructionFilter() {
        var shader = Shader.Find("Hidden/Melody RP/Post FX Stack/Motion/Reconstruction");
        material = new Material(shader);
        material.hideFlags = HideFlags.DontSave;
    }


}
