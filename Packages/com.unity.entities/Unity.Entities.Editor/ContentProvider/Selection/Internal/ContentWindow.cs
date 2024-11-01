using System;
using Unity.Properties;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    sealed class ContentWindow : EditorWindow
    {
        public static ContentWindow Show(ContentProvider provider, ContentWindowParameters options)
        {
            var window = Create(provider, options);
            window.Show();
            return window;
        }

        public static ContentWindow Create(ContentProvider provider, ContentWindowParameters options)
        {
            var window = CreateInstance<ContentWindow>();
            window.SetContent(new SerializableContent {Provider = provider}, options);
            return window;
        }

        [SerializeField] SerializableContent m_Content;
        [NonSerialized] DisplayContent m_DisplayContent;
        [SerializeField] Vector2 m_ScrollPosition;
        [SerializeField] ContentWindowParameters m_Options;

        ScrollView m_ScrollView;

        void SetContent(SerializableContent content, ContentWindowParameters options)
        {
            m_Content = content;
            m_DisplayContent = new DisplayContent(content)
            {
                InspectionContext = {ApplyInspectorStyling = options.ApplyInspectorStyling}
            };

            m_Options = options;

            var element = m_DisplayContent.CreateGUI();
            if (options.AddScrollView)
            {
                m_ScrollView.Add(element);
                rootVisualElement.Add(m_ScrollView);
            }
            else
            {
                rootVisualElement.Add(element);
            }

            if (options.ApplyInspectorStyling)
                element.contentContainer.style.paddingLeft = 15;

            m_DisplayContent.Content.Load();
            m_DisplayContent.Update();

            titleContent.text = m_Content.Name ?? nameof(ContentWindow);
            minSize = m_Options.MinSize;
        }

        // Invoked by the Unity update loop
        void OnEnable()
        {
            m_ScrollView = new ScrollView {scrollOffset = m_ScrollPosition};
        }

        // Invoked by the Unity update loop
        void Update()
        {
            // When reloading the window through the internal menu item, the serialized data gets patched through after
            // the OnEnable call, so we try to load the data here instead.
            if (null == m_DisplayContent)
            {
                if (null != m_Content)
                    SetContent(m_Content, m_Options);
                else
                {
                    Close();
                    return;
                }
            }

            m_ScrollPosition = m_ScrollView.scrollOffset;
            titleContent.text = !string.IsNullOrEmpty(m_Content.Name)
                ? m_Content.Name
                : TypeUtility.GetTypeDisplayName(m_Content.GetType());
            m_DisplayContent.Update();
            if (!m_DisplayContent.IsValid)
                Close();

            // We are saving here because we want to store the data inside the editor window so that it survives both
            // domain reloads and closing/re-opening Unity.
            m_Content.Save();
        }
    }
}
