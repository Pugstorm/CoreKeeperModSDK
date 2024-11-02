using System;
using Unity.Profiling;
using Unity.Properties;
using Unity.Entities.UI;
using Unity.Serialization.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class SystemScheduleWindow : DOTSEditorWindow, IHasCustomMenu
    {
        static readonly ProfilerMarker k_OnUpdateMarker =
            new ProfilerMarker($"{nameof(SystemScheduleWindow)}.{nameof(OnUpdate)}");

        readonly Cooldown m_Cooldown = new Cooldown(TimeSpan.FromMilliseconds(Constants.Inspector.CoolDownTime));

        static readonly string k_WindowName = L10n.Tr("Systems");
        static readonly string k_SystemContentName = L10n.Tr("System");
        static readonly string k_ShowFullPlayerLoopString = L10n.Tr("Show Full Player Loop");
        static readonly string k_WorldOptionString = L10n.Tr("World");
        static readonly string k_NamespaceOptionString = L10n.Tr("Namespace");
        static readonly string k_EntityCountOptionString = L10n.Tr("Entity Count");
        static readonly string k_TimeOptionString = L10n.Tr("Time (ms)");
        static readonly string k_EntitiesPreferencesString = L10n.Tr("Entities Preferences");
        static readonly string k_EntitiesPreferencesPath = "Preferences/Entities";
        static readonly string k_ViewOption = L10n.Tr("View Options");
        static readonly string k_ColumnOption = L10n.Tr("Column Options");
        static readonly string k_Setting = L10n.Tr("Setting");
        static readonly string k_FilterComponentType = L10n.Tr("Component type");
        static readonly string k_FilterComponentTypeTooltip = L10n.Tr("Filter systems that have the specified component type in queries");
        static readonly string k_FilterSystemDependencies = L10n.Tr("System dependencies");
        static readonly string k_FilterSystemDependenciesTooltip = L10n.Tr("Filter systems by their direct dependencies");

        VisualElement m_Root;
        CenteredMessageElement m_NoWorld;
        SystemTreeView m_SystemTreeView;
        VisualElement m_WorldSelector;
        VisualElement m_EmptySelectorWhenShowingFullPlayerLoop;
        SearchElement m_SearchElement;
        internal WorldProxyManager WorldProxyManager; // internal for tests.
        PlayerLoopSystemGraph m_LocalSystemGraph;
        int m_LastWorldVersion;
        bool m_ViewChange;
        bool m_GraphChange;

        WorldProxy m_SelectedWorldProxy;

        /// <summary>
        /// The systems window configuration. This is data which is managed externally by settings, tests or users but drives internal behaviours.
        /// </summary>
        [GeneratePropertyBag]
        public class SystemsWindowConfiguration
        {
            [CreateProperty] public bool Show0sInEntityCountAndTimeColumn = false;
            [CreateProperty] public bool ShowMorePrecisionForRunningTime = false;
            public bool ShowWorldColumn = true;
            public bool ShowNamespaceColumn = true;
            public bool ShowEntityCountColumn = true;
            public bool ShowTimeColumn = true;
            public bool ShowFullPlayerLoop;
        }

        // Internal for tests.
        internal SystemsWindowConfiguration m_Configuration;

        Label m_SystemHeaderLabel;
        Label m_WorldHeaderLabel;
        Label m_NamespaceHeaderLabel;
        Label m_EntityHeaderLabel;
        Label m_TimeHeaderLabel;

        [MenuItem(Constants.MenuItems.SystemScheduleWindow, false, Constants.MenuItems.SystemScheduleWindowPriority)]
        static void OpenWindow()
        {
            var window = GetWindow<SystemScheduleWindow>();
            window.Show();
        }

        public SystemScheduleWindow() : base(Analytics.Window.Systems)
        { }

        /// <summary>
        /// Build the GUI for the system window.
        /// </summary>
        protected override void OnCreate()
        {
            Resources.AddCommonVariables(rootVisualElement);

            titleContent = EditorGUIUtility.TrTextContent(k_WindowName, EditorIcons.System);
            minSize = Constants.MinWindowSize;

            m_Root = new VisualElement();
            m_Root.AddToClassList(UssClasses.SystemScheduleWindow.WindowRoot);
            rootVisualElement.Add(m_Root);

            m_NoWorld = new CenteredMessageElement() { Message = NoWorldMessageContent };
            rootVisualElement.Add(m_NoWorld);
            m_NoWorld.Hide();

            m_Configuration = UserSettings<SystemsWindowPreferenceSettings>.GetOrCreate(Constants.Settings.SystemsWindow).Configuration;

            Resources.Templates.SystemSchedule.AddStyles(m_Root);
            Resources.Templates.DotsEditorCommon.AddStyles(m_Root);

            WorldProxyManager = new WorldProxyManager();
            m_LocalSystemGraph = new PlayerLoopSystemGraph
            {
                WorldProxyManager = WorldProxyManager
            };
            WorldProxyManager.CreateWorldProxiesForAllWorlds();

            CreateToolBar(m_Root);
            CreateTreeViewHeader(m_Root);
            CreateTreeView(m_Root);

            if (!string.IsNullOrEmpty(SearchFilter))
                m_SearchElement.Search(SearchFilter);

            Selection.selectionChanged += OnGlobalSelectionChanged;
        }

        protected override void OnCleanup()
        {
            WorldProxyManager?.Dispose();
            m_SystemTreeView?.Dispose();

            Selection.selectionChanged -= OnGlobalSelectionChanged;
        }

        void CreateToolBar(VisualElement root)
        {
            var toolbar = new VisualElement();
            Resources.Templates.SystemScheduleToolbar.Clone(toolbar);
            var leftSide = toolbar.Q(className: UssClasses.SystemScheduleWindow.Toolbar.LeftSide);
            var rightSide = toolbar.Q(className: UssClasses.SystemScheduleWindow.Toolbar.RightSide);

            m_WorldSelector = CreateWorldSelector();
            m_EmptySelectorWhenShowingFullPlayerLoop = new ToolbarMenu { text = k_ShowFullPlayerLoopString };
            leftSide.Add(m_WorldSelector);
            leftSide.Add(m_EmptySelectorWhenShowingFullPlayerLoop);

            AddSearchIcon(rightSide, UssClasses.DotsEditorCommon.SearchIcon);

            var dropdownSettings = InspectorUtility.CreateDropdownSettings(UssClasses.DotsEditorCommon.SettingsIcon);
            AppendOptionMenu(dropdownSettings.menu);

            UpdateWorldSelectorDisplay();
            rightSide.Add(dropdownSettings);

            root.Add(toolbar);
            AddSearchElement(root);
            m_SearchElement.parent.Add(SearchUtils.CreateJumpButton(() => SystemSearchProvider.OpenProvider(m_SearchElement.value, SelectedWorld)));
        }

        void AppendOptionMenu(DropdownMenu menu)
        {
            // Full player loop
            menu.AppendAction(k_ViewOption, null, DropdownMenuAction.Status.Disabled);
            menu.AppendAction(k_ShowFullPlayerLoopString, a =>
            {
                m_Configuration.ShowFullPlayerLoop = !m_Configuration.ShowFullPlayerLoop;
                WorldProxyManager.IsFullPlayerLoop = m_Configuration.ShowFullPlayerLoop;

                UpdateWorldSelectorDisplay();

                if (World.All.Count > 0)
                    RebuildTreeView();
            }, a=> m_Configuration.ShowFullPlayerLoop ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            menu.AppendSeparator();

            // Column options
            menu.AppendAction(k_ColumnOption, null, DropdownMenuAction.Status.Disabled);
            menu.AppendAction(k_WorldOptionString, a =>
            {
                m_Configuration.ShowWorldColumn = !m_Configuration.ShowWorldColumn;
                m_WorldHeaderLabel.SetVisibility(m_Configuration.ShowWorldColumn);
                UpdateConfigurations();
                AdjustSystemHeaderLabelWidth();
            }, a=> m_Configuration.ShowWorldColumn ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            menu.AppendAction(k_NamespaceOptionString, a =>
            {
                m_Configuration.ShowNamespaceColumn = !m_Configuration.ShowNamespaceColumn;
                m_NamespaceHeaderLabel.SetVisibility(m_Configuration.ShowNamespaceColumn);
                UpdateConfigurations();
                AdjustSystemHeaderLabelWidth();
            }, a=> m_Configuration.ShowNamespaceColumn ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            menu.AppendAction(k_EntityCountOptionString, a =>
            {
                m_Configuration.ShowEntityCountColumn = !m_Configuration.ShowEntityCountColumn;
                m_EntityHeaderLabel.SetVisibility(m_Configuration.ShowEntityCountColumn);
                UpdateConfigurations();
                AdjustSystemHeaderLabelWidth();
            }, a=> m_Configuration.ShowEntityCountColumn ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            menu.AppendAction(k_TimeOptionString, a =>
            {
                m_Configuration.ShowTimeColumn = !m_Configuration.ShowTimeColumn;
                m_TimeHeaderLabel.SetVisibility(m_Configuration.ShowTimeColumn);
                UpdateConfigurations();
                AdjustSystemHeaderLabelWidth();
            }, a=> m_Configuration.ShowTimeColumn ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            menu.AppendSeparator();

            // Setting
            menu.AppendAction(k_Setting, null, DropdownMenuAction.AlwaysDisabled);
            menu.AppendAction(k_EntitiesPreferencesString, a =>
            {
                SettingsService.OpenUserPreferences(k_EntitiesPreferencesPath);
            });
        }

        void AddSearchElement(VisualElement root)
        {
            m_SearchElement = AddSearchElement<SystemForSearch>(root, UssClasses.DotsEditorCommon.SearchFieldContainer);
            m_SearchElement.RegisterSearchQueryHandler<SystemForSearch>(query =>
            {
                var parseResult = SearchQueryParser.ParseSearchQuery(query);
                m_SystemTreeView.SetFilter(query, parseResult);
            });

            m_SearchElement.AddSearchFilterPopupItem(Constants.ComponentSearch.Token, k_FilterComponentType, k_FilterComponentTypeTooltip, Constants.ComponentSearch.Op);
            m_SearchElement.AddSearchFilterPopupItem(Constants.SystemSchedule.k_SystemDependencyToken.Substring(0, 2), k_FilterSystemDependencies, k_FilterSystemDependenciesTooltip);

            m_SearchElement.AddSearchDataProperty(new PropertyPath(nameof(SystemForSearch.SystemName)));
            m_SearchElement.AddSearchFilterProperty(Constants.ComponentSearch.Token, new PropertyPath(nameof(SystemForSearch.ComponentNamesInQuery)));
            m_SearchElement.AddSearchFilterProperty(Constants.SystemSchedule.k_SystemDependencyToken.Substring(0, 2), new PropertyPath(nameof(SystemForSearch.SystemDependency)));
            m_SearchElement.EnableAutoComplete(ComponentTypeAutoComplete.Instance);
        }

        void UpdateWorldSelectorDisplay()
        {
            m_WorldSelector.SetVisibility(!m_Configuration.ShowFullPlayerLoop);
            m_EmptySelectorWhenShowingFullPlayerLoop.SetVisibility(m_Configuration.ShowFullPlayerLoop);
        }

        /// <summary>
        ///  Manually create header for the tree view.
        /// </summary>
        /// <param name="root"></param>
        void CreateTreeViewHeader(VisualElement root)
        {
            var headerRoot = new VisualElement();
            Resources.Templates.SystemScheduleTreeViewHeader.Clone(headerRoot);

            m_SystemHeaderLabel = headerRoot.Q<Label>(className: UssClasses.SystemScheduleWindow.TreeViewHeader.System);
            m_WorldHeaderLabel = headerRoot.Q<Label>(className: UssClasses.SystemScheduleWindow.TreeViewHeader.World);
            m_NamespaceHeaderLabel = headerRoot.Q<Label>(className: UssClasses.SystemScheduleWindow.TreeViewHeader.Namespace);
            m_EntityHeaderLabel = headerRoot.Q<Label>(className: UssClasses.SystemScheduleWindow.TreeViewHeader.EntityCount);
            m_TimeHeaderLabel = headerRoot.Q<Label>(className: UssClasses.SystemScheduleWindow.TreeViewHeader.Time);

            m_WorldHeaderLabel.SetVisibility(m_Configuration.ShowWorldColumn);
            m_NamespaceHeaderLabel.SetVisibility(m_Configuration.ShowNamespaceColumn);
            m_EntityHeaderLabel.SetVisibility(m_Configuration.ShowEntityCountColumn);
            m_TimeHeaderLabel.SetVisibility(m_Configuration.ShowTimeColumn);

            root.Add(headerRoot);
        }

        void CreateTreeView(VisualElement root)
        {
            m_SystemTreeView = new SystemTreeView
            {
                viewDataKey = nameof(SystemScheduleWindow),
                style = { flexGrow = 1 },
                LocalSystemGraph = m_LocalSystemGraph
            };
            UpdateConfigurations();
            root.Add(m_SystemTreeView);
        }

        void UpdateConfigurations()
        {
            m_SystemTreeView.ShowWorldColumn = m_Configuration.ShowWorldColumn;
            m_SystemTreeView.ShowNamespaceColumn = m_Configuration.ShowNamespaceColumn;
            m_SystemTreeView.ShowEntityCountColumn = m_Configuration.ShowEntityCountColumn;
            m_SystemTreeView.ShowTimeColumn = m_Configuration.ShowTimeColumn;
            m_SystemTreeView.ShowMorePrecisionForRunningTime = m_Configuration.ShowMorePrecisionForRunningTime;
            m_SystemTreeView.Show0sInEntityCountAndTimeColumn = m_Configuration.Show0sInEntityCountAndTimeColumn;
        }

        void UpdatePreferences()
        {
            if (m_SystemTreeView.ShowMorePrecisionForRunningTime != m_Configuration.ShowMorePrecisionForRunningTime)
                m_SystemTreeView.ShowMorePrecisionForRunningTime = m_Configuration.ShowMorePrecisionForRunningTime;

            if (m_SystemTreeView.Show0sInEntityCountAndTimeColumn != m_Configuration.Show0sInEntityCountAndTimeColumn)
                m_SystemTreeView.Show0sInEntityCountAndTimeColumn = m_Configuration.Show0sInEntityCountAndTimeColumn;
        }

        // internal for test.
        internal void RebuildTreeView()
        {
            m_SystemTreeView.Refresh(m_Configuration.ShowFullPlayerLoop ? null : m_SelectedWorldProxy);
        }

        protected override void OnUpdate()
        {
            using (k_OnUpdateMarker.Auto())
            {
                if (!m_Cooldown.Update(DateTime.Now))
                    return;

                if (m_SystemTreeView == null || WorldProxyManager == null)
                    return;

                UpdatePreferences();

                foreach (var updater in WorldProxyManager.GetAllWorldProxyUpdaters())
                {
                    if (!updater.IsActive() || !updater.IsDirty())
                        continue;

                    m_GraphChange = true;
                    updater.SetClean();
                }

                if (m_GraphChange)
                    m_LocalSystemGraph.BuildCurrentGraph();

                if (m_GraphChange || m_ViewChange)
                    RebuildTreeView();

                m_GraphChange = false;
                m_ViewChange = false;
            }
        }

        void AdjustSystemHeaderLabelWidth()
        {
            m_SystemHeaderLabel.style.width = 200f
                                              + (m_Configuration.ShowWorldColumn ? 0f : 100f)
                                              + (m_Configuration.ShowNamespaceColumn ? 0f : 120f)
                                              + (m_Configuration.ShowEntityCountColumn ? 0f : 75f)
                                              + (m_Configuration.ShowTimeColumn ? 0f : 75f);
        }

        protected override void OnWorldsChanged(bool containsAnyWorld)
        {
            m_Root.SetVisibility(containsAnyWorld);
            m_NoWorld.SetVisibility(!containsAnyWorld);

            if (m_SystemTreeView == null)
                return;

            WorldProxyManager.IsFullPlayerLoop = m_Configuration.ShowFullPlayerLoop;
            WorldProxyManager.CreateWorldProxiesForAllWorlds();

            if (SelectedWorld != null && SelectedWorld.IsCreated)
            {
                m_SelectedWorldProxy = WorldProxyManager.GetWorldProxyForGivenWorld(SelectedWorld);
                WorldProxyManager.SelectedWorldProxy = m_SelectedWorldProxy;
            }

            if (m_Configuration.ShowFullPlayerLoop)
                m_GraphChange = true;
        }

        protected override void OnWorldSelected(World world)
        {
            if (world == null || !world.IsCreated)
                return;

            if (m_Configuration.ShowFullPlayerLoop)
                return;

            m_SelectedWorldProxy = WorldProxyManager.GetWorldProxyForGivenWorld(world);
            WorldProxyManager.SelectedWorldProxy = m_SelectedWorldProxy;

            m_ViewChange = true;
        }

        public static void HighlightSystem(SystemProxy systemProxy)
        {
            SystemTreeView.SelectedSystem = systemProxy;

            if (HasOpenInstances<SystemScheduleWindow>())
                GetWindow<SystemScheduleWindow>().m_SystemTreeView.SetSelection();
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            if (Unsupported.IsDeveloperMode())
            {
                menu.AddItem(new GUIContent($"Debug..."), false, () =>
                    SelectionUtility.ShowInWindow(new SystemsWindowDebugContentProvider()));
            }
        }

        void OnGlobalSelectionChanged()
        {
            if (Selection.activeObject is InspectorContent content && content.Content.Name.Equals(k_SystemContentName))
                return;

            SystemTreeView.SelectedSystem = default;
            m_SystemTreeView.m_SystemTreeView.ClearSelection();
            m_SystemTreeView.m_SystemListView.ClearSelection();
        }
    }
}
