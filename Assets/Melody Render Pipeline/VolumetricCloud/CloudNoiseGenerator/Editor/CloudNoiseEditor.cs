using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CloudNoiseGenerator))]
public class CloudNoiseEditor : Editor {
    CloudNoiseGenerator worley;
    Editor worleySettingsEditor;

    public override void OnInspectorGUI() {
        DrawDefaultInspector();

        if (GUILayout.Button("Update")) {
            worley.ManualUpdate();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        if (GUILayout.Button("Save")) {
            Save();
        }

        if (GUILayout.Button("Load")) {
            Load();
        }

        if (worley.ActiveSettings != null) {
            DrawSettingsEditor(worley.ActiveSettings, ref worley.showSettingsEditor, ref worleySettingsEditor);
        }
    }

    void Save()  {
        FindObjectOfType<Save3D>().Save(worley.shapeTexture, CloudNoiseGenerator.shapeNoiseName);
        FindObjectOfType<Save3D>().Save(worley.detailTexture, CloudNoiseGenerator.detailNoiseName);
    }

    void Load() {
        worley.Load(CloudNoiseGenerator.shapeNoiseName, worley.shapeTexture);
        worley.Load(CloudNoiseGenerator.detailNoiseName, worley.detailTexture);
        EditorApplication.QueuePlayerLoopUpdate();
    }

    void DrawSettingsEditor(Object settings, ref bool foldOut, ref Editor editor) {
        if (settings != null) {
            foldOut = EditorGUILayout.InspectorTitlebar(foldOut, settings);
            using (var check = new EditorGUI.ChangeCheckScope()) {
                if (foldOut) {
                    CreateCachedEditor(settings, null, ref editor);
                    editor.OnInspectorGUI();
                }
                if (check.changed) {
                    worley.ActiveNoiseSettingsChanged();
                }
            }

        }
    }

    private void OnEnable() {
        worley = (CloudNoiseGenerator)target;
    }
}
