using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// Utility class to handle loading and adding a <see cref="StyleSheet"/>, along with its pro skin variant.
    /// </summary>
    class StyleSheetWithVariant
    {
        readonly string m_Name;
        readonly Lazy<StyleSheet> m_StyleSheet;
        readonly Lazy<StyleSheet> m_StyleSheetVariant;

        /// <summary>
        /// Construct a new <see cref="StyleSheetWithVariant"/>.
        /// </summary>
        /// <param name="name">The name of the uss asset, without extension.</param>
        public StyleSheetWithVariant(string name)
        {
            m_Name = name;
            m_StyleSheet = new Lazy<StyleSheet>(() => EditorResources.Load<StyleSheet>($"uss/{name}.uss", false));
            m_StyleSheetVariant = new Lazy<StyleSheet>(() => EditorResources.Load<StyleSheet>($"uss/{name}_{(EditorGUIUtility.isProSkin ? "dark" : "light")}.uss", false));
        }

        /// <summary>
        /// Construct a new <see cref="StyleSheetWithVariant"/>, and load assets from a package.
        /// </summary>
        /// <param name="packageId">The package identifier.</param>
        /// <param name="name">The name of the uss asset, without extension.</param>
        public StyleSheetWithVariant(string packageId, string name)
        {
            m_Name = name;
            m_StyleSheet = new Lazy<StyleSheet>(() => EditorResources.Load<StyleSheet>(packageId, $"uss/{name}.uss", false));
            m_StyleSheetVariant = new Lazy<StyleSheet>(() => EditorResources.Load<StyleSheet>(packageId, $"uss/{name}_{(EditorGUIUtility.isProSkin ? "dark" : "light")}.uss", false));
        }

        /// <summary>
        /// Add this style sheet to the specified visual element.
        /// </summary>
        /// <param name="element">The visual element to add styles to.</param>
        public void AddStyles(VisualElement element)
        {
            if (m_StyleSheet.Value != null)
            {
                element.styleSheets.Add(m_StyleSheet.Value);
                element.AddToClassList(m_Name.Substring(m_Name.LastIndexOf('/') + 1));
            }

            if (m_StyleSheetVariant.Value != null)
                element.styleSheets.Add(m_StyleSheetVariant.Value);
        }

        /// <summary>
        /// Remove this style sheet from the specified visual element.
        /// </summary>
        /// <param name="element">The visual element to remove styles from.</param>
        public void RemoveStyles(VisualElement element)
        {
            if (m_StyleSheet.Value != null)
            {
                element.styleSheets.Remove(m_StyleSheet.Value);
                element.RemoveFromClassList(m_Name);
            }

            if (m_StyleSheetVariant.Value != null)
                element.styleSheets.Remove(m_StyleSheetVariant.Value);
        }
    }
}
