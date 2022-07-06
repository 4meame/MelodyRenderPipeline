using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
using UnityEditor;
partial class CameraRender
{
    partial void DrawUnsupportedShaders();
    partial void DrawGizmosBeforeFX();
    //split DrawGizmos method in 2
    partial void DrawGizmosAfterFX();
    partial void PrepareForSceneWindow();
    partial void PrepareBuffer();
#if UNITY_EDITOR
    static ShaderTagId[] legacyShaderTagIds = {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };
    static Material errorMaterial;

    string SampleName { get; set; }

    partial void PrepareBuffer() {
        Profiler.BeginSample("Editor Only");
        buffer.name = camera.name;
        SampleName = camera.name;
        Profiler.EndSample();
    }

    partial void PrepareForSceneWindow() {
        if(camera.cameraType == CameraType.SceneView) {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
            //it will not affect the scene window
            useScaledRendering = false;
        }
    }

    partial void DrawGizmosBeforeFX() {
        if (Handles.ShouldRenderGizmos()) {
            if (useIntermediateBuffer) {
                Draw(depthAttachmentId, BuiltinRenderTextureType.CameraTarget, true);
                ExecuteBuffer();
            }
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
        }
    }
    partial void DrawGizmosAfterFX() {
        if (Handles.ShouldRenderGizmos()) {
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
    }

    partial void DrawUnsupportedShaders() {
        if(errorMaterial == null) {
            errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        var drawingSettings = new DrawingSettings(legacyShaderTagIds[0], new SortingSettings(camera)) { overrideMaterial = errorMaterial};
        for (int i = 1; i < legacyShaderTagIds.Length; i++) {
            drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
        }
        var filteringSettings = FilteringSettings.defaultValue;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }
#else
    const string sampleName = bufferName;
#endif
}
