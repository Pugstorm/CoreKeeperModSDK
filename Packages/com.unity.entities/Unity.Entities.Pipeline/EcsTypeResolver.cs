using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Unity.Entities.Pipeline
{
    internal static class EcsTypeResolver
    {
        internal static Type[] Types() => TypeManager.GetAllTypes().Select(info => info.Type)
            .Where(type => type != null && type != typeof(Entity)).Distinct().ToArray();

        internal static Type Resolve(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A component type name is required.");
            var matches = Types().Where(type => type.AssemblyQualifiedName == name || type.FullName == name || type.Name == name).ToArray();
            if (matches.Length == 1) return matches[0];
            if (matches.Length == 0) throw new ArgumentException($"No registered component type matches '{name}'. Use ecs_types to search registered types.");
            throw new ArgumentException($"Ambiguous component type '{name}': {string.Join("; ", matches.Select(Id))}");
        }

        internal static string Id(Type type) => type.AssemblyQualifiedName;

        internal static Entity ParseEntity(string text)
        {
            var parts = text?.Split(':');
            if (parts == null || parts.Length != 2 ||
                !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var index) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var version) ||
                index < 0)
                throw new ArgumentException($"Invalid entity '{text}'. Expected index:version, for example 1842:6 or 0:0 for Entity.Null.");
            return new Entity { Index = index, Version = version };
        }

        internal static string Format(Entity entity) =>
            entity.Index.ToString(CultureInfo.InvariantCulture) + ":" + entity.Version.ToString(CultureInfo.InvariantCulture);

        internal static World World(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A world name is required. Use ecs_worlds.");
            var matches = Worlds().Where(world => world.Name == name).ToArray();
            if (matches.Length == 0) throw new ArgumentException($"No created world named '{name}'. Use ecs_worlds.");
            if (matches.Length != 1) throw new ArgumentException($"Several worlds are named '{name}'. Give the worlds unique names before targeting them.");
            return matches[0];
        }

        internal static IEnumerable<World> Worlds()
        {
            // World.All deliberately rejects enumeration through IEnumerable<T>.
            foreach (var world in Unity.Entities.World.All)
                if (world.IsCreated) yield return world;
        }

        internal static Entity Existing(EntityManager manager, string text)
        {
            var entity = ParseEntity(text);
            if (!manager.Exists(entity)) throw new ArgumentException($"Entity {text} does not exist in the selected world.");
            return entity;
        }

        internal static ComponentType Component(Type type, string storage = null, bool write = false)
        {
            if (string.IsNullOrEmpty(storage) || storage == "entity")
                return write ? ComponentType.ReadWrite(type) : ComponentType.ReadOnly(type);
            if (storage != "chunk") throw new ArgumentException("storage must be 'entity' or 'chunk'.");
            if (!typeof(IComponentData).IsAssignableFrom(type)) throw new ArgumentException("Only IComponentData can use chunk storage.");
            return write ? ComponentType.ChunkComponent(type) : ComponentType.ChunkComponentReadOnly(type);
        }

        internal static void Page(int offset, int limit)
        {
            if (offset < 0 || limit < 1) throw new ArgumentException("offset must be nonnegative and limit must be positive.");
        }
    }
}
