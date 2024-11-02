using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEditor;
using UnityObject = UnityEngine.Object;

namespace Unity.Editor.Bridge
{
    static class InspectorWindowBridge
    {
        static Func<PropertyEditor, bool> s_GetHasPreviewCachedAccessor;
        static Func<PropertyEditor, bool> GetHasPreviewAccessor()
        {
            if (s_GetHasPreviewCachedAccessor == null)
            {
                var argument = Expression.Parameter(typeof(PropertyEditor), "argument");
                var member = Expression.Field(argument, "m_HasPreview");
                var lambda = Expression.Lambda<Func<PropertyEditor, bool>>(member, argument);
                s_GetHasPreviewCachedAccessor = lambda.Compile();
            }

            return s_GetHasPreviewCachedAccessor;
        }

        static Func<PropertyEditor, IPreviewable> s_GetSelectedPreviewCachedAccessor;
        static Func<PropertyEditor, IPreviewable> GetSelectedPreviewAccessor()
        {
            if (s_GetSelectedPreviewCachedAccessor == null)
            {
                var argument = Expression.Parameter(typeof(PropertyEditor), "argument");
                var member = Expression.Field(argument, "m_SelectedPreview");
                var lambda = Expression.Lambda<Func<PropertyEditor, IPreviewable>>(member, argument);
                s_GetSelectedPreviewCachedAccessor = lambda.Compile();
            }

            return s_GetSelectedPreviewCachedAccessor;
        }

        static Func<PropertyEditor, List<IPreviewable>> s_GetAllPreviewsCachedAccessor;
        static Func<PropertyEditor, List<IPreviewable>> GetAllPreviewsAccessor()
        {
            if (s_GetAllPreviewsCachedAccessor == null)
            {
                var argument = Expression.Parameter(typeof(PropertyEditor), "argument");
                var member = Expression.Field(argument, "m_Previews");
                var lambda = Expression.Lambda<Func<PropertyEditor, List<IPreviewable>>>(member, argument);
                s_GetAllPreviewsCachedAccessor = lambda.Compile();
            }

            return s_GetAllPreviewsCachedAccessor;
        }

        internal static DataMode GetInspectorWindowDataMode(UnityEditor.Editor editor)
        {
            return editor.dataMode;
        }

        public static EditorWindow GetPreviewOwner(IPreviewable previewInstance, out bool isSelected)
        {
            isSelected = false;

            if (previewInstance == null)
                return null;

            var hasPreview = GetHasPreviewAccessor();

            foreach (var inspectorWindow in InspectorWindow.GetAllInspectorWindows())
            {
                if (!hasPreview(inspectorWindow))
                    continue;

                var selectedPreview = GetSelectedPreviewAccessor().Invoke(inspectorWindow);
                isSelected = selectedPreview == previewInstance;

                // If the preview is selected, it is found by definition
                if (isSelected)
                    return inspectorWindow;

                var allPreviews = GetAllPreviewsAccessor().Invoke(inspectorWindow);
                if (allPreviews == null || allPreviews.Count == 0)
                    continue;

                // Otherwise, it may still be owned by this window
                for (var i = 0; i < allPreviews.Count; ++i)
                {
                    if (allPreviews[i] == previewInstance)
                    {
                        // Only a manual selection of the preview type sets m_SelectedPreview, so
                        // if it was null, the inspector is using the default preview at index 0.
                        // Note: There is an issue related to this where some previews are not present
                        // in m_Previews, but are still showing up. In those cases, we simply can't
                        // know if we are the selected preview until the user manually selects any
                        // preview in the dropdown. This happens with UGUI Buttons, for example.
                        isSelected = i == 0 && selectedPreview == null;
                        return inspectorWindow;
                    }
                }
            }

            return null;
        }

        public static void RepaintAllInspectors() => InspectorWindow.RepaintAllInspectors();

        public static void ReloadAllInspectors()
        {
            InspectorWindow.RefreshInspectors();
        }
    }
}
