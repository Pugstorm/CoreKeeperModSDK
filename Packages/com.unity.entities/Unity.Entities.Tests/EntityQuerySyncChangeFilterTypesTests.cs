using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.Entities.Tests
{
    [TestFixture("CompleteAllJobs")]
    [TestFixture("CompleteJob1")]
    [TestFixture("CompleteJob2")]
    [TestFixture("CompleteNoJobs")]
    partial class EntityQuerySyncChangeFilterTypesTests : ECSTestsFixture
    {
        public partial class SyncChangeFilterTypes_System : SystemBase
        {
            public EntityQuery EntityQuery;

            public JobHandle SetEcsTestData1(JobHandle dependency)
            {
                return
                    Entities
                        .ForEach((ref EcsTestData data) => { data = new EcsTestData(1); })
                        .Schedule(dependency);
            }

            public JobHandle SetEcsTestData2(JobHandle dependency)
            {
                return
                    Entities
                        .ForEach((ref EcsTestData2 data) => { data = new EcsTestData2(2); })
                        .Schedule(dependency);
            }

            protected override void OnUpdate()
            {
            }

            protected override void OnCreate()
            {
            }
        }

        SyncChangeFilterTypes_System _syncChangeFilterTypesSystem => World.GetOrCreateSystemManaged<SyncChangeFilterTypes_System>();

        bool completeJob1;
        bool completeJob2;

        JobHandle job1;
        JobHandle job2;

        public EntityQuerySyncChangeFilterTypesTests(string variation)
        {
            this.completeJob1 = variation == "CompleteJob1" || variation == "CompleteAllJobs";
            this.completeJob2 = variation == "CompleteJob2" || variation == "CompleteAllJobs";
        }

        public override void Setup()
        {
            base.Setup();

            m_ManagerDebug.IncrementGlobalSystemVersion();
            m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2), ComponentType.ChunkComponent<EcsTestData3>());

            job1 = _syncChangeFilterTypesSystem.SetEcsTestData1(default);
            job2 = _syncChangeFilterTypesSystem.SetEcsTestData2(default);

            // These jobs wont actually set the change version so the query will not match any entities so no structural change is executed.
            // Otherwise BeforeStructuralChange will throw an InvalidOperationException when it calls CompleteAllJobs.
            // We want to only check for the InvalidOperationException from SyncChunkFilterTypes.
            if (completeJob1)
                job1.Complete();

            if (completeJob2)
                job2.Complete();

            _syncChangeFilterTypesSystem.EntityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2));
            _syncChangeFilterTypesSystem.EntityQuery.SetChangedVersionFilter(new ComponentType[] {ComponentType.ReadWrite<EcsTestData>(), ComponentType.ReadWrite<EcsTestData2>()});
            _syncChangeFilterTypesSystem.EntityQuery.SetChangedFilterRequiredVersion(m_Manager.GlobalSystemVersion);
        }

        public override void TearDown()
        {
            job1.Complete();
            job2.Complete();

            base.TearDown();
        }

        void AssertThrowsIfAnyJobNotCompleted(TestDelegate code)
        {
            if (completeJob1 && completeJob2)
                Assert.DoesNotThrow(code);
            else
                Assert.Throws<InvalidOperationException>(code);
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void CommandBuffer_AddComponentWithEntityQuery_CaptureAtRecord_Syncs_ChangeFilterTypes()
        {
            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                AssertThrowsIfAnyJobNotCompleted(() => cmds.AddComponent(_syncChangeFilterTypesSystem.EntityQuery,
                    typeof(EcsTestData3), EntityQueryCaptureMode.AtRecord));
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void CommandBuffer_RemoveComponentWithEntityQuery_CaptureAtRecord_Syncs_ChangeFilterTypes()
        {
            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                AssertThrowsIfAnyJobNotCompleted(() => cmds.RemoveComponent(_syncChangeFilterTypesSystem.EntityQuery,
                    typeof(EcsTestData), EntityQueryCaptureMode.AtRecord));
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void CommandBuffer_DestroyEntityWithEntityQuery_CaptureAtRecord_Syncs_ChangeFilterTypes()
        {
            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                AssertThrowsIfAnyJobNotCompleted(() => cmds.DestroyEntity(_syncChangeFilterTypesSystem.EntityQuery, EntityQueryCaptureMode.AtRecord));
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void CommandBuffer_AddSharedComponentWithEntityQuery_CaptureAtRecord_Syncs_ChangeFilterTypes()
        {
            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                AssertThrowsIfAnyJobNotCompleted(() => cmds.AddSharedComponent(_syncChangeFilterTypesSystem.EntityQuery, new EcsTestSharedComp(7), EntityQueryCaptureMode.AtRecord));
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void EntityManager_RemoveComponentWithEntityQuery_Syncs_ChangeFilterTypes()
        {
            AssertThrowsIfAnyJobNotCompleted(() => m_Manager.RemoveComponent(_syncChangeFilterTypesSystem.EntityQuery, ComponentType.ReadWrite<EcsTestData>()));
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void EntityManager_AddComponentWithEntityQuery_Syncs_ChangeFilterTypes()
        {
            AssertThrowsIfAnyJobNotCompleted(() => m_Manager.AddComponent(_syncChangeFilterTypesSystem.EntityQuery, ComponentType.ReadWrite<EcsTestData3>()));
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void EntityManager_AddChunkComponentDataWithEntityQuery_Syncs_ChangeFilterTypes()
        {
            AssertThrowsIfAnyJobNotCompleted(() => m_Manager.AddChunkComponentData(_syncChangeFilterTypesSystem.EntityQuery, new EcsTestData3(7)));
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void EntityManager_RemoveChunkComponentDataWithEntityQuery_Syncs_ChangeFilterTypes()
        {
            AssertThrowsIfAnyJobNotCompleted(() => m_Manager.RemoveChunkComponentData<EcsTestData3>(_syncChangeFilterTypesSystem.EntityQuery));
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void EntityManager_AddSharedComponentDataWithEntityQuery_Syncs_ChangeFilterTypes()
        {
            AssertThrowsIfAnyJobNotCompleted(() => m_Manager.AddSharedComponentManaged(_syncChangeFilterTypesSystem.EntityQuery, new EcsTestSharedComp(7)));
        }
    }
}
