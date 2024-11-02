using System;
using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Tests
{
    class SizeTests : ECSTestsFixture
    {
#pragma warning disable 0219 // assigned but its value is never used
        [Test]
        public void SIZ_TagComponentDoesNotChangeCapacity()
        {
            var entity0 = m_Manager.CreateEntity(typeof(EcsTestData));
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestTag));

            unsafe {
                var access = m_Manager.GetCheckedEntityDataAccess();
                var ecs = access->EntityComponentStore;

                // a system ran, the version should match the global
                var chunk0 = access->EntityComponentStore->GetChunk(entity0);
                var chunk1 = access->EntityComponentStore->GetChunk(entity1);
                var archetype0 = access->EntityComponentStore->GetArchetype(chunk0);
                var archetype1 = access->EntityComponentStore->GetArchetype(chunk1);

                ChunkDataUtility.GetIndexInTypeArray(archetype0, TypeManager.GetTypeIndex<EcsTestData2>());

                Assert.True(ChunkDataUtility.AreLayoutCompatible(archetype0, archetype1));
            }
        }

        [Test]
        public void SIZ_TagComponentZeroSize()
        {
            var entity0 = m_Manager.CreateEntity(typeof(EcsTestTag));

            unsafe {
                var access = m_Manager.GetCheckedEntityDataAccess();
                var ecs = access->EntityComponentStore;
                // a system ran, the version should match the global
                var chunk0 = ecs->GetChunk(entity0);
                var archetype0 = ecs->GetArchetype(chunk0);
                var indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(archetype0, TypeManager.GetTypeIndex<EcsTestTag>());

                Assert.AreEqual(0, archetype0->SizeOfs[indexInTypeArray]);
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires component type safety checks")]
        unsafe public void SIZ_TagThrowsOnGetComponentData()
        {
            var entity0 = m_Manager.CreateEntity(typeof(EcsTestTag));

            Assert.Throws<ArgumentException>(() =>
            {
                var data = m_Manager.GetComponentData<EcsTestTag>(entity0);
            });
            Assert.Throws<ArgumentException>(() =>
            {
                m_Manager.GetComponentDataRawRW(entity0, ComponentType.ReadWrite<EcsTestTag>().TypeIndex);
            });
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires component type safety checks")]
        unsafe public void SIZ_TagThrowsOnSetComponentData()
        {
            var entity0 = m_Manager.CreateEntity(typeof(EcsTestTag));

            Assert.Throws<ArgumentException>(() =>
            {
                m_Manager.SetComponentData(entity0, default(EcsTestTag));
            });
            Assert.Throws<ArgumentException>(() =>
            {
                var value = new EcsTestTag();
                m_Manager.SetComponentDataRaw(entity0, ComponentType.ReadWrite<EcsTestTag>().TypeIndex, &value, sizeof(EcsTestTag));
            });
        }

        [Test]
        public void SIZ_TagCanAddComponentData()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddComponentData(entity, default(EcsTestTag));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(entity));
        }

        [Test]
        public void SIZ_TagThrowsOnComponentLookup()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestTag));
            var fromEntity = m_Manager.GetComponentLookup<EcsTestTag>();
            Assert.IsTrue(fromEntity.HasComponent(entity));
            var res = fromEntity[entity];
            Assert.AreEqual(res , default(EcsTestTag));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires component safety checks")]
        public void SIZ_TagCanGetNativeArrayFromArchetypeChunk()
        {
            m_Manager.CreateEntity(typeof(EcsTestTag));
            var group = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestTag>());
            var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            group.Dispose();

            var tagType = m_Manager.GetComponentTypeHandle<EcsTestTag>(false);

            Assert.AreEqual(1, ArchetypeChunkArray.TotalEntityCountInChunksIgnoreFiltering(chunks));

            for (int i = 0; i < chunks.Length; i++)
            {
                var chunk = chunks[i];
                Assert.IsTrue(chunk.Has(ref tagType));
                Assert.DoesNotThrow(() =>
                {
                    chunk.GetNativeArray(ref tagType);
                });
            }
        }

#pragma warning restore 0219

        unsafe struct TestTooBig : IComponentData
        {
            fixed byte Value[32*1024];
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires component type safety checks")]
        public void ThrowsWhenTooLargeCreate()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                m_Manager.CreateEntity(typeof(TestTooBig));
            });
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires component type safety checks")]
        public void ThrowsWhenTooLargeAddComponent()
        {
            var entity = m_Manager.CreateEntity();
            Assert.Throws<InvalidOperationException>(() =>
            {
                m_Manager.AddComponent<TestTooBig>(entity);
            });
        }
    }
}
