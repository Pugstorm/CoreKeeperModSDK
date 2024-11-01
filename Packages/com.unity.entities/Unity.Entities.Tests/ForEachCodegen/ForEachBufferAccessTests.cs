using System;
using Unity.Burst;
using NUnit.Framework;
using Unity.Jobs;
#pragma warning disable 649

namespace Unity.Entities.Tests.ForEachCodegen
{
    partial class ForEachBufferAccessTests : ECSTestsFixture
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
            var buffer1 = m_Manager.AddBuffer<EcsIntElement>(TestEntity1);
            buffer1.Add(1);
            m_Manager.SetComponentData(TestEntity2, new EcsTestDataEntity() { value0 = 2, value1 = TestEntity1 });
            m_Manager.SetComponentData(TestEntity2, new EcsTestData() { value = 2 });
            var buffer2 = m_Manager.AddBuffer<EcsIntElement>(TestEntity2);
            buffer2.Add(2);
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

            public void GetBufffer_GetsValueFromBuffer(Entity entity, ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.ForEach((ref EcsTestData td) => { td.value = SystemAPI.GetBuffer<EcsIntElement>(entity)[0].Value; }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities.ForEach((ref EcsTestData td) => { td.value = SystemAPI.GetBuffer<EcsIntElement>(entity)[0].Value; }).Schedule();
                        break;

                    // Flagged as an DC0063 error at compile-time with sourcegen
                    /*
                    case ScheduleType.ScheduleParallel:
                        Entities.ForEach((ref EcsTestData td) => { td.value = GetBuffer<EcsIntElement>(entity)[0].Value; }).ScheduleParallel();
                        break;
                    */
                }

                Dependency.Complete();
            }

            public void GetBufferFromGetBufferLookup_GetsValueFromBuffer(Entity entity, ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.ForEach((ref EcsTestData td) => { td.value = GetBufferLookup<EcsIntElement>(true)[entity][0].Value; }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities.ForEach((ref EcsTestData td) => { td.value = GetBufferLookup<EcsIntElement>(true)[entity][0].Value; }).Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities.ForEach((ref EcsTestData td) => { td.value = GetBufferLookup<EcsIntElement>(true)[entity][0].Value; }).ScheduleParallel();
                        break;
                }

                Dependency.Complete();
            }

            public void AddToBufferThroughGetBufferLookup_AddsValueToBuffer(Entity entity, ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.ForEach((ref EcsTestDataEntity tde) =>
                        {
                            var bfe = GetBufferLookup<EcsIntElement>(false);
                            bfe[entity].Clear();
                            bfe[entity].Add(new EcsIntElement(){ Value = 2 });
                        }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities.ForEach((ref EcsTestDataEntity tde) =>
                        {
                            var bfe = GetBufferLookup<EcsIntElement>(false);
                            bfe[entity].Clear();
                            bfe[entity].Add(new EcsIntElement(){ Value = 2 });
                        }).Schedule();
                        break;
                    // Flagged as an DC0063 error at compile-time with sourcegen
                    /*
                    case ScheduleType.ScheduleParallel:
                        Entities.ForEach((ref EcsTestDataEntity tde) =>
                        {
                            var bfe = GetBufferLookup<EcsIntElement>(false);
                            bfe[entity].Clear();
                            bfe[entity].Add(new EcsIntElement(){ Value = 2 });
                        }).ScheduleParallel();
                        break;
                    */
                }

                Dependency.Complete();
            }

            static int GetBufferValueByMethod(BufferLookup<EcsIntElement> bfe, Entity entity)
            {
                return bfe[entity][0].Value;
            }

            public void GetBufferThroughGetBufferLookupPassedToMethod_GetsValueFromBuffer(Entity entity)
            {
                Entities.ForEach((ref EcsTestDataEntity td) => { td.value0 = GetBufferValueByMethod(GetBufferLookup<EcsIntElement>(true), entity); }).Run();
            }

            static void AddToBufferByMethod(BufferLookup<EcsIntElement> bfe, Entity entity, int value)
            {
                bfe[entity].Clear();
                bfe[entity].Add(new EcsIntElement() { Value = value });
            }

            public void AddToBufferThroughGetBufferLookupPassedToMethod_AddsToBuffer(Entity entity)
            {
                Entities.ForEach((ref EcsTestDataEntity td) => { AddToBufferByMethod(GetBufferLookup<EcsIntElement>(false), entity, 2); }).Run();
            }

