using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// The <see cref="EntityDiffer"/> is use to efficiently track created and destroyed entities within a world over time.
    /// </summary>
    class EntityDiffer : IDisposable
    {
        /// <summary>
        /// A chunk shadow is used to retain a copy of all <see cref="Entity"/> data. This is compared against the current state of the world to detect which entities have changed.
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
                /// A copy of all entities in this chunk.
                /// </summary>
                public Entity* Entities;

                /// <summary>
                /// The sequence number for this chunk.
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
        unsafe struct ChunkChanges : IDisposable
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

        NativeParallelHashMap<ulong, ChunkShadow> m_ChunkShadowBySequenceNumber;
        NativeList<ulong> m_ChunkShadowBySequenceNumberKeys;
        NativeList<int> m_ChunkShadowBySequenceNumberKeysFreeList;
        NativeParallelHashSet<ulong> m_ChunkSequenceNumbers;
        NativeList<ChunkShadow> m_AllocatedChunkShadowByChunk;
        NativeList<ChunkChanges> m_ChangesByChunk;
        NativeList<ChunkShadow> m_RemovedChunks;
        NativeList<Entity> m_RemovedEntities;

        public EntityDiffer(World world)
        {
            m_ChunkShadowBySequenceNumber = new NativeParallelHashMap<ulong, ChunkShadow>(16, Allocator.Persistent);
            m_ChunkShadowBySequenceNumberKeys = new NativeList<ulong>(16, Allocator.Persistent);
            m_ChunkShadowBySequenceNumberKeysFreeList = new NativeList<int>(16, Allocator.Persistent);
            m_ChunkSequenceNumbers = new NativeParallelHashSet<ulong>(16, Allocator.Persistent);
            m_AllocatedChunkShadowByChunk = new NativeList<ChunkShadow>(16, Allocator.Persistent);
            m_ChangesByChunk = new NativeList<ChunkChanges>(16, Allocator.Persistent);
            m_RemovedChunks = new NativeList<ChunkShadow>(16, Allocator.Persistent);
            m_RemovedEntities = new NativeList<Entity>(Allocator.Persistent);
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
        }

        public unsafe JobHandle GetEntityQueryMatchDiffAsync(EntityQuery query, NativeList<Entity> newEntities, NativeList<Entity> missingEntities)
        {
            newEntities.Clear();
            missingEntities.Clear();

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
            m_RemovedChunks.Capacity = math.max(m_RemovedChunks.Capacity, m_ChunkShadowBySequenceNumberKeys.Length);

            var gatherEntityChangesJobHandle = new GatherEntityChangesJob
            {
                Chunks = chunks,
                ChunkShadowBySequenceNumber = m_ChunkShadowBySequenceNumber,
                ChangesByChunk = m_ChangesByChunk.GetUnsafeList()->Ptr
            }.Schedule(chunks, 1, resizeAndClearJobHandle);

            var gatherExistingChunksJobHandle = new GatherExistingChunksJob
            {
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
                RemovedChunks = m_RemovedChunks.AsDeferredJobArray(),
                RemovedEntities = m_RemovedEntities
            }.Schedule(gatherRemovedChunksJobHandle);

            var allocateNewShadowChunksJobHandle = new AllocateChunkShadowsJob
            {
                Chunks = chunks,
                ShadowChunksBySequenceNumber = m_ChunkShadowBySequenceNumber,
                AllocatedShadowChunks = m_AllocatedChunkShadowByChunk.GetUnsafeList()->Ptr
            }.Schedule(chunks, 1, resizeAndClearJobHandle);

            var buildChangeSetJobHandle = new BuildChangeSetJob
            {
                ChangesByChunk = m_ChangesByChunk.AsDeferredJobArray(),
                RemovedChunkEntities = m_RemovedEntities,
                AddedEntities = newEntities,
                RemovedEntities = missingEntities
            }.Schedule(JobHandle.CombineDependencies(gatherEntityChangesJobHandle, gatherRemovedEntitiesJobHandle, allocateNewShadowChunksJobHandle));

            var updateShadowChunksJobHandle = new UpdateShadowChunksJob
            {
                EntityComponentStore = query.GetEntityComponentStore(),
                Chunks = chunks,
                ChangesByChunk = m_ChangesByChunk.AsDeferredJobArray(),
                AllocatedChunkShadowsByChunk = m_AllocatedChunkShadowByChunk.AsDeferredJobArray(),
                ChunkShadowBySequenceNumber = m_ChunkShadowBySequenceNumber.AsParallelWriter()
            }.Schedule(chunks, 1, JobHandle.CombineDependencies(gatherEntityChangesJobHandle, gatherRemovedEntitiesJobHandle, allocateNewShadowChunksJobHandle));

            var removeShadowChunksJobHande = new RemoveChunkShadowsJob
            {
                AllocatedChunkShadowByChunk = m_AllocatedChunkShadowByChunk.AsDeferredJobArray(),
                RemovedChunks = m_RemovedChunks.AsDeferredJobArray(),
                ChunkShadowBySequenceNumber = m_ChunkShadowBySequenceNumber,
                ChunkShadowBySequenceNumberKeys = m_ChunkShadowBySequenceNumberKeys,
                ChunkShadowBySequenceNumberKeysFreeList = m_ChunkShadowBySequenceNumberKeysFreeList
            }.Schedule(updateShadowChunksJobHandle);

            var disposeChunksJobHandle = chunks.Dispose(JobHandle.CombineDependencies(buildChangeSetJobHandle, removeShadowChunksJobHande));

            var disposeGatheredChangesJobHandle = new DisposeGatheredChangesJob
            {
                GatheredChanges = m_ChangesByChunk
            }.Schedule(m_ChangesByChunk, 1, buildChangeSetJobHandle);

            return JobHandle.CombineDependencies(gatherRemovedChunksJobHandle, disposeChunksJobHandle, disposeGatheredChangesJobHandle);
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
        unsafe struct GatherEntityChangesJob : IJobParallelForDefer
        {
            [ReadOnly] public NativeList<ArchetypeChunk> Chunks;
            [ReadOnly] public NativeParallelHashMap<ulong, ChunkShadow> ChunkShadowBySequenceNumber;
            [NativeDisableUnsafePtrRestriction] public ChunkChanges* ChangesByChunk;

            public void Execute(int index)
            {
                var chunk = Chunks[index].m_Chunk;
                var archetype = Chunks[index].Archetype.Archetype;

                if (ChunkShadowBySequenceNumber.TryGetValue(chunk.SequenceNumber, out var shadow))
                {
                    var entityVersion = archetype->Chunks.GetChangeVersion(0, chunk.ListIndex);
                    if (!ChangeVersionUtility.DidChange(entityVersion, shadow.Ptr->Version))
                        return;

                    var changesForChunk = ChangesByChunk + index;
                    changesForChunk->Chunk = chunk;
                    changesForChunk->Shadow = shadow;
                    changesForChunk->AddedEntities = new UnsafeList<Entity>(0, Allocator.TempJob);
                    changesForChunk->RemovedEntities = new UnsafeList<Entity>(0, Allocator.TempJob);

                    var currentEntity = (Entity*)(chunk.Buffer + archetype->Offsets[0]);
                    var entityFromShadow = (Entity*)shadow.Ptr->Entities;

                    var currentCount = chunk.Count;
                    var shadowCount = shadow.Ptr->Count;

                    var i = 0;

                    for (; i < currentCount && i < shadowCount; i++)
                    {
                        if (currentEntity[i] == entityFromShadow[i])
                            continue;

                        // Was a valid entity but version was incremented, thus destroyed
                        if (entityFromShadow[i].Version != 0)
                        {
                            changesForChunk->RemovedEntities.Add(entityFromShadow[i]);
                            changesForChunk->AddedEntities.Add(currentEntity[i]);
                        }
                    }

                    for (; i < currentCount; i++)
                        changesForChunk->AddedEntities.Add(currentEntity[i]);

                    for (; i < shadowCount; i++)
                        changesForChunk->RemovedEntities.Add(entityFromShadow[i]);
                }
                else
                {
                    var addedComponentEntities = new UnsafeList<Entity>(chunk.Count, Allocator.TempJob);
                    var entities = chunk.Buffer + archetype->Offsets[0];
                    addedComponentEntities.AddRange(entities, chunk.Count);

                    var changesForChunk = ChangesByChunk + index;
                    changesForChunk->Chunk = chunk;
                    changesForChunk->Shadow = default;
                    changesForChunk->AddedEntities = addedComponentEntities;
                    changesForChunk->RemovedEntities = default;
                }
            }
        }

        [BurstCompile]
        unsafe struct GatherExistingChunksJob : IJobParallelForDefer
        {
            [ReadOnly] public NativeList<ArchetypeChunk> Chunks;
            [WriteOnly] public NativeParallelHashSet<ulong>.ParallelWriter ChunkSequenceNumbers;

            public void Execute(int index)
            {
                ChunkSequenceNumbers.Add(Chunks[index].m_Chunk.SequenceNumber);
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
            [ReadOnly] public NativeArray<ChunkShadow> RemovedChunks;
            [WriteOnly] public NativeList<Entity> RemovedEntities;

            public void Execute()
            {
                for (var i = 0; i < RemovedChunks.Length; i++)
                    RemovedEntities.AddRange(RemovedChunks[i].Ptr->Entities, RemovedChunks[i].Ptr->Count);
            }
        }

        [BurstCompile]
        unsafe struct AllocateChunkShadowsJob : IJobParallelForDefer
        {
            [ReadOnly] public NativeList<ArchetypeChunk> Chunks;
            [ReadOnly] public NativeParallelHashMap<ulong, ChunkShadow> ShadowChunksBySequenceNumber;
            [NativeDisableUnsafePtrRestriction] public ChunkShadow* AllocatedShadowChunks;

            public void Execute(int index)
            {
                var chunk = Chunks[index].m_Chunk;
                var archetype = Chunks[index].Archetype.Archetype;
                var chunkCount = chunk.Count;
                var sequenceNumber = chunk.SequenceNumber;

                if (ShadowChunksBySequenceNumber.TryGetValue(sequenceNumber, out var shadow))
                    return;

                var entities = chunk.Buffer + archetype->Offsets[0];

                shadow = new ChunkShadow(Allocator.Persistent);
                shadow.Ptr->Count = chunkCount;
                shadow.Ptr->Version = archetype->Chunks.GetChangeVersion(0, chunk.ListIndex);
                shadow.Ptr->Entities = (Entity*)Memory.Unmanaged.Allocate(sizeof(Entity) * archetype->ChunkCapacity, 4, Allocator.Persistent);
                shadow.Ptr->SequenceNumber = sequenceNumber;

                UnsafeUtility.MemCpy(shadow.Ptr->Entities, entities, chunkCount * sizeof(Entity));

                *(AllocatedShadowChunks + index) = shadow;
            }
        }

        [BurstCompile]
        unsafe struct BuildChangeSetJob : IJob
        {
            [ReadOnly] public NativeArray<ChunkChanges> ChangesByChunk;
            [ReadOnly] public NativeList<Entity> RemovedChunkEntities;

            public NativeList<Entity> AddedEntities;
            public NativeList<Entity> RemovedEntities;

            public void Execute()
            {
                var addedEntityCount = 0;
                var removedEntityCount = RemovedChunkEntities.Length;

                for (var i = 0; i < ChangesByChunk.Length; i++)
                {
                    var changesForChunk = ChangesByChunk[i];
                    addedEntityCount += changesForChunk.AddedEntities.Length;
                    removedEntityCount += changesForChunk.RemovedEntities.Length;
                }

                if (addedEntityCount == 0 && removedEntityCount == 0)
                    return;

                AddedEntities.Capacity = math.max(addedEntityCount, AddedEntities.Capacity);
                RemovedEntities.Capacity = math.max(removedEntityCount, RemovedEntities.Capacity);

                AddedEntities.Length = 0;
                RemovedEntities.Length = 0;

                for (var i = 0; i < ChangesByChunk.Length; i++)
                {
                    var changesForChunk = ChangesByChunk[i];

                    if (changesForChunk.AddedEntities.IsCreated)
                    {
                        AddedEntities.AddRangeNoResize(changesForChunk.AddedEntities.Ptr, changesForChunk.AddedEntities.Length);
                    }

                    if (changesForChunk.RemovedEntities.IsCreated)
                    {
                        RemovedEntities.AddRangeNoResize(changesForChunk.RemovedEntities.Ptr, changesForChunk.RemovedEntities.Length);
                    }
                }

                RemovedEntities.AddRangeNoResize(RemovedChunkEntities.GetUnsafeReadOnlyPtr(), RemovedChunkEntities.Length);
            }
        }

        [BurstCompile]
        unsafe struct UpdateShadowChunksJob : IJobParallelForDefer
        {
            [NativeDisableUnsafePtrRestriction][ReadOnly] public EntityComponentStore* EntityComponentStore;
            [ReadOnly] public NativeList<ArchetypeChunk> Chunks; // not used, but required to be here for IJobParallelForDefer
            [ReadOnly] public NativeArray<ChunkChanges> ChangesByChunk;
            [ReadOnly] public NativeArray<ChunkShadow> AllocatedChunkShadowsByChunk;
            [WriteOnly] public NativeParallelHashMap<ulong, ChunkShadow>.ParallelWriter ChunkShadowBySequenceNumber;

            public void Execute(int index)
            {
                var changes = ChangesByChunk[index];

                var chunk = changes.Chunk;

                if (ChunkIndex.Null == chunk)
                    return;

                if (AllocatedChunkShadowsByChunk[index].IsCreated)
                {
                    ChunkShadowBySequenceNumber.TryAdd(chunk.SequenceNumber, AllocatedChunkShadowsByChunk[index]);
                }
                else
                {
                    var archetype = EntityComponentStore->GetArchetype(chunk);
                    var entities = chunk.Buffer + archetype->Offsets[0];
                    var entitiesCount = chunk.Count;

                    changes.Shadow.Ptr->Count = entitiesCount;
                    changes.Shadow.Ptr->Version = archetype->Chunks.GetChangeVersion(0, chunk.ListIndex);

                    UnsafeUtility.MemCpy(changes.Shadow.Ptr->Entities, entities, entitiesCount * sizeof(Entity));
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
