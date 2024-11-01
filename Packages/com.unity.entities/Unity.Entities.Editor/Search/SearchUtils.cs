using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor.Search;
using UnityEngine;
using Unity.Editor.Bridge;
using UnityEngine.UIElements;
using System.Reflection;

namespace Unity.Entities.Editor
{
    internal class SearchItemWrapper<T> : UnityEngine.ScriptableObject
    {
        public SearchItem item;
        public T objItem;
    }

    [QueryListBlock("World", "world", "w", "=")]
    class QueryWorldBlock : QueryListBlock
    {
        public QueryWorldBlock(IQuerySource source, string id, string value, QueryListBlockAttribute attr)
            : base(source, id, value, attr)
        {
        }

        public override IEnumerable<SearchProposition> GetPropositions(SearchPropositionFlags flags)
        {
            var category = flags.HasFlag(SearchPropositionFlags.NoCategory) ? null : this.category;
            foreach (var t in World.All)
            {
                var name = t.Name.Contains(" ") ? $"\"{t.Name}\"" : t.Name;
                yield return new SearchProposition(category, t.Name, replacement: name, help: $"Entity World: {t.Name}", type: GetType(), data: name);
            }
        }
    }

    [QueryListBlock("Component", "c", "c", "=")]
    class QueryComponentTypeBlock : QueryListBlock
    {
        public QueryComponentTypeBlock(IQuerySource source, string id, string value, QueryListBlockAttribute attr)
            : base(source, id, value, attr)
        {
        }

        public override IEnumerable<SearchProposition> GetPropositions(SearchPropositionFlags flags)
        {
            var category = flags.HasFlag(SearchPropositionFlags.NoCategory) ? null : this.category;
            foreach (var cn in SearchUtils.componentNames)
            {
                var name = cn.Contains(" ") ? $"\"{cn}\"" : cn;
                yield return new SearchProposition(category, $"all={cn}", replacement: name, help: $"Component type: {cn}", type: GetType(), data: name);
            }
        }
    }

    [QueryListBlock("Component (All)", "All", "all", "=")]
    class QueryAllComponentTypeBlock : QueryComponentTypeBlock
    {
        public QueryAllComponentTypeBlock(IQuerySource source, string id, string value, QueryListBlockAttribute attr)
            : base(source, id, value, attr)
        {
        }
    }

    [QueryListBlock("Component (None)", "None", "-c", "=")]
    class QueryNotAllComponentTypeBlock : QueryListBlock
    {
        public QueryNotAllComponentTypeBlock(IQuerySource source, string id, string value, QueryListBlockAttribute attr)
            : base(source, id, value, attr)
        {
        }

        public override IEnumerable<SearchProposition> GetPropositions(SearchPropositionFlags flags)
        {
            var category = flags.HasFlag(SearchPropositionFlags.NoCategory) ? null : this.category;
            foreach (var cn in SearchUtils.componentNames)
            {
                var name = cn.Contains(" ") ? $"\"{cn}\"" : cn;
                yield return new SearchProposition(category, $"none={cn}", replacement: name, help: $"No Component of type: {cn}", type: GetType(), data: name);
            }
        }
    }

    [QueryListBlock("Component (None)", "None", "none", "=")]
    class QueryNoComponentTypeBlock : QueryNotAllComponentTypeBlock
    {
        public QueryNoComponentTypeBlock(IQuerySource source, string id, string value, QueryListBlockAttribute attr)
            : base(source, id, value, attr)
        {
        }
    }

    [QueryListBlock("Component (Any)", "Any", "any", "=")]
    class QueryAnyComponentTypeBlock : QueryListBlock
    {
        public QueryAnyComponentTypeBlock(IQuerySource source, string id, string value, QueryListBlockAttribute attr)
            : base(source, id, value, attr)
        {
        }

        public override IEnumerable<SearchProposition> GetPropositions(SearchPropositionFlags flags)
        {
            var category = flags.HasFlag(SearchPropositionFlags.NoCategory) ? null : this.category;
            foreach (var cn in SearchUtils.componentNames)
            {
                var name = cn.Contains(" ") ? $"\"{cn}\"" : cn;
                yield return new SearchProposition(category, $"any={cn}", replacement: name, help: $"Any Component of type: {cn}", type: GetType(), data: name);
            }
        }
    }

