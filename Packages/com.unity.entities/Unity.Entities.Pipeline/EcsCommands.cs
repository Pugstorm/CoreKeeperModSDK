using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Unity.Collections;
using Unity.Pipeline.Commands;
using UnityEngine.Scripting;

namespace Unity.Entities.Pipeline
{
    [Preserve]
    public static class EcsCommands
    {
        [CliCommand("ecs_worlds", "List created ECS worlds. Target other commands with an exact, unique world name.", Tags = new[] { "ecs/read" })]
        public static object Worlds() => new
        {
            worlds = EcsTypeResolver.Worlds().Select(w => new { name = w.Name, flags = w.Flags.ToString(), worldElapsedTime = w.Time.ElapsedTime }).ToArray(),
            debugNamesAvailable = NamesAvailable
        };

        [CliCommand("ecs_types", "Search registered ECS component types, or describe one exact type. Not required before querying or reading.", Tags = new[] { "ecs/read" })]
        public static object Types(
            [CliArg("search", "Case-insensitive name substring")] string search = null,
            [CliArg("type", "Exact short, namespace-qualified or assembly-qualified name")] string type = null,
            [CliArg("offset", "First result index")] int offset = 0,
            [CliArg("limit", "Maximum results")] int limit = 50)
        {
            EcsTypeResolver.Page(offset, limit);
            if (type != null)
            {
                var resolved = EcsTypeResolver.Resolve(type);
                return new { component = Metadata(resolved, EcsTypeResolver.Component(resolved)), fields = EcsValueCodec.Fields(resolved)
                    .Select(f => new { name = f.Name, type = f.FieldType.FullName, writable = !f.IsInitOnly,
                        enumNames = f.FieldType.IsEnum ? Enum.GetNames(f.FieldType) : null }).ToArray() };
            }
            var types = EcsTypeResolver.Types().Where(t => search == null || (t.FullName ?? t.Name).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(EcsTypeResolver.Id, StringComparer.Ordinal).ToArray();
            return new { total = types.Length, offset, types = types.Skip(offset).Take(limit).Select(t => Metadata(t, EcsTypeResolver.Component(t))).ToArray() };
        }

        [CliCommand("ecs_query", "Query one world using native all/any/none/disabled/absent/present/options, shared equality, and optional field/name filters. Matches include index:version and debug name. Runs synchronously after completing jobs.", Tags = new[] { "ecs/read" })]
        public static object Query(
            [CliArg("world", "Exact unique World.Name", Required = true)] string world,
            [CliArg("query", "Native component selection plus shared/where/name filters, select, offset and limit", Required = true)] EcsQueryArgs query)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            EcsTypeResolver.Page(query.Offset, query.Limit);
            ValidateSelections(query.Select);
            if (query.Name != null && !NamesAvailable) throw new NotSupportedException("Entity debug-name storage is disabled in this build.");
            var selected = EcsTypeResolver.World(world);
            var manager = selected.EntityManager;
            manager.CompleteAllTrackedJobs();
            var predicates = EcsQueryUtility.Predicates(manager, query.Where);
            using var nativeQuery = EcsQueryUtility.Build(manager, query);
            using var entities = nativeQuery.ToEntityArray(Allocator.Temp);
            var matches = new List<Entity>();
            foreach (var entity in entities)
            {
                if (query.Name != null)
                {
                    var name = manager.GetName(entity);
                    if (query.Name.EqualsName != null && name != query.Name.EqualsName) continue;
                    if (query.Name.Contains != null && name.IndexOf(query.Name.Contains, StringComparison.Ordinal) < 0) continue;
                }
                if (predicates.All(predicate => predicate(entity))) matches.Add(entity);
            }
            matches.Sort((a, b) => a.Index != b.Index ? a.Index.CompareTo(b.Index) : a.Version.CompareTo(b.Version));
            var rows = new JArray();
            var complete = true;
            foreach (var entity in matches.Skip(query.Offset).Take(query.Limit))
            {
                rows.Add(ReadEntity(manager, entity, query.Select, false, false, out var entityComplete));
                complete &= entityComplete;
            }
            return new
            {
                world, total = matches.Count, offset = query.Offset,
                hasMore = (long)query.Offset + query.Limit < matches.Count,
                entities = rows, complete,
                debugNamesAvailable = NamesAvailable, worldElapsedTime = selected.Time.ElapsedTime, frame = UnityEngine.Time.frameCount
            };
        }

