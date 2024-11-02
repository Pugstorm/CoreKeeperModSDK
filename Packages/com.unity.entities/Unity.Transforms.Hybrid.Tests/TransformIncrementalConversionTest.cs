using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Entities.Conversion;

namespace Unity.Transforms.Hybrid.Tests
{
    public class IncrementalConversionTests
    {
        AllocatorHelper<RewindableAllocator> m_AllocatorHelper;
        protected ref RewindableAllocator RwdAllocator => ref m_AllocatorHelper.Allocator;

        private IncrementalHierarchy m_Hierarchy;

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

        [TearDown]
        public void TearDown()
        {
            m_Hierarchy.Dispose();
            RwdAllocator.Rewind();
            // This is test only behavior for determinism.  Rewind twice such that all
            // tests start with an allocator containing only one memory block.
            RwdAllocator.Rewind();
        }

        static IEnumerable<int> GetInstanceIds(GameObject go)
        {
            var open = new Stack<UnityEngine.Transform>();
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
        public void Hierarchy_CollectChildIndices_WithHierarchy()
        {
            var go = new GameObject("root");
            new GameObject("c1").transform.SetParent(go.transform);
            var c2 = new GameObject("c2");
            c2.transform.SetParent(go.transform);
            new GameObject("c3").transform.SetParent(go.transform);
            new GameObject("c21").transform.SetParent(c2.transform);
            new GameObject("c22").transform.SetParent(c2.transform);
            IncrementalHierarchyFunctions.Build(new [] {go}, out m_Hierarchy, RwdAllocator.ToAllocator);

            var changedIds = new NativeList<int>(1, RwdAllocator.ToAllocator);
            changedIds.Add(go.GetInstanceID());

            var visitedIndices = new NativeParallelHashMap<int, bool>(6, RwdAllocator.ToAllocator);


            try
            {
                m_Hierarchy.AsReadOnly().CollectHierarchyInstanceIdsAndIndicesAsync(changedIds, visitedIndices).Complete();
                Assert.AreEqual(6, changedIds.Length);
                Assert.AreEqual(6, visitedIndices.Count());
                foreach (var id in GetInstanceIds(go))
                {
                    Assert.IsTrue(changedIds.Contains(id));
                    var index = m_Hierarchy.IndexByInstanceId[id];
                    bool success = visitedIndices.TryGetValue(index, out bool isInOriginalList);
                    Assert.IsTrue(success);
                    Assert.AreEqual(id == go.GetInstanceID(), isInOriginalList);
                }
            }
            finally
            {
                GameObject.DestroyImmediate(go);
            }
        }
    }
}