    [QueryListBlock("Kind", "Kind", "k", "=")]
    class QueryNodeKindBlock : QueryListBlock
    {
        public QueryNodeKindBlock(IQuerySource source, string id, string value, QueryListBlockAttribute attr)
            : base(source, id, value, attr)
        {
        }

        public override IEnumerable<SearchProposition> GetPropositions(SearchPropositionFlags flags)
        {
            return SearchUtils.GetEnumPropositions<NodeKind>(flags, this, "Kind:", new[] { NodeKind.Entity, NodeKind.GameObject, NodeKind.Scene, NodeKind.SubScene });
        }
    }

    [QueryListBlock("Prefab Type", "Prefab Type", "prefabtype", "=")]
    class QueryPrefabTypeBlock : QueryListBlock
    {
        public QueryPrefabTypeBlock(IQuerySource source, string id, string value, QueryListBlockAttribute attr)
            : base(source, id, value, attr)
        {
        }

        public override IEnumerable<SearchProposition> GetPropositions(SearchPropositionFlags flags)
        {
            return SearchUtils.GetEnumPropositions<Editor.Hierarchy.HierarchyPrefabType>(flags, this, "Prefab Type:");
        }
    }

    [QueryListBlock("System Dependencies", "System Dependencies", "sd", "=")]
    class QuerySystemDependenciesBlock : QueryListBlock
    {
        public QuerySystemDependenciesBlock(IQuerySource source, string id, string value, QueryListBlockAttribute attr)
            : base(source, id, value, attr)
        {
        }

        public override IEnumerable<SearchProposition> GetPropositions(SearchPropositionFlags flags)
        {
            var category = flags.HasFlag(SearchPropositionFlags.NoCategory) ? null : this.category;
            foreach (var sys in SystemSearchProvider.systems)
            {
                yield return new SearchProposition(category, $"{id}{op}{sys.name}", replacement: sys.name, help: $"{category} {sys.name}", type: GetType(), data: sys.name);
            }
        }
    }

    [QueryListBlock("System", "System", "s", ":")]
    class QuerySystemBlock : QuerySystemDependenciesBlock
    {
        public QuerySystemBlock(IQuerySource source, string id, string value, QueryListBlockAttribute attr)
            : base(source, id, value, attr)
        {
        }
    }

    [QueryListBlock("Origin System", "Origin System", "os", ":")]
    class QueryOriginSystemBlock : QuerySystemDependenciesBlock
    {
        public QueryOriginSystemBlock(IQuerySource source, string id, string value, QueryListBlockAttribute attr)
            : base(source, id, value, attr)
        {
        }
    }

    [QueryListBlock("Executing System", "Executing System", "es", ":")]
    class QueryExecutingSystemBlock : QuerySystemDependenciesBlock
    {
        public QueryExecutingSystemBlock(IQuerySource source, string id, string value, QueryListBlockAttribute attr)
            : base(source, id, value, attr)
        {
        }
    }


    [QueryListBlock("Category", "category", "category", ":")]
    class QueryComponentCategoryBlock : QueryListBlock
    {
        public QueryComponentCategoryBlock(IQuerySource source, string id, string value, QueryListBlockAttribute attr)
            : base(source, id, value, attr)
        {
        }

        public override IEnumerable<SearchProposition> GetPropositions(SearchPropositionFlags flags)
        {
            return SearchUtils.GetEnumPropositions<ComponentsWindow.DebugTypeCategory>(flags, this, "Category:");
        }
    }

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
    [QueryListBlock("Record Type", "rt", "rt", "=")]
    class QueryRecordTypeBlock : QueryListBlock
    {
        public QueryRecordTypeBlock(IQuerySource source, string id, string value, QueryListBlockAttribute attr)
            : base(source, id, value, attr)
        {
        }

        public override IEnumerable<SearchProposition> GetPropositions(SearchPropositionFlags flags)
        {
            return SearchUtils.GetEnumPropositions<EntitiesJournaling.RecordType>(flags, this, "Record Type:");
        }
    }
#endif

