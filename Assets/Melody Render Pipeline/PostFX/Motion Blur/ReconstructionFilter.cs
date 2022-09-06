using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ReconstructionFilter {
    enum Pass {
        Setup,
        TileMax1,
        TileMax2,
        TileMaxV,
        NeighborMax,
        Reconstruction
    }
    CommandBuffer buffer;
    //the maximum length of motion blur, given as a percentage of the screen height. Larger values may introduce artifacts.
    const float maxBlurRadius = 5;
    Material material;
    //texture format for storing 2D vectors.
    RenderTextureFormat vectorRTFormat = RenderTextureFormat.RGHalf;
    //texture format for storing packed velocity/depth.
    RenderTextureFormat packedRTFormat = RenderTextureFormat.ARGB2101010;

    public ReconstructionFilter(CommandBuffer buffer) {
        this.buffer = buffer;
        var shader = Shader.Find("Hidden/Melody RP/Post FX Stack/Motion/Reconstruction");
        material = new Material(shader);
        material.hideFlags = HideFlags.DontSave;
    }

    public void ProcessImage(float shutterAngle, int sampleCount, RenderTexture source, RenderTexture destination) {
        //calculate the maximum blur radius in pixels.
        var maxBlurPixels = (int)(maxBlurRadius * source.height / 100);
        //calculate the TileMax size, it should be a multiple of 8 and larger than maxBlur.
        var tileSize = ((maxBlurPixels - 1) / 8 + 1) * 8;
        //1st pass packing depth and velocity
        var velocityScale = shutterAngle / 360;
        material.SetFloat("_VelocityScale", velocityScale);
        material.SetFloat("_MaxBlurRadius", maxBlurPixels);
        material.SetFloat("_RcpMaxBlurRadius", 1.0f / maxBlurPixels);
        var VBuffer = GetTemporaryRT(source, 1, packedRTFormat);
        buffer.Blit(null, VBuffer, material, (int)Pass.Setup);
        //2nd pass - 1/2 TileMax filter
        var tile2 = GetTemporaryRT(source, 2, vectorRTFormat);
        buffer.Blit(VBuffer, tile2, material, (int)Pass.TileMax1);
        //3rd pass - 1/2 TileMax filter
        var tile4 = GetTemporaryRT(source, 4, vectorRTFormat);
        buffer.Blit(tile2, tile4, material, (int)Pass.TileMax2);
        ReleaseTemporaryRT(tile2);
        //4th pass - 1/2 TileMax filter
        var tile8 = GetTemporaryRT(source, 8, vectorRTFormat);
        buffer.Blit(tile4, tile8, material, (int)Pass.TileMax2);
        ReleaseTemporaryRT(tile4);
        //5th pass - Last TileMax filter (reduce to tileSize)
        var tileMaxOffs = Vector2.one * (tileSize / 8.0f - 1) * -0.5f;
        material.SetVector("_TileMaxOffs", tileMaxOffs);
        material.SetInt("_TileMaxLoop", tileSize / 8);
        var tile = GetTemporaryRT(source, tileSize, vectorRTFormat);
        buffer.Blit(tile8, tile, material, (int)Pass.TileMaxV);
        ReleaseTemporaryRT(tile8);
        //6th pass - NeighborMax filter
        var neighborMax = GetTemporaryRT(source, tileSize, vectorRTFormat);
        buffer.Blit(tile, neighborMax, material, (int)Pass.NeighborMax);
        ReleaseTemporaryRT(tile);
        //7th pass - Reconstruction pass
        material.SetFloat("_LoopCount", Mathf.Clamp(sampleCount, 2, 64) / 2);
        material.SetTexture("_NeighborMaxTex", neighborMax);
        material.SetTexture("_VelocityTex", VBuffer);
        buffer.Blit(source, destination, material, 5);
        //cleaning up
        ReleaseTemporaryRT(VBuffer);
        ReleaseTemporaryRT(neighborMax);
    }

    RenderTexture GetTemporaryRT(Texture source, int divider, RenderTextureFormat format) {
        var w = source.width / divider;
        var h = source.height / divider;
        var linear = RenderTextureReadWrite.Linear;
        var rt = RenderTexture.GetTemporary(w, h, 0, format, linear);
        rt.filterMode = FilterMode.Point;
        return rt;
    }

    void ReleaseTemporaryRT(RenderTexture rt) {
        RenderTexture.ReleaseTemporary(rt);
    }

}
