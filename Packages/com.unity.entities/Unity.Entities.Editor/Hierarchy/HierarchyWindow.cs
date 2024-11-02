using System;
using Unity.Collections;
using Unity.Editor.Bridge;
using Unity.Profiling;
using Unity.Scenes;
using Unity.Serialization.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    partial class HierarchyWindow : DOTSEditorWindow
    {
        static readonly ProfilerMarker k_OnUpdateMarker = new ProfilerMarker($"{nameof(HierarchyWindow)}.{nameof(OnUpdate)}");

        static readonly string k_FilterComponentType = L10n.Tr("All Components");
        static readonly string k_FilterComponentTypeTooltip = L10n.Tr("Filter entities that have all the specified component types");

        static readonly string k_FilterAnyComponentType = L10n.Tr("Any Component");
        static readonly string k_FilterAnyComponentTypeTooltip = L10n.Tr("Filter entities that have any of the specified component types");

        static readonly string k_FilterNoneComponentType = L10n.Tr("No Component");
        static readonly string k_FilterNoneComponentTypeTooltip = L10n.Tr("Filter entities that have none of the specified component types");

        static readonly string k_FilterIndexToken = L10n.Tr("Entity Index");
        static readonly string k_FilterIndexTokenTooltip = L10n.Tr("Filter entities that have the specified index");

        static readonly string k_FilterKindToken = L10n.Tr("Node Kind");
        static readonly string k_FilterKindTokenTooltip = L10n.Tr("Filter entities that have the specified Node type.");

        static readonly string k_FilterSharedComponentType = L10n.Tr("Shared Component");
        static readonly string k_FilterSharedComponentTypeTooltip = L10n.Tr("Filter entities that have a specific SharedComponent");

        static readonly string k_WindowName = L10n.Tr("Entities Hierarchy");
        static readonly Vector2 k_MinWindowSize = Constants.MinWindowSize;

        static readonly EntityQueryOptions[] k_EntityQueryOptions = new[] {
            EntityQueryOptions.FilterWriteGroup,
            EntityQueryOptions.IgnoreComponentEnabledState,
            EntityQueryOptions.IncludeDisabledEntities,
            EntityQueryOptions.IncludePrefab,
            EntityQueryOptions.IncludeSystems
        };

        static readonly NodeKind[] k_NodeKinds = new[] {
            NodeKind.Entity,
            NodeKind.GameObject,
            NodeKind.Scene,
            NodeKind.SubScene
        };

        static readonly HierarchyDecoratorCollection s_Decorators = new HierarchyDecoratorCollection();

        internal static void AddDecorator(IHierarchyItemDecorator decorator) => s_Decorators.Add(decorator);
        internal static void RemoveDecorator(IHierarchyItemDecorator decorator) => s_Decorators.Remove(decorator);

        readonly Cooldown m_BackgroundUpdateCooldown = new Cooldown(TimeSpan.FromMilliseconds(250));

        bool m_IsVisible;

        Hierarchy m_Hierarchy;
        HierarchyElement m_HierarchyElement;
        HierarchySettings m_HierarchySettings;

        /// <summary>
        /// Flag indicating we should try to apply the global selection on the next update cycle.
        /// </summary>
        bool m_GlobalSelectionRequest;

        /// <summary>
        /// The update version this request was made at.
        /// </summary>
        uint m_GlobalSelectionRequestUpdateVersion;
        SearchElement m_SearchElement;
        AutoComplete m_SearchAutocomplete;
        AutoComplete m_SharedComponentAutocomplete;

        [MenuItem(Constants.MenuItems.HierarchyWindow, false, Constants.MenuItems.HierarchyWindowPriority)]
        static void OpenWindow() => GetWindow<HierarchyWindow>();

        internal static void OpenWindow(string query)
        {
            var win = GetWindow<HierarchyWindow>();
            win.SetSearchQuery(query);
        }

        public HierarchyWindow() : base(Analytics.Window.Hierarchy)
        { }

        public void SetSearchQuery(string searchQuery)
        {
            m_SearchElement.Search(searchQuery);
        }

        protected override void OnCreate()
        {
            titleContent = new GUIContent(k_WindowName, EditorIcons.EntityGroup);
            minSize = k_MinWindowSize;

            Resources.Templates.DotsEditorCommon.AddStyles(rootVisualElement);
            Resources.AddCommonVariables(rootVisualElement);

            dataModeController.UpdateSupportedDataModes(GetSupportedDataModes(), GetPreferredDataMode());

            // Initialize the data models.
            m_HierarchySettings = UserSettings<HierarchySettings>.GetOrCreate(Constants.Settings.Hierarchy);
            m_Hierarchy = new Hierarchy(Allocator.Persistent, dataModeController.dataMode)
            {
                Configuration = m_HierarchySettings.Configuration,
                State = SessionState<HierarchyState>.GetOrCreate($"{GetType().Name}.{nameof(HierarchyState)}")
            };

            m_HierarchySettings.UseAdvanceSearchSettingChanged += OnUseAdvanceSearchSettingChanged;

            // Initialize the view models.
            CreateToolbar(rootVisualElement, m_Hierarchy);
            m_HierarchyElement = CreateHierarchyElement(rootVisualElement, m_Hierarchy);
            m_HierarchyElement.SetDecorators(s_Decorators);

            Selection.selectionChanged += OnGlobalSelectionChanged;
            dataModeController.dataModeChanged += OnDataModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        protected override void OnCleanup()
        {
            if (m_HierarchySettings != null) m_HierarchySettings.UseAdvanceSearchSettingChanged -= OnUseAdvanceSearchSettingChanged;
            EditorApplication.update -= OnBackgroundUpdate;
            Selection.selectionChanged -= OnGlobalSelectionChanged;
            if (dataModeController != null) dataModeController.dataModeChanged -= OnDataModeChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            m_Hierarchy?.Dispose();
        }

        void OnBecameVisible()
        {
            m_IsVisible = true;

            // Can happen when a domain reload occurs with the window open
            if (m_Hierarchy == null)
                return;

            RequestGlobalSelectionRestoration();
        }

        void OnUseAdvanceSearchSettingChanged()
        {
            SetupSearchOptions();
            if (!string.IsNullOrEmpty(m_SearchElement.value))
                m_SearchElement.ClearSearchString();
        }

        void RequestGlobalSelectionRestoration()
        {
            // Request the selection to be updated after the next update cycle.
            m_GlobalSelectionRequest = true;
            m_GlobalSelectionRequestUpdateVersion = m_Hierarchy?.UpdateVersion ?? 0;
        }

        void OnBecameInvisible()
        {
            m_IsVisible = false;
            m_BackgroundUpdateCooldown.Update(DateTime.Now);
        }

        void OnBackgroundUpdate()
        {
            if (m_IsVisible || !m_BackgroundUpdateCooldown.Update(DateTime.Now))
                return;

            Update();
        }

        void OnLostFocus()
        {
            m_HierarchyElement?.OnLostFocus();
        }

        HierarchyElement CreateHierarchyElement(VisualElement root, Hierarchy hierarchy)
        {
            var element = new HierarchyElement(hierarchy);
            element.OnSelectionChanged += OnHierarchySelectionChanged;
            root.Add(element);
            return element;
        }

        VisualElement CreateToolbar(VisualElement root, Hierarchy hierarchy)
        {
            var toolbar = Resources.Templates.Hierarchy.Toolbar.Clone();
            var leftSide = toolbar.Q<VisualElement>(className: UssClasses.Hierarchy.Toolbar.LeftSide);
            var rightSide = toolbar.Q<VisualElement>(className: UssClasses.Hierarchy.Toolbar.RightSide);
            leftSide.Add(CreateWorldSelector());

            AddSearchIcon(rightSide, UssClasses.DotsEditorCommon.SearchIcon);
            m_SearchElement = AddSearchElement<HierarchyNodeHandle>(toolbar, UssClasses.DotsEditorCommon.SearchFieldContainer);
            m_SearchElement.RegisterSearchQueryHandler<HierarchyNodeHandle>(query =>
            {
                if (hierarchy.Configuration.AdvancedSearch)
                {
                    var searchString = query.SearchString;
                    if (string.IsNullOrEmpty(searchString))
                    {
                        hierarchy.SetFilter(null);
                        return;
                    }

                    var desc = HierarchySearchProvider.CreateHierarchyQueryDescriptor(searchString);
                    if (!string.IsNullOrEmpty(desc.parsingErrors))
                    {
                        Debug.LogError(desc.parsingErrors);
                    }
                    var filter = HierarchySearchProvider.CreateHierarchyFilter(hierarchy.HierarchySearch, desc, hierarchy.Allocator);
                    hierarchy.SetFilter(filter);
                }
                else
                {
                    hierarchy.SetSearchQuery(query.SearchString, query.Tokens);
                }
            });

            SetupSearchOptions();
            m_SearchElement.parent.Add(SearchUtils.CreateJumpButton(() =>
            {
                HierarchySearchProvider.OpenProvider(m_SearchElement.value, SelectedWorld);
            }));

            root.Add(toolbar);

            return toolbar;
        }

        void SetupSearchOptions()
        {
            m_SearchElement.ClearSearchFilterPopupItem();
            if (m_Hierarchy.Configuration.AdvancedSearch)
            {
                // Advanced supported Filters:
                // all, none, any, EntityQueryOptions
                // kind, ei
                m_SearchElement.AddSearchFilterPopupItem(Constants.ComponentSearch.All, k_FilterComponentType, k_FilterComponentTypeTooltip, Constants.ComponentSearch.Op);
                m_SearchElement.AddSearchFilterPopupItem(Constants.ComponentSearch.None, k_FilterNoneComponentType, k_FilterNoneComponentType, Constants.ComponentSearch.Op);
                m_SearchElement.AddSearchFilterPopupItem(Constants.ComponentSearch.Any, k_FilterAnyComponentType, k_FilterAnyComponentType, Constants.ComponentSearch.Op);
                m_SearchElement.AddSearchFilterPopupItem(Constants.ComponentSearch.SharedComponentPrefix, k_FilterSharedComponentType, k_FilterSharedComponentTypeTooltip, "");
                m_SearchElement.AddSearchFilterPopupItem(Constants.Hierarchy.EntityIndexToken, k_FilterIndexToken, k_FilterIndexTokenTooltip, "=");

                foreach (var opt in k_EntityQueryOptions)
                {
                    m_SearchElement.AddSearchFilterPopupItem($"+{opt}", "Option", k_FilterIndexTokenTooltip, " ", isCompleteFilter: true);
                }

                foreach (var k in k_NodeKinds)
                {
                    m_SearchElement.AddSearchFilterPopupItem($"{Constants.Hierarchy.KindToken}={k}", k_FilterKindToken, k_FilterKindTokenTooltip, " ", isCompleteFilter: true);
                }

                m_SearchAutocomplete?.Dispose();
                m_SharedComponentAutocomplete?.Dispose();
                m_SearchAutocomplete = new AutoComplete(m_SearchElement, ComponentTypeAutoComplete.EntityQueryInstance);
                m_SharedComponentAutocomplete = new AutoComplete(m_SearchElement, SharedComponentTypeAutoComplete.Instance);
            }
            else
            {
                m_SearchElement.AddSearchFilterPopupItem(Constants.ComponentSearch.Token, k_FilterComponentType, k_FilterComponentTypeTooltip, Constants.ComponentSearch.Op);
                m_SearchElement.AddSearchFilterPopupItem(Constants.Hierarchy.EntityIndexToken, k_FilterIndexToken, k_FilterIndexTokenTooltip, "=");
                m_SearchAutocomplete?.Dispose();
                m_SharedComponentAutocomplete?.Dispose();
                m_SharedComponentAutocomplete = null;
                m_SearchAutocomplete = new AutoComplete(m_SearchElement, ComponentTypeAutoComplete.Instance);
            }
        }

        void OnGlobalSelectionChanged() => ApplyGlobalSelection(false);

        void ApplyGlobalSelection(bool allowReentry)
        {
            if (!m_IsVisible)
                return;

            // If this is a re-entry, bail out
            if (Selection.activeContext is HierarchySelectionContext && !allowReentry)
                return;

            switch (dataModeController.dataMode)
            {
                case DataMode.Authoring:
                case DataMode.Mixed:
                {
                    // In Authoring Mode or Mixed Mode, we are always interacting
                    // with the active selection (Hybrid Object, GameObject, or Entity)
                    SetSelection(Selection.activeObject);
                    return;
                }
                case DataMode.Runtime:
                {
                    // In runtime, try real hard to show an Entity if available
                    if (Selection.activeContext is EntitySelectionProxy proxy && proxy.Exists)
                        SetSelection(proxy);
                    else
                        SetSelection(Selection.activeObject);
                    return;
                }
                case DataMode.Disabled:
                default:
                {
                    return;
                }
            }
        }

        void SetSelection(UnityEngine.Object obj)
        {
            if (obj is EntitySelectionProxy selectedProxy)
            {
                if (selectedProxy.World != m_Hierarchy.World)
                    SelectedWorld = selectedProxy.World;
                m_HierarchyElement.SetSelection(HierarchyNodeHandle.FromEntity(selectedProxy.Entity));
            }
            else if (obj is GameObject gameObject)
            {
                if (m_Hierarchy.World != null && gameObject.TryGetComponent<SubScene>(out var subScene) && subScene.SceneGUID != default && m_Hierarchy.SubSceneMap.TryGetSubSceneNodeHandle(subScene, out var handle))
                    m_HierarchyElement.SetSelection(handle);
                else
                    m_HierarchyElement.SetSelection(HierarchyNodeHandle.FromGameObject(gameObject));
            }
            else
            {
                m_HierarchyElement.ClearSelection();
            }
        }

        public static void SelectHierarchyNode(Hierarchy hierarchy, HierarchyNodeHandle handle, DataMode dataMode)
        {
            var undoGroup = Undo.GetCurrentGroup();

            switch (handle.Kind)
            {
                case NodeKind.Entity:
                {
                    var entity = handle.ToEntity();

                    if (entity != Entity.Null)
                    {
                        var world = hierarchy.World;

                        // If we are in the middle of a transaction, we need to end it before we can get the authoring object
                        if (!world.EntityManager.CanBeginExclusiveEntityTransaction())
                            world.EntityManager.EndExclusiveEntityTransaction();

                        var authoringObject = world.EntityManager.Debug.GetAuthoringObjectForEntity(entity);

                        if (authoringObject == null)
                        {
                            EntitySelectionProxy.SelectEntity(world, entity);
                        }
                        else
                        {
                            var context = EntitySelectionProxy.CreateInstance(world, entity);
                            // Selected entities should always try to show up in Runtime mode
                            SelectionBridge.SetSelection(authoringObject, context, DataMode.Runtime);
                            Undo.SetCurrentGroupName($"Select {authoringObject.name} ({authoringObject.GetType().Name})");
                        }
                    }

                    break;
                }

                case NodeKind.SubScene:
                {
                    var subScene = hierarchy.SubSceneMap.GetSubSceneMonobehaviourFromHandle(handle);
                    SelectionBridge.SetSelection(subScene ? subScene.gameObject : null, HierarchySelectionContext.CreateInstance(handle), DataMode.Disabled);
                    if (subScene)
                        Undo.SetCurrentGroupName($"Select {subScene.name} ({subScene.GetType().Name})");

                    break;
                }

                case NodeKind.Scene:
                {
                    SelectionBridge.SetSelection(null, HierarchySelectionContext.CreateInstance(handle), DataMode.Disabled);
                    break;
                }

                case NodeKind.GameObject:
                {
                    var gameObject = hierarchy.GetUnityObject(handle) as GameObject;

                    // Don't reselect yourself
                    if (Selection.activeObject == gameObject)
                        return;

                    var world = hierarchy.World;
                    EntitySelectionProxy context;

                    if (world is not { IsCreated: true })
                    {
                        context = null;
                    }
                    else
                    {
                        var primaryEntity = world.EntityManager.Debug.GetPrimaryEntityForAuthoringObject(gameObject);

                        context = primaryEntity != Entity.Null && world.EntityManager.SafeExists(primaryEntity)
                                    ? EntitySelectionProxy.CreateInstance(world, primaryEntity)
                                    : null;
                    }

                    // Selected GameObjects should use whatever the current DataMode for the hierarchy is.
                    SelectionBridge.SetSelection(gameObject, context, dataMode);
                    Undo.SetCurrentGroupName($"Select {gameObject.name} ({gameObject.GetType().Name})");
                    break;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        void OnHierarchySelectionChanged(HierarchyNodeHandle handle)
        {
            if (!m_IsVisible)
                return;

            SelectHierarchyNode(m_Hierarchy, handle, dataModeController.dataMode);
        }

        protected override void OnWorldSelected(World world)
        {
            m_Hierarchy.SetWorld(world);
            m_HierarchyElement.Reset();

            // Force the search element to update if we have something serialized.
            rootVisualElement.Q<SearchElement>().Search(SearchFilter);
        }

        protected override void OnUpdate()
        {
            using (k_OnUpdateMarker.Auto())
            {
                m_Hierarchy.Update(m_IsVisible);

                if (!m_IsVisible)
                    return;

                m_HierarchyElement.Refresh();

                // We have a deferred selection request. Wait until we have completed a full update cycle before attempting to select.
                if (m_GlobalSelectionRequest && m_Hierarchy.UpdateVersion == m_GlobalSelectionRequestUpdateVersion + 1)
                {
                    ApplyGlobalSelection(true);
                    m_GlobalSelectionRequest = false;
                }
            }
        }

        void OnGUI()
        {
            var evt = Event.current;

            if (evt.type is not (EventType.ExecuteCommand or EventType.ValidateCommand))
                return;

            var execute = evt.type is EventType.ExecuteCommand;

            switch (evt.commandName)
            {
                case EventCommandNamesBridge.Find:
                    evt.Use();
                    if (execute)
                        m_SearchElement?.Q<TextField>().Focus();
                    break;
                case EventCommandNamesBridge.Delete:
                case EventCommandNamesBridge.SoftDelete:
                case EventCommandNamesBridge.Duplicate:
                case EventCommandNamesBridge.Rename:
                case EventCommandNamesBridge.Cut:
                case EventCommandNamesBridge.Copy:
                case EventCommandNamesBridge.Paste:
                case EventCommandNamesBridge.SelectAll:
                case EventCommandNamesBridge.DeselectAll:
                case EventCommandNamesBridge.InvertSelection:
                case EventCommandNamesBridge.SelectChildren:
                case EventCommandNamesBridge.SelectPrefabRoot:
                    evt.Use();
                    break;
            }

            if (execute)
                m_HierarchyElement?.HandleCommand(evt.commandName);
        }
    }
}
