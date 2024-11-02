using System;
using JetBrains.Annotations;
using Unity.Properties;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    struct ContentNotReady
    {
        readonly ContentProvider m_Provider;
        string m_PreviousName;
        string m_CachedText;

        public ContentNotReady(ContentProvider provider)
        {
            m_Provider = provider;
            m_PreviousName = string.Empty;
            m_CachedText = string.Empty;
            CacheDisplayText();
        }

        [CreateProperty, UsedImplicitly]
        public string Text
        {
            get
            {
                CacheDisplayText();
                return m_CachedText;
            }
        }

        void CacheDisplayText()
        {
            var name = m_Provider.Name;
            if (m_PreviousName == name)
                return;
            m_PreviousName = name;
            m_CachedText = $"{(string.IsNullOrEmpty(name) ? TypeUtility.GetTypeDisplayName(m_Provider.GetType()) : name)} is not ready for display";
        }
    }

    [UsedImplicitly]
    class ContentNotReadyInspector : PropertyInspector<ContentNotReady>
    {
        const string k_BasePath = "Packages/com.unity.entities/Unity.Entities.Editor/ContentProvider/Selection/";
        const string k_Prefix = "content-not-ready__spinner-";

        VisualElement m_Spinner;
        int m_Index;
        long m_TimePerImage = Convert.ToInt64(1000.0f / 12.0f);

        public override VisualElement Build()
        {
            var template = EditorGUIUtility.Load(k_BasePath + "uxml/content-not-ready.uxml") as VisualTreeAsset;
            var root = template.CloneTree();
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_BasePath + "uss/content-not-ready.uss");
            root.styleSheets.Add(styleSheet);
            var skinStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_BasePath + $"uss/content-not-ready_{(EditorGUIUtility.isProSkin?"dark":"light")}.uss");
            root.styleSheets.Add(skinStyleSheet);

            m_Spinner = root.Q(className: "content-not-ready__spinner");
            m_Spinner.schedule.Execute(TimerUpdateEvent).Every(m_TimePerImage);
            return root;
        }

        void TimerUpdateEvent(TimerState obj)
        {
            m_Spinner.RemoveFromClassList($"{k_Prefix}{m_Index}");
            m_Index = (m_Index + 1) % 12;
            m_Spinner.AddToClassList($"{k_Prefix}{m_Index}");
        }
    }
}
