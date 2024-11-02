using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// The <see cref="UnmanagedSharedComponentDataDiffer"/> is use to efficiently track changes for a given _unmanaged_ <see cref="ISharedComponentData"/> type over time.
    /// </summary>
    class UnmanagedSharedComponentDataDiffer : IDisposable
    {
        /// <summary>
        /// The structure returned when gathering changes. This can be used to unpack all changes since the last diff.
        /// </summary>
        public readonly unsafe struct ChangeSet : IDisposable
        {
            /// <summary>
            /// The backing data for the change set. This is a separate structure which is filled during job execution.
            /// </summary>
            /// <remarks>
            /// This packing is not perfectly optimized. In an ideal scenario we would have multiple chunks which use the same shared component to point to the same entry in the <see cref="SharedComponentData"/>.
            /// In practice what we get is each chunk making a single entry in the <see cref="SharedComponentData"/> which all entities from that chunk sharing it's data. This is done to speed up the diffing process itself.
            /// </remarks>
            internal struct Data
            {
                /// <summary>
                /// The set of all entities for which the tracked component was added in this diff.
                /// </summary>
                public UnsafeList<Entity> AddedComponentEntities;

                /// <summary>
                /// The set of all entities for which the tracked component was removed in this diff.
                /// </summary>
                public UnsafeList<Entity> RemovedComponentEntities;

                /// <summary>
                /// Index to the <see cref="SharedComponentData"/> for each <see cref="AddedComponentEntities"/>.
                /// </summary>
                /// <remarks>
                /// This is a squashed index and NOT the byte offset. To get the component data you must multiply the index by the component size and look in <see cref="SharedComponentData"/>.
                /// </remarks>
                public UnsafeList<int> AddedComponentIndices;

                /// <summary>
                /// Index to <see cref="SharedComponentData"/> for each <see cref="RemovedComponentEntities"/>.
                /// </summary>
                /// <remarks>
                /// This is a squashed index and NOT the byte offset. To get the component data you must multiply the index by the component size and look in <see cref="SharedComponentData"/>.
                /// </remarks>
                public UnsafeList<int> RemovedComponentIndices;

                /// <summary>
                /// The raw buffer containing all shared component data relevant to this change-set.
                /// </summary>
                public UnsafeList<byte> SharedComponentData;
            }

            readonly TypeIndex m_TypeIndex;
            readonly Allocator m_Allocator;

            [field: NativeDisableUnsafePtrRestriction]
            internal Data* Ptr { get; }

            /// <summary>
            /// The number of add component records in this change set.
            /// </summary>
            public int AddedSharedComponentCount => Ptr->AddedComponentEntities.Length;

            /// <summary>
            /// The number of remove component records in this change set.
            /// </summary>
            public int RemovedSharedComponentCount => Ptr->RemovedComponentEntities.Length;

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
                if (null == Ptr)
                    return;

                Ptr->AddedComponentEntities.Dispose();
                Ptr->RemovedComponentEntities.Dispose();
                Ptr->AddedComponentIndices.Dispose();
                Ptr->RemovedComponentIndices.Dispose();
                Ptr->SharedComponentData.Dispose();

                if (CollectionHelper.ShouldDeallocate(m_Allocator)) Memory.Unmanaged.Free(Ptr, m_Allocator);
            }

            public void GetAddedComponentEntities(NativeList<Entity> entities)
            {
                var addedComponentEntities = Ptr->AddedComponentEntities;
                entities.ResizeUninitialized(addedComponentEntities.Length);
                UnsafeUtility.MemCpy(entities.GetUnsafePtr(), addedComponentEntities.Ptr, addedComponentEntities.Length * UnsafeUtility.SizeOf<Entity>());
            }

            public void GetRemovedComponentEntities(NativeList<Entity> entities)
            {
                var removedComponentEntities = Ptr->RemovedComponentEntities;
                entities.ResizeUninitialized(removedComponentEntities.Length);
                UnsafeUtility.MemCpy(entities.GetUnsafePtr(), removedComponentEntities.Ptr, removedComponentEntities.Length * UnsafeUtility.SizeOf<Entity>());
            }

            public Entity GetAddedComponentEntity(int index)
            {
                return Ptr->AddedComponentEntities[index];
            }

            public Entity GetRemovedComponentEntity(int index)
            {
                return Ptr->RemovedComponentEntities[index];
            }

            public void GetAddedComponentData<T>(NativeList<T> components) where T : unmanaged, ISharedComponentData
            {
                var addedComponentIndices = Ptr->AddedComponentIndices;
                components.ResizeUninitialized(addedComponentIndices.Length);

                for (var i = 0; i < addedComponentIndices.Length; i++)
                {
                    UnsafeUtility.CopyPtrToStructure<T>(Ptr->SharedComponentData.Ptr + addedComponentIndices[i] * UnsafeUtility.SizeOf<T>(), out var value);
                    components[i] = value;
                }
            }

            public void GetRemovedComponentData<T>(NativeList<T> components) where T : unmanaged, ISharedComponentData
            {
                var removedComponentIndices = Ptr->RemovedComponentIndices;
                components.ResizeUninitialized(removedComponentIndices.Length);

                for (var i = 0; i < removedComponentIndices.Length; i++)
                {
                    UnsafeUtility.CopyPtrToStructure<T>(Ptr->SharedComponentData.Ptr + removedComponentIndices[i] * UnsafeUtility.SizeOf<T>(), out var value);
                    components[i] = value;
                }
            }

            public T GetAddedComponentData<T>(int index) where T : struct, ISharedComponentData
            {
                EnsureIsExpectedComponent<T>();
                UnsafeUtility.CopyPtrToStructure<T>(Ptr->SharedComponentData.Ptr + Ptr->AddedComponentIndices[index] * UnsafeUtility.SizeOf<T>(), out var value);
                return value;
            }

            public T GetRemovedComponentData<T>(int index) where T : struct, ISharedComponentData
            {
                EnsureIsExpectedComponent<T>();
                UnsafeUtility.CopyPtrToStructure<T>(Ptr->SharedComponentData.Ptr + Ptr->RemovedComponentIndices[index] * UnsafeUtility.SizeOf<T>(), out var value);
                return value;
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
                /// The entity version for the tracked chunk during the last diff.
                /// </summary>
                public uint Version;

                /// <summary>
                /// The shared component index (for our tracked component) and this specific chunk.
                /// </summary>
                public int SharedComponentElementIndex;

                /// <summary>
                /// A copy of all entities in this chunk.
                /// </summary>
                public Entity* Entities;

                /// <summary>
                /// The sequence number of the tracked chunk.
                /// </summary>
                public ulong SequenceNumber;

                /// <summary>
                /// The index this of this chunk in the internal <see cref="UnmanagedSharedComponentDataDiffer.m_ChunkShadowBySequenceNumberKeys"/> list.
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
                if (CollectionHelper.ShouldDeallocate(m_Allocator)) Memory.Unmanaged.Free(Ptr, m_Allocator);
            }
        }

        /// <summary>
        /// A set of changes for a given <see cref="Chunk"/>. This is an internal data structure used during the diffing process.
        /// </summary>
        unsafe struct ChunkChanges
        {
            public ChunkIndex Chunk;
            public ChunkShadow Shadow;
            public UnsafeList<Entity> AddedEntities;
            public UnsafeList<Entity> RemovedEntities;

            public void Dispose()
            {
                Chunk = ChunkIndex.Null;
                Shadow = default;
                AddedEntities.Dispose();
                RemovedEntities.Dispose();
            }
        }

        /// <summary>
        /// The type index for the tracked component.
        /// </summary>
        readonly TypeIndex m_TypeIndex;

        /// <summary>
        /// The component size of the tracked component.
        /// </summary>
        readonly int m_ComponentSize;

        /// <summary>
        /// This list stores a copy of ALL shared component data for the tracked type. This is used to keep track of previous values.
        /// </summary>
        NativeList<byte> m_ChunkShadowSharedComponentData;

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
        /// Initializes a new instance of <see cref="UnmanagedSharedComponentDataDiffer"/>.
        /// </summary>
        /// <param name="componentType">The component type to diff.</param>
        /// <exception cref="ArgumentException">The specified component can not be watched by the differ.</exception>
        public UnmanagedSharedComponentDataDiffer(ComponentType componentType)
        {
            if (!CanWatch(componentType))
                throw new ArgumentException($"{nameof(UnmanagedSharedComponentDataDiffer)} only supports unmanaged {nameof(ISharedComponentData)} components.", nameof(componentType));

            var typeInfo = TypeManager.GetTypeInfo(componentType.TypeIndex);

            m_TypeIndex = typeInfo.TypeIndex;
            m_ComponentSize = typeInfo.TypeSize;

            m_ChunkShadowSharedComponentData = new NativeList<byte>(16 * m_ComponentSize, Allocator.Persistent);
            m_ChunkShadowBySequenceNumber = new NativeParallelHashMap<ulong, ChunkShadow>(16, Allocator.Persistent);
            m_ChunkShadowBySequenceNumberKeys = new NativeList<ulong>(16, Allocator.Persistent);
            m_ChunkShadowBySequenceNumberKeysFreeList = new NativeList<int>(16, Allocator.Persistent);
            m_ChunkSequenceNumbers = new NativeParallelHashSet<ulong>(16, Allocator.Persistent);
            m_AllocatedChunkShadowByChunk = new NativeList<ChunkShadow>(16, Allocator.Persistent);
            m_ChangesByChunk = new NativeList<ChunkChanges>(16, Allocator.Persistent);
            m_RemovedChunks = new NativeList<ChunkShadow>(Allocator.Persistent);
            m_RemovedEntities = new NativeList<Entity>(Allocator.Persistent);
        }

        internal static bool CanWatch(ComponentType componentType)
        {
            if (!TypeManager.IsInitialized)
                throw new InvalidOperationException($"{nameof(TypeManager)} has not been initialized properly");

            var typeInfo = TypeManager.GetTypeInfo(componentType.TypeIndex);
            return typeInfo.Category == TypeManager.TypeCategory.ISharedComponentData && UnsafeUtility.IsUnmanaged(componentType.GetManagedType());
        }

        public void Dispose()
        {
            using (var chunks = m_ChunkShadowBySequenceNumber.GetValueArray(Allocator.Temp))
                for (var i = 0; i < chunks.Length; i++)
                    chunks[i].Dispose();

            m_ChunkShadowSharedComponentData.Dispose();
            m_ChunkShadowBySequenceNumber.Dispose();
            m_ChunkShadowBySequenceNumberKeys.Dispose();
            m_ChunkShadowBySequenceNumberKeysFreeList.Dispose();
            m_AllocatedChunkShadowByChunk.Dispose();
            m_ChunkSequenceNumbers.Dispose();
            m_ChangesByChunk.Dispose();
            m_RemovedChunks.Dispose();
            m_RemovedEntities.Dispose();
        }

        /// <summary>
        /// Returns a change set with all changes to the tracked component since the last time this method was called.
        /// </summary>
        /// <param name="entityManager">The world being tracked.</param>
        /// <param name="query">The set of entities to track.</param>
        /// <param name="allocator">The allocator to use for the returned data.</param>
        /// <returns>A change set with all changes since the last call to this method.</returns>
        public ChangeSet GatherComponentChanges(EntityManager entityManager, EntityQuery query, Allocator allocator)
        {
            var changes = GatherComponentChangesAsync(entityManager, query, allocator, out var jobHandle);
            jobHandle.Complete();
            return changes;
        }

        public unsafe ChangeSet GatherComponentChangesAsync(EntityManager entityManager, EntityQuery query, Allocator allocator, out JobHandle jobHandle)
        {
            var access = entityManager.GetCheckedEntityDataAccessExclusive();
            var chunks = query.ToArchetypeChunkListAsync(Allocator.TempJob, out var chunksJobHandle);

            m_ChunkSequenceNumbers.Clear();
            m_RemovedChunks.Clear();
            m_RemovedEntities.Clear();

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
            m_RemovedChunks.Capacity = math.max(m_RemovedChunks.Capacity, maxChunkCount);

            var changes = new ChangeSet(m_TypeIndex, allocator);

            // Scan all chunks which have our tracked component and gather all entities which have been created, destroyed or moved.
            // These are all changes which must be exported. This job will put this data in to a temporary storage 'GatheredChanges' which is
            // merged down later.
            var gatherEntityChangesJobHandle = new GatherEntityChangesJob
            {
                TypeIndex = m_TypeIndex,
                Chunks = chunks,
                ShadowChunksBySequenceNumber = m_ChunkShadowBySequenceNumber,
                GatheredChangesByChunk = m_ChangesByChunk.GetUnsafeList()->Ptr
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
            }.Schedule(m_ChunkShadowBySequenceNumberKeys, 1, gatherExistingChunksJobHandle);

            var gatherRemovedEntitiesJobHandle = new GatherRemovedEntitiesJob
            {
                RemoveChunks = m_RemovedChunks.AsDeferredJobArray(),
                RemovedEntities = m_RemovedEntities
            }.Schedule(gatherRemovedChunksJobHandle);

            var allocateNewShadowChunksJobHandle = new AllocateChunkShadowsJob
            {
                TypeIndex = m_TypeIndex,
                Chunks = chunks,
                ChunkShadowBySequenceNumber = m_ChunkShadowBySequenceNumber,
                AllocatedChunkShadowsByChunk = m_AllocatedChunkShadowByChunk.GetUnsafeList()
            }.Schedule(chunks, 1, resizeAndClearJobHandle);

            var buildChangeSetJobHandle = new BuildChangeSetJob
            {
                ECS = access->EntityComponentStore,
                TypeIndex = m_TypeIndex,
                ComponentSize = m_ComponentSize,
                Allocator = allocator,
                GatheredChangesByChunk = m_ChangesByChunk.AsDeferredJobArray(),
                RemovedChunks = m_RemovedChunks.AsDeferredJobArray(),
                RemovedEntities = m_RemovedEntities.AsDeferredJobArray(),
                ShadowChunkSharedComponentData = m_ChunkShadowSharedComponentData.GetUnsafeList(),
                SharedComponentDataByType = access->EntityComponentStore->m_UnmanagedSharedComponentsByType,
                Result = changes
            }.Schedule(JobHandle.CombineDependencies(gatherEntityChangesJobHandle, gatherRemovedEntitiesJobHandle, allocateNewShadowChunksJobHandle));

            var updateShadowChunksJobHandle = new UpdateShadowChunksJob
            {
                ECS = access->EntityComponentStore,
                Chunks = chunks,
                TypeIndex = m_TypeIndex,
                ChangesByChunk = m_ChangesByChunk.AsDeferredJobArray(),
                AllocatedChunkShadowsByChunk = m_AllocatedChunkShadowByChunk.AsDeferredJobArray(),
                ShadowChunksBySequenceNumber = m_ChunkShadowBySequenceNumber.AsParallelWriter()
            }.Schedule(chunks, 1, JobHandle.CombineDependencies(gatherEntityChangesJobHandle, gatherRemovedEntitiesJobHandle, allocateNewShadowChunksJobHandle));

            var removeShadowChunksJobHande = new RemoveChunkShadowsJob
            {
                AllocatedChunkShadowByChunk = m_AllocatedChunkShadowByChunk.AsDeferredJobArray(),
                RemovedChunks = m_RemovedChunks.AsDeferredJobArray(),
                ChunkShadowBySequenceNumber = m_ChunkShadowBySequenceNumber,
                ChunkShadowBySequenceNumberKeys = m_ChunkShadowBySequenceNumberKeys,
                ChunkShadowBySequenceNumberKeysFreeList = m_ChunkShadowBySequenceNumberKeysFreeList
            }.Schedule(JobHandle.CombineDependencies(buildChangeSetJobHandle, updateShadowChunksJobHandle));

            var copySharedComponentsJobHandle = new CopyStateToShadowSharedComponentDataJob
            {
                TypeIndex = m_TypeIndex,
                ComponentSize = m_ComponentSize,
                SharedComponentDataByType = access->EntityComponentStore->m_UnmanagedSharedComponentsByType,
                ShadowChunkSharedComponentData = m_ChunkShadowSharedComponentData.GetUnsafeList()
            }.Schedule(buildChangeSetJobHandle);

            var disposeChunksJobHandle = chunks.Dispose(JobHandle.CombineDependencies(copySharedComponentsJobHandle, removeShadowChunksJobHande));

            var disposeGatheredChangesJobHandle = new DisposeGatheredChangesJob
            {
                GatheredChanges = m_ChangesByChunk
            }.Schedule(m_ChangesByChunk, 1, buildChangeSetJobHandle);

            jobHandle = JobHandle.CombineDependencies(gatherRemovedChunksJobHandle, disposeChunksJobHandle, disposeGatheredChangesJobHandle);

            return changes;
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
        struct GatherRemovedChunks : IJobParallelForDefer
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
        unsafe struct GatherRemovedEntitiesJob : IJob
        {
            [ReadOnly] public NativeArray<ChunkShadow> RemoveChunks;
            [WriteOnly] public NativeList<Entity> RemovedEntities;

            public void Execute()
            {
                for (var i = 0; i < RemoveChunks.Length; i++)
                    RemovedEntities.AddRange(RemoveChunks[i].Ptr->Entities, RemoveChunks[i].Ptr->Count);
            }
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

        /// <summary>
        /// This job is used to gather all created, destroyed or moved entities by chunk.
        /// </summary>
        [BurstCompile]
        unsafe struct GatherEntityChangesJob : IJobParallelForDefer
        {
            public TypeIndex TypeIndex;

            [ReadOnly] public NativeList<ArchetypeChunk> Chunks;
            [ReadOnly] public NativeParallelHashMap<ulong, ChunkShadow> ShadowChunksBySequenceNumber;
            [NativeDisableUnsafePtrRestriction] public ChunkChanges* GatheredChangesByChunk;

            public void Execute(int index)
            {
                var chunk = Chunks[index].m_Chunk;
                var archetype = Chunks[index].Archetype.Archetype;
                var indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(archetype, TypeIndex);
                if (indexInTypeArray == -1) return;

                if (ShadowChunksBySequenceNumber.TryGetValue(chunk.SequenceNumber, out var shadow))
                {
                    var entityVersion = archetype->Chunks.GetChangeVersion(0, chunk.ListIndex);
                    if (!ChangeVersionUtility.DidChange(entityVersion, shadow.Ptr->Version))
                        return;

                    var changesForChunk = GatheredChangesByChunk + index;
                    changesForChunk->Chunk = chunk;
                    changesForChunk->Shadow = shadow;
                    changesForChunk->AddedEntities = new UnsafeList<Entity>(0, Allocator.TempJob);
                    changesForChunk->RemovedEntities = new UnsafeList<Entity>(0, Allocator.TempJob);

                    var entityDataPtr = (Entity*)(chunk.Buffer + archetype->Offsets[0]);
                    var currentCount = chunk.Count;
                    var previousCount = shadow.Ptr->Count;

                    var i = 0;
                    for (; i < currentCount && i < previousCount; i++)
                    {
                        var currentEntity = entityDataPtr[i];
                        var previousEntity = shadow.Ptr->Entities[i];

                        if (currentEntity != previousEntity)
                        {
                            changesForChunk->RemovedEntities.Add(previousEntity);
                            changesForChunk->AddedEntities.Add(currentEntity);
                        }
                    }

                    for (; i < currentCount; i++)
                        changesForChunk->AddedEntities.Add(entityDataPtr[i]);

                    for (; i < previousCount; i++)
                        changesForChunk->RemovedEntities.Add(shadow.Ptr->Entities[i]);
                }
                else
                {
                    // This is a new chunk
                    var addedEntities = new UnsafeList<Entity>(chunk.Count, Allocator.TempJob);
                    var entityDataPtr = chunk.Buffer + archetype->Offsets[0];
                    addedEntities.AddRange(entityDataPtr, chunk.Count);

                    var changesForChunk = GatheredChangesByChunk + index;
                    changesForChunk->Chunk = chunk;
                    changesForChunk->Shadow = default;
                    changesForChunk->AddedEntities = addedEntities;
                    changesForChunk->RemovedEntities = new UnsafeList<Entity>(0, Allocator.TempJob);
                }
            }
        }

        /// <summary>
        /// This job is used to allocate new chunk shadow objects which will hold a copy of data we are interested in.
        /// </summary>
        [BurstCompile]
        unsafe struct AllocateChunkShadowsJob : IJobParallelForDefer
        {
            public TypeIndex TypeIndex;
            [ReadOnly] public NativeList<ArchetypeChunk> Chunks;
            [ReadOnly] public NativeParallelHashMap<ulong, ChunkShadow> ChunkShadowBySequenceNumber;
            [NativeDisableUnsafePtrRestriction] public UnsafeList<ChunkShadow>* AllocatedChunkShadowsByChunk;

            public void Execute(int index)
            {
                var chunk = Chunks[index].m_Chunk;
                var archetype = Chunks[index].Archetype.Archetype;
                var indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(archetype, TypeIndex);
                if (indexInTypeArray == -1) return;
                var sequenceNumber = chunk.SequenceNumber;

                if (ChunkShadowBySequenceNumber.TryGetValue(sequenceNumber, out var shadow))
                    return;

                var sharedComponentValueArray = archetype->Chunks.GetSharedComponentValues(chunk.ListIndex);
                var sharedComponentOffset = indexInTypeArray - archetype->FirstSharedComponent;
                var sharedComponentDataIndex = sharedComponentValueArray[sharedComponentOffset];
                var elementIndex = EntityComponentStore.GetElementIndexFromSharedComponentIndex(sharedComponentDataIndex);

                var entityDataPtr = chunk.Buffer + archetype->Offsets[0];
                var chunkCount = chunk.Count;

                shadow = new ChunkShadow(Allocator.Persistent);
                shadow.Ptr->SequenceNumber = sequenceNumber;
                shadow.Ptr->Count = chunkCount;
                shadow.Ptr->Version = archetype->Chunks.GetChangeVersion(0, chunk.ListIndex);
                shadow.Ptr->SharedComponentElementIndex = elementIndex;
                shadow.Ptr->Entities = (Entity*)Memory.Unmanaged.Allocate(sizeof(Entity) * archetype->ChunkCapacity, 4, Allocator.Persistent);

                UnsafeUtility.MemCpy(shadow.Ptr->Entities, entityDataPtr, chunkCount * sizeof(Entity));
                AllocatedChunkShadowsByChunk->ElementAt(index) = shadow;
            }
        }

        [BurstCompile]
        unsafe struct BuildChangeSetJob : IJob
        {
            public TypeIndex TypeIndex;
            public int ComponentSize;
            public Allocator Allocator;

            [NativeDisableUnsafePtrRestriction] public EntityComponentStore* ECS;

            /// <summary>
            /// The set of changes gathered for this cycle.
            /// </summary>
            [ReadOnly] public NativeArray<ChunkChanges> GatheredChangesByChunk;
            [ReadOnly] public NativeArray<ChunkShadow> RemovedChunks;
            [ReadOnly] public NativeArray<Entity> RemovedEntities;

            [NativeDisableUnsafePtrRestriction] public UnsafeList<byte>* ShadowChunkSharedComponentData;

            /// <summary>
            /// The set of existing shared component data.
            /// </summary>
            public UnsafeList<ComponentTypeList> SharedComponentDataByType;

            [NativeDisableUnsafePtrRestriction] public ChangeSet Result;

            public void Execute()
            {
                var addedEntityCount = 0;
                var removedEntityCount = RemovedEntities.Length;
                var sharedComponentBufferCount = RemovedChunks.Length;

                for (var i = 0; i < GatheredChangesByChunk.Length; i++)
                {
                    var changesForChunk = GatheredChangesByChunk[i];

                    // Since we went wide earlier it's possible this chunk has no changes.
                    if (changesForChunk.AddedEntities.Length > 0)
                    {
                        addedEntityCount += changesForChunk.AddedEntities.Length;
                        sharedComponentBufferCount++;
                    }

                    // Since we went wide earlier it's possible this chunk has no changes.
                    if (changesForChunk.RemovedEntities.Length > 0)
                    {
                        removedEntityCount += changesForChunk.RemovedEntities.Length;
                        sharedComponentBufferCount++;
                    }
                }

                if (sharedComponentBufferCount == 0)
                    return;

                var sharedComponentDataBufferIndex = 0;
                var addedEntityCurrentCount = 0;
                var removedEntityCurrentCount = 0;

                var sharedComponentData = new UnsafeList<byte>(sharedComponentBufferCount * ComponentSize, Allocator);
                var addedComponentEntities = new UnsafeList<Entity>(addedEntityCount, Allocator);
                var addedComponentIndices = new UnsafeList<int>(addedEntityCount, Allocator);
                var removedComponentEntities = new UnsafeList<Entity>(removedEntityCount, Allocator);
                var removedComponentIndices = new UnsafeList<int>(removedEntityCount, Allocator);

                sharedComponentData.Length = sharedComponentBufferCount * ComponentSize;
                addedComponentEntities.Length = addedEntityCount;
                addedComponentIndices.Length = addedEntityCount;
                removedComponentEntities.Length = removedEntityCount;
                removedComponentIndices.Length = removedEntityCount;

                if (RemovedEntities.Length > 0)
                {
                    // We have already baked out a linear list of all entities that are removed via chunk destruction. We can simply copy this to the output.
                    UnsafeUtility.MemCpy(removedComponentEntities.Ptr, RemovedEntities.GetUnsafeReadOnlyPtr(), RemovedEntities.Length * sizeof(Entity));
                }

                // We now need to add mappings so that each removed entity references it's previous shared component value.
                // In this case `RemovedChunks` tells us the makeup of the entities in the `RemovedChunkEntities` list.
                for (var i = 0; i < RemovedChunks.Length; i++)
                {
                    var removedChunk = RemovedChunks[i];

                    // Fetch the last known shared component value for this chunk.
                    var shadowSharedComponentValueIndex = removedChunk.Ptr->SharedComponentElementIndex;
                    var shadowSharedComponentValuePtr = ShadowChunkSharedComponentData->Ptr + shadowSharedComponentValueIndex * ComponentSize;

                    // Add an entry to the sharedComponentDataBuffer for this chunk.
                    UnsafeUtility.MemCpy(sharedComponentData.Ptr + sharedComponentDataBufferIndex * ComponentSize, shadowSharedComponentValuePtr, ComponentSize);

                    // Add an index to this shared component buffer PER entity. NOTE. This __could__ be optimized by using some sort of chunk based data structure. But this is a simpler implementation.
                    UnsafeUtility.MemCpyReplicate(removedComponentIndices.Ptr + removedEntityCurrentCount, &sharedComponentDataBufferIndex, sizeof(int), removedChunk.Ptr->Count);

                    removedEntityCurrentCount += removedChunk.Ptr->Count;
                    sharedComponentDataBufferIndex++;
                }

                // The next step is to append any destroyed entities within the context of a chunk. That is an entity that was either moved or destroyed and but the chunk remains.
                for (var i = 0; i < GatheredChangesByChunk.Length; i++)
                {
                    var changesForChunk = GatheredChangesByChunk[i];

                    // Since we went wide earlier it's possible this chunk has no changes.
                    if (changesForChunk.RemovedEntities.Length == 0)
                        continue;

                    // Fetch the last known shared component value for this chunk.
                    var shadowSharedComponentValueIndex = changesForChunk.Shadow.Ptr->SharedComponentElementIndex;

                    if (shadowSharedComponentValueIndex != 0)
                    {
                        var shadowSharedComponentValuePtr = ShadowChunkSharedComponentData->Ptr + shadowSharedComponentValueIndex * ComponentSize;

                        // Add an entry to the sharedComponentDataBuffer for this chunk.
                        UnsafeUtility.MemCpy(sharedComponentData.Ptr + sharedComponentDataBufferIndex * ComponentSize, shadowSharedComponentValuePtr, ComponentSize);
                    }
                    else
                    {
                        UnsafeUtility.MemClear(sharedComponentData.Ptr + sharedComponentDataBufferIndex * ComponentSize, ComponentSize);
                    }

                    // Add the entity and lookup to the output buffer.
                    UnsafeUtility.MemCpy(removedComponentEntities.Ptr + removedEntityCurrentCount, changesForChunk.RemovedEntities.Ptr, changesForChunk.RemovedEntities.Length * sizeof(Entity));
                    UnsafeUtility.MemCpyReplicate(removedComponentIndices.Ptr + removedEntityCurrentCount, &sharedComponentDataBufferIndex, sizeof(int), changesForChunk.RemovedEntities.Length);

                    removedEntityCurrentCount += changesForChunk.RemovedEntities.Length;
                    sharedComponentDataBufferIndex++;
                }

                for (var i = 0; i < GatheredChangesByChunk.Length; i++)
                {
                    var changesForChunk = GatheredChangesByChunk[i];

                    // Since we went wide earlier it's possible this chunk has no changes.
                    if (changesForChunk.AddedEntities.Length == 0)
                        continue;

                    // Fetch the current shared component value for this chunk.
                    var archetype = ECS->GetArchetype(changesForChunk.Chunk);
                    var indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(archetype, TypeIndex);
                    var sharedComponentValueArray = archetype->Chunks.GetSharedComponentValues(changesForChunk.Chunk.ListIndex);
                    var sharedComponentOffset = indexInTypeArray - archetype->FirstSharedComponent;
                    var sharedComponentDataIndex = sharedComponentValueArray[sharedComponentOffset];
                    var elementIndex = EntityComponentStore.GetElementIndexFromSharedComponentIndex(sharedComponentDataIndex);

                    if (elementIndex != 0)
                    {
                        // Fetch the current shared component value for this chunk.
                        var sharedComponentValuePtr = (byte*) SharedComponentDataByType[TypeIndex.Index].Ptr + elementIndex * ComponentSize;

                        // Add an entry to the sharedComponentDataBuffer for this chunk.
                        UnsafeUtility.MemCpy(sharedComponentData.Ptr + sharedComponentDataBufferIndex * ComponentSize, sharedComponentValuePtr, ComponentSize);
                    }
                    else
                    {
                        UnsafeUtility.MemClear(sharedComponentData.Ptr + sharedComponentDataBufferIndex * ComponentSize, ComponentSize);
                    }

                    // Add the entity and lookup to the output buffer.
                    UnsafeUtility.MemCpy(addedComponentEntities.Ptr + addedEntityCurrentCount, changesForChunk.AddedEntities.Ptr, changesForChunk.AddedEntities.Length * sizeof(Entity));
                    UnsafeUtility.MemCpyReplicate(addedComponentIndices.Ptr + addedEntityCurrentCount, &sharedComponentDataBufferIndex, sizeof(int), changesForChunk.AddedEntities.Length);

                    addedEntityCurrentCount += changesForChunk.AddedEntities.Length;
                    sharedComponentDataBufferIndex++;
                }

                Result.Ptr->AddedComponentEntities = addedComponentEntities;
                Result.Ptr->RemovedComponentEntities = removedComponentEntities;
                Result.Ptr->AddedComponentIndices = addedComponentIndices;
                Result.Ptr->RemovedComponentIndices = removedComponentIndices;
                Result.Ptr->SharedComponentData = sharedComponentData;
            }
        }

        [BurstCompile]
        unsafe struct UpdateShadowChunksJob : IJobParallelForDefer
        {
            public TypeIndex TypeIndex;
            [NativeDisableUnsafePtrRestriction] public EntityComponentStore* ECS;
            [ReadOnly] public NativeList<ArchetypeChunk> Chunks; // not used, but required to be here for IJobParallelForDefer
            [ReadOnly] public NativeArray<ChunkChanges> ChangesByChunk;
            [ReadOnly] public NativeArray<ChunkShadow> AllocatedChunkShadowsByChunk;
            [WriteOnly] public NativeParallelHashMap<ulong, ChunkShadow>.ParallelWriter ShadowChunksBySequenceNumber;

            public void Execute(int index)
            {
                var changes = ChangesByChunk[index];

                var chunk = changes.Chunk;

                if (ChunkIndex.Null == chunk)
                    return;

                if (!changes.Shadow.IsCreated)
                {
                    // Assume the shadow was allocated correctly by the previous job. It exist in the sparse array at this chunks index.
                    ShadowChunksBySequenceNumber.TryAdd(chunk.SequenceNumber, AllocatedChunkShadowsByChunk[index]);
                }
                else
                {
                    var entityCount = chunk.Count;
                    var listIndex = chunk.ListIndex;

                    var archetype = ECS->GetArchetype(chunk);
                    var indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(archetype, TypeIndex);
                    var sharedComponentValueArray = archetype->Chunks.GetSharedComponentValues(listIndex);
                    var sharedComponentOffset = indexInTypeArray - archetype->FirstSharedComponent;
                    var sharedComponentDataIndex = sharedComponentValueArray[sharedComponentOffset];
                    var sharedComponentElementIndex = EntityComponentStore.GetElementIndexFromSharedComponentIndex(sharedComponentDataIndex);
                    var entities = chunk.Buffer + archetype->Offsets[0];

                    changes.Shadow.Ptr->SharedComponentElementIndex = sharedComponentElementIndex;
                    changes.Shadow.Ptr->Count = entityCount;
                    changes.Shadow.Ptr->Version = archetype->Chunks.GetChangeVersion(0, listIndex);

                    UnsafeUtility.MemCpy(changes.Shadow.Ptr->Entities, entities, entityCount * sizeof(Entity));
                }
            }
        }

        [BurstCompile]
        unsafe struct RemoveChunkShadowsJob : IJob
        {
            [ReadOnly] public NativeArray<ChunkShadow> AllocatedChunkShadowByChunk;
            [ReadOnly] public NativeArray<ChunkShadow> RemovedChunks;
            public NativeParallelHashMap<ulong, ChunkShadow> ChunkShadowBySequenceNumber;
            public NativeList<ulong> ChunkShadowBySequenceNumberKeys;
            public NativeList<int> ChunkShadowBySequenceNumberKeysFreeList;

            public void Execute()
            {
                for (var i=0; i<AllocatedChunkShadowByChunk.Length; i++)
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
        unsafe struct CopyStateToShadowSharedComponentDataJob : IJob
        {
            public TypeIndex TypeIndex;
            public int ComponentSize;

            /// <summary>
            /// The set of existing shared component data.
            /// </summary>
            public UnsafeList<ComponentTypeList> SharedComponentDataByType;

            /// <summary>
            /// The shadow copy of all shared component data.
            /// </summary>
            [NativeDisableUnsafePtrRestriction] public UnsafeList<byte>* ShadowChunkSharedComponentData;

            public void Execute()
            {
                ShadowChunkSharedComponentData->Clear();

                var typeIndexWithoutFlags = TypeIndex.Index;

                if (typeIndexWithoutFlags < SharedComponentDataByType.Length)
                {
                    var sharedComponentData = &SharedComponentDataByType.Ptr[typeIndexWithoutFlags];

                    if (sharedComponentData->IsCreated)
                    {
                        // For now we just do a brute force copy each frame. This should be optimized to only update changed things but will likely need some deeper investigation.
                        ShadowChunkSharedComponentData->Resize(sharedComponentData->Length * ComponentSize);
                        UnsafeUtility.MemCpy(ShadowChunkSharedComponentData->Ptr, sharedComponentData->Ptr, sharedComponentData->Length * ComponentSize);
                    }
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
