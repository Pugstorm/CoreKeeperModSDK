using Unity.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine.Profiling;

namespace Unity.Entities
{
    static unsafe partial class EntityDiffer
    {
        static readonly Profiling.ProfilerMarker s_PlaybackManagedChangesMarker = new Profiling.ProfilerMarker("PlaybackManagedChanges");
        static readonly Profiling.ProfilerMarker s_CopySharedComponentsMarker = new Profiling.ProfilerMarker("CopySharedComponents");
        static readonly Profiling.ProfilerMarker s_CopyManagedComponentsMarker = new Profiling.ProfilerMarker("CopyManagedComponents");

        [BurstCompile]
        struct PatchAndAddClonedChunks : IJobParallelFor
        {
            [ReadOnly] public NativeArray<ArchetypeChunk> SrcChunks;
            [ReadOnly] public NativeArray<ArchetypeChunk> DstChunks;

            [NativeDisableUnsafePtrRestriction] public EntityComponentStore* SrcEntityComponentStore;
            [NativeDisableUnsafePtrRestriction] public EntityComponentStore* DstEntityComponentStore;

            public void Execute(int index)
            {
                var srcChunk = SrcChunks[index].m_Chunk;
                var dstChunk = DstChunks[index].m_Chunk;

                var srcArchetype = SrcEntityComponentStore->GetArchetype(srcChunk);
                var dstArchetype = DstEntityComponentStore->GetArchetype(dstChunk);

                ChunkDataUtility.CloneChangeVersions(srcArchetype, srcChunk.ListIndex, dstArchetype, dstChunk.ListIndex);

                DstEntityComponentStore->AddExistingEntitiesInChunk(dstChunk);
            }
        }

        internal static void CopyAndReplaceChunks(
            EntityManager srcEntityManager,
            EntityManager dstEntityManager,
            EntityQuery dstEntityQuery,
            ArchetypeChunkChanges archetypeChunkChanges,
            NativeArray<EntityRemapUtility.EntityRemapInfo> remap = default)
        {
            s_CopyAndReplaceChunksProfilerMarker.Begin();
            var dstAccess = dstEntityManager.GetCheckedEntityDataAccess();
            var srcAccess = srcEntityManager.GetCheckedEntityDataAccess();

            var archetypeChanges = dstAccess->EntityComponentStore->BeginArchetypeChangeTracking();

            DestroyChunks(dstEntityManager, archetypeChunkChanges.DestroyedDstChunks.Chunks);

            if (!remap.IsCreated)
            {
                CloneAndAddChunks(srcEntityManager, dstEntityManager, archetypeChunkChanges.CreatedSrcChunks.Chunks);
            }
            else
            {
                using var cloned = new NativeList<ArchetypeChunk>(Allocator.TempJob);
                CloneAndAddChunks(srcEntityManager, dstEntityManager, archetypeChunkChanges.CreatedSrcChunks.Chunks, cloned);

                var srcChunks = archetypeChunkChanges.CreatedSrcChunks.Chunks;
                Assert.AreEqual(srcChunks.Length, cloned.Length);
                for (int ci = 0, cc = cloned.Length; ci < cc; ci++)
                {
                    var srcChunk = srcChunks[ci].m_Chunk;
                    var srcEntities = (Entity*)srcChunk.Buffer;
                    var dstChunk = cloned[ci].m_Chunk;
                    var dstEntities = (Entity*)dstChunk.Buffer;

                    for (int ei = 0, ec = srcChunk.Count; ei < ec; ei++)
                    {
                        EntityRemapUtility.AddEntityRemapping(ref remap, srcEntities[ei], dstEntities[ei]);
                    }
                }
            }

            dstAccess->EntityComponentStore->EndArchetypeChangeTracking(archetypeChanges, dstAccess->EntityQueryManager);
            srcAccess->EntityComponentStore->InvalidateChunkListCacheForChangedArchetypes();
            dstAccess->EntityComponentStore->InvalidateChunkListCacheForChangedArchetypes();

            //@TODO-opt: use a query that searches for all chunks that have chunk components on it
            //@TODO-opt: Move this into a job
            // Any chunk might have been recreated, so the ChunkHeader might be invalid
            using (var allDstChunks = dstEntityQuery.ToArchetypeChunkArray(Allocator.TempJob))
            {
                foreach (var chunk in allDstChunks)
                {
                    var metaEntity = chunk.m_Chunk.MetaChunkEntity;
                    if (metaEntity != Entity.Null)
                    {
                        if (dstEntityManager.Exists(metaEntity))
                            dstEntityManager.SetComponentData(metaEntity, new ChunkHeader { ArchetypeChunk = chunk });
                    }
                }
            }

            srcAccess->EntityComponentStore->IncrementGlobalSystemVersion();
            dstAccess->EntityComponentStore->IncrementGlobalSystemVersion();
            s_CopyAndReplaceChunksProfilerMarker.End();
        }

