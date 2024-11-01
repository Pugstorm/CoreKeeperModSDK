using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine.Scripting;
using UnityEngine.TestTools;

[assembly: InternalsVisibleTo("Unity.Entities.Hybrid")]

namespace Unity.Entities
{
    [AttributeUsage(AttributeTargets.Method)]
    internal class MonoPInvokeCallbackAttribute : Attribute
    {
        internal MonoPInvokeCallbackAttribute(Type type) {}
    }

    /// <summary>
    /// The EntityManager manages entities and components in a World.
    /// </summary>
    /// <remarks>
    /// The EntityManager provides an API to create, read, update, and destroy entities.
    ///
    /// A <see cref="World"/> has one EntityManager, which manages all the entities for that World.
    ///
    /// Many EntityManager operations result in *structural changes* that change the layout of entities in memory.
    /// Before it can perform such operations, the EntityManager must wait for all running Jobs to complete, an event
    /// called a *sync point*. A sync point both blocks the main thread and prevents the application from taking
    /// advantage of all available cores as the running Jobs wind down.
    ///
    /// Although you cannot prevent sync points entirely, you should avoid them as much as possible. To this end, the ECS
    /// framework provides the <see cref="EntityCommandBuffer"/>, which allows you to queue structural changes so that
    /// they all occur at one time in the frame.
    /// </remarks>
    [Preserve]
    [NativeContainer]
    [DebuggerTypeProxy(typeof(EntityManagerDebugView))]
    [DebuggerDisplay("{Debugger_GetName}")]
    [GenerateTestsForBurstCompatibility]
    [BurstCompile]
    public unsafe partial struct EntityManager : IEquatable<EntityManager>
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;

        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<EntityManager>();
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        private byte m_IsInExclusiveTransaction;
        private bool IsInExclusiveTransaction => m_IsInExclusiveTransaction != 0;
#endif

        static readonly ProfilerMarker k_ProfileMoveSharedComponents = new ProfilerMarker("MoveSharedComponents");
        static readonly ProfilerMarker k_ProfileMoveManagedComponents = new ProfilerMarker("MoveManagedComponents");
        static readonly ProfilerMarker k_ProfileMoveEntitiesFrom = new ProfilerMarker("EntityManager.MoveEntitiesFrom");

        [NativeDisableUnsafePtrRestriction]
        private EntityDataAccess* m_EntityDataAccess;

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private void AssertIsExclusiveTransaction()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (IsInExclusiveTransaction == !m_EntityDataAccess->IsInExclusiveTransaction)
            {
                if (IsInExclusiveTransaction)
                    throw new InvalidOperationException("EntityManager cannot be used from this context because it is part of an exclusive transaction that has already ended.");

                throw new InvalidOperationException("EntityManager cannot be used from this context because it is not part of the exclusive transaction that is currently active.");
            }
#endif
        }

        internal EntityDataAccess* GetCheckedEntityDataAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            AssertIsExclusiveTransaction();
            return m_EntityDataAccess;
        }

        internal EntityDataAccess* GetCheckedEntityDataAccessExclusive()
        {
            if (!CanBeginExclusiveEntityTransaction())
                EndExclusiveEntityTransaction();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            return m_EntityDataAccess;
        }

        internal EntityDataAccess* GetCheckedEntityDataAccess(SystemHandle system)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            AssertIsExclusiveTransaction();
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (system.m_WorldSeqNo != m_EntityDataAccess->m_WorldUnmanaged.SequenceNumber)
                throw new InvalidOperationException("System is from a different world.");
#endif
            return m_EntityDataAccess;
        }

        internal EntityDataAccess* GetCheckedEntityDataAccessExclusive(SystemHandle system)
        {
            if (!CanBeginExclusiveEntityTransaction())
                EndExclusiveEntityTransaction();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (system.m_WorldSeqNo != m_EntityDataAccess->m_WorldUnmanaged.SequenceNumber)
                throw new InvalidOperationException("System is from a different world.");
#endif
            return m_EntityDataAccess;
        }

        internal EntityDataAccess* GetUncheckedEntityDataAccess()
        {
            return m_EntityDataAccess;
        }

        internal EntityDataAccess* Debugger_GetEntityDataAccess()
        {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!AtomicSafetyHandle.IsHandleValid(m_Safety))
                return null;
            #endif
            return m_EntityDataAccess;
        }

        string Debugger_GetName
        {
            get
            {
                var v = Debugger_GetEntityDataAccess();
                if (v == null)
                    return "Null";
                return v->ManagedEntityDataAccess.World.ToString();
            }
        }

        // Attribute to indicate an EntityManager method makes structural changes.
        // Do not remove from EntityManager and please apply to all appropriate methods.
        [AttributeUsage(AttributeTargets.Method)]
        private class StructuralChangeMethodAttribute : Attribute
        {
        }

        /// <summary>
        /// The <see cref="World"/> of this EntityManager.
        /// </summary>
        /// <value>A World has one EntityManager and an EntityManager manages the entities of one World.</value>
        [ExcludeFromBurstCompatTesting("Returns managed World")]
        public World World => GetCheckedEntityDataAccess()->ManagedEntityDataAccess.World;

        /// <summary>
        /// The <see cref="WorldUnmanaged"/> of this EntityManager.
        /// </summary>
        /// <value>A World has one EntityManager and an EntityManager manages the entities of one World.</value>
        public WorldUnmanaged WorldUnmanaged => GetCheckedEntityDataAccess()->m_WorldUnmanaged;

        /// <summary>
        /// If this version number increases, it means that a structural change has occurred (Entities created / destroyed or components added / removed)
        /// </summary>
        public int EntityOrderVersion => GetCheckedEntityDataAccess()->EntityComponentStore->EntityOrderVersion;

        /// <summary> Obsolete. Use <see cref="EntityOrderVersion"/> instead.</summary>
        [Obsolete("EntityManager.Version has been deprecated. Use EntityOrderVersion instead (UnityUpgradable) -> EntityOrderVersion")]
        public int Version => GetCheckedEntityDataAccess()->EntityComponentStore->EntityOrderVersion;


        /// <summary>
        /// A counter that increments after every system update.
        /// </summary>
        /// <remarks>
        /// The ECS framework uses the GlobalSystemVersion to track changes in a conservative, efficient fashion.
        /// Changes are recorded per component per chunk.
        /// </remarks>
        /// <seealso cref="ArchetypeChunk.DidChange{T}(ref ComponentTypeHandle{T},uint)"/>
        /// <seealso cref="EntityQueryFilter.ChangedFilter"/>
        public uint GlobalSystemVersion => GetCheckedEntityDataAccess()->EntityComponentStore->GlobalSystemVersion;

        /// <summary>
        /// The capacity of the internal entities array.
        /// </summary>
        /// <value>The number of entities the array can hold before it must be resized.</value>
        /// <remarks>
        /// The entities array automatically resizes itself when the entity count approaches the capacity.
        /// You should rarely need to set this value directly.
        ///
        /// **Important:** when you set this value (or when the array automatically resizes), the EntityManager
        /// first ensures that all Jobs finish. This can prevent the Job scheduler from utilizing available CPU
        /// cores and threads, resulting in a temporary performance drop.
        /// </remarks>
#if ENTITY_STORE_V1
        public int EntityCapacity => GetCheckedEntityDataAccess()->EntityComponentStore->EntitiesCapacity;
#endif

        // TODO : this is a temporary workaround for the use of EntityCapacity in remapping
        // NOTE : this only accounts for entities which are actually stored in chunks,
        // not the ones which are merely referenced by chunks (aka external references)
        public int HighestEntityIndex()
        {
            int maxIndex = 0;
            var access = GetCheckedEntityDataAccess();
            var archetypes = access->EntityComponentStore->m_Archetypes;

            for (int archetypeIndex = 0, archetypeCount = archetypes.Length; archetypeIndex < archetypeCount; archetypeIndex++)
            {
                var chunks = archetypes[archetypeIndex]->Chunks;
                for (int chunkIndex = 0, chunkCount = chunks.Count; chunkIndex < chunkCount; chunkIndex++)
                {
                    var chunk = chunks[chunkIndex];
                    var entities = (Entity*)chunk.Buffer;
                    for (int entityIndex = 0, entityCount = chunk.Count; entityIndex < entityCount; entityIndex++)
                    {
                        var index = entities[entityIndex].Index;
                        if (index > maxIndex)
                        {
                            maxIndex = index;
                        }
                    }
                }
            }

            return maxIndex;
        }

        /// <summary>
        /// An EntityQuery instance that matches all normal components and
        /// - includes system entities
        /// - excludes chunk entities
        /// </summary>
        public EntityQuery UniversalQueryWithSystems => GetCheckedEntityDataAccess()->m_UniversalQueryWithSystems;

        /// <summary>
        /// An EntityQuery instance that matches all normal components and
        /// - excludes system entities
        /// - excludes chunk entities
        /// </summary>
        public EntityQuery UniversalQuery => GetCheckedEntityDataAccess()->m_UniversalQuery;

        /// <summary>
        /// An object providing debugging information and operations.
        /// </summary>
        public EntityManagerDebug Debug => new EntityManagerDebug(this);

        /// <summary>
        /// The total reserved address space for all Chunks in all Worlds.
        /// </summary>
        public static ulong TotalChunkAddressSpaceInBytes
        {
            get => Entities.EntityComponentStore.TotalChunkAddressSpaceInBytes;
            set => Entities.EntityComponentStore.TotalChunkAddressSpaceInBytes = value;
        }

        [ExcludeFromBurstCompatTesting("Takes managed World")]
        internal void Initialize(World world)
        {
            TypeManager.Initialize();
            ECBInterop.Initialize();
            EntityQueryImpl.Initialize();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            ComponentSafetyHandles.Initialize();
#endif

            // Pick any recorded types that have come in after a domain reload.
            EarlyInitHelpers.FlushEarlyInits();

            SystemBaseRegistry.InitializePendingTypes();

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            m_IsInExclusiveTransaction = 0;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = AtomicSafetyHandle.Create();

            CollectionHelper.SetStaticSafetyId(ref m_Safety, ref s_staticSafetyId.Data, "Unity.Entities.EntityManager");

            // Install a panic function, which helps with generating good error messages blaming the correct system when things go wrong.
            // The way this works it that if a schedule or atomic safety handle validation fails in the native code, it will
            // call out to a user panic function, which can try to log some more information and then the native code will retry the operation.
            // Here we figure out if some other system was to blame, and if so we synchronize all outstanding jobs to make the downstream
            // systems keep running.
            if (JobsUtility.PanicFunction == null)
            {
                JobsUtility.PanicFunction = () =>
                {
                    SystemState* state = SystemState.GetCurrentSystemFromJobDebugger();
                    if (state != null)
                    {
                        state->LogSafetyErrors();
                    }
                };
            }
#endif
            m_EntityDataAccess = (EntityDataAccess*)Memory.Unmanaged.Allocate(sizeof(EntityDataAccess), 16, Allocator.Persistent);
            UnsafeUtility.MemClear(m_EntityDataAccess, sizeof(EntityDataAccess));
            EntityDataAccess.Initialize(m_EntityDataAccess, world);
        }

        internal void PreDisposeCheck()
        {
            EndExclusiveEntityTransaction();
            GetCheckedEntityDataAccess()->DependencyManager->PreDisposeCheck();

        }

        [ExcludeFromBurstCompatTesting("Disposes managed lists")]
        internal void DestroyInstance()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif

            PreDisposeCheck();

            GetCheckedEntityDataAccess()->Dispose();
            Memory.Unmanaged.Free(m_EntityDataAccess, Allocator.Persistent);
            m_EntityDataAccess = null;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(m_Safety);
            m_Safety = default;
#endif
        }

        internal static EntityManager CreateEntityManagerInUninitializedState()
        {
            return new EntityManager();
        }

        /// <summary>
        /// Reports whether this EntityManager references the same manager as another instance.
        /// </summary>
        /// <param name="other">The object to compare.</param>
        /// <returns>True, if the <paramref name="other"/> parameter is an EntityManager instance that points to the same
        /// instance as this.</returns>
        public bool Equals(EntityManager other)
        {
            return m_EntityDataAccess == other.m_EntityDataAccess;
        }

        /// <summary>
        /// Reports whether this EntityManager references the same manager as another object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True, if the <paramref name="obj"/> parameter is an EntityManager instance that points to the same
        /// instance as this.</returns>
        [ExcludeFromBurstCompatTesting("Takes managed object")]
        public override bool Equals(object obj)
        {
            return obj is EntityManager other && Equals(other);
        }

        /// <summary>
        /// Computes a hash code for this object
        /// </summary>
        /// <returns>The hash code</returns>
        public override int GetHashCode()
        {
            return unchecked((int)(long)m_EntityDataAccess);
        }

        /// <summary>
        /// Compare two EntityManager objects for equality
        /// </summary>
        /// <param name="lhs">The left EntityManager instance</param>
        /// <param name="rhs">The right EntityManager instnace</param>
        /// <returns>True if the two instances are equal, or false if not.</returns>
        public static bool operator==(EntityManager lhs, EntityManager rhs)
        {
            return lhs.m_EntityDataAccess == rhs.m_EntityDataAccess;
        }

        /// <summary>
        /// Compare two EntityManager objects for inequality
        /// </summary>
        /// <param name="lhs">The left EntityManager instance</param>
        /// <param name="rhs">The right EntityManager instnace</param>
        /// <returns>False if the two instances are equal, or true if not.</returns>
        public static bool operator!=(EntityManager lhs, EntityManager rhs)
        {
            return lhs.m_EntityDataAccess != rhs.m_EntityDataAccess;
        }

        // ----------------------------------------------------------------------------------------------------------
        // PUBLIC
        // ----------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Gets the value of a component for an entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <typeparam name="T">The type of component to retrieve.</typeparam>
        /// <returns>A struct of type T containing the component value.</returns>
        /// <exception cref="ArgumentException">Thrown if the component type has no fields.</exception>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public T GetComponentData<T>(Entity entity) where T : unmanaged, IComponentData
        {
            var access = GetCheckedEntityDataAccess();
            return access->GetComponentData<T>(entity);
        }

        /// <summary>
        /// Gets the value of a component for an entity associated with a system.
        /// </summary>
        /// <param name="system">The system handle.</param>
        /// <typeparam name="T">The type of component to retrieve.</typeparam>
        /// <returns>A struct of type T containing the component value.</returns>
        /// <exception cref="ArgumentException">Thrown if the component type has no fields.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the system isn't from thie world.</exception>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public T GetComponentData<T>(SystemHandle system) where T : unmanaged, IComponentData
        {
            var access = GetCheckedEntityDataAccess(system);
            return access->GetComponentData<T>(system.m_Entity);
        }

        /// <summary>
        /// Gets the value of a component for an entity associated with a system.
        /// </summary>
        /// <param name="system">The system handle.</param>
        /// <typeparam name="T">The type of component to retrieve.</typeparam>
        /// <returns>A <see cref="RefRW{T}"/> struct of type T containing access to the component value.</returns>
        /// <exception cref="ArgumentException">Thrown if the component type has no fields.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the system isn't from thie world.</exception>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public RefRW<T> GetComponentDataRW<T>(SystemHandle system) where T : unmanaged, IComponentData
        {
            var access = GetCheckedEntityDataAccess(system);

            var typeIndex = TypeManager.GetTypeIndex<T>();
            var data = access->GetComponentDataRW_AsBytePointer(system.m_Entity, typeIndex);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new RefRW<T>(data, access->DependencyManager->Safety.GetSafetyHandle(typeIndex, false));
#else
            return new RefRW<T>(data);
#endif
        }

        /// <summary>
        /// Sets the value of a component of an entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="componentData">The data to set.</param>
        /// <typeparam name="T">The component type.</typeparam>
        /// <exception cref="ArgumentException">Thrown if the component type has no fields.</exception>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public void SetComponentData<T>(Entity entity, T componentData) where T : unmanaged, IComponentData
        {
            var access = GetCheckedEntityDataAccess();
            access->SetComponentData(entity, componentData);
        }

        /// <summary>
        /// Sets the value of a component of an entity associated with a system.
        /// </summary>
        /// <param name="system">The system handle.</param>
        /// <param name="componentData">The data to set.</param>
        /// <typeparam name="T">The component type.</typeparam>
        /// <exception cref="ArgumentException">Thrown if the component type has no fields.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the system isn't from thie world.</exception>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public void SetComponentData<T>(SystemHandle system, T componentData) where T : unmanaged, IComponentData
        {
            var access = GetCheckedEntityDataAccess(system);
            access->SetComponentData(system.m_Entity, componentData);
        }

        /// <summary>
        /// Gets the value of a chunk component.
        /// </summary>
        /// <remarks>
        /// A chunk component is common to all entities in a chunk. You can access a chunk <see cref="IComponentData"/>
        /// instance through either the chunk itself or through an entity stored in that chunk.
        /// </remarks>
        /// <param name="chunk">The chunk.</param>
        /// <typeparam name="T">The component type.</typeparam>
        /// <returns>A struct of type T containing the component value.</returns>
        /// <exception cref="ArgumentException">Thrown if the ArchetypeChunk object is invalid.</exception>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public T GetChunkComponentData<T>(ArchetypeChunk chunk) where T : unmanaged, IComponentData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (chunk.Invalid())
            {
                var typeName = TypeManager.GetTypeIndex<T>().ToFixedString();
                throw new System.ArgumentException(
                    $"GetChunkComponentData<{typeName}> can not be called with an invalid archetype chunk.");
            }
#endif
            var metaChunkEntity = chunk.m_Chunk.MetaChunkEntity;
            return GetComponentData<T>(metaChunkEntity);
        }

        /// <summary>
        /// Gets the value of chunk component for the chunk containing the specified entity.
        /// </summary>
        /// <remarks>
        /// A chunk component is common to all entities in a chunk. You can access a chunk <see cref="IComponentData"/>
        /// instance through either the chunk itself or through an entity stored in that chunk.
        /// </remarks>
        /// <param name="entity">The entity.</param>
        /// <typeparam name="T">The component type.</typeparam>
        /// <returns>A struct of type T containing the component value.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public T GetChunkComponentData<T>(Entity entity) where T : unmanaged, IComponentData
        {
            var access = GetCheckedEntityDataAccess();
            var store = access->EntityComponentStore;
            store->AssertEntitiesExist(&entity, 1);
            var chunk = store->GetChunk(entity);
            var metaChunkEntity = chunk.MetaChunkEntity;
            return access->GetComponentData<T>(metaChunkEntity);
        }

        /// <summary>
        /// Sets the value of a chunk component.
        /// </summary>
        /// <remarks>
        /// A chunk component is common to all entities in a chunk. You can access a chunk <see cref="IComponentData"/>
        /// instance through either the chunk itself or through an entity stored in that chunk.
        /// </remarks>
        /// <param name="chunk">The chunk to modify.</param>
        /// <param name="componentValue">The component data to set.</param>
        /// <typeparam name="T">The component type.</typeparam>
        /// <exception cref="ArgumentException">Thrown if the ArchetypeChunk object is invalid.</exception>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public void SetChunkComponentData<T>(ArchetypeChunk chunk, T componentValue) where T : unmanaged, IComponentData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (chunk.Invalid())
            {
                var typeName = TypeManager.GetTypeIndex<T>().ToFixedString();
                throw new System.ArgumentException(
                    $"SetChunkComponentData<{typeName}> can not be called with an invalid archetype chunk.");
            }
