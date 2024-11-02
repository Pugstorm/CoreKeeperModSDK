using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using Unity.Editor.Bridge;
using Unity.Entities.UI;
using Unity.Serialization.Editor;

namespace Unity.Entities.Editor
{
    internal class SystemDescriptor
    {
        public SystemProxy proxy;

        public string[] componentNamesInQueryCache;
        public string[] systemDependencyCache;

        public SystemDescriptor(SystemProxy proxy)
        {
            this.proxy = proxy;
            componentNamesInQueryCache = EntityQueryUtility.CollectComponentTypesFromSystemQuery(proxy).ToArray();
            systemDependencyCache = null;
        }

        public void UpdateDependencies(string[] dependencies)
        {
            systemDependencyCache = dependencies;
        }

        public string name => proxy.TypeName;

        public override string ToString()
        {
            return $"{name} Comp: {(componentNamesInQueryCache == null ? 0 : componentNamesInQueryCache.Length)} Dep: {(systemDependencyCache == null ? 0 : systemDependencyCache.Length)}";
        }
    }

    [ExcludeFromPreset]
    internal class SystemInfo : SearchItemWrapper<SystemDescriptor>
    {
        private void OnEnable()
        {
            hideFlags = hideFlags & HideFlags.NotEditable;
        }
    }

    [CustomEditor(typeof(SystemInfo))]
    internal class SystemWrapperEditor : UnityEditor.Editor
    {
        bool m_ComponentsFoldout;
        bool m_DependenciesFoldout;
        private void OnEnable()
        {
            m_ComponentsFoldout = true;
            m_DependenciesFoldout = true;
        }

        public override void OnInspectorGUI()
        {
            // TODO Search: for 2022, Inspector in SearchWindow has to be done with IMGUI

            var wrapper = (SystemInfo)target;
            var item = wrapper.item;
            var desc = wrapper.objItem;

            var oldWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 250;

            GUILayout.Label(desc.name);
            DoFoldout("Components", ref m_ComponentsFoldout, desc.componentNamesInQueryCache);
            DoFoldout("Dependencies", ref m_DependenciesFoldout, desc.systemDependencyCache);

            EditorGUIUtility.labelWidth = oldWidth;
        }

        static void DoFoldout(string title, ref bool foldout, string[] values)
        {
            if (values.Length == 0)
                return;

            foldout = EditorGUILayout.Foldout(foldout, $"{title} ({values.Length})");
            if (foldout)
            {
                EditorGUI.indentLevel++;

                foreach (var v in values)
                {
                    EditorGUILayout.LabelField(v);
                }

                EditorGUI.indentLevel--;
            }
        }
    }

    /// <summary>
    /// This class allows the SearchWindow to search for ECS System.
    /// </summary>
    public static class SystemSearchProvider
    {
        /// <summary>
        /// Search Provider type id. 
        /// </summary>
        public const string type = "system";

        internal static Dictionary<string, string[]> s_SystemDependencyMap = new Dictionary<string, string[]>();
        internal static World s_CurrentWorld;
        internal static WorldProxyManager s_WorldProxyManager;
        internal static WorldProxy s_WorldProxy;
        internal static bool s_MoreTimePrecision;
        
        internal static QueryEngine<SystemDescriptor> s_QueryEngine;
        internal static QueryEngine<SystemDescriptor> queryEngine
        {
            get
            {
                if (s_QueryEngine == null)
                    SetupQueryEngine();
                return s_QueryEngine;
            }
        }

        internal static List<SystemDescriptor> s_Systems;
        internal static IEnumerable<SystemDescriptor> systems
        {
            get
            {
                if (s_Systems == null)
                {
                    if (SetupSystemDescriptors())
                        GetAllSystems();
                }
                return s_Systems;
            }
        }

        static void SetWorld(string worldName)
        {
            Cleanup();
            s_CurrentWorld = SearchUtils.FindWorld(worldName);
        }

