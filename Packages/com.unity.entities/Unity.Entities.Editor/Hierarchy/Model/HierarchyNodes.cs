using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Properties;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Unity.Entities.Editor
{
    [GeneratePropertyBag]
    class HierarchyNodesState
    {
        [CreateProperty] internal HashSet<HierarchyNodeHandle> Expanded = new HashSet<HierarchyNodeHandle>();
    }

    /// <summary>
    /// The <see cref="HierarchyNodes"/> class represents a virtualized list over the linearly packed hierarchy. It provides linear access to all 'expanded/visible' nodes.
    /// This can be used directly by a 'ListView' for the virtualization.
    /// </summary>
    class HierarchyNodes : IList, IDisposable
    {
        /// <summary>
        /// The set of packed node data this list is exposing. This contains 'ALL' nodes in the hierarchy packed for efficient access.
        /// </summary>
        HierarchyNodeStore.Immutable m_ImmutableNodes;

        /// <summary>
        /// The set of 'visible' nodes based on the expanded state. These indices map back to the <see cref="m_ImmutableNodes"/>.
        /// </summary>
        NativeList<int> m_Nodes;

        /// <summary>
        /// The <see cref="m_Nodes"/> index for a given <see cref="HierarchyNodeHandle"/>. This is used to find the virtualized index for a specific node.
        /// </summary>
        NativeList<int> m_IndexByNode;

        /// <summary>
        /// The set of expanded states for the nodes.
        /// </summary>
        NativeParallelHashSet<HierarchyNodeHandle> m_ExpandedHashSet;

        /// <summary>
        /// The currently applied search query.
        /// </summary>
        HierarchyFilter m_Filter;

        /// <summary>
        /// The serialized state which is managed externally.
        /// </summary>
        HierarchyNodesState m_SerializableState = new HierarchyNodesState();

        int m_ImmutableNodeChangeVersion;
        int m_ChangeVersion;

        bool m_Rebuild;
        DataMode m_CurrentMode;

        /// <summary>
        /// Returns the number of 'visible' nodes. This is the virtualized count.
        /// </summary>
        public int Count => m_Nodes.Length;

        /// <summary>
        /// Returns the current change version for the nodes.
        /// </summary>
        public int ChangeVersion => m_ChangeVersion;

        /// <summary>
        /// Returns true if the nodes have been changed since the given version.
        /// </summary>
        public bool IsChanged(int changeVersion) => m_ChangeVersion != changeVersion;

        /// <summary>
        /// Returns true if the nodes are filtered by a search query.
        /// </summary>
        public bool HasFilter() => null != m_Filter;

        /// <summary>
        /// Returns the active search filter; if any.
        /// </summary>
        public HierarchyFilter GetFilter() => m_Filter;

        object IList.this[int index]
        {
            get => GetBuffer().GetNode(m_Nodes[index]);
            set => throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the immutable node data for the given <see cref="HierarchyNodeHandle"/>.
        /// </summary>
        /// <param name="handle">The handle to get the data for.</param>
        public HierarchyNode.Immutable this[HierarchyNodeHandle handle]
            => GetBuffer().GetNode(handle);

        /// <summary>
        /// Returns the immutable node data for the given index.
        /// </summary>
        /// <param name="index">The index to get the data for.</param>
        public HierarchyNode.Immutable this[int index]
            => GetBuffer().GetNode(m_Nodes[index]);

        /// <summary>
        /// Gets the immutable node buffer storing the node data.
        /// </summary>
        HierarchyNodeStore.Immutable GetBuffer()
        {
            if (!m_ImmutableNodes.IsCreated)
                throw new InvalidOperationException($"The packed node buffer has not been initialized. {nameof(HierarchyNodes)}.{nameof(Refresh)} must be called to update the internal state.");

            if (m_ImmutableNodes.ChangeVersion != m_ImmutableNodeChangeVersion)
                throw new InvalidOperationException($"The packed node buffer has been changed. {nameof(HierarchyNodes)}.{nameof(Refresh)} must be called to update the internal state.");

            return m_ImmutableNodes;
        }

        internal HierarchyNodes(Allocator allocator)
        {
            m_ChangeVersion = 0;
            m_ImmutableNodeChangeVersion = 0;
            m_Nodes = new NativeList<int>(allocator);
            m_IndexByNode = new NativeList<int>(allocator);
            m_ExpandedHashSet = new NativeParallelHashSet<HierarchyNodeHandle>(16, allocator);
        }

        public void Dispose()
        {
            m_Nodes.Dispose();
            m_IndexByNode.Dispose();
            m_ExpandedHashSet.Dispose();
        }

        /// <summary>
        /// Clears the internal state for re-use.
        /// </summary>
        public void Clear()
        {
            m_ImmutableNodes = default;
            m_ChangeVersion = 0;
            m_ImmutableNodeChangeVersion = 0;
            m_Nodes.Clear();
            m_IndexByNode.Clear();
            m_ExpandedHashSet.Clear();
        }

        public HierarchyNodesState GetSerializableState()
        {
            return m_SerializableState;
        }

        public void SetSerializableState(HierarchyNodesState state)
        {
            m_SerializableState = state ?? throw new ArgumentNullException(nameof(state));
            m_ExpandedHashSet.Clear();

            // Copy this data to a native acceleration structure to be available in bursted jobs.
            foreach (var node in state.Expanded)
                m_ExpandedHashSet.Add(node);

            m_Rebuild = true;
        }

        public bool Exists(HierarchyNodeHandle handle)
            => m_ImmutableNodes.Exists(handle);

        /// <summary>
        /// Returns <see langword="true"/> if the given <see cref="HierarchyNodeHandle"/> is expanded in the hierarchy.
        /// </summary>
        /// <param name="handle">The handle to get the expanded state for.</param>
        /// <returns><see langword="true"/> if the node is expanded; <see langword="false"/> otherwise.</returns>
        public bool IsExpanded(HierarchyNodeHandle handle)
        {
            switch (handle.Kind)
            {
                case NodeKind.Scene:
                case NodeKind.SubScene:
                    return !m_ExpandedHashSet.Contains(handle);
                default:
                    return m_ExpandedHashSet.Contains(handle);
            }
        }

        public void SetExpanded(HierarchyNodeHandle handle, bool expanded)
        {
            switch (handle.Kind)
            {
                case NodeKind.Scene:
                case NodeKind.SubScene:
                {
                    if (!expanded)
                    {
                        m_ExpandedHashSet.Add(handle);
                        m_SerializableState.Expanded.Add(handle);
                    }
                    else
                    {
                        m_ExpandedHashSet.Remove(handle);
                        m_SerializableState.Expanded.Remove(handle);
                    }

                    break;
                }
                default:
                {
                    if (expanded)
                    {
                        m_ExpandedHashSet.Add(handle);
                        m_SerializableState.Expanded.Add(handle);
                    }
                    else
                    {
                        m_ExpandedHashSet.Remove(handle);
                        m_SerializableState.Expanded.Remove(handle);
                    }

                    break;
                }
            }

            // Update both the serialized state and the acceleration structure.
            m_Rebuild = true;
        }

        /// <summary>
        /// Sets all ancestors to an expanded state. This is used for selecting a collapsed node deep in the hierarchy.
        /// </summary>
        /// <param name="handle">The node for which all ancestors will be expanded.</param>
        public void SetAncestorsExpanded(HierarchyNodeHandle handle)
        {
            if (handle.Kind == NodeKind.Root || !m_ImmutableNodes.IsCreated || m_ImmutableNodes.ChangeVersion != m_ImmutableNodeChangeVersion)
                return;

            var index = m_ImmutableNodes.IndexOf(handle);

            if (index == -1)
                return;

            var node = m_ImmutableNodes[index];

            var rebuild = false;

            for (;;)
            {
                index += node.ParentOffset;

                if (index <= 1)
                    break;

                node = m_ImmutableNodes[index];
                rebuild |= !IsExpanded(node.Handle);
                SetExpanded(node.Handle, true);
            }

            // Update both the serialized state and the acceleration structure.
            if (rebuild)
            {
                m_Rebuild = true;
            }
        }

        /// <summary>
        /// Returns the packed index for the given <see cref="HierarchyNodeHandle"/>.
        /// </summary>
        /// <param name="handle">The handle to get the index for.</param>
        /// <returns>The packed index.</returns>
        public int IndexOf(HierarchyNodeHandle handle)
        {
            if (handle.Kind == NodeKind.Root || !m_ImmutableNodes.IsCreated || m_ImmutableNodes.ChangeVersion != m_ImmutableNodeChangeVersion)
                return -1;

            var immutableIndex = m_ImmutableNodes.IndexOf(handle);

            if (immutableIndex == -1 || immutableIndex >= m_ImmutableNodes.Count)
                return -1;

            return m_IndexByNode[immutableIndex];
        }

        internal void SetFilter(HierarchyFilter query)
        {
            if (m_Filter == query)
                return;

            m_Filter = query;
            m_Rebuild = true;
        }

        internal void SetDataMode(DataMode mode)
        {
            m_CurrentMode = mode;
            m_Rebuild = true;
        }

        /// <summary>
        /// Updates the <see cref="HierarchyNodes"/> to the latest packed data. This method will early out if no work is needed and can be called
        /// </summary>
        /// <remarks>
        /// @TODO convert this to an enumerator which can be time-sliced.
        /// </remarks>
        internal unsafe void Refresh(HierarchyNodeStore.Immutable immutable, World world, NativeParallelHashMap<HierarchyNodeHandle, bool> subSceneStateMap)
        {
            if (immutable.ChangeVersion == 0)
                return;

            if (null != m_Filter)
            {
                // Filtering is applied. We should be showing all nodes which match the specified filter. This ignores the expanded states.
                //
                // We must rebuild if:
                //  1) The packed nodes are newer than our current node version.
                //  2) Our underlying node store has be repacked.
                //  3) The search filter has changed.
                //  4) The data mode changed.
                if (m_ImmutableNodeChangeVersion != immutable.ChangeVersion || m_Rebuild)
                {
                    m_ImmutableNodes = immutable;
                    m_ImmutableNodeChangeVersion = immutable.ChangeVersion;
                    m_Rebuild = false;
                    m_ChangeVersion++;

                    using var mask = m_Filter.Apply(m_ImmutableNodes, Allocator.TempJob);

                    new BuildFilteredNodes
                    {
                        Hierarchy = m_ImmutableNodes,
                        Filter = mask,
                        Nodes = m_Nodes,
                        IndexByNode = m_IndexByNode
                    }.Run();
                }
            }
            else
            {
                // No filtering is applied. We should be viewing all 'expanded' nodes.
                //
                // We must rebuild if:
                //  1) The packed nodes are newer than our current node version.
                //  2) Our underlying node store has be repacked.
                //  3) The expanded/collapsed state has changed for one or more nodes.
                //  4) The data mode changed.
                if (m_ImmutableNodeChangeVersion != immutable.ChangeVersion || m_Rebuild)
                {
                    m_ImmutableNodes = immutable;
                    m_ImmutableNodeChangeVersion = immutable.ChangeVersion;
                    m_Rebuild = false;
                    m_ChangeVersion++;

                    new BuildExpandedNodes
                    {
                        Hierarchy = m_ImmutableNodes,
                        Expanded = m_ExpandedHashSet,
                        SubSceneStateMap = subSceneStateMap,
                        Nodes = m_Nodes,
                        IndexByNode = m_IndexByNode,
                        DataMode = m_CurrentMode,
                        IsPlayMode = EditorApplication.isPlaying,
                        IsPrefabStage = PrefabStageUtility.GetCurrentPrefabStage() != null,
                        EntityGuid = typeof(EntityGuid),
                        Prefab = typeof(Prefab),
                        DataAccess = world != null ? world.EntityManager.GetCheckedEntityDataAccess() : null
                    }.Run();
                }
            }
        }

        [BurstCompile]
        internal unsafe struct BuildExpandedNodes : IJob
        {
            [ReadOnly] public HierarchyNodeStore.Immutable Hierarchy;
            [ReadOnly] public NativeParallelHashSet<HierarchyNodeHandle> Expanded;
            [ReadOnly] public NativeParallelHashMap<HierarchyNodeHandle,bool> SubSceneStateMap;

            public DataMode DataMode;
            public bool IsPlayMode;
            public bool IsPrefabStage;

            public NativeList<int> Nodes;
            public NativeList<int> IndexByNode;
            public ComponentType EntityGuid;
            public ComponentType Prefab;
            [NativeDisableUnsafePtrRestriction] public EntityDataAccess* DataAccess;

            public void Execute()
            {
                var inSubScene = false;
                SubSceneInfo currentSubScene = default;

                UnsafeParallelHashMap<int, bool> rootGameObjectsInSubScene = DataMode is DataMode.Mixed ? new UnsafeParallelHashMap<int, bool>(32, AllocatorManager.Temp) : default;

                // Skip the root.
                var count = Hierarchy.Count;
                var readIndex = 1;
                var writeIndex = 0;

                Nodes.ResizeUninitialized(count);
                IndexByNode.ResizeUninitialized(count);

                for (var i = 0; i < IndexByNode.Length; i++)
                    IndexByNode[i] = -1;

                for (; readIndex < count;)
                {
                    var node = Hierarchy[readIndex];

                    if (inSubScene)
                    {
                        if (readIndex > currentSubScene.SubSceneEndIndex)
                            inSubScene = false;
                    }

                    if (node.Handle.Kind is NodeKind.SubScene)
                    {
                        inSubScene = true;

                        SubSceneStateMap.TryGetValue(node.Handle, out var isLoaded);
                        currentSubScene = new SubSceneInfo { SubSceneEndIndex = readIndex + node.NextSiblingOffset - 1, IsOpened = isLoaded, SubSceneNodeDepth = node.Depth };
                        if (DataMode is DataMode.Mixed && currentSubScene.IsOpened)
                            CreateSubSceneGameObjectCache(readIndex, currentSubScene, ref rootGameObjectsInSubScene);
                    }

                    if (!ShouldIncludeNode(node, inSubScene, currentSubScene, rootGameObjectsInSubScene))
                    {
                        readIndex += node.NextSiblingOffset;
                        continue;
                    }

                    // Write the node index.
                    Nodes[writeIndex] = readIndex;
                    IndexByNode[readIndex] = writeIndex;

                    // Skip children if the node is not expanded; otherwise continue to the next node
                    if (node.ChildCount != 0 && !IsExpanded(node.Handle))
                        readIndex += node.NextSiblingOffset;
                    else
                        readIndex++;

                    writeIndex++;
                }

                Nodes.Length = writeIndex;
                if (rootGameObjectsInSubScene.IsCreated) rootGameObjectsInSubScene.Dispose();
            }

            void CreateSubSceneGameObjectCache(int currentReadIndex, SubSceneInfo subSceneInfo, ref UnsafeParallelHashMap<int,bool> rootGameObjectsInSubScene)
            {
                rootGameObjectsInSubScene.Clear();
                for (var readIndex = currentReadIndex+1; readIndex <= subSceneInfo.SubSceneEndIndex; )
                {
                    var node = Hierarchy[readIndex];
                    if (node.Handle.Kind is NodeKind.GameObject)
                    {
                        rootGameObjectsInSubScene.TryAdd(node.Handle.Index, false);
                        readIndex++;
                        continue;
                    }

                    readIndex += node.NextSiblingOffset;
                }
            }

            struct SubSceneInfo
            {
                public int SubSceneEndIndex;
                public bool IsOpened;
                public int SubSceneNodeDepth;
            }

            bool ShouldIncludeNode(HierarchyImmutableNodeData node, bool inSubScene, SubSceneInfo subSceneInfo, UnsafeParallelHashMap<int, bool> rootGameObjectCache)
            {
                if (IsPrefabStage && (node.Flags & HierarchyNodeFlags.IsPrefabStage) == 0)
                    return false;

                // Hide dynamically loaded subscenes in authoring mode
                if (DataMode is DataMode.Authoring && node.Handle.Kind is NodeKind.SubScene && subSceneInfo.SubSceneNodeDepth == 0)
                    return false;

                // Include any node that's under a visible node in a subScene
                // Include any node outside of a subScene that's under a visible node
                if (!inSubScene && node.Depth > 1
                    || inSubScene && node.Depth > subSceneInfo.SubSceneNodeDepth + 1)
                    return true;

                // Specific case for subScene content in mixed mode
                if (inSubScene && DataMode is DataMode.Mixed)
                {
                    if (node.Handle.Kind is NodeKind.GameObject)
                        return true;

                    if (node.Handle.Kind is NodeKind.Entity)
                    {
                        if (!subSceneInfo.IsOpened)
                            return true;

                        var entity = node.Handle.ToEntity();
                        // if the entity doesn't exist anymore `HasComponent` will return false.
                        // hide prefab entities
                        if (DataAccess->HasComponent(entity, Prefab))
                            return false;
                        if (!DataAccess->HasComponent(entity, EntityGuid))
                            return true;

                        var entityGuid = *(EntityGuid*)DataAccess->EntityComponentStore->GetComponentDataWithTypeRO(entity, EntityGuid.TypeIndex);
                        return !rootGameObjectCache.ContainsKey(entityGuid.OriginatingId);
                    }
                }

                if (node.Handle.Kind is NodeKind.Entity)
                    return DataMode is DataMode.Runtime or DataMode.Mixed || inSubScene && !subSceneInfo.IsOpened;

                if (node.Handle.Kind is NodeKind.GameObject)
                    // show root GO always except in Playmode + Authoring
                    // show GO in subScenes except in Runtime
                    return node.Depth <= 1 && (!IsPlayMode || DataMode is not DataMode.Authoring) || inSubScene && DataMode is not DataMode.Runtime;

                return true;
            }

            bool IsExpanded(HierarchyNodeHandle handle)
            {
                switch (handle.Kind)
                {
                    case NodeKind.Scene:
                    case NodeKind.SubScene:
                        return !Expanded.Contains(handle);
                    default:
                        return Expanded.Contains(handle);
                }
            }
        }

        [BurstCompile]
        struct BuildFilteredNodes : IJob
        {
            [ReadOnly] public HierarchyNodeStore.Immutable Hierarchy;
            [ReadOnly] public NativeBitArray Filter;

            public NativeList<int> Nodes;
            public NativeList<int> IndexByNode;

            public void Execute()
            {
                // Skip the root.
                var readIndex = 1;
                var writeIndex = 0;

                Nodes.ResizeUninitialized(Filter.Length);
                IndexByNode.ResizeUninitialized(Filter.Length);

                for (var i = 0; i < IndexByNode.Length; i++)
                    IndexByNode[i] = -1;

                for (; readIndex < Hierarchy.Count; readIndex++)
                {
                    if (!Filter.IsSet(readIndex))
                        continue;

                    IndexByNode[readIndex] = writeIndex;
                    Nodes[writeIndex] = readIndex;

                    writeIndex++;
                }

                Nodes.Length = writeIndex;
            }
        }

        bool ICollection.IsSynchronized => throw new NotImplementedException();
        bool IList.IsFixedSize => throw new NotImplementedException();
        bool IList.IsReadOnly => throw new NotImplementedException();
        object ICollection.SyncRoot => throw new NotImplementedException();
        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
        void ICollection.CopyTo(Array array, int index) => throw new NotImplementedException();
        int IList.Add(object value) => throw new NotImplementedException();
        bool IList.Contains(object value) => throw new NotImplementedException();
        int IList.IndexOf(object value) => throw new NotImplementedException();
        void IList.Insert(int index, object value) => throw new NotImplementedException();
        void IList.Remove(object value) => throw new NotImplementedException();
        void IList.RemoveAt(int index) => throw new NotImplementedException();
    }
}
