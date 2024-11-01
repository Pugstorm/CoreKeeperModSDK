using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
#if UNITY_2023_3_OR_NEWER
    [UxmlElement]
#endif
    partial class TabView : BindableElement, INotifyValueChanged<int>
    {
#if !UNITY_2023_3_OR_NEWER
        class TabViewFactory : UxmlFactory<TabView, UxmlTraits> { }
#endif

        static readonly string s_UssClassName = "tab-view";
        static readonly string s_TabHeaderClassName = "tab-view__tab-header";
        static readonly string s_TabContentClassName = "tab-view__tab-content";
        static readonly string s_TabClassName = "tab-view__tab";
        static readonly string s_ActiveTabClassName = "tab-view__tab--active";
        static readonly string s_DisabledClassName = "unity-disabled";

        public override VisualElement contentContainer { get; }

        readonly VisualElement m_Header;
        readonly VisualElement m_Content;
        int m_Index;

        readonly List<TabContent> m_Tabs = new List<TabContent>();

        public TabView()
        {
            Resources.Templates.TabView.AddStyles(this);
            AddToClassList(s_UssClassName);

            m_Header = new VisualElement();
            m_Header.AddToClassList(s_TabHeaderClassName);
            hierarchy.Add(m_Header);

            m_Content = new VisualElement();
            m_Content.AddToClassList(s_TabContentClassName);
            hierarchy.Add(m_Content);
            contentContainer = m_Content;
            m_Index = -1;
        }

        public virtual int value
        {
            get => m_Index;
            set
            {
                if (EqualityComparer<int>.Default.Equals(m_Index, value))
                    return;
                if (panel != null)
                {
                    using (ChangeEvent<int> pooled = ChangeEvent<int>.GetPooled(m_Index, value))
                    {
                        pooled.target = this;
                        SetValueWithoutNotify(value);
                        SendEvent(pooled);
                    }
                }
                else
                    SetValueWithoutNotify(value);
            }
        }

        public IEnumerable<TabContent> Tabs
        {
            get => m_Tabs;
            set
            {
                m_Tabs.Clear();
                m_Tabs.AddRange(value);
                m_Header.Clear();
                m_Content.Clear();

                var index = this.value;
                foreach (var tab in m_Tabs)
                {
                    AddTab(tab);
                }
                ResetTabs();
                SetValueWithoutNotify(index);
            }
        }

        public void SetValueWithoutNotify(int index)
        {
            if (index >= 0 && index < m_Content.childCount)
            {
                if (value >= 0)
                {
                    m_Header[value].RemoveFromClassList(s_ActiveTabClassName);
                    m_Header[value].AddToClassList(s_DisabledClassName);
                    SetVisibility(m_Content[value], false);
                }

                m_Index = index;
                m_Header[value].AddToClassList(s_ActiveTabClassName);
                m_Header[value].RemoveFromClassList(s_DisabledClassName);
                SetVisibility(m_Content[value], true);
            }
            else if (value != -1)
                ResetTabs();
        }

        internal void Internal_AddTab(TabContent content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            AddTab(content);
            ResetTabs();
            SetValueWithoutNotify(0);
        }

        void SetVisibility(VisualElement content, bool isVisible)
        {
            var tabContent = (TabContent)content;
            tabContent.SetVisibility(isVisible);
            tabContent.OnTabVisibilityChanged(isVisible);
        }

        void AddTab(TabContent content)
        {
            var tab = new Label(!string.IsNullOrEmpty(content.TabName) ? content.TabName : $"Tab {m_Header.childCount + 1}");
            tab.AddToClassList(s_TabClassName);
            tab.RegisterCallback<PointerDownEvent, TabView>((evt, tabView) => tabView.value = tabView.m_Header.IndexOf((VisualElement)evt.target), this);
            content.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == content)
                    tab.text = evt.newValue;
            });

            if (!m_Header.contentContainer.Contains(tab))
                m_Header.contentContainer.Add(tab);

            if (!m_Content.contentContainer.Contains(content))
                m_Content.contentContainer.Add(content);
        }

        void ResetTabs()
        {
            m_Index = -1;
            for (var i = 0; i < m_Content.childCount; ++i)
            {
                if (i < m_Header.childCount)
                    m_Header[i].AddToClassList(s_DisabledClassName);

                SetVisibility(m_Content[i], false);
            }
        }

        public void SwitchTab(string tabName)
        {
            for (var i = 0; i < m_Tabs.Count; i++)
            {
                if (!m_Tabs[i].TabName.Equals(tabName, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                value = i;
            }
        }
    }
}