    class SharedComponentPropertyDesc
    {
        public SharedComponentPropertyDesc(TypeManager.TypeInfo compInfo, FieldInfo propertyField)
        {
            componentInfo = compInfo;
            this.propertyField = propertyField;
            fullPropertyName = $"{fullComponentName}.{propertyField.Name}";
            shortPropertyName = $"{shortComponentName}.{propertyField.Name}";
            fullPropertyReplacement = $"{fullPropertyName}={SearchUtils.GetDefaultValue(propertyField.FieldType)}";
            shortPropertyReplacement = $"{shortPropertyName}={SearchUtils.GetDefaultValue(propertyField.FieldType)}";
            fullPropertyQueryReplacement = $"#{fullPropertyQueryReplacement}";
            shortPropertyQueryReplacement = $"#{shortPropertyReplacement}";
            useShortName = true;
        }

        internal bool useShortName;

        public readonly TypeManager.TypeInfo componentInfo;

        public string shortComponentName => componentInfo.Type.Name;
        public string fullComponentName => componentInfo.Type.FullName;
        public Type propertyType => propertyField.FieldType;
        public string propertyName => useShortName ? shortPropertyName : fullPropertyName;
        public string propertyReplacement => useShortName ? shortPropertyReplacement : fullPropertyReplacement;
        public string propertyQueryReplacement => useShortName ? shortPropertyQueryReplacement : fullPropertyQueryReplacement;

        public readonly string fullPropertyName;
        public readonly string shortPropertyName;
        public readonly string fullPropertyReplacement;
        public readonly string shortPropertyReplacement;
        public readonly string fullPropertyQueryReplacement;
        public readonly string shortPropertyQueryReplacement;
        public readonly FieldInfo propertyField;

        public string GetPropertyStringValue(object obj)
        {
            SearchUtils.TryConvertToString(propertyField.GetValue(obj), out var strValue);
            return strValue;
        }
    }

    class SharedComponentDesc
    {
        public Entities.TypeManager.TypeInfo info;
        public string typeName => info.Type.Name;
        public List<SharedComponentPropertyDesc> properties;
        public SharedComponentDesc(Entities.TypeManager.TypeInfo info)
        {
            this.info = info;
            properties = new List<SharedComponentPropertyDesc>();
            PopulateComponentPropertyDescs(this);
        }

        public ISharedComponentData CreateSharedComponent()
        {
            return (ISharedComponentData)Activator.CreateInstance(info.Type);
        }

        void PopulateComponentPropertyDescs(SharedComponentDesc compDesc)
        {
            var fields = compDesc.info.Type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            foreach (var field in fields)
            {
                if (!SearchUtils.IsSupportedType(field.FieldType))
                    continue;
                var desc = new SharedComponentPropertyDesc(compDesc.info, field);
                compDesc.properties.Add(desc);
            }
        }
    }

    static class SearchUtils
    {
        public static Texture2D componentIcon = PackageResources.LoadIcon("Components/Component.png");
        public static Texture2D chunkComponentIcon = PackageResources.LoadIcon("Components/Chunk Component.png");
        public static Texture2D bufferComponentIcon = PackageResources.LoadIcon("Components/Buffer Component.png");
        public static Texture2D sharedComponentIcon = PackageResources.LoadIcon("Components/Shared Component.png");
        public static Texture2D managedComponentIcon = PackageResources.LoadIcon("Components/Managed Component.png");
        public static Texture2D tagComponentIcon = PackageResources.LoadIcon("Components/Tag Component.png");
        public static Texture2D search = PackageResources.LoadIcon("Search/Search.png");
        public static Texture2D gotoIcon = PackageResources.LoadIcon("Go To/Go to.png");

        public static Texture2D sceneAssetIcon = SearchBridge.LoadIcon("SceneAsset Icon");
        public static Texture2D entityIcon = PackageResources.LoadIcon("Entity/Entity.png");
        public static Texture2D entityPrefabIcon = PackageResources.LoadIcon("Entity/EntityPrefab.png");
        public static Texture2D gameObjectIcon = SearchBridge.LoadIcon("GameObject Icon");
        public static Texture2D prefabIcon = SearchBridge.LoadIcon("Prefab Icon");

