#if CREST_HDRP

namespace Crest
{
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.HighDefinition;

    class SampleShadowsHDRP : CustomPass
    {
        static GameObject gameObject;
        static readonly string Name = "Sample Shadows";

        // These values come from unity_MatrxVP value in the frame debugger. unity_MatrxVP is marked as legacy and
        // breaks XR SPI. It is defined in:
        // "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/EditorShaderVariables.hlsl"
        static readonly Matrix4x4 s_Matrix = new Matrix4x4
        (
            new Vector4(2f, 0f, 0f, 0f),
            new Vector4(0f, -2f, 0f, 0f),
            new Vector4(0f, 0f, 0.00990099f, 0f),
            new Vector4(-1f, 1f, 0.990099f, 1f)
        );

        static readonly int sp_CrestViewProjectionMatrix = Shader.PropertyToID("_CrestViewProjectionMatrix");

        protected override void Execute(CustomPassContext context)
        {
            if (OceanRenderer.Instance == null || OceanRenderer.Instance._lodDataShadow == null) return;

            var camera = context.hdCamera.camera;
            var renderContext = context.renderContext;

            // Custom passes execute for every camera. We only support one camera for now.
            if (!ReferenceEquals(camera, OceanRenderer.Instance.ViewCamera)) return;
            // TODO: bail when not executing for main light or when no main light exists?
            // if (renderingData.lightData.mainLightIndex == -1) return;
            var commandBuffer = OceanRenderer.Instance._lodDataShadow.BufCopyShadowMap;
            if (commandBuffer == null) return;

            // Target is not multi-eye so stop mult-eye rendering for this command buffer. Breaks registered shadow
            // inputs without this.
            if (XRGraphics.enabled)
            {
                renderContext.StopMultiEye(camera);
            }

            commandBuffer.SetGlobalMatrix(sp_CrestViewProjectionMatrix, s_Matrix);

            renderContext.ExecuteCommandBuffer(commandBuffer);

            // Even if we do not call StopMultiEye, it is necessary to call StartMultiEye otherwise one eye no longer
            // renders.
            if (XRGraphics.enabled)
            {
                renderContext.StartMultiEye(camera);
            }
            else
            {
                // Restore matrices otherwise remaining render will have incorrect matrices. Each pass is responsible
                // for restoring matrices if required.
                commandBuffer.Clear();
                commandBuffer.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
                renderContext.ExecuteCommandBuffer(commandBuffer);
            }
        }

        public static void Enable()
        {
            CustomPassHelpers.CreateOrUpdate<SampleShadowsHDRP>(ref gameObject, Name, CustomPassInjectionPoint.BeforeTransparent);
        }

        public static void Disable()
        {
            // It should be safe to rely on this reference for this reference to fail.
            if (gameObject != null)
            {
                gameObject.SetActive(false);
            }
        }
    }
}

#endif // CREST_HDRP