        static void SelectItem(SearchItem item, SearchContext ctx)
        {
            var data = (SystemDescriptor)item.data;
            SelectionUtility.ShowInInspector(new SystemContentProvider
            {
                World = data.proxy.World,
                SystemProxy = data.proxy
            }, new InspectorContentParameters
            {
                UseDefaultMargins = false,
                ApplyInspectorStyling = false
            });
        }

        [SearchActionsProvider]
        internal static IEnumerable<SearchAction> ActionHandlers()
        {
            return new SearchAction[]
            {
                new SearchAction(type, "select", null, "Select System")
                {
                    execute = (items) =>
                    {
                        SelectItem(items[0], null);
                    },
                    closeWindowAfterExecution = false
                }
            };
        }

        [SearchItemProvider]
        internal static SearchProvider CreateProvider()
        {
            var p = new SearchProvider(type, "Systems")
            {
                type = type,
                filterId = "sys:",
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
                trackSelection = SelectItem,
                toObject = (item, type) =>
                {
                    var wrapper = SystemInfo.CreateInstance<SystemInfo>();
                    wrapper.name = item.label;
                    wrapper.item = item;
                    wrapper.objItem = (SystemDescriptor)item.data;
                    return wrapper;
                }
            };
            SearchBridge.SetTableConfig(p, GetDefaultTableConfig);
            return p;
        }

        static string AdjustPrecision(float sum)
        {
            return s_MoreTimePrecision ? sum.ToString("f4") : sum.ToString("f2");
        }

        [SearchColumnProvider(nameof(System))]
        internal static void SystemColumnProvider(SearchColumn column)
        {
            column.getter = args =>
            {
                if (args.item.data is SystemDescriptor data)
                {
                    switch (column.selector)
                    {
                        case "world": return data.proxy.World.Name;
                        case "namespace": return data.proxy.Namespace;
                        case "entitycount": return data.proxy.TotalEntityMatches;
                        case "time": return AdjustPrecision(data.proxy.RunTimeMillisecondsForDisplay);
                        case "isrunning": return data.proxy.IsRunning;
                        case "childcount": return data.proxy.ChildCount;
                        case "category": return data.proxy.Category;
                    }
                }
                return null;
            };
        }

        [SearchColumnProvider("Systems/Enabled")]
        internal static void EnabledColumn(SearchColumn column)
        {
            column.getter = args =>
            {
                if (args.item.data is SystemDescriptor data)
                    return data.proxy.Enabled;
                return null;
            };
            column.drawer = args =>
            {
                if (args.item.data is SystemDescriptor data)
                {
                    var rect = args.rect;
                    rect.x += rect.width / 2;
                    return GUI.Toggle(rect, data.proxy.Enabled, "");
                }
                return null;
            };
            column.setter = args =>
            {
                if (args.item.data is SystemDescriptor data)
                    data.proxy.SetEnabled((bool)args.value);
            };
        }

        static IEnumerable<string> GetWords(SystemDescriptor desc)
        {
            yield return desc.name;
        }