        public static class Styles
        {
            public static readonly GUIStyle iconButton = "IconButton";
        }

        static Dictionary<string, SharedComponentDesc> s_SharedComponentDescs;
        public static Dictionary<string, SharedComponentDesc> SharedComponents
        {
            get
            {
                if (s_SharedComponentDescs == null)
                {
                    s_SharedComponentDescs = new Dictionary<string, SharedComponentDesc>();
                    foreach (var type in TypeManager.AllTypes.Where(t => TypeManager.IsSharedComponentType(t.TypeIndex)))
                    {
                        s_SharedComponentDescs.Add(type.Type.FullName, new SharedComponentDesc(type));
                    }
                }
                return s_SharedComponentDescs;
            }
        }

        internal static object GetDefaultValue(Type t)
        {
            if (t == typeof(Hash128))
                return new Hash128("8c91bc4eab64d2840bd8688ced1ace09");
            if (t.IsValueType)
                return Activator.CreateInstance(t);
            if (t == typeof(string))
                return "";
            if (t == typeof(Entity))
                return Entity.Null;

            return null;
        }

        internal static bool TryConvertToString(object value, out string strValue)
        {
            if (value == null)
            {
                strValue = null;
            }
            else if (value is Entity entity)
            {
                strValue = $"{entity.Index}-{entity.Version}";
            }
            else
            {
                strValue = value.ToString();
            }
            return true;
        }

        internal static bool TryConvertValue(Type t, string strValue, out object typedValue)
        {
            try
            {
                if (t.IsPrimitive)
                {
                    typedValue = Convert.ChangeType(strValue, t);
                }
                else if (t.IsEnum)
                {
                    TryParseEnum(t, strValue, out typedValue);
                }
                else if (t == typeof(string))
                {
                    typedValue = strValue;
                }
                else if (t == typeof(Entity))
                {
                    var tokens = strValue.Split('-');
                    if (tokens.Length == 2)
                    {
                        typedValue = new Entity(){
                            Index = int.Parse(tokens[0]),
                            Version = int.Parse(tokens[1])
                        };
                    }
                    else
                    {
                        typedValue = Entity.Null;
                    }
                }
                else
                {
                    var constructor = SearchUtils.GetStringConstructor(t);
                    typedValue = constructor.Invoke(new object[] { strValue });
                }
            }
            catch (Exception)
            {
                typedValue = null;
                return false;
            }

            return true;
        }

        internal static ConstructorInfo GetStringConstructor(Type type)
        {
            foreach (var ctor in type.GetConstructors())
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                    return ctor;
            }

            return null;
        }

        internal static bool IsSupportedType(Type type)
        {
            if (type.IsPrimitive || type.IsEnum)
                return true;

            if (type == typeof(string) || type == typeof(Entity))
                return true;

            if (GetStringConstructor(type) != null)
            {
                return true;
            }
            return false;
        }

        static Dictionary<string, SharedComponentPropertyDesc> s_SharedComponentPropertyDescMap;
        static List<SharedComponentPropertyDesc> s_SharedComponentPropertyDescs;

        static void InitSharedComponentProperties()
        {
            if (s_SharedComponentPropertyDescs == null)
            {
                s_SharedComponentPropertyDescs = new List<SharedComponentPropertyDesc>();
                s_SharedComponentPropertyDescMap = new Dictionary<string, SharedComponentPropertyDesc>();
                foreach (var typeDesc in SharedComponents.Values)
                {
                    foreach (var propertyDesc in typeDesc.properties)
                    {
                        s_SharedComponentPropertyDescMap.Add(propertyDesc.fullPropertyName, propertyDesc);
                        if (!s_SharedComponentPropertyDescMap.ContainsKey(propertyDesc.shortPropertyName))
                        {
                            s_SharedComponentPropertyDescMap.Add(propertyDesc.shortPropertyName, propertyDesc);
                            propertyDesc.useShortName = true;
                        }
                        else
                        {
                            propertyDesc.useShortName = false;
                        }                        
                        s_SharedComponentPropertyDescs.Add(propertyDesc);
                    }
                }
            }
        }

