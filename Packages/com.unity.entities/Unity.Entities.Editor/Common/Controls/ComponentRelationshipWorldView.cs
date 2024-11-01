using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class ComponentRelationshipWorldView : VisualElement
    {
        static readonly string k_Title = L10n.Tr("Entities");
        static readonly string k_MoreLabel = L10n.Tr("More systems are matching this component. You can use the search to filter systems by name.");
        static readonly string k_MoreLabelWithFilter = L10n.Tr("More systems are matching this search. Refine the search terms to find a particular system.");

        ComponentRelationshipWorldViewData m_Data;

        readonly QueryWithEntitiesView m_EntitiesSection;
        readonly SystemQueriesListView m_SystemQueriesListView;

        public ComponentRelationshipWorldView(ComponentRelationshipWorldViewData data)
        {
            var root = new FoldoutWithoutActionButton
            {
                HeaderName = {text = data.World.Name},
                MatchingCount = {text = "World"}
            };

            m_Data = data;

            m_EntitiesSection = new QueryWithEntitiesView(data.QueryWithEntitiesViewData);
            var entitySectionName = m_EntitiesSection.Q<Label>(className: UssClasses.FoldoutWithActionButton.Name);
            entitySectionName.text = k_Title;
            m_EntitiesSection.Q<VisualElement>(className: UssClasses.FoldoutWithActionButton.Icon).AddToClassList(UssClasses.QueryWithEntities.EntityIcon);

            m_SystemQueriesListView = new SystemQueriesListView(new List<SystemQueriesViewData>(), new List<string>(),k_MoreLabel, k_MoreLabelWithFilter);
            var systemHeader = m_SystemQueriesListView.Q<FoldoutWithoutActionButton>();
            systemHeader.Q<Toggle>().AddToClassList(UssClasses.FoldoutWithoutActionButton.ToggleNoBorder);
            m_SystemQueriesListView.Q(className: UssClasses.FoldoutWithoutActionButton.Icon).AddToClassList(UssClasses.SystemListView.SystemIcon);

            root.Add(m_EntitiesSection);
            root.Add(m_SystemQueriesListView);
            Add(root);
        }

        public void Update()
        {
            m_EntitiesSection.Update();
            m_Data.ComponentMatchingSystems.Update();
            m_SystemQueriesListView.Update(m_Data.ComponentMatchingSystems.Systems);
            foreach (var queryView in m_SystemQueriesListView.Q<FoldoutWithoutActionButton>().Query(className:"foldout-with-action-button__name").ToList())
            {
                queryView.style.unityFontStyleAndWeight = FontStyle.Normal;
            }
        }

        public bool IsEmpty => m_Data.QueryWithEntitiesViewData.TotalEntityCount == 0 && m_Data.ComponentMatchingSystems.Systems.Count == 0;
    }
}
