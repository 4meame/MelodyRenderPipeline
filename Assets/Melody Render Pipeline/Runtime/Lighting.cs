using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class Lighting {
    const string bufferName = "Lighting";
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };
    #region Directional Light
    const int maxDirLightCount = 4;
    static int
        dirLightCountId = Shader.PropertyToID("_DirectionLightCount"),
        dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
        dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");
    static Vector4[]
        dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirections = new Vector4[maxDirLightCount],
        dirLightShadowData = new Vector4[maxDirLightCount];
    #endregion
    #region Other Light
    const int maxOtherLightCount = 64;
    static int
        otherLightCountId = Shader.PropertyToID("_OtherLightCount"),
        otherLightColorsId = Shader.PropertyToID("_OtherLightColors"),
        otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions"),
        otherLightDirectionsId = Shader.PropertyToID("_OtherLightDirections"),
        otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles"),
        otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");
    static Vector4[]
        otherLightColors = new Vector4[maxOtherLightCount],
        otherLightPositions = new Vector4[maxOtherLightCount],
        otherLightDirections = new Vector4[maxOtherLightCount],
        otherLightSpotAngles = new Vector4[maxOtherLightCount],
        otherLightShadowData = new Vector4[maxOtherLightCount];
    #endregion
    #region Main Light
    static int
        mainLightIndexId = Shader.PropertyToID("_MainLightIndex"),
        mainLightPositionId = Shader.PropertyToID("_MainLightPosition"),
        mainLightColorId = Shader.PropertyToID("_MainLightColor");
    static VisibleLight[] dirVisibleLights = new VisibleLight[maxDirLightCount];
    static int mainLightIndex = -1;
    static Vector4 mainLightPosition = new Vector4(0.0f, 0.0f, 1.0f, 0.0f);
    public Vector4 MainLightPosition {
        get {
            return mainLightPosition;
        }
    }
    static Color mainLightColor = Color.black;
    #endregion
    static string lightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";
    CullingResults cullingResults;
    Shadows shadows = new Shadows();
    #region Volumetric Data
    //Volumetric Light Data
    static int
        dirLightSampleDataId = Shader.PropertyToID("_DirectionalLightSampleData"),
        dirLightScatterDataId = Shader.PropertyToID("_DirectionalLightScatterData"),
        dirLightNoiseDataId = Shader.PropertyToID("_DirectionalLightNoiseData"),
        dirLightNoiseVelocityId = Shader.PropertyToID("_DirectionalLightNoiseVelocity");
    static Vector4[]
        dirLightSampleData = new Vector4[maxDirLightCount],
        dirLightScatterData = new Vector4[maxDirLightCount],
        dirLightNoiseData = new Vector4[maxDirLightCount],
        dirLightNoiseVelocity = new Vector4[maxDirLightCount];
    static int
        otherLightSampleDataId = Shader.PropertyToID("_OtherLightSampleData"),
        otherLightScatterDataId = Shader.PropertyToID("_OtherLightScatterData"),
        otherLightNoiseDataId = Shader.PropertyToID("_OtherLightNoiseData"),
        otherLightNoiseVelocityId = Shader.PropertyToID("_OtherLightNoiseVelocity");
    static Vector4[]
        otherLightSampleData = new Vector4[maxOtherLightCount],
        otherLightScatterData = new Vector4[maxOtherLightCount],
        otherLightNoiseData = new Vector4[maxOtherLightCount],
        otherLightNoiseVelocity = new Vector4[maxOtherLightCount];
    #endregion

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings, bool useLightsPerObject) {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName);
        shadows.Setup(context, cullingResults, shadowSettings);
        SetUpLights(useLightsPerObject);
        shadows.Render();
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    //unity create a list of all active lights per object, we have to sanitize the list so only non-directional lights remain(for per object light feature
    void SetUpLights(bool useLightsPerObject) {
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
        NativeArray<int> indexMap = useLightsPerObject ? cullingResults.GetLightIndexMap(Allocator.Temp) : default;
        int dirLightCount = 0, otherLightCount = 0;
        int i;
        for (i = 0; i < visibleLights.Length; i++) {
            VisibleLight visibleLight = visibleLights[i];
            int newIndex = -1;
            switch (visibleLight.lightType) {
                case LightType.Point:
                    if (otherLightCount < maxOtherLightCount) {
                        newIndex = otherLightCount;
                        SetUpPointLight(otherLightCount++, i, ref visibleLight);
                    }
                    break;
                case LightType.Directional:
                    if (dirLightCount < maxDirLightCount) {
                        SetUpDirectionalLight(dirLightCount++, i, ref visibleLight);
                    }
                    break;
                case LightType.Spot:
                    if (otherLightCount < maxOtherLightCount) {
                        newIndex = otherLightCount;
                        SetUpSpotLight(otherLightCount++, i, ref visibleLight);
                    }
                    break;
            }
            if (useLightsPerObject) {
                indexMap[i] = newIndex;
            }
        }
        #region PerOjbect Index
        if (useLightsPerObject) {
            for (; i < indexMap.Length; i++) {
                //eliminate the unvisalbe lights
                indexMap[i] = -1;
            }
            cullingResults.SetLightIndexMap(indexMap);
            indexMap.Dispose();
            Shader.EnableKeyword(lightsPerObjectKeyword);
        } else {
            Shader.DisableKeyword(lightsPerObjectKeyword);
        }
        #endregion
        #region Directional Light
        buffer.SetGlobalInt(dirLightCountId, dirLightCount);
        if (dirLightCount > 0) {
            //Vector4
            buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
            buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
            buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
            //volume data
            buffer.SetGlobalVectorArray(dirLightSampleDataId, dirLightSampleData);
            buffer.SetGlobalVectorArray(dirLightScatterDataId, dirLightScatterData);
            buffer.SetGlobalVectorArray(dirLightNoiseDataId, dirLightNoiseData);
            buffer.SetGlobalVectorArray(dirLightNoiseVelocityId, dirLightNoiseVelocity);
        }
        #endregion
        #region Other Light
        buffer.SetGlobalInt(otherLightCountId, otherLightCount);
        if (otherLightCount > 0) {
            //Vector4
            buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
            buffer.SetGlobalVectorArray(otherLightPositionsId, otherLightPositions);
            buffer.SetGlobalVectorArray(otherLightDirectionsId, otherLightDirections);
            buffer.SetGlobalVectorArray(otherLightSpotAnglesId, otherLightSpotAngles);
            buffer.SetGlobalVectorArray(otherLightShadowDataId, otherLightShadowData);
            //volume data
            buffer.SetGlobalVectorArray(otherLightSampleDataId, otherLightSampleData);
            buffer.SetGlobalVectorArray(otherLightScatterDataId, otherLightScatterData);
            buffer.SetGlobalVectorArray(otherLightNoiseDataId, otherLightNoiseData);
            buffer.SetGlobalVectorArray(otherLightNoiseVelocityId, otherLightNoiseVelocity);
        }
        #endregion
        #region Main Light
        if(dirLightCount > 0) {
            mainLightIndex = GetMainLightIndex();
            mainLightPosition = -dirVisibleLights[mainLightIndex].localToWorldMatrix.GetColumn(2);
            mainLightColor = dirVisibleLights[mainLightIndex].finalColor;
        } else {
            mainLightPosition = Vector4.zero;
            mainLightColor = Color.black;
        }
        //in fact the main light index is always 0, built in sorting
        buffer.SetGlobalFloat(mainLightIndexId, mainLightIndex);
        buffer.SetGlobalVector(mainLightPositionId, mainLightPosition);
        buffer.SetGlobalVector(mainLightColorId, mainLightColor);
        #endregion
    }

    static int GetMainLightIndex() {
        float brightestLightIntensity = 0.0f;
        int brightestDirectionalLightIndex = -1;
        for (int i = 0; i < dirVisibleLights.Length; i++) {
            VisibleLight currMainLight = dirVisibleLights[i];
            Light currLight = currMainLight.light;
           //Debug.LogError(currLight.intensity);
            if (currLight == null)
                break;
            //sun is main light for sure
            if (currLight == RenderSettings.sun) {
                return i;
            }
            //In case no sun light is present we will return the brightest directional light
            if (currLight.intensity > brightestLightIntensity) {
                brightestLightIntensity = currLight.intensity;
                brightestDirectionalLightIndex = i;
            }
        }
        return brightestDirectionalLightIndex;
    }

    void SetUpDirectionalLight(int index, int visibleIndex, ref VisibleLight visibleLight) {
        //final color is the color multiplys the intensity
        dirLightColors[index] = visibleLight.finalColor;
        //light forward vector can be found via the third column of the localToWorld Matrix
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        dirLightShadowData[index] = shadows.ReserveDirectionalShadows(visibleLight.light, visibleIndex);
        //main light index calculations
        dirVisibleLights[index] = visibleLight;
        //set volumetric data
        VolumetricLightComponent volumeLight = visibleLight.light.GetComponent<VolumetricLightComponent>();
        if (volumeLight != null) {
            dirLightSampleData[index] = new Vector4(volumeLight.sampleCount, volumeLight.HeightFog ? 1f : 0f, volumeLight.heightScale, volumeLight.groundHeight);
            dirLightScatterData[index] = new Vector4(volumeLight.scatteringCoef, volumeLight.extinctionCoef, volumeLight.skyBackgroundExtinctionCoef, volumeLight.mieG);
            dirLightNoiseData[index] = new Vector4(volumeLight.useNoise ? 1f : 0f, volumeLight.noiseScale, volumeLight.noiseIntensity, volumeLight.noiseIntensityOffset);
            dirLightNoiseVelocity[index] = new Vector4(volumeLight.noiseVelocity.x, volumeLight.noiseVelocity.y, 0f, 0f);
        }
    }

    void SetUpPointLight(int index, int visibleIndex, ref VisibleLight visibleLight) {
        otherLightColors[index] = visibleLight.finalColor;
        //light position vector can be found via the forth column of the localToWorld Matrix
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        //reduce work in shader, store 1/r2 in the vector;
        position.w = 1.0f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;
        //avoid being affected by spot angle
        otherLightSpotAngles[index] = new Vector4(0f, 1f);
        otherLightShadowData[index] = shadows.ReserveOtherShadows(visibleLight.light, visibleIndex);
        //set volumetric data
        VolumetricLightComponent volumeLight = visibleLight.light.GetComponent<VolumetricLightComponent>();
        if (volumeLight != null) {
            otherLightSampleData[index] = new Vector4(volumeLight.sampleCount, volumeLight.HeightFog ? 1f : 0f, volumeLight.heightScale, volumeLight.groundHeight);
            otherLightScatterData[index] = new Vector4(volumeLight.scatteringCoef, volumeLight.extinctionCoef, volumeLight.skyBackgroundExtinctionCoef, volumeLight.mieG);
            otherLightNoiseData[index] = new Vector4(volumeLight.useNoise ? 1f : 0f, volumeLight.noiseScale, volumeLight.noiseIntensity, volumeLight.noiseIntensityOffset);
            otherLightNoiseVelocity[index] = new Vector4(volumeLight.noiseVelocity.x, volumeLight.noiseVelocity.y, 0f, 0f);
        }
    }

    //spot light is a point light that is enclosed by an occluding sphere with a hole on it, the size of the hole determines the size of the light cone
    void SetUpSpotLight(int index, int visibleIndex, ref VisibleLight visibleLight) {
        otherLightColors[index] = visibleLight.finalColor;
        //light position vector can be found via the forth column of the localToWorld Matrix
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        //reduce work in shader, store 1/r2 in the vector;
        position.w = 1.0f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;
        //light forward vector can be found via the third column of the localToWorld Matrix
        otherLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        Light light = visibleLight.light;
        //inner cone of the spot light have even intensity
        float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
        //outer cone of the spot light has attenuation
        float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
        float angleRangeInv = 1.0f / Mathf.Max(0.001f, innerCos - outerCos);
        otherLightSpotAngles[index] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
        otherLightShadowData[index] = shadows.ReserveOtherShadows(visibleLight.light, visibleIndex);
        //set volumetric data
        VolumetricLightComponent volumeLight = visibleLight.light.GetComponent<VolumetricLightComponent>();
        if (volumeLight != null) {
            otherLightSampleData[index] = new Vector4(volumeLight.sampleCount, volumeLight.HeightFog ? 1f : 0f, volumeLight.heightScale, volumeLight.groundHeight);
            otherLightScatterData[index] = new Vector4(volumeLight.scatteringCoef, volumeLight.extinctionCoef, volumeLight.skyBackgroundExtinctionCoef, volumeLight.mieG);
            otherLightNoiseData[index] = new Vector4(volumeLight.useNoise ? 1f : 0f, volumeLight.noiseScale, volumeLight.noiseIntensity, volumeLight.noiseIntensityOffset);
            otherLightNoiseVelocity[index] = new Vector4(volumeLight.noiseVelocity.x, volumeLight.noiseVelocity.y, 0f, 0f);
        }
    }

    public void CleanUp() {
        shadows.CleanUp();
    }

}
