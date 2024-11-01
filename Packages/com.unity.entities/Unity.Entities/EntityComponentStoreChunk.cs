using System;
using System.Threading;
using Unity.Assertions;
using Unity.Burst.CompilerServices;
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

        // ----------------------------------------------------------------------------------------------------------
        // INTERNAL
        // ----------------------------------------------------------------------------------------------------------

        internal bool IsComponentEnabled(Entity entity, TypeIndex typeIndex)
        {
            var entityInChunk = GetEntityInChunk(entity);
            var archetype = GetArchetype(entityInChunk.Chunk);
            var typeOffset = ChunkDataUtility.GetIndexInTypeArray(archetype, typeIndex);

            return IsComponentEnabled(entityInChunk.Chunk, entityInChunk.IndexInChunk, typeOffset);
        }

        internal bool IsComponentEnabled(Entity entity, TypeIndex typeIndex, ref LookupCache typeLookupCache)
        {
            var entityInChunk = GetEntityInChunk(entity);
            var archetype = GetArchetype(entityInChunk.Chunk);
            if (Hint.Unlikely(archetype != typeLookupCache.Archetype))
                typeLookupCache.Update(archetype, typeIndex);

            return IsComponentEnabled(entityInChunk.Chunk, entityInChunk.IndexInChunk, typeLookupCache.IndexInArchetype);
        }

        internal bool IsComponentEnabled(ChunkIndex chunk, int indexInChunk, int typeIndexInArchetype)
        {
            var archetype = GetArchetype(chunk);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            // the bitmask size is always 128 bits, so make sure we're not indexing outside the chunk's capacity.
            var capacity = archetype->ChunkCapacity;
            if (Hint.Unlikely(indexInChunk < 0 || indexInChunk >= capacity))
                throw new ArgumentException($"indexInChunk {indexInChunk} is outside the valid range [0..{capacity - 1}]");
#endif

            var isComponentEnabled = ChunkDataUtility.GetEnabledRefRO(chunk, archetype, typeIndexInArchetype);
            return isComponentEnabled.IsSet(indexInChunk);
        }

        internal void SetComponentEnabled(Entity entity, TypeIndex typeIndex, bool value)
        {
            var entityInChunk = GetEntityInChunk(entity);
            var archetype = GetArchetype(entityInChunk.Chunk);
            var typeOffset = ChunkDataUtility.GetIndexInTypeArray(archetype, typeIndex);

            SetComponentEnabled(entityInChunk.Chunk, entityInChunk.IndexInChunk, typeOffset, value);
        }

        internal void SetComponentEnabled(Entity entity, TypeIndex typeIndex, bool value, ref LookupCache typeLookupCache)
        {
            var entityInChunk = GetEntityInChunk(entity);
            var archetype = GetArchetype(entityInChunk.Chunk);
            if (Hint.Unlikely(archetype != typeLookupCache.Archetype))
                typeLookupCache.Update(archetype, typeIndex);

            SetComponentEnabled(entityInChunk.Chunk, entityInChunk.IndexInChunk, typeLookupCache.IndexInArchetype, value);
        }

        internal void SetComponentEnabled(ChunkIndex chunk, int indexInChunk, int typeIndexInArchetype, bool value)
        {
            var archetype = GetArchetype(chunk);
            var capacity = archetype->ChunkCapacity;

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            // the bit array size is padded up to 64 bits, so we validate we're not indexing outside the valid data.
            if (Hint.Unlikely(indexInChunk < 0 || indexInChunk >= capacity))
                throw new ArgumentException($"indexInChunk {indexInChunk} is outside the valid range [0..{capacity - 1}]");
#endif

            var bits = ChunkDataUtility.GetEnabledRefRW(chunk, archetype, typeIndexInArchetype, GlobalSystemVersion, out var ptrChunkDisabledCount);
            var numStridesIntoBits = (indexInChunk / 64);
            var pBits = bits.Ptr + numStridesIntoBits;
            var indexInPBits = indexInChunk - (numStridesIntoBits * 64);
            var mask = 1L << indexInPBits;

            var oldBits = (long)*pBits;
            var newBits = 0L;
            var expectedOldBits = 0L;

            do
            {
                newBits = math.select(oldBits & ~mask, oldBits | mask, value);
                expectedOldBits = oldBits;
                oldBits = Interlocked.CompareExchange(ref UnsafeUtility.AsRef<long>(pBits), newBits, expectedOldBits);
            } while (expectedOldBits != oldBits);

            if (oldBits == newBits)
                return;

            // do we need increment or decrement?
            var adjustment = math.select(1, -1, value);
            Interlocked.Add(ref UnsafeUtility.AsRef<int>(ptrChunkDisabledCount), adjustment);
        }

        /// <summary>
        /// Get a pointer to the component type index enabled bits of the chunk the entity is part of.
        /// Also returns a pointer to the chunk's disable counter.
        /// Will update the chunk version
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="typeIndex"></param>
        /// <param name="typeLookupCache"></param>
        /// <param name="globalSystemVersion"></param>
        /// <param name="indexInBitField"></param>
        /// <param name="ptrChunkDisabledCount"></param>
        /// <returns></returns>
        public ulong* GetEnabledRawRW(Entity entity, TypeIndex typeIndex, ref LookupCache typeLookupCache, uint globalSystemVersion, out int indexInBitField, out int* ptrChunkDisabledCount)
        {
            AssertEntityHasComponent(entity, typeIndex, ref typeLookupCache);
            var entityInChunk = GetEntityInChunk(entity);
            var archetype = GetArchetype(entityInChunk.Chunk);
            indexInBitField = entityInChunk.IndexInChunk;
            if (Hint.Unlikely(archetype != typeLookupCache.Archetype))
                typeLookupCache.Update(archetype, typeIndex);
            return ChunkDataUtility.GetEnabledRefRW(entityInChunk.Chunk, archetype, typeLookupCache.IndexInArchetype, globalSystemVersion, out ptrChunkDisabledCount).Ptr;
        }

        /// <summary>
        /// Get a pointer to the component type index enabled bits of the chunk the entity is part of.
        /// Also returns a pointer to the chunk's disable counter
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="typeIndex"></param>
        /// <param name="typeLookupCache"></param>
        /// <param name="indexInBitField"></param>
        /// <param name="ptrChunkDisabledCount"></param>
        /// <returns></returns>
        public ulong* GetEnabledRawRO(Entity entity, TypeIndex typeIndex, ref LookupCache typeLookupCache, out int indexInBitField, out int* ptrChunkDisabledCount)
        {
            AssertEntityHasComponent(entity, typeIndex, ref typeLookupCache);
            var entityInChunk = GetEntityInChunk(entity);
            var archetype = GetArchetype(entityInChunk.Chunk);
            indexInBitField = entityInChunk.IndexInChunk;
            if (Hint.Unlikely(archetype != typeLookupCache.Archetype))
                typeLookupCache.Update(archetype, typeIndex);
            int memoryOrderIndexInArchetype = archetype->TypeIndexInArchetypeToMemoryOrderIndex[typeLookupCache.IndexInArchetype];
            ptrChunkDisabledCount = archetype->Chunks.GetPointerToChunkDisabledCountForType(memoryOrderIndexInArchetype, entityInChunk.Chunk.ListIndex);
            return ChunkDataUtility.GetEnabledRefRO(entityInChunk.Chunk, archetype, typeLookupCache.IndexInArchetype).Ptr;
        }

        //                              | ChangeVersion | OrderVersion |
        // -----------------------------|---------------|--------------|
        // Remove(EntityBatchInChunk)   | NO            | YES          |

        // Write Component to Chunk     | YES           | NO           |
        // Remove Component In-Place    | NO            | NO           |
        // Add Entities in Chunk        | YES           | YES          |
        // Add Component In-Place       | YES           | NO           |
        // Move Chunk World             | YES           | YES          |
        //
        // ChangeVersion : e.g. Should I update LocalToWorld from Translation?
        // OrderVersion : e.g. Should I re-allocate a lookaside cache based on chunk data?


        internal void AllocateEntities(Archetype* arch, ChunkIndex chunk, int baseIndex, int count, Entity* outputEntities)
        {
            Assert.AreEqual(arch->Offsets[0], 0);
            Assert.AreEqual(arch->SizeOfs[0], sizeof(Entity));

            var entityInChunkStart = (Entity*)chunk.Buffer + baseIndex;

#if !ENTITY_STORE_V1
            s_entityStore.Data.AllocateEntities(entityInChunkStart, count, chunk, baseIndex);

            if (outputEntities != null)
            {
                UnsafeUtility.MemCpy(outputEntities, entityInChunkStart, count * sizeof(Entity));
            }
#else
            for (var i = 0; i != count; i++)
            {
                var entityIndexInChunk = m_EntityInChunkByEntity[m_NextFreeEntityIndex].IndexInChunk;
                if (entityIndexInChunk == -1)
                {
                    IncreaseCapacity();
                    entityIndexInChunk = m_EntityInChunkByEntity[m_NextFreeEntityIndex].IndexInChunk;
                }

                var entityVersion = m_VersionByEntity[m_NextFreeEntityIndex];

                if (outputEntities != null)
                {
                    outputEntities[i].Index = m_NextFreeEntityIndex;
                    outputEntities[i].Version = entityVersion;
                }

                var entityInChunk = entityInChunkStart + i;

                entityInChunk->Index = m_NextFreeEntityIndex;
                entityInChunk->Version = entityVersion;

                m_EntityInChunkByEntity[m_NextFreeEntityIndex].IndexInChunk = baseIndex + i;
                m_EntityInChunkByEntity[m_NextFreeEntityIndex].Chunk = chunk;
#if !DOTS_DISABLE_DEBUG_NAMES
                m_NameByEntity[m_NextFreeEntityIndex] = new EntityName();
#endif

                m_NextFreeEntityIndex = entityIndexInChunk;
                m_EntityCreateDestroyVersion++;
            }
#endif
        }

        internal void DeallocateDataEntitiesInChunk(ChunkIndex chunk, Archetype* archetype, int indexInChunk, int batchCount)
        {
            DeallocateBuffers(chunk, indexInChunk, batchCount);
            DeallocateManagedComponents(chunk, indexInChunk, batchCount);

            var entities = (Entity*)chunk.Buffer + indexInChunk;

#if ENTITY_STORE_V1
            var freeIndex = m_NextFreeEntityIndex;

            for (var i = batchCount - 1; i >= 0; --i)
            {
                var entityIndex = entities[i].Index;

                m_EntityInChunkByEntity[entityIndex].Chunk = ChunkIndex.Null;
                m_VersionByEntity[entityIndex]++;
                m_EntityInChunkByEntity[entityIndex].IndexInChunk = freeIndex;
#if !DOTS_DISABLE_DEBUG_NAMES
                m_NameByEntity[entityIndex] = new EntityName();
#endif

                freeIndex = entityIndex;
            }

            m_NextFreeEntityIndex = freeIndex;
            m_EntityCreateDestroyVersion++;
#else
            s_entityStore.Data.DeallocateEntities(entities, batchCount);
#endif


            // Compute the number of things that need to moved and patched.
            int patchCount = Math.Min(batchCount, chunk.Count - indexInChunk - batchCount);

            ChunkDataUtility.RemoveFromEnabledBitsHierarchicalData(chunk, archetype, indexInChunk, batchCount);
            if (0 == patchCount)
            {
                // if we're not patching, we still need to clear the padding bits for the entities we destroyed
                ChunkDataUtility.ClearPaddingBits(chunk, archetype, indexInChunk, batchCount);
                return;
            }

            // updates indexInChunk to point to where the components will be moved to
            //Assert.IsTrue(chunk->archetype->sizeOfs[0] == sizeof(Entity) && chunk->archetype->offsets[0] == 0);
            var movedEntities = (Entity*)chunk.Buffer + (chunk.Count - patchCount);
            for (var i = 0; i != patchCount; i++)
            {
#if ENTITY_STORE_V1
                m_EntityInChunkByEntity[movedEntities[i].Index].IndexInChunk = indexInChunk + i;
#else
                var entityInChunk = GetEntityInChunk(movedEntities[i]);
                entityInChunk.IndexInChunk = indexInChunk + i;
                SetEntityInChunk(movedEntities[i], entityInChunk);
#endif
            }

            // Move component data from the end to where we deleted components
            var startIndex = chunk.Count - patchCount;

            fixed (EntityComponentStore* store = &this)
            {
                ChunkDataUtility.Copy(store, chunk, startIndex, chunk, indexInChunk, patchCount);
            }

            ChunkDataUtility.CloneEnabledBits(chunk, archetype, startIndex, chunk, archetype, indexInChunk, patchCount);
            var clearStartIndex = chunk.Count - batchCount;
            ChunkDataUtility.ClearPaddingBits(chunk, archetype, clearStartIndex, batchCount);
        }

        void DeallocateBuffers(ChunkIndex chunk, int indexInChunk, int batchCount)
        {
            var archetype = GetArchetype(chunk);
            var chunkBuffer = chunk.Buffer;

            for (int ti = 0, count = archetype->TypesCount; ti < count; ++ti)
            {
                var type = archetype->Types[ti];

                if (!type.IsBuffer)
                    continue;

                var basePtr = chunkBuffer + archetype->Offsets[ti];
                var stride = archetype->SizeOfs[ti];

                for (int i = 0; i < batchCount; ++i)
                {
                    byte* bufferPtr = basePtr + stride * (indexInChunk + i);
                    BufferHeader.Destroy((BufferHeader*)bufferPtr);
                }
            }
        }
    }
}
