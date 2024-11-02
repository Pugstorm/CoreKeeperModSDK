using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Editor.Bridge;
using Unity.Entities.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using static Unity.Entities.StructuralChangesProfiler;
using TreeView = Unity.Editor.Bridge.TreeView;

namespace Unity.Entities.Editor
{
    partial class StructuralChangesProfilerModule
    {
        class StructuralChangesProfilerModuleView
        {
            static readonly string s_StructuralChanges = L10n.Tr("Structural Changes");
            static readonly string s_All = L10n.Tr("All");
            static readonly string s_Cost = L10n.Tr("Cost (ms)");
            static readonly string s_Count = L10n.Tr("Count");
            static readonly string s_CreateEntity = L10n.Tr(k_CreateEntityCounterName);
            static readonly string s_DestroyEntity = L10n.Tr(k_DestroyEntityCounterName);
            static readonly string s_AddComponent = L10n.Tr(k_AddComponentCounterName);
            static readonly string s_RemoveComponent = L10n.Tr(k_RemoveComponentCounterName);
            static readonly string s_SetSharedComponent = L10n.Tr(k_SetSharedComponentCounterName);

            static readonly VisualElementTemplate s_WindowTemplate = PackageResources.LoadTemplate("ProfilerModules/structural-changes-profiler-window");
            static readonly VisualElementTemplate s_TreeViewItemTemplate = PackageResources.LoadTemplate("ProfilerModules/structural-changes-profiler-tree-view-item");

            StructuralChangesProfilerTreeViewItemData[] m_StructuralChangesDataSource;
            readonly List<StructuralChangesProfilerTreeViewItemData> m_StructuralChangesDataFiltered = new List<StructuralChangesProfilerTreeViewItemData>();

            VisualElement m_Window;
            Label m_Message;
            VisualElement m_Content;
            SearchElement m_SearchElement;
            TreeView m_TreeView;

            public StructuralChangesProfilerTreeViewItemData[] StructuralChangesDataSource
            {
                get => m_StructuralChangesDataSource;
                set => m_StructuralChangesDataSource = value;
            }

            public bool HasStructuralChangesDataSource => m_StructuralChangesDataSource?.Length > 0;

            public Action SearchFinished { get; set; }

            public VisualElement Create()
            {
                m_Window = s_WindowTemplate.Clone();

                var toolbar = m_Window.Q<Toolbar>("toolbar");
                m_SearchElement = toolbar.Q<SearchElement>("search");
                m_SearchElement.AddSearchDataCallback<StructuralChangesProfilerTreeViewItemData>(data =>
                {
                    var result = new string[3];
                    result[0] = ObjectNames.NicifyVariableName(data.Type.ToString());
                    result[1] = data.WorldName;
                    result[2] = data.SystemName;
                    return result;
                });
                m_SearchElement.AddSearchFilterCallbackWithPopupItem<StructuralChangesProfilerTreeViewItemData, double>("cost", data => data.ElapsedNanoseconds * 1e-6, s_Cost);
                m_SearchElement.FilterPopupWidth = 250;

                var searchHandler = new SearchHandler<StructuralChangesProfilerTreeViewItemData>(m_SearchElement)
                {
                    Mode = SearchHandlerType.async
                };
                searchHandler.SetSearchDataProvider(() =>
                {
                    return m_StructuralChangesDataSource;
                });
                searchHandler.OnBeginSearch += query =>
                {
                    m_StructuralChangesDataFiltered.Clear();
                };
                searchHandler.OnFilter += (query, filteredData) =>
                {
                    m_StructuralChangesDataFiltered.AddRange(filteredData);
                    SearchFinished?.Invoke();
                };
                // Work-around: OnFilter is not called if the search result yields no data.
                // So for the moment, we force a refresh in OnEndSearch, which means 2 refresh :(
                searchHandler.OnEndSearch += query =>
                {
                    SearchFinished?.Invoke();
                };

                m_Message = m_Window.Q<Label>("message");
                m_Message.text = s_NoFrameDataAvailable;

                m_Content = m_Window.Q("content");
                m_Content.SetVisibility(false);

                var header = m_Content.Q("header");
                header.Q<Label>("column1").text = s_StructuralChanges;
                header.Q<Label>("column2").text = s_Cost;
                header.Q<Label>("column3").text = s_Count;

                var container = m_Content.Q("tree-view-container");

                m_TreeView = new TreeView();
                container.Add(m_TreeView);
                m_TreeView.itemHeight = 18;
                m_TreeView.AddToClassList("structural-changes-profiler-window__tree-view");
                m_TreeView.makeItem = () =>
                {
                    return s_TreeViewItemTemplate.Clone();
                };
                m_TreeView.bindItem = (element, item) =>
                {
                    var itemData = (StructuralChangesProfilerTreeViewItem)item;
                    element.Q<Label>("column1").text = itemData.displayName;
                    element.Q<Label>("column2").text = FormattingUtility.NsToMsString(itemData.totalElapsedNanoseconds);
                    element.Q<Label>("column3").text = FormattingUtility.CountToString(itemData.totalCount);
                };
                m_TreeView.selectionType = SelectionType.Single;

                return m_Window;
            }

