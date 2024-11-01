using NUnit.Framework;
using Unity.Collections;
using System;

// ******* COPY AND PASTE WARNING *************
// NOTE: Duplicate tests (with only type differences)
// - CleanupComponentTests.cs and CleanupBufferElementTests.cs
// - Any change to this file should be reflected in the other file.
// Changes between two files:
// - s/CleanupComponentTests/CleanupBufferElementTests/
// - s/EcsCleanup1/EcsIntCleanupElement/g
// - Add VerifyBufferCount to CleanupBufferElementTests
// - CleanupBufferElementTests calls VerifyBufferCount instead of VerifyComponentCount on EcsIntCleanupElement
// - SetSharedComponent in CleanupComponentTests:
//               m_Manager.SetComponentData(entity, new EcsCleanup1(2));
//   Replaced with GetBuffer:
//               var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entity);
//               buffer.Add(2);
// ******* COPY AND PASTE WARNING *************

namespace Unity.Entities.Tests
{
    [TestFixture]
    class CleanupComponentTests : ECSTestsFixture
    {
        void VerifyComponentCount<T>(int expectedCount)
            where T : IComponentData
        {
            var group = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<T>());
            var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            group.Dispose();
            Assert.AreEqual(expectedCount, ArchetypeChunkArray.TotalEntityCountInChunksIgnoreFiltering(chunks));
        }

        void VerifyQueryCount(EntityQuery group, int expectedCount)
        {
            var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(expectedCount, ArchetypeChunkArray.TotalEntityCountInChunksIgnoreFiltering(chunks));
        }

        [Test]
        public void DeleteWhenEmpty()
        {
            var entity = m_Manager.CreateEntity(
                typeof(EcsTestData),
                typeof(EcsTestSharedComp),
                typeof(EcsCleanup1)
            );

            m_Manager.SetComponentData(entity, new EcsTestData(1));
            m_Manager.SetComponentData(entity, new EcsCleanup1(2));
            m_Manager.SetSharedComponentManaged(entity, new EcsTestSharedComp(3));

            VerifyComponentCount<EcsTestData>(1);

            m_Manager.DestroyEntity(entity);

            VerifyComponentCount<EcsTestData>(0);
            VerifyComponentCount<EcsCleanup1>(1);

            m_Manager.RemoveComponent<EcsCleanup1>(entity);

            VerifyComponentCount<EcsCleanup1>(0);

            Assert.IsFalse(m_Manager.Exists(entity));
        }

        [Test]
        public void DeleteWhenEmptyArray()
        {
            var entities = new Entity[512];

            for (var i = 0; i < 512; i++)
            {
                var entity = m_Manager.CreateEntity(
                    typeof(EcsTestData),
                    typeof(EcsTestSharedComp),
                    typeof(EcsCleanup1)
                );
                entities[i] = entity;

                m_Manager.SetComponentData(entity, new EcsTestData(i));
                m_Manager.SetComponentData(entity, new EcsCleanup1(i));
                m_Manager.SetSharedComponentManaged(entity, new EcsTestSharedComp(i % 7));
            }

            VerifyComponentCount<EcsTestData>(512);

            for (var i = 0; i < 512; i += 2)
            {
                var entity = entities[i];
                m_Manager.DestroyEntity(entity);
            }

            VerifyComponentCount<EcsTestData>(256);
            VerifyComponentCount<EcsCleanup1>(512);
            VerifyQueryCount(m_Manager.CreateEntityQuery(
                ComponentType.Exclude<EcsTestData>(),
                ComponentType.ReadWrite<EcsCleanup1>()), 256);

            for (var i = 0; i < 512; i += 2)
            {
                var entity = entities[i];
                m_Manager.RemoveComponent<EcsCleanup1>(entity);
            }

            VerifyComponentCount<EcsCleanup1>(256);

            for (var i = 0; i < 512; i += 2)
            {
                var entity = entities[i];
                Assert.IsFalse(m_Manager.Exists(entity));
            }

            for (var i = 1; i < 512; i += 2)
            {
                var entity = entities[i];
                Assert.IsTrue(m_Manager.Exists(entity));
            }
        }

