using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Baking;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.SceneManagement;

namespace Unity.Entities.Conversion
{
    /// <summary>
    /// Represents a hierarchy of GameObjects by a Burst-compatible data structure.
    ///
    /// The hierarchy is encoded by three parallel arrays storing the instance ID of a GameObject, its Transform, and
    /// the index of the GameObject's parent. Two supporting data structures maintain a mapping from instance ID to
    /// index and from index to indices of the parents of a GameObject.
    /// </summary>
    struct IncrementalHierarchy : IDisposable
    {
        /// <summary>
        /// Contains the instance ID for every element in the hierarchy. This array is parallel to the parent index
        /// array and the transform array.
        /// </summary>
        public NativeList<int> InstanceId;

        /// <summary>
        /// Contains the index of the parent in the hierarchy for every element in the hierarchy. An invalid parent is
        /// denoted by index -1. This array is parallel to the instance ID array and the transform array.
        /// </summary>
        public NativeList<int> ParentIndex;

        /// <summary>
        /// Contains a handle for the transform data of all GameObjects in this hierarchy. This array is parallel to
        /// the instance ID array and the parent index array.
        /// </summary>
        public TransformAccessArray TransformArray;

        public NativeList<TransformAuthoring> TransformAuthorings;

        /// <summary>
        /// Maps the index of each element to the indices of its children.
        /// </summary>
        public NativeParallelHashMap<int, UnsafeList<int>> ChildIndicesByIndex;

        /// <summary>
        /// Maps instance IDs to indices in the hierarchy.
        /// </summary>
        public NativeParallelHashMap<int, int> IndexByInstanceId;

        /// <summary>
        /// Contains the active state for every element in the hierarchy. This array is parallel to the parent index
        /// array and the transform array.
        /// </summary>
        public NativeList<bool> Active;

        /// <summary>
        /// Contains the static state for every element in the hierarchy. This array is parallel to the parent index
        /// array and the transform array.
        /// </summary>
        public NativeList<bool> Static;

        public void Dispose()
        {
            if (TransformArray.isCreated)
                TransformArray.Dispose();
            if (TransformAuthorings.IsCreated)
                TransformAuthorings.Dispose();
            if (InstanceId.IsCreated)
                InstanceId.Dispose();
            if (ParentIndex.IsCreated)
                ParentIndex.Dispose();
            if (ChildIndicesByIndex.IsCreated)
            {
                // We need to release all the lists in the hash
                var iterator = ChildIndicesByIndex.GetEnumerator();
                while (iterator.MoveNext())
                {
                    iterator.Current.Value.Dispose();
                }
                ChildIndicesByIndex.Dispose();
            }

            if (IndexByInstanceId.IsCreated)
                IndexByInstanceId.Dispose();
            if (Active.IsCreated)
                Active.Dispose();
            if (Static.IsCreated)
                Static.Dispose();
        }

        public SceneHierarchy AsReadOnly() => new SceneHierarchy(this);
    }

    static class IncrementalHierarchyFunctions
    {
        internal static bool TryAddSingle(IncrementalHierarchy hierarchy, GameObject go)
        {
            var t = go.transform;
            var p = t.parent;
            GameObject parent = null;
            if (p != null)
                parent = p.gameObject;
            return TryAddSingle(hierarchy, go, t, parent);
        }

        internal static bool TryAddSingle(IncrementalHierarchy hierarchy, GameObject go, Transform t, GameObject parent)
        {
            int id = go.GetInstanceID();
            int index = hierarchy.InstanceId.Length;
            if (!hierarchy.IndexByInstanceId.TryAdd(id, index))
                return false;
            hierarchy.InstanceId.Add(id);
            hierarchy.TransformArray.Add(t);
            hierarchy.TransformAuthorings.Add(default);
            hierarchy.Active.Add(go.activeSelf);
            hierarchy.Static.Add(go.isStatic);

            if (parent != null)
            {
                var pid = parent.GetInstanceID();
                // this line assumes that parent of this GameObject has already been added.
                hierarchy.IndexByInstanceId.TryGetValue(pid, out var parentIndex);
                hierarchy.ParentIndex.Add(parentIndex);
                AddChild(hierarchy, parentIndex, index);
            }
            else
                hierarchy.ParentIndex.Add(-1);

            return true;
        }

