using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Properties;
using Unity.Entities.UI;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class EntityQueryDisplay
    {
        public readonly EntityQueryContent Content;
        public readonly List<EntityViewData> SourceEntities;
        public readonly List<EntityViewData> FilteredEntities;
        public readonly List<ComponentViewData> SourceComponents;
        public readonly List<ComponentViewData> FilteredComponents;

        public World World => Content.World;
        public EntityQuery Query => Content.Query;

        public EntityQueryDisplay(EntityQueryContent content)
        {
            Content = content;
            SourceEntities = new List<EntityViewData>(content.Query.CalculateEntityCount());
            using (var entities = content.Query.ToEntityArray(Allocator.Temp))
            {
                foreach (var entity in entities)
                {
                    SourceEntities.Add(new EntityViewData(content.World, entity));
                }
            }

            SourceComponents = Query.GetComponentDataFromQuery().ToList();

            FilteredEntities = new List<EntityViewData>(SourceEntities);
            FilteredComponents = new List<ComponentViewData>(SourceComponents);
        }
    }

    [UsedImplicitly]
    class EntityQueryDisplayInspector : PropertyInspector<EntityQueryDisplay>
    {
        int m_LastHash;
        SearchElement m_EntitiesSearchElement;
        SearchElement m_ComponentsSearchElement;
        ListView m_EntitiesListView;
        ListView m_ComponentsListView;
        TabContent m_EntitiesTab;
        TabContent m_ComponentTab;

        readonly CenteredMessageElement m_EmptyEntitiesMessage = new CenteredMessageElement { Message = L10n.Tr("No Entities") };
        readonly CenteredMessageElement m_UniversalQueryMessage = new CenteredMessageElement { Message = L10n.Tr("This is a Universal Query. Universal Queries don’t have components. It will match all Entities.") };
        readonly CenteredMessageElement m_NoEntitiesResultMessage = new CenteredMessageElement { Message = L10n.Tr("No matches") };
        readonly CenteredMessageElement m_NoComponentResultMessage = new CenteredMessageElement { Message = L10n.Tr("No matches") };

        int GetQueryComponentCount()
        {
            var queryDesc = Target.Query.GetEntityQueryDesc();
            return queryDesc.All.Length + queryDesc.Any.Length + queryDesc.None.Length;
        }

        public override VisualElement Build()
        {
            var root = new VisualElement();
            root.RegisterCallback<DetachFromPanelEvent, EntityQueryDisplayInspector>((ev, @this) => Selection.selectionChanged -= @this.HandleGlobalSelectionChanged, this);

            Resources.AddCommonVariables(root);
            root.AddToClassList(UssClasses.Content.Query.EntityQuery.Container);
            var header = Resources.Templates.ContentProvider.EntityQueryHeader.Clone();
            var fromSystem = Target.Content.SystemProxy.Valid;

            if (fromSystem)
            {
                header.AddToClassList(UssClasses.Content.Query.EntityQuery.SystemQuery);
                header.Q<Label>(className: UssClasses.Content.Query.EntityQuery.HeaderMainTitle).text = $"Query #{Target.Content.QueryOrder}";
                header.Q<Label>(className: UssClasses.Content.Query.EntityQuery.HeaderSubTitle).text = Target.Content.SystemProxy.NicifiedDisplayName;
                header.Query(className: UssClasses.Content.Query.EntityQuery.HeaderGoTo).ForEach(v => v.RegisterCallback<MouseDownEvent>((evt) =>
                {
                    evt.StopPropagation();
#if !UNITY_2023_2_OR_NEWER
                    evt.PreventDefault();
#endif
                    SystemScheduleWindow.HighlightSystem(Target.Content.SystemProxy);
                    ContentUtilities.ShowSystemInspectorContent(Target.Content.SystemProxy);
                }));
            }
            else
            {
                header.AddToClassList(UssClasses.Content.Query.EntityQuery.ComponentQuery);
                var types = Target.Content.Query.GetQueryTypes();
                var componentType = types.Length > 0 ? types[0].GetManagedType() : typeof(Entity);
                header.Q<Label>(className: UssClasses.Content.Query.EntityQuery.HeaderMainTitle).text = componentType.Name;
                header.Q<Label>(className: UssClasses.Content.Query.EntityQuery.HeaderSubTitle).text = L10n.Tr("Component Query");
                header.Query(className: UssClasses.Content.Query.EntityQuery.HeaderGoTo).ForEach(v => v.RegisterCallback<MouseDownEvent>((evt) =>
                {
                    evt.StopPropagation();
#if !UNITY_2023_2_OR_NEWER
                    evt.PreventDefault();
#endif
                    ComponentsWindow.HighlightComponent(@componentType);
                    ContentUtilities.ShowComponentInspectorContent(@componentType);
                }));
            }

            var tabView = new TabView();
            m_EntitiesTab = new TabContent { TabName = $"Entities   {Target.SourceEntities.Count}" };
            m_ComponentTab = new TabContent { TabName = $"Components   {GetQueryComponentCount()}" };
            tabView.Tabs = new[] { m_ComponentTab, m_EntitiesTab };

            switch (Target.Content.Tab)
            {
                case EntityQueryContentTab.Components:
                    tabView.value = 0;
                    break;
                case EntityQueryContentTab.Entities:
                    tabView.value = 1;
                    break;
            }

            root.Add(header);
            root.Add(tabView);

            m_LastHash = Target.Query.GetCombinedComponentOrderVersion();

            var entitiesSearchContainer = new VisualElement();
            entitiesSearchContainer.AddToClassList(UssClasses.Content.Query.EntityQuery.SearchContainer);
            m_EntitiesSearchElement = new SearchElement { SearchDelay = 200 };
            m_EntitiesSearchElement.AddSearchDataProperty(new PropertyPath("EntityName"));
            m_EntitiesSearchElement.AddSearchFilterProperty("c", new PropertyPath("ComponentCount"));
            m_EntitiesSearchElement.AddSearchFilterPopupItem("c", "Component Count");
            m_EntitiesSearchElement.AddSearchFilterProperty("i", new PropertyPath("Index"));
            m_EntitiesSearchElement.AddSearchFilterPopupItem("i", "Entity Index");
            m_EntitiesSearchElement.AddSearchFilterProperty("id", new PropertyPath("InstanceId"));
            m_EntitiesSearchElement.AddSearchFilterPopupItem("id", "Instance Id");
            entitiesSearchContainer.Add(m_EntitiesSearchElement);
            m_EntitiesListView = new ListView { fixedItemHeight = 18, showAlternatingRowBackgrounds = AlternatingRowBackground.None, selectionType = SelectionType.Single };
            m_EntitiesListView.AddToClassList(UssClasses.Content.Query.EntityQuery.ListView);

            m_EntitiesSearchElement.RegisterSearchQueryHandler<EntityViewData>(search =>
            {
                Target.FilteredEntities.Clear();
                Target.FilteredEntities.AddRange(search.Apply(Target.SourceEntities));
                m_EntitiesListView.Rebuild();
                UpdateEntitiesEmptyMessagesVisibility();
            });

            m_EntitiesListView.makeItem += () => new EntityView(default);
            m_EntitiesListView.bindItem += (element, i) =>
            {
                if (element is EntityView entityView)
                    entityView.Update(Target.FilteredEntities[i]);
            };
            m_EntitiesListView.itemsSource = Target.FilteredEntities;
            m_EntitiesListView.selectionChanged += objects =>
            {
                if(!objects.Any())
                    return;
                var evd = objects.Cast<EntityViewData>().Single();
                EntitySelectionProxy.SelectEntity(evd.World, evd.Entity);
            };
            m_EntitiesTab.Add(entitiesSearchContainer);
            m_EntitiesTab.Add(m_EmptyEntitiesMessage);
            m_EntitiesTab.Add(m_NoEntitiesResultMessage);
            m_EntitiesTab.Add(m_EntitiesListView);
            UpdateEntitiesEmptyMessagesVisibility();

            var componentsSearchContainer = new VisualElement();
            componentsSearchContainer.AddToClassList(UssClasses.Content.Query.EntityQuery.SearchContainer);
            m_ComponentsSearchElement = new SearchElement { SearchDelay = 200 };
            m_ComponentsSearchElement.AddSearchDataProperty(new PropertyPath("Name"));
            m_ComponentsSearchElement.RegisterSearchQueryHandler<ComponentViewData>(search =>
            {
                Target.FilteredComponents.Clear();
                Target.FilteredComponents.AddRange(search.Apply(Target.SourceComponents));
                m_ComponentsListView.Rebuild();

                UpdateComponentsEmptyMessagesVisibility();
            });

            componentsSearchContainer.Add(m_ComponentsSearchElement);
            m_ComponentTab.Add(componentsSearchContainer);
            m_ComponentTab.Add(m_NoComponentResultMessage);
            m_ComponentTab.Add(m_UniversalQueryMessage);

            m_ComponentsListView = new ListView { fixedItemHeight = 18, showAlternatingRowBackgrounds = AlternatingRowBackground.None, selectionType = SelectionType.Single };
            m_ComponentsListView.AddToClassList(UssClasses.Content.Query.EntityQuery.ListView);
            m_ComponentsListView.makeItem += () =>
            {
                var view = new ComponentView(default);
                if (!fromSystem) view.m_AccessMode.Hide();
                return view;
            };
            m_ComponentsListView.bindItem += (element, i) =>
            {
                if (element is ComponentView cv)
                    cv.Update(Target.FilteredComponents[i]);
            };
            m_ComponentsListView.itemsSource = Target.FilteredComponents;
            m_ComponentsListView.selectionChanged += objects =>
            {
                if(!objects.Any())
                    return;
                var cvd = objects.Cast<ComponentViewData>().Single();

                ComponentsWindow.HighlightComponent(@cvd.InComponentType);
                ContentUtilities.ShowComponentInspectorContent(@cvd.InComponentType);
            };

            m_ComponentTab.Add(m_ComponentsListView);
            UpdateComponentsEmptyMessagesVisibility();

            Selection.selectionChanged += HandleGlobalSelectionChanged;

            return root;
        }

        void HandleGlobalSelectionChanged()
        {
            if (Selection.activeObject is EntitySelectionProxy selectedProxy && selectedProxy.World == Target.World)
            {
                var idx = Target.FilteredEntities.FindIndex(e => e.Entity == selectedProxy.Entity);
                HandleSelection(idx, m_EntitiesListView, m_EntitiesListView, m_ComponentsListView);
            }
            else if (Selection.activeObject is InspectorContent inspectorContent && inspectorContent.Content.Provider is ComponentContentProvider componentContentProvider)
            {
                var idx = Target.FilteredComponents.FindIndex(c => c.InComponentType == componentContentProvider.ComponentType);
                HandleSelection(idx, m_ComponentsListView, m_EntitiesListView, m_ComponentsListView);
            }
            else
            {
                HandleSelection(-1, null, m_EntitiesListView, m_ComponentsListView);
            }

            static void HandleSelection(int indexToSelect, ListView target, params ListView[] allListViews)
            {
                foreach (var listView in allListViews)
                {
                    if (listView != target)
                        listView.ClearSelection();
                }

                if (target == null)
                    return;

                if (indexToSelect < 0)
                    target.ClearSelection();
                else
                    target.SetSelectionWithoutNotify(new[] { indexToSelect });
            }
        }

        public override void Update()
        {
            if (Target.World == null || !Target.World.IsCreated || !Target.World.EntityManager.IsQueryValid(Target.Query))
                return;

            var currentHash = Target.Query.GetCombinedComponentOrderVersion();
            if (currentHash == m_LastHash)
                return;

            m_LastHash = currentHash;
            Target.SourceEntities.Clear();
            using (var entities = Target.Query.ToEntityArray(Allocator.Temp))
            {
                foreach (var entity in entities)
                {
                    Target.SourceEntities.Add(new EntityViewData(Target.World, entity));
                }

                m_EntitiesTab.TabName = $"Entities   {entities.Length}";
            }

            m_EntitiesSearchElement.Search();
            m_EntitiesListView.Rebuild();
            m_EntitiesListView.ForceUpdateBindings();

            UpdateEntitiesEmptyMessagesVisibility();
        }

        void UpdateComponentsEmptyMessagesVisibility()
        {
            m_ComponentsListView.SetVisibility(Target.FilteredComponents.Count > 0);
            m_UniversalQueryMessage.SetVisibility(Target.SourceComponents.Count == 0 && string.IsNullOrWhiteSpace(m_ComponentsSearchElement.value));
            m_NoComponentResultMessage.SetVisibility(Target.FilteredComponents.Count == 0 && !string.IsNullOrWhiteSpace(m_ComponentsSearchElement.value));
        }

        void UpdateEntitiesEmptyMessagesVisibility()
        {
            m_EntitiesListView.SetVisibility(Target.FilteredEntities.Count > 0);
            m_EmptyEntitiesMessage.SetVisibility(Target.SourceEntities.Count == 0 && string.IsNullOrWhiteSpace(m_EntitiesSearchElement.value));
            m_NoEntitiesResultMessage.SetVisibility(Target.FilteredEntities.Count == 0 && !string.IsNullOrWhiteSpace(m_EntitiesSearchElement.value));
        }
    }
}