        [Test]
        public void DeleteWhenEmptyArray2()
        {
            var entities = new Entity[512];

            for (var i = 0; i < 512; i++)
            {
                var entity = m_Manager.CreateEntity(
                    typeof(EcsTestData),
                    typeof(EcsTestSharedComp),
                    typeof(EcsCleanup1)
                );
                entities[i] = entity;

                m_Manager.SetComponentData(entity, new EcsTestData(i));
                m_Manager.SetComponentData(entity, new EcsCleanup1(i));
                m_Manager.SetSharedComponentManaged(entity, new EcsTestSharedComp(i % 7));
            }

            VerifyComponentCount<EcsTestData>(512);

            for (var i = 0; i < 256; i++)
            {
                var entity = entities[i];
                m_Manager.DestroyEntity(entity);
            }

            VerifyComponentCount<EcsTestData>(256);
            VerifyComponentCount<EcsCleanup1>(512);
            VerifyQueryCount(m_Manager.CreateEntityQuery(
                ComponentType.Exclude<EcsTestData>(),
                ComponentType.ReadWrite<EcsCleanup1>()), 256);

            for (var i = 0; i < 256; i++)
            {
                var entity = entities[i];
                m_Manager.RemoveComponent<EcsCleanup1>(entity);
            }

            VerifyComponentCount<EcsCleanup1>(256);

            for (var i = 0; i < 256; i++)
            {
                var entity = entities[i];
                Assert.IsFalse(m_Manager.Exists(entity));
            }

            for (var i = 256; i < 512; i++)
            {
                var entity = entities[i];
                Assert.IsTrue(m_Manager.Exists(entity));
            }
        }

