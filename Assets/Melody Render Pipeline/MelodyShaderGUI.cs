using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class MelodyShaderGUI : ShaderGUI
{
    //for showing and editing the material
    MaterialEditor editor;
    //reference to the material being edited
    Object[] materials;
    //properties that be edited
    MaterialProperty[] properties;

    bool showPresets;
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties) {
        EditorGUI.BeginChangeCheck();

        base.OnGUI(materialEditor, properties);
        editor = materialEditor;
        materials = materialEditor.targets;
        this.properties = properties;

        BakedEmission();

        EditorGUILayout.Space();
        showPresets = EditorGUILayout.Foldout(showPresets, "Presets", true);
        if (showPresets) {
            OpaquePreset();
            ClipPreset();
            FadePreset();
            TransparentPreset();
        }
        //when the material got changed via the GUI, check SetShadowCasterPass()
        if (EditorGUI.EndChangeCheck()) {
            SetShadowCasterPass();
            CopyLightMappingProperties();
        }
    }

    //set value
    bool SetProperties(string name, float value) {
        MaterialProperty materialProperty = FindProperty(name, properties, false);
        if(materialProperty != null) {
            materialProperty.floatValue = value;
            return true;
        }
        return false;
    }

    //set toggle
    void SetProperties(string name, string keyword, bool value) {
        if(SetProperties(name, value ? 1f : 0f)) {
            SetKeyword(keyword, value);
        }
    }

    //set keyword
    void SetKeyword(string keyword, bool enable) {
        if(enable) {
            foreach (Material m in materials) {
                m.EnableKeyword(keyword);
            }

        } else {
            foreach (Material m in materials) {
                m.DisableKeyword(keyword);
            }
        }
    }

    //return whether a property exists
    bool HasProperty(string name) => FindProperty(name, properties, false) != null;
    //hide the button that material that has not the preperty
    bool HasPremultiplyAlpha => HasProperty("_PremulAlpha");

    bool Clipping
    {
        set => SetProperties("_Clipping", "_CLIPPING", value);
    }

    bool PremultiplyAlpha
    {
        set => SetProperties("_PremulAlpha", "_PREMULTIPLY_ALPHA", value);
    }

    BlendMode SrcBlend
    {
        set => SetProperties("_SrcBlend", (float)value);
    }

    BlendMode DstBlend
    {
        set => SetProperties("_DstBlend", (float)value);
    }

    bool ZWrite
    {
        set => SetProperties("_ZWrite", value ? 1f : 0f);
    }

    RenderQueue RenderQueue
    {
        set
        {
            foreach (Material m in materials)
            {
                m.renderQueue = (int)value;
            }
        }
    }

    ShadowMode Shadows
    {
        set
        {
            if (SetProperties("_Shadows", (float)value))
            {
                SetKeyword("_SHADOWS_CLIP", value == ShadowMode.Clip);
                SetKeyword("_SHADOWS_DITHER", value == ShadowMode.Dither);
            }
        }
    }

    bool PresetButton (string name) {
        if (GUILayout.Button(name)) {
            //add an undo action
            editor.RegisterPropertyChangeUndo(name);
            return true;
        }
        return false;
    }

    void OpaquePreset() {
        if(PresetButton("Opaque")) {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.Geometry;
            Shadows = ShadowMode.On;
        }
    }

    void ClipPreset() {
        if (PresetButton("Clip")) {
            Clipping = true;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.AlphaTest;
            Shadows = ShadowMode.Clip;
            
        }
    }

    void FadePreset() {
        if (PresetButton("Fade")) {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.SrcAlpha;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
            Shadows = ShadowMode.Dither;
        }
    }

    void TransparentPreset() {
        if (HasPremultiplyAlpha && PresetButton("Transparent")) {
            Clipping = false;
            PremultiplyAlpha = true;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
            Shadows = ShadowMode.Dither;
        }
    }

    void SetShadowCasterPass() {
        MaterialProperty shadows = FindProperty("_Shadows", properties, false);
        if (shadows == null || shadows.hasMixedValue){
            return;
        }
        bool enabled = shadows.floatValue < (float)ShadowMode.Off;
        foreach (Material m in materials) {
            m.SetShaderPassEnabled("ShadowCaster", enabled);
        }
    }

    enum ShadowMode {
        On, Clip, Dither, Off
    }

    void BakedEmission() {
        EditorGUI.BeginChangeCheck();
        editor.LightmapEmissionProperty();
        if (EditorGUI.EndChangeCheck())
        {
            foreach (Material m in materials) {
                //guarantee the emission color is black,so no need to bake it in the light map
                //"&" of "&=" is equal to "=" of "+="
                m.globalIlluminationFlags &=
                    ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }
    }

    //for baking transparent
    //Unity has a hard-coded approach for transparent or cliping, light map data multiply the alpha components of a _MainTex and _Color property, using the _Cutoff property for alpha clipping
    void CopyLightMappingProperties() {
        MaterialProperty mainTex = FindProperty("_MainTex", properties, false);
        MaterialProperty baseMap = FindProperty("_BaseMap", properties, false);
        if (mainTex != null && baseMap != null) {
            mainTex.textureValue = baseMap.textureValue;
            mainTex.textureScaleAndOffset = baseMap.textureScaleAndOffset;
        }
        MaterialProperty color = FindProperty("_Color", properties, false);
        MaterialProperty baseColor = FindProperty("_BaseColor", properties, false);
        if (color != null && baseColor != null) {
            color.colorValue = baseColor.colorValue;
        }
    }
}