            public void MultipleGetBuffers_GetsValuesFromBuffers()
            {
                Entities.ForEach((ref EcsTestDataEntity tde) =>
                {
                    tde.value0 = SystemAPI.GetBuffer<EcsIntElement>(tde.value1)[0].Value + SystemAPI.GetBuffer<EcsIntElement>(tde.value1)[0].Value;
                }).Schedule();
                Dependency.Complete();
            }

            public void GetSameBufferInTwoEntitiesForEach_GetsValueFromBuffer()
            {
                Entities.ForEach((Entity entity, ref EcsTestDataEntity tde) =>
                {
                    tde.value0 += SystemAPI.GetBuffer<EcsIntElement>(tde.value1)[0].Value;
                    tde.value0 += SystemAPI.GetBuffer<EcsIntElement>(tde.value1)[0].Value;
                }).Schedule();
                Entities.ForEach((Entity entity, ref EcsTestDataEntity tde) =>
                {
                    tde.value0 += SystemAPI.GetBuffer<EcsIntElement>(tde.value1)[0].Value;
                    tde.value0 += SystemAPI.GetBuffer<EcsIntElement>(tde.value1)[0].Value;
                }).Schedule();
                Dependency.Complete();
            }

            public void BufferAccessInEntitiesForEachWithNestedCaptures_BufferAccessWorks()
            {
                var outerCapture = 20;
                {
                    var innerCapture = 10;
                    Entities
                        .ForEach((Entity entity, in EcsTestDataEntity tde) =>
                    {
                        var buffer = SystemAPI.GetBuffer<EcsIntElement>(tde.value1);
                        var val = buffer[0].Value;
                        buffer.Clear();
                        buffer.Add(new EcsIntElement() { Value = val * innerCapture * outerCapture });
                    }).Run();
                }
            }

            public void GetBufferLookupInEntitiesForEachWithNestedCaptures_BufferAccessWorks()
            {
                var outerCapture = 2;
                {
                    var innerCapture = 10;
                    Entities
                        .ForEach((Entity entity, in EcsTestDataEntity tde) =>
                    {
                        outerCapture = 10;

                        var bfeRead = GetBufferLookup<EcsIntElement>(true);
                        var val = bfeRead[tde.value1][0].Value;
                        var bfeWrite = GetBufferLookup<EcsIntElement>(false);
                        bfeWrite[entity].Clear();

                        bfeWrite[entity].Add(new EcsIntElement() { Value = val * innerCapture * outerCapture });
                    }).Run();
                }
            }

            public void BufferAccessMethodsExpandILPastShortBranchDistance_CausesNoExceptionsAndRuns()
            {
                Entities
                    .ForEach((Entity e, ref EcsTestData data) =>
                {
                    var a = 0;
                    if (data.value < 100)
                    {
                        if (GetBufferLookup<EcsIntElement>().HasBuffer(e))
                            a++;
                        if (GetBufferLookup<EcsIntElement>().HasBuffer(e))
                            a++;
                        if (GetBufferLookup<EcsIntElement>().HasBuffer(e))
                            a++;
                        if (GetBufferLookup<EcsIntElement>().HasBuffer(e))
                            a++;
                        if (GetBufferLookup<EcsIntElement>().HasBuffer(e))
                            a++;
                        if (GetBufferLookup<EcsIntElement>().HasBuffer(e))
                            a++;
                    }
                    data.value = a;
                }).Run();
            }

            public static bool StaticMethod()
            {
                return true;
            }

            public JobHandle CallsBufferAccessMethodAndExecutesStaticMethodWithBurst_CompilesAndRuns()
            {
                if (StaticMethod())
                {
                    return Entities
                        .WithBurst(FloatMode.Default, FloatPrecision.Standard, true)
                        .ForEach((in EcsTestDataEntity tde) =>
                        {
                            if (StaticMethod())
                            {
                                var buffer = SystemAPI.GetBuffer<EcsIntElement>(tde.value1);
                                buffer.Clear();
                                buffer.Add(new EcsIntElement() { Value = 42 });
                            }
                        }).Schedule(default);
                }

                return default;
            }

