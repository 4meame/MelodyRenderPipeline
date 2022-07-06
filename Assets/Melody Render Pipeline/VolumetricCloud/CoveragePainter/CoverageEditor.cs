using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CoveragePainter))]
public class CoverageEditor : Editor {
    CoveragePainter painter;

    void OnSceneGUI() {
        Handles.color = new Vector4(0, 1, 1, 0.9f);
        Handles.DrawSolidDisc(painter.worldPosition, Vector3.up, painter.brushRadius);
        Handles.color = new Vector4(0, 0.7f, 0.7f, 0.3f);
        Handles.DrawSolidDisc(painter.worldPosition_, Vector3.up, painter.brushRadius);

    }

    public override void OnInspectorGUI() {
        DrawDefaultInspector();

        if (GUILayout.Button("Create"))
        {
            painter.CreateCoverageRenderTexture();
        }

        if (GUILayout.Button("Load"))
        {
            painter.CopyCoverageAssetToRenderTexture();
        }

        if (GUILayout.Button("Save"))
        {
            painter.SaveCoverageRenderTexture();
        }

        if (GUILayout.Button("Delete"))
        {
            painter.DestoryCoverageRenderTexture();
        }
    }

    private void OnEnable() {
        painter = (CoveragePainter)target;
    }
}
