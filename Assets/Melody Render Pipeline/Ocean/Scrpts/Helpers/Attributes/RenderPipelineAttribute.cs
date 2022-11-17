namespace Crest
{
#if UNITY_EDITOR
    using Crest.EditorHelpers;
#endif
    using UnityEditor;
    using UnityEngine;

    public class RenderPipelineAttribute : DecoratorAttribute
    {
        readonly RenderPipeline _pipeline;
        readonly bool _inverted;

        public RenderPipelineAttribute(RenderPipeline pipeline, bool inverted = false)
        {
            _pipeline = pipeline;
            _inverted = inverted;
        }

#if UNITY_EDITOR
        internal override void Decorate(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            switch (_pipeline)
            {
                case RenderPipeline.Legacy:
                    if (RenderPipelineHelper.IsLegacy == _inverted) DecoratedDrawer.s_HideInInspector = true;
                    break;
                case RenderPipeline.HighDefinition:
                    if (RenderPipelineHelper.IsHighDefinition == _inverted) DecoratedDrawer.s_HideInInspector = true;
                    break;
                case RenderPipeline.Universal:
                    if (RenderPipelineHelper.IsUniversal == _inverted) DecoratedDrawer.s_HideInInspector = true;
                    break;
                default: break;
            }
        }
#endif
    }
}