        internal static void AddRecurse(IncrementalHierarchy hierarchy, GameObject go, List<GameObject> allGameObjects = null)
        {
            var t = go.transform;
            var p = t.parent;
            AddRecurse(hierarchy, go, t, p != null ? p.gameObject : null, allGameObjects);
        }

        static void AddRecurse(IncrementalHierarchy hierarchy, GameObject go, Transform top, GameObject parent, List<GameObject> allGameObjects)
        {
            TryAddSingle(hierarchy, go, top, parent);
            if(allGameObjects != null)
                allGameObjects.Add(go);
            int n = top.transform.childCount;
            for (int i = 0; i < n; i++)
            {
                var child = top.transform.GetChild(i);
                AddRecurse(hierarchy, child.gameObject, child, go, allGameObjects);
            }
        }

        static void AddRoots(IncrementalHierarchy hierarchy, IEnumerable<GameObject> gameObjects)
        {
            foreach (var go in gameObjects)
            {
                var t = go.transform;
                AddRecurse(hierarchy, go, t, null, null);
            }
        }

        internal static void AddChild(IncrementalHierarchy hierarchy, int parentIndex, int index)
        {
            if (!hierarchy.ChildIndicesByIndex.TryGetValue(parentIndex, out var childList))
            {
                childList = new UnsafeList<int>(1, Allocator.Persistent);
            }
            childList.Add(index);
            hierarchy.ChildIndicesByIndex[parentIndex] = childList;
        }

        internal static void ChangeChildrenOrderInParent(IncrementalHierarchy hierarchy, int parentId)
        {
            if (hierarchy.IndexByInstanceId.TryGetValue(parentId, out int newParentIdx))
            {
                UpdateChildrenIndices(hierarchy, newParentIdx);
            }
        }

        struct SiblingIndexEntry
        {
            public int index;
            public int value;
        }

        struct SiblingIndexEntryLastFirstComparer : IComparer<SiblingIndexEntry>
        {
            public int Compare(SiblingIndexEntry x, SiblingIndexEntry y)
            {
                int firstCriteria = x.value.CompareTo(y.value);
                if (firstCriteria == 0)
                {
                    // In reverse so the last duplicates are assigned first
                    return y.index.CompareTo(x.index);
                }
                return firstCriteria;
            }
        }

        internal static void UpdateChildrenIndices(IncrementalHierarchy hierarchy, int parentIndex)
        {
            using (var marker = new ProfilerMarker("UpdateChildrenIndices").Auto())
            {
                if (!hierarchy.ChildIndicesByIndex.TryGetValue(parentIndex, out var childList))
                {
                    childList = new UnsafeList<int>(1, Allocator.Persistent);
                }

                int numChildren = childList.Length;
                NativeArray<int> siblingIndices = new NativeArray<int>(numChildren, Allocator.Temp);
                UnsafeList<int> copiedChildList = new UnsafeList<int>(1, Allocator.Persistent);
                NativeArray <SiblingIndexEntry> sortedSiblingIndicesArray = new NativeArray<SiblingIndexEntry>(siblingIndices.Length, Allocator.Temp);
                for (int i = 0; i < numChildren; i++)
                {
                    var childIndex = childList[i];
                    copiedChildList.Add(childIndex);
                    siblingIndices[i] = hierarchy.TransformArray[childIndex].GetSiblingIndex();
                }

                // There are cases where the siblingsIndices array can be invalid (duplicated indices or indices greater than the size of the array):
                // Re-parenting several children at a time or swapping children in different parents, one event (ChangeGameObjectParent) is triggered for each children move.
                // But the engine hierarchy retrieved via TransformArray is updated already after the first event triggered
                // This will create either indices being out of range in siblingIndices or indices being potentially duplicated
                // We will need to update the siblingIndices array to a valid index array
                // Ex: [2 0 1 2] (invalid) will need to be updated to [3 0 1 2](valid) before updating the childlist with children moved one at a time

                // 1. We store for each entry the original index
                // So for [2,0,1,2]
                //      Index  0 1 2 3
                //      Value  2 0 1 2
                for (int index = 0; index < siblingIndices.Length; ++index)
                {
                    sortedSiblingIndicesArray[index] = new SiblingIndexEntry {index = index, value = siblingIndices[index]};
                }

                // This comparer will produce for input [2,0,1,2] the output [3, 0, 1, 2]
                var comparer = new SiblingIndexEntryLastFirstComparer();

                // 2. We sort the data. For the example, after sorting we get:
                //      Index  1 2 3 0
                //      Value  0 1 2 2
                sortedSiblingIndicesArray.Sort(comparer);

                // 3. We reassign the data using the stored index already sorted. After this we get the output that we wanted for the example
                // [3, 0, 1, 2]
                for (int index = 0; index < siblingIndices.Length; ++index)
                {
                    siblingIndices[ sortedSiblingIndicesArray[index].index ] = index;
                }
                sortedSiblingIndicesArray.Dispose();

                // Recreate the new children list
                for (int i = 0; i < numChildren; i++)
                {
                    var siblingIndex = siblingIndices[i];

                    if (siblingIndex < numChildren)
                        childList[siblingIndex] = copiedChildList[i];
                    else
                    {
                        UnityEngine.Debug.LogError($"Failed to fix the sibling indices: the sibling index {siblingIndex} is greater than the number of children: {numChildren}");
                    }
                }

                hierarchy.ChildIndicesByIndex[parentIndex] = childList;
                siblingIndices.Dispose();
                copiedChildList.Dispose();
            }
        }