#endif
            var metaChunkEntity = chunk.m_Chunk.MetaChunkEntity;
            SetComponentData<T>(metaChunkEntity, componentValue);
        }

        /// <summary>
        /// Gets the managed [UnityEngine.Component](https://docs.unity3d.com/ScriptReference/Component.html) object
        /// from an entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <typeparam name="T">The type of the managed object.</typeparam>
        /// <returns>The managed object, cast to type T.</returns>
        /// <remarks>
        /// Accessing data in a managed object forfeits many opportunities for increased performance. Using
        /// managed objects should be avoided or used sparingly.
        ///
        /// The method also works for adding managed objects implementing `IComponentData`, but `GetComponentData` is the preferred method for those objects.
        /// </remarks>
        [ExcludeFromBurstCompatTesting("Returns managed object")]
        public T GetComponentObject<T>(Entity entity)
        {
            var access = GetCheckedEntityDataAccess();
            return access->GetComponentObject<T>(entity, ComponentType.ReadWrite<T>());
        }

        /// <summary>
        /// Gets the managed [UnityEngine.Component](https://docs.unity3d.com/ScriptReference/Component.html) object
        /// from an entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="componentType">The type of the managed object.</param>
        /// <typeparam name="T">The return type of the managed object.</typeparam>
        /// <returns>The managed object, cast to type T.</returns>
        /// <remarks>
        /// Accessing data in a managed object forfeits many opportunities for increased performance. Using
        /// managed objects should be avoided or used sparingly.
        ///
        /// The method also works for adding managed objects implementing `IComponentData`, but `GetComponentData` is the preferred method for those objects.
        /// </remarks>
        [ExcludeFromBurstCompatTesting("Returns managed object")]
        public T GetComponentObject<T>(Entity entity, ComponentType componentType)
        {
            var access = GetCheckedEntityDataAccess();
            return access->GetComponentObject<T>(entity, componentType);
        }

        /// <summary>
        /// Gets the managed [UnityEngine.Component](https://docs.unity3d.com/ScriptReference/Component.html) object
        /// from an entity associated with a system.
        /// </summary>
        /// <param name="system">The system handle.</param>
        /// <typeparam name="T">The type of the managed object.</typeparam>
        /// <returns>The managed object, cast to type T.</returns>
        /// <remarks>
        /// Accessing data in a managed object forfeits many opportunities for increased performance. Using
        /// managed objects should be avoided or used sparingly.
        ///
        /// The method also works for adding managed objects implementing `IComponentData`, but `GetComponentData` is the preferred method for those objects.
        /// </remarks>
        [ExcludeFromBurstCompatTesting("Returns managed object")]
        public T GetComponentObject<T>(SystemHandle system)
        {
            var access = GetCheckedEntityDataAccess(system);
            return access->GetComponentObject<T>(system.m_Entity, ComponentType.ReadWrite<T>());
        }

        /// <summary>
        /// Gets the managed [UnityEngine.Component](https://docs.unity3d.com/ScriptReference/Component.html) object
        /// from an entity associated with a system.
        /// </summary>
        /// <param name="system">The system handle.</param>
        /// <param name="componentType">The type of the managed object.</param>
        /// <typeparam name="T">The return type of the managed object.</typeparam>
        /// <returns>The managed object, cast to type T.</returns>
        /// <remarks>
        /// Accessing data in a managed object forfeits many opportunities for increased performance. Using
        /// managed objects should be avoided or used sparingly.
        ///
        /// The method also works for adding managed objects implementing `IComponentData`, but `GetComponentData` is the preferred method for those objects.
        /// </remarks>
        [ExcludeFromBurstCompatTesting("Returns managed object")]
        public T GetComponentObject<T>(SystemHandle system, ComponentType componentType)
        {
            var access = GetCheckedEntityDataAccess(system);
            return access->GetComponentObject<T>(system.m_Entity, componentType);
        }

        #region Shared Component public APIs

        /// <summary>
        /// Gets the number of shared components managed by this EntityManager.
        /// </summary>
        /// <returns>The shared component count</returns>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public int GetSharedComponentCount()
        {
            var access = GetCheckedEntityDataAccess();
            return access->GetSharedComponentCount();
        }

        /// <summary> Obsolete. Use <see cref="SetSharedComponentManaged{T}(Unity.Entities.Entity,T)"/> instead.</summary>
        /// <param name="entity">The entity</param>
        /// <param name="componentData">A shared component object containing the values to set.</param>
        /// <typeparam name="T">The shared component type.</typeparam>
        [Obsolete("Use SetSharedComponentManaged<T> (UnityUpgradable) -> SetSharedComponentManaged<T>(*)", true)]
        public void SetSharedComponentData<T>(Entity entity, T componentData) where T : struct, ISharedComponentData
        {
        }

        /// <summary>
        /// Sets the shared component of an entity.
        /// </summary>
        /// <remarks>
        /// Changing a shared component value of an entity results in the entity being moved to a
        /// different chunk. The entity moves to a chunk with other entities that have the same shared component values.
        /// A new chunk is created if no chunk with the same archetype and shared component values currently exists.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before setting the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="entity">The entity</param>
        /// <param name="componentData">A shared component object containing the values to set.</param>
        /// <typeparam name="T">The shared component type.</typeparam>
        [StructuralChangeMethod]
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void SetSharedComponentManaged<T>(Entity entity, T componentData) where T : struct, ISharedComponentData
        {
            var access = GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            var ti = TypeManager.GetTypeIndex<T>();

            if (TypeManager.IsManagedSharedComponent(ti))
            {
                access->SetSharedComponentData_Managed(entity, componentData);
            }
            else
            {
                var defaultValue = default(T);
                access->SetSharedComponentData_Unmanaged(entity, ti, UnsafeUtility.AddressOf(ref componentData), UnsafeUtility.AddressOf(ref defaultValue));
            }
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Sets the shared component on an array of entities
        /// </summary>
        /// <remarks>
        /// Changing a shared component value of an entity results in the entity being moved to a
        /// different chunk. The entity moves to a chunk with other entities that have the same shared component values.
        /// A new chunk is created if no chunk with the same archetype and shared component values currently exists.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before setting the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="entities">The target entities</param>
        /// <param name="componentData">A shared component object containing the values to set.</param>
        /// <typeparam name="T">The shared component type.</typeparam>
        [StructuralChangeMethod]
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void SetSharedComponentManaged<T>(NativeArray<Entity> entities, T componentData) where T : struct, ISharedComponentData
        {
            var access = GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            var ti = TypeManager.GetTypeIndex<T>();

            if (TypeManager.IsManagedSharedComponent(ti))
            {
                access->SetSharedComponentData_Managed(entities, componentData);
            }
            else
            {
                var defaultValue = default(T);
                access->SetSharedComponentData_Unmanaged(entities, ti, UnsafeUtility.AddressOf(ref componentData), UnsafeUtility.AddressOf(ref defaultValue));
            }
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Sets the shared component of all entities in the query.
        /// </summary>
        /// <remarks>
        /// The component data stays in the same chunk, the internal shared component data indices will be adjusted.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before setting the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="query">The query where matching entities will be assigned the shared component to.</param>
        /// <param name="componentData">A shared component object containing the values to set.</param>
        /// <typeparam name="T">The shared component type.</typeparam>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleSharedComponentData) })]
        public void SetSharedComponent<T>(EntityQuery query, T componentData) where T : unmanaged, ISharedComponentData
        {
            var access = GetCheckedEntityDataAccess();
            access->AssertQueryIsValid(query);
            var queryImpl = query._GetImpl();
            if (queryImpl->IsEmptyIgnoreFilter)
                return;
            var changes = access->BeginStructuralChanges();
            var newSharedComponentDataIndex = access->InsertSharedComponent_Unmanaged(componentData);
            access->SetSharedComponentDataOnQueryDuringStructuralChange_Unmanaged(queryImpl, newSharedComponentDataIndex,
                ComponentType.ReadWrite<T>(), UnsafeUtility.AddressOf(ref componentData));
            access->EndStructuralChanges(ref changes);
        }
        /// <summary> Obsolete. Use <see cref="SetSharedComponentManaged{T}(Unity.Entities.Entity,T)"/> instead.</summary>
        /// <param name="query">The query where matching entities will be assigned the shared component to.</param>
        /// <param name="componentData">A shared component object containing the values to set.</param>
        /// <typeparam name="T">The shared component type.</typeparam>
        [Obsolete("Use SetSharedComponentManaged<T> (UnityUpgradable) -> SetSharedComponentManaged<T>(*)", true)]
        public void SetSharedComponentData<T>(EntityQuery query, T componentData) where T : struct, ISharedComponentData
        {}
        /// <summary>
        /// Sets the shared component of all entities in the query.
        /// </summary>
        /// <remarks>
        /// The component data stays in the same chunk, the internal shared component data indices will be adjusted.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before setting the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="query">The query where matching entities will be assigned the shared component to.</param>
        /// <param name="componentData">A shared component object containing the values to set.</param>
        /// <typeparam name="T">The shared component type.</typeparam>
        [StructuralChangeMethod]
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void SetSharedComponentManaged<T>(EntityQuery query, T componentData) where T : struct, ISharedComponentData
        {
            var access = GetCheckedEntityDataAccess();
            access->AssertQueryIsValid(query);
            var queryImpl = query._GetImpl();
            if (queryImpl->IsEmptyIgnoreFilter)
                return;
            var componentType = ComponentType.ReadWrite<T>();
            var changes = access->BeginStructuralChanges();
            var newSharedComponentDataIndex = access->InsertSharedComponent(componentData);
            if (TypeManager.IsManagedSharedComponent(componentType.TypeIndex))
            {
                access->SetSharedComponentDataOnQueryDuringStructuralChange(queryImpl, newSharedComponentDataIndex,
                    componentType);
            }
            else
            {
                access->SetSharedComponentDataOnQueryDuringStructuralChange_Unmanaged(queryImpl, newSharedComponentDataIndex,
                    componentType, UnsafeUtility.AddressOf(ref componentData));
            }
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Gets a shared component from an entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <typeparam name="T">The type of shared component.</typeparam>
        /// <returns>A copy of the shared component.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleSharedComponentData) })]
        public T GetSharedComponent<T>(Entity entity) where T : unmanaged, ISharedComponentData
        {
            var access = GetCheckedEntityDataAccess();
            return access->GetSharedComponentData_Unmanaged<T>(entity);
        }

        /// <summary> Obsolete. Use <see cref="GetSharedComponent{T}(Unity.Entities.Entity)"/> instead.</summary>
        /// <param name="entity">The target entity</param>
        /// <typeparam name="T">The type of the unmanaged shared component to look up on the target entity</typeparam>
        /// <returns>The index of the target entity's value for the shared component of type <typeparamref name="T"/>.</returns>
        [Obsolete("Use GetSharedComponent<T> (UnityUpgradable) -> GetSharedComponent<T>(*)", true)]
        public T GetUnmanagedSharedComponentData<T>(Entity entity) where T : unmanaged, ISharedComponentData
        {
            return default;
        }

        /// <summary>
        /// Retrieves the index of the managed or unmanaged shared component for an entity.
        /// </summary>
        /// <param name="entity">The target entity</param>
        /// <typeparam name="T">The type of the shared component to look up on the target entity</typeparam>
        /// <returns>The index of the target entity's value for the shared component of type <typeparamref name="T"/>.</returns>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public int GetSharedComponentIndexManaged<T>(Entity entity) where T : struct, ISharedComponentData
        {
            var ecs = GetCheckedEntityDataAccess()->EntityComponentStore;
            var typeIndex = TypeManager.GetTypeIndex<T>();
            ecs->AssertEntityHasComponent(entity, typeIndex);
            return ecs->GetSharedComponentDataIndex(entity, typeIndex);
        }

        /// <summary>
        /// Retrieves the index of the unmanaged shared component for an entity.
        /// </summary>
        /// <param name="entity">The target entity</param>
        /// <typeparam name="T">The type of the unmanaged shared component to look up on the target entity</typeparam>
        /// <returns>The index of the target entity's value for the shared component of type <typeparamref name="T"/>.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleSharedComponentData) })]
        public int GetSharedComponentIndex<T>(Entity entity) where T : unmanaged, ISharedComponentData
        {
            var ecs = GetCheckedEntityDataAccess()->EntityComponentStore;
            var typeIndex = TypeManager.GetTypeIndex<T>();
            ecs->AssertEntityHasComponent(entity, typeIndex);
            return ecs->GetSharedComponentDataIndex(entity, typeIndex);
        }

        /// <summary>
        /// Retrieves the index of the unmanaged shared component data value for an entity.
        /// </summary>
        /// <param name="entity">The target entity</param>
        /// <typeparam name="T">The type of the unmanaged shared component to look up on the target entity</typeparam>
        /// <returns>The index of the target entity's value for the shared component of type <typeparamref name="T"/>.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleSharedComponentData) })]
        [Obsolete("Use GetSharedComponentIndex<T> instead. (RemovedAfter Entities 1.0) (UnityUpgradable) -> GetSharedComponentIndex<T>(*)", true)]
        public int GetSharedComponentDataIndex<T>(Entity entity) where T : unmanaged, ISharedComponentData
        {
            return GetSharedComponentIndex<T>(entity);
        }

        /// <summary> Obsolete. Use <see cref="GetSharedComponentIndex{T}"/> instead.</summary>
        /// <param name="entity">The target entity</param>
        /// <typeparam name="T">The type of the unmanaged shared component to look up on the target entity</typeparam>
        /// <returns>The index of the target entity's value for the shared component of type <typeparamref name="T"/>.</returns>
        [Obsolete("Use GetSharedComponentIndex<T> (UnityUpgradable) -> GetSharedComponentIndex<T>(*)", true)]
        public int GetUnmanagedSharedComponentDataIndex<T>(Entity entity) where T : unmanaged, ISharedComponentData
        {
            return -1;
        }

        /// <summary>
        /// Gets a shared component by index.
        /// </summary>
        /// <remarks>
        /// The ECS framework maintains an internal list of unique shared components. You can get the components in this
        /// list, along with their indices using
        /// <see cref="GetAllUniqueSharedComponentsManaged{T}(System.Collections.Generic.List{T},System.Collections.Generic.List{int})"/>. An
        /// index in the list is valid and points to the same shared component index as long as the shared component
        /// order version from <see cref="GetSharedComponentOrderVersion{T}(T)"/> remains the same.
        /// </remarks>
        /// <param name="sharedComponentIndex">The index of the shared component in the internal shared component
        /// list.</param>
        /// <typeparam name="T">The data type of the shared component.</typeparam>
        /// <returns>A copy of the shared component.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleSharedComponentData) })]
        public T GetSharedComponent<T>(int sharedComponentIndex) where T : unmanaged, ISharedComponentData
        {
            var access = GetCheckedEntityDataAccess();
            return access->GetSharedComponentData_Unmanaged<T>(sharedComponentIndex);
        }

        /// <summary> Obsolete. Use <see cref="GetSharedComponent{T}(int)"/> instead.</summary>
        /// <param name="sharedComponentIndex">The index of the shared component in the internal shared component
        /// list.</param>
        /// <typeparam name="T">The data type of the shared component.</typeparam>
        /// <returns>A copy of the shared component.</returns>
        [Obsolete("Use GetSharedComponent<T> (UnityUpgradable) -> GetSharedComponent<T>(*)", true)]
        public T GetUnmanagedSharedComponentData<T>(int sharedComponentIndex) where T : unmanaged, ISharedComponentData
        {
            return default;
        }

        /// <summary> Obsolete. Use <see cref="SetSharedComponent{T}(Entity,T)"/> instead.</summary>
        /// <param name="entity">The entity</param>
        /// <param name="componentData">An unmanaged shared component object containing the values to set.</param>
        /// <typeparam name="T">The shared component type.</typeparam>
        [Obsolete("Use SetSharedComponent<T> (UnityUpgradable) -> SetSharedComponent<T>(*)", true)]
        public void SetUnmanagedSharedComponentData<T>(Entity entity, T componentData)
            where T : unmanaged, ISharedComponentData
        {
        }

        /// <summary>
        /// Sets the shared component of an entity.
        /// </summary>
        /// <remarks>
        /// Changing a shared component value of an entity results in the entity being moved to a
        /// different chunk. The entity moves to a chunk with other entities that have the same shared component values.
        /// A new chunk is created if no chunk with the same archetype and shared component values currently exists.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before setting the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="entity">The entity</param>
        /// <param name="componentData">An unmanaged shared component object containing the values to set.</param>
        /// <typeparam name="T">The shared component type.</typeparam>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleSharedComponentData) })]
        public void SetSharedComponent<T>(Entity entity, T componentData) where T : unmanaged, ISharedComponentData
        {
            var access = GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            var ti = TypeManager.GetTypeIndex<T>();
            Assert.IsFalse(TypeManager.IsManagedSharedComponent(ti));
            var defaultValue = default(T);
            access->SetSharedComponentData_Unmanaged(entity, ti, UnsafeUtility.AddressOf(ref componentData), UnsafeUtility.AddressOf(ref defaultValue));
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Sets the shared component value on an array of entities
        /// </summary>
        /// <remarks>
        /// Changing a shared component value of an entity results in the entity being moved to a
        /// different chunk. The entity moves to a chunk with other entities that have the same shared component values.
        /// A new chunk is created if no chunk with the same archetype and shared component values currently exists.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before setting the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="entities">The target entities</param>
        /// <param name="componentData">An unmanaged shared component object containing the values to set.</param>
        /// <typeparam name="T">The shared component type.</typeparam>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleSharedComponentData) })]
        public void SetSharedComponent<T>(NativeArray<Entity> entities, T componentData) where T : unmanaged, ISharedComponentData
        {
            var access = GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            var ti = TypeManager.GetTypeIndex<T>();
            Assert.IsFalse(TypeManager.IsManagedSharedComponent(ti));
            var defaultValue = default(T);
            access->SetSharedComponentData_Unmanaged(entities, ti, UnsafeUtility.AddressOf(ref componentData), UnsafeUtility.AddressOf(ref defaultValue));
            access->EndStructuralChanges(ref changes);
        }

        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleSharedComponentData) })]
        internal void SetSharedComponentNonDefault(Entity entity, void* componentDataAddr, TypeIndex typeIndex)
        {
            var access = GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            Assert.IsFalse(TypeManager.IsManagedSharedComponent(typeIndex));
            access->SetSharedComponentData_Unmanaged(entity, typeIndex, componentDataAddr, null);
            access->EndStructuralChanges(ref changes);
        }

        /// <summary> Obsolete. Use <see cref="GetSharedComponentManaged{T}(Unity.Entities.Entity)"/> instead.</summary>
        /// <param name="entity">The entity.</param>
        /// <typeparam name="T">The type of shared component.</typeparam>
        /// <returns>A copy of the shared component.</returns>
        [Obsolete("Use GetSharedComponentManaged<T> (UnityUpgradable) -> GetSharedComponentManaged<T>(*)", true)]
        public T GetSharedComponentData<T>(Entity entity) where T : struct, ISharedComponentData
        {
            return default;
        }


        /// <summary>
        /// Gets a shared component from an entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <typeparam name="T">The type of shared component.</typeparam>
        /// <returns>A copy of the shared component.</returns>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public T GetSharedComponentManaged<T>(Entity entity) where T : struct, ISharedComponentData
        {
            var access = GetCheckedEntityDataAccess();
            return access->GetSharedComponentData<T>(entity);
        }

        /// <summary> Obsolete. Use <see cref="GetSharedComponentManaged{T}(int)"/> instead.</summary>
        /// <param name="sharedComponentIndex">The index of the shared component in the internal shared component
        /// list.</param>
        /// <typeparam name="T">The data type of the shared component.</typeparam>
        /// <returns>A copy of the shared component.</returns>
        [Obsolete("Use GetSharedComponentManaged<T> (UnityUpgradable) -> GetSharedComponentManaged<T>(*)", true)]
        public T GetSharedComponentData<T>(int sharedComponentIndex) where T : struct, ISharedComponentData
        {
            return default;
        }


        /// <summary>
        /// Gets a shared component by index.
        /// </summary>
        /// <remarks>
        /// The ECS framework maintains an internal list of unique shared components. You can get the components in this
        /// list, along with their indices using
        /// <see cref="GetAllUniqueSharedComponentsManaged{T}(System.Collections.Generic.List{T},System.Collections.Generic.List{int})"/>. An
        /// index in the list is valid and points to the same shared component index as long as the shared component
        /// order version from <see cref="GetSharedComponentOrderVersion{T}(T)"/> remains the same.
        /// </remarks>
        /// <param name="sharedComponentIndex">The index of the shared component in the internal shared component
        /// list.</param>
        /// <typeparam name="T">The data type of the shared component.</typeparam>
        /// <returns>A copy of the shared component.</returns>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public T GetSharedComponentManaged<T>(int sharedComponentIndex) where T : struct, ISharedComponentData
        {
            var access = GetCheckedEntityDataAccess();
            if (EntityComponentStore.IsUnmanagedSharedComponentIndex(sharedComponentIndex))
            {
                T data = default(T);
                access->EntityComponentStore->GetSharedComponentData_Unmanaged(sharedComponentIndex, TypeManager.GetTypeIndex<T>(), UnsafeUtility.AddressOf(ref data));
                return data;
            }

            return access->ManagedComponentStore.GetSharedComponentData_Managed<T>(sharedComponentIndex);
        }

        /// <summary>
        /// Retrieves a shared component value as a managed boxed object.
        /// </summary>
        /// <remarks>This API is for debugging or performance-insensitive purposes.</remarks>
        /// <param name="sharedComponentIndex">The shared component index</param>
        /// <param name="typeIndex">The type index</param>
        /// <returns>The shared component value, boxed into an object.</returns>
        /// <seealso cref="GetSharedComponent{T}(Unity.Entities.Entity)"/>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public object GetSharedComponentDataBoxed(int sharedComponentIndex, TypeIndex typeIndex)
        {
            var access = GetCheckedEntityDataAccess();
            return access->GetSharedComponentDataBoxed(sharedComponentIndex, typeIndex);
        }

        /// <summary> Obsolete. Use <see cref="GetAllUniqueSharedComponentsManaged{T}(System.Collections.Generic.List{T})"/> instead.</summary>
        /// <param name="sharedComponentValues">A List&lt;T&gt; object to receive the unique instances of the
        /// shared component of type T.</param>
        /// <typeparam name="T">The type of shared component.</typeparam>
        [Obsolete("Use GetAllUniqueSharedComponentsManaged<T> (UnityUpgradable) -> GetAllUniqueSharedComponentsManaged<T>(*)", true)]
        public void GetAllUniqueSharedComponentData<T>(List<T> sharedComponentValues) where T : struct, ISharedComponentData
        {}


        /// <summary>
        /// Gets a list of all the unique instances of a shared component type.
        /// </summary>
        /// <remarks>
        /// All entities with the same archetype and the same values for a shared component are stored in the same set
        /// of chunks. This function finds the unique shared components existing across chunks and archetype and
        /// fills a list with copies of those components.
        /// Note that the first element of the output list will always be the default value for <typeparamref name="T"/>,
        /// even if no entities or chunks currently use that value.
        /// </remarks>
        /// <param name="sharedComponentValues">A List&lt;T&gt; object to receive the unique instances of the
        /// shared component of type T.</param>
        /// <typeparam name="T">The type of shared component.</typeparam>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void GetAllUniqueSharedComponentsManaged<T>(List<T> sharedComponentValues)
            where T : struct, ISharedComponentData
        {
            var access = GetCheckedEntityDataAccess();
            access->GetAllUniqueSharedComponents(sharedComponentValues);
        }

        /// <summary> Obsolete. Use <see cref="GetAllUniqueSharedComponentsManaged{T}(List{T},List{int})"/> instead.</summary>
        /// <param name="sharedComponentValues">A List&lt;T&gt; object to receive the unique instances of the
        /// shared component of type T.</param>
        /// <param name="sharedComponentIndices">A List&lt;int&gt; object to receive the unique instances of the
        /// shared components' indices.</param>
        /// <typeparam name="T">The type of shared component.</typeparam>
        [Obsolete(
            "Use GetAllUniqueSharedComponentsManaged<T> (UnityUpgradable) -> GetAllUniqueSharedComponentsManaged<T>(*)",
            true)]
        public void GetAllUniqueSharedComponentData<T>(List<T> sharedComponentValues, List<int> sharedComponentIndices) where T : struct, ISharedComponentData
        {
        }


        /// <summary>
        /// Gets a list of all unique shared components of the same type and a corresponding list of indices into the
        /// internal shared component list.
        /// </summary>
        /// <remarks>
        /// All entities with the same archetype and the same values for a shared component are stored in the same set
        /// of chunks. This function finds the unique shared components existing across chunks and archetype and
        /// fills a list with copies of those components and fills in a separate list with the indices of those components
        /// in the internal shared component list. You can use the indices to access the same shared components directly
        /// by calling <see cref="GetSharedComponentManaged{T}(int)"/>, passing in the index. An index remains valid until
        /// the shared component order version changes. Check this version using
        /// <see cref="GetSharedComponentOrderVersion{T}(T)"/>.
        /// </remarks>
        /// <param name="sharedComponentValues">A List&lt;T&gt; object to receive the unique instances of the
        /// shared component of type T.</param>
        /// <param name="sharedComponentIndices">A List&lt;int&gt; object to receive the unique instances of the
        /// shared components' indices.</param>
        /// <typeparam name="T">The type of shared component.</typeparam>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void GetAllUniqueSharedComponentsManaged<T>(List<T> sharedComponentValues, List<int> sharedComponentIndices)
            where T : struct, ISharedComponentData
        {
            var access = GetCheckedEntityDataAccess();
            access->GetAllUniqueSharedComponents(sharedComponentValues, sharedComponentIndices);
        }

        /// <summary> Obsolete. Use <see cref="GetAllUniqueSharedComponentsManaged{T}(List{T},List{int},List{int})"/> instead.</summary>
        /// <param name="sharedComponentValues">A List&lt;T&gt; object to receive the unique instances of the
        /// shared component of type T.</param>
        /// <param name="sharedComponentIndices">A List&lt;int&gt; object to receive the unique instances of the
        /// shared components' indices.</param>
        /// <param name="sharedComponentVersions">A List&lt;int&gt; object to receive the unique instances of the
        /// shared components' versions.</param>
        /// <typeparam name="T">The type of shared component.</typeparam>
        [Obsolete(
            "Use GetAllUniqueSharedComponentsManaged<T> (UnityUpgradable) -> GetAllUniqueSharedComponentsManaged<T>(*)",
            true)]
        public void GetAllUniqueSharedComponentData<T>(
            List<T> sharedComponentValues,
            List<int> sharedComponentIndices,
            List<int> sharedComponentVersions)
        where T : struct, ISharedComponentData
        {
        }


        /// <summary>
        /// Gets a list of all unique shared components of the same type and a corresponding list of indices into the
        /// internal shared component list and their internal versions.
        /// </summary>
        /// <remarks>
        /// All entities with the same archetype and the same values for a shared component are stored in the same set
        /// of chunks. This function finds the unique shared components existing across chunks and archetype and
        /// fills a list with copies of those components and fills in two separated lists: One with the indices of those
        /// components in the internal shared component list and one with their internal versions to detect free list
        /// reuse. You can use the indices to ask the same shared components directly
        /// by calling <see cref="GetSharedComponentManaged{T}(int)"/>, passing in the index. An index remains valid until
        /// the shared component order version changes. Check this version using
        /// <see cref="GetSharedComponentOrderVersion{T}(T)"/>.
        /// </remarks>
        /// <param name="sharedComponentValues">A List&lt;T&gt; object to receive the unique instances of the
        /// shared component of type T.</param>
        /// <param name="sharedComponentIndices">A List&lt;int&gt; object to receive the unique instances of the
        /// shared components' indices.</param>
        /// <param name="sharedComponentVersions">A List&lt;int&gt; object to receive the unique instances of the
        /// shared components' versions.</param>
        /// <typeparam name="T">The type of shared component.</typeparam>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void GetAllUniqueSharedComponentsManaged<T>(List<T> sharedComponentValues, List<int> sharedComponentIndices, List<int> sharedComponentVersions)
            where T : struct, ISharedComponentData
        {
            var access = GetCheckedEntityDataAccess();
            access->GetAllUniqueSharedComponents(sharedComponentValues, sharedComponentIndices, sharedComponentVersions);
        }

        /// <summary>
        /// Gets a list of all the unique instances of an unmanaged shared component type.
        /// </summary>
        /// <remarks>
        /// All entities with the same archetype and the same values for a shared component are stored in the same set
        /// of chunks. This function finds the unique shared components existing across chunks and archetype and
        /// fills a list with copies of those components.
        /// Note that the first element of the output list will always be the default value for <typeparamref name="T"/>,
        /// even if no entities or chunks currently use that value.
        /// </remarks>
        /// <param name="sharedComponentValues">A List&lt;T&gt; object to receive the unique instances of the
        /// shared component of type T.</param>
        /// <param name="allocator">The allocator for the native list of sharedComponentValues</param>
        /// <typeparam name="T">The type of shared component.</typeparam>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleSharedComponentData) })]
        public void GetAllUniqueSharedComponents<T>(out NativeList<T> sharedComponentValues, AllocatorManager.AllocatorHandle allocator) where T : unmanaged, ISharedComponentData
        {
            var access = GetCheckedEntityDataAccess();
            sharedComponentValues = new NativeList<T>(0, allocator);
            sharedComponentValues.m_ListData->Dispose();
            access->GetAllUniqueSharedComponents_Unmanaged<T>(out *sharedComponentValues.m_ListData, allocator);
        }

        /// <summary> Obsolete. Use <see cref="AddSharedComponent{T}(Unity.Entities.Entity,T)"/> instead.</summary>
        /// <param name="entity">The entity.</param>
        /// <param name="componentData">The shared component value to set.</param>
        /// <typeparam name="T">The shared component type.</typeparam>
        /// <returns>Returns false if the entity already has the shared component. The shared component value is set either way.</returns>
        [Obsolete("Use AddSharedComponent<T> (UnityUpgradable) -> AddSharedComponent<T>(*)", true)]
        public bool AddUnmanagedSharedComponentData<T>(Entity entity, T componentData)
            where T : unmanaged, ISharedComponentData
        {
            return false;
        }

        /// <summary>
        /// Adds a shared component to an entity.
        /// </summary>
        /// <remarks>
        /// The fields of the `componentData` parameter are assigned to the added shared component.
        ///
        /// Adding a component to an entity changes its archetype and results in the entity being moved to a
        /// different chunk. The entity moves to a chunk with other entities that have the same shared component values.
        /// A new chunk is created if no chunk with the same archetype and shared component values currently exists.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <returns>Returns false if the entity already has the shared component. The shared component value is set either way.</returns>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entity">The entity.</param>
        /// <param name="componentData">The shared component value to set.</param>
        /// <typeparam name="T">The shared component type.</typeparam>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleSharedComponentData) })]
        public bool AddSharedComponent<T>(Entity entity, T componentData) where T : unmanaged, ISharedComponentData
        {
            var ti = TypeManager.GetTypeIndex<T>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (TypeManager.IsManagedType(ti))
            {
                throw new ArgumentException($"Can't use this API with a Managed Shared Component, {TypeManager.GetType(ti)} must be a blittable, unmanaged type");
            }
#endif
            var access = GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            var defData = default(T);
            var result = access->AddSharedComponentDataDuringStructuralChange_Unmanaged(entity,
                ComponentType.ReadWrite<T>(),
                UnsafeUtility.AddressOf(ref componentData),
                UnsafeUtility.AddressOf(ref defData));
            access->EndStructuralChanges(ref changes);
            return result;
        }

        /// <summary>
        /// Adds a shared component to multiple entities
        /// </summary>
        /// <remarks>
        /// The fields of the `componentData` parameter are assigned to the added shared component.
        ///
        /// Adding a component to an entity changes its archetype and results in the entity being moved to a
        /// different chunk. The entity moves to a chunk with other entities that have the same shared component values.
        /// A new chunk is created if no chunk with the same archetype and shared component values currently exists.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">An element of <paramref name="entities"/> is invalid or does not exist.</exception>
        /// <param name="entities">The array of entities.</param>
        /// <param name="componentData">The shared component value to set.</param>
        /// <typeparam name="T">The shared component type.</typeparam>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleSharedComponentData) })]
        public void AddSharedComponent<T>(NativeArray<Entity> entities, T componentData) where T : unmanaged, ISharedComponentData
        {
            var ti = TypeManager.GetTypeIndex<T>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (TypeManager.IsManagedType(ti))
            {
                throw new ArgumentException($"Can't use this API with a Managed Shared Component, {TypeManager.GetType(ti)} must be a blittable, unmanaged type");
            }
#endif
            var access = GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            var defData = default(T);
            access->AddSharedComponentDataDuringStructuralChange_Unmanaged(entities,
                ComponentType.ReadWrite<T>(),
                UnsafeUtility.AddressOf(ref componentData),
                UnsafeUtility.AddressOf(ref defData));
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Gets the version number of the specified shared component.
        /// </summary>
        /// <remarks>
        /// This version number is incremented each time there is a structural change involving entities in the chunk of
        /// the specified shared component. Such changes include creating or destroying entities or anything that changes
        /// the archetype of an entity.
        ///
        /// Version numbers can overflow. To compare if one version is more recent than another, directly checking if
        /// VersionB is greater than VersionA is not sufficient. Instead, use the helper function:
        ///
        /// <code>
        /// bool VersionBisNewer = ChangeVersionUtility.DidChange(VersionB, VersionA);
        /// </code>
        /// </remarks>
        /// <param name="sharedComponent">The shared component instance.</param>
        /// <typeparam name="T">The shared component type.</typeparam>
        /// <returns>The current version number.</returns>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public int GetSharedComponentOrderVersionManaged<T>(T sharedComponent) where T : struct, ISharedComponentData
        {
            var access = GetCheckedEntityDataAccess();
            return access->GetSharedComponentVersion(sharedComponent);
        }

        /// <summary>
        /// Gets the version number of the specified shared component.
        /// </summary>
        /// <remarks>
        /// This version number is incremented each time there is a structural change involving entities in the chunk of
        /// the specified shared component. Such changes include creating or destroying entities or anything that changes
        /// the archetype of an entity.
        ///
        /// Version numbers can overflow. To compare if one version is more recent than another, directly checking if
        /// VersionB is greater than VersionA is not sufficient. Instead, use the helper function:
        ///
        /// <code>
        /// bool VersionBisNewer = ChangeVersionUtility.DidChange(VersionB, VersionA);
        /// </code>
        /// </remarks>
        /// <param name="sharedComponent">The unmanaged shared component instance.</param>
        /// <typeparam name="T">The unmanaged shared component type.</typeparam>
        /// <returns>The current version number.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleSharedComponentData) })]
        public int GetSharedComponentOrderVersion<T>(T sharedComponent) where T : unmanaged, ISharedComponentData
        {
            var access = GetCheckedEntityDataAccess();
            return access->GetSharedComponentVersion_Unmanaged(sharedComponent);
        }

        #endregion

        DynamicBuffer<T> GetBufferInternal<T>(EntityDataAccess* access, Entity entity, bool isReadOnly = false) where T : unmanaged, IBufferElementData
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandles = &access->DependencyManager->Safety;
#endif

            return access->GetBuffer<T>(entity,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                safetyHandles->GetSafetyHandle(typeIndex, isReadOnly),
                safetyHandles->GetBufferSafetyHandle(typeIndex),
#endif
                isReadOnly
            );
        }

        /// <summary>
        /// Gets the dynamic buffer of an entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="isReadOnly">Specify whether the access to the component through this object is read only
        /// or read and write.</param>
        /// <typeparam name="T">The type of the buffer's elements.</typeparam>
        /// <returns>The DynamicBuffer object for accessing the buffer contents.</returns>
        /// <exception cref="ArgumentException">Thrown if T is an unsupported type.</exception>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleBufferElement) })]
        public DynamicBuffer<T> GetBuffer<T>(Entity entity, bool isReadOnly = false) where T : unmanaged, IBufferElementData
        {
            var access = GetCheckedEntityDataAccess();
            return GetBufferInternal<T>(access, entity, isReadOnly);
        }

        /// <summary>
        /// Gets the dynamic buffer of an entity associated with a system.
        /// </summary>
        /// <param name="system">The system handle.</param>
        /// <param name="isReadOnly">Specify whether the access to the component through this object is read only
        /// or read and write.</param>
        /// <typeparam name="T">The type of the buffer's elements.</typeparam>
        /// <returns>The DynamicBuffer object for accessing the buffer contents.</returns>
        /// <exception cref="ArgumentException">Thrown if T is an unsupported type.</exception>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleBufferElement) })]
        public DynamicBuffer<T> GetBuffer<T>(SystemHandle system, bool isReadOnly = false) where T : unmanaged, IBufferElementData
        {
            var access = GetCheckedEntityDataAccess(system);
            return GetBufferInternal<T>(access, system.m_Entity, isReadOnly);
        }

        /// <summary>
        /// Gets a struct containing information about the archetype in which an entity is stored.
        /// </summary>
        /// <param name="entity">The entity being queried for storage information.</param>
        /// <returns>Information about the storage of the target entity.</returns>
        public EntityStorageInfo GetStorageInfo(Entity entity)
        {
            var access = GetCheckedEntityDataAccess();
            return access->GetStorageInfo(entity);
        }

        /// <summary>
        /// Swaps the components of two entities.
        /// </summary>
        /// <remarks>
        /// The entities must have the same components. However, this function can swap the components of entities in
        /// different worlds, so they do not need to have identical archetype instances.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before swapping the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="leftChunk">A chunk containing one of the entities to swap.</param>
        /// <param name="leftIndex">The index within the `leftChunk` of the entity and components to swap. Must be in
        /// the range [0,leftChunk.Count).</param>
        /// <param name="rightChunk">The chunk containing the other entity to swap. This chunk can be the same as
        /// the `leftChunk`. It also does not need to be in the same World as `leftChunk`.</param>
        /// <param name="rightIndex">The index within the `rightChunk`  of the entity and components to swap. Must be in
        /// the range [0,rightChunk.Count).</param>
        [StructuralChangeMethod]
        public void SwapComponents(ArchetypeChunk leftChunk, int leftIndex, ArchetypeChunk rightChunk, int rightIndex)
        {
            var access = GetCheckedEntityDataAccess();
            access->SwapComponents(leftChunk, leftIndex, rightChunk, rightIndex);
        }

        /// <summary>
        /// Gets the chunk in which the specified entity is stored.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>The chunk containing the entity.</returns>
        public ArchetypeChunk GetChunk(Entity entity)
        {
            var ecs = GetCheckedEntityDataAccess()->EntityComponentStore;
            var chunk = ecs->GetChunk(entity);
            return new ArchetypeChunk(chunk, ecs);
        }

        /// <summary>
        /// Gets the number of component types associated with an entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>The number of components.</returns>
        public int GetComponentCount(Entity entity)
        {
            var ecs = GetCheckedEntityDataAccess()->EntityComponentStore;
            ecs->AssertEntitiesExist(&entity, 1);
            var archetype = ecs->GetArchetype(entity);
            return archetype->TypesCount - 1;
        }

        /// <summary>
        /// Adds a component to an entity.
        /// </summary>
        /// <remarks>
        /// Can add any kind of component except chunk components. For chunk
        /// components, use <see cref="AddChunkComponentData"/>.
        ///
        /// Adding a component changes the entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// The added component has the default values for the type.
        ///
        /// If the <see cref="Entity"/> object refers to an entity that already has the specified <see cref="ComponentType"/>,
        /// the function returns false without performing any modifications.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entity">The Entity.</param>
        /// <param name="componentType">The type of component to add.</param>
        /// <returns>If the component was successfully added to the entity.</returns>
        [StructuralChangeMethod]
        public bool AddComponent(Entity entity, ComponentType componentType)
        {
            var access = GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            var result = access->AddComponentDuringStructuralChange(entity, componentType);
            access->EndStructuralChanges(ref changes);
            return result;
        }

        /// <summary>
        /// Adds a component to an entity associated with a system.
        /// </summary>
        /// <remarks>
        /// Can add any kind of component except chunk components. For chunk
        /// components, use <see cref="AddChunkComponentData"/>.
        ///
        /// Adding a component changes the entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// The added component has the default values for the type.
        ///
        /// If the <see cref="Entity"/> object refers to an entity that already has the specified <see cref="ComponentType"/>,
        /// the function returns false without performing any modifications.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="system">The system handle.</param>
        /// <param name="componentType">The type of component to add.</param>
        /// <returns>If the component was successfully added to the entity.</returns>
        [StructuralChangeMethod]
        public bool AddComponent(SystemHandle system, ComponentType componentType)
        {
            return AddComponent(system.m_Entity, componentType);
        }

        /// <summary>
        /// Adds a component to an entity.
        /// </summary>
        /// <remarks>
        /// Can add any kind of component except chunk components. For chunk
        /// components, use <see cref="AddChunkComponentData"/>.
        ///
        /// Adding a component changes the entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// The added component has the default values for the type.
        ///
        /// If the <see cref="Entity"/> object refers to an entity that already has the specified <see cref="ComponentType"/>,
        /// the function returns false without performing any modifications.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before addting the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entity">The Entity.</param>
        /// <typeparam name="T">The type of component to add.</typeparam>
        /// <returns>If the component was successfully added to the entity.</returns>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public bool AddComponent<T>(Entity entity)
        {
            return AddComponent(entity, ComponentType.ReadWrite<T>());
        }

        /// <summary>
        /// Adds a component to an entity associated with a system.
        /// </summary>
        /// <remarks>
        /// Can add any kind of component except chunk components. For chunk
        /// components, use <see cref="AddChunkComponentData"/>.
        ///
        /// Adding a component changes the entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// The added component has the default values for the type.
        ///
        /// If the <see cref="Entity"/> object refers to an entity that already has the specified <see cref="ComponentType"/>,
        /// the function returns false without performing any modifications.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before addting the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="system">The system handle.</param>
        /// <typeparam name="T">The type of component to add.</typeparam>
        /// <returns>If the component was successfully added to the entity.</returns>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public bool AddComponent<T>(SystemHandle system)
        {
            return AddComponent(system.m_Entity, ComponentType.ReadWrite<T>());
        }

        /// <summary>
        /// Creates and supports only a single entity with a given unmanaged component.
        /// </summary>
        /// <remarks>
        /// Note that while zero-size component may be created, they can not be accessed directly in code. They are
        /// only usable for situations such as in calling <see cref="ComponentSystemBase.RequireForUpdate{T}"/>.
        /// </remarks>
        /// <param name="name">The debug friendly name associated with the singleton component entity.</param>
        /// <typeparam name="T">The type of component to create.</typeparam>
        /// <returns>The Entity object that you can use to access the singleton component entity.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the component type has no fields, is enableable, or an entity containing it already exists.</exception>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public Entity CreateSingleton<T>(FixedString64Bytes name = default) where T : unmanaged, IComponentData
        {
            return CreateSingletonEntityInternal<T>(name);
        }

        /// <summary>
        /// Creates and supports only a single entity with a given unmanaged component.
        /// </summary>
        /// <remarks>
        /// Note that while zero-size component may be created, they can not be accessed directly in code. They are
        /// only usable for situations such as in calling <see cref="ComponentSystemBase.RequireForUpdate{T}"/>.
        /// </remarks>
        /// <param name="componentData">The data to assign to the singleton component.</param>
        /// <param name="name">The debug friendly name associated with the singleton component entity.</param>
        /// <typeparam name="T">The type of component to create.</typeparam>
        /// <returns>The Entity object that you can use to access the singleton component entity.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the component type has no fields, is enableable, or an entity containing it already exists.</exception>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public Entity CreateSingleton<T>(T componentData, FixedString64Bytes name = default) where T : unmanaged, IComponentData
        {
            var entity = CreateSingletonEntityInternal<T>(name);
            SetComponentData(entity, componentData);
            return entity;
        }

        /// <summary>
        /// Creates and supports only a single entity with a given unmanaged component buffer.
        /// </summary>
        /// <param name="name">The debug friendly name associated with the singleton component buffer entity.</param>
        /// <typeparam name="T">The type of component buffer to create.</typeparam>
        /// <returns>The Entity object that you can use to access the singleton component buffer entity.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the component buffer type has no fields, is enableable, or an entity containing it already exists.</exception>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleBufferElement) })]
        public Entity CreateSingletonBuffer<T>(FixedString64Bytes name = default) where T : unmanaged, IBufferElementData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            var typeIndex = TypeManager.GetTypeIndex<T>();
            if (TypeManager.IsZeroSized(typeIndex))
            {
                var typeName = typeIndex.ToFixedString();
                throw new InvalidOperationException($"Singleton component {typeName} can not be created because it is a zero-size type.");
            }
