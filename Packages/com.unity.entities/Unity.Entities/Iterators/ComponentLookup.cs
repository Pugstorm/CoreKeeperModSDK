using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities
{
    /// <summary> Obsolete. Use <see cref="ComponentLookup{T}"/> instead.</summary>
    /// <typeparam name="T">The type of <see cref="IComponentData"/> to access.</typeparam>
    [Obsolete("This type has been renamed to ComponentLookup<T>. (RemovedAfter Entities 1.0) (UnityUpgradable) -> ComponentLookup<T>", true)]
    public struct ComponentDataFromEntity<T> where T : unmanaged, IComponentData
    {
    }

    /// <summary>
    /// A [NativeContainer] that provides access to all instances of components of type T, indexed by <see cref="Entity"/>.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IComponentData"/> to access.</typeparam>
    /// <remarks>
    /// ComponentLookup is a native container that provides array-like access to components of a specific
    /// type. You can use ComponentLookup to look up data associated with one entity while iterating over a
    /// different set of entities. For example, Unity.Transforms stores the <see cref="Entity"/> object of parent entities
    /// in a Parent component and looks up the parent's LocalToWorld matrix using
    /// ComponentLookup&lt;LocalToWorld&gt; when calculating the world positions of child entities.
    ///
    /// To get a ComponentLookup, call <see cref="ComponentSystemBase.GetComponentLookup{T}"/>.
    ///
    /// Pass a ComponentLookup container to a job by defining a public field of the appropriate type
    /// in your IJob implementation. You can safely read from ComponentLookup in any job, but by
    /// default, you cannot write to components in the container in parallel jobs (including
    /// <see cref="IJobEntity"/>, Entities.Foreach and <see cref="IJobChunk"/>). If you know that two instances of a parallel
    /// job can never write to the same index in the container, you can disable the restriction on parallel writing
    /// by adding [NativeDisableParallelForRestrictionAttribute] to the ComponentLookup field definition in the job struct.
    ///
    /// If you would like to access an entity's components outside of a job, consider using the <see cref="EntityManager"/> methods
    /// <see cref="EntityManager.GetComponentData{T}"/> and <see cref="EntityManager.SetComponentData{T}"/>
    /// instead, to avoid the overhead of creating a ComponentLookup object.
    ///
    /// [NativeContainer]: https://docs.unity3d.com/ScriptReference/Unity.Collections.LowLevel.Unsafe.NativeContainerAttribute
    /// [NativeDisableParallelForRestrictionAttribute]: https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeDisableParallelForRestrictionAttribute.html
    /// </remarks>
    [NativeContainer]
    public unsafe struct ComponentLookup<T> where T : unmanaged, IComponentData
    {
        [NativeDisableUnsafePtrRestriction]
        readonly EntityDataAccess*       m_Access;
        LookupCache                      m_Cache;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle               m_Safety;
#endif
        readonly TypeIndex               m_TypeIndex;
        uint                             m_GlobalSystemVersion;
        readonly byte                    m_IsZeroSized;          // cache of whether T is zero-sized
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        readonly byte                    m_IsReadOnly;
#endif

        internal uint GlobalSystemVersion => m_GlobalSystemVersion;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal ComponentLookup(TypeIndex typeIndex, EntityDataAccess* access, bool isReadOnly)
        {
            var safetyHandles = &access->DependencyManager->Safety;
            m_Safety = safetyHandles->GetSafetyHandleForComponentLookup(typeIndex, isReadOnly);
            m_IsReadOnly = isReadOnly ? (byte)1 : (byte)0;
            m_TypeIndex = typeIndex;
            m_Access = access;
            m_Cache = default;
            m_GlobalSystemVersion = access->EntityComponentStore->GlobalSystemVersion;
            m_IsZeroSized = ComponentType.FromTypeIndex(typeIndex).IsZeroSized ? (byte)1 : (byte)0;
        }

#else
        internal ComponentLookup(int typeIndex, EntityDataAccess* access)
        {
            m_TypeIndex = typeIndex;
            m_Access = access;
            m_Cache = default;
            m_GlobalSystemVersion = access->EntityComponentStore->GlobalSystemVersion;
            m_IsZeroSized = ComponentType.FromTypeIndex(typeIndex).IsZeroSized ? (byte)1 : (byte)0;
        }

#endif

        /// <summary>
        /// When a ComponentLookup is cached by a system across multiple system updates, calling this function
        /// inside the system's OnUpdate() method performs the minimal incremental updates necessary to make the
        /// type handle safe to use.
        /// </summary>
        /// <param name="system">The system on which this type handle is cached.</param>
        public void Update(SystemBase system)
        {
            Update(ref *system.m_StatePtr);
        }

        /// <summary>
        /// When a ComponentLookup is cached by a system across multiple system updates, calling this function
        /// inside the system's OnUpdate() method performs the minimal incremental updates necessary to make the
        /// type handle safe to use.
        /// </summary>
        /// <param name="systemState">The SystemState of the system on which this type handle is cached.</param>
        public void Update(ref SystemState systemState)
        {
            // NOTE: We could in theory fetch all this data from m_Access.EntityComponentStore and void the SystemState from being passed in.
            //       That would unfortunately allow this API to be called from a job. So we use the required system parameter as a way of signifying to the user that this can only be invoked from main thread system code.
            //       Additionally this makes the API symmetric to ComponentTypeHandle.
            m_GlobalSystemVersion =  systemState.m_EntityComponentStore->GlobalSystemVersion;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandles = &m_Access->DependencyManager->Safety;
            m_Safety = safetyHandles->GetSafetyHandleForComponentLookup(m_TypeIndex, m_IsReadOnly != 0);
#endif
        }

        /// <summary>
        /// Reports whether the specified <see cref="Entity"/> instance still refers to a valid entity and that it has a
        /// component of type T.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>True if the entity has a component of type T, and false if it does not. Also returns false if
        /// the Entity instance refers to an entity that has been destroyed.</returns>
        public bool HasComponent(Entity entity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            var ecs = m_Access->EntityComponentStore;
            return ecs->HasComponent(entity, m_TypeIndex, ref m_Cache);
        }

        /// <summary>
        /// Reports whether the specified <see cref="SystemHandle"/> associated <see cref="Entity"/> is valid and contains a
        /// component of type T.
        /// </summary>
        /// <param name="system">The system handle.</param>
        /// <returns>True if the entity associated with the system has a component of type T, and false if it does not. Also returns false if
        /// the system handle refers to a system that has been destroyed.</returns>
        public bool HasComponent(SystemHandle system)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            var ecs = m_Access->EntityComponentStore;
            return ecs->HasComponent(system.m_Entity, m_TypeIndex, ref m_Cache);
        }

        /// <summary>
        /// Retrieves the component associated with the specified <see cref="Entity"/>, if it exists. Then reports if the instance still refers to a valid entity and that it has a
        /// component of type T.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// /// <param name="componentData">The component of type T for the given entity, if it exists.</param>
        /// <returns>True if the entity has a component of type T, and false if it does not.</returns>
        public bool TryGetComponent(Entity entity, out T componentData)
        {

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            var ecs = m_Access->EntityComponentStore;

            if (m_IsZeroSized != 0)
            {
                componentData = default;
                return ecs->HasComponent(entity, m_TypeIndex, ref m_Cache);
            }

            if (Hint.Unlikely(!ecs->Exists(entity)))
            {
                componentData = default;
                return false;
            }
            void* ptr = ecs->GetOptionalComponentDataWithTypeRO(entity, m_TypeIndex, ref m_Cache);
            if (ptr == null)
            {
                componentData = default;
                return false;
            }
            UnsafeUtility.CopyPtrToStructure(ptr, out componentData);
            return true;
        }

        /// <summary>
        /// Reports whether any of IComponentData components of the type T, in the chunk containing the
        /// specified <see cref="Entity"/>, could have changed.
        /// </summary>
        /// <remarks>
        /// Note that for efficiency, the change version applies to whole chunks not individual entities. The change
        /// version is incremented even when another job or system that has declared write access to a component does
        /// not actually change the component value.</remarks>
        /// <param name="entity">The entity.</param>
        /// <param name="version">The version to compare. In a system, this parameter should be set to the
        /// current <see cref="Unity.Entities.ComponentSystemBase.LastSystemVersion"/> at the time the job is run or
        /// scheduled.</param>
        /// <returns>True, if the version number stored in the chunk for this component is more recent than the version
        /// passed to the <paramref name="version"/> parameter.</returns>
        public bool DidChange(Entity entity, uint version)
        {
            var ecs = m_Access->EntityComponentStore;
            var chunk = ecs->GetChunk(entity);
            var archetype = ecs->GetArchetype(chunk);
            if (Hint.Unlikely(archetype != m_Cache.Archetype))
                m_Cache.Update(archetype, m_TypeIndex);
            var typeIndexInArchetype = m_Cache.IndexInArchetype;
            if (typeIndexInArchetype == -1) return false;
            var chunkVersion = archetype->Chunks.GetChangeVersion(typeIndexInArchetype, chunk.ListIndex);

            return ChangeVersionUtility.DidChange(chunkVersion, version);
        }

        /// <summary>
        /// Gets the <see cref="IComponentData"/> instance of type T for the specified entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>An <see cref="IComponentData"/> type.</returns>
        /// <remarks>
        /// Normally, you cannot write to components accessed using a ComponentLookup instance
        /// in a parallel Job. This restriction is in place because multiple threads could write to the same component,
        /// leading to a race condition and nondeterministic results. However, when you are certain that your algorithm
        /// cannot write to the same component from different threads, you can manually disable this safety check
        /// by putting the [NativeDisableParallelForRestriction] attribute on the ComponentLookup field in the Job.
        ///
        /// [NativeDisableParallelForRestrictionAttribute]: https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeDisableParallelForRestrictionAttribute.html
        /// </remarks>
        public T this[Entity entity]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                var ecs = m_Access->EntityComponentStore;
                ecs->AssertEntityHasComponent(entity, m_TypeIndex, ref m_Cache);

                if (m_IsZeroSized != 0)
                    return default;

                void* ptr = ecs->GetComponentDataWithTypeRO(entity, m_TypeIndex, ref m_Cache);
                UnsafeUtility.CopyPtrToStructure(ptr, out T data);

                return data;
            }
            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                var ecs = m_Access->EntityComponentStore;
                ecs->AssertEntityHasComponent(entity, m_TypeIndex, ref m_Cache);

                if (m_IsZeroSized != 0)
                    return;

                void* ptr = ecs->GetComponentDataWithTypeRW(entity, m_TypeIndex, m_GlobalSystemVersion, ref m_Cache);
                UnsafeUtility.CopyStructureToPtr(ref value, ptr);
            }
        }

        /// <summary>
        /// Gets the <see cref="IComponentData"/> instance of type T for the specified system's associated entity.
        /// </summary>
        /// <param name="system">The system handle.</param>
        /// <returns>An <see cref="IComponentData"/> type.</returns>
        /// <remarks>
        /// Normally, you cannot write to components accessed using a ComponentDataFromEntity instance
        /// in a parallel Job. This restriction is in place because multiple threads could write to the same component,
        /// leading to a race condition and nondeterministic results. However, when you are certain that your algorithm
        /// cannot write to the same component from different threads, you can manually disable this safety check
        /// by putting the [NativeDisableParallelForRestriction] attribute on the ComponentDataFromEntity field in the Job.
        ///
        /// [NativeDisableParallelForRestrictionAttribute]: https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeDisableParallelForRestrictionAttribute.html
        /// </remarks>
        public T this[SystemHandle system]
        {
            get => this[system.m_Entity];
            set => this[system.m_Entity] = value;
        }

        /// <summary>
        /// Checks whether the <see cref="IComponentData"/> of type T is enabled on the specified <see cref="Entity"/>.
        /// For the purposes of EntityQuery matching, an entity with a disabled component will behave as if it does not
        /// have that component. The type T must implement the <see cref="IEnableableComponent"/> interface.
        /// </summary>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entity">The entity whose component should be checked.</param>
        /// <returns>True if the specified component is enabled, or false if it is disabled.</returns>
        /// <seealso cref="SetComponentEnabled(Entity, bool)"/>
        public bool IsComponentEnabled(Entity entity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return m_Access->IsComponentEnabled(entity, m_TypeIndex, ref m_Cache);
        }

        /// <summary>
        /// Checks whether the <see cref="IComponentData"/> of type T is enabled on the specified system using a <see cref="SystemHandle"/>.
        /// For the purposes of EntityQuery matching, a system with a disabled component will behave as if it does not
        /// have that component. The type T must implement the <see cref="IEnableableComponent"/> interface.
        /// </summary>
        /// <exception cref="ArgumentException">The <see cref="SystemHandle"/> does not exist.</exception>
        /// <param name="systemHandle">The system whose component should be checked.</param>
        /// <returns>True if the specified component is enabled, or false if it is disabled.</returns>
        /// <seealso cref="SetComponentEnabled(SystemHandle, bool)"/>
        public bool IsComponentEnabled(SystemHandle systemHandle)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return m_Access->IsComponentEnabled(systemHandle.m_Entity, m_TypeIndex, ref m_Cache);
        }

        /// <summary>
        /// Enable or disable the <see cref="IComponentData"/> of type T on the specified system using a <see cref="SystemHandle"/>. This operation
        /// does not cause a structural change (even if it occurs on a worker thread), or affect the value of the component.
        /// For the purposes of EntityQuery matching, a system with a disabled component will behave as if it does not
        /// have that component. The type T must implement the <see cref="IEnableableComponent"/> interface.
        /// </summary>
        /// <exception cref="ArgumentException">The <see cref="SystemHandle"/> does not exist.</exception>
        /// <param name="systemHandle">The system whose component should be enabled or disabled.</param>
        /// <param name="value">True if the specified component should be enabled, or false if it should be disabled.</param>
        /// <seealso cref="IsComponentEnabled(SystemHandle)"/>
        public void SetComponentEnabled(SystemHandle systemHandle, bool value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            m_Access->SetComponentEnabled(systemHandle.m_Entity, m_TypeIndex, value, ref m_Cache);
        }

        /// <summary>
        /// Enable or disable the <see cref="IComponentData"/> of type T on the specified <see cref="Entity"/>. This operation
        /// does not cause a structural change (even if it occurs on a worker thread), or affect the value of the component.
        /// For the purposes of EntityQuery matching, an entity with a disabled component will behave as if it does not
        /// have that component. The type T must implement the <see cref="IEnableableComponent"/> interface.
        /// </summary>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="entity">The entity whose component should be enabled or disabled.</param>
        /// <param name="value">True if the specified component should be enabled, or false if it should be disabled.</param>
        /// <seealso cref="IsComponentEnabled(Entity)"/>
        public void SetComponentEnabled(Entity entity, bool value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            m_Access->SetComponentEnabled(entity, m_TypeIndex, value, ref m_Cache);
        }

        /// <summary>
        /// Gets a safe reference to the component data.
        /// </summary>
        /// <param name="system">The system handle with the referenced entity</param>
        /// <returns>Returns a safe reference to the component data. Throws an
        /// exception if the component doesn't exist.</returns>
        public RefRW<T> GetRefRW(SystemHandle system)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            var ecs = m_Access->EntityComponentStore;
            ecs->AssertEntityHasComponent(system.m_Entity, m_TypeIndex, ref m_Cache);

            if (m_IsZeroSized != 0)
                return default;

            void* ptr = ecs->GetComponentDataWithTypeRW(system.m_Entity, m_TypeIndex, m_GlobalSystemVersion, ref m_Cache);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new RefRW<T>(ptr, m_Safety);
