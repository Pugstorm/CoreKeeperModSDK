using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Tests
{
    [TestFixture]
    sealed class CopyAndReplaceTests : EntityDifferTestFixture
    {
        //@TODO: Test class based components (Currently doesn't work)
        //@TODO: Test number of created / destroyed chunk counts to be what is expected (for perf)
        //@TODO: Handle chunk component versions. It seems likely that they can currently go out of sync between two worlds so systems might not pick up a change. (Currently doesn't work)
        //@TODO: Test for blob data (Manually tested)
        //@TODO: Test that Copy&Replace doesn't modify, add or remove cleanu components. But also don't change chunk layout. (Currently doesn't work)

        unsafe void CreateTestData(out Entity entity, out Entity metaEntity, int value, int componentChunkValue)
        {
            entity = SrcEntityManager.CreateEntity();
            SrcEntityManager.AddComponentData(entity, new EcsTestData(value));
            SrcEntityManager.AddSharedComponent(entity, new EcsTestSharedComp(6));
            SrcEntityManager.AddChunkComponentData(SrcEntityManager.UniversalQuery, new EcsTestData2(7));

            metaEntity = SrcEntityManager.GetChunk(entity).m_Chunk.MetaChunkEntity;

            Assert.AreEqual(7, SrcEntityManager.GetComponentData<EcsTestData2>(metaEntity).value0);
        }

        unsafe void TestValues(Entity entity, Entity  metaEntity, int componentDataValue, int componentChunkValue)
        {
            Assert.AreEqual(componentDataValue, DstEntityManager.GetComponentData<EcsTestData>(entity).value);
            Assert.AreEqual(6, DstEntityManager.GetSharedComponent<EcsTestSharedComp>(entity).value);
            Assert.AreEqual(componentChunkValue, DstEntityManager.GetChunkComponentData<EcsTestData2>(entity).value0);

            Assert.AreEqual(metaEntity, DstEntityManager.GetChunk(entity).m_Chunk.MetaChunkEntity);

            SrcEntityManager.Debug.CheckInternalConsistency();
            DstEntityManager.Debug.CheckInternalConsistency();
        }

        [Test]
        public void ReplaceWithEnabledBits()
        {
            var srcArchetype = SrcEntityManager.CreateArchetype(typeof(EcsTestDataEnableable));
            using var srcEntities = SrcEntityManager.CreateEntity(srcArchetype, 2, SrcWorld.UpdateAllocator.ToAllocator);
            SrcEntityManager.SetComponentData(srcEntities[0], new EcsTestDataEnableable { value = 17 });
            SrcEntityManager.SetComponentData(srcEntities[1], new EcsTestDataEnableable { value = 23 });
            SrcEntityManager.SetComponentEnabled<EcsTestDataEnableable>(srcEntities[0], false);

#pragma warning disable CS0618 // Type or member is obsolete
            DstEntityManager.CopyAndReplaceEntitiesFrom(SrcEntityManager);
#pragma warning restore CS0618 // Type or member is obsolete

            using var dstEntities = DstEntityManager.UniversalQuery.ToEntityArray(DstWorld.UpdateAllocator.ToAllocator);
            Assert.IsFalse(DstEntityManager.IsComponentEnabled<EcsTestDataEnableable>(dstEntities[0]));
            Assert.AreEqual(17, DstEntityManager.GetComponentData<EcsTestDataEnableable>(dstEntities[0]).value);
            Assert.IsTrue(DstEntityManager.IsComponentEnabled<EcsTestDataEnableable>(dstEntities[1]));
            Assert.AreEqual(23, DstEntityManager.GetComponentData<EcsTestDataEnableable>(dstEntities[1]).value);
        }

        [Test]
        public void ReplaceEntityManagerContents([Values] bool createToReplaceEntity)
        {
            CreateTestData(out var entity, out var metaEntity, 5, 7);

            if (createToReplaceEntity)
                DstEntityManager.CreateEntity(typeof(EcsTestData), typeof(EcsTestSharedComp));

#if ENTITY_STORE_V1
#pragma warning disable CS0618 // Type or member is obsolete
            DstEntityManager.CopyAndReplaceEntitiesFrom(SrcEntityManager);
#pragma warning restore CS0618 // Type or member is obsolete
#else
            var remap = SrcEntityManager.CreateEntityRemapArray(Allocator.Temp);

#pragma warning disable CS0618 // Type or member is obsolete
            DstEntityManager.CopyAndReplaceEntitiesFrom(SrcEntityManager, remap);
#pragma warning restore CS0618 // Type or member is obsolete

            entity = EntityRemapUtility.RemapEntity(ref remap, entity);
            metaEntity = DstEntityManager.GetChunk(entity).m_Chunk.MetaChunkEntity;
#endif

            Assert.AreEqual(1, SrcEntityManager.UniversalQuery.CalculateEntityCount());
            Assert.AreEqual(1, DstEntityManager.UniversalQuery.CalculateEntityCount());

            TestValues(entity, metaEntity, 5, 7);
        }

        [Test]
        public void ReplaceChangedEntities()
        {
            CreateTestData(out var entity, out var metaEntity, 5, 7);

#pragma warning disable CS0618 // Type or member is obsolete
            DstEntityManager.CopyAndReplaceEntitiesFrom(SrcEntityManager);
#pragma warning restore CS0618 // Type or member is obsolete

            SrcEntityManager.SetComponentData(entity, new EcsTestData(11));

#if ENTITY_STORE_V1
#pragma warning disable CS0618 // Type or member is obsolete
            DstEntityManager.CopyAndReplaceEntitiesFrom(SrcEntityManager);
#pragma warning restore CS0618 // Type or member is obsolete
#else
            var remap = SrcEntityManager.CreateEntityRemapArray(Allocator.Temp);

#pragma warning disable CS0618 // Type or member is obsolete
            DstEntityManager.CopyAndReplaceEntitiesFrom(SrcEntityManager, remap);
#pragma warning restore CS0618 // Type or member is obsolete

            entity = EntityRemapUtility.RemapEntity(ref remap, entity);
            metaEntity = DstEntityManager.GetChunk(entity).m_Chunk.MetaChunkEntity;
#endif

            TestValues(entity, metaEntity, 11, 7);
        }

        [Test]
        public void ReplaceChangedChunkComponent()
        {
            CreateTestData(out var entity, out var metaEntity, 5, 7);

            SrcEntityManager.SetComponentData(metaEntity, new EcsTestData2(11));

#if ENTITY_STORE_V1
#pragma warning disable CS0618 // Type or member is obsolete
            DstEntityManager.CopyAndReplaceEntitiesFrom(SrcEntityManager);
#pragma warning restore CS0618 // Type or member is obsolete
#else
            var remap = SrcEntityManager.CreateEntityRemapArray(Allocator.Temp);

#pragma warning disable CS0618 // Type or member is obsolete
            DstEntityManager.CopyAndReplaceEntitiesFrom(SrcEntityManager, remap);
#pragma warning restore CS0618 // Type or member is obsolete

            entity = EntityRemapUtility.RemapEntity(ref remap, entity);
            metaEntity = DstEntityManager.GetChunk(entity).m_Chunk.MetaChunkEntity;
#endif

            TestValues(entity, metaEntity, 5, 11);
        }

        [Test]
        public void ReplaceChangedNothing()
        {
            CreateTestData(out var entity, out var metaEntity, 5, 7);

#pragma warning disable CS0618 // Type or member is obsolete
            DstEntityManager.CopyAndReplaceEntitiesFrom(SrcEntityManager);
#pragma warning restore CS0618 // Type or member is obsolete


#if ENTITY_STORE_V1
#pragma warning disable CS0618 // Type or member is obsolete
            DstEntityManager.CopyAndReplaceEntitiesFrom(SrcEntityManager);
#pragma warning restore CS0618 // Type or member is obsolete
#else
            var remap = SrcEntityManager.CreateEntityRemapArray(Allocator.Temp);

#pragma warning disable CS0618 // Type or member is obsolete
            DstEntityManager.CopyAndReplaceEntitiesFrom(SrcEntityManager, remap);
#pragma warning restore CS0618 // Type or member is obsolete

            entity = EntityRemapUtility.RemapEntity(ref remap, entity);
            metaEntity = DstEntityManager.GetChunk(entity).m_Chunk.MetaChunkEntity;
#endif
            TestValues(entity, metaEntity, 5, 7);
        }

        [Test]
        public void Replace_AfterCreatingAndDestroyingAllEntities()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            DstEntityManager.CopyAndReplaceEntitiesFrom(SrcEntityManager);
#pragma warning restore CS0618 // Type or member is obsolete

            var emptyArchetype = DstEntityManager.CreateArchetype();
            DstEntityManager.CreateEntity(emptyArchetype, 10000);
            DstEntityManager.DestroyEntity(DstEntityManager.UniversalQuery);

#pragma warning disable CS0618 // Type or member is obsolete
            DstEntityManager.CopyAndReplaceEntitiesFrom(SrcEntityManager);
#pragma warning restore CS0618 // Type or member is obsolete

            Assert.AreEqual(0, DstEntityManager.UniversalQuery.CalculateChunkCount());
        }
    }
}