        [CliCommand("ecs_read", "Read entities and names, component inventory or selected values, including buffer ranges. Entity references are index:version. No component selection lists types; allValues reads all values.", Tags = new[] { "ecs/read" })]
        public static object Read(
            [CliArg("world", "Exact unique World.Name", Required = true)] string world,
            [CliArg("request", "entities (index:version strings), components (type/fields/storage/offset/limit), or allValues", Required = true)] EcsReadArgs request)
        {
            if (request?.Entities == null || request.Entities.Length == 0) throw new ArgumentException("entities must contain at least one index:version reference.");
            if (request.AllValues && request.Components != null) throw new ArgumentException("Use components or allValues, not both.");
            ValidateSelections(request.Components);
            var selected = EcsTypeResolver.World(world);
            var manager = selected.EntityManager;
            manager.CompleteAllTrackedJobs();
            var entities = new JArray();
            var complete = true;
            foreach (var reference in request.Entities)
            {
                var entity = EcsTypeResolver.ParseEntity(reference);
                if (manager.Exists(entity))
                {
                    entities.Add(ReadEntity(manager, entity, request.Components, request.AllValues, true, out var entityComplete));
                    complete &= entityComplete;
                }
                else
                {
                    entities.Add(new JObject { ["entity"] = reference, ["error"] = "missingEntity" });
                    complete = false;
                }
            }
            return new { world, entities, complete,
                debugNamesAvailable = NamesAvailable, worldElapsedTime = selected.Time.ElapsedTime, frame = UnityEngine.Time.frameCount };
        }

        internal static void ValidateSelections(EcsSelection[] selections)
        {
            if (selections == null) return;
            foreach (var selection in selections)
            {
                if (selection == null) throw new ArgumentException("Null component selection.");
                EcsTypeResolver.Page(selection.Offset, selection.Limit);
                var type = EcsTypeResolver.Resolve(selection.Type);
                EcsTypeResolver.Component(type, selection.Storage);
                foreach (var path in selection.Fields ?? Array.Empty<string>()) EcsValueCodec.Path(type, path);
            }
        }

        internal static JObject Identity(EntityManager manager, Entity entity) => new JObject
        {
            ["entity"] = EcsTypeResolver.Format(entity), ["name"] = manager.Exists(entity) ? manager.GetName(entity) : null
        };

        static JObject ReadEntity(EntityManager manager, Entity entity, EcsSelection[] selections, bool allValues, bool inventory, out bool complete)
        {
            var result = Identity(manager, entity);
            complete = true;
            if (selections == null && !allValues && !inventory) return result;
            var components = new JArray();
            if (selections != null)
            {
                foreach (var selection in selections)
                {
                    var type = EcsTypeResolver.Resolve(selection.Type);
                    components.Add(ReadComponent(manager, entity, type, EcsTypeResolver.Component(type, selection.Storage), selection, out var componentComplete));
                    complete &= componentComplete;
                }
            }
            else
            {
                using var types = manager.GetComponentTypes(entity);
                foreach (var component in types)
                {
                    var type = TypeManager.GetType(component.TypeIndex);
                    if (type == null || type == typeof(Entity)) continue;
                    components.Add(ReadComponent(manager, entity, type, component,
                        allValues ? new EcsSelection { Type = EcsTypeResolver.Id(type), Storage = component.IsChunkComponent ? "chunk" : "entity" } : null, out var componentComplete));
                    complete &= componentComplete;
                }
            }
            result["components"] = components;
            return result;
        }

        static JObject Metadata(Type type, ComponentType component) => new JObject
        {
            ["type"] = EcsTypeResolver.Id(type), ["shortName"] = type.Name,
            ["storage"] = component.IsChunkComponent ? "chunk" : "entity",
            ["category"] = component.IsBuffer ? "buffer" : component.IsSharedComponent ? "shared" :
                component.IsZeroSized ? "tag" : type.IsClass ? "managed" : "component",
            ["enableable"] = component.IsEnableable
        };

        static JObject ReadComponent(EntityManager manager, Entity entity, Type type, ComponentType component, EcsSelection selection, out bool complete)
        {
            complete = true;
            var result = new JObject { ["type"] = type.Name };
            if (component.IsChunkComponent) result["storage"] = "chunk";
            if (!manager.HasComponent(entity, component)) { result["error"] = "missingComponent"; complete = false; return result; }
            if (component.IsEnableable) result["enabled"] = manager.IsComponentEnabled(entity, component);
            if (selection == null) return result;
            try
            {
                var access = EcsAccess.For(type);
                if (component.IsBuffer)
                {
                    var length = access.Length(manager, entity);
                    var end = (int)Math.Min(length, (long)selection.Offset + selection.Limit);
                    var elements = new JArray();
                    for (var i = selection.Offset; i < end; i++) elements.Add(new JObject
                    {
                        ["index"] = i, ["value"] = EcsValueCodec.Project(access.Element(manager, entity, i), type, selection.Fields)
                    });
                    result["length"] = length; result["offset"] = selection.Offset; result["elements"] = elements; result["hasMore"] = end < length;
                }
                else result["value"] = EcsValueCodec.Project(access.Read(manager, entity, component.IsChunkComponent), type, selection.Fields);
                complete = !HasUnsupportedValue(result);
            }
            catch (NotSupportedException error) { result["error"] = "unsupported"; result["reason"] = error.Message; complete = false; }
            return result;
        }

        static bool HasUnsupportedValue(JToken token)
        {
            if (token is JObject obj && obj["error"]?.Type == JTokenType.String &&
                obj.Value<string>("error") == "unsupported" && obj["reason"] != null) return true;
            return token.Children().Any(HasUnsupportedValue);
        }

        internal static bool NamesAvailable
        {
            get
            {
#if DOTS_DISABLE_DEBUG_NAMES
                return false;
#else
                return true;
#endif
            }
        }
    }
}
