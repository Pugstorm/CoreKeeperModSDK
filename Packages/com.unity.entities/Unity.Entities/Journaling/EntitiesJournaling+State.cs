#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.LowLevel;
using Unity.Entities.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.Entities
{
    partial class EntitiesJournaling
    {
        /// <summary>
        /// Journaling state data container.
        /// </summary>
        unsafe struct JournalingState : IDisposable
        {
            // One buffer is allocated per EntityComponentStore
            const int k_SystemVersionBufferSize = 1024 * 1024;

            struct SystemVersionHandle
            {
                public uint Version;
                public SystemHandle Handle;
            }

            struct SystemVersionNode
            {
                public EntityComponentStore* Store;
                public UnsafeCircularBuffer<SystemVersionHandle>* Buffer;
                public SystemVersionNode* NextNode;
            };

            UnsafeCircularBuffer<Record> m_Records;
            UnsafeCircularBuffer<byte> m_Buffer;
            SystemVersionNode* m_SystemVersionNodes;
            SystemVersionNode* m_LastSystemVersionNode;
            SpinLock m_Lock;
            ulong m_RecordIndex;

            internal JournalingState(int capacityInBytes)
            {
                var ratio = (float)sizeof(Record) / sizeof(Header);
                var recordsCapacity = 1 << math.floorlog2((int)(capacityInBytes * ratio) / sizeof(Record)); // previous power of 2
                var bufferCapacity = capacityInBytes - (recordsCapacity * sizeof(Record));
                m_Records = new UnsafeCircularBuffer<Record>(recordsCapacity, Allocator.Persistent);
                m_Buffer = new UnsafeCircularBuffer<byte>(bufferCapacity, Allocator.Persistent);
                m_SystemVersionNodes = null;
                m_LastSystemVersionNode = null;
                m_Lock = new SpinLock();
                m_RecordIndex = 0;
            }

            internal int RecordCount => m_Records.Count;
            internal ulong RecordIndex => m_RecordIndex;
            internal ulong UsedBytes => (ulong)((float)m_Buffer.Count / m_Buffer.Capacity * AllocatedBytes);
            internal ulong AllocatedBytes => ((ulong)m_Records.Capacity * (ulong)sizeof(Record)) + (ulong)m_Buffer.Capacity;

            public void Dispose()
            {
                var node = m_SystemVersionNodes;
                while (node != null)
                {
                    node->Buffer->Dispose();
                    AllocatorManager.Free(Allocator.Persistent, node->Buffer);

                    var nextNode = node->NextNode;
                    AllocatorManager.Free(Allocator.Persistent, node);
                    node = nextNode;
                }
                m_SystemVersionNodes = null;
                m_LastSystemVersionNode = null;
                m_Buffer.Dispose();
                m_Records.Dispose();
                m_RecordIndex = 0;
            }

            internal RecordViewArray GetRecords(Ordering ordering, bool blocking)
            {
                var locked = m_Lock.TryAcquire(blocking);
                try
                {
                    if (!locked)
                        throw new InvalidOperationException("Record buffer is currently locked for write.");

                    var bufferFrontIndex = m_Buffer.FrontIndex;
                    if (bufferFrontIndex != 0)
                    {
                        // Unwind circular buffers for linear access
                        m_Records.Unwind();
                        m_Buffer.Unwind();

                        // Patch records position after unwind
                        var sizeOf = UnsafeUtility.SizeOf<Record>();
                        for (var i = 0; i < m_Records.Count; ++i)
                        {
                            var recordPtr = m_Records.Ptr + i;
                            var position = recordPtr->Position - bufferFrontIndex;
                            if (position < 0)
                                position = m_Buffer.Capacity + position;
                            *recordPtr = new Record(position, recordPtr->Length);
                        }
                    }
                    return new RecordViewArray(m_RecordIndex, in m_Records, in m_Buffer, ordering);
                }
                finally
                {
                    if (locked)
                        m_Lock.Release();
                }
            }

            internal void PushBack(EntityComponentStore* store, uint version, in SystemHandle handle)
            {
                // 0 is reserved for systems that never run
                if (version == 0)
                    return;

                m_Lock.Acquire();
                try
                {
                    // Get the buffer for this entity component store
                    var buffer = GetSystemVersionBuffer(store);
                    if (buffer == null)
                    {
                        UnityEngine.Debug.LogError($"EntitiesJournaling: Failed to allocate system version buffer.");
                        return;
                    }

                    // Make space if buffer is full
                    if (buffer->IsFull)
                        buffer->PopFront();

                    // Add system version handle to the buffer
                    if (!buffer->PushBack(new SystemVersionHandle { Version = version, Handle = handle }))
                        UnityEngine.Debug.LogError($"EntitiesJournaling: Failed to push back system version in buffer.");
                }
                finally
                {
                    m_Lock.Release();
                }
            }

            internal void PushBack(RecordType recordType, ulong worldSequenceNumber, in SystemHandle executingSystem, in SystemHandle originSystem, Entity* entities, int entityCount, TypeIndex* types, int typeCount, void* data, int dataLength)
            {
                m_Lock.Acquire();
                try
                {
                    if (!PushBackRecord(recordType, entityCount, typeCount, dataLength))
                        return;

                    PushBackHeader(recordType, worldSequenceNumber, in executingSystem, in originSystem, entityCount, typeCount, dataLength);
                    PushBackEntities(entities, entityCount);
                    PushBackTypes(types, typeCount);
                    PushBackData(data, dataLength);
                }
                finally
                {
                    m_Lock.Release();
                }
            }

            internal void PushBack(RecordType recordType, ulong worldSequenceNumber, in SystemHandle executingSystem, in SystemHandle originSystem, ArchetypeChunk* chunks, int chunkCount, TypeIndex* types, int typeCount, void* data, int dataLength)
            {
                var entityCount = GetEntityCount(chunks, chunkCount);

                m_Lock.Acquire();
                try
                {
                    if (!PushBackRecord(recordType, entityCount, typeCount, dataLength))
                        return;

                    PushBackHeader(recordType, worldSequenceNumber, in executingSystem, in originSystem, entityCount, typeCount, dataLength);
                    PushBackEntities(chunks, chunkCount);
                    PushBackTypes(types, typeCount);
                    PushBackData(data, dataLength);
                }
                finally
                {
                    m_Lock.Release();
                }
            }

            internal void PushBack(RecordType recordType, ulong worldSequenceNumber, in SystemHandle executingSystem, in SystemHandle originSystem, Archetype* archetype, ChunkIndex chunk, TypeIndex* types, int typeCount, void* data, int dataLength)
            {
                var entityCount = chunk.Count;

                m_Lock.Acquire();
                try
                {
                    if (!PushBackRecord(recordType, entityCount, typeCount, dataLength))
                        return;

                    PushBackHeader(recordType, worldSequenceNumber, in executingSystem, in originSystem, entityCount, typeCount, dataLength);
                    PushBackEntities(archetype, chunk);
                    PushBackTypes(types, typeCount);
                    PushBackData(data, dataLength);
                }
                finally
                {
                    m_Lock.Release();
                }
            }

            internal void PushBack(RecordType recordType, EntityComponentStore* store, uint version, in SystemHandle originSystem, Entity* entities, int entityCount, TypeIndex* types, int typeCount, void* data, int dataLength)
            {
                m_Lock.Acquire();
                try
                {
                    if (!PushBackRecord(recordType, entityCount, typeCount, dataLength))
                        return;

                    var executingSystem = GetSystemHandle(store, version);
                    PushBackHeader(recordType, store->WorldSequenceNumber, in executingSystem, in originSystem, entityCount, typeCount, dataLength);
                    PushBackEntities(entities, entityCount);
                    PushBackTypes(types, typeCount);
                    PushBackData(data, dataLength);
                }
                finally
                {
                    m_Lock.Release();
                }
            }

            internal void PushBack(RecordType recordType, EntityComponentStore* store, uint version, in SystemHandle originSystem, ArchetypeChunk* chunks, int chunkCount, TypeIndex* types, int typeCount, void* data, int dataLength)
            {
                var entityCount = GetEntityCount(chunks, chunkCount);

                m_Lock.Acquire();
                try
                {
                    if (!PushBackRecord(recordType, entityCount, typeCount, dataLength))
                        return;

                    var executingSystem = GetSystemHandle(store, version);
                    PushBackHeader(recordType, store->WorldSequenceNumber, in executingSystem, in originSystem, entityCount, typeCount, dataLength);
                    PushBackEntities(chunks, chunkCount);
                    PushBackTypes(types, typeCount);
                    PushBackData(data, dataLength);
                }
                finally
                {
                    m_Lock.Release();
                }
            }

            internal void PushBack(RecordType recordType, EntityComponentStore* store, uint version, in SystemHandle originSystem, Archetype* archetype, ChunkIndex chunk, TypeIndex* types, int typeCount, void* data, int dataLength)
            {
                var entityCount = chunk.Count;

                m_Lock.Acquire();
                try
                {
                    if (!PushBackRecord(recordType, entityCount, typeCount, dataLength))
                        return;

                    var executingSystem = GetSystemHandle(store, version);
                    PushBackHeader(recordType, store->WorldSequenceNumber, in executingSystem, in originSystem, entityCount, typeCount, dataLength);
                    PushBackEntities(archetype, chunk);
                    PushBackTypes(types, typeCount);
                    PushBackData(data, dataLength);
                }
                finally
                {
                    m_Lock.Release();
                }
            }

            internal void Clear()
            {
                m_Lock.Acquire();
                try
                {
                    var node = m_SystemVersionNodes;
                    while (node != null)
                    {
                        node->Buffer->Clear();
                        node = node->NextNode;
                    }
                    m_Records.Clear();
                    m_Buffer.Clear();
                    m_RecordIndex = 0;
                }
                finally
                {
                    m_Lock.Release();
                }
            }

            internal void ClearSystemVersionBuffers()
            {
                m_Lock.Acquire();
                try
                {
                    var node = m_SystemVersionNodes;
                    while (node != null)
                    {
                        node->Buffer->Clear();
                        node = node->NextNode;
                    }
                }
                finally
                {
                    m_Lock.Release();
                }
            }

            bool PushBackRecord(RecordType recordType, int entityCount, int typeCount, int dataLength)
            {
                // Verify payload size can fit in buffer
                var length = sizeof(Header) + (sizeof(Entity) * entityCount) + (sizeof(int) * typeCount) + dataLength;
                if (length > m_Buffer.Capacity)
                {
                    UnityEngine.Debug.LogError($"EntitiesJournaling: Cannot store {recordType} event, buffer not large enough. Increase entities journaling total memory in preferences.");
                    return false;
                }
                else if (length == m_Buffer.Capacity)
                {
                    m_Records.Clear();
                    m_Buffer.Clear();
                }

                // Verify we can fit the new record and its payload in buffer
                var record = new Record(m_Buffer.BackIndex, length);
                if (m_Records.IsFull || (m_Buffer.Capacity - m_Buffer.Count < record.Length))
                {
                    // Doesn't fit, remove old records
                    var recordCount = 0;
                    var bytesCount = 0;
                    for (var i = 0; i < m_Records.Count; ++i)
                    {
                        recordCount++;
                        bytesCount += m_Records[i].Length;
                        if (m_Buffer.Capacity - (m_Buffer.Count - bytesCount) >= record.Length)
                            break;
                    }

                    if (!m_Records.PopFront(recordCount))
                    {
                        UnityEngine.Debug.LogError($"EntitiesJournaling: Failed to pop front {recordCount} records.");
                        return false;
                    }

                    if (!m_Buffer.PopFront(bytesCount))
                    {
                        UnityEngine.Debug.LogError($"EntitiesJournaling: Failed to pop front {bytesCount} bytes in buffer.");
                        return false;
                    }
                }

                // Push back the new record (it should never fail)
                if (!m_Records.PushBack(record))
                    UnityEngine.Debug.LogError($"EntitiesJournaling: Failed to push back record.");

                return true;
            }

            void PushBackHeader(RecordType recordType, ulong worldSequenceNumber, in SystemHandle executingSystem, in SystemHandle originSystem, int entityCount, int typeCount, int dataLength)
            {
                var header = new Header(m_RecordIndex++, recordType, FrameCountSystem.FrameCount, worldSequenceNumber, in executingSystem, in originSystem, entityCount, typeCount, dataLength);
                if (!m_Buffer.PushBack((byte*)&header, sizeof(Header)))
                    UnityEngine.Debug.LogError($"EntitiesJournaling: Failed to push back header in buffer.");
            }

            void PushBackEntities(Entity* entities, int entityCount)
            {
                if (entities == null || entityCount <= 0)
                    return;

                if (!m_Buffer.PushBack((byte*)entities, sizeof(Entity) * entityCount))
                    UnityEngine.Debug.LogError($"EntitiesJournaling: Failed to push back entities in buffer.");
            }

            void PushBackEntities(ArchetypeChunk* chunks, int chunkCount)
            {
                if (chunks == null || chunkCount <= 0)
                    return;

                for (var i = 0; i < chunkCount; ++i)
                {
                    var archetypeChunk = chunks[i];
                    var chunk = archetypeChunk.m_Chunk;
                    var archetype = archetypeChunk.Archetype.Archetype;
                    var buffer = chunk.Buffer;
                    var length = archetypeChunk.Count;
                    var startOffset = archetype->Offsets[0];
                    if (!m_Buffer.PushBack(buffer + startOffset, sizeof(Entity) * length))
                        UnityEngine.Debug.LogError($"EntitiesJournaling: Failed to push back archetype chunk entities in buffer.");
                }
            }

            void PushBackEntities(Archetype* archetype, ChunkIndex chunk)
            {
                if (chunk == ChunkIndex.Null)
                    return;

                var buffer = chunk.Buffer;
                var length = chunk.Count;
                var startOffset = archetype->Offsets[0];
                if (!m_Buffer.PushBack(buffer + startOffset, sizeof(Entity) * length))
                    UnityEngine.Debug.LogError($"EntitiesJournaling: Failed to push back chunk entities in buffer.");
            }

            void PushBackTypes(TypeIndex* types, int typeCount)
            {
                if (types == null || typeCount <= 0)
                    return;

                if (!m_Buffer.PushBack((byte*)types, sizeof(TypeIndex) * typeCount))
                    UnityEngine.Debug.LogError($"EntitiesJournaling: Failed to push back component types in buffer.");
            }

            void PushBackData(void* data, int dataLength)
            {
                if (data == null || dataLength <= 0)
                    return;

                if (!m_Buffer.PushBack((byte*)data, dataLength))
                    UnityEngine.Debug.LogError($"EntitiesJournaling: Failed to push back data in buffer.");
            }

            UnsafeCircularBuffer<SystemVersionHandle>* GetSystemVersionBuffer(EntityComponentStore* store)
            {
                // Check if last node match
                if (m_LastSystemVersionNode != null && m_LastSystemVersionNode->Store == store)
                    return m_LastSystemVersionNode->Buffer;

                // Search existing nodes
                var node = m_SystemVersionNodes;
                while (node != null)
                {
                    if (node->Store == store)
                    {
                        m_LastSystemVersionNode = node;
                        return node->Buffer;
                    }

                    node = node->NextNode;
                }

                // Allocate new node
                node = Allocate<SystemVersionNode>(Allocator.Persistent, NativeArrayOptions.ClearMemory);
                if (node == null)
                    return null;

                node->Store = store;
                node->Buffer = Allocate<UnsafeCircularBuffer<SystemVersionHandle>>(Allocator.Persistent, NativeArrayOptions.ClearMemory);
                if (node->Buffer == null)
                {
                    AllocatorManager.Free(Allocator.Persistent, node);
                    return null;
                }

                node->Buffer->Construct(k_SystemVersionBufferSize / sizeof(SystemVersionHandle), Allocator.Persistent);
                node->NextNode = m_SystemVersionNodes;
                m_SystemVersionNodes = node;
                m_LastSystemVersionNode = node;

                return node->Buffer;
            }

            SystemHandle GetSystemHandle(EntityComponentStore* entityComponentStore, uint globalSystemVersion)
            {
                if (globalSystemVersion == 0)
                    return default;

                var buffer = GetSystemVersionBuffer(entityComponentStore);
                if (buffer == null || buffer->Count == 0)
                    return default;

                // Try fast lookup
                var firstIndex = buffer->ElementAt(0).Version;
                var index = (int)(globalSystemVersion - firstIndex);
                if (index >= 0 && index < buffer->Count)
                {
                    var element = buffer->ElementAt(index);
                    if (element.Version == globalSystemVersion)
                        return element.Handle;
                }

                // Slow reverse lookup
                for (var i = buffer->Count - 1; i >= 0; --i)
                {
                    var element = buffer->ElementAt(i);
                    if (element.Version == globalSystemVersion)
                        return element.Handle;
                }

                // System handle not found for this version.
                // Here it could mean an event recorded outside a system (legitimate)
                // or a system handle was not recorded (bug), but we cannot know for sure.
                // Because of this, we cannot throw an error here.
                return default;
            }

            static int GetEntityCount(ArchetypeChunk* chunks, int chunkCount)
            {
                if (chunks == null || chunkCount <= 0)
                    return 0;

                var entityCount = 0;
                for (var i = 0; i < chunkCount; ++i)
                    entityCount += chunks[i].Count;

                return entityCount;
            }
        }
    }
}
#endif