        [Test]
        public void DoNotInstantiateCleanup()
        {
            var entity0 = m_Manager.CreateEntity(
                typeof(EcsTestData),
                typeof(EcsTestSharedComp),
                typeof(EcsCleanup1)
            );

            m_Manager.Instantiate(entity0);

            VerifyComponentCount<EcsCleanup1>(1);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity safety checks")]
        public void InstantiateResidueEntityThrows()
        {
            var entity0 = m_Manager.CreateEntity(
                typeof(EcsTestData),
                typeof(EcsCleanup1)
            );

            m_Manager.DestroyEntity(entity0);
            Assert.Throws<ArgumentException>(() => m_Manager.Instantiate(entity0));
        }

        [Test]
        public void DeleteFromEntity()
        {
            var entities = new Entity[512];

            for (var i = 0; i < 512; i++)
            {
                var entity = m_Manager.CreateEntity(
                    typeof(EcsTestData),
                    typeof(EcsCleanup1)
                );
                entities[i] = entity;

                m_Manager.SetComponentData(entity, new EcsTestData(i));
                m_Manager.SetComponentData(entity, new EcsCleanup1(i));
            }

            VerifyComponentCount<EcsTestData>(512);

            for (var i = 0; i < 512; i++)
            {
                var entity = entities[i];
                m_Manager.DestroyEntity(entity);
            }

            VerifyComponentCount<EcsTestData>(0);
            VerifyComponentCount<EcsCleanup1>(512);

            var group = m_Manager.CreateEntityQuery(
                ComponentType.Exclude<EcsTestData>(),
                ComponentType.ReadWrite<EcsCleanup1>());

            for (var i = 0; i < 512; i++)
            {
                var entity = entities[i];
                m_Manager.RemoveComponent(entity, typeof(EcsCleanup1));
            }

            VerifyComponentCount<EcsCleanup1>(0);

            for (var i = 0; i < 512; i++)
            {
                var entity = entities[i];
                Assert.IsFalse(m_Manager.Exists(entity));
            }
        }

        [Test]
        public void DeleteFromEntityQuery()
        {
            var entities = new Entity[512];

            for (var i = 0; i < 512; i++)
            {
                var entity = m_Manager.CreateEntity(
                    typeof(EcsTestData),
                    typeof(EcsCleanup1)
                );
                entities[i] = entity;

                m_Manager.SetComponentData(entity, new EcsTestData(i));
                m_Manager.SetComponentData(entity, new EcsCleanup1(i));
            }

            VerifyComponentCount<EcsTestData>(512);

            for (var i = 0; i < 512; i++)
            {
                var entity = entities[i];
                m_Manager.DestroyEntity(entity);
            }

            VerifyComponentCount<EcsTestData>(0);
            VerifyComponentCount<EcsCleanup1>(512);

            var group = m_Manager.CreateEntityQuery(
                ComponentType.Exclude<EcsTestData>(),
                ComponentType.ReadWrite<EcsCleanup1>());

            m_Manager.RemoveComponent(group, typeof(EcsCleanup1));

            VerifyComponentCount<EcsCleanup1>(0);

            for (var i = 0; i < 512; i++)
            {
                var entity = entities[i];
                Assert.IsFalse(m_Manager.Exists(entity));
            }
        }

        [Test]
        public void DeleteTagFromEntityQuery()
        {
            var entities = new Entity[512];

            for (var i = 0; i < 512; i++)
            {
                var entity = m_Manager.CreateEntity(
                    typeof(EcsTestData),
                    typeof(EcsCleanupTag1)
                );
                entities[i] = entity;

                m_Manager.SetComponentData(entity, new EcsTestData(i));
            }

            VerifyComponentCount<EcsTestData>(512);

            for (var i = 0; i < 512; i++)
            {
                var entity = entities[i];
                m_Manager.DestroyEntity(entity);
            }

            VerifyComponentCount<EcsTestData>(0);
            VerifyComponentCount<EcsCleanupTag1>(512);

            var group = m_Manager.CreateEntityQuery(
                ComponentType.Exclude<EcsTestData>(),
                ComponentType.ReadWrite<EcsCleanupTag1>());

            m_Manager.RemoveComponent(group, typeof(EcsCleanupTag1));

            VerifyComponentCount<EcsCleanupTag1>(0);

            for (var i = 0; i < 512; i++)
            {
                var entity = entities[i];
                Assert.IsFalse(m_Manager.Exists(entity));
            }
        }

        [Test]
        public void DestroyCleanupEntitySecondTimeIsIgnored()
        {
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestSharedComp), typeof(EcsCleanup1));
            m_Manager.SetComponentData(entity1, new EcsTestData(1));
            m_Manager.SetComponentData(entity1, new EcsCleanup1(101));
            m_Manager.SetSharedComponentManaged(entity1, new EcsTestSharedComp(42));
            m_Manager.DestroyEntity(entity1);
            var chunkBefore = m_Manager.GetChunk(entity1);
            var entity2 = entity1;
            // fill up chunk
            for (int i = 2; chunkBefore == m_Manager.GetChunk(entity2); ++i)
            {
                entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestSharedComp), typeof(EcsCleanup1));
                m_Manager.SetComponentData(entity2, new EcsTestData(i));
                m_Manager.SetComponentData(entity2, new EcsCleanup1(i + 100));
                m_Manager.SetSharedComponentManaged(entity2, new EcsTestSharedComp(42));
                m_Manager.DestroyEntity(entity2);
            }

            m_Manager.DestroyEntity(entity1);
            var chunkAfter = m_Manager.GetChunk(entity1);

            Assert.AreEqual(chunkBefore, chunkAfter);
        }

        struct CleanupShared : ICleanupSharedComponentData
        {
            public int Value;
        }

        [Test]
        public void CleanupSharedKeepsValueAfterDestroy()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddSharedComponentManaged(entity, new CleanupShared { Value = 123 });
            m_Manager.DestroyEntity(entity);
            EntitiesAssert.ContainsOnly(m_Manager, EntityMatch.Exact<CleanupEntity>(new CleanupShared { Value = 123 }));
        }
    }
}
