using System;
using Unity.Collections;

namespace Unity.Entities
{
    /// <summary>
    /// Provides an interface to safely perform <see cref="EntityManager"/> operations from a single worker thread, by temporarily
    /// giving that thread exclusive access to a <see cref="World"/>'s <see cref="EntityManager"/>.
    /// </summary>
    /// <remarks>The intended use case for this feature is to let a worker thread stream Entity data into a staging <see cref="World"/>
    /// and perform any necessary post-processing structural changes, prior to moving the final data into the main simulation world
    /// using <see cref="EntityManager.MoveEntitiesFrom"/>. This lets the main thread continue to safely process the primary world
    /// while the new data is loading in, and not require a main-thread sync point until the new Entities are fully loaded
    /// and ready to be injected.</remarks>
    /// <seealso cref="EntityManager.BeginExclusiveEntityTransaction"/>
    /// <seealso cref="EntityManager.EndExclusiveEntityTransaction"/>
    public unsafe struct ExclusiveEntityTransaction
    {
        private EntityManager m_Manager;

        /// <summary>
        /// Return the entity manager this transaction operates upon
        /// </summary>
        public EntityManager EntityManager => m_Manager;

        internal ExclusiveEntityTransaction(EntityManager manager)
        {
            m_Manager = manager;
        }

