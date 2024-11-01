using System;
using NUnit.Framework;
using Unity.Collections;

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
    class CleanupBufferElementTests : ECSTestsFixture
    {
        void VerifyComponentCount<T>(int expectedCount)
            where T : IComponentData
        {
            var group = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<T>());
            var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            group.Dispose();
            Assert.AreEqual(expectedCount, ArchetypeChunkArray.TotalEntityCountInChunksIgnoreFiltering(chunks));
        }

        void VerifyBufferCount<T>(int expectedCount)
            where T : ICleanupBufferElementData
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
                typeof(EcsIntCleanupElement)
            );

            m_Manager.SetComponentData(entity, new EcsTestData(1));
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entity);
            buffer.Add(2);
            m_Manager.SetSharedComponentManaged(entity, new EcsTestSharedComp(3));

            VerifyComponentCount<EcsTestData>(1);

            m_Manager.DestroyEntity(entity);

            VerifyComponentCount<EcsTestData>(0);
            VerifyBufferCount<EcsIntCleanupElement>(1);

            m_Manager.RemoveComponent<EcsIntCleanupElement>(entity);

            VerifyBufferCount<EcsIntCleanupElement>(0);

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
                    typeof(EcsIntCleanupElement)
                );
                entities[i] = entity;

                m_Manager.SetComponentData(entity, new EcsTestData(i));
                var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entity);
                buffer.Add(2);
                m_Manager.SetSharedComponentManaged(entity, new EcsTestSharedComp(i % 7));
            }

            VerifyComponentCount<EcsTestData>(512);

            for (var i = 0; i < 512; i += 2)
            {
                var entity = entities[i];
                m_Manager.DestroyEntity(entity);
            }

            VerifyComponentCount<EcsTestData>(256);
            VerifyBufferCount<EcsIntCleanupElement>(512);
            VerifyQueryCount(m_Manager.CreateEntityQuery(
                ComponentType.Exclude<EcsTestData>(),
                ComponentType.ReadWrite<EcsIntCleanupElement>()), 256);

            for (var i = 0; i < 512; i += 2)
            {
                var entity = entities[i];
                m_Manager.RemoveComponent<EcsIntCleanupElement>(entity);
            }

            VerifyBufferCount<EcsIntCleanupElement>(256);

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
                    typeof(EcsIntCleanupElement)
                );
                entities[i] = entity;

                m_Manager.SetComponentData(entity, new EcsTestData(i));
                var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entity);
                buffer.Add(i);
                m_Manager.SetSharedComponentManaged(entity, new EcsTestSharedComp(i % 7));
            }

            VerifyComponentCount<EcsTestData>(512);

            for (var i = 0; i < 256; i++)
            {
                var entity = entities[i];
                m_Manager.DestroyEntity(entity);
            }

            VerifyComponentCount<EcsTestData>(256);
            VerifyBufferCount<EcsIntCleanupElement>(512);
            VerifyQueryCount(m_Manager.CreateEntityQuery(
                ComponentType.Exclude<EcsTestData>(),
                ComponentType.ReadWrite<EcsIntCleanupElement>()), 256);

            for (var i = 0; i < 256; i++)
            {
                var entity = entities[i];
                m_Manager.RemoveComponent<EcsIntCleanupElement>(entity);
            }

            VerifyBufferCount<EcsIntCleanupElement>(256);

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
                typeof(EcsIntCleanupElement)
            );

            m_Manager.Instantiate(entity0);

            VerifyBufferCount<EcsIntCleanupElement>(1);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity safety checks")]
        public void InstantiateResidueEntityThrows()
        {
            var entity0 = m_Manager.CreateEntity(
                typeof(EcsTestData),
                typeof(EcsIntCleanupElement)
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
                    typeof(EcsIntCleanupElement)
                );
                entities[i] = entity;

                m_Manager.SetComponentData(entity, new EcsTestData(i));
                var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entity);
                buffer.Add(i);
            }

            VerifyComponentCount<EcsTestData>(512);

            for (var i = 0; i < 512; i++)
            {
                var entity = entities[i];
                m_Manager.DestroyEntity(entity);
            }

            VerifyComponentCount<EcsTestData>(0);
            VerifyBufferCount<EcsIntCleanupElement>(512);

            var group = m_Manager.CreateEntityQuery(
                ComponentType.Exclude<EcsTestData>(),
                ComponentType.ReadWrite<EcsIntCleanupElement>());

            for (var i = 0; i < 512; i++)
            {
                var entity = entities[i];
                m_Manager.RemoveComponent(entity, typeof(EcsIntCleanupElement));
            }

            VerifyBufferCount<EcsIntCleanupElement>(0);

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
                    typeof(EcsIntCleanupElement)
                );
                entities[i] = entity;

                m_Manager.SetComponentData(entity, new EcsTestData(i));
                var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entity);
                buffer.Add(i);
            }

            VerifyComponentCount<EcsTestData>(512);

            for (var i = 0; i < 512; i++)
            {
                var entity = entities[i];
                m_Manager.DestroyEntity(entity);
            }

            VerifyComponentCount<EcsTestData>(0);
            VerifyBufferCount<EcsIntCleanupElement>(512);

            var group = m_Manager.CreateEntityQuery(
                ComponentType.Exclude<EcsTestData>(),
                ComponentType.ReadWrite<EcsIntCleanupElement>());

            m_Manager.RemoveComponent(group, typeof(EcsIntCleanupElement));

            VerifyBufferCount<EcsIntCleanupElement>(0);

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
                    typeof(EcsIntCleanupElement)
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
            VerifyBufferCount<EcsIntCleanupElement>(512);

            var group = m_Manager.CreateEntityQuery(
                ComponentType.Exclude<EcsTestData>(),
                ComponentType.ReadWrite<EcsIntCleanupElement>());

            m_Manager.RemoveComponent(group, typeof(EcsIntCleanupElement));

            VerifyBufferCount<EcsIntCleanupElement>(0);

            for (var i = 0; i < 512; i++)
            {
                var entity = entities[i];
                Assert.IsFalse(m_Manager.Exists(entity));
            }
        }
    }
}