        [BurstCompile]
        struct DestroyChunksJob : IJob
        {
            public EntityManager EntityManager;
            public NativeList<ArchetypeChunk> Chunks;

            public void Execute()
            {
                var access = EntityManager.GetCheckedEntityDataAccess();
                var ecs = access->EntityComponentStore;

                for (var i = 0; i < Chunks.Length; i++)
                {
                    var chunk = Chunks[i].m_Chunk;
                    var archetype = Chunks[i].Archetype.Archetype;
                    var count = chunk.Count;
                    ChunkDataUtility.DeallocateBuffers(archetype, chunk);
                    ecs->DeallocateManagedComponents(chunk, 0, count);

                    archetype->EntityCount -= chunk.Count;
                    ecs->FreeEntities(chunk);

                    ChunkDataUtility.SetChunkCountKeepMetaChunk(archetype, chunk, 0);
                }
            }
        }

        static void DestroyChunks(EntityManager entityManager, NativeList<ArchetypeChunk> chunks)
        {
            s_DestroyChunksProfilerMarker.Begin();
            new DestroyChunksJob
            {
                EntityManager = entityManager,
                Chunks = chunks
            }.Run();
            s_PlaybackManagedChangesMarker.Begin();
            var access = entityManager.GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;
            var mcs = access->ManagedComponentStore;
            mcs.Playback(ref ecs->ManagedChangesTracker);
            s_PlaybackManagedChangesMarker.End();
            s_DestroyChunksProfilerMarker.End();
        }

        [BurstCompile]
        struct CollectSharedComponentIndices : IJob
        {
            public NativeList<ArchetypeChunk> Chunks;
            public NativeList<int> SharedComponentIndices;

            public void Execute()
            {
                var indices = new UnsafeParallelHashSet<int>(128, Allocator.Temp);
                for (int i = 0; i < Chunks.Length; i++)
                    HandleChunk(Chunks[i].Archetype.Archetype, Chunks[i].m_Chunk, ref indices);

                SharedComponentIndices.AddRange(indices.ToNativeArray(Allocator.Temp));
            }

            void HandleChunk(Archetype* srcArchetype, ChunkIndex srcChunk, ref UnsafeParallelHashSet<int> indices)
            {
                var n = srcArchetype->NumSharedComponents;
                var sharedIndices = stackalloc int[srcArchetype->NumSharedComponents];
                var sharedComponents = srcArchetype->Chunks.GetSharedComponentValues(srcChunk.ListIndex);
                sharedComponents.CopyTo(sharedIndices, 0, srcArchetype->NumSharedComponents);
                for (int i = 0; i < n; i++)
                {
                    indices.Add(sharedIndices[i]);
                }
            }
        }

        [BurstCompile]
        struct CreateNewChunks : IJob
        {
            public NativeList<ArchetypeChunk> Chunks;
            public NativeArray<ArchetypeChunk> ClonedChunks;
            public EntityManager DstEntityManager;

            [ReadOnly]
            public NativeArray<int> SrcSharedComponentIndices;
            [ReadOnly]
            public NativeArray<int> DstSharedComponentIndices;

            public void Execute()
            {
                var dstEntityComponentStore = DstEntityManager.GetCheckedEntityDataAccess()->EntityComponentStore;

                var remapping = new UnsafeParallelHashMap<int, int>(SrcSharedComponentIndices.Length, Allocator.Temp);
                for (int i = 0; i < SrcSharedComponentIndices.Length; i++)
                    remapping.Add(SrcSharedComponentIndices[i], DstSharedComponentIndices[i]);
                for (int i = 0; i < Chunks.Length; i++)
                    HandleChunk(i, dstEntityComponentStore, remapping);
            }

