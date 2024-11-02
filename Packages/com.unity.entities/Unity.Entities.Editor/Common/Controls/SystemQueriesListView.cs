using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class SystemQueriesListView : VisualElement
    {
        static readonly string k_SectionHeader = L10n.Tr("Systems");
        readonly string m_MoreLabelText;
        readonly string m_MoreLabelWithFilterText;

        public FoldoutWithoutActionButton m_SystemElements;
        Label m_MoreLabel;
        List<string> m_SearchTerms;

        public SystemQueriesListView(List<SystemQueriesViewData> systems, List<string> searchTerms, string moreLabel, string moreLabelWithFilter)
        {
            m_SearchTerms = searchTerms;
            m_MoreLabelText = moreLabel;
            m_MoreLabelWithFilterText = moreLabelWithFilter;

            Resources.Templates.SystemListView.Clone(this);
            var content = this.Q(className: UssClasses.SystemListView.ContentElement);

            m_SystemElements = new FoldoutWithoutActionButton
            {
                HeaderName = { text = k_SectionHeader },
                MatchingCount = { text = systems.Count.ToString() }
            };

            content.Insert(0, m_SystemElements);

            m_MoreLabel = content.Q<Label>(className: UssClasses.SystemListView.MoreLabel);
            m_MoreLabel.text = m_MoreLabelText;
            m_MoreLabel.Hide();

            Update(systems);
        }

        public void OnSearchTermsChanged() => m_MoreLabel.text = m_SearchTerms.Count > 0 ? m_MoreLabelWithFilterText : m_MoreLabelText;

        bool Filter(SystemQueriesViewData system)
        {
            if (m_SearchTerms.Count == 0)
                return true;

            foreach (var term in m_SearchTerms)
            {
                var matches = system.SystemName.IndexOf(term, StringComparison.InvariantCultureIgnoreCase) >= 0;
                if (matches)
                    return true;
            }

            return false;
        }

        public void Update(List<SystemQueriesViewData> systems)
        {
            var ui = m_SystemElements.Query<SystemQueriesView>().ToList();
            m_SystemElements.MatchingCount.text = systems.Count.ToString();

            var uiIndex = 0;
            var systemIndex = 0;
            var visibleItemCount = 0;
            var atLeastOneMoreSystemNotShown = false;
            while (uiIndex < ui.Count && systemIndex < systems.Count && visibleItemCount < Constants.Inspector.MaxVisibleSystemCount)
            {
                if (!Filter(systems[systemIndex]))
                {
                    systemIndex++;
                    continue;
                }

                ui[uiIndex].Update(systems[systemIndex]);
                uiIndex++;
                systemIndex++;
                visibleItemCount++;
            }

            while (systemIndex < systems.Count)
            {
                if (!Filter(systems[systemIndex]))
                {
                    systemIndex++;
                    continue;
                }

                if (visibleItemCount++ == Constants.Inspector.MaxVisibleSystemCount)
                {
                    atLeastOneMoreSystemNotShown = true;
                    break;
                }

                var systemElement = new SystemQueriesView(systems[systemIndex]);
                m_SystemElements.Add(systemElement);
                systemIndex++;
            }

            for (; uiIndex < ui.Count; uiIndex++)
            {
                ui[uiIndex].RemoveFromHierarchy();
            }

            m_MoreLabel.SetVisibility(atLeastOneMoreSystemNotShown);
        }
    }
}