#else
            return new RefRW<T> (ptr);
#endif
        }

        /// <summary>
        /// Gets a safe reference to the component data.
        /// </summary>
        /// <param name="entity">The referenced entity</param>
        /// <returns>Returns a safe reference to the component data. Throws an
        /// exception if the component doesn't exist.</returns>
        public RefRW<T> GetRefRW(Entity entity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            var ecs = m_Access->EntityComponentStore;
            ecs->AssertEntityHasComponent(entity, m_TypeIndex, ref m_Cache);

            if (m_IsZeroSized != 0)
                return default;

            void* ptr = ecs->GetComponentDataWithTypeRW(entity, m_TypeIndex, m_GlobalSystemVersion, ref m_Cache);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new RefRW<T> (ptr, m_Safety);
#else
            return new RefRW<T> (ptr);
#endif
        }

        /// <summary>
        /// Gets a safe reference to the component data.
        /// </summary>
        /// <param name="entity">The referenced entity</param>
        /// <returns>Returns a safe reference to the component data. Throws an
        /// exception if the component doesn't exist.</returns>
        public RefRO<T> GetRefRO(Entity entity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            var ecs = m_Access->EntityComponentStore;
            ecs->AssertEntityHasComponent(entity, m_TypeIndex, ref m_Cache);

            if (m_IsZeroSized != 0)
                return default;

            void* ptr = ecs->GetComponentDataWithTypeRO(entity, m_TypeIndex, ref m_Cache);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new RefRO<T> (ptr, m_Safety);
#else
            return new RefRO<T> (ptr);
#endif
        }

        /// <summary>
        /// Gets a safe reference to the component data and a default RefRW (RefRW.IsValid == false).
        /// /// </summary>
        /// <param name="entity">The referenced entity</param>
        /// <returns>Returns a safe reference to the component data and a default RefRW.</returns>
        public RefRW<T> GetRefRWOptional(Entity entity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            if (!HasComponent(entity))
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                return new RefRW<T>(null, default);
#else
                return new RefRW<T>(null);
#endif

            if (m_IsZeroSized != 0)
                return default;

            var ecs = m_Access->EntityComponentStore;
            void* ptr = ecs->GetComponentDataWithTypeRW(entity, m_TypeIndex, m_GlobalSystemVersion, ref m_Cache);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new RefRW<T> (ptr, m_Safety);
#else
            return new RefRW<T> (ptr);
#endif
        }

        /// <summary>
        /// Gets a safe reference to the component data and
        /// a default RefRO (RefRO.IsValid == false).
        /// </summary>
        /// <param name="entity">The referenced entity</param>
        /// <returns>Returns a safe reference to the component data and a default RefRW.</returns>
        public RefRO<T> GetRefROOptional(Entity entity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            if (!HasComponent(entity))
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                return new RefRO<T>(null, default);
#else
                return new RefRO<T>(null);
#endif

            if (m_IsZeroSized != 0)
                return default;

            var ecs = m_Access->EntityComponentStore;
            void* ptr = ecs->GetComponentDataWithTypeRO(entity, m_TypeIndex, ref m_Cache);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new RefRO<T>(ptr, m_Safety);
#else
            return new RefRO<T> (ptr);
#endif
        }

        SafeBitRef MakeSafeBitRef(ulong* ptr, int offsetInBits)
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            => new SafeBitRef(ptr, offsetInBits, m_Safety);
#else
            => new SafeBitRef(ptr, offsetInBits);