            void HandleChunk(int idx, EntityComponentStore* dstEntityComponentStore, UnsafeParallelHashMap<int, int> sharedComponentRemap)
            {
                var srcChunk = Chunks[idx].m_Chunk;
                var srcArchetype = Chunks[idx].Archetype.Archetype;
                var numSharedComponents = srcArchetype->NumSharedComponents;
                var dstSharedIndices = stackalloc int[numSharedComponents];
                var srcSharedComponents = srcArchetype->Chunks.GetSharedComponentValues(srcChunk.ListIndex);
                srcSharedComponents.CopyTo(dstSharedIndices, 0, numSharedComponents);
                for (int i = 0; i < numSharedComponents; i++)
                    dstSharedIndices[i] = sharedComponentRemap[dstSharedIndices[i]];

                var dstArchetype = dstEntityComponentStore->GetOrCreateArchetype(srcArchetype->Types, srcArchetype->TypesCount);
                var dstChunk = dstEntityComponentStore->GetCleanChunkNoMetaChunk(dstArchetype, dstSharedIndices);
                dstChunk.MetaChunkEntity = srcChunk.MetaChunkEntity;
                var srcChunkCount = srcChunk.Count;
                ChunkDataUtility.SetChunkCountKeepMetaChunk(dstArchetype, dstChunk, srcChunkCount);
                dstArchetype->EntityCount += srcChunkCount;
                dstChunk.SequenceNumber = srcChunk.SequenceNumber;

                ClonedChunks[idx] = new ArchetypeChunk { m_Chunk = dstChunk };
            }
        }

        [BurstCompile]
        struct CopyChunkBuffers : IJobFor
        {
            [ReadOnly]
            public NativeList<ArchetypeChunk> Chunks;
            [ReadOnly]
            public NativeArray<ArchetypeChunk> ClonedChunks;

            public void Execute(int index)
            {
                var srcChunk = Chunks[index].m_Chunk;
                var dstChunk = ClonedChunks[index].m_Chunk;
                UnsafeUtility.MemCpy(dstChunk.Buffer, srcChunk.Buffer, Chunk.kChunkBufferSize);
                BufferHeader.PatchAfterCloningChunk(Chunks[index].Archetype.Archetype, dstChunk.Buffer, dstChunk.Count);
            }
        }

