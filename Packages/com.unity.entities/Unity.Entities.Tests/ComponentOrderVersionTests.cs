using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Tests
{
    class ComponentOrderVersionTests : ECSTestsFixture
    {
        int oddTestValue = 34;
        int evenTestValue = 17;

        void AddEvenOddTestData()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var evenShared = new SharedData1(evenTestValue);
            var oddShared = new SharedData1(oddTestValue);
            for (int i = 0; i < 100; i++)
            {
                Entity e = m_Manager.CreateEntity(archetype);
                var testData = m_Manager.GetComponentData<EcsTestData>(e);
                testData.value = i;
                m_Manager.SetComponentData(e, testData);
                if ((i & 0x01) == 0)
                {
                    m_Manager.AddSharedComponent(e, evenShared);
                }
                else
                {
                    m_Manager.AddSharedComponent(e, oddShared);
                }
            }
        }

        void ActionEvenOdd(Action<int, EntityQuery> even, Action<int, EntityQuery> odd)
        {
            var group = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(SharedData1));
            group.CompleteDependency();

            m_Manager.GetAllUniqueSharedComponents<SharedData1>(out var uniqueValues, Allocator.Temp);

            for (int sharedIndex = 0; sharedIndex != uniqueValues.Length; sharedIndex++)
            {
                var sharedData = uniqueValues[sharedIndex];
                group.SetSharedComponentFilter(sharedData);
                var version = m_Manager.GetSharedComponentOrderVersion(sharedData);

                if (sharedData.value == evenTestValue)
                {
                    even(version, group);
                }

                if (sharedData.value == oddTestValue)
                {
                    odd(version, group);
                }
            }

            group.Dispose();
        }

        void TestSourceEvenValues(int version, EntityQuery group)
        {
            var testData = group.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);

            Assert.AreEqual(50, testData.Length);

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(i * 2, testData[i].value);
            }
        }

        void TestSourceOddValues(int version, EntityQuery group)
        {
            var testData = group.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);

            Assert.AreEqual(50, testData.Length);

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(1 + (i * 2), testData[i].value);
            }
        }

        [Test]
        public void SharedComponentNoChangeValuesUnchanged()
        {
            AddEvenOddTestData();
            ActionEvenOdd(TestSourceEvenValues, TestSourceOddValues);
        }

        void ChangeGroupOrder(int version, EntityQuery group)
        {
            var entities = group.ToEntityArray(World.UpdateAllocator.ToAllocator);

            for (int i = 0; i < 50; i++)
            {
                var e = entities[i];
                if ((i & 0x01) == 0)
                {
                    var testData2 = new EcsTestData2(i);
                    m_Manager.AddComponentData(e, testData2);
                }
            }
        }

        [Test]
        public void SharedComponentChangeOddGroupOrderOnlyOddVersionChanged()
        {
            AddEvenOddTestData();

            ActionEvenOdd((version, group) => {}, ChangeGroupOrder);
            ActionEvenOdd((version, group) => { Assert.Greater(version, 1); },
                (version, group) => { Assert.Greater(version, 1); });
        }

        [Test]
        public void SharedComponentChangeOddGroupOrderEvenValuesUnchanged()
        {
            AddEvenOddTestData();

            ActionEvenOdd((version, group) => {}, ChangeGroupOrder);
            ActionEvenOdd(TestSourceEvenValues, (version, group) => {});
        }

        void DestroyAllButOneEntityInGroup(int version, EntityQuery group)
        {
            var entities = group.ToEntityArray(World.UpdateAllocator.ToAllocator);

            for (int i = 0; i < 49; i++)
            {
                var e = entities[i];
                m_Manager.DestroyEntity(e);
            }
        }

        [Test]
        public void SharedComponentDestroyAllButOneEntityInOddGroupEvenValuesUnchanged()
        {
            AddEvenOddTestData();

            ActionEvenOdd((version, group) => {}, DestroyAllButOneEntityInGroup);
            ActionEvenOdd(TestSourceEvenValues, (version, group) => {});
        }

        [Test]
        public void UnrelatedChunkOrderUnchanged()
        {
            AddEvenOddTestData();

            ActionEvenOdd((version, group) =>
            {
                var entityType = m_Manager.GetEntityTypeHandle();
                var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
                var firstEntity = chunks[0].GetNativeArray(entityType);
                m_Manager.DestroyEntity(firstEntity);
            }, (version, group) => {});

            ActionEvenOdd(
                (version, group) =>
                {
                    var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
                    for (int i = 0; i < chunks.Length; i++)
                        Assert.Greater(1, chunks[i].GetOrderVersion());
                },
                (version, group) =>
                {
                    var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
                    for (int i = 0; i < chunks.Length; i++)
                        Assert.AreEqual(1, chunks[i].GetOrderVersion());
                });
        }

        [Test]
        public void CreateEntity()
        {
            m_Manager.CreateEntity(typeof(EcsTestData));
            Assert.AreEqual(1, m_Manager.GetComponentOrderVersion<EcsTestData>());
            Assert.AreEqual(0, m_Manager.GetComponentOrderVersion<EcsTestData2>());
        }

        [Test]
        public void DestroyEntity()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.DestroyEntity(entity);

            Assert.AreEqual(2, m_Manager.GetComponentOrderVersion<EcsTestData>());
            Assert.AreEqual(0, m_Manager.GetComponentOrderVersion<EcsTestData2>());
        }

        [Test]
        public void AddComponent()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.AddComponentData(entity, new EcsTestData2());

            Assert.AreEqual(3, m_Manager.GetComponentOrderVersion<EcsTestData>());
            Assert.AreEqual(1, m_Manager.GetComponentOrderVersion<EcsTestData2>());
        }

        [Test]
        public void RemoveComponent()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            m_Manager.RemoveComponent<EcsTestData2>(entity);

            Assert.AreEqual(3, m_Manager.GetComponentOrderVersion<EcsTestData>());
            Assert.AreEqual(2, m_Manager.GetComponentOrderVersion<EcsTestData2>());
        }

        [Test]
        public void ChangedOnlyAffectedArchetype()
        {
            m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData3));
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            m_Manager.RemoveComponent<EcsTestData2>(entity1);

            Assert.AreEqual(4, m_Manager.GetComponentOrderVersion<EcsTestData>());
            Assert.AreEqual(2, m_Manager.GetComponentOrderVersion<EcsTestData2>());
            Assert.AreEqual(1, m_Manager.GetComponentOrderVersion<EcsTestData3>());
        }

        [Test]
        public void SetSharedComponent()
        {
            var entity = m_Manager.CreateEntity(typeof(SharedData1), typeof(SharedData2));
            m_Manager.SetSharedComponentManaged(entity, new SharedData1(1));

            Assert.LessOrEqual(2, m_Manager.GetComponentOrderVersion<SharedData2>());
            Assert.LessOrEqual(2, m_Manager.GetComponentOrderVersion<SharedData1>());
            Assert.LessOrEqual(1, m_Manager.GetSharedComponentOrderVersion(new SharedData1(1)));
        }

        [Test]
        public void DestroySharedComponentEntity()
        {
            var sharedData = new SharedData1(1);

            var destroyEntity = m_Manager.CreateEntity(typeof(SharedData1));
            m_Manager.SetSharedComponentManaged(destroyEntity, sharedData);
            /*var dontDestroyEntity = */ m_Manager.Instantiate(destroyEntity);

            Assert.LessOrEqual(2, m_Manager.GetSharedComponentOrderVersion(sharedData));

            m_Manager.DestroyEntity(destroyEntity);

            Assert.LessOrEqual(3, m_Manager.GetSharedComponentOrderVersion(sharedData));
        }

        [Test]
        public void GetSharedComponentOrderVersionIncrementingWithManaged([Values(0, 1)]int value)
        {
            int unaffectedSharedValue = 15;
            m_Manager.AddSharedComponentManaged(m_Manager.CreateEntity(), new ManagedSharedData2(unaffectedSharedValue));
            var unaffectedVersion = m_Manager.GetSharedComponentOrderVersionManaged(new ManagedSharedData2(unaffectedSharedValue));

            var v0 = m_Manager.GetSharedComponentOrderVersionManaged(new ManagedSharedData2(value));

            var entity = m_Manager.CreateEntity();
            m_Manager.AddSharedComponentManaged(entity, new ManagedSharedData2(value));
            var v1 = m_Manager.GetSharedComponentOrderVersionManaged(new ManagedSharedData2(value));
            Assert.Greater(v1, v0);

            m_Manager.RemoveComponent<ManagedSharedData2>(entity);
            var v2 = m_Manager.GetSharedComponentOrderVersionManaged(new ManagedSharedData2(value));
            Assert.Greater(v2, v1);

            m_Manager.AddSharedComponentManaged(entity, new ManagedSharedData2(value));
            var v3 = m_Manager.GetSharedComponentOrderVersionManaged(new ManagedSharedData2(value));
            Assert.Greater(v3, v2);

            var clone = m_Manager.Instantiate(entity);
            var v4 = m_Manager.GetSharedComponentOrderVersionManaged(new ManagedSharedData2(value));
            Assert.Greater(v4, v3);

            m_Manager.DestroyEntity(clone);
            var v5 = m_Manager.GetSharedComponentOrderVersionManaged(new ManagedSharedData2(value));
            Assert.Greater(v5, v4);

            m_Manager.DestroyEntity(entity);
            var v6 = m_Manager.GetSharedComponentOrderVersionManaged(new ManagedSharedData2(value));
            Assert.Greater(v6, v5);

            Assert.AreEqual(unaffectedVersion, m_Manager.GetSharedComponentOrderVersionManaged(new ManagedSharedData2(unaffectedSharedValue)));
        }

        [Test]
        public void GetUnmanagedSharedComponentOrderVersionIncrementing([Values(0, 1)]int value)
        {
            int unaffectedSharedValue = 15;
            m_Manager.AddSharedComponentManaged(m_Manager.CreateEntity(), new SharedData1(unaffectedSharedValue));
            var unaffectedVersion = m_Manager.GetSharedComponentOrderVersion(new SharedData1(unaffectedSharedValue));

            var v0 = m_Manager.GetSharedComponentOrderVersion(new SharedData1(value));

            var entity = m_Manager.CreateEntity();
            m_Manager.AddSharedComponentManaged(entity, new SharedData1(value));
            var v1 = m_Manager.GetSharedComponentOrderVersion(new SharedData1(value));
            Assert.Greater(v1, v0);

            m_Manager.RemoveComponent<SharedData1>(entity);
            var v2 = m_Manager.GetSharedComponentOrderVersion(new SharedData1(value));
            Assert.Greater(v2, v1);

            m_Manager.AddSharedComponentManaged(entity, new SharedData1(value));
            var v3 = m_Manager.GetSharedComponentOrderVersion(new SharedData1(value));
            Assert.Greater(v3, v2);

            var clone = m_Manager.Instantiate(entity);
            var v4 = m_Manager.GetSharedComponentOrderVersion(new SharedData1(value));
            Assert.Greater(v4, v3);

            m_Manager.DestroyEntity(clone);
            var v5 = m_Manager.GetSharedComponentOrderVersion(new SharedData1(value));
            Assert.Greater(v5, v4);

            m_Manager.DestroyEntity(entity);
            var v6 = m_Manager.GetSharedComponentOrderVersion(new SharedData1(value));
            Assert.Greater(v6, v5);

            Assert.AreEqual(unaffectedVersion, m_Manager.GetSharedComponentOrderVersion(new SharedData1(unaffectedSharedValue)));
        }

        [Test]
        public void ChangeArchetypeInPlace_ChangesOrderVersion()
        {
            m_Manager.Debug.SetGlobalSystemVersion(10);

            var entity = m_Manager.CreateEntity(typeof(EcsTestData));

            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            using (var chunks = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(1, chunks.Length);
                Assert.AreEqual(10, chunks[0].GetOrderVersion());

                m_Manager.Debug.SetGlobalSystemVersion(20);
                m_Manager.AddComponent<EcsTestTag>(query);

                Assert.AreEqual(20, chunks[0].GetOrderVersion());
            }
        }
    }
}