        internal EntityArchetype CreateArchetype(ComponentType* types, int count)
        {
            return m_Manager.CreateArchetype(types, count);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.CreateArchetype(ComponentType[])"/>
        public EntityArchetype CreateArchetype(params ComponentType[] types)
        {
            return m_Manager.CreateArchetype(types);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.CreateEntity(EntityArchetype)"/>
        public Entity CreateEntity(EntityArchetype archetype)
        {
            return m_Manager.CreateEntity(archetype);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.CreateEntity(EntityArchetype,NativeArray{Entity})"/>
        public void CreateEntity(EntityArchetype archetype, NativeArray<Entity> entities)
        {
            m_Manager.CreateEntity(archetype, entities);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.CreateEntity(ComponentType[])"/>
        public Entity CreateEntity(params ComponentType[] types)
        {
            return m_Manager.CreateEntity(types);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.Instantiate(Entity)"/>
        public Entity Instantiate(Entity srcEntity)
        {
            return m_Manager.Instantiate(srcEntity);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.Instantiate(Entity,NativeArray{Entity})"/>
        public void Instantiate(Entity srcEntity, NativeArray<Entity> outputEntities)
        {
            m_Manager.Instantiate(srcEntity, outputEntities);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.DestroyEntity(NativeArray{Entity})"/>
        public void DestroyEntity(NativeArray<Entity> entities)
        {
            m_Manager.DestroyEntity(entities);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.DestroyEntity(NativeSlice{Entity})"/>
        public void DestroyEntity(NativeSlice<Entity> entities)
        {
            m_Manager.DestroyEntity(entities);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.DestroyEntity(Entity)"/>
        public void DestroyEntity(Entity entity)
        {
            m_Manager.DestroyEntity(entity);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.AddComponent(Entity,ComponentType)"/>
        public void AddComponent(Entity entity, ComponentType componentType)
        {
            m_Manager.AddComponent(entity, componentType);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.AddBuffer{T}(Entity)"/>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleBufferElement) })]
        public DynamicBuffer<T> AddBuffer<T>(Entity entity) where T : unmanaged, IBufferElementData
        {
            return m_Manager.AddBuffer<T>(entity);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.HasBuffer{T}(Entity)"/>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleBufferElement) })]
        public bool HasBuffer<T>(Entity entity) where T : struct, IBufferElementData
        {
            return m_Manager.HasBuffer<T>(entity);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.RemoveComponent(Entity,ComponentType)"/>
        public void RemoveComponent(Entity entity, ComponentType type)
        {
            m_Manager.RemoveComponent(entity, type);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.Exists(Entity)"/>
        public bool Exists(Entity entity)
        {
            return m_Manager.Exists(entity);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.HasComponent(Entity,ComponentType)"/>
        public bool HasComponent(Entity entity, ComponentType type)
        {
            return m_Manager.HasComponent(entity, type);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.GetComponentData{T}(Entity)"/>
        public T GetComponentData<T>(Entity entity) where T : unmanaged, IComponentData
        {
            return m_Manager.GetComponentData<T>(entity);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.SetComponentData{T}(Entity,T)"/>
        public void SetComponentData<T>(Entity entity, T componentData) where T : unmanaged, IComponentData
        {
            m_Manager.SetComponentData(entity, componentData);
        }

        /// <summary> Obsolete. Use <see cref="GetSharedComponentManaged{T}"/> instead.</summary>
        /// <param name="entity">The entity.</param>
        /// <typeparam name="T">The type of entity</typeparam>
        /// <returns></returns>
        [Obsolete("Use GetSharedComponentManaged<T> (UnityUpgradable) -> GetSharedComponentManaged<T>(*)", true)]
        public T GetSharedComponentData<T>(Entity entity) where T : struct, ISharedComponentData
        {
            return default;
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.GetSharedComponentManaged{T}(Entity)"/>
        public T GetSharedComponentManaged<T>(Entity entity) where T : struct, ISharedComponentData
        {
            return m_Manager.GetSharedComponentManaged<T>(entity);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.GetSharedComponent{T}(Entity)"/>
        public T GetSharedComponent<T>(Entity entity) where T : unmanaged, ISharedComponentData
        {
            return m_Manager.GetSharedComponent<T>(entity);
        }

        /// <summary> Obsolete. Use <see cref="SetSharedComponentManaged{T}"/> instead.</summary>
        /// <param name="entity">The entity.</param>
        /// <param name="componentData">The data to set.</param>
        /// <typeparam name="T">The component type.</typeparam>
        [Obsolete("Use SetSharedComponentManaged<T> (UnityUpgradable) -> SetSharedComponentManaged<T>(*)", true)]
        public void SetSharedComponentData<T>(Entity entity, T componentData) where T : struct, ISharedComponentData
        {
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.SetSharedComponentManaged{T}(Entity,T)"/>
        public void SetSharedComponentManaged<T>(Entity entity, T componentData) where T : struct, ISharedComponentData
        {
            m_Manager.SetSharedComponentManaged(entity, componentData);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.SetSharedComponentManaged{T}(NativeArray{Entity},T)"/>
        public void SetSharedComponentManaged<T>(NativeArray<Entity> entities, T componentData) where T : struct, ISharedComponentData
        {
            m_Manager.SetSharedComponentManaged(entities, componentData);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.SetSharedComponent{T}(Entity,T)"/>
        public void SetSharedComponent<T>(Entity entity, T componentData) where T : unmanaged, ISharedComponentData
        {
            m_Manager.SetSharedComponent(entity, componentData);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.SetSharedComponent{T}(NativeArray{Entity},T)"/>
        public void SetSharedComponent<T>(NativeArray<Entity> entities, T componentData) where T : unmanaged, ISharedComponentData
        {
            m_Manager.SetSharedComponent(entities, componentData);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.AddSharedComponentManaged{T}(NativeArray{ArchetypeChunk},T)"/>
        internal void AddSharedComponentManaged<T>(NativeArray<ArchetypeChunk> chunks, T componentData)
            where T : struct, ISharedComponentData
        {
            m_Manager.AddSharedComponentManaged(chunks, componentData);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.AddSharedComponent{T}(NativeArray{ArchetypeChunk},T)"/>
        internal void AddSharedComponent<T>(NativeArray<ArchetypeChunk> chunks, T componentData)
            where T : unmanaged, ISharedComponentData
        {
            m_Manager.AddSharedComponent(chunks, componentData);
        }

        /// <summary> Obsolete. Use <see cref="AddSharedComponentManaged{T}(Entity,T)"/> instead.</summary>
        /// <param name="entity">The entity.</param>
        /// <param name="componentData">The shared component value to set.</param>
        /// <typeparam name="T">The shared component type.</typeparam>
        /// <returns>Returns false</returns>
        [Obsolete("Use AddSharedComponentManaged<T> (UnityUpgradable) -> AddSharedComponentManaged<T>(*)", true)]
        public bool AddSharedComponentData<T>(Entity entity, T componentData) where T : struct, ISharedComponentData
        {
            return false;
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.AddSharedComponentManaged{T}(Entity,T)"/>
        public bool AddSharedComponentManaged<T>(Entity entity, T componentData)  where T : struct, ISharedComponentData
        {
            return m_Manager.AddSharedComponentManaged(entity, componentData);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.AddSharedComponentManaged{T}(NativeArray{Entity},T)"/>
        public void AddSharedComponentManaged<T>(NativeArray<Entity> entities, T componentData)  where T : struct, ISharedComponentData
        {
            m_Manager.AddSharedComponentManaged(entities, componentData);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.AddSharedComponent{T}(Entity,T)"/>
        public bool AddSharedComponent<T>(Entity entity, T componentData)  where T : unmanaged, ISharedComponentData
        {
            return m_Manager.AddSharedComponent(entity, componentData);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.AddSharedComponent{T}(NativeArray{Entity},T)"/>
        public void AddSharedComponent<T>(NativeArray<Entity> entities, T componentData)  where T : unmanaged, ISharedComponentData
        {
            m_Manager.AddSharedComponent(entities, componentData);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.GetBuffer{T}(Entity,bool)"/>
        public DynamicBuffer<T> GetBuffer<T>(Entity entity, bool isReadOnly = false) where T : unmanaged, IBufferElementData
        {
            return m_Manager.GetBuffer<T>(entity, isReadOnly);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.SwapComponents(ArchetypeChunk,int,ArchetypeChunk,int)"/>
        public void SwapComponents(ArchetypeChunk leftChunk, int leftIndex, ArchetypeChunk rightChunk, int rightIndex)
        {
            m_Manager.SwapComponents(leftChunk, leftIndex, rightChunk, rightIndex);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.SetComponentEnabled(Entity,ComponentType,bool)"/>
        public void SetComponentEnabled(Entity entity, ComponentType componentType, bool value)
        {
            m_Manager.SetComponentEnabled(entity, componentType, value);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.SetComponentEnabled{T}(Entity,bool)"/>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleEnableableComponent) })]
        public void SetComponentEnabled<T>(Entity entity, bool value)  where T:
#if UNITY_DISABLE_MANAGED_COMPONENTS
            unmanaged,
#endif
            IEnableableComponent
        {
            m_Manager.SetComponentEnabled<T>(entity,value);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.IsComponentEnabled(Entity,ComponentType)"/>
        public bool IsComponentEnabled(Entity entity, ComponentType componentType)
        {
           return  m_Manager.IsComponentEnabled(entity, componentType);
        }

        /// <inheritdoc cref="Unity.Entities.EntityManager.IsComponentEnabled{T}(Entity)"/>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleEnableableComponent) })]
        public bool IsComponentEnabled<T>(Entity entity)where T:
#if UNITY_DISABLE_MANAGED_COMPONENTS
            unmanaged,
#endif
            IEnableableComponent
        {
            return m_Manager.IsComponentEnabled<T>(entity);
        }

#if ENTITY_STORE_V1
        internal void AllocateConsecutiveEntitiesForLoading(int count)
        {
            m_Manager.AllocateConsecutiveEntitiesForLoading(count);
        }
#endif
    }
}