        public static bool TryGetSharedComponentPropertyDesc(string propertyName, out SharedComponentPropertyDesc desc)
        {
            InitSharedComponentProperties();
            return s_SharedComponentPropertyDescMap.TryGetValue(propertyName, out desc);
        }

        internal static SharedComponentPropertyDesc GetSharedComponentPropertyDesc(string propertyName)
        {
            InitSharedComponentProperties();
            return s_SharedComponentPropertyDescMap[propertyName];
        }

        public static IEnumerable<SharedComponentPropertyDesc> GetSharedComponentPropertyDescs()
        {
            InitSharedComponentProperties();
            return s_SharedComponentPropertyDescs;
        }
       
        static string[] s_ComponentNames;
        public static IEnumerable<string> componentNames
        {
            get
            {
                if (s_ComponentNames == null)
                {
                    s_ComponentNames = TypeManager.AllTypes.Where(t => t.Type != null).Select(t => t.Type.Name).ToArray();
                }
                return s_ComponentNames;
            }
        }

        public static Texture2D GetComponentIcon(TypeIndex typeIndex)
        {
            if (TypeManager.IsChunkComponent(typeIndex))
                return chunkComponentIcon;
            else if (TypeManager.IsBuffer(typeIndex))
                return bufferComponentIcon;
            else if (TypeManager.IsSharedComponentType(typeIndex))
                return sharedComponentIcon;
            else if (TypeManager.IsManagedComponent(typeIndex))
                return managedComponentIcon;
            else if (TypeManager.IsZeroSized(typeIndex))
                return tagComponentIcon;
            else
                return componentIcon;
        }

        const string k_EditorWorld = "Editor World";
        const string k_DefaultWorld = "Default World";

        public static World FindWorld(string worldName = null)
        {
            if (worldName != null)
            {
                worldName = worldName.ToLowerInvariant();
                if (worldName == "default")
                    worldName = k_DefaultWorld;
                else if (worldName == "editor")
                    worldName = k_EditorWorld;
            }

            foreach (var world in World.All)
            {
                var name = world.Name;
                if (worldName != null)
                {
                    if (name.Equals(worldName, StringComparison.InvariantCultureIgnoreCase))
                        return world;
                }
                else
                {
                    if ((world.Flags & WorldFlags.Editor) != 0)
                        return world;

                    if ((world.Flags & WorldFlags.Game) != 0)
                        return world;

                    if ((world.Flags & WorldFlags.GameServer) != 0)
                        return world;

                    if ((world.Flags & WorldFlags.Live) != 0)
                        return world;
                }
            }

            return null;
        }

        internal static IEnumerable<SearchProposition> GetEnumPropositions<TEnum>(SearchPropositionFlags flags, QueryListBlock b, string helpTemplate) where TEnum : System.Enum
        {
            return GetEnumPropositions(flags, b, helpTemplate, (TEnum[])Enum.GetValues(typeof(TEnum)));
        }

        internal static IEnumerable<SearchProposition> GetEnumPropositions<TEnum>(SearchPropositionFlags flags, QueryListBlock b, string helpTemplate, IEnumerable<TEnum> values) where TEnum : System.Enum
        {
            var category = flags.HasFlag(SearchPropositionFlags.NoCategory) ? null : b.category;
            foreach (var obj in values)
            {
                var e = obj;
                yield return new SearchProposition(category: category, label: e.ToString(), help: $"{helpTemplate} {e}",
                        data: e, priority: 0, icon: null, type: b.GetType(), color: SearchBridge.GetBackgroundColor(b));
            }
        }

        public static void GetQueryParts(IQueryNode n, List<IFilterNode> filters, List<IQueryNode> toggles, List<ISearchNode> searches)
        {
            if (n == null)
                return;
            if (n.type == QueryNodeType.Toggle)
            {
                toggles.Add(n);
            }
            else if (n is IFilterNode filterNode)
            {
                filters.Add(filterNode);
            }
            else if (n is ISearchNode searchNode)
            {
                searches.Add(searchNode);
            }

            if (n.children != null)
            {
                foreach (var child in n.children)
                {
                    GetQueryParts(child, filters, toggles, searches);
                }
            }
        }