        static void CloneAndAddChunks(EntityManager srcEntityManager, EntityManager dstEntityManager, NativeList<ArchetypeChunk> chunks, NativeList<ArchetypeChunk> clonedList = default)
        {
            s_CloneAndAddChunksProfilerMarker.Begin();

            // sort chunks by archetype and clone chunks
            var srcSharedComponentIndices = new NativeList<int>(128, Allocator.TempJob);
            new CollectSharedComponentIndices
            {
                Chunks = chunks,
                SharedComponentIndices = srcSharedComponentIndices,
            }.Run();

            // copy shared components
            s_CopySharedComponentsMarker.Begin();
            var srcAccess = srcEntityManager.GetCheckedEntityDataAccess();
            var dstAccess = dstEntityManager.GetCheckedEntityDataAccess();
            var srcManagedComponentStore = srcAccess->ManagedComponentStore;
            var dstManagedComponentStore = dstAccess->ManagedComponentStore;

            var dstSharedComponentIndicesRemapped = new NativeArray<int>(srcSharedComponentIndices.AsArray(), Allocator.TempJob);
            dstAccess->CopySharedComponents(srcAccess, (int*) dstSharedComponentIndicesRemapped.GetUnsafeReadOnlyPtr(), dstSharedComponentIndicesRemapped.Length);
            s_CopySharedComponentsMarker.End();

            // clone chunks
            NativeArray<ArchetypeChunk> cloned;
            if (clonedList.IsCreated)
            {
                clonedList.Resize(chunks.Length, NativeArrayOptions.UninitializedMemory);
                cloned = clonedList.AsArray();
            }
            else
            {
                cloned = new NativeArray<ArchetypeChunk>(chunks.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            }

            new CreateNewChunks
            {
                Chunks = chunks,
                ClonedChunks = cloned,
                DstEntityManager = dstEntityManager,
                SrcSharedComponentIndices = srcSharedComponentIndices.AsArray(),
                DstSharedComponentIndices = dstSharedComponentIndicesRemapped,
            }.Run();

            var copyJob = new CopyChunkBuffers
            {
                Chunks = chunks,
                ClonedChunks = cloned
            }.Schedule(chunks.Length, default);
            JobHandle.ScheduleBatchedJobs();
            srcSharedComponentIndices.Dispose();

#if !ENTITY_STORE_V1
            copyJob.Complete();
            var entityRemapping = srcEntityManager.CreateEntityRemapArray(Allocator.TempJob);
            dstEntityManager.DuplicateEntitiesForDiffer(entityRemapping, chunks.AsArray(), cloned);

#if !DOTS_DISABLE_DEBUG_NAMES
            dstAccess->EntityComponentStore->CopyAndUpdateNameByEntity(srcAccess->EntityComponentStore, chunks.AsArray(), entityRemapping);
#endif
#endif

            s_PlaybackManagedChangesMarker.Begin();
            dstManagedComponentStore.Playback(ref dstAccess->EntityComponentStore->ManagedChangesTracker);

            // Release any references obtained by CopySharedComponents above
            for (var i = 0; i < dstSharedComponentIndicesRemapped.Length; i++)
                dstAccess->RemoveSharedComponentReference(dstSharedComponentIndicesRemapped[i]);
            s_PlaybackManagedChangesMarker.End();

            dstSharedComponentIndicesRemapped.Dispose();
            copyJob.Complete();

            // Copy enabled bits.
            // Note that CloneEnabledBits() requires the dstChunk enabled bit hierarchical count to be correct as a precondition,
            // which it is (zero) thanks to CreateNewChunks calling GetCleanChunkNoMetaChunk().
            for (int i = 0; i < cloned.Length; i++)
            {
                var srcChunk = chunks[i].m_Chunk;
                var dstChunk = cloned[i].m_Chunk;
                var srcArchetype = srcAccess->EntityComponentStore->GetArchetype(srcChunk);
                var dstArchetype = dstAccess->EntityComponentStore->GetArchetype(dstChunk);
                var srcChunkCount = srcChunk.Count;
                ChunkDataUtility.CloneEnabledBits(srcChunk, srcArchetype, 0, dstChunk, dstArchetype, 0, srcChunkCount);
                Assertions.Assert.AreEqual(srcChunkCount, dstChunk.Count);
                // Can't do this until chunk count is up to date and padding bits are clear,
                // which is implicitly the case at this point
                ChunkDataUtility.UpdateChunkDisabledEntityCounts(dstChunk, dstArchetype);
            }

            s_CopyManagedComponentsMarker.Begin();
            for (int i = 0; i < cloned.Length; i++)
            {
                var dstChunk = cloned[i].m_Chunk;
                var dstArchetype = dstAccess->EntityComponentStore->GetArchetype(dstChunk);
                var numManagedComponents = dstArchetype->NumManagedComponents;
                var hasCompanionComponents = dstArchetype->HasCompanionComponents;
                for (int t = 0; t < numManagedComponents; ++t)
                {
                    int indexInArchetype = t + dstArchetype->FirstManagedComponent;
                    var offset = dstArchetype->Offsets[indexInArchetype];
                    var a = (int*) (dstChunk.Buffer + offset);
                    int count = dstChunk.Count;

                    if (hasCompanionComponents)
                    {
                        // We consider hybrid components as always different, there's no reason to clone those at this point.
                        var typeIndex = dstArchetype->Types[indexInArchetype].TypeIndex;
                        var typeCategory = TypeManager.GetTypeInfo(typeIndex).Category;
                        if (typeCategory == TypeManager.TypeCategory.UnityEngineObject)
                        {
                            // We still need to patch their indices, because otherwise they might point to invalid memory in
                            // the managed component store. Setting them to the invalid index 0 is harmless, assuming nobody
                            // actually operates on the shadow world.
                            UnsafeUtility.MemSet(a, 0, sizeof(int) * count);
                            continue;
                        }
                    }

                    dstManagedComponentStore.CloneManagedComponentsFromDifferentWorld(a, count,
                        srcManagedComponentStore, ref *dstAccess->EntityComponentStore);
                }

#if !ENTITY_STORE_V1
                dstChunk.MetaChunkEntity = EntityRemapUtility.RemapEntity(ref entityRemapping, dstChunk.MetaChunkEntity);
#endif
            }
            s_CopyManagedComponentsMarker.End();

#if !ENTITY_STORE_V1
            dstEntityManager.RemapEntitiesForDiffer(entityRemapping, chunks.AsArray(), cloned);

            s_PlaybackManagedChangesMarker.Begin();
            dstManagedComponentStore.Playback(ref dstAccess->EntityComponentStore->ManagedChangesTracker);
            s_PlaybackManagedChangesMarker.End();

            entityRemapping.Dispose();
#endif

#if ENTITY_STORE_V1
            // Ensure capacity in the dst world before we start linking entities.
            dstAccess->EntityComponentStore->EnsureCapacity(srcEntityManager.EntityCapacity);
            dstAccess->EntityComponentStore->CopyNextFreeEntityIndex(srcAccess->EntityComponentStore);
#endif

#if !DOTS_DISABLE_DEBUG_NAMES && ENTITY_STORE_V1
            dstAccess->EntityComponentStore->CopyAndUpdateNameByEntity(srcAccess->EntityComponentStore);
#endif

            new PatchAndAddClonedChunks
            {
                SrcChunks = chunks.AsArray(),
                DstChunks = cloned,
                SrcEntityComponentStore = srcAccess->EntityComponentStore,
                DstEntityComponentStore = dstAccess->EntityComponentStore
            }.Schedule(chunks.Length, 64).Complete();

            if (!clonedList.IsCreated)
            {
                cloned.Dispose();
            }

            s_CloneAndAddChunksProfilerMarker.End();
        }
    }
}
