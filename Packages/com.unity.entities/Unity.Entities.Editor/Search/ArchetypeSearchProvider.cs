using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using static Unity.Entities.Editor.MemoryProfilerModule;
using static Unity.Entities.MemoryProfiler;
using Unity.Editor.Bridge;

namespace Unity.Entities.Editor
{
    [ExcludeFromPreset]
    internal class ArchetypeInfo : SearchItemWrapper<MemoryProfilerTreeViewItemData>
    {
        private void OnEnable()
        {
            hideFlags = hideFlags & HideFlags.NotEditable;
            name = "Archetype";
        }
    }

    [CustomEditor(typeof(ArchetypeInfo))]
    internal class ArchetypeWrapperEditor : UnityEditor.Editor
    {
        bool m_ComponentFoldout;
        List<TypeIndex> m_ComponentTypes;

        bool m_ChunkComponentFoldout;
        List<TypeIndex> m_ChunkComponentTypes;

        bool m_SharedComponentFoldout;
        List<TypeIndex> m_SharedComponentTypes;

        private void OnEnable()
        {
            m_ComponentFoldout = true;
            m_ChunkComponentFoldout = true;
            m_SharedComponentFoldout = true;
        }

        private void SetupTypeLists(MemoryProfilerTreeViewItemData arch)
        {
            if (m_ComponentTypes != null)
                return;

            m_ComponentTypes = new();
            m_ChunkComponentTypes = new();
            m_SharedComponentTypes = new();
            foreach (var typeIndex in arch.ComponentTypes.OrderByDescending(MemoryProfilerModuleView.GetTypeSizeInChunk))
            {
                if (TypeManager.IsChunkComponent(typeIndex))
                    m_ChunkComponentTypes.Add(typeIndex);
                else if (TypeManager.IsSharedComponentType(typeIndex))
                    m_SharedComponentTypes.Add(typeIndex);
                else
                    m_ComponentTypes.Add(typeIndex);
            }
        }

        public override void OnInspectorGUI()
        {
            // TODO Search: for 2022, Inspector in SearchWindow has to be done with IMGUI

            var wrapper = (ArchetypeInfo)target;
            var item = wrapper.item;
            var arch = wrapper.objItem;

            SetupTypeLists(arch);

            var oldWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 250;

            EditorGUILayout.TextField(item.label);
            EditorGUILayout.LabelField($"World:", arch.WorldName);
            EditorGUILayout.LabelField($"Allocated KB:", FormattingUtility.BytesToString(arch.AllocatedBytes));
            EditorGUILayout.LabelField($"Unused KB:", FormattingUtility.BytesToString(arch.UnusedBytes));
            EditorGUILayout.LabelField($"Chunk Capacity:", arch.ChunkCapacity.ToString());
            EditorGUILayout.LabelField($"Unused Entities:", arch.UnusedEntityCount.ToString());
            EditorGUILayout.LabelField($"Chunk Capacity:", arch.ChunkCapacity.ToString());

            DoFoldout("Components", ref m_ComponentFoldout, m_ComponentTypes);
            DoFoldout("Chunk Components", ref m_ChunkComponentFoldout, m_ChunkComponentTypes);
            DoFoldout("Shared Components", ref m_SharedComponentFoldout, m_SharedComponentTypes);

            EditorGUIUtility.labelWidth = oldWidth;
        }

        static void DoFoldout(string title, ref bool foldout, List<TypeIndex> types)
        {
            if (types.Count == 0)
                return;

            foldout = EditorGUILayout.Foldout(foldout, $"{title} ({types.Count})");
            if (foldout)
            {
                EditorGUI.indentLevel++;
                
                foreach (var typeIndex in types)
                {
                    var type = TypeManager.GetType(typeIndex);
                    var name = type?.Name ?? "Unknown";
                    var icon = SearchUtils.GetComponentIcon(typeIndex);
                    var content = new GUIContent(name, icon);
                    if (!TypeManager.IsChunkComponent(typeIndex) && !TypeManager.IsSharedComponentType(typeIndex))
                    {
                        var typeInfo = TypeManager.GetTypeInfo(typeIndex);
                        var bytes = FormattingUtility.BytesToString((ulong)typeInfo.SizeInChunk);
                        EditorGUILayout.LabelField(content, new GUIContent(bytes));
                    }
                    else
                    {
                        EditorGUILayout.LabelField(content);
                    }
                }

                EditorGUI.indentLevel--;
            }
        }
    }