        internal static void RemoveChild(IncrementalHierarchy hierarchy, int oldParentIdx, int idx)
        {
            if (hierarchy.ChildIndicesByIndex.TryGetValue(oldParentIdx, out var list))
            {
                int removeIndex = UnsafeListExtensions.IndexOf(list, idx);
                if (removeIndex != -1)
                {
                    if (list.Length > 1)
                    {
                        // We found the item, but there are other ones there too
                        list.RemoveAt(removeIndex);
                        hierarchy.ChildIndicesByIndex[oldParentIdx] = list;
                    }
                    else
                    {
                        hierarchy.ChildIndicesByIndex.Remove(oldParentIdx);
                        list.Dispose();
                    }
                }
            }
        }

        internal static void RemoveAllImmediateChildren(IncrementalHierarchy hierarchy, int oldParentIdx)
        {
            if (hierarchy.ChildIndicesByIndex.TryGetValue(oldParentIdx, out var list))
            {
                hierarchy.ChildIndicesByIndex.Remove(oldParentIdx);
                list.Dispose();
            }
        }

        internal static void ReplaceChildIndex(IncrementalHierarchy hierarchy, int parentIdx, int oldIdx, int newIdx)
        {
            if (hierarchy.ChildIndicesByIndex.TryGetValue(parentIdx, out var list))
            {
                int removeIndex = UnsafeListExtensions.IndexOf(list, oldIdx);
                if (removeIndex != -1)
                {
                    list[removeIndex] = newIdx;
                }
                else
                {
                    // There is no old value to replace, just add it at the end
                    list.Add(newIdx);
                }
                hierarchy.ChildIndicesByIndex[parentIdx] = list;
            }
            else
            {
                // If the parent doesn't exist, do a normal add
                AddChild(hierarchy, parentIdx, newIdx);
            }
        }

        internal static void MoveChildrenToDifferentParent(IncrementalHierarchy hierarchy, int oldParent, int newParent)
        {
            // If there is no entry for the old parent, then there is nothing to move
            if (hierarchy.ChildIndicesByIndex.TryGetValue(oldParent, out var oldList))
            {
                if (hierarchy.ChildIndicesByIndex.TryGetValue(newParent, out var newList))
                {
                    if (newList.Length > 0)
                    {
                        // Slower path, copy the elements into the new list and release the old list
                        newList.AddRange(oldList);
                        oldList.Dispose();
                        hierarchy.ChildIndicesByIndex[newParent] = newList;
                    }
                    else
                    {
                        // If the new list was empty, delete it and replace it by the oldList
                        newList.Dispose();
                        hierarchy.ChildIndicesByIndex[newParent] = oldList;
                    }
                }
                else
                {
                    // Fast path, just move the lists
                    hierarchy.ChildIndicesByIndex[newParent] = oldList;
                }
                hierarchy.ChildIndicesByIndex.Remove(oldParent);
            }
        }

        internal struct Enumerator : IEnumerator<int>, IEnumerable<int>
        {
            internal IncrementalHierarchy Hierarchy;
            internal int InitialParentIdx;
            internal int CurrentIndexValue;

            internal UnsafeList<UnsafeList<int>.Enumerator> Stack;