        static void SetupQueryEngine()
        {
            s_QueryEngine = new();
            s_QueryEngine.SetSearchDataCallback(GetWords);

            SearchBridge.AddFilter<string, SystemDescriptor>(s_QueryEngine, "c", OnComponentTypeFilter, new[] { ":", "=" });
            SearchBridge.AddFilter<string, SystemDescriptor>(s_QueryEngine, "sd", OnSystemDependencyFilter, new[] { ":", "=" });

            // Note: Skip filter for world filter that shouldn't be tested against the dataset
            s_QueryEngine.skipUnknownFilters = true;

            SearchBridge.SetFilter(s_QueryEngine, "entitycount", data => data.proxy.TotalEntityMatches)
                .AddOrUpdateProposition(category: null, label: "Entity Count", replacement: "entitycount>10", help: "Search Systems by Entity Count");

            SearchBridge.SetFilter(s_QueryEngine, "componentcount", data => data.componentNamesInQueryCache.Length)
                .AddOrUpdateProposition(category: null, label: "Component Count", replacement: "componentcount>5", help: "Search Systems by Component Count");

            SearchBridge.SetFilter(s_QueryEngine, "dependencycount", data => data.systemDependencyCache.Length)
                .AddOrUpdateProposition(category: null, label: "Dependency Count", replacement: "dependencycount>0", help: "Search Systems by Dependency Count");
            SearchBridge.SetFilter(s_QueryEngine, "time", data => data.proxy.RunTimeMillisecondsForDisplay)
                .AddOrUpdateProposition(category: null, label: "Time", replacement: "time>100", help: "Search Systems by time");

            SearchBridge.SetFilter(s_QueryEngine, "enabled", data => data.proxy.Enabled)
                .AddOrUpdateProposition(category: null, label: "Enabled", replacement: "enabled=true", help: "Search Enabled systems");

            SearchBridge.SetFilter(s_QueryEngine, "isrunning", data => data.proxy.IsRunning)
                .AddOrUpdateProposition(category: null, label: "Is Running", replacement: "isrunning=true", help: "Search Running systems");

            SearchBridge.SetFilter(s_QueryEngine, "childcount", data => data.proxy.ChildCount)
                .AddOrUpdateProposition(category: null, label: "childcount", replacement: "childcount>5", help: "Search systems by child count");

            SearchBridge.SetFilter(s_QueryEngine, "category", data => data.proxy.Category)
                .AddOrUpdateProposition(category: null, label: "category", replacement: "category=Unknown", help: "Search systems by category");

            SearchBridge.SetFilter(s_QueryEngine, "parent", data => data.proxy.Parent.TypeName)
                .AddOrUpdateProposition(category: null, label: "parent", replacement: "parent=Unknown", help: "Search systems by parent");
        }

        static bool SetupSystemDescriptors()
        {
            s_WorldProxyManager = new WorldProxyManager();
            s_SystemDependencyMap = new();            
            if (s_CurrentWorld == null)
            {
                Debug.LogWarning("System Search provider: cannot find a valid World");
                return false;
            }
            s_WorldProxyManager.CreateWorldProxiesForAllWorlds();
            s_WorldProxy = s_WorldProxyManager.GetWorldProxyForGivenWorld(s_CurrentWorld);
            s_WorldProxyManager.SelectedWorldProxy = s_WorldProxy;
            return true;
        }

        static void OnEnable()
        {
            s_CurrentWorld = World.DefaultGameObjectInjectionWorld;

            s_MoreTimePrecision = UserSettings<SystemsWindowPreferenceSettings>.GetOrCreate(Constants.Settings.SystemsWindow).Configuration.ShowMorePrecisionForRunningTime;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Cleanup();
        }

        static void Cleanup()
        {
            s_CurrentWorld = null;
            s_WorldProxyManager?.Dispose();
            s_WorldProxyManager = null;
            s_Systems?.Clear();
            s_Systems = null;
            s_SystemDependencyMap?.Clear();
            s_SystemDependencyMap = null;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange == PlayModeStateChange.ExitingEditMode || stateChange == PlayModeStateChange.ExitingPlayMode)
            {
                Cleanup();
            }
            else if (stateChange == PlayModeStateChange.EnteredPlayMode || stateChange == PlayModeStateChange.EnteredEditMode)
            {
                SearchBridge.RefreshWindowsWithProvider(type);
            }
        }

        static bool OnComponentTypeFilter(SystemDescriptor desc, QueryFilterOperator op, string value)
        {
            return SearchBridge.CompareWords(op, value, desc.componentNamesInQueryCache);
        }

        static bool OnSystemDependencyFilter(SystemDescriptor desc, QueryFilterOperator op, string value)
        {
            return SearchBridge.CompareWords(op, value, desc.systemDependencyCache);
        }

