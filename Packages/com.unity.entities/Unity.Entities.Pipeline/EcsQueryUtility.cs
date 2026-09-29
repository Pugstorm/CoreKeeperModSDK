using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Unity.Entities.Pipeline
{
    internal static class EcsQueryUtility
    {
        internal static EntityQuery Build(EntityManager manager, EcsQueryArgs args)
        {
            var descriptions = args.Descriptions;
            if (descriptions != null && HasSelection(args)) throw new ArgumentException("Use either descriptions or the top-level component sets/options, not both.");
            if (descriptions == null) descriptions = new EcsDescription[] { args };
            if (descriptions.Length == 0 || descriptions.Any(d => d == null)) throw new ArgumentException("descriptions must contain at least one non-null description.");
            var native = descriptions.Select(Description).ToArray();
            if (native.Select(d => (d.Options & EntityQueryOptions.IgnoreComponentEnabledState) != 0).Distinct().Count() != 1)
                throw new ArgumentException("IgnoreComponentEnabledState must agree across descriptions.");
            var shared = args.Shared ?? Array.Empty<EcsSharedFilter>();
            if (shared.Length > 2) throw new ArgumentException("Unity supports at most two shared-component filters.");
            var prepared = shared.Select(filter =>
            {
                if (filter == null) throw new ArgumentException("Null shared filter.");
                var type = EcsTypeResolver.Resolve(filter.Type);
                if (!typeof(ISharedComponentData).IsAssignableFrom(type)) throw new ArgumentException($"{filter.Type} is not a shared component.");
                var index = TypeManager.GetTypeIndex(type);
                if (native.Any(d => !d.All.Concat(d.Present).Any(c => c.TypeIndex == index)))
                    throw new ArgumentException($"Shared filter {type.FullName} must be required in all/present in every description.");
                return (type, value: EcsValueCodec.Decode(filter.Value, type, true));
            }).ToArray();
            if (prepared.Select(p => p.type).Distinct().Count() != prepared.Length) throw new ArgumentException("Duplicate shared filter type.");
            var query = manager.CreateEntityQuery(native);
            try
            {
                foreach (var item in prepared) EcsAccess.For(item.type).Filter(query, item.value);
                return query;
            }
            catch { query.Dispose(); throw; }
        }

        static bool HasSelection(EcsDescription description) => description.All != null || description.Any != null ||
            description.None != null || description.Disabled != null || description.Absent != null || description.Present != null || description.Options != null;

        static EntityQueryDesc Description(EcsDescription input)
        {
            var description = new EntityQueryDesc
            {
                All = Components(input.All), Any = Components(input.Any), None = Components(input.None),
                Disabled = Components(input.Disabled), Absent = Components(input.Absent), Present = Components(input.Present)
            };
            var types = description.All.Concat(description.Any).Concat(description.None).Concat(description.Disabled).Concat(description.Absent).Concat(description.Present).ToArray();
            if (types.Select(type => type.TypeIndex).Distinct().Count() != types.Length) throw new ArgumentException("Duplicate component type within a query description.");
            if (description.Disabled.Any(type => !type.IsEnableable)) throw new ArgumentException("disabled requires enableable component types.");
            foreach (var option in input.Options ?? Array.Empty<string>())
            {
                if (!Enum.GetNames(typeof(EntityQueryOptions)).Contains(option) || option == "IncludeDisabled")
                    throw new ArgumentException($"Unknown/obsolete query option '{option}'. Use EntityQueryOptions names.");
                description.Options |= (EntityQueryOptions)Enum.Parse(typeof(EntityQueryOptions), option);
            }
            description.Validate();
            return description;
        }

        static ComponentType[] Components(JToken[] tokens) => (tokens ?? Array.Empty<JToken>()).Select(token =>
        {
            if (token?.Type == JTokenType.String && token.Value<string>().TrimStart().StartsWith("{", StringComparison.Ordinal))
                token = JObject.Parse(token.Value<string>());
            if (token?.Type == JTokenType.String) return EcsTypeResolver.Component(EcsTypeResolver.Resolve(token.Value<string>()));
            if (!(token is JObject obj)) throw new ArgumentException("Component sets accept type names or {type,access,storage} objects.");
            foreach (var property in obj.Properties())
                if (property.Name != "type" && property.Name != "access" && property.Name != "storage") throw new ArgumentException($"Unknown component selector field '{property.Name}'.");
            var access = obj.Value<string>("access") ?? "readOnly";
            if (access != "readOnly" && access != "readWrite") throw new ArgumentException("access must be readOnly or readWrite.");
            return EcsTypeResolver.Component(EcsTypeResolver.Resolve(obj.Value<string>("type")), obj.Value<string>("storage"), access == "readWrite");
        }).ToArray();

        internal static Func<Entity, bool>[] Predicates(EntityManager manager, EcsPredicate[] predicates)
        {
            return (predicates ?? Array.Empty<EcsPredicate>()).Select(predicate =>
            {
                if (predicate == null) throw new ArgumentException("Null where predicate.");
                var type = EcsTypeResolver.Resolve(predicate.Type);
                var component = EcsTypeResolver.Component(type, predicate.Storage);
                if (component.IsBuffer) throw new ArgumentException("where predicates currently select component fields, not buffer elements.");
                var path = EcsValueCodec.Path(type, predicate.Field);
                var fieldType = path[path.Length - 1].FieldType;
                var expected = EcsValueCodec.Decode(predicate.Value, fieldType);
                if (!new[] { "eq", "ne", "lt", "lte", "gt", "gte" }.Contains(predicate.Op)) throw new ArgumentException($"Unknown predicate operator '{predicate.Op}'.");
                var ordering = predicate.Op != "eq" && predicate.Op != "ne";
                if (ordering && !typeof(IComparable).IsAssignableFrom(fieldType)) throw new ArgumentException($"{fieldType.Name} does not support ordering.");
                var access = EcsAccess.For(type);
                return new Func<Entity, bool>(entity =>
                {
                    if (!manager.HasComponent(entity, component)) return false;
                    var value = EcsValueCodec.Get(access.Read(manager, entity, component.IsChunkComponent), path);
                    if (predicate.Op == "eq") return Equals(value, expected);
                    if (predicate.Op == "ne") return !Equals(value, expected);
                    if (value == null || expected == null) return false;
                    var comparison = value is string text ? StringComparer.Ordinal.Compare(text, (string)expected) : ((IComparable)value).CompareTo(expected);
                    switch (predicate.Op)
                    {
                        case "lt": return comparison < 0;
                        case "lte": return comparison <= 0;
                        case "gt": return comparison > 0;
                        default: return comparison >= 0;
                    }
                });
            }).ToArray();
        }
    }
}
