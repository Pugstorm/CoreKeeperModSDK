using System;
using Unity.Assertions;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.Entities
{
    internal unsafe partial struct EntityComponentStore
    {
        // ----------------------------------------------------------------------------------------------------------
        // PUBLIC
        // ----------------------------------------------------------------------------------------------------------

        public bool AddComponent(Entity entity, ComponentType type)
        {
            var dstChunk = GetChunkWithEmptySlotsWithAddedComponent(entity, type);
            if (dstChunk == ChunkIndex.Null)
                return false;

            Move(entity, dstChunk);
            return true;
        }

        public void AddComponent(Entity entity, in ComponentTypeSet componentTypeSet)
        {
            var chunk = GetChunk(entity);
            var archetype = GetArchetype(chunk);
            var newArchetype = GetArchetypeWithAddedComponents(archetype, componentTypeSet);
            if (newArchetype == archetype)  // none were added
                return;
            var archetypeChunkFilter = GetArchetypeChunkFilterWithAddedComponents(chunk, newArchetype);
            Move(entity, ref archetypeChunkFilter);
        }

        public bool RemoveComponent(Entity entity, ComponentType type)
        {
            var dstChunk = GetChunkWithEmptySlotsWithRemovedComponent(entity, type);
            if (dstChunk == ChunkIndex.Null)
                return false;

            Move(entity, dstChunk);
            return true;
        }

        public void RemoveComponent(Entity entity, in ComponentTypeSet componentTypeSet)
        {
            if (Hint.Unlikely(!Exists(entity)))
                return;
            var chunk = GetChunk(entity);
            var archetype = GetArchetype(chunk);
            var newArchetype = GetArchetypeWithRemovedComponents(archetype, componentTypeSet);
            if (newArchetype == archetype)  // none were removed
                return;
            var archetypeChunkFilter = GetArchetypeChunkFilterWithRemovedComponents(chunk, newArchetype);
            Move(entity, ref archetypeChunkFilter);
        }

        bool AddComponent(EntityBatchInChunk entityBatchInChunk, ComponentType componentType, int sharedComponentIndex = 0)
        {
            var srcChunk = entityBatchInChunk.Chunk;
            var archetypeChunkFilter = GetArchetypeChunkFilterWithAddedComponent(srcChunk, componentType, sharedComponentIndex);
            if (archetypeChunkFilter.Archetype == null)
                return false;

            Move(entityBatchInChunk, ref archetypeChunkFilter);
            return true;
        }

        bool AddComponents(EntityBatchInChunk entityBatchInChunk, in ComponentTypeSet componentTypeSet)
        {
            var srcChunk = entityBatchInChunk.Chunk;
            var srcArchetype = GetArchetype(srcChunk);
            var dstArchetype = GetArchetypeWithAddedComponents(srcArchetype, componentTypeSet);
            if (dstArchetype == srcArchetype)  // none were added
                return false;

            var archetypeChunkFilter = GetArchetypeChunkFilterWithAddedComponents(srcChunk, dstArchetype);
            if (archetypeChunkFilter.Archetype == null)
                return false;

            Move(entityBatchInChunk, ref archetypeChunkFilter);
            return true;
        }

        bool RemoveComponent(EntityBatchInChunk entityBatchInChunk, ComponentType componentType)
        {
            var srcChunk = entityBatchInChunk.Chunk;
            var archetypeChunkFilter = GetArchetypeChunkFilterWithRemovedComponent(srcChunk, componentType);
            if (archetypeChunkFilter.Archetype == null)
                return false;

            Move(entityBatchInChunk, ref archetypeChunkFilter);
            return true;
        }

        bool RemoveComponents(EntityBatchInChunk entityBatchInChunk, in ComponentTypeSet componentTypeSet)
        {
            var srcChunk = entityBatchInChunk.Chunk;
            var srcArchetype = GetArchetype(srcChunk);

            var dstArchetype = GetArchetypeWithRemovedComponents(srcArchetype, componentTypeSet);
            if (dstArchetype == srcArchetype)  // none were removed
                return false;

            var archetypeChunkFilter = GetArchetypeChunkFilterWithRemovedComponents(srcChunk, dstArchetype);
            if (archetypeChunkFilter.Archetype == null)
                return false;

            Move(entityBatchInChunk, ref archetypeChunkFilter);
            return true;
        }

        public void AddComponent(ArchetypeChunk* chunks, int chunkCount, ComponentType componentType, int sharedComponentIndex = 0)
        {
            Archetype* prevArchetype = null;
            Archetype* dstArchetype = null;
            int indexInTypeArray = 0;

            for (int i = 0; i < chunkCount; i++)
            {
                var chunk = chunks[i].m_Chunk;
                var srcArchetype = chunks[i].Archetype.Archetype;
                if (prevArchetype != srcArchetype)
                {
                    dstArchetype = GetArchetypeWithAddedComponent(srcArchetype, componentType, &indexInTypeArray);
                    prevArchetype = srcArchetype;
                }

                if (dstArchetype == null)
                    continue;

                var archetypeChunkFilter = GetArchetypeChunkFilterWithAddedComponent(srcArchetype, chunk.ListIndex, dstArchetype, indexInTypeArray, componentType, sharedComponentIndex);

                Move(chunk, ref archetypeChunkFilter);
            }
        }

        struct ChunkAndEnabledMask : IComparable<ChunkAndEnabledMask>
        {
            public v128 EnabledMask;
            public ChunkIndex Chunk;
            public int ChunkEntityCount;
            public byte UseEnabledMask;
            public int CompareTo(ChunkAndEnabledMask other) => Chunk.CompareTo(other.Chunk);
        }

        public void AddComponent(EntityQueryImpl *queryImpl, ComponentType componentType, int sharedComponentIndex = 0)
        {
            var chunkCacheIterator = new UnsafeChunkCacheIterator(queryImpl->_Filter,
                queryImpl->_QueryData->HasEnableableComponents != 0,
                queryImpl->GetMatchingChunkCache(), queryImpl->_QueryData->MatchingArchetypes.Ptr);
            int chunkIndex = -1;
            v128 chunkEnabledMask = default;
            int maxChunkCount = chunkCacheIterator.Length;
            using var chunksToProcess = new UnsafeList<ChunkAndEnabledMask>(maxChunkCount, Allocator.TempJob);
            while (chunkCacheIterator.MoveNextChunk(ref chunkIndex, out var archetypeChunk, out int chunkEntityCount,
                       out byte useEnabledMask, ref chunkEnabledMask))
            {
                // Structural changes are not allowed while using an UnsafeChunkCacheIterator, so we just track
                // the chunks & metadata to process outside the loop.
                chunksToProcess.AddNoResize(new ChunkAndEnabledMask
                {
                    Chunk = archetypeChunk.m_Chunk,
                    EnabledMask = chunkEnabledMask,
                    ChunkEntityCount = chunkEntityCount,
                    UseEnabledMask = useEnabledMask,
                });
            }
            // Apply the structural change to all chunks
            Archetype* srcArchetype = null;
            Archetype* dstArchetype = null;
            int indexInTypeArray = 0;
            var chunkBatches = stackalloc EntityBatchInChunk[TypeManager.MaximumChunkCapacity];
            for(int iChunk=0,chunkCount=chunksToProcess.Length; iChunk<chunkCount; ++iChunk)
            {
                var chunk = chunksToProcess[iChunk].Chunk;
                var chunkArchetype = GetArchetype(chunk);
                if (Hint.Unlikely(srcArchetype != chunkArchetype))
                {
                    srcArchetype = chunkArchetype;
                    dstArchetype = GetArchetypeWithAddedComponent(chunkArchetype, componentType, &indexInTypeArray);
                }
                if (dstArchetype == null)
                {
                    // the srcArchetype already has the requested component. If it's a shared component, we may still need to
                    // assign the new component value. Otherwise, there's nothing to do for this chunk.
                    if (Hint.Unlikely(componentType.IsSharedComponent))
                    {
                        SetSharedComponentDataIndexForChunk(chunk, chunkArchetype, componentType, sharedComponentIndex,
                            chunksToProcess[iChunk].UseEnabledMask, chunksToProcess[iChunk].EnabledMask, chunkBatches);
                    }
                    continue;
                }
                var archetypeChunkFilter = GetArchetypeChunkFilterWithAddedComponent(srcArchetype, chunk.ListIndex, dstArchetype,
                    indexInTypeArray, componentType, sharedComponentIndex);
                if (chunksToProcess[iChunk].UseEnabledMask == 0)
                {
                    // All entities in the chunk are enabled; move it en masse
                    Move(chunk, ref archetypeChunkFilter);
                }
                else
                {
                    // Populate a list of entity batches in this chunk to process
                    // Move(entity, archetypeChunkFilter) just creates a single-entity batch internally, so using batches
                    // for both branches here is fine.
                    int batchCount = 0;
                    int batchEndIndex = 0;
                    chunkEnabledMask = chunksToProcess[iChunk].EnabledMask;
                    while (EnabledBitUtility.TryGetNextRange(chunkEnabledMask, batchEndIndex,
                               out int batchStartIndex, out batchEndIndex))
                    {
                        chunkBatches[batchCount++] = new EntityBatchInChunk
                        {
                            Chunk = chunk, Count = batchEndIndex - batchStartIndex, StartIndex = batchStartIndex
                        };
                    }

                    Assert.IsTrue(batchCount <= TypeManager.MaximumChunkCapacity);
                    // Iterate batches backwards, to avoid mutating entities we haven't processed yet
                    for (int i = batchCount - 1; i >= 0; i--)
                    {
                        Move(chunkBatches[i], ref archetypeChunkFilter);
                    }
                }
            }
        }

        // Shared codepath for SetSharedComponent(query) and AddComponent(query)
        void SetSharedComponentDataIndexForChunk(ChunkIndex chunk, Archetype* chunkArchetype,
            ComponentType componentType, int sharedComponentIndex, byte useEnabledMask, v128 chunkEnabledMask, EntityBatchInChunk* chunkBatches)
        {
            var archetypeChunkFilter =
                GetArchetypeChunkFilterWithChangedSharedComponent(chunk, componentType, sharedComponentIndex);
            if (archetypeChunkFilter.Archetype == null)
                return; // this chunk already has the desired shared component value
            if (useEnabledMask == 0)
            {
                // All entities in the chunk are enabled; set the value en masse
                ChunkDataUtility.SetSharedComponentDataIndex(chunk, chunkArchetype,
                    archetypeChunkFilter.SharedComponentValues, componentType.TypeIndex);
            }
            else
            {
                // Populate a list of entity batches in this chunk to process
                // Move(entity, archetypeChunkFilter) just creates a single-entity batch internally, so using batches
                // for both branches here is fine.
                int batchCount = 0;
                int batchEndIndex = 0;
                while (EnabledBitUtility.TryGetNextRange(chunkEnabledMask, batchEndIndex, out int batchStartIndex,
                           out batchEndIndex))
                {
                    chunkBatches[batchCount++] = new EntityBatchInChunk
                    {
                        Chunk = chunk, Count = batchEndIndex - batchStartIndex, StartIndex = batchStartIndex
                    };
                }

                Assert.IsTrue(batchCount <= TypeManager.MaximumChunkCapacity);
                // Iterate batches backwards, to avoid mutating entities we haven't processed yet
                for (int i = batchCount - 1; i >= 0; i--)
                {
                    ChunkDataUtility.SetSharedComponentDataIndex(chunkBatches[i], chunkArchetype,
                        archetypeChunkFilter.SharedComponentValues, componentType.TypeIndex);
                }
            }
        }

        public void AddComponents(EntityQueryImpl* queryImpl, in ComponentTypeSet componentTypeSet)
        {
            var chunkCacheIterator = new UnsafeChunkCacheIterator(queryImpl->_Filter,
                queryImpl->_QueryData->HasEnableableComponents != 0,
                queryImpl->GetMatchingChunkCache(), queryImpl->_QueryData->MatchingArchetypes.Ptr);

            int chunkIndex = -1;
            v128 chunkEnabledMask = default;
            int maxChunkCount = chunkCacheIterator.Length;
            using var chunksToProcess = new UnsafeList<ChunkAndEnabledMask>(maxChunkCount, Allocator.TempJob);
            while (chunkCacheIterator.MoveNextChunk(ref chunkIndex, out var archetypeChunk, out int chunkEntityCount,
                       out byte useEnabledMask, ref chunkEnabledMask))
            {
                // Structural changes are not allowed while using an UnsafeChunkCacheIterator, so we just track
                // the chunks & metadata to process outside the loop.
                chunksToProcess.AddNoResize(new ChunkAndEnabledMask
                {
                    Chunk = archetypeChunk.m_Chunk,
                    EnabledMask = chunkEnabledMask,
                    ChunkEntityCount = chunkEntityCount,
                    UseEnabledMask = useEnabledMask,
                });
            }
            // Apply the structural change to all chunks
            Archetype* srcArchetype = null;
            Archetype* dstArchetype = null;
            var chunkBatches = stackalloc EntityBatchInChunk[TypeManager.MaximumChunkCapacity];
            for(int iChunk=0,chunkCount=chunksToProcess.Length; iChunk<chunkCount; ++iChunk)
            {
                var chunk = chunksToProcess[iChunk].Chunk;
                var chunkArchetype = GetArchetype(chunk);
                if (Hint.Unlikely(srcArchetype != chunkArchetype))
                {
                    srcArchetype = chunkArchetype;
                    dstArchetype = GetArchetypeWithAddedComponents(chunkArchetype, componentTypeSet);
                }
                if (Hint.Unlikely(srcArchetype == dstArchetype))
                    continue; // none were added
                var archetypeChunkFilter = GetArchetypeChunkFilterWithAddedComponents(chunk, dstArchetype);
                if (chunksToProcess[iChunk].UseEnabledMask == 0)
                {
                    // All entities in the chunk are enabled; move it en masse
                    Move(chunk, ref archetypeChunkFilter);
                }
                else
                {
                    // Populate a list of entity batches in this chunk to process
                    // Move(entity, archetypeChunkFilter) just creates a single-entity batch internally, so using batches
                    // for both branches here is fine.
                    int batchCount = 0;
                    int batchEndIndex = 0;
                    chunkEnabledMask = chunksToProcess[iChunk].EnabledMask;
                    while (EnabledBitUtility.TryGetNextRange(chunkEnabledMask, batchEndIndex, out int batchStartIndex, out batchEndIndex))
                    {
                        chunkBatches[batchCount++] = new EntityBatchInChunk
                        {
                            Chunk = chunk, Count = batchEndIndex - batchStartIndex, StartIndex = batchStartIndex
                        };
                    }
                    Assert.IsTrue(batchCount <= TypeManager.MaximumChunkCapacity);
                    // Iterate batches backwards, to avoid mutating entities we haven't processed yet
                    for (int i = batchCount - 1; i >= 0; i--)
                    {
                        Move(chunkBatches[i], ref archetypeChunkFilter);
                    }
                }
            }
        }

        public void RemoveComponent(ArchetypeChunk* chunks, int chunkCount, ComponentType componentType)
        {
            Archetype* prevArchetype = null;
            Archetype* dstArchetype = null;
            int indexInTypeArray = 0;

            for (int i = 0; i < chunkCount; i++)
            {
                var chunk = chunks[i].m_Chunk;
                var srcArchetype = chunks[i].Archetype.Archetype;

                if (prevArchetype != srcArchetype)
                {
                    dstArchetype = GetArchetypeWithRemovedComponent(srcArchetype, componentType, &indexInTypeArray);
                    prevArchetype = srcArchetype;
                }

                if (dstArchetype == srcArchetype)
                    continue;

                var archetypeChunkFilter = GetArchetypeChunkFilterWithRemovedComponent(chunk, dstArchetype, indexInTypeArray, componentType);

                Move(chunk, ref archetypeChunkFilter);
            }
        }

        public void RemoveComponent(EntityQueryImpl* queryImpl, ComponentType componentType)
        {
            var chunkCacheIterator = new UnsafeChunkCacheIterator(queryImpl->_Filter,
                queryImpl->_QueryData->HasEnableableComponents != 0,
                queryImpl->GetMatchingChunkCache(), queryImpl->_QueryData->MatchingArchetypes.Ptr);
            int chunkIndex = -1;
            v128 chunkEnabledMask = default;
            int maxChunkCount = chunkCacheIterator.Length;
            using var chunksToProcess = new UnsafeList<ChunkAndEnabledMask>(maxChunkCount, Allocator.TempJob);
            while (chunkCacheIterator.MoveNextChunk(ref chunkIndex, out var archetypeChunk, out int chunkEntityCount,
                       out byte useEnabledMask, ref chunkEnabledMask))
            {
                // Structural changes are not allowed while using an UnsafeChunkCacheIterator, so we just track
                // the chunks & metadata to process outside the loop.
                chunksToProcess.AddNoResize(new ChunkAndEnabledMask
                {
                    Chunk = archetypeChunk.m_Chunk,
                    EnabledMask = chunkEnabledMask,
                    ChunkEntityCount = chunkEntityCount,
                    UseEnabledMask = useEnabledMask,
                });
            }
            // Apply the structural change to all chunks
            Archetype* srcArchetype = null;
            Archetype* dstArchetype = null;
            int indexInTypeArray = 0;
            var chunkBatches = stackalloc EntityBatchInChunk[TypeManager.MaximumChunkCapacity];
            for(int iChunk=0,chunkCount=chunksToProcess.Length; iChunk<chunkCount; ++iChunk)
            {
                var chunk = chunksToProcess[iChunk].Chunk;
                var chunkArchetype = GetArchetype(chunk);
                if (Hint.Unlikely(srcArchetype != chunkArchetype))
                {
                    srcArchetype = chunkArchetype;
                    dstArchetype = GetArchetypeWithRemovedComponent(chunkArchetype, componentType, &indexInTypeArray);
                }
                if (Hint.Unlikely(dstArchetype == srcArchetype))
                    continue;
                var archetypeChunkFilter = GetArchetypeChunkFilterWithRemovedComponent(chunk, dstArchetype, indexInTypeArray, componentType);
                if (chunksToProcess[iChunk].UseEnabledMask == 0)
                {
                    // All entities in the chunk are enabled; move it en masse
                    Move(chunk, ref archetypeChunkFilter);
                }
                else
                {
                    // Populate a list of entity batches in this chunk to process
                    // Move(entity, archetypeChunkFilter) just creates a single-entity batch internally, so using batches
                    // for both branches here is fine.
                    int batchCount = 0;
                    int batchEndIndex = 0;
                    chunkEnabledMask = chunksToProcess[iChunk].EnabledMask;
                    while (EnabledBitUtility.TryGetNextRange(chunkEnabledMask, batchEndIndex, out int batchStartIndex, out batchEndIndex))
                    {
                        chunkBatches[batchCount++] = new EntityBatchInChunk
                        {
                            Chunk = chunk, Count = batchEndIndex - batchStartIndex, StartIndex = batchStartIndex
                        };
                    }
                    Assert.IsTrue(batchCount <= TypeManager.MaximumChunkCapacity);
                    // Iterate batches backwards, to avoid mutating entities we haven't processed yet
                    for (int i = batchCount - 1; i >= 0; i--)
                    {
                        Move(chunkBatches[i], ref archetypeChunkFilter);
                    }
                }
            }
        }

        public void RemoveComponents(EntityQueryImpl* queryImpl, in ComponentTypeSet componentTypeSet)
        {
            var chunkCacheIterator = new UnsafeChunkCacheIterator(queryImpl->_Filter,
                queryImpl->_QueryData->HasEnableableComponents != 0,
                queryImpl->GetMatchingChunkCache(), queryImpl->_QueryData->MatchingArchetypes.Ptr);

            int chunkIndex = -1;
            v128 chunkEnabledMask = default;
            int maxChunkCount = chunkCacheIterator.Length;
            using var chunksToProcess = new UnsafeList<ChunkAndEnabledMask>(maxChunkCount, Allocator.TempJob);
            while (chunkCacheIterator.MoveNextChunk(ref chunkIndex, out var archetypeChunk, out int chunkEntityCount,
                       out byte useEnabledMask, ref chunkEnabledMask))
            {
                // Structural changes are not allowed while using an UnsafeChunkCacheIterator, so we just track
                // the chunks & metadata to process outside the loop.
                chunksToProcess.AddNoResize(new ChunkAndEnabledMask
                {
                    Chunk = archetypeChunk.m_Chunk,
                    EnabledMask = chunkEnabledMask,
                    ChunkEntityCount = chunkEntityCount,
                    UseEnabledMask = useEnabledMask,
                });
            }
            // Apply the structural change to all chunks
            Archetype* srcArchetype = null;
            Archetype* dstArchetype = null;
            var chunkBatches = stackalloc EntityBatchInChunk[TypeManager.MaximumChunkCapacity];
            for(int iChunk=0,chunkCount=chunksToProcess.Length; iChunk<chunkCount; ++iChunk)
            {
                var chunk = chunksToProcess[iChunk].Chunk;
                var chunkArchetype = GetArchetype(chunk);
                if (Hint.Unlikely(srcArchetype != chunkArchetype))
                {
                    srcArchetype = chunkArchetype;
                    dstArchetype = GetArchetypeWithRemovedComponents(chunkArchetype, componentTypeSet);
                }
                if (Hint.Unlikely(dstArchetype == srcArchetype))
                    continue; // none were removed
                var archetypeChunkFilter = GetArchetypeChunkFilterWithRemovedComponents(chunk, dstArchetype);
                if (chunksToProcess[iChunk].UseEnabledMask == 0)
                {
                    // All entities in the chunk are enabled; move it en masse
                    Move(chunk, ref archetypeChunkFilter);
                }
                else
                {
                    // Populate a list of entity batches in this chunk to process
                    // Move(entity, archetypeChunkFilter) just creates a single-entity batch internally, so using batches
                    // for both branches here is fine.
                    int batchCount = 0;
                    int batchEndIndex = 0;
                    chunkEnabledMask = chunksToProcess[iChunk].EnabledMask;
                    while (EnabledBitUtility.TryGetNextRange(chunkEnabledMask, batchEndIndex, out int batchStartIndex, out batchEndIndex))
                    {
                        chunkBatches[batchCount++] = new EntityBatchInChunk
                        {
                            Chunk = chunk, Count = batchEndIndex - batchStartIndex, StartIndex = batchStartIndex
                        };
                    }
                    Assert.IsTrue(batchCount <= TypeManager.MaximumChunkCapacity);
                    // Iterate batches backwards, to avoid mutating entities we haven't processed yet
                    for (int i = batchCount - 1; i >= 0; i--)
                    {
                        Move(chunkBatches[i], ref archetypeChunkFilter);
                    }
                }
            }
        }

        public void AddComponent(UnsafeList<EntityBatchInChunk>* sortedEntityBatchList, ComponentType type, int existingSharedComponentIndex)
        {
            Assert.IsFalse(type.IsChunkComponent);

            // Reverse order so that batch indices do not change while iterating.
            for (int i = sortedEntityBatchList->Length - 1; i >= 0; i--)
                AddComponent(sortedEntityBatchList->Ptr[i], type, existingSharedComponentIndex);
        }

        public void AddComponents(UnsafeList<EntityBatchInChunk>* sortedEntityBatchList, in ComponentTypeSet typeSet)
        {
            Assert.IsFalse(typeSet.ChunkComponentCount > 0);

            // Reverse order so that batch indices do not change while iterating.
            for (int i = sortedEntityBatchList->Length - 1; i >= 0; i--)
                AddComponents(sortedEntityBatchList->Ptr[i], typeSet);
        }

        public void RemoveComponents(UnsafeList<EntityBatchInChunk>* sortedEntityBatchList, in ComponentTypeSet typeSet)
        {
            Assert.IsFalse(typeSet.ChunkComponentCount > 0);

            // Reverse order so that batch indices do not change while iterating.
            for (int i = sortedEntityBatchList->Length - 1; i >= 0; i--)
                RemoveComponents(sortedEntityBatchList->Ptr[i], typeSet);
        }

        public void RemoveComponent(UnsafeList<EntityBatchInChunk>* sortedEntityBatchList, ComponentType type)
        {
            Assert.IsFalse(type.IsChunkComponent);

            // Reverse order so that batch indices do not change while iterating.
            for (int i = sortedEntityBatchList->Length - 1; i >= 0; i--)
                RemoveComponent(sortedEntityBatchList->Ptr[i], type);
        }

        public void SetSharedComponentDataIndex(Entity entity, ComponentType componentType, int dstSharedComponentDataIndex)
        {
            var archetypeChunkFilter = GetArchetypeChunkFilterWithChangedSharedComponent(GetChunk(entity), componentType, dstSharedComponentDataIndex);
            if (archetypeChunkFilter.Archetype == null)
                return;

            ChunkDataUtility.SetSharedComponentDataIndex(entity, archetypeChunkFilter.Archetype, archetypeChunkFilter.SharedComponentValues, componentType.TypeIndex);
        }

        public void SetSharedComponentDataIndex(EntityQueryImpl* queryImpl, ComponentType componentType, int dstSharedComponentDataIndex)
        {
            var chunkCacheIterator = new UnsafeChunkCacheIterator(queryImpl->_Filter,
                queryImpl->_QueryData->HasEnableableComponents != 0,
                queryImpl->GetMatchingChunkCache(), queryImpl->_QueryData->MatchingArchetypes.Ptr);
            int chunkIndex = -1;
            v128 chunkEnabledMask = default;
            int maxChunkCount = chunkCacheIterator.Length;
            using var chunksToProcess = new UnsafeList<ChunkAndEnabledMask>(maxChunkCount, Allocator.TempJob);
            while (chunkCacheIterator.MoveNextChunk(ref chunkIndex, out var archetypeChunk, out int chunkEntityCount,
                       out byte useEnabledMask, ref chunkEnabledMask))
            {
                // Structural changes are not allowed while using an UnsafeChunkCacheIterator, so we just track
                // the chunks & metadata to process outside the loop.
                chunksToProcess.AddNoResize(new ChunkAndEnabledMask
                {
                    Chunk = archetypeChunk.m_Chunk,
                    EnabledMask = chunkEnabledMask,
                    ChunkEntityCount = chunkEntityCount,
                    UseEnabledMask = useEnabledMask,
                });
            }
            // Apply the structural change to all chunks
            var chunkBatches = stackalloc EntityBatchInChunk[TypeManager.MaximumChunkCapacity];
            for(int iChunk=0,chunkCount=chunksToProcess.Length; iChunk<chunkCount; ++iChunk)
            {
                var chunk = chunksToProcess[iChunk].Chunk;
                var chunkArchetype = GetArchetype(chunk);
                SetSharedComponentDataIndexForChunk(chunk, chunkArchetype, componentType, dstSharedComponentDataIndex,
                    chunksToProcess[iChunk].UseEnabledMask, chunksToProcess[iChunk].EnabledMask, chunkBatches);
            }
        }

        // Note previously called SetArchetype: SetArchetype is used internally to refer to the function which only creates the cross-reference between the
        // entity id and the archetype (m_ArchetypeByEntity). This is not "Setting" the archetype, it is moving the components to a different archetype.
        public void Move(Entity entity, Archetype* dstArchetype)
        {
            var archetypeChunkFilter = GetArchetypeChunkFilterWithChangedArchetype(GetChunk(entity), dstArchetype);
            if (archetypeChunkFilter.Archetype == null)
                return;

            Move(entity, ref archetypeChunkFilter);
        }

        public void Move(Entity entity, Archetype* archetype, SharedComponentValues sharedComponentValues)
        {
            var archetypeChunkFilter = new ArchetypeChunkFilter(archetype, sharedComponentValues);
            Move(entity, ref archetypeChunkFilter);
        }

        public void Move(ChunkIndex srcChunk, Archetype* archetype, SharedComponentValues sharedComponentValues)
        {
            var archetypeChunkFilter = new ArchetypeChunkFilter(archetype, sharedComponentValues);
            Move(srcChunk, ref archetypeChunkFilter);
        }

        public void MoveAndSetChangeVersion(EntityBatchInChunk batch, Archetype* archetype, SharedComponentValues sharedComponentValues, TypeIndex typeIndex)
        {
            var archetypeChunkFilter = new ArchetypeChunkFilter(archetype, sharedComponentValues);
            MoveAndSetChangeVersion(batch, ref archetypeChunkFilter, typeIndex);
        }

        // ----------------------------------------------------------------------------------------------------------
        // INTERNAL
        // ----------------------------------------------------------------------------------------------------------

        void Move(Entity entity, ChunkIndex dstChunk)
        {
            var srcEntityInChunk = GetEntityInChunk(entity);
            var srcChunk = srcEntityInChunk.Chunk;
            var srcChunkIndex = srcEntityInChunk.IndexInChunk;
            var entityBatch = new EntityBatchInChunk { Chunk = srcChunk, Count = 1, StartIndex = srcChunkIndex };

            Move(entityBatch, dstChunk);
        }

        void Move(Entity entity, ref ArchetypeChunkFilter archetypeChunkFilter)
        {
            var srcEntityInChunk = GetEntityInChunk(entity);
            var entityBatch = new EntityBatchInChunk
                {Chunk = srcEntityInChunk.Chunk, Count = 1, StartIndex = srcEntityInChunk.IndexInChunk};

            Move(entityBatch, ref archetypeChunkFilter);
        }

        void Move(ChunkIndex srcChunk, ref ArchetypeChunkFilter archetypeChunkFilter)
        {
            var srcArchetype = GetArchetype(srcChunk);
            if (archetypeChunkFilter.Archetype->CleanupComplete)
            {
                ChunkDataUtility.Deallocate(srcArchetype, srcChunk);
                return;
            }

            if (ChunkDataUtility.AreLayoutCompatible(srcArchetype, archetypeChunkFilter.Archetype))
            {
                fixed(int* sharedComponentValues = archetypeChunkFilter.SharedComponentValues)
                {
                    ChunkDataUtility.ChangeArchetypeInPlace(srcArchetype, srcChunk, archetypeChunkFilter.Archetype, sharedComponentValues);
                }
                return;
            }

            var entityBatch = new EntityBatchInChunk { Chunk = srcChunk, Count = srcChunk.Count, StartIndex = 0 };
            Move(entityBatch, ref archetypeChunkFilter);
        }

        void Move(EntityBatchInChunk entityBatchInChunk, ref ArchetypeChunkFilter archetypeChunkFilter)
        {
            var cleanupComplete = archetypeChunkFilter.Archetype->CleanupComplete;

            var srcChunk = entityBatchInChunk.Chunk;
            var srcRemainingCount = entityBatchInChunk.Count;
            var startIndex = entityBatchInChunk.StartIndex;

            if ((srcRemainingCount == srcChunk.Count) && cleanupComplete)
            {
                var srcArchetype = GetArchetype(srcChunk);
                ChunkDataUtility.Deallocate(srcArchetype, srcChunk);
                return;
            }

            while (srcRemainingCount > 0)
            {
                var dstChunk = GetChunkWithEmptySlots(ref archetypeChunkFilter);
                var dstCount = Move(new EntityBatchInChunk { Chunk = srcChunk, Count = srcRemainingCount, StartIndex = startIndex }, dstChunk);
                srcRemainingCount -= dstCount;
            }
        }

        // This variant is to implement SetSharedComponent(EntityBatchInChunk), where we need to update the shared component's change version
        // for all chunks that entities in the batch are moved into.
        void MoveAndSetChangeVersion(EntityBatchInChunk entityBatchInChunk, ref ArchetypeChunkFilter archetypeChunkFilter, TypeIndex typeIndex)
        {
            var cleanupComplete = archetypeChunkFilter.Archetype->CleanupComplete;

            var srcChunk = entityBatchInChunk.Chunk;
            var srcRemainingCount = entityBatchInChunk.Count;
            var startIndex = entityBatchInChunk.StartIndex;

            if ((srcRemainingCount == srcChunk.Count) && cleanupComplete)
            {
                var srcArchetype = GetArchetype(srcChunk);
                ChunkDataUtility.Deallocate(srcArchetype, srcChunk);
                return;
            }

            var dstArchetype = archetypeChunkFilter.Archetype;
            int typeIndexInDstArchetype = ChunkDataUtility.GetIndexInTypeArray(dstArchetype, typeIndex);
            Assert.AreNotEqual(-1, typeIndexInDstArchetype, "expected type not found in destination archetype");
            while (srcRemainingCount > 0)
            {
                var dstChunk = GetChunkWithEmptySlots(ref archetypeChunkFilter);
                var dstCount = Move(new EntityBatchInChunk { Chunk = srcChunk, Count = srcRemainingCount, StartIndex = startIndex }, dstChunk);
                srcRemainingCount -= dstCount;
                dstArchetype->Chunks.SetChangeVersion(typeIndexInDstArchetype, dstChunk.ListIndex,
                    GlobalSystemVersion);
            }
        }

        // ----------------------------------------------------------------------------------------------------------
        // Core, self-contained functions to change chunks. No other functions should actually move data from
        // one Chunk to another, or otherwise change the structure of a Chunk after creation.
        // ----------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Move subset of chunk data into another chunk.
        /// </summary>
        /// <remarks>
        /// Chunks can be of same archetype (but differ by shared component values).
        /// </remarks>
        /// <returns>Returns the number moved. Caller handles if less than indicated in srcBatch.</returns>
        int Move(in EntityBatchInChunk srcBatch, ChunkIndex dstChunk)
        {
            var srcChunk = srcBatch.Chunk;
            var srcArchetype = GetArchetype(srcChunk);
            var dstArchetype = GetArchetype(dstChunk);
            var dstUnusedCount = dstArchetype->ChunkCapacity - dstChunk.Count;
            var srcCount = math.min(dstUnusedCount, srcBatch.Count);
            var srcStartIndex = srcBatch.StartIndex + srcBatch.Count - srcCount;

            var partialSrcBatch = new EntityBatchInChunk
            {
                Chunk = srcChunk,
                StartIndex = srcStartIndex,
                Count = srcCount
            };

            ChunkDataUtility.Clone(srcArchetype, partialSrcBatch, dstArchetype, dstChunk);

            fixed (EntityComponentStore* store = &this)
            {
                ChunkDataUtility.Remove(store, partialSrcBatch);
            }

            return srcCount;
        }
    }
}
