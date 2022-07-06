using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using LightType = UnityEngine.LightType;

public partial class MelodyRenderPipeline {
    partial void InitializeForEditor();

#if UNITY_EDITOR
    partial void InitializeForEditor() {
        Lightmapping.SetDelegate(lightsDelegate);
    }

    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        Lightmapping.ResetDelegate();
    }

    static Lightmapping.RequestLightsDelegate lightsDelegate = (Light[] lights, NativeArray<LightDataGI> output) => {
        var lightData = new LightDataGI();
        for (int i = 0; i < lights.Length; i++) {
            Light light = lights[i];
            switch (light.type)
            {
                case LightType.Spot:
                    var spotLight = new SpotLight();
                    LightmapperUtils.Extract(light, ref spotLight);
                    lightData.Init(ref spotLight);
                    break;
                case LightType.Directional:
                    var directionalLight = new DirectionalLight();
                    LightmapperUtils.Extract(light, ref directionalLight);
                    lightData.Init(ref directionalLight);
                    break;
                case LightType.Point:
                    var pointLight = new PointLight();
                    LightmapperUtils.Extract(light, ref pointLight);
                    lightData.Init(ref pointLight);
                    break;
                case LightType.Area:
                    var rectangleLight = new RectangleLight();
                    LightmapperUtils.Extract(light, ref rectangleLight);
                    //do not support realtime area light
                    rectangleLight.mode = LightMode.Baked;
                    lightData.Init(ref rectangleLight);
                    break;
                default:
                    //instruct unity to not bake the light by default, with the light's intance ID
                    lightData.InitNoBake(light.GetInstanceID());
                    break;
            }
            lightData.falloff = FalloffType.InverseSquared;
            output[i] = lightData;
        }
    };
#endif
}
