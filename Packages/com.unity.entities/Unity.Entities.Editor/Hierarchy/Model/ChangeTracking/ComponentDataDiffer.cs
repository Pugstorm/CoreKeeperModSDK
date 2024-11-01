using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// The <see cref="ComponentDataDiffer"/> is use to efficiently track changes for a given <see cref="IComponentData"/> type over time.
    /// </summary>
    unsafe class ComponentDataDiffer : IDisposable
    {
        /// <summary>
        /// The structure returned when gathering changes. This can be used to unpack all changes since the last diff.
        /// </summary>
        public readonly unsafe struct ChangeSet : IDisposable
        {
            internal struct Data
            {
                public UnsafeList<byte> Buffer;
                public UnsafeList<Entity> AddedComponents;
                public UnsafeList<Entity> RemovedComponents;
            }

            readonly TypeIndex m_TypeIndex;
            readonly Allocator m_Allocator;

            [field: NativeDisableUnsafePtrRestriction]
            internal Data* Ptr { get; }

            /// <summary>
            /// The number of add component records in this change set.
            /// </summary>
            public int AddedComponentCount => Ptr->AddedComponents.IsCreated ? Ptr->AddedComponents.Length : 0;

            /// <summary>
            /// The number of remove component records in this change set.
            /// </summary>
            public int RemovedComponentCount => Ptr->RemovedComponents.IsCreated ? Ptr->RemovedComponents.Length : 0;

            /// <summary>
            /// Constructs a new instance of <see cref="ChangeSet"/>.
            /// </summary>
            /// <param name="typeIndex">The type index this change-set contains changes for.</param>
            /// <param name="allocator">The allocator type to use when constructing this instance.</param>
            public ChangeSet(TypeIndex typeIndex, Allocator allocator)
            {
                m_TypeIndex = typeIndex;
                m_Allocator = allocator;
                Ptr = (Data*) Memory.Unmanaged.Allocate(UnsafeUtility.SizeOf<Data>(), UnsafeUtility.AlignOf<Data>(), m_Allocator);
                UnsafeUtility.MemClear(Ptr, UnsafeUtility.SizeOf<Data>());
            }

            public void Dispose()
            {
                if (null == Ptr) return;

                Ptr->Buffer.Dispose();
                Ptr->AddedComponents.Dispose();
                Ptr->RemovedComponents.Dispose();

                if (CollectionHelper.ShouldDeallocate(m_Allocator)) Memory.Unmanaged.Free(Ptr, m_Allocator);
            }

            public void GetAddedComponentEntities(NativeList<Entity> entities)
            {
                if (!Ptr->Buffer.IsCreated) return;
                var addedComponents = Ptr->AddedComponents;
                entities.ResizeUninitialized(addedComponents.Length);
                UnsafeUtility.MemCpy(entities.GetUnsafePtr(), addedComponents.Ptr, addedComponents.Length * UnsafeUtility.SizeOf<Entity>());
            }

            public void GetRemovedComponentEntities(NativeList<Entity> entities)
            {
                if (!Ptr->Buffer.IsCreated) return;
                var removedComponents = Ptr->RemovedComponents;
                entities.ResizeUninitialized(removedComponents.Length);
                UnsafeUtility.MemCpy(entities.GetUnsafePtr(), removedComponents.Ptr, removedComponents.Length * UnsafeUtility.SizeOf<Entity>());
            }

            public void GetAddedComponentData<T>(NativeList<T> components) where T : unmanaged
            {
                EnsureIsExpectedComponent<T>();

                if (!Ptr->Buffer.IsCreated) return;
                var addedComponents = Ptr->AddedComponents;
                components.ResizeUninitialized(addedComponents.Length);
                UnsafeUtility.MemCpy(components.GetUnsafePtr(), Ptr->Buffer.Ptr, addedComponents.Length * UnsafeUtility.SizeOf<T>());
            }

            public void GetRemovedComponentData<T>(NativeList<T> components) where T : unmanaged
            {
                EnsureIsExpectedComponent<T>();

                if (!Ptr->Buffer.IsCreated) return;

                var removedComponents = Ptr->RemovedComponents;
                components.ResizeUninitialized(removedComponents.Length);
                UnsafeUtility.MemCpy(components.GetUnsafePtr(), (byte*)Ptr->Buffer.Ptr + Ptr->AddedComponents.Length * UnsafeUtility.SizeOf<T>(), removedComponents.Length * UnsafeUtility.SizeOf<T>());
            }

            public (NativeArray<Entity> entities, NativeArray<T> componentData) GetAddedComponents<T>(Allocator allocator) where T : unmanaged
            {
                EnsureIsExpectedComponent<T>();

                if (!Ptr->Buffer.IsCreated)
                    return (new NativeArray<Entity>(0, allocator), new NativeArray<T>(0, allocator));

                var addedComponents = Ptr->AddedComponents;
                var entities = CollectionHelper.CreateNativeArray<Entity>(addedComponents.Length, allocator);
                var components = CollectionHelper.CreateNativeArray<T>(addedComponents.Length, allocator);
                UnsafeUtility.MemCpy(entities.GetUnsafePtr(), addedComponents.Ptr, addedComponents.Length * UnsafeUtility.SizeOf<Entity>());
                UnsafeUtility.MemCpy(components.GetUnsafePtr(), Ptr->Buffer.Ptr, addedComponents.Length * UnsafeUtility.SizeOf<T>());

                return (entities, components);
            }

            public NativeArray<Entity> GetEntitiesWithRemovedComponents<T>(Allocator allocator) where T : struct
            {
                EnsureIsExpectedComponent<T>();

                if (!Ptr->Buffer.IsCreated)
                    return new NativeArray<Entity>(0, allocator);

                var removedComponents = Ptr->RemovedComponents;
                var entities = CollectionHelper.CreateNativeArray<Entity>(removedComponents.Length, allocator);
                UnsafeUtility.MemCpy(entities.GetUnsafePtr(), removedComponents.Ptr, removedComponents.Length * UnsafeUtility.SizeOf<Entity>());

                return entities;
            }

            public (NativeArray<Entity> entities, NativeArray<T> componentData) GetRemovedComponents<T>(Allocator allocator) where T : unmanaged
            {
                EnsureIsExpectedComponent<T>();

                if (!Ptr->Buffer.IsCreated)
                    return (new NativeArray<Entity>(0, allocator), new NativeArray<T>(0, allocator));

                var removedComponents = Ptr->RemovedComponents;
                var entities = CollectionHelper.CreateNativeArray<Entity>(removedComponents.Length, allocator);
                var components = CollectionHelper.CreateNativeArray<T>(removedComponents.Length, allocator);
                UnsafeUtility.MemCpy(entities.GetUnsafePtr(), removedComponents.Ptr, removedComponents.Length * UnsafeUtility.SizeOf<Entity>());
                UnsafeUtility.MemCpy(components.GetUnsafePtr(), (byte*)Ptr->Buffer.Ptr + Ptr->AddedComponents.Length * UnsafeUtility.SizeOf<T>(), removedComponents.Length * UnsafeUtility.SizeOf<T>());

                return (entities, components);
            }

            void EnsureIsExpectedComponent<T>() where T : struct
            {
                if (TypeManager.GetTypeIndex<T>() != m_TypeIndex)
                    throw new InvalidOperationException($"Unable to retrieve data for component type {typeof(T)} (type index {TypeManager.GetTypeIndex<T>()}), this container only holds data for the type with type index {m_TypeIndex}.");
            }
        }

        /// <summary>
        /// A chunk shadow is used to retain a copy of all <see cref="Entity"/> data with the tracked component type. This is compared against the current state of the world to detect which data and therefore entities have changed.
        /// </summary>
        readonly unsafe struct ChunkShadow : IDisposable
        {
            internal struct Data
            {
                /// <summary>
                /// The number of entities the tracked chunk has during the last diff.
                /// </summary>
                public int Count;

                /// <summary>
                /// The component version for the tracked chunk during the last diff.
                /// </summary>
                public uint ComponentVersion;

                /// <summary>
                /// The entity version for the tracked chunk during the last diff.
                /// </summary>
                public uint EntityVersion;

                /// <summary>
                /// A copy of all entities in this chunk.
                /// </summary>
                public byte* Entities;

                /// <summary>
                /// A copy of the tracked component data in this chunk.
                /// </summary>
                public byte* Components;

                /// <summary>
                /// The sequence number of the tracked chunk.
                /// </summary>
                public ulong SequenceNumber;

                /// <summary>
                /// The index this of this chunk in the internal <see cref="ComponentDataDiffer.m_ChunkShadowBySequenceNumberKeys"/> list.
                /// </summary>
                public int Index;
            }

            readonly Allocator m_Allocator;

            [field: NativeDisableUnsafePtrRestriction]
            internal Data* Ptr { get; }

            public bool IsCreated => null != Ptr;

            public ChunkShadow(Allocator allocator)
            {
                m_Allocator = allocator;
                Ptr = (Data*) Memory.Unmanaged.Allocate(UnsafeUtility.SizeOf<Data>(), UnsafeUtility.AlignOf<Data>(), m_Allocator);
                UnsafeUtility.MemClear(Ptr, UnsafeUtility.SizeOf<Data>());
            }

            public void Dispose()
            {
                if (null != Ptr->Entities) Memory.Unmanaged.Free(Ptr->Entities, Allocator.Persistent);
                if (null != Ptr->Components) Memory.Unmanaged.Free(Ptr->Components, Allocator.Persistent);
                if (CollectionHelper.ShouldDeallocate(m_Allocator)) Memory.Unmanaged.Free(Ptr, m_Allocator);
            }
        }

        /// <summary>
        /// A set of changes for a given <see cref="Chunk"/>. This is an internal data structure used during the diffing process.
        /// </summary>
        unsafe struct ChunkChanges : IDisposable
        {
            public ChunkIndex Chunk;
            public ChunkShadow Shadow;
            public UnsafeList<byte> AddedComponentData;
            public UnsafeList<byte> RemovedComponentData;
            public UnsafeList<Entity> AddedEntities;
            public UnsafeList<Entity> RemovedEntities;

            public void Dispose()
            {
                Chunk = ChunkIndex.Null;
                Shadow = default;
                AddedComponentData.Dispose();
                RemovedComponentData.Dispose();
                AddedEntities.Dispose();
                RemovedEntities.Dispose();
            }
        }

        readonly EntityComponentStore* m_EntityComponentStore;

        /// <summary>
        /// The type index for the tracked component.
        /// </summary>
        readonly TypeIndex m_TypeIndex;

        /// <summary>
        /// The component size of the tracked component.
        /// </summary>
        readonly int m_ComponentSize;

        /// <summary>
        /// This map stores a <see cref="ChunkShadow"/> per chunk in the world we are tracking. NOTE: This only considered chunks which match our original input query.
        /// </summary>
        NativeParallelHashMap<ulong, ChunkShadow> m_ChunkShadowBySequenceNumber;

        /// <summary>
        /// This list stores a cached version of the <see cref="m_ChunkShadowBySequenceNumber"/> keys. This is incrementally updated an used to accelerate lookups and avoid a rebuild each diff.
        /// </summary>
        NativeList<ulong> m_ChunkShadowBySequenceNumberKeys;

        /// <summary>
        /// The free indices for <see cref="m_ChunkShadowBySequenceNumberKeys"/> in the <see cref="m_ChunkShadowBySequenceNumberKeys"/> for recently destroyed shadow chunks.
        /// </summary>
        NativeList<int> m_ChunkShadowBySequenceNumberKeysFreeList;

        /// <summary>
        /// The buffer use to track all live chunks in the world during the diff.
        /// </summary>
        NativeParallelHashSet<ulong> m_ChunkSequenceNumbers;

        /// <summary>
        /// The buffer used when allocating chunk shadows within a diff tick. This is a sparse array sized to the total chunks being processed.
        /// </summary>
        NativeList<ChunkShadow> m_AllocatedChunkShadowByChunk;

        /// <summary>
        /// The buffer used when gathering chunk changes for the diff. This is sparse array sized to the total chunks being processed.
        /// </summary>
        NativeList<ChunkChanges> m_ChangesByChunk;

        /// <summary>
        /// The buffer used to gather removed chunks for the diff.
        /// </summary>
        NativeList<ChunkShadow> m_RemovedChunks;

        /// <summary>
        /// The buffer used to gather removed entities from within the removed chunks.
        /// </summary>
        NativeList<Entity> m_RemovedEntities;

        /// <summary>
        /// The buffer used to gather removed component data from within the removed chunks.
        /// </summary>
        NativeList<byte> m_RemovedComponents;

        /// <summary>
        /// Returns true if the given <see cref="componentType"/> can be watched by a <see cref="ComponentDataDiffer"/>.
        /// </summary>
        /// <param name="componentType">The component type to check</param>
        /// <returns><see langword="true"/> if the specified component type can be watched. <see langword="false"/> otherwise.</returns>
        public static bool CanWatch(ComponentType componentType)
        {
            if (!TypeManager.IsInitialized)
                throw new InvalidOperationException($"{nameof(TypeManager)} has not been initialized properly");

            var typeInfo = TypeManager.GetTypeInfo(componentType.TypeIndex);
            return typeInfo.Category == TypeManager.TypeCategory.ComponentData && UnsafeUtility.IsUnmanaged(componentType.GetManagedType());
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ComponentDataDiffer"/>.
        /// </summary>
        /// <param name="componentType">The component type to diff.</param>
        /// <exception cref="ArgumentException">The specified component can not be watched by the differ.</exception>
        public ComponentDataDiffer(EntityComponentStore* ecs, ComponentType componentType)
        {
            if (!CanWatch(componentType))
                throw new ArgumentException($"{nameof(ComponentDataDiffer)} only supports unmanaged {nameof(IComponentData)} components.", nameof(componentType));

            var typeInfo = TypeManager.GetTypeInfo(componentType.TypeIndex);

            m_TypeIndex = typeInfo.TypeIndex;
            m_ComponentSize = typeInfo.SizeInChunk;

            m_ChunkShadowBySequenceNumber = new NativeParallelHashMap<ulong, ChunkShadow>(16, Allocator.Persistent);
            m_ChunkShadowBySequenceNumberKeys = new NativeList<ulong>(16, Allocator.Persistent);
            m_ChunkShadowBySequenceNumberKeysFreeList = new NativeList<int>(16, Allocator.Persistent);
            m_ChunkSequenceNumbers = new NativeParallelHashSet<ulong>(16, Allocator.Persistent);
            m_AllocatedChunkShadowByChunk = new NativeList<ChunkShadow>(16, Allocator.Persistent);
            m_ChangesByChunk = new NativeList<ChunkChanges>(16, Allocator.Persistent);
            m_RemovedChunks = new NativeList<ChunkShadow>(Allocator.Persistent);
            m_RemovedEntities = new NativeList<Entity>(Allocator.Persistent);
            m_RemovedComponents = new NativeList<byte>(Allocator.Persistent);
            m_EntityComponentStore = ecs;
        }

        public void Dispose()
        {
            using (var array = m_ChunkShadowBySequenceNumber.GetValueArray(Allocator.Temp))
                for (var i = 0; i < array.Length; i++)
                    array[i].Dispose();

            m_ChunkShadowBySequenceNumber.Dispose();
            m_ChunkShadowBySequenceNumberKeys.Dispose();
            m_ChunkShadowBySequenceNumberKeysFreeList.Dispose();
            m_ChunkSequenceNumbers.Dispose();
            m_AllocatedChunkShadowByChunk.Dispose();
            m_ChangesByChunk.Dispose();
            m_RemovedChunks.Dispose();
            m_RemovedEntities.Dispose();
            m_RemovedComponents.Dispose();
        }

        /// <summary>
        /// Returns a change set with all changes to the tracked component since the last time this method was called.
        /// </summary>
        /// <param name="query">The set of entities to track.</param>
        /// <param name="allocator">The allocator to use for the returned data.</param>
        /// <returns>A change set with all changes since the last call to this method.</returns>
        public ChangeSet GatherComponentChanges(EntityQuery query, Allocator allocator)
        {
            var changes = GatherComponentChangesAsync(query, allocator, out var jobHandle);
            jobHandle.Complete();
            return changes;
        }

        public unsafe ChangeSet GatherComponentChangesAsync(EntityQuery query, Allocator allocator, out JobHandle jobHandle)
        {
            var chunks = query.ToArchetypeChunkListAsync(Allocator.TempJob, out var chunksJobHandle);

            m_ChunkSequenceNumbers.Clear();
            m_RemovedChunks.Clear();
            m_RemovedEntities.Clear();
            m_RemovedComponents.Clear();

            // Can't read any of chunks fields on the main thread while these jobs are running, including length/capacity.
            int maxChunkCount = query.CalculateChunkCountWithoutFiltering();

            // These two lists must have the same length as the chunks list. Set their capacity conservatively on the main thread,
            // then shrink them to the appropriate size in a job.
            m_AllocatedChunkShadowByChunk.Capacity = math.max(m_AllocatedChunkShadowByChunk.Capacity, maxChunkCount);
            m_ChangesByChunk.Capacity = math.max(m_ChangesByChunk.Capacity, maxChunkCount);
            var resizeAndClearJobHandle = new ResizeAndClearChunkListsJob
            {
                Chunks = chunks,
                AllocatedChunkShadowByChunk = m_AllocatedChunkShadowByChunk,
                GatheredChanges = m_ChangesByChunk,
            }.Schedule(chunksJobHandle);

            m_ChunkSequenceNumbers.Capacity = math.max(m_ChunkSequenceNumbers.Capacity, maxChunkCount);
            m_ChunkShadowBySequenceNumber.Capacity = math.max(m_ChunkShadowBySequenceNumber.Capacity, maxChunkCount * 2);
            m_RemovedChunks.Capacity = math.max(m_RemovedChunks.Capacity, m_ChunkShadowBySequenceNumberKeys.Length);

            var changes = new ChangeSet(m_TypeIndex, allocator);

            var gatherEntityAndComponentChangesJobHandle = new GatherEntityAndComponentChangesJob
            {
                TypeIndex = m_TypeIndex,
                ComponentSize = m_ComponentSize,
                Chunks = chunks,
                ShadowChunksBySequenceNumber = m_ChunkShadowBySequenceNumber,
                GatheredChanges = m_ChangesByChunk.GetUnsafeList()->Ptr
            }.Schedule(chunks, 1, resizeAndClearJobHandle);

            var gatherExistingChunksJobHandle = new GatherExistingChunks
            {
                TypeIndex = m_TypeIndex,
                Chunks = chunks,
                ChunkSequenceNumbers = m_ChunkSequenceNumbers.AsParallelWriter()
            }.Schedule(chunks, 1, chunksJobHandle);

            var gatherRemovedChunksJobHandle = new GatherRemovedChunks
            {
                ChunkShadowBySequenceNumberKeys = m_ChunkShadowBySequenceNumberKeys,
                ChunkShadowBySequenceNumber = m_ChunkShadowBySequenceNumber,
                ChunkSequenceNumbers = m_ChunkSequenceNumbers,
                RemovedChunks = m_RemovedChunks.AsParallelWriter()
            }.Schedule(m_ChunkShadowBySequenceNumberKeys.Length, 1, gatherExistingChunksJobHandle);

            var gatherRemovedEntitiesAndComponentsJobHandle = new GatherRemovedEntitiesAndComponentsJob
            {
                ComponentSize = m_ComponentSize,
                RemoveChunks = m_RemovedChunks.AsDeferredJobArray(),
                RemovedEntities = m_RemovedEntities,
                RemovedComponents = m_RemovedComponents
            }.Schedule(gatherRemovedChunksJobHandle);

            var allocateChunkShadowsJobHandle = new AllocateChunkShadowsJob
            {
                TypeIndex = m_TypeIndex,
                ComponentSize = m_ComponentSize,
                Chunks = chunks,
                ChunkShadowBySequenceNumber = m_ChunkShadowBySequenceNumber,
                AllocatedChunkShadowByChunk = m_AllocatedChunkShadowByChunk.GetUnsafeList()
            }.Schedule(chunks, 1, resizeAndClearJobHandle);

            var buildChangeSetJobHandle = new BuildChangeSetJob
            {
                ComponentSize = m_ComponentSize,
                Allocator = allocator,
                GatheredChangesByChunk = m_ChangesByChunk.AsDeferredJobArray(),
                RemovedEntities = m_RemovedEntities.AsDeferredJobArray(),
                RemovedComponents = m_RemovedComponents.AsDeferredJobArray(),
                Result = changes
            }.Schedule(JobHandle.CombineDependencies(gatherEntityAndComponentChangesJobHandle, gatherRemovedEntitiesAndComponentsJobHandle, allocateChunkShadowsJobHandle));

            var updateShadowChunksJobHandle = new UpdateShadowChunksJob
            {
                TypeIndex = m_TypeIndex,
                Chunks = chunks,
                EntityComponentStore = m_EntityComponentStore,
                ComponentSize = m_ComponentSize,
                GatheredChangesByChunk = m_ChangesByChunk.AsDeferredJobArray(),
                AllocatedChunkShadowsByChunk = m_AllocatedChunkShadowByChunk.AsDeferredJobArray(),
                ChunkShadowBySequenceNumber = m_ChunkShadowBySequenceNumber.AsParallelWriter()
            }.Schedule(chunks, 1, JobHandle.CombineDependencies(gatherEntityAndComponentChangesJobHandle, gatherRemovedEntitiesAndComponentsJobHandle, allocateChunkShadowsJobHandle));

            var removeChunkShadowsJobHandle = new RemoveChunkShadowsJob
            {
                Chunks = chunks,
                AllocatedChunkShadowByChunk = m_AllocatedChunkShadowByChunk.AsDeferredJobArray(),
                RemovedChunks = m_RemovedChunks.AsDeferredJobArray(),
                ChunkShadowBySequenceNumber = m_ChunkShadowBySequenceNumber,
                ChunkShadowBySequenceNumberKeys = m_ChunkShadowBySequenceNumberKeys,
                ChunkShadowBySequenceNumberKeysFreeList = m_ChunkShadowBySequenceNumberKeysFreeList
            }.Schedule(updateShadowChunksJobHandle);

            var disposeChunksJobHandle = chunks.Dispose(JobHandle.CombineDependencies(buildChangeSetJobHandle, removeChunkShadowsJobHandle));

            var disposeGatheredChangesJobHandle = new DisposeGatheredChangesJob
            {
                GatheredChanges = m_ChangesByChunk,
            }.Schedule(m_ChangesByChunk, 1, buildChangeSetJobHandle);

            jobHandle = JobHandle.CombineDependencies(gatherRemovedChunksJobHandle, disposeChunksJobHandle, disposeGatheredChangesJobHandle);

            return changes;
        }

        [BurstCompile]
        unsafe struct ResizeAndClearChunkListsJob : IJob
        {
            [ReadOnly] public NativeList<ArchetypeChunk> Chunks;
            public NativeList<ChunkShadow> AllocatedChunkShadowByChunk;
            public NativeList<ChunkChanges> GatheredChanges;
            public void Execute()
            {
                AllocatedChunkShadowByChunk.Resize(Chunks.Length, NativeArrayOptions.UninitializedMemory);
                GatheredChanges.Resize(Chunks.Length, NativeArrayOptions.UninitializedMemory);
                UnsafeUtility.MemClear(AllocatedChunkShadowByChunk.GetUnsafePtr(), Chunks.Length * sizeof(ChunkShadow));
                UnsafeUtility.MemClear(GatheredChanges.GetUnsafePtr(), Chunks.Length * sizeof(ChunkChanges));
            }
        }

        [BurstCompile]
        unsafe struct GatherEntityAndComponentChangesJob : IJobParallelForDefer
        {
            public TypeIndex TypeIndex;
            public int ComponentSize;

            [ReadOnly] public NativeList<ArchetypeChunk> Chunks;
            [ReadOnly] public NativeParallelHashMap<ulong, ChunkShadow> ShadowChunksBySequenceNumber;
            [NativeDisableUnsafePtrRestriction] public ChunkChanges* GatheredChanges;

            public void Execute(int index)
            {
                var chunk = Chunks[index].m_Chunk;
                var archetype = Chunks[index].Archetype.Archetype;
                var indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(archetype, TypeIndex);

                if (indexInTypeArray == -1) return;

                if (ShadowChunksBySequenceNumber.TryGetValue(chunk.SequenceNumber, out var shadow))
                {
                    var componentVersion = archetype->Chunks.GetChangeVersion(indexInTypeArray, chunk.ListIndex);
                    var entityVersion = archetype->Chunks.GetChangeVersion(0, chunk.ListIndex);
                    if (!ChangeVersionUtility.DidChange(componentVersion, shadow.Ptr->ComponentVersion) &&
                        !ChangeVersionUtility.DidChange(entityVersion, shadow.Ptr->EntityVersion))
                        return;

                    var changesForChunk = GatheredChanges + index;
                    changesForChunk->Chunk = chunk;
                    changesForChunk->Shadow = shadow;

                    if (!changesForChunk->AddedEntities.IsCreated)
                    {
                        changesForChunk->AddedEntities = new UnsafeList<Entity>(0, Allocator.TempJob);
                        changesForChunk->AddedComponentData = new UnsafeList<byte>(0, Allocator.TempJob);
                        changesForChunk->RemovedEntities = new UnsafeList<Entity>(0, Allocator.TempJob);
                        changesForChunk->RemovedComponentData = new UnsafeList<byte>(0, Allocator.TempJob);
                    }

                    var entityDataPtr = chunk.Buffer + archetype->Offsets[0];
                    var componentDataPtr = chunk.Buffer + archetype->Offsets[indexInTypeArray];

                    var currentCount = chunk.Count;
                    var previousCount = shadow.Ptr->Count;

                    var i = 0;

                    for (; i < currentCount && i < previousCount; i++)
                    {
                        var currentComponentData = componentDataPtr + ComponentSize * i;
                        var previousComponentData = shadow.Ptr->Components + ComponentSize * i;

                        var entity = *(Entity*)(entityDataPtr + sizeof(Entity) * i);
                        var previousEntity = *(Entity*)(shadow.Ptr->Entities + sizeof(Entity) * i);

                        if (entity != previousEntity || UnsafeUtility.MemCmp(currentComponentData, previousComponentData, ComponentSize) != 0)
                        {
                            OnRemovedComponent(changesForChunk, previousEntity, previousComponentData, ComponentSize);
                            OnNewComponent(changesForChunk, entity, currentComponentData, ComponentSize);
                        }
                    }

                    for (; i < currentCount; i++)
                    {
                        var entity = *(Entity*)(entityDataPtr + sizeof(Entity) * i);
                        var currentComponentData = componentDataPtr + ComponentSize * i;
                        OnNewComponent(changesForChunk, entity, currentComponentData, ComponentSize);
                    }

                    for (; i < previousCount; i++)
                    {
                        var entity = *(Entity*)(shadow.Ptr->Entities + sizeof(Entity) * i);
                        var previousComponentData = shadow.Ptr->Components + ComponentSize * i;
                        OnRemovedComponent(changesForChunk, entity, previousComponentData, ComponentSize);
                    }
                }
                else
                {
                    var addedComponentDataBuffer = new UnsafeList<byte>(chunk.Count * ComponentSize, Allocator.TempJob);
                    var addedComponentEntities = new UnsafeList<Entity>(chunk.Count, Allocator.TempJob);

                    var entityDataPtr = chunk.Buffer + archetype->Offsets[0];
                    var componentDataPtr = chunk.Buffer + archetype->Offsets[indexInTypeArray];

                    addedComponentDataBuffer.AddRange(componentDataPtr, chunk.Count * ComponentSize);
                    addedComponentEntities.AddRange(entityDataPtr, chunk.Count);

                    var changesForChunk = GatheredChanges + index;
                    changesForChunk->Chunk = chunk;
                    changesForChunk->Shadow = default;
                    changesForChunk->AddedComponentData = addedComponentDataBuffer;
                    changesForChunk->AddedEntities = addedComponentEntities;
                }
            }

            static void OnNewComponent(ChunkChanges* changesForChunk, Entity entity, byte* currentComponentData, int componentSize)
            {
                changesForChunk->AddedEntities.Add(entity);
                changesForChunk->AddedComponentData.AddRange(currentComponentData, componentSize);
            }

            static void OnRemovedComponent(ChunkChanges* changesForChunk, Entity entity, byte* previousComponentData, int componentSize)
            {
                changesForChunk->RemovedEntities.Add(entity);
                changesForChunk->RemovedComponentData.AddRange(previousComponentData, componentSize);
            }
        }

        [BurstCompile]
        unsafe struct GatherExistingChunks : IJobParallelForDefer
        {
            public TypeIndex TypeIndex;

            [ReadOnly] public NativeList<ArchetypeChunk> Chunks;
            [WriteOnly] public NativeParallelHashSet<ulong>.ParallelWriter ChunkSequenceNumbers;

            public void Execute(int index)
            {
                var chunk = Chunks[index].m_Chunk;
                var archetype = Chunks[index].Archetype.Archetype;

                if (ChunkDataUtility.GetIndexInTypeArray(archetype, TypeIndex) == -1) // Archetype doesn't match required component
                    return;

                ChunkSequenceNumbers.Add(chunk.SequenceNumber);
            }
        }

        [BurstCompile]
        struct GatherRemovedChunks : IJobParallelFor
        {
            [ReadOnly] public NativeList<ulong> ChunkShadowBySequenceNumberKeys;
            [ReadOnly] public NativeParallelHashMap<ulong, ChunkShadow> ChunkShadowBySequenceNumber;
            [ReadOnly] public NativeParallelHashSet<ulong> ChunkSequenceNumbers;
            [WriteOnly] public NativeList<ChunkShadow>.ParallelWriter RemovedChunks;

            public void Execute(int index)
            {
                var chunkSequenceNumber = ChunkShadowBySequenceNumberKeys[index];
                if (chunkSequenceNumber == 0 || ChunkSequenceNumbers.Contains(chunkSequenceNumber)) return;
                var chunkShadow = ChunkShadowBySequenceNumber[chunkSequenceNumber];
                RemovedChunks.AddNoResize(chunkShadow);
            }
        }

        [BurstCompile]
        unsafe struct GatherRemovedEntitiesAndComponentsJob : IJob
        {
            public int ComponentSize;
            [ReadOnly] public NativeArray<ChunkShadow> RemoveChunks;
            [WriteOnly] public NativeList<Entity> RemovedEntities;
            [WriteOnly] public NativeList<byte> RemovedComponents;

            public void Execute()
            {
                for (var i = 0; i < RemoveChunks.Length; i++)
                {
                    RemovedEntities.AddRange(RemoveChunks[i].Ptr->Entities, RemoveChunks[i].Ptr->Count);
                    RemovedComponents.AddRange(RemoveChunks[i].Ptr->Components, RemoveChunks[i].Ptr->Count * ComponentSize);
                }
            }
        }

        [BurstCompile]
        unsafe struct AllocateChunkShadowsJob : IJobParallelForDefer
        {
            public TypeIndex TypeIndex;
            public int ComponentSize;
            [ReadOnly] public NativeList<ArchetypeChunk> Chunks;
            [ReadOnly] public NativeParallelHashMap<ulong, ChunkShadow> ChunkShadowBySequenceNumber;
            [NativeDisableUnsafePtrRestriction] public UnsafeList<ChunkShadow>* AllocatedChunkShadowByChunk;

            public void Execute(int index)
            {
                var chunk = Chunks[index].m_Chunk;
                var archetype = Chunks[index].Archetype.Archetype;
                var indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(archetype, TypeIndex);
                if (indexInTypeArray == -1) return;
                var capacity = archetype->ChunkCapacity;
                var sequenceNumber = chunk.SequenceNumber;

                if (ChunkShadowBySequenceNumber.TryGetValue(sequenceNumber, out var shadow))
                    return;

                var entityDataPtr = chunk.Buffer + archetype->Offsets[0];
                var componentDataPtr = chunk.Buffer + archetype->Offsets[indexInTypeArray];

                shadow = new ChunkShadow(Allocator.Persistent);

                var chunkCount = chunk.Count;
                shadow.Ptr->Count = chunkCount;
                shadow.Ptr->ComponentVersion = archetype->Chunks.GetChangeVersion(indexInTypeArray, chunk.ListIndex);
                shadow.Ptr->EntityVersion = archetype->Chunks.GetChangeVersion(0, chunk.ListIndex);
                shadow.Ptr->Entities = (byte*)Memory.Unmanaged.Allocate(sizeof(Entity) * capacity, 4, Allocator.Persistent);
                shadow.Ptr->Components = (byte*)Memory.Unmanaged.Allocate(ComponentSize * capacity, 4, Allocator.Persistent);
                shadow.Ptr->SequenceNumber = sequenceNumber;

                UnsafeUtility.MemCpy(shadow.Ptr->Entities, entityDataPtr, chunkCount * sizeof(Entity));
                UnsafeUtility.MemCpy(shadow.Ptr->Components, componentDataPtr, chunkCount * ComponentSize);

                AllocatedChunkShadowByChunk->ElementAt(index) = shadow;
            }
        }

        [BurstCompile]
        unsafe struct BuildChangeSetJob : IJob
        {
            public int ComponentSize;
            public Allocator Allocator;

            [ReadOnly] public NativeArray<ChunkChanges> GatheredChangesByChunk;

            [ReadOnly] public NativeArray<Entity> RemovedEntities;
            [ReadOnly] public NativeArray<byte> RemovedComponents;

            [NativeDisableUnsafePtrRestriction] public ChangeSet Result;

            public void Execute()
            {
                var addedEntityCount = 0;
                var removedEntityCount = RemovedEntities.Length;

                for (var i = 0; i < GatheredChangesByChunk.Length; i++)
                {
                    var changesForChunk = GatheredChangesByChunk[i];
                    addedEntityCount += changesForChunk.AddedEntities.Length;
                    removedEntityCount += changesForChunk.RemovedEntities.Length;
                }

                if (addedEntityCount == 0 && removedEntityCount == 0)
                    return;

                var buffer = new UnsafeList<byte>((addedEntityCount + removedEntityCount) * ComponentSize, Allocator);
                var addedComponents = new UnsafeList<Entity>(addedEntityCount, Allocator);
                var removedComponents = new UnsafeList<Entity>(removedEntityCount, Allocator);

                var chunksWithRemovedData = new NativeList<int>(GatheredChangesByChunk.Length, Allocator.Temp);

                for (var i = 0; i < GatheredChangesByChunk.Length; i++)
                {
                    var changesForChunk = GatheredChangesByChunk[i];

                    if (changesForChunk.AddedComponentData.IsCreated)
                    {
                        buffer.AddRangeNoResize(changesForChunk.AddedComponentData.Ptr, changesForChunk.AddedComponentData.Length);
                        addedComponents.AddRangeNoResize(changesForChunk.AddedEntities.Ptr, changesForChunk.AddedEntities.Length);
                    }

                    if (changesForChunk.RemovedComponentData.IsCreated)
                        chunksWithRemovedData.AddNoResize(i);
                }

                for (var i = 0; i < chunksWithRemovedData.Length; i++)
                {
                    var changesForChunk = GatheredChangesByChunk[chunksWithRemovedData[i]];
                    buffer.AddRangeNoResize(changesForChunk.RemovedComponentData.Ptr, changesForChunk.RemovedComponentData.Length);
                    removedComponents.AddRangeNoResize(changesForChunk.RemovedEntities.Ptr, changesForChunk.RemovedEntities.Length);
                }

                chunksWithRemovedData.Dispose();

                buffer.AddRangeNoResize(RemovedComponents.GetUnsafeReadOnlyPtr(), RemovedComponents.Length);
                removedComponents.AddRangeNoResize(RemovedEntities.GetUnsafeReadOnlyPtr(), RemovedEntities.Length);

                Result.Ptr->Buffer = buffer;
                Result.Ptr->AddedComponents = addedComponents;
                Result.Ptr->RemovedComponents = removedComponents;
            }
        }

        [BurstCompile]
        unsafe struct UpdateShadowChunksJob : IJobParallelForDefer
        {
            public TypeIndex TypeIndex;
            public int ComponentSize;

            [NativeDisableUnsafePtrRestriction][ReadOnly] public EntityComponentStore* EntityComponentStore;
            [ReadOnly] public NativeList<ArchetypeChunk> Chunks; // not used, but required to be here for IJobParallelForDefer
            [ReadOnly] public NativeArray<ChunkChanges> GatheredChangesByChunk;
            [ReadOnly] public NativeArray<ChunkShadow> AllocatedChunkShadowsByChunk;
            [WriteOnly] public NativeParallelHashMap<ulong, ChunkShadow>.ParallelWriter ChunkShadowBySequenceNumber;

            public void Execute(int index)
            {
                var changes = GatheredChangesByChunk[index];

                var chunk = changes.Chunk;

                if (ChunkIndex.Null == chunk)
                    return;

                if (!changes.Shadow.IsCreated)
                {
                    ChunkShadowBySequenceNumber.TryAdd(chunk.SequenceNumber, AllocatedChunkShadowsByChunk[index]);
                }
                else
                {
                    var archetype = EntityComponentStore->GetArchetype(chunk);
                    var indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(archetype, TypeIndex);

                    var buffer = chunk.Buffer;
                    var entities = buffer + archetype->Offsets[0];
                    var components = buffer + archetype->Offsets[indexInTypeArray];
                    var listIndex = chunk.ListIndex;

                    changes.Shadow.Ptr->Count = changes.Chunk.Count;
                    changes.Shadow.Ptr->EntityVersion = archetype->Chunks.GetChangeVersion(0, listIndex);
                    changes.Shadow.Ptr->ComponentVersion = archetype->Chunks.GetChangeVersion(indexInTypeArray, listIndex);

                    var entityCount = chunk.Count;

                    UnsafeUtility.MemCpy(changes.Shadow.Ptr->Entities, entities, entityCount * sizeof(Entity));
                    UnsafeUtility.MemCpy(changes.Shadow.Ptr->Components, components, entityCount * ComponentSize);
                }
            }
        }

        [BurstCompile]
        unsafe struct RemoveChunkShadowsJob : IJob
        {
            [ReadOnly] public NativeList<ArchetypeChunk> Chunks;
            [ReadOnly] public NativeArray<ChunkShadow> AllocatedChunkShadowByChunk;
            [ReadOnly] public NativeArray<ChunkShadow> RemovedChunks;
            public NativeParallelHashMap<ulong, ChunkShadow> ChunkShadowBySequenceNumber;
            public NativeList<ulong> ChunkShadowBySequenceNumberKeys;
            public NativeList<int> ChunkShadowBySequenceNumberKeysFreeList;

            public void Execute()
            {
                // AllocatedChunkShadowByChunk is conservatively sized; only the first Chunks.Length elements are valid.
                int chunkCount = Chunks.Length;
                for (var i=0; i<chunkCount; i++)
                {
                    var shadow = AllocatedChunkShadowByChunk[i];

                    if (!shadow.IsCreated)
                        continue;

                    if (ChunkShadowBySequenceNumberKeysFreeList.Length > 0)
                    {
                        shadow.Ptr->Index = ChunkShadowBySequenceNumberKeysFreeList[^1];
                        ChunkShadowBySequenceNumberKeysFreeList.Length--;
                        ChunkShadowBySequenceNumberKeys[shadow.Ptr->Index] = shadow.Ptr->SequenceNumber;
                    }
                    else
                    {
                        shadow.Ptr->Index = ChunkShadowBySequenceNumberKeys.Length;
                        ChunkShadowBySequenceNumberKeys.Add(shadow.Ptr->SequenceNumber);
                    }
                }

                for (var i = 0; i < RemovedChunks.Length; i++)
                {
                    ChunkShadowBySequenceNumber.Remove(RemovedChunks[i].Ptr->SequenceNumber);
                    ChunkShadowBySequenceNumberKeys[RemovedChunks[i].Ptr->Index] = 0;
                    ChunkShadowBySequenceNumberKeysFreeList.Add(RemovedChunks[i].Ptr->Index);
                    RemovedChunks[i].Dispose();
                }
            }
        }

        [BurstCompile]
        unsafe struct DisposeGatheredChangesJob : IJobParallelForDefer
        {
            [ReadOnly] public NativeList<ChunkChanges> GatheredChanges;

            public void Execute(int index)
            {
                GatheredChanges[index].Dispose();
            }
        }
    }
}
