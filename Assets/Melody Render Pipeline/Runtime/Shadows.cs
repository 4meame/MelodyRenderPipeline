using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class Shadows {
    const string bufferName = "Shadows";
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };
    ScriptableRenderContext context;
    CullingResults cullingResults;
    ShadowSettings settings;

    const int maxShadowedDirectionalLightCount = 4, maxShadowOtherLightCount = 16;
    const int maxCascadeCount = 4;
    int ShadowedDirectionalLightCount, ShadowedOtherLightCount;
    struct ShadowedDirectionalLight {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }
    ShadowedDirectionalLight[] shadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

    struct ShadowedOtherLight {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float normalBias;
        public bool isPoint;
    }
    ShadowedOtherLight[] shadowedOtherLights = new ShadowedOtherLight[maxShadowOtherLightCount];

    static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
               dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
               otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas"),
               otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices"),
               otherShadowTilesId= Shader.PropertyToID("_OtherShadowTiles"),
               cascadeCountId = Shader.PropertyToID("_CascadeCount"),
               cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
               cascadeDataId = Shader.PropertyToID("_CascadeData"),
               shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade"),
               //used for turning off clamping when packing isn't appropriate 
               shadowPancakingId = Shader.PropertyToID("_ShadowPancaking"),
               //used for PCF
               shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");

    static Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascadeCount],
                       otherShadowMatrices = new Matrix4x4[maxShadowOtherLightCount];
    static Vector4[] cascadeCullingSpheres = new Vector4[maxCascadeCount],
                     cascadeData = new Vector4[maxCascadeCount],
                     otherShadowTiles = new Vector4[maxShadowOtherLightCount];
    static string[] dirShadowFilterKeywords = { "_DIRECTIONAL_PCF3", "_DIRECTIONAL_PCF5", "_DIRECTIONAL_PCF7" },
                    cascadeBlendKeywords = { "_CASCADE_BLEND_SOFT", "_CASCADE_BLEND_DITHER" },
                    otherShadowFilterKeywords = { "_OTHER_PCF3", "_OTHER_PCF5", "_OTHER_PCF7" };
    Vector4 atlasSizes;
    #region shadow mask
    static string[] shadowMaskKeywords = { "_SHADOW_MASK_ALWAYS", "_SHADOW_MASK_DISTANCE" };
    bool useShadowMask;
    #endregion
    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings) {
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;
        ShadowedDirectionalLightCount = 0;
        ShadowedOtherLightCount = 0;
        #region shadow mask
        useShadowMask = false;
        #endregion
    }

    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    //figure which light gets shadows
    // Vector3 ---->> Vector4 (public Vector3 ReserveDirectionalShadows(Light light, int visibleLightIndex) {
    // We store shadow mask light index in the forth component
    public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex) {
        //keep track how many light have been reserved, ignore light that has no shadow
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount
            && light.shadows != LightShadows.None && light.shadowStrength > 0f) {
            #region shadow mask
            //shadow mask light index
            float maskChannel = -1;
            //baked data store in each light
            LightBakingOutput lightBakingOutput = light.bakingOutput;
            if (lightBakingOutput.lightmapBakeType == LightmapBakeType.Mixed && lightBakingOutput.mixedLightingMode == MixedLightingMode.Shadowmask) {
                useShadowMask = true;
                maskChannel = lightBakingOutput.occlusionMaskChannel;
            }
            //consider light shadow get be culled
            if (!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds bounds)) {
                return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
            }
            #endregion
                shadowedDirectionalLights[ShadowedDirectionalLightCount] = new ShadowedDirectionalLight { visibleLightIndex = visibleLightIndex, slopeScaleBias = light.shadowBias, nearPlaneOffset = light.shadowNearPlane };
            //multiply tiles by the cascade count
            return new Vector4(light.shadowStrength, settings.directional.cascadeCount * ShadowedDirectionalLightCount++, light.shadowNormalBias, maskChannel);
        }
        return new Vector4(0f, 0f, 0f, -1f);
    }

    public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex) {
        if (light.shadows == LightShadows.None || light.shadowStrength <= 0f) {
            return new Vector4(0f, 0f, 0f, -1f);
        }
        #region shadow mask
        float maskChannel = -1f;
        LightBakingOutput lightBakingOutput = light.bakingOutput;
        if (lightBakingOutput.lightmapBakeType == LightmapBakeType.Mixed && lightBakingOutput.mixedLightingMode == MixedLightingMode.Shadowmask) {
            useShadowMask = true;
            maskChannel = lightBakingOutput.occlusionMaskChannel;
        }
        bool isPoint = light.type == LightType.Point;
        //point lights aren't limited to a cone, we need to render the shadow to a cube map, done by treating 1 point light as if it were 6 light source rendering all six faces separately
        //we can support for up to 2 realtime point lights as the would take 12 of 16 available tiles.
        int newLightCount = ShadowedOtherLightCount + (isPoint ? 6 : 1);
        //negative strength means that use shadow mask
        if (newLightCount >= maxShadowOtherLightCount || !cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds bounds)) {
            return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
        }
        #endregion
        shadowedOtherLights[ShadowedOtherLightCount] = new ShadowedOtherLight { visibleLightIndex = visibleLightIndex, slopeScaleBias = light.shadowBias, normalBias = light.shadowNormalBias, isPoint = isPoint };
        Vector4 data = new Vector4(light.shadowStrength, ShadowedOtherLightCount, isPoint ? 1f : 0f, maskChannel);
        ShadowedOtherLightCount = newLightCount;
        return data;
    }

    public void Render() {
        if (ShadowedDirectionalLightCount > 0) {
            RenderDirectionalShadows();
        } else {
            //no dir light set the dummy map
            buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }
        if (ShadowedOtherLightCount > 0) {
            RenderOtherShadows();
        } else {
            //no dir light set the dummy map
            buffer.SetGlobalTexture(otherShadowAtlasId, dirShadowAtlasId);
        }

        #region shadow mask
        //we can enable shadow mask in Render(), no matter there is real time shadows, shadow map isn't realtime
        buffer.BeginSample(bufferName);
        //shadow mask or distance shadow mask
        SetKeywords(shadowMaskKeywords, useShadowMask ? QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 : -1);
        //Moved from the RenderDirectionalShadows(), cause we need the fade data even if there is no directional light in the scene
        buffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount);
        //avoid divisions in GPU, cascade fade formula : (1 - d2/r2) / (1 - (1 - f2)
        float f = 1f - settings.directional.cascadeFade;
        buffer.SetGlobalVector(shadowDistanceFadeId, new Vector4(1f / settings.maxDistance, 1f / settings.distanceFade, 1f / (1f - f * f), 0f));
        buffer.SetGlobalVector(shadowAtlasSizeId, atlasSizes);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
        #endregion
    }

    //Render Directional Shadows
    void RenderDirectionalShadows() {
        int atlasSize = (int)settings.directional.atlasSize;
        atlasSizes.x = atlasSize;
        atlasSizes.y = 1f / atlasSize;
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        //instrcut the GPU render this texture, identifying a render texture and how its data should be loaded and stored
        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId, 1f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();
        //when lights are more than one, split the atlas in 4 tiles in case shadow maps are rendered superimposed
        //when using cascades shadow, multiplying tiles by cascade count, which tiles are more than 2, euqal to 4
        int tiles = ShadowedDirectionalLightCount * settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;
        for (int i = 0; i < ShadowedDirectionalLightCount; i++) {
            RenderDirectionalShadows(i, split, tileSize);
        }
        ////Move it to Render()
        //buffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount);
        buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
        ////avoid divisions in GPU, cascade fade formula : (1 - d2/r2) / (1 - (1 - f2), Move it to Render()
        //float f = 1f - settings.directional.cascadeFade;
        //buffer.SetGlobalVector(shadowDistanceFadeId, new Vector4(1f / settings.maxDistance, 1f / settings.distanceFade, 1f / (1f - f * f), 0f));
        //Move it to Render()
        //buffer.SetGlobalVector(shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize, 0f, 0f));
        SetKeywords(dirShadowFilterKeywords, (int)settings.directional.filter - 1);
        SetKeywords(cascadeBlendKeywords, (int)settings.directional.cascadeBlend -1);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    //Render per directional shadow
    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        ShadowedDirectionalLight light = shadowedDirectionalLights[index];
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = settings.directional.CascadeRatios;
        //use for culling shadow casters from larger cascades which can be coverd by smaller cascades
        float cullingFactor =  Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);
        //draw shadow maps per cascade for light
        for (int i = 0; i < cascadeCount; i++) {
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.visibleLightIndex, i, cascadeCount, ratios, tileSize, light.nearPlaneOffset,
                                   out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);
            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            shadowSettings.splitData = splitData;
            //only assign it for the first light, all light has the same cascades
            if (index == 0) {
                SetCascadeData(i, splitData.cullingSphere, tileSize);
            }
            int tileIndex = tileOffset + i;
            //conversion matrix from world space to light space, set atlas tile and store atlas matrices
            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrices(projectionMatrix * viewMatrix, SetTileViewport(tileIndex, split, tileSize), split);
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            //set slope bias
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);
        }
    }

    //Render Other Shadows
    void RenderOtherShadows() {
        int atlasSize = (int)settings.other.atlasSize;
        atlasSizes.z = atlasSize;
        atlasSizes.w = 1f / atlasSize;
        buffer.GetTemporaryRT(otherShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        //instrcut the GPU render this texture, identifying a render texture and how its data should be loaded and stored
        buffer.SetRenderTarget(otherShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId, 0f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();
        //when lights are more than one, split the atlas in 4 tiles in case shadow maps are rendered superimposed
        //when using cascades shadow, multiplying tiles by cascade count, which tiles are more than 2, euqal to 4
        int tiles = ShadowedOtherLightCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;
        for (int i = 0; i < ShadowedOtherLightCount;) {
            if (shadowedOtherLights[i].isPoint)
            {
                RnederPointShadows(i, split, tileSize);
                i += 6;
            } else {
                RnederSpotShadows(i, split, tileSize);
                i += 1;
            }
        }
        buffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices);
        buffer.SetGlobalVectorArray(otherShadowTilesId, otherShadowTiles);
        SetKeywords(otherShadowFilterKeywords, (int)settings.other.filter - 1);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    void RnederSpotShadows(int index, int split, int tileSize) {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(light.visibleLightIndex, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData);
        shadowSettings.splitData = splitData;
        //to match the perspective projection, knowing detail is TODO
        float texelSize = 2f / (tileSize * projMatrix.m00);
        float filterSize = texelSize * ((float)settings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.1412136f;
        Vector2 offset = SetTileViewport(index, split, tileSize);
        SetOtherTilesData(index, offset, 1f / split, bias);
        otherShadowMatrices[index] = ConvertToAtlasMatrices(projMatrix * viewMatrix, offset, split);
        buffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
        buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
        ExecuteBuffer();
        context.DrawShadows(ref shadowSettings);
        buffer.SetGlobalDepthBias(0f, 0f);
    }

    void RnederPointShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        //The field of view for cubemap faces is always 90°, thus the world-space tile size at distance 1 is always 2, no need to calculate every iteration
        float texelSize = 2f / (tileSize );
        float filterSize = texelSize * ((float)settings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.1412136f;
        float fovBias = Mathf.Atan(1f + bias + filterSize) * Mathf.Rad2Deg * 2f - 90f;
        //calculate 6 cubefaces
        for (int i = 0; i < 6; i++) {
            cullingResults.ComputePointShadowMatricesAndCullingPrimitives(light.visibleLightIndex, (CubemapFace)i, fovBias, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData);
            //manually filps everything upside dont ithe atlas at second time, avoid leaking, knowing detail is TODO
            viewMatrix.m11 = -viewMatrix.m11;
            viewMatrix.m12 = -viewMatrix.m12;
            viewMatrix.m13 = -viewMatrix.m13;
            shadowSettings.splitData = splitData;
            int tileIndex = index + i;
            Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
            SetOtherTilesData(tileIndex, offset, 1f / split, bias);
            otherShadowMatrices[tileIndex] = ConvertToAtlasMatrices(projMatrix * viewMatrix, offset, split);
            buffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);
        }
    }

    void SetOtherTilesData (int index, Vector2 offset, float scale, float bias) {
        float border = atlasSizes.w * 0.5f;
        Vector4 data = Vector4.zero;
        data.x = offset.x * scale + border;
        data.y = offset.y * scale + border;
        data.z = scale - border - border;
        data.w = bias;
        otherShadowTiles[index] = data;
    }

    void SetKeywords(string[] keywords, int enableIndex) {
        //int enableIndex = (int)settings.directional.filter - 1;
        for (int i = 0; i < keywords.Length; i++) {
            if (enableIndex == i)
            {
                buffer.EnableShaderKeyword(keywords[i]);
            } else {
                buffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }

    Vector2 SetTileViewport(int index, int split, float tileSize) {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
        return offset;
    }

    void SetCascadeData(int index, Vector4 cullingSphere, float tileSize) {
        //bias offset equal to world-space texel size
        float texelSize = 2f * cullingSphere.w / tileSize;
        //match bias for PCF fitler size
        float filterSize = texelSize * (1f + (float)settings.directional.filter);
        //avoid sampling regon outside of the cascade's culling sphere
        cullingSphere.w -= filterSize;
        //store square radius, so do not have to calculate it in shader
        cullingSphere.w *= cullingSphere.w;
        cascadeCullingSpheres[index] = cullingSphere;
        //store 1 diveded radius, so do not have to calculate it in shader
        cascadeData[index] = new Vector4(1f / cascadeCullingSpheres[index].w, filterSize * 1.4142136f, 0f, 0f);
    }

    Matrix4x4 ConvertToAtlasMatrices(Matrix4x4 m, Vector2 offset, int split) {
        if (SystemInfo.usesReversedZBuffer) {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        float scale = 1f / split;
        //take apart matrices multiplication for convenience
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);
        return m;
    }

    public void CleanUp() {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        if (ShadowedOtherLightCount > 0) {
            buffer.ReleaseTemporaryRT(otherShadowAtlasId);
        }
        ExecuteBuffer();
    }
}
