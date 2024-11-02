using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    /// <summary>
    /// Provides utility methods around styling.
    /// </summary>
    static class StylingUtility
    {
        const float k_LabelRatio = 0.42f;
        const float k_Indent = 19.0f;

        /// <summary>
        /// Dynamically computes and sets the width of <see cref="Label"/> elements so that they stay properly aligned
        /// when indented with <see cref="Foldout"/> elements.
        /// </summary>
        /// <remarks>
        /// This will effectively inline the <see cref="IStyle.width"/> and the <see cref="IStyle.minWidth"/> value of
        /// every <see cref="VisualElement"/> under the provided root.
        /// </remarks>
        /// <param name="root">The target element</param>
        public static void AlignInspectorLabelWidth(VisualElement root)
        {
            var width = root.localBound.width * k_LabelRatio;
            AlignInspectorLabelWidth (root, width, 0);
        }

        static void AlignInspectorLabelWidth (VisualElement element, float topLevelLabelWidth, int indentLevel)
        {
            if (element.ClassListContains(UssClasses.Unity.Label))
            {
                element.style.width = Mathf.Max(topLevelLabelWidth - indentLevel * k_Indent, 0.0f);
                element.style.minWidth = 0;
                element.style.textOverflow = TextOverflow.Ellipsis;
                element.style.flexWrap = Wrap.NoWrap;
                element.style.overflow = Overflow.Hidden;
                element.style.whiteSpace = WhiteSpace.NoWrap;
            }

            if (element is Foldout)
            {
                var label = element.Q<Toggle>().Q(className:UssClasses.ListElement.ToggleInput);
                if (null != label)
                {
                    label.style.width = Mathf.Max(topLevelLabelWidth - indentLevel * k_Indent + 16.0f, 0.0f);
                    label.style.minWidth = 0;
                    label.style.textOverflow = TextOverflow.Ellipsis;
                    label.style.flexWrap = Wrap.NoWrap;
                    label.style.overflow = Overflow.Hidden;
                    label.style.whiteSpace = WhiteSpace.NoWrap;
                }

                ++indentLevel;
            }

            if (element is IReloadableElement && element.ClassListContains(UssClasses.ListElement.Item))
                --indentLevel;

            for(var i = 0; i < element.childCount; ++i)
            {
                var child = element[i];
                AlignInspectorLabelWidth (child, topLevelLabelWidth, indentLevel);
            }
        }
    }
}
