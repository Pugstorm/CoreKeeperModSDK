using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using Unity.Editor.Bridge;
using Unity.Entities.UI;

namespace Unity.Entities.Editor
{
    internal readonly struct ComponentTypeDescriptor
    {
        public readonly TypeManager.TypeInfo info;
        public readonly string name;
        public readonly ComponentsWindow.DebugTypeCategory category;
        public readonly Texture2D icon;
        public readonly string displayName;

        public ComponentTypeDescriptor(TypeManager.TypeInfo typeInfo)
        {
            info = typeInfo;
            name = TypeUtility.GetTypeDisplayName(info.Type);
            displayName = ComponentsUtility.GetComponentDisplayName(TypeUtility.GetTypeDisplayName(info.Type));
            category = ComponentsWindow.DebugTypeCategory.None;
            switch (typeInfo.Category)
            {
                case TypeManager.TypeCategory.ComponentData:
                    if (TypeManager.IsZeroSized(typeInfo.TypeIndex))
                    {
                        category |= ComponentsWindow.DebugTypeCategory.Tag;
                    }
                    else
                    {
                        category |= ComponentsWindow.DebugTypeCategory.Data;
                    }
                    break;
                case TypeManager.TypeCategory.BufferData:
                    category |= ComponentsWindow.DebugTypeCategory.Buffer;
                    break;
                case TypeManager.TypeCategory.ISharedComponentData:
                    category |= ComponentsWindow.DebugTypeCategory.Shared;
                    break;
                case TypeManager.TypeCategory.EntityData:
                    category |= ComponentsWindow.DebugTypeCategory.Entity;
                    break;
                case TypeManager.TypeCategory.UnityEngineObject:
                    category |= ComponentsWindow.DebugTypeCategory.Companion;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            icon = SearchUtils.GetComponentIcon(typeInfo.TypeIndex);

            if (TypeManager.IsManagedComponent(typeInfo.TypeIndex))
            {
                category |= ComponentsWindow.DebugTypeCategory.Managed;
            }
        }
    }
    [ExcludeFromPreset]
    internal class ComponentInfo : SearchItemWrapper<ComponentTypeDescriptor>
    {
        private void OnEnable()
        {
            hideFlags = hideFlags & HideFlags.NotEditable;
        }
    }

    [CustomEditor(typeof(ComponentInfo))]
    internal class ComponentWrapperEditor : UnityEditor.Editor
    {
        private void OnEnable()
        {
        }

        public override void OnInspectorGUI()
        {
            // TODO Search: for 2022, Inspector in SearchWindow has to be done with IMGUI

            var wrapper = (ComponentInfo)target;
            var item = wrapper.item;
            var desc = wrapper.objItem;

            var oldWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 150;

            EditorGUILayout.TextField($"Namespace", desc.info.Type.Namespace);
            EditorGUILayout.LabelField($"Is Baking-Only Type", desc.info.BakingOnlyType.ToString());
            EditorGUILayout.LabelField($"Is Temporary Type", desc.info.TemporaryBakingType.ToString());
            EditorGUILayout.LabelField($"Type Index", desc.info.TypeIndex.Value.ToString());
            EditorGUILayout.TextField($"Stable Type Hash", desc.info.StableTypeHash.ToString());
            EditorGUILayout.LabelField($"Category", desc.category.ToString());
            EditorGUILayout.LabelField($"Buffer Capacity in Chunk", FormattingUtility.BytesToString((ulong)desc.info.BufferCapacity));
            EditorGUILayout.LabelField($"Type Size", FormattingUtility.BytesToString((ulong)desc.info.TypeSize));
            EditorGUILayout.LabelField($"Size in Chunk", FormattingUtility.BytesToString((ulong)desc.info.SizeInChunk));
            EditorGUILayout.LabelField($"Alignment", FormattingUtility.BytesToString((ulong)desc.info.AlignmentInBytes));
            EditorGUILayout.LabelField($"Alignment in Chunk", FormattingUtility.BytesToString((ulong)desc.info.AlignmentInChunkInBytes));


            EditorGUIUtility.labelWidth = oldWidth;
        }
    }

    /// <summary>
    /// This class allows the SearchWindow to search for ECS components.
    /// </summary>
    public static class ComponentSearchProvider
    {
        /// <summary>
        /// Search Provider type id. 
        /// </summary>
        public const string type = "component";

        internal static QueryEngine<ComponentTypeDescriptor> s_QueryEngine;
        internal static QueryEngine<ComponentTypeDescriptor> queryEngine
        {
            get
            {
                if (s_QueryEngine == null)
                    SetupQueryEngine();
                return s_QueryEngine;
            }
        }

        internal static List<ComponentTypeDescriptor> s_TypeInfos;
        internal static List<ComponentTypeDescriptor> typeInfos
        {
            get
            {
                if (s_TypeInfos == null)
                {
                    SetupTypeInfos();
                }
                    
                return s_TypeInfos;
            }
        }

        [SearchActionsProvider]
        internal static IEnumerable<SearchAction> ActionHandlers()
        {
            return new SearchAction[]
            {
                new SearchAction(type, "select", null, "Select component")
                {
                    execute = (items) =>
                    {
                        SelectItem(items[0], null);
                    },
                    closeWindowAfterExecution = false
                }
                
            };
        }

        static void SelectItem(SearchItem item, SearchContext ctx)
        {
            var data = (ComponentTypeDescriptor)item.data;
            SelectionUtility.ShowInInspector(new ComponentContentProvider
            {
                ComponentType = data.info.Type
            }, new InspectorContentParameters
            {
                ApplyInspectorStyling = false,
                UseDefaultMargins = false
            });
        }

        [SearchItemProvider]
        internal static SearchProvider CreateProvider()
        {
            var p = new SearchProvider(type, "Components")
            {
                type = type,
                filterId = "comp:",
                onEnable = OnEnable,
                onDisable = OnDisable,
                fetchColumns = FetchColumns,
                isExplicitProvider = true,
                active = true,
                priority = 2500,
                fetchItems = (context, items, provider) => FetchItems(context, provider),
                fetchPropositions = (context, options) => FetchPropositions(context, options),
                fetchThumbnail = SearchUtils.DefaultFetchThumbnail,
                fetchLabel = SearchUtils.DefaultFetchLabel,
                fetchDescription = SearchUtils.DefaultFetchDescription,
                showDetails = true,
                showDetailsOptions = ShowDetailsOptions.Default | ShowDetailsOptions.Inspector,
                trackSelection = SelectItem,
                toObject = (item, type) =>
                {
                    var wrapper = ComponentInfo.CreateInstance<ComponentInfo>();
                    wrapper.name = item.label;
                    wrapper.item = item;
                    wrapper.objItem = (ComponentTypeDescriptor)item.data;
                    return wrapper;
                }
            };
            SearchBridge.SetTableConfig(p, GetDefaultTableConfig);
            return p;
        }

        [SearchColumnProvider(nameof(Component))]
        internal static void ArchetypeColumnProvider(SearchColumn column)
        {
            column.getter = args =>
            {
                if (args.item.data is ComponentTypeDescriptor data)
                {
                    switch (column.selector)
                    {
                        case "category": return data.category.ToString();
                        case "typeindex": return data.info.TypeIndex.Value;
                        case "capacity": return data.info.BufferCapacity;
                        case "hasblobassetrefs": return data.info.HasBlobAssetRefs;
                        case "hasweakassetrefs": return data.info.HasWeakAssetRefs;
                        case "haswritegroups": return data.info.HasWriteGroups;
                        case "sizeinchunk": return data.info.SizeInChunk;
                        case "typesize": return data.info.TypeSize;
                        case "bakingonlytype": return data.info.BakingOnlyType;
                        case "temporarybakingtype": return data.info.TemporaryBakingType;
                    }
                }

                return null;
            };
        }

        static IEnumerable<string> GetWords(ComponentTypeDescriptor desc)
        {
            yield return desc.name;
            yield return desc.displayName;
        }

        static void SetupQueryEngine()
        {
            s_QueryEngine = new();
            s_QueryEngine.SetSearchDataCallback(GetWords);

            SearchBridge.SetFilter(s_QueryEngine, "category", data => data.category);
            SearchBridge.SetFilter(s_QueryEngine, "capacity", data => data.info.BufferCapacity)
                .AddOrUpdateProposition(category: null, label: "Capacity", replacement: "capacity>1024", help: "Search Components by Buffer Capacity");
            SearchBridge.SetFilter(s_QueryEngine, "index", data => data.info.TypeIndex.Value)
                .AddOrUpdateProposition(category: null, label: "Type Index", replacement: "index=839002", help: "Search Components by Type Index");
            SearchBridge.SetFilter(s_QueryEngine, "hasblobassetrefs", data => data.info.HasBlobAssetRefs)
                .AddOrUpdateProposition(category: null, label: "HasBlobAssetRefs", replacement: "hasblobassetrefs=true", help: "Search Components with BlobAssetRefs");
            SearchBridge.SetFilter(s_QueryEngine, "hasweakassetrefs", data => data.info.HasWeakAssetRefs)
                .AddOrUpdateProposition(category: null, label: "HasWeakAssetRefs", replacement: "hasweakassetrefs=true", help: "Search Components with WeakAssetRefs");
            SearchBridge.SetFilter(s_QueryEngine, "haswritegroups", data => data.info.HasWriteGroups)
                .AddOrUpdateProposition(category: null, label: "HasWriteGroups", replacement: "haswritegroups=true", help: "Search Components with WriteGroups");
            SearchBridge.SetFilter(s_QueryEngine, "iszerosized", data => data.info.IsZeroSized)
                .AddOrUpdateProposition(category: null, label: "IsZeroSized", replacement: "iszerosized=true", help: "Search Components with size of zero");
            SearchBridge.SetFilter(s_QueryEngine, "sizeinchunk", data => data.info.SizeInChunk)
                .AddOrUpdateProposition(category: null, label: "SizeInChunk", replacement: "sizeinchunk>1024", help: "Search Components with chunk size");
            SearchBridge.SetFilter(s_QueryEngine, "typesize", data => data.info.TypeSize)
                .AddOrUpdateProposition(category: null, label: "TypeSize", replacement: "typesize>1024", help: "Search Components with size");
            SearchBridge.SetFilter(s_QueryEngine, "bakingonlytype", data => data.info.BakingOnlyType)
                .AddOrUpdateProposition(category: null, label: "BakingOnlyType", replacement: "bakingonlytype=true", help: "Search Components that are used only in Baking");
            SearchBridge.SetFilter(s_QueryEngine, "temporarybakingtype", data => data.info.TemporaryBakingType)
                .AddOrUpdateProposition(category: null, label: "TemporaryBakingType", replacement: "temporarybakingtype=true", help: "Search Components that are used temporarily in Baking");
        }

        static void OnEnable()
        {
            
        }

        static void OnDisable()
        {
            
        }

        static SearchTable GetDefaultTableConfig(SearchContext context)
        {
            return new SearchTable(type, new[] { new SearchColumn("Name", "label") }.Concat(FetchColumns(null, null)));
        }

        static void SetupTypeInfos()
        {
            s_TypeInfos = new();
            // Note: if a type is added "after-the-fact", it might not be displayed in the window, we can add a way to
            // detect this if and when that time comes.
            TypeManager.Initialize();
            foreach (var typeInfo in TypeManager.AllTypes)
            {
                // First type is the "null" type, which we don't care about.
                if (null == typeInfo.Type)
                    continue;

                // TypeManager will generate type info for all types derived from UnityEngine.Object, so we are skipping
                // them here
                if (typeInfo.Category == TypeManager.TypeCategory.UnityEngineObject)
                    continue;

                var desc = new ComponentTypeDescriptor(typeInfo);
                s_TypeInfos.Add(desc);
            }
            s_TypeInfos.Sort((lhs, rhs) => string.Compare(lhs.name, rhs.name, StringComparison.Ordinal));
        }

        static IEnumerable<SearchItem> FetchItems(SearchContext context, SearchProvider provider)
        {
            var searchQuery = context.searchQuery;
            ParsedQuery<ComponentTypeDescriptor> query = null;
            if (!string.IsNullOrEmpty(searchQuery))
            {
                query = queryEngine.ParseQuery(context.searchQuery);
                if (!query.valid)
                {
                    Debug.LogError(string.Join(" ", query.errors.Select(e => e.reason)));
                    yield break;
                }
            }

            var results = typeInfos as IEnumerable<ComponentTypeDescriptor>;
            if (query != null)
            {
                results = query.Apply(results);
            }

            var score = 0;
            foreach (var data in results)
            {
                var item = provider.CreateItem(context, data.info.GetHashCode().ToString(), score++, data.displayName, data.category.ToString(), data.icon, data);
                yield return item;
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

            foreach (var l in SearchBridge.GetPropositionsFromListBlockType(typeof(QueryComponentCategoryBlock)))
                yield return l;
        }

        static IEnumerable<SearchColumn> FetchColumns(SearchContext context, IEnumerable<SearchItem> items)
        {
            yield return new SearchColumn("Components/Category", "category", nameof(Component));
            if (context == null)
            {
                // Default Columns set:
                yield break;
            }
            yield return new SearchColumn("Components/Capacity", "capacity", nameof(Component));
            yield return new SearchColumn("Components/SizeInChunk", "sizeinchunk", nameof(Component));
            yield return new SearchColumn("Components/TypeSize", "typesize", nameof(Component));
            yield return new SearchColumn("Components/TypeIndex", "typeindex", nameof(Component));
            yield return new SearchColumn("Components/HasBlobAssetRefs", "hasblobassetrefs", nameof(Component));
            yield return new SearchColumn("Components/HasWeakAssetRefs", "hasweakassetrefs", nameof(Component));
            yield return new SearchColumn("Components/HasWriteGroups", "haswritegroups", nameof(Component));
            yield return new SearchColumn("Components/IsZeroSized", "iszerosized", nameof(Component));
            yield return new SearchColumn("Components/BakingOnlyType", "bakingonlytype", nameof(Component));
            yield return new SearchColumn("Components/TemporaryBakingType", "temporarybakingtype", nameof(Component));
        }

        [MenuItem("Window/Search/Components", priority = 1371)]
        static void OpenProviderMenu()
        {
            OpenProvider();
        }

        [UnityEditor.ShortcutManagement.Shortcut("Help/Search/Components")]
        static void OpenShortcut()
        {
            OpenProvider();
        }

        /// <summary>
        /// Open SearchWindow with ComponentSearchProvider enabled.
        /// </summary>
        /// <param name="query">Optional initial query.</param>
        public static void OpenProvider(string query = null)
        {
            SearchBridge.OpenContextualTable(type, query ?? "", GetDefaultTableConfig(null));
        }
    }
}
