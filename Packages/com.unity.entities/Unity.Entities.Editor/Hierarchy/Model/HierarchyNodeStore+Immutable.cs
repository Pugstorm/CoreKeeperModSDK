using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// The <see cref="HierarchyImmutableNodeData"/> represents a node in the linear baked hierarchy.
    /// </summary>
    struct HierarchyImmutableNodeData
    {
        public HierarchyNodeHandle Handle;
        public int ParentOffset;
        public int NextSiblingOffset;
        public int ChildCount;
        public int Depth;
        public HierarchyNodeFlags Flags;
    }

    partial struct HierarchyNodeStore
    {
        struct HierarchyNodeHandleComparer : IComparer<HierarchyNodeHandle>
        {
            public HierarchyNodeMap<HierarchyNodeData> Nodes;

            public int Compare(HierarchyNodeHandle x, HierarchyNodeHandle y)
            {
                var sortIndexComparison = Nodes[x].SortIndex.CompareTo(Nodes[y].SortIndex);
                return sortIndexComparison != 0 ? sortIndexComparison : x.CompareTo(y);
            }
        }

        /// <summary>
        /// The <see cref="Immutable"/> structure represents a baked out version of the <see cref="HierarchyNodeStore"/> in depth-first order.
        /// </summary>
        [GenerateTestsForBurstCompatibility]
        public unsafe struct Immutable : IDisposable, IEquatable<Immutable>
        {
            // ReSharper disable once MemberHidesStaticFromOuterClass
            struct Data
            {
                public int ChangeVersion;
            }

            readonly Allocator m_Allocator;

            [NativeDisableUnsafePtrRestriction] Data* m_Data;

            /// <summary>
            /// The depth first packed hierarchy data.
            /// </summary>
            [NativeDisableUnsafePtrRestriction] internal UnsafeList<HierarchyImmutableNodeData>* m_HandleNodes;

            /// <summary>
            /// A set of custom root entities. This is to handle a high volume of root entities (something common in dots).
            /// </summary>
            /// <remarks>
            /// This array has a padding at the front since it uses negative indexing.
            /// </remarks>
            [NativeDisableUnsafePtrRestriction] internal UnsafeList<Entity>* m_EntityNodes;

            /// <summary>
            /// The packed index per entity which maps to the packed sets <see cref="m_HandleNodes"/> and <see cref="m_EntityNodes"/>.
            /// </summary>
#if ENTITY_STORE_V1
            [NativeDisableUnsafePtrRestriction] internal UnsafeList<int>* m_IndexByEntity;
#else
            [NativeDisableUnsafePtrRestriction] internal UnsafeHashMap<int, int>* m_IndexByEntity;
#endif

            /// <summary>
            /// The packed index per non-entity handle.
            /// </summary>
            UnsafeParallelHashMap<HierarchyNodeHandle, int> m_IndexByHandle;

            /// <summary>
            /// Gets a value indicating if the packed hierarchy has been initialized or not.
            /// </summary>
            public bool IsCreated
                => null != m_Data;

            /// <summary>
            /// Gets the current change version for the packed hierarchy.
            /// </summary>
            public int ChangeVersion
                => m_Data->ChangeVersion;

            /// <summary>
            /// Returns the number of valid nodes that exist in the hierarchy.
            /// </summary>
            /// <returns>The number of nodes that exist in the hierarchy.</returns>
            public int Count
                => m_HandleNodes->Length + m_EntityNodes->Length;

            /// <summary>
            /// Returns the number of 'hierarchical' nodes. i.e. nodes which have some parent, child relationships.
            /// </summary>
            internal int CountHierarchicalNodes
                => m_HandleNodes->Length;

            internal HierarchyImmutableNodeData this[int index]
            {
                get
                {
                    if (index < m_HandleNodes->Length)
                        return m_HandleNodes->ElementAt(index);

                    // This is a root level entity. We didn't bother to bake out any hierarchical information since there is none.
                    var entity = m_EntityNodes->ElementAt(index - m_HandleNodes->Length);

                    // Dynamically generate a data struct and return it.
                    return new HierarchyImmutableNodeData
                    {
                        ChildCount = 0,
                        Handle = HierarchyNodeHandle.FromEntity(entity),
                        ParentOffset = -index,
                        Depth = 0,
                        NextSiblingOffset = index + 1
                    };
                }
            }

            internal void SetChangeVersion(int changeVersion)
                => m_Data->ChangeVersion = changeVersion;

            public Immutable(Allocator allocator)
            {
                m_Allocator = allocator;
                m_Data = (Data*)Memory.Unmanaged.Allocate(UnsafeUtility.SizeOf<Data>(), UnsafeUtility.AlignOf<Data>(), allocator);
                m_Data->ChangeVersion = 0;
                m_HandleNodes = UnsafeList<HierarchyImmutableNodeData>.Create(16, allocator);
                m_EntityNodes = UnsafeList<Entity>.Create(16, allocator);
#if ENTITY_STORE_V1
                m_IndexByEntity = UnsafeList<int>.Create(16, allocator);
#else
                m_IndexByEntity = AllocatorManager.Allocate<UnsafeHashMap<int, int>>(allocator);
                *m_IndexByEntity = new UnsafeHashMap<int, int>(16, allocator);
#endif
                m_IndexByHandle = new UnsafeParallelHashMap<HierarchyNodeHandle, int>(16, allocator);
                Clear();
            }

            public void Dispose()
            {
                UnsafeList<HierarchyImmutableNodeData>.Destroy(m_HandleNodes);
                UnsafeList<Entity>.Destroy(m_EntityNodes);
#if ENTITY_STORE_V1
                UnsafeList<int>.Destroy(m_IndexByEntity);
#else
                m_IndexByEntity->Dispose();
                AllocatorManager.Free(m_Allocator, m_IndexByEntity);
#endif
                m_IndexByHandle.Dispose();
                Memory.Unmanaged.Free(m_Data, m_Allocator);
                m_Data = null;
            }

            /// <summary>
            /// Clears the data from this packed hierarchy.
            /// </summary>
            public void Clear()
            {
                m_Data->ChangeVersion = 0;
                m_HandleNodes->Clear();
                m_EntityNodes->Clear();
                m_IndexByEntity->Clear();

#if ENTITY_STORE_V1
                // Setup the list to always include a virtual root node.
                m_IndexByEntity->Add(0);
#else
                // Setup the list to always include a virtual root node.
                m_IndexByEntity->Add(0, 0);
#endif

                m_IndexByHandle.Clear();

                m_HandleNodes->Add(new HierarchyImmutableNodeData
                {
                    ChildCount = 0,
                    Depth = -1,
                    Handle = HierarchyNodeHandle.Root,
                    NextSiblingOffset = 1,
                    ParentOffset = 0
                });
            }

            /// <summary>
            /// Returns <see langword="true"/> if the given handle exists in the hierarchy.
            /// </summary>
            /// <param name="handle">The handle to check existence for.</param>
            /// <returns><see langword="true"/> if the given handle exists; <see langword="false"/> otherwise.</returns>
            public bool Exists(HierarchyNodeHandle handle)
            {
                switch (handle.Kind)
                {
                    case NodeKind.Entity:
                    {
                        var index = IndexOf(handle);

                        if (index == -1)
                            return false;

                        if (index < m_HandleNodes->Length)
                            return m_HandleNodes->ElementAt(index).Handle.Version == handle.Version;

                        index -= m_HandleNodes->Length;
                        return m_EntityNodes->ElementAt(index).Version == handle.Version;
                    }

                    default:
                        return m_IndexByHandle.ContainsKey(handle);
                }
            }

            /// <summary>
            /// Gets the root node for the <see cref="HierarchyNodeStore"/>.
            /// </summary>
            public HierarchyNode.Immutable GetRoot()
                => new HierarchyNode.Immutable(this, 0, ChangeVersion);

            /// <summary>
            /// Gets the <see cref="HierarchyNode"/> for the given handle.
            /// </summary>
            public HierarchyNode.Immutable GetNode(HierarchyNodeHandle handle)
                => new HierarchyNode.Immutable(this, IndexOf(handle), ChangeVersion);

            /// <summary>
            /// Gets the <see cref="HierarchyNode"/> for the given handle.
            /// </summary>
            public HierarchyNode.Immutable GetNode(int index)
                => new HierarchyNode.Immutable(this, index, ChangeVersion);

            /// <summary>
            /// Returns the packed index for the given <see cref="HierarchyNodeHandle"/>.
            /// </summary>
            /// <param name="handle">The handle to get the index for.</param>
            /// <returns>The packed index for the given handle.</returns>
            public int IndexOf(HierarchyNodeHandle handle)
            {
                switch (handle.Kind)
                {
                    case NodeKind.Entity:
                    {
#if ENTITY_STORE_V1
                        if (handle.Index < 0 || handle.Index >= m_IndexByEntity->Length)
                            return -1;
                        return m_IndexByEntity->ElementAt(handle.Index);
#else
                        if (!m_IndexByEntity->TryGetValue(handle.Index, out var value))
                            value = -1;
                        return value;
#endif
                    }

                    default:
                    {
                        if (!m_IndexByHandle.TryGetValue(handle, out var index))
                            index = -1;

                        return index;
                    }
                }
            }

            internal void SetPackedIndex(HierarchyNodeHandle handle, int index)
            {
                switch (handle.Kind)
                {
                    case NodeKind.Entity:
#if ENTITY_STORE_V1
                        m_IndexByEntity->ElementAt(handle.Index) = index;
#else
                        (*m_IndexByEntity)[handle.Index] = index;
#endif
                        break;
                    default:
                        m_IndexByHandle[handle] = index;
                        break;
                }
            }

            internal int GetPackedIndex(HierarchyNodeHandle handle)
            {
                switch (handle.Kind)
                {
                    case NodeKind.Entity:
#if ENTITY_STORE_V1
                        return m_IndexByEntity->ElementAt(handle.Index);
#else
                        return (*m_IndexByEntity)[handle.Index];
#endif
                    default:
                        if (!m_IndexByHandle.TryGetValue(handle, out var index))
                            index = -1;
                        return index;
                }
            }

            public bool Equals(Immutable other)
                => m_Data == other.m_Data;

            public override bool Equals(object obj)
                => obj is Immutable other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (int) m_Allocator;
                    hashCode = (hashCode * 397) ^ unchecked((int) (long) m_Data);
                    hashCode = (hashCode * 397) ^ m_HandleNodes->GetHashCode();
                    hashCode = (hashCode * 397) ^ m_IndexByEntity->GetHashCode();
                    hashCode = (hashCode * 397) ^ m_IndexByHandle.GetHashCode();
                    return hashCode;
                }
            }
        }

        /// <summary>
        /// The state object used to execute the 'ExportImmutable' method over several ticks.
        /// </summary>
        [GenerateTestsForBurstCompatibility]
        public unsafe struct ExportImmutableState : IDisposable
        {
            public struct ChildrenStackData
            {
                public int ParentIndex;
                public int ChildrenBufferStartIndex;
                public int ChildrenBufferEndIndex;
            }

            struct ExportImmutableStateData
            {
                public int PackingIndex;
                public int Depth;
                public int Version;
            }

            readonly Allocator m_Allocator;

            [NativeDisableUnsafePtrRestriction] ExportImmutableStateData* m_ExportImmutableStateData;

            public NativeList<HierarchyNodeHandle> ChildrenBuffer;
            public NativeList<ChildrenStackData> ChildrenStack;

            public bool IsCreated => ChildrenBuffer.IsCreated;

            public int PackingIndex
            {
                get => m_ExportImmutableStateData->PackingIndex;
                set => m_ExportImmutableStateData->PackingIndex = value;
            }

            public int Depth
            {
                get => m_ExportImmutableStateData->Depth;
                set => m_ExportImmutableStateData->Depth = value;
            }

            internal int Version
            {
                get => m_ExportImmutableStateData->Version;
                set => m_ExportImmutableStateData->Version = value;
            }

            public ExportImmutableState(Allocator allocator)
            {
                m_Allocator = allocator;
                m_ExportImmutableStateData = (ExportImmutableStateData*) UnsafeUtility.Malloc(UnsafeUtility.SizeOf<ExportImmutableStateData>(), UnsafeUtility.AlignOf<ExportImmutableStateData>(), allocator);
                m_ExportImmutableStateData->PackingIndex = 0;
                m_ExportImmutableStateData->Depth = -1;
                ChildrenStack = new NativeList<ChildrenStackData>(allocator);
                ChildrenBuffer = new NativeList<HierarchyNodeHandle>(allocator);
            }

            public void Dispose()
            {
                UnsafeUtility.Free(m_ExportImmutableStateData, m_Allocator);
                m_ExportImmutableStateData = null;
                ChildrenStack.Dispose();
                ChildrenBuffer.Dispose();
            }

            internal void Clear()
            {
                ChildrenStack.Clear();
                ChildrenBuffer.Clear();
                PackingIndex = 0;
                Depth = -1;
            }
        }

        public struct ExportImmutableEnumerator : IEnumerator
        {
            enum Step
            {
                HierarchyNodes,
                EntityNodes
            }

            readonly HierarchyNodeStore m_Hierarchy;
            readonly World m_World;
            readonly ExportImmutableState m_State;

            readonly Immutable m_Write;
            readonly Immutable m_Read;

            Step m_Step;

            readonly int m_StateVersion;
            readonly int m_TotalCount;
            readonly int m_BatchCount;

            /// <summary>
            /// Returns the enumerator progress. This is an estimate and should not be relied upon for any logic.
            /// </summary>
            public float Progress => m_TotalCount > 0 ? m_State.PackingIndex / (float) m_TotalCount : 0;

            public object Current => null;

            public void Reset() => throw new InvalidOperationException($"{nameof(ExportImmutableEnumerator)} can not be reset. Instead a new instance should be created.");

            public ExportImmutableEnumerator(HierarchyNodeStore hierarchy, World world, ExportImmutableState state, Immutable write, Immutable read, int batchSize)
            {
                if (write.Equals(read))
                    throw new InvalidOperationException("Can not read and write from the same immutable buffer.");

                if (!state.IsCreated)
                    throw new ArgumentException("The given state object is not allocated.");

                m_Hierarchy = hierarchy;
                m_World = world;
                m_State = state;
                m_Write = write;
                m_Read = read;
                m_Step = Step.HierarchyNodes;
                m_TotalCount = m_Hierarchy.Count();
                m_BatchCount = batchSize;

                m_State.Clear();
                m_StateVersion = ++m_State.Version;
                write.SetChangeVersion(m_Hierarchy.ChangeVersion);
                m_Hierarchy.IncrementChangeVersion();
            }

            public bool MoveNext()
            {
                if (!m_State.IsCreated)
                    throw new InvalidOperationException("The state object has been disposed during enumeration.");

                if (m_StateVersion != m_State.Version)
                    throw new InvalidOperationException("The state object has been modified by another enumerator.");

                switch (m_Step)
                {
                    case Step.HierarchyNodes:
                    {
                        unsafe
                        {
                            int exceptionThrown = 0;
                            var job = new ExportImmutableHierarchyNodesBatchJob
                            {
                                ExceptionThrown = &exceptionThrown,
#if ENTITY_STORE_V1
                                EntityCapacity = m_World != null ? m_World.EntityManager.EntityCapacity : 0,
#endif
                                Nodes = m_Hierarchy.m_Nodes,
                                Children = m_Hierarchy.m_Children,
                                ReadChangeVersion = m_Read.IsCreated ? m_Read.ChangeVersion : -1,
                                ReadNodes = m_Read,
                                WriteNodes = m_Write,
                                State = m_State,
                                BatchSize = m_BatchCount
                            };
                            job.Run();

                            if (exceptionThrown != 0)
                            {
                                throw new InvalidOperationException("Exception thrown during enumeration.");
                            }

                            // Keep going until the stack is empty.
                            if (m_State.ChildrenStack.Length <= 0)
                                m_Step = Step.EntityNodes;

                            return true;
                        }
                    }

                    case Step.EntityNodes:
                    {
                        // @TODO time-slice
                        new ExportImmutableEntitiesNodesBatchJob
                        {
                            Nodes = m_Hierarchy.m_Nodes,
                            State = m_State,
                            WriteNodes = m_Write,
                        }.Run();

                        return false;
                    }

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public void ExportImmutable(World world, Immutable dstBuffer)
        {
            using var srcBuffer = new Immutable(Allocator.TempJob);
            using var state = new ExportImmutableState(Allocator.TempJob);

            var enumerator = CreateBuildImmutableEnumerator(world, state, dstBuffer, srcBuffer, 0);
            while (enumerator.MoveNext())
            {
            }
        }

        public void ExportImmutable(World world, Immutable dstBuffer, Immutable srcBuffer)
        {
            using var state = new ExportImmutableState(Allocator.TempJob);

            var enumerator = CreateBuildImmutableEnumerator(world, state, dstBuffer, srcBuffer, 0);
            while (enumerator.MoveNext())
            {
            }
        }

        /// <summary>
        /// Creates an enumerator which will write out the immutable hierarchy over several ticks.
        /// </summary>
        /// <param name="world">The world holding the data.</param>
        /// <param name="state">The re-usable state object use to maintain state between ticks.</param>
        /// <param name="dstBuffer">The buffer to write to.</param>
        /// <param name="srcBuffer">The buffer to read from; this accelerates performance by allowing re-use of previously baked data.</param>
        /// <param name="batchSize">The amount of nodes to process per tick.</param>
        /// <returns>An enumerator which can be ticked.</returns>
        public ExportImmutableEnumerator CreateBuildImmutableEnumerator(World world, ExportImmutableState state, Immutable dstBuffer, Immutable srcBuffer, int batchSize)
        {
            return new ExportImmutableEnumerator(this, world, state, dstBuffer, srcBuffer, batchSize);
        }

        /// <summary>
        /// This job is responsible for baking out a linear set of nodes with offsets.
        /// </summary>
        [BurstCompile]
        unsafe struct ExportImmutableHierarchyNodesBatchJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public int* ExceptionThrown;

#if ENTITY_STORE_V1
            public int EntityCapacity;
#endif

            [ReadOnly] public HierarchyNodeMap<HierarchyNodeData> Nodes;
            [ReadOnly] public UnsafeParallelMultiHashMap<HierarchyNodeHandle, HierarchyNodeHandle> Children;
            [ReadOnly] public int ReadChangeVersion;

            public ExportImmutableState State;
            public int BatchSize;

            public Immutable ReadNodes;
            public Immutable WriteNodes;

            int m_PackingIndex;

            bool HasFlag(HierarchyNodeHandle handle, HierarchyNodeFlags flag)
                => (Nodes[handle].Flags & flag) != 0;

            /// <summary>
            /// Copy state values to local members to avoid pointer lookups in hot paths.
            /// </summary>
            void BeginBatch()
            {
                m_PackingIndex = State.PackingIndex;
            }

            /// <summary>
            /// Copy state values back to the shared state.
            /// </summary>
            void EndBatch()
            {
                State.PackingIndex = m_PackingIndex;
            }

            public void Execute()
            {
                // Swallowing the exception could get us stuck in an infinite loop.
                // So this mechanism is used to break from underlying iteration on main thread.
                // The idea is to reset this to zero on every normal (non-exception) exit point of Execute.
                *ExceptionThrown = 1;

                BeginBatch();

                var batchIndex = 0;

                if (m_PackingIndex == 0)
                {
                    // The total number of nodes which require hierarchical information.
                    var handleCount = Nodes.ValueByHandleCount + Nodes.ValueByEntity.CountNonSharedDefault;

                    WriteNodes.m_HandleNodes->Resize(handleCount, NativeArrayOptions.ClearMemory);
                    WriteNodes.m_HandleNodes->Length = handleCount;

#if ENTITY_STORE_V1
                    // Allocate a sparse lookup from 'entity' to the baked out index.
                    WriteNodes.m_IndexByEntity->Resize(EntityCapacity, NativeArrayOptions.ClearMemory);
#endif

                    // Start at the root and depth first traverse.
                    PushNode(HierarchyNodeHandle.Root, -1);
                }

                for (;; batchIndex++)
                {
                    if (State.ChildrenStack.Length == 0)
                    {
                        // Stack is empty. We are done processing all nodes.
                        WriteNodes.m_HandleNodes->Length = m_PackingIndex;
                        EndBatch();
                        break;
                    }

                    // The batch size does not correspond to any specific amount of work.
                    // Instead it represents a number of iterations.
                    if (BatchSize > 0 && batchIndex >= BatchSize)
                    {
                        EndBatch();
                        *ExceptionThrown = 0;
                        return;
                    }

                    // Read the top node of the stack.
                    ref var children = ref State.ChildrenStack.ElementAt(State.ChildrenStack.Length - 1);

                    // If this node still has children to process push them on to the stack.
                    if (children.ChildrenBufferStartIndex < children.ChildrenBufferEndIndex)
                    {
                        PushNode(State.ChildrenBuffer[children.ChildrenBufferStartIndex++], parentIndex: children.ParentIndex);
                    }
                    else
                    {
                        ref var node = ref WriteNodes.m_HandleNodes->ElementAt(children.ParentIndex);

                        // Fixup the next sibling offsets after writing all children.
                        node.NextSiblingOffset = m_PackingIndex - children.ParentIndex;

                        // Pop this node from the stack and fixup depth counter.
                        State.Depth--;
                        State.ChildrenStack.Length -= 1;
                        State.ChildrenBuffer.Length -= node.ChildCount;
                    }
                }

                *ExceptionThrown = 0;
            }

            void PushNode(HierarchyNodeHandle handle, int parentIndex)
            {
#if ENTITY_STORE_V1
                // Broad phase check to see if the node has changed since the last pack.
                // This optimization lets us re-use the information from a previous depth first traversal by referring to the last packed buffer (see 'ReadNodes')

                if (handle.Kind != NodeKind.Root && Nodes[handle].ChangeVersion <= ReadChangeVersion)
                {
                    // The read buffer contains the data we are interested, we can perform a copy and remap.
                    var readNodeIndex = ReadNodes.GetPackedIndex(handle);
                    var readNode = ReadNodes.m_HandleNodes->ElementAt(readNodeIndex);
                    var nextSiblingOffset = readNode.NextSiblingOffset;

                    // Delta between the current depth and the depth of mem-copied read nodes
                    var diffDepth = State.Depth - readNode.Depth;

                    // The raw data for the nodes remains unchanged and we can safely copy it.
                    var dst = WriteNodes.m_HandleNodes->Ptr + m_PackingIndex;
                    var src = ReadNodes.m_HandleNodes->Ptr + readNodeIndex;
                    var len = UnsafeUtility.SizeOf<HierarchyImmutableNodeData>() * nextSiblingOffset;

                    if (m_PackingIndex == readNodeIndex && UnsafeUtility.MemCmp(dst, src, len) == 0)
                    {
                        // This is a very specialized case. We have determined that the data has NOT changed AND the data already exists in the destination buffer.
                        // This can happen since we are copying back and forth between two buffers.
                        // @NOTE We really shouldn't have to mem compare here and should be able to tell just from the change version and pack index.
                        //       In practice this results in corrupted data. But we still make some nice gains if we can skip the packed index update.
                        //
                        dst->ParentOffset = parentIndex - m_PackingIndex;
                        m_PackingIndex += nextSiblingOffset;
                        return;
                    }

                    UnsafeUtility.MemCpy(dst, src, len);

                    // The mapping must be updated.
                    for (int readIndex = readNodeIndex, writeIndex = m_PackingIndex, end = readNodeIndex + nextSiblingOffset; readIndex < end; readIndex++, writeIndex++)
                    {
                        WriteNodes.SetPackedIndex(ReadNodes[readIndex].Handle, writeIndex);
                    }

                    if (diffDepth != 0)
                    {
                        // Depth must be updated.
                        for (int readIndex = readNodeIndex, writeIndex = m_PackingIndex, end = readNodeIndex + nextSiblingOffset; readIndex < end; readIndex++, writeIndex++)
                        {
                            WriteNodes.m_HandleNodes->ElementAt(writeIndex).Depth += diffDepth;
                        }
                    }

                    // The top level node can be moved around. The parent index must be patched.
                    dst->ParentOffset = parentIndex - m_PackingIndex;
                    m_PackingIndex += nextSiblingOffset;
                    return;
                }
#endif

                var childrenBufferStartIndex = State.ChildrenBuffer.Length;
                var childCount = 0;

                if (Children.TryGetFirstValue(handle, out var child, out var iterator))
                {
                    do
                    {
                        childCount++;
                        State.ChildrenBuffer.Add(child);
                    } while (Children.TryGetNextValue(out child, ref iterator));
                }

                if (handle.Kind == NodeKind.Root)
                {
                    var enumerator = Nodes.ValueByEntity.GetNonDefaultEntityEnumerator();

                    while (enumerator.MoveNext())
                    {
                        if (enumerator.Current.Value.Parent != HierarchyNodeHandle.Root) continue;
                        State.ChildrenBuffer.Add(HierarchyNodeHandle.FromEntity(enumerator.Current.Entity));
                        childCount++;
                    }
                }

                if (childCount > 0 && HasFlag(handle, HierarchyNodeFlags.ChildrenRequireSorting))
                {
                    var children = State.ChildrenBuffer.AsArray().GetSubArray(childrenBufferStartIndex, childCount);

                    children.Sort(new HierarchyNodeHandleComparer
                    {
                        Nodes = Nodes
                    });
                }

                var index = m_PackingIndex++;

                WriteNodes.SetPackedIndex(handle, index);
                WriteNodes.m_HandleNodes->ElementAt(index) = new HierarchyImmutableNodeData
                {
                    Handle = handle,
                    ParentOffset = parentIndex - index,
                    NextSiblingOffset = 1, // default the next sibling to be the next node. If we have children this value is patched when popping off the stack.
                    ChildCount = childCount,
                    Depth = State.Depth,
                    Flags = Nodes[handle].Flags
                };

                if (childCount <= 0)
                    return;

                State.Depth++;
                State.ChildrenStack.Add(new ExportImmutableState.ChildrenStackData
                {
                    ParentIndex = index,
                    ChildrenBufferStartIndex = childrenBufferStartIndex,
                    ChildrenBufferEndIndex = childrenBufferStartIndex + childCount
                });
            }
        }

        [BurstCompile]
        unsafe struct ExportImmutableEntitiesNodesBatchJob : IJob
        {
            [ReadOnly] public HierarchyNodeMap<HierarchyNodeData> Nodes;

            public ExportImmutableState State;

            public Immutable WriteNodes;

            public void Execute()
            {
                // The total number of 'root' entities. These do not require hierarchical information and will be stored in a specialized storage.
                var entityCount = Nodes.ValueByEntity.Count - Nodes.ValueByEntity.CountNonSharedDefault;

                WriteNodes.m_EntityNodes->Resize(entityCount);
                WriteNodes.m_EntityNodes->Length = entityCount;

                if (entityCount == 0)
                    return;

                var packingIndex = State.PackingIndex;

                Nodes.ValueByEntity.GetDefaultEntities(WriteNodes.m_EntityNodes->Ptr);

                for (var i = 0; i < entityCount; i++)
                {
                    var entity = WriteNodes.m_EntityNodes->ElementAt(i);
#if ENTITY_STORE_V1
                    WriteNodes.m_IndexByEntity->ElementAt(entity.Index) = packingIndex++;
#else
                    (*WriteNodes.m_IndexByEntity)[entity.Index] = packingIndex++;
#endif
                }

                State.PackingIndex = packingIndex;

                // Patch up the root to include these elements as children.
                var root = WriteNodes.m_HandleNodes->ElementAt(0);
                root.NextSiblingOffset = Nodes.Count();
                root.ChildCount += entityCount;
            }
        }
    }
}
