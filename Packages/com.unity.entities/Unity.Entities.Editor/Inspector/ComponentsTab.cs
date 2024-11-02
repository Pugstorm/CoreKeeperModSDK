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
    class ComponentsTab : ITabContent
    {
        readonly EntityInspectorContext m_Context;
        bool m_IsVisible;

        public string TabName { get; } = L10n.Tr("Components");
        public void OnTabVisibilityChanged(bool isVisible)
        {
            if (isVisible)
                Analytics.SendEditorEvent(Analytics.Window.Inspector, Analytics.EventType.InspectorTabFocus, Analytics.ComponentsTabName);
            m_IsVisible = isVisible;
        }

        public ComponentsTab(EntityInspectorContext entityInspectorContext)
        {
            m_Context = entityInspectorContext;
        }

        [UsedImplicitly]
        class ComponentsTabInspector : PropertyInspector<ComponentsTab>
        {
            readonly List<ComponentElementBase> m_FilteredElements = new List<ComponentElementBase>();

            EntityInspectorComponentStructure m_CurrentComponentStructure;
            EntityInspectorComponentStructure m_LastComponentStructure;
            EntityInspectorBuilderVisitor m_InspectorBuilderVisitor;
            VisualElement m_Root;
            TagComponentContainer m_TagsRoot;
            VisualElement m_ComponentsRoot;
            SearchElement m_SearchElement;

            public override VisualElement Build()
            {
                m_Root = Resources.Templates.Inspector.ComponentsTab.Clone();

                m_SearchElement = m_Root.Q<SearchElement>(className: UssClasses.Inspector.ComponentsTab.SearchField);
                m_SearchElement.RegisterSearchQueryHandler<ComponentElementBase>(query =>
                {
                    using var pooled = PooledList<ComponentElementBase>.Make();
                    var list = pooled.List;
                    m_Root.Query<ComponentElementBase>().ToList(list);
                    m_FilteredElements.Clear();
                    m_FilteredElements.AddRange(query.Apply(list));
                    SearchChanged(list);
                });

                m_TagsRoot = new TagComponentContainer(Target.m_Context);
                m_ComponentsRoot = new VisualElement();
                m_Root.Add(m_TagsRoot);
                m_Root.Add(m_ComponentsRoot);

                m_Root.RegisterCallback<GeometryChangedEvent, VisualElement>((_, elem) =>
                {
                    StylingUtility.AlignInspectorLabelWidth(elem);
                }, m_Root);

                m_InspectorBuilderVisitor = new EntityInspectorBuilderVisitor(Target.m_Context);
                m_CurrentComponentStructure = new EntityInspectorComponentStructure();
                BuildOrUpdateUI();

                return m_Root;
            }

            public override void Update()
            {
                if (!Target.m_IsVisible || !Target.m_Context.TargetExists())
                    return;

                BuildOrUpdateUI();
            }

            void BuildOrUpdateUI()
            {
                m_CurrentComponentStructure.Reset();

                var container = Target.m_Context.EntityContainer;
                var propertyBag = PropertyBag.GetPropertyBag<EntityContainer>();
                var properties = propertyBag.GetProperties(ref container);

                foreach (var property in properties)
                {
                    if (property is IComponentProperty componentProperty)
                    {
                        if (componentProperty.Type == ComponentPropertyType.Tag)
                            m_CurrentComponentStructure.Tags.Add(componentProperty.Name);
                        else
                            m_CurrentComponentStructure.Components.Add(componentProperty.Name);
                    }
                }

                m_CurrentComponentStructure.Sort();

                if (m_LastComponentStructure == null)
                {
                    m_LastComponentStructure = new EntityInspectorComponentStructure();
                    foreach (var path in m_CurrentComponentStructure.Tags)
                    {
                        PropertyContainer.Accept(m_InspectorBuilderVisitor, ref container, new PropertyPath(path));
                        m_TagsRoot.Add(m_InspectorBuilderVisitor.Result);
                    }

                    foreach (var path in m_CurrentComponentStructure.Components)
                    {
                        PropertyContainer.Accept(m_InspectorBuilderVisitor, ref container, new PropertyPath(path));
                        m_ComponentsRoot.Add(m_InspectorBuilderVisitor.Result);
                    }
                }
                else
                {
                    UpdateUI(!m_CurrentComponentStructure.Tags.SequenceEqual(m_LastComponentStructure.Tags),
                             !m_CurrentComponentStructure.Components.SequenceEqual(m_LastComponentStructure.Components));
                }

                m_LastComponentStructure.CopyFrom(m_CurrentComponentStructure);
            }

            void UpdateUI(bool updateTags, bool updateComponents)
            {
                if (!updateTags && !updateComponents)
                    return;

                var container = Target.m_Context.EntityContainer;

                // update tags
                if (updateTags)
                {
                    InspectorUtility.Synchronize(m_LastComponentStructure.Tags,
                        m_CurrentComponentStructure.Tags,
                                StringComparer.OrdinalIgnoreCase,
                                m_TagsRoot,
                                Factory);
                }

                // update regular components
                if (updateComponents)
                {
                    InspectorUtility.Synchronize(m_LastComponentStructure.Components,
                        m_CurrentComponentStructure.Components,
                                EntityInspectorComponentsComparer.Instance,
                                m_ComponentsRoot,
                                Factory);
                }

                VisualElement Factory(string path)
                {
                    PropertyContainer.Accept(m_InspectorBuilderVisitor, ref container, new PropertyPath(path));
                    return m_InspectorBuilderVisitor.Result;
                }
            }

            void SearchChanged(List<ComponentElementBase> list)
            {
                if (m_FilteredElements.Count == 0)
                {
                    foreach (var componentElementBase in list)
                        componentElementBase.Show();
                }
                else
                {
                    foreach (var componentElementBase in list)
                        componentElementBase.SetVisibility(m_FilteredElements.Contains(componentElementBase));
                }
            }
        }
    }
}