    /// <summary>
    /// This class allows the SearchWindow to search for ECS Archetype.
    /// </summary>
    public static class ArchetypeSearchProvider
    {
        /// <summary>
        /// Search Provider type id. 
        /// </summary>
        public const string type = "archetype";

        static ArchetypesWindow.ArchetypesMemoryDataRecorder m_Recorder;
        static NativeList<ulong> m_ArchetypesStableHash;
        static NativeList<ArchetypeMemoryData> m_ArchetypesMemoryData;
        static MemoryProfilerTreeViewItemData[] m_ArchetypesDataSource;

        internal static QueryEngine<MemoryProfilerModule.MemoryProfilerTreeViewItemData> s_QueryEngine;
        internal static QueryEngine<MemoryProfilerModule.MemoryProfilerTreeViewItemData> queryEngine
        {
            get
            {
                if (s_QueryEngine == null)
                    SetupQueryEngine();
                return s_QueryEngine;
            }
        }

        [SearchActionsProvider]
        internal static IEnumerable<SearchAction> ActionHandlers()
        {
            yield break;
        }

        [SearchItemProvider]
        internal static SearchProvider CreateProvider()
        {
            var p = new SearchProvider(type, "Archetypes")
            {
                type = type,
                filterId = "arch:",
                onEnable = OnEnable,
                onDisable = OnDisable,
                fetchColumns = FetchColumns,
                isExplicitProvider = true,
                active = true,
                priority = 2500,
                fetchThumbnail = SearchUtils.DefaultFetchThumbnail,
                fetchLabel = SearchUtils.DefaultFetchLabel,
                fetchDescription = SearchUtils.DefaultFetchDescription,
                fetchItems = (context, items, provider) => FetchItems(context, provider),
                fetchPropositions = (context, options) => FetchPropositions(context, options),
                showDetails = true,
                showDetailsOptions = ShowDetailsOptions.Default | ShowDetailsOptions.Inspector,
                toObject = (item, type) =>
                {
                    var wrapper = ArchetypeInfo.CreateInstance<ArchetypeInfo>();
                    wrapper.name = item.id;
                    wrapper.item = item;
                    wrapper.objItem = (MemoryProfilerTreeViewItemData)item.data;
                    return wrapper;
                }
            };
            SearchBridge.SetTableConfig(p, GetDefaultTableConfig);
            return p;
        }

        [SearchColumnProvider(nameof(Archetype))]
        internal static void ArchetypeColumnProvider(SearchColumn column)
        {
            column.getter = args =>
            {
                if (args.item.data is MemoryProfilerTreeViewItemData data)
                {
                    switch (column.selector)
                    {
                        case "allocated": return FormattingUtility.BytesToString(data.AllocatedBytes);
                        case "unused": return FormattingUtility.BytesToString(data.UnusedBytes);
                        case "entities": return data.EntityCount;
                        case "unusedEntities": return data.UnusedEntityCount;
                        case "chunks": return data.ChunkCount;
                        case "capacity": return FormattingUtility.BytesToString((ulong)data.ChunkCapacity);
                        case "segments": return data.SegmentCount;
                        case "components": return data.ComponentTypes.Length;
                    }
                }

                return null;
            };
        }

        static IEnumerable<string> GetArchComponentTypes(MemoryProfilerTreeViewItemData data)
        {
            return data.ComponentTypes
                    .Select(t => TypeManager.GetType(t))
                    .Where(t => t != null)
                    .Select(t => t.Name);
        }