#endif
            return CreateSingletonEntityInternal<T>(name);
        }

        [StructuralChangeMethod]
        private Entity CreateSingletonEntityInternal<T>(FixedString64Bytes name = default) where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            var query = CreateEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAllRW<T>().WithOptions(EntityQueryOptions.IncludeSystems));
            var typeIndex = TypeManager.GetTypeIndex<T>();

            if (query.CalculateEntityCount() != 0)
            {
                var typeName = typeIndex.ToFixedString();
                throw new InvalidOperationException($"Singleton component {typeName} can not be created because entities already exist with this component type.");
            }

            if (TypeManager.IsEnableable(typeIndex))
            {
                var typeName = typeIndex.ToFixedString();
                throw new InvalidOperationException($"Singleton component {typeName} can not be created because it is an enableable component type.");
            }
#endif
            var componentType = ComponentType.ReadWrite<T>();
            var entity = CreateEntity(CreateArchetype(&componentType, 1));

#if !DOTS_DISABLE_DEBUG_NAMES
            if (name == default)
                name.CopyFromTruncated(componentType.ToFixedString());
            GetCheckedEntityDataAccess()->SetName(entity, in name);
#endif
            return entity;
        }

        /// <summary>
        /// Adds a component to a set of entities defined by a EntityQuery.
        /// </summary>
        /// <remarks>
        /// Can add any kind of component.
        ///
        /// Adding a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// The added components have the default values for the type.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before addting the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="entityQuery">The EntityQuery defining the entities to modify.</param>
        /// <param name="componentType">The type of component to add.</param>
        [StructuralChangeMethod]
        public void AddComponent(EntityQuery entityQuery, ComponentType componentType)
        {
            var access = GetCheckedEntityDataAccess();
            access->AssertQueryIsValid(entityQuery);
            var queryImpl = entityQuery._GetImpl();

            if (queryImpl->IsEmptyIgnoreFilter)
                return;

            var changes = access->BeginStructuralChanges();
            access->AddComponentToQueryDuringStructuralChange(queryImpl, componentType);
            access->EndStructuralChanges(ref changes);
        }


        /// <summary>
        /// Adds components to a set of entities defined by a EntityQuery.
        /// </summary>
        /// <remarks>
        /// You can use this method to add a component to an Entity.
        ///
        /// The added components have the default values for the type.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before addting the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="entityQuery">The EntityQuery defining the entities to modify.</param>
        /// <param name="componentTypeSet">The type of components to add.</param>
        [StructuralChangeMethod]
        public void AddComponent(EntityQuery entityQuery, in ComponentTypeSet componentTypeSet)
        {
            var access = GetCheckedEntityDataAccess();
            access->AssertQueryIsValid(entityQuery);
            var queryImpl = entityQuery._GetImpl();
            if (queryImpl->IsEmptyIgnoreFilter)
                return;

            var changes = access->BeginStructuralChanges();
            access->AddComponentsToQueryDuringStructuralChange(queryImpl, componentTypeSet);
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Adds a component to a set of entities defined by the EntityQuery and
        /// sets the component of each entity in the query to the value in the component array.
        /// </summary>
        /// <remarks>
        /// You can use this method to add a component to an Entity.
        ///
        /// Adding components changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        /// componentArray.Length must match entityQuery.ToEntityArray().Length.
        /// </remarks>
        /// <param name="entityQuery">The EntityQuery defining the entities to add component to</param>
        /// <param name="componentArray">The NativeArray to add the components to.</param>
        /// <typeparam name="T">The type of component to add.</typeparam>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public void AddComponentData<T>(EntityQuery entityQuery, NativeArray<T> componentArray) where T : unmanaged, IComponentData
        {
            var access = GetCheckedEntityDataAccess();
            access->AssertQueryIsValid(entityQuery);
            if (entityQuery.IsEmptyIgnoreFilter)
                return;

            var queryData = entityQuery._GetImpl()->_QueryData;
            if (queryData->HasEnableableComponents != 0)
            {
                // Complete jobs that may be writing to any enableable types in this query
                for (int i = 0; i < queryData->EnableableComponentTypeIndexCount; ++i)
                    access->DependencyManager->CompleteWriteDependency(queryData->EnableableComponentTypeIndices[i]);
            }

            var entities = entityQuery.ToEntityArray(Allocator.TempJob);
            {
                if (entities.Length != componentArray.Length)
                    throw new ArgumentException($"AddComponentData number of entities in query '{entities.Length}' must match componentArray.Length '{componentArray.Length}'.");

                AddComponent(entityQuery, ComponentType.ReadWrite<T>());

                var componentData = GetComponentLookup<T>();
                for (int i = 0; i != componentArray.Length; i++)
                    componentData[entities[i]] = componentArray[i];
            }
            entities.Dispose();
        }

        /// <summary>
        /// Adds a component to a set of entities defined by a EntityQuery.
        /// </summary>
        /// <remarks>
        /// Can add any kind of component except chunk components.
        ///
        /// Adding a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// The added components have the default values for the type.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="entityQuery">The EntityQuery defining the entities to modify.</param>
        /// <typeparam name="T">The type of component to add.</typeparam>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public void AddComponent<T>(EntityQuery entityQuery)
        {
            AddComponent(entityQuery, ComponentType.ReadWrite<T>());
        }

        /// <summary>
        /// Adds a component to a set of entities.
        /// </summary>
        /// <remarks>
        /// Can add any kind of component except chunk components.
        ///
        /// Adding a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// The added components have the default values for the type.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before creating the chunk. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The `entities` array refers to an entity that does not exist.</exception>
        /// <param name="entities">An array of Entity objects.</param>
        /// <param name="componentType">The type of component to add.</param>
        [StructuralChangeMethod]
        public void AddComponent(NativeArray<Entity> entities, ComponentType componentType)
        {
            if (entities.Length == 0)
                return;

            if (componentType.IsChunkComponent)
                throw new ArgumentException($"Cannot add ChunkComponent {componentType} on NativeArray of entities.");

            var access = GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;
            ecs->AssertEntitiesExist((Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            // Have to record here because method is not using EntityDataAccess
            if (Hint.Unlikely(ecs->m_RecordToJournal != 0))
                access->JournalAddRecord_AddComponent(default, in entities, &componentType.TypeIndex, 1);
#endif

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                access->StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.AddComponent, in access->m_WorldUnmanaged);
#endif

            // This method does not use EntityDataAccess.AddComponentDuringStructuralChange because
            // we must get the entity batch list before the call to EntityDataAccess.BeforeStructuralChange
            var entityBatchList = default(NativeList<EntityBatchInChunk>);
            var useBatches = entities.Length > EntityDataAccess.FASTER_TO_BATCH_THRESHOLD &&
                ecs->CreateEntityBatchList(entities, componentType.IsSharedComponent ? 1 : 0, Allocator.Temp, out entityBatchList);

            var changes = access->BeginStructuralChanges();
            if (useBatches)
            {
                ecs->AssertCanAddComponent(entityBatchList, componentType);
                StructuralChange.AddComponentEntitiesBatch(ecs, (UnsafeList<EntityBatchInChunk>*)NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(ref entityBatchList), componentType.TypeIndex);
            }
            else
            {
                ecs->AssertCanAddComponent(entities, componentType);
                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    StructuralChange.AddComponentEntity(ecs, &entity, componentType.TypeIndex);
                }
            }
            access->EndStructuralChanges(ref changes);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                ecs->StructuralChangesRecorder.End();
#endif
        }



        /// <summary>
        /// Adds components to a specified collection of entities.
        /// </summary>
        /// <remarks>
        /// You can use this method to add a component to an Entity.
        ///
        /// The added components have the default values for the type.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="entities">The collection of entities to modify.</param>
        /// <param name="componentTypeSet">The type of components to add.</param>
        [StructuralChangeMethod]
        public void AddComponent(NativeArray<Entity> entities, in ComponentTypeSet componentTypeSet)
        {
            var access = GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            access->AddMultipleComponentsDuringStructuralChange(entities, componentTypeSet);
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Removes multiple components from a specified colleciton of entities.
        /// </summary>
        /// <remarks>
        /// You can use this method to remove a component from an Entity
        ///
        /// It's OK if some or all of the components to remove are already missing from the entity.
        ///
        /// Removing components changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// If any of the types are a managed component which implements <see cref="IDisposable"/>, this operation will invoke Dispose() on the component value.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before removing the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entities">The collection of entities to modify.</param>
        /// <param name="componentTypeSet">The types of components to remove.</param>
        [StructuralChangeMethod]
        public void RemoveComponent(NativeArray<Entity> entities, in ComponentTypeSet componentTypeSet)
        {
            var access = GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            access->RemoveMultipleComponentsDuringStructuralChange(entities, componentTypeSet);
            access->EndStructuralChanges(ref changes);

        }

        /// <summary>
        /// Remove a component from a set of entities.
        /// </summary>
        /// <remarks>
        /// Can remove any kind of component.
        ///
        /// Removing a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// If componentType is a managed component which implements <see cref="IDisposable"/>, this operation will invoke Dispose() on the component value.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before creating the chunk. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The `entities` array refers to an entity that does not exist.</exception>
        /// <param name="entities">An array of Entity objects.</param>
        /// <param name="componentType">The type of component to remove.</param>
        [StructuralChangeMethod]
        public void RemoveComponent(NativeArray<Entity> entities, ComponentType componentType)
        {
            if (entities.Length == 0)
                return;

            var access = GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            access->RemoveComponentDuringStructuralChange(entities, componentType);
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Adds a component to a set of entities.
        /// </summary>
        /// <remarks>
        /// Can add any kind of component.
        ///
        /// Adding a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// The added components have the default values for the type.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The `entities` array refers to an entity that does not exist.</exception>
        /// <param name="entities">An array of Entity objects.</param>
        /// <typeparam name="T">The type of component to add.</typeparam>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public void AddComponent<T>(NativeArray<Entity> entities)
        {
            AddComponent(entities, ComponentType.ReadWrite<T>());
        }

        /// <summary>
        /// Adds a set of component to an entity associated with a system.
        /// </summary>
        /// <remarks>
        /// You can use this method to add a component to an Entity.
        ///
        /// Adding components changes the entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// The added components have the default values for the type.
        ///
        /// If the <see cref="Entity"/> object refers to an entity that has been destroyed, this function throws an InvalidOperationException
        /// exception.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before setting the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="system">The system handle.</param>
        /// <param name="componentTypeSet">The types of components to add.</param>
        [StructuralChangeMethod]
        public void AddComponent(SystemHandle system, in ComponentTypeSet componentTypeSet)
        {
            var access = GetCheckedEntityDataAccess(system);
            var changes = access->BeginStructuralChanges();
            access->AddMultipleComponentsDuringStructuralChange(system.m_Entity, componentTypeSet);
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Adds a set of component to an entity.
        /// </summary>
        /// <remarks>
        /// You can use this method to add a component to an Entity.
        ///
        /// Adding components changes the entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// The added components have the default values for the type.
        ///
        /// If the <see cref="Entity"/> object refers to an entity that has been destroyed, this function throws an InvalidOperationException
        /// exception.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entity">The entity to modify.</param>
        /// <param name="componentTypeSet">The types of components to add.</param>
        [StructuralChangeMethod]
        public void AddComponent(Entity entity, in ComponentTypeSet componentTypeSet)
        {
            var access = GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            access->AddMultipleComponentsDuringStructuralChange(entity, componentTypeSet);
            access->EndStructuralChanges(ref changes);
        }


        /// <summary> Obsolete. Use <see cref="AddComponent"/> instead.</summary>
        /// <param name="entity">The entity to modify.</param>
        /// <param name="componentTypeSet">The types of components to add.</param>
        // We can't use (UnityUpgradable) reliably here, since the parameter type ComponentTypes is also being renamed
        // to ComponentTypeSet. So for now it's just a warning that wraps the new function.
        [Obsolete("AddComponents() has been renamed to AddComponent(). AddComponents() will be removed in a future package release.", false)]
        public void AddComponents(Entity entity, in ComponentTypeSet componentTypeSet)
        {
            AddComponent(entity, componentTypeSet);
        }


        /// <summary>
        /// Removes a component from an entity.
        /// </summary>
        /// <remarks>
        /// Can remove any kind of component.
        ///
        /// Returns false if the entity already does not have the specified component, or if the entity does not exist.
        ///
        /// Removing a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// If componentType is a managed component which implements <see cref="IDisposable"/>, this operation will invoke Dispose() on the component value.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before removing the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entity">The entity to modify.</param>
        /// <param name="componentType">The type of component to remove.</param>
        /// <returns>True if the component was removed successfully.</returns>
        [StructuralChangeMethod]
        public bool RemoveComponent(Entity entity, ComponentType componentType)
        {
            var access = GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            var result = access->RemoveComponentDuringStructuralChange(entity, componentType);
            access->EndStructuralChanges(ref changes);
            return result;
        }

        /// <summary>
        /// Removes a component from an entity associated with a system.
        /// </summary>
        /// <remarks>
        /// Can remove any kind of component.
        ///
        /// Returns false if the entity already does not have the specified component, or if the entity does not exist.
        ///
        /// Removing a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// If componentType is a managed component which implements <see cref="IDisposable"/>, this operation will invoke Dispose() on the component value.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before removing the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="system">The system handle.</param>
        /// <param name="componentType">The type of component to remove.</param>
        /// <returns>True if the component was removed successfully.</returns>
        [StructuralChangeMethod]
        public bool RemoveComponent(SystemHandle system, ComponentType componentType)
        {
            var access = GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            var result = access->RemoveComponentDuringStructuralChange(system.m_Entity, componentType);
            access->EndStructuralChanges(ref changes);
            return result;
        }

        /// <summary>
        /// Removes multiple components from an entity.
        /// </summary>
        /// <remarks>
        /// You can use this method to remove a component from an Entity
        ///
        /// It's OK if some or all of the components to remove are already missing from the entity, or if the target
        /// entity does not exist.
        ///
        /// Removing components changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// If any of the types are a managed component which implements <see cref="IDisposable"/>, this operation will invoke Dispose() on the component value.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before removing the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entity">The entity to modify.</param>
        /// <param name="componentTypeSet">The types of components to remove.</param>
        [StructuralChangeMethod]
        public void RemoveComponent(Entity entity, in ComponentTypeSet componentTypeSet)
        {
            var access = GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            access->RemoveComponentDuringStructuralChange(entity, componentTypeSet);
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Removes a component from a set of entities defined by an EntityQuery.
        /// </summary>
        /// <remarks>
        /// Can remove any kind of component.
        ///
        /// It's OK if some or all of the components to remove are already missing from some or all of the entities.
        ///
        /// Removing a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// If componentType is a managed component which implements <see cref="IDisposable"/>, this operation will invoke Dispose() on the component value.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before removing the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="entityQuery">The EntityQuery defining the entities to modify.</param>
        /// <param name="componentType">The type of component to remove.</param>
        [StructuralChangeMethod]
        public void RemoveComponent(EntityQuery entityQuery, ComponentType componentType)
        {
            var access = GetCheckedEntityDataAccess();
            access->AssertQueryIsValid(entityQuery);
            var queryImpl = entityQuery._GetImpl();
            if (queryImpl->IsEmptyIgnoreFilter)
                return;
            var changes = access->BeginStructuralChanges();
            access->RemoveComponentFromQueryDuringStructuralChange(queryImpl, componentType);
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Removes a set of components from a set of entities defined by an EntityQuery.
        /// </summary>
        /// <remarks>
        /// You can use this method to remove a component from an Entity
        ///
        /// It's OK if some or all of the components to remove are already missing from some or all of the entities.
        ///
        /// Removing a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// If any of the types are a managed component which implements <see cref="IDisposable"/>, this operation will invoke Dispose() on the component value.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before removing the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="entityQuery">The EntityQuery defining the entities to modify.</param>
        /// <param name="componentTypeSet">The types of components to add.</param>
        [StructuralChangeMethod]
        public void RemoveComponent(EntityQuery entityQuery, in ComponentTypeSet componentTypeSet)
        {
            var access = GetCheckedEntityDataAccess();
            access->AssertQueryIsValid(entityQuery);
            var queryImpl = entityQuery._GetImpl();
            if (queryImpl->IsEmptyIgnoreFilter)
                return;

            var changes = access->BeginStructuralChanges();
            access->RemoveMultipleComponentsFromQueryDuringStructuralChange(queryImpl, componentTypeSet);
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Removes a component from an entity.
        /// </summary>
        /// <remarks>
        /// Can remove any kind of component except chunk components.
        ///
        /// Returns false if the entity was already missing the component.
        ///
        /// Removing a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// If T is a managed component which implements <see cref="IDisposable"/>, this operation will invoke Dispose() on the component value.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before removing the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entity">The entity.</param>
        /// <typeparam name="T">The type of component to remove.</typeparam>
        /// <returns>True if the component was removed successfully.</returns>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public bool RemoveComponent<T>(Entity entity)
        {
            return RemoveComponent(entity, ComponentType.ReadWrite<T>());
        }

        /// <summary>
        /// Removes a component from an entity associated with a system.
        /// </summary>
        /// <remarks>
        /// Can remove any kind of component except chunk components.
        ///
        /// Returns false if the entity was already missing the component.
        ///
        /// Removing a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// If T is a managed component which implements <see cref="IDisposable"/>, this operation will invoke Dispose() on the component value.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before removing the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="system">The system handle.</param>
        /// <typeparam name="T">The type of component to remove.</typeparam>
        /// <returns>True if the component was removed successfully.</returns>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public bool RemoveComponent<T>(SystemHandle system)
        {
            return RemoveComponent(system.m_Entity, ComponentType.ReadWrite<T>());
        }

        /// <summary>
        /// Removes a component from a set of entities defined by a EntityQuery.
        /// </summary>
        /// <remarks>
        /// Can remove any kind of component except chunk components.
        ///
        /// It's OK if the component to remove is already missing from some or all of the entities.
        ///
        /// Removing a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before removing the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="entityQuery">The EntityQuery defining the entities to modify.</param>
        /// <typeparam name="T">The type of component to remove.</typeparam>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public void RemoveComponent<T>(EntityQuery entityQuery)
        {
            RemoveComponent(entityQuery, ComponentType.ReadWrite<T>());
        }

        /// <summary>
        /// Removes a component from a set of entities.
        /// </summary>
        /// <remarks>
        /// Can remove any kind of component except chunk components.
        ///
        /// It's OK if the component to remove is already missing from some or all of the entities.
        ///
        /// Removing a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// If T is a managed component which implements <see cref="IDisposable"/>, this operation will invoke Dispose()  on the component value.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before removing the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="entities">An array identifying the entities to modify.</param>
        /// <typeparam name="T">The type of component to remove.</typeparam>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public void RemoveComponent<T>(NativeArray<Entity> entities)
        {
            RemoveComponent(entities, ComponentType.ReadWrite<T>());
        }

        /// <summary>
        /// Adds a component to an entity and set the value of that component.
        /// </summary>
        /// <remarks>
        /// Can add any kind of component except chunk components, managed components, or shared components.
        ///
        /// Returns false if the entity already had the component. The component's data is set regardless.
        ///
        /// Adding a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entity">The entity.</param>
        /// <param name="componentData">The data to set.</param>
        /// <typeparam name="T">The type of component.</typeparam>
        /// <returns>Returns false if the entity alredy had the component.</returns>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public bool AddComponentData<T>(Entity entity, T componentData) where T : unmanaged, IComponentData
        {
            var type = ComponentType.ReadWrite<T>();
            var added = AddComponent(entity, type);
            if (!type.IsZeroSized)
                SetComponentData(entity, componentData);

            return added;
        }

        /// <summary>
        /// Adds a component to an entity associated with a system and sets the value of that component.
        /// </summary>
        /// <remarks>
        /// Can add any kind of component except chunk components, managed components, or shared components.
        ///
        /// Returns false if the entity already had the component. The component's data is set regardless.
        ///
        /// Adding a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="system">The system handle.</param>
        /// <param name="componentData">The data to set.</param>
        /// <typeparam name="T">The type of component.</typeparam>
        /// <returns>Returns false if the entity already had the component.</returns>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public bool AddComponentData<T>(SystemHandle system, T componentData) where T : unmanaged, IComponentData
        {
            var type = ComponentType.ReadWrite<T>();
            var added = AddComponent(system.m_Entity, type);
            if (!type.IsZeroSized)
                SetComponentData(system.m_Entity, componentData);

            return added;
        }

        /// <summary>
        /// Removes a chunk component from the specified entity.
        /// </summary>
        /// <remarks>
        /// A chunk component is common to all entities in a chunk. Removing the chunk component from an entity changes
        /// that entity's archetype and results in the entity being moved to a different chunk (that does not have the
        /// removed component).
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before removing the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <returns>False if the entity did not have the component.</returns>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entity">The entity.</param>
        /// <typeparam name="T">The type of component to remove.</typeparam>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public bool RemoveChunkComponent<T>(Entity entity)
        {
            return RemoveComponent(entity, ComponentType.ChunkComponent<T>());
        }

        /// <summary>
        /// Adds a chunk component to the specified entity.
        /// </summary>
        /// <remarks>
        /// Adding a chunk component to an entity changes that entity's archetype and results in the entity being moved
        /// to a different chunk, either one that already has an archetype containing the chunk component or a new
        /// chunk.
        ///
        /// A chunk component is common to all entities in a chunk. You can access a chunk <see cref="IComponentData"/>
        /// instance through either the chunk itself or through an entity stored in that chunk. In either case, getting
        /// or setting the component reads or writes the same data.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <returns>False if the entity already had the chunk component. The chunk component's value is set regardless.</returns>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entity">The entity.</param>
        /// <typeparam name="T">The type of component, which must implement IComponentData.</typeparam>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public bool AddChunkComponentData<T>(Entity entity) where T : unmanaged, IComponentData
        {
            return AddComponent(entity, ComponentType.ChunkComponent<T>());
        }

        /// <summary>
        /// Adds a chunk component to each of the chunks identified by an EntityQuery and sets the component values.
        /// </summary>
        /// <remarks>
        /// This function finds all chunks whose archetype satisfies the EntityQuery and adds the specified
        /// component to them.
        ///
        /// A chunk component is common to all entities in a chunk. You can access a chunk <see cref="IComponentData"/>
        /// instance through either the chunk itself or through an entity stored in that chunk.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="entityQuery">The EntityQuery identifying the chunks to modify.</param>
        /// <param name="componentData">The data to set.</param>
        /// <typeparam name="T">The type of component, which must implement IComponentData.</typeparam>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public void AddChunkComponentData<T>(EntityQuery entityQuery, T componentData) where T : unmanaged, IComponentData
        {
            var access = GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;
            access->AssertQueryIsValid(entityQuery);
            if (entityQuery.IsEmptyIgnoreFilter)
                return;

            using var chunks = entityQuery.ToArchetypeChunkArray(Allocator.TempJob);

            if (chunks.Length > 0)
            {
                ArchetypeChunk* chunkPtr = (ArchetypeChunk*)NativeArrayUnsafeUtility.GetUnsafePtr(chunks);
                var componentType = ComponentType.ReadWrite<T>();
                ecs->AssertCanAddComponent(entityQuery._GetImpl(), ComponentType.ChunkComponent<T>());
                var componentTypeIndex = componentType.TypeIndex;
                var componentTypeIndexForAdd = TypeManager.MakeChunkComponentTypeIndex(componentTypeIndex);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
                // Have to record here because method is not using EntityDataAccess
                if (Hint.Unlikely(ecs->m_RecordToJournal != 0))
                    access->JournalAddRecord_AddComponent(default, chunkPtr, chunks.Length, &componentTypeIndexForAdd, 1);
#endif

                var changes = access->BeginStructuralChanges();
                StructuralChange.AddComponentChunks(ecs, chunkPtr, chunks.Length, componentTypeIndexForAdd);
                access->EndStructuralChanges(ref changes);
                StructuralChange.SetChunkComponent(ecs, chunkPtr, chunks.Length, &componentData, componentTypeIndex);
            }
        }

        /// <summary>
        /// Removes a chunk component from the chunks identified by an EntityQuery.
        /// </summary>
        /// <remarks>
        /// A chunk component is common to all entities in a chunk. You can access a chunk <see cref="IComponentData"/>
        /// instance through either the chunk itself or through an entity stored in that chunk.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before removing the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="entityQuery">The EntityQuery identifying the chunks to modify.</param>
        /// <typeparam name="T">The type of component to remove.</typeparam>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public void RemoveChunkComponentData<T>(EntityQuery entityQuery)
        {
            RemoveComponent(entityQuery, ComponentType.ChunkComponent<T>());
        }

        /// <summary>
        /// Adds a dynamic buffer component to an entity.
        /// </summary>
        /// <remarks>
        /// A buffer component stores the number of elements inside the chunk defined by the [InternalBufferCapacity]
        /// attribute applied to the buffer element type declaration. Any additional elements are stored in a separate memory
        /// block that is managed by the EntityManager.
        ///
        /// Adding a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// (You can add a buffer component with the regular AddComponent methods, but unlike those methods, this
        /// method conveniently also returns the new buffer.)
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entity">The entity.</param>
        /// <typeparam name="T">The type of buffer element. Must implement IBufferElementData.</typeparam>
        /// <returns>The buffer.</returns>
        /// <seealso cref="InternalBufferCapacityAttribute"/>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleBufferElement) })]
        public DynamicBuffer<T> AddBuffer<T>(Entity entity) where T : unmanaged, IBufferElementData
        {
            AddComponent<T>(entity);
            return GetBuffer<T>(entity);
        }

        /// <summary>
        /// Adds a managed [UnityEngine.Component](https://docs.unity3d.com/ScriptReference/Component.html)
        /// object to an entity.
        /// </summary>
        /// <remarks>
        /// Accessing data in a managed object forfeits many opportunities for increased performance. Adding
        /// managed objects to an entity should be avoided or used sparingly.
        ///
        /// Adding a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// The method also works for adding managed objects implementing `IComponentData`, but `AddComponentData` is the preferred method for those objects.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the object. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="entity">The entity to modify.</param>
        /// <param name="componentData">An object inheriting UnityEngine.Component.</param>
        /// <exception cref="ArgumentNullException">If the componentData object is not an instance of
        /// UnityEngine.Component.</exception>
        [StructuralChangeMethod]
        [ExcludeFromBurstCompatTesting("Takes managed object")]
        public void AddComponentObject(Entity entity, object componentData)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (componentData == null)
                throw new ArgumentNullException(nameof(componentData));
#endif

            ComponentType type = componentData.GetType();

            AddComponent(entity, type);
            SetComponentObject(entity, type, componentData);
        }

        /// <summary>
        /// Adds a managed [UnityEngine.Component](https://docs.unity3d.com/ScriptReference/Component.html)
        /// object to an entity associated with a system.
        /// </summary>
        /// <remarks>
        /// Accessing data in a managed object forfeits many opportunities for increased performance. Adding
        /// managed objects to an entity should be avoided or used sparingly.
        ///
        /// Adding a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// The method also works for adding managed objects implementing `IComponentData`, but `AddComponentData` is the preferred method for those objects.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the object. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="system">The system handle with the system entity to modify.</param>
        /// <param name="componentData">An object inheriting UnityEngine.Component.</param>
        /// <exception cref="ArgumentNullException">If the componentData object is not an instance of
        /// UnityEngine.Component.</exception>
        [StructuralChangeMethod]
        [ExcludeFromBurstCompatTesting("Takes managed object")]
        public void AddComponentObject(SystemHandle system, object componentData)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (componentData == null)
                throw new ArgumentNullException(nameof(componentData));
#endif

            ComponentType type = componentData.GetType();

            AddComponent(system.m_Entity, type);
            SetComponentObject(system.m_Entity, type, componentData);
        }

        /// <summary> Obsolete. Use <see cref="AddSharedComponentManaged{T}(Unity.Entities.Entity,T)"/> instead.</summary>
        /// <param name="entity">The entity.</param>
        /// <param name="componentData">The shared component value to set.</param>
        /// <typeparam name="T">The shared component type.</typeparam>
        /// <returns>Returns false if the entity already has the shared component. The shared component value is set either way.</returns>
        [Obsolete("Use AddSharedComponentManaged (UnityUpgradable) -> AddSharedComponentManaged<T>(*)", true)]
        public bool AddSharedComponentData<T>(Entity entity, T componentData) where T : struct, ISharedComponentData
        {
            return false;
        }


        /// <summary>
        /// Adds a shared component to an entity.
        /// </summary>
        /// <remarks>
        /// The fields of the `componentData` parameter are assigned to the added shared component.
        ///
        /// Adding a component to an entity changes its archetype and results in the entity being moved to a
        /// different chunk. The entity moves to a chunk with other entities that have the same shared component values.
        /// A new chunk is created if no chunk with the same archetype and shared component values currently exists.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <returns>Returns false if the entity already has the shared component. The shared component value is set either way.</returns>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entity">The entity.</param>
        /// <param name="componentData">The shared component value to set.</param>
        /// <typeparam name="T">The shared component type.</typeparam>
        [StructuralChangeMethod]
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public bool AddSharedComponentManaged<T>(Entity entity, T componentData) where T : struct, ISharedComponentData
        {
            var access = GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            var componentType = ComponentType.ReadWrite<T>();
            bool result;
            if (componentType.TypeIndex.IsManagedSharedComponent)
            {
                result = access->AddSharedComponentDataDuringStructuralChange_Managed(entity, componentData);
            }
            else
            {
                var defaultData = default(T);
                result = access->AddSharedComponentDataDuringStructuralChange_Unmanaged(entity, componentType, UnsafeUtility.AddressOf(ref componentData), UnsafeUtility.AddressOf(ref defaultData));
            }
            access->EndStructuralChanges(ref changes);
            return result;
        }

        /// <summary>
        /// Adds a shared component to an array of entities
        /// </summary>
        /// <remarks>
        /// The fields of the `componentData` parameter are assigned to the added shared component.
        ///
        /// Adding a component to an entity changes its archetype and results in the entity being moved to a
        /// different chunk. The entity moves to a chunk with other entities that have the same shared component values.
        /// A new chunk is created if no chunk with the same archetype and shared component values currently exists.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">A target <see cref="Entity"/> does not exist.</exception>
        /// <param name="entities">The target entities</param>
        /// <param name="componentData">The shared component value to set.</param>
        /// <typeparam name="T">The shared component type.</typeparam>
        [StructuralChangeMethod]
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void AddSharedComponentManaged<T>(NativeArray<Entity> entities, T componentData) where T : struct, ISharedComponentData
        {
            var access = GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            var componentType = ComponentType.ReadWrite<T>();
            if (TypeManager.IsManagedSharedComponent(componentType.TypeIndex))
            {
                access->AddSharedComponentDataDuringStructuralChange_Managed(entities, componentData);
            }
            else
            {
                var defaultData = default(T);
                access->AddSharedComponentDataDuringStructuralChange_Unmanaged(entities, componentType, UnsafeUtility.AddressOf(ref componentData), UnsafeUtility.AddressOf(ref defaultData));
            }
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Adds an unmanaged shared component to a set of entities defined by a EntityQuery.
        /// </summary>
        /// <remarks>
        /// The fields of the `componentData` parameter are assigned to all of the added shared components.
        ///
        /// Adding a component to an entity changes its archetype and results in the entity being moved to a
        /// different chunk. The entity moves to a chunk with other entities that have the same shared component values.
        /// A new chunk is created if no chunk with the same archetype and shared component values currently exists.
        ///
        /// Chunks that already have component <typeparamref name="T"/> will still update the component value
        /// to <paramref name="componentData"/>.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entityQuery">The EntityQuery defining a set of entities to modify.</param>
        /// <param name="componentData">The data to set.</param>
        /// <typeparam name="T">The data type of the shared component.</typeparam>
        [StructuralChangeMethod]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleSharedComponentData) })]
        public void AddSharedComponent<T>(EntityQuery entityQuery, T componentData)
            where T : unmanaged, ISharedComponentData
        {
            var access = GetCheckedEntityDataAccess();
            access->AssertQueryIsValid(entityQuery);
            var queryImpl = entityQuery._GetImpl();
            if (queryImpl->IsEmptyIgnoreFilter)
                return;
            var componentType = ComponentType.ReadWrite<T>();

            var changes = access->BeginStructuralChanges();
            var newSharedComponentDataIndex = access->InsertSharedComponent_Unmanaged(componentData);
            access->AddSharedComponentDataToQueryDuringStructuralChange_Unmanaged(queryImpl, newSharedComponentDataIndex, componentType,
                UnsafeUtility.AddressOf(ref componentData));
            access->EndStructuralChanges(ref changes);
        }

        /// <summary> Obsolete. Use <see cref="AddSharedComponentManaged{T}(Unity.Entities.Entity,T)"/> instead.</summary>
        /// <param name="entityQuery">The EntityQuery defining a set of entities to modify.</param>
        /// <param name="componentData">The data to set.</param>
        /// <typeparam name="T">The data type of the shared component.</typeparam>
        [Obsolete("Use AddSharedComponentManaged (UnityUpgradable) -> AddSharedComponentManaged<T>(*)")]
        public void AddSharedComponentData<T>(EntityQuery entityQuery, T componentData) where T : struct, ISharedComponentData {}

        /// <summary>
        /// Adds a possibly-managed shared component to a set of entities defined by a EntityQuery.
        /// </summary>
        /// <remarks>
        /// The fields of the `componentData` parameter are assigned to all of the added shared components.
        ///
        /// Adding a component to an entity changes its archetype and results in the entity being moved to a
        /// different chunk. The entity moves to a chunk with other entities that have the same shared component values.
        /// A new chunk is created if no chunk with the same archetype and shared component values currently exists.
        ///
        /// Chunks that already have component <typeparamref name="T"/> will still update the component value
        /// to <paramref name="componentData"/>.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entityQuery">The EntityQuery defining a set of entities to modify.</param>
        /// <param name="componentData">The data to set.</param>
        /// <typeparam name="T">The data type of the shared component.</typeparam>
        [StructuralChangeMethod]
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void AddSharedComponentManaged<T>(EntityQuery entityQuery, T componentData)
            where T : struct, ISharedComponentData
        {
            var access = GetCheckedEntityDataAccess();
            access->AssertQueryIsValid(entityQuery);
            var queryImpl = entityQuery._GetImpl();
            if (queryImpl->IsEmptyIgnoreFilter)
                return;
            var componentType = ComponentType.ReadWrite<T>();
            var changes = access->BeginStructuralChanges();
            var newSharedComponentDataIndex = access->InsertSharedComponent(componentData);
            if (TypeManager.IsManagedSharedComponent(componentType.TypeIndex))
            {
                access->AddSharedComponentDataToQueryDuringStructuralChange(queryImpl, newSharedComponentDataIndex,
                    componentType);
            }
            else
            {
                access->AddSharedComponentDataToQueryDuringStructuralChange_Unmanaged(queryImpl, newSharedComponentDataIndex,
                    componentType, UnsafeUtility.AddressOf(ref componentData));
            }
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Adds and removes components of an entity to match the specified EntityArchetype.
        /// </summary>
        /// <remarks>
        /// Components of the archetype which the entity already has will preserve their values.
        ///
        /// Components of the archetype which the entity does *not* have will get the default value for their types.
        ///
        /// Adding a component to an entity changes its archetype and results in the entity being moved to a
        /// different chunk. The entity moves to a chunk with other entities that have the same shared component values.
        /// A new chunk is created if no chunk with the same archetype and shared component values currently exists.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entity">The entity whose archetype to change.</param>
        /// <param name="archetype">The new archetype for the entity.</param>
        [StructuralChangeMethod]
        public void SetArchetype(Entity entity, EntityArchetype archetype)
        {
            archetype.CheckValidEntityArchetype();

            var access = GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;

            EntityComponentStore.AssertValidArchetype(ecs, archetype);
            ecs->AssertEntitiesExist(&entity, 1);

            var oldArchetype = ecs->GetArchetype(entity);
            var newArchetype = archetype.Archetype;

            if (oldArchetype == newArchetype)
            {
                return;
            }

            EntityComponentStore.AssertArchetypeDoesNotRemoveCleanupComponents(oldArchetype, newArchetype);

            var changes = access->BeginStructuralChanges();

            StructuralChange.MoveEntityArchetype(ecs, &entity, archetype.Archetype);

            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Adds or removes the <see cref="Disabled"/> component to an <see cref="Entity"/>.
        /// </summary>
        /// <remarks>
        /// Entities with the <see cref="Disabled"/> component are not matched by any <see cref="EntityQuery"/>, and are
        /// therefore not processed by systems. To override this behavior for a specific query, use
        /// EntityQueryOptions.IncludeDisabledEntities at query creation time.
        ///
        /// If the entity was converted from a prefab and thus has a <see cref="LinkedEntityGroup"/> component, the entire group will be enabled or disabled.
        ///
        /// This operation requires a structural change to add or remove the <see cref="Disabled"/> component. To enable or disable individual components
        /// without requiring a structural change, see <see cref="IEnableableComponent"/> and <see cref="SetComponentEnabled{T}"/>.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entity">The entity to enable or disable</param>
        /// <param name="enabled">True if the entity should be enabled</param>
        /// <seealso cref="IsEnabled(Entity)"/>
        [StructuralChangeMethod]
        public void SetEnabled(Entity entity, bool enabled)
        {
            var access = GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            access->SetEnabled(entity, enabled);
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Check whether an entire entity has the <see cref="Disabled"/> tag component.
        /// </summary>
        /// <remarks>
        /// Entities with the <see cref="Disabled"/> component are not matched by any <see cref="EntityQuery"/>, and are
        /// therefore not processed by systems. To override this behavior for a specific query, use
        /// EntityQueryOptions.IncludeDisabledEntities at query creation time.
        /// </remarks>
        /// <param name="entity">The entity to check</param>
        /// <returns>True if the target entity does not have <see cref="Disabled"/> tag component, or false if it does.</returns>
        /// <seealso cref="SetEnabled(Entity,bool)"/>
        public bool IsEnabled(Entity entity)
        {
            return GetCheckedEntityDataAccess()->IsEnabled(entity);
        }
        /// <summary> Obsolete. Use <see cref="IsEnabled(Entity)"/> instead.</summary>
        /// <param name="entity">The entity to check</param>
        /// <returns>True if the target entity does not have <see cref="Disabled"/> tag component, or false if it does.</returns>
        [Obsolete("This function has been renamed to IsEnabled().  (RemovedAfter Entities 1.0) (UnityUpgradable) -> IsEnabled(*)", false)]
        public bool GetEnabled(Entity entity)
        {
            return IsEnabled(entity);
        }

        /// <summary>
        /// Checks whether a given <see cref="ComponentType"/> is enabled on the specified <see cref="Entity"/>.
        /// </summary>
        /// <remarks>An entity with a disabled component will not match an <see cref="EntityQuery"/> that requires that component,
        /// and will match a query that excludes that component.</remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <typeparam name="T">The component type whose enabled status should be checked. This type must implement the
        /// <see cref="IEnableableComponent"/> interface.</typeparam>
        /// <param name="entity">The entity whose component should be checked.</param>
        /// <returns>True if the specified component is enabled, or false if it is disabled.</returns>
        /// <seealso cref="SetComponentEnabled{T}(Entity,bool)"/>
        /// <seealso cref="ComponentLookup{T}.IsComponentEnabled(Entity)"/>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleEnableableComponent) })]
        public bool IsComponentEnabled<T>(Entity entity) where T :
#if UNITY_DISABLE_MANAGED_COMPONENTS
            unmanaged,
#endif
            IEnableableComponent
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            var access = GetCheckedEntityDataAccess();
            if (!access->IsInExclusiveTransaction)
                access->DependencyManager->CompleteWriteDependency(typeIndex);
            return access->IsComponentEnabled(entity, typeIndex);
        }

        /// <summary>
        /// Checks whether a given <see cref="ComponentType"/> is enabled on the specified system using <see cref="SystemHandle"/>.
        /// </summary>
        /// <exception cref="ArgumentException">The <see cref="SystemHandle"/> does not exist.</exception>
        /// <typeparam name="T">The component type whose enabled status should be checked. This type must implement the
        /// <see cref="IEnableableComponent"/> interface.</typeparam>
        /// <param name="systemHandle">The system whose component should be checked.</param>
        /// <returns>True if the specified component is enabled, or false if it is disabled.</returns>
        /// <seealso cref="SetComponentEnabled{T}(SystemHandle,bool)"/>
        /// <seealso cref="ComponentLookup{T}.IsComponentEnabled(SystemHandle)"/>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleEnableableComponent) })]
        public bool IsComponentEnabled<T>(SystemHandle systemHandle) where T :
#if UNITY_DISABLE_MANAGED_COMPONENTS
            unmanaged,
#endif
            IEnableableComponent => IsComponentEnabled<T>(systemHandle.m_Entity);

        /// <summary>
        /// Enable or disable a <see cref="ComponentType"/> on the specified <see cref="SystemHandle"/>.
        /// </summary>
        /// <remarks>
        /// This operation does not cause a structural change, and does not affect the current value of the component.
        /// To enable/disable components from a job, use <see cref="ComponentLookup{T}.SetComponentEnabled(SystemHandle,bool)"/>.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="SystemHandle"/> does not exist.</exception>
        /// <typeparam name="T">The component type to enable or disable. This type must implement the
        /// <see cref="IEnableableComponent"/> interface.</typeparam>
        /// <param name="systemHandle">The system whose component should be enabled or disabled.</param>
        /// <param name="value">True if the specified component should be enabled, or false if it should be disabled.</param>
        /// <seealso cref="IsComponentEnabled{T}(SystemHandle)"/>
        /// <seealso cref="ComponentLookup{T}.SetComponentEnabled(SystemHandle,bool)"/>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleEnableableComponent) })]
        public void SetComponentEnabled<T>(SystemHandle systemHandle, bool value) where T :
#if UNITY_DISABLE_MANAGED_COMPONENTS
            unmanaged,
#endif
            IEnableableComponent => SetComponentEnabled<T>(systemHandle.m_Entity, value);

        /// <summary>
        /// Checks whether a given <see cref="ComponentType"/> is enabled on the specified <see cref="Entity"/>.
        /// </summary>
        /// <remarks>An entity with a disabled component will not match an <see cref="EntityQuery"/> that requires that component,
        /// and will match a query that excludes that component.</remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entity">The entity whose component should be checked.</param>
        /// <param name="componentType">The component type to enable or disable. This type must implement the
        /// <see cref="IEnableableComponent"/> interface.</param>
        /// <returns>True if the specified component is enabled, or false if it is disabled.</returns>
        /// <seealso cref="SetComponentEnabled(Entity,ComponentType,bool)"/>
        /// <seealso cref="ComponentLookup{T}.IsComponentEnabled(Entity)"/>
        public bool IsComponentEnabled(Entity entity, ComponentType componentType)
        {
            var access = GetCheckedEntityDataAccess();
            if (!access->IsInExclusiveTransaction)
                access->DependencyManager->CompleteWriteDependency(componentType.TypeIndex);
            return access->IsComponentEnabled(entity, componentType.TypeIndex);
        }
        /// <summary>
        /// Enable or disable a <see cref="ComponentType"/> on the specified <see cref="Entity"/>.
        /// </summary>
        /// <remarks>
        /// This operation does not cause a structural change, and does not affect the current value of the component.
        ///
        /// An entity with a disabled component will not match an <see cref="EntityQuery"/> that requires that component,
        /// and will match a query that excludes that component.
        ///
        /// To enable/disable components from a job, use <see cref="ComponentLookup{T}.SetComponentEnabled(Entity,bool)"/>.
        /// To enable/disable an entire <see cref="Entity"/>, use <see cref="SetEnabled(Entity,bool)"/> (which, unlike this function,
        /// does involve a structural change).
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <typeparam name="T">The component type to enable or disable. This type must implement the
        /// <see cref="IEnableableComponent"/> interface.</typeparam>
        /// <param name="entity">The entity whose component should be enabled or disabled.</param>
        /// <param name="value">True if the specified component should be enabled, or false if it should be disabled.</param>
        /// <seealso cref="IsComponentEnabled{T}(Entity)"/>
        /// <seealso cref="ComponentLookup{T}.SetComponentEnabled(Entity,bool)"/>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleEnableableComponent) })]
        public void SetComponentEnabled<T>(Entity entity, bool value) where T:
#if UNITY_DISABLE_MANAGED_COMPONENTS
            unmanaged,
#endif
            IEnableableComponent
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            var access = GetCheckedEntityDataAccess();
            if (!access->IsInExclusiveTransaction)
                access->DependencyManager->CompleteReadAndWriteDependency(typeIndex);
            access->SetComponentEnabled(entity, typeIndex, value);
        }

        /// <summary>
        /// Sets or clears the "is enabled" bit for the provided component on all entities in all chunks matched by the
        /// query, ignoring the current state of any enableable components in the query.</summary>
        /// <typeparam name="T">The component type which should be enabled or disabled on all matching chunks. This type
        /// must be included in the query's required types, and must implement <see cref="IEnableableComponent"/>.</typeparam>
        /// <param name="query">The query whose matching chunks should be affected.</param>
        /// <param name="value">If true, the component <typeparamref name="T"/> will be enabled on all entities in all
        /// matching chunks. Otherwise, the component will be disabled on all entities in all matching chunks.</param>
        /// <remarks>
        /// This method ignores the current state of all enableable components in the query. The target component
        /// will be enabled or disabled on every entity in every chunk that matches the query. Specifically, this function
        /// will enable disabled components on entities, even if the disabled component would cause its entity to not match
        /// the query.
        ///
        /// To enable or disable a component on all entities that match a query (while respecting the current status of
        /// enableable components), the recommended workaround is to use a simple IJobEntity or foreach loop.
        ///
        /// If any jobs are currently running which read or write the target component, this function will block until
        /// they complete before performing the requested operation.
        /// </remarks>
        /// <seealso cref="IsComponentEnabled(Entity,ComponentType)"/>
        /// <seealso cref="ComponentLookup{T}.SetComponentEnabled(Entity,bool)"/>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleEnableableComponent) })]
        public void SetComponentEnabled<T>(EntityQuery query, bool value) where T:
#if UNITY_DISABLE_MANAGED_COMPONENTS
            unmanaged,
#endif
            IEnableableComponent
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            query._GetImpl()->SetEnabledBitsOnAllChunks(typeIndex, value);
        }

        /// <summary>
        /// Sets or clears the "is enabled" bit for the provided component on all entities in all chunks matched by the
        /// query, ignoring the current state of any enableable components in the query.</summary>
        /// <param name="componentType">The component type which should be enabled or disabled on all matching chunks. This type
        /// must be included in the query's required types, and must implement <see cref="IEnableableComponent"/>.</param>
        /// <param name="query">The query whose matching chunks should be affected.</param>
        /// <param name="value">If true, the component <typeparamref name="T"/> will be enabled on all entities in all
        /// matching chunks. Otherwise, the component will be disabled on all entities in all matching chunks.</param>
        /// <remarks>
        /// This method ignores the current state of all enableable components in the query. The target component
        /// will be enabled or disabled on every entity in every chunk that matches the query. Specifically, this function
        /// will enable disabled components on entities, even if the disabled component would cause its entity to not match
        /// the query.
        ///
        /// To enable or disable a component on all entities that match a query (while respecting the current status of
        /// enableable components), the recommended workaround is to use a simple IJobEntity or foreach loop.
        ///
        /// If any jobs are currently running which read or write the target component, this function will block until
        /// they complete before performing the requested operation.
        /// </remarks>
        /// <seealso cref="IsComponentEnabled(Entity,ComponentType)"/>
        /// <seealso cref="ComponentLookup{T}.SetComponentEnabled(Entity,bool)"/>
        public void SetComponentEnabled(EntityQuery query, ComponentType componentType, bool value)
            => query._GetImpl()->SetEnabledBitsOnAllChunks(componentType.TypeIndex, value);

        /// <summary>
        /// Enable or disable <see cref="ComponentType"/> on the specified <see cref="Entity"/>.
        /// </summary>
        /// <remarks>
        /// This operation does not cause a structural change, and does not affect the current value of the component.
        ///
        /// An entity with a disabled component will not match an <see cref="EntityQuery"/> that requires that component,
        /// and will match a query that excludes that component.
        ///
        /// To enable/disable components from a job, use <see cref="ComponentLookup{T}.SetComponentEnabled(Entity,bool)"/>.
        /// To enable/disable an entire <see cref="Entity"/>, use <see cref="SetEnabled(Entity,bool)"/> (which, unlike this function,
        /// does involve a structural change).
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entity">The entity whose component should be enabled or disabled.</param>
        /// <param name="componentType">The component type to enable or disable. This type must implement the
        /// <see cref="IEnableableComponent"/> interface.</param>
        /// <param name="value">True if the specified component should be enabled, or false if it should be disabled.</param>
        /// <seealso cref="IsComponentEnabled(Entity,ComponentType)"/>
        /// <seealso cref="ComponentLookup{T}.SetComponentEnabled(Entity,bool)"/>
        public void SetComponentEnabled(Entity entity, ComponentType componentType, bool value)
        {
            var access = GetCheckedEntityDataAccess();
            if (!access->IsInExclusiveTransaction)
                access->DependencyManager->CompleteReadAndWriteDependency(componentType.TypeIndex);
            access->SetComponentEnabled(entity, componentType.TypeIndex, value);
        }

        /// <summary>
        /// Creates an entity having the specified archetype.
        /// </summary>
        /// <remarks>
        /// The EntityManager creates the entity in the first available chunk with the matching archetype that has
        /// enough space.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before creating the entity. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="archetype">The archetype for the new entity.</param>
        /// <returns>The Entity object that you can use to access the entity.</returns>
        [StructuralChangeMethod]
        public Entity CreateEntity(EntityArchetype archetype)
        {
            var access = GetCheckedEntityDataAccess();
            access->PrepareForAdditiveStructuralChanges(archetype.Archetype);
            var changes = access->BeginAdditiveStructuralChanges();
            var result = access->CreateEntityDuringStructuralChange(archetype);
            access->EndStructuralChanges(ref changes);
            return result;
        }

        /// <summary>
        /// Creates an entity having components of the specified types.
        /// </summary>
        /// <remarks>
        /// The EntityManager creates the entity in the first available chunk with the matching archetype that has
        /// enough space.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before creating the entity. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="types">The types of components to add to the new entity.</param>
        /// <returns>The Entity object that you can use to access the entity.</returns>
        [StructuralChangeMethod]
        [ExcludeFromBurstCompatTesting("Takes managed array")]
        public Entity CreateEntity(params ComponentType[] types)
        {
            return CreateEntity(CreateArchetype(types));
        }

        /// <summary>
        /// Creates an entity having components of the specified types.
        /// </summary>
        /// <remarks>
        /// The EntityManager creates the entity in the first available chunk with the matching archetype that has
        /// enough space.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before creating the entity. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="types">The types of components to add to the new entity.</param>
        /// <returns>The Entity object that you can use to access the entity.</returns>
        [StructuralChangeMethod]
        public Entity CreateEntity(ReadOnlySpan<ComponentType> types)
        {
            return CreateEntity(CreateArchetype(types));
        }

        /// <summary>
        /// Creates an entity with no components.
        /// </summary>
        /// <remarks>
        /// The EntityManager creates the entity in the first available chunk with the archetype having no components.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before setting the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <returns>The Entity object that you can use to access the entity.</returns>
        [StructuralChangeMethod]
        public Entity CreateEntity()
        {
            var access = GetCheckedEntityDataAccess();
            access->PrepareForAdditiveStructuralChanges();
            var changes = access->BeginAdditiveStructuralChanges();
            var result = access->CreateEntity();
            access->EndStructuralChanges(ref changes);
            return result;
        }

        /// <summary>
        /// Creates a set of entities of the specified archetype.
        /// </summary>
        /// <remarks>Fills the [NativeArray](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1.html)
        /// object assigned to the `entities` parameter with the Entity objects of the created entities. Each entity
        /// has the components specified by the <see cref="EntityArchetype"/> object assigned
        /// to the `archetype` parameter. The EntityManager adds these entities to the <see cref="World"/> entity list. Use the
        /// Entity objects in the array for further processing, such as setting the component values.</remarks>
        /// <param name="archetype">The archetype defining the structure for the new entities.</param>
        /// <param name="entities">An array to hold the Entity objects needed to access the new entities.
        /// The length of the array determines how many entities are created.</param>
        [StructuralChangeMethod]
        public void CreateEntity(EntityArchetype archetype, NativeArray<Entity> entities)
        {
            var access = GetCheckedEntityDataAccess();
            access->PrepareForAdditiveStructuralChanges(archetype.Archetype);
            var changes = access->BeginAdditiveStructuralChanges();
            access->CreateEntityDuringStructuralChange(archetype, entities);
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Creates a set of entities of the specified archetype.
        /// </summary>
        /// <remarks>Creates a [NativeArray](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1.html) of entities,
        /// each of which has the components specified by the <see cref="EntityArchetype"/> object assigned
        /// to the `archetype` parameter. The EntityManager adds these entities to the <see cref="World"/> entity list.</remarks>
        /// <param name="archetype">The archetype defining the structure for the new entities.</param>
        /// <param name="entityCount">The number of entities to create with the specified archetype.</param>
        /// <param name="allocator">How the created native array should be allocated.</param>
        /// <returns>
        /// A [NativeArray](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1.html) of entities
        /// with the given archetype.
        /// </returns>
        [StructuralChangeMethod]
        public NativeArray<Entity> CreateEntity(EntityArchetype archetype, int entityCount, AllocatorManager.AllocatorHandle allocator)
        {
            var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, allocator);
            var access = GetCheckedEntityDataAccess();
            access->PrepareForAdditiveStructuralChanges(archetype.Archetype);
            var changes = access->BeginAdditiveStructuralChanges();
            access->CreateEntityDuringStructuralChange(archetype, entities);
            access->EndStructuralChanges(ref changes);

            return entities;
        }

        /// <summary>
        /// Creates a set of entities of the specified archetype.
        /// </summary>
        /// <remarks>Unlike the other overloads, this does not create an array of Entity values. You don't always need the Entity value of a newly created entity because maybe you only need to access the entity through queries.</remarks>
        /// <param name="archetype">The archetype defining the structure for the new entities.</param>
        /// <param name="entityCount">The number of entities to create with the specified archetype.</param>
        [StructuralChangeMethod]
        public void CreateEntity(EntityArchetype archetype, int entityCount)
        {
            var access = GetCheckedEntityDataAccess();

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            // When entities journaling recording is enabled, we make a temp array to record the entities created
            if (Hint.Unlikely(access->EntityComponentStore->m_RecordToJournal != 0))
            {
                using var entities = CreateEntity(archetype, entityCount, Allocator.Temp);
                return;
            }
#endif

            access->PrepareForAdditiveStructuralChanges(archetype.Archetype);
            var changes = access->BeginAdditiveStructuralChanges();
            access->CreateEntityDuringStructuralChange(archetype, null, entityCount);
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Destroy all entities having a common set of component types.
        /// </summary>
        /// <remarks>
        /// Since entities in the same chunk share the same component structure, this function effectively destroys
        /// the chunks holding any entities identified by the `entityQueryFilter` parameter.
        ///
        /// If any entities matching the query have the <see cref="LinkedEntityGroup"/> component, this operation
        /// will fail if any entities in a <see cref="LinkedEntityGroup"/> buffer do not also match the query.
        /// </remarks>
        /// <param name="entityQuery">Defines the components an entity must have to qualify for destruction.</param>
        /// <exception cref="ArgumentException">Thrown if this operation would not destroy all entities in all
        /// <see cref="LinkedEntityGroup"/> buffers on all entities matched by <paramref name="entityQuery"/>.</exception>
        [StructuralChangeMethod]
        public void DestroyEntity(EntityQuery entityQuery)
        {
            var access = GetCheckedEntityDataAccess();
            access->AssertQueryIsValid(entityQuery);
            var queryImpl = entityQuery._GetImpl();
            if (queryImpl->IsEmptyIgnoreFilter)
                return;
            var changes = access->BeginStructuralChanges();
            access->DestroyEntitiesInQueryDuringStructuralChange(queryImpl);
            access->EndStructuralChanges(ref changes);
        }

#if ENTITY_STORE_V1
        /// <summary>
        /// Destroys all entities in the EntityManager and resets the internal entity ID version table.
        /// </summary>
        /// <remarks>
        /// This method can be used to reset an EntityManager for the purpose of creating data that can be written to disk with a deterministic, exact matching file on disk.
        /// It resets all chunk and entity version state so that it can be serialized to disk back to a state that is the same as a clean world.
        /// Archetypes and EntityQuery are not reset since they are often cached / owned by systems, but these are also not stored on disk.
        /// </remarks>
        [ExcludeFromBurstCompatTesting("Takes managed array")]
        public void DestroyAndResetAllEntities()
        {
            DestroyEntity(UniversalQueryWithSystems);
            if (Debug.EntityCount != 0)
                throw new System.ArgumentException("Destroying all entities failed. Some entities couldn't be deleted.");

            // FreeAllEntities also resets entity index
            var access = GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;
            ecs->FreeAllEntities(true);

            access->ManagedComponentStore.ResetManagedComponentStoreForDeserialization(0, ref *ecs);
            access->ManagedComponentStore.PrepareForDeserialize();
        }
#else
        // TODO - this function should be marked as deprecated, it doesn't do anymore the kind of reinitialization it was supposed to do.
        public void DestroyAndResetAllEntities()
        {
            DestroyEntity(UniversalQueryWithSystems);
            if (Debug.EntityCount != 0)
                throw new System.ArgumentException("Destroying all entities failed. Some entities couldn't be deleted.");
        }
#endif

        /// <summary>
        /// Destroys all entities in an array.
        /// </summary>
        /// <remarks>
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before destroying the entity. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="entities">An array containing the Entity objects of the entities to destroy.</param>
        [StructuralChangeMethod]
        public void DestroyEntity(NativeArray<Entity> entities)
        {
            DestroyEntityInternal((Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length);
        }

        /// <summary>
        /// Destroys all entities in a slice of an array.
        /// </summary>
        /// <remarks>
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before destroying the entity. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="entities">The slice of an array containing the Entity objects of the entities to destroy.</param>
        [StructuralChangeMethod]
        public void DestroyEntity(NativeSlice<Entity> entities)
        {
            DestroyEntityInternal((Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length);
        }

        /// <summary>
        /// Destroys an entity.
        /// </summary>
        /// <remarks>
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before destroying the entity. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="entity">The Entity object of the entity to destroy.</param>
        [StructuralChangeMethod]
        public void DestroyEntity(Entity entity)
        {
            DestroyEntityInternal(&entity, 1);
        }

        /// <summary>
        /// Clones an entity.
        /// </summary>
        /// <remarks>
        /// The new entity has the same archetype and component values as the original; however, <see cref="ICleanupComponentData"/>
        /// and <see cref="Prefab"/> components are removed from the clone.
        ///
        /// If the source entity was converted from a prefab and thus has a <see cref="LinkedEntityGroup"/> component,
        /// the entire group is cloned as a new set of entities. Entity references on components that are being cloned to entities inside
        /// the set are remapped to the instantiated entities.
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before creating the entity. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="srcEntity">The entity to clone.</param>
        /// <returns>The Entity object for the new entity.</returns>
        [StructuralChangeMethod]
        public Entity Instantiate(Entity srcEntity)
        {
            Entity entity;
            var access = GetCheckedEntityDataAccess();
            var archetype = access->EntityComponentStore->GetArchetype(srcEntity);
            access->PrepareForAdditiveStructuralChanges(archetype->InstantiateArchetype);
            var changes = access->BeginAdditiveStructuralChanges();
            access->InstantiateInternalDuringStructuralChange(srcEntity, &entity, 1);
            access->EndStructuralChanges(ref changes);
            return entity;
        }

        /// <summary>
        /// Makes multiple clones of an entity.
        /// </summary>
        /// <remarks>
        /// The new entity has the same archetype and component values as the original, however cleanup and prefab tag components are removed from the clone.
        ///
        /// If the source entity has a <see cref="LinkedEntityGroup"/> component, the entire group is cloned as a new
        /// set of entities. Entity references on components that are being cloned to entities inside the set are remapped to the instantiated entities.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before creating the entity. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="srcEntity">The entity to clone.</param>
        /// <param name="outputEntities">An array to receive the Entity objects of the root entity in each clone.
        /// The length of this array determines the number of clones.</param>
        [StructuralChangeMethod]
        public void Instantiate(Entity srcEntity, NativeArray<Entity> outputEntities)
        {
            var access = GetCheckedEntityDataAccess();
            var archetype = access->EntityComponentStore->GetArchetype(srcEntity);
            access->PrepareForAdditiveStructuralChanges(archetype->InstantiateArchetype);
            var changes = access->BeginAdditiveStructuralChanges();
            access->InstantiateInternalDuringStructuralChange(srcEntity, (Entity*)outputEntities.GetUnsafePtr(), outputEntities.Length);
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Makes multiple clones of an entity.
        /// </summary>
        /// <remarks>
        /// The new entity has the same archetype and component values as the original, however cleanup and prefab tag components are removed from the clone.
        ///
        /// If the source entity has a <see cref="LinkedEntityGroup"/> component, the entire group is cloned as a new
        /// set of entities. Entity references on components that are being cloned to entities inside the set are remapped to the instantiated entities.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before creating the entity. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="srcEntity">The entity to clone.</param>
        /// <param name="instanceCount">The number of entities to instantiate with the same components as the source entity.</param>
        /// <param name="allocator">How the created native array should be allocated.</param>
        /// <returns>A [NativeArray](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1.html) of entities.</returns>
        [StructuralChangeMethod]
        public NativeArray<Entity> Instantiate(Entity srcEntity, int instanceCount, AllocatorManager.AllocatorHandle allocator)
        {
            var access = GetCheckedEntityDataAccess();

            NativeArray<Entity> entities;
            entities = CollectionHelper.CreateNativeArray<Entity>(instanceCount, allocator);
            var archetype = access->EntityComponentStore->GetArchetype(srcEntity);
            access->PrepareForAdditiveStructuralChanges(archetype->InstantiateArchetype);
            var changes = access->BeginAdditiveStructuralChanges();
            access->InstantiateInternalDuringStructuralChange(srcEntity, (Entity*)entities.GetUnsafePtr(), instanceCount);
            access->EndStructuralChanges(ref changes);
            return entities;
        }

        /// <summary>
        /// Clones a set of entities.
        /// </summary>
        /// <remarks>
        /// The new entity has the same archetype and component values as the original, however cleanup and prefab tag components are removed from the clone.
        ///
        /// Entity references on components that are being cloned to entities inside the set are remapped to the instantiated entities.
        /// This method overload ignores the <see cref="LinkedEntityGroup"/> component,
        /// since the group of entities that will be cloned is passed explicitly.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before creating the entity. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="srcEntities">The set of entities to clone</param>
        /// <param name="outputEntities">the set of entities that were cloned. outputEntities.Length must match srcEntities.Length</param>
        [StructuralChangeMethod]
        [Obsolete("This method will be removed in a future Entities release. " +
                  "If you wish to clone the full hierarchy of every entity in a given array, simply loop through the array to and invoke `EntityManager.Instantiate(Entity entity)` on each entity.")]
        public void Instantiate(NativeArray<Entity> srcEntities, NativeArray<Entity> outputEntities)
        {
            var access = GetCheckedEntityDataAccess();
            access->PrepareForInstantiateAdditiveStructuralChanges(srcEntities);
            var changes = access->BeginAdditiveStructuralChanges();
            access->InstantiateInternalDuringStructuralChange((Entity*)srcEntities.GetUnsafeReadOnlyPtr(), (Entity*)outputEntities.GetUnsafePtr(), srcEntities.Length, outputEntities.Length, true);
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Clones a set of entities, different from Instantiate because it does not remove the <see cref="Prefab"/> tag component.
        /// </summary>
        /// <remarks>
        /// The new entity has the same archetype and component values as the original, however cleanup components are removed from the clone.
        ///
        /// Entity references on components that are being cloned to entities inside the set are remapped to the instantiated entities.
        /// This method overload ignores the <see cref="LinkedEntityGroup"/> component,
        /// since the group of entities that will be cloned is passed explicitly.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before creating the entity. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="srcEntities">The set of entities to clone</param>
        /// <param name="outputEntities">the set of entities that were cloned. outputEntities.Length must match srcEntities.Length</param>
        [StructuralChangeMethod]
        [ExcludeFromCoverage]
        [Obsolete("This method is not safe to use in some contexts, and will be removed from the public API in a future Entities release. To create copies of a prefab Entity, use EntityManager.Instantiate().")]
        public void CopyEntities(NativeArray<Entity> srcEntities, NativeArray<Entity> outputEntities)
        {
            CopyEntitiesInternal(srcEntities, outputEntities);
        }
        internal void CopyEntitiesInternal(NativeArray<Entity> srcEntities, NativeArray<Entity> outputEntities)
        {
            var access = GetCheckedEntityDataAccess();
            access->PrepareForCopyAdditiveStructuralChanges(srcEntities);
            var changes = access->BeginAdditiveStructuralChanges();
            access->InstantiateInternalDuringStructuralChange((Entity*)srcEntities.GetUnsafeReadOnlyPtr(), (Entity*)outputEntities.GetUnsafePtr(), srcEntities.Length, outputEntities.Length, false);
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// **Obsolete.** The use of this function is not recommended.
        /// Consider using a more idiomatic ECS way to detect creation and destruction, like (enableable) tags and cleanup components.
        /// </summary>
        /// <remarks>
        /// Detects the created and destroyed entities compared to last time the method was called with the given state.
        /// Entities must be fully destroyed, if cleanup components keep it alive it still counts as not yet destroyed.
        /// <see cref="EntityCommandBuffer"/> instances that have not been played back will have no effect on this until they are played back.
        /// </remarks>
        /// <param name="state">The same state list must be passed when you call this method, it remembers the entities that were already notified created and destroyed.</param>
        /// <param name="createdEntities">The entities that were created.</param>
        /// <param name="destroyedEntities">The entities that were destroyed.</param>
        /// <returns>a <see cref="JobHandle"/> for the job scheduled by this method.</returns>
        [Obsolete("Consider using a more idiomatic ECS way to detect creation and destruction, like (enableable) tags and cleanup components.")]
        public JobHandle GetCreatedAndDestroyedEntitiesAsync(NativeList<int> state, NativeList<Entity> createdEntities, NativeList<Entity> destroyedEntities)
        {
            return GetCheckedEntityDataAccess()->GetCreatedAndDestroyedEntitiesAsync(state, createdEntities, destroyedEntities);
        }

        /// <summary>
        /// **Obsolete.** The use of this function is not recommended.
        /// Consider using a more idiomatic ECS way to detect creation and destruction, like (enableable) tags and cleanup components.
        /// </summary>
        /// <remarks>
        /// Detects the created and destroyed entities compared to last time the method was called with the given state.
        /// Entities must be fully destroyed, if cleanup components keep it alive it still counts as not yet destroyed.
        /// <see cref="EntityCommandBuffer"/> instances that have not been played back will have no effect on this until they are played back.
        /// </remarks>
        /// <param name="state">The same state list must be passed when you call this method, it remembers the entities that were already notified created and destroyed.</param>
        /// <param name="createdEntities">The entities that were created.</param>
        /// <param name="destroyedEntities">The entities that were destroyed.</param>
        [Obsolete("Consider using a more idiomatic ECS way to detect creation and destruction, like (enableable) tags and cleanup components.")]
        public void GetCreatedAndDestroyedEntities(NativeList<int> state, NativeList<Entity> createdEntities, NativeList<Entity> destroyedEntities)
        {
            GetCheckedEntityDataAccess()->GetCreatedAndDestroyedEntities(state,  createdEntities, destroyedEntities);
        }

        /// <summary>
        /// Creates an archetype from a set of component types.
        /// </summary>
        /// <remarks>
        /// Creates a new archetype in the ECS framework's internal type registry, unless the archetype already exists.
        /// </remarks>
        /// <param name="types">The component types to include as part of the archetype.</param>
        /// <returns>The EntityArchetype object for the archetype.</returns>
        [ExcludeFromBurstCompatTesting("Takes managed array")]
        public EntityArchetype CreateArchetype(params ComponentType[] types)
        {
            if (types == null)
                throw new NullReferenceException(nameof(types));

            fixed(ComponentType* typesPtr = types)
            {
                return CreateArchetype(typesPtr, types.Length);
            }
        }

        /// <summary>
        /// Creates an archetype from a set of component types.
        /// </summary>
        /// <remarks>
        /// Creates a new archetype in the ECS framework's internal type registry, unless the archetype already exists.
        /// </remarks>
        /// <param name="types">The component types to include as part of the archetype.</param>
        /// <returns>The EntityArchetype object for the archetype.</returns>
        public EntityArchetype CreateArchetype(ReadOnlySpan<ComponentType> types)
        {
            fixed(ComponentType* typesPtr = types)
            {
                return CreateArchetype(typesPtr, types.Length);
            }
        }

        /// <summary>
        /// Creates an archetype from a set of component types.
        /// </summary>
        /// <remarks>
        /// Creates a new archetype in the ECS framework's internal type registry, unless the archetype already exists.
        /// </remarks>
        /// <param name="types">The component types to include as part of the archetype.</param>
        /// <returns>The EntityArchetype object for the archetype.</returns>
        public EntityArchetype CreateArchetype(NativeArray<ComponentType> types)
        {
            return CreateArchetype((ComponentType*)types.GetUnsafeReadOnlyPtr(), types.Length);
        }

        internal EntityArchetype CreateArchetype(ComponentType* types, int count)
        {
            var access = GetCheckedEntityDataAccess();
            access->PrepareForAdditiveStructuralChanges();
            var changes = access->BeginAdditiveStructuralChanges();
            var result = access->CreateArchetype(types, count, true);
            // CheckIsAdditiveArchetypeStructuralChangePossible may throw: in that case, the archetype is
            // leaked. This is fine because the exception we thrown highlights an issue that must be solved
            // to assure the correct behaviour of the user code.
            access->CheckIsAdditiveArchetypeStructuralChangePossible(result.Archetype);
            access->EndStructuralChanges(ref changes);
            return result;
        }

        /// <summary>
        /// Creates an archetype from a set of component types.
        /// </summary>
        /// <remarks>
        /// Creates a new archetype in the ECS framework's internal type registry, unless the archetype already exists.
        /// </remarks>
        /// <param name="types">The component types to include as part of the archetype.</param>
        /// <returns>The EntityArchetype object for the archetype.</returns>
        internal EntityArchetype CreateArchetypeWithoutSimulateComponent(NativeArray<ComponentType> types)
        {
            return CreateArchetypeWithoutSimulateComponent((ComponentType*)types.GetUnsafeReadOnlyPtr(), types.Length);
        }

        internal EntityArchetype CreateArchetypeWithoutSimulateComponent(ComponentType* types, int count)
        {
            var access = GetCheckedEntityDataAccess();
            access->PrepareForAdditiveStructuralChanges();
            var changes = access->BeginAdditiveStructuralChanges();
            var result = access->CreateArchetype(types, count, false);
            // CheckIsAdditiveArchetypeStructuralChangePossible may throw: in that case, the archetype is
            // leaked. This is fine because the exception we thrown highlights an issue that must be solved
            // to assure the correct behaviour of the user code.
            access->CheckIsAdditiveArchetypeStructuralChangePossible(result.Archetype);
            access->EndStructuralChanges(ref changes);
            return result;
        }

        struct IsolateCopiedEntities : IComponentData {}

        /// <summary>
        /// Instantiates / Copies all entities from srcEntityManager and copies them into this EntityManager.
        /// Entity references on components that are being cloned to entities inside the srcEntities set are remapped to the instantiated entities.
        /// </summary>
        /// <param name="srcEntityManager">The EntityManager to copy from</param>
        /// <param name="srcEntities">Entities in the srcEntityManager that have component with Entity references</param>
        /// <param name="outputEntities">New entities that have been remapped from srcEntities</param>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void CopyEntitiesFrom(EntityManager srcEntityManager, NativeArray<Entity> srcEntities, NativeArray<Entity> outputEntities = default)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (outputEntities.IsCreated && outputEntities.Length != srcEntities.Length)
                throw  new ArgumentException("outputEntities.Length must match srcEntities.Length");
#endif

            using (var srcManagerInstances = new NativeArray<Entity>(srcEntities.Length, Allocator.Temp))
            {
                srcEntityManager.CopyEntitiesInternal(srcEntities, srcManagerInstances);
                srcEntityManager.AddComponent(srcManagerInstances, ComponentType.ReadWrite<IsolateCopiedEntities>());

                var instantiated = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<IsolateCopiedEntities>()
                    .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
                    .Build(srcEntityManager);

                using (var entityRemapping = srcEntityManager.CreateEntityRemapArray(Allocator.TempJob))
                {
                    MoveEntitiesFromInternalQuery(srcEntityManager, instantiated, entityRemapping);

                    EntityRemapUtility.GetTargets(out var output, entityRemapping);
                    RemoveComponent(output, ComponentType.ReadWrite<IsolateCopiedEntities>());
                    output.Dispose();

                    if (outputEntities.IsCreated)
                    {
                        for (int i = 0; i != outputEntities.Length; i++)
                            outputEntities[i] = entityRemapping[srcManagerInstances[i].Index].Target;
                    }
                }
            }
        }

        /// <summary>
        /// Copies all entities from srcEntityManager and replaces all entities in this EntityManager
        /// </summary>
        /// <remarks>
        /// Guarantees that the chunk layout and order of the entities will match exactly, thus this method can be used for deterministic rollback.
        /// This feature is not complete and only supports a subset of the EntityManager features at the moment:
        /// * Currently it copies all CleanupComponents (They should not be copied)
        /// * Currently does not support class based components
        /// </remarks>
        /// <param name="srcEntityManager">The EntityManager to copy from</param>
        [Obsolete("This function only works in a narrow set of circumstances and has semantics that differ from a regular copy.", false)]
#if ENTITY_STORE_V1
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void CopyAndReplaceEntitiesFrom(EntityManager srcEntityManager)
        {
            srcEntityManager.CompleteAllTrackedJobs();
            CompleteAllTrackedJobs();

            var srcAccess = srcEntityManager.GetCheckedEntityDataAccess();
            var selfAccess = GetCheckedEntityDataAccess();

            int maxSrcChunksCount = srcAccess->m_UniversalQueryWithChunksAndSystems.CalculateChunkCountWithoutFiltering();
            using (var srcChunks = srcAccess->m_UniversalQueryWithChunksAndSystems.ToArchetypeChunkListAsync(Allocator.TempJob, out var srcChunksJob))
            using (var dstChunks = selfAccess->m_UniversalQueryWithChunksAndSystems.ToArchetypeChunkListAsync(Allocator.TempJob, out var dstChunksJob))
            {
                using (var archetypeChunkChanges = EntityDiffer.GetArchetypeChunkChanges(
                    srcChunks,
                    dstChunks,
                    maxSrcChunksCount,
                    Allocator.TempJob,
                    jobHandle: out var archetypeChunkChangesJob,
                    dependsOn: JobHandle.CombineDependencies(srcChunksJob, dstChunksJob)))
                {
                    archetypeChunkChangesJob.Complete();

                    EntityDiffer.CopyAndReplaceChunks(srcEntityManager, this, selfAccess->m_UniversalQueryWithChunksAndSystems, archetypeChunkChanges);
                    EntityComponentStore.AssertAllEntitiesCopied(srcAccess->EntityComponentStore, selfAccess->EntityComponentStore);
                }
            }
        }
#else
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void CopyAndReplaceEntitiesFrom(EntityManager srcEntityManager, NativeArray<EntityRemapUtility.EntityRemapInfo> remap = default)
        {
            srcEntityManager.CompleteAllTrackedJobs();
            CompleteAllTrackedJobs();

            var srcAccess = srcEntityManager.GetCheckedEntityDataAccess();
            var selfAccess = GetCheckedEntityDataAccess();

            using (var srcChunks = srcAccess->m_UniversalQueryWithChunksAndSystems.ToArchetypeChunkListAsync(Allocator.TempJob, out var srcChunksJob))
            using (var dstChunks = selfAccess->m_UniversalQueryWithChunksAndSystems.ToArchetypeChunkListAsync(Allocator.TempJob, out var dstChunksJob))
            {
                srcChunksJob.Complete();
                dstChunksJob.Complete();

                using var archetypeChunkChanges = new EntityDiffer.ArchetypeChunkChanges(Allocator.TempJob);
                var createdChunks = archetypeChunkChanges.CreatedSrcChunks;
                var destroyedChunks = archetypeChunkChanges.DestroyedDstChunks;

                {
                    var chunks = createdChunks.Chunks;
                    var flags = createdChunks.Flags;
                    var entityCounts = createdChunks.EntityCounts;

                    for (int ci = 0, cc = srcChunks.Length, count = 0; ci < cc; ci++)
                    {
                        chunks.Add(srcChunks[ci]);
                        flags.Add(EntityDiffer.ArchetypeChunkChangeFlags.None);
                        entityCounts.Add(count);
                        count += srcChunks[ci].Count;
                    }
                }

                {
                    var chunks = destroyedChunks.Chunks;
                    var flags = destroyedChunks.Flags;
                    var entityCounts = destroyedChunks.EntityCounts;
                    for (int ci = 0, cc = dstChunks.Length, count = 0; ci < cc; ci++)
                    {
                        chunks.Add(dstChunks[ci]);
                        flags.Add(EntityDiffer.ArchetypeChunkChangeFlags.None);
                        entityCounts.Add(count);
                        count += dstChunks[ci].Count;
                    }
                }

                EntityDiffer.CopyAndReplaceChunks(srcEntityManager, this, selfAccess->m_UniversalQueryWithChunksAndSystems, archetypeChunkChanges, remap);
            }
        }
#endif

        /// <summary>
        /// Moves all entities managed by the specified EntityManager to the world of this EntityManager.
        /// </summary>
        /// <remarks>
        /// The entities moved are owned by this EntityManager.
        ///
        /// Each <see cref="World"/> has one EntityManager, which manages all the entities in that world. This function
        /// allows you to transfer entities from one World to another.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before moving the entity. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="srcEntities">The EntityManager whose entities are appropriated.</param>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void MoveEntitiesFrom(EntityManager srcEntities)
        {
            using(k_ProfileMoveEntitiesFrom.Auto())
            using (var entityRemapping = srcEntities.CreateEntityRemapArray(Allocator.TempJob))
                MoveEntitiesFromInternalAll(srcEntities, entityRemapping);
        }

        /// <summary>
        /// Moves all entities managed by the specified EntityManager to the <see cref="World"/> of this EntityManager and fills
        /// an array with their Entity objects.
        /// </summary>
        /// <remarks>
        /// After the move, the entities are managed by this EntityManager. Use the `output` array to make post-move
        /// changes to the transferred entities.
        ///
        /// Each world has one EntityManager, which manages all the entities in that world. This function
        /// allows you to transfer entities from one World to another.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before moving the entity. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="output">An array to receive the Entity objects of the transferred entities.</param>
        /// <param name="srcEntities">The EntityManager whose entities are appropriated.</param>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void MoveEntitiesFrom(out NativeArray<Entity> output, EntityManager srcEntities)
        {
            using(k_ProfileMoveEntitiesFrom.Auto())
            using (var entityRemapping = srcEntities.CreateEntityRemapArray(Allocator.TempJob))
            {
                MoveEntitiesFromInternalAll(srcEntities, entityRemapping);
                EntityRemapUtility.GetTargets(out output, entityRemapping);
            }
        }

        /// <summary>
        /// Moves all entities managed by the specified EntityManager to the <see cref="World"/> of this EntityManager and fills
        /// an array with their <see cref="Entity"/> objects.
        /// </summary>
        /// <remarks>
        /// After the move, the entities are managed by this EntityManager. Use the `output` array to make post-move
        /// changes to the transferred entities.
        ///
        /// Each world has one EntityManager, which manages all the entities in that world. This function
        /// allows you to transfer entities from one World to another.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before moving the entity. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="output">An array to receive the Entity objects of the transferred entities.</param>
        /// <param name="srcEntities">The EntityManager whose entities are appropriated.</param>
        /// <param name="entityRemapping">A set of entity transformations to make during the transfer.</param>
        /// <exception cref="ArgumentException"></exception>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void MoveEntitiesFrom(out NativeArray<Entity> output, EntityManager srcEntities, NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping)
        {
            using (k_ProfileMoveEntitiesFrom.Auto())
            {
                MoveEntitiesFromInternalAll(srcEntities, entityRemapping);
                EntityRemapUtility.GetTargets(out output, entityRemapping);
            }
        }

        /// <summary>
        /// Moves all entities managed by the specified EntityManager to the <see cref="World"/> of this EntityManager.
        /// </summary>
        /// <remarks>
        /// After the move, the entities are managed by this EntityManager.
        ///
        /// Each World has one EntityManager, which manages all the entities in that world. This function
        /// allows you to transfer entities from one world to another.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before moving the entity. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="srcEntities">The EntityManager whose entities are appropriated.</param>
        /// <param name="entityRemapping">A set of entity transformations to make during the transfer.</param>
        /// <exception cref="ArgumentException">Thrown if you attempt to transfer entities to the EntityManager
        /// that already owns them.</exception>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void MoveEntitiesFrom(EntityManager srcEntities, NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping)
        {
            using (k_ProfileMoveEntitiesFrom.Auto())
                MoveEntitiesFromInternalAll(srcEntities, entityRemapping);

        }

        /// <summary>
        /// Moves a selection of the entities managed by the specified EntityManager to the <see cref="World"/> of this EntityManager
        /// and fills an array with their <see cref="Entity"/> objects.
        /// </summary>
        /// <remarks>
        /// After the move, the entities are managed by this EntityManager. Use the `output` array to make post-move
        /// changes to the transferred entities.
        ///
        /// Each world has one EntityManager, which manages all the entities in that world. This function
        /// allows you to transfer entities from one World to another.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before moving the entity. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="srcEntities">The EntityManager whose entities are appropriated.</param>
        /// <param name="filter">A EntityQuery that defines the entities to move. Must be part of the source
        /// World. This query must not reference any types that implement IEnableableComponent.</param>
        /// <exception cref="ArgumentException"></exception>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void MoveEntitiesFrom(EntityManager srcEntities, EntityQuery filter)
        {
            using (k_ProfileMoveEntitiesFrom.Auto())
            using (var entityRemapping = srcEntities.CreateEntityRemapArray(Allocator.TempJob))
                MoveEntitiesFromInternalQuery(srcEntities, filter, entityRemapping);
        }

        /// <summary>
        /// Moves a selection of the entities managed by the specified EntityManager to the <see cref="World"/> of this EntityManager
        /// and fills an array with their <see cref="Entity"/> objects.
        /// </summary>
        /// <remarks>
        /// After the move, the entities are managed by this EntityManager. Use the `output` array to make post-move
        /// changes to the transferred entities.
        ///
        /// Each world has one EntityManager, which manages all the entities in that world. This function
        /// allows you to transfer entities from one World to another.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before moving the entity. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="output">An array to receive the Entity objects of the transferred entities.</param>
        /// <param name="srcEntities">The EntityManager whose entities are appropriated.</param>
        /// <param name="filter">A EntityQuery that defines the entities to move. Must be part of the source
        /// World. This query must not reference any types that implement IEnableableComponent.</param>
        /// <param name="entityRemapping">A set of entity transformations to make during the transfer.</param>
        /// <exception cref="ArgumentException"></exception>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void MoveEntitiesFrom(out NativeArray<Entity> output, EntityManager srcEntities, EntityQuery filter, NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping)
        {
            using (k_ProfileMoveEntitiesFrom.Auto())
            {
                MoveEntitiesFromInternalQuery(srcEntities, filter, entityRemapping);
                EntityRemapUtility.GetTargets(out output, entityRemapping);
            }
        }

        /// <summary>
        /// Moves a selection of the entities managed by the specified EntityManager to the <see cref="World"/> of this EntityManager.
        /// </summary>
        /// <remarks>
        /// After the move, the entities are managed by this EntityManager.
        ///
        /// Each world has one EntityManager, which manages all the entities in that world. This function
        /// allows you to transfer entities from one World to another.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before moving the entity. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="srcEntities">The EntityManager whose entities are appropriated.</param>
        /// <param name="filter">A EntityQuery that defines the entities to move. Must be part of the source
        /// World. This query must not reference any types that implement IEnableableComponent.</param>
        /// <param name="entityRemapping">A set of entity transformations to make during the transfer.</param>
        /// <exception cref="ArgumentException">Thrown if the EntityQuery object used as the `filter` comes
        /// from a different world than the `srcEntities` EntityManager.</exception>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void MoveEntitiesFrom(EntityManager srcEntities, EntityQuery filter, NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping)
        {
            using (k_ProfileMoveEntitiesFrom.Auto())
                MoveEntitiesFromInternalQuery(srcEntities, filter, entityRemapping);

        }

        /// <summary>
        /// Moves a selection of the entities managed by the specified EntityManager to the <see cref="World"/> of this EntityManager
        /// and fills an array with their <see cref="Entity"/> objects.
        /// </summary>
        /// <remarks>
        /// After the move, the entities are managed by this EntityManager. Use the `output` array to make post-move
        /// changes to the transferred entities.
        ///
        /// Each world has one EntityManager, which manages all the entities in that world. This function
        /// allows you to transfer entities from one World to another.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before moving the entity. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="output">An array to receive the Entity objects of the transferred entities.</param>
        /// <param name="srcEntities">The EntityManager whose entities are appropriated.</param>
        /// <param name="filter">A EntityQuery that defines the entities to move. Must be part of the source
        /// World. This query must not reference any types that implement IEnableableComponent.</param>
        /// <exception cref="ArgumentException"></exception>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void MoveEntitiesFrom(out NativeArray<Entity> output, EntityManager srcEntities, EntityQuery filter)
        {
            using (k_ProfileMoveEntitiesFrom.Auto())
            using (var entityRemapping = srcEntities.CreateEntityRemapArray(Allocator.TempJob))
            {
                MoveEntitiesFromInternalQuery(srcEntities, filter, entityRemapping);
                EntityRemapUtility.GetTargets(out output, entityRemapping);
            }
        }

        /// <summary>
        /// Creates a remapping array with one element for each entity in the <see cref="World"/>.
        /// </summary>
        /// <param name="allocator">The type of memory allocation to use when creating the array.</param>
        /// <returns>An array containing a no-op identity transformation for each entity.</returns>
        public NativeArray<EntityRemapUtility.EntityRemapInfo> CreateEntityRemapArray(AllocatorManager.AllocatorHandle allocator)
        {
#if !ENTITY_STORE_V1
            var remapSize = HighestEntityIndex() + 1;
#else
            var remapSize = EntityCapacity;
#endif
            var array = CollectionHelper.CreateNativeArray<EntityRemapUtility.EntityRemapInfo>(remapSize, allocator);
            return array;
        }

        /// <summary>
        /// Gets the version number of the specified component type.
        /// </summary>
        /// <remarks>This version number is incremented each time there is a structural change involving the specified
        /// type of component. Such changes include creating or destroying entities that have this component and adding
        /// or removing the component type from an entity. Shared components are not covered by this version;
        /// see <see cref="GetSharedComponentOrderVersion{T}(T)"/>.
        ///
        /// Version numbers can overflow. To compare if one version is more recent than another, directly checking if
        /// VersionB is greater than VersionA is not sufficient. Instead, use the helper function:
        ///
        /// <code>
        /// bool VersionBisNewer = ChangeVersionUtility.DidChange(VersionB, VersionA);
        /// </code>
        /// </remarks>
        /// <typeparam name="T">The component type.</typeparam>
        /// <returns>The current version number.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public int GetComponentOrderVersion<T>()
        {
            var access = GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;
            return ecs->GetComponentTypeOrderVersion(TypeManager.GetTypeIndex<T>());
        }

        // @TODO documentation for serialization/deserialization
        /// <summary>
        /// Prepares an empty <see cref="World"/> to load serialized entities.
        /// </summary>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void PrepareForDeserialize()
        {
            if (Debug.EntityCount != 0)
            {
                using (var allEntities = GetAllEntities())
                {
                    throw new System.ArgumentException($"PrepareForDeserialize requires the world to be completely empty, but there are {allEntities.Length}.\nFor example: {Debug.GetEntityInfo(allEntities[0])}");
                }
            }

            GetCheckedEntityDataAccess()->ManagedComponentStore.PrepareForDeserialize();
        }

        /// <summary>
        /// Waits for all tracked jobs to complete.
        /// </summary>
        /// <remarks>Calling <see cref="CompleteAllTrackedJobs"/> blocks the main thread until all currently running tracked Jobs finish. </remarks>
        /// <remarks>Tracked JobHandles for this <see cref="World"/> include every systems resulting JobHandle directly after their OnUpdate. </remarks>
        public void CompleteAllTrackedJobs()
        {
            GetCheckedEntityDataAccess()->CompleteAllTrackedJobs();
        }

        /// <summary>
        /// Obsolete. Use <see cref="CompleteAllTrackedJobs"/> instead.
        /// </summary>
        [Obsolete("CompleteAllJobs() has been deprecated. Use CompleteAllTrackedJobs() instead (RemovedAfter 2023-04-08) (UnityUpgradable) -> CompleteAllTrackedJobs()", true)]
        public void CompleteAllJobs()
        {
            GetCheckedEntityDataAccess()->CompleteAllTrackedJobs();
        }

        /// <summary>
        /// Gets the dynamic type object required to access a chunk component of type T.
        /// </summary>
        /// <remarks>
        /// To access a component stored in a chunk, you must have the type registry information for the component.
        /// This function provides that information. Use the returned <see cref="ComponentTypeHandle{T}"/>
        /// object with the functions of an <see cref="ArchetypeChunk"/> object to get information about the components
        /// in that chunk and to access the component values.
        /// </remarks>
        /// <param name="isReadOnly">Specify whether the access to the component through this object is read only
        /// or read and write. For managed components isReadonly will always be treated as false.</param>
        /// <typeparam name="T">The compile-time type of the component.</typeparam>
        /// <returns>The run-time type information of the component.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public ComponentTypeHandle<T> GetComponentTypeHandle<T>(bool isReadOnly)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var access = GetCheckedEntityDataAccess();
            var typeIndex = TypeManager.GetTypeIndex<T>();
            return new ComponentTypeHandle<T>(
                access->DependencyManager->Safety.GetSafetyHandleForComponentTypeHandle(typeIndex, isReadOnly), isReadOnly,
                GlobalSystemVersion);
#else
            return new ComponentTypeHandle<T>(isReadOnly, GlobalSystemVersion);
#endif
        }

        /// <summary>
        /// Gets the dynamic type object required to access a chunk component of dynamic type acquired from reflection.
        /// </summary>
        /// <remarks>
        /// To access a component stored in a chunk, you must have the type registry information for the component.
        /// This function provides that information. Use the returned <see cref="DynamicComponentTypeHandle"/>
        /// object with the functions of an <see cref="ArchetypeChunk"/> object to get information about the components
        /// in that chunk and to access the component values.
        /// </remarks>
        /// <param name="componentType">Type of the component</param>
        /// <returns>The run-time type information of the component.</returns>
        public DynamicComponentTypeHandle GetDynamicComponentTypeHandle(ComponentType componentType)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var access = GetCheckedEntityDataAccess();
            if (!componentType.IsBuffer)
            {
                return new DynamicComponentTypeHandle(componentType,
                    access->DependencyManager->Safety.GetSafetyHandleForDynamicComponentTypeHandle(componentType.TypeIndex, componentType.AccessModeType == ComponentType.AccessMode.ReadOnly),
                    default(AtomicSafetyHandle), GlobalSystemVersion);
            }
            else
            {
                return new DynamicComponentTypeHandle(componentType,
                    access->DependencyManager->Safety.GetSafetyHandleForDynamicComponentTypeHandle(componentType.TypeIndex, componentType.AccessModeType == ComponentType.AccessMode.ReadOnly),
                    access->DependencyManager->Safety.GetBufferHandleForBufferTypeHandle(componentType.TypeIndex),
                    GlobalSystemVersion);
            }

#else
            return new DynamicComponentTypeHandle(componentType, GlobalSystemVersion);
#endif
        }

        /// <summary>
        /// Gets the dynamic type object required to access a chunk buffer containing elements of type T.
        /// </summary>
        /// <remarks>
        /// To access a component stored in a chunk, you must have the type registry information for the component.
        /// This function provides that information for buffer components. Use the returned
        /// <see cref="ComponentTypeHandle{T}"/> object with the functions of an <see cref="ArchetypeChunk"/>
        /// object to get information about the components in that chunk and to access the component values.
        /// </remarks>
        /// <param name="isReadOnly">Specify whether the access to the component through this object is read only
        /// or read and write. </param>
        /// <typeparam name="T">The compile-time type of the buffer elements.</typeparam>
        /// <returns>The run-time type information of the buffer component.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleBufferElement) })]
        public BufferTypeHandle<T> GetBufferTypeHandle<T>(bool isReadOnly)
            where T : unmanaged, IBufferElementData
        {
            return GetCheckedEntityDataAccess()->GetBufferTypeHandle<T>(isReadOnly);
        }

        /// <summary>
        /// Gets the dynamic type object required to access a shared component of type T.
        /// </summary>
        /// <remarks>
        /// To access a component stored in a chunk, you must have the type registry information for the component.
        /// This function provides that information for shared components. Use the returned
        /// <see cref="ComponentTypeHandle{T}"/> object with the functions of an <see cref="ArchetypeChunk"/>
        /// object to get information about the components in that chunk and to access the component values.
        /// </remarks>
        /// <typeparam name="T">The compile-time type of the shared component.</typeparam>
        /// <returns>The run-time type information of the shared component.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleSharedComponentData) })]
        public SharedComponentTypeHandle<T> GetSharedComponentTypeHandle<T>()
            where T : struct, ISharedComponentData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var typeIndex = TypeManager.GetTypeIndex<T>();
            var access = GetCheckedEntityDataAccess();
            return new SharedComponentTypeHandle<T>(access->DependencyManager->Safety.GetSafetyHandleForSharedComponentTypeHandle(typeIndex));
#else
            return new SharedComponentTypeHandle<T>(false);
#endif
        }

        /// <summary>
        /// Gets the dynamic type object required to access a shared component of the given type.
        /// </summary>
        /// <remarks>
        /// To access a component stored in a chunk, you must have the type registry information for the component.
        /// This function provides that information for shared components. Use the returned
        /// <see cref="DynamicSharedComponentTypeHandle"/> object with the functions of an <see cref="ArchetypeChunk"/>
        /// object to get information about the components in that chunk and to access the component values.
        /// </remarks>
        /// <param name="componentType">The component type to get access to.</param>
        /// <returns>The run-time type information of the shared component.</returns>
        public DynamicSharedComponentTypeHandle GetDynamicSharedComponentTypeHandle(ComponentType componentType)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var access = GetCheckedEntityDataAccess();
            return new DynamicSharedComponentTypeHandle(componentType,
                access->DependencyManager->Safety.GetSafetyHandleForDynamicSharedComponentTypeHandle(componentType.TypeIndex,
                    // Only read only mode supported for DynamicSharedComponentTypeHandle
                    true));
#else
            return new DynamicSharedComponentTypeHandle(componentType);
#endif
        }

        /// <summary>
        /// Gets the dynamic type object required to access the <see cref="Entity"/> component of a chunk.
        /// </summary>
        /// <remarks>
        /// All chunks have an implicit <see cref="Entity"/> component referring to the entities in that chunk.
        ///
        /// To access any component stored in a chunk, you must have the type registry information for the component.
        /// This function provides that information for the implicit <see cref="Entity"/> component. Use the returned
        /// <see cref="ComponentTypeHandle{T}"/> object with the functions of an <see cref="ArchetypeChunk"/>
        /// object to access the component values.
        /// </remarks>
        /// <returns>The run-time type information of the Entity component.</returns>
        public EntityTypeHandle GetEntityTypeHandle()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var access = GetCheckedEntityDataAccess();
            return new EntityTypeHandle(
                access->DependencyManager->Safety.GetSafetyHandleForEntityTypeHandle());
#else
            return new EntityTypeHandle(false);
#endif
        }

        /// <summary>
        /// Gets an entity's component types.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="allocator">The type of allocation for creating the NativeArray to hold the ComponentType
        /// objects.</param>
        /// <returns>An array of ComponentType containing all the types of components associated with the entity.</returns>
        public NativeArray<ComponentType> GetComponentTypes(Entity entity, Allocator allocator = Allocator.Temp)
        {
            return GetComponentTypes(entity, (AllocatorManager.AllocatorHandle)allocator);
        }

        /// <summary>
        /// Gets an entity's component types.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="allocator">The type of allocation for creating the NativeArray to hold the ComponentType
        /// objects.</param>
        /// <returns>An array of ComponentType containing all the types of components associated with the entity.</returns>
        public NativeArray<ComponentType> GetComponentTypes(Entity entity, AllocatorManager.AllocatorHandle allocator)
        {
            var access = GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;

            ecs->AssertEntitiesExist(&entity, 1);
            var archetype = new EntityArchetype { Archetype = ecs->GetArchetype(entity) };
            return archetype.GetComponentTypes(allocator);
        }

        /// <summary>
        /// Gets a system entity's component types.
        /// </summary>
        /// <param name="system">The system handle.</param>
        /// <param name="allocator">The type of allocation for creating the NativeArray to hold the ComponentType
        /// objects.</param>
        /// <returns>An array of ComponentType containing all the types of components associated with the entity.</returns>
        public NativeArray<ComponentType> GetComponentTypes(SystemHandle system, Allocator allocator = Allocator.Temp)
        {
            return GetComponentTypes(system, (AllocatorManager.AllocatorHandle)allocator);
        }

        /// <summary>
        /// Gets a system entity's component types.
        /// </summary>
        /// <param name="system">The system handle.</param>
        /// <param name="allocator">The type of allocation for creating the NativeArray to hold the ComponentType
        /// objects.</param>
        /// <returns>An array of ComponentType containing all the types of components associated with the entity.</returns>
        public NativeArray<ComponentType> GetComponentTypes(SystemHandle system, AllocatorManager.AllocatorHandle allocator)
        {
            var access = GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;

            ecs->AssertEntitiesExist(&system.m_Entity, 1);
            var archetype = new EntityArchetype { Archetype = ecs->GetArchetype(system.m_Entity) };
            return archetype.GetComponentTypes(allocator);
        }

        /// <summary>
        /// Gets a list of the types of components that can be assigned to the specified component.
        /// </summary>
        /// <remarks>Assignable components include those with the same compile-time type and those that
        /// inherit from the same compile-time type.</remarks>
        /// <param name="interfaceType">The type to check.</param>
        /// <param name="listOut">The list to receive the output.</param>
        /// <returns>The list that was passed in, containing the System.Types that can be assigned to `interfaceType`.</returns>
        [ExcludeFromBurstCompatTesting("Takes managed Type")]
        public List<Type> GetAssignableComponentTypes(Type interfaceType, List<Type> listOut)
        {
            // #todo Cache this. It only can change when TypeManager.GetTypeCount() changes
            var componentTypeCount = TypeManager.GetTypeCount();
            for (var i = 0; i < componentTypeCount; i++)
            {
                var type = TypeManager.GetType(new TypeIndex { Value = i });
                if (interfaceType.IsAssignableFrom(type)) listOut.Add(type);
            }

            return listOut;
        }

        /// <summary>
        /// Gets a list of the types of components that can be assigned to the specified component.
        /// </summary>
        /// <remarks>Assignable components include those with the same compile-time type and those that
        /// inherit from the same compile-time type.</remarks>
        /// <param name="interfaceType">The type to check.</param>
        /// <returns>A new List object containing the System.Types that can be assigned to `interfaceType`.</returns>
        [ExcludeFromBurstCompatTesting("Takes managed Type")]
        public List<Type> GetAssignableComponentTypes(Type interfaceType)
            => GetAssignableComponentTypes(interfaceType, new List<Type>());

        /// <summary>
        /// Creates a EntityQuery from an array of component types.
        /// </summary>
        /// <param name="requiredComponents">An array containing the component types.</param>
        /// <returns>The EntityQuery derived from the specified array of component types.</returns>
        /// <seealso cref="EntityQueryDesc"/>
        [ExcludeFromBurstCompatTesting("Takes managed array")]
        public EntityQuery CreateEntityQuery(params ComponentType[] requiredComponents)
        {
            var access = GetCheckedEntityDataAccess();
            fixed(ComponentType* requiredComponentsPtr = requiredComponents)
            {
                var query = access->EntityQueryManager->CreateEntityQuery(access, requiredComponentsPtr, requiredComponents.Length);
                return query;
            }
        }

        /// <summary>
        /// Creates a EntityQuery from an EntityQueryDesc.
        /// </summary>
        /// <param name="queriesDesc">A queryDesc identifying a set of component types.</param>
        /// <returns>The EntityQuery corresponding to the queryDesc.</returns>
        [ExcludeFromBurstCompatTesting("Takes managed array")]
        public EntityQuery CreateEntityQuery(params EntityQueryDesc[] queriesDesc)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp);
            EntityQueryManager.ConvertToEntityQueryBuilder(ref builder, queriesDesc);
            var result = CreateEntityQuery(builder);
            builder.Dispose();
            return result;
        }

        /// <summary>
        /// Creates an EntityQuery from an EntityQueryBuilder.
        /// </summary>
        /// <param name="queriesDesc">A queryDesc identifying a set of component types.</param>
        /// <returns>The EntityQuery corresponding to the queryDesc.</returns>
        public EntityQuery CreateEntityQuery(in EntityQueryBuilder queriesDesc)
        {
            var query = CreateEntityQueryUnowned(queriesDesc);
            return query;
        }

        internal EntityQuery CreateEntityQueryUnowned(in EntityQueryBuilder queriesDesc)
        {
            var access = GetCheckedEntityDataAccess();
            return access->EntityQueryManager->CreateEntityQuery(access, queriesDesc);
        }

        /// <summary>
        /// Gets all the chunks managed by this EntityManager.
        /// </summary>
        /// <remarks>
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before getting the chunk. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="allocator">The type of allocation for creating the NativeArray to hold the ArchetypeChunk
        /// objects.</param>
        /// <returns>An array of ArchetypeChunk objects referring to all the chunks in the <see cref="World"/>.</returns>
        public NativeArray<ArchetypeChunk> GetAllChunks(Allocator allocator = Allocator.TempJob)
        {
            return GetAllChunks((AllocatorManager.AllocatorHandle) allocator);
        }

        /// <summary>
        /// Gets all the chunks managed by this EntityManager.
        /// </summary>
        /// <remarks>
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before getting the chunk. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="allocator">The type of allocation for creating the NativeArray to hold the ArchetypeChunk
        /// objects.</param>
        /// <returns>An array of ArchetypeChunk objects referring to all the chunks in the <see cref="World"/>.</returns>
        public NativeArray<ArchetypeChunk> GetAllChunks(AllocatorManager.AllocatorHandle allocator)
        {
            var access = GetCheckedEntityDataAccess();
            var query = access->m_UniversalQuery;
            return query.ToArchetypeChunkArray(allocator);
        }

        /// <summary>
        /// Gets all the chunks managed by this EntityManager, including the meta chunks (containing chunk components).
        /// </summary>
        /// <remarks>
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before getting the chunk. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="allocator">The type of allocation for creating the NativeArray to hold the ArchetypeChunk
        /// objects.</param>
        /// <returns>An array of ArchetypeChunk objects referring to all the chunks in the <see cref="World"/>.</returns>
        public NativeArray<ArchetypeChunk> GetAllChunksAndMetaChunks(Allocator allocator = Allocator.TempJob)
        {
            return GetAllChunksAndMetaChunks((AllocatorManager.AllocatorHandle) allocator);
        }

        /// <summary>
        /// Gets all the chunks managed by this EntityManager, including the meta chunks (containing chunk components).
        /// </summary>
        /// <remarks>
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before getting the chunk. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="allocator">The type of allocation for creating the NativeArray to hold the ArchetypeChunk
        /// objects.</param>
        /// <returns>An array of ArchetypeChunk objects referring to all the chunks in the <see cref="World"/>.</returns>
        public NativeArray<ArchetypeChunk> GetAllChunksAndMetaChunks(AllocatorManager.AllocatorHandle allocator)
        {
            var access = GetCheckedEntityDataAccess();
            var query = access->m_UniversalQueryWithChunks;
            return query.ToArchetypeChunkArray(allocator);
        }

        NativeArray<ArchetypeChunk> GetAllChunksAndMetaChunksWithSystems(AllocatorManager.AllocatorHandle allocator)
        {
            var access = GetCheckedEntityDataAccess();
            var query = access->m_UniversalQueryWithChunksAndSystems;
            return query.ToArchetypeChunkArray(allocator);
        }

        NativeArray<ArchetypeChunk> GetAllChunksWithSystems(AllocatorManager.AllocatorHandle allocator)
        {
            var access = GetCheckedEntityDataAccess();
            var query = access->m_UniversalQueryWithSystems;
            return query.ToArchetypeChunkArray(allocator);
        }

        /// <summary>
        /// Gets all the archetypes.
        /// </summary>
        /// <remarks>The function adds the archetype objects to the existing contents of the list.
        /// The list is not cleared.</remarks>
        /// <param name="allArchetypes">A native list to receive the EntityArchetype objects.</param>
        public void GetAllArchetypes(NativeList<EntityArchetype> allArchetypes)
        {
            var access = GetCheckedEntityDataAccess();
            for (var i = 0; i < access->EntityComponentStore->m_Archetypes.Length; ++i)
            {
                var archetype = access->EntityComponentStore->m_Archetypes.Ptr[i];
                var entityArchetype = new EntityArchetype()
                {
                    Archetype = archetype,
                };
                allArchetypes.Add(entityArchetype);
            }
        }

        /// <summary>
        /// This method is obsolete. Use <see cref="EntityQuery.GetEntityQueryMask"/> instead.
        /// </summary>
        /// <remarks>**Obsolete.** Use <see cref="EntityQuery.GetEntityQueryMask"/> instead.</remarks>
        /// <param name="query">The query whose mask should be returned</param>
        /// <returns>A query mask for the provided query</returns>
        [Obsolete("Use EntityQuery.GetEntityQueryMask()")]
        public EntityQueryMask GetEntityQueryMask(EntityQuery query)
        {
            return query.GetEntityQueryMask();
        }

        // @TODO Point to documentation for multithreaded way to check Entity validity.
        /// <summary>
        /// Reports whether an Entity object is still valid.
        /// </summary>
        /// <remarks>
        /// An Entity object does not contain a reference to its entity. Instead, the Entity struct contains an index
        /// and a generational version number. When an entity is destroyed, the EntityManager increments the version
        /// of the entity within the internal array of entities. The index of a destroyed entity is recycled when a
        /// new entity is created.
        ///
        /// After an entity is destroyed, any existing Entity objects will still contain the
        /// older version number. This function compares the version numbers of the specified Entity object and the
        /// current version of the entity recorded in the entities array. If the versions are different, the Entity
        /// object no longer refers to an existing entity and cannot be used.
        /// </remarks>
        /// <param name="entity">The Entity object to check.</param>
        /// <returns>True, if <see cref="Entity.Version"/> matches the version of the current entity at
        /// <see cref="Entity.Index"/> in the entities array.</returns>
        public bool Exists(Entity entity)
        {
            return GetCheckedEntityDataAccess()->Exists(entity);
        }

        /// <summary>
        /// Checks whether an entity has a specific type of component.
        /// </summary>
        /// <remarks>Always returns false for an entity that has been destroyed.</remarks>
        /// <param name="entity">The Entity object.</param>
        /// <typeparam name="T">The data type of the component.</typeparam>
        /// <returns>True, if the specified entity has the component.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public bool HasComponent<T>(Entity entity)
        {
            return GetCheckedEntityDataAccess()->HasComponent(entity, ComponentType.ReadWrite<T>());
        }

        /// <summary>
        /// Checks whether an entity associated with a system has a specific type of component.
        /// </summary>
        /// <remarks>Always returns false for an entity that has been destroyed.</remarks>
        /// <param name="system">The system handle.</param>
        /// <typeparam name="T">The data type of the component.</typeparam>
        /// <returns>True, if the specified system entity has the component.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public bool HasComponent<T>(SystemHandle system)
        {
            return GetCheckedEntityDataAccess()->HasComponent(system.m_Entity, ComponentType.ReadWrite<T>());
        }

        /// <summary>
        /// Checks whether an entity has a dynamic buffer of a specific IBufferElementData type.
        /// </summary>
        /// <remarks>Always returns false for an entity that has been destroyed.</remarks>
        /// <param name="entity">The Entity object.</param>
        /// <typeparam name="T">The IBufferElementData type.</typeparam>
        /// <returns>True, if the specified entity has a dynamic buffer of IBufferElementData type.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleBufferElement) })]
        public bool HasBuffer<T>(Entity entity) where T : struct, IBufferElementData
        {
            return GetCheckedEntityDataAccess()->HasComponent(entity, ComponentType.ReadWrite<T>());
        }

        /// <summary>
        /// Checks whether an entity has a specific type of component.
        /// </summary>
        /// <remarks>Always returns false for an entity that has been destroyed.</remarks>
        /// <param name="entity">The Entity object.</param>
        /// <param name="type">The data type of the component.</param>
        /// <returns>True, if the specified entity has the component.</returns>
        public bool HasComponent(Entity entity, ComponentType type)
        {
            return GetCheckedEntityDataAccess()->HasComponent(entity, type);
        }

        /// <summary>
        /// Checks whether an entity associated with a system has a specific type of component.
        /// </summary>
        /// <remarks>Always returns false for an entity that has been destroyed.</remarks>
        /// <param name="system">The system handle.</param>
        /// <param name="type">The data type of the component.</param>
        /// <returns>True, if the specified system entity has the component.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public bool HasComponent(SystemHandle system, ComponentType type)
        {
            return GetCheckedEntityDataAccess()->HasComponent(system.m_Entity, type);
        }

        /// <summary>
        /// Checks whether the chunk containing an entity has a specific type of component.
        /// </summary>
        /// <remarks>Always returns false for an entity that has been destroyed.</remarks>
        /// <param name="entity">The Entity object.</param>
        /// <typeparam name="T">The data type of the chunk component.</typeparam>
        /// <returns>True, if the chunk containing the specified entity has the component.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public bool HasChunkComponent<T>(Entity entity)
        {
            return GetCheckedEntityDataAccess()->HasComponent(entity, ComponentType.ChunkComponent<T>());
        }

        /// <summary>
        /// Look up an aspect for an entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <typeparam name="T">The type of aspect to retrieve.</typeparam>
        /// <returns>An aspect struct of type T representing the aspect on the entity.</returns>
        /// <remarks>
        /// T must implement the <see cref="IAspect"/> interface.
        /// The given entity is assumed to have all the components required by the aspect type.
        /// </remarks>
        /// <remarks>
        /// This method will create an instance of the aspect struct using
        /// an internal SystemState.
        /// </remarks>
        /// <remarks>
        /// When calling from an ISystem, use SystemAPI.GetAspect instead.
        /// When calling from a SystemBase, use SystemBase.GetAspect instead.
        /// Use this method when calling from outside the dots runtime, e.g. from the editor code.
        /// </remarks>
        [ExcludeFromBurstCompatTesting("This unfortunately needs access to the managed world for the ExternalAPIState.")]
        public T GetAspect<T>(Entity entity) where T : struct, IAspect, IAspectCreate<T>
        {
            T aspect = default;
            return aspect.CreateAspect(entity, ref *World.ExternalAPIState);
        }

        /// <summary>
        /// Completes the dependency chain required for this component to have read and write access.
        /// So it completes all write dependencies of the component to allow for reading,
        /// and it completes all read dependencies, so we can write to it.
        /// </summary>
        /// <typeparam name="T">The type of component you want to complete dependencies on</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public void CompleteDependencyBeforeRW<T>()
            =>GetUncheckedEntityDataAccess()->DependencyManager->CompleteReadAndWriteDependency(TypeManager.GetTypeIndex<T>());

        /// <summary>
        /// Completes the dependency chain required for this component to have read access.
        /// So it completes all write dependencies of the component to allow for reading.
        /// </summary>
        /// <typeparam name="T">The type of component you want to complete dependencies on</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public void CompleteDependencyBeforeRO<T>()
            => GetUncheckedEntityDataAccess()->DependencyManager->CompleteWriteDependency(TypeManager.GetTypeIndex<T>());

        // ----------------------------------------------------------------------------------------------------------
        // INTERNAL
        // ----------------------------------------------------------------------------------------------------------

        #region Internal APIs

        [ExcludeFromBurstCompatTesting("Takes managed object")]
        internal void SetSharedComponentDataBoxedDefaultMustBeNull(Entity entity, TypeIndex typeIndex, object componentData)
        {
            var hashCode = 0;
            if (componentData != null)
                hashCode = TypeManager.GetHashCode(componentData, typeIndex);

            SetSharedComponentDataBoxedDefaultMustBeNull(entity, typeIndex, hashCode, componentData);
        }

        [ExcludeFromBurstCompatTesting("Takes managed object")]
        void SetSharedComponentDataBoxedDefaultMustBeNull(Entity entity, TypeIndex typeIndex, int hashCode, object componentData)
        {
            var access = GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            access->SetSharedComponentDataBoxedDefaultMustBeNullDuringStructuralChange(entity, typeIndex, hashCode, componentData);
            access->EndStructuralChanges(ref changes);
        }

        [ExcludeFromBurstCompatTesting("Takes managed object")]
        internal void SetComponentObject(Entity entity, ComponentType componentType, object componentObject)
        {
            var access = GetCheckedEntityDataAccess();
            access->SetComponentObject(entity, componentType, componentObject);
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        internal ComponentLookup<T> GetComponentLookup<T>(bool isReadOnly = false)
            where T : unmanaged, IComponentData
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            return GetComponentLookup<T>(typeIndex, isReadOnly);
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        internal ComponentLookup<T> GetComponentLookup<T>(TypeIndex typeIndex, bool isReadOnly)
            where T : unmanaged, IComponentData
        {
            var access = GetCheckedEntityDataAccess();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandles = &access->DependencyManager->Safety;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new ComponentLookup<T>(typeIndex, access, isReadOnly);
#else
            return new ComponentLookup<T>(typeIndex, access);
#endif
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleBufferElement) })]
        internal BufferLookup<T> GetBufferLookup<T>(bool isReadOnly = false)
            where T : unmanaged, IBufferElementData
        {
            return GetBufferLookup<T>(TypeManager.GetTypeIndex<T>(), isReadOnly);
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleBufferElement) })]
        internal BufferLookup<T> GetBufferLookup<T>(TypeIndex typeIndex, bool isReadOnly = false)
            where T : unmanaged, IBufferElementData
        {
            var access = GetCheckedEntityDataAccess();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandles = &access->DependencyManager->Safety;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new BufferLookup<T>(typeIndex, access, isReadOnly,
                safetyHandles->GetSafetyHandleForComponentLookup(typeIndex, isReadOnly),
                safetyHandles->GetBufferHandleForBufferLookup(typeIndex));
#else
            return new BufferLookup<T>(typeIndex, access, isReadOnly);
#endif
        }

        internal EntityStorageInfoLookup GetEntityStorageInfoLookup()
        {
            var access = GetCheckedEntityDataAccess();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandles = &access->DependencyManager->Safety;
            return new EntityStorageInfoLookup(access, safetyHandles->GetSafetyHandleForEntityTypeHandle());
#else
            return new EntityStorageInfoLookup(access);
#endif
        }

        internal void SetComponentDataRaw(Entity entity, TypeIndex typeIndex, void* data, int size)
        {
            var access = GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;
            var deps = access->DependencyManager;

            deps->CompleteReadAndWriteDependency(typeIndex);
            access->SetComponentDataRaw(entity, typeIndex, data, size);
        }

        internal void* GetComponentDataRawRW(Entity entity, TypeIndex typeIndex)
        {
            var access = GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;
            var deps = access->DependencyManager;
            ecs->AssertEntityHasComponent(entity, typeIndex);
            deps->CompleteReadAndWriteDependency(typeIndex);

            return access->GetComponentDataRawRWEntityHasComponent(entity, typeIndex);
        }

        internal void* GetComponentDataRawRO(Entity entity, TypeIndex typeIndex)
        {
            var access = GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;
            var deps = access->DependencyManager;
            ecs->AssertEntityHasComponent(entity, typeIndex);
            deps->CompleteWriteDependency(typeIndex);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (TypeManager.GetTypeInfo(typeIndex).IsZeroSized)
                throw new System.ArgumentException(
                    $"GetComponentDataRawRO can not be called with a zero sized component.");
#endif


            var ptr = ecs->GetComponentDataWithTypeRO(entity, typeIndex);
            return ptr;
        }

        internal void SetEnableableComponent(Entity entity, TypeIndex typeIndex, bool value)
        {
            var access = GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;
            var deps = access->DependencyManager;
            ecs->AssertEntityHasComponent(entity, typeIndex);
            deps->CompleteReadAndWriteDependency(typeIndex);

            access->SetComponentEnabled(entity, typeIndex, value);
        }

        [ExcludeFromBurstCompatTesting("Returns managed object")]
        internal object GetSharedComponentData(Entity entity, TypeIndex typeIndex)
        {
            var access = GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;
            var mcs = access->ManagedComponentStore;

            ecs->AssertEntityHasComponent(entity, typeIndex);

            var sharedComponentIndex = ecs->GetSharedComponentDataIndex(entity, typeIndex);
            return GetSharedComponentDataBoxed(sharedComponentIndex, typeIndex);
        }

        internal void* GetBufferRawRW(Entity entity, TypeIndex typeIndex)
        {
            var access = GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;

            ecs->AssertEntityHasComponent(entity, typeIndex);

            access->DependencyManager->CompleteReadAndWriteDependency(typeIndex);

            BufferHeader* header = (BufferHeader*)ecs->GetComponentDataWithTypeRW(entity, typeIndex, ecs->GlobalSystemVersion);

            return BufferHeader.GetElementPointer(header);
        }

        internal void* GetBufferRawRO(Entity entity, TypeIndex typeIndex)
        {
            var access = GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;

            ecs->AssertEntityHasComponent(entity, typeIndex);

            access->DependencyManager->CompleteWriteDependency(typeIndex);

            BufferHeader* header = (BufferHeader*)ecs->GetComponentDataWithTypeRO(entity, typeIndex);

            return BufferHeader.GetElementPointer(header);
        }

        internal int GetBufferLength(Entity entity, TypeIndex typeIndex)
        {
            var access = GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;

            ecs->AssertEntityHasComponent(entity, typeIndex);

            access->DependencyManager->CompleteWriteDependency(typeIndex);

            BufferHeader* header = (BufferHeader*)ecs->GetComponentDataWithTypeRO(entity, typeIndex);

            return header->Length;
        }

        // these are used by tiny, do not remove
        [UsedImplicitly]
        internal void AddComponentRaw(Entity entity, TypeIndex typeIndex) => AddComponent(entity, ComponentType.FromTypeIndex(typeIndex));
        [UsedImplicitly]
        internal void RemoveComponentRaw(Entity entity, TypeIndex typeIndex) => RemoveComponent(entity, ComponentType.FromTypeIndex(typeIndex));

        internal void DestroyEntityInternal(Entity* entities, int count)
        {
            var access = GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            access->DestroyEntityInternalDuringStructuralChange(entities, count);
            access->EndStructuralChanges(ref changes);
        }

        void MoveEntitiesFromInternalQuery(EntityManager srcEntities, EntityQuery filter, NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping)
        {
            var srcAccess = srcEntities.GetCheckedEntityDataAccess();

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            // Must come first - if this doesn't pass, AssertQueryIsValid won't pass either
            if (filter._GetImpl()->_Access != srcAccess)
                throw new ArgumentException(
                    "EntityManager.MoveEntitiesFrom failed - srcEntities and filter must belong to the same World)");
#endif

            srcAccess->AssertQueryIsValid(filter);
            // queries with enableable components are not supported in this code path.
            srcAccess->AssertQueryHasNoEnableableComponents(filter);

            var selfAccess = GetCheckedEntityDataAccess();

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (srcEntities.m_EntityDataAccess == m_EntityDataAccess)
                throw new ArgumentException("srcEntities must not be the same as this EntityManager.");
#endif
            BeforeStructuralChange();
            srcEntities.BeforeStructuralChange();

            using (var chunks = filter.ToArchetypeChunkArray(Allocator.TempJob))
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                for (int i = 0; i < chunks.Length; ++i)
                {
                    var archetype = srcAccess->EntityComponentStore->GetArchetype(chunks[i].m_Chunk);
                    if (archetype->HasChunkHeader)
                        throw new ArgumentException("MoveEntitiesFrom can not move chunks that contain ChunkHeader components.");
                }
#endif

                var archetypeChanges = selfAccess->EntityComponentStore->BeginArchetypeChangeTracking();

                MoveChunksFromFiltered(chunks, entityRemapping, srcAccess->EntityComponentStore, srcAccess->ManagedComponentStore);

                selfAccess->EntityComponentStore->EndArchetypeChangeTracking(archetypeChanges, selfAccess->EntityQueryManager);
                selfAccess->EntityComponentStore->InvalidateChunkListCacheForChangedArchetypes();
                srcAccess->EntityComponentStore->InvalidateChunkListCacheForChangedArchetypes();
            }
        }

        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        internal void MoveEntitiesFromInternalAll(EntityManager srcEntities, NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping)
        {
            var srcAccess = srcEntities.GetCheckedEntityDataAccess();
            var selfAccess = GetCheckedEntityDataAccess();

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (srcEntities.m_EntityDataAccess == m_EntityDataAccess)
                throw new ArgumentException("srcEntities must not be the same as this EntityManager.");

            if (entityRemapping.Length <= srcEntities.Debug.HighestIndexEntity)
                throw new ArgumentException("entityRemapping.Length isn't large enough, use srcEntities.CreateEntityRemapArray");

            if (!srcAccess->AllSharedComponentReferencesAreFromChunks(srcAccess->EntityComponentStore))
                throw new ArgumentException(
                    "EntityManager.MoveEntitiesFrom failed - All ISharedComponentData references must be from EntityManager. (For example EntityQuery.SetFilter with a shared component type is not allowed during EntityManager.MoveEntitiesFrom)");
#endif

            srcEntities.BeforeStructuralChange();
            var archetypeChanges = selfAccess->BeginStructuralChanges();

            MoveChunksFromAll(entityRemapping, srcAccess->EntityComponentStore, srcAccess->ManagedComponentStore);

            selfAccess->EndStructuralChanges(ref archetypeChanges);
            srcAccess->EntityComponentStore->InvalidateChunkListCacheForChangedArchetypes();
        }

        [BurstCompile]
        static void RemapChunksForFilteredMove(ref NativeArray<ArchetypeChunk> chunks,
            ref NativeArray<RemapChunk> remapChunks,
            ref NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping,
            ref NativeList<ChunkIndex> managedChunks, out int managedComponentCount,
            EntityComponentStore* dstEntityComponentStore, EntityComponentStore* srcEntityComponentStore)
        {
            int chunkCount = chunks.Length;

            Archetype* previousSrcArchetype = null;
            Archetype* dstArchetype = null;
            managedComponentCount = 0;

            using var toDestroy = new NativeList<Entity>(Allocator.TempJob);
            for (int i = 0; i < chunkCount; ++i)
            {
                var chunk = chunks[i].m_Chunk;
                var archetype = srcEntityComponentStore->GetArchetype(chunk);

                // Move Chunk World. ChangeVersion:Yes OrderVersion:Yes
                if (previousSrcArchetype != archetype)
                {
                    dstArchetype = dstEntityComponentStore->GetOrCreateArchetype(archetype->Types, archetype->TypesCount);
                    previousSrcArchetype = archetype;
                }

                remapChunks[i] = new RemapChunk {chunk = chunk, dstArchetype = dstArchetype};

                if (dstArchetype->NumManagedComponents > 0)
                {
                    managedComponentCount += chunk.Count * dstArchetype->NumManagedComponents;
                    managedChunks.Add(chunk);
                }

                if (archetype->MetaChunkArchetype != null)
                {
                    Entity srcEntity = chunk.MetaChunkEntity;
                    Entity dstEntity;

                    dstEntityComponentStore->CreateEntities(dstArchetype->MetaChunkArchetype, &dstEntity, 1);

                    var srcEntityInChunk = srcEntityComponentStore->GetEntityInChunk(srcEntity);
                    var dstEntityInChunk = dstEntityComponentStore->GetEntityInChunk(dstEntity);

                    var srcMetaArchetype = srcEntityComponentStore->GetArchetype(srcEntity);
                    var dstMetaArchetype = dstEntityComponentStore->GetArchetype(dstEntity);

                    ChunkDataUtility.CopyComponents(srcEntityInChunk.Chunk, srcMetaArchetype, srcEntityInChunk.IndexInChunk,
                        dstEntityInChunk.Chunk, dstMetaArchetype, dstEntityInChunk.IndexInChunk, 1, dstEntityComponentStore->GlobalSystemVersion);
                    EntityRemapUtility.AddEntityRemapping(ref entityRemapping, srcEntity, dstEntity);

                    toDestroy.Add(srcEntity);
                }
            }
            if (toDestroy.Length > 0)
                srcEntityComponentStore->DestroyEntities((Entity*)toDestroy.GetUnsafeReadOnlyPtr(), toDestroy.Length);
        }

        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        internal void MoveChunksFromFiltered(
            NativeArray<ArchetypeChunk> chunks,
            NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping,
            EntityComponentStore* srcEntityComponentStore,
            ManagedComponentStore srcManagedComponentStore)
        {
            var access = GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;
            var mcs = access->ManagedComponentStore;

            new MoveChunksJob
            {
                srcEntityComponentStore = srcEntityComponentStore,
                dstEntityComponentStore = ecs,
                entityRemapping = entityRemapping,
                chunks = chunks
            }.Run();

            int managedComponentCount = 0;
            var remapChunks = new NativeArray<RemapChunk>(chunks.Length, Allocator.TempJob);
            var managedChunks = new NativeList<ChunkIndex>(0, Allocator.TempJob);
            RemapChunksForFilteredMove(ref chunks, ref remapChunks, ref entityRemapping, ref managedChunks, out managedComponentCount, ecs, srcEntityComponentStore);

            NativeArray<int> srcManagedIndices = default;
            NativeArray<int> dstManagedIndices = default;
            int nonNullManagedComponentCount = 0;
            if (managedComponentCount > 0)
            {
                srcManagedIndices = new NativeArray<int>(managedComponentCount, Allocator.TempJob);
                dstManagedIndices = new NativeArray<int>(managedComponentCount, Allocator.TempJob);
                new GatherManagedComponentIndicesInChunkJob
                {
                    SrcEntityComponentStore = srcEntityComponentStore,
                    DstEntityComponentStore = ecs,
                    SrcManagedIndices = srcManagedIndices,
                    DstManagedIndices = dstManagedIndices,
                    Chunks = managedChunks.AsArray(),
                    NonNullCount = &nonNullManagedComponentCount
                }.Run();
            }

            mcs.Playback(ref ecs->ManagedChangesTracker);
            srcManagedComponentStore.Playback(ref srcEntityComponentStore->ManagedChangesTracker);

            k_ProfileMoveSharedComponents.Begin();
            var remapShared = access->MoveSharedComponents(srcEntityComponentStore, srcManagedComponentStore, chunks, Allocator.TempJob);
            k_ProfileMoveSharedComponents.End();

            if (managedComponentCount > 0)
            {
                k_ProfileMoveManagedComponents.Begin();
                mcs.MoveManagedComponentsFromDifferentWorld(srcManagedIndices, dstManagedIndices, nonNullManagedComponentCount, srcManagedComponentStore);
                srcEntityComponentStore->m_ManagedComponentFreeIndex.Add(srcManagedIndices.GetUnsafeReadOnlyPtr(), sizeof(int) * srcManagedIndices.Length);
                k_ProfileMoveManagedComponents.End();
            }

            new ChunkPatchEntities
            {
                RemapChunks = remapChunks,
                EntityRemapping = entityRemapping,
                EntityComponentStore = ecs
            }.Run();

            var remapChunksJob = new RemapChunksFilteredJob
            {
                dstEntityComponentStore = ecs,
                remapChunks = remapChunks,
                entityRemapping = entityRemapping,
                chunkHeaderType = TypeManager.GetTypeIndex<ChunkHeader>()
            }.Schedule(remapChunks.Length, 1);

            var moveChunksBetweenArchetypeJob = new MoveFilteredChunksBetweenArchetypeJob
            {
                RemapChunks = remapChunks,
                RemapShared = remapShared,
                SrcEntityComponentStore = srcEntityComponentStore,
            }.Schedule(remapChunksJob);

            moveChunksBetweenArchetypeJob.Complete();

            mcs.Playback(ref ecs->ManagedChangesTracker);

            if (managedComponentCount > 0)
            {
                srcManagedIndices.Dispose();
                dstManagedIndices.Dispose();
            }
            managedChunks.Dispose();
            remapShared.Dispose();
            remapChunks.Dispose();
        }

        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        internal void MoveChunksFromAll(
            NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping,
            EntityComponentStore* srcEntityComponentStore,
            ManagedComponentStore srcManagedComponentStore)
        {
            var access = GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;
            var mcs = access->ManagedComponentStore;

            var moveChunksJob = new MoveAllChunksJob
            {
                srcEntityComponentStore = srcEntityComponentStore,
                dstEntityComponentStore = ecs,
                entityRemapping = entityRemapping
            }.Schedule();
            var freeChunksJob = new FreeAllChunksJob
            {
                EntityComponentStore = srcEntityComponentStore
            }.Schedule(moveChunksJob);

            int managedComponentCount = srcEntityComponentStore->ManagedComponentIndexUsedCount;
            NativeArray<int> srcManagedIndices = default;
            NativeArray<int> dstManagedIndices = default;
            JobHandle gatherManagedComponentIndices = default;
            if (managedComponentCount > 0)
            {
                srcManagedIndices = new NativeArray<int>(managedComponentCount, Allocator.TempJob);
                dstManagedIndices = new NativeArray<int>(managedComponentCount, Allocator.TempJob);
                ecs->ReserveManagedComponentIndices(managedComponentCount);
                gatherManagedComponentIndices = new
                    GatherAllManagedComponentIndicesJob
                {
                    SrcEntityComponentStore = srcEntityComponentStore,
                    DstEntityComponentStore = ecs,
                    SrcManagedIndices = srcManagedIndices,
                    DstManagedIndices = dstManagedIndices
                }.Schedule();
            }

            JobHandle.ScheduleBatchedJobs();


            int chunkCount = 0;
            for (var i = 0; i < srcEntityComponentStore->m_Archetypes.Length; ++i)
            {
                var srcArchetype = srcEntityComponentStore->m_Archetypes.Ptr[i];
                chunkCount += srcArchetype->Chunks.Count;
            }

            var remapChunks = new NativeArray<RemapChunk>(chunkCount, Allocator.TempJob);
            var remapArchetypes = new NativeArray<RemapArchetype>(srcEntityComponentStore->m_Archetypes.Length, Allocator.TempJob);

            int chunkIndex = 0;
            int archetypeIndex = 0;
            for (var i = 0; i < srcEntityComponentStore->m_Archetypes.Length; ++i)
            {
                var srcArchetype = srcEntityComponentStore->m_Archetypes.Ptr[i];
                if (srcArchetype->Chunks.Count != 0)
                {
                    var dstArchetype = ecs->GetOrCreateArchetype(srcArchetype->Types,
                        srcArchetype->TypesCount);

                    remapArchetypes[archetypeIndex] = new RemapArchetype
                    { srcArchetype = srcArchetype, dstArchetype = dstArchetype };

                    srcEntityComponentStore->m_ChunkListChangesTracker.TrackArchetype(srcArchetype);
                    ecs->m_ChunkListChangesTracker.TrackArchetype(dstArchetype);

                    for (var j = 0; j < srcArchetype->Chunks.Count; ++j)
                    {
                        var srcChunk = srcArchetype->Chunks[j];
                        remapChunks[chunkIndex] = new RemapChunk { chunk = srcChunk, dstArchetype = dstArchetype };
                        chunkIndex++;
                    }

                    archetypeIndex++;
                    ecs->IncrementComponentTypeOrderVersion(dstArchetype);
                }
            }

            moveChunksJob.Complete();

            mcs.Playback(ref ecs->ManagedChangesTracker);
            srcManagedComponentStore.Playback(ref srcEntityComponentStore->ManagedChangesTracker);

            k_ProfileMoveSharedComponents.Begin();
            var remapShared = access->MoveAllSharedComponents(srcEntityComponentStore, srcManagedComponentStore, Allocator.TempJob);
            k_ProfileMoveSharedComponents.End();

            gatherManagedComponentIndices.Complete();

            k_ProfileMoveManagedComponents.Begin();
            mcs.MoveManagedComponentsFromDifferentWorld(srcManagedIndices, dstManagedIndices, srcManagedIndices.Length, srcManagedComponentStore);
            srcEntityComponentStore->m_ManagedComponentFreeIndex.Length = 0;
            srcEntityComponentStore->m_ManagedComponentIndex = 1;
            k_ProfileMoveManagedComponents.End();

            new ChunkPatchEntities
            {
                RemapChunks = remapChunks,
                EntityRemapping = entityRemapping,
                EntityComponentStore = ecs
            }.Run();

            var remapAllChunksJob = new RemapAllChunksJob
            {
                dstEntityComponentStore = ecs,
                remapChunks = remapChunks,
                entityRemapping = entityRemapping
            }.Schedule(remapChunks.Length, 1
#if !ENTITY_STORE_V1
                // Freeing entities with entity store V2 requires reading from the actual chunks.
                , freeChunksJob
#endif
            );

            var remapArchetypesJob = new RemapAllArchetypesJob
            {
                remapArchetypes = remapArchetypes,
                remapShared = remapShared,
                dstEntityComponentStore = ecs,
                chunkHeaderType = TypeManager.GetTypeIndex<ChunkHeader>()
            }.Schedule(archetypeIndex, 1, remapAllChunksJob);

            mcs.Playback(ref ecs->ManagedChangesTracker);

            if (managedComponentCount > 0)
            {
                srcManagedIndices.Dispose();
                dstManagedIndices.Dispose();
            }

            remapArchetypesJob.Complete();
            remapShared.Dispose();
            remapChunks.Dispose();
            freeChunksJob.Complete();
        }

        [BurstCompile]
        internal struct RemapEntitiesManagedForDiffer : IJob
        {
            public NativeArray<ArchetypeChunk> DstChunks;
            public NativeArray<EntityRemapUtility.EntityRemapInfo> EntityRemapping;
            [NativeDisableUnsafePtrRestriction]
            public EntityComponentStore* EntityComponentStore;

            public void Execute()
            {
                for (int ci = 0, cc = DstChunks.Length; ci < cc; ci++)
                {
                    var remapChunk = DstChunks[ci];
                    var chunk = remapChunk.m_Chunk;
                    Archetype* dstArchetype = remapChunk.Archetype.Archetype;
                    EntityComponentStore->ManagedChangesTracker.PatchEntities(dstArchetype, chunk, chunk.Count, EntityRemapping);
                }
            }
        }

        [BurstCompile]
        struct RemapEntitiesUnmanagedForDiffer : IJobParallelFor
        {
            [Collections.ReadOnly] public NativeArray<EntityRemapUtility.EntityRemapInfo> EntityRemapping;
            [Collections.ReadOnly] public NativeArray<ArchetypeChunk> DstChunks;

            [NativeDisableUnsafePtrRestriction] public EntityComponentStore* dstEntityComponentStore;

            public void Execute(int index)
            {
                var remapChunk = DstChunks[index];
                var chunk = remapChunk.m_Chunk;
                Archetype* dstArchetype = remapChunk.Archetype.Archetype;

                var entityCount = chunk.Count;
                EntityRemapUtility.PatchEntities(dstArchetype->ScalarEntityPatches + 1,
                    dstArchetype->ScalarEntityPatchCount - 1, dstArchetype->BufferEntityPatches,
                    dstArchetype->BufferEntityPatchCount, chunk.Buffer, entityCount, ref EntityRemapping);
            }
        }

