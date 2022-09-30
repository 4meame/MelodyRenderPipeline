using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class LensFlareCommon {
    static LensFlareCommon m_Instance = null;
    static readonly object m_Padlock = new object();
    static List<LensFlareComponent> m_Data = new List<LensFlareComponent>();
    //max occlusion
    public static int maxLensFlareWithOcclusion = 128;
    //occlusion RT temporal filter
    public static int maxLensFlareWithOcclusionTemporalSample = 8;
    //1 : enable temporal merge, 0 : disable temporal merge
    public static int mergeNeeded = 1;
    public static RenderTexture occlusionRT = null;
    static int frameIndex = 0;

    private LensFlareCommon() {

    }

    static public void Initialize() {
        if (occlusionRT == null && mergeNeeded > 0) {
            //allocating occlusion RT
            occlusionRT = new RenderTexture(maxLensFlareWithOcclusion, maxLensFlareWithOcclusionTemporalSample + 1 * mergeNeeded, 32);
            occlusionRT.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat;
            occlusionRT.dimension = TextureDimension.Tex2D;
            occlusionRT.enableRandomWrite = true;
        }
    }

    static public void Dispose() {
        if (occlusionRT != null && !Application.isPlaying) {
            occlusionRT.Release();
            occlusionRT = null;
        }
    }

    public static LensFlareCommon Instance {
        get {
            if (m_Instance == null) {
                lock (m_Padlock) {
                    if (m_Instance == null) {
                        m_Instance = new LensFlareCommon();
                    }
                }
            }
            return m_Instance;
        }
    }

    List<LensFlareComponent> Data {
        get {
            return m_Data;
        }
    }

    public List<LensFlareComponent> GetData() {
        return Data;
    }

    public bool IsEmpty() {
        return Data.Count == 0;
    }

    public void AddData(LensFlareComponent data) {
        Debug.Assert(Instance == this, "LensFlareCommon can have only one instance");
        if (!m_Data.Contains(data)) {
            m_Data.Add(data);
        }
    }

    public void RemoveData(LensFlareComponent data) {
        Debug.Assert(Instance == this, "LensFlareCommon can have only one instance");
        if (m_Data.Contains(data)) {
            m_Data.Remove(data);
        }
    }

    static public float ShapeAttenuationPointLight() {
        return 1.0f;
    }

    static public float ShapeAttenuationDirectionLight(Vector3 lightForward, Vector3 eyeToLight) {
        return Mathf.Max(Vector3.Dot(lightForward, eyeToLight), 0.0f);
    }

    static public float ShapeAttenuationSpotConeLight(Vector3 lightForward, Vector3 eyeToLight, float spotAngle, float innerSpotPercent01) {
        float outerDot = Mathf.Max(Mathf.Cos(0.5f * spotAngle * Mathf.Deg2Rad), 0.0f);
        float innerDot = Mathf.Max(Mathf.Cos(0.5f * spotAngle * Mathf.Deg2Rad * innerSpotPercent01), 0.0f);
        float dot = Mathf.Max(Vector3.Dot(lightForward, eyeToLight), 0.0f);
        return Mathf.Clamp01((dot - outerDot) / (innerDot - outerDot));
    }

    static public void ShapeAttenuationSpotCubeLight() {

    }

    static public void ShapeAttenuationPyramidCubeLight() {

    }

    static public void ShapeAttenuationAreaTubeLight() {

    }

    static public void ShapeAttenuationAreaRectangleLight() {

    }

    static public void ShapeAttenuationAreaDiscLight() {

    }

    static public Vector4 GetFlareData0(Vector2 screenPos, Vector2 translationScale, Vector2 rayOff0, Vector2 vLocalScreenRatio, float angleDeg, float position, float angularOffset, Vector2 positionOffset, bool autoRotate) {
        if (!SystemInfo.graphicsUVStartsAtTop) {
            angleDeg *= -1;
            positionOffset.y *= -1;
        }
        float globalCos0 = Mathf.Cos(-angularOffset * Mathf.Deg2Rad);
        float globalSin0 = Mathf.Sin(-angularOffset * Mathf.Deg2Rad);
        Vector2 rayOff = -translationScale * (screenPos + screenPos * (position - 1.0f));
        rayOff = new Vector2(globalCos0 * rayOff.x - globalSin0 * rayOff.y, globalSin0 * rayOff.x + globalCos0 * rayOff.y);
        float rotation = angleDeg;
        rotation += 180.0f;
        if (autoRotate) {
            Vector2 pos = (rayOff.normalized * vLocalScreenRatio) * translationScale;
            rotation += -Mathf.Rad2Deg * Mathf.Atan2(pos.y, pos.x);
        }
        rotation *= Mathf.Deg2Rad;
        float localCos0 = Mathf.Cos(-rotation);
        float localSin0 = Mathf.Sin(-rotation);
        return new Vector4(localCos0, localSin0, positionOffset.x + rayOff0.x * translationScale.x, -positionOffset.y + rayOff0.y * translationScale.y);
    }
    static Vector2 GetLensFlareRayOffset(Vector2 screenPos, float position, float globalCos0, float globalSin0) {
        Vector2 rayOff = -(screenPos + screenPos * (position - 1.0f));
        return new Vector2(globalCos0 * rayOff.x - globalSin0 * rayOff.y, globalSin0 * rayOff.x + globalCos0 * rayOff.y);
    }

    //directional light or local light : point,spot,area light
    static Vector3 WorldToViewport(Camera camera, bool isLocalLight, bool isCameraRelative, Matrix4x4 viewProjMatrix, Vector3 positionWS) {
        if (isLocalLight) {
            //treat light as world local object
            return WorldToViewportLocal(isCameraRelative, viewProjMatrix, camera.transform.position, positionWS);
        } else {
            return WorldToViewportDistance(camera, positionWS);
        }
    }

    static Vector3 WorldToViewportLocal(bool isCameraRelative, Matrix4x4 viewProjMatrix, Vector3 cameraPosWS, Vector3 positionWS) {
        Vector3 localPositionWS = positionWS;
        if (isCameraRelative) {
            //force camera pos is world origin, must set ture here, maybe have some mistake
            localPositionWS -= cameraPosWS;
        }
        Vector4 viewportPos4 = viewProjMatrix * localPositionWS;
        Vector3 viewportPos = new Vector3(viewportPos4.x, viewportPos4.y, 0f);
        viewportPos /= viewportPos4.w;
        viewportPos.x = viewportPos.x * 0.5f + 0.5f;
        viewportPos.y = viewportPos.y * 0.5f + 0.5f;
        viewportPos.y = 1.0f - viewportPos.y;
        viewportPos.z = viewportPos4.w;
        return viewportPos;
    }

    static Vector3 WorldToViewportDistance(Camera cam, Vector3 positionWS) {
        Vector4 camPos = cam.worldToCameraMatrix * positionWS;
        Vector4 viewportPos4 = cam.projectionMatrix * camPos;
        Vector3 viewportPos = new Vector3(viewportPos4.x, viewportPos4.y, 0f);
        viewportPos /= viewportPos4.w;
        viewportPos.x = viewportPos.x * 0.5f + 0.5f;
        viewportPos.y = viewportPos.y * 0.5f + 0.5f;
        viewportPos.z = viewportPos4.w;
        return viewportPos;
    }

    static public void ComputeOcclusion(Material lensFlareMaterial, LensFlareCommon lensFlares, Camera camera, 
        float actualWidth, float actualHeight, bool isCameraRelative,
        Vector3 cameraPosWS, Matrix4x4 viewProjMatrix, 
        CommandBuffer buffer, 
        bool taaEnabled, int _FlareOcclusionTex, int _FlareOcclusionIndex, int _FlareTex, int _FlareColorValue, int _FlareData0, int _FlareData1, int _FlareData2, int _FlareData3, int _FlareData4) {

        Vector2 vScreenRatio;
        if(lensFlares.IsEmpty() || occlusionRT == null) {
            Debug.LogError("null");
            return;
        }
        //exit in sceneView camera
        if (camera.cameraType == CameraType.SceneView) {
            //determine whether the "Animated Materials" checkbox is checked for the current view.
            for (int i = 0; i < UnityEditor.SceneView.sceneViews.Count; i++) {
                var sv = UnityEditor.SceneView.sceneViews[i] as UnityEditor.SceneView;
                if (sv.camera == camera) {
                    return;
                }
            }
        }
        //calculate screenRatio
        Vector2 screenSize = new Vector2(actualWidth, actualHeight);
        float screenRatio = screenSize.x / screenSize.y;
        vScreenRatio = new Vector2(screenRatio, 1.0f);
        //RT identifier already set
        buffer.SetRenderTarget(occlusionRT);
        if (!taaEnabled) {
            buffer.ClearRenderTarget(false, true, Color.black);
        }
        //ddx ddy
        float dx = 1.0f / ((float)maxLensFlareWithOcclusion);
        float dy = 1.0f / ((float)(maxLensFlareWithOcclusionTemporalSample + 1 * mergeNeeded));
        float halfx = 0.5f / ((float)maxLensFlareWithOcclusion);
        float halfy = 0.5f / ((float)(maxLensFlareWithOcclusionTemporalSample + 1 * mergeNeeded));

        int taaValue = taaEnabled ? 1 : 0;
        int occlusionIndex = 0;
        foreach (LensFlareComponent lensFlare in lensFlares.GetData()) {
            if (lensFlare == null) {
                continue;
            }
            LensFlareData data = lensFlare.lensFlareData;

            if (!lensFlare.enabled ||
                !lensFlare.gameObject.activeSelf ||
                !lensFlare.gameObject.activeInHierarchy ||
                data == null ||
                data.elements == null ||
                data.elements.Length == 0 ||
                !lensFlare.useOcclusion ||
                (lensFlare.useOcclusion && lensFlare.sampleCount == 0) ||
                lensFlare.intensity <= 0.0f) {
                continue;
            }
            //get light world position
            Light light = lensFlare.GetComponent<Light>();
            Vector3 positionWS;
            Vector3 viewPortPos;
            bool isLocalLight = false;
            if (light.type == LightType.Directional) {
                //directional light
                positionWS = -light.transform.forward * camera.farClipPlane;
            } else {
                positionWS = light.transform.position;
                isLocalLight = true;
            }
            viewPortPos = WorldToViewport(camera, isLocalLight, isCameraRelative, viewProjMatrix, positionWS);
            //opposite direction
            if (viewPortPos.z < 0.0f) { 
                continue; 
            }
            //viewport cull
            if (!lensFlare.allowOffScreen) {
                if (viewPortPos.x < 0.0f || viewPortPos.x > 1.0f ||
                    viewPortPos.y < 0.0f || viewPortPos.y > 1.0f) {
                    continue;
                }
            }
            //calcuate attenuation coef
            Vector3 diffToObject = positionWS - cameraPosWS;
            float distanceToObject = diffToObject.magnitude;
            float coefDistSample = distanceToObject / lensFlare.maxAttenuationDistance;
            float coefScaleSample = distanceToObject / lensFlare.maxAttenuationScale;
            float distanceAttenuation = isLocalLight && lensFlare.distanceAttenuationCurve.length > 0 ? lensFlare.distanceAttenuationCurve.Evaluate(coefDistSample) : 1.0f;
            float scaleAttenuation = isLocalLight && lensFlare.scaleAttenuationCurve.length > 1 ? lensFlare.scaleAttenuationCurve.Evaluate(coefScaleSample) : 1.0f;
            //viewport screenPos Z with offset
            Vector3 direction = (camera.transform.position - lensFlare.transform.position).normalized;
            Vector3 screenPosZ = WorldToViewport(camera, isLocalLight, isCameraRelative, viewProjMatrix, positionWS + direction * lensFlare.occlusionOffset);
            //calculate occlusion radius
            float adjustedOcclusionRadius = isLocalLight ? lensFlare.occlusionRadius : lensFlare.CelestialProjectedOcclusionRadius(camera);
            Vector2 occlusionRadiusEdgeScreenPos0 = (Vector2)viewPortPos;
            Vector2 occlusionRadiusEdgeScreenPos1 = WorldToViewport(camera, isLocalLight, isCameraRelative, viewProjMatrix, positionWS + camera.transform.up * adjustedOcclusionRadius);
            float occlusionRadius = (occlusionRadiusEdgeScreenPos1 - occlusionRadiusEdgeScreenPos0).magnitude;
            //_FlareData1 x: OcclusionRadius, y: OcclusionSampleCount, z: ScreenPosZ, w: ScreenRatio
            buffer.SetGlobalVector(_FlareData1, new Vector4(occlusionRadius, lensFlare.sampleCount, screenPosZ.z, actualHeight / actualWidth));
            buffer.EnableShaderKeyword("FLARE_COMPUTE_OCCLUSION");
            Vector2 screenPos = new Vector2(2.0f * viewPortPos.x - 1.0f, 1.0f - 2.0f * viewPortPos.y);
            Vector2 radPos = new Vector2(Mathf.Abs(screenPos.x), Mathf.Abs(screenPos.y));
            // l1 norm (instead of l2 norm), do not know what is it
            float radius = Mathf.Max(radPos.x, radPos.y);
            float radialsScaleRadius = lensFlare.radialScreenAttenuationCurve.length > 0 ? lensFlare.radialScreenAttenuationCurve.Evaluate(radius) : 1.0f;
            float currentIntensity = lensFlare.intensity * radialsScaleRadius * distanceAttenuation;
            if (currentIntensity <= 0.0f) {
                continue;
            }
            buffer.SetGlobalVector(_FlareOcclusionIndex, new Vector4(((float)(occlusionIndex)) * dx + halfx, halfy, 0, frameIndex + 1));
            //falloff value
            float gradientPosition = Mathf.Clamp01(1.0f - 1e-6f);
            //_FlareData3 x: Allow Offscreen, y: Edge Offset, z: Falloff, w: invSideCount
            buffer.SetGlobalVector(_FlareData3, new Vector4(lensFlare.allowOffScreen ? 1.0f : -1.0f, gradientPosition, Mathf.Exp(Mathf.Lerp(0.0f, 4.0f, 1.0f)), 1.0f / 3.0f));

            float globalCos0 = Mathf.Cos(0.0f);
            float globalSin0 = Mathf.Sin(0.0f);
            float position = 0.0f;
            Vector2 rayOffset = GetLensFlareRayOffset(screenPos, position, globalCos0, globalSin0);
            Vector4 flareData0 = GetFlareData0(screenPos, Vector2.one, rayOffset, vScreenRatio, 0.0f, position, 0.0f, Vector2.zero, false);
            //_FlareData0 x: localCos0, y: localSin0, zw: PositionOffsetXY
            buffer.SetGlobalVector(_FlareData0, flareData0);
            //_FlareData2 //xy: ScreenPos, zw: FlareSize
            buffer.SetGlobalVector(_FlareData2, new Vector4(screenPos.x, screenPos.y, 0.0f, 0.0f));
            //render to screen
            buffer.SetViewport(new Rect() { x = occlusionIndex, y = (frameIndex + 1 * mergeNeeded) * taaValue, width = 1, height = 1 });
            buffer.DrawProcedural(Matrix4x4.identity, lensFlareMaterial, 4, MeshTopology.Quads, 4, 1);
            ++occlusionIndex;
        }
        ++frameIndex;
        frameIndex %= maxLensFlareWithOcclusionTemporalSample;
    }

    static public void DoLensFlareCommon(Material lensFlareMaterial, LensFlareCommon lensFlares, Camera camera,
        float actualWidth, float actualHeight, bool isCameraRelative,
        Vector3 cameraPosWS, Matrix4x4 viewProjMatrix,
        CommandBuffer buffer,RenderTargetIdentifier colorBuffer,
        System.Func<Light, Camera, Vector3, float> GetLensFlareLightAttenuation,
        bool taaEnabled, int _FlareOcclusionTex, int _FlareOcclusionIndex, int _FlareTex, int _FlareColorValue, int _FlareData0, int _FlareData1, int _FlareData2, int _FlareData3, int _FlareData4) {

        Vector2 vScreenRatio;
        if (lensFlares.IsEmpty()) {
            return;
        }
        //exit in sceneView camera
        if (camera.cameraType == CameraType.SceneView) {
            //determine whether the "Animated Materials" checkbox is checked for the current view.
            for (int i = 0; i < UnityEditor.SceneView.sceneViews.Count; i++) {
                var sv = UnityEditor.SceneView.sceneViews[i] as UnityEditor.SceneView;
                if (sv.camera == camera) {
                    return;
                }
            }
        }
        //calculate screenRatio
        Vector2 screenSize = new Vector2(actualWidth, actualHeight);
        float screenRatio = screenSize.x / screenSize.y;
        vScreenRatio = new Vector2(screenRatio, 1.0f);
        //RT identifier already set
        buffer.SetRenderTarget(colorBuffer);
        buffer.SetViewport(new Rect() { width = screenSize.x, height = screenSize.y });

        int occlusionIndex = 0;
        foreach (LensFlareComponent lensFlare in lensFlares.GetData()) {
            if (lensFlare == null) {
                continue;
            }
            LensFlareData data = lensFlare.lensFlareData;

            if (!lensFlare.enabled ||
                !lensFlare.gameObject.activeSelf ||
                !lensFlare.gameObject.activeInHierarchy ||
                data == null ||
                data.elements == null ||
                data.elements.Length == 0 ||           
                lensFlare.intensity <= 0.0f) {
                continue;
            }
            //get light world position
            Light light = lensFlare.GetComponent<Light>();
            Vector3 positionWS;
            Vector3 viewPortPos;
            bool isLocalLight = false;
            if (light.type == LightType.Directional) {
                //directional light
                positionWS = -light.transform.forward * camera.farClipPlane;
            } else {
                positionWS = light.transform.position;
                isLocalLight = true;
            }
            viewPortPos = WorldToViewport(camera, isLocalLight, isCameraRelative, viewProjMatrix, positionWS);
            //opposite direction
            if (viewPortPos.z < 0.0f) {
                continue;
            }
            //viewport cull
            if (!lensFlare.allowOffScreen) {
                if (viewPortPos.x < 0.0f || viewPortPos.x > 1.0f ||
                    viewPortPos.y < 0.0f || viewPortPos.y > 1.0f) {
                    continue;
                }
            }
            //calcuate attenuation coef
            Vector3 diffToObject = positionWS - cameraPosWS;
            //check if the light is forward, can be an issue with, the math associated to Panini projection
            if (Vector3.Dot(camera.transform.forward, diffToObject) < 0.0f) {
                continue;
            }
            float distanceToObject = diffToObject.magnitude;
            float coefDistSample = distanceToObject / lensFlare.maxAttenuationDistance;
            float coefScaleSample = distanceToObject / lensFlare.maxAttenuationScale;
            float distanceAttenuation = isLocalLight && lensFlare.distanceAttenuationCurve.length > 0 ? lensFlare.distanceAttenuationCurve.Evaluate(coefDistSample) : 1.0f;
            float scaleAttenuation = isLocalLight && lensFlare.scaleAttenuationCurve.length > 1 ? lensFlare.scaleAttenuationCurve.Evaluate(coefScaleSample) : 1.0f;
            //lensflare color
            Color globalColorModulation = Color.white;
            if (light != null) {
                if (lensFlare.attenuationByLightShape)
                    globalColorModulation *= GetLensFlareLightAttenuation(light, camera, -diffToObject.normalized);
            }
            globalColorModulation *= distanceAttenuation;
            //viewport screenPos Z with offset
            Vector3 direction = (camera.transform.position - lensFlare.transform.position).normalized;
            Vector3 screenPosZ = WorldToViewport(camera, isLocalLight, isCameraRelative, viewProjMatrix, positionWS + direction * lensFlare.occlusionOffset);
            //calculate occlusion radius
            float adjustedOcclusionRadius = isLocalLight ? lensFlare.occlusionRadius : lensFlare.CelestialProjectedOcclusionRadius(camera);
            Vector2 occlusionRadiusEdgeScreenPos0 = (Vector2)viewPortPos;
            Vector2 occlusionRadiusEdgeScreenPos1 = WorldToViewport(camera, isLocalLight, isCameraRelative, viewProjMatrix, positionWS + camera.transform.up * adjustedOcclusionRadius);
            float occlusionRadius = (occlusionRadiusEdgeScreenPos1 - occlusionRadiusEdgeScreenPos0).magnitude;
            //_FlareData1 x: OcclusionRadius, y: OcclusionSampleCount, z: ScreenPosZ, w: ScreenRatio
            buffer.SetGlobalVector(_FlareData1, new Vector4(occlusionRadius, lensFlare.sampleCount, screenPosZ.z, actualHeight / actualWidth));
            if (lensFlare.useOcclusion) {
                buffer.EnableShaderKeyword("FLARE_OCCLUSION");
            } else {
                buffer.DisableShaderKeyword("FLARE_OCCLUSION");
            }
            if(occlusionRT != null) {
                buffer.SetGlobalTexture(_FlareOcclusionTex, occlusionRT);
            }
            buffer.SetGlobalVector(_FlareOcclusionIndex, new Vector4((float)(occlusionIndex + 0.5f) / maxLensFlareWithOcclusion, 0.5f, 0.0f, 0.0f));
            if (lensFlare.useOcclusion && lensFlare.sampleCount > 0)
                ++occlusionIndex;
            //draw lensflare elements
            foreach (LensFlareDataElement element in data.elements) {
                if (element == null ||
                    element.visible == false ||
                    (element.lensFlareTexture == null && element.flareType == LensFlareType.Image) ||
                    element.localIntensity <= 0.0f ||
                    element.count <= 0 ||
                    element.localIntensity <= 0.0f) {
                    continue;
                }
                //you can apply light color temperature in debug view inspector
                Color colorModulation = globalColorModulation;
                if (light != null && element.modulateByLightColor) {
                    if (light.useColorTemperature)
                        colorModulation *= light.color * Mathf.CorrelatedColorTemperatureToRGB(light.colorTemperature);
                    else
                        colorModulation *= light.color;
                }
                Color currColor = colorModulation;
                Vector2 screenPos = new Vector2(2.0f * viewPortPos.x - 1.0f, 1.0f - 2.0f * viewPortPos.y);
                Vector2 radPos = new Vector2(Mathf.Abs(screenPos.x), Mathf.Abs(screenPos.y));
                // l1 norm (instead of l2 norm), do not know what is it
                float radius = Mathf.Max(radPos.x, radPos.y);
                float radialsScaleRadius = lensFlare.radialScreenAttenuationCurve.length > 0 ? lensFlare.radialScreenAttenuationCurve.Evaluate(radius) : 1.0f;
                float currentIntensity = lensFlare.intensity * radialsScaleRadius * distanceAttenuation;
                if (currentIntensity <= 0.0f) {
                    continue;
                }
                Texture lensFlareTex = element.lensFlareTexture;
                float aspectRatio;
                if (element.flareType == LensFlareType.Image)
                {
                    aspectRatio = element.preserveAspectRatio ? ((float)lensFlareTex.height / (float)lensFlareTex.width) : 1.0f;
                } else {
                    aspectRatio = 1.0f;
                }
                Vector2 elementSizeXY;
                if (element.preserveAspectRatio) {
                    if (aspectRatio >= 1.0f) {
                        elementSizeXY = new Vector2(element.sizeXY.x / aspectRatio, element.sizeXY.y);
                    } else {
                        elementSizeXY = new Vector2(element.sizeXY.x, element.sizeXY.y * aspectRatio);
                    }
                } else {
                    elementSizeXY = new Vector2(element.sizeXY.x, element.sizeXY.y);
                }
                //arbitrary value
                float scaleSize = 0.1f;
                Vector2 size = new Vector2(elementSizeXY.x, elementSizeXY.y);
                float combineScale = scaleSize * scaleAttenuation * element.uniformScale * lensFlare.scale;
                size *= combineScale;
                currColor *= element.tint;
                currColor *= currentIntensity;
                //NOTE HERE
                float angularOffset = SystemInfo.graphicsUVStartsAtTop ? element.angularOffset : -element.angularOffset;
                float globalCos0 = Mathf.Cos(-angularOffset * Mathf.Deg2Rad);
                float globalSin0 = Mathf.Sin(-angularOffset * Mathf.Deg2Rad);
                float position = 2.0f * element.position;
                //set material pass
                LensFlareBlendMode blendMode = element.blendMode;
                int materialPass;
                if (blendMode == LensFlareBlendMode.Additive) {
                    materialPass = 0;
                }
                else if (blendMode == LensFlareBlendMode.Screen) {
                    materialPass = 1;
                }
                else if (blendMode == LensFlareBlendMode.Premultiply) {
                    materialPass = 2;
                }
                else if (blendMode == LensFlareBlendMode.Lerp) {
                    materialPass = 3;
                }
                else {
                    materialPass = 0;
                }
                //set keywords
                if (element.flareType == LensFlareType.Image) {
                    buffer.DisableShaderKeyword("FLARE_CIRCLE");
                    buffer.DisableShaderKeyword("FLARE_POLYGON");
                }
                else if (element.flareType == LensFlareType.Circle) {
                    buffer.EnableShaderKeyword("FLARE_CIRCLE");
                    buffer.DisableShaderKeyword("FLARE_POLYGON");
                }
                else if (element.flareType == LensFlareType.Polygon) {
                    buffer.DisableShaderKeyword("FLARE_CIRCLE");
                    buffer.EnableShaderKeyword("FLARE_POLYGON");
                }
                if (element.flareType == LensFlareType.Circle || element.flareType == LensFlareType.Polygon) {
                    if (element.inverseSDF) {
                        buffer.EnableShaderKeyword("FLARE_INVERSE_SDF");
                    }
                    else {
                        buffer.DisableShaderKeyword("FLARE_INVERSE_SDF");
                    }
                }
                else {
                    buffer.DisableShaderKeyword("FLARE_INVERSE_SDF");
                }


                if (element.lensFlareTexture != null) {
                    buffer.SetGlobalTexture(_FlareTex, element.lensFlareTexture);
                }

                float gradientPosition = Mathf.Clamp01((1.0f - element.edgeOffset) - 1e-6f);
                if (element.flareType == LensFlareType.Polygon) {
                    gradientPosition = Mathf.Pow(gradientPosition + 1.0f, 5);
                }
                //_FlareData3 //x: Allow Offscreen, y: Edge Offset, z: Falloff, w: invSideCount
                buffer.SetGlobalVector(_FlareData3, new Vector4(lensFlare.allowOffScreen ? 1.0f : -1.0f, gradientPosition, Mathf.Exp(Mathf.Lerp(0.0f, 4.0f, Mathf.Clamp01(1.0f - element.fallOff))), 1.0f / (float)element.sideCount));

                //local function
                Vector2 ComputeLocalSize(Vector2 rayOff, Vector2 rayOff0, Vector2 currSize, AnimationCurve distortionCurve) {
                    Vector2 localRadPos;
                    float localRadius;
                    if (!element.distortionRelativeToCenter) {
                        localRadPos = (rayOff - rayOff0) * 0.5f;
                        localRadius = Mathf.Clamp01(Mathf.Max(Mathf.Abs(localRadPos.x), Mathf.Abs(localRadPos.y)));
                    }
                    else {
                        localRadPos = screenPos + (rayOff + new Vector2(element.positionOffset.x, -element.positionOffset.y)) * element.translationScale;
                        localRadius = Mathf.Clamp01(localRadPos.magnitude);
                    }
                    float localLerpValue = Mathf.Clamp01(distortionCurve.Evaluate(localRadius));
                    return new Vector2(Mathf.Lerp(currSize.x, element.targetSizeDistortion.x * combineScale / aspectRatio, localLerpValue), Mathf.Lerp(currSize.y, element.targetSizeDistortion.y * combineScale / aspectRatio, localLerpValue));
                }

                float SDFSmoothness = element.sdfRoundness;
                if (element.flareType == LensFlareType.Polygon) {
                    float invSide = 1.0f / (float)element.sideCount;
                    float rCos = Mathf.Cos(Mathf.PI * invSide);
                    float roundValue = rCos * SDFSmoothness;
                    float r = rCos - roundValue;
                    float an = 2.0f * Mathf.PI * invSide;
                    float he = r * Mathf.Tan(0.5f * an);
                    buffer.SetGlobalVector(_FlareData4, new Vector4(SDFSmoothness, r, an, he));
                }
                else {
                    buffer.SetGlobalVector(_FlareData4, new Vector4(SDFSmoothness, 0.0f, 0.0f, 0.0f));
                }

                //draw procudural quad
                if (!element.allowMultipleElement || element.count == 1) {
                    Vector2 localSize = size;
                    Vector2 rayOff = GetLensFlareRayOffset(screenPos, position, globalCos0, globalSin0);
                    if (element.enableRadialDistortion) {
                        Vector2 rayOff0 = GetLensFlareRayOffset(screenPos, 0.0f, globalCos0, globalSin0);
                        localSize = ComputeLocalSize(rayOff, rayOff0, localSize, element.distortionCurve);
                    }
                    Vector4 flareData0 = GetFlareData0(screenPos, element.translationScale, rayOff, vScreenRatio, element.rotation, position, angularOffset, element.positionOffset, element.autoRotate);
                    //_FlareData0 x: localCos0, y: localSin0, zw: PositionOffsetXY
                    buffer.SetGlobalVector(_FlareData0, flareData0);
                    //_FlareData2 xy: ScreenPos, zw: FlareSize
                    buffer.SetGlobalVector(_FlareData2, new Vector4(screenPos.x, screenPos.y, localSize.x, localSize.y));
                    buffer.SetGlobalVector(_FlareColorValue, currColor);
                    buffer.DrawProcedural(Matrix4x4.identity, lensFlareMaterial, materialPass, MeshTopology.Quads, 4, 1);
                }
                else {
                    //spread flares
                    float dLength = 2.0f * element.lengthSpread / ((float)(element.count - 1));
                    if (element.distribution == LensFlareDistribution.Uniform) {
                        float uniformAngle = 0.0f;
                        for (int elementIdx = 0; elementIdx < element.count; ++elementIdx) {
                            Vector2 localSize = size;
                            Vector2 rayOff = GetLensFlareRayOffset(screenPos, position, globalCos0, globalSin0);
                            if (element.enableRadialDistortion) {
                                Vector2 rayOff0 = GetLensFlareRayOffset(screenPos, 0.0f, globalCos0, globalSin0);
                                localSize = ComputeLocalSize(rayOff, rayOff0, localSize, element.distortionCurve);
                            }
                            float timeScale = element.count >= 2 ? ((float)elementIdx) / ((float)(element.count - 1)) : 0.5f;
                            Color color = element.colorGradient.Evaluate(timeScale);
                            Vector4 flareData0 = GetFlareData0(screenPos, element.translationScale, rayOff, vScreenRatio, element.rotation + uniformAngle, position, angularOffset, element.positionOffset, element.autoRotate);
                            //_FlareData0 x: localCos0, y: localSin0, zw: PositionOffsetXY
                            buffer.SetGlobalVector(_FlareData0, flareData0);
                            //_FlareData2 xy: ScreenPos, zw: FlareSize
                            buffer.SetGlobalVector(_FlareData2, new Vector4(screenPos.x, screenPos.y, localSize.x, localSize.y));
                            buffer.SetGlobalVector(_FlareColorValue, currColor * color);
                            buffer.DrawProcedural(Matrix4x4.identity, lensFlareMaterial, materialPass, MeshTopology.Quads, 4, 1);
                            //gradurally
                            position += dLength;
                            uniformAngle += element.uniformAngle;
                        }
                    }
                    else if (element.distribution == LensFlareDistribution.Random) {
                        Random.State backupRandomState = Random.state;
                        Random.InitState(element.seed);
                        Vector2 side = new Vector2(globalCos0, globalSin0);
                        side *= element.positionVariation.y;
                        float RandomRange(float min, float max) {
                            return Random.Range(min, max);
                        }
                        for (int elementIdx = 0; elementIdx < element.count; ++elementIdx) {
                            float localIntensity = RandomRange(-1.0f, 1.0f) * element.intensityVariation + 1.0f;
                            Vector2 localSize = size;
                            Vector2 rayOff = GetLensFlareRayOffset(screenPos, position, globalCos0, globalSin0);
                            if (element.enableRadialDistortion) {
                                Vector2 rayOff0 = GetLensFlareRayOffset(screenPos, 0.0f, globalCos0, globalSin0);
                                localSize = ComputeLocalSize(rayOff, rayOff0, localSize, element.distortionCurve);
                            }
                            localSize += localSize * (RandomRange(-1.0f, 1.0f) * element.scaleVariation);
                            Color randomColor = element.colorGradient.Evaluate(RandomRange(-1.0f, 1.0f));
                            Vector2 localPositionOffset = element.positionOffset + RandomRange(-1.0f, 1.0f) * side;
                            float localRotation = element.rotation + RandomRange(-Mathf.PI, Mathf.PI) * element.rotationVariation;
                            if(localIntensity > 0.0f) {
                                Vector4 flareData0 = GetFlareData0(screenPos, element.translationScale, rayOff, vScreenRatio, localRotation, position, angularOffset, localPositionOffset, element.autoRotate);
                                //_FlareData0 x: localCos0, y: localSin0, zw: PositionOffsetXY
                                buffer.SetGlobalVector(_FlareData0, flareData0);
                                //_FlareData2 xy: ScreenPos, zw: FlareSize
                                buffer.SetGlobalVector(_FlareData2, new Vector4(screenPos.x, screenPos.y, localSize.x, localSize.y));
                                buffer.SetGlobalVector(_FlareColorValue, currColor * randomColor * localIntensity);
                                buffer.DrawProcedural(Matrix4x4.identity, lensFlareMaterial, materialPass, MeshTopology.Quads, 4, 1);
                            }
                            position += dLength;
                            position += 0.5f * dLength * RandomRange(-1.0f, 1.0f) * element.positionVariation.x;
                        }
                    }
                    else if (element.distribution == LensFlareDistribution.Curve) {
                        for (int elementIdx = 0; elementIdx < element.count; ++elementIdx) {
                            float timeScale = element.count >= 2 ? ((float)elementIdx) / ((float)(element.count - 1)) : 0.5f;
                            Color color = element.colorGradient.Evaluate(timeScale);
                            float positionSpacing = element.positionCurve.length > 0 ? element.positionCurve.Evaluate(timeScale) : 1.0f;
                            float localPos = position + 2.0f * element.lengthSpread * positionSpacing;
                            Vector2 localSize = size;
                            Vector2 rayOff = GetLensFlareRayOffset(screenPos, localPos, globalCos0, globalSin0);
                            if (element.enableRadialDistortion) {
                                Vector2 rayOff0 = GetLensFlareRayOffset(screenPos, 0.0f, globalCos0, globalSin0);
                                localSize = ComputeLocalSize(rayOff, rayOff0, localSize, element.distortionCurve);
                            }
                            float sizeCurveValue = element.scaleCurve.length > 0 ? element.scaleCurve.Evaluate(timeScale) : 1.0f;
                            localSize *= sizeCurveValue;
                            float angleFromCurve = element.uniformAngleCurve.Evaluate(timeScale) * (180.0f - (180.0f / (float)element.count));
                            Vector4 flareData0 = GetFlareData0(screenPos, element.translationScale, rayOff, vScreenRatio, element.rotation + angleFromCurve, localPos, angularOffset, element.positionOffset, element.autoRotate);
                            //_FlareData0 x: localCos0, y: localSin0, zw: PositionOffsetXY
                            buffer.SetGlobalVector(_FlareData0, flareData0);
                            //_FlareData2 xy: ScreenPos, zw: FlareSize
                            buffer.SetGlobalVector(_FlareData2, new Vector4(screenPos.x, screenPos.y, localSize.x, localSize.y));
                            buffer.SetGlobalVector(_FlareColorValue, currColor * color);
                            buffer.DrawProcedural(Matrix4x4.identity, lensFlareMaterial, materialPass, MeshTopology.Quads, 4, 1);
                        }
                    }
                }
            }
        }
    }
}
