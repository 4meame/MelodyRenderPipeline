// Crest Ocean System

// Copyright 2021 Wave Harmonic Ltd

namespace Crest
{
    using UnityEngine;
    using UnityEngine.Rendering;

    public class UnderwaterMaskPass
    {
        const string k_ShaderPathOceanMask = "Hidden/Crest/Underwater/Ocean Mask";

        static PropertyWrapperMaterial _oceanMaskMaterial;

        static UnderwaterMaskPass s_instance;
        UnderwaterRenderer _underwaterRenderer;

        public static bool enabled;

        public UnderwaterMaskPass()
        {
            _oceanMaskMaterial = new PropertyWrapperMaterial(k_ShaderPathOceanMask);
            _oceanMaskMaterial.material.hideFlags = HideFlags.HideAndDontSave;
        }

        internal static void CleanUp()
        {
            s_instance = null;
        }

        public static void Enable(UnderwaterRenderer underwaterRenderer)
        {
            if (s_instance == null)
            {
                s_instance = new UnderwaterMaskPass();
            }

            UnderwaterRenderer.Instance.OnEnableMask();

            s_instance._underwaterRenderer = underwaterRenderer;

            enabled = true;
        }

        public static void Disable()
        {
            if (UnderwaterRenderer.Instance != null)
            {
                UnderwaterRenderer.Instance.OnDisableMask();
            }

            enabled = false;
        }

        public static void Execute(ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, out bool underwater)
        {
            if (!enabled)
            {
                underwater = false;
                return;
            }

            if (!s_instance._underwaterRenderer.IsActive)
            {
                underwater = false;
                return;
            }

            // Only support main camera, scene camera and preview camera.
            if (!ReferenceEquals(s_instance._underwaterRenderer._camera, camera))
            {
#if UNITY_EDITOR
                if (!s_instance._underwaterRenderer.IsActiveForEditorCamera(camera))
#endif
                {
                    underwater = false;
                    return;
                }
            }

            if (!Helpers.MaskIncludesLayer(camera.cullingMask, OceanRenderer.Instance.Layer))
            {
                underwater = false;
                return;
            }

            underwater = true;
            var cameraTargetDescriptor = new RenderTextureDescriptor((int)bufferSize.x, (int)bufferSize.y);
            var descriptor = cameraTargetDescriptor;
            // Keywords and other things.
            UnderwaterRenderer.Instance.SetUpVolume(_oceanMaskMaterial.material);
            UnderwaterRenderer.Instance.SetUpMaskTextures(descriptor);
            if (UnderwaterRenderer.Instance._mode != UnderwaterRenderer.Mode.FullScreen && UnderwaterRenderer.Instance._volumeGeometry != null)
            {
                UnderwaterRenderer.Instance.SetUpVolumeTextures(descriptor);
            }

            var commandBuffer = UnderwaterRenderer.Instance.BufUnderwaterMask;

            commandBuffer.SetRenderTarget(UnderwaterRenderer.Instance._maskTarget, UnderwaterRenderer.Instance._depthTarget);
            context.ExecuteCommandBuffer(commandBuffer);
            commandBuffer.Clear();

            XRHelpers.Update(camera);

            // Populate water volume before mask so we can use the stencil.
            if (UnderwaterRenderer.Instance._mode != UnderwaterRenderer.Mode.FullScreen && UnderwaterRenderer.Instance._volumeGeometry != null)
            {
                UnderwaterRenderer.Instance.PopulateVolume(commandBuffer, UnderwaterRenderer.Instance._volumeFrontFaceTarget, UnderwaterRenderer.Instance._volumeBackFaceTarget);
                // Copy only the stencil by copying everything and clearing depth.
                commandBuffer.CopyTexture(UnderwaterRenderer.Instance._mode == UnderwaterRenderer.Mode.Portal ? UnderwaterRenderer.Instance._volumeFrontFaceTarget : UnderwaterRenderer.Instance._volumeBackFaceTarget, UnderwaterRenderer.Instance._depthTarget);
                Helpers.Blit(commandBuffer, UnderwaterRenderer.Instance._depthTarget, Helpers.UtilityMaterial, (int)Helpers.UtilityPass.ClearDepth);
            }

            UnderwaterRenderer.Instance.SetUpMask(commandBuffer, UnderwaterRenderer.Instance._maskTarget, UnderwaterRenderer.Instance._depthTarget);
            UnderwaterRenderer.PopulateOceanMask(
                commandBuffer,
                camera,
                OceanRenderer.Instance.Tiles,
                UnderwaterRenderer.Instance._cameraFrustumPlanes,
                _oceanMaskMaterial.material,
                UnderwaterRenderer.Instance._farPlaneMultiplier,
                UnderwaterRenderer.Instance.EnableShaderAPI,
                UnderwaterRenderer.Instance._debug._disableOceanMask
            );

            UnderwaterRenderer.Instance.FixMaskArtefacts
            (
                commandBuffer,
                descriptor,
                UnderwaterRenderer.Instance._maskTarget
            );

            context.ExecuteCommandBuffer(commandBuffer);
            commandBuffer.Clear();
        }
    }
}