        static SearchTable GetDefaultTableConfig(SearchContext context)
        {
            return new SearchTable(type, FetchColumns(null, null));
        }

        static IEnumerable<SystemDescriptor> GetAllSystems()
        {
            if (s_SystemDependencyMap == null)
                s_SystemDependencyMap = new();
            if (s_Systems == null)
                s_Systems = new();

            s_SystemDependencyMap.Clear();
            s_Systems.Clear();

            foreach (var world in World.All)
            {
                var worldProxy = s_WorldProxyManager.GetWorldProxyForGivenWorld(world);
                if (worldProxy == null)
                    continue;

                foreach (var system in worldProxy.AllSystems)
                {
                    s_Systems.Add(new SystemDescriptor(system));
                    BuildSystemDependencyMap(system);
                }
            }

            FillSystemDependencyCache(s_Systems);

            return s_Systems;
        }

        static class Styles
        {
            public static Texture2D systemIcon = PackageResources.LoadIcon("System/System.png");
            public static Texture2D systemGroupIcon = PackageResources.LoadIcon("Group/Group.png");
            public static Texture2D beginCommandBufferIcon = PackageResources.LoadIcon("BeginCommandBuffer/BeginCommandBuffer.png");
            public static Texture2D endCommandBufferIcon = PackageResources.LoadIcon("EndCommandBuffer/EndCommandBuffer.png");
            public static Texture2D unmanagedSystemIcon = PackageResources.LoadIcon("UnmanagedSystem/UnmanagedSystem.png");
        }

        static Texture2D GetSystemIcon(SystemProxy systemProxy)
        {
            var flags = systemProxy.Valid ? systemProxy.Category : 0;
            if ((flags & SystemCategory.ECBSystemBegin) != 0)
                return Styles.beginCommandBufferIcon;
            if ((flags & SystemCategory.ECBSystemEnd) != 0)
                return Styles.endCommandBufferIcon;
            if ((flags & SystemCategory.EntityCommandBufferSystem) != 0)
                return null;
            if ((flags & SystemCategory.Unmanaged) != 0)
                return Styles.unmanagedSystemIcon;
            if ((flags & SystemCategory.SystemGroup) != 0)
                return Styles.systemGroupIcon;
            if ((flags & SystemCategory.SystemBase) != 0)
                return Styles.systemIcon;

            return null;
        }

        static void BuildSystemDependencyMap(SystemProxy systemProxy)
        {
            var keyString = systemProxy.TypeName;

            // TODO: Find better solution to be able to uniquely identify each system.
            // At the moment, we are using system name to identify each system, which is not reliable
            // because there can be multiple systems with the same name in a world. This is only a
            // temporary solution to avoid the error of adding the same key into the map. We need to
            // find a proper solution to be able to uniquely identify each system.
            if (!s_SystemDependencyMap.ContainsKey(keyString))
            {
                var handle = systemProxy;
                var dependencies = handle.UpdateBeforeSet
                    .Concat(handle.UpdateAfterSet)
                    .Select(s => s.TypeName)
                    .ToArray();
                s_SystemDependencyMap.Add(keyString, dependencies);
            }
        }

        static void FillSystemDependencyCache(List<SystemDescriptor> descriptors)
        {
            foreach (var desc in descriptors)
            {
                var dependencies = (from kvp in s_SystemDependencyMap where kvp.Value.Contains(desc.name) select kvp.Key).ToArray();
                desc.UpdateDependencies(dependencies);
            }
        }

