using Unity.Burst;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

#pragma warning disable 649

namespace Unity.Entities.Tests.ForEachCodegen
{
    partial class ForEachComponentAccessTests : ECSTestsFixture
    {
        SystemBase_TestSystem TestSystem;
        static Entity TestEntity1;
        static Entity TestEntity2;

        [SetUp]
        public void SetUp()
        {
            TestSystem = World.GetOrCreateSystemManaged<SystemBase_TestSystem>();

            var myArch = m_Manager.CreateArchetype(
                ComponentType.ReadWrite<EcsTestDataEntity>(),
                ComponentType.ReadWrite<EcsTestData>());

            TestEntity1 = m_Manager.CreateEntity(myArch);
            TestEntity2 = m_Manager.CreateEntity(myArch);
            m_Manager.SetComponentData(TestEntity1, new EcsTestDataEntity() { value0 = 1, value1 = TestEntity2 });
            m_Manager.SetComponentData(TestEntity1, new EcsTestData() { value = 1 });
            m_Manager.SetComponentData(TestEntity2, new EcsTestDataEntity() { value0 = 2, value1 = TestEntity1 });
            m_Manager.SetComponentData(TestEntity2, new EcsTestData() { value = 2 });
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

            public void HasComponent_HasComponent(Entity entity, ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.ForEach((ref EcsTestData td) => { td.value = SystemAPI.HasComponent<EcsTestDataEntity>(entity) ? 333 : 0; }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities.ForEach((ref EcsTestData td) => { td.value = SystemAPI.HasComponent<EcsTestDataEntity>(entity) ? 333 : 0; }).Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities.ForEach((ref EcsTestData td) => { td.value = SystemAPI.HasComponent<EcsTestDataEntity>(entity) ? 333 : 0; }).ScheduleParallel();
                        break;
                }

                Dependency.Complete();
            }

            public void GetComponent_GetsValue(Entity entity, ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.ForEach((ref EcsTestData td) => { td.value = SystemAPI.GetComponent<EcsTestDataEntity>(entity).value0; }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities.ForEach((ref EcsTestData td) => { td.value = SystemAPI.GetComponent<EcsTestDataEntity>(entity).value0; }).Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities.ForEach((ref EcsTestData td) => { td.value = SystemAPI.GetComponent<EcsTestDataEntity>(entity).value0; }).ScheduleParallel();
                        break;
                }

                Dependency.Complete();
            }

            public void SetComponent_SetsValue(Entity entity, ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.ForEach((ref EcsTestDataEntity tde) => { SystemAPI.SetComponent(entity, new EcsTestData(){ value = 2 }); }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities.ForEach((ref EcsTestDataEntity tde) => { SystemAPI.SetComponent(entity, new EcsTestData(){ value = 2 }); }).Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        // Flagged as an DC0063 error at compile-time with sourcegen
                        break;
                }

                Dependency.Complete();
            }

            public void SetComponent_WithArgumentWithElementAccessor_SetsValue(Entity entity, ScheduleType scheduleType)
            {
                var entityArray = new NativeArray<Entity>(1, Allocator.TempJob);
                entityArray[0] = entity;

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.ForEach((ref EcsTestDataEntity tde) => { SystemAPI.SetComponent(entityArray[0], new EcsTestData() { value = 2 }); }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities.ForEach((ref EcsTestDataEntity tde) => { SystemAPI.SetComponent(entityArray[0], new EcsTestData() { value = 2 }); }).Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        // Flagged as an DC0063 error at compile-time with sourcegen
                        break;
                }

                Dependency.Complete();

                entityArray.Dispose();
            }

            public void GetComponentThroughGetComponentLookup_GetsValue(Entity entity, ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.ForEach((ref EcsTestData td) => { td.value = SystemAPI.GetComponentLookup<EcsTestDataEntity>(true)[entity].value0; }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities.ForEach((ref EcsTestData td) => { td.value = SystemAPI.GetComponentLookup<EcsTestDataEntity>(true)[entity].value0; }).Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities.ForEach((ref EcsTestData td) => { td.value = SystemAPI.GetComponentLookup<EcsTestDataEntity>(true)[entity].value0; }).ScheduleParallel();
                        break;
                }

                Dependency.Complete();
            }

            public void SetComponentThroughGetComponentLookup_SetsValue(Entity entity, ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.ForEach((ref EcsTestDataEntity tde) =>
                        {
                            var lookup = SystemAPI.GetComponentLookup<EcsTestData>(false);
                            lookup[entity] = new EcsTestData(){ value = 2 };
                        }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities.ForEach((ref EcsTestDataEntity tde) =>
                        {
                            var lookup = SystemAPI.GetComponentLookup<EcsTestData>(false);
                            lookup[entity] = new EcsTestData(){ value = 2 };
                        }).Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        // Flagged as an DC0063 error at compile-time with sourcegen
                        break;
                }

                Dependency.Complete();
            }

            static int GetComponentDataValueByMethod(ComponentLookup<EcsTestData> lookup, Entity entity)
            {
                return lookup[entity].value;
            }

            public void GetComponentThroughGetComponentLookupPassedToMethod_GetsValue(Entity entity)
            {
                Entities.ForEach((ref EcsTestDataEntity td) => { td.value0 = GetComponentDataValueByMethod(SystemAPI.GetComponentLookup<EcsTestData>(true), entity); }).Run();
            }

            static void SetComponentDataValueByMethod(ComponentLookup<EcsTestData> lookup, Entity entity, int value)
            {
                lookup[entity] = new EcsTestData() { value = value };
            }

            public void SetComponentThroughGetComponentLookupPassedToMethod_GetsValue(Entity entity)
            {
                Entities.ForEach((ref EcsTestDataEntity td) => { SetComponentDataValueByMethod(SystemAPI.GetComponentLookup<EcsTestData>(false), entity, 2); }).Run();
            }

            public void GetComponentFromComponentDataField_GetsValue()
            {
                Entities.ForEach((ref EcsTestDataEntity tde) => { tde.value0 = SystemAPI.GetComponent<EcsTestData>(tde.value1).value; }).Schedule();
                Dependency.Complete();
            }

            public void SetComponentViaNamedArgument_SetsValue()
            {
                Entities
                    .WithoutBurst()
                    .ForEach((Entity entity, in EcsTestDataEntity tde) =>
                    {
                        SystemAPI.SetComponent(entity: entity, new EcsTestData() { value = 2 });
                        SystemAPI.SetComponent(component: new EcsTestData() { value = 2 }, entity: entity);
                    }).Schedule();
                Dependency.Complete();
            }

            public void GetComponentFromStaticField_GetsValue()
            {
                Entities.ForEach((ref EcsTestDataEntity tde) => { tde.value0 = SystemAPI.GetComponent<EcsTestData>(tde.value1).value; }).Schedule();
                Dependency.Complete();
            }

            public void MultipleGetComponents_GetsValues()
            {
                Entities.ForEach((ref EcsTestDataEntity tde) =>
                {
                    tde.value0 = SystemAPI.GetComponent<EcsTestData>(tde.value1).value + SystemAPI.GetComponent<EcsTestData>(tde.value1).value;
                }).Schedule();
                Dependency.Complete();
            }

            public void GetComponentSetComponent_SetsValue()
            {
                Entities
                    .WithoutBurst()
                    .ForEach((Entity entity, in EcsTestDataEntity tde) =>
                    {
                        SystemAPI.SetComponent(entity, SystemAPI.GetComponent<EcsTestData>(tde.value1));
                    }).Schedule();
                Dependency.Complete();
            }

            public void GetComponentSetComponent_ThroughComponentLookup_SetsValue()
            {
                Entities
                    .WithoutBurst()
                    .ForEach((Entity entity, in EcsTestDataEntity tde) =>
                    {
                        var lookupWrite = GetComponentLookup<EcsTestData>(false);
                        lookupWrite[entity] = new EcsTestData() {value = GetComponentLookup<EcsTestData>(true)[tde.value1].value};
                    }).Schedule();
                Dependency.Complete();
            }

            public void GetSameComponentInTwoEntitiesForEach_GetsValue()
            {
                Entities.ForEach((Entity entity, ref EcsTestDataEntity tde) =>
                {
                    tde.value0 += SystemAPI.GetComponent<EcsTestData>(tde.value1).value;
                    tde.value0 += SystemAPI.GetComponent<EcsTestData>(tde.value1).value;
                }).Schedule();
                Entities.ForEach((Entity entity, ref EcsTestDataEntity tde) =>
                {
                    tde.value0 += SystemAPI.GetComponent<EcsTestData>(tde.value1).value;
                    tde.value0 += SystemAPI.GetComponent<EcsTestData>(tde.value1).value;
                }).Schedule();
                Dependency.Complete();
            }

            public void ComponentAccessInEntitiesForEachWithNestedCaptures_ComponentAccessWorks()
            {
                var outerCapture = 2;
                {
                    var innerCapture = 10;
                    Entities
                        .ForEach((Entity entity, in EcsTestDataEntity tde) =>
                    {
                        if (SystemAPI.HasComponent<EcsTestDataEntity>(entity))
                            outerCapture = 10;

                        var val = SystemAPI.GetComponent<EcsTestData>(tde.value1).value;
                        SystemAPI.SetComponent(entity, new EcsTestData(val * innerCapture * outerCapture));
                    }).Run();
                }
            }

            public void GetComponentLookupInEntitiesForEachWithNestedCaptures_ComponentAccessWorks()
            {
                var outerCapture = 2;
                {
                    var innerCapture = 10;
                    Entities
                        .ForEach((Entity entity, in EcsTestDataEntity tde) =>
                    {
                        if (SystemAPI.HasComponent<EcsTestDataEntity>(entity))
                            outerCapture = 10;

                        var lookupRead = SystemAPI.GetComponentLookup<EcsTestData>(true);
                        var val = lookupRead[tde.value1].value;
                        var lookupWrite = SystemAPI.GetComponentLookup<EcsTestData>(false);
                        lookupWrite[entity] = new EcsTestData(val * innerCapture * outerCapture);
                    }).Run();
                }
            }

            public void ComponentAccessMethodsExpandILPastShortBranchDistance_CausesNoExceptionsAndRuns()
            {
                Entities
                    .ForEach((Entity e, ref EcsTestData data) =>
                {
                    var a = 0;
                    if (data.value < 100)
                    {
                        if (SystemAPI.HasComponent<EcsTestDataEntity>(e))
                            a++;
                        if (SystemAPI.HasComponent<EcsTestDataEntity>(e))
                            a++;
                        if (SystemAPI.HasComponent<EcsTestDataEntity>(e))
                            a++;
                        if (SystemAPI.HasComponent<EcsTestDataEntity>(e))
                            a++;
                        if (SystemAPI.HasComponent<EcsTestDataEntity>(e))
                            a++;
                        if (SystemAPI.HasComponent<EcsTestDataEntity>(e))
                            a++;
                    }
                    data.value = a;
                }).Run();
            }

            public static bool StaticMethod()
            {
                return true;
            }

            public JobHandle CallsComponentAccessMethodAndExecutesStaticMethodWithBurst_CompilesAndRuns()
            {
                if (StaticMethod())
                {
                    return Entities
                        .WithBurst()
                        .ForEach((in EcsTestDataEntity tde) =>
                        {
                            if (StaticMethod())
                                SystemAPI.SetComponent(tde.value1, new EcsTestData(42));
                        }).Schedule(default);
                }

                return default;
            }
        }

