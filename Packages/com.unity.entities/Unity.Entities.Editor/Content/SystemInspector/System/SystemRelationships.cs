using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Unity.Properties;
using Unity.Entities.UI;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class SystemRelationships : ITabContent
    {
        public string TabName { get; } = L10n.Tr("Relationships");

        [CreateProperty] readonly SystemEntities m_Entities;
        [CreateProperty, UsedImplicitly] readonly SystemDependencies m_SystemDependencies;
        bool m_IsVisible;

        public void OnTabVisibilityChanged(bool isVisible) => m_IsVisible = isVisible;

        public SystemRelationships(SystemEntities entities, SystemDependencies systemDependencies)
        {
            m_Entities = entities;
            m_SystemDependencies = systemDependencies;
        }

        [UsedImplicitly]
        class SystemEntitiesInspector : PropertyInspector<SystemRelationships>
        {
            static readonly string k_SystemDependenciesSection = L10n.Tr("Scheduling Constraints");

            readonly Cooldown m_Cooldown = new Cooldown(TimeSpan.FromMilliseconds(Constants.Inspector.CoolDownTime));
            readonly List<QueryWithEntitiesView> m_Views = new List<QueryWithEntitiesView>();
            VisualElement m_SectionContainer;
            int m_SystemConstraintsCount;
            Label m_EmptyMessage;

            public override VisualElement Build()
            {
                var root = new VisualElement();
                Resources.Templates.DotsEditorCommon.AddStyles(root);
                m_EmptyMessage = new Label(Constants.Inspector.EmptyRelationshipMessage);
                m_EmptyMessage.AddToClassList(UssClasses.Inspector.EmptyMessage);
                root.Add(m_EmptyMessage);
                m_SectionContainer = new VisualElement();
                root.Add(m_SectionContainer);

                m_SectionContainer.Add(BuildQueriesView());
                m_SectionContainer.Add(BuildSystemConstraintsView());

                UpdateVisibility();
                Update();
                return root;
            }

            void UpdateVisibility()
            {
                var hasContentToShow = m_SystemConstraintsCount > 0 || Target.m_Entities.EntitiesFromQueries.Sum(v => v.TotalEntityCount) > 0;
                m_EmptyMessage.SetVisibility(!hasContentToShow);
                m_SectionContainer.SetVisibility(hasContentToShow);
            }

            VisualElement BuildQueriesView()
            {
                var section = new FoldoutWithoutActionButton
                {
                    HeaderName = {text = L10n.Tr("Entities")}
                };

                foreach (var queryEntities in Target.m_Entities.EntitiesFromQueries)
                {
                    var queryWithEntities = new QueryWithEntitiesView(queryEntities);
                    m_Views.Add(queryWithEntities);
                    section.Add(queryWithEntities);
                }
                return section;
            }

            VisualElement BuildSystemConstraintsView()
            {
                var updateBeforeSystemViewDataList = Target.m_SystemDependencies.GetUpdateBeforeSystemViewDataList();
                var updateAfterSystemViewDataList = Target.m_SystemDependencies.GetUpdateAfterSystemViewDataList();
                m_SystemConstraintsCount = updateBeforeSystemViewDataList.Count + updateAfterSystemViewDataList.Count;

                var currentSystemName = Target.m_SystemDependencies.CurrentSystemName;
                var sectionElement = new FoldoutWithoutActionButton
                {
                    HeaderName = { text = k_SystemDependenciesSection },
                    MatchingCount = { text = m_SystemConstraintsCount.ToString() }
                };
                var updateBeforeSection = new FoldoutWithoutActionButton
                {
                    HeaderName = { text = $"Update {currentSystemName} Before" },
                    MatchingCount = { text = updateAfterSystemViewDataList.Count.ToString() }
                };
                updateBeforeSection.Q<Toggle>().AddToClassList(UssClasses.FoldoutWithoutActionButton.ToggleNoBorder);
                var updateAfterSection = new FoldoutWithoutActionButton
                {
                    HeaderName = { text = $"Update {currentSystemName} After" },
                    MatchingCount = { text = updateBeforeSystemViewDataList.Count.ToString() }
                };
                updateAfterSection.Q<Toggle>().AddToClassList(UssClasses.FoldoutWithoutActionButton.ToggleNoBorder);
                sectionElement.Add(updateBeforeSection);
                sectionElement.Add(updateAfterSection);
                foreach (var systemDependencyInfo in updateAfterSystemViewDataList)
                {
                    updateBeforeSection.Add(new SystemDependencyView(systemDependencyInfo));
                }
                foreach (var systemDependencyInfo in updateBeforeSystemViewDataList)
                {
                    updateAfterSection.Add(new SystemDependencyView(systemDependencyInfo));
                }
                return sectionElement;
            }

            public override void Update()
            {
                if (!Target.m_IsVisible || !m_Cooldown.Update(DateTime.UtcNow))
                    return;

                foreach (var view in m_Views)
                {
                    view.Update();
                }

                UpdateVisibility();
            }
        }
    }
}
