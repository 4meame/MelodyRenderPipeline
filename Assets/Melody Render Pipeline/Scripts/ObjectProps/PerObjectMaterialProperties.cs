using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{
    static int baseMapID = Shader.PropertyToID("_BaseMap"),
               baseColorID = Shader.PropertyToID("_BaseColor"),
               cufoffID = Shader.PropertyToID("_Cutoff"),
               maskMapID = Shader.PropertyToID("_MaskMap"),
               metallicID = Shader.PropertyToID("_Metallic"),
               smoothnessID = Shader.PropertyToID("_Smoothness"),
               emissionColorID = Shader.PropertyToID("_EmissionColor"),
               fresnelId = Shader.PropertyToID("_Fresnel"),
               normalMapId = Shader.PropertyToID("_NormalMap"),
               normalScaleId = Shader.PropertyToID("_NormalScale");
    [SerializeField]
    Texture2D baseMap;
    [SerializeField]
    Texture2D maskMap;
    [SerializeField]
    Texture2D normalMap;
    [SerializeField, ColorUsage(true, true)]
    Color baseColor = Color.white;
    [SerializeField, Range(0f, 1f)]
    float cutoff = 0.5f,
          metallic = 0.0f,
          smoothness = 0.5f,
          fresnel = 1.0f,
          normalScale = 1.0f;
    [SerializeField, ColorUsage(false, true)]
    Color emissionColor = Color.black;
    static MaterialPropertyBlock block;

    void OnValidate() {
        if(block == null) {
            block = new MaterialPropertyBlock();
        }
        block.SetTexture(baseMapID, baseMap == null ? Texture2D.whiteTexture : baseMap);
        block.SetColor(baseColorID, baseColor);
        block.SetFloat(cufoffID, cutoff);
        block.SetTexture(maskMapID, maskMap == null ? Texture2D.whiteTexture : maskMap);
        block.SetFloat(metallicID, metallic);
        block.SetFloat(smoothnessID, smoothness);
        block.SetColor(emissionColorID, emissionColor);
        block.SetFloat(fresnelId, fresnel);
        block.SetTexture(normalMapId, normalMap == null ? Texture2D.normalTexture : normalMap);
        block.SetFloat(normalScaleId, normalScale);
        GetComponent<Renderer>().SetPropertyBlock(block);
    }

    void Awake() {
        OnValidate();
    }
}