#if !ENTITY_STORE_V1
        // Both DuplicateEntitiesForDiffer and RemapEntitiesForDiffer are there to help with entity
        // duplication between the main world and the shadow world used by the differ. Since those
        // cannot use the same entities anymore with entity store V2, an explicit remapping is required.
        internal void DuplicateEntitiesForDiffer(
            NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping,
            NativeArray<ArchetypeChunk> srcChunks,
            NativeArray<ArchetypeChunk> dstChunks)
        {
            for (int ci = 0, cc = srcChunks.Length; ci < cc; ci++)
            {
                var srcChunk = srcChunks[ci].m_Chunk;
                var dstChunk = dstChunks[ci].m_Chunk;

                var srcEntities = (Entity*)srcChunk.Buffer;
                var dstEntities = (Entity*)dstChunk.Buffer;
                var ec = dstChunk.Count;

                EntityComponentStore.s_entityStore.Data.AllocateEntities(dstEntities, ec, ChunkIndex.Null, 0);

                for (int ei = 0; ei < ec; ei++)
                {
                    EntityRemapUtility.AddEntityRemapping(ref entityRemapping, srcEntities[ei], dstEntities[ei]);
                }
            }
        }

        // Both DuplicateEntitiesForDiffer and RemapEntitiesForDiffer are there to help with entity
        // duplication between the main world and the shadow world used by the differ. Since those
        // cannot use the same entities anymore with entity store V2, an explicit remapping is required.
        internal void RemapEntitiesForDiffer(
            NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping,
            NativeArray<ArchetypeChunk> srcChunks,
            NativeArray<ArchetypeChunk> dstChunks)
        {
            var dstEntityComponentStore = GetCheckedEntityDataAccess()->EntityComponentStore;

            new RemapEntitiesManagedForDiffer
            {
                DstChunks = dstChunks,
                EntityRemapping = entityRemapping,
                EntityComponentStore = dstEntityComponentStore
            }.Run();

            new RemapEntitiesUnmanagedForDiffer
            {
                dstEntityComponentStore = dstEntityComponentStore,
                DstChunks = dstChunks,
                EntityRemapping = entityRemapping
            }.Run(dstChunks.Length);
        }
