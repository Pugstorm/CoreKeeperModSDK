using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;
using Unity.Entities.UI;
using Unity.Serialization.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ListView = UnityEngine.UIElements.ListView;

namespace Unity.Entities.Editor
{
    class ComponentsWindow : EditorWindow
    {
        static readonly string k_ComponentWindowName = L10n.Tr("Components");
        static readonly string k_ComponentContentName = L10n.Tr("Component");
        internal static readonly List<ComponentTypeViewData> s_Types = new List<ComponentTypeViewData>();
        static bool s_Initialized;
        static Type s_SelectedComponent;
        SearchElement m_SearchElement;
        ListView m_ListView;
        List<ComponentTypeViewData> m_FilteredTypes;

        State m_State;
        class State
        {
            public string SearchString;
        }

        [SerializeField]
        string m_ComponentsWindowInstanceKey;
        string ComponentsWindowInstanceKey
        {
            get
            {
                if (string.IsNullOrEmpty(m_ComponentsWindowInstanceKey))
                    m_ComponentsWindowInstanceKey = Guid.NewGuid().ToString("N");

                return m_ComponentsWindowInstanceKey;
            }
        }

        [Flags]
        internal enum DebugTypeCategory
        {
            None = 0,
            Data = 1 << 1,
            Buffer = 1 << 2,
            Shared = 1 << 3,
            Entity = 1 << 4,
            Tag = 1 << 5,
            Managed = 1 << 6,
            Companion = 1 << 7
        }

