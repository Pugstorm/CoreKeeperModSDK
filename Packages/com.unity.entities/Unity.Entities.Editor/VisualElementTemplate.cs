using System;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// Utility class to handle loading and cloning a <see cref="VisualTreeAsset"/>, along with its <see cref="StyleSheet"/>.
    /// </summary>
    class VisualElementTemplate
    {
        readonly Lazy<VisualTreeAsset> m_Template;
        readonly StyleSheetWithVariant m_StyleSheet;

        /// <summary>
        /// Construct a new VisualElementTemplate.
        /// </summary>
        /// <param name="name">The name of the uxml asset, without extension.</param>
        public VisualElementTemplate(string name)
        {
            m_Template = new Lazy<VisualTreeAsset>(() => EditorResources.Load<VisualTreeAsset>($"uxml/{name}.uxml", true));
            m_StyleSheet = new StyleSheetWithVariant(name);
        }

        /// <summary>
        /// Construct a new VisualElementTemplate, and load assets from a package.
        /// </summary>
        /// <param name="packageId">The package identifier.</param>
        /// <param name="name">The name of the uxml asset, without extension.</param>
        public VisualElementTemplate(string packageId, string name)
        {
            m_Template = new Lazy<VisualTreeAsset>(() => EditorResources.Load<VisualTreeAsset>(packageId, $"uxml/{name}.uxml", true));
            m_StyleSheet = new StyleSheetWithVariant(packageId, name);
        }

        /// <summary>
        /// Instantiate the visual element template.
        /// </summary>
        /// <param name="root">Optional parent of the visual element template.</param>
        /// <returns>An instance of the visual element template.</returns>
        public VisualElement Clone(VisualElement root = null)
        {
            root = CloneTemplate(root);
            AddStyles(root);
            return root;
        }

        /// <summary>
        /// Add this visual element template styles to the specified visual element.
        /// </summary>
        /// <param name="element">The visual element to add styles to.</param>
        public void AddStyles(VisualElement element)
        {
            m_StyleSheet.AddStyles(element);
        }

        /// <summary>
        /// Remove this visual element template styles from the specified visual element.
        /// </summary>
        /// <param name="element">The visual element to remove styles from.</param>
        public void RemoveStyles(VisualElement element)
        {
            m_StyleSheet.RemoveStyles(element);
        }

        VisualElement CloneTemplate(VisualElement element)
        {
            if (element == null)
                return m_Template.Value.CloneTree();

            m_Template.Value.CloneTree(element);
            return element;
        }
    }
}