#endif

        #region Nested type definitions

        internal struct RemapChunk
        {
            public ChunkIndex chunk;
            public Archetype* dstArchetype;
        }

        internal struct RemapArchetype
        {
            public Archetype* srcArchetype;
            public Archetype* dstArchetype;
        }

        [BurstCompile]
        internal struct ChunkPatchEntities : IJob
        {
            public NativeArray<RemapChunk> RemapChunks;
            public NativeArray<EntityRemapUtility.EntityRemapInfo> EntityRemapping;
            [NativeDisableUnsafePtrRestriction]
            public EntityComponentStore* EntityComponentStore;

            public void Execute()
            {
                for (int i = 0; i < RemapChunks.Length; i++)
                {
                    var remapChunk = RemapChunks[i];
                    var chunk = remapChunk.chunk;
                    Archetype* dstArchetype = remapChunk.dstArchetype;
                    EntityComponentStore->ManagedChangesTracker.PatchEntities(dstArchetype, chunk, chunk.Count, EntityRemapping);
                }
            }
        }

        [BurstCompile]
        internal struct MoveChunksJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public EntityComponentStore* srcEntityComponentStore;
            [NativeDisableUnsafePtrRestriction] public EntityComponentStore* dstEntityComponentStore;
            public NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping;
            [Collections.ReadOnly] public NativeArray<ArchetypeChunk> chunks;

            public void Execute()
            {
                int chunkCount = chunks.Length;
                for (int i = 0; i < chunkCount; ++i)
                {
                    var chunk = chunks[i].m_Chunk;
                    dstEntityComponentStore->AllocateEntitiesForRemapping(chunk, srcEntityComponentStore, ref entityRemapping);
                    srcEntityComponentStore->FreeEntities(chunk);
                }
            }
        }

        [BurstCompile]
        internal struct GatherAllManagedComponentIndicesJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public EntityComponentStore* SrcEntityComponentStore;
            [NativeDisableUnsafePtrRestriction] public EntityComponentStore* DstEntityComponentStore;

            public NativeArray<int> SrcManagedIndices;
            public NativeArray<int> DstManagedIndices;
            public void Execute()
            {
                DstEntityComponentStore->AllocateManagedComponentIndices((int*)DstManagedIndices.GetUnsafePtr(), DstManagedIndices.Length);
                int srcCounter = 0;
                for (var iChunk = 0; iChunk < SrcEntityComponentStore->m_Archetypes.Length; ++iChunk)
                {
                    var srcArchetype = SrcEntityComponentStore->m_Archetypes.Ptr[iChunk];
                    for (var j = 0; j < srcArchetype->Chunks.Count; ++j)
                    {
                        var chunk = srcArchetype->Chunks[j];
                        var firstManagedComponent = srcArchetype->FirstManagedComponent;
                        var numManagedComponents = srcArchetype->NumManagedComponents;
                        for (int i = 0; i < numManagedComponents; ++i)
                        {
                            int type = i + firstManagedComponent;
                            var a = (int*)ChunkDataUtility.GetComponentDataRO(chunk, srcArchetype, 0, type);
                            for (int ei = 0, count = chunk.Count; ei < count; ++ei)
                            {
                                var managedComponentIndex = a[ei];
                                if (managedComponentIndex == 0)
                                    continue;

                                SrcManagedIndices[srcCounter] = managedComponentIndex;
                                a[ei] = DstManagedIndices[srcCounter++];
                            }
                        }
                    }
                }

                Assert.AreEqual(SrcManagedIndices.Length, srcCounter);
            }
        }

        [BurstCompile]
        struct GatherManagedComponentIndicesInChunkJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public EntityComponentStore* SrcEntityComponentStore;
            [NativeDisableUnsafePtrRestriction] public EntityComponentStore* DstEntityComponentStore;
            public NativeArray<ChunkIndex> Chunks;

            public NativeArray<int> SrcManagedIndices;
            public NativeArray<int> DstManagedIndices;

            [NativeDisableUnsafePtrRestriction] public int* NonNullCount;

            public void Execute()
            {
                var count = DstManagedIndices.Length;
                DstEntityComponentStore->AllocateManagedComponentIndices((int*)DstManagedIndices.GetUnsafePtr(), count);

                int srcCounter = 0;
                for (var iChunk = 0; iChunk < Chunks.Length; ++iChunk)
                {
                    var chunk = Chunks[iChunk];
                    var srcArchetype = SrcEntityComponentStore->GetArchetype(chunk);
                    var firstManagedComponent = srcArchetype->FirstManagedComponent;
                    var numManagedComponents = srcArchetype->NumManagedComponents;
                    for (int i = 0; i < numManagedComponents; ++i)
                    {
                        int type = i + firstManagedComponent;
                        var a = (int*)ChunkDataUtility.GetComponentDataRO(chunk, srcArchetype, 0, type);
                        for (int ei = 0, entityCount = chunk.Count; ei < entityCount; ++ei)
                        {
                            var managedComponentIndex = a[ei];
                            if (managedComponentIndex == 0)
                                continue;

                            SrcManagedIndices[srcCounter] = managedComponentIndex;
                            a[ei] = DstManagedIndices[srcCounter++];
                        }
                    }
                }

                if (srcCounter < count)
                    DstEntityComponentStore->m_ManagedComponentFreeIndex.Add((int*)DstManagedIndices.GetUnsafePtr() + srcCounter, (count - srcCounter) * sizeof(int));
                *NonNullCount = srcCounter;
            }
        }

        [BurstCompile]
        struct RemapChunksFilteredJob : IJobParallelFor
        {
            [Collections.ReadOnly] public NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping;
            [Collections.ReadOnly] public NativeArray<RemapChunk> remapChunks;

            [NativeDisableUnsafePtrRestriction] public EntityComponentStore* dstEntityComponentStore;

            public TypeIndex chunkHeaderType;

            public void Execute(int index)
            {
                var chunk = remapChunks[index].chunk;
                Archetype* dstArchetype = remapChunks[index].dstArchetype;

                var entityCount = chunk.Count;
                dstEntityComponentStore->RemapChunk(chunk, 0, entityCount, ref entityRemapping);
                EntityRemapUtility.PatchEntities(dstArchetype->ScalarEntityPatches + 1,
                    dstArchetype->ScalarEntityPatchCount - 1, dstArchetype->BufferEntityPatches,
                    dstArchetype->BufferEntityPatchCount, chunk.Buffer, entityCount, ref entityRemapping);

                // Fix up chunk pointers in ChunkHeaders
                if (dstArchetype->HasChunkComponents)
                {
                    var metaArchetype = dstArchetype->MetaChunkArchetype;
                    var indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(metaArchetype, chunkHeaderType);
                    var offset = metaArchetype->Offsets[indexInTypeArray];
                    var sizeOf = sizeof(ChunkHeader);

                    // Set chunk header without bumping change versions since they are zeroed when processing meta chunk
                    // modifying them here would be a race condition
                    var metaChunkEntity = chunk.MetaChunkEntity;
                    var metaEntityInChunk = dstEntityComponentStore->GetEntityInChunk(metaChunkEntity);
                    var chunkHeader = (ChunkHeader*)(metaEntityInChunk.Chunk.Buffer + (offset + sizeOf * metaEntityInChunk.IndexInChunk));
                    chunkHeader->ArchetypeChunk = new ArchetypeChunk(chunk, dstEntityComponentStore);
                }
            }
        }

        [BurstCompile]
        struct MoveFilteredChunksBetweenArchetypeJob : IJob
        {
            [Collections.ReadOnly] public NativeArray<RemapChunk> RemapChunks;
            [Collections.ReadOnly] public NativeHashMap<int, int> RemapShared;
            [NativeDisableUnsafePtrRestriction] public EntityComponentStore* SrcEntityComponentStore;

            public void Execute()
            {
                int* sharedComponentCopy = stackalloc int[EntityComponentStore.kMaxSharedComponentCount];

                int chunkCount = RemapChunks.Length;
                for (int iChunk = 0; iChunk < chunkCount; ++iChunk)
                {
                    var chunk = RemapChunks[iChunk].chunk;
                    var srcArchetype = SrcEntityComponentStore->GetArchetype(chunk);
                    var dstArchetype = RemapChunks[iChunk].dstArchetype;
                    int numSharedComponents = dstArchetype->NumSharedComponents;
                    var sharedComponentValues = srcArchetype->Chunks.GetSharedComponentValues(chunk.ListIndex);
                    if (numSharedComponents != 0)
                    {
                        for (int i = 0; i < numSharedComponents; ++i)
                        {
                            var sharedComponentIndex = sharedComponentValues[i];

                            // Default values are not stored in the remap table, so we detect and skip them
                            if (EntityComponentStore.GetElementIndexFromSharedComponentIndex(sharedComponentIndex) != 0)
                                sharedComponentCopy[i] = RemapShared[sharedComponentIndex];
                            else
                                sharedComponentCopy[i] = 0;
                        }

                        sharedComponentValues = sharedComponentCopy;
                    }
                    ChunkDataUtility.MoveArchetype(srcArchetype, chunk, dstArchetype, sharedComponentValues);
                }
            }
        }

        [BurstCompile]
        struct RemapAllChunksJob : IJobParallelFor
        {
            [Collections.ReadOnly] public NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping;
            [Collections.ReadOnly] public NativeArray<RemapChunk> remapChunks;

            [NativeDisableUnsafePtrRestriction] public EntityComponentStore* dstEntityComponentStore;

            public void Execute(int index)
            {
                var chunk = remapChunks[index].chunk;
                Archetype* dstArchetype = remapChunks[index].dstArchetype;

                var entityCount = chunk.Count;
                dstEntityComponentStore->RemapChunk(chunk, 0, entityCount, ref entityRemapping);
                EntityRemapUtility.PatchEntities(dstArchetype->ScalarEntityPatches + 1,
                    dstArchetype->ScalarEntityPatchCount - 1, dstArchetype->BufferEntityPatches,
                    dstArchetype->BufferEntityPatchCount, chunk.Buffer, entityCount, ref entityRemapping);

                dstEntityComponentStore->SetArchetype(chunk, dstArchetype);
                chunk.ListIndex += dstArchetype->Chunks.Count;
                chunk.ListWithEmptySlotsIndex += dstArchetype->ChunksWithEmptySlots.Length;
            }
        }

        [BurstCompile]
        struct RemapAllArchetypesJob : IJobParallelFor
        {
            [DeallocateOnJobCompletion][Collections.ReadOnly] public NativeArray<RemapArchetype> remapArchetypes;

            [NativeDisableUnsafePtrRestriction] public EntityComponentStore* dstEntityComponentStore;

            [Collections.ReadOnly] public NativeParallelHashMap<int, int> remapShared;

            public TypeIndex chunkHeaderType;

            // This must be run after chunks have been remapped since FreeChunksBySharedComponents needs the shared component
            // indices in the chunks to be remapped
            public void Execute(int index)
            {
                var srcArchetype = remapArchetypes[index].srcArchetype;
                int srcChunkCount = srcArchetype->Chunks.Count;

                var dstArchetype = remapArchetypes[index].dstArchetype;
                int dstChunkCount = dstArchetype->Chunks.Count;

                dstArchetype->Chunks.MoveChunks(srcArchetype->Chunks);

                if (srcArchetype->NumSharedComponents == 0)
                {
                    if (srcArchetype->ChunksWithEmptySlots.Length != 0)
                    {
                        dstArchetype->ChunksWithEmptySlots.SetCapacity(
                            srcArchetype->ChunksWithEmptySlots.Length + dstArchetype->ChunksWithEmptySlots.Length);
                        dstArchetype->ChunksWithEmptySlots.AddRange(srcArchetype->ChunksWithEmptySlots);
                        srcArchetype->ChunksWithEmptySlots.Resize(0);
                    }
                }
                else
                {
                    for (int i = 0; i < dstArchetype->NumSharedComponents; ++i)
                    {
                        var srcArray = srcArchetype->Chunks.GetSharedComponentValueArrayForType(i);
                        var dstArray = dstArchetype->Chunks.GetSharedComponentValueArrayForType(i) + dstChunkCount;
                        for (int j = 0; j < srcChunkCount; ++j)
                        {
                            int srcIndex = srcArray[j];

                            if (EntityComponentStore.GetElementIndexFromSharedComponentIndex(srcIndex) != 0)
                            {
                                int remapped = remapShared[srcIndex];
                                dstArray[j] = remapped;
                            }
                            // Default values are not stored in the remap table, reset the dstArray element if we have a default value
                            else
                            {
                                dstArray[j] = 0;
                            }
                        }
                    }

                    var dstCapacity = dstArchetype->ChunkCapacity;
                    for (int i = 0; i < srcChunkCount; ++i)
                    {
                        var chunk = dstArchetype->Chunks[i + dstChunkCount];
                        if (chunk.Count < dstCapacity)
                            dstArchetype->FreeChunksBySharedComponents.Add(chunk);
                    }

                    srcArchetype->FreeChunksBySharedComponents.Init(srcArchetype, 16);
                }

                var globalSystemVersion = dstEntityComponentStore->GlobalSystemVersion;
                // Set change versions to GlobalSystemVersion
                for (int iType = 0; iType < dstArchetype->TypesCount; ++iType)
                {
                    var dstArray = dstArchetype->Chunks.GetChangeVersionArrayForType(iType) + dstChunkCount;
                    for (int i = 0; i < srcChunkCount; ++i)
                    {
                        dstArray[i] = globalSystemVersion;
                    }
                }

                // Copy chunk count array
                var dstCountArray = dstArchetype->Chunks.GetChunkEntityCountArray() + dstChunkCount;
                UnsafeUtility.MemCpy(dstCountArray, srcArchetype->Chunks.GetChunkEntityCountArray(),
                    sizeof(int) * srcChunkCount);

                // Copy enabled bits
                var srcEnabledBits = srcArchetype->Chunks.GetPointerToComponentEnabledArrayForArchetype();
                var dstEnabledBits = (byte*)dstArchetype->Chunks.GetComponentEnabledMaskArrayForChunk(dstChunkCount);
                long srcEnabledBitsSize = (long)srcArchetype->Chunks.ComponentEnabledBitsSizeTotalPerChunk * srcChunkCount;
                UnsafeUtility.MemCpy(dstEnabledBits, srcEnabledBits, srcEnabledBitsSize);

                // Adjust enabled bits hierarchical values
                for (int srcChunkIndex = 0; srcChunkIndex < srcChunkCount; ++srcChunkIndex)
                {
                    int dstChunkIndex = dstChunkCount + srcChunkIndex;
                    dstArchetype->Chunks.InitializeDisabledCountForChunk(dstChunkIndex);
                    var chunkEntityCount = dstArchetype->Chunks[dstChunkIndex].Count;
                    for (int t = 0; t < dstArchetype->EnableableTypesCount; ++t)
                    {
                        var dstIndexInArchetype = dstArchetype->EnableableTypeIndexInArchetype[t];
                        int dstMemoryOrderIndexInArchetype = dstArchetype->TypeIndexInArchetypeToMemoryOrderIndex[dstIndexInArchetype];
                        var dstEnabledBitsForChunk = dstArchetype->Chunks.GetEnabledArrayForTypeInChunk(dstMemoryOrderIndexInArchetype, dstChunkIndex);

                        var disabledCount = chunkEntityCount - dstEnabledBitsForChunk.CountBits(0, chunkEntityCount);
                        dstArchetype->Chunks.SetChunkDisabledCountForType(dstMemoryOrderIndexInArchetype, dstChunkIndex, disabledCount);
                    }
                }

                // Fix up chunk pointers in ChunkHeaders
                if (dstArchetype->HasChunkComponents)
                {
                    var metaArchetype = dstArchetype->MetaChunkArchetype;
                    var indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(metaArchetype, chunkHeaderType);
                    var offset = metaArchetype->Offsets[indexInTypeArray];
                    var sizeOf = metaArchetype->SizeOfs[indexInTypeArray];

                    for (int i = 0; i < srcChunkCount; ++i)
                    {
                        // Set chunk header without bumping change versions since they are zeroed when processing meta chunk
                        // modifying them here would be a race condition
                        var chunk = dstArchetype->Chunks[i + dstChunkCount];
                        var metaChunkEntity = chunk.MetaChunkEntity;
                        var metaEntityInChunk = dstEntityComponentStore->GetEntityInChunk(metaChunkEntity);
                        var chunkHeader = (ChunkHeader*)(metaEntityInChunk.Chunk.Buffer + (offset + sizeOf * metaEntityInChunk.IndexInChunk));
                        chunkHeader->ArchetypeChunk = new ArchetypeChunk(chunk, dstEntityComponentStore);
                    }
                }

                dstArchetype->EntityCount += srcArchetype->EntityCount;
                srcArchetype->Chunks.Dispose();
                srcArchetype->EntityCount = 0;
            }
        }

        [BurstCompile]
        struct MoveAllChunksJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public EntityComponentStore* srcEntityComponentStore;
            [NativeDisableUnsafePtrRestriction] public EntityComponentStore* dstEntityComponentStore;
            public NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping;

            public void Execute()
            {
                dstEntityComponentStore->AllocateEntitiesForRemapping(srcEntityComponentStore, ref entityRemapping);
            }
        }

        [BurstCompile]
        internal struct FreeAllChunksJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public EntityComponentStore* EntityComponentStore;

            public void Execute()
            {
                EntityComponentStore->FreeAllEntities(
#if ENTITY_STORE_V1
                    false
#endif
                    );
            }
        }

        #endregion

        internal uint GetChunkVersionHash(Entity entity)
        {
            var access = GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;

            if (!ecs->Exists(entity))
                return 0;

            var chunk = ecs->GetChunk(entity);
            var archetype = ecs->GetArchetype(chunk);
            var typeCount = archetype->TypesCount;

            uint hash = 0;
            var archetypeChunkData = archetype->Chunks;
            var chunkListIndex = chunk.ListIndex;
            for (int i = 0; i < typeCount; ++i)
            {
                hash += archetypeChunkData.GetChangeVersion(i, chunkListIndex);
            }

            return hash;
        }

        internal void BeforeStructuralChange()
        {
            GetCheckedEntityDataAccess()->BeforeStructuralChange();
        }

        internal TypeIndex GetComponentTypeIndex(Entity entity, int index)
        {
            var access = GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;

            ecs->AssertEntitiesExist(&entity, 1);
            var archetype = ecs->GetArchetype(entity);

            if ((uint)index >= archetype->TypesCount) return TypeIndex.Null;

            return archetype->Types[index + 1].TypeIndex;
        }

        internal EntityQuery CreateEntityQuery(ComponentType* requiredComponents, int count)
        {
            var access = GetCheckedEntityDataAccess();
            return access->EntityQueryManager->CreateEntityQuery(access, requiredComponents, count);
        }

        /// <summary>
        /// Check if an entity query is still valid
        /// </summary>
        /// <param name="query">The EnityQuery to check.</param>
        /// <returns>Returns true if the specified query handle is still valid (and can be disposed).</returns>
        public bool IsQueryValid(EntityQuery query)
        {
            return GetCheckedEntityDataAccess()->IsQueryValid(query);
        }

        internal bool HasComponentRaw(Entity entity, TypeIndex typeIndex)
        {
            var access = GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;
            return ecs->HasComponent(entity, typeIndex);
        }

        #endregion
    }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
    /// <summary>
    /// Variants of EntityManager methods that support managed components.
    /// </summary>
    public static unsafe partial class EntityManagerManagedComponentExtensions
    {
        /// <summary>
        /// Creates and supports only a single entity with a given managed component.
        /// </summary>
        /// <remarks>
        /// Note that while zero-size component may be created, they can not be accessed directly in code. They are
        /// only usable for situations such as in calling <see cref="ComponentSystemBase.RequireForUpdate{T}"/>.
        /// </remarks>
        /// <param name="manager">This entity manager.</param>
        /// <param name="name">The debug friendly name associated with the singleton component entity.</param>
        /// <typeparam name="T">The type of component to create.</typeparam>
        /// <returns>The Entity object that you can use to access the singleton component entity.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the component type has no fields, is enableable, or an entity containing it already exists.</exception>
        public static Entity CreateSingleton<T>(this EntityManager manager, FixedString64Bytes name = default) where T : class, IComponentData, new()
        {
            return manager.CreateSingletonEntityInternal<T>(name);
        }

        /// <summary>
        /// Creates and supports only a single entity with a given managed component.
        /// </summary>
        /// <remarks>
        /// Note that while zero-size component may be created, they can not be accessed directly in code. They are
        /// only usable for situations such as in calling <see cref="ComponentSystemBase.RequireForUpdate{T}"/>.
        /// </remarks>
        /// <param name="manager">This entity manager.</param>
        /// <param name="componentData">The data to assign to the singleton component.</param>
        /// <param name="name">The debug friendly name associated with the singleton component entity.</param>
        /// <typeparam name="T">The type of component to create.</typeparam>
        /// <returns>The Entity object that you can use to access the singleton component entity.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the component type has no fields, is enableable, or an entity containing it already exists.</exception>
        public static Entity CreateSingleton<T>(this EntityManager manager, T componentData, FixedString64Bytes name = default) where T : class, IComponentData, new()
        {
            var entity = manager.CreateSingletonEntityInternal<T>(name);
            manager.SetComponentData(entity, componentData);
            return entity;
        }

        private static Entity CreateSingletonEntityInternal<T>(this EntityManager manager, FixedString64Bytes name = default) where T : class, IComponentData, new()
        {
            var componentType = ComponentType.ReadWrite<T>();
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            EntityQuery query = manager.CreateEntityQuery(componentType);
            var typeIndex = TypeManager.GetTypeIndex<T>();
            if (query.CalculateEntityCount() != 0)
            {
                var typeName = typeIndex.ToFixedString();
                throw new InvalidOperationException($"Singleton component {typeName} can not be created because entities already exist with this component type.");
            }
            if (TypeManager.IsEnableable(typeIndex))
            {
                var typeName = typeIndex.ToFixedString();
                throw new InvalidOperationException($"Singleton component {typeName} can not be created because it is an enableable component type.");
            }
#endif
            var entity = manager.CreateEntity(manager.CreateArchetype(&componentType, 1));

#if !DOTS_DISABLE_DEBUG_NAMES
            if (name == default)
                name.CopyFromTruncated(componentType.ToFixedString());
            manager.GetCheckedEntityDataAccess()->SetName(entity, in name);
#endif
            return entity;
        }

        /// <summary>
        /// Gets the value of a component for an entity.
        /// </summary>
        /// <param name="manager">This entity manager.</param>
        /// <param name="entity">The entity.</param>
        /// <typeparam name="T">The type of component to retrieve.</typeparam>
        /// <returns>A struct of type T containing the component value.</returns>
        /// <exception cref="ArgumentException">Thrown if the component type has no fields.</exception>
        public static T GetComponentData<T>(this EntityManager manager, Entity entity) where T : class, IComponentData, new()
        {
            var access = manager.GetCheckedEntityDataAccess();
            return access->GetComponentData<T>(entity, access->ManagedComponentStore);
        }

        /// <summary>
        /// Gets the value of a component for an entity associated with a system.
        /// </summary>
        /// <param name="manager">This entity manager.</param>
        /// <param name="system">The system handle.</param>
        /// <typeparam name="T">The type of component to retrieve.</typeparam>
        /// <returns>A struct of type T containing the component value.</returns>
        /// <exception cref="ArgumentException">Thrown if the component type has no fields.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the system isn't from thie world.</exception>
        public static T GetComponentData<T>(this EntityManager manager, SystemHandle system) where T : class, IComponentData, new()
        {
            var access = manager.GetCheckedEntityDataAccess();
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (system.m_WorldSeqNo != access->m_WorldUnmanaged.SequenceNumber)
                throw new InvalidOperationException("System is from a different world.");
#endif
            return access->GetComponentData<T>(system.m_Entity, access->ManagedComponentStore);
        }

        /// <summary>
        /// Sets the value of a component of an entity.
        /// </summary>
        /// <param name="manager">This entity manager.</param>
        /// <param name="entity">The entity.</param>
        /// <param name="componentData">The data to set.</param>
        /// <typeparam name="T">The component type.</typeparam>
        /// <exception cref="ArgumentException">Thrown if the component type has no fields.</exception>
        public static void SetComponentData<T>(this EntityManager manager, Entity entity, T componentData) where T : class, IComponentData, new()
        {
            var type = ComponentType.ReadWrite<T>();
            manager.SetComponentObject(entity, type, componentData);
        }

        /// <summary>
        /// Sets the value of a component of an entity associated with a system.
        /// </summary>
        /// <param name="manager">This entity manager.</param>
        /// <param name="system">The system handle.</param>
        /// <param name="componentData">The data to set.</param>
        /// <typeparam name="T">The component type.</typeparam>
        /// <exception cref="ArgumentException">Thrown if the component type has no fields.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the system isn't from this world.</exception>
        public static void SetComponentData<T>(this EntityManager manager, SystemHandle system, T componentData) where T : class, IComponentData, new()
        {
            var access = manager.GetCheckedEntityDataAccess();
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (system.m_WorldSeqNo != access->m_WorldUnmanaged.SequenceNumber)
                throw new InvalidOperationException("System is from a different world.");
#endif
            var type = ComponentType.ReadWrite<T>();
            access->SetComponentObject(system.m_Entity, type, componentData);
        }

        /// <summary>
        /// Gets the value of a chunk component.
        /// </summary>
        /// <remarks>
        /// A chunk component is common to all entities in a chunk. You can access a chunk <see cref="IComponentData"/>
        /// instance through either the chunk itself or through an entity stored in that chunk.
        /// </remarks>
        /// <param name="manager">This entity manager.</param>
        /// <param name="chunk">The chunk.</param>
        /// <typeparam name="T">The component type.</typeparam>
        /// <returns>A struct of type T containing the component value.</returns>
        /// <exception cref="ArgumentException">Thrown if the ArchetypeChunk object is invalid.</exception>
        public static T GetChunkComponentData<T>(this EntityManager manager, ArchetypeChunk chunk) where T : class, IComponentData, new()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (chunk.Invalid())
            {
                var typeName = TypeManager.GetTypeIndex<T>().ToFixedString();
                throw new System.ArgumentException(
                    $"GetChunkComponentData<{typeName}> can not be called with an invalid archetype chunk.");
            }
#endif
            var metaChunkEntity = chunk.m_Chunk.MetaChunkEntity;
            return manager.GetComponentData<T>(metaChunkEntity);
        }

        /// <summary>
        /// Gets the value of chunk component for the chunk containing the specified entity.
        /// </summary>
        /// <remarks>
        /// A chunk component is common to all entities in a chunk. You can access a chunk <see cref="IComponentData"/>
        /// instance through either the chunk itself or through an entity stored in that chunk.
        /// </remarks>
        /// <param name="manager">This entity manager.</param>
        /// <param name="entity">The entity.</param>
        /// <typeparam name="T">The component type.</typeparam>
        /// <returns>A struct of type T containing the component value.</returns>
        public static T GetChunkComponentData<T>(this EntityManager manager, Entity entity) where T : class, IComponentData, new()
        {
            var access = manager.GetCheckedEntityDataAccess();
            access->EntityComponentStore->AssertEntitiesExist(&entity, 1);
            var chunk = access->EntityComponentStore->GetChunk(entity);
            var metaChunkEntity = chunk.MetaChunkEntity;
            return access->GetComponentData<T>(metaChunkEntity, access->ManagedComponentStore);
        }

        /// <summary>
        /// Sets the value of a chunk component.
        /// </summary>
        /// <remarks>
        /// A chunk component is common to all entities in a chunk. You can access a chunk <see cref="IComponentData"/>
        /// instance through either the chunk itself or through an entity stored in that chunk.
        /// </remarks>
        /// <param name="manager">This entity manager.</param>
        /// <param name="chunk">The chunk to modify.</param>
        /// <param name="componentValue">The component data to set.</param>
        /// <typeparam name="T">The component type.</typeparam>
        /// <exception cref="ArgumentException">Thrown if the ArchetypeChunk object is invalid.</exception>
        public static void SetChunkComponentData<T>(this EntityManager manager, ArchetypeChunk chunk, T componentValue) where T : class, IComponentData, new()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (chunk.Invalid())
            {
                var typeName = TypeManager.GetTypeIndex<T>().ToFixedString();
                throw new System.ArgumentException(
                    $"SetChunkComponentData<{typeName}> can not be called with an invalid archetype chunk.");
            }