        static void SetupQueryEngine()
        {
            s_QueryEngine = new();
            s_QueryEngine.SetSearchDataCallback(data =>
            {
                return GetArchComponentTypes(data).Append(FormattingUtility.HashToString(data.StableHash));
            });
            SearchBridge.SetFilter(s_QueryEngine, "allocated", data => data.AllocatedBytes)
                .AddOrUpdateProposition(category: null, label: "Allocated", replacement: "allocated>1024", help: "Search archetypes by allocated bytes.");
            SearchBridge.SetFilter(s_QueryEngine, "unused", data => data.UnusedBytes)
                .AddOrUpdateProposition(category: null, label: "Unused", replacement: "unused>1024", help: "Search archetypes by unused bytes.");
            SearchBridge.SetFilter(s_QueryEngine, "entities", data => data.EntityCount)
                .AddOrUpdateProposition(category: null, label: "Entities Count", replacement: "entities>3", help: "Search archetypes by entity count.");
            SearchBridge.SetFilter(s_QueryEngine, "unusedEntities", data => data.UnusedEntityCount)
                .AddOrUpdateProposition(category: null, label: "Unused Entity Count", replacement: "unusedEntities>3", help: "Search archetypes by unused entity count.");
            SearchBridge.SetFilter(s_QueryEngine, "chunks", data => data.ChunkCount)
                .AddOrUpdateProposition(category: null, label: "Chunks Count", replacement: "chunks>3", help: "Search archetypes by chunks count.");
            SearchBridge.SetFilter(s_QueryEngine, "capacity", data => data.ChunkCapacity)
                .AddOrUpdateProposition(category: null, label: "Capacity", replacement: "capacity>3", help: "Search archetypes by capacity.");
            SearchBridge.SetFilter(s_QueryEngine, "segments", data => data.SegmentCount)
                .AddOrUpdateProposition(category: null, label: "Segments Count", replacement: "segments>3", help: "Search archetypes by segments count.");
            SearchBridge.SetFilter(s_QueryEngine, "components", data => data.ComponentTypes.Length)
                .AddOrUpdateProposition(category: null, label: "Components Count", replacement: "components>3", help: "Search archetypes by components count.");

            // ListBlock
            s_QueryEngine.AddFilter("w", data => data.WorldName);
            SearchBridge.AddFilter<string, MemoryProfilerTreeViewItemData>(s_QueryEngine, "c", OnTypeFilter, new[] { ":", "=" });
        }

        static void OnEnable()
        {
            m_Recorder = new ArchetypesWindow.ArchetypesMemoryDataRecorder();
            m_ArchetypesStableHash = new NativeList<ulong>(64, Allocator.Persistent);
            m_ArchetypesMemoryData = new NativeList<ArchetypeMemoryData>(64, Allocator.Persistent);
        }

        static void OnDisable()
        {
            m_ArchetypesMemoryData.Dispose();
            m_ArchetypesStableHash.Dispose();
            m_Recorder?.Dispose();
            m_Recorder = null;
        }

        static SearchTable GetDefaultTableConfig(SearchContext context)
        {
            return new SearchTable(type, new[] { new SearchColumn("Name", "label") }.Concat(FetchColumns(null, null)));
        }

        static IEnumerable<MemoryProfilerTreeViewItemData> GetTreeViewData()
        {
            var worldsData = m_Recorder.WorldsData.Distinct().ToDictionary(x => x.SequenceNumber, x => x);
            var archetypesData = m_Recorder.ArchetypesData.Distinct().ToDictionary(x => x.StableHash, x => x);
            foreach (var archetypeMemoryData in m_Recorder.ArchetypesMemoryData)
            {
                if (worldsData.TryGetValue(archetypeMemoryData.WorldSequenceNumber, out var worldData) &&
                    archetypesData.TryGetValue(archetypeMemoryData.StableHash, out var archetypeData))
                {
                    yield return new MemoryProfilerTreeViewItemData(worldData.Name, archetypeData, archetypeMemoryData);
                }
            }
        }