        [Test]
        public void HasComponentInRun_HasComponent([Values] ScheduleType scheduleType)
        {
            TestSystem.HasComponent_HasComponent(TestEntity2, scheduleType);
            Assert.AreEqual(333, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }

        [Test]
        public void GetComponent_GetsValue([Values] ScheduleType scheduleType)
        {
            TestSystem.GetComponent_GetsValue(TestEntity2, scheduleType);
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }

        [Test]
        public void SetComponent_SetsValue([Values(ScheduleType.Run, ScheduleType.Schedule)] ScheduleType scheduleType)
        {
            TestSystem.SetComponent_SetsValue(TestEntity1, scheduleType);
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }

        [Test]
        public void SetComponent_WithArgumentWithElementAccessor_SetsValue([Values(ScheduleType.Run, ScheduleType.Schedule)] ScheduleType scheduleType)
        {
            TestSystem.SetComponent_WithArgumentWithElementAccessor_SetsValue(TestEntity1, scheduleType);
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }

        [Test]
        public void GetComponentThroughGetComponentLookup_GetsValue([Values] ScheduleType scheduleType)
        {
            TestSystem.GetComponentThroughGetComponentLookup_GetsValue(TestEntity2, scheduleType);
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }

        [Test]
        public void SetComponentThroughGetComponentLookup_SetsValue([Values(ScheduleType.Run, ScheduleType.Schedule)] ScheduleType scheduleType)
        {
            TestSystem.SetComponentThroughGetComponentLookup_SetsValue(TestEntity1, scheduleType);
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }

        [Test]
        public void GetComponentThroughGetComponentLookupPassedToMethod_GetsValue()
        {
            TestSystem.GetComponentThroughGetComponentLookupPassedToMethod_GetsValue(TestEntity2);
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestDataEntity>(TestEntity1).value0);
        }

