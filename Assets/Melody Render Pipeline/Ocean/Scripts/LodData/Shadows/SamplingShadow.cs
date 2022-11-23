// Crest Ocean System

// Copyright 2021 Wave Harmonic Ltd

using UnityEngine;
using UnityEngine.Rendering;

namespace Crest
{
    public class SamplingShadow
    {
        static SamplingShadow _instance;
        public static bool Created => _instance != null;
        public static bool enabled;
        public SamplingShadow()
        {

        }

        public static void Enable()
        {
            if (_instance == null)
            {
                _instance = new SamplingShadow();
            }

            enabled = true;
        }

        public static void Disable()
        {
            enabled = false;
        }

        public static void SampleShadowPass(ScriptableRenderContext context, Camera camera)
        {
            if (!enabled) {
                return;
            }

            if (OceanRenderer.Instance == null || OceanRenderer.Instance._lodDataShadow == null)
            {
                return;
            }

            // Only sample shadows for the main camera.
            if (!ReferenceEquals(OceanRenderer.Instance.ViewCamera, camera))
            {
                return;
            }


            var cmd = OceanRenderer.Instance._lodDataShadow.BufCopyShadowMap;
            if (cmd == null) return;

            context.ExecuteCommandBuffer(cmd);

            {
                // Restore matrices otherwise remaining render will have incorrect matrices. Each pass is responsible
                // for restoring matrices if required.
                cmd.Clear();
                cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
                context.ExecuteCommandBuffer(cmd);
            }
        }
    }
}
