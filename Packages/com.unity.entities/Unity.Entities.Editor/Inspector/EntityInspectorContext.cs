using System;
using Unity.Entities.UI;
using UnityEditor;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// Context to be used when inspecting an <see cref="Entity"/>.
    /// </summary>
    class EntityInspectorContext : InspectionContext
    {
        const string k_InvalidEntityName = "{ Invalid Entity }";

        const WorldFlags k_ReadonlyFlags = WorldFlags.Conversion
                                         | WorldFlags.Shadow
                                         | WorldFlags.Staging
                                         | WorldFlags.Streaming;

        internal World World { get; private set; }
        internal EntityContainer EntityContainer { get; private set; }

        internal EntityManager EntityManager => EntityContainer.EntityManager;
        internal Entity Entity => EntityContainer.Entity;
        internal bool IsReadOnly => EntityContainer.IsReadOnly;

        internal EntityAspectsCollectionContainer AspectsCollectionContainer => new EntityAspectsCollectionContainer(World, Entity, IsReadOnly);

        internal void SetContext(EntitySelectionProxy proxy, bool forceWritable = false)
        {
            if (!proxy.Exists)
            {
                // The proxy is no longer valid, we can't generate a valid container out of it
                EntityContainer = default;
                return;
            }

            World = proxy.World;

            if (forceWritable)
            {
                EntityContainer = new EntityContainer(World.EntityManager, proxy.Entity, false);
            }
            else
            {
                var isReadonly = !EditorApplication.isPlaying || IsWorldReadOnly(World);
                EntityContainer = new EntityContainer(World.EntityManager, proxy.Entity, isReadonly);
            }
        }

        internal bool TargetExists()
        {
            return World is { IsCreated: true } && World.EntityManager.SafeExists(Entity);
        }

        internal string GetTargetName()
        {
            if (!TargetExists())
                return k_InvalidEntityName;

            var name = EntityManager.GetName(Entity);
            return string.IsNullOrEmpty(name) ? $"Entity {{{Entity.Index}:{Entity.Version}}}" : name;
        }

        internal void SetTargetName(string name)
        {
            if (!TargetExists())
                return;

            EntityManager.SetName(Entity, name);
        }

        internal UnityEngine.Object GetSourceObject()
            => this.TryGetComponentData(out EntityGuid guid) ? EditorUtility.InstanceIDToObject(guid.OriginatingId) : null;

        static bool IsWorldReadOnly(World world)
        {
            return (world.Flags & k_ReadonlyFlags) != WorldFlags.None;
        }
    }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
    static class EntityInspectorContextClassExtensions
    {
        public static bool TryGetComponentData<T>(this EntityInspectorContext context, out T component)
            where T : class, IComponentData, new()
        {
            if (!context.TargetExists() || !context.EntityManager.HasComponent<T>(context.Entity))
            {
                component = default;
                return false;
            }

            component = context.EntityManager.GetComponentData<T>(context.Entity);
            return true;
        }

        public static bool TryGetChunkComponentData<T>(this EntityInspectorContext context, out T component)
            where T : class, IComponentData, new()
        {
            if (!context.TargetExists() || !context.EntityManager.HasComponent<T>(context.Entity))
            {
                component = default;
                return false;
            }

            component = context.EntityManager.GetChunkComponentData<T>(context.Entity);
            return true;
        }
    }
#endif

    static class EntityInspectorContextStructExtensions
    {
        public static bool TryGetComponentData<T>(this EntityInspectorContext context, out T component)
            where T : unmanaged, IComponentData
        {
            if (!context.TargetExists() || !context.EntityManager.HasComponent<T>(context.Entity))
            {
                component = default;
                return false;
            }

            component = context.EntityManager.GetComponentData<T>(context.Entity);
            return true;
        }

        public static bool TryGetChunkComponentData<T>(this EntityInspectorContext context, out T component)
            where T : unmanaged, IComponentData
        {
            if (!context.TargetExists() || !context.EntityManager.HasComponent<T>(context.Entity))
            {
                component = default;
                return false;
            }

            component = context.EntityManager.GetChunkComponentData<T>(context.Entity);
            return true;
        }
    }
}
