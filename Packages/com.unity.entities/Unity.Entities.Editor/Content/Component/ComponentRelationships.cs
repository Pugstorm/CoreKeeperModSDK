using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Entities.UI;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class ComponentRelationships : ITabContent
    {
        public string TabName { get; } = L10n.Tr("Relationships");
        public readonly Type ComponentType;

        bool m_IsVisible;

        public ComponentRelationships(Type componentType)
        {
            ComponentType = componentType;
        }

        public void OnTabVisibilityChanged(bool isVisible) => m_IsVisible = isVisible;

        [UsedImplicitly]
        class ComponentRelationshipsInspector : PropertyInspector<ComponentRelationships>
        {
            readonly Cooldown m_Cooldown = new Cooldown(TimeSpan.FromMilliseconds(Constants.Inspector.CoolDownTime));
            List<ComponentRelationshipWorldView> m_WorldSections = new List<ComponentRelationshipWorldView>();
            bool m_AnyResults;
            VisualElement m_Root;
            readonly Label m_NoResultsLabel = new Label(Constants.Inspector.EmptyRelationshipMessage);
            readonly WorldListChangeTracker m_WorldListChangeTracker = new WorldListChangeTracker();
            Label m_EmptyMessage;

            public override VisualElement Build()
            {
                m_Root = new VisualElement();
                Resources.Templates.DotsEditorCommon.AddStyles(m_Root);
                m_NoResultsLabel.AddToClassList(UssClasses.Inspector.EmptyMessage);
                m_Root.Add(m_NoResultsLabel);

                return m_Root;
            }

            public override void Update()
            {
                if (!Target.m_IsVisible || !m_Cooldown.Update(DateTime.Now))
                    return;

                m_AnyResults = false;

                if (m_WorldListChangeTracker.HasChanged())
                    CreateWorldSections();

                foreach (var worldSection in m_WorldSections)
                {
                    worldSection.Update();
                    worldSection.SetVisibility(!worldSection.IsEmpty);
                    if (!worldSection.IsEmpty)
                        m_AnyResults = true;
                }

                m_NoResultsLabel.SetVisibility(!m_AnyResults);
            }

            void CreateWorldSections()
            {
                foreach (var worldSection in m_WorldSections)
                {
                    worldSection.RemoveFromHierarchy();
                }

                m_WorldSections.Clear();

                foreach (var world in World.All)
                {
                    if (world == null || !world.IsCreated)
                        continue;

                    var worldSectionView = new ComponentRelationshipWorldView(new ComponentRelationshipWorldViewData(world, Target.ComponentType));
                    m_WorldSections.Add(worldSectionView);
                    m_Root.Add(worldSectionView);
                }
            }
        }
    }
}