#endif
            var metaChunkEntity = chunk.m_Chunk.MetaChunkEntity;
            manager.SetComponentData<T>(metaChunkEntity, componentValue);
        }

        /// <summary>
        /// Adds a managed component to an entity and set the value of that component.
        /// </summary>
        /// <remarks>
        /// Adding a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="manager">This entity manager.</param>
        /// <param name="entity">The entity.</param>
        /// <param name="componentData">The data to set.</param>
        /// <typeparam name="T">The type of component.</typeparam>
        public static void AddComponentData<T>(this EntityManager manager, Entity entity, T componentData) where T : class, IComponentData, new()
        {
            var type = ComponentType.ReadWrite<T>();

            manager.AddComponent(entity, type);
            manager.SetComponentData(entity, componentData);
        }

        /// <summary>
        /// Adds a managed component to an entity associated with a system and set the value of that component.
        /// </summary>
        /// <remarks>
        /// Adding a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="manager">This entity manager.</param>
        /// <param name="system">The system handle.</param>
        /// <param name="componentData">The data to set.</param>
        /// <typeparam name="T">The type of component.</typeparam>
        public static void AddComponentData<T>(this EntityManager manager, SystemHandle system, T componentData) where T : class, IComponentData, new()
        {
            manager.AddComponentData(system.m_Entity, componentData);
        }

        /// <summary>
        /// Safely move a managed component on the source entity to another destination entity.
        /// </summary>
        /// <remarks>
        /// If the source and destination entity are identical, no operation is performed.
        ///
        /// This operation seems similar to
        ///
        /// value = GetComponentData&lt;T&gt;(src);
        /// AddComponentData(dst, value)
        /// RemoveComponent&lt;T&gt;(src)
        ///
        /// But for managed components which implement <see cref="IDisposable"/>, calling RemoveComponent will invoke
        /// Dispose() on the component value, leaving the destination entity with an uninitialized object.```
        /// This operation ensures the component is properly moved over.
        /// </remarks>
        /// <param name="manager">This entity manager.</param>
        /// <param name="src">
        /// The Entity the managed component will be removed from. This entity must have a component
        /// of type <typeparamref name="T"/>.
        /// </param>
        /// <param name="dst">
        /// The Entity the managed component will be added to. If this entity already has
        /// <typeparamref name="T"/> with a different value than <paramref name="src"/>, the existing value will be
        /// removed and disposed before the new value is assigned.
        /// </param>
        /// <typeparam name="T">The managed component type.</typeparam>
        public static void MoveComponent<T>(this EntityManager manager, Entity src, Entity dst) where T : class, IComponentData, new()
        {
            var access = manager.GetCheckedEntityDataAccess();
            var changes = access->BeginStructuralChanges();
            access->MoveComponentObjectDuringStructuralChange(src, dst, ComponentType.ReadWrite<T>());
            access->EndStructuralChanges(ref changes);
        }

        /// <summary>
        /// Adds a chunk component to the specified entity.
        /// </summary>
        /// <remarks>
        /// Adding a chunk component to an entity changes that entity's archetype and results in the entity being moved
        /// to a different chunk, either one that already has an archetype containing the chunk component or a new
        /// chunk.
        ///
        /// A chunk component is common to all entities in a chunk. You can access a chunk <see cref="IComponentData"/>
        /// instance through either the chunk itself or through an entity stored in that chunk. In either case, getting
        /// or setting the component reads or writes the same data.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="manager">This entity manager.</param>
        /// <param name="entity">The entity.</param>
        /// <typeparam name="T">The type of component, which must implement IComponentData.</typeparam>
        public static void AddChunkComponentData<T>(this EntityManager manager, Entity entity) where T : class, IComponentData, new()
        {
            manager.AddComponent(entity, ComponentType.ChunkComponent<T>());
        }

        /// <summary>
        /// Adds a managed chunk component to each of the chunks identified by an EntityQuery and set the component values.
        /// </summary>
        /// <remarks>
        /// This function finds all chunks whose archetype satisfies the EntityQuery and adds the specified
        /// component to them.
        ///
        /// A chunk component is common to all entities in a chunk. You can access a chunk <see cref="IComponentData"/>
        /// instance through either the chunk itself or through an entity stored in that chunk.
        ///
        /// **Important:** This method creates a sync point, which means that the EntityManager waits for all
        /// currently running jobs to complete before adding the component. No additional jobs can start before
        /// the method is finished. A sync point can cause a drop in performance because the ECS framework might not
        /// be able to use the processing power of all available cores.
        /// </remarks>
        /// <param name="manager">This entity manager.</param>
        /// <param name="entityQuery">The EntityQuery identifying the chunks to modify.</param>
        /// <param name="componentData">The data to set.</param>
        /// <typeparam name="T">The type of component, which must implement IComponentData.</typeparam>
        public static void AddChunkComponentData<T>(this EntityManager manager, EntityQuery entityQuery, T componentData) where T : class, IComponentData, new()
        {
            var access = manager.GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;

            access->AssertQueryIsValid(entityQuery);

            using var chunks = entityQuery.ToArchetypeChunkArray(Allocator.TempJob);
            if (chunks.Length > 0)
            {
                ecs->AssertCanAddComponent(entityQuery._GetImpl(), ComponentType.ChunkComponent<T>());
                var type = ComponentType.ReadWrite<T>();
                var chunkType = ComponentType.FromTypeIndex(TypeManager.MakeChunkComponentTypeIndex(type.TypeIndex));

                var changes = access->BeginStructuralChanges();
                StructuralChange.AddComponentChunks(ecs, (ArchetypeChunk*)NativeArrayUnsafeUtility.GetUnsafePtr(chunks), chunks.Length, chunkType.TypeIndex);
                access->EndStructuralChanges(ref changes);

                manager.SetChunkComponent(chunks, componentData);
            }
        }

        static void SetChunkComponent<T>(this EntityManager manager, NativeArray<ArchetypeChunk> chunks, T componentData) where T : class, IComponentData, new()
        {
            for (int i = 0; i < chunks.Length; i++)
            {
                var srcChunk = chunks[i].m_Chunk;
                manager.SetComponentData(srcChunk.MetaChunkEntity, componentData);
            }
        }
    }
#endif
}