        internal readonly struct ComponentTypeViewData
        {
            public readonly Type Type;
            public readonly string Name;
            public readonly DebugTypeCategory Category;
            public readonly string IconClass;
            public string DisplayName => ComponentsUtility.GetComponentDisplayName(TypeUtility.GetTypeDisplayName(Type));

            public ComponentTypeViewData(TypeManager.TypeInfo typeInfo)
            {
                Type = typeInfo.Type;
                Name = TypeUtility.GetTypeDisplayName(Type);
                Category = DebugTypeCategory.None;
                switch (typeInfo.Category)
                {
                    case TypeManager.TypeCategory.ComponentData:
                        if (TypeManager.IsZeroSized(typeInfo.TypeIndex))
                        {
                            Category |= DebugTypeCategory.Tag;
                            IconClass = "tag-component-icon";
                        }
                        else
                        {
                            Category |= DebugTypeCategory.Data;
                            IconClass = "component-icon";
                        }
                        break;
                    case TypeManager.TypeCategory.BufferData:
                        Category |= DebugTypeCategory.Buffer;
                        IconClass = "buffer-component-icon";
                        break;
                    case TypeManager.TypeCategory.ISharedComponentData:
                        Category |= DebugTypeCategory.Shared;
                        IconClass = "shared-component-icon";
                        break;
                    case TypeManager.TypeCategory.EntityData:
                        Category |= DebugTypeCategory.Entity;
                        IconClass = "entity-icon";
                        break;
                    case TypeManager.TypeCategory.UnityEngineObject:
                        Category |= DebugTypeCategory.Companion;
                        IconClass = "companion-component-icon";
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (TypeManager.IsManagedComponent(typeInfo.TypeIndex))
                {
                    Category |= DebugTypeCategory.Managed;
                    IconClass = "managed-component-icon";
                }
            }
        }

        [MenuItem(Constants.MenuItems.ComponentsWindow, false, Constants.MenuItems.ComponentsWindowPriority)]
        static void Open()
        {
            GetWindow<ComponentsWindow>();
        }

        protected void OnFocus()
        {
            Analytics.SendEditorEvent(Analytics.Window.Components, Analytics.EventType.WindowFocus);
        }

        static void CacheComponentsData()
        {
            if (s_Initialized)
                return;

            // Note: if a type is added "after-the-fact", it might not be displayed in the window, we can add a way to
            // detect this if and when that time comes.
            TypeManager.Initialize();
            foreach (var type in TypeManager.AllTypes)
            {
                // First type is the "null" type, which we don't care about.
                if (null == type.Type)
                    continue;

                // TypeManager will generate type info for all types derived from UnityEngine.Object, so we are skipping
                // them here
                if (type.Category == TypeManager.TypeCategory.UnityEngineObject)
                    continue;

                s_Types.Add(new ComponentTypeViewData(type));
            }
            s_Types.Sort((lhs, rhs) => string.Compare(lhs.Name, rhs.Name, StringComparison.Ordinal));
            s_Initialized = true;
        }

        void CreateGUI()
        {
            titleContent = EditorGUIUtility.TrTextContent(k_ComponentWindowName, EditorIcons.Component);
            CacheComponentsData();
            minSize = Constants.MinWindowSize;
            titleContent.text = k_ComponentWindowName;
            m_FilteredTypes = new List<ComponentTypeViewData>();
            m_FilteredTypes.AddRange(s_Types);
            m_State = SessionState<State>.GetOrCreate($"{nameof(ComponentsWindow)}.{nameof(State)}.{ComponentsWindowInstanceKey}");

            Selection.selectionChanged += OnGlobalSelectionChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            Build();
        }

        void OnDisable()
        {
            Selection.selectionChanged -= OnGlobalSelectionChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        void Build()
        {
            Resources.Templates.ContentProvider.ComponentsWindow.Clone(rootVisualElement);
            Resources.Templates.DotsEditorCommon.AddStyles(rootVisualElement);
            m_ListView = rootVisualElement.Q<ListView>(className:"components-window__list-view");
            m_ListView.fixedItemHeight = Constants.ListView.ItemHeight;

            m_ListView.makeItem = () => new ComponentTypeView();
            m_ListView.bindItem = (element, i) =>
            {
                ((ComponentTypeView)element).UpdateTarget(m_FilteredTypes[i]);
            };
            m_ListView.itemsSource = m_FilteredTypes;
            m_ListView.selectionChanged += objects =>
            {
                var componentTypeViewData = objects.OfType<ComponentTypeViewData>().FirstOrDefault();
                if (componentTypeViewData.Type == null)
                    return;

                SelectionUtility.ShowInInspector(new ComponentContentProvider
                {
                    ComponentType = componentTypeViewData.Type
                }, new InspectorContentParameters
                {
                    ApplyInspectorStyling = false,
                    UseDefaultMargins = false
                });

                s_SelectedComponent = componentTypeViewData.Type;
            };

            m_SearchElement = rootVisualElement.Q<SearchElement>(className: "components-window__search-element");
            m_SearchElement.RegisterSearchQueryHandler<ComponentTypeViewData>(handler =>
            {
                m_FilteredTypes.Clear();
                m_FilteredTypes.AddRange(handler.Apply(s_Types));
                m_State.SearchString = handler.SearchString;
                m_ListView.Rebuild();
                SetSelection();
            });
            m_SearchElement.value = m_State.SearchString;
            m_SearchElement.Search();
            m_SearchElement.parent.Add(SearchUtils.CreateJumpButton(() => ComponentSearchProvider.OpenProvider(m_SearchElement.value)));

            SetSelection();
        }

        void SetSelection()
        {
            if (s_SelectedComponent == null || s_Types.Count == 0)
                return;

            var searchList = string.IsNullOrEmpty(m_SearchElement.value) ? s_Types : m_FilteredTypes;
            var index = searchList.FindIndex(c => c.Type == s_SelectedComponent);
            if (index == -1)
                return;

            m_ListView.ClearSelection();
            m_ListView.ScrollToItem(index);
            m_ListView.selectedIndex = index;
        }

        void OnGlobalSelectionChanged()
        {
            if (Selection.activeObject is InspectorContent content && content.Content.Name.Equals(k_ComponentContentName))
                return;

            s_SelectedComponent = null;
            m_ListView.ClearSelection();
        }

        void OnPlayModeStateChanged(PlayModeStateChange obj)
        {
            if (obj is not (PlayModeStateChange.ExitingEditMode or PlayModeStateChange.ExitingPlayMode))
                return;

            s_SelectedComponent = null;
            m_ListView.ClearSelection();

            SelectionUtility.ShowInInspector(new ComponentContentProvider
            {
                ComponentType = null
            }, new InspectorContentParameters
            {
                ApplyInspectorStyling = false,
                UseDefaultMargins = false
            });
        }

        public static void HighlightComponent(Type componentType)
        {
            s_SelectedComponent = componentType;

            if (HasOpenInstances<ComponentsWindow>())
                GetWindow<ComponentsWindow>().SetSelection();
        }

        class ComponentTypeView : VisualElement
        {
            VisualElement m_Icon;
            string m_PreviousIconClass;
            Label m_ComponentNameLabel;
            Label m_ComponentTypeLabel;

            ComponentTypeViewData m_Target;

            public ComponentTypeView()
            {
                Resources.AddCommonVariables(this);
                Resources.Templates.ComponentTypeView.Clone(this);

                m_Icon = this.Q(classes: "component-type-view__icon");
                m_ComponentNameLabel = this.Q<Label>(classes: "component-type-view__name");
                m_ComponentTypeLabel = this.Q<Label>(classes: "component-type-view__category");
            }

            public void UpdateTarget(ComponentTypeViewData target)
            {
                m_Target = target;

                m_ComponentNameLabel.text = m_Target.DisplayName;
                m_ComponentTypeLabel.text = m_Target.Category.ToString();

                var currentIconClass = m_Target.IconClass;
                if (currentIconClass == m_PreviousIconClass)
                    return;

                if (!string.IsNullOrEmpty(m_PreviousIconClass))
                    m_Icon.RemoveFromClassList(m_PreviousIconClass);

                m_Icon.AddToClassList(currentIconClass);
                m_PreviousIconClass = currentIconClass;
            }
        }
    }
}