        public static T ParseEnum<T>(string value)
        {
            TryParseEnum(typeof(T), value, out var enumValue);
            return (T)enumValue;
        }

        public static bool TryParseEnum(Type t, string value, out object enumValue)
        {
            var values = Enum.GetValues(t);
            var names = Enum.GetNames(t);
            for (var i = 0; i < names.Length; ++i)
            {
                if (names[i].Equals(value, StringComparison.InvariantCultureIgnoreCase))
                {
                    enumValue = values.GetValue(i);
                    return true;
                }
            }
            enumValue = values.GetValue(0);
            return false;
        }

        public static string AddOrReplaceFilterInQuery(string originalQuery, string filter, string op, object value)
        {
            var newFilter = $"{filter}{op}{value}";
            if (originalQuery.Contains(newFilter))
                return originalQuery;

            var regexStr = $"\\b{filter}{op}(\\S*)";
            var regex = new Regex(regexStr);

            var newQuery = regex.Replace(originalQuery, newFilter);
            var m = regex.Match(originalQuery);
            if (newQuery == originalQuery)
            {
                if (newQuery.Length == 0)
                    newQuery = newFilter;
                else if (newQuery[newQuery.Length - 1] == ':')
                    newQuery = $"{newQuery}{newFilter}";
                else
                    newQuery = $"{newQuery} {newFilter}";
            }
            return newQuery;
        }

        public static string DefaultFetchLabel(SearchItem item, SearchContext context)
        {
            return item.label ?? item.id ?? string.Empty;
        }

        public static string DefaultFetchDescription(SearchItem item, SearchContext context)
        {
            var desc = item.description ?? string.Empty;
            if (item.options.HasFlag(SearchItemOptions.Compacted) && item?.provider?.fetchLabel != null)
            {
                return $"{item.provider.fetchLabel(item, context)} - {desc}";
            }
            return desc;
        }

        public static Texture2D DefaultFetchThumbnail(SearchItem item, SearchContext context)
        {
            return item.thumbnail;
        }

        public static Button CreateJumpButton(Action onButtonClick)
        {
            var jump = new Button();
            SetupJumpButton(jump, onButtonClick);
            return jump;
        }

        public static void SetupJumpButton(Button jump, Action onButtonClick)
        {
            jump.style.backgroundImage = SearchBridge.LoadIcon("SearchJump Icon");
            jump.AddToClassList(UssClasses.DotsEditorCommon.SearchIcon);
            jump.clicked += onButtonClick;
        }

        public static string GetDefaultWorldQuery(string query, World world = null)
        {
            query = query ?? "";
            if (query.StartsWith("w=") || query.Contains("w="))
            {
                return query;
            }

            world = world ?? SearchUtils.FindWorld();
            if (world == null)
                return query;
            query = $"w=\"{world.Name}\" {query}";
            return query;
        }

        internal static void AddError(string reason, SearchContext context, SearchProvider provider)
        {
            context.AddSearchQueryError(new SearchQueryError(0, 0, reason, context, provider));
        }

        public static string CreateComponentQuery(object component)
        {
            if (component is ISharedComponentData sharedComponent)
            {
                return CreateSharedComponentQuery(sharedComponent);
            }            

            return $"all={component.GetType().FullName}";
        }

        public static string CreateSharedComponentQuery(ISharedComponentData sharedComponent)
        {
            string query = null;
            if (SharedComponents.TryGetValue(sharedComponent.GetType().FullName, out var compDesc))
            {
                foreach(var prop in compDesc.properties)
                {
                    var value = prop.GetPropertyStringValue(sharedComponent);
                    if (value == null)
                        continue;
                    if (query == null)
                    {
                        query = "";
                    }
                    else
                    {
                        query += " ";
                    }
                    if (value.Contains(" "))
                        value = $"\"{value}\"";
                    query += $"#{prop.propertyName}={value}";
                }
            }

            return query;
        }
    }

}
