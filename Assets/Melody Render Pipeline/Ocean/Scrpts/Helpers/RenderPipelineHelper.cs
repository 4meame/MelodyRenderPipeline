using UnityEngine.Rendering;
#if CREST_URP
using UnityEngine.Rendering.Universal;
#endif
#if CREST_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

namespace Crest
{
    public enum RenderPipeline
    {
        Legacy,
        HighDefinition,
        Universal,
    }

    public class RenderPipelineHelper
    {
        // GraphicsSettings.currentRenderPipeline could be from the graphics setting or current quality level.
        public static bool IsLegacy => GraphicsSettings.currentRenderPipeline == null;

        public static bool IsUniversal
        {
            get
            {
#if CREST_URP
                return GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;
#else
                return false;
#endif
            }
        }

        public static bool IsHighDefinition
        {
            get
            {
#if CREST_HDRP
                return GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset;
#else
                return false;
#endif
            }
        }
    }
}