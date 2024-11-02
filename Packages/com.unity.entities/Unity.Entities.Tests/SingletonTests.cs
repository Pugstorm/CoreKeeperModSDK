using System;
using NUnit.Framework;
using Unity.Jobs;

namespace Unity.Entities.Tests
{
    class SingletonTests : ECSTestsFixture
    {
        partial class EmptyTestSystem : SystemBase
        {
            protected override void OnUpdate() { }
        }

        partial struct EmptyTestISystem : ISystem
        {
        }

        [Test]
        public void GetSetSingleton_Works()
        {
            m_Manager.CreateSingleton<EcsTestData>();

            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestData>().Build(EmptySystem);
            query.SetSingleton(new EcsTestData(10));
            Assert.AreEqual(10, query.GetSingleton<EcsTestData>().value);
        }

        [Test]
        public void GetCreateSingleton_Works()
        {
            m_Manager.CreateSingleton(new EcsTestData(10));
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAll<EcsTestData>().Build(EmptySystem);
            Assert.AreEqual(10, query.GetSingleton<EcsTestData>().value);
        }

        [Test]
        public void GetSetSingletonRW_ByRef_Modifies_Singleton()
        {
            m_Manager.CreateSingleton<EcsTestData>();
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestData>().Build(EmptySystem);
            query.SetSingleton(new EcsTestData(10));

            {
                const int expected = 42;
                ref var singleton = ref query.GetSingletonRW<EcsTestData>().ValueRW;
                singleton.value = expected;

                Assert.AreEqual(expected, query.GetSingleton<EcsTestData>().value);
                Assert.AreEqual(expected, query.GetSingletonRW<EcsTestData>().ValueRO.value);
            }

            {
                const int expected = 33;
                query.GetSingletonRW<EcsTestData>().ValueRW.value = expected;

                Assert.AreEqual(expected, query.GetSingleton<EcsTestData>().value);
                Assert.AreEqual(expected, query.GetSingletonRW<EcsTestData>().ValueRO.value);
            }
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void GetSingletonRW_Use_After_Free_Throws()
        {
            const int expected = 33;
            m_Manager.CreateSingleton<EcsTestData>();
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestData>().Build(EmptySystem);

            query.SetSingleton(new EcsTestData(expected));
            var singletonRef = query.GetSingletonRW<EcsTestData>();
            Assert.AreEqual(expected, singletonRef.ValueRO.value);

            var singletonEntity = query.GetSingletonEntity();
            ref var singletonRefRef = ref singletonRef.ValueRW;
            Assert.AreNotEqual(Entity.Null, singletonEntity);
            m_Manager.DestroyEntity(singletonEntity);

            // The reference now points to chunk memory that has either been repurposed or even deallocated
            // we must throw to signal to the user this memory is not ok to access
            Assert.Throws<ObjectDisposedException>(() => { Assert.AreEqual(expected, singletonRef.ValueRO.value); });
        }

        [Test]
        public void GetSetSingletonRW_ByValue_DoesNot_Modify_Singleton()
        {
            const int expected = 42;
            m_Manager.CreateSingleton<EcsTestData>();
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestData>().Build(EmptySystem);

            query.SetSingleton(new EcsTestData(expected));

            var singleton = query.GetSingletonRW<EcsTestData>().ValueRW;
            singleton.value = 10;
            Assert.AreEqual(expected, query.GetSingleton<EcsTestData>().value);
            Assert.AreEqual(expected, query.GetSingletonRW<EcsTestData>().ValueRO.value);
        }

        [Test]
        public void GetSetSingletonRW_Works()
        {
            const int expected = 42;
            m_Manager.CreateSingleton<EcsTestData>();
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestData>().Build(EmptySystem);
            query.SetSingleton(new EcsTestData(expected));

            Assert.AreEqual(expected, query.GetSingletonRW<EcsTestData>().ValueRO.value);
        }

        [Test]
        public void GetCreateSingletonRW_Works()
        {
            const int expected = 42;
            m_Manager.CreateSingleton(new EcsTestData(expected));
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestData>().Build(EmptySystem);
            Assert.AreEqual(expected, query.GetSingletonRW<EcsTestData>().ValueRO.value);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void CreateSingleton_EnableableComponent_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => m_Manager.CreateSingleton<EcsTestDataEnableable>());
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void GetSingleton_EnableableComponent_Throws()
        {
            m_Manager.CreateEntity(typeof(EcsTestDataEnableable));
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestDataEnableable>().Build(EmptySystem);
            Assert.Throws<InvalidOperationException>(() => query.GetSingleton<EcsTestDataEnableable>());
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void GetSingletonEntity_EnableableComponent_Throws()
        {
            m_Manager.CreateEntity(typeof(EcsTestDataEnableable));
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsIntElementEnableable>().Build(EmptySystem);
            Assert.Throws<InvalidOperationException>(() => query.GetSingletonEntity());
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void CreateSingletonBuffer_EnableableComponent_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => m_Manager.CreateSingletonBuffer<EcsIntElementEnableable>());
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void GetSingletonBuffer_EnableableComponent_Throws()
        {
            m_Manager.CreateEntity(typeof(EcsIntElementEnableable));
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsIntElementEnableable>().Build(EmptySystem);
            Assert.Throws<InvalidOperationException>(() => query.GetSingletonBuffer<EcsIntElementEnableable>());
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void TryGetSingleton_EnableableComponent_Throws()
        {
            m_Manager.CreateEntity(typeof(EcsTestDataEnableable));
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestDataEnableable>().Build(EmptySystem);
            Assert.Throws<InvalidOperationException>(() => query.TryGetSingleton<EcsTestDataEnableable>(out var value));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void TryGetSingletonEntity_EnableableComponent_Throws()
        {
            m_Manager.CreateEntity(typeof(EcsTestDataEnableable));
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestDataEnableable>().Build(EmptySystem);
            Assert.Throws<InvalidOperationException>(() => query.TryGetSingletonEntity<EcsTestDataEnableable>(out var ent));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void TryGetSingletonBuffer_EnableableComponent_Throws()
        {
            m_Manager.CreateEntity(typeof(EcsIntElementEnableable));
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsIntElementEnableable>().Build(EmptySystem);
            Assert.Throws<InvalidOperationException>(() => query.TryGetSingletonBuffer<EcsIntElementEnableable>(out var buffer));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void SetSingleton_EnableableComponent_Throws()
        {
            m_Manager.CreateEntity(typeof(EcsTestDataEnableable));
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestDataEnableable>().Build(EmptySystem);
            Assert.Throws<InvalidOperationException>(() => query.SetSingleton<EcsTestDataEnableable>(new EcsTestDataEnableable()));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void HasSingleton_EnableableComponent_Throws()
        {
            m_Manager.CreateEntity(typeof(EcsIntElementEnableable));
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestData>().Build(EmptySystem);
            Assert.Throws<InvalidOperationException>(() => query.HasSingleton<EcsIntElementEnableable>());
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void GetSingleton_QueryHasEnableableComponent_Works()
        {
            var e1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            var e2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            m_Manager.SetComponentData(e1, new EcsTestData(17));
            m_Manager.SetComponentData(e2, new EcsTestData(23));
            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(e1, false);
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAll<EcsTestData,EcsTestDataEnableable>().Build(EmptySystem);
            Assert.AreEqual(23, query.GetSingleton<EcsTestData>().value);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        [Ignore("GetSingletonEntity forbids any enableable components in the query until DOTS-9695 is addressed.")] // TODO(DOTS-9695): re-enable this test
        public void GetSingletonEntity_QueryHasEnableableComponent_Works()
        {
            var e1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            var e2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            m_Manager.SetComponentData(e1, new EcsTestData(17));
            m_Manager.SetComponentData(e2, new EcsTestData(23));
            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(e1, false);
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAll<EcsTestData,EcsTestDataEnableable>().Build(EmptySystem);
            Assert.AreEqual(e2, query.GetSingletonEntity());
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void GetSingletonBuffer_QueryHasEnableableComponent_Works()
        {
            var e1 = m_Manager.CreateEntity(typeof(EcsIntElement), typeof(EcsTestDataEnableable));
            var e2 = m_Manager.CreateEntity(typeof(EcsIntElement), typeof(EcsTestDataEnableable));
            m_Manager.GetBuffer<EcsIntElement>(e1).Add(new EcsIntElement { Value = 17});
            m_Manager.GetBuffer<EcsIntElement>(e2).Add(new EcsIntElement { Value = 23});
            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(e1, false);
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAll<EcsIntElement,EcsTestDataEnableable>().Build(EmptySystem);
            Assert.AreEqual(23, query.GetSingletonBuffer<EcsIntElement>()[0].Value);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void TryGetSingleton_QueryHasEnableableComponent_Works()
        {
            var e1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            var e2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            m_Manager.SetComponentData(e1, new EcsTestData(17));
            m_Manager.SetComponentData(e2, new EcsTestData(23));
            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(e1, false);
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAll<EcsTestData,EcsTestDataEnableable>().Build(EmptySystem);
            Assert.IsTrue(query.TryGetSingleton<EcsTestData>(out var singleton));
            Assert.AreEqual(23, singleton.value);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        [Ignore("TryGetSingletonEntity forbids any enableable components in the query until DOTS-9695 is addressed.")] // TODO(DOTS-9695): re-enable this test
        public void TryGetSingletonEntity_QueryHasEnableableComponent_Works()
        {
            var e1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            var e2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            m_Manager.SetComponentData(e1, new EcsTestData(17));
            m_Manager.SetComponentData(e2, new EcsTestData(23));
            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(e1, false);
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAll<EcsTestData,EcsTestDataEnableable>().Build(EmptySystem);
            Assert.IsTrue(query.TryGetSingletonEntity<EcsTestData>(out var singletonEntity));
            Assert.AreEqual(e2, singletonEntity);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void TryGetSingletonBuffer_QueryHasEnableableComponent_Works()
        {
            var e1 = m_Manager.CreateEntity(typeof(EcsIntElement), typeof(EcsTestDataEnableable));
            var e2 = m_Manager.CreateEntity(typeof(EcsIntElement), typeof(EcsTestDataEnableable));
            m_Manager.GetBuffer<EcsIntElement>(e1).Add(new EcsIntElement { Value = 17});
            m_Manager.GetBuffer<EcsIntElement>(e2).Add(new EcsIntElement { Value = 23});
            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(e1, false);
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAll<EcsIntElement,EcsTestDataEnableable>().Build(EmptySystem);
            Assert.IsTrue(query.TryGetSingletonBuffer<EcsIntElement>(out var buf));
            Assert.AreEqual(23, buf[0].Value);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void SetSingleton_QueryHasEnableableComponent_Works()
        {
            var e1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            var e2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            m_Manager.SetComponentData(e1, new EcsTestData(17));
            m_Manager.SetComponentData(e2, new EcsTestData(23));
            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(e1, false);
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestData>().WithAll<EcsTestDataEnableable>().Build(EmptySystem);
            query.SetSingleton(new EcsTestData(42));
            Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData>(e2).value);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void HasSingleton_QueryHasEnableableComponentWorks()
        {
            var e1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            var e2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            m_Manager.SetComponentData(e1, new EcsTestData(17));
            m_Manager.SetComponentData(e2, new EcsTestData(23));
            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(e1, false);
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAll<EcsTestData,EcsTestDataEnableable>().Build(EmptySystem);
            Assert.IsTrue(query.HasSingleton<EcsTestData>());
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires opt-in debug checks")]
        public void HasSingleton_MoreThanOneInstance_Throws()
        {
            var e1 = m_Manager.CreateEntity(typeof(EcsIntElement), typeof(EcsTestSharedComp));
            var e2 = m_Manager.CreateEntity(typeof(EcsIntElement), typeof(EcsTestSharedComp));
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsIntElement,EcsTestSharedComp>().Build(EmptySystem);
            Assert.Throws<InvalidOperationException>(() => query.HasSingleton<EcsIntElement>());
            // If the query has filtering enabled and only one instance matches the filter, this should not throw
            var sharedComponentValue = new EcsTestSharedComp { value = 17 };
            m_Manager.SetSharedComponent(e2, sharedComponentValue);
            query.SetSharedComponentFilter(sharedComponentValue);
            Assert.DoesNotThrow(() => query.HasSingleton<EcsIntElement>());
            Assert.AreEqual(e2, query.GetSingletonEntity());
        }

        [Test]
        public void GetSingletonBuffer_Works()
        {
            var ent = m_Manager.CreateSingletonBuffer<EcsIntElement>();

            var buffer = m_Manager.GetBuffer<EcsIntElement>(ent);
            buffer.Add(new EcsIntElement { Value = 17 });
            buffer.Add(new EcsIntElement { Value = 23 });
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsIntElement>().Build(EmptySystem);;

            var singletonBuffer = query.GetSingletonBuffer<EcsIntElement>();
            Assert.AreEqual(buffer.Length, singletonBuffer.Length);
            Assert.AreEqual(buffer[0], singletonBuffer[0]);
            Assert.AreEqual(buffer[1], singletonBuffer[1]);
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void GetSingletonBuffer_WriteToReadOnly_Throws()
        {
            var ent = m_Manager.CreateSingletonBuffer<EcsIntElement>();

            var buffer = m_Manager.GetBuffer<EcsIntElement>(ent);
            buffer.Add(new EcsIntElement { Value = 17 });
            buffer.Add(new EcsIntElement { Value = 23 });
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsIntElement>().Build(EmptySystem);
            var singletonBuffer = query.GetSingletonBuffer<EcsIntElement>(true);
            Assert.Throws<InvalidOperationException>(() => singletonBuffer.Add(new EcsIntElement { Value = 10 }));
            Assert.Throws<InvalidOperationException>(() => singletonBuffer[0] = new EcsIntElement { Value = 1024 });
        }

        [Test]
        public void GetSingletonBuffer_WriteToReadWrite_Works()
        {
            var ent = m_Manager.CreateSingletonBuffer<EcsIntElement>();

            var buffer = m_Manager.GetBuffer<EcsIntElement>(ent);
            buffer.Add(new EcsIntElement { Value = 17 });
            buffer.Add(new EcsIntElement { Value = 23 });
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsIntElement>().Build(EmptySystem);
            var singletonBuffer = query.GetSingletonBuffer<EcsIntElement>(false);
            Assert.DoesNotThrow(() => singletonBuffer.Add(new EcsIntElement { Value = 10 }));
            Assert.DoesNotThrow(() => singletonBuffer[0] = new EcsIntElement { Value = 1024 });
        }

        [Test]
        public void SingletonMethodsWithValidFilter_GetsAndSets()
        {
            var queryWithFilter1 = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(SharedData1));
            queryWithFilter1.SetSharedComponentFilterManaged(new SharedData1(1));
            var queryWithFilter2 = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(SharedData1));
            queryWithFilter2.SetSharedComponentFilterManaged(new SharedData1(2));

            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(SharedData1));
            m_Manager.SetComponentData(entity1, new EcsTestData(-1));
            m_Manager.SetSharedComponentManaged(entity1, new SharedData1(1));

            var entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(SharedData1));
            m_Manager.SetComponentData(entity2, new EcsTestData(-1));
            m_Manager.SetSharedComponentManaged(entity2, new SharedData1(2));

            Assert.DoesNotThrow(() => queryWithFilter1.SetSingleton(new EcsTestData(1)));
            Assert.DoesNotThrow(() => queryWithFilter2.SetSingleton(new EcsTestData(2)));

            Assert.DoesNotThrow(() => queryWithFilter1.GetSingletonEntity());
            Assert.DoesNotThrow(() => queryWithFilter2.GetSingletonEntity());

            var data1 = queryWithFilter1.GetSingleton<EcsTestData>();
            Assert.AreEqual(1, data1.value);
            var data2 = queryWithFilter2.GetSingleton<EcsTestData>();
            Assert.AreEqual(2, data2.value);

            // These need to be reset or the AllSharedComponentReferencesAreFromChunks check will fail
            queryWithFilter1.ResetFilter();
            queryWithFilter2.ResetFilter();
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void SingletonMethodsWithInvalidFilter_Throws()
        {
            var queryWithFilterMissingEntity = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(SharedData1));
            queryWithFilterMissingEntity.SetSharedComponentFilterManaged(new SharedData1(1));
            var queryWithFilterWithAdditionalEntity = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(SharedData1));
            queryWithFilterWithAdditionalEntity.SetSharedComponentFilterManaged(new SharedData1(2));

            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(SharedData1), typeof(EcsIntElement));
            m_Manager.SetSharedComponentManaged(entity1, new SharedData1(2));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(SharedData1), typeof(EcsIntElement));
            m_Manager.SetSharedComponentManaged(entity2, new SharedData1(2));

            Assert.Throws<InvalidOperationException>(() => queryWithFilterMissingEntity.GetSingleton<EcsTestData>());
            Assert.Throws<InvalidOperationException>(() => queryWithFilterMissingEntity.SetSingleton(new EcsTestData(1)));
            Assert.Throws<InvalidOperationException>(() => queryWithFilterMissingEntity.GetSingletonEntity());
            Assert.Throws<InvalidOperationException>(() => queryWithFilterMissingEntity.GetSingletonBuffer<EcsIntElement>());

            Assert.Throws<InvalidOperationException>(() => queryWithFilterWithAdditionalEntity.GetSingleton<EcsTestData>());
            Assert.Throws<InvalidOperationException>(() => queryWithFilterWithAdditionalEntity.SetSingleton(new EcsTestData(1)));
            Assert.Throws<InvalidOperationException>(() => queryWithFilterWithAdditionalEntity.GetSingletonEntity());
            Assert.Throws<InvalidOperationException>(() => queryWithFilterWithAdditionalEntity.GetSingletonBuffer<EcsIntElement>());

            // These need to be reset or the AllSharedComponentReferencesAreFromChunks check will fail
            queryWithFilterMissingEntity.ResetFilter();
            queryWithFilterWithAdditionalEntity.ResetFilter();
        }

        [Test]
        public void GetSetSingletonMultipleComponents()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData3), typeof(EcsTestData), typeof(EcsTestData2));

            m_Manager.SetComponentData(entity, new EcsTestData(10));
            var query1 = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestData>().Build(EmptySystem);
            Assert.AreEqual(10, query1.GetSingleton<EcsTestData>().value);
            var query2 = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestData2>().Build(EmptySystem);
            query2.SetSingleton(new EcsTestData2(100));
            Assert.AreEqual(100, m_Manager.GetComponentData<EcsTestData2>(entity).value0);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void GetSetSingleton_DoesntExist_Throws()
        {
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestData>().Build(EmptySystem);
            var queryTag = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestTag>().Build(EmptySystem);
            Assert.IsFalse(queryTag.HasSingleton<EcsTestTag>());
            Assert.Throws<InvalidOperationException>(() => query.SetSingleton(new EcsTestData()));
            Assert.Throws<InvalidOperationException>(() => query.GetSingleton<EcsTestData>());
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void GetSingletonBuffer_DoesntExist_Throws()
        {
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsIntElement>().Build(EmptySystem);
            Assert.IsFalse(query.HasSingleton<EcsIntElement>());
            Assert.Throws<InvalidOperationException>(() => query.GetSingletonBuffer<EcsIntElement>());
        }

        [Test]
        public void CreateSingleton_ZeroSizeComponent_Works()
        {
            Assert.DoesNotThrow(() => m_Manager.CreateSingleton<EcsTestTag>());
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void GetSetSingleton_ZeroSizeComponent_Throws()
        {
            m_Manager.CreateEntity(typeof(EcsTestTag));
            var queryTag = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestTag>().Build(EmptySystem);
            Assert.IsTrue(queryTag.HasSingleton<EcsTestTag>());
            Assert.Throws<InvalidOperationException>(() => queryTag.SetSingleton(new EcsTestTag()));
            Assert.Throws<InvalidOperationException>(() => queryTag.GetSingleton<EcsTestTag>());
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void GetSetSingleton_MultipleEntities_Throws()
        {
            m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.CreateEntity(typeof(EcsTestData));
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestData>().Build(EmptySystem);
            Assert.Throws<InvalidOperationException>(() => query.SetSingleton(new EcsTestData()));
            Assert.Throws<InvalidOperationException>(() => query.GetSingleton<EcsTestData>());
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void CreateSingleton_MultipleEntities_Throws()
        {
            m_Manager.CreateSingleton<EcsTestData>();
            Assert.Throws<InvalidOperationException>(() => m_Manager.CreateSingleton<EcsTestData>());
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void GetSingletonBuffer_MultipleEntities_Throws()
        {
            m_Manager.CreateEntity(typeof(EcsIntElement));
            m_Manager.CreateEntity(typeof(EcsIntElement));
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsIntElement>().Build(EmptySystem);
            Assert.Throws<InvalidOperationException>(() => query.GetSingletonBuffer<EcsIntElement>());
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void CreateSingletonBuffer_MultipleEntities_Throws()
        {
            m_Manager.CreateSingletonBuffer<EcsIntElement>();
            Assert.Throws<InvalidOperationException>(() => m_Manager.CreateSingletonBuffer<EcsIntElement>());
        }

        [Test]
        public void RequireForUpdate_Singleton()
        {
            EmptySystem.RequireForUpdate<EcsTestData>();
            EmptySystem.GetEntityQuery(typeof(EcsTestData2));

            m_Manager.CreateSingleton<EcsTestData2>();
            Assert.IsFalse(EmptySystem.ShouldRunSystem());
            m_Manager.CreateSingleton<EcsTestData>();
            Assert.IsTrue(EmptySystem.ShouldRunSystem());
        }

        [Test]
        public void RequireForUpdate_Multiple()
        {
            // RequireSingletonForUpdate was renamed to RequireForUpdate, no longer requires
            // only one component, should work with multiple.
            EmptySystem.RequireForUpdate<EcsTestData>();
            EmptySystem.GetEntityQuery(typeof(EcsTestData2));

            m_Manager.CreateSingleton<EcsTestData2>();
            Assert.IsFalse(EmptySystem.ShouldRunSystem());
            m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.CreateEntity(typeof(EcsTestData));
            Assert.IsTrue(EmptySystem.ShouldRunSystem());
        }

        [Test]
        public void HasSingletonWorks()
        {
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestData>().Build(EmptySystem);
            Assert.IsFalse(query.HasSingleton<EcsTestData>());
            m_Manager.CreateSingleton<EcsTestData>();
            Assert.IsTrue(query.HasSingleton<EcsTestData>());

            var queryElement = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsIntElement>().Build(EmptySystem);
            Assert.IsFalse(queryElement.HasSingleton<EcsIntElement>());
            m_Manager.CreateSingletonBuffer<EcsIntElement>();
            Assert.IsTrue(queryElement.HasSingleton<EcsIntElement>());
        }

        [Test]
        public void HasSingleton_ReturnsTrueWithEntityWithOnlyComponent()
        {
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestData>().Build(EmptySystem);
            Assert.IsFalse(query.HasSingleton<EcsTestData>());

            m_Manager.CreateEntity(typeof(EcsTestData));
            Assert.IsTrue(query.HasSingleton<EcsTestData>());
        }

        [Test]
        public void GetSingletonEntityWorks()
        {
            var entity = m_Manager.CreateSingleton<EcsTestData>();

            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestData>().Build(EmptySystem);
            var singletonEntity = query.GetSingletonEntity();
            Assert.AreEqual(entity, singletonEntity);
        }

        [Test]
        public void TryGetSingletonEntity_Works()
        {
            var entity = m_Manager.CreateSingleton<EcsTestData>();
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestData>().Build(EmptySystem);
            var hasEntity = query.TryGetSingletonEntity<EcsTestData>(out var singletonEntity);

            Assert.True(hasEntity);
            Assert.AreEqual(entity, singletonEntity);
        }

        [Test]
        public void TryGetSingletonEntity_NoSingleton_ReturnsFalse()
        {
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestData>().Build(EmptySystem);
            var hasEntity = query.TryGetSingletonEntity<EcsTestData>(out var singletonEntity);

            Assert.IsFalse(hasEntity);
            Assert.AreEqual(default(Entity), singletonEntity);
        }

        [Test]
        public void TryGetSingleton_Works()
        {
            m_Manager.CreateSingleton<EcsTestData>();
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestData>().Build(EmptySystem);
            query.SetSingleton(new EcsTestData(10));
            var hasSingleton = query.TryGetSingleton<EcsTestData>(out var ecsTestData);
            Assert.IsTrue(hasSingleton);
            Assert.AreEqual(10, ecsTestData.value);

        }

        [Test]
        public void TryGetSingleton_NoSingleton_ReturnsFalse()
        {
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestData>().Build(EmptySystem);
            var hasSingleton = query.TryGetSingleton<EcsTestData>(out var ecsTestData);
            Assert.IsFalse(hasSingleton);
            Assert.AreEqual(default(EcsTestData).value, ecsTestData.value);
        }

        [Test]
        public void TryGetSingletonBuffer_Works()
        {
            var ent = m_Manager.CreateSingletonBuffer<EcsIntElement>();
            var buffer = m_Manager.GetBuffer<EcsIntElement>(ent);
            buffer.Add(new EcsIntElement{Value = 10});
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsIntElement>().Build(EmptySystem);
            var hasSingleton = query.TryGetSingletonBuffer<EcsIntElement>(out var singletonBuffer);
            Assert.IsTrue(hasSingleton);
            Assert.AreEqual(1, singletonBuffer.Length);
            Assert.AreEqual(10, singletonBuffer[0].Value);
        }

        [Test]
        public void TryGetSingletonBuffer_NoSingleton_ReturnsFalse()
        {
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsIntElement>().Build(EmptySystem);
            var hasSingleton = query.TryGetSingletonBuffer<EcsIntElement>(out var singletonBuffer);
            Assert.IsFalse(hasSingleton);
            Assert.IsFalse(singletonBuffer.IsCreated);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void SetSingleton_ReadOnlyType_Throws()
        {
            m_Manager.CreateSingleton<EcsTestData>();
            var query = m_Manager.CreateEntityQuery(ComponentType.ReadOnly<EcsTestData>());
            Assert.Throws<InvalidOperationException>(() => query.SetSingleton(new EcsTestData {value = 17}));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void GetSingletonWithMultipleComponentsAndMissingRequestedThrows()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var query = EmptySystem.GetEntityQuery(typeof(EcsTestData), typeof(EcsTestData2));
            Assert.Throws<InvalidOperationException>(() => query.GetSingleton<EcsTestData3>());
        }

        [Test]
        public void SystemEntityComponentDisallowsCreateSingleton()
        {
            using (var world = new World("WorldX"))
            {
                var system = world.GetOrCreateSystem<EmptyTestISystem>();

                var e = world.EntityManager.CreateSingleton<EcsTestData>();
                world.EntityManager.DestroyEntity(e);
                Assert.DoesNotThrow(() => e = world.EntityManager.CreateSingleton<EcsTestData>());
                world.EntityManager.DestroyEntity(e);

                world.EntityManager.AddComponent<EcsTestData>(system);
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                Assert.Throws<InvalidOperationException>(() => world.EntityManager.CreateSingleton<EcsTestData>());
#endif
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void GetSetSingleton_ManagedComponents()
        {
            m_Manager.CreateSingleton<EcsTestManagedComponent>();
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestManagedComponent>().Build(EmptySystem);
            const string kTestVal = "SomeString";
            query.SetSingleton(new EcsTestManagedComponent() { value = kTestVal });
            Assert.AreEqual(kTestVal, query.GetSingleton<EcsTestManagedComponent>().value);
        }

        [Test]
        public void HasSingletonWorks_ManagedComponents()
        {
            var query = new EntityQueryBuilder(EmptySystem.WorldUpdateAllocator).WithAllRW<EcsTestManagedComponent>().Build(EmptySystem);
            Assert.IsFalse(query.HasSingleton<EcsTestManagedComponent>());
            m_Manager.CreateSingleton<EcsTestManagedComponent>();
            Assert.IsTrue(query.HasSingleton<EcsTestManagedComponent>());
        }

#endif
    }
}
