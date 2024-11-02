using System;
using Unity.Editor.Bridge;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// Proxy object for an <see cref="Unity.Entities.Entity"/> so it can be selected and shown in the inspector.
    /// </summary>
    class EntitySelectionProxy : ScriptableObject, ISerializationCallbackReceiver
    {
        static EntitySelectionProxy s_LastSelected;

        [InitializeOnLoadMethod]
        static void RefCountGlobalSelection()
        {
            Selection.selectionChanged += () =>
            {
                var entitySelectionProxy = Selection.activeObject as EntitySelectionProxy ??
                                           Selection.activeContext as EntitySelectionProxy;

                if (s_LastSelected != null && s_LastSelected != entitySelectionProxy)
                    s_LastSelected.Release();

                s_LastSelected = entitySelectionProxy;

                if (entitySelectionProxy == null)
                    return;

                // Being currently selected counts as being retained
                entitySelectionProxy.Retain();
            };
        }

        /// <summary>
        /// Creates and configures an instance of EntitySelectionProxy wrapping the specified <see cref="Unity.Entities.Entity"/>.
        /// </summary>
        /// <param name="world">The <see cref="Unity.Entities.World"/> in which the Entity exists.</param>
        /// <param name="entity">The entity to be wrapped by this instance of EntitySelectionProxy.</param>
        /// <returns>A fully configured EntitySelectionProxy, wrapping the specified entity.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <see cref="world"/> is <code>null</code> or world.<see cref="Unity.Entities.World.IsCreated"/> is <code>false</code>.</exception>
        public static EntitySelectionProxy CreateInstance(World world, Entity entity)
        {
            if (world is not { IsCreated: true })
                throw new ArgumentNullException(nameof(world));

            if (!EntityExistsAndIsValid(world, entity))
                entity = Entity.Null;

            var proxy = CreateInstance<EntitySelectionProxy>();
            proxy.hideFlags = HideFlags.DontSaveInBuild |
                              HideFlags.DontSaveInEditor |
                              HideFlags.NotEditable;

            proxy.Initialize(world, entity);

            var undoGroup = Undo.GetCurrentGroup();
            Undo.RegisterCreatedObjectUndo(proxy, $"Create {nameof(EntitySelectionProxy)}({entity.Index}, {entity.Version})");
            Undo.CollapseUndoOperations(undoGroup);

            return proxy;
        }

        /// <summary>
        /// Creates and selects an instance of EntitySelectionProxy wrapping the specified <see cref="Unity.Entities.Entity"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This operation does not return the created instance of EntitySelectionProxy. If you want to hold onto the created instance, please use <see cref="CreateInstance"/> instead.
        /// </para>
        /// <para>
        /// This operation may produce no results if <see cref="world"/> is <code>null</code>, if world.<see cref="Unity.Entities.World.IsCreated"/> is <code>false</code>, if the specified
        /// <see cref="entity"/> does not exist in the specified world, or if the current selection already points to the specified entity.
        /// </para>
        /// </remarks>
        /// <param name="world">The <see cref="Unity.Entities.World"/> in which the Entity exists.</param>
        /// <param name="entity">The entity to be wrapped by this instance of EntitySelectionProxy.</param>
        public static void SelectEntity(World world, Entity entity)
        {
            if (!EntityExistsAndIsValid(world, entity))
                return;

            Undo.SetCurrentGroupName($"Select {entity.ToString()}");
            CreateInstance(world, entity).Select();
        }

        // Workaround because EntityManager.Exists can potentially throw when used in the editor
        static bool EntityExistsAndIsValid(World world, Entity entity)
        {
            return world is { IsCreated: true }
                   && entity.Index >= 0
#if ENTITY_STORE_V1
                   && (uint)entity.Index < (uint)world.EntityManager.EntityCapacity
#endif
                   && world.EntityManager.Exists(entity);
        }

        [SerializeField] int entityIndex;
        [SerializeField] int entityVersion;

        // Try to remember the world when performing Undo/Redo
        [SerializeField] string worldName;

        int m_RefCount;

        /// <summary>
        /// The <see cref="Unity.Entities.World"/> in which the wrapped <see cref="Unity.Entities.Entity"/> exists.
        /// </summary>
        public World World { get; private set; }

        /// <summary>
        /// The <see cref="Unity.Entities.Entity"/> wrapped by this instance of EntitySelectionProxy.
        /// </summary>
        public Entity Entity => new() { Index = entityIndex, Version = entityVersion };

        /// <summary>
        /// Whether the wrapped <see cref="Unity.Entities.Entity"/> currently exists and is valid.
        /// </summary>
        public bool Exists => World is { IsCreated: true } && World.EntityManager.SafeExists(Entity);

        /// <summary>
        /// <para>
        /// The <see cref="Unity.Entities.EntityContainer"/> allowing the <see cref="Unity.Entities.Entity"/> data to be visited.
        /// </para>
        /// <seealso cref="Unity.Properties.PropertyContainer.Visit"/>
        /// </summary>
        public EntityContainer Container { get; private set; }

        /// <summary>
        /// Sets this instance of EntitySelectionProxy as the active selection.
        /// </summary>
        /// <remarks>
        /// This operation produces no result if the current selection is the same instance or if the current selection is another instance
        /// of EntitySelectionProxy wrapping the same <see cref="Unity.Entities.Entity"/>.
        /// </remarks>
        public void Select()
        {
            // Don't reselect yourself
            if (Selection.activeObject == this)
                return;

            // Don't reselect the same entity
            if (Selection.activeObject is EntitySelectionProxy selectionProxy && selectionProxy.World == World && selectionProxy.Entity == Entity)
                return;

            // Can only be Runtime if directly selected
            SelectionBridge.SetSelection(this, null, DataMode.Runtime);
        }

        internal void Retain() => m_RefCount++;

        internal void Release()
        {
            if (--m_RefCount <= 0)
            {
                var undoGroup = Undo.GetCurrentGroup();
                var undoGroupName = Undo.GetCurrentGroupName();
                Undo.DestroyObjectImmediate(this);
                Undo.SetCurrentGroupName(undoGroupName);
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        internal static bool FindPrimaryEntity(GameObject obj, EntitySelectionProxy proxy, out World world, out Entity entity)
        {
            if (proxy != null && proxy.World != null && proxy.World.IsCreated)
            {
                var proxyEntity = proxy.Entity;
                var proxyEntityManager = proxy.World.EntityManager;

                if (proxyEntityManager.HasComponent<EntityGuid>(proxyEntity))
                {
                    var guid = proxyEntityManager.GetComponentData<EntityGuid>(proxyEntity);

                    if (obj.GetInstanceID() == guid.OriginatingId)
                    {
                        world = proxy.World;
                        entity = proxyEntity;
                        return true;
                    }
                }
            }

            world = World.DefaultGameObjectInjectionWorld;

            if (world == null || !world.IsCreated)
            {
                entity = default;
                return false;
            }

            entity = World.DefaultGameObjectInjectionWorld.EntityManager.Debug.GetPrimaryEntityForAuthoringObject(obj);
            return entity != Entity.Null;
        }

        void Initialize(World world, Entity entity)
        {
            entityIndex = entity.Index;
            entityVersion = entity.Version;
            CreateContext(world);
        }

        void CreateContext(World world)
        {
            if (world == null)
                return;

            World = world;
            Container = new EntityContainer(world.EntityManager, Entity);
        }

        /// <summary>Returns a hash code.</summary>
        public override int GetHashCode()
        {
            // The picking internally keeps track of the hash code of the picked objects for various stuff like picking cycling.
            // It assumes that if two objects have different hash codes, then they are different.
            // It also assumes that if two objects have the same hash code, then they are the same.
            // So we need a GetHashCode function that is decently reliable at doing a 1:1 Entity->Int mapping.

            const uint EntityBitMask = (1 << 28) - 1;
            const uint WorldNumberBitMask = (1 << 4) - 1;

            uint entityIndexU32 = (uint)entityIndex;
            uint worldNumberU32 = World is not { IsCreated: true } ? 0 : (uint)World.SequenceNumber;

            uint entityIndexBits = entityIndexU32 & EntityBitMask;
            uint worldNumberBits = (worldNumberU32 & WorldNumberBitMask) << 28;

            return (int)(worldNumberBits | entityIndexBits);
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() => worldName = World?.Name;

        void ISerializationCallbackReceiver.OnAfterDeserialize() => CreateContext(FindWorldByName(worldName));

        static World FindWorldByName(string worldName)
        {
            if (string.IsNullOrEmpty(worldName))
                return null;

            foreach (var world in World.All)
            {
                if (world.Name == worldName)
                    return world;
            }

            return null;
        }
    }
}