            public void HasBuffer(Entity entity, ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.ForEach((ref EcsTestData td) => { td.value = HasBuffer<EcsIntElement>(entity) ? 333 : 0; }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities.ForEach((ref EcsTestData td) => { td.value = HasBuffer<EcsIntElement>(entity) ? 333 : 0; }).Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities.ForEach((ref EcsTestData td) => { td.value = HasBuffer<EcsIntElement>(entity) ? 333 : 0; }).ScheduleParallel();
                        break;
                }

                Dependency.Complete();
            }
        }

        [Test]
        public void GetBuffer_GetsValueFromBuffer([Values(ScheduleType.Schedule, ScheduleType.Run)] ScheduleType scheduleType)
        {
            TestSystem.GetBufffer_GetsValueFromBuffer(TestEntity2, scheduleType);
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }

        [Test]
        public void GetBufferFromGetBufferLookup_GetsValueFromBuffer([Values] ScheduleType scheduleType)
        {
            TestSystem.GetBufferFromGetBufferLookup_GetsValueFromBuffer(TestEntity2, scheduleType);
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }

        [Test]
        public void AddToBufferThroughGetBufferLookup_AddsValueToBuffer([Values(ScheduleType.Run, ScheduleType.Schedule)] ScheduleType scheduleType)
        {
            TestSystem.AddToBufferThroughGetBufferLookup_AddsValueToBuffer(TestEntity1, scheduleType);
            Assert.AreEqual(2, m_Manager.GetBuffer<EcsIntElement>(TestEntity1)[0].Value);
        }

        [Test]
        public void GetBufferThroughGetBufferLookupPassedToMethod_GetsValueFromBuffer()
        {
            TestSystem.GetBufferThroughGetBufferLookupPassedToMethod_GetsValueFromBuffer(TestEntity2);
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestDataEntity>(TestEntity1).value0);
        }

        [Test]
        public void AddToBufferThroughGetBufferLookupPassedToMethod_AddsToBuffer()
        {
            TestSystem.AddToBufferThroughGetBufferLookupPassedToMethod_AddsToBuffer(TestEntity1);
            Assert.AreEqual(2, m_Manager.GetBuffer<EcsIntElement>(TestEntity1)[0].Value);
        }

        [Test]
        public void MultipleGetBuffers_GetsValuesFromBuffers()
        {
            TestSystem.MultipleGetBuffers_GetsValuesFromBuffers();
            Assert.AreEqual(4, m_Manager.GetComponentData<EcsTestDataEntity>(TestEntity1).value0);
        }

        [Test]
        public void GetSameBufferInTwoEntitiesForEach_GetsValueFromBuffer()
        {
            TestSystem.GetSameBufferInTwoEntitiesForEach_GetsValueFromBuffer();
            Assert.AreEqual(9, m_Manager.GetComponentData<EcsTestDataEntity>(TestEntity1).value0);
        }

        [Test]
        public void BufferAccessInEntitiesForEachWithNestedCaptures_BufferAccessWorks()
        {
            TestSystem.BufferAccessInEntitiesForEachWithNestedCaptures_BufferAccessWorks();
            Assert.AreEqual(200, m_Manager.GetBuffer<EcsIntElement>(TestEntity1)[0].Value);
        }

        [Test]
        public void GetBufferLookupInEntitiesForEachWithNestedCaptures_BufferAccessWorks()
        {
            TestSystem.GetBufferLookupInEntitiesForEachWithNestedCaptures_BufferAccessWorks();
            Assert.AreEqual(200, m_Manager.GetBuffer<EcsIntElement>(TestEntity1)[0].Value);
        }

        // This test is to check that patched component access methods don't expand IL incorrectly past the limits of
        // short branch instructions.  We should call SimplifyMacros on the cloned method to ensure that we aren't
        // using short branch instructions.  If that is not happening this test will fail.
        [Test]
        public void BufferAccessMethodsExpandILPastShortBranchDistance_CausesNoExceptionsAndRuns()
        {
            TestSystem.BufferAccessMethodsExpandILPastShortBranchDistance_CausesNoExceptionsAndRuns();
            Assert.AreEqual(6, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }

        [Test]
        public void CallsBufferAccessMethodAndExecutesStaticMethodWithBurst_CompilesAndRuns()
        {
            TestSystem.CallsBufferAccessMethodAndExecutesStaticMethodWithBurst_CompilesAndRuns().Complete();
            Assert.AreEqual(42, m_Manager.GetBuffer<EcsIntElement>(TestEntity1)[0].Value);
        }

        [Test]
        public void HasBuffer_InvokedInsideEntitiesForEach([Values] ScheduleType scheduleType)
        {
            TestSystem.HasBuffer(TestEntity2, scheduleType);
            Assert.AreEqual(333, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }

        [Test]
        public void EntityManager_HasBuffer([Values] ScheduleType scheduleType)
        {
            Assert.IsTrue(TestSystem.EntityManager.HasBuffer<EcsIntElement>(TestEntity1));
            Assert.IsTrue(TestSystem.EntityManager.HasBuffer<EcsIntElement>(TestEntity2));
        }
    }
}
