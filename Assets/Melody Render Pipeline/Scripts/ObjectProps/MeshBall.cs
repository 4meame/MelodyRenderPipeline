using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MeshBall : MonoBehaviour
{
    static int baseColorID = Shader.PropertyToID("_BaseColor"),
               metallicID = Shader.PropertyToID("_Metallic"),
               smoothnessID = Shader.PropertyToID("_Smoothness");

    [SerializeField]
    Mesh mesh = default;
    [SerializeField]
    Material material = default;

    Matrix4x4[] matrices = new Matrix4x4[1023];
    Vector4[] baseColors = new Vector4[1023];
    float[] metallic = new float[1023],
            smoothness = new float[1023];
    MaterialPropertyBlock block;
    [SerializeField]
    LightProbeProxyVolume lightProbeVolume = null;
    void Awake() {
        for (int i = 0; i < matrices.Length; i++) {
            matrices[i] = Matrix4x4.TRS(Random.insideUnitSphere * 10.0f, Quaternion.Euler(Random.value * 360f, Random.value * 360f, Random.value * 360f), Vector3.one * Random.Range(0.5f, 1.0f));
            baseColors[i] = new Vector4(Random.value, Random.value, Random.value, Random.Range(0.5f, 1.0f));
            metallic[i] = Random.value < 0.25f ? 1f : 0f;
            smoothness[i] = Random.Range(0.05f, 0.95f);
        }
    }

    void Update() {
        if (block == null) {
            block = new MaterialPropertyBlock();
            block.SetVectorArray(baseColorID, baseColors);
            block.SetFloatArray(metallicID, metallic);
            block.SetFloatArray(smoothnessID, smoothness);
            //Support light probes GI for instancing
            //Manually generate light probes position
            if (!lightProbeVolume) {
                var positions = new Vector3[1023];
                for (int i = 0; i < matrices.Length; i++) {
                    positions[i] = matrices[i].GetColumn(3);
                }
                var lightProbes = new SphericalHarmonicsL2[1023];
                #region LPPV Shadow Mask
                var occlusionProbes = new Vector4[1023];
                #endregion
                LightProbes.CalculateInterpolatedLightAndOcclusionProbes(positions, lightProbes, occlusionProbes);
                block.CopySHCoefficientArraysFrom(lightProbes);
                #region LPPV Shadow Mask
                block.CopyProbeOcclusionArrayFrom(occlusionProbes);
                #endregion
            }
        }
        Graphics.DrawMeshInstanced(mesh, 0, material, matrices, 1023, block, ShadowCastingMode.On, true, 0, null, lightProbeVolume ? LightProbeUsage.UseProxyVolume : LightProbeUsage.CustomProvided, lightProbeVolume);
    }
}