        static IEnumerable<SearchItem> FetchItems(SearchContext context, SearchProvider provider)
        {
            if (s_CurrentWorld == null)
                s_CurrentWorld = World.DefaultGameObjectInjectionWorld;
            
            var searchQuery = context.searchQuery;
            ParsedQuery<SystemDescriptor> query = null;
            if (!string.IsNullOrEmpty(searchQuery))
            {
                query = queryEngine.ParseQuery(context.searchQuery);
                if (!query.valid)
                {
                    context.AddSearchQueryErrors(query.errors.Select(e => new SearchQueryError(e, context, provider)));
                    yield break;
                }

                var toggles = new List<IQueryNode>();
                var filters = new List<IFilterNode>();
                var searches = new List<ISearchNode>();
                SearchUtils.GetQueryParts(query.queryGraph.root, filters, toggles, searches);
                var worldFilter = filters.Where(f => f.filterId == "w").FirstOrDefault();
                if (worldFilter != null && s_CurrentWorld != null && worldFilter.filterValue != s_CurrentWorld.Name)
                {
                    // Note: since wolrd filter is skipped, the quotes are not removed automatically.
                    var worldName = worldFilter.filterValue.Replace("\"", "");
                    SetWorld(worldName);
                }
            }

            if (s_CurrentWorld == null || systems == null)
            {
                SearchUtils.AddError("Cannot find a world to execute the query", context, provider);
                yield break;
            }

            var results = string.IsNullOrEmpty(searchQuery) ? systems : query.Apply(systems);
            var score = 0;
            foreach (var data in results)
            {
                yield return provider.CreateItem(context, data.proxy.TypeFullName, score++, data.name, data.proxy.Namespace, GetSystemIcon(data.proxy), data);
            }
        }

        static IEnumerable<SearchProposition> FetchPropositions(SearchContext context, SearchPropositionOptions options)
        {
            if (!options.flags.HasAny(SearchPropositionFlags.QueryBuilder))
                yield break;

            foreach (var p in SearchBridge.GetPropositionsFromListBlockType(typeof(QueryWorldBlock)))
                yield return p;

            foreach (var p in SearchBridge.GetAndOrQueryBlockPropositions())
                yield return p;

            foreach (var p in SearchBridge.GetPropositions(queryEngine))
                yield return p;

            foreach (var l in SearchBridge.GetPropositionsFromListBlockType(typeof(QueryComponentTypeBlock)))
                yield return l;

            foreach (var l in SearchBridge.GetPropositionsFromListBlockType(typeof(QuerySystemDependenciesBlock)))
                yield return l;
        }

        static IEnumerable<SearchColumn> FetchColumns(SearchContext context, IEnumerable<SearchItem> items)
        {
            yield return new SearchColumn("Systems/Enabled", "Systems/Enabled", "Systems/Enabled");
            if (context == null)
            {
                yield return new SearchColumn("Name", "label");
            }

            yield return new SearchColumn("Systems/World", "world", nameof(System));
            yield return new SearchColumn("Systems/Namespace", "namespace", nameof(System));
            yield return new SearchColumn("Systems/Entity Count", "entitycount", nameof(System));
            yield return new SearchColumn("Systems/Time", "time", nameof(System));
            if (context == null)
            {
                yield break;
            }

            yield return new SearchColumn("Systems/Category", "category", nameof(System));
            yield return new SearchColumn("Systems/Is Running", "isrunning", nameof(System));
            yield return new SearchColumn("Systems/Child Count", "childcount", nameof(System));
        }

        [MenuItem("Window/Search/Systems", priority = 1371)]
        static void OpenProviderMenu()
        {
            OpenProvider();
        }

        [UnityEditor.ShortcutManagement.Shortcut("Help/Search/Systems")]
        static void OpenShortcut()
        {
            OpenProvider();
        }

        /// <summary>
        /// Open SearchWindow with SystemSearchProvider enabled.
        /// </summary>
        /// <param name="query">Optional initial query.</param>
        public static void OpenProvider(string query = null, World world = null)
        {
            query = SearchUtils.GetDefaultWorldQuery(query, world);
            SearchBridge.OpenContextualTable(type, query, GetDefaultTableConfig(null));
        }
    }

}
