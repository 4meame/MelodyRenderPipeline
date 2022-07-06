using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{
    static int baseColorID = Shader.PropertyToID("_BaseColor"),
               cufoffID = Shader.PropertyToID("_Cutoff"),
               metallicID = Shader.PropertyToID("_Metallic"),
               smoothnessID = Shader.PropertyToID("_Smoothness"),
               emissionColorID = Shader.PropertyToID("_EmissionColor"),
               fresnelId = Shader.PropertyToID("_Fresnel");

    [SerializeField, ColorUsage(true, true)]
    Color baseColor = Color.white;
    [SerializeField, Range(0f, 1f)]
    float cutoff = 0.5f,
          metallic = 0.0f,
          smoothness = 0.5f,
          fresnel = 1.0f;
    [SerializeField, ColorUsage(false, true)]
    Color emissionColor = Color.black;
    static MaterialPropertyBlock block;

    void OnValidate() {
        if(block == null) {
            block = new MaterialPropertyBlock();
        }
        block.SetColor(baseColorID, baseColor);
        block.SetFloat(cufoffID, cutoff);
        block.SetFloat(metallicID, metallic);
        block.SetFloat(smoothnessID, smoothness);
        block.SetColor(emissionColorID, emissionColor);
        block.SetFloat(fresnelId, fresnel);
        GetComponent<Renderer>().SetPropertyBlock(block);
    }

    void Awake() {
        OnValidate();
    }
}
