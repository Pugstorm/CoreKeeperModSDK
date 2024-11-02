using System;
using NUnit.Framework;
using Unity.Collections;
using System.Collections.Generic;

namespace Unity.Entities.Tests
{
    class IterationTests : ECSTestsFixture
    {
        [Test]
        public void CreateEntityQuery()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2));
            Assert.AreEqual(0, query.CalculateEntityCount());

            var entity = m_Manager.CreateEntity(archetype);
            m_Manager.SetComponentData(entity, new EcsTestData(42));
            var arr = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(1, arr.Length);
            Assert.AreEqual(42, arr[0].value);

            m_Manager.DestroyEntity(entity);
        }

        struct TempComponentNeverInstantiated : IComponentData
        {
            private int m_Internal;
        }

        [Test]
        public void IterateEmptyArchetype()
        {
            var query = m_Manager.CreateEntityQuery(typeof(TempComponentNeverInstantiated));
            Assert.AreEqual(0, query.CalculateEntityCount());

            var archetype = m_Manager.CreateArchetype(typeof(TempComponentNeverInstantiated));
            Assert.AreEqual(0, query.CalculateEntityCount());

            Entity ent = m_Manager.CreateEntity(archetype);
            Assert.AreEqual(1, query.CalculateEntityCount());
            m_Manager.DestroyEntity(ent);
            Assert.AreEqual(0, query.CalculateEntityCount());
        }

        [Test]
        public void IterateChunkedEntityQuery()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            Assert.AreEqual(0, query.CalculateEntityCount());

            var entitiesA = m_Manager.CreateEntity(archetype1, 5000, m_Manager.World.UpdateAllocator.ToAllocator);
            var entitiesB = m_Manager.CreateEntity(archetype2, 5000, m_Manager.World.UpdateAllocator.ToAllocator);
            for (int i = 0; i < entitiesA.Length; i++)
            {
                m_Manager.SetComponentData(entitiesA[i], new EcsTestData(i));
            }
            for (int i = 0; i < entitiesB.Length; i++)
            {
                m_Manager.SetComponentData(entitiesB[i], new EcsTestData(i + entitiesA.Length));
            }

            var arr = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(entitiesA.Length + entitiesB.Length, arr.Length);
            Dictionary<int, bool> values = new Dictionary<int, bool>();    // Tiny doesn't support HashSet, does support Dictionary
            for (int i = 0; i < arr.Length; i++)
            {
                int val = arr[i].value;
                FastAssert.IsFalse(values.ContainsKey(i));
                FastAssert.IsTrue(val >= 0);
                FastAssert.IsTrue(val < entitiesA.Length + entitiesB.Length);
                values.Add(i, true);
            }
            m_Manager.DestroyEntity(entitiesA);
            m_Manager.DestroyEntity(entitiesB);
        }

        [Test]
        public void IterateChunkedEntityQueryBackwards()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            Assert.AreEqual(0, query.CalculateEntityCount());

            var entitiesA = m_Manager.CreateEntity(archetype1, 5000, m_Manager.World.UpdateAllocator.ToAllocator);
            var entitiesB = m_Manager.CreateEntity(archetype2, 5000, m_Manager.World.UpdateAllocator.ToAllocator);
            for (int i = 0; i < entitiesA.Length; i++)
            {
                m_Manager.SetComponentData(entitiesA[i], new EcsTestData(i));
            }
            for (int i = 0; i < entitiesB.Length; i++)
            {
                m_Manager.SetComponentData(entitiesB[i], new EcsTestData(i + entitiesA.Length));
            }

            var arr = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(entitiesA.Length + entitiesB.Length, arr.Length);
            Dictionary<int, bool> values = new Dictionary<int, bool>();    // Tiny doesn't support HashSet, does support Dictionary
            for (int i = 0; i < arr.Length; ++i)
            {
                int val = arr[i].value;
                FastAssert.IsFalse(values.ContainsKey(i));
                FastAssert.IsTrue(val >= 0);
                FastAssert.IsTrue(val < entitiesA.Length + entitiesB.Length);
                values.Add(i, true);
            }
            m_Manager.DestroyEntity(entitiesA);
            m_Manager.DestroyEntity(entitiesB);
        }

        [Test]
        public void IterateChunkedEntityQueryAfterDestroy()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            Assert.AreEqual(0, query.CalculateEntityCount());

            Entity[] entities = new Entity[10000];
            for (int i = 0; i < entities.Length / 2; i++)
            {
                entities[i] = m_Manager.CreateEntity(archetype1);
                m_Manager.SetComponentData(entities[i], new EcsTestData(i));
            }
            for (int i = entities.Length / 2; i < entities.Length; i++)
            {
                entities[i] = m_Manager.CreateEntity(archetype2);
                m_Manager.SetComponentData(entities[i], new EcsTestData(i));
            }
            for (int i = 0; i < entities.Length; i++)
            {
                if (i % 2 != 0)
                {
                    m_Manager.DestroyEntity(entities[i]);
                }
            }

            var arr = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(entities.Length / 2, arr.Length);
            Dictionary<int, bool> values = new Dictionary<int, bool>();    // Tiny doesn't support HashSet, does support Dictionary
            for (int i = 0; i < arr.Length; i++)
            {
                int val = arr[i].value;
                FastAssert.IsFalse(values.ContainsKey(i));
                FastAssert.IsTrue(val >= 0);
                FastAssert.IsTrue(val % 2 == 0);
                FastAssert.IsTrue(val < entities.Length);
                values.Add(i, true);
            }

            for (int i = entities.Length / 2; i < entities.Length; i++)
            {
                if (i % 2 == 0)
                    m_Manager.RemoveComponent<EcsTestData>(entities[i]);
            }
            arr = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(entities.Length / 4, arr.Length);
            values = new Dictionary<int, bool>();
            for (int i = 0; i < arr.Length; i++)
            {
                int val = arr[i].value;
                FastAssert.IsFalse(values.ContainsKey(i));
                FastAssert.IsTrue(val >= 0);
                FastAssert.IsTrue(val % 2 == 0);
                FastAssert.IsTrue(val < entities.Length / 2);
                values.Add(i, true);
            }

            for (int i = 0; i < entities.Length; i++)
            {
                if (i % 2 == 0)
                    m_Manager.DestroyEntity(entities[i]);
            }
        }

        [Test]
        public void QueryCopyFromNativeArray()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var entities = new NativeArray<Entity>(10, Allocator.Persistent);
            m_Manager.CreateEntity(archetype, entities);

            var dataToCopyA = new NativeArray<EcsTestData>(10, Allocator.Persistent);
            var dataToCopyB = new NativeArray<EcsTestData>(5, Allocator.Persistent);

            for (int i = 0; i < dataToCopyA.Length; ++i)
            {
                dataToCopyA[i] = new EcsTestData { value = 2 };
            }

            for (int i = 0; i < dataToCopyB.Length; ++i)
            {
                dataToCopyA[i] = new EcsTestData { value = 3 };
            }

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            query.CopyFromComponentDataArray(dataToCopyA);

            for (int i = 0; i < dataToCopyA.Length; ++i)
            {
                Assert.AreEqual(m_Manager.GetComponentData<EcsTestData>(entities[i]).value, dataToCopyA[i].value);
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.Throws<ArgumentException>(() => { query.CopyFromComponentDataArray(dataToCopyB); });
#endif

            query.Dispose();
            entities.Dispose();
            dataToCopyA.Dispose();
            dataToCopyB.Dispose();
        }

        [Test]
        public void EntityQueryFilteredEntityIndexWithMultipleArchetypes()
        {
            var archetypeA = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));
            var archetypeB = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp));

            var entity1A = m_Manager.CreateEntity(archetypeA);
            var entity2A = m_Manager.CreateEntity(archetypeA);
            var entityB  = m_Manager.CreateEntity(archetypeB);

            m_Manager.SetSharedComponentManaged(entity1A, new EcsTestSharedComp { value = 1});
            m_Manager.SetSharedComponentManaged(entity2A, new EcsTestSharedComp { value = 2});

            m_Manager.SetSharedComponentManaged(entityB, new EcsTestSharedComp { value = 1});

            query.SetSharedComponentFilterManaged(new EcsTestSharedComp {value = 1});

            Assert.AreEqual(2, query.CalculateEntityCount());

            using var chunks = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(archetypeA, chunks[0].Archetype);
            Assert.AreEqual(archetypeB, chunks[1].Archetype);

            query.Dispose();
        }

        [Test]
        public void EntityQueryFilteredChunkCount()
        {
            var archetypeA = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp));

            for (int i = 0; i < archetypeA.ChunkCapacity * 2; ++i)
            {
                var entityA = m_Manager.CreateEntity(archetypeA);
                m_Manager.SetSharedComponentManaged(entityA, new EcsTestSharedComp { value = 1});
            }

            var entityB  = m_Manager.CreateEntity(archetypeA);
            m_Manager.SetSharedComponentManaged(entityB, new EcsTestSharedComp { value = 2});

            query.SetSharedComponentFilterManaged(new EcsTestSharedComp {value = 1});
            Assert.AreEqual(2, query.CalculateChunkCount());
            Assert.AreEqual(2 * archetypeA.ChunkCapacity, query.CalculateEntityCount());

            query.SetSharedComponentFilterManaged(new EcsTestSharedComp {value = 2});
            Assert.AreEqual(1, query.CalculateChunkCount());
            Assert.AreEqual(1, query.CalculateEntityCount());

            query.Dispose();
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void CreateEntityQuery_ManagedComponents()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestManagedComponent));

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestManagedComponent));
            Assert.AreEqual(0, query.CalculateEntityCount());

            var entity = m_Manager.CreateEntity(archetype);

            m_Manager.SetComponentData(entity, new EcsTestData(42));
            var arr = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(1, arr.Length);
            Assert.AreEqual(42, arr[0].value);

            m_Manager.SetComponentData(entity, new EcsTestManagedComponent() { value = "SomeString" });
            var classArr = query.ToComponentDataArray<EcsTestManagedComponent>();
            Assert.AreEqual(1, classArr.Length);
            Assert.AreEqual("SomeString", classArr[0].value);

            m_Manager.DestroyEntity(entity);
        }

        [Test]
        public void IterateChunkedEntityQuery_ManagedComponents()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestManagedComponent));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestManagedComponent));

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestManagedComponent));
            Assert.AreEqual(0, query.CalculateEntityCount());

            Entity[] entities = new Entity[10000];
            for (int i = 0; i < entities.Length / 2; i++)
            {
                entities[i] = m_Manager.CreateEntity(archetype1);
                m_Manager.SetComponentData(entities[i], new EcsTestData(i));
                m_Manager.SetComponentData(entities[i], new EcsTestManagedComponent() { value = i.ToString() });
            }
            for (int i = entities.Length / 2; i < entities.Length; i++)
            {
                entities[i] = m_Manager.CreateEntity(archetype2);
                m_Manager.SetComponentData(entities[i], new EcsTestData(i));
                m_Manager.SetComponentData(entities[i], new EcsTestManagedComponent() { value = i.ToString() });
            }

            var arr = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
            var classArr = query.ToComponentDataArray<EcsTestManagedComponent>();
            Assert.AreEqual(entities.Length, arr.Length);
            Assert.AreEqual(entities.Length, classArr.Length);
            HashSet<int> values = new HashSet<int>();
            HashSet<string> classValues = new HashSet<string>();
            for (int i = 0; i < arr.Length; i++)
            {
                int val = arr[i].value;
                FastAssert.IsFalse(values.Contains(i));
                FastAssert.IsTrue(val >= 0);
                FastAssert.IsTrue(val < entities.Length);
                values.Add(i);

                string classVal = classArr[i].value;
                FastAssert.IsFalse(classValues.Contains(i.ToString()));
                FastAssert.IsTrue(classVal != null);
                FastAssert.IsTrue(classVal != new EcsTestManagedComponent().value);
                classValues.Add(i.ToString());
            }

            for (int i = 0; i < entities.Length; i++)
                m_Manager.DestroyEntity(entities[i]);
        }

        [Test]
        public void IterateChunkedEntityQueryBackwards_ManagedComponents()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestManagedComponent));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestManagedComponent));

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestManagedComponent));
            Assert.AreEqual(0, query.CalculateEntityCount());

            Entity[] entities = new Entity[10000];
            for (int i = 0; i < entities.Length / 2; i++)
            {
                entities[i] = m_Manager.CreateEntity(archetype1);
                m_Manager.SetComponentData(entities[i], new EcsTestData(i));
                m_Manager.SetComponentData(entities[i], new EcsTestManagedComponent() { value = i.ToString() });
            }
            for (int i = entities.Length / 2; i < entities.Length; i++)
            {
                entities[i] = m_Manager.CreateEntity(archetype2);
                m_Manager.SetComponentData(entities[i], new EcsTestData(i));
                m_Manager.SetComponentData(entities[i], new EcsTestManagedComponent() { value = i.ToString() });
            }

            var arr = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
            var classArr = query.ToComponentDataArray<EcsTestManagedComponent>();
            Assert.AreEqual(entities.Length, arr.Length);
            Assert.AreEqual(entities.Length, classArr.Length);
            HashSet<int> values = new HashSet<int>();
            HashSet<string> classValues = new HashSet<string>();
            for (int i = 0; i < arr.Length; ++i)
            {
                int val = arr[i].value;
                FastAssert.IsFalse(values.Contains(i));
                FastAssert.IsTrue(val >= 0);
                FastAssert.IsTrue(val < entities.Length);
                values.Add(i);

                string classVal = classArr[i].value;
                FastAssert.IsFalse(classValues.Contains(i.ToString()));
                FastAssert.IsTrue(classVal != null);
                FastAssert.IsTrue(classVal != new EcsTestManagedComponent().value);
                classValues.Add(i.ToString());
            }

            for (int i = 0; i < entities.Length; i++)
                m_Manager.DestroyEntity(entities[i]);
        }

        [Test]
        public void IterateChunkedEntityQueryAfterDestroy_ManagedComponents()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestManagedComponent));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestManagedComponent));

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestManagedComponent));
            Assert.AreEqual(0, query.CalculateEntityCount());

            Entity[] entities = new Entity[10000];
            for (int i = 0; i < entities.Length / 2; i++)
            {
                entities[i] = m_Manager.CreateEntity(archetype1);
                m_Manager.SetComponentData(entities[i], new EcsTestData(i));
                m_Manager.SetComponentData(entities[i], new EcsTestManagedComponent() { value = i.ToString() });
            }
            for (int i = entities.Length / 2; i < entities.Length; i++)
            {
                entities[i] = m_Manager.CreateEntity(archetype2);
                m_Manager.SetComponentData(entities[i], new EcsTestData(i));
                m_Manager.SetComponentData(entities[i], new EcsTestManagedComponent() { value = i.ToString() });
            }
            for (int i = 0; i < entities.Length; i++)
            {
                if (i % 2 != 0)
                {
                    m_Manager.DestroyEntity(entities[i]);
                }
            }

            var arr = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
            var classArr = query.ToComponentDataArray<EcsTestManagedComponent>();
            Assert.AreEqual(entities.Length / 2, arr.Length);
            Assert.AreEqual(entities.Length / 2, classArr.Length);

            HashSet<int> values = new HashSet<int>();
            HashSet<string> classValues = new HashSet<string>();
            for (int i = 0; i < arr.Length; i++)
            {
                int val = arr[i].value;
                FastAssert.IsFalse(values.Contains(i));
                FastAssert.IsTrue(val >= 0);
                FastAssert.IsTrue(val % 2 == 0);
                FastAssert.IsTrue(val < entities.Length);
                values.Add(i);

                string classVal = classArr[i].value;
                FastAssert.IsFalse(classValues.Contains(i.ToString()));
                FastAssert.IsTrue(classVal != null);
                FastAssert.IsTrue(classVal != new EcsTestManagedComponent().value);
                classValues.Add(i.ToString());
            }

            for (int i = entities.Length / 2; i < entities.Length; i++)
            {
                if (i % 2 == 0)
                    m_Manager.RemoveComponent<EcsTestData>(entities[i]);
            }
            arr = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
            classArr = query.ToComponentDataArray<EcsTestManagedComponent>();
            Assert.AreEqual(entities.Length / 4, arr.Length);
            Assert.AreEqual(entities.Length / 4, classArr.Length);
            values = new HashSet<int>();
            classValues = new HashSet<string>();
            for (int i = 0; i < arr.Length; i++)
            {
                int val = arr[i].value;
                FastAssert.IsFalse(values.Contains(i));
                FastAssert.IsTrue(val >= 0);
                FastAssert.IsTrue(val % 2 == 0);
                FastAssert.IsTrue(val < entities.Length / 2);
                values.Add(i);

                string classVal = classArr[i].value;
                FastAssert.IsFalse(classValues.Contains(i.ToString()));
                FastAssert.IsTrue(classVal != null);
                FastAssert.IsTrue(classVal != new EcsTestManagedComponent().value);
                classValues.Add(i.ToString());
            }

            for (int i = 0; i < entities.Length; i++)
            {
                if (i % 2 == 0)
                    m_Manager.DestroyEntity(entities[i]);
            }
        }

        [Test]
        public void EntityQueryFilteredChunkCount_ManagedComponents()
        {
            var archetypeA = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestManagedComponent), typeof(EcsTestSharedComp));

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestManagedComponent), typeof(EcsTestSharedComp));

            for (int i = 0; i < archetypeA.ChunkCapacity * 2; ++i)
            {
                var entityA = m_Manager.CreateEntity(archetypeA);
                m_Manager.SetSharedComponentManaged(entityA, new EcsTestSharedComp { value = 1 });
            }

            var entityB = m_Manager.CreateEntity(archetypeA);
            m_Manager.SetSharedComponentManaged(entityB, new EcsTestSharedComp { value = 2 });

            query.SetSharedComponentFilterManaged(new EcsTestSharedComp {value = 1});
            Assert.AreEqual(2*archetypeA.ChunkCapacity, query.CalculateEntityCount());

            query.SetSharedComponentFilterManaged(new EcsTestSharedComp {value = 2});
            Assert.AreEqual(1, query.CalculateEntityCount());

            query.Dispose();
        }
#endif
    }
}
