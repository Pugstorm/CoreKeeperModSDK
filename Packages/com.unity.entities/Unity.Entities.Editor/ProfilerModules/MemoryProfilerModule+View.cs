using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Editor.Bridge;
using Unity.Entities.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using TreeView = Unity.Editor.Bridge.TreeView;

namespace Unity.Entities.Editor
{
    partial class MemoryProfilerModule
    {
        public class MemoryProfilerModuleView
        {
            const string k_UserSettingsKey = "Entities" + nameof(MemoryProfiler) + ".";
            const string k_ShowEmptyArchetypesKey = k_UserSettingsKey + nameof(ShowEmptyArchetypes);

            static readonly string s_ShowEmptyArchetypes = L10n.Tr("Show Empty Archetypes");
            static readonly string s_All = L10n.Tr("All");
            static readonly string s_Archetypes = L10n.Tr("Archetypes");
            static readonly string s_Allocated = L10n.Tr("Allocated");
            static readonly string s_Unused = L10n.Tr("Unused");
            static readonly string s_EntityCount = L10n.Tr("Entities");
            static readonly string s_UnusedEntities = L10n.Tr("Unused Entities");
            static readonly string s_ChunkCount = L10n.Tr("Chunks");
            static readonly string s_ChunkCapacity = L10n.Tr("Chunk Capacity");
            static readonly string s_Components = L10n.Tr("Components");
            static readonly string s_ExternalComponents = L10n.Tr("External Components");
            static readonly string s_ChunkComponents = L10n.Tr("Chunk Components");
            static readonly string s_SharedComponents = L10n.Tr("Shared Components");
            static readonly string s_Segments = L10n.Tr("Segments");
            static readonly string s_Unknown = L10n.Tr("Unknown");
            static readonly string s_ComponentSizeInChunkTooltip = L10n.Tr("Component size in chunk.");
            static readonly string s_ComponentsSizeInChunkTooltip = L10n.Tr("Components size in chunk.");

            static readonly VisualElementTemplate s_WindowTemplate = PackageResources.LoadTemplate("ProfilerModules/memory-profiler-window");
            static readonly VisualElementTemplate s_LeftPaneTemplate = PackageResources.LoadTemplate("ProfilerModules/memory-profiler-left-pane");
            static readonly VisualElementTemplate s_RightPaneTemplate = PackageResources.LoadTemplate("ProfilerModules/memory-profiler-right-pane");
            static readonly VisualElementTemplate s_TreeViewItemTemplate = PackageResources.LoadTemplate("ProfilerModules/memory-profiler-tree-view-item");
            static readonly VisualElementTemplate s_ComponentTemplate = PackageResources.LoadTemplate("ProfilerModules/memory-profiler-component");

            MemoryProfilerTreeViewItemData[] m_ArchetypesDataSource;
            readonly List<MemoryProfilerTreeViewItemData> m_ArchetypesDataFiltered = new List<MemoryProfilerTreeViewItemData>();

            VisualElement m_Window;
            TwoPaneSplitView m_Splitter;

            // Left pane elements
            VisualElement m_LeftPane;
            SearchElement m_SearchElement;
            Label m_Message;
            VisualElement m_Content;
            TreeView m_TreeView;

            // Right pane elements
            VisualElement m_RightPane;
            TextField m_ArchetypeName;
            Label m_EntityCount;
            Label m_UnusedEntityCount;
            Label m_ChunkCount;
            Label m_ChunkCapacity;
            //Label m_SegmentCount;
            FoldoutField m_ComponentsFoldout;
            Label m_ComponentsSizeInChunk;
            Label m_ExternalComponents;
            FoldoutField m_ChunkComponentsFoldout;
            FoldoutField m_SharedComponentsFoldout;

            bool ShowEmptyArchetypes
            {
                get => EditorUserSettings.GetConfigValue(k_ShowEmptyArchetypesKey) == true.ToString();
                set => EditorUserSettings.SetConfigValue(k_ShowEmptyArchetypesKey, value ? value.ToString() : null);
            }