#endif
        /// <summary>
        /// Gets a safe reference to the component enabled state.
        /// </summary>
        /// <typeparam name="T2">The component type</typeparam>
        /// <param name="entity">The referenced entity</param>
        /// <returns>Returns a safe reference to the component enabled state.
        /// Throws an exception if the component doesn't exist.</returns>
        public EnabledRefRW<T2> GetEnabledRefRW<T2>(Entity entity) where T2 : unmanaged, IEnableableComponent, IComponentData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            var ecs = m_Access->EntityComponentStore;
            ecs->AssertEntityHasComponent(entity, m_TypeIndex, ref m_Cache);

            int indexInBitField;
            int* ptrChunkDisabledCount;
            var ptr = ecs->GetEnabledRawRW(entity, m_TypeIndex, ref m_Cache, m_GlobalSystemVersion,
                out indexInBitField, out ptrChunkDisabledCount);

            return new EnabledRefRW<T2>(MakeSafeBitRef(ptr, indexInBitField), ptrChunkDisabledCount);
        }

        /// <summary>
        /// Gets a safe reference to the component enabled state.
        /// </summary>
        /// <typeparam name="T2">The component type</typeparam>
        /// <param name="entity">The referenced entity</param>
        /// <returns>Returns a safe reference to the component enabled state. If the component
        /// doesn't exist, it returns a default ComponentEnabledRefRW.</returns>
        public EnabledRefRW<T2> GetComponentEnabledRefRWOptional<T2>(Entity entity)
            where T2 : unmanaged, IComponentData, IEnableableComponent
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            if (!HasComponent(entity))
                return new EnabledRefRW<T2>(default, default);

            var ecs = m_Access->EntityComponentStore;
            ecs->AssertEntityHasComponent(entity, m_TypeIndex, ref m_Cache);

            var ptr = ecs->GetEnabledRawRW(entity, m_TypeIndex, ref m_Cache, m_GlobalSystemVersion,
                out var indexInBitField, out var ptrChunkDisabledCount);

            return new EnabledRefRW<T2>(MakeSafeBitRef(ptr, indexInBitField), ptrChunkDisabledCount);
        }

        /// <summary>
        /// Gets a safe reference to the component enabled state.
        /// </summary>
        /// <typeparam name="T2">The component type</typeparam>
        /// <param name="entity">The referenced entity</param>
        /// <returns>Returns a safe reference to the component enabled state.
        /// Throws an exception if the component doesn't exist.</returns>
        public EnabledRefRO<T2> GetEnabledRefRO<T2>(Entity entity) where T2 : unmanaged, IEnableableComponent, IComponentData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            var ecs = m_Access->EntityComponentStore;
            ecs->AssertEntityHasComponent(entity, m_TypeIndex, ref m_Cache);
            int indexInBitField;
            var ptr = ecs->GetEnabledRawRO(entity, m_TypeIndex, ref m_Cache, out indexInBitField, out _);
            return new EnabledRefRO<T2>(MakeSafeBitRef(ptr, indexInBitField));
        }

        /// <summary>
        /// Gets a safe reference to the component enabled state.
        /// </summary>
        /// <typeparam name="T2">The component type</typeparam>
        /// <param name="entity">The referenced entity</param>
        /// <returns> Returns a safe reference to the component enabled state.
        /// If the component doesn't exist, returns a default ComponentEnabledRefRO.</returns>
        public EnabledRefRO<T2> GetComponentEnabledRefROOptional<T2>(Entity entity)
            where T2 : unmanaged, IComponentData, IEnableableComponent
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            if (!HasComponent(entity))
                return new EnabledRefRO<T2>(default);

            var ecs = m_Access->EntityComponentStore;
            ecs->AssertEntityHasComponent(entity, m_TypeIndex, ref m_Cache);
            int indexInBitField;
            var ptr = ecs->GetEnabledRawRO(entity, m_TypeIndex, ref m_Cache, out indexInBitField, out _);
            return new EnabledRefRO<T2>(MakeSafeBitRef(ptr, indexInBitField));
        }
    }
}
