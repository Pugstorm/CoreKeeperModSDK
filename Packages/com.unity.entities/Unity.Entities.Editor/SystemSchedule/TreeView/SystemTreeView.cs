using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Editor.Bridge;
using Unity.Entities.UI;
using UnityEditor;
using UnityEngine.UIElements;
using ListView = Unity.Editor.Bridge.ListView;
using TreeView = Unity.Editor.Bridge.TreeView;

namespace Unity.Entities.Editor
{
    class SystemTreeView : VisualElement, IDisposable
    {
        static readonly string k_NoSystemsFoundTitle = L10n.Tr("No system matches your search");
        static readonly string k_ComponentTypeNotFoundTitle = L10n.Tr("Type not found");
        static readonly string k_ComponentTypeNotFoundContent = L10n.Tr("\"{0}\" is not a component type");

        internal readonly TreeView m_SystemTreeView; // internal for test.
        internal readonly IList<ITreeViewItem> m_TreeViewRootItems = new List<ITreeViewItem>();
        internal readonly ListView m_SystemListView; // For search results.
        internal readonly List<SystemTreeViewItem> m_ListViewFilteredItems = new List<SystemTreeViewItem>();

        int m_LastSelectedItemId;
        WorldProxy m_WorldProxy;
        readonly CenteredMessageElement m_SearchEmptyMessage;
        int m_ScrollToItemId = -1;

        public SearchQueryParser.ParseResult SearchFilter;
        ISearchQuery<SystemForSearch> m_CurrentSearchQuery;

        readonly List<SystemForSearch> m_AllSystemsForSearch = new List<SystemForSearch>();
        readonly Dictionary<string, string[]> m_SystemDependencyMap = new Dictionary<string, string[]>();
        readonly List<SystemForSearch> m_SearchResultsFlatSystemList = new List<SystemForSearch>();

        internal SystemGraph LocalSystemGraph;
        public static SystemProxy SelectedSystem;

        public bool ShowWorldColumn { get; set; }
        public bool ShowNamespaceColumn { get; set; }
        public bool ShowEntityCountColumn { get; set; }
        public bool ShowMorePrecisionForRunningTime { get; set; }
        public bool Show0sInEntityCountAndTimeColumn { get; set; }
        public bool ShowTimeColumn { get; set; }

        /// <summary>
        /// Constructor of the tree view.
        /// </summary>
        public SystemTreeView()
        {
            m_SystemTreeView = new TreeView(m_TreeViewRootItems, Constants.ListView.ItemHeight, MakeTreeViewItem, ReleaseTreeViewItem, BindTreeViewItem)
            {
                viewDataKey = "full-view",
                selectionType = SelectionType.Single,
                name = "SystemTreeView",
                style =
                {
                    flexGrow = 1,
                    minWidth = 645f
                }
            };

            m_SystemTreeView.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (m_ScrollToItemId == -1)
                    return;

                var tempId = m_ScrollToItemId;
                m_ScrollToItemId = -1;
                if (m_SystemTreeView.FindItem(tempId) != null)
                    m_SystemTreeView.ScrollToItem(tempId);
            });

            m_SystemTreeView.onSelectionChange += OnSelectionChanged;
            Add(m_SystemTreeView);

            m_SearchEmptyMessage = new CenteredMessageElement { Title = k_NoSystemsFoundTitle };
            m_SearchEmptyMessage.Hide();
            Add(m_SearchEmptyMessage);

            // Create list view for search results.
            m_SystemListView = new ListView(m_ListViewFilteredItems, Constants.ListView.ItemHeight, MakeListViewItem, ReleaseListViewItem, BindListViewItem)
            {
                viewDataKey = "search-view",
                selectionType = SelectionType.Single,
                name = "SystemListView",
                style =
                {
                    flexGrow = 1,
                    minWidth = 645f
                }
            };
            m_SystemListView.RegisterCallback<PointerDownEvent>(evt =>
            {
#if UNITY_2023_2_OR_NEWER
                if (evt.target == m_SystemListView.Q(className: ScrollView.contentAndVerticalScrollUssClassName))
                    Selection.activeObject = null;
#else
                if (evt.button == (int) MouseButton.LeftMouse)
                    Selection.activeObject = null;
#endif
            });
            m_SystemTreeView.RegisterCallback<PointerDownEvent>(evt =>
            {
#if UNITY_2023_2_OR_NEWER
                if (evt.target == m_SystemTreeView.Q(className: ScrollView.contentAndVerticalScrollUssClassName))
                    Selection.activeObject = null;
#else
                if (evt.button == (int) MouseButton.LeftMouse)
                    Selection.activeObject = null;
#endif
            });

            m_SystemListView.onSelectionChange += OnSelectionChanged;

