using System;
using System.Collections;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Editor.Tests
{
    class HierarchyDataModeFilteringTests
    {
        World m_World;
        HierarchyNodeStore m_HierarchyNodeStore;

        [SetUp]
        public void SetUp()
        {
            m_World = new World(nameof(HierarchyNodeStoreTests));
            m_HierarchyNodeStore = new HierarchyNodeStore(Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            m_World.Dispose();
            m_HierarchyNodeStore.Dispose();
        }

        void BuildTestHierarchy(bool isSubSceneOpened, out HierarchyNodeStore.Immutable nodes, out NativeParallelHashSet<HierarchyNodeHandle> expandedNodes, out HierarchyNodeHandle subSceneNode, out HierarchyNodeHandle dynamicSubSceneNode)
        {
            var scene = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Scene, 1));
            subSceneNode = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.SubScene, 2), scene);
            m_HierarchyNodeStore.SetSortIndex(scene, 1);
            m_HierarchyNodeStore.SetSortIndex(subSceneNode, 1);
            if (isSubSceneOpened)
            {
                var goA = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.GameObject, 3), subSceneNode);
                var goB = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.GameObject, 4), subSceneNode);
                m_HierarchyNodeStore.SetSortIndex(goA, 1);
                m_HierarchyNodeStore.SetSortIndex(goB, 2);
            }

            var emptyArchetype = m_World.EntityManager.CreateArchetype();
            var entities = m_World.EntityManager.CreateEntity(emptyArchetype, 11, Allocator.Temp);

            // This test relies on entities having specific indices.
            // But it also requires those entities to effectively exists.
            // As long as it runs in an empty world, it should be fine.
            // The following loop makes sure of that.
            for (int i = 0; i < entities.Length; i++)
            {
                Assert.AreEqual(entities[i].Index, i);
            }

            var entityA = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromEntity(entities[5]), subSceneNode);
            var entityB = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromEntity(entities[6]), subSceneNode);
            var entityC = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromEntity(entities[7]));
            var entityD = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromEntity(entities[8]));
            dynamicSubSceneNode = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.SubScene, 9), HierarchyNodeHandle.Root);
            m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromEntity(entities[10]), dynamicSubSceneNode);
            m_HierarchyNodeStore.SetSortIndex(entityA, 3);
            m_HierarchyNodeStore.SetSortIndex(entityB, 4);
            m_HierarchyNodeStore.SetSortIndex(entityC, 2);
            m_HierarchyNodeStore.SetSortIndex(entityD, 3);

            nodes = new HierarchyNodeStore.Immutable(Allocator.TempJob);
            m_HierarchyNodeStore.ExportImmutable(m_World, nodes);

            if (isSubSceneOpened)
            {
                HierarchyTestHelpers.AssertImmutableIsSequenceEqualTo(nodes, new[]
                {
                    "0", // root
                    "- 1", // scene
                    "-- 2", // subScene
                    "--- 3", // goA
                    "--- 4", // goB
                    "--- 5", // entityA
                    "--- 6", // entityB
                    "- 7", // entityC
                    "- 8", // entityD
                    "- 9", // dynamic subScene
                    "-- 10", // loaded entity
                });
            }
            else
            {
                HierarchyTestHelpers.AssertImmutableIsSequenceEqualTo(nodes, new[]
                {
                    "0", // root
                    "- 1", // scene
                    "-- 2", // subScene
                    "--- 5", // entityA
                    "--- 6", // entityB
                    "- 7", // entityC
                    "- 8", // entityD
                    "- 9", // dynamic subScene
                    "-- 10", // loaded entity
                });
            }

            expandedNodes = new NativeParallelHashSet<HierarchyNodeHandle>(2, AllocatorManager.TempJob);
        }

        public static IEnumerable GetTestCases()
        {
            yield return new TestCaseData( /*isPlaymode*/false, /*isSubSceneOpened*/ true, DataMode.Authoring, new[]
            {
                " 1", // scene
                "- 2", // subScene
                "-- 3", // goA
                "-- 4", // goB
            }).SetName("BuildExpandedNodes_EditMode_OpenedSubScene_Authoring");
            yield return new TestCaseData( /*isPlaymode*/false, /*isSubSceneOpened*/ true, DataMode.Runtime, new []
            {
                " 1", // scene
                "- 2", // subScene
                "-- 5", // entityA
                "-- 6", // entityB
                " 7", // entityC
                " 8", // entityD
                " 9", // dynamic subScene
                "- 10", // loaded entity
            }).SetName("BuildExpandedNodes_EditMode_OpenedSubScene_Runtime");
            yield return new TestCaseData( /*isPlaymode*/false, /*isSubSceneOpened*/ false, DataMode.Authoring, new []
            {
                " 1", // scene
                "- 2", // subScene
                "-- 5", // entityA
                "-- 6", // entityB
            }).SetName("BuildExpandedNodes_EditMode_ClosedSubScene_Authoring");
            yield return new TestCaseData( /*isPlaymode*/false, /*isSubSceneOpened*/ false, DataMode.Runtime, new []
            {
                " 1", // scene
                "- 2", // subScene
                "-- 5", // entityA
                "-- 6", // entityB
                " 7", // entityC
                " 8", // entityD
                " 9", // dynamic subScene
                "- 10", // loaded entity
            }).SetName("BuildExpandedNodes_EditMode_ClosedSubScene_Runtime");

            yield return new TestCaseData( /*isPlaymode*/true, /*isSubSceneOpened*/ true, DataMode.Authoring,  new []
            {
                " 1", // scene
                "- 2", // subScene
                "-- 3", // goA
                "-- 4", // goB
            }).SetName("BuildExpandedNodes_PlayMode_OpenedSubScene_Authoring");
            yield return new TestCaseData( /*isPlaymode*/true, /*isSubSceneOpened*/ true, DataMode.Mixed, new[]
            {
                " 1", // scene
                "- 2", // subScene
                "-- 3", // goA
                "-- 4", // goB
                "-- 5", // entityA
                "-- 6", // entityB
                " 7", // entityC
                " 8", // entityD
                " 9", // dynamic subScene
                "- 10", // loaded entity
            }).SetName("BuildExpandedNodes_PlayMode_OpenedSubScene_Mixed");
            yield return new TestCaseData( /*isPlaymode*/true, /*isSubSceneOpened*/ true, DataMode.Runtime,  new []
            {
                " 1", // scene
                "- 2", // subScene
                "-- 5", // entityA
                "-- 6", // entityB
                " 7", // entityC
                " 8", // entityD
                " 9", // dynamic subScene
                "- 10", // loaded entity
            }).SetName("BuildExpandedNodes_PlayMode_OpenedSubScene_Runtime");
            yield return new TestCaseData( /*isPlaymode*/true, /*isSubSceneOpened*/ false, DataMode.Authoring,  new []
            {
                " 1", // scene
                "- 2", // subScene
                "-- 5", // entityA
                "-- 6", // entityB
            }).SetName("BuildExpandedNodes_PlayMode_ClosedSubScene_Authoring");
            yield return new TestCaseData( /*isPlaymode*/true, /*isSubSceneOpened*/ false, DataMode.Mixed, new []
            {
                " 1", // scene
                "- 2", // subScene
                "-- 5", // entityA
                "-- 6", // entityB
                " 7", // entityC
                " 8", // entityD
                " 9", // dynamic subScene
                "- 10", // loaded entity
            }).SetName("BuildExpandedNodes_PlayMode_ClosedSubScene_Mixed");
            yield return new TestCaseData( /*isPlaymode*/true, /*isSubSceneOpened*/ false, DataMode.Runtime, new []
            {
                " 1", // scene
                "- 2", // subScene
                "-- 5", // entityA
                "-- 6", // entityB
                " 7", // entityC
                " 8", // entityD
                " 9", // dynamic subScene
                "- 10", // loaded entity
            }).SetName("BuildExpandedNodes_PlayMode_ClosedSubScene_Runtime");
        }

        [Test, TestCaseSource(nameof(GetTestCases))]
        public unsafe void BuildExpandedNodes([Values] bool isPlaymode, [Values] bool isSubSceneOpened, [Values(DataMode.Authoring, DataMode.Mixed, DataMode.Runtime)] DataMode dataMode, string[] expectedNodes)
        {
            BuildTestHierarchy(isSubSceneOpened, out var nodes, out var expandedNodes, out var subSceneNode, out var dynamicSubSceneNode);
            var subSceneStateMap = new NativeParallelHashMap<HierarchyNodeHandle, bool>(1, AllocatorManager.TempJob);
            subSceneStateMap.Add(subSceneNode, isSubSceneOpened);
            subSceneStateMap.Add(dynamicSubSceneNode, false);

            var filteredNodes = new NativeList<int>(nodes.Count, AllocatorManager.TempJob);
            var filteredIndexByNode = new NativeList<int>(nodes.Count, AllocatorManager.TempJob);
            try
            {
                new HierarchyNodes.BuildExpandedNodes
                {
                    Hierarchy = nodes,
                    Expanded = expandedNodes,
                    SubSceneStateMap = subSceneStateMap,
                    DataMode = dataMode,
                    IsPlayMode = isPlaymode,
                    Nodes = filteredNodes,
                    IndexByNode = filteredIndexByNode,
                    Prefab = typeof(Prefab),
                    EntityGuid = typeof(EntityGuid),
                    DataAccess = m_World.EntityManager.GetCheckedEntityDataAccess()
                }.Run();

                var resultNodes = new string[filteredNodes.Length];
                for (var i = 0; i < filteredNodes.Length; i++)
                {
                    var node = nodes[filteredNodes[i]];
                    resultNodes[i] = $"{new string('-', node.Depth)} {node.Handle.Index}";
                }
                Assert.That(resultNodes, Is.EquivalentTo(expectedNodes));
            }
            finally
            {
                nodes.Dispose();
                expandedNodes.Dispose();
                subSceneStateMap.Dispose();
                filteredNodes.Dispose();
                filteredIndexByNode.Dispose();
            }
        }

        [Test]
        public unsafe void BuildExpandedNodes_PlayMode_OpenedSubScene_Mixed_WithConvertedGameObjectsAndPrefab()
        {
            // create entities and gameobjects to match entity guid to actual gameobject instance ids
            var subSceneGameObjects = new GameObject[3];
            var archetype = m_World.EntityManager.CreateArchetype(typeof(EntityGuid));
            using var subSceneEntities = m_World.EntityManager.CreateEntity(archetype, 5, Allocator.Temp);

            for (var i = 0; i < subSceneGameObjects.Length; i++)
            {
                subSceneGameObjects[i] = new GameObject();
                m_World.EntityManager.SetComponentData(subSceneEntities[i], new EntityGuid(subSceneGameObjects[i].GetInstanceID(), 0, 0, 0));
            }

            // create an entity not matching a gameobject
            m_World.EntityManager.SetComponentData(subSceneEntities[3], new EntityGuid(1, 0, 0, 0));
            // create a prefab entity
            m_World.EntityManager.AddComponent<Prefab>(subSceneEntities[4]);

            // create node hierarchy
            var scene = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Scene, 1));
            var subSceneNode = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.SubScene, 2), scene);
            m_HierarchyNodeStore.SetSortIndex(scene, 1);
            m_HierarchyNodeStore.SetSortIndex(subSceneNode, 1);
            for (var i = 0; i < subSceneGameObjects.Length; i++)
            {
                var go = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromGameObject(subSceneGameObjects[i]), subSceneNode);
                var e = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromEntity(subSceneEntities[i]), subSceneNode);
                m_HierarchyNodeStore.SetSortIndex(go, i);
                m_HierarchyNodeStore.SetSortIndex(e, i + 100);
            }
            var entityNotMatchingGo = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromEntity(subSceneEntities[3]), subSceneNode);
            m_HierarchyNodeStore.SetSortIndex(entityNotMatchingGo, 103);
            var prefabEntity = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromEntity(subSceneEntities[4]), subSceneNode);
            m_HierarchyNodeStore.SetSortIndex(prefabEntity, 104);

            var nodes = new HierarchyNodeStore.Immutable(Allocator.TempJob);
            m_HierarchyNodeStore.ExportImmutable(m_World, nodes);

            // ensure the exported hierarchy is the one expected
            HierarchyTestHelpers.AssertImmutableIsSequenceEqualTo(nodes, new[]
            {
                "0", // root
                "- 1 ", // scene
                "-- 2", // subScene
                $"--- {subSceneGameObjects[0].GetInstanceID()}", // go
                $"--- {subSceneGameObjects[1].GetInstanceID()}", // go
                $"--- {subSceneGameObjects[2].GetInstanceID()}", // go
                $"--- {subSceneEntities[0].Index}", // e
                $"--- {subSceneEntities[1].Index}", // e
                $"--- {subSceneEntities[2].Index}", // e
                $"--- {subSceneEntities[3].Index}", // not matching go
                $"--- {subSceneEntities[4].Index}", // prefab
            });

            // filter and assert filtered
            var subSceneStateMap = new NativeParallelHashMap<HierarchyNodeHandle, bool>(1, AllocatorManager.TempJob);
            subSceneStateMap.Add(subSceneNode, true);
            var expandedNodes = new NativeParallelHashSet<HierarchyNodeHandle>(1, AllocatorManager.TempJob);
            var filteredNodes = new NativeList<int>(nodes.Count, AllocatorManager.TempJob);
            var filteredIndexByNode = new NativeList<int>(nodes.Count, AllocatorManager.TempJob);
            try
            {
                new HierarchyNodes.BuildExpandedNodes
                {
                    Hierarchy = nodes,
                    Expanded = expandedNodes,
                    SubSceneStateMap = subSceneStateMap,
                    DataMode = DataMode.Mixed,
                    IsPlayMode = true,
                    Nodes = filteredNodes,
                    IndexByNode = filteredIndexByNode,
                    Prefab = typeof(Prefab),
                    EntityGuid = typeof(EntityGuid),
                    DataAccess = m_World.EntityManager.GetCheckedEntityDataAccess()
                }.Run();

                var resultNodes = new string[filteredNodes.Length];
                for (var i = 0; i < filteredNodes.Length; i++)
                {
                    var node = nodes[filteredNodes[i]];
                    resultNodes[i] = $"{new string('-', node.Depth)} {node.Handle.Index}";
                }
                Assert.That(resultNodes, Is.EquivalentTo(new[]
                {
                    " 1", // scene
                    "- 2", // subScene
                    $"-- {subSceneGameObjects[0].GetInstanceID()}", // go
                    $"-- {subSceneGameObjects[1].GetInstanceID()}", // go
                    $"-- {subSceneGameObjects[2].GetInstanceID()}", // go
                    $"-- {subSceneEntities[3].Index}", // go not matching go in subscene
                }));
            }
            finally
            {
                nodes.Dispose();
                expandedNodes.Dispose();
                subSceneStateMap.Dispose();
                filteredNodes.Dispose();
                filteredIndexByNode.Dispose();
            }
        }
    }
}