            public bool MoveNext()
            {
                if (!Stack.IsCreated)
                    Create();

                while (!Stack.IsEmpty)
                {
                    ref var top = ref Stack.ElementAt(Stack.Length - 1);
                    if (top.MoveNext())
                    {
                        // We have a valid value
                        CurrentIndexValue = top.Current;

                        // We add their children to the stack
                        if (Hierarchy.ChildIndicesByIndex.TryGetValue(CurrentIndexValue, out var childrenList))
                            Stack.Add(childrenList.GetEnumerator());

                        return true;
                    }
                    // We don't have a valid value, we pop
                    Stack.RemoveAtSwapBack(Stack.Length - 1);
                }
                return false;
            }

            public void Create()
            {
                Stack = new UnsafeList<UnsafeList<int>.Enumerator>(10, Allocator.Temp);
                Reset();
            }

            public void Reset()
            {
                Stack.Clear();
                var enumerator = IncrementalHierarchyFunctions.GetChildren(Hierarchy, InitialParentIdx);
                Stack.Add(enumerator);
            }

            object IEnumerator.Current => Current;
            public int Current => CurrentIndexValue;
            public void Dispose()
            {
                if (Stack.IsCreated)
                    Stack.Dispose();
            }

            public IEnumerator<int> GetEnumerator()
            {
                return this;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        internal static Enumerator GetChildrenRecursively(IncrementalHierarchy hierarchy, int parentIndex)
        {
            var enumerator = new Enumerator();
            enumerator.Hierarchy = hierarchy;
            enumerator.InitialParentIdx = parentIndex;
            return enumerator;
        }

        internal static UnsafeList<int>.Enumerator GetChildren(IncrementalHierarchy hierarchy, int parentIdx)
        {
            if (hierarchy.ChildIndicesByIndex.TryGetValue(parentIdx, out var list))
            {
                return list.GetEnumerator();
            }
            return default;
        }

        internal static UnsafeList<int>.Enumerator GetChildren(NativeParallelHashMap<int, UnsafeList<int>> childIndicesByIndex, int parentIdx)
        {
            if (childIndicesByIndex.TryGetValue(parentIdx, out var list))
            {
                return list.GetEnumerator();
            }
            return default;
        }

        internal static int ChildrenCount(IncrementalHierarchy hierarchy)
        {
            int count = 0;
            foreach (var entry in hierarchy.ChildIndicesByIndex)
            {
                count += entry.Value.Length;
            }
            return count;
        }

        internal static void ChangeParents(IncrementalHierarchy hierarchy, NativeKeyValueArrays<int, int> parentChange, NativeList<int> outChangeFailed, NativeList<IncrementalBakingChanges.ParentChange> outChangeSuccessful)
        {
            var instanceIds = parentChange.Keys;
            var parentInstanceIds = parentChange.Values;
            for (int i = 0; i < instanceIds.Length; i++)
            {
                var instanceId = instanceIds[i];
                if (!hierarchy.IndexByInstanceId.TryGetValue(instanceId, out int idx))
                {
                    outChangeFailed.Add(instanceId);
                    // this case might happen when an instance was already removed
                    continue;
                }

                int oldParentIdx = hierarchy.ParentIndex[idx];
                int oldParentId = 0;
                if (oldParentIdx != -1)
                {
                    oldParentId = hierarchy.InstanceId[oldParentIdx];
                    RemoveChild(hierarchy, oldParentIdx, idx);
                }

                int newParentId = parentInstanceIds[i];
                if (hierarchy.IndexByInstanceId.TryGetValue(newParentId, out int newParentIdx))
                {
                    AddChild(hierarchy, newParentIdx, idx);
                    UpdateChildrenIndices(hierarchy, newParentIdx);
                    hierarchy.ParentIndex[idx] = newParentIdx;

                    outChangeSuccessful.Add(new IncrementalBakingChanges.ParentChange
                    {
                        InstanceId = instanceId,
                        NewParentInstanceId = newParentId,
                        PreviousParentInstanceId = oldParentId,
                    });
                }
                else
                {
                    if (newParentId != 0)
                        outChangeFailed.Add(instanceId);
                    else
                    {
                        // We are a root object
                        outChangeSuccessful.Add(new IncrementalBakingChanges.ParentChange
                        {
                            InstanceId = instanceId,
                            NewParentInstanceId = newParentId,
                            PreviousParentInstanceId = oldParentId,
                        });
                    }

                    hierarchy.ParentIndex[idx] = -1;
                }
            }
        }

        internal static void UpdateActiveAndStaticState(IncrementalHierarchy hierarchy, int instanceId, bool active, bool isStatic)
        {
            var index = hierarchy.IndexByInstanceId[instanceId];
            ref var activeStatus = ref hierarchy.Active.ElementAt(index);
            activeStatus = active;

            ref var staticStatus = ref hierarchy.Static.ElementAt(index);
            staticStatus = isStatic;
        }

        internal static void Remove(IncrementalHierarchy hierarchy, NativeArray<int> instances)
        {
            var openInstanceIds = new NativeList<int>(instances.Length, Allocator.Temp);
            openInstanceIds.AddRange(instances);

            // This code currently doesn't make use of the fact that we are always deleting entire subhierarchies
            while (openInstanceIds.Length > 0)
            {
                int id = openInstanceIds[openInstanceIds.Length - 1];
                openInstanceIds.Length -= 1;
                if (!hierarchy.IndexByInstanceId.TryGetValue(id, out int idx))
                    continue;

                {
                    // push children and remove children array entry
                    var iter = GetChildren(hierarchy, idx);
                    while (iter.MoveNext())
                        openInstanceIds.Add(hierarchy.InstanceId[iter.Current]);
                    RemoveAllImmediateChildren(hierarchy, idx);
                }

                // Remove-and-swap on the arrays
                hierarchy.InstanceId.RemoveAtSwapBack(idx);
                int oldParentIdx = hierarchy.ParentIndex[idx];
                hierarchy.ParentIndex.RemoveAtSwapBack(idx);
                hierarchy.TransformArray.RemoveAtSwapBack(idx);
                hierarchy.Active.RemoveAtSwapBack(idx);
                hierarchy.Static.RemoveAtSwapBack(idx);
                hierarchy.TransformAuthorings.RemoveAtSwapBack(idx);

                // then patch up the lookup tables
                hierarchy.IndexByInstanceId.Remove(id);
                if (oldParentIdx != -1)
                    RemoveChild(hierarchy, oldParentIdx, idx);
                int swappedIdx = hierarchy.InstanceId.Length;
                if (swappedIdx > 0 && swappedIdx != idx)
                {
                    // update index to instance id lookup
                    int swappedId = hierarchy.InstanceId[idx];
                    hierarchy.IndexByInstanceId[swappedId] = idx;

                    // update index to children lookup of parent
                    int swappedParentIdx = hierarchy.ParentIndex[idx];
                    if (swappedParentIdx != -1)
                    {
                        ReplaceChildIndex(hierarchy, swappedParentIdx, swappedIdx, idx);
                    }

                    // update index to children lookup of swapped index
                    var iter = GetChildren(hierarchy, swappedIdx);
                    while (iter.MoveNext())
                    {
                        hierarchy.ParentIndex[iter.Current] = idx;
                    }

                    MoveChildrenToDifferentParent(hierarchy, swappedIdx, idx);
                }
            }
        }

#if UNITY_EDITOR
        static void ValidateThatHierarchyContainsScene(Scene scene, IncrementalHierarchy hierarchy)
        {
            Stack<GameObject> open = new Stack<GameObject>();
            foreach (var go in scene.GetRootGameObjects())
                open.Push(go);
            var childIndexCache = new List<int>();
            var childIdCache = new List<int>();
            while (open.Count > 0)
            {
                var go = open.Pop();
                var id = go.GetInstanceID();
                if (hierarchy.IndexByInstanceId.TryGetValue(id, out var idx))
                {
                    if (go.transform != hierarchy.TransformArray[idx])
                    {
                        var otherTransform = hierarchy.TransformArray[idx];
                        var otherId = otherTransform?.gameObject?.GetInstanceID() ?? 0;
                        Debug.LogError($"Object {go} ({go.GetInstanceID()}) is stored at index {idx}, but the transform stored there is {otherTransform} ({otherId})");
                    }

                    var parentIdx = hierarchy.ParentIndex[idx];
                    if (go.transform.parent == null && parentIdx != -1)
                    {
                        int parentId = hierarchy.InstanceId[parentIdx];
                        var parentObj = UnityEditor.EditorUtility.InstanceIDToObject(parentId);
                        Debug.LogError(
                            $"Object {go} ({go.GetInstanceID()}) has no parent, but in the hierarchy parent {parentObj} ({parentId}) is stored");
                    }
                    else if (go.transform.parent != null)
                    {
                        int parentId = hierarchy.InstanceId[parentIdx];
                        var parentObjFromTransform = go.transform.parent.gameObject;
                        if (parentObjFromTransform.GetInstanceID() != parentId)
                        {
                            var parentObj = UnityEditor.EditorUtility.InstanceIDToObject(parentId);
                            Debug.LogError(
                                $"Object {go} ({go.GetInstanceID()}) has parent {parentObjFromTransform} ({parentObjFromTransform.GetInstanceID()}), but in the hierarchy parent {parentObj} ({parentId} is stored)");
                        }
                    }

                    // validate children
                    childIndexCache.Clear();
                    childIdCache.Clear();
                    var childIter = GetChildren(hierarchy, idx);
                    while (childIter.MoveNext())
                    {
                        childIndexCache.Add(childIter.Current);
                        childIdCache.Add(hierarchy.InstanceId[childIter.Current]);
                    }

                    if (childIndexCache.Count != go.transform.childCount)
                        Debug.LogError(
                            $"Object {go} ({go.GetInstanceID()}) has {go.transform.childCount} children, but in the hierarchy {childIndexCache.Count} children are stored");

                    for (int i = 0; i < go.transform.childCount; i++)
                    {
                        var child = go.transform.GetChild(i).gameObject;
                        var childId = child.GetInstanceID();
                        if (!childIdCache.Contains(childId))
                            Debug.LogError(
                                $"Object {go} ({go.GetInstanceID()}) has child {child} ({childId}), but in the hierarchy it is missing");
                    }
                }
                else
                    Debug.LogError($"Object {go} ({go.GetInstanceID()}) is not present in the hierarchy");

                for (int i = 0; i < go.transform.childCount; i++)
                    open.Push(go.transform.GetChild(i).gameObject);
            }
        }

        static void ValidateThatSceneContainsHierarchy(Scene scene, IncrementalHierarchy hierarchy)
        {
            if (hierarchy.InstanceId.Length == 0)
                return;
            var objects = new List<UnityEngine.Object>();
            Resources.InstanceIDToObjectList(hierarchy.InstanceId.AsArray(), objects);
            for (int i = 0; i < objects.Count; i++)
            {
                var go = objects[i] as GameObject;
                if (go == null)
                {
                    Debug.LogError(
                        $"Object {objects[i]} ({hierarchy.InstanceId[i]}) is in the hierarchy, but doesn't exist anymore or isn't a GameObject");
                    continue;
                }
                if (go.scene.IsValid() && go.scene != scene)
                    Debug.LogError($"Object {objects[i]} ({hierarchy.InstanceId[i]}) from scene {go.scene.name} ({go.scene.handle}) is in the hierarchy, but is not part of the conversion scene {scene.name} ({scene.handle})");
                if (hierarchy.TransformArray[i] != go.transform)
                {
                    var otherTransform = hierarchy.TransformArray[i];
                    var otherId = otherTransform?.gameObject.GetInstanceID() ?? 0;
                    Debug.LogError($"Object {go} ({go.GetInstanceID()}) is stored at index {i}, but the transform stored there is {otherTransform} ({otherId})");
                }
            }
        }

        internal static void Validate(Scene scene, IncrementalHierarchy hierarchy)
        {
            ValidateThatHierarchyContainsScene(scene, hierarchy);
            ValidateThatSceneContainsHierarchy(scene, hierarchy);
        }
#endif

        internal static void Build(GameObject[] roots, out IncrementalHierarchy hierarchy, Allocator alloc)
        {
            hierarchy = new IncrementalHierarchy
            {
                TransformArray = new TransformAccessArray(roots.Length),
                TransformAuthorings = new NativeList<TransformAuthoring>(roots.Length, alloc),
                ChildIndicesByIndex = new NativeParallelHashMap<int, UnsafeList<int>>(roots.Length, alloc),
                IndexByInstanceId = new NativeParallelHashMap<int, int>(roots.Length, alloc),
                InstanceId = new NativeList<int>(roots.Length, alloc),
                ParentIndex = new NativeList<int>(roots.Length, alloc),
                Active = new NativeList<bool>(roots.Length, alloc),
                Static = new NativeList<bool>(roots.Length, alloc)
            };
            AddRoots(hierarchy, roots);
        }
    }
}
