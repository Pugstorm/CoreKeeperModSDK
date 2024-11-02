#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using Unity.Editor.Bridge;
using Unity.Entities.UI;
using UnityEngine.UIElements;
using System.Text.RegularExpressions;

namespace Unity.Entities.Editor
{
    [ExcludeFromPreset]
    internal class JournalEntry : SearchItemWrapper<EntitiesJournaling.RecordView>
    {
        private void OnEnable()
        {
            hideFlags = hideFlags & HideFlags.NotEditable;
        }
    }

    [ExcludeFromPreset]
    internal class JournalManager : ScriptableObject
    {
        private void OnEnable()
        {
            hideFlags = hideFlags & HideFlags.NotEditable;
        }
    }

    [CustomEditor(typeof(JournalManager))]
    internal class JournalManagerWrapperEditor : UnityEditor.Editor
    {

        private void OnEnable()
        {
        }

        public override void OnInspectorGUI()
        {
            // TODO Search: for 2022, Inspector in SearchWindow has to be done with IMGUI

            EditorGUILayout.LabelField("Record Count", FormattingUtility.CountToString(EntitiesJournaling.RecordCount));
            EditorGUILayout.LabelField("Record Index", EntitiesJournaling.RecordIndex.ToString());
            EditorGUILayout.LabelField("Used Bytes", FormattingUtility.BytesToString(EntitiesJournaling.UsedBytes));
            EditorGUILayout.LabelField("Allocated Bytes", FormattingUtility.BytesToString(EntitiesJournaling.AllocatedBytes));
        }
    }

    [CustomEditor(typeof(JournalEntry))]
    internal class JournalingWrapperEditor : UnityEditor.Editor
    {
        bool m_EntityFoldout;
        bool m_ComponentsFoldout;
        private void OnEnable()
        {
            m_ComponentsFoldout = m_EntityFoldout = true;
        }

        public override void OnInspectorGUI()
        {
            // TODO Search: for 2022, Inspector in SearchWindow has to be done with IMGUI

            var wrapper = (JournalEntry)target;
            var item = wrapper.item;
            var desc = wrapper.objItem;
            var context = item.context;

            var oldWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 125;
            EditorGUILayout.TextField("Record Index", desc.Index.ToString());
            EditorGUILayout.TextField("Frame", desc.FrameIndex.ToString());
            EditorGUILayout.TextField("World", desc.World.Name);

            DrawSearchableValue(context, null, "Executing System", desc.ExecutingSystem.Name,
                () => AddToQuery(context, "es", "=", desc.ExecutingSystem.Name),
                () => JournalSearchProvider.SelectSystem(desc.World.Reference, desc.ExecutingSystem.Handle));
            DrawSearchableValue(context, null, "Origin System", desc.OriginSystem.Name,
                () => AddToQuery(context, "os", "=", desc.OriginSystem.Name),
                () => JournalSearchProvider.SelectSystem(desc.World.Reference, desc.OriginSystem.Handle));

            DrawEntities(context, ref m_EntityFoldout, desc);
            DrawComponents(context, ref m_ComponentsFoldout, desc);

            EditorGUIUtility.labelWidth = oldWidth;
        }

        static void DrawEntities(SearchContext context, ref bool foldout, EntitiesJournaling.RecordView record)
        {
            if (record.Entities.Length == 0)
                return;

            foldout = EditorGUILayout.Foldout(foldout, $"Entities ({record.Entities.Length})");
            if (foldout)
            {
                EditorGUI.indentLevel++;
                for(var i = 0; i < record.Entities.Length; ++i)
                {
                    var entityView = record.Entities[i];
                    DrawSearchableValue(context, SearchUtils.entityIcon, null, entityView.Name,
                        () => AddToQuery(context, "ei", "=", entityView.Index),
                        () => SelectEntity(record, entityView.Reference));
                }
                EditorGUI.indentLevel--;
            }
        }

        static void DrawComponents(SearchContext context, ref bool foldout, EntitiesJournaling.RecordView record)
        {
            if (record.ComponentTypes.Length == 0)
                return;

            foldout = EditorGUILayout.Foldout(foldout, $"Components ({record.ComponentTypes.Length})");
            if (foldout)
            {
                EditorGUI.indentLevel++;
                for (var i = 0; i < record.ComponentTypes.Length; ++i)
                {
                    var componentView = record.ComponentTypes[i];
                    DrawSearchableValue(context, SearchUtils.GetComponentIcon(componentView.TypeIndex), null, componentView.Name,
                        () => AddToQuery(context, "ci", "=", componentView.TypeIndex.Value),
                        () => SelectComponent(componentView.TypeIndex));
                }
                EditorGUI.indentLevel--;
            }
        }

