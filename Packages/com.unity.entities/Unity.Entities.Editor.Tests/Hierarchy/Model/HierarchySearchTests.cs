using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;

namespace Unity.Entities.Editor.Tests
{
    class HierarchySearchTests
    {
        World m_World;
        HierarchyNameStore m_NameStore;
        HierarchySearch m_HierarchySearch;
        HierarchyNodeStore m_HierarchyNodeStore;

        [SetUp]
        public void SetUp()
        {
            m_World = new World(nameof(HierarchyNodeStoreTests));
            m_HierarchyNodeStore = new HierarchyNodeStore(Allocator.Persistent);
            m_NameStore = new HierarchyNameStore(Allocator.Persistent);
            m_HierarchySearch = new HierarchySearch(m_NameStore, Allocator.Persistent);
            m_HierarchySearch.ExcludeUnnamedNodes = true;
            m_NameStore.SetWorld(m_World);
            m_HierarchySearch.SetWorld(m_World);
        }

        [TearDown]
        public void TearDown()
        {
            m_World.Dispose();
            m_NameStore.Dispose();
            m_HierarchySearch.Dispose();
            m_HierarchyNodeStore.Dispose();
        }

        public static IEnumerable GetTestCases()
        {
            yield return new TestCaseData(new [] { "Token" }, new[] { "token" }).SetName("Tokens are converted to lowercase");
            yield return new TestCaseData(new [] { "BBBB", "D", "AAA", "CC", "AAAAA" }, new[] { "aaaaa", "bbbb", "cc", "d" }).SetName("Tokens are sorted by length and redundancies are removed");
            yield return new TestCaseData(new [] { "GameObject", "c=Rotation", "C=Translation" }, new[] { "gameobject" }).SetName("Component tokens are excluded");
            yield return new TestCaseData(new [] { "GameObject", "\"quoted\"" }, new[] { "gameobject", "quoted" }).SetName("Quote pairs are stripped");
            yield return new TestCaseData(new [] { "\"Game Object (22)\"" }, new[] { "game object (22)" }).SetName("Quoted sentences are preserved");
            yield return new TestCaseData(new [] { "GameObject", "\"\"" }, new[] { "gameobject" }).SetName("Empty quotes are stripped");
            yield return new TestCaseData(new [] { "GameObject", "\"" }, new[] { "gameobject" }).SetName("Unmatched quote characters are stripped");
            yield return new TestCaseData(new [] { "GameObject", "\"Hi", "Hello\"" }, new[] { "gameobject", "hello\"", "\"hi" }).SetName("Quote characters are not stripped when part of a valid token");
            yield return new TestCaseData(new [] { "c=Rotation", "\"", "\"\"" }, new string[0]).SetName("Returns an empty list if necessary");
        }