            public void Update()
            {
                if (m_Window == null)
                    return;

                var itemId = 0;
                var rootItem = new StructuralChangesProfilerTreeViewItem { id = itemId++, displayName = s_All };
                var createEntityItem = new StructuralChangesProfilerTreeViewItem { id = itemId++, displayName = s_CreateEntity };
                var destroyEntityItem = new StructuralChangesProfilerTreeViewItem { id = itemId++, displayName = s_DestroyEntity };
                var addComponentItem = new StructuralChangesProfilerTreeViewItem { id = itemId++, displayName = s_AddComponent };
                var removeComponentItem = new StructuralChangesProfilerTreeViewItem { id = itemId++, displayName = s_RemoveComponent };
                var setSharedComponentItem = new StructuralChangesProfilerTreeViewItem { id = itemId++, displayName = s_SetSharedComponent };

                foreach (var data in m_StructuralChangesDataFiltered)
                {
                    var eventItem = default(StructuralChangesProfilerTreeViewItem);
                    switch (data.Type)
                    {
                        case StructuralChangeType.CreateEntity:
                            eventItem = createEntityItem;
                            break;
                        case StructuralChangeType.DestroyEntity:
                            eventItem = destroyEntityItem;
                            break;
                        case StructuralChangeType.AddComponent:
                            eventItem = addComponentItem;
                            break;
                        case StructuralChangeType.RemoveComponent:
                            eventItem = removeComponentItem;
                            break;
                        case StructuralChangeType.SetSharedComponent:
                            eventItem = setSharedComponentItem;
                            break;
                        default:
                            throw new NotImplementedException(data.Type.ToString());
                    }

                    var worldItem = eventItem.children.FirstOrDefault(item => item.displayName == data.WorldName);
                    if (worldItem == null)
                    {
                        worldItem = new StructuralChangesProfilerTreeViewItem { id = itemId++, displayName = data.WorldName };
                        eventItem.AddChild(worldItem);
                    }

                    var systemItem = worldItem.children.FirstOrDefault(item => item.displayName == data.SystemName);
                    if (systemItem == null)
                    {
                        systemItem = new StructuralChangesProfilerTreeViewItem { id = itemId++, displayName = data.SystemName };
                        worldItem.AddChild(systemItem);
                    }

                    systemItem.totalElapsedNanoseconds += data.ElapsedNanoseconds;
                    systemItem.totalCount++;
                    worldItem.totalElapsedNanoseconds += data.ElapsedNanoseconds;
                    worldItem.totalCount++;
                    eventItem.totalElapsedNanoseconds += data.ElapsedNanoseconds;
                    eventItem.totalCount++;
                    rootItem.totalElapsedNanoseconds += data.ElapsedNanoseconds;
                    rootItem.totalCount++;
                }

                if (createEntityItem.hasChildren)
                    rootItem.AddChild(createEntityItem);
                if (destroyEntityItem.hasChildren)
                    rootItem.AddChild(destroyEntityItem);
                if (addComponentItem.hasChildren)
                    rootItem.AddChild(addComponentItem);
                if (removeComponentItem.hasChildren)
                    rootItem.AddChild(removeComponentItem);
                if (setSharedComponentItem.hasChildren)
                    rootItem.AddChild(setSharedComponentItem);

                AddLeafCountRecursive(rootItem);

                rootItem.SortChildrenRecursive(item => item.totalElapsedNanoseconds, false);
                if (rootItem.hasChildren)
                {
                    m_TreeView.rootItems = new[] { rootItem };
                    m_TreeView.ExpandItem(rootItem.id);
                }
                else
                {
                    m_TreeView.rootItems = Array.Empty<StructuralChangesProfilerTreeViewItem>();
                }

                m_Message.SetVisibility(false);
                m_Content.SetVisibility(true);
            }

            public void Search()
            {
                if (m_Window == null)
                    return;

                m_SearchElement.Search();
            }

            public void Clear(string message)
            {
                if (m_Window == null)
                    return;

                m_StructuralChangesDataSource = null;
                m_StructuralChangesDataFiltered.Clear();
                m_TreeView.rootItems = Array.Empty<StructuralChangesProfilerTreeViewItem>();
                m_Message.SetVisibility(true);
                m_Message.text = message;
                m_Content.SetVisibility(false);
            }

            int AddLeafCountRecursive(StructuralChangesProfilerTreeViewItem item)
            {
                var count = item.hasChildren ? 0 : 1;
                foreach (var child in item.children)
                    count += AddLeafCountRecursive(child);
                if (item.hasChildren)
                    item.displayName += $" ({count})";
                return count;
            }
        }
    }
}
