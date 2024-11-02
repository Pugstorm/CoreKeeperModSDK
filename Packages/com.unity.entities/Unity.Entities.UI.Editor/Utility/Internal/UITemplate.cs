using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    readonly struct UITemplate
    {
        public static UITemplate Null = default;
        public static VisualElement m_Container;

        readonly string m_UxmlPath;
        readonly string m_UssPath;
        readonly string m_Name;

        VisualTreeAsset Template => EditorGUIUtility.Load(m_UxmlPath) as VisualTreeAsset;
        StyleSheet StyleSheet => AssetDatabase.LoadAssetAtPath<StyleSheet>(m_UssPath);

        public UITemplate(string name)
        {
            m_Name = name;
            m_UxmlPath = Resources.UxmlFromName(m_Name);
            m_UssPath = Resources.UssFromName(m_Name);
            m_Container = new VisualElement();
        }

        /// <summary>
        /// Clones the template into the given root element and applies the style sheets from the template.
        /// </summary>
        /// <param name="root">The element that will serve as the root for cloning the template.</param>
        public VisualElement Clone(VisualElement root = null)
        {
            root = CloneTemplate(root);
            AddStyleSheetSkinVariant(root);
            return root;
        }

        public VisualElement CloneWithoutTemplateContainer()
        {
            m_Container = CloneTemplate(m_Container);
            if (m_Container.childCount > 1)
                Debug.LogWarning($"{nameof(UITemplate)}.{nameof(CloneWithoutTemplateContainer)} should only be called with uxml files containing a single root. Template called `{m_Name}` contains {m_Container.childCount} roots.");

            var child = m_Container[0];
            AddStyleSheetSkinVariant(child);
            m_Container.Clear();
            return child;
        }

        public void AddStyles(VisualElement element)
        {
            AddStyleSheetSkinVariant(element);
        }

        public void RemoveStyles(VisualElement element)
        {
            RemoveStyleSheetSkinVariant(element);
        }

        VisualElement CloneTemplate(VisualElement element)
        {
            if (null == Template)
            {
                return element;
            }

            if (null == element)
            {
                return Template.CloneTree();
            }

            Template.CloneTree(element);
            return element;
        }

        void AddStyleSheetSkinVariant(VisualElement element)
        {
            if (null == StyleSheet)
            {
                return;
            }

            if (null == element)
            {
                return;
            }

            element.styleSheets.Add(StyleSheet);
            var assetPath = AssetDatabase.GetAssetPath(StyleSheet);
            assetPath = assetPath.Insert(assetPath.LastIndexOf('.'), Resources.SkinSuffix);
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<StyleSheet>(assetPath) is var skin && null != skin)
            {
                element.styleSheets.Add(skin);
            }
        }

        void RemoveStyleSheetSkinVariant(VisualElement element)
        {
            if (null == StyleSheet)
            {
                return;
            }

            if (null == element)
            {
                return;
            }

            element.styleSheets.Remove(StyleSheet);
            var assetPath = AssetDatabase.GetAssetPath(StyleSheet);
            assetPath = assetPath.Insert(assetPath.LastIndexOf('.'), Resources.SkinSuffix);
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<StyleSheet>(assetPath) is var skin && null != skin)
            {
                element.styleSheets.Remove(skin);
            }
        }
    }
}
