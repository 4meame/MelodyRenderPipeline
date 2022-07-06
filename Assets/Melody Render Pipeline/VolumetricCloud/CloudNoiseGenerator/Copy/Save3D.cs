using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Save3D : MonoBehaviour {
    const int threadGroupSize = 32;
    public ComputeShader slicer;

    //save render texture as 3D texture
    public void Save(RenderTexture volumeTexture, string saveName) {
#if UNITY_EDITOR
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        saveName = sceneName + "_" + saveName;
        int resolution = volumeTexture.width;
        //series of slices of the 3D Texture
        Texture2D[] slices = new Texture2D[resolution];
        //resolution decribes how many 2D slices a 3D texture
        slicer.SetInt("resolution", resolution);
        slicer.SetTexture(0, "volumeTexture", volumeTexture);
        for (int layer = 0; layer < resolution; layer++) {
            //width and height of the slice is also the resolution, cause that is a cube texture
            var slice = new RenderTexture(resolution, resolution, 0);
            slice.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            slice.enableRandomWrite = true;
            slice.Create();
            //Set per slice
            slicer.SetTexture(0, "slice", slice);
            slicer.SetInt("layer", layer);
            int numThreadGroups = Mathf.CeilToInt(resolution / (float)threadGroupSize);
            slicer.Dispatch(0, numThreadGroups, numThreadGroups, 1);
            //convert 
            slices[layer] = ConvertFromRenderTexture(slice);
        }
        //resolution : xy, slice : z
        var x = Tex3DFromText2Darray(slices, resolution);
        UnityEditor.AssetDatabase.CreateAsset(x, "Assets/Resources/" + saveName + ".asset");
#endif
    }

    //Get pixels color from one slice which coord is (x,y,n)
    Texture3D Tex3DFromText2Darray(Texture2D[] slices, int resolution) {
        Texture3D tex3D = new Texture3D(resolution, resolution, resolution, TextureFormat.ARGB32, false);
        tex3D.filterMode = FilterMode.Trilinear;
        Color[] outputPixels = tex3D.GetPixels();
        for (int z = 0; z < resolution; z++) {
            Color c = slices[z].GetPixel(0, 0);
            Color[] layerPixels = slices[z].GetPixels();
            for (int x = 0; x < resolution; x++) {
                for (int y = 0; y < resolution; y++) {
                    outputPixels[x + resolution * (y + z * resolution)] = layerPixels[x + y * resolution];
                }
            }
        }
        tex3D.SetPixels(outputPixels);
        tex3D.Apply();
        return tex3D;
    }

    Texture2D ConvertFromRenderTexture(RenderTexture rt) {
        Texture2D output = new Texture2D(rt.width, rt.height);
        RenderTexture.active = rt;
        output.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        output.Apply();
        return output;
    }
}