        [Test]
        public void SetComponentThroughGetComponentLookupPassedToMethod_GetsValue()
        {
            TestSystem.SetComponentThroughGetComponentLookupPassedToMethod_GetsValue(TestEntity1);
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }

        [Test]
        public void GetComponentFromComponentDataField_GetsValue()
        {
            TestSystem.GetComponentFromComponentDataField_GetsValue();
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestDataEntity>(TestEntity1).value0);
        }

        [Test]
        public void SetComponentViaNamedArgument_SetsValue()
        {
            TestSystem.SetComponentViaNamedArgument_SetsValue();
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }

        [Test]
        public void GetComponentFromStaticField_GetsValue()
        {
            TestSystem.GetComponentFromStaticField_GetsValue();
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestDataEntity>(TestEntity1).value0);
        }

        [Test]
        public void MultipleGetComponents_GetsValues()
        {
            TestSystem.MultipleGetComponents_GetsValues();
            Assert.AreEqual(4, m_Manager.GetComponentData<EcsTestDataEntity>(TestEntity1).value0);
        }

        [Test]
        public void GetComponentSetComponent_SetsValue()
        {
            TestSystem.GetComponentSetComponent_SetsValue();
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }

        [Test]
        public void GetComponentSetComponent_ThroughComponentLookup_SetsValue()
        {
            TestSystem.GetComponentSetComponent_ThroughComponentLookup_SetsValue();
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }

        [Test]
        public void GetSameComponentInTwoEntitiesForEach_GetsValue()
        {
            TestSystem.GetSameComponentInTwoEntitiesForEach_GetsValue();
            Assert.AreEqual(9, m_Manager.GetComponentData<EcsTestDataEntity>(TestEntity1).value0);
        }

        [Test]
        public void ComponentAccessInEntitiesForEachWithNestedCaptures_ComponentAccessWorks()
        {
            TestSystem.ComponentAccessInEntitiesForEachWithNestedCaptures_ComponentAccessWorks();
            Assert.AreEqual(200, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }

        [Test]
        public void GetComponentLookupInEntitiesForEachWithNestedCaptures_ComponentAccessWorks()
        {
            TestSystem.GetComponentLookupInEntitiesForEachWithNestedCaptures_ComponentAccessWorks();
            Assert.AreEqual(200, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }

        // This test is to check that patched component access methods don't expand IL incorrectly past the limits of
        // short branch instructions.  We should call SimplifyMacros on the cloned method to ensure that we aren't
        // using short branch instructions.  If that is not happening this test will fail.
        [Test]
        public void ComponentAccessMethodsExpandILPastShortBranchDistance_CausesNoExceptionsAndRuns()
        {
            TestSystem.ComponentAccessMethodsExpandILPastShortBranchDistance_CausesNoExceptionsAndRuns();
            Assert.AreEqual(6, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }

        [Test]
        public void CallsComponentAccessMethodAndExecutesStaticMethodWithBurst_CompilesAndRuns()
        {
            TestSystem.CallsComponentAccessMethodAndExecutesStaticMethodWithBurst_CompilesAndRuns().Complete();
            Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }
    }
}
