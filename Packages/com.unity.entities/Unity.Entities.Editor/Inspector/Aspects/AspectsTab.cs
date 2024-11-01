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
    class AspectsTab : ITabContent
    {
        readonly EntityInspectorContext m_Context;
        bool m_IsVisible;
        public string TabName { get; } = L10n.Tr("Aspects");
        public void OnTabVisibilityChanged(bool isVisible)
        {
            if (isVisible)
                Analytics.SendEditorEvent(Analytics.Window.Inspector, Analytics.EventType.InspectorTabFocus, Analytics.AspectsTabName);
            m_IsVisible = isVisible;
        }

        public AspectsTab(EntityInspectorContext context)
        {
            m_Context = context;
        }

        [UsedImplicitly]
        class AspectsTabInspector : PropertyInspector<AspectsTab>
        {
            readonly List<AspectElementBase> m_FilteredElements = new List<AspectElementBase>();
            readonly EntityInspectorAspectStructureVisitor m_AspectStructureVisitor = new EntityInspectorAspectStructureVisitor();

            EntityInspectorAspectStructure m_LastAspectStructure;
            EntityInspectorAspectsVisitor m_EntityInspectorAspectsVisitor;

            EntityInspectorContext m_Context;
            VisualElement m_Root;
            VisualElement m_AspectsRoot;
            SearchElement m_SearchElement;
            Label m_NoResultsLabel;

            public override VisualElement Build()
            {
                m_Root = Resources.Templates.Inspector.AspectsTab.Clone();

                m_AspectsRoot = m_Root.Q(classes: UssClasses.Inspector.AspectsTab.Content);
                m_AspectsRoot.RegisterCallback<GeometryChangedEvent, VisualElement>((evt, elem) =>
                    StylingUtility.AlignInspectorLabelWidth(elem), m_AspectsRoot);

                m_SearchElement = m_Root.Q<SearchElement>();
                m_SearchElement = m_Root.Q<SearchElement>();
                m_SearchElement.RegisterSearchQueryHandler<AspectElementBase>(query =>
                {
                    using var pooled = PooledList<AspectElementBase>.Make();
                    var list = pooled.List;
                    m_Root.Query<AspectElementBase>().ToList(list);
                    m_FilteredElements.Clear();
                    m_FilteredElements.AddRange(query.Apply(list));
                    SearchChanged(list);
                });

                m_Context = Target.m_Context;
                m_EntityInspectorAspectsVisitor = new EntityInspectorAspectsVisitor(m_Context);

                m_NoResultsLabel = m_Root.Q<Label>(className:UssClasses.Inspector.EmptyMessage);
                m_NoResultsLabel.text = Constants.Inspector.EmptyAspectsMessage;
                return m_Root;
            }

            public override void Update()
            {
                if (!Target.m_IsVisible)
                    return;

                BuildOrUpdateUI();
                m_NoResultsLabel.SetVisibility(m_AspectStructureVisitor.InspectorAspectStructure.Aspects.Count == 0);
            }

            void BuildOrUpdateUI()
            {
                var container = m_Context.AspectsCollectionContainer;

                m_AspectStructureVisitor.Reset();
                PropertyContainer.Accept(m_AspectStructureVisitor, ref container);
                m_AspectStructureVisitor.InspectorAspectStructure.Sort();

                if (m_LastAspectStructure == null)
                {
                    // build
                    m_LastAspectStructure = new EntityInspectorAspectStructure();
                    foreach (var aspect in m_AspectStructureVisitor.InspectorAspectStructure.Aspects)
                    {
                        PropertyContainer.Accept(m_EntityInspectorAspectsVisitor, ref container, new PropertyPath(aspect));
                        m_AspectsRoot.Add(m_EntityInspectorAspectsVisitor.Result);
                    }
                }
                else
                {
                    // update
                    UpdateUI(!m_AspectStructureVisitor.InspectorAspectStructure.Aspects.SequenceEqual(m_LastAspectStructure.Aspects));
                }

                m_LastAspectStructure.CopyFrom(m_AspectStructureVisitor.InspectorAspectStructure);
            }

            void UpdateUI(bool updateAspects)
            {
                if (!updateAspects)
                    return;

                var container = m_Context.AspectsCollectionContainer;

                InspectorUtility.Synchronize(m_LastAspectStructure.Aspects,
                    m_AspectStructureVisitor.InspectorAspectStructure.Aspects,
                    EntityInspectorAspectsComparer.Instance,
                    m_AspectsRoot,
                    Factory);

                VisualElement Factory(string path)
                {
                    PropertyContainer.Accept(m_EntityInspectorAspectsVisitor, ref container, new PropertyPath(path));
                    return m_EntityInspectorAspectsVisitor.Result;
                }
            }

            void SearchChanged(List<AspectElementBase> list)
            {
                if (m_FilteredElements.Count == 0)
                {
                    foreach (var aspectElementBase in list)
                        aspectElementBase.Show();
                }
                else
                {
                    foreach (var aspectElementBase in list)
                        aspectElementBase.SetVisibility(m_FilteredElements.Contains(aspectElementBase));
                }
            }
        }
    }
}
