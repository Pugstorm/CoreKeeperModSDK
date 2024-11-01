using System;
using NUnit.Framework;

namespace Unity.Entities.Tests.ForEachCodegen
{
    partial class ForEachEntityInfoAccessTests : ECSTestsFixture
    {
        SystemBase_TestSystem m_TestSystem;
        static Entity s_ForeachEntity;
        static Entity s_TestEntity;

        [SetUp]
        public void SetUp()
        {
            m_TestSystem = World.GetOrCreateSystemManaged<SystemBase_TestSystem>();
            var foreachArchetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestComponentWithBool));
            s_ForeachEntity = m_Manager.CreateEntity(foreachArchetype);
            s_TestEntity = m_Manager.CreateEntity();
            var entityBuffer = m_Manager.AddBuffer<EcsComplexEntityRefElement>(s_TestEntity);
            entityBuffer.ResizeUninitialized(4);
            entityBuffer[0] = new EcsComplexEntityRefElement {Entity = s_TestEntity};
            entityBuffer[1] = new EcsComplexEntityRefElement {Entity = s_TestEntity};
            entityBuffer[2] = new EcsComplexEntityRefElement {Entity = Entity.Null};
            entityBuffer[3] = new EcsComplexEntityRefElement {Entity = s_TestEntity};
        }

        internal enum ScheduleType
        {
            Run,
            Schedule,
            ScheduleParallel
        }

        public partial class SystemBase_TestSystem : SystemBase
        {
            protected override void OnUpdate() {}

            public void SystemBaseExists_Basic(ScheduleType scheduleType)
            {
                var entity = s_TestEntity;
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.ForEach((ref EcsTestComponentWithBool td) => { td.value = SystemAPI.Exists(entity); }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities.ForEach((ref EcsTestComponentWithBool td) => { td.value = SystemAPI.Exists(entity); }).Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities.ForEach((ref EcsTestComponentWithBool td) => { td.value = SystemAPI.Exists(entity); }).ScheduleParallel();
                        break;
                }

                Dependency.Complete();
                Assert.IsTrue(EntityManager.GetComponentData<EcsTestComponentWithBool>(s_ForeachEntity).value);
            }

            public void SystemBaseExists_WorksWithGetBufferLookup(ScheduleType scheduleType)
            {
                var entity = s_TestEntity;
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.ForEach((ref EcsTestComponentWithBool td) => td.value = SystemAPI.Exists(GetBufferLookup<EcsComplexEntityRefElement>(true)[entity][0].Entity)).Run();
                        break;

                    case ScheduleType.Schedule:
                        Entities.ForEach((ref EcsTestComponentWithBool td) => td.value = SystemAPI.Exists(GetBufferLookup<EcsComplexEntityRefElement>(true)[entity][0].Entity)).Schedule();
                        break;

                    case ScheduleType.ScheduleParallel:
                        Entities.ForEach((ref EcsTestComponentWithBool td) => td.value = SystemAPI.Exists(GetBufferLookup<EcsComplexEntityRefElement>(true)[entity][0].Entity)).ScheduleParallel();
                        break;
                }

                Dependency.Complete();
                Assert.IsTrue(EntityManager.GetComponentData<EcsTestComponentWithBool>(s_ForeachEntity).value);
            }

            /* Todo: enable on fix: DOTS-5239
            public void SystemBaseExists_WorksWithGetBufferLookupInLine(Entity entity, ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.ForEach((ref EcsTestComponentWithBool td) => { td.value = Exists(GetBufferLookup<EcsComplexEntityRefElement>(true)[entity][0].Entity); }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities.ForEach((ref EcsTestComponentWithBool td) => { td.value = Exists(GetBufferLookup<EcsComplexEntityRefElement>(true)[entity][0].Entity); }).Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities.ForEach((ref EcsTestComponentWithBool td) => { td.value = Exists(GetBufferLookup<EcsComplexEntityRefElement>(true)[entity][0].Entity); }).ScheduleParallel();
                        break;
                }

                Dependency.Complete();
                Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
            }*/

            public void SystemBaseExists_MultipleWorksWithGetBufferLookup(ScheduleType scheduleType)
            {
                var entity = s_TestEntity;
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.ForEach((ref EcsTestData td) =>
                        {
                            var entityHolder = GetBufferLookup<EcsComplexEntityRefElement>(true)[entity];
                            td.value += SystemAPI.Exists(entityHolder[0].Entity)?1:0;
                            td.value += SystemAPI.Exists(entityHolder[1].Entity)?2:0;
                            td.value += SystemAPI.Exists(entityHolder[2].Entity)?4:0; // Doesn't exist
                            td.value += SystemAPI.Exists(entityHolder[3].Entity)?8:0;
                        }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities.ForEach((ref EcsTestData td) =>
                        {
                            var entityHolder = GetBufferLookup<EcsComplexEntityRefElement>(true)[entity];
                            td.value += SystemAPI.Exists(entityHolder[0].Entity)?1:0;
                            td.value += SystemAPI.Exists(entityHolder[1].Entity)?2:0;
                            td.value += SystemAPI.Exists(entityHolder[2].Entity)?4:0; // Doesn't exist
                            td.value += SystemAPI.Exists(entityHolder[3].Entity)?8:0;
                        }).Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities.ForEach((ref EcsTestData td) =>
                        {
                            var entityHolder = GetBufferLookup<EcsComplexEntityRefElement>(true)[entity];
                            td.value += SystemAPI.Exists(entityHolder[0].Entity)?1:0;
                            td.value += SystemAPI.Exists(entityHolder[1].Entity)?2:0;
                            td.value += SystemAPI.Exists(entityHolder[2].Entity)?4:0; // Doesn't exist
                            td.value += SystemAPI.Exists(entityHolder[3].Entity)?8:0;
                        }).ScheduleParallel();
                        break;
                }
                Dependency.Complete();
                Assert.AreEqual(1|2|8,EntityManager.GetComponentData<EcsTestData>(s_ForeachEntity).value);
            }

            public void SystemBaseGetEntityStorageInfoLookup_MultipleWorksWithGetBufferLookup(ScheduleType scheduleType)
            {
                var entity = s_TestEntity;
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.ForEach((ref EcsTestData td) =>
                        {
                            var storageInfoLookup = GetEntityStorageInfoLookup();
                            var entityHolder = GetBufferLookup<EcsComplexEntityRefElement>(true)[entity];
                            td.value += storageInfoLookup.Exists(entityHolder[0].Entity)?1:0;
                            td.value += storageInfoLookup.Exists(entityHolder[1].Entity)?2:0;
                            td.value += storageInfoLookup.Exists(entityHolder[2].Entity)?4:0; // Doesn't exist
                            td.value += storageInfoLookup.Exists(entityHolder[3].Entity)?8:0;
                        }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities.ForEach((ref EcsTestData td) =>
                        {
                            var storageInfoLookup = GetEntityStorageInfoLookup();
                            var entityHolder = GetBufferLookup<EcsComplexEntityRefElement>(true)[entity];
                            td.value += storageInfoLookup.Exists(entityHolder[0].Entity)?1:0;
                            td.value += storageInfoLookup.Exists(entityHolder[1].Entity)?2:0;
                            td.value += storageInfoLookup.Exists(entityHolder[2].Entity)?4:0; // Doesn't exist
                            td.value += storageInfoLookup.Exists(entityHolder[3].Entity)?8:0;
                        }).Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities.ForEach((ref EcsTestData td) =>
                        {
                            var storageInfoLookup = GetEntityStorageInfoLookup();
                            var entityHolder = GetBufferLookup<EcsComplexEntityRefElement>(true)[entity];
                            td.value += storageInfoLookup.Exists(entityHolder[0].Entity)?1:0;
                            td.value += storageInfoLookup.Exists(entityHolder[1].Entity)?2:0;
                            td.value += storageInfoLookup.Exists(entityHolder[2].Entity)?4:0; // Doesn't exist
                            td.value += storageInfoLookup.Exists(entityHolder[3].Entity)?8:0;
                        }).ScheduleParallel();
                        break;
                }
                Dependency.Complete();
                Assert.AreEqual(1|2|8,EntityManager.GetComponentData<EcsTestData>(s_ForeachEntity).value);
            }
        }

        [Test]
        public void SystemBaseExists_Basic([Values] ScheduleType scheduleType)
            => m_TestSystem.SystemBaseExists_Basic(scheduleType);

        [Test]
        public void SystemBaseExists_WorksWithGetBufferLookup([Values] ScheduleType scheduleType)
            => m_TestSystem.SystemBaseExists_WorksWithGetBufferLookup(scheduleType);

        /* Todo: enable on fix: DOTS-5239
        [Test]
        public void SystemBaseExists_WorksWithGetBufferLookup([Values] ScheduleType scheduleType)
            => TestSystem.SystemBaseExists_WorksWithGetBufferLookupInLine(TestEntity2, scheduleType);
        */

        [Test]
        public void SystemBaseExists_MultipleWorksWithGetBufferLookup([Values] ScheduleType scheduleType)
            => m_TestSystem.SystemBaseExists_MultipleWorksWithGetBufferLookup(scheduleType);

        [Test]
        public void SystemBaseGetEntityStorageInfoLookup_MultipleWorksWithGetBufferLookup([Values] ScheduleType scheduleType)
            => m_TestSystem.SystemBaseGetEntityStorageInfoLookup_MultipleWorksWithGetBufferLookup(scheduleType);
    }
}
