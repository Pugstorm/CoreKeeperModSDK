using System;
using Unity.Assertions;
using Unity.Serialization.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    abstract class DOTSEditorWindow : EditorWindow
    {
        readonly WorldDisplayNameCache m_WorldDisplayNameCache = new WorldDisplayNameCache();

        // This bool is added to detect if the window is properly initialized before any updates or
        // OnDisable() is being called, since the calling order for CreateGUI, Update and OnDisable is not definite.
        bool m_Created;

        ToolbarMenu m_WorldSelector;
        VisualElement m_SearchFieldContainer;
        SearchElement m_SearchField;
        Image m_SearchIcon;
        bool m_PreviousShowAdvancedWorldsValue;
        World m_SelectedWorld;
        bool m_SelectedWorldChanged;
        Analytics.Window m_AnalyticsWindowNameCached;
        readonly WorldListChangeTracker m_WorldListChangeTracker = new WorldListChangeTracker();

        [SerializeField]
        string m_EditorWindowInstanceKey;

        protected static string NoWorldMessageContent { get; } = L10n.Tr("No World available");

        protected string EditorWindowInstanceKey
        {
            get
            {
                if (string.IsNullOrEmpty(m_EditorWindowInstanceKey))
                    m_EditorWindowInstanceKey = Guid.NewGuid().ToString("N");

                return m_EditorWindowInstanceKey;
            }
        }

        internal BaseStateContainer BaseState => SessionState<BaseStateContainer>.GetOrCreate($"{GetType().Name}.{nameof(BaseStateContainer)}+{EditorWindowInstanceKey}");

        protected string SearchFilter
        {
            get => IsSearchFieldVisible ? BaseState.SearchFilter : null;
            private set
            {
                BaseState.SearchFilter = value;
                m_SearchField.value = value;
            }
        }

        protected internal World SelectedWorld
        {
            get => m_SelectedWorld;
            set
            {
                if (m_SelectedWorld == value)
                    return;

                m_SelectedWorld = value;
                m_WorldSelector.text = value?.Name ?? string.Empty;
                BaseState.SelectedWorldName = m_WorldSelector.text;
                m_SelectedWorldChanged = true;
            }
        }

        protected DOTSEditorWindow(Analytics.Window analyticsWindow)
        {
            m_AnalyticsWindowNameCached = analyticsWindow;
        }

        // Internal for unit tests
        internal void ChangeCurrentWorld(World newWorld)
        {
            BaseState.SelectedWorldName = newWorld?.Name;
            SelectedWorld = newWorld;
            Update();
        }

        protected bool IsSearchFieldVisible => m_SearchField != null && m_SearchField.style.display == DisplayStyle.Flex;

        World FindSelectedWorld()
        {
            if (World.All.Count == 0)
                return null;

            var selectedWorld = World.All[0];

            if (string.IsNullOrWhiteSpace(BaseState.SelectedWorldName))
                return selectedWorld;

            foreach (var world in World.All)
            {
                if (world.Name == BaseState.SelectedWorldName)
                    return world;
            }

            return selectedWorld;
        }

        protected ToolbarMenu CreateWorldSelector()
        {
            m_WorldSelector = new ToolbarMenu
            {
                name = "worldMenu",
                variant = ToolbarMenu.Variant.Popup
            };

            UpdateWorldDropDownMenu();
            SelectedWorld = FindSelectedWorld();

            m_PreviousShowAdvancedWorldsValue = UserSettings<AdvancedSettings>.GetOrCreate(Constants.Settings.Advanced).ShowAdvancedWorlds;

            return m_WorldSelector;
        }

        protected SearchElement AddSearchElement<TData>(VisualElement parent, string ussClass)
        {
            m_SearchFieldContainer = new VisualElement();
            m_SearchFieldContainer.AddToClassList(ussClass);

            m_SearchField = new SearchElement();
            m_SearchField.SetValueWithoutNotify(string.IsNullOrEmpty(BaseState.SearchFilter) ? string.Empty : BaseState.SearchFilter);
            m_SearchField.AddToClassList(UssClasses.DotsEditorCommon.SearchField);
            m_SearchField.RegisterSearchQueryHandler<TData>(query =>
            {
                if (BaseState.IsSearchFieldVisible)
                    BaseState.SearchFilter = query.SearchString;
            });

            m_SearchField.SetVisibility(BaseState.IsSearchFieldVisible);

            m_SearchFieldContainer.Add(m_SearchField);

            parent.Add(m_SearchFieldContainer);

            return m_SearchField;
        }

        protected void SetSearchFieldVisibility(bool visible)
        {
            BaseState.IsSearchFieldVisible = visible;
            if (m_SearchField == null)
                return;

            if (visible)
            {
                m_SearchField.Show();
                m_SearchField.Q("unity-text-input").Focus();
            }
            else
            {
                m_SearchField.Hide();
            }

            m_SearchField.Search(SearchFilter);
        }

        protected void AddSearchIcon(VisualElement parent, string ussClass)
        {
            var searchIconContainer = new VisualElement();
            searchIconContainer.AddToClassList(UssClasses.DotsEditorCommon.SearchIconContainer);

            m_SearchIcon = new Image();
            Resources.Templates.DotsEditorCommon.AddStyles(m_SearchIcon);
            m_SearchIcon.AddToClassList(UssClasses.DotsEditorCommon.CommonResources);
            m_SearchIcon.AddToClassList(ussClass);

            m_SearchIcon.RegisterCallback<MouseUpEvent>(evt => SetSearchFieldVisibility(!IsSearchFieldVisible));

            searchIconContainer.Add(m_SearchIcon);
            parent.Add(searchIconContainer);
        }

        protected void OnEnable()
        {
            // NO-OP, please use CreateGUI to ensure UI is fully ready to be instantiated.
            // This method exists to ensure no window does its own initialization in OnEnable.
        }

        protected void CreateGUI()
        {
            // This is where the whole window should be initialized if we want UITK to be ready to load all resources.
            // Note: Window creation must be fully complete by the end of this method.
            // Note 2: There is no order guarantee, this could be called before or after Update or OnDisable.
            OnCreate();
            m_Created = true;
        }

        protected void OnDisable()
        {
            // OnDisable may be called before CreateGUI and we need to guard against it.
            if (!m_Created)
                return;

            m_Created = false;
            OnCleanup();
        }

        public void Update()
        {
            // Update may be called 1..n times before CreateGUI and we need to guard against it.
            if (!m_Created)
                return;

            if (SelectedWorld is { IsCreated: false })
            {
                SelectedWorld = null;
            }

            if (NeedToChangeWorldDropDownMenu())
            {
                SelectedWorld = FindSelectedWorld();
                UpdateWorldDropDownMenu();
            }

            if (m_SelectedWorldChanged)
            {
                m_SelectedWorldChanged = false;
                OnWorldSelected(m_SelectedWorld);
            }

            OnUpdate();
        }

        protected void OnFocus()
        {
            Analytics.SendEditorEvent(m_AnalyticsWindowNameCached, Analytics.EventType.WindowFocus);
        }

        bool NeedToChangeWorldDropDownMenu()
        {
            if (null == m_WorldSelector)
                return false;

            if (m_WorldListChangeTracker.HasChanged())
            {
                m_WorldDisplayNameCache.RebuildCache();
                return true;
            }

            var showAdvancedWorlds = UserSettings<AdvancedSettings>.GetOrCreate(Constants.Settings.Advanced).ShowAdvancedWorlds;
            if (m_PreviousShowAdvancedWorldsValue != showAdvancedWorlds)
            {
                m_PreviousShowAdvancedWorldsValue = showAdvancedWorlds;
                return true;
            }

            return false;
        }

        protected void UpdateWorldDropDownMenu()
        {
            Assert.IsNotNull(m_WorldSelector);

            var menu = m_WorldSelector.menu;
            var menuItemsCount = menu.MenuItems().Count;

            for (var i = 0; i < menuItemsCount; i++)
            {
                menu.RemoveItemAt(0);
            }

            var advancedSettings = UserSettings<AdvancedSettings>.GetOrCreate(Constants.Settings.Advanced);

            if (World.All.Count > 0)
                AppendWorldMenu(menu, advancedSettings.ShowAdvancedWorlds);
            else
                m_WorldSelector.text = "No World";

            OnWorldsChanged(World.All.Count > 0);
        }

        void AppendWorldMenu(DropdownMenu menu, bool showAdvancedWorlds)
        {
            var worldCategories = WorldCategoryHelper.Categories;

            foreach (var category in worldCategories)
            {
                if (showAdvancedWorlds)
                {
                    menu.AppendAction(category.Name.ToUpper(), null, DropdownMenuAction.Status.Disabled);
                    AppendWorlds(menu, category);
                    menu.AppendSeparator();
                }
                else if (category.Flag == WorldFlags.Live)
                {
                    AppendWorlds(menu, category);
                    break;
                }
            }
        }

        void AppendWorlds(DropdownMenu menu, WorldCategoryHelper.Category category)
        {
            foreach (var world in category.Worlds)
            {
                menu.AppendAction(m_WorldDisplayNameCache.GetWorldDisplayName(world), OnWorldSelected, a =>
                    (SelectedWorld == world)
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal, world);
            }
        }

        void OnWorldSelected(DropdownMenuAction action)
        {
            var world = action.userData as World;
            BaseState.SelectedWorldName = world?.Name;
            SelectedWorld = world;
        }

        protected virtual void OnCreate() { }

        protected virtual void OnUpdate() { }

        protected virtual void OnCleanup() { }

        protected virtual void OnWorldsChanged(bool containsAnyWorld) { }

        protected virtual void OnWorldSelected(World world) { }

        internal class BaseStateContainer
        {
            public string SelectedWorldName;
            public string SearchFilter;
            public bool IsSearchFieldVisible = true; // Visible by default
        }
    }
}