            public MemoryProfilerTreeViewItemData[] ArchetypesDataSource
            {
                get => m_ArchetypesDataSource;
                set => m_ArchetypesDataSource = value;
            }

            public bool HasArchetypesDataSource => m_ArchetypesDataSource?.Length > 0;

            public Action SearchFinished { get; set; }

            public VisualElement Create()
            {
                m_Window = s_WindowTemplate.Clone();
                Resources.Templates.DotsEditorCommon.AddStyles(m_Window);
                m_Splitter = m_Window.Q<TwoPaneSplitView>("splitter");
                CreateViewLeftPane(m_Splitter.Q("left-pane"));
                CreateViewRightPane(m_Splitter.Q("right-pane"));
                return m_Window;
            }

            void CreateViewLeftPane(VisualElement root)
            {
                m_LeftPane = s_LeftPaneTemplate.Clone(root);

                var toolbar = m_LeftPane.Q<Toolbar>("toolbar");
                m_SearchElement = toolbar.Q<SearchElement>("search");
                m_SearchElement.AddSearchDataCallback<MemoryProfilerTreeViewItemData>(data =>
                {
                    return data.ComponentTypes
                        .Select(t => TypeManager.GetType(t))
                        .Where(t => t != null)
                        .Select(t => t.Name)
                        .Append(FormattingUtility.HashToString(data.StableHash))
                        .ToArray();
                });
                m_SearchElement.AddSearchFilterCallbackWithPopupItem<MemoryProfilerTreeViewItemData, ulong>("allocated", data => data.AllocatedBytes, "Allocated Bytes");
                m_SearchElement.AddSearchFilterCallbackWithPopupItem<MemoryProfilerTreeViewItemData, ulong>("unused", data => data.UnusedBytes, "Unused Bytes");
                m_SearchElement.AddSearchFilterCallbackWithPopupItem<MemoryProfilerTreeViewItemData, int>("entities", data => data.EntityCount, "Entity Count");
                m_SearchElement.AddSearchFilterCallbackWithPopupItem<MemoryProfilerTreeViewItemData, int>("unusedEntities", data => data.UnusedEntityCount, "Unused Entity Count");
                m_SearchElement.AddSearchFilterCallbackWithPopupItem<MemoryProfilerTreeViewItemData, int>("chunks", data => data.ChunkCount, "Chunk Count");
                m_SearchElement.AddSearchFilterCallbackWithPopupItem<MemoryProfilerTreeViewItemData, int>("capacity", data => data.ChunkCapacity, "Chunk Capacity");
                m_SearchElement.AddSearchFilterCallbackWithPopupItem<MemoryProfilerTreeViewItemData, int>("segments", data => data.SegmentCount, "Segment Count");
                m_SearchElement.AddSearchFilterCallbackWithPopupItem<MemoryProfilerTreeViewItemData, int>("components", data => data.ComponentTypes.Length, "Component Type Count");
                m_SearchElement.FilterPopupWidth = 250;

                m_SearchElement.parent.Add(SearchUtils.CreateJumpButton(() => ArchetypeSearchProvider.OpenProvider(m_SearchElement.value)));

                var searchHandler = new SearchHandler<MemoryProfilerTreeViewItemData>(m_SearchElement)
                {
                    Mode = SearchHandlerType.async
                };
                searchHandler.SetSearchDataProvider(() =>
                {
                    return m_ArchetypesDataSource?.Where(a => a.EntityCount > 0 || ShowEmptyArchetypes) ?? Enumerable.Empty<MemoryProfilerTreeViewItemData>();
                });
                searchHandler.OnBeginSearch += query =>
                {
                    m_ArchetypesDataFiltered.Clear();
                };
                searchHandler.OnFilter += (query, filteredData) =>
                {
                    m_ArchetypesDataFiltered.AddRange(filteredData);
                    SearchFinished?.Invoke();
                };
                // Work-around: OnFilter is not called if the search result yields no data.
                // So for the moment, we force a refresh in OnEndSearch, which means 2 refresh :(
                searchHandler.OnEndSearch += query =>
                {
                    SearchFinished?.Invoke();
                };

                var options = toolbar.Q<Button>("options");
                options.clicked += () =>
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent(s_ShowEmptyArchetypes), ShowEmptyArchetypes, () =>
                    {
                        ShowEmptyArchetypes = !ShowEmptyArchetypes;
                        m_SearchElement.Search();
                    });
                    menu.DropDown(options.worldBound);
                };

