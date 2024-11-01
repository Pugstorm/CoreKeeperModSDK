using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Baking;
using Unity.Entities.Conversion;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Unity.Entities.Tests.Conversion
{
    public class IncrementalHierarchyTests
    {
        private IncrementalHierarchy m_Hierarchy;
        private TestWithObjects m_Objects;

        private AllocatorHelper<RewindableAllocator> m_AllocatorHelper;
        private ref RewindableAllocator RwdAllocator => ref m_AllocatorHelper.Allocator;

        [OneTimeSetUp]
        public virtual void OneTimeSetUp()
        {
            m_AllocatorHelper = new AllocatorHelper<RewindableAllocator>(Allocator.Persistent);
            m_AllocatorHelper.Allocator.Initialize(128 * 1024, true);
        }

        [OneTimeTearDown]
        public virtual void OneTimeTearDown()
        {
            m_AllocatorHelper.Allocator.Dispose();
            m_AllocatorHelper.Dispose();
        }

        [SetUp]
        public void SetUp()
        {
            m_Objects.SetUp();
        }

        [TearDown]
        public void TearDown()
        {
            m_Hierarchy.Dispose();
            m_Objects.TearDown();
            RwdAllocator.Rewind();
            // This is test only behavior for determinism.  Rewind twice such that all
            // tests start with an allocator containing only one memory block.
            RwdAllocator.Rewind();
        }

        void AssertSize(int n)
        {
            Assert.AreEqual(n, m_Hierarchy.InstanceId.Length, $"{nameof(m_Hierarchy.InstanceId)} doesn't have the expected size {n}");
            Assert.AreEqual(n, m_Hierarchy.ParentIndex.Length, $"{nameof(m_Hierarchy.ParentIndex)} doesn't have the expected size {n}");
            Assert.AreEqual(n, m_Hierarchy.TransformArray.length, $"{nameof(m_Hierarchy.TransformArray)} doesn't have the expected size {n}");
            Assert.AreEqual(n, m_Hierarchy.TransformAuthorings.Length, $"{nameof(m_Hierarchy.TransformAuthorings)} doesn't have the expected size {n}");
            Assert.GreaterOrEqual(n, IncrementalHierarchyFunctions.ChildrenCount(m_Hierarchy), $"{nameof(m_Hierarchy.ChildIndicesByIndex)} doesn't have the expected maximum size {n}");
            Assert.AreEqual(n, m_Hierarchy.IndexByInstanceId.Count(), $"{nameof(m_Hierarchy.IndexByInstanceId)} doesn't have the expected size {n}");
        }

        void AssertIndexInBounds(int idx, int n, string name) => Assert.IsTrue(0 <= idx && idx < n, $"Index {idx} is out of bounds of {name}, length {n}");

        void AssertConsistency(GameObject go)
        {
            bool success = m_Hierarchy.IndexByInstanceId.TryGetValue(go.GetInstanceID(), out int index);
            Assert.IsTrue(success, $"{nameof(m_Hierarchy.IndexByInstanceId)} does not contain {go.GetInstanceID()}");
            AssertIndexInBounds(index, m_Hierarchy.InstanceId.Length, nameof(m_Hierarchy.InstanceId));
            Assert.AreEqual(m_Hierarchy.InstanceId[index], go.GetInstanceID());
            Assert.AreSame(m_Hierarchy.TransformArray[index], go.transform);
            //@TODO: DOTS-5467
            //Assert.AreSame(m_Hierarchy.TransformAuthorings, go.transform);
            if (go.transform.childCount > 0)
            {
                bool hasChildren = m_Hierarchy.ChildIndicesByIndex.ContainsKey(index);
                Assert.IsTrue(hasChildren, $"{go} has children but not in the hierarchy");
                int childCount = go.transform.childCount;
                var iter = IncrementalHierarchyFunctions.GetChildren(m_Hierarchy, index);

                var childFound = new List<int>();
                for (int c = 0; c < childCount; c++)
                {
                    Assert.IsTrue(iter.MoveNext(), $"{go} only has {c} children in the hierarchy, but {childCount} in reality.");
                    int childIndex = iter.Current;
                    AssertIndexInBounds(childIndex, m_Hierarchy.InstanceId.Length, nameof(m_Hierarchy.InstanceId));
                    int childId = m_Hierarchy.InstanceId[childIndex];
                    childFound.Add(childId);
                }

                for (int c = 0; c < childCount; c++)
                {
                    var child = go.transform.GetChild(c).gameObject;
                    Assert.IsTrue(childFound.Contains(child.GetInstanceID()), $"Child {child} of {go} is not registered in the hierarchy");
                    AssertConsistency(child);
                }
            }

            if (go.transform.parent != null)
            {
                var parentTransform = go.transform.parent;
                var parent = parentTransform != null ? parentTransform.gameObject : null;
                int parentId = parent != null ? parent.GetInstanceID() : 0;
                int parentIndex = -1;
                if (parentId != 0)
                {
                    bool parentFound = m_Hierarchy.IndexByInstanceId.TryGetValue(parentId, out parentIndex);
                    Assert.IsTrue(parentFound);
                }

                var storedIndex = m_Hierarchy.ParentIndex[index];
                if (storedIndex == -1)
                    Assert.AreEqual(parentIndex, storedIndex, $"Parent of {go} is incorrect: (null), should be {parent}");
                else
                {
                    AssertIndexInBounds(storedIndex, m_Hierarchy.InstanceId.Length, nameof(m_Hierarchy.InstanceId));
                    var obj = EditorUtility.InstanceIDToObject(m_Hierarchy.InstanceId[storedIndex]);
                    Assert.AreEqual(parentIndex, storedIndex, $"Parent of {go} is incorrect: {obj?.ToString() ?? "null"}, should be {parent}");
                }
            }
        }

        [Test]
        public void Hierarchy_WithSingleGameObject_IsEmptyAfterDelete()
        {
            var go = m_Objects.CreateGameObject("root");
            IncrementalHierarchyFunctions.Build(new [] {go}, out m_Hierarchy, Allocator.Temp);
            AssertSize(1);
            AssertConsistency(go);
            IncrementalHierarchyFunctions.Remove(m_Hierarchy, new NativeArray<int>(new []{go.GetInstanceID()}, Allocator.Temp));
            AssertSize(0);
        }

        [Test]
        public void Hierarchy_WithHierarchy_BuildIsConsistent()
        {
            var go = m_Objects.CreateGameObject("root");
            var c1 = m_Objects.CreateGameObject("c1");
            c1.transform.SetParent(go.transform);
            var c2 = m_Objects.CreateGameObject("c2");
            c2.transform.SetParent(go.transform);
            var c3 = m_Objects.CreateGameObject("c3");
            c3.transform.SetParent(go.transform);
            IncrementalHierarchyFunctions.Build(new [] {go}, out m_Hierarchy, Allocator.Temp);
            AssertSize(4);
            AssertConsistency(go);
        }

        [Test]
        public void Hierarchy_WithHierarchy_Remove1By1_MaintainsConsistency()
        {
            var go = m_Objects.CreateGameObject("root");
            var c1 = m_Objects.CreateGameObject("c1");
            c1.transform.SetParent(go.transform);
            var c2 = m_Objects.CreateGameObject("c2");
            c2.transform.SetParent(go.transform);
            var c3 = m_Objects.CreateGameObject("c3");
            c3.transform.SetParent(go.transform);
            IncrementalHierarchyFunctions.Build(new [] {go}, out m_Hierarchy, Allocator.Temp);
            AssertSize(4);
            AssertConsistency(go);
            IncrementalHierarchyFunctions.Remove(m_Hierarchy, new NativeArray<int>(new []{ c2.GetInstanceID() }, Allocator.Temp));
            Object.DestroyImmediate(c2);
            AssertSize(3);
            AssertConsistency(go);
            IncrementalHierarchyFunctions.Remove(m_Hierarchy, new NativeArray<int>(new []{ c1.GetInstanceID() }, Allocator.Temp));
            Object.DestroyImmediate(c1);
            AssertSize(2);
            AssertConsistency(go);
            IncrementalHierarchyFunctions.Remove(m_Hierarchy, new NativeArray<int>(new []{ c3.GetInstanceID() }, Allocator.Temp));
            Object.DestroyImmediate(c3);
            AssertSize(1);
            AssertConsistency(go);
            IncrementalHierarchyFunctions.Remove(m_Hierarchy, new NativeArray<int>(new []{ go.GetInstanceID() }, Allocator.Temp));
            Object.DestroyImmediate(go);
            AssertSize(0);
        }

        [Test]
        public void Hierarchy_WithHierarchy_DeleteRemovesFullHierarchy()
        {
            var go = m_Objects.CreateGameObject("root");
            m_Objects.CreateGameObject("c1").transform.SetParent(go.transform);
            m_Objects.CreateGameObject("c2").transform.SetParent(go.transform);
            m_Objects.CreateGameObject("c3").transform.SetParent(go.transform);
            IncrementalHierarchyFunctions.Build(new [] {go}, out m_Hierarchy, Allocator.Temp);
            AssertSize(4);
            AssertConsistency(go);
            IncrementalHierarchyFunctions.Remove(m_Hierarchy, new NativeArray<int>(new []{ go.GetInstanceID() }, Allocator.Temp));
            AssertSize(0);
        }

        [Test]
        public void Hierarchy_WithHierarchy_DeleteRemovesSubHierarchy()
        {
            var go = m_Objects.CreateGameObject("root");
            m_Objects.CreateGameObject("c1").transform.SetParent(go.transform);
            var c2 = m_Objects.CreateGameObject("c2");
            c2.transform.SetParent(go.transform);
            m_Objects.CreateGameObject("c3").transform.SetParent(go.transform);

            m_Objects.CreateGameObject("c21").transform.SetParent(c2.transform);
            m_Objects.CreateGameObject("c22").transform.SetParent(c2.transform);
            IncrementalHierarchyFunctions.Build(new [] {go}, out m_Hierarchy, Allocator.Temp);
            AssertSize(6);
            AssertConsistency(go);
            IncrementalHierarchyFunctions.Remove(m_Hierarchy, new NativeArray<int>(new []{ c2.GetInstanceID() }, Allocator.Temp));
            Object.DestroyImmediate(c2);
            AssertSize(3);
            AssertConsistency(go);
            IncrementalHierarchyFunctions.Remove(m_Hierarchy, new NativeArray<int>(new []{ go.GetInstanceID() }, Allocator.Temp));
            AssertSize(0);
        }

        [Test]
        public void Hierarchy_WithHierarchy_DeleteSeparateHierarchy_MaintainsConsistency()
        {
            var root1 = m_Objects.CreateGameObject("root1");
            m_Objects.CreateGameObject("c1").transform.SetParent(root1.transform);
            var root2 = m_Objects.CreateGameObject("root2");
            m_Objects.CreateGameObject("c2").transform.SetParent(root2.transform);
            IncrementalHierarchyFunctions.Build(new [] {root1, root2}, out m_Hierarchy, Allocator.Temp);
            AssertSize(4);
            AssertConsistency(root1);
            AssertConsistency(root2);
            IncrementalHierarchyFunctions.Remove(m_Hierarchy, new NativeArray<int>(new []{ root1.GetInstanceID() }, Allocator.Temp));
            Object.DestroyImmediate(root1);
            AssertSize(2);
            AssertConsistency(root2);
            IncrementalHierarchyFunctions.Remove(m_Hierarchy, new NativeArray<int>(new []{ root2.GetInstanceID() }, Allocator.Temp));
            AssertSize(0);
        }

        [Test]
        public void Hierarchy_WithHierarchy_DeleteOutOfOrder_MaintainsConsistency()
        {
            var root1 = m_Objects.CreateGameObject("root1");
            var c1 = m_Objects.CreateGameObject("c1");
            c1.transform.SetParent(root1.transform);
            var c2 = m_Objects.CreateGameObject("c2");
            c2.transform.SetParent(c1.transform);
            IncrementalHierarchyFunctions.Build(new [] {root1}, out m_Hierarchy, Allocator.Temp);
            AssertSize(3);
            AssertConsistency(root1);
            IncrementalHierarchyFunctions.Remove(m_Hierarchy, new NativeArray<int>(new []{ c2.GetInstanceID(), c1.GetInstanceID() }, Allocator.Temp));
            Object.DestroyImmediate(c1);
            AssertSize(1);
            AssertConsistency(root1);
        }

        [Test]
        public void Hierarchy_WithHierarchy_AddingIncrementally_MaintainsConsistency()
        {
            var go = m_Objects.CreateGameObject("root");
            IncrementalHierarchyFunctions.Build(new [] {go}, out m_Hierarchy, Allocator.Temp);
            AssertSize(1);
            AssertConsistency(go);
            var c1 = m_Objects.CreateGameObject("c1");
            c1.transform.SetParent(go.transform);
            IncrementalHierarchyFunctions.TryAddSingle(m_Hierarchy, c1);
            AssertSize(2);
            AssertConsistency(go);
            var c2 = m_Objects.CreateGameObject("c2");
            c2.transform.SetParent(go.transform);
            IncrementalHierarchyFunctions.TryAddSingle(m_Hierarchy, c2);
            AssertSize(3);
            AssertConsistency(go);
            var c3 = m_Objects.CreateGameObject("c3");
            c3.transform.SetParent(go.transform);
            IncrementalHierarchyFunctions.TryAddSingle(m_Hierarchy, c3);
            AssertSize(4);
            AssertConsistency(go);
        }

        [Test]
        public void Hierarchy_WithHierarchy_AddingSubHierarchy_MaintainsConsistency()
        {
            var go = m_Objects.CreateGameObject("root");
            IncrementalHierarchyFunctions.Build(new [] {go}, out m_Hierarchy, Allocator.Temp);
            AssertSize(1);
            AssertConsistency(go);
            var c1 = m_Objects.CreateGameObject("c1");
            c1.transform.SetParent(go.transform);
            var c2 = m_Objects.CreateGameObject("c2");
            c2.transform.SetParent(c1.transform);
            var c3 = m_Objects.CreateGameObject("c3");
            c3.transform.SetParent(c2.transform);
            IncrementalHierarchyFunctions.AddRecurse(m_Hierarchy, c1);
            AssertSize(4);
            AssertConsistency(go);
        }

        [Test]
        public void Hierarchy_WithHierarchy_RecursiveChildrenEnumeration_WalksAllChildren()
        {
            var go = m_Objects.CreateGameObject("root");
            var c1 = m_Objects.CreateGameObject("c1"); c1.transform.SetParent(go.transform);
            var c2 = m_Objects.CreateGameObject("c2"); c2.transform.SetParent(go.transform);
            var c3 = m_Objects.CreateGameObject("c3"); c3.transform.SetParent(go.transform);

            var c21 = m_Objects.CreateGameObject("c21"); c21.transform.SetParent(c2.transform);
            var c22 = m_Objects.CreateGameObject("c22"); c22.transform.SetParent(c2.transform);

            var gameObjectHierarchy = go.GetComponentsInChildren<Transform>();

            IncrementalHierarchyFunctions.Build(new [] {go}, out m_Hierarchy, Allocator.Temp);
            AssertSize(6);
            AssertConsistency(go);

            var rootIndex = m_Hierarchy.IndexByInstanceId[go.GetInstanceID()];
            var children = IncrementalHierarchyFunctions.GetChildrenRecursively(m_Hierarchy, rootIndex);

            // We skip the parent so start at 1
            int index = 1;
            foreach(var childIndex in children)
            {
                var childInstance = m_Hierarchy.InstanceId[childIndex];

                Assert.IsTrue(gameObjectHierarchy[index].gameObject.GetInstanceID() == childInstance);
                index++;
            }

            // Lastly, make sure we walked everything and didn't stop short
            Assert.AreEqual(gameObjectHierarchy.Length, index);
        }

        [Test]
        public void Hierarchy_WithHierarchy_ChangingParents_MaintainsConsistency()
        {
            var go = m_Objects.CreateGameObject("root");
            var c1 = m_Objects.CreateGameObject("c1");
            c1.transform.SetParent(go.transform);
            var c2 = m_Objects.CreateGameObject("c2");
            c2.transform.SetParent(go.transform);
            var c3 = m_Objects.CreateGameObject("c3");
            c3.transform.SetParent(go.transform);
            IncrementalHierarchyFunctions.Build(new [] {go}, out m_Hierarchy, Allocator.Temp);
            AssertSize(4);
            AssertConsistency(go);

            var parentChanges = new NativeParallelHashMap<int, int>(0, Allocator.Temp);
            c3.transform.SetParent(c2.transform);
            parentChanges.Add(c3.GetInstanceID(), c2.GetInstanceID());
            var success = new NativeList<IncrementalBakingChanges.ParentChange>(Allocator.Temp);
            IncrementalHierarchyFunctions.ChangeParents(m_Hierarchy, parentChanges.GetKeyValueArrays(Allocator.Temp), default, success);
            AssertSize(4);
            AssertConsistency(go);
            parentChanges.Clear();

            c2.transform.SetParent(c1.transform);
            parentChanges.Add(c2.GetInstanceID(), c1.GetInstanceID());
            success.Clear();
            IncrementalHierarchyFunctions.ChangeParents(m_Hierarchy, parentChanges.GetKeyValueArrays(Allocator.Temp), default, success);
            AssertSize(4);
            AssertConsistency(go);
            parentChanges.Clear();
        }

        GameObject CreateRandomHierarchy(string prefix, float chance = .6f, int depth=1)
        {
            var root = new GameObject(prefix);
            if (depth >= 10)
                return root;
            while (Random.value < chance)
            {
                var child = CreateRandomHierarchy(prefix + root.transform.childCount, depth: depth + 1);
                child.transform.parent = root.transform;
            }
            return root;
        }

        GameObject FindRandomChild(GameObject go, float chance = .2f)
        {
            // heavily biased after a few steps down the hierarchy
            int n = go.transform.childCount;
            if (n == 0 || Random.value < chance) return go;
            int idx = Random.Range(0, n);
            return FindRandomChild(go.transform.GetChild(idx).gameObject);
        }

        static bool IsChild(GameObject parent, GameObject child)
        {
            Transform c = child.transform;
            while (c != null)
            {
                if (c == parent.transform)
                    return true;
                c = c.transform.parent;
            }

            return false;
        }



        [Test, Explicit]
        public void Hierarchy_FuzzTesting([Values(0, 1, 2, 3, 4, 5, 6, 7, 8, 9)] int seed)
        {
            Random.InitState(seed);
            var root = CreateRandomHierarchy("r");
            const int steps = 1000;
            IncrementalHierarchyFunctions.Build(new []{ root }, out m_Hierarchy, Allocator.Temp);
            AssertConsistency(root);
            // This has to be TempJob or RewindableAllocator because of DOTS-1357
            var instanceIds = new NativeList<int>(0, RwdAllocator.ToAllocator);
            var success = new NativeList<IncrementalBakingChanges.ParentChange>(0, RwdAllocator.ToAllocator);
            var reorder = new NativeParallelHashMap<int, int>(0, Allocator.Temp);
            var gos = new List<GameObject>();
            try
            {
                for (int i = 0; i < steps; i++)
                {
                    if (root == null)
                    {
                        AssertSize(0);
                        return;
                    }

                    float r = Random.value;
                    if (r < .4f)
                    {
                        // delete
                        var childrenToDelete = Random.Range(0, 7);
                        for (int c = 0; c < childrenToDelete; c++)
                        {
                            var go = FindRandomChild(root);
                            gos.Add(go);
                            instanceIds.Add(go.GetInstanceID());
                        }

                        IncrementalHierarchyFunctions.Remove(m_Hierarchy, instanceIds.AsArray());
                        foreach (var go in gos)
                            Object.DestroyImmediate(go);
                        instanceIds.Clear();
                        gos.Clear();
                        if (root != null)
                            AssertConsistency(root);
                    }
                    else if (r < .8f)
                    {
                        // add
                        var childrenToAdd = Random.Range(0, 3);
                        for (int c = 0; c < childrenToAdd; c++)
                        {
                            var randomChild = FindRandomChild(root);
                            var go = CreateRandomHierarchy(randomChild.name + "a");
                            go.transform.parent = randomChild.transform;
                            IncrementalHierarchyFunctions.AddRecurse(m_Hierarchy, go);
                            AssertConsistency(root);
                        }
                    }
                    else
                    {
                        // re-order
                        var childrenToReorder = Random.Range(0, 10);
                        for (int c = 0; c < childrenToReorder; c++)
                        {
                            var c1 = FindRandomChild(root);
                            var c2 = FindRandomChild(root);
                            if (c1 == c2 || c1 == root || IsChild(c1, c2))
                                continue;
                            c1.transform.parent = c2.transform;
                            reorder.Remove(c1.GetInstanceID());
                            reorder.Add(c1.GetInstanceID(), c2.GetInstanceID());
                        }

                        IncrementalHierarchyFunctions.ChangeParents(m_Hierarchy, reorder.GetKeyValueArrays(Allocator.Temp), default, success);
                        reorder.Clear();
                        success.Clear();
                        AssertConsistency(root);
                    }
                }
            }
            finally
            {
                instanceIds.Dispose();
                success.Dispose();
            }
        }

        static IEnumerable<int> GetInstanceIds(GameObject go)
        {
            var open = new Stack<Transform>();
            open.Push(go.transform);
            while (open.Count > 0)
            {
                var top = open.Pop();
                yield return top.gameObject.GetInstanceID();
                int n = top.childCount;
                for (int i = 0; i < n; i++)
                    open.Push(top.GetChild(i));
            }
        }

        [Test]
        public void Hierarchy_CollectChildInstanceIds_WithHierarchy()
        {
            var go = m_Objects.CreateGameObject("root");
            m_Objects.CreateGameObject("c1").transform.SetParent(go.transform);
            var c2 = m_Objects.CreateGameObject("c2");
            c2.transform.SetParent(go.transform);
            m_Objects.CreateGameObject("c3").transform.SetParent(go.transform);
            m_Objects.CreateGameObject("c21").transform.SetParent(c2.transform);
            m_Objects.CreateGameObject("c22").transform.SetParent(c2.transform);
            IncrementalHierarchyFunctions.Build(new [] {go}, out m_Hierarchy, RwdAllocator.ToAllocator);
            AssertConsistency(go);

            var changedIds = new NativeList<int>(1, RwdAllocator.ToAllocator);
            changedIds.Add(go.GetInstanceID());

            var visitedInstances = new NativeParallelHashSet<int>(6, RwdAllocator.ToAllocator);
            m_Hierarchy.AsReadOnly().CollectHierarchyInstanceIds(changedIds.AsArray(), visitedInstances);

            try
            {
                Assert.AreEqual(1, changedIds.Length);
                Assert.AreEqual(6, visitedInstances.Count());
                foreach (var id in GetInstanceIds(go))
                    visitedInstances.Contains(id);
            }
            finally
            {
                changedIds.Dispose();
                visitedInstances.Dispose();
            }
        }
    }
}