        [TestCaseSource(nameof(GetTestCases))]
        public void CreateHierarchyFilter_ExpectedTokens(string[] input, string[] expected)
        {
            using var filter = m_HierarchySearch.CreateHierarchyFilter(string.Empty, input, Allocator.Temp);

            // NOTE: NativeList does not implement GetEnumerator in a way that is understandable by the Assertion engine,
            // so we convert them to plain C# arrays before comparing them.
            Assert.That(filter.Tokens.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void CreateHierarchyFilter_DoesNotThrow_WhenTruncatingTokens()
        {
            // FixedString will throw an error if truncation occurs while copying data from a string.
            // No matter what happens, we don't want to throw when a search token is above the limit of characters.
            // If this is a problem, it should be handled somewhere else during validation (ideally in `SearchElement`).

            var longToken = new string('s', FixedString64Bytes.utf8MaxLengthInBytes + 1);
            var truncatedToken = new string('s', FixedString64Bytes.utf8MaxLengthInBytes);

            var input = new [] { longToken };
            var expectedOutput = new[] { truncatedToken };

            Assert.DoesNotThrow(() =>
            {
                using var filter = m_HierarchySearch.CreateHierarchyFilter(string.Empty, input, Allocator.Temp);
                Assert.That(filter.Tokens.ToArray(), Is.EqualTo(expectedOutput));
            });
        }

        [Test]
        public void CreateHierarchyFilter_DoesNotThrow_WhenTruncatingQuotedTokens()
        {
            // Quoted tokens go through a different path; we want to ensure we hit both for this case.

            var longToken = new string('s', FixedString64Bytes.utf8MaxLengthInBytes + 1);
            var truncatedToken = new string('s', FixedString64Bytes.utf8MaxLengthInBytes);

            var input = new [] { $"\"{longToken}\"" };
            var expectedOutput = new[] { $"{truncatedToken}" };

            Assert.DoesNotThrow(() =>
            {
                using var filter = m_HierarchySearch.CreateHierarchyFilter(string.Empty, input, Allocator.Temp);
                Assert.That(filter.Tokens.ToArray(), Is.EqualTo(expectedOutput));
            });
        }

        [Test]
        public void HierarchyFilter_IncludeParentSubScene()
        {
            var scene = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Scene, 1));
            var subScene = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.SubScene, 2), scene);
            var goA = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.GameObject, 3), subScene);
            var goB = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.GameObject, 4), subScene);
            var nestedSubScene = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.SubScene, 5), subScene);
            var goX = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.GameObject, 6), nestedSubScene);
            var goY = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.GameObject, 7), nestedSubScene);

            m_NameStore.SetName(scene, "Scene");
            m_NameStore.SetName(subScene, "SubScene");
            m_NameStore.SetName(goA, "GO A");
            m_NameStore.SetName(goB, "GO B");
            m_NameStore.SetName(nestedSubScene, "NestedSubScene");
            m_NameStore.SetName(goX, "GO X");
            m_NameStore.SetName(goY, "GO Y");

            m_HierarchyNodeStore.SetSortIndex(scene, 1);
            m_HierarchyNodeStore.SetSortIndex(subScene, 1);
            m_HierarchyNodeStore.SetSortIndex(goA, 1);
            m_HierarchyNodeStore.SetSortIndex(goB, 2);
            m_HierarchyNodeStore.SetSortIndex(nestedSubScene, 3);
            m_HierarchyNodeStore.SetSortIndex(goX, 1);
            m_HierarchyNodeStore.SetSortIndex(goY, 2);

            using var nodes = new HierarchyNodeStore.Immutable(Allocator.Persistent);
            m_HierarchyNodeStore.ExportImmutable(m_World, nodes);

            HierarchyTestHelpers.AssertImmutableIsSequenceEqualTo(nodes, new []
            {
                "0", // root
                "- 1", // scene
                "-- 2", // subScene
                "--- 3", // goA
                "--- 4", // goB
                "--- 5", // nestedSubScene
                "---- 6", // goX
                "---- 7", // goY
            });

            using var filterA = m_HierarchySearch.CreateHierarchyFilter(string.Empty, new[] { "A" }, Allocator.TempJob);
            using var bitMaskA = filterA.Apply(nodes, Allocator.TempJob);

            HierarchyTestHelpers.AssertFilteredImmutableIsSequenceEqualTo(nodes, bitMaskA, new HierarchyNodeHandle[]
            {
                subScene,
                goA
            });

            using var filterX = m_HierarchySearch.CreateHierarchyFilter(string.Empty, new[] { "X" }, Allocator.TempJob);
            using var bitMaskX = filterX.Apply(nodes, Allocator.TempJob);

            HierarchyTestHelpers.AssertFilteredImmutableIsSequenceEqualTo(nodes, bitMaskX, new HierarchyNodeHandle[]
            {
                nestedSubScene,
                goX
            });
        }

        struct TestComponentA : IComponentData {}
        struct TestComponentB : IComponentData, IEnableableComponent {}

        [Test]
        public void HierarchyFilter_EntityQueryDesc()
        {
            // Populate the world with some entities.
            var entEmpty1 = m_World.EntityManager.CreateEntity();
            var entEmpty2 = m_World.EntityManager.CreateEntity();
            var entA1 = m_World.EntityManager.CreateEntity(ComponentType.ReadOnly<TestComponentA>());
            var entA2 = m_World.EntityManager.CreateEntity(ComponentType.ReadOnly<TestComponentA>());
            var entA3 = m_World.EntityManager.CreateEntity(ComponentType.ReadOnly<TestComponentA>());
            var entAB1 = m_World.EntityManager.CreateEntity(ComponentType.ReadOnly<TestComponentA>(), ComponentType.ReadOnly<TestComponentB>());
            var entAB2 = m_World.EntityManager.CreateEntity(ComponentType.ReadOnly<TestComponentA>(), ComponentType.ReadOnly<TestComponentB>());
            var entAB3 = m_World.EntityManager.CreateEntity(ComponentType.ReadOnly<TestComponentA>(), ComponentType.ReadOnly<TestComponentB>());
            var entAB4 = m_World.EntityManager.CreateEntity(ComponentType.ReadOnly<TestComponentA>(), ComponentType.ReadOnly<TestComponentB>());
            // Disable the enableable component on a few entities. We still expect these entities to be matched by the hierarchy filter;
            // the idea would be to display them with the relevant components marked as disabled in the UI.
            m_World.EntityManager.SetComponentEnabled<TestComponentB>(entAB3, false);
            m_World.EntityManager.SetComponentEnabled<TestComponentB>(entAB4, false);
            var expectedWithA = new Entity[] { entA1, entA2, entA3, entAB1, entAB2, entAB3, entAB4 };
            var expectedWithB = new Entity[] { entAB1, entAB2, entAB3, entAB4 };

            // Integrate entity changes in to the dynamic hierarchy model.
            using var tracker = new HierarchyEntityChangeTracker(m_World, Allocator.Persistent);
            using var changes = tracker.GetChanges(m_World.UpdateAllocator.ToAllocator);
            using var sceneTagToSubSceneNodeHandle = new NativeParallelHashMap<SceneTag, HierarchyNodeHandle>(0, Allocator.TempJob);
            m_HierarchyNodeStore.IntegrateEntityChanges(m_World, changes, sceneTagToSubSceneNodeHandle);

            // Export the dynamic model to a baked linear set.
            using var immutable = new HierarchyNodeStore.Immutable(Allocator.Persistent);
            m_HierarchyNodeStore.ExportImmutable(m_World, immutable);

            // Create a virtualized view over the linear set.
            using var nodes = new HierarchyNodes(Allocator.Persistent);
            nodes.SetDataMode(DataMode.Disabled);

            // Empty virtual set.
            {
                Assert.That(nodes.Count, Is.EqualTo(0));
            }

            // Initial virtual set.
            {
                using var map = new NativeParallelHashMap<HierarchyNodeHandle, bool>(0, Allocator.TempJob);
                nodes.Refresh(immutable, m_World, map);
                Assert.That(nodes.Count, Is.EqualTo(0));
            }

            using (var filter = m_HierarchySearch.CreateHierarchyFilter("c=TestComponentA", null, Allocator.TempJob))
            {
                using var map = new NativeParallelHashMap<HierarchyNodeHandle, bool>(0, Allocator.TempJob);
                nodes.SetFilter(filter);
                nodes.Refresh(immutable, m_World, map);
                var actualWithA = new List<Entity>();
                for (int i = 0; i < nodes.Count; ++i)
                {
                    if (nodes[i].GetHandle().Kind == NodeKind.Entity)
                        actualWithA.Add(nodes[i].GetHandle().ToEntity());
                }
                CollectionAssert.AreEquivalent(expectedWithA, actualWithA);
            }

            using (var filter = m_HierarchySearch.CreateHierarchyFilter("c=TestComponentB", null, Allocator.TempJob))
            {
                using var map = new NativeParallelHashMap<HierarchyNodeHandle, bool>(0, Allocator.TempJob);
                nodes.SetFilter(filter);
                nodes.Refresh(immutable, m_World, map);
                var actualWithB = new List<Entity>();
                for (int i = 0; i < nodes.Count; ++i)
                {
                    if (nodes[i].GetHandle().Kind == NodeKind.Entity)
                        actualWithB.Add(nodes[i].GetHandle().ToEntity());
                }
                CollectionAssert.AreEquivalent(expectedWithB, actualWithB);
            }
        }
    }
}