        static void DrawSearchableValue(SearchContext context, Texture2D icon, string label, string value, Action searchAction, Action selectAction)
        {
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(value)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (label == null)
                    {
                        EditorGUILayout.LabelField(new GUIContent(value, icon));
                    }
                    else
                    {
                        EditorGUILayout.TextField(new GUIContent(label, icon), value);
                    }

                    if (GUILayout.Button(SearchUtils.search, SearchUtils.Styles.iconButton, GUILayout.Width(16)))
                    {
                        searchAction();
                    }
                    if (GUILayout.Button(SearchUtils.gotoIcon, SearchUtils.Styles.iconButton, GUILayout.Width(16)))
                    {
                        selectAction();
                    }
                }
            }
        }

        static void AddToQuery(SearchContext context, string token, string op, object value)
        {
            if (context == null || context.searchView == null)
                return;

            var newQuery = SearchUtils.AddOrReplaceFilterInQuery(context.searchText, token, op, value);
            context.searchView.SetSearchText(newQuery);
        }

        static void SelectComponent(TypeIndex index)
        {
            var type = TypeManager.GetType(index);
            if (type == null)
                return;
            var undoGroup = Undo.GetCurrentGroup();
            ContentUtilities.ShowComponentInspectorContent(type);
            Undo.CollapseUndoOperations(undoGroup);
        }

        static void SelectEntity(EntitiesJournaling.RecordView record, Entity entity)
        {
            if (record == EntitiesJournaling.RecordView.Null)
                return;

            var world = record.World.Reference;
            if (world == null)
                return;

            if (entity == Entity.Null)
                return;

            var undoGroup = Undo.GetCurrentGroup();
            EntitySelectionProxy.SelectEntity(world, entity);
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    static class JournalSearchProvider
    {
        public const string type = "journal";

        internal static QueryEngine<EntitiesJournaling.RecordView> s_QueryEngine;
        internal static QueryEngine<EntitiesJournaling.RecordView> queryEngine
        {
            get
            {
                if (s_QueryEngine == null)
                    SetupQueryEngine();
                return s_QueryEngine;
            }
        }

        static EntitiesJournalingWindow.ReadOnlyRecordViewList s_Records;
        static EntitiesJournalingWindow.ComponentDataValuesVisitor s_ComponentDataValuesVisitor;
        internal static WorldProxyManager s_WorldProxyManager;

        static bool IsJournalManager(IReadOnlyCollection<SearchItem> items)
        {
            return items.Count == 1 && items.First().data == null;
        }

        static void ClearResultsAndRefresh(ISearchView searchView = null)
        {
            s_Records = null;
            if (searchView == null)
            {
                SearchBridge.RefreshWindowsWithProvider(type);
            }
            else
            {
                searchView.Refresh(RefreshFlags.ItemsChanged);
            }
        }

        internal static void SelectSystem(World world, SystemHandle system)
        {
            if (world == null || system == default)
                return;

            var worldProxy = s_WorldProxyManager.GetWorldProxyForGivenWorld(world);
            var systemProxy = new SystemProxy(system, world, worldProxy);
            ContentUtilities.ShowSystemInspectorContent(systemProxy);
        }

        [SearchActionsProvider]
        internal static IEnumerable<SearchAction> ActionHandlers()
        {
            return new SearchAction[]
            {
                new SearchAction(type, "Record", null, "Start Recording")
                {
                    execute = (items) =>
                    {
                        EntitiesJournaling.Enabled = true;
                    },
                    enabled = items => !EntitiesJournaling.Enabled && IsJournalManager(items),
                    closeWindowAfterExecution = false
                },

                new SearchAction(type, "Stop Recording", null, "Stop Recording")
                {
                    execute = (items) =>
                    {
                        EntitiesJournaling.Enabled = false;
                    },
                    enabled = items => EntitiesJournaling.Enabled && IsJournalManager(items),
                    closeWindowAfterExecution = false
                },

                new SearchAction(type, "Clear", null, "Clear Recording")
                {
                    execute = (items) =>
                    {
                        EntitiesJournaling.Clear();
                    },
                    enabled = items => !EntitiesJournaling.Enabled && IsJournalManager(items),
                    closeWindowAfterExecution = false
                },

                new SearchAction(type, "Export...", null, "Save Recording")
                {
                    execute = (items) =>
                    {
                        EntitiesJournalingUtilities.ExportToCSV();
                    },
                    enabled = items => !EntitiesJournaling.Enabled && IsJournalManager(items),
                    closeWindowAfterExecution = false
                },
            };
        }

        static string[] GetComponentDataText(EntitiesJournaling.RecordView record)
        {
            return EntitiesJournaling.TryGetRecordDataAsComponentDataArrayBoxed(record, out var componentDataArray) ? GetComponentDataValues(componentDataArray) : Array.Empty<string>();
        }

        static string[] GetComponentDataValues(object componentDataArray)
        {
            PropertyContainer.Accept(s_ComponentDataValuesVisitor, componentDataArray);
            return s_ComponentDataValuesVisitor.GetValues();
        }

        [SearchItemProvider]
        internal static SearchProvider CreateProvider()
        {
            var p = new SearchProvider(type, "Journaling")
            {
                type = type,
                filterId = "comp:",
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
                    if (item.data == null)
                    {
                        var mgr = JournalManager.CreateInstance<JournalManager>();
                        mgr.name = "Journal";
                        return mgr;
                    }

                    var wrapper = JournalEntry.CreateInstance<JournalEntry>();
                    wrapper.item = item;
                    wrapper.name = item.label;
                    if (item.data != null)
                        wrapper.objItem = (EntitiesJournaling.RecordView)item.data;
                    return wrapper;
                }
            };
            SearchBridge.SetTableConfig(p, GetDefaultTableConfig);
            return p;
        }

        [SearchColumnProvider(nameof(EntitiesJournaling.RecordView))]
        internal static void JournalingColumnProvider(SearchColumn column)
        {
            column.getter = args =>
            {
                if (s_Records != null && args.item.data is EntitiesJournaling.RecordView data)
                {
                    switch (column.selector)
                    {
                        case "record_index": return EntitiesJournalingWindow.GetRecordIndexText(data);
                        case "frame_index": return EntitiesJournalingWindow.GetFrameIndexText(data);
                        case "world": return EntitiesJournalingWindow.GetWorldName(data);
                        case "summary": return args.item.description;
                        case "executing_system": return EntitiesJournalingWindow.GetExecutingSystemName(data);
                        case "originating_system": return EntitiesJournalingWindow.GetOriginSystemName(data);
                        case "entities": return EntitiesJournalingWindow.GetEntitiesText(data);
                        case "components": return EntitiesJournalingWindow.GetComponentTypesText(data);
                        case "entities_count": return data.Entities.Length;
                        case "components_count": return data.ComponentTypes.Length;
                        case "components_data": return string.Join(",", GetComponentDataText(data));
                        case "record_data_system_name": return EntitiesJournalingWindow.GetRecordDataSystemName(data);
                    }
                }

                return null;
            };
        }

        static IEnumerable<string> GetWords(EntitiesJournaling.RecordView data)
        {
            yield return EntitiesJournalingWindow.GetRecordTypeText(data);
            yield return EntitiesJournalingWindow.GetSummaryText(data);
        }

        static void SetupQueryEngine()
        {
            s_QueryEngine = new();
            s_QueryEngine.SetSearchDataCallback(GetWords);
            s_QueryEngine.AddOperatorHandler<IEnumerable<string>, string>(":", (lhs, rhs, options) => lhs.Any(element => element.Contains(rhs)));
            s_QueryEngine.AddOperatorHandler<IEnumerable<string>, string>("=", (lhs, rhs, options) => lhs.Any(element => string.Compare(element, rhs, true) == 0));
            s_QueryEngine.AddOperatorHandler<IEnumerable<int>, int>("=", (lhs, rhs, options) => lhs.Any(element => element == rhs));

            SearchBridge.SetFilter(s_QueryEngine, "ri", data => data.Index)
                .AddOrUpdateProposition(category: null, label: "Record Index", replacement: "ri=42", help: "Search Entry by Index");
            SearchBridge.SetFilter(s_QueryEngine, "f", data => data.FrameIndex)
                .AddOrUpdateProposition(category: null, label: "Frame Index", replacement: "f>30", help: "Search Entry by Frame Index");
            SearchBridge.SetFilter(s_QueryEngine, "wi", data => data.World.SequenceNumber)
                .AddOrUpdateProposition(category: null, label: "World Index", replacement: "wi=0", help: "Search Entry by World Index");

            SearchBridge.SetFilter(s_QueryEngine, "e", data => data.Entities.Select(e => e.Name))
                .AddOrUpdateProposition(category: null, label: "Entity Name", replacement: "e:Entity", help: "Search Entry by Entity name");
            SearchBridge.SetFilter(s_QueryEngine, "ei", data => data.Entities.Select(e => e.Index))
                .AddOrUpdateProposition(category: null, label: "Entity Index", replacement: "ei=0", help: "Search Entry by Entity Index");
            SearchBridge.SetFilter(s_QueryEngine, "ec", data => data.Entities.Length)
                .AddOrUpdateProposition(category: null, label: "Entity Count", replacement: "ec>2", help: "Search Entry by Entity Count");

            SearchBridge.SetFilter(s_QueryEngine, "v", data => data.Entities.Length)
                .AddOrUpdateProposition(category: null, label: "Component Data", replacement: "v=this", help: "Search Entry by Component Data");

            SearchBridge.SetFilter(s_QueryEngine, "ci", data => data.ComponentTypes.Select(e => e.TypeIndex.Value))
                .AddOrUpdateProposition(category: null, label: "Component Index", replacement: "ci=0", help: "Search Entry by Component Index");
            SearchBridge.SetFilter(s_QueryEngine, "cc", data => data.ComponentTypes.Length)
                .AddOrUpdateProposition(category: null, label: "Component Count", replacement: "cc>2", help: "Search Entry by Component Count");

            // ListBlocks
            SearchBridge.SetFilter(s_QueryEngine, "es", data => EntitiesJournalingWindow.GetExecutingSystemName(data));
            SearchBridge.SetFilter(s_QueryEngine, "os", data => EntitiesJournalingWindow.GetOriginSystemName(data));
            SearchBridge.SetFilter(s_QueryEngine, "s", data => new[] { EntitiesJournalingWindow.GetExecutingSystemName(data), EntitiesJournalingWindow.GetOriginSystemName(data) });
            SearchBridge.SetFilter(s_QueryEngine, "c", data => data.ComponentTypes.Select(e => e.Name));
            SearchBridge.SetFilter(s_QueryEngine, "w", data => EntitiesJournalingWindow.GetWorldName(data));
            SearchBridge.SetFilter(s_QueryEngine, "rt", data => data.RecordType.ToString());
        }

        static void OnEnable()
        {
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            EntitiesJournaling.s_JournalingOperationExecuted -= JournalingOperationExecuted;
            EntitiesJournaling.s_JournalingOperationExecuted += JournalingOperationExecuted;
            s_Records = null;
            s_ComponentDataValuesVisitor = new();
            s_WorldProxyManager = new();
            s_WorldProxyManager.CreateWorldProxiesForAllWorlds();
        }

        static void OnDisable()
        {
            EntitiesJournaling.s_JournalingOperationExecuted -= JournalingOperationExecuted;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            s_Records = null;
            s_ComponentDataValuesVisitor = null;
            s_WorldProxyManager.Dispose();
            s_WorldProxyManager = null;
        }

        static void JournalingOperationExecuted(EntitiesJournaling.JournalingOperationType type)
        {
            switch(type)
            {
                case EntitiesJournaling.JournalingOperationType.StartRecording:
                    ClearResultsAndRefresh();
                    break;
                case EntitiesJournaling.JournalingOperationType.StopRecording:
                    ClearResultsAndRefresh();
                    break;
                case EntitiesJournaling.JournalingOperationType.ClearResults:
                    ClearResultsAndRefresh();
                    break;
            }
        }

        static void PlayModeChanged(PlayModeStateChange pmc)
        {
            if (pmc == PlayModeStateChange.ExitingEditMode || pmc == PlayModeStateChange.ExitingPlayMode)
            {
                s_WorldProxyManager.CreateWorldProxiesForAllWorlds();
                ClearResultsAndRefresh();
            }
        }

        static SearchTable GetDefaultTableConfig(SearchContext context)
        {
            return new SearchTable(type, FetchColumns(null, null));
        }

        static IEnumerable<SearchItem> FetchItems(SearchContext context, SearchProvider provider)
        {
            // Yield fake journal item that will always be on top of results due to priority.
            yield return provider.CreateItem(context, "JournalManager", int.MinValue, "Journal Controller", "Allow start/stop of recording session", null, null);

            if (EntitiesJournaling.RecordCount == 0 || EntitiesJournaling.Enabled)
            {
                yield break;
            }

            if (s_Records == null || s_Records.Count != EntitiesJournaling.RecordCount)
            {
                s_Records = new EntitiesJournalingWindow.ReadOnlyRecordViewList(EntitiesJournaling.GetRecords(EntitiesJournaling.Ordering.Descending));
                if (EntitiesJournaling.Preferences.PostProcess)
                    s_Records.RunPostProcess();
            }

            var searchQuery = context.searchQuery;
            ParsedQuery<EntitiesJournaling.RecordView> query = null;
            if (!string.IsNullOrEmpty(searchQuery))
            {
                query = queryEngine.ParseQuery(context.searchQuery);
                if (!query.valid)
                {
                    Debug.LogError(string.Join(" ", query.errors.Select(e => e.reason)));
                    yield break;
                }
            }

            var results = s_Records as IEnumerable<EntitiesJournaling.RecordView>;
            if (query != null)
            {
                results = query.Apply(results);
            }

            var score = 0;
            foreach (var data in results)
            {
                yield return provider.CreateItem(context, data.GetHashCode().ToString(), score++, EntitiesJournalingWindow.GetRecordTypeText(data), EntitiesJournalingWindow.GetSummaryText(data), SearchUtils.componentIcon, data);
            }
        }

        static IEnumerable<SearchProposition> FetchPropositions(SearchContext context, SearchPropositionOptions options)
        {
            if (!options.flags.HasAny(SearchPropositionFlags.QueryBuilder))
                yield break;

            foreach (var p in SearchBridge.GetAndOrQueryBlockPropositions())
                yield return p;

            foreach (var p in SearchBridge.GetPropositions(queryEngine))
                yield return p;

            foreach (var l in SearchBridge.GetPropositionsFromListBlockType(typeof(QueryWorldBlock)))
                yield return l;

            foreach (var l in SearchBridge.GetPropositionsFromListBlockType(typeof(QueryRecordTypeBlock)))
                yield return l;

            foreach (var l in SearchBridge.GetPropositionsFromListBlockType(typeof(QueryComponentTypeBlock)))
                yield return l;

            foreach (var l in SearchBridge.GetPropositionsFromListBlockType(typeof(QuerySystemBlock)))
                yield return l;

            foreach (var l in SearchBridge.GetPropositionsFromListBlockType(typeof(QueryOriginSystemBlock)))
                yield return l;

            foreach (var l in SearchBridge.GetPropositionsFromListBlockType(typeof(QueryExecutingSystemBlock)))
                yield return l;
        }

        static IEnumerable<SearchColumn> FetchColumns(SearchContext context, IEnumerable<SearchItem> items)
        {
            yield return new SearchColumn("Journaling/Record Index", "record_index", nameof(EntitiesJournaling.RecordView));
            if (context == null)
            {
                // Default column Mode:
                yield return new SearchColumn("Name", "label");
                yield return new SearchColumn("Journaling/Summary", "summary", nameof(EntitiesJournaling.RecordView));
            }
            else
            {
                yield return new SearchColumn("Journaling/Frame Index", "frame_index", nameof(EntitiesJournaling.RecordView));
                yield return new SearchColumn("Journaling/Summary", "summary", nameof(EntitiesJournaling.RecordView));
                yield return new SearchColumn("Journaling/World", "world", nameof(EntitiesJournaling.RecordView));
                yield return new SearchColumn("Journaling/Executing System", "executing_system", nameof(EntitiesJournaling.RecordView));
                yield return new SearchColumn("Journaling/Originating System", "originating_system", nameof(EntitiesJournaling.RecordView));
                yield return new SearchColumn("Journaling/Entities", "entities", nameof(EntitiesJournaling.RecordView));
                yield return new SearchColumn("Journaling/Entities Count", "entities_count", nameof(EntitiesJournaling.RecordView));
                yield return new SearchColumn("Journaling/Components", "components", nameof(EntitiesJournaling.RecordView));
                yield return new SearchColumn("Journaling/Components Count", "components_count", nameof(EntitiesJournaling.RecordView));
                yield return new SearchColumn("Journaling/Components Data", "components_data", nameof(EntitiesJournaling.RecordView));
                yield return new SearchColumn("Journaling/Record Data System Name", "record_data_system_name", nameof(EntitiesJournaling.RecordView));
            }
        }

        [MenuItem("Window/Search/Journal", priority = 1371)]
        static void OpenProviderMenu()
        {
            OpenProvider();
        }

        [UnityEditor.ShortcutManagement.Shortcut("Help/Search/Journal")]
        static void OpenShortcut()
        {
            OpenProvider();
        }

        internal static void OpenProvider(string query = null)
        {
            SearchBridge.OpenContextualTable(type, query ?? "", GetDefaultTableConfig(null), viewState => viewState.flags |= UnityEngine.Search.SearchViewFlags.OpenInspectorPreview);
        }
    }

}
#endif
