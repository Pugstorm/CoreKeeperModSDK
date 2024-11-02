using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    static class VisualElementExtensions
    {
        public static void Show(this VisualElement v) => SetVisibility(v, true);
        public static void Hide(this VisualElement v) => SetVisibility(v, false);
        public static void SetVisibility(this VisualElement v, bool isVisible) => v.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        public static void ToggleVisibility(this VisualElement v)
        {
            if (v.style.display == DisplayStyle.Flex)
                v.style.display = DisplayStyle.None;
            else
                v.style.display = DisplayStyle.Flex;
        }
        public static bool IsVisible(this VisualElement v) => v.style.display == DisplayStyle.Flex;

        public static void ForceUpdateBindings(this VisualElement element)
        {
            using (var pooled = PooledList<IBinding>.Make())
            {
                var list = pooled.List;
                PopulateBindings(element, list);

                foreach (var binding in list)
                {
                    binding.PreUpdate();
                }

                foreach (var binding in list)
                {
                    binding.Update();
                }
            }
        }

        static void PopulateBindings(this VisualElement element, List<IBinding> list)
        {
            if (element is IBindable bindable && null != bindable.binding)
                list.Add(bindable.binding);

            if (element is IBinding binding)
                list.Add(binding);

            foreach (var child in element.Children())
            {
                PopulateBindings(child, list);
            }
        }
        
        /// <summary>
        /// Retrieves a specific child element by following a path of element indexes down through the visual tree.
        /// Use this method along with <see cref="FindElementInTree"/>.
        /// </summary>
        /// <param name="childIndexes">An array of indexes that represents the path of elements that this method follows through the visual tree.</param>
        /// <returns>The child element, or null if the child is not found.</returns>
        internal static VisualElement ElementAtTreePath(this VisualElement self, List<int> childIndexes)
        {
            VisualElement child = self;
            foreach (var index in childIndexes)
            {
                if (index >= 0 && index < child.hierarchy.childCount)
                {
                    child = child.hierarchy[index];
                }
                else
                {
                    return null;
                }
            }

            return child;
        }

        internal static bool FindElementInTree(this VisualElement self, VisualElement element, List<int> outChildIndexes)
        {
            var child = element;
            var hierarchyParent = child.hierarchy.parent;

            while (hierarchyParent != null)
            {
                outChildIndexes.Insert(0, hierarchyParent.hierarchy.IndexOf(child));

                if (hierarchyParent == self)
                {
                    return true;
                }

                child = hierarchyParent;
                hierarchyParent = hierarchyParent.hierarchy.parent;
            }

            outChildIndexes.Clear();
            return false;
        }

        internal static TElement WithIconPrefix<TElement>(this TElement element, string name) where TElement : VisualElement
        {
            Resources.Templates.DotsEditorCommon.AddStyles(element);

            var icon = new VisualElement();
            icon.style.backgroundImage = UnityEditor.EditorGUIUtility.IconContent(name).image as Texture2D;
            icon.AddToClassList("icon-prefix");

            // Try to find a label and insert the icon just before it.
            var label = element.Q(className: "unity-label");
            var index = element.IndexOf(label);
            element.Insert(index, icon);
            return element;
        }
    }
}