                m_Message = m_LeftPane.Q<Label>("message");
                m_Content = m_LeftPane.Q("content");
                m_Content.SetVisibility(false);

                var leftHeader = m_Content.Q("header");
                leftHeader.Q<Label>("column1").text = s_Archetypes;
                leftHeader.Q<Label>("column2").text = s_Allocated;
                leftHeader.Q<Label>("column3").text = s_Unused;

                var container = m_Content.Q("tree-view-container");
                m_TreeView = new TreeView();
                container.Add(m_TreeView);
                m_TreeView.itemHeight = 18;
                m_TreeView.AddToClassList("memory-profiler-left-pane__tree-view");
                m_TreeView.makeItem = () =>
                {
                    return s_TreeViewItemTemplate.Clone();
                };
                m_TreeView.bindItem = (element, item) =>
                {
                    var itemData = (MemoryProfilerTreeViewItem)item;
                    element.Q<Label>("column1").text = itemData.displayName;
                    element.Q<Label>("column2").text = FormattingUtility.BytesToString(itemData.totalAllocatedBytes);
                    element.Q<Label>("column3").text = FormattingUtility.BytesToString(itemData.totalUnusedBytes);
                };
                m_TreeView.onSelectionChange += OnTreeViewSelectionChanged;
                m_TreeView.selectionType = SelectionType.Single;
            }

            void CreateViewRightPane(VisualElement root)
            {
                m_RightPane = s_RightPaneTemplate.Clone(root);
                m_RightPane.visible = false;

                var header = m_RightPane.Q("header");
                m_ArchetypeName = header.Q<TextField>("name");

                var content = m_RightPane.Q("content");
                content.Q<Label>("entity-count-label").text = s_EntityCount;
                m_EntityCount = content.Q<Label>("entity-count-value");

                content.Q<Label>("unused-entity-count-label").text = s_UnusedEntities;
                m_UnusedEntityCount = content.Q<Label>("unused-entity-count-value");

                content.Q<Label>("chunk-count-label").text = s_ChunkCount;
                m_ChunkCount = content.Q<Label>("chunk-count-value");

                content.Q<Label>("chunk-capacity-label").text = s_ChunkCapacity;
                m_ChunkCapacity = content.Q<Label>("chunk-capacity-value");

                //content.Q<Label>("segment-count-label").text = s_Segments;
                //m_SegmentCount = content.Q<Label>("segment-count-value");

                m_ComponentsFoldout = content.Q<FoldoutField>("components");
                m_ComponentsFoldout.text = s_Components;
                m_ComponentsFoldout.open = true;

                m_ComponentsSizeInChunk = new Label();
                m_ComponentsSizeInChunk.style.unityTextAlign = TextAnchor.MiddleRight;
                m_ComponentsSizeInChunk.tooltip = s_ComponentsSizeInChunkTooltip;
                m_ComponentsFoldout.value = m_ComponentsSizeInChunk;

                m_ExternalComponents = content.Q<Label>("external-components");
                m_ExternalComponents.text = s_ExternalComponents;

                m_ChunkComponentsFoldout = content.Q<FoldoutField>("chunk-components");
                m_ChunkComponentsFoldout.text = s_ChunkComponents;
                m_ChunkComponentsFoldout.open = true;

                m_SharedComponentsFoldout = content.Q<FoldoutField>("shared-components");
                m_SharedComponentsFoldout.text = s_SharedComponents;
                m_SharedComponentsFoldout.open = true;
            }

