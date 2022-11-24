// Crest Ocean System

// Copyright 2021 Wave Harmonic Ltd


namespace Crest
{
    using UnityEngine;
    using UnityEngine.Rendering;

    public class UnderwaterEffectPass
    {
        const string SHADER_UNDERWATER_EFFECT = "Hidden/Crest/Underwater/Underwater Effect";
        static readonly int sp_TemporaryColor = Shader.PropertyToID("_TemporaryColor");
        static readonly int sp_CameraForward = Shader.PropertyToID("_CameraForward");

        static PropertyWrapperMaterial _underwaterEffectMaterial;
        static RenderTargetIdentifier _colorTarget;
        static RenderTargetIdentifier _depthTarget;
        static RenderTargetIdentifier _temporaryColorTarget = new RenderTargetIdentifier(sp_TemporaryColor, 0, CubemapFace.Unknown, -1);
        static bool _firstRender = true;

        static UnderwaterEffectPass s_instance;
        UnderwaterRenderer _underwaterRenderer;

        public static bool enabled;

        public UnderwaterEffectPass()
        {
            _underwaterEffectMaterial = new PropertyWrapperMaterial(SHADER_UNDERWATER_EFFECT);
            _underwaterEffectMaterial.material.hideFlags = HideFlags.HideAndDontSave;
        }

        internal static void CleanUp()
        {
            s_instance = null;
        }

        public static void Enable(UnderwaterRenderer underwaterRenderer)
        {
            if (s_instance == null)
            {
                s_instance = new UnderwaterEffectPass();
            }

            s_instance._underwaterRenderer = underwaterRenderer;

            enabled = true;
        }

        public static void Disable()
        {
            enabled = false;
        }

        public static void Execute(ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, bool useHDR, int colorAttachmentId, int depthAttachmentId)
        {
            if (!enabled)
            {
                return;
            }

            if (!s_instance._underwaterRenderer.IsActive)
            {
                return;
            }

            // Only support main camera, scene camera and preview camera.
            if (!ReferenceEquals(s_instance._underwaterRenderer._camera, camera))
            {
#if UNITY_EDITOR
                if (!s_instance._underwaterRenderer.IsActiveForEditorCamera(camera))
#endif
                {
                    return;
                }
            }

            if (!Helpers.MaskIncludesLayer(camera.cullingMask, OceanRenderer.Instance.Layer))
            {
                return;
            }

            var cameraTargetDescriptor = new RenderTextureDescriptor((int)bufferSize.x, (int)bufferSize.y, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            var commandBuffer = UnderwaterRenderer.Instance.BufUnderwaterEffect;

            _colorTarget = colorAttachmentId;
            _depthTarget = depthAttachmentId;

            // Calling ConfigureTarget is recommended by Unity, but that means it can only use it once? Also Blit breaks
            // XR SPI. Using SetRenderTarget and custom Blit instead.
            {
                var descriptor = cameraTargetDescriptor;
                descriptor.msaaSamples = 1;
                commandBuffer.GetTemporaryRT(sp_TemporaryColor, descriptor);
            }

            if (UnderwaterRenderer.Instance.UseStencilBufferOnEffect)
            {
                var descriptor = cameraTargetDescriptor;
                descriptor.colorFormat = RenderTextureFormat.Depth;
                descriptor.depthBufferBits = 24;
                descriptor.SetMSAASamples(camera);
                descriptor.bindMS = descriptor.msaaSamples > 1;

                commandBuffer.GetTemporaryRT(UnderwaterRenderer.ShaderIDs.s_CrestWaterVolumeStencil, descriptor);
            }

            // Ensure legacy underwater fog is disabled.
            if (_firstRender)
            {
                OceanRenderer.Instance.OceanMaterial.DisableKeyword("_OLD_UNDERWATER");
            }

#if UNITY_EDITOR
            if (!UnderwaterRenderer.IsFogEnabledForEditorCamera(camera))
            {
                return;
            }
#endif

            UnderwaterRenderer.UpdatePostProcessMaterial(
                UnderwaterRenderer.Instance._mode,
                camera,
                _underwaterEffectMaterial,
                UnderwaterRenderer.Instance._sphericalHarmonicsData,
                UnderwaterRenderer.Instance._meniscus,
                _firstRender || UnderwaterRenderer.Instance._copyOceanMaterialParamsEachFrame,
                UnderwaterRenderer.Instance._debug._viewOceanMask,
                UnderwaterRenderer.Instance._debug._viewStencil,
                UnderwaterRenderer.Instance._filterOceanData,
                ref UnderwaterRenderer.Instance._currentOceanMaterial,
                UnderwaterRenderer.Instance.EnableShaderAPI
            );

            // Required for XR SPI as forward vector in matrix is incorrect.
            _underwaterEffectMaterial.material.SetVector(sp_CameraForward, camera.transform.forward);

            // Create a separate stencil buffer context by copying the depth texture.
            if (UnderwaterRenderer.Instance.UseStencilBufferOnEffect)
            {
                if (camera.cameraType == CameraType.SceneView)
                {
                    commandBuffer.SetRenderTarget(UnderwaterRenderer.Instance._depthStencilTarget);
                    Helpers.Blit(commandBuffer, UnderwaterRenderer.Instance._depthStencilTarget, Helpers.UtilityMaterial, (int)Helpers.UtilityPass.CopyDepth);
                }
                else
                {
                    // Copy depth then clear stencil. Things to note:
                    // - Does not work with MSAA. Source is null.
                    // - Does not work with scene camera due to possible Unity bug. Source is RenderTextureFormat.ARGB32 instead of RenderTextureFormat.Depth.
                    commandBuffer.CopyTexture(_depthTarget, UnderwaterRenderer.Instance._depthStencilTarget);
                    commandBuffer.SetRenderTarget(UnderwaterRenderer.Instance._depthStencilTarget);
                    Helpers.Blit(commandBuffer, UnderwaterRenderer.Instance._depthStencilTarget, Helpers.UtilityMaterial, (int)Helpers.UtilityPass.ClearStencil);
                }
            }

            // Copy color buffer.
            {
                commandBuffer.CopyTexture(_colorTarget, _temporaryColorTarget);
            }

            commandBuffer.SetGlobalTexture(UnderwaterRenderer.ShaderIDs.s_CrestCameraColorTexture, _temporaryColorTarget);

            if (UnderwaterRenderer.Instance.UseStencilBufferOnEffect)
            {
                commandBuffer.SetRenderTarget(_colorTarget, UnderwaterRenderer.Instance._depthStencilTarget);
            }
            else
            {
                {
#if UNITY_EDITOR
                    if (camera.cameraType == CameraType.SceneView)
                    {
                        // If executing before transparents, scene view needed this. Works for other events too.
                        commandBuffer.SetRenderTarget(_colorTarget);
                    }
                    else
#endif
                    {
                        // No MSAA needed depth target set. Setting depth is necessary for depth to be bound as a target
                        // which is needed for volumes.
                        commandBuffer.SetRenderTarget(_colorTarget,  _depthTarget);
                    }
                }
            }

            UnderwaterRenderer.Instance.ExecuteEffect(commandBuffer, _underwaterEffectMaterial.material);

            context.ExecuteCommandBuffer(commandBuffer);

            commandBuffer.ReleaseTemporaryRT(sp_TemporaryColor);

            if (UnderwaterRenderer.Instance.UseStencilBufferOnEffect)
            {
                commandBuffer.ReleaseTemporaryRT(UnderwaterRenderer.ShaderIDs.s_CrestWaterVolumeStencil);
            }

            commandBuffer.Clear();

            _firstRender = false;
        }
    }
}
