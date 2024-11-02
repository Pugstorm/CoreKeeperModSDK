using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Editor.Tests
{
    class EntityDifferTests
    {
        World m_World;
        NativeList<Entity> m_CreatedEntitiesQueue;
        NativeList<Entity> m_DestroyedEntitiesQueue;
        EntityDiffer m_Differ;

        protected World World => m_World;

        [SetUp]
        public void Setup()
        {
            m_World = new World("TestWorld");
            m_CreatedEntitiesQueue = new NativeList<Entity>(World.UpdateAllocator.ToAllocator);
            m_DestroyedEntitiesQueue = new NativeList<Entity>(World.UpdateAllocator.ToAllocator);
            m_Differ = new EntityDiffer(m_World);
        }

        [TearDown]
        public void TearDown()
        {
            m_CreatedEntitiesQueue.Dispose();
            m_DestroyedEntitiesQueue.Dispose();
            m_Differ.Dispose();
            m_World.Dispose();
        }

        [Test]
        public void EntityDiffer_Simple()
        {
            var (created, destroyed) = GetEntityQueryMatchDiff(m_World.EntityManager.UniversalQuery);

            Assert.That(created, Is.Empty);
            Assert.That(destroyed, Is.Empty);
        }

        [Test]
        public unsafe void EntityDiffer_DetectMissingComponentWhenEntityDestroyed()
        {
            var entityA = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var entityB = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            using (var query = m_World.EntityManager.CreateEntityQuery(typeof(EcsTestData)))
            {
                GetEntityQueryMatchDiff(query);

                m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
                m_World.EntityManager.DestroyEntity(entityB);

                var (created, destroyed) = GetEntityQueryMatchDiff(query);
                Assert.That(created, Is.Empty);
                Assert.That(destroyed, Is.EquivalentTo(new[] { entityB }));
            }
        }

#if ENTITY_STORE_V1
        [Test]
        public void EntityDiffer_HandleGrowEntityManagerCapacity()
        {
            var initialCapacity = m_World.EntityManager.EntityCapacity;
            var archetype = m_World.EntityManager.CreateArchetype(typeof(EcsTestData));
            using (var entities = m_World.EntityManager.CreateEntity(archetype, initialCapacity + 1, World.UpdateAllocator.ToAllocator))
            {
                Assert.That(m_World.EntityManager.EntityCapacity, Is.GreaterThan(initialCapacity));

                using (var query = m_World.EntityManager.CreateEntityQuery(typeof(EcsTestData)))
                {
                    var (created, _) = GetEntityQueryMatchDiff(query);
                    Assert.That(created, Is.EquivalentTo(entities.ToArray()));
                }
            }
        }
#endif

        [Test]
        public unsafe void EntityDiffer_DetectEntityChangesReusingSameQuery()
        {
            var entityA = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            using (var query = m_World.EntityManager.CreateEntityQuery(typeof(EcsTestData)))
            {
                var (created, destroyed) = GetEntityQueryMatchDiff(query);

                Assert.That(created, Is.EquivalentTo(new[] { entityA }));
                Assert.That(destroyed, Is.Empty);

                m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();

                var entityB = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
                m_World.EntityManager.DestroyEntity(entityA);
                (created, destroyed) = GetEntityQueryMatchDiff(query);

                Assert.That(created, Is.EquivalentTo(new[] { entityB }));
                Assert.That(destroyed, Is.EquivalentTo(new[] { entityA }));
            }
        }

        [Test]
        public void EntityDiffer_DetectEntityChanges()
        {
            var entityA = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var entityB = m_World.EntityManager.CreateEntity(typeof(EcsTestData2));

            using (var query = m_World.EntityManager.CreateEntityQuery(typeof(EcsTestData)))
            {
                var (created, destroyed) = GetEntityQueryMatchDiff(query);

                Assert.That(created, Is.EquivalentTo(new[] { entityA }));
                Assert.That(destroyed, Is.Empty);
            }

            using (var query = m_World.EntityManager.CreateEntityQuery(typeof(EcsTestData2)))
            {
                var (created, destroyed) = GetEntityQueryMatchDiff(query);

                Assert.That(created, Is.EquivalentTo(new[] { entityB }));
                Assert.That(destroyed, Is.EquivalentTo(new[] { entityA }));
            }

            m_World.EntityManager.DestroyEntity(entityB);

            using (var query = m_World.EntityManager.CreateEntityQuery(typeof(EcsTestData2)))
            {
                var (created, destroyed) = GetEntityQueryMatchDiff(query);

                Assert.That(created, Is.Empty);
                Assert.That(destroyed, Is.EquivalentTo(new[] { entityB }));
            }
        }

        [Test]
        public void EntityDiffer_ReuseIndex()
        {
            var entityA = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var entityB = m_World.EntityManager.CreateEntity(typeof(EcsTestData2));
            using (var query = m_World.EntityManager.CreateEntityQuery(typeof(EcsTestData)))
            {
                var (created, destroyed) = GetEntityQueryMatchDiff(query);

                Assert.That(created, Is.EquivalentTo(new[] { entityA }));
                Assert.That(destroyed, Is.Empty);
            }

            m_World.EntityManager.DestroyEntity(entityA);
            var entityB2 = m_World.EntityManager.CreateEntity(typeof(EcsTestData2));
            Assert.That(entityB2.Index, Is.EqualTo(entityA.Index));

            using (var query = m_World.EntityManager.CreateEntityQuery(typeof(EcsTestData2)))
            {
                var (created, destroyed) = GetEntityQueryMatchDiff(query);
                Assert.That(created, Is.EquivalentTo(new[] { entityB, entityB2 }));
                Assert.That(destroyed, Is.EquivalentTo(new[] { entityA }));
            }
        }

        [Test]
        public unsafe void EntityDiffer_DetectMissingAndNewEntityWhenArchetypeChanged()
        {
            var entityA = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var entityB = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            var (created, destroyed) = GetEntityQueryMatchDiff(m_World.EntityManager.UniversalQuery);

            Assert.That(created, Is.EquivalentTo(new[] { entityA, entityB }));
            Assert.That(destroyed, Is.Empty);

            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            m_World.EntityManager.RemoveComponent<EcsTestData>(entityA);

            (created, destroyed) = GetEntityQueryMatchDiff(m_World.EntityManager.UniversalQuery);

            Assert.That(created, Is.EquivalentTo(new[] { entityA, entityB }));
            Assert.That(destroyed, Is.EquivalentTo(new[] { entityA, entityB }));
        }

        [Test]
        public unsafe void EntityDiffer_DetectMissingEntityWhenDestroyed()
        {
            var entityA = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var entityB = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            var (created, destroyed) = GetEntityQueryMatchDiff(m_World.EntityManager.UniversalQuery);

            Assert.That(created, Is.EquivalentTo(new[] { entityA, entityB }));
            Assert.That(destroyed, Is.Empty);

            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            m_World.EntityManager.DestroyEntity(entityA);

            (created, destroyed) = GetEntityQueryMatchDiff(m_World.EntityManager.UniversalQuery);

            Assert.That(created, Is.EquivalentTo(new[] { entityB }));
            Assert.That(destroyed, Is.EquivalentTo(new[] { entityA, entityB }));
        }

        [Test]
        public void EntityDiffer_MakeSureAllEntitiesAreProcessedWhenExecutedInDifferentBatches()
        {
            var entityA = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var entityB = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var entityC = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            using (var query = m_World.EntityManager.CreateEntityQuery(typeof(EcsTestData)))
            {
                var (created, destroyed) = GetEntityQueryMatchDiff(query);

                Assert.That(created, Is.EquivalentTo(new[] { entityA, entityB, entityC }));
                Assert.That(destroyed, Is.Empty);
            }
        }

        [Test]
        public void EntityDiffer_Test_BinarySearch1_NoFind()
        {
            var data = new Entities.EntityDiffer.PackedEntityGuidsCollection(10, Allocator.Temp);
            data.List.Add(new EntityGuid(0, 0, 0, 0));

            Assert.AreEqual(-1, data.BinarySearchRange(new EntityGuid(1, 0, 0, 0), 0, data.List.Length, 0));

            data.Dispose();
        }

        [Test]
        public void EntityDiffer_Test_BinarySearch1_FindFirst()
        {
            var data = new Entities.EntityDiffer.PackedEntityGuidsCollection(10, Allocator.Temp);
            data.List.Add(new EntityGuid(0, 0, 0, 0));

            Assert.AreEqual(0, data.BinarySearchRange(new EntityGuid(0, 0, 0, 0), 0, data.List.Length, 0));

            data.Dispose();
        }

        [Test]
        public void EntityDiffer_Test_BinarySearch1_Search2()
        {
            var data = new Entities.EntityDiffer.PackedEntityGuidsCollection(10, Allocator.Temp);
            data.List.Add(new EntityGuid(1, 0, 0, 0));
            data.List.Add(new EntityGuid(2, 0, 0, 0));
            data.List.Add(new EntityGuid(3, 0, 0, 0));

            Assert.AreEqual(1, data.BinarySearchRange(new EntityGuid(2, 0, 0, 0), 0, data.List.Length, 0));

            data.Dispose();
        }

        [Test]
        public void EntityDiffer_Test_BinarySearch1_Search4()
        {
            var data = new Entities.EntityDiffer.PackedEntityGuidsCollection(10, Allocator.Temp);
            data.List.Add(new EntityGuid(1, 0, 0, 0));
            data.List.Add(new EntityGuid(2, 0, 0, 0));
            data.List.Add(new EntityGuid(3, 0, 0, 0));
            data.List.Add(new EntityGuid(4, 0, 0, 0));
            data.List.Add(new EntityGuid(5, 0, 0, 0));
            data.List.Add(new EntityGuid(6, 0, 0, 0));

            Assert.AreEqual(3, data.BinarySearchRange(new EntityGuid(4, 0, 0, 0), 0, data.List.Length, 0));

            data.Dispose();
        }

        [Test]
        public void EntityDiffer_Test_BinarySearch2_Search()
        {
            var data = new Entities.EntityDiffer.PackedEntityGuidsCollection(10, Allocator.Temp);
            data.List.Add(new EntityGuid(1, 0, 0, 0));
            data.List.Add(new EntityGuid(2, 0, 0, 0));

            Assert.AreEqual(0, data.BinarySearchRange(new EntityGuid(1, 0, 0, 0), 0, data.List.Length, 2));

            data.Dispose();
        }

        [Test]
        public void EntityDiffer_Test_BinarySearch2_Search_BadHint()
        {
            var data = new Entities.EntityDiffer.PackedEntityGuidsCollection(10, Allocator.Temp);
            data.List.Add(new EntityGuid(1, 0, 0, 0));
            data.List.Add(new EntityGuid(2, 0, 0, 0));
            data.List.Add(new EntityGuid(3, 0, 0, 0));
            data.List.Add(new EntityGuid(4, 0, 0, 0));
            data.List.Add(new EntityGuid(5, 0, 0, 0));

            Assert.AreEqual(0, data.BinarySearchRange(new EntityGuid(1, 0, 0, 0), 0, data.List.Length, 5));

            data.Dispose();
        }

        [Test]
        public void EntityDiffer_Test_BinarySearch2_Search_NoFind()
        {
            var data = new Entities.EntityDiffer.PackedEntityGuidsCollection(10, Allocator.Temp);
            data.List.Add(new EntityGuid(1, 0, 0, 0));
            data.List.Add(new EntityGuid(2, 0, 0, 0));
            data.List.Add(new EntityGuid(3, 0, 0, 0));
            data.List.Add(new EntityGuid(4, 0, 0, 0));
            data.List.Add(new EntityGuid(5, 0, 0, 0));

            Assert.AreEqual(-1, data.BinarySearchRange(new EntityGuid(7, 0, 0, 0), 0, data.List.Length, 7));

            data.Dispose();
        }

        (Entity[] created, Entity[] destroyed) GetEntityQueryMatchDiff(EntityQuery query)
        {
            m_Differ.GetEntityQueryMatchDiffAsync(query, m_CreatedEntitiesQueue, m_DestroyedEntitiesQueue).Complete();
            using (var created = m_CreatedEntitiesQueue.ToArray(World.UpdateAllocator.ToAllocator))
            using (var destroyed = m_DestroyedEntitiesQueue.ToArray(World.UpdateAllocator.ToAllocator))
            {
                return (created.ToArray(), destroyed.ToArray());
            }
        }
    }
}
