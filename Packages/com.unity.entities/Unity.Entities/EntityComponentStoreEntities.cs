using System;
using System.Diagnostics;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace Unity.Entities
{
    unsafe partial struct EntityComponentStore
    {
        internal static readonly SharedStatic<EntityStore> s_entityStore = SharedStatic<EntityStore>.GetOrCreate<EntityStore.BurstStaticIdentifier>();

        internal struct EntityStore : IDisposable
        {
            internal struct BurstStaticIdentifier { }

            struct DataBlock
            {
                // 8k entities per DataBlock
                public fixed ulong allocated[k_EntitiesInBlock / 64];
                public fixed ulong entityInChunk[k_EntitiesInBlock];
                public fixed int versions[k_EntitiesInBlock];
#if !DOTS_DISABLE_DEBUG_NAMES
                public fixed int nameByEntityIndex[k_EntitiesInBlock];
#endif
            }

            const int k_EntitiesInBlock = 8192;
#if !DOTS_DISABLE_DEBUG_NAMES
            const int k_BlockSize = k_EntitiesInBlock / 8 + k_EntitiesInBlock * 16;
#else
            const int k_BlockSize = k_EntitiesInBlock / 8 + k_EntitiesInBlock * 12;
#endif
            const int k_BlockCount = 16384;
            const int k_BlockBusy = -1;
            internal const int MaximumTheoreticalAmountOfEntities = k_EntitiesInBlock * k_BlockCount;

            // 16k pointers, each allocation containing data for 8k entities
            fixed ulong m_DataBlocks[k_BlockCount];
            fixed int m_EntityCount[k_BlockCount];

            public void IntegrityCheck(int blockIndex)
            {
                // It is assumed that integrity check is performed on a stable state.
                // In other words, no potential concurrent access.

                var block = (DataBlock*)m_DataBlocks[blockIndex];

                if(block == null)
                {
                    Assert.AreEqual(0, m_EntityCount[blockIndex]);

                    if(m_EntityCount[blockIndex] != 0)
                    {
                        Debug.Log($"block index {blockIndex} pointer {(ulong)block:X16} count {m_EntityCount[blockIndex]}");
                    }

                    return;
                }

                var allocated = block->allocated;
                var versions = block->versions;

                for (int i = 0; i < k_EntitiesInBlock; ++i)
                {
                    var aliveA = (allocated[i / 64] >> (i % 64)) & 1;
                    var aliveB = (ulong)versions[i] & 1;
                    Assert.AreEqual(aliveA, aliveB);
                }
            }

            public void IntegrityCheck()
            {
                Assert.AreEqual(k_BlockSize, sizeof(DataBlock));

                for (int i = 0; i < k_BlockCount; i++)
                {
                    IntegrityCheck(i);
                }
            }

            public int DebugOnlyThreadUnsafeEntityCount
            {
                get
                {
                    int count = 0;

                    for (int i = 0; i < k_BlockCount; i++)
                    {
                        count += m_EntityCount[i];
                    }

                    return count;
                }
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
            void DebugOnlyThrowIfEntityDoesntExist(Entity entity, DataBlock* block, int indexInBlock)
            {
                bool MissingInBitmask()
                {
                    var bitfield = block->allocated[indexInBlock / 64];
                    var mask = 1UL << (indexInBlock % 64);

                    return (bitfield & mask) == 0;
                }

                if (block == null || MissingInBitmask())
                {
                    throw new ArgumentException(
                        "All entities passed to EntityManager must exist. One of the entities has already been destroyed or was never created. " +
                        AppendDestroyedEntityRecordError(entity));
                }
            }

            internal Entity GetEntityByEntityIndex(int index)
            {
                var blockIndex = index / k_EntitiesInBlock;
                var indexInBlock = index % k_EntitiesInBlock;

                var block = (DataBlock*)m_DataBlocks[blockIndex];
                if (block == null) return Entity.Null;

                var version = block->versions[indexInBlock];
                return ((uint)version & 1) == 0 ? Entity.Null : new Entity { Index = index, Version = version };
            }

            internal bool Exists(Entity entity)
            {
                var blockIndex = entity.Index / k_EntitiesInBlock;
                var indexInBlock = entity.Index % k_EntitiesInBlock;

                var block = (DataBlock*)m_DataBlocks[blockIndex];
                if (block == null) return false;

                if (((uint)entity.Version & 1) == 0 || block->versions[indexInBlock] != entity.Version) return false;
                return true;
            }

            public void SetEntityInChunk(Entity entity, EntityInChunk entityInChunk)
            {
                var blockIndex = entity.Index / k_EntitiesInBlock;
                var indexInBlock = entity.Index % k_EntitiesInBlock;

                var block = (DataBlock*)m_DataBlocks[blockIndex];

                DebugOnlyThrowIfEntityDoesntExist(entity, block, indexInBlock);

                ((EntityInChunk*)block->entityInChunk)[indexInBlock] = entityInChunk;
            }

            public void SetEntityVersion(Entity entity, int version)
            {
                // TODO - find a way to remove this function.
                // Changing the version is potentially dangerous but currently required for deserialization.

                var blockIndex = entity.Index / k_EntitiesInBlock;
                var indexInBlock = entity.Index % k_EntitiesInBlock;

                var block = (DataBlock*)m_DataBlocks[blockIndex];

                DebugOnlyThrowIfEntityDoesntExist(entity, block, indexInBlock);

                block->versions[indexInBlock] = version;
            }

            public EntityInChunk GetEntityInChunk(Entity entity)
            {
                var blockIndex = entity.Index / k_EntitiesInBlock;
                var indexInBlock = entity.Index % k_EntitiesInBlock;

                var block = (DataBlock*)m_DataBlocks[blockIndex];
                if (block == null) return EntityInChunk.Null;

                var bitfield = block->allocated[indexInBlock / 64];
                var mask = 1UL << (indexInBlock % 64);

                if ((bitfield & mask) == 0) return EntityInChunk.Null;

                return ((EntityInChunk*)block->entityInChunk)[indexInBlock];
            }

            internal void AllocateEntities(NativeArray<Entity> entities)
            {
                AllocateEntities((Entity*)entities.GetUnsafePtr(), entities.Length, ChunkIndex.Null, 0);
            }

            internal void AllocateEntities(Entity* entities, int entityCount)
            {
                AllocateEntities(entities, entityCount, ChunkIndex.Null, 0);
            }

            internal void AllocateEntities(Entity* entities, int totalCount, ChunkIndex chunkIndex, int firstEntityInChunkIndex)
            {
                var entityInChunkIndex = firstEntityInChunkIndex;

                for (int i = 0; i < k_BlockCount; i++)
                {
                    var blockCount = Interlocked.Add(ref m_EntityCount[i], 0);

                    if (blockCount == k_BlockBusy || blockCount == k_EntitiesInBlock)
                    {
                        continue;
                    }

                    var blockAvailable = k_EntitiesInBlock - blockCount;
                    var count = math.min(blockAvailable, totalCount);

                    // Set the count to a flag indicating that this block is busy (-1)
                    var before = Interlocked.CompareExchange(ref m_EntityCount[i], k_BlockBusy, blockCount);

                    if (before != blockCount)
                    {
                        // Another thread is messing around with this block, it's either busy or was changed
                        // between the time we read the count and now. In both cases, let's keep looking.
                        continue;
                    }

                    DataBlock* block = (DataBlock*)m_DataBlocks[i];

                    // Be careful that the block might exist even if the count is zero, checking the pointer
                    // for null is the only valid way to tell if the block exists or not.
                    if (block == null)
                    {
                        block = (DataBlock*)Memory.Unmanaged.Allocate(k_BlockSize, 8, Allocator.Persistent);
                        UnsafeUtility.MemClear(block, k_BlockSize);
                        m_DataBlocks[i] = (ulong)block;
                    }

                    int remainingCount = math.min(blockAvailable, count);
                    var allocated = block->allocated;
                    var versions = block->versions;
                    var entityInChunk = block->entityInChunk;
                    var baseEntityIndex = i * k_EntitiesInBlock;

                    while (remainingCount > 0)
                    {
                        for (int maskIndex = 0; maskIndex < k_EntitiesInBlock / 64; maskIndex++)
                        {
                            if (allocated[maskIndex] != ~0UL)
                            {
                                // There is some space in this one

                                for (int entity = 0; entity < 64; entity++)
                                {
                                    var indexInBlock = maskIndex * 64 + entity;
                                    var mask = 1UL << (indexInBlock % 64);

                                    if ((allocated[maskIndex] & mask) == 0)
                                    {
                                        allocated[maskIndex] |= mask;

                                        *entities = new Entity
                                        {
                                            Index = baseEntityIndex + indexInBlock,
                                            Version = versions[indexInBlock] += 1
                                        };

                                        if (chunkIndex != ChunkIndex.Null)
                                        {
                                            ((EntityInChunk*)entityInChunk)[indexInBlock] = new EntityInChunk
                                            {
                                                Chunk = chunkIndex,
                                                IndexInChunk = entityInChunkIndex,
                                            };
                                        }
                                        else
                                        {
                                            entityInChunk[indexInBlock] = 0;
                                        }

                                        entities++;
                                        entityInChunkIndex++;
                                        remainingCount--;

                                        if (remainingCount == 0)
                                        {
                                            break;
                                        }
                                    }
                                }

                                if (remainingCount == 0)
                                {
                                    break;
                                }
                            }
                        }
                    }

                    Assert.AreEqual(0, remainingCount);

                    var resultCheck = Interlocked.CompareExchange(ref m_EntityCount[i], blockCount + count, k_BlockBusy);

                    Assert.AreEqual(resultCheck, k_BlockBusy);

                    totalCount -= count;

                    if (totalCount == 0)
                    {
                        return;
                    }
                }

                throw new InvalidOperationException("Could not find a data block for entity allocation.");
            }

            internal void DeallocateEntities(NativeArray<Entity> entities)
            {
                DeallocateEntities((Entity*)entities.GetUnsafePtr(), entities.Length);
            }

            internal void DeallocateEntities(Entity* entities, int count)
            {
                for (int i = 0; i < count;)
                {
                    int rangeStart = i;
                    int startIndex = entities[i].Index;

                    int prevIndexInBlock = startIndex % k_EntitiesInBlock;
                    int blockIndex = startIndex / k_EntitiesInBlock;

                    for (i++; i < count; i++)
                    {
                        if (entities[i].Index / k_EntitiesInBlock != blockIndex)
                        {
                            // Different data block
                            break;
                        }

                        int indexInBlock = entities[i].Index % k_EntitiesInBlock;
                        if (indexInBlock != prevIndexInBlock + 1)
                        {
                            // Same data block, but not the next entity in range
                            break;
                        }

                        prevIndexInBlock = indexInBlock;
                    }

                    int rangeEnd = i;
                    int endIndex = startIndex + (rangeEnd - rangeStart);
                    int blockCount = Interlocked.Add(ref m_EntityCount[blockIndex], 0);

                    if (blockCount == 0)
                    {
                        // Looks like this block has been already deallocated.
                        // We are trying to deallocate entities which are already gone, skip.
                        continue;
                    }

                    while (true)
                    {
                        if (blockCount != k_BlockBusy)
                        {
                            // Set the count to a flag indicating that this block is busy (-1)
                            var before = Interlocked.CompareExchange(ref m_EntityCount[blockIndex], k_BlockBusy, blockCount);

                            if (before == blockCount)
                            {
                                // Exchange succeeded
                                break;
                            }

                            blockCount = before;
                        }
                        else
                        {
                            blockCount = Interlocked.Add(ref m_EntityCount[blockIndex], 0);
                        }
                    }

                    if (blockCount == 0)
                    {
                        // This is very unlikely, but the block has been deallocated while we were waiting for it.
                        // Same as the test above, skip the block. But don't forget to restore the count.
                        var resultCheck = Interlocked.CompareExchange(ref m_EntityCount[i], 0, k_BlockBusy);
                        Assert.AreEqual(resultCheck, k_BlockBusy);

                        continue;
                    }

                    var block = (DataBlock*)m_DataBlocks[blockIndex];

                    // It would be tempting to check to immediately check if deallocation would bring the entity count
                    // for the data block to zero and deallocate the whole block. Unfortunately, in the eventuality that
                    // we are trying to deallocate an entity which was already deallocated, this could lead to
                    // discarding the data used by allocated entities. So we need to take the slow route and process
                    // the data block even if we end up getting rid of it immediately after.

                    var allocated = block->allocated;
                    var versions = block->versions;

                    for (int j = startIndex, indexInEntitiesArray = rangeStart; j < endIndex; j++, indexInEntitiesArray++)
                    {
                        var indexInBlock = j % k_EntitiesInBlock;

                        if (versions[indexInBlock] == entities[indexInEntitiesArray].Version)
                        {
                            // Matching versions confirm that we are deallocating the intended entity

                            var mask = 1UL << (indexInBlock % 64);

                            versions[indexInBlock]++;
                            allocated[indexInBlock / 64] &= ~0UL ^ mask;

#if !DOTS_DISABLE_DEBUG_NAMES
                            block->nameByEntityIndex[indexInBlock] = default;
#endif

                            blockCount--;
                        }
                    }

                    // Do not deallocate the block even if it's empty. Versions should be preserved.

                    {
                        var resultCheck = Interlocked.CompareExchange(ref m_EntityCount[blockIndex], blockCount, k_BlockBusy);
                        Assert.AreEqual(resultCheck, k_BlockBusy);
                    }
                }
            }

            public void Dispose()
            {
                for (int i = 0; i < k_BlockCount; ++i)
                {
                    var block = (void*)m_DataBlocks[i];
                    if (block != null)
                    {
                        Memory.Unmanaged.Free(block, Allocator.Persistent);
                    }
                }

                this = default;
            }

#if !DOTS_DISABLE_DEBUG_NAMES
            internal EntityName GetEntityName(Entity entity)
            {
                return GetEntityName(entity.Index);
            }

            internal EntityName GetEntityName(int index)
            {
                var blockIndex = index / k_EntitiesInBlock;
                var indexInBlock = index % k_EntitiesInBlock;

                var block = (DataBlock*)m_DataBlocks[blockIndex];
                if (block == null) return default;

                var bitfield = block->allocated[indexInBlock / 64];
                var mask = 1UL << (indexInBlock % 64);

                if ((bitfield & mask) == 0) return default;

                var nameIndex = block->nameByEntityIndex[indexInBlock];
                return new EntityName { Index = nameIndex };
            }

            public void SetEntityName(Entity entity, EntityName name)
            {
                var blockIndex = entity.Index / k_EntitiesInBlock;
                var indexInBlock = entity.Index % k_EntitiesInBlock;

                var block = (DataBlock*)m_DataBlocks[blockIndex];

                DebugOnlyThrowIfEntityDoesntExist(entity, block, indexInBlock);

                block->nameByEntityIndex[indexInBlock] = name.Index;
            }
#endif
        }
    }
}
