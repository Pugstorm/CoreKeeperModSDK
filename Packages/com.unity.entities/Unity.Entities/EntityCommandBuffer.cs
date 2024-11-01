using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine.Profiling;

namespace Unity.Entities
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct BasicCommand
    {
        public ECBCommand CommandType;
        public int TotalSize;
        public int SortKey;  /// Used to order command execution during playback
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CreateCommand
    {
        public BasicCommand Header;
        public EntityArchetype Archetype;
        public int IdentityIndex;
        public int BatchCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EntityCommand
    {
        public BasicCommand Header;
        public Entity Entity;
        public int IdentityIndex;
        public int BatchCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct EntityQueryCommand
    {
        public BasicCommand Header;
        public EntityQueryImpl* QueryImpl;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct EntityQueryComponentCommand
    {
        public EntityQueryCommand Header;
        public TypeIndex ComponentTypeIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct EntityQueryComponentTypeSetCommand
    {
        public EntityQueryCommand Header;
        public ComponentTypeSet TypeSet;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct EntityQueryComponentCommandWithUnmanagedSharedComponent
    {
        public EntityQueryComponentCommand Header;
        public int ComponentSize;
        public int HashCode;
        public byte IsDefault;
        public byte ValueRequiresEntityFixup;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct EntityQueryComponentCommandWithObject
    {
        public EntityQueryComponentCommand Header;
        public int HashCode;
        public EntityComponentGCNode GCNode;

        internal object GetBoxedObject()
        {
            if (GCNode.BoxedObject.IsAllocated)
                return GCNode.BoxedObject.Target;
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct MultipleEntitiesCommand
    {
        public BasicCommand Header;
        public EntityNode Entities;
        public int EntitiesCount;
        public AllocatorManager.AllocatorHandle Allocator;
        public int SkipDeferredEntityLookup;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct MultipleEntitiesComponentCommand
    {
        public MultipleEntitiesCommand Header;
        public TypeIndex ComponentTypeIndex;
        public short ComponentSize;
        public byte ValueRequiresEntityFixup;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct MultipleEntitiesCommand_WithUnmanagedSharedComponent
    {
        public MultipleEntitiesCommand Header;
        public TypeIndex ComponentTypeIndex;
        public int ComponentSize;
        public int HashCode;
        public byte IsDefault;
        public byte ValueRequiresEntityFixup;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct MultipleEntitiesComponentCommandWithObject
    {
        public MultipleEntitiesCommand Header;
        public TypeIndex ComponentTypeIndex;
        public int HashCode;
        public EntityComponentGCNode GCNode;

        internal object GetBoxedObject()
        {
            if (GCNode.BoxedObject.IsAllocated)
                return GCNode.BoxedObject.Target;
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MultipleEntitiesAndComponentsCommand
    {
        public MultipleEntitiesCommand Header;
        public ComponentTypeSet TypeSet;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EntityComponentCommand
    {
        public EntityCommand Header;
        public TypeIndex ComponentTypeIndex;
        public short ComponentSize;
        public byte ValueRequiresEntityFixup;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EntityEnabledCommand
    {
        public EntityCommand Header;
        public byte IsEnabled;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EntityComponentEnabledCommand
    {
        public EntityEnabledCommand Header;
        public TypeIndex ComponentTypeIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EntityNameCommand
    {
        public EntityCommand Header;
        public FixedString64Bytes Name;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EntityQueryMaskCommand
    {
        public EntityComponentCommand Header;
        public EntityQueryMask Mask;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EntityMultipleComponentsCommand
    {
        public EntityCommand Header;
        public ComponentTypeSet TypeSet;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EntityBufferCommand
    {
        public EntityCommand Header;
        public TypeIndex ComponentTypeIndex;
        public short ComponentSize;
        public byte ValueRequiresEntityFixup;
        public BufferHeaderNode BufferNode; // must be the last field; contains a BufferHeader that extends beyond the end of the struct
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct EntityManagedComponentCommand
    {
        public EntityCommand Header;
        public TypeIndex ComponentTypeIndex;
        public EntityComponentGCNode GCNode;

        internal object GetBoxedObject()
        {
            if (GCNode.BoxedObject.IsAllocated)
                return GCNode.BoxedObject.Target;
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct EntityMoveManagedComponentCommand
    {
        public EntityCommand Header;
        public Entity SrcEntity;
        public TypeIndex ComponentTypeIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct EntityUnmanagedSharedComponentCommand
    {
        public EntityCommand Header;
        public TypeIndex ComponentTypeIndex;
        public int HashCode;
        public byte IsDefault;
        public byte ValueRequiresEntityFixup;
    }


    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct EntitySharedComponentCommand
    {
        public EntityCommand Header;
        public TypeIndex ComponentTypeIndex;
        public int HashCode;
        public EntityComponentGCNode GCNode;

        internal object GetBoxedObject()
        {
            if (GCNode.BoxedObject.IsAllocated)
                return GCNode.BoxedObject.Target;
            return null;
        }
    }

    internal unsafe struct EntityComponentGCNode
    {
        public GCHandle BoxedObject;
        public EntityComponentGCNode* Prev;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct BufferHeaderNode
    {
        public BufferHeaderNode* Prev;
        public BufferHeader TempBuffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct EntityNode
    {
        public Entity* Ptr;
        public EntityNode* Prev;
    }


    [StructLayout(LayoutKind.Sequential, Size = 32)]
    internal unsafe struct ChainCleanup
    {
        public EntityNode* EntityArraysCleanupList;
        public BufferHeaderNode* BufferCleanupList;
        public EntityComponentGCNode* CleanupList;
    }

    [StructLayout(LayoutKind.Sequential, Size = (64 > JobsUtility.CacheLineSize) ? 64: JobsUtility.CacheLineSize)]
    internal unsafe struct EntityCommandBufferChain
    {
        public ECBChunk* m_Tail;
        public ECBChunk* m_Head;
        public ChainCleanup* m_Cleanup;
        public CreateCommand*                m_PrevCreateCommand;
        public EntityCommand*                m_PrevEntityCommand;
        public EntityCommandBufferChain* m_NextChain;
        public int m_LastSortKey;
        public bool m_CanBurstPlayback;

        internal static void InitChain(EntityCommandBufferChain* chain, AllocatorManager.AllocatorHandle allocator)
        {
            chain->m_Cleanup = (ChainCleanup*)Memory.Unmanaged.Allocate(sizeof(ChainCleanup), sizeof(ChainCleanup), allocator);
            chain->m_Cleanup->CleanupList = null;
            chain->m_Cleanup->BufferCleanupList = null;
            chain->m_Cleanup->EntityArraysCleanupList = null;

            chain->m_Tail = null;
            chain->m_Head = null;
            chain->m_PrevCreateCommand = null;
            chain->m_PrevEntityCommand = null;
            chain->m_LastSortKey = -1;
            chain->m_NextChain = null;
            chain->m_CanBurstPlayback = true;
        }
    }

    internal unsafe struct ECBSharedPlaybackState
    {
        public struct BufferWithFixUp
        {
            public EntityBufferCommand* cmd;
        }

        public Entity* CreateEntityBatch;
        public BufferWithFixUp* BuffersWithFixUp;
        public int CreatedEntityCount;
        public int LastBuffer;
        public int CommandBufferID;
    }

    internal unsafe struct ECBChainPlaybackState
    {
        public ECBChunk* Chunk;
        public int Offset;
        public int NextSortKey;
        public bool CanBurstPlayback;
    }

    internal unsafe struct ECBChainHeapElement
    {
        public int SortKey;
        public int ChainIndex;
    }
    internal unsafe struct ECBChainPriorityQueue : IDisposable
    {
        private readonly ECBChainHeapElement* m_Heap;
        private int m_Size;
        private readonly AllocatorManager.AllocatorHandle m_Allocator;
        private static readonly int BaseIndex = 1;
        public ECBChainPriorityQueue(ECBChainPlaybackState* chainStates, int chainStateCount, AllocatorManager.AllocatorHandle alloc)
        {
            m_Size = chainStateCount;
            m_Allocator = alloc;
            m_Heap = (ECBChainHeapElement*)Memory.Unmanaged.Allocate((m_Size + BaseIndex) * sizeof(ECBChainHeapElement), 64, m_Allocator);
            for (int i = m_Size - 1; i >= m_Size / 2; --i)
            {
                m_Heap[BaseIndex + i].SortKey = chainStates[i].NextSortKey;
                m_Heap[BaseIndex + i].ChainIndex = i;
            }
            for (int i = m_Size / 2 - 1; i >= 0; --i)
            {
                m_Heap[BaseIndex + i].SortKey = chainStates[i].NextSortKey;
                m_Heap[BaseIndex + i].ChainIndex = i;
                Heapify(BaseIndex + i);
            }
        }

        public void Dispose()
        {
            Memory.Unmanaged.Free(m_Heap, m_Allocator);
        }

        public bool Empty { get { return m_Size <= 0; } }
        public ECBChainHeapElement Peek()
        {
            //Assert.IsTrue(!Empty, "Can't Peek() an empty heap");
            if (Empty)
            {
                return new ECBChainHeapElement { ChainIndex = -1, SortKey = -1};
            }
            return m_Heap[BaseIndex];
        }

        public ECBChainHeapElement Pop()
        {
            //Assert.IsTrue(!Empty, "Can't Pop() an empty heap");
            if (Empty)
            {
                return new ECBChainHeapElement { ChainIndex = -1, SortKey = -1};
            }
            ECBChainHeapElement top = Peek();
            m_Heap[BaseIndex] = m_Heap[m_Size--];
            if (!Empty)
            {
                Heapify(BaseIndex);
            }
            return top;
        }

        public void ReplaceTop(ECBChainHeapElement value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Empty)
                Assert.IsTrue(false, "Can't ReplaceTop() an empty heap");
#endif
            m_Heap[BaseIndex] = value;
            Heapify(BaseIndex);
        }

        private void Heapify(int i)
        {
            // The index taken by this function is expected to be already biased by BaseIndex.
            // Thus, m_Heap[size] is a valid element (specifically, the final element in the heap)
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (i < BaseIndex || i > m_Size)
                Assert.IsTrue(false, $"heap index {i} is out of range with size={m_Size}");
#endif
            ECBChainHeapElement val = m_Heap[i];
            while (i <= m_Size / 2)
            {
                int child = 2 * i;
                if (child < m_Size && (m_Heap[child + 1].SortKey < m_Heap[child].SortKey))
                {
                    child++;
                }
                if (val.SortKey < m_Heap[child].SortKey)
                {
                    break;
                }
                m_Heap[i] = m_Heap[child];
                i = child;
            }
            m_Heap[i] = val;
        }
    }

    internal enum ECBCommand
    {
        InstantiateEntity,

        CreateEntity,
        DestroyEntity,

        AddComponent,
        AddMultipleComponents,
        RemoveComponent,
        RemoveMultipleComponents,
        SetComponent,
        SetEntityEnabled,
        SetComponentEnabled,
        SetName,

        AddBuffer,
        SetBuffer,
        AppendToBuffer,

        AddManagedComponentData,
        SetManagedComponentData,
        MoveManagedComponentData,

        AddComponentLinkedEntityGroup,
        SetComponentLinkedEntityGroup,
        ReplaceComponentLinkedEntityGroup,

        AddSharedComponentData,
        SetSharedComponentData,
        AddUnmanagedSharedComponentData,
        SetUnmanagedSharedComponentData,

        AddUnmanagedSharedComponentValueForMultipleEntities,
        SetUnmanagedSharedComponentValueForMultipleEntities,
        AddUnmanagedSharedComponentValueForEntityQuery,
        SetUnmanagedSharedComponentValueForEntityQuery,

        AddComponentForEntityQuery,
        AddMultipleComponentsForMultipleEntities,
        AddMultipleComponentsForEntityQuery,
        RemoveComponentForEntityQuery,
        RemoveMultipleComponentsForMultipleEntities,
        RemoveMultipleComponentsForEntityQuery,

        AddSharedComponentWithValueForMultipleEntities,
        AddSharedComponentWithValueForEntityQuery,
        SetSharedComponentValueForMultipleEntities,
        SetSharedComponentValueForEntityQuery,

        AddComponentForMultipleEntities,
        AddComponentObjectForMultipleEntities,
        AddComponentObjectForEntityQuery,
        SetComponentObjectForMultipleEntities,
        RemoveComponentForMultipleEntities,

        DestroyMultipleEntities,
        DestroyForEntityQuery,
    }

    /// <summary>
    /// Organized in memory like a single block with Chunk header followed by Size bytes of data.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct ECBChunk
    {
        internal int Used;
        internal int Size;
        internal ECBChunk* Next;
        internal ECBChunk* Prev;

        internal int Capacity => Size - Used;

        internal int Bump(int size)
        {
            var off = Used;
            Used += size;
            return off;
        }

        internal int BaseSortKey
        {
            get
            {
                fixed(ECBChunk* pThis = &this)
                {
                    if (Used < sizeof(BasicCommand))
                    {
                        return -1;
                    }
                    var buf = (byte*)pThis + sizeof(ECBChunk);
                    var header = (BasicCommand*)(buf);
                    return header->SortKey;
                }
            }
        }
    }

    internal unsafe struct EntityCommandBufferData
    {
        public EntityCommandBufferChain m_MainThreadChain;
        public EntityCommandBufferChain* m_ThreadedChains;
        public int m_RecordedChainCount;
        public int m_MinimumChunkSize;
        public AllocatorManager.AllocatorHandle m_Allocator;
        public PlaybackPolicy m_PlaybackPolicy;
        public bool m_ShouldPlayback;
        public bool m_DidPlayback;
        public bool m_ForceFullDispose;
        public Entity m_Entity;
        public int m_BufferWithFixupsCount;
        public UnsafeAtomicCounter32 m_BufferWithFixups;
        internal static readonly int ALIGN_64_BIT = 8;
        public int m_CommandBufferID;

        internal void InitForParallelWriter()
        {
            if (m_ThreadedChains != null)
                return;

#if UNITY_2022_2_14F1_OR_NEWER
            int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
            int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
            int allocSize = sizeof(EntityCommandBufferChain) * maxThreadCount;

            m_ThreadedChains = (EntityCommandBufferChain*)Memory.Unmanaged.Allocate(allocSize, JobsUtility.CacheLineSize, m_Allocator);
            UnsafeUtility.MemClear(m_ThreadedChains, allocSize);
            // each thread's chain is lazily initialized inside Reserve() when its first command is written.
        }

        internal void DestroyForParallelWriter()
        {
            if (m_ThreadedChains != null)
            {
                Memory.Unmanaged.Free(m_ThreadedChains, m_Allocator);
                m_ThreadedChains = null;
            }
        }

        private void ResetCreateCommandBatching(EntityCommandBufferChain* chain)
        {
            chain->m_PrevCreateCommand = null;
        }

        private void ResetEntityCommandBatching(EntityCommandBufferChain* chain)
        {
            chain->m_PrevEntityCommand = null;
        }

        internal void ResetCommandBatching(EntityCommandBufferChain* chain)
        {
            ResetCreateCommandBatching(chain);
            ResetEntityCommandBatching(chain);
        }

        internal Entity* CloneAndSearchForDeferredEntities(NativeArray<Entity> entities,
            out bool containsDeferredEntities)
        {
            var output = (Entity*)Memory.Unmanaged.Allocate(entities.Length * sizeof(Entity), ALIGN_64_BIT, m_Allocator);
            containsDeferredEntities = false;
            int i = 0;
            int len = entities.Length;
            for (; i < len; ++i)
            {
                var e = entities[i];
                output[i] = e;
                if (e.Index < 0)
                {
                    containsDeferredEntities = true;
                    break;
                }
            }
            for (; i < len; ++i)
            {
                output[i] = entities[i];
            }
            return output;
        }

        internal void AddCreateCommand(EntityCommandBufferChain* chain, int sortKey, ECBCommand op, int index, EntityArchetype archetype, bool batchable)
        {
            if (batchable &&
                chain->m_PrevCreateCommand != null &&
                chain->m_PrevCreateCommand->Archetype == archetype)
            {
                ++chain->m_PrevCreateCommand->BatchCount;
            }
            else
            {
                ResetEntityCommandBatching(chain);
                var sizeNeeded = Align(sizeof(CreateCommand), ALIGN_64_BIT);
                var cmd = (CreateCommand*)Reserve(chain, sortKey, sizeNeeded);

                cmd->Header.CommandType = op;
                cmd->Header.TotalSize = sizeof(CreateCommand);
                cmd->Header.SortKey = chain->m_LastSortKey;
                cmd->Archetype = archetype;
                cmd->IdentityIndex = index;
                cmd->BatchCount = 1;

                chain->m_PrevCreateCommand = cmd;
            }
        }

        internal void AddEntityCommand(EntityCommandBufferChain* chain, int sortKey, ECBCommand op, int index, Entity e, bool batchable)
        {
            if (batchable &&
                chain->m_PrevEntityCommand != null &&
                chain->m_PrevEntityCommand->Entity == e)
            {
                ++chain->m_PrevEntityCommand->BatchCount;
            }
            else
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(e == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddEntityCommand");
#endif
                ResetCreateCommandBatching(chain);
                var sizeNeeded = Align(sizeof(EntityCommand), ALIGN_64_BIT);
                var cmd = (EntityCommand*)Reserve(chain, sortKey, sizeNeeded);

                cmd->Header.CommandType = op;
                cmd->Header.TotalSize = sizeNeeded;
                cmd->Header.SortKey = chain->m_LastSortKey;
                cmd->Entity = e;
                cmd->IdentityIndex = index;
                cmd->BatchCount = 1;
                chain->m_PrevEntityCommand = cmd;
            }
        }

        internal void AddLinkedEntityGroupComponentCommand<T>(EntityCommandBufferChain* chain, int sortKey, ECBCommand op, EntityQueryMask mask, Entity e, T component) where T : unmanaged, IComponentData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(e == Entity.Null))
                throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddLinkedEntityGroupComponentCommand");
#endif
            var ctype = ComponentType.ReadWrite<T>();
            if (ctype.IsZeroSized)
            {
                AddLinkedEntityGroupTypeCommand(chain, sortKey, op, mask, e, ctype);
                return;
            }

            // NOTE: This has to be sizeof not TypeManager.SizeInChunk since we use UnsafeUtility.CopyStructureToPtr
            //       even on zero size components.
            short typeSize = (short)UnsafeUtility.SizeOf<T>();
            var sizeNeeded = Align(sizeof(EntityQueryMaskCommand) + typeSize, ALIGN_64_BIT);

            ResetCommandBatching(chain);
            var cmd = (EntityQueryMaskCommand*)Reserve(chain, sortKey, sizeNeeded);

            cmd->Header.Header.Header.CommandType = op;
            cmd->Header.Header.Header.TotalSize = sizeNeeded;
            cmd->Header.Header.Header.SortKey = chain->m_LastSortKey;
            cmd->Header.Header.Entity = e;
            cmd->Header.Header.IdentityIndex = 0;
            cmd->Header.Header.BatchCount = 1;
            cmd->Header.ComponentTypeIndex = ctype.TypeIndex;
            cmd->Header.ComponentSize = typeSize;
            cmd->Mask = mask;
            byte* componentValue = (byte*)(cmd + 1);
            UnsafeUtility.CopyStructureToPtr(ref component, componentValue);
            cmd->Header.ValueRequiresEntityFixup = RequiresEntityFixUp(componentValue, ctype.TypeIndex) ? (byte)1 : (byte)0;
        }

        internal void AddLinkedEntityGroupTypeCommand(EntityCommandBufferChain* chain, int sortKey, ECBCommand op, EntityQueryMask mask, Entity e, ComponentType t)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(e == Entity.Null))
                throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddLinkedEntityGroupTypeCommand");
#endif
            var sizeNeeded = Align(sizeof(EntityQueryMaskCommand), ALIGN_64_BIT);

            ResetCommandBatching(chain);
            var data = (EntityQueryMaskCommand*)Reserve(chain, sortKey, sizeNeeded);

            data->Header.Header.Header.CommandType = op;
            data->Header.Header.Header.TotalSize = sizeNeeded;
            data->Header.Header.Header.SortKey = chain->m_LastSortKey;
            data->Header.Header.Entity = e;
            data->Header.Header.IdentityIndex = 0;
            data->Header.Header.BatchCount = 1;
            data->Header.ComponentTypeIndex = t.TypeIndex;
            data->Header.ComponentSize = 0;
            data->Header.ValueRequiresEntityFixup = 0;
            data->Mask = mask;
        }

        internal void AddMultipleEntityCommand(EntityCommandBufferChain* chain, int sortKey, ECBCommand op, int firstIndex, int count, Entity e, bool batchable)
        {
            if (batchable &&
                chain->m_PrevEntityCommand != null &&
                chain->m_PrevEntityCommand->Entity == e)
            {
                chain->m_PrevEntityCommand->BatchCount += count;
            }
            else
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(e == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddMultipleEntityCommand");
#endif
                ResetCreateCommandBatching(chain);
                var sizeNeeded = Align(sizeof(EntityCommand), ALIGN_64_BIT);
                var cmd = (EntityCommand*)Reserve(chain, sortKey, sizeNeeded);

                cmd->Header.CommandType = op;
                cmd->Header.TotalSize = sizeNeeded;
                cmd->Header.SortKey = chain->m_LastSortKey;
                cmd->Entity = e;
                cmd->IdentityIndex = firstIndex;
                cmd->BatchCount = count;
                chain->m_PrevEntityCommand = null;
            }
        }

        internal bool RequiresEntityFixUp(byte* data, TypeIndex typeIndex)
        {
            if (!TypeManager.HasEntityReferences(typeIndex))
                return false;

            var offsets = TypeManager.GetEntityOffsets(typeIndex, out var offsetCount);
            for (int i = 0; i < offsetCount; i++)
            {
                if (((Entity*)(data + offsets[i].Offset))->Index < 0)
                {
                    return true;
                }
            }
            return false;
        }

        internal void AddEntityComponentTypeWithValueCommand<T>(EntityCommandBufferChain* chain, int sortKey, ECBCommand op, Entity e, T component) where T : unmanaged, IComponentData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(e == Entity.Null))
                throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddEntityComponentCommand");
#endif
            var ctype = ComponentType.ReadWrite<T>();
            if (ctype.IsZeroSized)
            {
                AddEntityComponentTypeWithoutValueCommand(chain, sortKey, op, e, ctype);
                return;
            }

            // NOTE: This has to be sizeof not TypeManager.SizeInChunk since we use UnsafeUtility.CopyStructureToPtr
            //       even on zero size components.
            short typeSize = (short)UnsafeUtility.SizeOf<T>();
            var sizeNeeded = Align(sizeof(EntityComponentCommand) + typeSize, ALIGN_64_BIT);

            ResetCommandBatching(chain);
            var cmd = (EntityComponentCommand*)Reserve(chain, sortKey, sizeNeeded);

            cmd->Header.Header.CommandType = op;
            cmd->Header.Header.TotalSize = sizeNeeded;
            cmd->Header.Header.SortKey = chain->m_LastSortKey;
            cmd->Header.Entity = e;
            cmd->Header.IdentityIndex = 0;
            cmd->Header.BatchCount = 1;
            cmd->ComponentTypeIndex = ctype.TypeIndex;
            cmd->ComponentSize = typeSize;
            byte* componentValue = (byte*)(cmd + 1);
            UnsafeUtility.CopyStructureToPtr(ref component, componentValue);
            cmd->ValueRequiresEntityFixup = RequiresEntityFixUp(componentValue, ctype.TypeIndex) ? (byte)1 : (byte)0;
        }

        internal void UnsafeAddEntityComponentCommand(EntityCommandBufferChain* chain, int sortKey, ECBCommand op, Entity e, TypeIndex typeIndex, int typeSize, void* componentDataPtr)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            UnityEngine.Assertions.Assert.AreEqual(TypeManager.GetTypeInfo(typeIndex).TypeSize, typeSize, "Type size does not match TypeManager's size!");
            UnityEngine.Assertions.Assert.IsTrue(componentDataPtr != null, "componentDataPtr is null!");
            if (Hint.Unlikely(e == Entity.Null))
                throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.UnsafeAddEntityComponentCommand");
#endif
            var sizeNeeded = Align(sizeof(EntityComponentCommand) + typeSize, ALIGN_64_BIT);

            ResetCommandBatching(chain);
            var cmd = (EntityComponentCommand*)Reserve(chain, sortKey, sizeNeeded);

            cmd->Header.Header.CommandType = op;
            cmd->Header.Header.TotalSize = sizeNeeded;
            cmd->Header.Header.SortKey = chain->m_LastSortKey;
            cmd->Header.Entity = e;
            cmd->Header.IdentityIndex = 0;
            cmd->Header.BatchCount = 1;
            cmd->ComponentTypeIndex = typeIndex;
            cmd->ComponentSize = (short)typeSize;
            byte* componentValue = (byte*)(cmd + 1);
            UnsafeUtility.MemCpy(componentValue, componentDataPtr, typeSize);
            cmd->ValueRequiresEntityFixup = RequiresEntityFixUp(componentValue, typeIndex) ? (byte)1 : (byte)0;
        }

        internal void AddEntityEnabledCommand(EntityCommandBufferChain* chain, int sortKey, ECBCommand op, Entity e,
            bool value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(e == Entity.Null))
                throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddEntityEnabledCommand");
#endif
            var sizeNeeded = Align(sizeof(EntityEnabledCommand), ALIGN_64_BIT);

            ResetCommandBatching(chain);
            var cmd = (EntityEnabledCommand*)Reserve(chain, sortKey, sizeNeeded);

            cmd->Header.Header.CommandType = op;
            cmd->Header.Header.TotalSize = sizeNeeded;
            cmd->Header.Header.SortKey = chain->m_LastSortKey;
            cmd->Header.Entity = e;
            cmd->Header.IdentityIndex = 0;
            cmd->Header.BatchCount = 1;
            cmd->IsEnabled = value ? (byte)1 : (byte)0;
        }

        internal void AddEntityComponentEnabledCommand(EntityCommandBufferChain* chain, int sortKey, ECBCommand op, Entity e, TypeIndex typeIndex, bool value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(e == Entity.Null))
                throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddEntityComponentEnabledCommand");
#endif
            var sizeNeeded = Align(sizeof(EntityComponentEnabledCommand), ALIGN_64_BIT);

            ResetCommandBatching(chain);
            var cmd = (EntityComponentEnabledCommand*)Reserve(chain, sortKey, sizeNeeded);

            cmd->Header.Header.Header.CommandType = op;
            cmd->Header.Header.Header.TotalSize = sizeNeeded;
            cmd->Header.Header.Header.SortKey = chain->m_LastSortKey;
            cmd->Header.Header.Entity = e;
            cmd->Header.Header.IdentityIndex = 0;
            cmd->Header.Header.BatchCount = 1;
            cmd->ComponentTypeIndex = typeIndex;
            cmd->Header.IsEnabled = value ? (byte)1 : (byte)0;
        }

        internal void AddEntityNameCommand(EntityCommandBufferChain* chain, int sortKey, ECBCommand op, Entity e, in FixedString64Bytes name)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(e == Entity.Null))
                throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddEntityComponentEnabledCommand");
#endif
            var sizeNeeded = Align(sizeof(EntityNameCommand), ALIGN_64_BIT);

            ResetCommandBatching(chain);
            var cmd = (EntityNameCommand*)Reserve(chain, sortKey, sizeNeeded);

            cmd->Header.Header.CommandType = op;
            cmd->Header.Header.TotalSize = sizeNeeded;
            cmd->Header.Header.SortKey = chain->m_LastSortKey;
            cmd->Header.Entity = e;
            cmd->Header.IdentityIndex = 0;
            cmd->Header.BatchCount = 1;
            cmd->Name = name;
        }

        internal BufferHeader* AddEntityBufferCommand<T>(EntityCommandBufferChain* chain, int sortKey, ECBCommand op,
            Entity e, out int internalCapacity) where T : struct, IBufferElementData
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            ref readonly var type = ref TypeManager.GetTypeInfo<T>();

            // We use type.SizeInChunk here instead of sizeof(T), because what we're serializing in the command buffer
            // is a full BufferHeader + internal buffer of Ts, not just a single T. SizeInChunk for buffer types
            // implicitly takes this into account.
            var sizeNeeded = Align(sizeof(EntityBufferCommand) + type.SizeInChunk, ALIGN_64_BIT);

            ResetCommandBatching(chain);
            var cmd = (EntityBufferCommand*)Reserve(chain, sortKey, sizeNeeded);

            cmd->Header.Header.CommandType = op;
            cmd->Header.Header.TotalSize = sizeNeeded;
            cmd->Header.Header.SortKey = chain->m_LastSortKey;
            cmd->Header.Entity = e;
            cmd->Header.IdentityIndex = 0;
            cmd->Header.BatchCount = 1;
            cmd->ComponentTypeIndex = typeIndex;
            cmd->ComponentSize = (short)type.SizeInChunk;
            cmd->ValueRequiresEntityFixup = 0;

            BufferHeader* header = &cmd->BufferNode.TempBuffer;
            BufferHeader.Initialize(header, type.BufferCapacity);

            // Track all DynamicBuffer headers created during recording. Until the ECB is played back, it owns the
            // memory allocations for these buffers and is responsible for deallocating them when the ECB is disposed.
            cmd->BufferNode.Prev = chain->m_Cleanup->BufferCleanupList;
            chain->m_Cleanup->BufferCleanupList = &(cmd->BufferNode);
            // The caller may invoke methods on the DynamicBuffer returned by this command during ECB recording which
            // cause it to allocate memory (for example, DynamicBuffer.AddRange). These allocations always use
            // Allocator.Persistent, not the ECB's allocator. These allocations must ALWAYS be manually cleaned up
            // if the ECB is disposed without being played back. So, we have to force the full ECB cleanup process
            // to run in this case, even if it could normally be skipped.
            m_ForceFullDispose = true;

            internalCapacity = type.BufferCapacity;

            if (TypeManager.HasEntityReferences(typeIndex))
            {
                cmd->ValueRequiresEntityFixup = 1;
                m_BufferWithFixups.Add(1);
            }

            return header;
        }

        internal static int Align(int size, int alignmentPowerOfTwo)
        {
            return (size + alignmentPowerOfTwo - 1) & ~(alignmentPowerOfTwo - 1);
        }

        internal void AddEntityComponentTypeWithoutValueCommand(EntityCommandBufferChain* chain, int sortKey, ECBCommand op, Entity e, ComponentType t)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(e == Entity.Null))
                throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddEntityComponentTypeCommand");
#endif
            var sizeNeeded = Align(sizeof(EntityComponentCommand), ALIGN_64_BIT);

            ResetCommandBatching(chain);
            var data = (EntityComponentCommand*)Reserve(chain, sortKey, sizeNeeded);

            data->Header.Header.CommandType = op;
            data->Header.Header.TotalSize = sizeNeeded;
            data->Header.Header.SortKey = chain->m_LastSortKey;
            data->Header.Entity = e;
            data->Header.IdentityIndex = 0;
            data->Header.BatchCount = 1;
            data->ComponentTypeIndex = t.TypeIndex;
            data->ComponentSize = 0;
            data->ValueRequiresEntityFixup = 0;
        }

        internal void AddEntityComponentTypesCommand(EntityCommandBufferChain* chain, int sortKey, ECBCommand op, Entity e, in ComponentTypeSet t)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(e == Entity.Null))
                throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddEntityComponentTypesCommand");
#endif
            var sizeNeeded = Align(sizeof(EntityMultipleComponentsCommand), ALIGN_64_BIT);

            ResetCommandBatching(chain);
            var data = (EntityMultipleComponentsCommand*)Reserve(chain, sortKey, sizeNeeded);

            data->Header.Header.CommandType = op;
            data->Header.Header.TotalSize = sizeNeeded;
            data->Header.Header.SortKey = chain->m_LastSortKey;
            data->Header.Entity = e;
            data->Header.IdentityIndex = 0;
            data->Header.BatchCount = 1;
            data->TypeSet = t;
        }

        internal bool AppendMultipleEntitiesCommand(EntityCommandBufferChain* chain, int sortKey, ECBCommand op,
            EntityQuery entityQuery)
        {
            var entities = entityQuery.ToEntityArray(m_Allocator.ToAllocator);
            if (entities.Length == 0)
            {
                entities.Dispose();
                return false;
            }
            var result = AppendMultipleEntitiesCommand(chain, sortKey, op,
                (Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length, false);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.DisposeHandle(ref entities.m_Safety); // dispose safety handle, but we'll dispose of the actual data in playback
#endif
            return result;
        }
        internal bool AppendMultipleEntitiesCommand(EntityCommandBufferChain* chain, int sortKey, ECBCommand op,
            Entity* entities, int entityCount, bool mayContainDeferredEntities)
        {
            var sizeNeeded = Align(sizeof(MultipleEntitiesCommand), ALIGN_64_BIT);

            ResetCommandBatching(chain);

            var cmd = (MultipleEntitiesCommand*)Reserve(chain, sortKey, sizeNeeded);
            cmd->Entities.Ptr = entities;
            cmd->Entities.Prev = chain->m_Cleanup->EntityArraysCleanupList;
            chain->m_Cleanup->EntityArraysCleanupList = &(cmd->Entities);

            cmd->EntitiesCount = entityCount;
            cmd->Allocator = m_Allocator;
            cmd->SkipDeferredEntityLookup = mayContainDeferredEntities ? 0 : 1;

            cmd->Header.CommandType = op;
            cmd->Header.TotalSize = sizeNeeded;
            cmd->Header.SortKey = chain->m_LastSortKey;

            return true;
        }

        internal bool AppendMultipleEntitiesComponentCommandWithValue<T>(EntityCommandBufferChain* chain, int sortKey,
            ECBCommand op, EntityQuery entityQuery, T component) where T : unmanaged, IComponentData
        {
            var entities = entityQuery.ToEntityArray(m_Allocator.ToAllocator); // disposed in playback
            if (entities.Length == 0)
            {
                entities.Dispose();
                return false;
            }
            var result = AppendMultipleEntitiesComponentCommandWithValue(chain, sortKey, op,
                (Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length, false, component);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.DisposeHandle(ref entities.m_Safety); // dispose safety handle, but we'll dispose of the actual data in playback
#endif
            return result;
        }
        internal bool AppendMultipleEntitiesComponentCommandWithValue<T>(EntityCommandBufferChain* chain, int sortKey,
            ECBCommand op, Entity* entities, int entityCount, bool mayContainDeferredEntities, T component) where T : unmanaged, IComponentData
        {
            var ctype = ComponentType.ReadWrite<T>();
            if (ctype.IsZeroSized)
                return AppendMultipleEntitiesComponentCommand(chain, sortKey, op, entities, entityCount, mayContainDeferredEntities, ctype);

            var typeSize = (short)UnsafeUtility.SizeOf<T>();
            var sizeNeeded = Align(sizeof(MultipleEntitiesComponentCommand) + typeSize, ALIGN_64_BIT);

            ResetCommandBatching(chain);

            var cmd = (MultipleEntitiesComponentCommand*)Reserve(chain, sortKey, sizeNeeded);
            cmd->Header.Entities.Ptr = entities;
            cmd->Header.Entities.Prev = chain->m_Cleanup->EntityArraysCleanupList;
            chain->m_Cleanup->EntityArraysCleanupList = &(cmd->Header.Entities);

            cmd->Header.EntitiesCount = entityCount;
            cmd->Header.Allocator = m_Allocator;
            cmd->Header.SkipDeferredEntityLookup = mayContainDeferredEntities ? 0 : 1;

            cmd->Header.Header.CommandType = op;
            cmd->Header.Header.TotalSize = sizeNeeded;
            cmd->Header.Header.SortKey = chain->m_LastSortKey;
            cmd->ComponentTypeIndex = ctype.TypeIndex;
            cmd->ComponentSize = typeSize;
            byte* componentValue = (byte*)(cmd + 1);
            UnsafeUtility.CopyStructureToPtr(ref component, componentValue);
            cmd->ValueRequiresEntityFixup = RequiresEntityFixUp(componentValue, ctype.TypeIndex) ? (byte)1 : (byte)0;
            return true;
        }

        internal bool AppendMultipleEntitiesComponentCommandWithObject(EntityCommandBufferChain* chain, int sortKey,
            ECBCommand op, EntityQuery entityQuery, object boxedComponent, ComponentType ctype)
        {
            var entities = entityQuery.ToEntityArray(m_Allocator.ToAllocator);
            if (entities.Length == 0)
            {
                entities.Dispose();
                return false;
            }
            var result = AppendMultipleEntitiesComponentCommandWithObject(chain, sortKey, op,
                (Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length, false, boxedComponent, ctype);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.DisposeHandle(ref entities.m_Safety); // dispose safety handle, but we'll dispose of the actual data in playback
#endif
            return result;
        }
        internal bool AppendMultipleEntitiesComponentCommandWithObject(EntityCommandBufferChain* chain, int sortKey,
            ECBCommand op, Entity* entities, int entityCount, bool mayContainDeferredEntities, object boxedComponent, ComponentType ctype)
        {
            var sizeNeeded = Align(sizeof(MultipleEntitiesComponentCommandWithObject), ALIGN_64_BIT);

            ResetCommandBatching(chain);

            var cmd = (MultipleEntitiesComponentCommandWithObject*)Reserve(chain, sortKey, sizeNeeded);

            cmd->Header.Entities.Ptr = entities;
            cmd->Header.Entities.Prev = chain->m_Cleanup->EntityArraysCleanupList;
            chain->m_Cleanup->EntityArraysCleanupList = &(cmd->Header.Entities);

            cmd->Header.EntitiesCount = entityCount;
            cmd->Header.Allocator = m_Allocator;
            cmd->Header.SkipDeferredEntityLookup = mayContainDeferredEntities ? 0 : 1;

            cmd->Header.Header.CommandType = op;
            cmd->Header.Header.TotalSize = sizeNeeded;
            cmd->Header.Header.SortKey = chain->m_LastSortKey;
            cmd->ComponentTypeIndex = ctype.TypeIndex;

            // TODO(DOTS-3465): if boxedComponent contains Entity references to temporary Entities, they will not currently be fixed up.

            if (boxedComponent != null)
            {
                cmd->GCNode.BoxedObject = GCHandle.Alloc(boxedComponent);
                // We need to store all GCHandles on a cleanup list so we can dispose them later, regardless of if playback occurs or not.
                cmd->GCNode.Prev = chain->m_Cleanup->CleanupList;
                chain->m_Cleanup->CleanupList = &(cmd->GCNode);
            }
            else
            {
                cmd->GCNode.BoxedObject = new GCHandle();
            }
            return true;
        }

        internal bool AppendEntityQueryComponentCommandWithSharedValue<T>(EntityCommandBufferChain* chain,
            int sortKey, ECBCommand op, EntityQuery entityQuery, int hashCode,
            object boxedComponent) where T : struct, ISharedComponentData
        {
            var ctype = ComponentType.ReadWrite<T>();
            var sizeNeeded = Align(sizeof(EntityQueryComponentCommandWithObject), ALIGN_64_BIT);

            ResetCommandBatching(chain);

            var cmd = (EntityQueryComponentCommandWithObject*)Reserve(chain, sortKey, sizeNeeded);

            cmd->Header.Header.Header.CommandType = op;
            cmd->Header.Header.Header.TotalSize = sizeNeeded;
            cmd->Header.Header.Header.SortKey = chain->m_LastSortKey;

            cmd->Header.Header.QueryImpl = entityQuery._GetImpl();

            cmd->Header.ComponentTypeIndex = ctype.TypeIndex;

            cmd->HashCode = hashCode;
            // TODO(DOTS-3465): if boxedComponent contains Entity references to temporary Entities, they will not currently be fixed up.
            if (boxedComponent != null)
            {
                cmd->GCNode.BoxedObject = GCHandle.Alloc(boxedComponent);
                // We need to store all GCHandles on a cleanup list so we can dispose them later, regardless of if playback occurs or not.
                cmd->GCNode.Prev = chain->m_Cleanup->CleanupList;
                chain->m_Cleanup->CleanupList = &(cmd->GCNode);
            }
            else
            {
                cmd->GCNode.BoxedObject = new GCHandle();
            }
            return true;
        }

        internal bool AppendMultipleEntitiesComponentCommandWithSharedValue<T>(EntityCommandBufferChain* chain,
            int sortKey, ECBCommand op, EntityQuery entityQuery, int hashCode,
            object boxedComponent) where T : struct, ISharedComponentData
        {
            var entities = entityQuery.ToEntityArray(m_Allocator.ToAllocator);
            if (entities.Length == 0)
            {
                entities.Dispose();
                return false;
            }
            var result = AppendMultipleEntitiesComponentCommandWithSharedValue<T>(chain, sortKey, op,
                (Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length, false, hashCode,
                boxedComponent);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.DisposeHandle(ref entities.m_Safety); // dispose safety handle, but we'll dispose of the actual data in playback

#endif
            return result;
        }

        internal bool AppendMultipleEntitiesComponentCommandWithSharedValue<T>(
            EntityCommandBufferChain* chain,
            int sortKey,
            ECBCommand op,
            Entity* entities,
            int entityCount,
            bool mayContainDeferredEntities,
            int hashCode,
            object boxedComponent)
            where T : struct, ISharedComponentData
        {
            var ctype = ComponentType.ReadWrite<T>();
            var sizeNeeded = Align(sizeof(MultipleEntitiesComponentCommandWithObject), ALIGN_64_BIT);

            ResetCommandBatching(chain);

            var cmd = (MultipleEntitiesComponentCommandWithObject*)Reserve(chain, sortKey, sizeNeeded);

            cmd->Header.Entities.Ptr = entities;
            cmd->Header.Entities.Prev = chain->m_Cleanup->EntityArraysCleanupList;
            chain->m_Cleanup->EntityArraysCleanupList = &(cmd->Header.Entities);

            cmd->Header.EntitiesCount = entityCount;
            cmd->Header.Allocator = m_Allocator;
            cmd->Header.SkipDeferredEntityLookup = mayContainDeferredEntities ? 0 : 1;

            cmd->Header.Header.CommandType = op;
            cmd->Header.Header.TotalSize = sizeNeeded;
            cmd->Header.Header.SortKey = chain->m_LastSortKey;
            cmd->ComponentTypeIndex = ctype.TypeIndex;
            cmd->HashCode = hashCode;

            // TODO(DOTS-3465): if boxedComponent contains Entity references to temporary Entities, they will not currently be fixed up.

            if (boxedComponent != null)
            {
                cmd->GCNode.BoxedObject = GCHandle.Alloc(boxedComponent);
                // We need to store all GCHandles on a cleanup list so we can dispose them later, regardless of if playback occurs or not.
                cmd->GCNode.Prev = chain->m_Cleanup->CleanupList;
                chain->m_Cleanup->CleanupList = &(cmd->GCNode);
            }
            else
            {
                cmd->GCNode.BoxedObject = new GCHandle();
            }
            return true;
        }

        internal bool AppendMultipleEntitiesCommand_WithUnmanagedSharedValue<T>(
            EntityCommandBufferChain* chain,
            int sortKey,
            ECBCommand op,
            EntityQuery entityQuery,
            bool mayContainDeferredEntities,
            int hashCode,
            void* componentAddr)
            where T : struct, ISharedComponentData
        {

            var entities = entityQuery.ToEntityArray(m_Allocator.ToAllocator);
            if (entities.Length == 0)
            {
                entities.Dispose();
                return false;
            }

            AppendMultipleEntitiesCommand_WithUnmanagedSharedValue<T>(
                chain,
                sortKey,
                op,
                (Entity*)entities.GetUnsafeReadOnlyPtr(),
                entities.Length,
                mayContainDeferredEntities,
                hashCode,
                componentAddr);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.DisposeHandle(ref entities.m_Safety); // dispose safety handle, but we'll dispose of the actual data in playback
#endif
            return true;
        }

        internal bool AppendEntityQueryComponentCommandWithUnmanagedSharedValue<T>(
            EntityCommandBufferChain* chain,
            int sortKey,
            ECBCommand op,
            EntityQuery entityQuery,
            int hashCode,
            void* componentAddr)
            where T : struct, ISharedComponentData
        {
            var ctype = ComponentType.ReadWrite<T>();
            var typeSize = UnsafeUtility.SizeOf<T>();
            var sizeNeeded = Align(sizeof(EntityQueryComponentCommandWithUnmanagedSharedComponent) + typeSize, ALIGN_64_BIT);

            ResetCommandBatching(chain);

            var cmd = (EntityQueryComponentCommandWithUnmanagedSharedComponent*) Reserve(chain, sortKey, sizeNeeded);

            cmd->Header.Header.Header.CommandType = op;
            cmd->Header.Header.Header.TotalSize = sizeNeeded;
            cmd->Header.Header.Header.SortKey = chain->m_LastSortKey;
            cmd->Header.Header.QueryImpl = entityQuery._GetImpl();
            cmd->Header.ComponentTypeIndex = ctype.TypeIndex;
            cmd->ComponentSize = typeSize;
            cmd->HashCode = hashCode;
            cmd->IsDefault = componentAddr == null ? (byte)1 : (byte)0;
            byte* componentValue = (byte*) (cmd + 1);
            if (componentAddr != null)
            {
                UnsafeUtility.MemCpy(componentValue, componentAddr, typeSize);
            }
            else
            {
                UnsafeUtility.MemSet(componentValue, 0, typeSize);
            }
            cmd->ValueRequiresEntityFixup = RequiresEntityFixUp(componentValue, ctype.TypeIndex) ? (byte)1 : (byte)0;

            return true;
        }

        internal bool AppendMultipleEntitiesCommand_WithUnmanagedSharedValue<T>(
            EntityCommandBufferChain* chain,
            int sortKey,
            ECBCommand op,
            Entity* entities,
            int entityCount,
            bool mayContainDeferredEntities,
            int hashCode,
            void* componentAddr) where T : struct, ISharedComponentData
        {
            var ctype = ComponentType.ReadWrite<T>();
            var typeSize = UnsafeUtility.SizeOf<T>();
            var sizeNeeded = Align(sizeof(MultipleEntitiesCommand_WithUnmanagedSharedComponent) + typeSize, ALIGN_64_BIT);

            ResetCommandBatching(chain);

            var cmd = (MultipleEntitiesCommand_WithUnmanagedSharedComponent*) Reserve(chain, sortKey, sizeNeeded);

            cmd->Header.Entities.Ptr = entities;
            cmd->Header.Entities.Prev = chain->m_Cleanup->EntityArraysCleanupList;
            chain->m_Cleanup->EntityArraysCleanupList = &(cmd->Header.Entities);

            cmd->Header.EntitiesCount = entityCount;
            cmd->Header.Allocator = m_Allocator;
            cmd->Header.SkipDeferredEntityLookup = mayContainDeferredEntities ? 0 : 1;

            cmd->Header.Header.CommandType = op;
            cmd->Header.Header.TotalSize = sizeNeeded;
            cmd->Header.Header.SortKey = chain->m_LastSortKey;
            cmd->ComponentTypeIndex = ctype.TypeIndex;
            cmd->ComponentSize = typeSize;
            cmd->HashCode = hashCode;
            cmd->IsDefault = componentAddr == null ? (byte)1 : (byte)0;
            byte* componentValue = (byte*) (cmd + 1);
            if (componentAddr != null)
            {
                UnsafeUtility.MemCpy(componentValue, componentAddr, typeSize);
            }
            else
            {
                UnsafeUtility.MemSet(componentValue, 0, typeSize);
            }
            cmd->ValueRequiresEntityFixup = RequiresEntityFixUp(componentValue, ctype.TypeIndex) ? (byte)1 : (byte)0;

            return true;
        }

        internal bool AppendEntityQueryCommand(
            EntityCommandBufferChain* chain,
            int sortKey,
            ECBCommand op,
            EntityQuery entityQuery)
        {
            var sizeNeeded = Align(sizeof(EntityQueryCommand), ALIGN_64_BIT);

            ResetCommandBatching(chain);

            var cmd = (EntityQueryCommand*)Reserve(chain, sortKey, sizeNeeded);
            cmd->QueryImpl = entityQuery._GetImpl();

            cmd->Header.CommandType = op;
            cmd->Header.TotalSize = sizeNeeded;
            cmd->Header.SortKey = chain->m_LastSortKey;
            return true;
        }

        internal bool AppendEntityQueryComponentCommand(
            EntityCommandBufferChain* chain,
            int sortKey,
            ECBCommand op,
            EntityQuery entityQuery,
            ComponentType t)
        {
            var sizeNeeded = Align(sizeof(EntityQueryComponentCommand), ALIGN_64_BIT);

            ResetCommandBatching(chain);

            var cmd = (EntityQueryComponentCommand*)Reserve(chain, sortKey, sizeNeeded);
            cmd->Header.QueryImpl = entityQuery._GetImpl();

            cmd->Header.Header.CommandType = op;
            cmd->Header.Header.TotalSize = sizeNeeded;
            cmd->Header.Header.SortKey = chain->m_LastSortKey;
            cmd->ComponentTypeIndex = t.TypeIndex;
            return true;
        }

        internal bool AppendEntityQueryComponentTypeSetCommand(
            EntityCommandBufferChain* chain,
            int sortKey,
            ECBCommand op,
            EntityQuery entityQuery,
            in ComponentTypeSet typeSet)
        {
            var sizeNeeded = Align(sizeof(EntityQueryComponentTypeSetCommand), ALIGN_64_BIT);

            ResetCommandBatching(chain);

            var cmd = (EntityQueryComponentTypeSetCommand*)Reserve(chain, sortKey, sizeNeeded);
            cmd->Header.QueryImpl = entityQuery._GetImpl();

            cmd->Header.Header.CommandType = op;
            cmd->Header.Header.TotalSize = sizeNeeded;
            cmd->Header.Header.SortKey = chain->m_LastSortKey;
            cmd->TypeSet = typeSet;
            return true;
        }

        internal bool AppendMultipleEntitiesComponentCommand(
            EntityCommandBufferChain* chain,
            int sortKey,
            ECBCommand op,
            EntityQuery entityQuery,
            ComponentType t)
        {
            var entities = entityQuery.ToEntityArray(m_Allocator.ToAllocator); // disposed in playback
            if (entities.Length == 0)
            {
                entities.Dispose();
                return false;
            }
            var result = AppendMultipleEntitiesComponentCommand(chain, sortKey, op,
                (Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length, false, t);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.DisposeHandle(ref entities.m_Safety); // dispose safety handle, but we'll dispose of the actual data in playback
#endif
            return result;
        }
        internal bool AppendMultipleEntitiesComponentCommand(EntityCommandBufferChain* chain, int sortKey, ECBCommand op,
            Entity* entities, int entityCount, bool mayContainDeferredEntities, ComponentType t)
        {
            var sizeNeeded = Align(sizeof(MultipleEntitiesComponentCommand), ALIGN_64_BIT);

            ResetCommandBatching(chain);

            var cmd = (MultipleEntitiesComponentCommand*)Reserve(chain, sortKey, sizeNeeded);
            cmd->Header.Entities.Ptr = entities;
            cmd->Header.Entities.Prev = chain->m_Cleanup->EntityArraysCleanupList;
            chain->m_Cleanup->EntityArraysCleanupList = &(cmd->Header.Entities);

            cmd->Header.EntitiesCount = entityCount;
            cmd->Header.Allocator = m_Allocator;
            cmd->Header.SkipDeferredEntityLookup = mayContainDeferredEntities ? 0 : 1;

            cmd->Header.Header.CommandType = op;
            cmd->Header.Header.TotalSize = sizeNeeded;
            cmd->Header.Header.SortKey = chain->m_LastSortKey;
            cmd->ComponentTypeIndex = t.TypeIndex;
            cmd->ComponentSize = 0;   // signifies that the command doesn't include a value for the new component
            cmd->ValueRequiresEntityFixup = 0;
            return true;
        }

        internal bool AppendMultipleEntitiesMultipleComponentsCommand(EntityCommandBufferChain* chain, int sortKey,
            ECBCommand op, EntityQuery entityQuery, in ComponentTypeSet t)
        {
            var entities = entityQuery.ToEntityArray(m_Allocator.ToAllocator); // disposed in playback
            if (entities.Length == 0)
            {
                entities.Dispose();
                return false;
            }
            var result = AppendMultipleEntitiesMultipleComponentsCommand(chain, sortKey, op,
                (Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length, false, t);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.DisposeHandle(ref entities.m_Safety); // dispose safety handle, but we'll dispose of the actual data in playback
#endif
            return result;
        }
        internal bool AppendMultipleEntitiesMultipleComponentsCommand(EntityCommandBufferChain* chain, int sortKey,
            ECBCommand op, Entity* entities, int entityCount, bool mayContainDeferredEntities, in ComponentTypeSet t)
        {
            var sizeNeeded = Align(sizeof(MultipleEntitiesAndComponentsCommand), ALIGN_64_BIT);

            ResetCommandBatching(chain);

            var cmd = (MultipleEntitiesAndComponentsCommand*)Reserve(chain, sortKey, sizeNeeded);
            cmd->Header.Entities.Ptr = entities;
            cmd->Header.Entities.Prev = chain->m_Cleanup->EntityArraysCleanupList;
            chain->m_Cleanup->EntityArraysCleanupList = &(cmd->Header.Entities);

            cmd->Header.EntitiesCount = entityCount;
            cmd->Header.Allocator = m_Allocator;
            cmd->Header.SkipDeferredEntityLookup = mayContainDeferredEntities ? 0 : 1;

            cmd->Header.Header.CommandType = op;
            cmd->Header.Header.TotalSize = sizeNeeded;
            cmd->Header.Header.SortKey = chain->m_LastSortKey;

            cmd->TypeSet = t;
            return true;
        }

        internal void AddEntitySharedComponentCommand<T>(EntityCommandBufferChain* chain, int sortKey, ECBCommand op, Entity e, int hashCode, object boxedObject)
            where T : struct
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            AddEntitySharedComponentCommand(chain, sortKey, op, e, hashCode, typeIndex, boxedObject);
        }

        internal void AddEntitySharedComponentCommand(EntityCommandBufferChain* chain, int sortKey, ECBCommand op, Entity e, int hashCode, TypeIndex typeIndex, object boxedObject)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(e == Entity.Null))
                throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddEntitySharedComponentCommand");
#endif
            var sizeNeeded = Align(sizeof(EntitySharedComponentCommand), ALIGN_64_BIT);

            ResetCommandBatching(chain);
            var data = (EntitySharedComponentCommand*)Reserve(chain, sortKey, sizeNeeded);

            data->Header.Header.CommandType = op;
            data->Header.Header.TotalSize = sizeNeeded;
            data->Header.Header.SortKey = chain->m_LastSortKey;
            data->Header.Entity = e;
            data->Header.IdentityIndex = 0;
            data->Header.BatchCount = 1;
            data->ComponentTypeIndex = typeIndex;
            data->HashCode = hashCode;

            // TODO(DOTS-3465): if boxedComponent contains Entity references to temporary Entities, they will not currently be fixed up.

            if (boxedObject != null)
            {
                data->GCNode.BoxedObject = GCHandle.Alloc(boxedObject);
                // We need to store all GCHandles on a cleanup list so we can dispose them later, regardless of if playback occurs or not.
                data->GCNode.Prev = chain->m_Cleanup->CleanupList;
                chain->m_Cleanup->CleanupList = &(data->GCNode);
            }
            else
            {
                data->GCNode.BoxedObject = new GCHandle();
            }
        }

        internal void AddEntityUnmanagedSharedComponentCommand<T>(EntityCommandBufferChain* chain, int sortKey, ECBCommand op, Entity e, int hashCode, void* componentData)
            where T : struct
        {
            // NOTE: This has to be sizeof not TypeManager.SizeInChunk since we use UnsafeUtility.CopyStructureToPtr
            //       even on zero size components.
            var typeSize = UnsafeUtility.SizeOf<T>();
            var typeIndex = TypeManager.GetTypeIndex<T>();
            AddEntityUnmanagedSharedComponentCommand(chain, sortKey, op, e, hashCode, typeIndex, typeSize, componentData);
        }

        internal void AddEntityUnmanagedSharedComponentCommand(EntityCommandBufferChain* chain, int sortKey, ECBCommand op, Entity e, int hashCode, TypeIndex typeIndex, int typeSize, void* componentData)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(e == Entity.Null))
                throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddEntityUnmanagedSharedComponentCommand");
#endif
            var sizeNeeded = Align(sizeof(EntityUnmanagedSharedComponentCommand) + typeSize, ALIGN_64_BIT);

            ResetCommandBatching(chain);
            var cmd = (EntityUnmanagedSharedComponentCommand*)Reserve(chain, sortKey, sizeNeeded);

            cmd->Header.Header.CommandType = op;
            cmd->Header.Header.TotalSize = sizeNeeded;
            cmd->Header.Header.SortKey = chain->m_LastSortKey;
            cmd->Header.Entity = e;
            cmd->Header.IdentityIndex = 0;
            cmd->Header.BatchCount = 1;
            cmd->ComponentTypeIndex = typeIndex;
            cmd->HashCode = hashCode;
            cmd->IsDefault = componentData == null ? (byte)1 : (byte)0;
            byte* componentValue = (byte*)(cmd + 1);
            if (componentData != null)
            {
                UnsafeUtility.MemCpy(componentValue, componentData, typeSize);
            }
            else
            {
                UnsafeUtility.MemSet(componentValue, 0, typeSize);
            }
            cmd->ValueRequiresEntityFixup = RequiresEntityFixUp(componentValue, typeIndex) ? (byte)1 : (byte)0;
        }

        internal byte* Reserve(EntityCommandBufferChain* chain, int sortKey, int size)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            var align = Align(size, ALIGN_64_BIT);
            if(align != size)
                Assert.IsTrue(false, $"Misaligned size. Expected alignment of {ALIGN_64_BIT} but was {align}. Unaligned access can cause crashes on platforms such as ARM.");
#endif
            if (Hint.Unlikely(chain->m_Head == null))
                EntityCommandBufferChain.InitChain(chain, m_Allocator);

            int newSortKey = sortKey;
            if (Hint.Unlikely(newSortKey < chain->m_LastSortKey))
            {
                // copy current chain to new next and reset current chain
                EntityCommandBufferChain* nextChain = (EntityCommandBufferChain*)Memory.Unmanaged.Allocate(sizeof(EntityCommandBufferChain), ALIGN_64_BIT, m_Allocator);
                *nextChain = *chain;
                EntityCommandBufferChain.InitChain(chain, m_Allocator);
                chain->m_NextChain = nextChain;
            }
            chain->m_LastSortKey = newSortKey;

            if (Hint.Unlikely(chain->m_Tail == null || chain->m_Tail->Capacity < size))
            {
                var chunkSize = math.max(m_MinimumChunkSize, size);

                var c = (ECBChunk*)Memory.Unmanaged.Allocate(sizeof(ECBChunk) + chunkSize, 16, m_Allocator);
                var prev = chain->m_Tail;
                c->Next = null;
                c->Prev = prev;
                c->Used = 0;
                c->Size = chunkSize;

                if (prev != null) prev->Next = c;

                if (chain->m_Head == null)
                {
                    chain->m_Head = c;
                    // This seems to be the best place to track the number of non-empty command buffer chunks
                    // during the recording process.
                    Interlocked.Increment(ref m_RecordedChainCount);
                }

                chain->m_Tail = c;
            }

            var offset = chain->m_Tail->Bump(size);
            var ptr = (byte*)chain->m_Tail + sizeof(ECBChunk) + offset;
            return ptr;
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public DynamicBuffer<T> CreateBufferCommand<T>(ECBCommand commandType, EntityCommandBufferChain* chain, int sortKey, Entity e, AtomicSafetyHandle bufferSafety, AtomicSafetyHandle arrayInvalidationSafety) where T : unmanaged, IBufferElementData
#else
        public DynamicBuffer<T> CreateBufferCommand<T>(ECBCommand commandType, EntityCommandBufferChain* chain, int sortKey, Entity e) where T : unmanaged, IBufferElementData
#endif
        {
            int internalCapacity;
            BufferHeader* header = AddEntityBufferCommand<T>(chain, sortKey, commandType, e, out internalCapacity);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safety = bufferSafety;
            AtomicSafetyHandle.UseSecondaryVersion(ref safety);
            var arraySafety = arrayInvalidationSafety;
            return new DynamicBuffer<T>(header, safety, arraySafety, false, false, 0, internalCapacity);
#else
            return new DynamicBuffer<T>(header, internalCapacity);
#endif
        }

        public void AppendToBufferCommand<T>(EntityCommandBufferChain* chain, int sortKey, Entity e, T element) where T : struct, IBufferElementData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(e == Entity.Null))
                throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AppendToBufferCommand");
#endif
            var typeIndex = TypeManager.GetTypeIndex<T>();
            // NOTE: This has to be sizeof not TypeManager.SizeInChunk since we use UnsafeUtility.CopyStructureToPtr
            //       even on zero size components.
            var typeSize = (short)UnsafeUtility.SizeOf<T>();
            var sizeNeeded = Align(sizeof(EntityComponentCommand) + typeSize, ALIGN_64_BIT);

            ResetCommandBatching(chain);
            var cmd = (EntityComponentCommand*)Reserve(chain, sortKey, sizeNeeded);

            cmd->Header.Header.CommandType = ECBCommand.AppendToBuffer;
            cmd->Header.Header.TotalSize = sizeNeeded;
            cmd->Header.Header.SortKey = chain->m_LastSortKey;
            cmd->Header.Entity = e;
            cmd->Header.IdentityIndex = 0;
            cmd->Header.BatchCount = 1;
            cmd->ComponentTypeIndex = typeIndex;
            cmd->ComponentSize = typeSize;
            byte* componentValue = (byte*)(cmd + 1);
            UnsafeUtility.CopyStructureToPtr(ref element, componentValue);
            cmd->ValueRequiresEntityFixup = TypeManager.HasEntityReferences(typeIndex) ? (byte)1 : (byte)0;
        }
    }

    /// <summary>
    /// Specifies if the <see cref="EntityCommandBuffer"/> can be played a single time or multiple times.
    /// </summary>
    public enum PlaybackPolicy
    {
        /// <summary>
        /// The <see cref="EntityCommandBuffer"/> can only be played once. After a first playback, the EntityCommandBuffer must be disposed.
        /// </summary>
        SinglePlayback,
        /// <summary>
        /// The <see cref="EntityCommandBuffer"/> can be played back more than once.
        /// </summary>
        /// <remarks>Even though the EntityCommandBuffer can be played back more than once, no commands can be added after the first playback.</remarks>
        MultiPlayback
    }

    /// <summary>
    /// Specifies when an <see cref="EntityQuery"/> passed to an <see cref="EntityCommandBuffer"/> should be evaluated.
    /// </summary>
    /// <remarks>
    /// This can significantly affect which entities are matched by the query, as well as the overall performance of the
    /// requested operation.
    /// </remarks>
    public enum EntityQueryCaptureMode
    {
        /// <summary>
        /// Request that the query's results be captured immediately, when the command is recorded.
        /// </summary>
        /// <remarks>
        /// The entire array of matching entities will be serialized into the command buffer, and this exact set of
        /// entities will be processed at playback time. This approach is far less efficient, but may lead to a more
        /// predictable set of entities being processed.
        ///
        /// At playback time, the command throws an error if one of these entities is destroyed before playback. (With
        /// safety checks enabled, an exception is thrown. Without safety checks, playback will perform invalid and
        /// unsafe memory access.)
        /// </remarks>
        AtRecord,

        /// <summary>
        /// Request that the query's results be captured when the corresponding command is played back.
        /// </summary>
        /// <remarks>
        /// Only a reference to the query itself is serialized into the command buffer. The requested operation is applied
        /// to the query during playback. This approach is generally far more efficient, but may lead to unexpected
        /// entities being processed (if entities which match the query are created or destroyed between recording the
        /// command buffer and playing it back).
        ///
        /// Since the serialized query is stored by reference, modifying or deleting the query after the
        /// command is recorded may affect the set of chunks and entities matched at playback time.
        /// </remarks>
        AtPlayback,
    }

    /// <summary>
    /// This attribute should be added to a public method in the `EntityCommandBuffer` class iff the following conditions are fulfilled:
    /// 1. The method is allowed to run inside of the Entities.ForEach() lambda function (one exception would be Playback(), since we do not
    /// want entity command buffers to be played back inside of Entities.ForEach());
    /// 2. Source-generation of the method when used inside Entities.ForEach() has been implemented.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    internal class SupportedInEntitiesForEach : Attribute
    {
    }

    /// <summary>
    ///     A thread-safe command buffer that can buffer commands that affect entities and components for later playback.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    [BurstCompile]
    public unsafe partial struct EntityCommandBuffer : IDisposable
    {
        /// <summary>
        ///     The minimum chunk size to allocate from the job allocator.
        /// </summary>
        /// We keep this relatively small as we don't want to overload the temp allocator in case people make a ton of command buffers.
        private const int kDefaultMinimumChunkSize = 4 * 1024;

        [NativeDisableUnsafePtrRestriction] internal EntityCommandBufferData* m_Data;

        internal int SystemID;
        internal SystemHandle OriginSystemHandle;
        internal int PassedPrePlaybackValidation; // non-zero if pre-playback validation ran on this ECB successfully; zero if it failed or didn't run at all.

        private struct ECBIDAllocator
        {
            public static readonly SharedStatic<int> Ref = SharedStatic<int>.GetOrCreate<EntityCommandBuffer, ECBIDAllocator>();
        }

        static readonly SharedStatic<int> _ms_CommandBufferIDAllocator = ECBIDAllocator.Ref;

        internal static int ms_CommandBufferIDAllocator
        {
            get => _ms_CommandBufferIDAllocator.Data;
            set => _ms_CommandBufferIDAllocator.Data = value;
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety0;
        private AtomicSafetyHandle m_BufferSafety;
        private AtomicSafetyHandle m_ArrayInvalidationSafety;
        private int m_SafetyReadOnlyCount;
        private int m_SafetyReadWriteCount;

        internal void WaitForWriterJobs()
        {
            AtomicSafetyHandle.EnforceAllBufferJobsHaveCompleted(m_Safety0);
            AtomicSafetyHandle.EnforceAllBufferJobsHaveCompleted(m_BufferSafety);
            AtomicSafetyHandle.EnforceAllBufferJobsHaveCompleted(m_ArrayInvalidationSafety);
        }

        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<EntityCommandBuffer>();
#endif
        // TODO(michalb): bugfix for https://jira.unity3d.com/browse/BUR-1767, remove when burst is upgraded to 1.7.2.
        static readonly ProfilerMarker k_ProfileEcbPlayback = new ProfilerMarker("EntityCommandBuffer.Playback");
        static readonly ProfilerMarker k_ProfileEcbDispose = new ProfilerMarker("EntityCommandBuffer.Dispose");
        /// <summary>
        ///     Allows controlling the size of chunks allocated from the temp job allocator to back the command buffer.
        /// </summary>
        /// Larger sizes are more efficient, but create more waste in the allocator.
        public int MinimumChunkSize
        {
            get { return m_Data->m_MinimumChunkSize > 0 ? m_Data->m_MinimumChunkSize : kDefaultMinimumChunkSize; }
            set { m_Data->m_MinimumChunkSize = Math.Max(0, value); }
        }

        /// <summary>
        /// Controls whether this command buffer should play back.
        /// </summary>
        ///
        /// This property is normally true, but can be useful to prevent
        /// the buffer from playing back when the user code is not in control
        /// of the site of playback.
        ///
        /// For example, is a buffer has been acquired from an EntityCommandBufferSystem and partially
        /// filled in with data, but it is discovered that the work should be aborted,
        /// this property can be set to false to prevent the buffer from playing back.
        public bool ShouldPlayback
        {
            get { return m_Data != null ? m_Data->m_ShouldPlayback : false; }
            set { if (m_Data != null) m_Data->m_ShouldPlayback = value; }
        }

        /// <summary>
        /// Returns true if the <see cref="EntityCommandBuffer"/> has not been initialized or no commands have been recorded.
        /// </summary>
        public bool IsEmpty => (m_Data == null) ? true : m_Data->m_RecordedChainCount == 0;

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        internal void EnforceSingleThreadOwnership()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (m_Data == null)
                throw new NullReferenceException("The EntityCommandBuffer has not been initialized! The EntityCommandBuffer needs to be passed an Allocator when created!");
#endif
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety0);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        internal void AssertDidNotPlayback()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (m_Data != null && m_Data->m_DidPlayback)
                throw new InvalidOperationException("The EntityCommandBuffer has already been played back and no further commands can be added.");
#endif
        }

        /// <summary>
        ///  Creates a new command buffer.
        /// </summary>
        /// <param name="allocator">Memory allocator to use for chunks and data</param>
        public EntityCommandBuffer(AllocatorManager.AllocatorHandle allocator)
            : this(allocator, PlaybackPolicy.SinglePlayback)
        {
        }

        /// <summary>
        ///  Creates a new command buffer.
        /// </summary>
        /// <param name="label">Memory allocator to use for chunks and data</param>
        /// <param name="playbackPolicy">Specifies if the EntityCommandBuffer can be played a single time or more than once.</param>
        public EntityCommandBuffer(Allocator label, PlaybackPolicy playbackPolicy)
        : this((AllocatorManager.AllocatorHandle)label, playbackPolicy)
        {
        }

        /// <summary>
        ///  Creates a new command buffer.
        /// </summary>
        /// <param name="allocator">Memory allocator to use for chunks and data</param>
        /// <param name="playbackPolicy">Specifies if the EntityCommandBuffer can be played a single time or more than once.</param>
        public EntityCommandBuffer(AllocatorManager.AllocatorHandle allocator, PlaybackPolicy playbackPolicy)
        {
            m_Data = (EntityCommandBufferData*)Memory.Unmanaged.Allocate(sizeof(EntityCommandBufferData), UnsafeUtility.AlignOf<EntityCommandBufferData>(), allocator);
            m_Data->m_Allocator = allocator;
            m_Data->m_PlaybackPolicy = playbackPolicy;
            m_Data->m_MinimumChunkSize = kDefaultMinimumChunkSize;
            m_Data->m_ShouldPlayback = true;
            m_Data->m_DidPlayback = false;
            m_Data->m_BufferWithFixupsCount = 0;
            m_Data->m_BufferWithFixups = new UnsafeAtomicCounter32(&m_Data->m_BufferWithFixupsCount);
            m_Data->m_CommandBufferID = --ms_CommandBufferIDAllocator;

            m_Data->m_MainThreadChain = default; // initial chains are lazily initialized when the first command is recorded

            m_Data->m_ThreadedChains = null;
            m_Data->m_RecordedChainCount = 0;

            SystemID = 0;
            PassedPrePlaybackValidation = 0;
            OriginSystemHandle = default;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety0 = CollectionHelper.CreateSafetyHandle(allocator);

            // Used for all buffers returned from the API, so we can invalidate them once Playback() has been called.
            m_BufferSafety = AtomicSafetyHandle.Create();
            // Used to invalidate array aliases to buffers
            m_ArrayInvalidationSafety = AtomicSafetyHandle.Create();

            allocator.AddSafetyHandle(m_Safety0); // so that when allocator rewinds, this handle will invalidate
            allocator.AddSafetyHandle(m_BufferSafety); // so that when allocator rewinds, this handle will invalidate
            allocator.AddSafetyHandle(m_ArrayInvalidationSafety); // so that when allocator rewinds, this handle will invalidate

            m_SafetyReadOnlyCount = 0;
            m_SafetyReadWriteCount = 3;

            CollectionHelper.SetStaticSafetyId(ref m_Safety0, ref s_staticSafetyId.Data, "Unity.Entities.EntityCommandBuffer");
            AtomicSafetyHandle.SetStaticSafetyId(ref m_BufferSafety, s_staticSafetyId.Data); // uses id created above
            AtomicSafetyHandle.SetStaticSafetyId(ref m_ArrayInvalidationSafety, s_staticSafetyId.Data); // uses id created above
#endif
            m_Data->m_Entity = new Entity();
            m_Data->m_Entity.Version = m_Data->m_CommandBufferID;
            m_Data->m_BufferWithFixups.Reset();
        }

        /// <summary>
        /// Is true if the <see cref="EntityCommandBuffer"/> has been initialized correctly.
        /// </summary>
        public bool IsCreated   { get { return m_Data != null; } }

        [BurstCompile]
        static void DisposeInternal(ref EntityCommandBuffer ecb)
        {
            if (!ecb.IsCreated)
                return;
            k_ProfileEcbDispose.Begin();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref ecb.m_Safety0);
            AtomicSafetyHandle.Release(ecb.m_ArrayInvalidationSafety);
            AtomicSafetyHandle.Release(ecb.m_BufferSafety);
#endif

            // There's no need to walk chains and dispose individual allocations if the provided allocator
            // uses auto-dispose; they'll all be freed automatically when the allocator rewinds.
            bool disposeChains = !ecb.m_Data->m_Allocator.IsAutoDispose;
            // ...however, under some conditions we need to walk the chains anyway and manually free their allocations,
            // even with auto-dispose allocators.
            if (ecb.m_Data->m_ForceFullDispose &&
                (!ecb.m_Data->m_DidPlayback || ecb.m_Data->m_PlaybackPolicy == PlaybackPolicy.MultiPlayback))
            {
                disposeChains = true;
            }
            if (ecb.m_Data != null && disposeChains)
            {
                ecb.FreeChain(&ecb.m_Data->m_MainThreadChain, ecb.m_Data->m_PlaybackPolicy, ecb.m_Data->m_DidPlayback);

                if (ecb.m_Data->m_ThreadedChains != null)
                {
#if UNITY_2022_2_14F1_OR_NEWER
                    int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
                    int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
                    for (int i = 0; i < maxThreadCount; ++i)
                    {
                        ecb.FreeChain(&ecb.m_Data->m_ThreadedChains[i], ecb.m_Data->m_PlaybackPolicy, ecb.m_Data->m_DidPlayback);
                    }

                    ecb.m_Data->DestroyForParallelWriter();
                }

                Memory.Unmanaged.Free(ecb.m_Data, ecb.m_Data->m_Allocator);
            }
            ecb.m_Data = null;
            k_ProfileEcbDispose.End();
        }

        /// <summary>
        /// Deals with freeing and releasing unmanaged memory allocated by the entity command buffer.
        /// </summary>
        public void Dispose()
        {
            DisposeInternal(ref this); // forward to Burst-compiled function
        }

        private void FreeChain(EntityCommandBufferChain* chain, PlaybackPolicy playbackPolicy, bool didPlayback)
        {
            if (chain->m_Head == null)
                return; // skip uninitialized chains;
            bool first = true;
            while (chain != null)
            {
                ECBInterop.CleanupManaged(chain);        // Buffers played in ecbs which can be played back more than once are always copied during playback.
                if (playbackPolicy == PlaybackPolicy.MultiPlayback || !didPlayback)
                {
                    var bufferCleanupList = chain->m_Cleanup->BufferCleanupList;
                    while (bufferCleanupList != null)
                    {
                        var prev = bufferCleanupList->Prev;
                        BufferHeader.Destroy(&bufferCleanupList->TempBuffer);
                        bufferCleanupList = prev;
                    }
                }
                chain->m_Cleanup->BufferCleanupList = null;

                // Arrays of entities captured from an input EntityQuery at record time are always cleaned up
                // at Dispose time.
                var entityArraysCleanupList = chain->m_Cleanup->EntityArraysCleanupList;
                while (entityArraysCleanupList != null)
                {
                    var prev = entityArraysCleanupList->Prev;
                    Memory.Unmanaged.Free(entityArraysCleanupList->Ptr, m_Data->m_Allocator);
                    entityArraysCleanupList = prev;
                }
                chain->m_Cleanup->EntityArraysCleanupList = null;
                Memory.Unmanaged.Free(chain->m_Cleanup, m_Data->m_Allocator);
                while (chain->m_Tail != null)
                {
                    var prev = chain->m_Tail->Prev;
                    Memory.Unmanaged.Free(chain->m_Tail, m_Data->m_Allocator);
                    chain->m_Tail = prev;
                }
                chain->m_Head = null;

                var chainToFree = chain;
                chain = chain->m_NextChain;
                chainToFree->m_NextChain = null;
                if (!first)
                {
                    // we need to free the chain we have just visited, but only if it is not the first one
                    Memory.Unmanaged.Free(chainToFree, m_Data->m_Allocator);
                }
                first = false;
            }
        }

        internal int MainThreadSortKey => Int32.MaxValue;
        private const bool kBatchableCommand = true;

        /// <summary>Records a command to create an entity with specified archetype.</summary>
        /// <remarks>At playback, this command throws an error if the archetype contains the <see cref="Prefab"/> tag.</remarks>
        /// <param name="archetype">The archetype of the new entity.</param>
        /// <returns>An entity that is deferred and will be fully realized when this EntityCommandBuffer is played back.</returns>
        /// <exception cref="ArgumentException">Throws if the archetype is null.</exception>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public Entity CreateEntity(EntityArchetype archetype)
        {
            archetype.CheckValidEntityArchetype();
            return _CreateEntity(archetype);
        }

        /// <summary>Records a command to create an entity with no components.</summary>
        /// <returns>An entity that is deferred and will be fully realized when this EntityCommandBuffer is played back.</returns>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public Entity CreateEntity()
        {
            EntityArchetype archetype = new EntityArchetype();
            return _CreateEntity(archetype);
        }

        private Entity _CreateEntity(EntityArchetype archetype)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            int index = --m_Data->m_Entity.Index;
            m_Data->AddCreateCommand(&m_Data->m_MainThreadChain, MainThreadSortKey, ECBCommand.CreateEntity, index, archetype, kBatchableCommand);
            return m_Data->m_Entity;
        }

        /// <summary>Records a command to create an entity with specified entity prefab.</summary>
        /// <remarks>An instantiated entity has the same components and component values as the
        /// prefab entity, minus the Prefab tag component.
        /// If the source entity was destroyed before playback, this command throws an error.</remarks>
        /// <param name="e">The entity prefab.</param>
        /// <returns>An entity that is deferred and will be fully realized when this EntityCommandBuffer is played back.</returns>
        /// <exception cref="ArgumentNullException"> Thrown if Entity e is null and if safety checks are enabled.</exception>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public Entity Instantiate(Entity e)
        {
            CheckEntityNotNull(e);
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            int index = --m_Data->m_Entity.Index;
            m_Data->AddEntityCommand(&m_Data->m_MainThreadChain, MainThreadSortKey, ECBCommand.InstantiateEntity,
                index, e, kBatchableCommand);
            return m_Data->m_Entity;
        }

        /// <summary>Records a command to create a NativeArray of entities with specified entity prefab.</summary>
        /// <remarks>An instantiated entity has the same components and component values as the prefab entity, minus the Prefab tag component.
        /// If the source entity was destroyed before playback, this command throws an error.</remarks>
        /// <param name="e">The entity prefab.</param>
        /// <param name="entities">The NativeArray of entities that will be populated with realized entities when this EntityCommandBuffer is played back.</param>
        /// <exception cref="ArgumentNullException"> Thrown if Entity e is null and if safety checks are enabled.</exception>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void Instantiate(Entity e, NativeArray<Entity> entities)
        {
            CheckEntityNotNull(e);
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            var entity = m_Data->m_Entity;
            int baseIndex = Interlocked.Add(ref m_Data->m_Entity.Index, -entities.Length) + entities.Length - 1;
            for (int i=0; i<entities.Length; ++i)
            {
                entity.Index = baseIndex - i;
                entities[i] = entity;
            }
            m_Data->AddMultipleEntityCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                ECBCommand.InstantiateEntity, baseIndex, entities.Length, e, kBatchableCommand);
        }

        /// <summary>Records a command to destroy an entity.</summary>
        /// <remarks>At playback, this command throws an error if the entity is
        /// [deferred](xref:systems-entity-command-buffers), or was destroyed between recording and playback, or if the entity
        /// has the <see cref="Prefab"/> tag.</remarks>
        /// <param name="e">The entity to destroy.</param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void DestroyEntity(Entity e)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            m_Data->AddEntityCommand(&m_Data->m_MainThreadChain, MainThreadSortKey, ECBCommand.DestroyEntity, 0, e, false);
        }

        /// <summary>Records a command to destroy a NativeArray of entities.</summary>
        /// <remarks>At playback, this command only runs if the entity count is greater than 0.
        /// This command throws an error if any of the entities [are deferred](xref:systems-entity-command-buffers),
        /// were destroyed between recording and playback, or if any of the entities have
        /// the <see cref="Prefab"/> tag.</remarks>
        /// <param name="entities">The NativeArray of entities to destroy.</param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void DestroyEntity(NativeArray<Entity> entities)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            var entitiesCopy = m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
            m_Data->AppendMultipleEntitiesCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                ECBCommand.DestroyMultipleEntities, entitiesCopy, entities.Length, containsDeferredEntities);
        }

        /// <summary>Records a command to add a dynamic buffer to an entity.</summary>
        /// <remarks>At playback, if the entity already has this type of dynamic buffer,
        /// this method sets the dynamic buffer contents. If the entity doesn't have a
        /// <see cref="DynamicBuffer{T}"/> component that stores elements of type T, then
        /// this method adds a DynamicBuffer component with the provided contents. If the
        /// entity is destroyed before playback, or is deferred, an error is thrown.</remarks>
        /// <param name="e">The entity to add the dynamic buffer to.</param>
        /// <typeparam name="T">The <see cref="IBufferElementData"/> type stored by the <see cref="DynamicBuffer{T}"/>.</typeparam>
        /// <returns>The <see cref="DynamicBuffer{T}"/> that will be added when the command plays back.</returns>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public DynamicBuffer<T> AddBuffer<T>(Entity e) where T : unmanaged, IBufferElementData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return m_Data->CreateBufferCommand<T>(ECBCommand.AddBuffer, &m_Data->m_MainThreadChain, MainThreadSortKey, e, m_BufferSafety, m_ArrayInvalidationSafety);
#else
            return m_Data->CreateBufferCommand<T>(ECBCommand.AddBuffer, &m_Data->m_MainThreadChain, MainThreadSortKey, e);
#endif
        }

        /// <summary>Records a command to set a dynamic buffer on an entity.</summary>
        /// <remarks>At playback this command throws an error if the entity is destroyed
        /// before playback, or if [it's deferred](xref:systems-entity-command-buffers),
        /// or if the entity doesn't have a <see cref="DynamicBuffer{T}"/> component that
        /// stores elements of type T.</remarks>
        /// <param name="e">The entity to set the dynamic buffer on.</param>
        /// <typeparam name="T">The <see cref="IBufferElementData"/> type stored by the <see cref="DynamicBuffer{T}"/>.</typeparam>
        /// <returns>The <see cref="DynamicBuffer{T}"/> that will be set when the command plays back.</returns>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public DynamicBuffer<T> SetBuffer<T>(Entity e) where T : unmanaged, IBufferElementData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return m_Data->CreateBufferCommand<T>(ECBCommand.SetBuffer, &m_Data->m_MainThreadChain, MainThreadSortKey, e, m_BufferSafety, m_ArrayInvalidationSafety);
#else
            return m_Data->CreateBufferCommand<T>(ECBCommand.SetBuffer, &m_Data->m_MainThreadChain, MainThreadSortKey, e);
#endif
        }

        /// <summary>Records a command to append a single element to the end of a dynamic buffer component.</summary>
        /// <remarks>At playback this command throws an error if the entity is destroyed
        /// before playback, or if [it's deferred](xref:systems-entity-command-buffers),
        /// or if the entity doesn't have a <see cref="DynamicBuffer{T}"/> component that
        /// stores elements of type T.</remarks>
        /// <param name="e">The entity to which the dynamic buffer belongs.</param>
        /// <param name="element">The new element to add to the <see cref="DynamicBuffer{T}"/> component.</param>
        /// <typeparam name="T">The <see cref="IBufferElementData"/> type stored by the <see cref="DynamicBuffer{T}"/>.</typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void AppendToBuffer<T>(Entity e, T element) where T : struct, IBufferElementData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            m_Data->AppendToBufferCommand<T>(&m_Data->m_MainThreadChain, MainThreadSortKey, e, element);
        }

        /// <summary> Records a command to add component of type T to an entity. </summary>
        /// <remarks>At playback, if the entity already has this type of component, the value will just be set.
        /// Throws an error if this entity is destroyed before playback, if this entity is still [deferred](xref:systems-entity-command-buffers),
        /// if T is type Entity or <see cref="Prefab"/>, or adding this component type makes the archetype too large.</remarks>
        /// <param name="e"> The entity to have the component added. </param>
        /// <param name="component">The value to add on the new component in playback for the entity.</param>
        /// <typeparam name="T"> The type of component to add. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void AddComponent<T>(Entity e, T component) where T : unmanaged, IComponentData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            m_Data->AddEntityComponentTypeWithValueCommand(&m_Data->m_MainThreadChain, MainThreadSortKey, ECBCommand.AddComponent, e, component);
        }

        /// <summary> Records a command to add component of type T to a NativeArray of entities. </summary>
        /// <remarks>At playback, if any entity already has this type of component, the value will just be set.
        /// Throws an error if any entity is destroyed before playback, if any entity is still [deferred](xref:systems-entity-command-buffers),
        /// if T is type Entity or <see cref="Prefab"/>, or adding this component type makes the archetype too large.</remarks>
        /// <param name="entities"> The NativeArray of entities to have the component added. </param>
        /// <param name="component">The value to add on the new component in playback for all entities in the NativeArray.</param>
        /// <typeparam name="T"> The type of component to add. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void AddComponent<T>(NativeArray<Entity> entities, T component) where T : unmanaged, IComponentData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            var entitiesCopy = m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
            m_Data->AppendMultipleEntitiesComponentCommandWithValue(&m_Data->m_MainThreadChain, MainThreadSortKey,
                ECBCommand.AddComponentForMultipleEntities, entitiesCopy, entities.Length, containsDeferredEntities, component);
        }

        /// <summary> Records a command to add component of type T to an entity. </summary>
        /// <remarks>At playback, this command does nothing if the entity already has the component.
        /// Throws an error if this entity is destroyed before playback, if this entity is still [deferred](xref:systems-entity-command-buffers),
        /// if T is type Entity or <see cref="Prefab"/>, or adding this component type makes the archetype too large.</remarks>
        /// <param name="e"> The entity to have the component added. </param>
        /// <typeparam name="T"> The type of component to add. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void AddComponent<T>(Entity e) where T : unmanaged, IComponentData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            m_Data->AddEntityComponentTypeWithoutValueCommand(&m_Data->m_MainThreadChain, MainThreadSortKey, ECBCommand.AddComponent, e, ComponentType.ReadWrite<T>());
        }

        /// <summary> Records a command to add component of type T to a NativeArray of entities. </summary>
        /// <remarks>At playback, if an entity already has this component, it is skipped.
        /// Throws an error if any entity is destroyed before playback, or if any entity is still [deferred](xref:systems-entity-command-buffers),
        /// if T is type Entity or <see cref="Prefab"/>, or adding this component type makes the archetype too large.</remarks>
        /// <param name="entities"> The NativeArray of entities to have the component added. </param>
        /// <typeparam name="T"> The type of component to add. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void AddComponent<T>(NativeArray<Entity> entities) where T : unmanaged, IComponentData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            var entitiesCopy = m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
            m_Data->AppendMultipleEntitiesComponentCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                ECBCommand.AddComponentForMultipleEntities, entitiesCopy, entities.Length, containsDeferredEntities, ComponentType.ReadWrite<T>());
        }

        /// <summary> Records a command to add a component to an entity. </summary>
        /// <remarks>At playback, this command does nothing if the entity already has the component.
        /// Throws an error if any entity is destroyed before playback, or if any entity is still [deferred](xref:systems-entity-command-buffers),
        /// if component is type Entity or <see cref="Prefab"/>, or adding this component type makes the archetype too large.</remarks>
        /// <param name="e"> The entity to get the additional component. </param>
        /// <param name="componentType"> The type of component to add. </param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void AddComponent(Entity e, ComponentType componentType)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            m_Data->AddEntityComponentTypeWithoutValueCommand(&m_Data->m_MainThreadChain, MainThreadSortKey, ECBCommand.AddComponent, e, componentType);
        }

        /// <summary> Records a command to add a component to an entity. </summary>
        /// <remarks>At playback, this command does nothing if the entity already has the component.
        /// Throws an error if any entity is destroyed before playback, or if any entity is still [deferred](xref:systems-entity-command-buffers),
        /// if component is type Entity or <see cref="Prefab"/>, or adding this component type makes the archetype too large.</remarks>
        /// <param name="typeIndex"> The TypeIndex of the component being set. </param>
        /// <param name="typeSize"> The Size of the type of the component being set. </param>
        /// <param name="componentDataPtr"> The pointer to the data of the component to be copied. </param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        internal void UnsafeAddComponent(Entity e, TypeIndex typeIndex, int typeSize, void* componentDataPtr)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            m_Data->UnsafeAddEntityComponentCommand(&m_Data->m_MainThreadChain, MainThreadSortKey, ECBCommand.AddComponent, e, typeIndex, typeSize, componentDataPtr);
        }

        /// <summary> Records a command to add a component to a NativeArray of entities. </summary>
        /// <remarks>At playback, if an entity already has this component, it is skipped.
        /// Throws an error if any entity is destroyed before playback, or if any entity is still [deferred](xref:systems-entity-command-buffers),
        /// if component is type Entity or <see cref="Prefab"/>, or adding this component type makes the archetype too large.</remarks>
        /// <param name="entities"> The NativeArray of entities to have the component added. </param>
        /// <param name="componentType"> The type of component to add. </param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void AddComponent(NativeArray<Entity> entities, ComponentType componentType)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            var entitiesCopy = m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
            m_Data->AppendMultipleEntitiesComponentCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                ECBCommand.AddComponentForMultipleEntities, entitiesCopy, entities.Length, containsDeferredEntities, componentType);
        }


        /// <summary> Records a command to add one or more components to an entity. </summary>
        /// <remarks>At playback, you can include a component type that the entity already has.
        /// Throws an error if this entity is destroyed before playback, if this entity is still [deferred](xref:systems-entity-command-buffers),
        /// if any component type is type Entity or <see cref="Prefab"/>, or adding a component type makes the archetype too large.</remarks>
        /// <param name="e"> The entity to get additional components. </param>
        /// <param name="componentTypeSet"> The types of components to add. </param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void AddComponent(Entity e, in ComponentTypeSet componentTypeSet)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            m_Data->AddEntityComponentTypesCommand(&m_Data->m_MainThreadChain, MainThreadSortKey, ECBCommand.AddMultipleComponents, e, componentTypeSet);
        }

        /// <summary> Records a command to add one or more components to a NativeArray of entities. </summary>
        /// <remarks>At playback, you can include a component type that any of the entities already have.
        /// Throws an error if this entity is destroyed before playback, if this entity is still [deferred](xref:systems-entity-command-buffers),
        /// if any component type is type Entity or <see cref="Prefab"/>, or adding a component type makes the archetype too large.</remarks>
        /// <param name="entities"> The NativeArray of entities to have the components added. </param>
        /// <param name="componentTypeSet"> The types of components to add. </param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void AddComponent(NativeArray<Entity> entities, in ComponentTypeSet componentTypeSet)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            var entitiesCopy = m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
            m_Data->AppendMultipleEntitiesMultipleComponentsCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                ECBCommand.AddMultipleComponentsForMultipleEntities, entitiesCopy, entities.Length, containsDeferredEntities, componentTypeSet);
        }

        /// <summary> Records a command to set a component value on an entity.</summary>
        /// <remarks> At playback, this command throws an error if the entity is destroyed before playback,
        /// if this entity is still [deferred](xref:systems-entity-command-buffers), if the entity doesn't have the component type,
        /// if the entity has the <see cref="Prefab"/> tag, or if T is zero sized.</remarks>
        /// <param name="e"> The entity to set the component value of. </param>
        /// <param name="component"> The component value to set. </param>
        /// <typeparam name="T"> The type of component to set. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void SetComponent<T>(Entity e, T component) where T : unmanaged, IComponentData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            m_Data->AddEntityComponentTypeWithValueCommand(&m_Data->m_MainThreadChain, MainThreadSortKey, ECBCommand.SetComponent, e, component);
        }

        /// <summary> Records a command to set a component value on an entity.</summary>
        /// <remarks> At playback, this command throws an error if this entity is destroyed before playback,
        /// if this entity is still deferred, if the entity doesn't have the component type,
        /// if the entity has the <see cref="Prefab"/> tag, or if T is zero sized.</remarks>
        /// <param name="e"> The entity to set the component value of. </param>
        /// <param name="typeIndex"> The TypeIndex of the component being set. </param>
        /// <param name="typeSize"> The Size of the type of the component being set. </param>
        /// <param name="componentDataPtr"> The pointer to the data of the component to be copied. </param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        internal void UnsafeSetComponent(Entity e, TypeIndex typeIndex, int typeSize, void* componentDataPtr)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            m_Data->UnsafeAddEntityComponentCommand(&m_Data->m_MainThreadChain, MainThreadSortKey, ECBCommand.SetComponent, e, typeIndex, typeSize, componentDataPtr);
        }

        /// <summary>
        /// Records a command to add or remove the <see cref="Disabled"/> component. By default EntityQuery does not include entities containing the Disabled component.
        /// Enabled entities are processed by systems, disabled entities are not.
        ///
        /// If the entity was converted from a prefab and thus has a <see cref="LinkedEntityGroup"/> component, the entire group will be enabled or disabled.
        /// </summary>
        /// <remarks> At playback, this command throws an error if this entity is destroyed before playback,
        /// if the entity has the <see cref="Prefab"/> tag, or if this entity is still deferred.</remarks>
        /// <param name="e">The entity whose component should be enabled or disabled.</param>
        /// <param name="value">True if the specified component should be enabled, or false if it should be disabled.</param>
        [GenerateTestsForBurstCompatibility]
        public void SetEnabled(Entity e, bool value)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();

            m_Data->AddEntityEnabledCommand(&m_Data->m_MainThreadChain, MainThreadSortKey, ECBCommand.SetEntityEnabled, e, value);
        }

        /// <summary>
        /// Records a command to enable or disable a <see cref="ComponentType"/> on the specified <see cref="Entity"/>. This operation
        /// does not cause a structural change, or affect the value of the component. For the purposes
        /// of EntityQuery matching, an entity with a disabled component will behave as if it does not have that component.
        /// </summary>
        /// <remarks> At playback, this command throws an error if this entity is destroyed before playback,
        /// if this entity is still deferred, if the entity has the <see cref="Prefab"/> tag, or if the entity doesn't have the component type.</remarks>
        /// <typeparam name="T">The component type to enable or disable. This type must implement the
        /// <see cref="IEnableableComponent"/> interface.</typeparam>
        /// <param name="e">The entity whose component should be enabled or disabled.</param>
        /// <param name="value">True if the specified component should be enabled, or false if it should be disabled.</param>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleEnableableComponent) })]
        [SupportedInEntitiesForEach]
        public void SetComponentEnabled<T>(Entity e, bool value) where T : struct, IEnableableComponent
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            m_Data->AddEntityComponentEnabledCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                ECBCommand.SetComponentEnabled, e, TypeManager.GetTypeIndex<T>(), value);
        }
        /// <summary>
        /// Records a command to enable or disable a <see cref="ComponentType"/> on the specified <see cref="Entity"/>. This operation
        /// does not cause a structural change, or affect the value of the component. For the purposes
        /// of EntityQuery matching, an entity with a disabled component will behave as if it does not have that component.
        /// </summary>
        /// <remarks> At playback, this command throws an error if this entity is destroyed before playback,
        /// if this entity is still deferred, if the entity has the <see cref="Prefab"/> tag, or if the entity doesn't have the component type.</remarks>
        /// <param name="e">The entity whose component should be enabled or disabled.</param>
        /// <param name="componentType">The component type to enable or disable. This type must implement the
        /// <see cref="IEnableableComponent"/> interface.</param>
        /// <param name="value">True if the specified component should be enabled, or false if it should be disabled.</param>
        [SupportedInEntitiesForEach]
        public void SetComponentEnabled(Entity e, ComponentType componentType, bool value)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            m_Data->AddEntityComponentEnabledCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                ECBCommand.SetComponentEnabled, e, componentType.TypeIndex, value);
        }

        /// <summary> Records a command to set a name of an entity if Debug Names is enabled.</summary>
        /// <remarks> At playback, this command throws an error if this entity is destroyed before playback,
        /// if this entity is still deferred, if the entity has the <see cref="Prefab"/> tag, or if the EntityNameStore has reached its limit.</remarks>
        /// <param name="e"> The entity to set the name value of. </param>
        /// <param name="name"> The name to set. </param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void SetName(Entity e, in FixedString64Bytes name)
        {
#if !DOTS_DISABLE_DEBUG_NAMES
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            m_Data->AddEntityNameCommand(&m_Data->m_MainThreadChain, MainThreadSortKey, ECBCommand.SetName, e, name);
#endif //!DOTS_DISABLE_DEBUG_NAMES
        }

        /// <summary> Records a command to remove component of type T from an entity. </summary>
        /// <remarks> At playback, it's not an error if the entity doesn't have component T.
        /// Will throw an error if this entity is destroyed before playback,
        /// if this entity is still deferred, or if T is type Entity or <see cref="Prefab"/>.</remarks>
        /// <param name="e"> The entity to have the component removed. </param>
        /// <typeparam name="T"> The type of component to remove. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void RemoveComponent<T>(Entity e)
        {
            RemoveComponent(e, ComponentType.ReadWrite<T>());
        }

        /// <summary> Records a command to remove component of type T from a NativeArray of entities. </summary>
        /// <remarks>At playback, it's not an error if any entity doesn't have component T.
        /// Will throw an error if one of these entities is destroyed before playback,
        /// if one of these entities is still deferred, or if T is type Entity or <see cref="Prefab"/>.</remarks>
        /// <param name="entities"> The NativeArray of entities to have the component removed. </param>
        /// <typeparam name="T"> The type of component to remove. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void RemoveComponent<T>(NativeArray<Entity> entities)
        {
            RemoveComponent(entities, ComponentType.ReadWrite<T>());
        }

        /// <summary> Records a command to remove a component from an entity. </summary>
        /// <remarks>At playback, it's not an error if the entity doesn't have the component type.
        /// Will throw an error if this entity is destroyed before playback,
        /// if this entity is still deferred, or if the component type is Entity or <see cref="Prefab"/>.</remarks>
        /// <param name="e"> The entity to have the component removed. </param>
        /// <param name="componentType"> The type of component to remove. </param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void RemoveComponent(Entity e, ComponentType componentType)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            m_Data->AddEntityComponentTypeWithoutValueCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                ECBCommand.RemoveComponent, e, componentType);
        }

        /// <summary> Records a command to remove one or more components from a NativeArray of entities. </summary>
        /// <remarks>At playback, it's not an error if any entity doesn't have the component type.
        /// Will throw an error if one of these entities is destroyed before playback,
        /// if one of these entities is still deferred, or if the component type is Entity or <see cref="Prefab"/>.</remarks>
        /// <param name="entities"> The NativeArray of entities to have the component removed. </param>
        /// <param name="componentType"> The type of component to remove. </param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void RemoveComponent(NativeArray<Entity> entities, ComponentType componentType)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            var entitiesCopy = m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
            m_Data->AppendMultipleEntitiesComponentCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                ECBCommand.RemoveComponentForMultipleEntities, entitiesCopy, entities.Length, containsDeferredEntities, componentType);
        }

        /// <summary> Records a command to remove one or more components from an entity. </summary>
        /// <remarks>At playback, it's not an error if the entity doesn't have one of the component types.
        /// Will throw an error if this entity is destroyed before playback,
        /// if this entity is still deferred, or if any of the component types are Entity or <see cref="Prefab"/>.</remarks>
        /// <param name="e"> The entity to have components removed. </param>
        /// <param name="componentTypeSet"> The types of components to remove. </param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void RemoveComponent(Entity e, in ComponentTypeSet componentTypeSet)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            m_Data->AddEntityComponentTypesCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                ECBCommand.RemoveMultipleComponents, e, componentTypeSet);
        }

        /// <summary> Records a command to remove one or more components from a NativeArray of entities. </summary>
        /// <remarks>At playback, it's not an error if any entity doesn't have one of the component types.
        /// Will throw an error if one of these entities is destroyed before playback,
        /// if one of these entities is still deferred, or if any of the component types are Entity or <see cref="Prefab"/>.</remarks>
        /// <param name="entities"> The NativeArray of entities to have components removed. </param>
        /// <param name="componentTypeSet"> The types of components to remove. </param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void RemoveComponent(NativeArray<Entity> entities, in ComponentTypeSet componentTypeSet)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            var entitiesCopy = m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
            m_Data->AppendMultipleEntitiesMultipleComponentsCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                ECBCommand.RemoveMultipleComponentsForMultipleEntities, entitiesCopy, entities.Length, containsDeferredEntities, componentTypeSet);
        }

        /// <summary>Records a command to add a component to all entities matching a query.</summary>
        /// <remarks>
        /// Does not affect entities which already have the component.
        /// </remarks>
        /// <param name="entityQuery">The query specifying the entities to which the component is added. </param>
        /// <param name="componentType">The type of component to add.</param>
        /// <param name="queryCaptureMode">Controls when the entities matching <paramref name="entityQuery"/> are computed and captured.</param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void AddComponent(EntityQuery entityQuery, ComponentType componentType, EntityQueryCaptureMode queryCaptureMode)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            if (queryCaptureMode == EntityQueryCaptureMode.AtRecord)
                m_Data->AppendMultipleEntitiesComponentCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                    ECBCommand.AddComponentForMultipleEntities, entityQuery, componentType);
            else
                m_Data->AppendEntityQueryComponentCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                    ECBCommand.AddComponentForEntityQuery, entityQuery, componentType);
        }
        /// <summary>Obsolete. Use <see cref="AddComponent(EntityQuery,ComponentType,EntityQueryCaptureMode)"/> instead.</summary>
        /// <param name="entityQuery">The query specifying the entities to which the component is added.</param>
        /// <param name="componentType">The type of component to add.</param>
        [SupportedInEntitiesForEach]
        [Obsolete("This method now takes an extra parameter to control when the query is evaluated. To preserve the current semantics, use EntityQueryCaptureMode.AtRecord (RemovedAfter Entities 2.0)")]
        public void AddComponent(EntityQuery entityQuery, ComponentType componentType)
            => AddComponent(entityQuery, componentType, EntityQueryCaptureMode.AtRecord);
        /// <summary>Obsolete. Use <see cref="AddComponent(EntityQuery,ComponentType,EntityQueryCaptureMode)"/> instead.</summary>
        /// <param name="entityQuery">The query specifying the entities to which the component is added. </param>
        /// <param name="componentType">The type of component to add.</param>
        [SupportedInEntitiesForEach]
        [Obsolete("Use AddComponent (RemovedAfter Entities 1.0) (UnityUpgradable) -> AddComponent(*)")]
        public void AddComponentForEntityQuery(EntityQuery entityQuery, ComponentType componentType)
            => AddComponent(entityQuery, componentType, EntityQueryCaptureMode.AtRecord);

        /// <summary>Records a command to add a component to all entities matching a query.</summary>
        /// <remarks>
        /// Does not affect entities which already have the component.
        /// </remarks>
        /// <param name="entityQuery">The query specifying the entities to which the component is added. </param>
        /// <param name="queryCaptureMode">Controls when the entities matching <paramref name="entityQuery"/> are computed and captured.</param>
        /// <typeparam name="T"> The type of component to add. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void AddComponent<T>(EntityQuery entityQuery, EntityQueryCaptureMode queryCaptureMode)
            => AddComponent(entityQuery, ComponentType.ReadWrite<T>(), queryCaptureMode);
        /// <summary>Obsolete. Use <see cref="AddComponent{T}(EntityQuery,EntityQueryCaptureMode)"/> instead.</summary>
        /// <param name="entityQuery">The query specifying the entities to which the component is added.</param>
        /// <typeparam name="T"> The type of component to add. </typeparam>
        [SupportedInEntitiesForEach]
        [Obsolete("This method now takes an extra parameter to control when the query is evaluated. To preserve the current semantics, use EntityQueryCaptureMode.AtRecord (RemovedAfter Entities 2.0)")]
        public void AddComponent<T>(EntityQuery entityQuery)
            => AddComponent(entityQuery, ComponentType.ReadWrite<T>(), EntityQueryCaptureMode.AtRecord);
        /// <summary>Obsolete. Use <see cref="AddComponent{T}(EntityQuery,EntityQueryCaptureMode)"/> instead.</summary>
        /// <param name="entityQuery">The query specifying the entities to which the component is added. </param>
        /// <typeparam name="T"> The type of component to add. </typeparam>
        [SupportedInEntitiesForEach]
        [Obsolete("Use AddComponent (RemovedAfter Entities 1.0) (UnityUpgradable) -> AddComponent<T>(*)")]
        public void AddComponentForEntityQuery<T>(EntityQuery entityQuery)
            => AddComponent(entityQuery, ComponentType.ReadWrite<T>(), EntityQueryCaptureMode.AtRecord);


        /// <summary>Records a command to add a component to all entities matching a query. Also sets the value of this new component on all the matching entities.</summary>
        /// <remarks>The set of entities matching the query is 'captured' in the method call, and the recorded command stores an array of all these entities.
        ///
        /// Entities which already have the component type will have the component set to the value.
        ///
        /// At playback, this command throws an error if one of these entities is destroyed before playback. (With safety checks enabled, an exception is thrown. Without safety checks,
        /// playback will perform invalid and unsafe memory access.)
        /// </remarks>
        /// <param name="entityQuery">The query specifying the entities to which the component is added. </param>
        /// <param name="value">The value to set on the new component in playback for all entities matching the query.</param>
        /// <typeparam name="T"> The type of component to add. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void AddComponent<T>(EntityQuery entityQuery, T value) where T : unmanaged, IComponentData
        {
            // TODO(DOTS-8709): There is no efficient capture-at-playback path for this operation. Add one, or remove this method.
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            m_Data->AppendMultipleEntitiesComponentCommandWithValue(&m_Data->m_MainThreadChain, MainThreadSortKey,
                ECBCommand.AddComponentForMultipleEntities, entityQuery, value);
        }
        /// <summary>Obsolete. Use <see cref="AddComponent{T}(EntityQuery, T)"/> instead.</summary>
        /// <param name="entityQuery">The query specifying the entities to which the component is added. </param>
        /// <param name="value">The value to set on the new component in playback for all entities matching the query.</param>
        /// <typeparam name="T"> The type of component to add. </typeparam>
        [SupportedInEntitiesForEach]
        [Obsolete("Use AddComponent (RemovedAfter Entities 1.0) (UnityUpgradable) -> AddComponent<T>(*)")]
        public void AddComponentForEntityQuery<T>(EntityQuery entityQuery, T value) where T : unmanaged, IComponentData
            => AddComponent<T>(entityQuery, value);

        /// <summary>Records a command to add multiple components to all entities matching a query.</summary>
        /// <remarks>The set of entities matching the query is 'captured' in the method call, and the recorded command stores an array of all these entities.
        ///
        /// Some matching entities may already have some or all of the specified components. After this operation, all matching entities will have all of the components.
        ///
        /// At playback, this command throws an error if one of these entities is destroyed before playback. (With safety checks enabled, an exception is thrown. Without safety checks,
        /// playback will perform invalid and unsafe memory access.)
        /// </remarks>
        /// <param name="entityQuery">The query specifying the entities to which the components are added. </param>
        /// <param name="componentTypeSet">The types of components to add.</param>
        /// <param name="queryCaptureMode">Controls when the entities matching <paramref name="entityQuery"/> are computed and captured.</param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void AddComponent(EntityQuery entityQuery, in ComponentTypeSet componentTypeSet, EntityQueryCaptureMode queryCaptureMode)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            if (queryCaptureMode == EntityQueryCaptureMode.AtRecord)
                m_Data->AppendMultipleEntitiesMultipleComponentsCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                    ECBCommand.AddMultipleComponentsForMultipleEntities, entityQuery, componentTypeSet);
            else
                m_Data->AppendEntityQueryComponentTypeSetCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                    ECBCommand.AddMultipleComponentsForEntityQuery, entityQuery, componentTypeSet);
        }
        /// <summary>Obsolete. Use <see cref="AddComponent(EntityQuery,ComponentTypeSet,EntityQueryCaptureMode)"/> instead.</summary>
        /// <param name="entityQuery">The query specifying the entities to which the component is added.</param>
        /// <param name="componentTypeSet">The types of components to add.</param>
        [SupportedInEntitiesForEach]
        [Obsolete("This method now takes an extra parameter to control when the query is evaluated. To preserve the current semantics, use EntityQueryCaptureMode.AtRecord (RemovedAfter Entities 2.0)")]
        public void AddComponent(EntityQuery entityQuery, in ComponentTypeSet componentTypeSet)
            => AddComponent(entityQuery, componentTypeSet, EntityQueryCaptureMode.AtRecord);
        /// <summary>Obsolete. Use <see cref="AddComponent(EntityQuery,ComponentTypeSet,EntityQueryCaptureMode)"/> instead.</summary>
        /// <param name="entityQuery">The query specifying the entities to which the components are added. </param>
        /// <param name="componentTypeSet">The types of components to add.</param>
        [SupportedInEntitiesForEach]
        [Obsolete("Use AddComponent (RemovedAfter Entities 1.0) (UnityUpgradable) -> AddComponent(*)")]
        public void AddComponentForEntityQuery(EntityQuery entityQuery, in ComponentTypeSet componentTypeSet)
            => AddComponent(entityQuery, componentTypeSet, EntityQueryCaptureMode.AtRecord);


        /// <summary> Records a command to add a possibly-managed shared component to all entities matching a query.</summary>
        /// <remarks>
        /// Entities which already have the component type will have the component set to the value.
        /// </remarks>
        /// <param name="entityQuery"> The query specifying which entities to add the component value to. </param>
        /// <param name="component"> The component value to add. </param>
        /// <param name="queryCaptureMode">Controls when the entities matching <paramref name="entityQuery"/> are computed and captured.</param>
        /// <typeparam name="T"> The type of shared component to set. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void AddSharedComponentManaged<T>(EntityQuery entityQuery, T component, EntityQueryCaptureMode queryCaptureMode) where T : struct, ISharedComponentData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();

            var typeIndex = TypeManager.GetTypeIndex<T>();
            var isManaged = TypeManager.IsManagedSharedComponent(typeIndex);
            var isDefaultObject = IsDefaultObject(ref component, out var hashCode);

            if (isManaged)
            {
                if (queryCaptureMode == EntityQueryCaptureMode.AtRecord)
                    m_Data->AppendMultipleEntitiesComponentCommandWithSharedValue<T>(&m_Data->m_MainThreadChain,
                        MainThreadSortKey,
                        ECBCommand.AddSharedComponentWithValueForMultipleEntities,
                        entityQuery,
                        hashCode,
                        isDefaultObject ? null : component);
                else
                    m_Data->AppendEntityQueryComponentCommandWithSharedValue<T>(&m_Data->m_MainThreadChain,
                        MainThreadSortKey,
                        ECBCommand.AddSharedComponentWithValueForEntityQuery,
                        entityQuery,
                        hashCode,
                        isDefaultObject ? null : component);
            }
            else
            {
                var componentAddr = UnsafeUtility.AddressOf(ref component);
                if (queryCaptureMode == EntityQueryCaptureMode.AtRecord)
                {
                    m_Data->AppendMultipleEntitiesCommand_WithUnmanagedSharedValue<T>(
                        &m_Data->m_MainThreadChain,
                        MainThreadSortKey,
                        ECBCommand.AddUnmanagedSharedComponentValueForMultipleEntities,
                        entityQuery,
                        false,
                        hashCode,
                        isDefaultObject ? null : componentAddr);
                }
                else
                {
                    m_Data->AppendEntityQueryComponentCommandWithUnmanagedSharedValue<T>(
                        &m_Data->m_MainThreadChain,
                        MainThreadSortKey,
                        ECBCommand.AddUnmanagedSharedComponentValueForEntityQuery,
                        entityQuery,
                        hashCode,
                        isDefaultObject ? null : componentAddr);
                }
            }
        }
        /// <summary>Obsolete. Use <see cref="AddSharedComponentManaged{T}(EntityQuery,T,EntityQueryCaptureMode)"/> instead.</summary>
        /// <param name="entityQuery">The query specifying the entities to which the component is added.</param>
        /// <param name="component"> The component value to add. </param>
        /// <typeparam name="T"> The type of shared component to set. </typeparam>
        [SupportedInEntitiesForEach]
        [Obsolete("This method now takes an extra parameter to control when the query is evaluated. To preserve the current semantics, use EntityQueryCaptureMode.AtRecord (RemovedAfter Entities 2.0)")]
        public void AddSharedComponentManaged<T>(EntityQuery entityQuery, T component) where T : struct, ISharedComponentData
            => AddSharedComponentManaged(entityQuery, component, EntityQueryCaptureMode.AtRecord);
        /// <summary>Obsolete. Use <see cref="AddSharedComponentManaged{T}(EntityQuery,T,EntityQueryCaptureMode)"/> instead.</summary>
        /// <param name="entityQuery"> The query specifying which entities to add the component value to. </param>
        /// <param name="component"> The component value to add. </param>
        /// <typeparam name="T"> The type of shared component to set. </typeparam>
        [SupportedInEntitiesForEach]
        [Obsolete("Use AddSharedComponentManaged (RemovedAfter Entities 1.0) (UnityUpgradable) -> AddSharedComponent<T>(*)")]
        public void AddSharedComponentForEntityQuery<T>(EntityQuery entityQuery, T component) where T : unmanaged, ISharedComponentData
            => AddSharedComponentManaged<T>(entityQuery, component, EntityQueryCaptureMode.AtRecord);

        /// <summary> Records a command to add a unmanaged shared component to all entities matching a query.</summary>
        /// <remarks>
        /// Entities which already have the component type will have the component set to the value.
        /// </remarks>
        /// <param name="entityQuery"> The query specifying which entities to add the component value to. </param>
        /// <param name="component"> The component value to add. </param>
        /// <param name="queryCaptureMode">Controls when the entities matching <paramref name="entityQuery"/> are computed and captured.</param>
        /// <typeparam name="T"> The type of shared component to set. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void AddSharedComponent<T>(EntityQuery entityQuery, T component, EntityQueryCaptureMode queryCaptureMode)
            where T : unmanaged, ISharedComponentData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();

            var typeIndex = TypeManager.GetTypeIndex<T>();
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.IsFalse(TypeManager.IsManagedSharedComponent(typeIndex));
#endif
            var isDefaultObject = IsDefaultObjectUnmanaged(ref component, out var hashCode);

            var componentAddr = UnsafeUtility.AddressOf(ref component);
            if (queryCaptureMode == EntityQueryCaptureMode.AtRecord)
            {
                m_Data->AppendMultipleEntitiesCommand_WithUnmanagedSharedValue<T>(
                    &m_Data->m_MainThreadChain,
                    MainThreadSortKey,
                    ECBCommand.AddUnmanagedSharedComponentValueForMultipleEntities,
                    entityQuery,
                    false,
                    hashCode,
                    isDefaultObject ? null : componentAddr);
            }
            else
            {
                m_Data->AppendEntityQueryComponentCommandWithUnmanagedSharedValue<T>(
                    &m_Data->m_MainThreadChain,
                    MainThreadSortKey,
                    ECBCommand.AddUnmanagedSharedComponentValueForEntityQuery,
                    entityQuery,
                    hashCode,
                    isDefaultObject ? null : componentAddr);
            }
        }
        /// <summary>Obsolete. Use <see cref="AddSharedComponent{T}(EntityQuery,T,EntityQueryCaptureMode)"/> instead.</summary>
        /// <param name="entityQuery">The query specifying the entities to which the component is added.</param>
        /// <param name="component"> The component value to add. </param>
        /// <typeparam name="T"> The type of shared component to set. </typeparam>
        [SupportedInEntitiesForEach]
        [Obsolete("This method now takes an extra parameter to control when the query is evaluated. To preserve the current semantics, use EntityQueryCaptureMode.AtRecord (RemovedAfter Entities 2.0)")]
        public void AddSharedComponent<T>(EntityQuery entityQuery, T component) where T : unmanaged, ISharedComponentData
                => AddSharedComponent(entityQuery, component, EntityQueryCaptureMode.AtRecord);


        /// <summary> Records a command to add a hybrid component and set its value for all entities matching a query.</summary>
        /// <remarks>The set of entities matching the query is 'captured' in the method call, and the recorded command stores an array of all these entities.
        ///
        /// At playback, this command throws an error if one of these entities is destroyed before playback.
        /// Entities which already have the component type will have the component set to the value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown in playback if one or more of the entities has been destroyed. (With safety checks disabled,
        /// playback will perform invalid and unsafe memory access.).</exception>
        /// <param name="entityQuery"> The query specifying which entities to add the component value to.</param>
        /// <param name="componentData"> The component object to add. </param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        /// <exception cref="ArgumentNullException">Throws if componentData is null.</exception>
        [SupportedInEntitiesForEach]
        public void AddComponentObject(EntityQuery entityQuery, object componentData)
        {
            // TODO(DOTS-8709): There is no efficient capture-at-playback path for this operation. Add one, or remove this method.
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (componentData == null)
                throw new ArgumentNullException(nameof(componentData));
#endif

            ComponentType type = componentData.GetType();
            m_Data->AppendMultipleEntitiesComponentCommandWithObject(&m_Data->m_MainThreadChain, MainThreadSortKey,
                ECBCommand.AddComponentObjectForMultipleEntities, entityQuery, componentData, type);
        }
        /// <summary>Obsolete. Use <see cref="AddComponentObject(EntityQuery, object)"/> instead.</summary>
        /// <param name="entityQuery"> The query specifying which entities to add the component value to.</param>
        /// <param name="componentData"> The component object to add. </param>
        [SupportedInEntitiesForEach]
        [Obsolete("Use AddComponentObject (RemovedAfter Entities 1.0) (UnityUpgradable) -> AddComponentObject(*)")]
        public void AddComponentObjectForEntityQuery(EntityQuery entityQuery, object componentData)
            => AddComponentObject(entityQuery, componentData);

        /// <summary> Records a command to set a hybrid component value for all entities matching a query.</summary>
        /// <remarks>The set of entities matching the query is 'captured' in the method call, and the recorded command stores an array of all these entities.
        ///
        /// At playback, this command throws an error if one of these entities is destroyed before playback,
        /// if any entity has the <see cref="Prefab"/> tag, or if any entity does not have the component type at playback.
        /// Playback Entities which already have the component type will have the component set to the value.</remarks>
        /// <exception cref="InvalidOperationException">Thrown in playback if one or more of the entities does not have the component type or has been destroyed. (With safety checks disabled,
        /// playback will perform invalid and unsafe memory access.).</exception>
        /// <param name="entityQuery"> The query specifying which entities to set the component value for.</param>
        /// <param name="componentData"> The component object to set.</param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        /// <exception cref="ArgumentNullException">Throws if componentData is null.</exception>
        [SupportedInEntitiesForEach]
        public void SetComponentObject(EntityQuery entityQuery, object componentData)
        {
            // TODO(DOTS-8709): There is no efficient capture-at-playback path for this operation. Add one, or remove this method.
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (componentData == null)
                throw new ArgumentNullException(nameof(componentData));
#endif

            ComponentType type = componentData.GetType();
            m_Data->AppendMultipleEntitiesComponentCommandWithObject(&m_Data->m_MainThreadChain, MainThreadSortKey,
                ECBCommand.SetComponentObjectForMultipleEntities, entityQuery, componentData, type);
        }
        /// <summary> Obsolete. Use <see cref="SetComponentObject(EntityQuery, object)"/> instead.</summary>
        /// <param name="entityQuery"> The query specifying which entities to set the component value for.</param>
        /// <param name="componentData"> The component object to set.</param>
        [SupportedInEntitiesForEach]
        [Obsolete("Use SetComponentObject (RemovedAfter Entities 1.0) (UnityUpgradable) -> SetComponentObject(*)")]
        public void SetComponentObjectForEntityQuery(EntityQuery entityQuery, object componentData)
            => SetComponentObject(entityQuery, componentData);

        /// <summary> Records a command to set a possibly-managed shared component value on all entities matching a query.</summary>
        /// <remarks>
        /// Fails if any of the entities do not have the type of shared component. [todo: should it be required that the component type is included in the query?]
        /// </remarks>
        /// <param name="entityQuery"> The query specifying which entities to add the component value to. </param>
        /// <param name="component"> The component value to add. </param>
        /// <param name="queryCaptureMode">Controls when the entities matching <paramref name="entityQuery"/> are computed and captured.</param>
        /// <typeparam name="T"> The type of shared component to set. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void SetSharedComponentManaged<T>(EntityQuery entityQuery, T component, EntityQueryCaptureMode queryCaptureMode) where T : struct, ISharedComponentData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();

            var typeIndex = TypeManager.GetTypeIndex<T>();
            var isManaged = TypeManager.IsManagedSharedComponent(typeIndex);
            var isDefaultObject = IsDefaultObject(ref component, out var hashCode);

            if (isManaged)
            {
                if (queryCaptureMode == EntityQueryCaptureMode.AtRecord)
                    m_Data->AppendMultipleEntitiesComponentCommandWithSharedValue<T>(&m_Data->m_MainThreadChain,
                        MainThreadSortKey,
                        ECBCommand.SetSharedComponentValueForMultipleEntities,
                        entityQuery,
                        hashCode,
                        isDefaultObject ? null : component);
                else
                    m_Data->AppendEntityQueryComponentCommandWithSharedValue<T>(&m_Data->m_MainThreadChain,
                        MainThreadSortKey,
                        ECBCommand.SetSharedComponentValueForEntityQuery,
                        entityQuery,
                        hashCode,
                        isDefaultObject ? null : component);
            }
            else
            {
                var componentAddr = UnsafeUtility.AddressOf(ref component);
                if (queryCaptureMode == EntityQueryCaptureMode.AtRecord)
                    m_Data->AppendMultipleEntitiesCommand_WithUnmanagedSharedValue<T>(
                        &m_Data->m_MainThreadChain,
                        MainThreadSortKey,
                        ECBCommand.SetUnmanagedSharedComponentValueForMultipleEntities,
                        entityQuery,
                        false,
                        hashCode,
                        isDefaultObject ? null : componentAddr);
                else
                    m_Data->AppendEntityQueryComponentCommandWithUnmanagedSharedValue<T>(
                        &m_Data->m_MainThreadChain,
                        MainThreadSortKey,
                        ECBCommand.SetUnmanagedSharedComponentValueForEntityQuery,
                        entityQuery,
                        hashCode,
                        isDefaultObject ? null : componentAddr);
            }
        }
        /// <summary>Obsolete. Use <see cref="SetSharedComponentManaged{T}(EntityQuery,T,EntityQueryCaptureMode)"/> instead.</summary>
        /// <param name="entityQuery">The query specifying the entities to which the component is added.</param>
        /// <param name="component"> The component value to set. </param>
        /// <typeparam name="T"> The type of shared component to set. </typeparam>
        [SupportedInEntitiesForEach]
        [Obsolete("This method now takes an extra parameter to control when the query is evaluated. To preserve the current semantics, use EntityQueryCaptureMode.AtRecord (RemovedAfter Entities 2.0)")]
        public void SetSharedComponentManaged<T>(EntityQuery entityQuery, T component) where T : struct, ISharedComponentData
            => SetSharedComponentManaged(entityQuery, component, EntityQueryCaptureMode.AtRecord);
        /// <summary>Obsolete. Use <see cref="SetSharedComponentManaged{T}(EntityQuery, T, EntityQueryCaptureMode)"/> instead.</summary>
        /// <param name="entityQuery"> The query specifying which entities to add the component value to. </param>
        /// <param name="component"> The component value to set. </param>
        /// <typeparam name="T"> The type of shared component to set. </typeparam>
        [SupportedInEntitiesForEach]
        [Obsolete("Use SetSharedComponentManaged (RemovedAfter Entities 1.0) (UnityUpgradable) -> SetSharedComponentManaged<T>(*)")]
        public void SetSharedComponentForEntityQueryManaged<T>(EntityQuery entityQuery, T component) where T : struct, ISharedComponentData
            => SetSharedComponentManaged<T>(entityQuery, component, EntityQueryCaptureMode.AtRecord);

        /// <summary> Records a command to set an unmanaged shared component value on all entities matching a query.</summary>
        /// <remarks>
        /// Fails if any of the entities do not have the type of shared component. [todo: should it be required that the component type is included in the query?]
        /// </remarks>
        /// <param name="entityQuery"> The query specifying which entities to add the component value to. </param>
        /// <param name="component"> The component value to add. </param>
        /// <param name="queryCaptureMode">Controls when the entities matching <paramref name="entityQuery"/> are computed and captured.</param>
        /// <typeparam name="T"> The type of shared component to set. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void SetSharedComponent<T>(EntityQuery entityQuery, T component, EntityQueryCaptureMode queryCaptureMode)
            where T : unmanaged, ISharedComponentData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();

            var typeIndex = TypeManager.GetTypeIndex<T>();
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.IsFalse(TypeManager.IsManagedSharedComponent(typeIndex));
#endif
            var isDefaultObject = IsDefaultObjectUnmanaged(ref component, out var hashCode);

            var componentAddr = UnsafeUtility.AddressOf(ref component);
            if (queryCaptureMode == EntityQueryCaptureMode.AtRecord)
                m_Data->AppendMultipleEntitiesCommand_WithUnmanagedSharedValue<T>(
                    &m_Data->m_MainThreadChain,
                    MainThreadSortKey,
                    ECBCommand.SetUnmanagedSharedComponentValueForMultipleEntities,
                    entityQuery,
                    false,
                    hashCode,
                    isDefaultObject ? null : componentAddr);
            else
                m_Data->AppendEntityQueryComponentCommandWithUnmanagedSharedValue<T>(
                    &m_Data->m_MainThreadChain,
                    MainThreadSortKey,
                    ECBCommand.SetUnmanagedSharedComponentValueForEntityQuery,
                    entityQuery,
                    hashCode,
                    isDefaultObject ? null : componentAddr);
        }
        /// <summary>Obsolete. Use <see cref="SetSharedComponent{T}(EntityQuery,T,EntityQueryCaptureMode)"/> instead.</summary>
        /// <param name="entityQuery">The query specifying the entities to which the component is added.</param>
        /// <param name="component"> The component value to set. </param>
        /// <typeparam name="T"> The type of shared component to set. </typeparam>
        [SupportedInEntitiesForEach]
        [Obsolete("This method now takes an extra parameter to control when the query is evaluated. To preserve the current semantics, use EntityQueryCaptureMode.AtRecord (RemovedAfter Entities 2.0)")]
        public void SetSharedComponent<T>(EntityQuery entityQuery, T component) where T : unmanaged, ISharedComponentData
            => SetSharedComponent(entityQuery, component, EntityQueryCaptureMode.AtRecord);
        /// <summary>Obsolete. Use <see cref="SetSharedComponent{T}(Unity.Entities.EntityQuery,T,EntityQueryCaptureMode)"/> instead.</summary>
        /// <param name="entityQuery"> The query specifying which entities to add the component value to. </param>
        /// <param name="component"> The component value to add. </param>
        /// <typeparam name="T"> The type of shared component to set. </typeparam>
        [SupportedInEntitiesForEach]
        [Obsolete("Use SetSharedComponent (RemovedAfter Entities 1.0) (UnityUpgradable) -> SetSharedComponent<T>(*)")]
        public void SetSharedComponentForEntityQuery<T>(EntityQuery entityQuery, T component) where T : unmanaged, ISharedComponentData
            => SetSharedComponent<T>(entityQuery, component, EntityQueryCaptureMode.AtRecord);

        /// <summary>Records a command to remove a component from all entities matching a query.</summary>
        /// <remarks>
        /// Does not affect entities already missing the component.
        /// </remarks>
        /// <param name="entityQuery">The query specifying the entities from which the component is removed. </param>
        /// <param name="componentType">The types of component to remove.</param>
        /// <param name="queryCaptureMode">Controls when the entities matching <paramref name="entityQuery"/> are computed and captured.</param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void RemoveComponent(EntityQuery entityQuery, ComponentType componentType, EntityQueryCaptureMode queryCaptureMode)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            if (queryCaptureMode == EntityQueryCaptureMode.AtRecord)
                m_Data->AppendMultipleEntitiesComponentCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                    ECBCommand.RemoveComponentForMultipleEntities, entityQuery, componentType);
            else
                m_Data->AppendEntityQueryComponentCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                    ECBCommand.RemoveComponentForEntityQuery, entityQuery, componentType);
        }
        /// <summary>Obsolete. Use <see cref="RemoveComponent(EntityQuery,ComponentType,EntityQueryCaptureMode)"/> instead.</summary>
        /// <param name="entityQuery">The query specifying the entities to which the component is added.</param>
        /// <param name="componentType">The type of component to add.</param>
        [SupportedInEntitiesForEach]
        [Obsolete("This method now takes an extra parameter to control when the query is evaluated. To preserve the current semantics, use EntityQueryCaptureMode.AtRecord (RemovedAfter Entities 2.0)")]
        public void RemoveComponent(EntityQuery entityQuery, ComponentType componentType)
            => RemoveComponent(entityQuery, componentType, EntityQueryCaptureMode.AtRecord);
        /// <summary>Obsolete. Use <see cref="RemoveComponent(EntityQuery, ComponentType, EntityQueryCaptureMode)"/> instead.</summary>
        /// <param name="entityQuery">The query specifying the entities from which the component is removed. </param>
        /// <param name="componentType">The types of component to remove.</param>
        [SupportedInEntitiesForEach]
        [Obsolete("Use RemoveComponent (RemovedAfter Entities 1.0) (UnityUpgradable) -> RemoveComponent(*)")]
        public void RemoveComponentForEntityQuery(EntityQuery entityQuery, ComponentType componentType)
            => RemoveComponent(entityQuery, componentType, EntityQueryCaptureMode.AtRecord);


        /// <summary>Records a command to remove a component from all entities matching a query.</summary>
        /// <remarks>
        /// Does not affect entities already missing the component.
        /// </remarks>
        /// <param name="entityQuery">The query specifying the entities from which the component is removed. </param>
        /// <param name="queryCaptureMode">Controls when the entities matching <paramref name="entityQuery"/> are computed and captured.</param>
        /// <typeparam name="T"> The type of component to remove. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void RemoveComponent<T>(EntityQuery entityQuery, EntityQueryCaptureMode queryCaptureMode)
            => RemoveComponent(entityQuery, ComponentType.ReadWrite<T>(), queryCaptureMode);
        /// <summary>Obsolete. Use <see cref="RemoveComponent{T}(EntityQuery,EntityQueryCaptureMode)"/> instead.</summary>
        /// <param name="entityQuery">The query specifying the entities to which the component is added.</param>
        [SupportedInEntitiesForEach]
        [Obsolete("This method now takes an extra parameter to control when the query is evaluated. To preserve the current semantics, use EntityQueryCaptureMode.AtRecord (RemovedAfter Entities 2.0)")]
        public void RemoveComponent<T>(EntityQuery entityQuery)
            => RemoveComponent(entityQuery, ComponentType.ReadWrite<T>(), EntityQueryCaptureMode.AtRecord);
        /// <summary>Obsolete. Use <see cref="RemoveComponent{T}(EntityQuery)"/> instead.</summary>
        /// <param name="entityQuery">The query specifying the entities from which the component is removed. </param>
        /// <typeparam name="T"> The type of component to remove. </typeparam>
        [SupportedInEntitiesForEach]
        [Obsolete("Use RemoveComponent (RemovedAfter Entities 1.0) (UnityUpgradable) -> RemoveComponent<T>(*)")]
        public void RemoveComponentForEntityQuery<T>(EntityQuery entityQuery)
            => RemoveComponent<T>(entityQuery, EntityQueryCaptureMode.AtRecord);


        /// <summary>Records a command to remove multiple components from all entities matching a query.</summary>
        /// <remarks>The set of entities matching the query is 'captured' in the method call, and the recorded command stores an array of all these entities.
        ///
        /// Some matching entities may already be missing some or all of the specified components. After this operation, all matching entities will have none of the components.
        ///
        /// At playback, this command throws an error if one of these entities is destroyed before playback. (With safety checks enabled, an exception is thrown. Without safety checks,
        /// playback will perform invalid and unsafe memory access.)
        /// </remarks>
        /// <param name="entityQuery">The query specifying the entities from which the components are removed. </param>
        /// <param name="componentTypeSet">The types of components to remove.</param>
        /// <param name="queryCaptureMode">Controls when the entities matching <paramref name="entityQuery"/> are computed and captured.</param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void RemoveComponent(EntityQuery entityQuery, in ComponentTypeSet componentTypeSet, EntityQueryCaptureMode queryCaptureMode)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            if (queryCaptureMode == EntityQueryCaptureMode.AtRecord)
                m_Data->AppendMultipleEntitiesMultipleComponentsCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                    ECBCommand.RemoveMultipleComponentsForMultipleEntities, entityQuery, componentTypeSet);
            else
                m_Data->AppendEntityQueryComponentTypeSetCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                    ECBCommand.RemoveMultipleComponentsForEntityQuery, entityQuery, componentTypeSet);
        }
        /// <summary>Obsolete. Use <see cref="RemoveComponent(EntityQuery,ComponentTypeSet,EntityQueryCaptureMode)"/> instead.</summary>
        /// <param name="entityQuery">The query specifying the entities from which the components are removed. </param>
        /// <param name="componentTypeSet">The types of components to remove.</param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        [Obsolete("This method now takes an extra parameter to control when the query is evaluated. To preserve the current semantics, use EntityQueryCaptureMode.AtRecord (RemovedAfter Entities 2.0)")]
        public void RemoveComponent(EntityQuery entityQuery, in ComponentTypeSet componentTypeSet)
            => RemoveComponent(entityQuery, componentTypeSet, EntityQueryCaptureMode.AtRecord);
        /// <summary>Obsolete. Use <see cref="RemoveComponent(EntityQuery,ComponentTypeSet,EntityQueryCaptureMode)"/> instead.</summary>
        /// <param name="entityQuery">The query specifying the entities from which the components are removed. </param>
        /// <param name="componentTypeSet">The types of components to remove.</param>
        [SupportedInEntitiesForEach]
        [Obsolete("Use RemoveComponent (RemovedAfter Entities 1.0) (UnityUpgradable) -> RemoveComponent(*)")]
        public void RemoveComponentForEntityQuery(EntityQuery entityQuery, in ComponentTypeSet componentTypeSet)
            => RemoveComponent(entityQuery, componentTypeSet, EntityQueryCaptureMode.AtRecord);

        /// <summary>Records a command to destroy all entities matching a query.</summary>
        /// <param name="entityQuery">The query specifying the entities to destroy.</param>
        /// <param name="queryCaptureMode">Controls when the entities matching <paramref name="entityQuery"/> are computed and captured.</param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void DestroyEntity(EntityQuery entityQuery, EntityQueryCaptureMode queryCaptureMode)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            if (queryCaptureMode == EntityQueryCaptureMode.AtRecord)
                m_Data->AppendMultipleEntitiesCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                    ECBCommand.DestroyMultipleEntities, entityQuery);
            else
                m_Data->AppendEntityQueryCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                    ECBCommand.DestroyForEntityQuery, entityQuery);
        }
        /// <summary>Obsolete. Use <see cref="DestroyEntity(EntityQuery,EntityQueryCaptureMode)"/> instead.</summary>
        /// <param name="entityQuery">The query specifying the entities to destroy.</param>
        /// <typeparam name="T"> The type of shared component to set. </typeparam>
        [SupportedInEntitiesForEach]
        [Obsolete("This method now takes an extra parameter to control when the query is evaluated. To preserve the current semantics, use EntityQueryCaptureMode.AtRecord (RemovedAfter Entities 2.0)")]
        public void DestroyEntity(EntityQuery entityQuery)
            => DestroyEntity(entityQuery, EntityQueryCaptureMode.AtRecord);
        /// <summary>Obsolete. Use <see cref="DestroyEntity(Unity.Entities.EntityQuery, EntityQueryCaptureMode)"/> instead.</summary>
        /// <param name="entityQuery">The query specifying the entities to destroy.</param>
        [SupportedInEntitiesForEach]
        [Obsolete("Use DestroyEntity (RemovedAfter Entities 1.0) (UnityUpgradable) -> DestroyEntity(*)")]
        public void DestroyEntitiesForEntityQuery(EntityQuery entityQuery)
            => DestroyEntity(entityQuery, EntityQueryCaptureMode.AtRecord);

        static bool IsDefaultObject<T>(ref T component, out int hashCode) where T : struct, ISharedComponentData
        {
            var defaultValue = default(T);

            hashCode = TypeManager.GetHashCode(ref component);
            return TypeManager.Equals(ref defaultValue, ref component);
        }

        static bool IsDefaultObjectUnmanaged<T>(ref T component, out int hashCode) where T : unmanaged, ISharedComponentData
        {
            var defaultValue = default(T);

            hashCode = TypeManager.SharedComponentGetHashCode(UnsafeUtility.AddressOf(ref component), TypeManager.GetTypeIndex<T>());
            return TypeManager.SharedComponentEquals(UnsafeUtility.AddressOf(ref defaultValue),
                UnsafeUtility.AddressOf(ref component),
                TypeManager.GetTypeIndex<T>());
        }

        /// <summary> Records a command to add a possibly-managed shared component value on an entity.</summary>
        /// <remarks>At playback, this command throws an error if this entity is destroyed before playback,
        /// if this entity is still deferred, if adding this shared component exceeds the maximum number of shared components,
        /// or adding a component type makes the archetype too large.</remarks>
        /// <param name="e"> The entity to add the shared component value to. </param>
        /// <param name="sharedComponent"> The shared component value to add. </param>
        /// <typeparam name="T"> The type of shared component to add. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void AddSharedComponentManaged<T>(Entity e, T sharedComponent) where T : struct, ISharedComponentData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();

            var typeIndex = TypeManager.GetTypeIndex<T>();
            var isManaged = TypeManager.IsManagedSharedComponent(typeIndex);
            var isDefaultObject = IsDefaultObject(ref sharedComponent, out var hashCode);

            if (isManaged)
            {
                m_Data->AddEntitySharedComponentCommand<T>(&m_Data->m_MainThreadChain,
                    MainThreadSortKey,
                    ECBCommand.AddSharedComponentData,
                    e,
                    hashCode,
                    isDefaultObject ? null : sharedComponent);
            }
            else
            {
                var componentData = UnsafeUtility.AddressOf(ref sharedComponent);
                m_Data->AddEntityUnmanagedSharedComponentCommand<T>(&m_Data->m_MainThreadChain,
                    MainThreadSortKey,
                    ECBCommand.AddUnmanagedSharedComponentData,
                    e,
                    hashCode,
                    isDefaultObject ? null : componentData);
            }
        }


        /// <summary> Records a command to add an unmanaged shared component value on an entity.</summary>
        /// <remarks>At playback, this command throws an error if this entity is destroyed before playback,
        /// if this entity is still deferred, if adding this shared component exceeds the maximum number of shared components,
        /// or adding a component type makes the archetype too large.</remarks>
        /// <param name="e"> The entity to add the shared component value to. </param>
        /// <param name="sharedComponent"> The shared component value to add. </param>
        /// <typeparam name="T"> The type of shared component to add. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void AddSharedComponent<T>(Entity e, T sharedComponent) where T : unmanaged, ISharedComponentData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();

            var componentAddr = UnsafeUtility.AddressOf(ref sharedComponent);

            var isDefaultObject = IsDefaultObjectUnmanaged(ref sharedComponent, out var hashCode);
            m_Data->AddEntityUnmanagedSharedComponentCommand<T>(&m_Data->m_MainThreadChain,
                MainThreadSortKey,
                ECBCommand.AddUnmanagedSharedComponentData,
                e,
                hashCode,
                isDefaultObject ? null : componentAddr);
        }

        /// <summary> Records a command to add a possibly-managed shared component value on a NativeArray of entities.</summary>
        /// <remarks>At playback, this command throws an error if any entity is destroyed before playback,
        /// if any entity is still deferred, if adding this shared component exceeds the maximum number of shared components,
        /// or adding a component type makes the archetype too large.</remarks>
        /// <param name="entities"> The NativeArray of entities to add the shared component value to. </param>
        /// <param name="sharedComponent"> The shared component value to add. </param>
        /// <typeparam name="T"> The type of shared component to add. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void AddSharedComponentManaged<T>(NativeArray<Entity> entities, T sharedComponent) where T : struct, ISharedComponentData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();

            var entitiesCopy = m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
            var typeIndex = TypeManager.GetTypeIndex<T>();
            var isManaged = TypeManager.IsManagedSharedComponent(typeIndex);
            var isDefaultObject = IsDefaultObject(ref sharedComponent, out var hashCode);
            if (isManaged)
            {
                m_Data->AppendMultipleEntitiesComponentCommandWithSharedValue<T>(
                    &m_Data->m_MainThreadChain,
                    MainThreadSortKey,
                    ECBCommand.AddSharedComponentWithValueForMultipleEntities,
                    entitiesCopy,
                    entities.Length,
                    containsDeferredEntities,
                    hashCode,
                    isDefaultObject ? null : sharedComponent);
            }
            else
            {
                m_Data->AppendMultipleEntitiesCommand_WithUnmanagedSharedValue<T>(
                    &m_Data->m_MainThreadChain,
                    MainThreadSortKey,
                    ECBCommand.AddUnmanagedSharedComponentValueForMultipleEntities,
                    entitiesCopy,
                    entities.Length,
                    containsDeferredEntities,
                    hashCode,
                    isDefaultObject ? null : UnsafeUtility.AddressOf(ref sharedComponent));
            }
        }

        /// <summary> Records a command to add an unmanaged shared component value on a NativeArray of entities.</summary>
        /// <remarks>At playback, this command throws an error if any entity is destroyed before playback,
        /// if any entity is still deferred, if adding this shared component exceeds the maximum number of shared components,
        /// or adding a component type makes the archetype too large.</remarks>
        /// <param name="entities"> The NativeArray of entities to add the shared component value to. </param>
        /// <param name="sharedComponent"> The shared component value to add. </param>
        /// <typeparam name="T"> The type of shared component to add. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void AddSharedComponent<T>(NativeArray<Entity> entities, T sharedComponent)
            where T : unmanaged, ISharedComponentData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();

            var entitiesCopy = m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
            var isDefaultObject = IsDefaultObjectUnmanaged(ref sharedComponent, out var hashCode);

            m_Data->AppendMultipleEntitiesCommand_WithUnmanagedSharedValue<T>(
                &m_Data->m_MainThreadChain,
                MainThreadSortKey,
                ECBCommand.AddUnmanagedSharedComponentValueForMultipleEntities,
                entitiesCopy,
                entities.Length,
                containsDeferredEntities,
                hashCode,
                isDefaultObject ? null : UnsafeUtility.AddressOf(ref sharedComponent));
        }

        /// <summary> Records a command to set a possibly-managed shared component value on an entity.</summary>
        /// <remarks> At playback, this command throws an error if this entity is destroyed before playback,
        /// if this entity is still deferred, if the entity has the <see cref="Prefab"/> tag, or if the entity doesn't have the shared component type.</remarks>
        /// <param name="e"> The entity to set the shared component value of. </param>
        /// <param name="sharedComponent"> The shared component value to set. </param>
        /// <typeparam name="T"> The type of shared component to set. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void SetSharedComponentManaged<T>(Entity e, T sharedComponent) where T : struct, ISharedComponentData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();

            var typeIndex = TypeManager.GetTypeIndex<T>();
            var isManaged = TypeManager.IsManagedSharedComponent(typeIndex);
            var isDefaultObject = IsDefaultObject(ref sharedComponent, out var hashCode);

            if (isManaged)
            {
                m_Data->AddEntitySharedComponentCommand<T>(
                    &m_Data->m_MainThreadChain,
                    MainThreadSortKey,
                    ECBCommand.SetSharedComponentData,
                    e,
                    hashCode,
                    isDefaultObject ? null : sharedComponent);
            }
            else
            {
                var componentAddr = UnsafeUtility.AddressOf(ref sharedComponent);
                m_Data->AddEntityUnmanagedSharedComponentCommand<T>(
                    &m_Data->m_MainThreadChain,
                    MainThreadSortKey,
                    ECBCommand.SetUnmanagedSharedComponentData,
                    e,
                    hashCode,
                    isDefaultObject ? null : componentAddr);
            }
        }

        /// <summary>
        /// Only for inserting a non-default value
        /// </summary>
        internal void UnsafeSetSharedComponentManagedNonDefault(Entity e, object sharedComponent, TypeIndex typeIndex)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();

            int hashCode = sharedComponent != null ? TypeManager.GetHashCode(sharedComponent, typeIndex) : 0;
            if (typeIndex.IsManagedType)
            {
                m_Data->AddEntitySharedComponentCommand(
                    &m_Data->m_MainThreadChain,
                    MainThreadSortKey,
                    ECBCommand.SetSharedComponentData,
                    e,
                    hashCode,
                    typeIndex,
                    sharedComponent);
            }
            else
            {
                byte* componentAddr = null;
                GCHandle handle = default;
                if (sharedComponent != null)
                {
                    handle = GCHandle.Alloc(sharedComponent, GCHandleType.Pinned);
                    componentAddr = (byte*)handle.AddrOfPinnedObject();
                }

                m_Data->AddEntityUnmanagedSharedComponentCommand(
                    &m_Data->m_MainThreadChain,
                    MainThreadSortKey,
                    ECBCommand.SetUnmanagedSharedComponentData,
                    e,
                    hashCode,
                    typeIndex,
                    TypeManager.GetTypeInfo(typeIndex).TypeSize,
                    componentAddr);

                if(componentAddr != null)
                {
                    handle.Free();
                }

            }
        }

        /// <summary> Records a command to set an unmanaged shared component value on an entity.</summary>
        /// <remarks> At playback, this command throws an error if this entity is destroyed before playback,
        /// if this entity is still deferred, if the entity has the <see cref="Prefab"/> tag, or if the entity doesn't have the shared component type.</remarks>
        /// <param name="e"> The entity to set the shared component value of. </param>
        /// <param name="sharedComponent"> The shared component value to set. </param>
        /// <typeparam name="T"> The type of shared component to set. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void SetSharedComponent<T>(Entity e, T sharedComponent) where T : unmanaged, ISharedComponentData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();

            var isDefaultObject = IsDefaultObjectUnmanaged(ref sharedComponent, out var hashCode);

            var componentAddr = UnsafeUtility.AddressOf(ref sharedComponent);
            m_Data->AddEntityUnmanagedSharedComponentCommand<T>(
                &m_Data->m_MainThreadChain,
                MainThreadSortKey,
                ECBCommand.SetUnmanagedSharedComponentData,
                e,
                hashCode,
                isDefaultObject ? null : componentAddr);
        }

        /// <summary> Records a command to set a possibly-managed shared component value on a NativeArray of entities.</summary>
        /// <remarks> At playback, this command throws an error if any entity is destroyed before playback,
        /// if any entity is still deferred, if any entity has the <see cref="Prefab"/> tag, or if any entity doesn't have the shared component type.</remarks>
        /// <param name="entities"> The NativeArray of entities to set the shared component value of. </param>
        /// <param name="sharedComponent"> The shared component value to set. </param>
        /// <typeparam name="T"> The type of shared component to set. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void SetSharedComponentManaged<T>(NativeArray<Entity> entities, T sharedComponent)
            where T : struct, ISharedComponentData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();

            var typeIndex = TypeManager.GetTypeIndex<T>();
            var isManaged = TypeManager.IsManagedSharedComponent(typeIndex);
            var isDefaultObject = IsDefaultObject(ref sharedComponent, out var hashCode);

            var entitiesCopy = m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
            if (isManaged)
            {
                m_Data->AppendMultipleEntitiesComponentCommandWithSharedValue<T>(
                    &m_Data->m_MainThreadChain,
                    MainThreadSortKey,
                    ECBCommand.SetSharedComponentValueForMultipleEntities,
                    entitiesCopy,
                    entities.Length,
                    containsDeferredEntities,
                    hashCode,
                    isDefaultObject ? (object)null : sharedComponent);
            }
            else
            {
                m_Data->AppendMultipleEntitiesCommand_WithUnmanagedSharedValue<T>(
                    &m_Data->m_MainThreadChain,
                    MainThreadSortKey,
                    ECBCommand.SetUnmanagedSharedComponentValueForMultipleEntities,
                    entitiesCopy,
                    entities.Length,
                    containsDeferredEntities,
                    hashCode,
                    isDefaultObject ? null : UnsafeUtility.AddressOf(ref sharedComponent));
            }
        }

        /// <summary> Records a command to set an unmanaged shared component value on a NativeArray of entities.</summary>
        /// <remarks> At playback, this command throws an error if any entity is destroyed before playback,
        /// if any entity is still deferred, if any entity has the <see cref="Prefab"/> tag, or if any entity doesn't have the shared component type.</remarks>
        /// <param name="entities"> The NativeArray of entities to set the shared component value of. </param>
        /// <param name="sharedComponent"> The shared component value to set. </param>
        /// <typeparam name="T"> The type of shared component to set. </typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public void SetSharedComponent<T>(NativeArray<Entity> entities, T sharedComponent)
            where T : unmanaged, ISharedComponentData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();

            var isDefaultObject = IsDefaultObjectUnmanaged(ref sharedComponent, out var hashCode);

            var entitiesCopy = m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);

            m_Data->AppendMultipleEntitiesCommand_WithUnmanagedSharedValue<T>(
                &m_Data->m_MainThreadChain,
                MainThreadSortKey,
                ECBCommand.SetUnmanagedSharedComponentValueForMultipleEntities,
                entitiesCopy,
                entities.Length,
                containsDeferredEntities,
                hashCode,
                isDefaultObject ? null : UnsafeUtility.AddressOf(ref sharedComponent));
        }

        /// <summary>Records a command that adds a component to an entity's <see cref="LinkedEntityGroup"/> based on an <see cref="EntityQueryMask"/>.
        /// Entities in the <see cref="LinkedEntityGroup"/> that don't match the mask will be skipped safely.</summary>
        /// <remarks>At playback, this command throws an error if the entity is destroyed before playback,
        /// if the entity is still deferred, or if any of the matching linked entities cannot add the component.</remarks>
        /// <param name="e">The entity whose LinkedEntityGroup will be referenced.</param>
        /// <param name="mask">The EntityQueryMask that is used to determine which linked entities to add the component to.
        /// Note that EntityQueryMask ignores all query filtering (including chunk filtering and enableable components),
        /// and may thus match more entities than expected.</param>
        /// <param name="component"> The component value to set. </param>
        /// <typeparam name="T"> The type of component to add.</typeparam>
        /// <exception cref="ArgumentException">Throws if the component has a reference to a deferred entity, requiring fixup within the command buffer.</exception>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        public void AddComponentForLinkedEntityGroup<T>(Entity e, EntityQueryMask mask, T component) where T : unmanaged, IComponentData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            m_Data->AddLinkedEntityGroupComponentCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                ECBCommand.AddComponentLinkedEntityGroup, mask, e, component);
        }

        /// <summary>Records a command that adds a component to an entity's <see cref="LinkedEntityGroup"/> based on an <see cref="EntityQueryMask"/>.
        /// Entities in the <see cref="LinkedEntityGroup"/> that don't match the mask will be skipped safely.</summary>
        /// <remarks>At playback, this command throws an error if the entity is destroyed before playback,
        /// if the entity is still deferred, or if any of the matching linked entities cannot add the component.</remarks>
        /// <param name="e">The entity whose LinkedEntityGroup will be referenced.</param>
        /// <param name="mask">The EntityQueryMask that is used to determine which linked entities to add the component to.
        /// Note that EntityQueryMask ignores all query filtering (including chunk filtering and enableable components),
        /// and may thus match more entities than expected.</param>
        /// <param name="componentType"> The component type to add. </param>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        public void AddComponentForLinkedEntityGroup(Entity e, EntityQueryMask mask, ComponentType componentType)
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            m_Data->AddLinkedEntityGroupTypeCommand(&m_Data->m_MainThreadChain, MainThreadSortKey, ECBCommand.AddComponentLinkedEntityGroup, mask, e, componentType);
        }

        /// <summary>Records a command that sets a component for an entity's <see cref="LinkedEntityGroup"/> based on an <see cref="EntityQueryMask"/>.
        /// Entities in the <see cref="LinkedEntityGroup"/> that don't match the mask will be skipped safely.</summary>
        /// <remarks>At playback, this command throws an error if the entity is destroyed before playback,
        /// if the entity is still deferred, if the entity has the <see cref="Prefab"/> tag, or if any of the matching linked entities do not already have the component.</remarks>
        /// <param name="e">The entity whose LinkedEntityGroup will be modified by this command.</param>
        /// <param name="mask">The EntityQueryMask that is used to determine which linked entities to set the component for.
        /// Note that EntityQueryMask ignores all query filtering (including chunk filtering and enableable components),
        /// and may thus match more entities than expected.</param>
        /// <param name="component"> The component value to set. </param>
        /// <typeparam name="T"> The type of component to add.</typeparam>
        /// <exception cref="ArgumentException">Throws if the component has a reference to a deferred entity, requiring fixup within the command buffer.</exception>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        public void SetComponentForLinkedEntityGroup<T>(Entity e, EntityQueryMask mask, T component) where T : unmanaged, IComponentData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            m_Data->AddLinkedEntityGroupComponentCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                ECBCommand.SetComponentLinkedEntityGroup, mask, e, component);
        }

        /// <summary>Records a command that replaces a component value for an entity's <see cref="LinkedEntityGroup"/>.
        /// Entities in the <see cref="LinkedEntityGroup"/> that don't have the component will be skipped safely.</summary>
        /// <remarks>At playback, this command throws an error if the entity is destroyed before playback or
        /// if the entity is still deferred.</remarks>
        /// <param name="e">The entity whose LinkedEntityGroup will be referenced.</param>
        /// <param name="component"> The component value to set. </param>
        /// <typeparam name="T"> The type of component to add.</typeparam>
        /// <exception cref="ArgumentException">Throws if the component has a reference to a deferred entity, requiring fixup within the command buffer.</exception>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        public void ReplaceComponentForLinkedEntityGroup<T>(Entity e, T component) where T : unmanaged, IComponentData
        {
            EnforceSingleThreadOwnership();
            AssertDidNotPlayback();
            m_Data->AddEntityComponentTypeWithValueCommand(&m_Data->m_MainThreadChain, MainThreadSortKey,
                ECBCommand.ReplaceComponentLinkedEntityGroup, e, component);
        }

        /// <summary>
        /// Play back all recorded operations against an entity manager.
        /// </summary>
        /// <param name="mgr">The entity manager that will receive the operations</param>
        public void Playback(EntityManager mgr)
        {
            PlaybackInternal(mgr.GetCheckedEntityDataAccess());
        }

        /// <summary>
        /// Play back all recorded operations with an exclusive entity transaction.
        /// <seealso cref="EntityManager.BeginExclusiveEntityTransaction"/>.
        /// </summary>
        /// <param name="mgr">The exclusive entity transaction that will process the operations</param>
        public void Playback(ExclusiveEntityTransaction mgr)
        {
            PlaybackInternal(mgr.EntityManager.GetCheckedEntityDataAccess());
        }

        void PlaybackInternal(EntityDataAccess* mgr)
        {
            EnforceSingleThreadOwnership();

            if (!ShouldPlayback || m_Data == null)
                return;
            if (m_Data != null && m_Data->m_DidPlayback && m_Data->m_PlaybackPolicy == PlaybackPolicy.SinglePlayback)
            {
                throw new InvalidOperationException(
                    "Attempt to call Playback() on an EntityCommandBuffer that has already been played back.\nEntityCommandBuffers created with the SinglePlayback policy can only be played back once.");
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_BufferSafety);
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_ArrayInvalidationSafety);
#endif

            k_ProfileEcbPlayback.Begin();

            if (ENABLE_PRE_PLAYBACK_VALIDATION)
            {
                var walker = new EcbWalker<PrePlaybackValidationProcessor>(default, ECBProcessorType.PrePlaybackValidationProcessor);
                walker.processor.Init(mgr, m_Data, in OriginSystemHandle);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                walker.processor.playbackProcessor.ecbSafetyHandle = m_Safety0;
#endif
                walker.WalkChains(this);
                walker.processor.Cleanup();
                PassedPrePlaybackValidation = 1;
            }
            else if (PLAYBACK_WITH_TRACE)
            {
                var walker = new EcbWalker<PlaybackWithTraceProcessor>(default, ECBProcessorType.PlaybackWithTraceProcessor);
                walker.processor.Init(mgr, m_Data, in OriginSystemHandle);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                walker.processor.playbackProcessor.ecbSafetyHandle = m_Safety0;
#endif
                walker.WalkChains(this);
                walker.processor.Cleanup();
            }
            else
            {
                var walker = new EcbWalker<PlaybackProcessor>(default, ECBProcessorType.PlaybackProcessor);
                walker.processor.Init(mgr, m_Data, in OriginSystemHandle);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                walker.processor.ecbSafetyHandle = m_Safety0;
#endif
                walker.WalkChains(this);
                walker.processor.Cleanup();
            }

            m_Data->m_DidPlayback = true;
            k_ProfileEcbPlayback.End();
        }

        // This enum is used by the ECBInterop to allow us to have generic chain walking code
        // Each IEcbProcessor should have a type here
        // ECBInterop._ProcessChainChunk(...) needs to be updated with the new option as well
        internal enum ECBProcessorType
        {
            PlaybackProcessor,
            DebugViewProcessor,
            PlaybackWithTraceProcessor,
            PrePlaybackValidationProcessor,
        }

        internal interface IEcbProcessor
        {
            public void DestroyEntity(BasicCommand* header);
            public void RemoveComponent(BasicCommand* header);
            public void RemoveMultipleComponents(BasicCommand* header);
            public void CreateEntity(BasicCommand* header);
            public void InstantiateEntity(BasicCommand* header);
            public void AddComponent(BasicCommand* header);
            public void AddMultipleComponents(BasicCommand* header);
            public void SetComponent(BasicCommand* header);
            public void SetEnabled(BasicCommand* header);
            public void SetComponentEnabled(BasicCommand* header);
            public void SetName(BasicCommand* header);
            public void AddBuffer(BasicCommand* header);
            public void SetBuffer(BasicCommand* header);
            public void AppendToBuffer(BasicCommand* header);
            public void AddComponentForEntityQuery(BasicCommand* header);
            public void AddComponentForMultipleEntities(BasicCommand* header);
            public void RemoveComponentForEntityQuery(BasicCommand* header);
            public void RemoveComponentForMultipleEntities(BasicCommand* header);
            public void AddMultipleComponentsForMultipleEntities(BasicCommand* header);
            public void AddMultipleComponentsForEntityQuery(BasicCommand* header);
            public void RemoveMultipleComponentsForMultipleEntities(BasicCommand* header);
            public void RemoveMultipleComponentsForEntityQuery(BasicCommand* header);
            public void DestroyMultipleEntities(BasicCommand* header);
            public void DestroyForEntityQuery(BasicCommand* header);
            public void AddComponentLinkedEntityGroup(BasicCommand* header);
            public void SetComponentLinkedEntityGroup(BasicCommand* header);
            public void ReplaceComponentLinkedEntityGroup(BasicCommand* header);
            public void AddManagedComponentData(BasicCommand* header);
            public void MoveManagedComponentData(BasicCommand* header);
            public void AddComponentObjectForMultipleEntities(BasicCommand* header);
            public void SetComponentObjectForMultipleEntities(BasicCommand* header);
            public void AddSharedComponentData(BasicCommand* header);
            public void AddSharedComponentWithValueForMultipleEntities(BasicCommand* header);
            public void AddSharedComponentWithValueForEntityQuery(BasicCommand* header);
            public void SetSharedComponentValueForMultipleEntities(BasicCommand* header);
            public void SetSharedComponentValueForEntityQuery(BasicCommand* header);
            public void SetManagedComponentData(BasicCommand* header);
            public void SetSharedComponentData(BasicCommand* header);
            public void AddUnmanagedSharedComponentData(BasicCommand* header);
            public void SetUnmanagedSharedComponentData(BasicCommand* header);
            public void AddUnmanagedSharedComponentValueForMultipleEntities(BasicCommand* header);
            public void AddUnmanagedSharedComponentValueForEntityQuery(BasicCommand* header);
            public void SetUnmanagedSharedComponentValueForMultipleEntities(BasicCommand* header);
            public void SetUnmanagedSharedComponentValueForEntityQuery(BasicCommand* header);

            public ECBProcessorType ProcessorType { get; }
        }

        internal static void ProcessManagedCommand<T>(T* processor, BasicCommand* header) where T : unmanaged, IEcbProcessor
        {
            switch ((ECBCommand)header->CommandType)
            {
                case ECBCommand.AddManagedComponentData:
                    processor->AddManagedComponentData(header);
                    break;

                case ECBCommand.MoveManagedComponentData:
                    processor->MoveManagedComponentData(header);
                    break;

                case ECBCommand.AddSharedComponentData:
                    processor->AddSharedComponentData(header);
                    break;

                case ECBCommand.AddComponentObjectForMultipleEntities:
                    processor->AddComponentObjectForMultipleEntities(header);
                    break;

                case ECBCommand.SetComponentObjectForMultipleEntities:
                    processor->SetComponentObjectForMultipleEntities(header);
                    break;

                case ECBCommand.AddSharedComponentWithValueForMultipleEntities:
                    processor->AddSharedComponentWithValueForMultipleEntities(header);
                    break;

                case ECBCommand.SetSharedComponentValueForMultipleEntities:
                    processor->SetSharedComponentValueForMultipleEntities(header);
                    break;

                case ECBCommand.AddSharedComponentWithValueForEntityQuery:
                    processor->AddSharedComponentWithValueForEntityQuery(header);
                    break;

                case ECBCommand.SetSharedComponentValueForEntityQuery:
                    processor->SetSharedComponentValueForEntityQuery(header);
                    break;

                case ECBCommand.SetManagedComponentData:
                    processor->SetManagedComponentData(header);
                    break;

                case ECBCommand.SetSharedComponentData:
                    processor->SetSharedComponentData(header);
                    break;

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                default:
                {
                    throw new InvalidOperationException($"Invalid command type {(ECBCommand)header->CommandType} not recognized.");
                }
#endif
            }
        }

        internal struct EcbWalker<T> where T: unmanaged, IEcbProcessor {
            public T processor;
            public ECBProcessorType processorType;
            public EcbWalker(T ecbProcessor, ECBProcessorType type)
            {
                processor = ecbProcessor;
                processorType = type;
            }
            public void WalkChains(EntityCommandBuffer ecb)
            {
                EntityCommandBufferData* data = ecb.m_Data;
                // Walk all chains (Main + Threaded) and build a NativeArray of PlaybackState objects.
                // Only chains with non-null Head pointers will be included.
                if (data->m_RecordedChainCount <= 0)
                    return;
                fixed (void* pThis = &this)
                {
                    var chainStates = stackalloc ECBChainPlaybackState[data->m_RecordedChainCount];
                    int initialChainCount = 0;
                    for (var chain = &data->m_MainThreadChain; chain != null; chain = chain->m_NextChain)
                    {
                        if (chain->m_Head != null)
                        {
#pragma warning disable 728
                            chainStates[initialChainCount++] = new ECBChainPlaybackState
                            {
                                Chunk = chain->m_Head,
                                Offset = 0,
                                NextSortKey = chain->m_Head->BaseSortKey,
                                CanBurstPlayback = chain->m_CanBurstPlayback
                            };
#pragma warning restore 728
                        }
                    }

                    if (data->m_ThreadedChains != null)
                    {
#if UNITY_2022_2_14F1_OR_NEWER
                        int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
                        int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
                        for (int i = 0; i < maxThreadCount; ++i)
                        {
                            for (var chain = &data->m_ThreadedChains[i]; chain != null; chain = chain->m_NextChain)
                            {
                                if (chain->m_Head != null)
                                {
#pragma warning disable 728
                                    chainStates[initialChainCount++] = new ECBChainPlaybackState
                                    {
                                        Chunk = chain->m_Head,
                                        Offset = 0,
                                        NextSortKey = chain->m_Head->BaseSortKey,
                                        CanBurstPlayback = chain->m_CanBurstPlayback
                                    };
#pragma warning restore 728
                                }
                            }
                        }
                    }
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                    if (data->m_RecordedChainCount != initialChainCount)
                        Assert.IsTrue(false,
                            $"RecordedChainCount ({data->m_RecordedChainCount}) != initialChainCount ({initialChainCount}");
#endif

                    using (ECBChainPriorityQueue chainQueue = new ECBChainPriorityQueue(chainStates,
                        data->m_RecordedChainCount, Allocator.Temp))
                    {
                        ECBChainHeapElement currentElem = chainQueue.Pop();

                        while (currentElem.ChainIndex != -1)
                        {
                            ECBChainHeapElement nextElem = chainQueue.Peek();

                            var chunk = chainStates[currentElem.ChainIndex].Chunk;
                            var off = chainStates[currentElem.ChainIndex].Offset;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                            if (chunk == null)
                                Assert.IsTrue(false, $"chainStates[{currentElem.ChainIndex}].Chunk is null.");
                            if (off < 0 || off >= chunk->Used)
                                Assert.IsTrue(false, $"chainStates[{currentElem.ChainIndex}].Offset is invalid: {off}. Should be between 0 and {chunk->Used}");
#endif

                            if (chainStates[currentElem.ChainIndex].CanBurstPlayback)
                            {
                                // Bursting PlaybackChain
                                ECBInterop.ProcessChainChunk(pThis, (int)processorType, chainStates,
                                    currentElem.ChainIndex, nextElem.ChainIndex);
                            }
                            else
                            {
                                // Non-Bursted PlaybackChain
                                ECBInterop._ProcessChainChunk(pThis, (int)processorType, chainStates,
                                    currentElem.ChainIndex, nextElem.ChainIndex);
                            }

                            if (chainStates[currentElem.ChainIndex].Chunk == null)
                            {
                                chainQueue.Pop(); // ignore return value; we already have it as nextElem
                            }
                            else
                            {
                                currentElem.SortKey = chainStates[currentElem.ChainIndex].NextSortKey;
                                chainQueue.ReplaceTop(currentElem);
                            }

                            currentElem = nextElem;
                        }
                    }
                }
            }

            internal void ProcessChain(ECBChainPlaybackState* chainStates, int currentChain,
                int nextChain)
            {
                int nextChainSortKey = (nextChain != -1) ? chainStates[nextChain].NextSortKey : -1;
                var chunk = chainStates[currentChain].Chunk;
                var off = chainStates[currentChain].Offset;

                while (chunk != null)
                {
                    var buf = (byte*) chunk + sizeof(ECBChunk);
                    while (off < chunk->Used)
                    {
                        var header = (BasicCommand*) (buf + off);
                        if (nextChain != -1 && header->SortKey > nextChainSortKey)
                        {
                            // early out because a different chain needs to playback
                            var state = chainStates[currentChain];
                            state.Chunk = chunk;
                            state.Offset = off;
                            state.NextSortKey = header->SortKey;
                            chainStates[currentChain] = state;
                            return;
                        }

                        var processed = ProcessUnmanagedCommand(header);
                        if (!processed)
                        {
                            ECBInterop.ProcessManagedCommand(UnsafeUtility.AddressOf(ref processor), (int)processor.ProcessorType, header);
                        }

                        off += header->TotalSize;
                    }

                    // Reached the end of a chunk; advance to the next one
                    chunk = chunk->Next;
                    off = 0;
                }

                // Reached the end of the chain; update its playback state to make sure it's ignored
                // for the remainder of playback.
                {
                    var state = chainStates[currentChain];
                    state.Chunk = null;
                    state.Offset = 0;
                    state.NextSortKey = Int32.MinValue;
                    chainStates[currentChain] = state;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool ProcessUnmanagedCommand(BasicCommand* header)
            {
                switch ((ECBCommand)header->CommandType)
                {
                    case ECBCommand.InstantiateEntity:
                        processor.InstantiateEntity(header);
                        return true;
                    case ECBCommand.CreateEntity:
                        processor.CreateEntity(header);
                        return true;
                    case ECBCommand.DestroyEntity:
                        processor.DestroyEntity(header);
                        return true;

                    case ECBCommand.AddComponent:
                        processor.AddComponent(header);
                        return true;
                    case ECBCommand.AddMultipleComponents:
                        processor.AddMultipleComponents(header);
                        return true;

                    case ECBCommand.RemoveComponent:
                        processor.RemoveComponent(header);
                        return true;

                    case ECBCommand.RemoveMultipleComponents:
                        processor.RemoveMultipleComponents(header);
                        return true;

                    case ECBCommand.SetComponent:
                        processor.SetComponent(header);
                        return true;

                    case ECBCommand.SetEntityEnabled:
                        processor.SetEnabled(header);
                        return true;

                    case ECBCommand.SetComponentEnabled:
                        processor.SetComponentEnabled(header);
                        return true;


                    case ECBCommand.SetName:
                        processor.SetName(header);
                        return true;


                    case ECBCommand.AddBuffer:
                        processor.AddBuffer(header);
                        return true;

                    case ECBCommand.SetBuffer:
                        processor.SetBuffer(header);
                        return true;

                    case ECBCommand.AppendToBuffer:
                        processor.AppendToBuffer(header);
                        return true;

                    case ECBCommand.AddComponentForEntityQuery:
                        processor.AddComponentForEntityQuery(header);
                        return true;

                    case ECBCommand.AddComponentForMultipleEntities:
                        processor.AddComponentForMultipleEntities(header);
                        return true;

                    case ECBCommand.RemoveComponentForEntityQuery:
                        processor.RemoveComponentForEntityQuery(header);
                        return true;

                    case ECBCommand.RemoveComponentForMultipleEntities:
                        processor.RemoveComponentForMultipleEntities(header);
                        return true;

                    case ECBCommand.AddMultipleComponentsForMultipleEntities:
                        processor.AddMultipleComponentsForMultipleEntities(header);
                        return true;

                    case ECBCommand.AddMultipleComponentsForEntityQuery:
                        processor.AddMultipleComponentsForEntityQuery(header);
                        return true;

                    case ECBCommand.RemoveMultipleComponentsForMultipleEntities:
                        processor.RemoveMultipleComponentsForMultipleEntities(header);
                        return true;

                    case ECBCommand.RemoveMultipleComponentsForEntityQuery:
                        processor.RemoveMultipleComponentsForEntityQuery(header);
                        return true;

                    case ECBCommand.DestroyMultipleEntities:
                        processor.DestroyMultipleEntities(header);
                        return true;
                    case ECBCommand.DestroyForEntityQuery:
                        processor.DestroyForEntityQuery(header);
                        return true;

                    case ECBCommand.AddUnmanagedSharedComponentData:
                        processor.AddUnmanagedSharedComponentData(header);
                        return true;
                    case ECBCommand.SetUnmanagedSharedComponentData:
                        processor.SetUnmanagedSharedComponentData(header);
                        return true;
                    case ECBCommand.AddUnmanagedSharedComponentValueForMultipleEntities:
                        processor.AddUnmanagedSharedComponentValueForMultipleEntities(header);
                        return true;
                    case ECBCommand.SetUnmanagedSharedComponentValueForMultipleEntities:
                        processor.SetUnmanagedSharedComponentValueForMultipleEntities(header);
                        return true;
                    case ECBCommand.AddUnmanagedSharedComponentValueForEntityQuery:
                        processor.AddUnmanagedSharedComponentValueForEntityQuery(header);
                        return true;
                    case ECBCommand.SetUnmanagedSharedComponentValueForEntityQuery:
                        processor.SetUnmanagedSharedComponentValueForEntityQuery(header);
                        return true;

                    case ECBCommand.AddComponentLinkedEntityGroup:
                        processor.AddComponentLinkedEntityGroup(header);
                        return true;

                    case ECBCommand.SetComponentLinkedEntityGroup:
                        processor.SetComponentLinkedEntityGroup(header);
                        return true;

                    case ECBCommand.ReplaceComponentLinkedEntityGroup:
                        processor.ReplaceComponentLinkedEntityGroup(header);
                        return true;
                }

                return false;
            }
        }

        internal struct PlaybackProcessor : IEcbProcessor
        {
            public EntityDataAccess* mgr;
            public EntityComponentStore.ArchetypeChanges archetypeChanges;
            public ECBSharedPlaybackState playbackState;
            public PlaybackPolicy playbackPolicy;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            public AtomicSafetyHandle ecbSafetyHandle; // used for temporary NativeArray views created & destroyed during playback
#endif
            public byte isFirstPlayback;
            public int entityCount;
            public int bufferCount;
            public SystemHandle originSystem;
            private byte trackStructuralChanges;

            public void Init(EntityDataAccess* entityDataAccess, EntityCommandBufferData* data, in SystemHandle originSystemHandle)
            {
                mgr = entityDataAccess;
                playbackPolicy = data->m_PlaybackPolicy;
                isFirstPlayback = (byte)(data->m_DidPlayback ? 0 : 1);
                originSystem = originSystemHandle;

                // Don't begin/end structural changes unless at least one command was recorded.
                // This prevents empty command buffers from needlessly causing structural changes, which some
                // existing code relies on.
                if (data->m_RecordedChainCount > 0)
                {
                    archetypeChanges = mgr->BeginStructuralChanges();
                    trackStructuralChanges = 1;
                }

                // Play back the recorded commands in increasing sortKey order
                entityCount = -data->m_Entity.Index;
                bufferCount = *data->m_BufferWithFixups.Counter;

                Entity* createEntitiesBatch = null;
                ECBSharedPlaybackState.BufferWithFixUp* buffersWithFixup = null;

                if (entityCount > 0)
                    createEntitiesBatch = (Entity*) Memory.Unmanaged.Allocate(entityCount * sizeof(Entity),
                            4, Allocator.Temp);
                if (bufferCount > 0)
                    buffersWithFixup = (ECBSharedPlaybackState.BufferWithFixUp*)
                        Memory.Unmanaged.Allocate(bufferCount * sizeof(ECBSharedPlaybackState.BufferWithFixUp),
                            4, Allocator.Temp);


                playbackState = new ECBSharedPlaybackState
                {
                    CommandBufferID = data->m_CommandBufferID,
                    CreateEntityBatch = createEntitiesBatch,
                    BuffersWithFixUp = buffersWithFixup,
                    CreatedEntityCount = entityCount,
                    LastBuffer = 0,
                };
            }

            public void Cleanup()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (bufferCount != playbackState.LastBuffer)
                    Assert.IsTrue(false, $"bufferCount ({bufferCount}) != playbackState.LastBuffer ({playbackState.LastBuffer})");
#endif
                for (int i = 0; i < playbackState.LastBuffer; i++)
                {
                    ECBSharedPlaybackState.BufferWithFixUp* fixup = playbackState.BuffersWithFixUp + i;
                    EntityBufferCommand* cmd = fixup->cmd;
                    var entity = SelectEntity(cmd->Header.Entity, playbackState);
                    if (mgr->Exists(entity) && mgr->HasComponent(entity, ComponentType.FromTypeIndex(cmd->ComponentTypeIndex)))
                        FixupBufferContents(mgr, cmd, entity, playbackState);
                }

                Memory.Unmanaged.Free(playbackState.CreateEntityBatch, Allocator.Temp);
                Memory.Unmanaged.Free(playbackState.BuffersWithFixUp, Allocator.Temp);

                if (trackStructuralChanges != 0)
                {
                    mgr->EndStructuralChanges(ref archetypeChanges);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void DestroyEntity(BasicCommand* header)
            {
                var cmd = (EntityCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.DestroyEntity");
#endif
                Entity entity = SelectEntity(cmd->Entity, playbackState);
                mgr->DestroyEntityInternalDuringStructuralChange(&entity, 1, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveComponent(BasicCommand* header)
            {
                var cmd = (EntityComponentCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.RemoveComponent");
#endif
                var entity = SelectEntity(cmd->Header.Entity, playbackState);
                mgr->RemoveComponentDuringStructuralChange(entity, ComponentType.FromTypeIndex(cmd->ComponentTypeIndex), in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveMultipleComponents(BasicCommand* header)
            {
                var cmd = (EntityMultipleComponentsCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.RemoveMultipleComponents");
#endif
                var entity = SelectEntity(cmd->Header.Entity, playbackState);
                var componentTypes = cmd->TypeSet;

                mgr->RemoveComponentDuringStructuralChange(entity, componentTypes, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void CreateEntity(BasicCommand* header)
            {
                var cmd = (CreateCommand*)header;
                EntityArchetype at = cmd->Archetype;

                if (!at.Valid)
                    at = mgr->GetEntityAndSimulateArchetype();

                int index = -cmd->IdentityIndex - 1;

                mgr->CreateEntityDuringStructuralChange(at, playbackState.CreateEntityBatch + index, cmd->BatchCount, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void InstantiateEntity(BasicCommand* header)
            {
                var cmd = (EntityCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.InstantiateEntity");
#endif
                var index = -cmd->IdentityIndex - 1;
                Entity srcEntity = SelectEntity(cmd->Entity, playbackState);
                mgr->InstantiateInternalDuringStructuralChange(srcEntity, playbackState.CreateEntityBatch + index,
                    cmd->BatchCount, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetComponentFromEmbeddedValue(EntityComponentCommand* cmd, Entity entity)
            {
                byte* srcValue = (byte*)(cmd + 1);
                if (cmd->ValueRequiresEntityFixup != 0)
                {
                    // We fixup the value in the command buffer itself. This would break multi-playback mode, but we
                    // already disallow multi-playback mode on any command buffer that uses fixup.
                    AssertNoFixupInMultiPlayback(isFirstPlayback != 0);
                    FixupTemporaryEntitiesInComponentValue(srcValue, cmd->ComponentTypeIndex, playbackState);
                }
                mgr->SetComponentDataRaw(entity, cmd->ComponentTypeIndex, srcValue, cmd->ComponentSize, originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddComponent(BasicCommand* header)
            {
                var cmd = (EntityComponentCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddComponent");
#endif
                var componentType = ComponentType.FromTypeIndex(cmd->ComponentTypeIndex);
                var entity = SelectEntity(cmd->Header.Entity, playbackState);
                mgr->AddComponentDuringStructuralChange(entity, componentType, in originSystem);
                if (cmd->ComponentSize != 0)
                {
                    SetComponentFromEmbeddedValue(cmd, entity);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddMultipleComponents(BasicCommand* header)
            {
                var cmd = (EntityMultipleComponentsCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddMultipleComponents");
#endif
                var entity = SelectEntity(cmd->Header.Entity, playbackState);
                var componentTypes = cmd->TypeSet;
                mgr->AddMultipleComponentsDuringStructuralChange(entity, componentTypes);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetComponent(BasicCommand* header)
            {
                var cmd = (EntityComponentCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.SetComponent");
#endif
                var entity = SelectEntity(cmd->Header.Entity, playbackState);
                SetComponentFromEmbeddedValue(cmd, entity);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetEnabled(BasicCommand* header)
            {
                var cmd = (EntityEnabledCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.SetEnabled");
#endif
                var entity = SelectEntity(cmd->Header.Entity, playbackState);
                mgr->SetEnabled(entity, cmd->IsEnabled != 0);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetComponentEnabled(BasicCommand* header)
            {
                var cmd = (EntityComponentEnabledCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.SetComponentEnabled");
#endif
                var entity = SelectEntity(cmd->Header.Header.Entity, playbackState);
                mgr->SetComponentEnabled(entity, cmd->ComponentTypeIndex, cmd->Header.IsEnabled != 0);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetName(BasicCommand* header)
            {
                var cmd = (EntityNameCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.SetName");
#endif
                var entity = SelectEntity(cmd->Header.Entity, playbackState);
                mgr->SetName(entity, in cmd->Name);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddBuffer(BasicCommand* header)
            {
                var cmd = (EntityBufferCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddBuffer");
#endif
                var entity = SelectEntity(cmd->Header.Entity, playbackState);
                mgr->AddComponentDuringStructuralChange(entity, ComponentType.FromTypeIndex(cmd->ComponentTypeIndex), in originSystem);
                if (playbackPolicy == PlaybackPolicy.SinglePlayback)
                {
                    mgr->SetBufferRaw(entity, cmd->ComponentTypeIndex,
                        &cmd->BufferNode.TempBuffer,
                        cmd->ComponentSize, in originSystem);
                    if (cmd->ValueRequiresEntityFixup != 0)
                    {
                        AssertNoFixupInMultiPlayback(isFirstPlayback != 0);
                        AddToPostPlaybackFixup(cmd, ref playbackState);
                    }
                }
                else
                {
                    if (Hint.Unlikely(cmd->ValueRequiresEntityFixup != 0))
                        AssertNoFixupInMultiPlayback(isFirstPlayback != 0);
                    // copy the buffer to ensure that no two entities point to the same buffer from the ECB
                    // either in the same world or in different worlds
                    var buffer = CloneBuffer(&cmd->BufferNode.TempBuffer, cmd->ComponentTypeIndex);
                    mgr->SetBufferRaw(entity, cmd->ComponentTypeIndex, &buffer,
                        cmd->ComponentSize, in originSystem);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetBuffer(BasicCommand* header)
            {
                var cmd = (EntityBufferCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.SetBuffer");
#endif
                var entity = SelectEntity(cmd->Header.Entity, playbackState);
                if (playbackPolicy == PlaybackPolicy.SinglePlayback)
                {
                    mgr->SetBufferRaw(entity, cmd->ComponentTypeIndex, &cmd->BufferNode.TempBuffer,
                        cmd->ComponentSize, in originSystem);
                    if (cmd->ValueRequiresEntityFixup != 0)
                    {
                        AssertNoFixupInMultiPlayback(isFirstPlayback != 0);
                        AddToPostPlaybackFixup(cmd, ref playbackState);
                    }
                }
                else
                {
                    if (Hint.Unlikely(cmd->ValueRequiresEntityFixup != 0))
                        AssertNoFixupInMultiPlayback(isFirstPlayback != 0);
                    // copy the buffer to ensure that no two entities point to the same buffer from the ECB
                    // either in the same world or in different worlds
                    var buffer = CloneBuffer(&cmd->BufferNode.TempBuffer, cmd->ComponentTypeIndex);
                    mgr->SetBufferRaw(entity, cmd->ComponentTypeIndex, &buffer, cmd->ComponentSize, in originSystem);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AppendToBuffer(BasicCommand* header)
            {
                var cmd = (EntityComponentCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AppendToBuffer");
#endif
                var entity = SelectEntity(cmd->Header.Entity, playbackState);
                CheckBufferExistsOnEntity(mgr->EntityComponentStore, entity, cmd);
                BufferHeader* bufferHeader =
                    (BufferHeader*)mgr->GetComponentDataRW_AsBytePointer(entity, cmd->ComponentTypeIndex);

                ref readonly var typeInfo = ref TypeManager.GetTypeInfo(cmd->ComponentTypeIndex);
                var alignment = typeInfo.AlignmentInBytes;
                var elementSize = typeInfo.ElementSize;

                BufferHeader.EnsureCapacity(bufferHeader, bufferHeader->Length + 1, elementSize, alignment, BufferHeader.TrashMode.RetainOldData, false, 0);

                var offset = bufferHeader->Length * elementSize;
                UnsafeUtility.MemCpy(BufferHeader.GetElementPointer(bufferHeader) + offset, cmd + 1, (long)elementSize);
                bufferHeader->Length += 1;
                if (cmd->ValueRequiresEntityFixup != 0)
                {
                    AssertNoFixupInMultiPlayback(isFirstPlayback != 0);
                    FixupTemporaryEntitiesInComponentValue(BufferHeader.GetElementPointer(bufferHeader) + offset, typeInfo.TypeIndex, playbackState);
                }
            }

            // Creates a temporary NativeArray<Entity> view data from an Entity* pointer+count. This array does not need to be Disposed();
            // it does not own any of its data. The array will share a safety handle with the ECB, and its lifetime must not exceed that
            // of the ECB itself.
            private void CreateTemporaryNativeArrayView(Entity* entities, int entityCount,
                out NativeArray<Entity> outArray)
            {
                outArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<Entity>(entities, entityCount,
                    Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                // The temporary array still needs an atomic handle, but the array itself will not have Dispose() called
                // on it (it doesn't own its data). And even if it did, NativeArray.Dispose() skips disposing the safety
                // handle if the array uses Allocator.None. The solution is to pass the primary ECB safety handle into
                // the playback processor, and use it for temporary arrays created during playback.
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref outArray, ecbSafetyHandle);
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddComponentForEntityQuery(BasicCommand* header)
            {
                var cmd = (EntityQueryComponentCommand*)header;
                var componentType = ComponentType.FromTypeIndex(cmd->ComponentTypeIndex);
                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary).
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                mgr->AddComponentToQueryDuringStructuralChange(cmd->Header.QueryImpl, componentType, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddComponentForMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesComponentCommand*)header;
                var componentType = ComponentType.FromTypeIndex(cmd->ComponentTypeIndex);

                CreateTemporaryNativeArrayView(cmd->Header.Entities.Ptr, cmd->Header.EntitiesCount, out var entities);
                if (cmd->Header.SkipDeferredEntityLookup == 0)
                {
                    for (int len = entities.Length, i = 0; i < len; ++i)
                    {
                        if (entities[i].Index < 0)
                            entities[i] = SelectEntity(entities[i], playbackState);
                    }
                }
                mgr->AddComponentDuringStructuralChange(entities, componentType, in originSystem);

                if (cmd->ComponentSize > 0)
                {
                    byte* srcValue = (byte*)(cmd + 1);
                    if (cmd->ValueRequiresEntityFixup != 0)
                    {
                        AssertNoFixupInMultiPlayback(isFirstPlayback != 0);
                        FixupTemporaryEntitiesInComponentValue(srcValue, cmd->ComponentTypeIndex, playbackState);
                    }
                    for (int i = 0; i < entities.Length; i++)
                    {
                        mgr->SetComponentDataRaw(entities[i], cmd->ComponentTypeIndex, cmd + 1, cmd->ComponentSize, in originSystem);
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveComponentForEntityQuery(BasicCommand* header)
            {
                var cmd = (EntityQueryComponentCommand*)header;
                var componentType = ComponentType.FromTypeIndex(cmd->ComponentTypeIndex);
                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary).
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                mgr->RemoveComponentFromQueryDuringStructuralChange(cmd->Header.QueryImpl, componentType, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveComponentForMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesComponentCommand*)header;
                var componentType = ComponentType.FromTypeIndex(cmd->ComponentTypeIndex);

                CreateTemporaryNativeArrayView(cmd->Header.Entities.Ptr, cmd->Header.EntitiesCount, out var entities);
                if (cmd->Header.SkipDeferredEntityLookup == 0)
                {
                    for (int len = entities.Length, i = 0; i < len; ++i)
                    {
                        if (entities[i].Index < 0)
                            entities[i] = SelectEntity(entities[i], playbackState);
                    }
                }

                mgr->RemoveComponentDuringStructuralChange(entities, componentType, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddMultipleComponentsForMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesAndComponentsCommand*)header;

                CreateTemporaryNativeArrayView(cmd->Header.Entities.Ptr, cmd->Header.EntitiesCount, out var entities);
                if (cmd->Header.SkipDeferredEntityLookup == 0)
                {
                    for (int len = entities.Length, i = 0; i < len; ++i)
                    {
                        if (entities[i].Index < 0)
                            entities[i] = SelectEntity(entities[i], playbackState);
                    }
                }

                mgr->AddMultipleComponentsDuringStructuralChange(entities, cmd->TypeSet, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddMultipleComponentsForEntityQuery(BasicCommand* header)
            {
                var cmd = (EntityQueryComponentTypeSetCommand*)header;
                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary).
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                mgr->AddComponentsToQueryDuringStructuralChange(cmd->Header.QueryImpl, cmd->TypeSet, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveMultipleComponentsForMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesAndComponentsCommand*)header;

                CreateTemporaryNativeArrayView(cmd->Header.Entities.Ptr, cmd->Header.EntitiesCount, out var entities);
                if (cmd->Header.SkipDeferredEntityLookup == 0)
                {
                    for (int len = entities.Length, i = 0; i < len; ++i)
                    {
                        if (entities[i].Index < 0)
                            entities[i] = SelectEntity(entities[i], playbackState);
                    }
                }

                mgr->RemoveMultipleComponentsDuringStructuralChange(entities, cmd->TypeSet, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveMultipleComponentsForEntityQuery(BasicCommand* header)
            {
                var cmd = (EntityQueryComponentTypeSetCommand*)header;
                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary).
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                mgr->RemoveMultipleComponentsFromQueryDuringStructuralChange(cmd->Header.QueryImpl, cmd->TypeSet, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void DestroyMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesCommand*)header;

                CreateTemporaryNativeArrayView(cmd->Entities.Ptr, cmd->EntitiesCount, out var entities);
                if (cmd->SkipDeferredEntityLookup == 0)
                {
                    for (int len = entities.Length, i = 0; i < len; ++i)
                    {
                        if (entities[i].Index < 0)
                            entities[i] = SelectEntity(entities[i], playbackState);
                    }
                }

                mgr->DestroyEntityDuringStructuralChange(entities, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void DestroyForEntityQuery(BasicCommand* header)
            {
                var cmd = (EntityQueryCommand*)header;
                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary). And then immediately begin
                    // a new batch, to track the changes made by this operation.
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                mgr->DestroyEntitiesInQueryDuringStructuralChange(cmd->QueryImpl, in originSystem);
            }

            public void AddComponentLinkedEntityGroup(BasicCommand* header)
            {
                var cmd = (EntityQueryMaskCommand*) header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddComponentLinkedEntityGroup");
#endif
                var entity = SelectEntity(cmd->Header.Header.Entity, playbackState);
                byte *srcValue = (byte*) (cmd + 1);
                if (cmd->Header.ValueRequiresEntityFixup != 0)
                {
                    AssertNoFixupInMultiPlayback(isFirstPlayback != 0);
                    FixupTemporaryEntitiesInComponentValue(srcValue, cmd->Header.ComponentTypeIndex, in playbackState);
                }
                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary). And then immediately begin
                    // a new batch, to track the changes made by this operation.
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                mgr->AddComponentForLinkedEntityGroup(entity, cmd->Mask, cmd->Header.ComponentTypeIndex, srcValue,
                    cmd->Header.ComponentSize);
            }

            public void SetComponentLinkedEntityGroup(BasicCommand* header)
            {
                var cmd = (EntityQueryMaskCommand*) header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.SetComponentLinkedEntityGroup");
#endif
                var entity = SelectEntity(cmd->Header.Header.Entity, playbackState);
                byte *srcValue = (byte*) (cmd + 1);
                if (cmd->Header.ValueRequiresEntityFixup != 0)
                {
                    AssertNoFixupInMultiPlayback(isFirstPlayback != 0);
                    FixupTemporaryEntitiesInComponentValue(srcValue, cmd->Header.ComponentTypeIndex, in playbackState);
                }
                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary). And then immediately begin
                    // a new batch, to track the changes made by this operation.
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                mgr->SetComponentForLinkedEntityGroup(entity, cmd->Mask, cmd->Header.ComponentTypeIndex, srcValue,
                    cmd->Header.ComponentSize);
            }

            public void ReplaceComponentLinkedEntityGroup(BasicCommand* header)
            {
                var cmd = (EntityComponentCommand*) header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.ReplaceComponentLinkedEntityGroup");
#endif
                var entity = SelectEntity(cmd->Header.Entity, playbackState);
                byte *srcValue = (byte*) (cmd + 1);
                if (cmd->ValueRequiresEntityFixup != 0)
                {
                    AssertNoFixupInMultiPlayback(isFirstPlayback != 0);
                    FixupTemporaryEntitiesInComponentValue(srcValue, cmd->ComponentTypeIndex, in playbackState);
                }
                mgr->ReplaceComponentForLinkedEntityGroup(entity, cmd->ComponentTypeIndex, srcValue,
                    cmd->ComponentSize);
            }

            [BurstDiscard]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddManagedComponentData(BasicCommand* header)
            {
                var cmd = (EntityManagedComponentCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddManagedComponentData");
#endif
                var entity = SelectEntity(cmd->Header.Entity, playbackState);
                var addedManaged = mgr->AddComponentDuringStructuralChange(entity, ComponentType.FromTypeIndex(cmd->ComponentTypeIndex), in originSystem);
                if (addedManaged)
                {
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }

                var box = cmd->GetBoxedObject();
                if (box != null && TypeManager.HasEntityReferences(cmd->ComponentTypeIndex))
                    FixupManagedComponent.FixUpComponent(box, playbackState);

                mgr->SetComponentObject(entity, ComponentType.FromTypeIndex(cmd->ComponentTypeIndex), box, in originSystem);
            }


            [BurstDiscard]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void MoveManagedComponentData(BasicCommand* header)
            {
                var cmd = (EntityMoveManagedComponentCommand*)header;
                var srcEntity = SelectEntity(cmd->SrcEntity, playbackState);
                var dstEntity = SelectEntity(cmd->Header.Entity, playbackState);
                var componentType = ComponentType.FromTypeIndex(cmd->ComponentTypeIndex);
                mgr->MoveComponentObjectDuringStructuralChange(srcEntity, dstEntity, componentType, originSystem);
                CommitStructuralChanges(mgr, ref archetypeChanges);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddUnmanagedSharedComponentData(BasicCommand* header)
            {
                var cmd = (EntityUnmanagedSharedComponentCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddUnmanagedSharedComponentData");
#endif
                var entity = SelectEntity(cmd->Header.Entity, playbackState);
                byte *srcValue = (byte*) (cmd + 1);
                if (cmd->IsDefault == 0 && cmd->ValueRequiresEntityFixup != 0)
                {
                    AssertNoFixupInMultiPlayback(isFirstPlayback != 0);
                    FixupTemporaryEntitiesInComponentValue(srcValue, cmd->ComponentTypeIndex, in playbackState);
                }
                var tmp = new NativeArray<Entity>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                tmp[0] = entity;
                mgr->AddSharedComponentDataAddrDefaultMustBeNullDuringStructuralChange(
                    tmp,
                    cmd->ComponentTypeIndex,
                    cmd->HashCode,
                    cmd->IsDefault == 0 ? (void*)srcValue : null);
                CommitStructuralChanges(mgr, ref archetypeChanges);
            }

            [BurstDiscard]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddSharedComponentData(BasicCommand* header)
            {
                var cmd = (EntitySharedComponentCommand*) header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddSharedComponentData");
#endif
                var entity = SelectEntity(cmd->Header.Entity, playbackState);
                var addedShared = mgr->AddSharedComponentDataBoxedDefaultMustBeNullDuringStructuralChange(
                    entity,
                    cmd->ComponentTypeIndex,
                    cmd->HashCode,
                    cmd->GetBoxedObject(),
                    in originSystem);
                if (addedShared)
                {
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
            }

            [BurstDiscard]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddComponentObjectForMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesComponentCommandWithObject*)header;

                var componentType = ComponentType.FromTypeIndex(cmd->ComponentTypeIndex);
                CreateTemporaryNativeArrayView(cmd->Header.Entities.Ptr, cmd->Header.EntitiesCount, out var entities);
                if (cmd->Header.SkipDeferredEntityLookup == 0)
                {
                    for (int len = entities.Length, i = 0; i < len; ++i)
                    {
                        if (entities[i].Index < 0)
                            entities[i] = SelectEntity(entities[i], playbackState);
                    }
                }
                mgr->AddComponentDuringStructuralChange(entities, componentType, in originSystem);

                CommitStructuralChanges(mgr, ref archetypeChanges);

                var box = cmd->GetBoxedObject();
                var typeIndex = cmd->ComponentTypeIndex;
                if (box != null && TypeManager.HasEntityReferences(typeIndex))
                    FixupManagedComponent.FixUpComponent(box, playbackState);

                for (int len = entities.Length, i = 0; i < len; i++)
                {
                    mgr->SetComponentObject(entities[i], componentType, box, in originSystem);
                }
            }

            [BurstDiscard]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetComponentObjectForMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesComponentCommandWithObject*)header;

                var componentType = ComponentType.FromTypeIndex(cmd->ComponentTypeIndex);
                CreateTemporaryNativeArrayView(cmd->Header.Entities.Ptr, cmd->Header.EntitiesCount, out var entities);
                if (cmd->Header.SkipDeferredEntityLookup == 0)
                {
                    for (int len = entities.Length, i = 0; i < len; ++i)
                    {
                        if (entities[i].Index < 0)
                            entities[i] = SelectEntity(entities[i], playbackState);
                    }
                }

                if (!mgr->EntityComponentStore->ManagedChangesTracker.Empty)
                {
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }

                var box = cmd->GetBoxedObject();
                var typeIndex = cmd->ComponentTypeIndex;
                if (box != null && TypeManager.HasEntityReferences(typeIndex))
                    FixupManagedComponent.FixUpComponent(box, playbackState);

                for (int len = entities.Length, i = 0; i < len; i++)
                {
                    mgr->SetComponentObject(entities[i], componentType, box, in originSystem);
                }
            }

            [BurstDiscard]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddSharedComponentWithValueForMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesComponentCommandWithObject*)header;

                CreateTemporaryNativeArrayView(cmd->Header.Entities.Ptr, cmd->Header.EntitiesCount, out var entities);
                if (cmd->Header.SkipDeferredEntityLookup == 0)
                {
                    for (int len = entities.Length, i = 0; i < len; ++i)
                    {
                        if (entities[i].Index < 0)
                            entities[i] = SelectEntity(entities[i], playbackState);
                    }
                }

                var boxedObject = cmd->GetBoxedObject();
                var hashcode = cmd->HashCode;
                var typeIndex = cmd->ComponentTypeIndex;

                // TODO: we aren't yet doing fix-up for Entity fields (see DOTS-3465)
                mgr->AddSharedComponentDataBoxedDefaultMustBeNullDuringStructuralChange(entities, typeIndex,
                    hashcode, boxedObject, in originSystem);

                CommitStructuralChanges(mgr, ref archetypeChanges);
            }

            [BurstDiscard]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddSharedComponentWithValueForEntityQuery(BasicCommand* header)
            {
                var cmd = (EntityQueryComponentCommandWithObject*)header;

                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary). And then immediately begin
                    // a new batch, to track the changes made by this operation.
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                var typeIndex = cmd->Header.ComponentTypeIndex;
                int newSharedComponentDataIndex = mgr->InsertSharedComponent_Managed(typeIndex,
                    cmd->HashCode, cmd->GetBoxedObject());
                // TODO: we aren't yet doing fix-up for Entity fields (see DOTS-3465)
                mgr->AddSharedComponentDataToQueryDuringStructuralChange(cmd->Header.Header.QueryImpl,
                    newSharedComponentDataIndex, ComponentType.FromTypeIndex(typeIndex), in originSystem);

                CommitStructuralChanges(mgr, ref archetypeChanges);
            }

            [BurstDiscard]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetSharedComponentValueForMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesComponentCommandWithObject*)header;

                CreateTemporaryNativeArrayView(cmd->Header.Entities.Ptr, cmd->Header.EntitiesCount, out var entities);
                if (cmd->Header.SkipDeferredEntityLookup == 0)
                {
                    for (int len = entities.Length, i = 0; i < len; ++i)
                    {
                        if (entities[i].Index < 0)
                            entities[i] = SelectEntity(entities[i], playbackState);
                    }
                }

                var boxedObject = cmd->GetBoxedObject();
                var hashcode = cmd->HashCode;
                var typeIndex = cmd->ComponentTypeIndex;

                for (int len = entities.Length, i = 0; i < len; i++)
                {
                    var e = entities[i];
                    mgr->SetSharedComponentDataBoxedDefaultMustBeNullDuringStructuralChange(e, typeIndex,
                        hashcode, boxedObject, in originSystem);
                }

                CommitStructuralChanges(mgr, ref archetypeChanges);
            }

            [BurstDiscard]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetSharedComponentValueForEntityQuery(BasicCommand* header)
            {
                var cmd = (EntityQueryComponentCommandWithObject*)header;

                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary). And then immediately begin
                    // a new batch, to track the changes made by this operation.
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                var typeIndex = cmd->Header.ComponentTypeIndex;
                int newSharedComponentDataIndex = mgr->InsertSharedComponent_Managed(typeIndex,
                    cmd->HashCode, cmd->GetBoxedObject());
                // TODO: we aren't yet doing fix-up for Entity fields (see DOTS-3465)
                mgr->SetSharedComponentDataOnQueryDuringStructuralChange(cmd->Header.Header.QueryImpl,
                    newSharedComponentDataIndex, ComponentType.FromTypeIndex(typeIndex), in originSystem);

                CommitStructuralChanges(mgr, ref archetypeChanges);
            }

            [BurstDiscard]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetManagedComponentData(BasicCommand* header)
            {
                var cmd = (EntityManagedComponentCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.SetManagedComponentData");
#endif
                var entity = SelectEntity(cmd->Header.Entity, playbackState);
                if (!mgr->EntityComponentStore->ManagedChangesTracker.Empty)
                {
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }

                var box = cmd->GetBoxedObject();
                if (box != null && TypeManager.HasEntityReferences(cmd->ComponentTypeIndex))
                    FixupManagedComponent.FixUpComponent(box, playbackState);

                mgr->SetComponentObject(entity, ComponentType.FromTypeIndex(cmd->ComponentTypeIndex), cmd->GetBoxedObject(), in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetUnmanagedSharedComponentData(BasicCommand* header)
            {
                var cmd = (EntityUnmanagedSharedComponentCommand*) header;
                var entity = SelectEntity(cmd->Header.Entity, playbackState);
                byte *srcValue = (byte*) (cmd + 1);
                if (cmd->IsDefault == 0 && cmd->ValueRequiresEntityFixup != 0)
                {
                    AssertNoFixupInMultiPlayback(isFirstPlayback != 0);
                    FixupTemporaryEntitiesInComponentValue(srcValue, cmd->ComponentTypeIndex, in playbackState);
                }
                var tmp = new NativeArray<Entity>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                tmp[0] = entity;
                mgr->SetSharedComponentDataAddrDefaultMustBeNullDuringStructuralChange(
                    tmp,
                    cmd->ComponentTypeIndex,
                    cmd->HashCode,
                    (cmd->IsDefault == 0) ? srcValue : null);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddUnmanagedSharedComponentValueForMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesCommand_WithUnmanagedSharedComponent*)header;

                CreateTemporaryNativeArrayView(cmd->Header.Entities.Ptr, cmd->Header.EntitiesCount, out var entities);
                if (cmd->Header.SkipDeferredEntityLookup == 0)
                {
                    for (int len = entities.Length, i = 0; i < len; ++i)
                    {
                        if (entities[i].Index < 0)
                            entities[i] = SelectEntity(entities[i], playbackState);
                    }
                }

                var hashcode = cmd->HashCode;
                var typeIndex = cmd->ComponentTypeIndex;
                byte *srcValue = (byte*) (cmd + 1);
                if (cmd->IsDefault == 0 && cmd->ValueRequiresEntityFixup != 0)
                {
                    AssertNoFixupInMultiPlayback(isFirstPlayback != 0);
                    FixupTemporaryEntitiesInComponentValue(srcValue, typeIndex, in playbackState);
                }
                mgr->AddSharedComponentDataAddrDefaultMustBeNullDuringStructuralChange(
                    entities,
                    typeIndex,
                    hashcode,
                    (cmd->IsDefault == 0) ? srcValue : null);

                CommitStructuralChanges(mgr, ref archetypeChanges);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddUnmanagedSharedComponentValueForEntityQuery(BasicCommand* header)
            {
                var cmd = (EntityQueryComponentCommandWithUnmanagedSharedComponent*)header;

                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary). And then immediately begin
                    // a new batch, to track the changes made by this operation.
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                var typeIndex = cmd->Header.ComponentTypeIndex;
                byte *srcValue = (byte*) (cmd + 1);
                if (cmd->IsDefault == 0 && cmd->ValueRequiresEntityFixup != 0)
                {
                    AssertNoFixupInMultiPlayback(isFirstPlayback != 0);
                    FixupTemporaryEntitiesInComponentValue(srcValue, typeIndex, in playbackState);
                }
                int newSharedComponentDataIndex = mgr->InsertSharedComponent_Unmanaged(typeIndex, cmd->HashCode,
                    cmd->IsDefault == 1 ? null : srcValue,
                    null);
                mgr->AddSharedComponentDataToQueryDuringStructuralChange_Unmanaged(cmd->Header.Header.QueryImpl,
                    newSharedComponentDataIndex, ComponentType.FromTypeIndex(typeIndex),
                    cmd->IsDefault == 1 ? null : srcValue,
                    in originSystem);

                CommitStructuralChanges(mgr, ref archetypeChanges);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetUnmanagedSharedComponentValueForMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesCommand_WithUnmanagedSharedComponent*)header;

                CreateTemporaryNativeArrayView(cmd->Header.Entities.Ptr, cmd->Header.EntitiesCount, out var entities);
                if (cmd->Header.SkipDeferredEntityLookup == 0)
                {
                    for (int len = entities.Length, i = 0; i < len; ++i)
                    {
                        if (entities[i].Index < 0)
                            entities[i] = SelectEntity(entities[i], playbackState);
                    }
                }
                byte *srcValue = (byte*) (cmd + 1);
                if (cmd->IsDefault == 0 && cmd->ValueRequiresEntityFixup != 0)
                {
                    AssertNoFixupInMultiPlayback(isFirstPlayback != 0);
                    FixupTemporaryEntitiesInComponentValue(srcValue, cmd->ComponentTypeIndex, in playbackState);
                }
                mgr->SetSharedComponentDataAddrDefaultMustBeNullDuringStructuralChange(
                    entities,
                    cmd->ComponentTypeIndex,
                    cmd->HashCode,
                    cmd->IsDefault == 1 ? null : srcValue);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetUnmanagedSharedComponentValueForEntityQuery(BasicCommand* header)
            {
                var cmd = (EntityQueryComponentCommandWithUnmanagedSharedComponent*)header;

                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary). And then immediately begin
                    // a new batch, to track the changes made by this operation.
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                var typeIndex = cmd->Header.ComponentTypeIndex;
                byte *srcValue = (byte*) (cmd + 1);
                if (cmd->IsDefault == 0 && cmd->ValueRequiresEntityFixup != 0)
                {
                    AssertNoFixupInMultiPlayback(isFirstPlayback != 0);
                    FixupTemporaryEntitiesInComponentValue(srcValue, typeIndex, in playbackState);
                }
                int newSharedComponentDataIndex = mgr->InsertSharedComponent_Unmanaged(typeIndex, cmd->HashCode,
                    cmd->IsDefault == 1 ? null : srcValue,
                    null);
                mgr->SetSharedComponentDataOnQueryDuringStructuralChange_Unmanaged(cmd->Header.Header.QueryImpl,
                    newSharedComponentDataIndex, ComponentType.FromTypeIndex(typeIndex),
                    cmd->IsDefault == 1 ? null : srcValue,
                    in originSystem);

                CommitStructuralChanges(mgr, ref archetypeChanges);
            }

            public ECBProcessorType ProcessorType => ECBProcessorType.PlaybackProcessor;

            public void SetSharedComponentData(BasicCommand* header)
            {
                var cmd = (EntitySharedComponentCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.SetSharedComponentData");
#endif
                var entity = SelectEntity(cmd->Header.Entity, playbackState);
                mgr->SetSharedComponentDataBoxedDefaultMustBeNullDuringStructuralChange(entity, cmd->ComponentTypeIndex, cmd->HashCode,
                    cmd->GetBoxedObject(), in originSystem);
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckEntityNotNull(Entity entity)
        {
            if (entity == Entity.Null)
                throw new InvalidOperationException("Invalid Entity.Null passed.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckCommandEntity(in ECBSharedPlaybackState playbackState, Entity deferredEntity)
        {
            if (playbackState.CreateEntityBatch == null)
                throw new InvalidOperationException(
                    "playbackState.CreateEntityBatch passed to SelectEntity is null (likely due to an ECB command recording an invalid temporary Entity).");
            if (deferredEntity.Version != playbackState.CommandBufferID)
                throw new InvalidOperationException(
                    $"Deferred Entity {deferredEntity} was created by a different command buffer. Deferred Entities can only be used by the command buffer that created them.");
            int index = -deferredEntity.Index - 1;
            if (index < 0 || index >= playbackState.CreatedEntityCount)
                throw new InvalidOperationException(
                    $"Deferred Entity {deferredEntity} is out of range. Was it created by a different EntityCommandBuffer?");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckEntityVersionValid(Entity entity)
        {
            if (entity.Version <= 0)
                throw new InvalidOperationException("Invalid Entity version");
        }

        private static unsafe Entity SelectEntity(Entity cmdEntity, in ECBSharedPlaybackState playbackState)
        {
            CheckEntityNotNull(cmdEntity);
            if (cmdEntity.Index < 0)
            {
                CheckCommandEntity(playbackState, cmdEntity);
                int index = -cmdEntity.Index - 1;
                Entity e = *(playbackState.CreateEntityBatch + index);
                CheckEntityVersionValid(e);
                return e;
            }
            return cmdEntity;
        }

        private static void CommitStructuralChanges(EntityDataAccess* mgr,
            ref EntityComponentStore.ArchetypeChanges archetypeChanges)
        {
            mgr->EndStructuralChanges(ref archetypeChanges);
            archetypeChanges = mgr->BeginStructuralChanges();
        }

        private static void FixupTemporaryEntitiesInComponentValue(byte* data, TypeIndex typeIndex, in ECBSharedPlaybackState playbackState)
        {
            FixupTemporaryEntitiesInComponentValue(data, 1, typeIndex, playbackState);
        }

        private static void FixupTemporaryEntitiesInComponentValue(byte* data, int count, TypeIndex typeIndex, in ECBSharedPlaybackState playbackState)
        {
            ref readonly var componentTypeInfo = ref TypeManager.GetTypeInfo(typeIndex);

            var offsets = TypeManager.GetEntityOffsets(componentTypeInfo);
            var offsetCount = componentTypeInfo.EntityOffsetCount;
            for (var componentCount = 0; componentCount < count; componentCount++, data += componentTypeInfo.ElementSize)
            {
                for (int i = 0; i < offsetCount; i++)
                {
                    // Need fix ups
                    Entity* e = (Entity*)(data + offsets[i].Offset);
                    if (e->Index < 0)
                    {
                        var index = -e->Index - 1;
                        Entity real = *(playbackState.CreateEntityBatch + index);
                        *e = real;
                    }
                }
            }
        }

        class FixupManagedComponent : Unity.Properties.PropertyVisitor, Unity.Properties.IVisitPropertyAdapter<Entity>
        {
            [ThreadStatic]
            public static FixupManagedComponent _CachedVisitor;

            ECBSharedPlaybackState PlaybackState;
            public FixupManagedComponent()
            {
                AddAdapter(this);
            }

            public static void FixUpComponent(object obj, in ECBSharedPlaybackState state)
            {
                var visitor = FixupManagedComponent._CachedVisitor;
                if (FixupManagedComponent._CachedVisitor == null)
                    FixupManagedComponent._CachedVisitor = visitor = new FixupManagedComponent();

                visitor.PlaybackState = state;
                Unity.Properties.PropertyContainer.Accept(visitor, ref obj);
            }

            void Unity.Properties.IVisitPropertyAdapter<Entity>.Visit<TContainer>(in Unity.Properties.VisitContext<TContainer, Entity> context, ref TContainer container, ref Entity value)
            {
                if (value.Index < 0)
                {
                    var index = -value.Index - 1;
                    Entity real = *(PlaybackState.CreateEntityBatch + index);
                    value = real;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void AddToPostPlaybackFixup(EntityBufferCommand* cmd, ref ECBSharedPlaybackState playbackState)
        {
            var entity = SelectEntity(cmd->Header.Entity, playbackState);
            ECBSharedPlaybackState.BufferWithFixUp* toFixup =
                playbackState.BuffersWithFixUp + playbackState.LastBuffer++;
            toFixup->cmd = cmd;
        }

        static void FixupBufferContents(
            EntityDataAccess* mgr, EntityBufferCommand* cmd, Entity entity,
            ECBSharedPlaybackState playbackState)
        {
            BufferHeader* bufferHeader = (BufferHeader*)mgr->EntityComponentStore->GetComponentDataWithTypeRW(entity, cmd->ComponentTypeIndex, mgr->EntityComponentStore->GlobalSystemVersion);
            FixupTemporaryEntitiesInComponentValue(BufferHeader.GetElementPointer(bufferHeader), bufferHeader->Length,
                cmd->ComponentTypeIndex, playbackState);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckBufferExistsOnEntity(EntityComponentStore* mgr, Entity entity, EntityComponentCommand* cmd)
        {
            if (!mgr->HasComponent(entity, cmd->ComponentTypeIndex))
                throw new InvalidOperationException("Buffer does not exist on entity, cannot append element.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        static void AssertNoFixupInMultiPlayback(bool isFirstPlayback)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (isFirstPlayback)
                return;
            throw new InvalidOperationException("EntityCommandBuffer commands which set components with entity references cannot be played more than once.");
#endif
        }

        static BufferHeader CloneBuffer(BufferHeader* srcBuffer, TypeIndex componentTypeIndex)
        {
            BufferHeader clone = new BufferHeader();
            BufferHeader.Initialize(&clone, 0);

            var alignment = 8; // TODO: Need a way to compute proper alignment for arbitrary non-generic types in TypeManager
            ref readonly var elementSize = ref TypeManager.GetTypeInfo(componentTypeIndex).ElementSize;
            BufferHeader.Assign(&clone, BufferHeader.GetElementPointer(srcBuffer), srcBuffer->Length, elementSize, alignment, false, 0);
            return clone;
        }

        /// <summary>An extension of EntityCommandBuffer that allows concurrent (deterministic) command buffer recording.</summary>
        /// <returns>The <see cref="ParallelWriter"/> that can be used to record commands in parallel.</returns>
        public ParallelWriter AsParallelWriter()
        {
            ParallelWriter parallelWriter;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety0);
            parallelWriter.m_Safety0 = m_Safety0;
            AtomicSafetyHandle.UseSecondaryVersion(ref parallelWriter.m_Safety0);
            parallelWriter.m_BufferSafety = m_BufferSafety;
            parallelWriter.m_ArrayInvalidationSafety = m_ArrayInvalidationSafety;
            parallelWriter.m_SafetyReadOnlyCount = 0;
            parallelWriter.m_SafetyReadWriteCount = 3;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (m_Data->m_Allocator.ToAllocator == Allocator.Temp)
            {
                throw new InvalidOperationException($"{nameof(EntityCommandBuffer.ParallelWriter)} can not use Allocator.Temp; use the EntityCommandBufferSystem's RewindableAllocator instead");
            }
#endif
            parallelWriter.m_Data = m_Data;
            parallelWriter.m_ThreadIndex = -1;

            if (parallelWriter.m_Data != null)
            {
                parallelWriter.m_Data->InitForParallelWriter();
            }

            return parallelWriter;
        }

        /// <summary>
        /// Allows concurrent (deterministic) command buffer recording.
        /// </summary>
        [NativeContainer]
        [NativeContainerIsAtomicWriteOnly]
        [StructLayout(LayoutKind.Sequential)]
        unsafe public struct ParallelWriter
        {
            [NativeDisableUnsafePtrRestriction] internal EntityCommandBufferData* m_Data;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety0;
            internal AtomicSafetyHandle m_BufferSafety;
            internal AtomicSafetyHandle m_ArrayInvalidationSafety;
            internal int m_SafetyReadOnlyCount;
            internal int m_SafetyReadWriteCount;
#endif

            // NOTE: Until we have a way to safely batch, let's keep it off
            private const bool kBatchableCommand = false;

            //internal ref int m_EntityIndex;
            [NativeSetThreadIndex]
            internal int m_ThreadIndex;

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
            private void CheckWriteAccess()
            {
                if (m_Data == null)
                    throw new NullReferenceException("The EntityCommandBuffer has not been initialized! The EntityCommandBuffer needs to be passed an Allocator when created!");
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety0);
#endif
            }

            private EntityCommandBufferChain* ThreadChain => (m_ThreadIndex >= 0) ? &m_Data->m_ThreadedChains[m_ThreadIndex] : &m_Data->m_MainThreadChain;

            /// <summary>Records a command to create an entity with specified archetype.</summary>
            /// <remarks>At playback, this command will throw an error if the archetype contains the <see cref="Prefab"/> tag.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="archetype">The archetype of the new entity.</param>
            /// <returns>An entity that is deferred and will be fully realized when this EntityCommandBuffer is played back.</returns>
            /// <exception cref="ArgumentException">Throws if the archetype is null.</exception>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public Entity CreateEntity(int sortKey, EntityArchetype archetype)
            {
                archetype.CheckValidEntityArchetype();
                return _CreateEntity(sortKey, archetype);
            }

            /// <summary>Records a command to create an entity with no components.</summary>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <returns>An entity that is deferred and will be fully realized when this EntityCommandBuffer is played back.</returns>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public Entity CreateEntity(int sortKey)
            {
                EntityArchetype archetype = new EntityArchetype();
                return _CreateEntity(sortKey, archetype);
            }

            private Entity _CreateEntity(int sortKey, EntityArchetype archetype)
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                // NOTE: Contention could be a performance problem especially on ARM
                // architecture. Maybe reserve a few indices for each job would be a better
                // approach or hijack the Version field of an Entity and store sortKey
                var entity = m_Data->m_Entity;
                entity.Index = Interlocked.Decrement(ref m_Data->m_Entity.Index);
                m_Data->AddCreateCommand(chain, sortKey, ECBCommand.CreateEntity,  entity.Index, archetype, kBatchableCommand);
                return entity;
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
            private static void CheckNotNull(Entity e)
            {
                if (Hint.Unlikely(e == Entity.Null))
                    throw new ArgumentNullException(nameof(e));
            }

            /// <summary>Records a command to create an entity with specified entity prefab.</summary>
            /// <remarks>An instantiated entity will have the same components and component values as the prefab entity, minus the Prefab tag component.
            /// At playback, this command will throw an error if the source entity was destroyed before playback.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e">The entity prefab.</param>
            /// <returns>An entity that is deferred and will be fully realized when this EntityCommandBuffer is played back.</returns>
            /// <exception cref="ArgumentNullException"> Thrown if Entity e is null and if safety checks are enabled.</exception>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public Entity Instantiate(int sortKey, Entity e)
            {
                CheckNotNull(e);

                CheckWriteAccess();
                var chain = ThreadChain;
                var entity = m_Data->m_Entity;
                entity.Index = Interlocked.Decrement(ref m_Data->m_Entity.Index);
                m_Data->AddEntityCommand(chain, sortKey, ECBCommand.InstantiateEntity, entity.Index, e, kBatchableCommand);
                return entity;
            }

            /// <summary>Records a command to create a NativeArray of entities with specified entity prefab.</summary>
            /// <remarks>An instantiated entity will have the same components and component values as the prefab entity, minus the Prefab tag component.
            /// At playback, this command will throw an error if the source entity was destroyed before playback.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e">The entity prefab.</param>
            /// <param name="entities">The NativeArray of entities that will be populated with realized entities when this EntityCommandBuffer is played back.</param>
            /// <exception cref="ArgumentNullException"> Thrown if Entity e is null and if safety checks are enabled.</exception>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void Instantiate(int sortKey, Entity e, NativeArray<Entity> entities)
            {
                CheckNotNull(e);

                CheckWriteAccess();
                var chain = ThreadChain;
                var entity = m_Data->m_Entity;
                int baseIndex = Interlocked.Add(ref m_Data->m_Entity.Index, -entities.Length) + entities.Length - 1;
                for (int i=0; i<entities.Length; ++i)
                {
                    entity.Index = baseIndex - i;
                    entities[i] = entity;
                }
                m_Data->AddMultipleEntityCommand(chain, sortKey, ECBCommand.InstantiateEntity, baseIndex, entities.Length, e, kBatchableCommand);
            }


            /// <summary>Records a command to destroy an entity.</summary>
            /// <remarks>At playback, this command will throw an error if any of the entities are still deferred or were destroyed between recording and playback,
            /// or if the entity has the <see cref="Prefab"/> tag.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e">The entity to destroy.</param>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void DestroyEntity(int sortKey, Entity e)
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                m_Data->AddEntityCommand(chain, sortKey, ECBCommand.DestroyEntity, 0, e, false);
            }

            /// <summary>Records a command to destroy a NativeArray of entities.</summary>
            /// <remarks>At playback, this command will do nothing if entities has a count of 0.
            /// This command will throw an error if any of the entities are still deferred or were destroyed between recording and playback,
            /// or if any of the entities have the <see cref="Prefab"/> tag.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="entities">The NativeArray of entities to destroy.</param>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void DestroyEntity(int sortKey, NativeArray<Entity> entities)
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                var entitiesCopy = m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
                m_Data->AppendMultipleEntitiesCommand(chain, sortKey, ECBCommand.DestroyMultipleEntities, entitiesCopy, entities.Length, containsDeferredEntities);
            }

            /// <summary> Records a command to add component of type T to an entity. </summary>
            /// <remarks>At playback, if the entity already has this type of component, the value will just be set.
            /// Will throw an error if this entity is destroyed before playback, if this entity is still deferred,
            /// if T is type Entity or <see cref="Prefab"/>, or adding this componentType makes the archetype too large.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e"> The entity to have the component added. </param>
            /// <param name="component">The value to add on the new component in playback for the entity.</param>
            /// <typeparam name="T"> The type of component to add. </typeparam>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void AddComponent<T>(int sortKey, Entity e, T component) where T : unmanaged, IComponentData
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                m_Data->AddEntityComponentTypeWithValueCommand(chain, sortKey, ECBCommand.AddComponent, e, component);
            }

            /// <summary> Records a command to add a component to an entity. </summary>
            /// <remarks>At playback, this command will do nothing if the entity already has the component.
            /// Will throw an error if this entity is destroyed before playback, if this entity is still deferred,
            /// if component type is type Entity or <see cref="Prefab"/>, or adding this componentType makes the archetype too large.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="typeIndex"> The TypeIndex of the component being set. </param>
            /// <param name="typeSize"> The Size of the type of the component being set. </param>
            /// <param name="componentDataPtr"> The pointer to the data of the component to be copied. </param>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
            internal void UnsafeAddComponent(int sortKey, Entity e, TypeIndex typeIndex, int typeSize, void* componentDataPtr)
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                m_Data->UnsafeAddEntityComponentCommand(chain, sortKey, ECBCommand.AddComponent, e, typeIndex, typeSize, componentDataPtr);
            }

            /// <summary> Records a command to add component of type T to a NativeArray of entities. </summary>
            /// <remarks>At playback, if any entity already has this type of component, the value will just be set.
            /// Will throw an error if any entity is destroyed before playback, if any entity is still deferred,
            /// if T is type Entity or <see cref="Prefab"/>, or adding this componentType makes the archetype too large.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="entities"> The NativeArray of entities to have the component added. </param>
            /// <param name="component">The value to add on the new component in playback for all entities in the NativeArray.</param>
            /// <typeparam name="T"> The type of component to add. </typeparam>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void AddComponent<T>(int sortKey, NativeArray<Entity> entities, T component) where T : unmanaged, IComponentData
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                var entitiesCopy = m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
                m_Data->AppendMultipleEntitiesComponentCommandWithValue(chain, sortKey,
                    ECBCommand.AddComponentForMultipleEntities, entitiesCopy, entities.Length, containsDeferredEntities, component);
            }

            /// <summary> Records a command to add component of type T to an entity. </summary>
            /// <remarks>At playback, this command will do nothing if the entity already has the component.
            /// Will throw an error if this entity is destroyed before playback, if this entity is still deferred,
            /// if T is type Entity or <see cref="Prefab"/>, or adding this componentType makes the archetype too large.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e"> The entity to have the component added. </param>
            /// <typeparam name="T"> The type of component to add. </typeparam>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void AddComponent<T>(int sortKey, Entity e) where T : unmanaged, IComponentData
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                m_Data->AddEntityComponentTypeWithoutValueCommand(chain, sortKey, ECBCommand.AddComponent, e, ComponentType.ReadWrite<T>());
            }

            /// <summary> Records a command to add component of type T to a NativeArray of entities. </summary>
            /// <remarks>At playback, if an entity already has this component, it will be skipped.
            /// Will throw an error if any entity is destroyed before playback, if any entity is still deferred,
            /// if T is type Entity or <see cref="Prefab"/>, or adding this componentType makes the archetype too large.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="entities"> The NativeArray of entities to have the component added. </param>
            /// <typeparam name="T"> The type of component to add. </typeparam>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void AddComponent<T>(int sortKey, NativeArray<Entity> entities) where T : unmanaged, IComponentData
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                var entitiesCopy = m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
                m_Data->AppendMultipleEntitiesComponentCommand(chain, sortKey,
                    ECBCommand.AddComponentForMultipleEntities, entitiesCopy, entities.Length, containsDeferredEntities,
                    ComponentType.ReadWrite<T>());
            }

            /// <summary> Records a command to add a component to an entity. </summary>
            /// <remarks>At playback, this command will do nothing if the entity already has the component.
            /// Will throw an error if this entity is destroyed before playback, if this entity is still deferred,
            /// if component type is type Entity or <see cref="Prefab"/>, or adding this componentType makes the archetype too large.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e"> The entity to get the additional component. </param>
            /// <param name="componentType"> The type of component to add. </param>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void AddComponent(int sortKey, Entity e, ComponentType componentType)
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                m_Data->AddEntityComponentTypeWithoutValueCommand(chain, sortKey, ECBCommand.AddComponent, e, componentType);
            }

            /// <summary> Records a command to add a component to a NativeArray of entities. </summary>
            /// <remarks>At playback, if an entity already has this component, it will be skipped.
            /// Will throw an error if any entity is destroyed before playback, if any entity is still deferred,
            /// if component type is type Entity or <see cref="Prefab"/>, or adding this componentType makes the archetype too large.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="entities"> The NativeArray of entities to have the component added. </param>
            /// <param name="componentType"> The type of component to add. </param>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void AddComponent(int sortKey, NativeArray<Entity> entities, ComponentType componentType)
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                var entitiesCopy = m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
                m_Data->AppendMultipleEntitiesComponentCommand(chain, sortKey,
                    ECBCommand.AddComponentForMultipleEntities, entitiesCopy, entities.Length, containsDeferredEntities, componentType);
            }

            /// <summary> Records a command to add one or more components to an entity. </summary>
            /// <remarks>At playback, it's not an error to include a component type that the entity already has.
            /// Will throw an error if this entity is destroyed before playback, if this entity is still deferred,
            /// if any component type is type Entity or <see cref="Prefab"/>, or adding a component type makes the archetype too large.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e"> The entity to get additional components. </param>
            /// <param name="typeSet"> The types of components to add. </param>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void AddComponent(int sortKey, Entity e, in ComponentTypeSet typeSet)
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                m_Data->AddEntityComponentTypesCommand(chain, sortKey,ECBCommand.AddMultipleComponents, e, typeSet);
            }

            /// <summary> Records a command to add one or more components to a NativeArray of entities. </summary>
            /// <remarks>At playback, it's not an error to include a component type that any of the entities already have.
            /// Will throw an error if any entity is destroyed before playback, if any entity is still deferred,
            /// if any component type is type Entity or <see cref="Prefab"/>, or adding a component type makes the archetype too large.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="entities"> The NativeArray of entities to have the components added. </param>
            /// <param name="typeSet"> The types of components to add. </param>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void AddComponent(int sortKey, NativeArray<Entity> entities, in ComponentTypeSet typeSet)
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                var entitiesCopy = m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
                m_Data->AppendMultipleEntitiesMultipleComponentsCommand(chain, sortKey,
                    ECBCommand.AddMultipleComponentsForMultipleEntities, entitiesCopy, entities.Length, containsDeferredEntities, typeSet);
            }

            /// <summary>Records a command to add a dynamic buffer to an entity.</summary>
            /// <remarks>At playback, if the entity already has this type of dynamic buffer,
            /// this method sets the dynamic buffer contents. If the entity doesn't have a
            /// <see cref="DynamicBuffer{T}"/> component that stores elements of type T, then
            /// this method adds a DynamicBuffer component with the provided contents. If the
            /// entity is destroyed before playback, or [is deferred](xref:systems-entity-command-buffers),
            /// an error is thrown.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e">The entity to add the dynamic buffer to.</param>
            /// <typeparam name="T">The <see cref="IBufferElementData"/> type stored by the <see cref="DynamicBuffer{T}"/>.</typeparam>
            /// <returns>The <see cref="DynamicBuffer{T}"/> that will be added when the command plays back.</returns>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public DynamicBuffer<T> AddBuffer<T>(int sortKey, Entity e) where T : unmanaged, IBufferElementData
            {
                CheckWriteAccess();
                var chain = ThreadChain;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                return m_Data->CreateBufferCommand<T>(ECBCommand.AddBuffer, chain, sortKey, e, m_BufferSafety, m_ArrayInvalidationSafety);
#else
                return m_Data->CreateBufferCommand<T>(ECBCommand.AddBuffer, chain, sortKey, e);
#endif
            }

            /// <summary>Records a command to set a dynamic buffer on an entity.</summary>
            /// <remarks>At playback, this command throws an error if this entity is destroyed before playback,
            /// if this entity is still deferred, or if the entity doesn't have a <see cref="DynamicBuffer{T}"/> component storing elements of type T.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e">The entity to set the dynamic buffer on.</param>
            /// <typeparam name="T">The <see cref="IBufferElementData"/> type stored by the <see cref="DynamicBuffer{T}"/>.</typeparam>
            /// <returns>The <see cref="DynamicBuffer{T}"/> that will be set when the command plays back.</returns>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public DynamicBuffer<T> SetBuffer<T>(int sortKey, Entity e) where T : unmanaged, IBufferElementData
            {
                CheckWriteAccess();
                var chain = ThreadChain;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                return m_Data->CreateBufferCommand<T>(ECBCommand.SetBuffer, chain, sortKey, e, m_BufferSafety, m_ArrayInvalidationSafety);
#else
                return m_Data->CreateBufferCommand<T>(ECBCommand.SetBuffer, chain, sortKey, e);
#endif
            }

            /// <summary>Records a command to append a single element to the end of a dynamic buffer component.</summary>
            /// <remarks>At playback, this command throws an error if this entity is destroyed before playback,
            /// if this entity is still deferred, or if the entity doesn't have a <see cref="DynamicBuffer{T}"/> component storing elements of type T.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e">The entity to which the dynamic buffer belongs.</param>
            /// <param name="element">The new element to add to the <see cref="DynamicBuffer{T}"/> component.</param>
            /// <typeparam name="T">The <see cref="IBufferElementData"/> type stored by the <see cref="DynamicBuffer{T}"/>.</typeparam>
            /// <exception cref="InvalidOperationException">Thrown if the entity does not have a <see cref="DynamicBuffer{T}"/>
            /// component storing elements of type T at the time the entity command buffer executes this append-to-buffer command.</exception>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void AppendToBuffer<T>(int sortKey, Entity e, T element) where T : struct, IBufferElementData
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                m_Data->AppendToBufferCommand<T>(chain, sortKey, e, element);
            }

            /// <summary> Records a command to set a component value on an entity.</summary>
            /// <remarks> At playback, this command throws an error if this entity is destroyed before playback,
            /// if this entity is still deferred, if the entity doesn't have the component type,
            /// if the entity has the <see cref="Prefab"/> tag, or if T is zero sized.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e"> The entity to set the component value of. </param>
            /// <param name="component"> The component value to set. </param>
            /// <typeparam name="T"> The type of component to set. </typeparam>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void SetComponent<T>(int sortKey, Entity e, T component) where T : unmanaged, IComponentData
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                m_Data->AddEntityComponentTypeWithValueCommand(chain, sortKey, ECBCommand.SetComponent, e, component);
            }

            /// <summary> Records a command to set a component value on an entity.</summary>
            /// <remarks> At playback, this command throws an error if this entity is destroyed before playback,
            /// if this entity is still deferred, if the entity doesn't have the component type,
            /// if the entity has the <see cref="Prefab"/> tag, or if T is zero sized.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e"> The entity to set the component value of. </param>
            /// <param name="typeIndex"> The TypeIndex of the component being set. </param>
            /// <param name="typeSize"> The Size of the type of the component being set. </param>
            /// <param name="componentDataPtr"> The pointer to the data of the component to be copied. </param>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
            internal void UnsafeSetComponent(int sortKey, Entity e, TypeIndex typeIndex, int typeSize, void* componentDataPtr)
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                m_Data->UnsafeAddEntityComponentCommand(chain, sortKey, ECBCommand.SetComponent, e, typeIndex, typeSize, componentDataPtr);
            }

            /// <summary>
            /// Records a command to add or remove the <see cref="Disabled"/> component. By default EntityQuery does not include entities containing the Disabled component.
            /// Enabled entities are processed by systems, disabled entities are not.
            ///
            /// If the entity was converted from a prefab and thus has a <see cref="LinkedEntityGroup"/> component, the entire group will be enabled or disabled.
            /// </summary>
            /// <remarks> At playback, this command throws an error if this entity is destroyed before playback,
            /// if the entity has the <see cref="Prefab"/> tag, or if this entity is still deferred.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e">The entity whose component should be enabled or disabled.</param>
            /// <param name="value">True if the specified component should be enabled, or false if it should be disabled.</param>
            [GenerateTestsForBurstCompatibility]
            public void SetEnabled(int sortKey, Entity e, bool value)
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                m_Data->AddEntityEnabledCommand(chain, sortKey, ECBCommand.SetEntityEnabled, e, value);
            }

            /// <summary>
            /// Records a command to enable or disable a <see cref="ComponentType"/> on the specified <see cref="Entity"/>. This operation
            /// does not cause a structural change, or affect the value of the component. For the purposes
            /// of EntityQuery matching, an entity with a disabled component will behave as if it does not have that component.
            /// </summary>
            /// <remarks> At playback, this command throws an error if this entity is destroyed before playback,
            /// if this entity is still deferred, if the entity has the <see cref="Prefab"/> tag, or if the entity doesn't have the component type.</remarks>
            /// <typeparam name="T">The component type to enable or disable. This type must implement the
            /// <see cref="IEnableableComponent"/> interface.</typeparam>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e">The entity whose component should be enabled or disabled.</param>
            /// <param name="value">True if the specified component should be enabled, or false if it should be disabled.</param>
            [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleEnableableComponent) })]
            public void SetComponentEnabled<T>(int sortKey, Entity e, bool value) where T: struct, IEnableableComponent
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                m_Data->AddEntityComponentEnabledCommand(chain, sortKey,
                    ECBCommand.SetComponentEnabled, e, TypeManager.GetTypeIndex<T>(), value);
            }
            /// <summary>
            /// Records a command to enable or disable a <see cref="ComponentType"/> on the specified <see cref="Entity"/>. This operation
            /// does not cause a structural change, or affect the value of the component. For the purposes
            /// of EntityQuery matching, an entity with a disabled component will behave as if it does not have that component.
            /// </summary>
            /// <remarks> At playback, this command throws an error if this entity is destroyed before playback,
            /// if this entity is still deferred, if the entity has the <see cref="Prefab"/> tag, or if the entity doesn't have the component type.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e">The entity whose component should be enabled or disabled.</param>
            /// <param name="componentType">The component type to enable or disable. This type must implement the
            /// <see cref="IEnableableComponent"/> interface.</param>
            /// <param name="value">True if the specified component should be enabled, or false if it should be disabled.</param>
            public void SetComponentEnabled(int sortKey, Entity e, ComponentType componentType, bool value)
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                m_Data->AddEntityComponentEnabledCommand(chain, sortKey,
                    ECBCommand.SetComponentEnabled, e, componentType.TypeIndex, value);
            }

            /// <summary> Records a command to set a name of an entity if Debug Names is enabled.</summary>
            /// <remarks> At playback, this command throws an error if this entity is destroyed before playback,
            /// if this entity is still deferred, if the entity has the <see cref="Prefab"/> tag, or if the EntityNameStore has reached its limit.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e"> The entity to set the name value of. </param>
            /// <param name="name"> The name to set. </param>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void SetName(int sortKey, Entity e, in FixedString64Bytes name)
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                m_Data->AddEntityNameCommand(chain, sortKey, ECBCommand.SetName, e, name);
            }

            /// <summary> Records a command to remove component of type T from an entity. </summary>
            /// <remarks>At playback, it's not an error if the entity doesn't have component T.
            /// Will throw an error if this entity is destroyed before playback,
            /// if this entity is still deferred, or if T is type Entity or <see cref="Prefab"/>.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e"> The entity to have the component removed. </param>
            /// <typeparam name="T"> The type of component to remove. </typeparam>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void RemoveComponent<T>(int sortKey, Entity e)
            {
                RemoveComponent(sortKey, e, ComponentType.ReadWrite<T>());
            }

            /// <summary> Records a command to remove component of type T from a NativeArray of entities. </summary>
            /// <remarks>At playback, it's not an error if any entity doesn't have component T.
            /// Will throw an error if one of these entities is destroyed before playback,
            /// if one of these entities is still deferred, or if T is type Entity or <see cref="Prefab"/>.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="entities"> The NativeArray of entities to have the component removed. </param>
            /// <typeparam name="T"> The type of component to remove. </typeparam>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void RemoveComponent<T>(int sortKey, NativeArray<Entity> entities)
            {
                RemoveComponent(sortKey, entities, ComponentType.ReadWrite<T>());
            }

            /// <summary> Records a command to remove a component from an entity. </summary>
            /// <remarks>At playback, it's not an error if the entity doesn't have the component type.
            /// Will throw an error if this entity is destroyed before playback,
            /// if this entity is still deferred, or if the component type is Entity or <see cref="Prefab"/>.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e"> The entity to have the component removed. </param>
            /// <param name="componentType"> The type of component to remove. </param>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void RemoveComponent(int sortKey, Entity e, ComponentType componentType)
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                m_Data->AddEntityComponentTypeWithoutValueCommand(chain, sortKey, ECBCommand.RemoveComponent, e, componentType);
            }

            /// <summary> Records a command to remove one or more components from a NativeArray of entities. </summary>
            /// <remarks>At playback, it's not an error if any entity doesn't have the component type.
            /// Will throw an error if one of these entities is destroyed before playback,
            /// if one of these entities is still deferred, or if the component type is Entity or <see cref="Prefab"/>.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="entities"> The NativeArray of entities to have the component removed. </param>
            /// <param name="componentType"> The type of component to remove. </param>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void RemoveComponent(int sortKey, NativeArray<Entity> entities, ComponentType componentType)
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                var entitiesCopy = m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
                m_Data->AppendMultipleEntitiesComponentCommand(chain, sortKey,
                    ECBCommand.RemoveComponentForMultipleEntities, entitiesCopy, entities.Length, containsDeferredEntities, componentType);
            }

            /// <summary> Records a command to remove one or more components from an entity. </summary>
            /// <remarks>At playback, it's not an error if the entity doesn't have one of the component types.
            /// Will throw an error if this entity is destroyed before playback,
            /// if this entity is still deferred, or if any of the component types are Entity or <see cref="Prefab"/>.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e"> The entity to have the components removed. </param>
            /// <param name="typeSet"> The types of components to remove. </param>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void RemoveComponent(int sortKey, Entity e, in ComponentTypeSet typeSet)
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                m_Data->AddEntityComponentTypesCommand(chain, sortKey,ECBCommand.RemoveMultipleComponents, e, typeSet);
            }

            /// <summary> Records a command to remove one or more components from a NativeArray of entities. </summary>
            /// <remarks>At playback, it's not an error if any entity doesn't have one of the component types.
            /// Will throw an error if one of these entities is destroyed before playback,
            /// if one of these entities is still deferred, or if any of the component types are Entity or <see cref="Prefab"/>.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="entities"> The NativeArray of entities to have components removed. </param>
            /// <param name="typeSet"> The types of components to remove. </param>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void RemoveComponent(int sortKey, NativeArray<Entity> entities, in ComponentTypeSet typeSet)
            {
                CheckWriteAccess();
                var chain = ThreadChain;
                var entitiesCopy = m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
                m_Data->AppendMultipleEntitiesMultipleComponentsCommand(chain, sortKey,
                    ECBCommand.RemoveMultipleComponentsForMultipleEntities, entitiesCopy, entities.Length, containsDeferredEntities, typeSet);
            }

            /// <summary> Records a command to add a shared component value on an entity.</summary>
            /// <remarks>At playback, this command throws an error if this entity is destroyed before playback,
            /// if this entity is still deferred, if adding this shared component exceeds the maximum number of shared components,
            /// or adding a component type makes the archetype too large.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e"> The entity to add the shared component value to. </param>
            /// <param name="sharedComponent"> The shared component value to add. </param>
            /// <typeparam name="T"> The type of shared component to add. </typeparam>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void AddSharedComponentManaged<T>(int sortKey, Entity e, T sharedComponent) where T : struct, ISharedComponentData
            {
                CheckWriteAccess();
                var chain = ThreadChain;

                int hashCode;
                if (IsDefaultObject(ref sharedComponent, out hashCode))
                    m_Data->AddEntitySharedComponentCommand<T>(chain, sortKey, ECBCommand.AddSharedComponentData, e, hashCode, null);
                else
                    m_Data->AddEntitySharedComponentCommand<T>(chain, sortKey, ECBCommand.AddSharedComponentData, e, hashCode, sharedComponent);
            }

            /// <summary> Records a command to add an unmanaged shared component value on an entity.</summary>
            /// <remarks>At playback, this command throws an error if this entity is destroyed before playback,
            /// if this entity is still deferred, if adding this shared component exceeds the maximum number of shared components,
            /// or adding a component type makes the archetype too large.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e"> The entity to add the shared component value to. </param>
            /// <param name="sharedComponent"> The shared component value to add. </param>
            /// <typeparam name="T"> The type of shared component to add. </typeparam>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void AddSharedComponent<T>(int sortKey, Entity e, T sharedComponent)
                where T : unmanaged, ISharedComponentData
            {
                CheckWriteAccess();
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                var typeIndex = TypeManager.GetTypeIndex<T>();
                var isManaged = TypeManager.IsManagedSharedComponent(typeIndex);
                UnityEngine.Assertions.Assert.IsFalse(isManaged, $"{sharedComponent}: is managed and was passed to AddSharedComponentUnmanaged");
#endif
                var componentData = IsDefaultObjectUnmanaged(ref sharedComponent, out var hashCode)
                    ? null
                    : UnsafeUtility.AddressOf(ref sharedComponent);
                m_Data->AddEntityUnmanagedSharedComponentCommand<T>(ThreadChain, sortKey, ECBCommand.AddUnmanagedSharedComponentData, e, hashCode, componentData);
            }

            /// <summary> Records a command to add a possibly-managed shared component value on a NativeArray of entities.</summary>
            /// <remarks>At playback, this command throws an error if any entity is destroyed before playback,
            /// if any entity is still deferred, if adding this shared component exceeds the maximum number of shared components,
            /// or adding a component type makes the archetype too large.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="entities"> The NativeArray of entities to add the shared component value to. </param>
            /// <param name="sharedComponent"> The shared component value to add. </param>
            /// <typeparam name="T"> The type of shared component to add. </typeparam>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void AddSharedComponentManaged<T>(int sortKey, NativeArray<Entity> entities, T sharedComponent) where T : struct, ISharedComponentData
            {
                CheckWriteAccess();
                var chain = ThreadChain;

                int hashCode;
                var entitiesCopy = m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
                var isdefault = IsDefaultObject(ref sharedComponent, out hashCode);

                if (TypeManager.IsManagedSharedComponent(TypeManager.GetTypeIndex<T>()))
                {
                    m_Data->AppendMultipleEntitiesComponentCommandWithSharedValue<T>(
                        chain,
                        sortKey,
                        ECBCommand.AddSharedComponentWithValueForMultipleEntities,
                        entitiesCopy,
                        entities.Length,
                        containsDeferredEntities,
                        hashCode,
                        isdefault ? (object) null : sharedComponent);

                }
                else
                {
                    m_Data->AppendMultipleEntitiesCommand_WithUnmanagedSharedValue<T>(
                        chain,
                        sortKey,
                        ECBCommand.AddUnmanagedSharedComponentValueForMultipleEntities,
                        entitiesCopy,
                        entities.Length,
                        containsDeferredEntities,
                        hashCode,
                        isdefault ? null : UnsafeUtility.AddressOf(ref sharedComponent));
                }
            }

            /// <summary> Records a command to add an unmanaged shared component value on a NativeArray of entities.</summary>
            /// <remarks>At playback, this command throws an error if any entity is destroyed before playback,
            /// if any entity is still deferred, if adding this shared component exceeds the maximum number of shared components,
            /// or adding a component type makes the archetype too large.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="entities"> The NativeArray of entities to add the shared component value to. </param>
            /// <param name="sharedComponent"> The shared component value to add. </param>
            /// <typeparam name="T"> The type of shared component to add. </typeparam>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void AddSharedComponent<T>(int sortKey, NativeArray<Entity> entities, T sharedComponent)
                where T : unmanaged, ISharedComponentData
            {
                CheckWriteAccess();
                var chain = ThreadChain;

                int hashCode;
                var entitiesCopy =
                    m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
                var isdefault = IsDefaultObjectUnmanaged(ref sharedComponent, out hashCode);

                m_Data->AppendMultipleEntitiesCommand_WithUnmanagedSharedValue<T>(
                    chain,
                    sortKey,
                    ECBCommand.AddUnmanagedSharedComponentValueForMultipleEntities,
                    entitiesCopy,
                    entities.Length,
                    containsDeferredEntities,
                    hashCode,
                    isdefault ? null : UnsafeUtility.AddressOf(ref sharedComponent));
            }

            /// <summary> Records a command to set a shared component value on an entity.</summary>
            /// <remarks> At playback, this command throws an error if this entity is destroyed before playback,
            /// if this entity is still deferred, if the entity has the <see cref="Prefab"/> tag, or if the entity doesn't have the shared component type.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e"> The entity to set the shared component value of. </param>
            /// <param name="sharedComponent"> The shared component value to set. </param>
            /// <typeparam name="T"> The type of shared component to set. </typeparam>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void SetSharedComponentManaged<T>(int sortKey, Entity e, T sharedComponent) where T : struct, ISharedComponentData
            {
                CheckWriteAccess();
                var chain = ThreadChain;

                int hashCode;

                var typeIndex = TypeManager.GetTypeIndex<T>();
                var isDefaultObject = IsDefaultObject(ref sharedComponent, out hashCode);
                var isManaged = TypeManager.IsManagedSharedComponent(typeIndex);
                if (isManaged)
                {
                    m_Data->AddEntitySharedComponentCommand<T>(
                        chain,
                        sortKey,
                        ECBCommand.SetSharedComponentData,
                        e,
                        hashCode,
                        isDefaultObject ? (object) null : sharedComponent);
                }
                else
                {
                    m_Data->AddEntityUnmanagedSharedComponentCommand<T>(
                        chain,
                        sortKey,
                        ECBCommand.SetUnmanagedSharedComponentData,
                        e,
                        hashCode,
                        isDefaultObject ? null : UnsafeUtility.AddressOf(ref sharedComponent));
                }
            }

            /// <summary> Records a command to set an unmanaged shared component value on an entity.</summary>
            /// <remarks> At playback, this command throws an error if this entity is destroyed before playback,
            /// if this entity is still deferred, if the entity has the <see cref="Prefab"/> tag, or if the entity doesn't have the shared component type.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e"> The entity to set the shared component value of. </param>
            /// <param name="sharedComponent"> The shared component value to set. </param>
            /// <typeparam name="T"> The type of shared component to set. </typeparam>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void SetSharedComponent<T>(int sortKey, Entity e, T sharedComponent) where T : unmanaged, ISharedComponentData
            {
                CheckWriteAccess();

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                var typeIndex = TypeManager.GetTypeIndex<T>();
                var isManaged = TypeManager.IsManagedSharedComponent(typeIndex);
                UnityEngine.Assertions.Assert.IsFalse(isManaged, $"{sharedComponent}: is managed and was passed to SetSharedComponentUnmanaged");
#endif
                var componentData = IsDefaultObjectUnmanaged(ref sharedComponent, out var hashCode) ? null : UnsafeUtility.AddressOf(ref sharedComponent);
                m_Data->AddEntityUnmanagedSharedComponentCommand<T>(
                    ThreadChain,
                    sortKey,
                    ECBCommand.SetUnmanagedSharedComponentData,
                    e,
                    hashCode,
                    componentData);
            }

            /// <summary>
            /// Only for inserting a non-default value
            /// </summary>
            internal void UnsafeSetSharedComponentNonDefault(int sortKey, Entity e, void* componentDataPtr, TypeIndex typeIndex)
            {
                CheckWriteAccess();

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                var isManaged = TypeManager.IsManagedSharedComponent(typeIndex);
                UnityEngine.Assertions.Assert.IsFalse(isManaged, $"{typeIndex}: is managed and was passed to UnsafeSetSharedComponentNonDefault");
#endif
                // Guarantee that it is non-default
                m_Data->AddEntityUnmanagedSharedComponentCommand(
                    ThreadChain,
                    sortKey,
                    ECBCommand.SetUnmanagedSharedComponentData,
                    e,
                    TypeManager.SharedComponentGetHashCode(componentDataPtr, typeIndex),
                    typeIndex,
                    TypeManager.GetTypeInfo(typeIndex).TypeSize,
                    componentDataPtr);
            }

            /// <summary> Records a command to set a shared component value on a NativeArray of entities.</summary>
            /// <remarks> At playback, this command throws an error if any entity is destroyed before playback,
            /// if any entity is still deferred, if any entity has the <see cref="Prefab"/> tag, or if any entity doesn't have the shared component type.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="entities"> The NativeArray of entities to set the shared component value of. </param>
            /// <param name="sharedComponent"> The shared component value to set. </param>
            /// <typeparam name="T"> The type of shared component to set. </typeparam>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void SetSharedComponentManaged<T>(int sortKey, NativeArray<Entity> entities, T sharedComponent) where T : struct, ISharedComponentData
            {
                CheckWriteAccess();
                var chain = ThreadChain;

                int hashCode;
                var entitiesCopy = m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
                var isDefaultObject = IsDefaultObject(ref sharedComponent, out hashCode);

                if (TypeManager.IsManagedSharedComponent(TypeManager.GetTypeIndex<T>()))
                {
                    m_Data->AppendMultipleEntitiesComponentCommandWithSharedValue<T>(
                        chain,
                        sortKey,
                        ECBCommand.SetSharedComponentValueForMultipleEntities,
                        entitiesCopy,
                        entities.Length,
                        containsDeferredEntities,
                        hashCode,
                        isDefaultObject ? default : sharedComponent);
                }
                else
                {
                    m_Data->AppendMultipleEntitiesCommand_WithUnmanagedSharedValue<T>(
                        chain,
                        sortKey,
                        ECBCommand.SetUnmanagedSharedComponentValueForMultipleEntities,
                        entitiesCopy,
                        entities.Length,
                        containsDeferredEntities,
                        hashCode,
                        isDefaultObject ? null : UnsafeUtility.AddressOf(ref sharedComponent));
                }
            }

            /// <summary> Records a command to set a shared component value on a NativeArray of entities.</summary>
            /// <remarks> At playback, this command throws an error if any entity is destroyed before playback,
            /// if any entity is still deferred, if any entity has the <see cref="Prefab"/> tag, or if any entity doesn't have the shared component type.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="entities"> The NativeArray of entities to set the shared component value of. </param>
            /// <param name="sharedComponent"> The shared component value to set. </param>
            /// <typeparam name="T"> The type of shared component to set. </typeparam>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            public void SetSharedComponent<T>(int sortKey, NativeArray<Entity> entities, T sharedComponent)
                where T : unmanaged, ISharedComponentData
            {
                CheckWriteAccess();
                var chain = ThreadChain;

                int hashCode;
                var entitiesCopy =
                    m_Data->CloneAndSearchForDeferredEntities(entities, out var containsDeferredEntities);
                var isDefaultObject = IsDefaultObjectUnmanaged(ref sharedComponent, out hashCode);

                m_Data->AppendMultipleEntitiesCommand_WithUnmanagedSharedValue<T>(
                    chain,
                    sortKey,
                    ECBCommand.SetUnmanagedSharedComponentValueForMultipleEntities,
                    entitiesCopy,
                    entities.Length,
                    containsDeferredEntities,
                    hashCode,
                    isDefaultObject ? null : UnsafeUtility.AddressOf(ref sharedComponent));
            }

            /// <summary>Records a command that adds a component to an entity's <see cref="LinkedEntityGroup"/> based on an <see cref="EntityQueryMask"/>.
            /// Entities in the <see cref="LinkedEntityGroup"/> that don't match the mask will be skipped safely.</summary>
            /// <remarks>At playback, this command throws an error if the entity is destroyed before playback,
            /// if the entity is still deferred, or if any of the matching linked entities cannot add the component.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e">The entity whose LinkedEntityGroup will be referenced.</param>
            /// <param name="mask">The EntityQueryMask that is used to determine which linked entities to add the component to.
            /// Note that EntityQueryMask ignores all query filtering (including chunk filtering and enableable components),
            /// and may thus match more entities than expected.</param>
            /// <param name="component"> The component value to set. </param>
            /// <typeparam name="T"> The type of component to add.</typeparam>
            /// <exception cref="ArgumentException">Throws if the component has a reference to a deferred entity, requiring fixup within the command buffer.</exception>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
            public void AddComponentForLinkedEntityGroup<T>(int sortKey, Entity e, EntityQueryMask mask, T component) where T : unmanaged, IComponentData
            {
                CheckWriteAccess();
                var chain = ThreadChain;

                m_Data->AddLinkedEntityGroupComponentCommand(chain, sortKey, ECBCommand.AddComponentLinkedEntityGroup, mask, e, component);
            }

            /// <summary>Records a command that adds a component to an entity's <see cref="LinkedEntityGroup"/> based on an <see cref="EntityQueryMask"/>.
            /// Entities in the <see cref="LinkedEntityGroup"/> that don't match the mask will be skipped safely.</summary>
            /// <remarks>At playback, this command throws an error if the entity is destroyed before playback,
            /// if the entity is still deferred, or if any of the matching linked entities cannot add the component.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e">The entity whose LinkedEntityGroup will be referenced.</param>
            /// <param name="mask">The EntityQueryMask that is used to determine which linked entities to add the component to.
            /// Note that EntityQueryMask ignores all query filtering (including chunk filtering and enableable components),
            /// and may thus match more entities than expected.</param>
            /// <param name="componentType"> The component type to add. </param>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
            public void AddComponentForLinkedEntityGroup(int sortKey, Entity e, EntityQueryMask mask, ComponentType componentType)
            {
                CheckWriteAccess();
                var chain = ThreadChain;

                m_Data->AddLinkedEntityGroupTypeCommand(chain, sortKey, ECBCommand.AddComponentLinkedEntityGroup, mask, e, componentType);
            }

            /// <summary>Records a command that sets a component for an entity's <see cref="LinkedEntityGroup"/> based on an <see cref="EntityQueryMask"/>.
            /// Entities in the <see cref="LinkedEntityGroup"/> that don't match the mask will be skipped safely.</summary>
            /// <remarks>At playback, this command throws an error if the entity is destroyed before playback,
            /// if the entity is still deferred, if the entity has the <see cref="Prefab"/> tag, or if any of the matching linked entities do not already have the component.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e">The entity whose LinkedEntityGroup will be referenced.</param>
            /// <param name="mask">The EntityQueryMask that is used to determine which linked entities to set the component for.
            /// Note that EntityQueryMask ignores all query filtering (including chunk filtering and enableable components),
            /// and may thus match more entities than expected.</param>
            /// <param name="component"> The component value to set. </param>
            /// <typeparam name="T"> The type of component to add.</typeparam>
            /// <exception cref="ArgumentException">Throws if the component has a reference to a deferred entity, requiring fixup within the command buffer.</exception>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
            public void SetComponentForLinkedEntityGroup<T>(int sortKey, Entity e, EntityQueryMask mask, T component) where T : unmanaged, IComponentData
            {
                CheckWriteAccess();
                var chain = ThreadChain;

                m_Data->AddLinkedEntityGroupComponentCommand(chain, sortKey, ECBCommand.SetComponentLinkedEntityGroup, mask, e, component);
            }

            /// <summary>Records a command that replaces a component value for an entity's <see cref="LinkedEntityGroup"/>.
            /// Entities in the <see cref="LinkedEntityGroup"/> that don't have the component will be skipped safely.</summary>
            /// <remarks>At playback, this command throws an error if the entity is destroyed before playback or
            /// if the entity is still deferred.</remarks>
            /// <param name="sortKey">A unique index for each set of commands added to this EntityCommandBuffer
            /// across all parallel jobs writing commands to this buffer. The <see cref="ChunkIndexInQuery"/> provided by
            /// <see cref="IJobEntity"/> is an appropriate value to use for this parameter. In an <see cref="IJobChunk"/>
            /// pass the 'unfilteredChunkIndex' value from <see cref="IJobChunk.Execute"/>.</param>
            /// <param name="e">The entity whose LinkedEntityGroup will be referenced.</param>
            /// <param name="component"> The component value to set. </param>
            /// <typeparam name="T"> The type of component to add.</typeparam>
            /// <exception cref="ArgumentException">Throws if the component has a reference to a deferred entity, requiring fixup within the command buffer.</exception>
            /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
            /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
            public void ReplaceComponentForLinkedEntityGroup<T>(int sortKey, Entity e, T component) where T : unmanaged, IComponentData
            {
                CheckWriteAccess();
                var chain = ThreadChain;

                m_Data->AddEntityComponentTypeWithValueCommand(chain, sortKey, ECBCommand.ReplaceComponentLinkedEntityGroup, e, component);
            }
        }
    }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
    /// <summary>
    /// Provides additional methods for using managed components with an EntityCommandBuffer.
    /// </summary>
    public static unsafe class EntityCommandBufferManagedComponentExtensions
    {
        /// <summary> Records a command to add and set a managed component for an entity.</summary>
        /// <remarks>At playback, if the entity already has this type of component, the value will just be set.
        /// Will throw an error if this entity is destroyed before playback, if this entity is still deferred,
        /// or adding this componentType makes the archetype too large.</remarks>
        /// <param name="ecb"> This entity command buffer.</param>
        /// <param name="e"> The entity to set the component value on.</param>
        /// <param name="component"> The component value to add. </param>
        /// <typeparam name="T"> The type of component to add.</typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public static void AddComponent<T>(this EntityCommandBuffer ecb, Entity e, T component) where T : class
        {
            ecb.EnforceSingleThreadOwnership();
            ecb.AssertDidNotPlayback();
            AddEntityManagedComponentCommandFromMainThread(ecb.m_Data, ecb.MainThreadSortKey, ECBCommand.AddManagedComponentData, e, component);
        }

        /// <summary> Records a command to add a managed component for an entity.</summary>
        /// <remarks>At playback, this command throws an error if this entity is destroyed before playback,
        /// if this entity is still deferred, or adding this componentType makes the archetype too large.</remarks>
        /// <param name="ecb"> This entity command buffer.</param>
        /// <param name="e"> The entity to set the component value on.</param>
        /// <typeparam name="T"> The type of component to add.</typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public static void AddComponent<T>(this EntityCommandBuffer ecb, Entity e) where T : class
        {
            ecb.EnforceSingleThreadOwnership();
            ecb.AssertDidNotPlayback();
            AddEntityManagedComponentCommandFromMainThread(ecb.m_Data, ecb.MainThreadSortKey, ECBCommand.AddManagedComponentData, e, default(T));
        }

        /// <summary>Records a command to safely move a managed component (and its current value) from one entity to another</summary>
        /// <remarks>At playback, this command throws an error if this entity is destroyed before playback,
        /// if this entity is still deferred, or adding this componentType makes the archetype too large.</remarks>
        /// <param name="ecb">This entity command buffer.</param>
        /// <param name="src">The entity whose <typeparamref name="T"/> component should be moved to <paramref name="dst"/>. This entity must have a component of type <typeparamref name="T"/> at playback time.</param>
        /// <param name="dst">
        /// The Entity the managed component will be added to. If this entity already has
        /// <typeparamref name="T"/> with a different value than <paramref name="src"/>, the existing value will be
        /// removed and disposed before the new value is assigned.
        /// </param>
        /// <typeparam name="T"> The type of component to move. Must be a managed type.</typeparam>
        /// <remarks>
        /// If the source and destination entity are identical, no operation is performed.
        ///
        /// This operation seems similar to
        ///
        /// value = GetComponentData&lt;T&gt;(src);
        /// AddComponentData(dst, value)
        /// RemoveComponent&lt;T&gt;(src)
        ///
        /// But for managed components which implement <see cref="IDisposable"/>, calling RemoveComponent will invoke Dispose() on the component value, leaving the destination entity with an uninitialized object.```
        /// This operation ensures the component is properly moved over.
        /// </remarks>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        /// <exception cref="ArgumentException">Throws if <paramref name="src"/> does not have component <typeparamref name="T"/> at playback time.</exception>
        [SupportedInEntitiesForEach]
        public static void MoveComponent<T>(this EntityCommandBuffer ecb, Entity src, Entity dst) where T :  class, IComponentData, new()
        {
            if (src == dst)
                return;
            ecb.EnforceSingleThreadOwnership();
            ecb.AssertDidNotPlayback();
            AddMoveManagedComponentCommandFromMainThread<T>(ecb.m_Data, ecb.MainThreadSortKey, ECBCommand.MoveManagedComponentData, src, dst);
        }

        /// <summary> Records a command to set a managed component for an entity.</summary>
        /// <remarks> At playback, this command throws an error if this entity is destroyed before playback,
        /// if this entity is still deferred, if the entity has the <see cref="Prefab"/> tag, or if the entity doesn't have the shared component type.</remarks>
        /// <param name="ecb"> This entity command buffer.</param>
        /// <param name="e"> The entity to set the component value on.</param>
        /// <param name="component"> The component value to add. </param>
        /// <typeparam name="T"> The type of component to set.</typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public static void SetComponent<T>(this EntityCommandBuffer ecb, Entity e, T component) where T : class, IComponentData, new()
        {
            ecb.EnforceSingleThreadOwnership();
            ecb.AssertDidNotPlayback();
            AddEntityManagedComponentCommandFromMainThread(ecb.m_Data, ecb.MainThreadSortKey, ECBCommand.SetManagedComponentData, e, component);
        }

        /// <summary>
        /// Records a command to enable or disable a <see cref="ComponentType"/> on the specified <see cref="Entity"/>. This operation
        /// does not cause a structural change, or affect the value of the component. For the purposes
        /// of EntityQuery matching, an entity with a disabled component will behave as if it does not have that component.
        /// </summary>
        /// <remarks> At playback, this command throws an error if this entity is destroyed before playback,
        /// if this entity is still deferred, if the entity has the <see cref="Prefab"/> tag, or if the entity doesn't have the component type.</remarks>
        /// <typeparam name="T">The component type to enable or disable. This type must implement the
        /// <see cref="IEnableableComponent"/> interface.</typeparam>
        /// <param name="ecb"> This entity command buffer.</param>
        /// <param name="e">The entity whose component should be enabled or disabled.</param>
        /// <param name="value">True if the specified component should be enabled, or false if it should be disabled.</param>
        [SupportedInEntitiesForEach]
        public static void SetComponentEnabled<T>(this EntityCommandBuffer ecb, Entity e, bool value) where T : class, IEnableableComponent, new()
        {
            ecb.EnforceSingleThreadOwnership();
            ecb.AssertDidNotPlayback();
            ecb.m_Data->AddEntityComponentEnabledCommand(&ecb.m_Data->m_MainThreadChain, ecb.MainThreadSortKey,
                ECBCommand.SetComponentEnabled, e, TypeManager.GetTypeIndex<T>(), value);
        }

        /// <summary>Obsolete. Use <see cref="AddComponent{T}(Unity.Entities.EntityCommandBuffer,Unity.Entities.EntityQuery,T)"/> instead.</summary>
        /// <param name="ecb"> This entity command buffer.</param>
        /// <param name="query"> The query specifying which entities to add the component value to.</param>
        /// <param name="component"> The component value to add. </param>
        /// <typeparam name="T"> The type of component to add.</typeparam>
        [SupportedInEntitiesForEach]
        [Obsolete("Use AddComponent (RemovedAfter Entities 1.0) (UnityUpgradable) -> AddComponent<T>(*)")]
        public static void AddComponentForEntityQuery<T>(this EntityCommandBuffer ecb, EntityQuery query, T component) where T : class, IComponentData, new()
            => AddComponent<T>(ecb, query, component);

        /// <summary> Records a command to add a managed component and set its value for all entities matching a query.</summary>
        /// <remarks>The set of entities matching the query is 'captured' in the method call, and the recorded command stores an array of all these entities.
        ///
        /// Entities which already have the component type will have the component set to the value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown in playback if one or more of the entities has been destroyed. (With safety checks disabled,
        /// playback will perform invalid and unsafe memory access.).</exception>
        /// <param name="ecb"> This entity command buffer.</param>
        /// <param name="query"> The query specifying which entities to add the component value to.</param>
        /// <param name="component"> The component value to add. </param>
        /// <typeparam name="T"> The type of component to add.</typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public static void AddComponent<T>(this EntityCommandBuffer ecb, EntityQuery query, T component) where T : class, IComponentData, new()
        {
            ecb.AddComponentObject(query, component);
        }

        /// <summary>Obsolete. Use <see cref="SetComponent{T}(Unity.Entities.EntityCommandBuffer,Unity.Entities.EntityQuery,T)"/> instead.</summary>
        /// <param name="ecb"> This entity command buffer.</param>
        /// <param name="query"> The query specifying which entities to set the component value for.</param>
        /// <param name="component"> The component value to set.</param>
        /// <typeparam name="T"> The type of component to set.</typeparam>
        [SupportedInEntitiesForEach]
        [Obsolete("Use SetComponent (RemovedAfter Entities 1.0) (UnityUpgradable) -> SetComponent<T>(*)")]
        public static void SetComponentForEntityQuery<T>(this EntityCommandBuffer ecb, EntityQuery query, T component) where T : class, IComponentData, new()
            => SetComponent<T>(ecb, query, component);

        /// <summary> Records a command to set a managed component value for all entities matching a query.</summary>
        /// <remarks>The set of entities matching the query is 'captured' in the method call, and the recorded command stores an array of all these entities.
        ///
        /// If any entity does not have the component type at playback , playback Entities which already have the component type will have the component set to the value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown in playback if one or more of the entities does not have the component type or has been destroyed. (With safety checks disabled,
        /// playback will perform invalid and unsafe memory access.).</exception>
        /// <param name="ecb"> This entity command buffer.</param>
        /// <param name="query"> The query specifying which entities to set the component value for.</param>
        /// <param name="component"> The component value to set.</param>
        /// <typeparam name="T"> The type of component to set.</typeparam>
        /// <exception cref="NullReferenceException">Throws if an Allocator was not passed in when the EntityCommandBuffer was created.</exception>
        /// <exception cref="InvalidOperationException">Throws if this EntityCommandBuffer has already been played back.</exception>
        [SupportedInEntitiesForEach]
        public static void SetComponent<T>(this EntityCommandBuffer ecb, EntityQuery query, T component) where T : class, IComponentData, new()
        {
            ecb.SetComponentObject(query, component);
        }

        internal static void AddEntityManagedComponentCommandFromMainThread<T>(EntityCommandBufferData* ecbd, int sortKey, ECBCommand op, Entity e, T component) where T : class
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(e == Entity.Null))
                throw new InvalidOperationException("Invalid Entity.Null passed.");
#endif
            var typeIndex = TypeManager.GetTypeIndex<T>();
            var sizeNeeded = EntityCommandBufferData.Align(sizeof(EntityManagedComponentCommand), EntityCommandBufferData.ALIGN_64_BIT);

            var chain = &ecbd->m_MainThreadChain;
            ecbd->ResetCommandBatching(chain);
            var data = (EntityManagedComponentCommand*)ecbd->Reserve(chain, sortKey, sizeNeeded);

            data->Header.Header.CommandType = op;
            data->Header.Header.TotalSize = sizeNeeded;
            data->Header.Header.SortKey = chain->m_LastSortKey;
            data->Header.Entity = e;
            data->Header.IdentityIndex = 0;
            data->Header.BatchCount = 1;
            data->ComponentTypeIndex = typeIndex;

            if (component != null)
            {
                data->GCNode.BoxedObject = GCHandle.Alloc(component);
                // We need to store all GCHandles on a cleanup list so we can dispose them later, regardless of if playback occurs or not.
                data->GCNode.Prev = chain->m_Cleanup->CleanupList;
                chain->m_Cleanup->CleanupList = &(data->GCNode);
            }
            else
            {
                data->GCNode.BoxedObject = new GCHandle();
            }
        }

        internal static void AddMoveManagedComponentCommandFromMainThread<T>(EntityCommandBufferData* ecbd, int sortKey, ECBCommand op, Entity src, Entity dst) where T : class
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(src == Entity.Null || dst == Entity.Null))
                throw new InvalidOperationException("Invalid Entity.Null passed.");
#endif
            var typeIndex = TypeManager.GetTypeIndex<T>();
            var sizeNeeded = EntityCommandBufferData.Align(sizeof(EntityMoveManagedComponentCommand), EntityCommandBufferData.ALIGN_64_BIT);

            var chain = &ecbd->m_MainThreadChain;
            ecbd->ResetCommandBatching(chain);
            var data = (EntityMoveManagedComponentCommand*)ecbd->Reserve(chain, sortKey, sizeNeeded);

            data->Header.Header.CommandType = op;
            data->Header.Header.TotalSize = sizeNeeded;
            data->Header.Header.SortKey = chain->m_LastSortKey;
            data->Header.Entity = dst;
            data->Header.IdentityIndex = 0;
            data->Header.BatchCount = 1;
            data->ComponentTypeIndex = typeIndex;
            data->SrcEntity = src;
        }
    }
#endif
}
