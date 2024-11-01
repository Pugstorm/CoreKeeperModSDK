using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Editor.Tests
{
    [TestFixture]
    sealed class HierarchyNodeStoreTests
    {
        World m_World;
        HierarchyNodeStore m_HierarchyNodeStore;

        World World => m_World;

        [SetUp]
        public void SetUp()
        {
            m_World = new World(nameof(HierarchyNodeStoreTests));
            m_HierarchyNodeStore = new HierarchyNodeStore(Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            m_HierarchyNodeStore.Dispose();
            m_World.Dispose();
        }

        [Test]
        public void RootAlwaysExists()
        {
            using var hierarchy = new HierarchyNodeStore(World.UpdateAllocator.ToAllocator);
            Assert.That(hierarchy.Exists(HierarchyNodeHandle.Root));
            Assert.That(hierarchy.GetNode(HierarchyNodeHandle.Root).GetDepth(), Is.EqualTo(-1));
            Assert.That(hierarchy.GetNode(HierarchyNodeHandle.Root).GetChildCount(), Is.EqualTo(0));
        }

        [Test]
        public void NodeExists()
        {
            Assert.That(m_HierarchyNodeStore.Exists(default), Is.False);

            var node = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 1, version: 1));
            Assert.That(m_HierarchyNodeStore.Exists(node.GetHandle()), Is.True);
        }

        [Test]
        public void Node_WhenHandlesDoesNotExist_ThrowArgumentException()
        {
            var ex = Assert.Throws<ArgumentException>(() => new HierarchyNode(m_HierarchyNodeStore, new HierarchyNodeHandle(NodeKind.Entity, index: 1, version: 1)));
            Assert.That(ex.Message, Is.EqualTo($"Unable to create {nameof(HierarchyNodeHandle)}. The specified handle does not exist in the hierarchy."));
        }

        [Test]
        public void Clear()
        {
            Assert.That(m_HierarchyNodeStore.Exists(default), Is.False);

            var node = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 1, version: 1));
            Assert.That(m_HierarchyNodeStore.Exists(node.GetHandle()), Is.True);

            m_HierarchyNodeStore.Clear();

            Assert.That(m_HierarchyNodeStore.Exists(node.GetHandle()), Is.False);
            Assert.That(m_HierarchyNodeStore.Exists(HierarchyNodeHandle.Root));
        }

        [Test]
        public void GetRoot()
        {
            Assert.That(m_HierarchyNodeStore.GetRoot(), Is.EqualTo(m_HierarchyNodeStore.GetNode(HierarchyNodeHandle.Root)));
        }

        [Test]
        public void GetNode_WhenHandleIsUnknown_ThrowsInvalidOperationException()
        {
            var handle = new HierarchyNodeHandle(NodeKind.Entity, index: 1, version: 1);
            var ex = Assert.Throws<InvalidOperationException>(() => m_HierarchyNodeStore.GetNode(handle));
            Assert.That(ex.Message, Is.EqualTo($"The specified handle {handle} does not exist in the hierarchy."));
        }

        [Test]
        public void AddNode_WhenNodeIsRoot_ThrowsInvalidOperationException()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => { m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.Root); });
            Assert.That(ex.Message, Is.EqualTo($"Trying to add {nameof(HierarchyNodeHandle)} with {nameof(NodeKind)}.{nameof(NodeKind.Root)}. This is not allowed."));
        }

        [Test]
        public void AddNode_WhenNodeAlreadyExists_ThrowsInvalidOperationException()
        {
            var handle = new HierarchyNodeHandle(NodeKind.Entity, index: 1, version: 1);
            Assert.DoesNotThrow(() => m_HierarchyNodeStore.AddNode(handle));

            var ex = Assert.Throws<InvalidOperationException>(() => m_HierarchyNodeStore.AddNode(handle));
            Assert.That(ex.Message, Is.EqualTo($"The specified handle {handle} already exist in the hierarchy."));
        }

        [Test]
        public void AddNode_WhenParentDoesNotExist_ThrowsInvalidOperationException()
        {
            var parent = new HierarchyNodeHandle(NodeKind.Entity, index: 1, version: 1);
            var handle = new HierarchyNodeHandle(NodeKind.Entity, index: 2, version: 1);
            var ex = Assert.Throws<InvalidOperationException>(() => m_HierarchyNodeStore.AddNode(handle, parent));
            Assert.That(ex.Message, Is.EqualTo($"The specified handle {parent} does not exist in the hierarchy."));
        }

        [Test]
        public void AddNode_WhenNodeIsEntity_IsAddedCorrectly()
        {
            var node = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromEntity(new Entity {Index = 1, Version = 1}));
            Assert.That(m_HierarchyNodeStore.Exists(node), Is.EqualTo(true));
            Assert.That((HierarchyNodeHandle) node.GetParent(), Is.EqualTo(HierarchyNodeHandle.Root));
            Assert.That(node.GetDepth(), Is.EqualTo(0));

            var root = m_HierarchyNodeStore.GetNode(HierarchyNodeHandle.Root);
            Assert.That(root.GetChildCount(), Is.EqualTo(1));

            var children = root.GetChildren();
            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0], Is.EqualTo(node));
        }

        [Test]
        public void RemoveNode_WhenNodeIsRoot_ThrowsInvalidOperationException()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => m_HierarchyNodeStore.RemoveNode(HierarchyNodeHandle.Root));
            Assert.That(ex.Message, Is.EqualTo($"Trying to remove {nameof(HierarchyNodeHandle)} with {nameof(NodeKind)}.{nameof(NodeKind.Root)}. This is not allowed."));
        }

        [Test]
        public void RemoveNode_WhenNodeDoesNotExist_ThrowsInvalidOperationException()
        {
            var handle = HierarchyNodeHandle.FromEntity(new Entity { Index = 1, Version = 1 });
            var ex = Assert.Throws<InvalidOperationException>(() => m_HierarchyNodeStore.RemoveNode(handle));
            Assert.That(ex.Message, Is.EqualTo($"The specified {handle} does not exist in the hierarchy."));
        }

        [Test]
        public void RemoveNode_WhenNodeIsEntityWithNoChildren_IsRemovedCorrectly()
        {
            var node = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromEntity(new Entity {Index = 1, Version = 1}));
            var root = m_HierarchyNodeStore.GetNode(HierarchyNodeHandle.Root);
            Assert.That(m_HierarchyNodeStore.Exists(node), Is.EqualTo(true));
            Assert.That(root.GetChildCount(), Is.EqualTo(1));
            m_HierarchyNodeStore.RemoveNode(node);
            Assert.That(m_HierarchyNodeStore.Exists(node), Is.EqualTo(false));
            Assert.That(root.GetChildCount(), Is.EqualTo(0));
        }

        [Test]
        public void RemoveNode_WhenNodeIsNotEntityWithNoChildren_IsRemovedCorrectly()
        {
            var node = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromGameObject(1));
            var root = m_HierarchyNodeStore.GetNode(HierarchyNodeHandle.Root);
            Assert.That(m_HierarchyNodeStore.Exists(node), Is.EqualTo(true));
            Assert.That(root.GetChildCount(), Is.EqualTo(1));
            m_HierarchyNodeStore.RemoveNode(node);
            Assert.That(m_HierarchyNodeStore.Exists(node), Is.EqualTo(false));
            Assert.That(root.GetChildCount(), Is.EqualTo(0));
        }

        [Test]
        public void RemoveNode_WhenNodeIsEntityWithChildren_IsRemovedCorrectly()
        {
            var parent = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromEntity(new Entity {Index = 1, Version = 1}));
            var child = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromEntity(new Entity {Index = 2, Version = 1}), parent);

            Assert.That(m_HierarchyNodeStore.Exists(parent), Is.EqualTo(true));
            Assert.That(m_HierarchyNodeStore.Exists(child), Is.EqualTo(true));
            Assert.That(parent.GetChildCount(), Is.EqualTo(1));

            m_HierarchyNodeStore.RemoveNode(parent);
        }


        [Test]
        public void RemoveNode_WhenNodeIsNotEntityWithChildren_IsRemovedCorrectly()
        {
            var parent = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromGameObject(1));
            var child = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromGameObject(2), parent);

            Assert.That(m_HierarchyNodeStore.Exists(parent), Is.EqualTo(true));
            Assert.That(m_HierarchyNodeStore.Exists(child), Is.EqualTo(true));
            Assert.That(parent.GetChildCount(), Is.EqualTo(1));

            m_HierarchyNodeStore.RemoveNode(parent);
        }

        [Test]
        public void SetParent_WhenParentIsSameAsNode_ThrowsInvalidOperationException()
        {
            var handle = new HierarchyNodeHandle(NodeKind.Entity, index: 1, version: 1);
            m_HierarchyNodeStore.AddNode(handle);
            var ex = Assert.Throws<InvalidOperationException>(() => m_HierarchyNodeStore.SetParent(handle, handle));
            Assert.That(ex.Message, Is.EqualTo($"Trying to set the parent for {handle} as itself."));
        }

        [Test]
        public void SetParent_WhenParentDoesNotExist_ThrowsInvalidOperationException()
        {
            var handle = new HierarchyNodeHandle(NodeKind.Entity, index: 1, version: 1);
            var parent = new HierarchyNodeHandle(NodeKind.Entity, index: 2, version: 1);
            m_HierarchyNodeStore.AddNode(handle);
            var ex = Assert.Throws<InvalidOperationException>(() => m_HierarchyNodeStore.SetParent(handle, parent));
            Assert.That(ex.Message, Is.EqualTo($"The specified {handle} does not exist in the hierarchy."));
        }

        [Test]
        public void SetParent_WhenHandleDoesNotExist_ThrowsInvalidOperationException()
        {
            var handle = new HierarchyNodeHandle(NodeKind.Entity, index: 1, version: 1);
            var parent = new HierarchyNodeHandle(NodeKind.Entity, index: 2, version: 1);
            m_HierarchyNodeStore.AddNode(parent);
            var ex = Assert.Throws<InvalidOperationException>(() => m_HierarchyNodeStore.SetParent(handle, parent));
            Assert.That(ex.Message, Is.EqualTo($"The specified {handle} does not exist in the hierarchy."));
        }

        [Test]
        public void GetChildren_WhenHandleDoesNotExist_ThrowsInvalidOperationException()
        {
            var handle = new HierarchyNodeHandle(NodeKind.Entity, index: 1, version: 1);

            {
                var ex = Assert.Throws<InvalidOperationException>(() => m_HierarchyNodeStore.GetChildren(handle));
                Assert.That(ex.Message, Is.EqualTo($"The specified {handle} does not exist in the hierarchy."));
            }
            {
                var ex = Assert.Throws<InvalidOperationException>(() => m_HierarchyNodeStore.GetChildren(handle, new List<HierarchyNode>()));
                Assert.That(ex.Message, Is.EqualTo($"The specified {handle} does not exist in the hierarchy."));
            }
        }

        [Test]
        public void GetChildren_ClearsExistingList()
        {
            var a = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 1, version: 1));
            var b = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 2, version: 1), a);
            var c = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 3, version: 1), b);
            var list = new List<HierarchyNode> { b, c };

            m_HierarchyNodeStore.GetChildren(m_HierarchyNodeStore.GetRoot(), list);

            Assert.That(list, Is.EquivalentTo(new[] { a }));
        }

        [Test]
        public void SchedulePacking_WithNoNodes_DoesNotThrow()
        {
            using (var packed = new HierarchyNodeStore.Immutable(World.UpdateAllocator.ToAllocator))
            {
                m_HierarchyNodeStore.ExportImmutable(m_World, packed);
                Assert.That(packed.Count, Is.EqualTo(1));
            }
        }

        [Test]
        public void SchedulePacking_WithSingleNode_DoesNotThrow()
        {
            var handle = HierarchyNodeHandle.FromEntity(new Entity {Index = 1, Version = 1});
            m_HierarchyNodeStore.AddNode(handle);

            using (var packed = new HierarchyNodeStore.Immutable(World.UpdateAllocator.ToAllocator))
            {
                m_HierarchyNodeStore.ExportImmutable(m_World, packed);
                var node = packed.GetNode(handle);
                Assert.That(packed.Count, Is.EqualTo(2));
                Assert.That(node.GetDepth(), Is.EqualTo(0));
                Assert.That(node.GetChildCount(), Is.EqualTo(0));
            }
        }

        [Test]
        public void SchedulePacking_WithSimpleHierarchy_DoesNotThrow()
        {
            var index = 1;
            var a = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromEntity(new Entity {Index = index++, Version = 1}));
            var b = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromEntity(new Entity {Index = index++, Version = 1}));
            var c = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromEntity(new Entity {Index = index++, Version = 1}));
            var d = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromEntity(new Entity {Index = index++, Version = 1}));
            m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromEntity(new Entity {Index = index++, Version = 1}));

            a.AddChild(b);
            a.AddChild(c);
            c.AddChild(d);

            using (var packed = new HierarchyNodeStore.Immutable(World.UpdateAllocator.ToAllocator))
            {
                m_HierarchyNodeStore.ExportImmutable(m_World, packed);
                Assert.That(packed.Count, Is.EqualTo(6));
                Assert.That(packed.GetNode(a).GetChildCount(), Is.EqualTo(2));
                Assert.That(packed.GetNode(d).GetDepth(), Is.EqualTo(2));
            }
        }

        [Test]
        public void SchedulePacking_UpdatesDepth_WhenReusingNodesFromPreviousBuffer()
        {
            using var buffer1 = new HierarchyNodeStore.Immutable(Allocator.Persistent);
            using var buffer2 = new HierarchyNodeStore.Immutable(Allocator.Persistent);

            var a = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 4, version: 1));
            var b = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 3, version: 1));
            a.SetParent(b);

            m_HierarchyNodeStore.ExportImmutable(m_World, buffer1, buffer2);
            HierarchyTestHelpers.AssertImmutableIsSequenceEqualTo(buffer1, new[]
            {
                "0",
                "- 3",
                "-- 4"
            });

            var c = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 2, version: 1));
            b.SetParent(c);

            m_HierarchyNodeStore.ExportImmutable(m_World, buffer2, buffer1);
            HierarchyTestHelpers.AssertImmutableIsSequenceEqualTo(buffer2, new[]
            {
                "0",
                "- 2",
                "-- 3",
                "--- 4"
            });

            var d = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 1, version: 1));
            c.SetParent(d);

            m_HierarchyNodeStore.ExportImmutable(m_World, buffer1, buffer2);
            HierarchyTestHelpers.AssertImmutableIsSequenceEqualTo(buffer1, new[]
            {
                "0",
                "- 1",
                "-- 2",
                "--- 3",
                "---- 4"
            });
        }

        [Test]
        public void Parenting_ViaStore()
        {
            var parent = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 1, version: 1));
            var childA = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 2, version: 1));
            var childB = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 3, version: 1));

            Assert.That(m_HierarchyNodeStore.GetParent(parent), Is.EqualTo(HierarchyNodeHandle.Root));
            Assert.That(m_HierarchyNodeStore.GetParent(childA), Is.EqualTo(HierarchyNodeHandle.Root));
            Assert.That(m_HierarchyNodeStore.GetParent(childB), Is.EqualTo(HierarchyNodeHandle.Root));
            Assert.That(m_HierarchyNodeStore.GetChildren(parent), Is.Empty);
            Assert.That(m_HierarchyNodeStore.GetChildren(childA), Is.Empty);
            Assert.That(m_HierarchyNodeStore.GetChildren(childB), Is.Empty);
            Assert.That(m_HierarchyNodeStore.GetDepth(parent), Is.EqualTo(0));
            Assert.That(m_HierarchyNodeStore.GetDepth(childA), Is.EqualTo(0));
            Assert.That(m_HierarchyNodeStore.GetDepth(childB), Is.EqualTo(0));
            Assert.That(m_HierarchyNodeStore.GetChildCount(parent), Is.EqualTo(0));
            Assert.That(m_HierarchyNodeStore.GetChildCount(childA), Is.EqualTo(0));
            Assert.That(m_HierarchyNodeStore.GetChildCount(childB), Is.EqualTo(0));

            m_HierarchyNodeStore.SetParent(childA, parent);
            m_HierarchyNodeStore.SetParent(childB, parent);

            Assert.That(m_HierarchyNodeStore.GetParent(parent), Is.EqualTo(HierarchyNodeHandle.Root));
            Assert.That(m_HierarchyNodeStore.GetParent(childA), Is.EqualTo(parent.GetHandle()));
            Assert.That(m_HierarchyNodeStore.GetParent(childB), Is.EqualTo(parent.GetHandle()));
            Assert.That(m_HierarchyNodeStore.GetChildren(parent), Is.EquivalentTo(new[] { childA, childB }));
            Assert.That(m_HierarchyNodeStore.GetChildren(childA), Is.Empty);
            Assert.That(m_HierarchyNodeStore.GetChildren(childB), Is.Empty);
            Assert.That(m_HierarchyNodeStore.GetDepth(parent), Is.EqualTo(0));
            Assert.That(m_HierarchyNodeStore.GetDepth(childA), Is.EqualTo(1));
            Assert.That(m_HierarchyNodeStore.GetDepth(childB), Is.EqualTo(1));
            Assert.That(m_HierarchyNodeStore.GetChildCount(parent), Is.EqualTo(2));
            Assert.That(m_HierarchyNodeStore.GetChildCount(childA), Is.EqualTo(0));
            Assert.That(m_HierarchyNodeStore.GetChildCount(childB), Is.EqualTo(0));
        }

        [Test]
        public void Parenting_ViaNode()
        {
            var parent = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 1, version: 1));
            var childA = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 2, version: 1));
            var childB = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 3, version: 1));

            Assert.That(parent.GetParent(), Is.EqualTo(m_HierarchyNodeStore.GetNode(HierarchyNodeHandle.Root)));
            Assert.That(childA.GetParent(), Is.EqualTo(m_HierarchyNodeStore.GetNode(HierarchyNodeHandle.Root)));
            Assert.That(childB.GetParent(), Is.EqualTo(m_HierarchyNodeStore.GetNode(HierarchyNodeHandle.Root)));
            Assert.That(parent.GetChildren(), Is.Empty);
            Assert.That(childA.GetChildren(), Is.Empty);
            Assert.That(childB.GetChildren(), Is.Empty);
            Assert.That(parent.GetDepth(), Is.EqualTo(0));
            Assert.That(childA.GetDepth(), Is.EqualTo(0));
            Assert.That(childB.GetDepth(), Is.EqualTo(0));
            Assert.That(parent.GetChildCount(), Is.EqualTo(0));
            Assert.That(childA.GetChildCount(), Is.EqualTo(0));
            Assert.That(childB.GetChildCount(), Is.EqualTo(0));

            childA.SetParent(parent);
            childB.SetParent(parent);

            Assert.That(parent.GetParent(), Is.EqualTo(m_HierarchyNodeStore.GetNode(HierarchyNodeHandle.Root)));
            Assert.That(childA.GetParent(), Is.EqualTo(parent));
            Assert.That(childB.GetParent(), Is.EqualTo(parent));
            Assert.That(parent.GetChildren(), Is.EquivalentTo(new[] { childA, childB }));
            Assert.That(childA.GetChildren(), Is.Empty);
            Assert.That(childB.GetChildren(), Is.Empty);
            Assert.That(parent.GetDepth(), Is.EqualTo(0));
            Assert.That(childA.GetDepth(), Is.EqualTo(1));
            Assert.That(childB.GetDepth(), Is.EqualTo(1));
            Assert.That(parent.GetChildCount(), Is.EqualTo(2));
            Assert.That(childA.GetChildCount(), Is.EqualTo(0));
            Assert.That(childB.GetChildCount(), Is.EqualTo(0));
        }

        [Test]
        public void Parenting_WhenRemoveParent_MovesChildrenToRoot()
        {
            var grandparent = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 1, version: 1));
            var parent = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 2, version: 1), grandparent);
            var childA = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 3, version: 1), parent);
            var childB = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 4, version: 1), parent);

            Assert.That(childA.GetParent(), Is.EqualTo(parent));
            Assert.That(childB.GetParent(), Is.EqualTo(parent));
            Assert.That(parent.GetParent(), Is.EqualTo(grandparent));
            Assert.That(grandparent.GetParent(), Is.EqualTo(m_HierarchyNodeStore.GetNode(HierarchyNodeHandle.Root)));

            m_HierarchyNodeStore.RemoveNode(parent);

            Assert.That(m_HierarchyNodeStore.Exists(parent), Is.False);
            Assert.That(childA.GetParent(), Is.EqualTo(m_HierarchyNodeStore.GetNode(HierarchyNodeHandle.Root)));
            Assert.That(childB.GetParent(), Is.EqualTo(m_HierarchyNodeStore.GetNode(HierarchyNodeHandle.Root)));
            Assert.That(grandparent.GetParent(), Is.EqualTo(m_HierarchyNodeStore.GetNode(HierarchyNodeHandle.Root)));
        }

        [Test]
        public void Parenting_WhenRemoveParentWithRemoveChildrenRecursivelyFlag_RemovesAllChildren()
        {
            var grandparent = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromGameObject(1));
            var parent = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromGameObject(2), grandparent);
            var childA = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromGameObject(3), parent);
            var childB = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromGameObject(4), parent);

            Assert.That(childA.GetParent(), Is.EqualTo(parent));
            Assert.That(childB.GetParent(), Is.EqualTo(parent));
            Assert.That(parent.GetParent(), Is.EqualTo(grandparent));
            Assert.That(grandparent.GetParent(), Is.EqualTo(m_HierarchyNodeStore.GetNode(HierarchyNodeHandle.Root)));

            m_HierarchyNodeStore.RemoveNode(grandparent, removeChildrenRecursively: true);

            Assert.That(m_HierarchyNodeStore.Exists(grandparent), Is.False);
            Assert.That(m_HierarchyNodeStore.Exists(parent), Is.False);
            Assert.That(m_HierarchyNodeStore.Exists(childA), Is.False);
            Assert.That(m_HierarchyNodeStore.Exists(childB), Is.False);
        }

        [Test]
        public void Parenting_WhenAddingUnknownChildToNode_CreatesIt()
        {
            var parent = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 1, version: 1));
            var childHandle = new HierarchyNodeHandle(NodeKind.Entity, index: 2, version: 1);

            Assert.That(m_HierarchyNodeStore.Exists(childHandle), Is.False);
            Assert.DoesNotThrow(() => parent.AddChild(childHandle));
            Assert.That(m_HierarchyNodeStore.Exists(childHandle), Is.True);
            Assert.That(parent.GetChildren(), Is.EquivalentTo(new[] { m_HierarchyNodeStore.GetNode(childHandle) }));
        }

        [Test]
        public void Parenting_WhenNewNodesCreated_ChangeVersionIsPropagated()
        {
            Assert.That(m_HierarchyNodeStore.GetRootChangeVersion(), Is.EqualTo(0));
            var entityA = m_World.EntityManager.CreateEntity();
            var entityB = m_World.EntityManager.CreateEntity();
            var entityC = m_World.EntityManager.CreateEntity();
            var entityD = m_World.EntityManager.CreateEntity();

            var a = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromEntity(entityA));
            var b = m_HierarchyNodeStore.AddNode(HierarchyNodeHandle.FromEntity(entityB));

            Assert.That(m_HierarchyNodeStore.GetRootChangeVersion(), Is.EqualTo(1));

            // Exporting immutable hierarchy to bump the change version
            using var nodes = new HierarchyNodeStore.Immutable(Allocator.Persistent);
            m_HierarchyNodeStore.ExportImmutable(m_World, nodes);

            a.AddChild(HierarchyNodeHandle.FromEntity(entityC));
            b.AddChild(HierarchyNodeHandle.FromEntity(entityD));

            Assert.That(m_HierarchyNodeStore.GetRootChangeVersion(), Is.EqualTo(2));
        }

        [Test]
        public void Sorting_WhenRootSortingOrderChanged_SortingIsRespected()
        {
            var a = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 1, version: 1));
            var b = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 2, version: 1));
            var c = m_HierarchyNodeStore.AddNode(new HierarchyNodeHandle(NodeKind.Entity, index: 3, version: 1));

            a.SetSortIndex(3);
            b.SetSortIndex(2);
            c.SetSortIndex(1);

            Assert.That(m_HierarchyNodeStore.GetChildren(HierarchyNodeHandle.Root).SequenceEqual(new [] { c, b, a }));

            a.SetSortIndex(1);
            b.SetSortIndex(3);
            c.SetSortIndex(2);
            Assert.That(a.GetSortIndex(), Is.EqualTo(1));
            Assert.That(b.GetSortIndex(), Is.EqualTo(3));
            Assert.That(c.GetSortIndex(), Is.EqualTo(2));

            Assert.That(m_HierarchyNodeStore.GetChildren(HierarchyNodeHandle.Root).SequenceEqual(new [] { a, c, b }));
        }
    }
}