            public void Rebuild()
            {
                if (m_Window == null)
                    return;

                var itemId = 0;
                var rootItem = new MemoryProfilerTreeViewItem() { id = itemId++, displayName = s_All };

                foreach (var worldName in m_ArchetypesDataFiltered.Select(x => x.WorldName).Distinct())
                    rootItem.AddChild(new MemoryProfilerTreeViewItem { id = itemId++, displayName = worldName });

                foreach (var archetypeData in m_ArchetypesDataFiltered)
                {
                    var worldItem = rootItem.children.First(item => item.displayName == archetypeData.WorldName);
                    var archetypeDataItem = worldItem.children.FirstOrDefault(x => x.data.StableHash == archetypeData.StableHash);
                    if (archetypeDataItem == null)
                    {
                        archetypeDataItem = new MemoryProfilerTreeViewItem
                        {
                            id = itemId++,
                            displayName = $"Archetype {FormattingUtility.HashToString(archetypeData.StableHash)}",
                            data = archetypeData
                        };
                        worldItem.AddChild(archetypeDataItem);
                    }

                    archetypeDataItem.totalAllocatedBytes += archetypeData.AllocatedBytes;
                    archetypeDataItem.totalUnusedBytes += archetypeData.UnusedBytes;
                    worldItem.totalAllocatedBytes += archetypeData.AllocatedBytes;
                    worldItem.totalUnusedBytes += archetypeData.UnusedBytes;
                    rootItem.totalAllocatedBytes += archetypeData.AllocatedBytes;
                    rootItem.totalUnusedBytes += archetypeData.UnusedBytes;
                }

                AddLeafCountRecursive(rootItem);

                rootItem.SortChildrenRecursive(item => item.totalAllocatedBytes, false);
                if (rootItem.hasChildren)
                {
                    m_TreeView.rootItems = new[] { rootItem };
                    m_TreeView.ExpandItem(rootItem.id);
                }
                else
                {
                    m_TreeView.rootItems = Array.Empty<MemoryProfilerTreeViewItem>();
                }

                m_Message.SetVisibility(false);
                m_Content.SetVisibility(true);
                SetInspectorValue(m_TreeView.selectedItem as MemoryProfilerTreeViewItem);
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

                m_ArchetypesDataSource = null;
                m_ArchetypesDataFiltered.Clear();
                m_TreeView.rootItems = Array.Empty<MemoryProfilerTreeViewItem>();
                m_Message.SetVisibility(true);
                m_Message.text = message;
                m_Content.SetVisibility(false);
                SetInspectorValue(null);
            }

            void OnTreeViewSelectionChanged(IEnumerable<ITreeViewItem> items)
            {
                SetInspectorValue(items.FirstOrDefault() as MemoryProfilerTreeViewItem);
            }