            Add(m_SystemListView);
        }

        void OnSelectionChanged(IEnumerable<object> selection)
        {
            if (selection.FirstOrDefault() is SystemTreeViewItem selectedItem)
                OnSelectionChanged(selectedItem);
        }

        void OnSelectionChanged(SystemTreeViewItem selectedItem)
        {
            // By selecting a system within Systems window, we need to clear up SelectedSystem which is set only from the outside.
            SelectedSystem = default;

            if (selectedItem == null || !selectedItem.SystemProxy.Valid)
                return;

            m_LastSelectedItemId = selectedItem.id;
            m_ScrollToItemId = selectedItem.id;

            OpenInspector(selectedItem.SystemProxy);
        }

        void OpenInspector(SystemProxy systemProxy)
        {
            SelectionUtility.ShowInInspector(new SystemContentProvider
            {
                World = systemProxy.World,
                SystemProxy = systemProxy,
                LocalSystemGraph = LocalSystemGraph
            }, new InspectorContentParameters
            {
                UseDefaultMargins = false,
                ApplyInspectorStyling = false
            });
        }

        VisualElement MakeTreeViewItem() => SystemInformationVisualElement.Acquire(this);

        static void ReleaseTreeViewItem(VisualElement ve) => ((SystemInformationVisualElement)ve).Release();

        VisualElement MakeListViewItem()
        {
            // ListView changes user created VisualElements in a way that no reversible using public API
            // Wrapping pooled item in a non reusable container prevent us from reusing a pooled item in an eventual checked pseudo state
            var wrapper = new VisualElement();
            wrapper.Add(SystemInformationVisualElement.Acquire(this));
            return wrapper;
        }

        static void ReleaseListViewItem(VisualElement ve) => ((SystemInformationVisualElement)ve[0]).Release();

        public void SetFilter(ISearchQuery<SystemForSearch> searchQuery, SearchQueryParser.ParseResult parseResult)
        {
            m_CurrentSearchQuery = searchQuery;
            SearchFilter = parseResult;
            Refresh();
        }

        public void Refresh(WorldProxy worldProxy)
        {
            m_WorldProxy = worldProxy;

            m_AllSystemsForSearch.Clear();
            m_SystemDependencyMap.Clear();

            RecreateTreeViewRootItems();
            FillSystemDependencyCache();
            Refresh();
        }

        void RecreateTreeViewRootItems()
        {
            ReleaseAllPooledItems();

            if (World.All.Count > 0 && string.IsNullOrEmpty(SearchFilter.ErrorComponentType))
            {
                var graph = LocalSystemGraph;

                foreach (var node in graph.Roots)
                {
                    if (!node.ShowForWorldProxy(m_WorldProxy))
                        continue;

                    var item = SystemTreeViewItem.Acquire((PlayerLoopSystemGraph)graph, node, null, m_WorldProxy);
                    PopulateAllChildren(item);
                    m_TreeViewRootItems.Add(item);
                }

                m_SystemTreeView.Refresh();
            }
        }

        void PopulateAllChildren(SystemTreeViewItem item)
        {
            if (item.SystemProxy.Valid)
            {
                var systemForSearch = new SystemForSearch(item.SystemProxy)
                {
                    Node = item.Node,
                    SystemItemId = item.id
                };
                m_AllSystemsForSearch.Add(systemForSearch);

                BuildSystemDependencyMap(item.SystemProxy);
            }

            if (!item.HasChildren)
                return;

            item.PopulateChildren();

            foreach (var child in item.children)
            {
                PopulateAllChildren(child as SystemTreeViewItem);
            }
        }

        void BuildSystemDependencyMap(SystemProxy systemProxy)
        {
            var keyString = systemProxy.TypeName;

            // TODO: Find better solution to be able to uniquely identify each system.
            // At the moment, we are using system name to identify each system, which is not reliable
            // because there can be multiple systems with the same name in a world. This is only a
            // temporary solution to avoid the error of adding the same key into the map. We need to
            // find a proper solution to be able to uniquely identify each system.
            if (!m_SystemDependencyMap.ContainsKey(keyString))
            {
                var handle = systemProxy;
                var dependencies = handle.UpdateBeforeSet
                    .Concat(handle.UpdateAfterSet)
                    .Select(s => s.TypeName)
                    .ToArray();
                m_SystemDependencyMap.Add(keyString, dependencies);
            }
        }

        void FillSystemDependencyCache()
        {
            foreach (var systemForSearch in m_AllSystemsForSearch)
            {
                systemForSearch.SystemDependencyCache = (from kvp in m_SystemDependencyMap where kvp.Value.Contains(systemForSearch.SystemName) select kvp.Key).ToArray();
            }
        }

        void BuildFilterResults()
        {
            m_SearchResultsFlatSystemList.Clear();
            if (m_CurrentSearchQuery == null || string.IsNullOrWhiteSpace(m_CurrentSearchQuery.SearchString) || m_CurrentSearchQuery.Tokens.Count == 0 && string.IsNullOrEmpty(SearchFilter.ErrorComponentType))
            {
                m_SearchResultsFlatSystemList.AddRange(m_AllSystemsForSearch);
            }
            else
            {
#if QUICKSEARCH_AVAILABLE
                m_SearchResultsFlatSystemList.AddRange( m_CurrentSearchQuery.Apply(m_AllSystemsForSearch));
#else
                using (var candidates = PooledHashSet<SystemForSearch>.Make())
                {
                    foreach (var system in m_AllSystemsForSearch)
                    {
                        if (SearchFilter.Names.All(n => system.SystemName.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0))
                            candidates.Set.Add(system);

                        if (candidates.Set.Contains(system) && !SearchFilter.ComponentNames.All(component => system.ComponentNamesInQuery.Any(c => c.IndexOf(component, StringComparison.OrdinalIgnoreCase) >= 0)))
                            candidates.Set.Remove(system);

                        if (candidates.Set.Contains(system) && SearchFilter.DependencySystemNames.All(dependency => system.SystemDependency.Any(c => c.IndexOf(dependency, StringComparison.OrdinalIgnoreCase) >= 0)))
                            m_SearchResultsFlatSystemList.Add(system);
                    }
                }
#endif
            }
        }

        void PopulateListViewWithSearchResults()
        {
            BuildFilterResults();

            foreach (var filteredItem in m_ListViewFilteredItems)
            {
                filteredItem.Release();
            }
            m_ListViewFilteredItems.Clear();
            foreach (var system in m_SearchResultsFlatSystemList)
            {
                var listViewItems = SystemTreeViewItem.Acquire(LocalSystemGraph, system.Node, null, m_WorldProxy);
                m_ListViewFilteredItems.Add(listViewItems);
            }

            m_SystemListView.Refresh();
        }

        /// <summary>
        /// Refresh tree view to update with latest information.
        /// </summary>
        void Refresh()
        {
            // Check if there is search result
            if (!SearchFilter.IsEmpty)
            {
                PopulateListViewWithSearchResults();
                var hasSearchResult = m_ListViewFilteredItems.Any();

                m_SystemListView.SetVisibility(hasSearchResult);
                m_SystemTreeView.Hide();

                m_SearchEmptyMessage.SetVisibility(!hasSearchResult);
                if (string.IsNullOrEmpty(SearchFilter.ErrorComponentType))
                {
                    m_SearchEmptyMessage.Title = k_NoSystemsFoundTitle;
                    m_SearchEmptyMessage.Message = string.Empty;
                }
                else
                {
                    m_SearchEmptyMessage.Title = k_ComponentTypeNotFoundTitle;
                    m_SearchEmptyMessage.Message = string.Format(k_ComponentTypeNotFoundContent, SearchFilter.ErrorComponentType);
                }
            }
            else
            {
                m_SystemListView.Hide();
                m_SystemTreeView.Show();

                m_SearchEmptyMessage.Hide();
            }

            SetSelection();
        }

        public void SetSelection()
        {
            if (m_WorldProxy == null)
                return;

            if (SelectedSystem.Valid && SelectedSystem.WorldProxy.Equals(m_WorldProxy) && m_AllSystemsForSearch.Count > 0)
            {
                 var selectedSystem = m_AllSystemsForSearch.FirstOrDefault(s => s.SystemProxy.Equals(SelectedSystem));
                 if (selectedSystem != null)
                     m_LastSelectedItemId = selectedSystem.SystemItemId;
            }

            if (SearchFilter.IsEmpty) // Tree view
            {
                m_SystemTreeView.ClearSelection();
                if (m_SystemTreeView.FindItem(m_LastSelectedItemId) == null)
                    return;

                m_SystemTreeView.Select(m_LastSelectedItemId, false);
            }
            else // List view
            {
                m_SystemListView.ClearSelection();
                var index = m_ListViewFilteredItems.FindIndex(item => item.id == m_LastSelectedItemId);
                if (index == -1)
                    return;

                m_SystemListView.ScrollToItem(index);
                m_SystemListView.selectedIndex = index;
            }
        }

        void BindTreeViewItem(VisualElement element, ITreeViewItem item)
        {
            var target = item as SystemTreeViewItem;
            var systemInformationElement = element as SystemInformationVisualElement;
            if (null == systemInformationElement)
                return;

            systemInformationElement.Target = target;
            systemInformationElement.Update();
        }

        void BindListViewItem(VisualElement element, int itemIndex) => BindTreeViewItem(element[0], (ITreeViewItem)m_SystemListView.itemsSource[itemIndex]);

        public void Dispose() => ReleaseAllPooledItems();

        void ReleaseAllPooledItems()
        {
            foreach (var rootItem in m_TreeViewRootItems)
            {
                ((SystemTreeViewItem)rootItem).Release();
            }
            m_TreeViewRootItems.Clear();

            foreach (var filteredItem in m_ListViewFilteredItems)
            {
                filteredItem.Release();
            }
            m_ListViewFilteredItems.Clear();
        }
    }
}