        static void TickArchetypeSource()
        {
            m_Recorder.Record();
            if (!ArchetypesWindow.MemCmp(m_ArchetypesStableHash.AsArray(), m_Recorder.ArchetypesStableHash) ||
                !ArchetypesWindow.MemCmp(m_ArchetypesMemoryData.AsArray(), m_Recorder.ArchetypesMemoryData))
            {
                m_ArchetypesDataSource = GetTreeViewData().ToArray();
                m_ArchetypesStableHash.CopyFrom(m_Recorder.ArchetypesStableHash);
                m_ArchetypesMemoryData.CopyFrom(m_Recorder.ArchetypesMemoryData);
            }

            if (m_ArchetypesDataSource == null)
                m_ArchetypesDataSource = GetTreeViewData().ToArray();
        }

        static IEnumerable<SearchItem> FetchItems(SearchContext context, SearchProvider provider)
        {
            var searchQuery = context.searchQuery;
            ParsedQuery<MemoryProfilerTreeViewItemData> query = null;
            if (!string.IsNullOrEmpty(searchQuery))
            {
                query = queryEngine.ParseQuery(context.searchQuery);
                if (!query.valid)
                {
                    Debug.LogError(string.Join(" ", query.errors.Select(e => e.reason)));
                    yield break;
                }
            }

            TickArchetypeSource();
            var results = m_ArchetypesDataSource?.Where(a => a.EntityCount > 0) ?? Enumerable.Empty<MemoryProfilerTreeViewItemData>();
            if (query != null)
            {
                results = query.Apply(results);
            }

            var archetypeIcon = PackageResources.LoadIcon("Archetype/Archetype.png");
            foreach (var arch in results)
            {
                var hash = FormattingUtility.HashToString(arch.StableHash);
                yield return provider.CreateItem(context, hash, arch.WorldName.GetHashCode(), $"Archetype {hash}", arch.WorldName, archetypeIcon, arch);
            }
        }

        static IEnumerable<SearchProposition> FetchPropositions(SearchContext context, SearchPropositionOptions options)
        {
            foreach(var p in SearchBridge.GetPropositions(queryEngine))
                yield return p;

            foreach (var l in SearchBridge.GetPropositionsFromListBlockType(typeof(QueryWorldBlock)))
                yield return l;

            foreach (var l in SearchBridge.GetPropositionsFromListBlockType(typeof(QueryComponentTypeBlock)))
                yield return l;
        }

        static IEnumerable<SearchColumn> FetchColumns(SearchContext context, IEnumerable<SearchItem> items)
        {            
            yield return new SearchColumn("Archetype/Allocated (KB)", "allocated", nameof(Archetype));
            yield return new SearchColumn("Archetype/Unused (KB)", "unused", nameof(Archetype));
            if (context == null)
                yield break;
            yield return new SearchColumn("Archetype/Entities", "entities", nameof(Archetype));
            yield return new SearchColumn("Archetype/UnusedEntities", "unusedEntities", nameof(Archetype));
            yield return new SearchColumn("Archetype/Chunks", "chunks", nameof(Archetype));
            yield return new SearchColumn("Archetype/Capacity", "capacity", nameof(Archetype));
            yield return new SearchColumn("Archetype/Segments", "segments", nameof(Archetype));
            yield return new SearchColumn("Archetype/Components", "components", nameof(Archetype));
        }

        static bool OnTypeFilter(MemoryProfilerTreeViewItemData arch, QueryFilterOperator op, string value)
        {
            var results = GetArchComponentTypes(arch);
            return SearchBridge.CompareWords(op, value, results);
        }

        [MenuItem("Window/Search/Archetypes", priority = 1371)]
        static void OpenProviderMenu()
        {
            OpenProvider();
        }

        [UnityEditor.ShortcutManagement.Shortcut("Help/Search/Archetypes")]
        static void OpenShortcut()
        {
            OpenProvider();
        }

        /// <summary>
        /// Open SearchWindow with ArchetypeSearchProvider enabled.
        /// </summary>
        /// <param name="query">Optional initial query.</param>
        public static void OpenProvider(string query = null)
        {
            SearchBridge.OpenContextualTable(type, query ?? "", GetDefaultTableConfig(null));
        }
    }

}