            void SetInspectorValue(MemoryProfilerTreeViewItem item)
            {
                if (item != null && item.data.ComponentTypes != null)
                {
                    m_ArchetypeName.value = item.displayName;
                    m_EntityCount.text = FormattingUtility.CountToString(item.data.EntityCount);
                    m_UnusedEntityCount.text = FormattingUtility.CountToString(item.data.UnusedEntityCount);
                    m_ChunkCount.text = FormattingUtility.CountToString(item.data.ChunkCount);
                    m_ChunkCapacity.text = FormattingUtility.CountToString(item.data.ChunkCapacity);
                    //m_SegmentCount.text = CountToString(item.data.SegmentCount);
                    m_ComponentsFoldout.Clear();
                    m_ExternalComponents.SetVisibility(false);
                    m_ChunkComponentsFoldout.Clear();
                    m_ChunkComponentsFoldout.SetVisibility(false);
                    m_SharedComponentsFoldout.Clear();
                    m_SharedComponentsFoldout.SetVisibility(false);

                    foreach (var typeIndex in item.data.ComponentTypes.OrderByDescending(GetTypeSizeInChunk))
                    {
                        var component = s_ComponentTemplate.Clone();
                        var componentIcon = component.Q<Image>("icon");
                        var componentName = component.Q<Label>("name");
                        var componentSizeInChunk = component.Q<Label>("size");

                        componentSizeInChunk.tooltip = s_ComponentSizeInChunkTooltip;

                        if (typeIndex <= 0)
                        {
                            componentIcon.AddToClassList("memory-profiler-component__icon-component");
                            componentName.text = s_Unknown;
                            componentSizeInChunk.text = "? B";
                        }
                        else
                        {
                            if (TypeManager.IsChunkComponent(typeIndex))
                                componentIcon.AddToClassList("memory-profiler-component__icon-chunk-component");
                            else if (TypeManager.IsBuffer(typeIndex))
                                componentIcon.AddToClassList("memory-profiler-component__icon-buffer-component");
                            else if (TypeManager.IsSharedComponentType(typeIndex))
                                componentIcon.AddToClassList("memory-profiler-component__icon-shared-component");
                            else if (TypeManager.IsManagedComponent(typeIndex))
                                componentIcon.AddToClassList("memory-profiler-component__icon-managed-component");
                            else if (TypeManager.IsZeroSized(typeIndex))
                                componentIcon.AddToClassList("memory-profiler-component__icon-tag-component");
                            else
                                componentIcon.AddToClassList("memory-profiler-component__icon-component");

                            var type = TypeManager.GetType(typeIndex);
                            componentName.text = type?.Name ?? s_Unknown;

                            // Chunk and shared components store data outside archetype
                            if (!TypeManager.IsChunkComponent(typeIndex) &&
                                !TypeManager.IsSharedComponentType(typeIndex))
                            {
                                var typeInfo = TypeManager.GetTypeInfo(typeIndex);
                                componentSizeInChunk.text = FormattingUtility.BytesToString((ulong)typeInfo.SizeInChunk);
                            }
                        }

                        if (TypeManager.IsChunkComponent(typeIndex))
                            m_ChunkComponentsFoldout.Add(component);
                        else if (TypeManager.IsSharedComponentType(typeIndex))
                            m_SharedComponentsFoldout.Add(component);
                        else
                            m_ComponentsFoldout.Add(component);
                    }

                    m_ComponentsFoldout.text = $"{s_Components} ({FormattingUtility.CountToString(m_ComponentsFoldout.childCount)})";
                    m_ComponentsSizeInChunk.text = FormattingUtility.BytesToString((ulong)item.data.InstanceSize);

                    m_ExternalComponents.SetVisibility(m_ChunkComponentsFoldout.childCount > 0 || m_SharedComponentsFoldout.childCount > 0);

                    if (m_ChunkComponentsFoldout.childCount > 0)
                    {
                        m_ChunkComponentsFoldout.text = $"{s_ChunkComponents} ({FormattingUtility.CountToString(m_ChunkComponentsFoldout.childCount)})";
                        m_ChunkComponentsFoldout.SetVisibility(true);
                    }

                    if (m_SharedComponentsFoldout.childCount > 0)
                    {
                        m_SharedComponentsFoldout.text = $"{s_SharedComponents} ({FormattingUtility.CountToString(m_SharedComponentsFoldout.childCount)})";
                        m_SharedComponentsFoldout.SetVisibility(true);
                    }

                    m_RightPane.visible = true;
                }
                else
                {
                    m_RightPane.visible = false;
                    m_ArchetypeName.value = null;
                    m_EntityCount.text = null;
                    m_UnusedEntityCount.text = null;
                    m_ChunkCount.text = null;
                    m_ChunkCapacity.text = null;
                    //m_SegmentCount.text = null;
                    m_ComponentsFoldout.Clear();
                    m_ChunkComponentsFoldout.Clear();
                    m_SharedComponentsFoldout.Clear();
                }
            }

            int AddLeafCountRecursive(MemoryProfilerTreeViewItem item)
            {
                var count = item.hasChildren ? 0 : 1;
                foreach (var child in item.children)
                    count += AddLeafCountRecursive(child);
                if (item.hasChildren)
                    item.displayName += $" ({count})";
                return count;
            }

            internal static int GetTypeSizeInChunk(TypeIndex typeIndex)
            {
                return typeIndex != TypeIndex.Null ? TypeManager.GetTypeInfo(typeIndex).SizeInChunk : 0;
            }
        }
    }
}
