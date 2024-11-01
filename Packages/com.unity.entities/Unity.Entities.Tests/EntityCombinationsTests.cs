using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Assert = FastAssert;

namespace Unity.Entities.Tests
{
    partial class EntityCombinationsTests : ECSTestsFixture
    {
        public enum ProcessMode
        {
            Single,
            Parallel,
            Run
        }

        NativeArray<Entity> PrepareData(int entityCount)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestData4));

            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            m_Manager.CreateEntity(archetype, entities);
            for (int i = 0; i < entities.Length; i++)
                m_Manager.SetComponentData(entities[i], new EcsTestData(i));

            return entities;
        }

        NativeArray<Entity> PrepareData_Buffer(int entityCount)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsIntElement), typeof(EcsIntElement2), typeof(EcsIntElement3), typeof(EcsIntElement4));

            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            m_Manager.CreateEntity(archetype, entities);

            return entities;
        }

        EntityQuery PrepareQuery(int componentTypeCount)
        {
            switch (componentTypeCount)
            {
                case 1:
                    return m_Manager.CreateEntityQuery(typeof(EcsTestData));
                case 2:
                    return m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2));
                case 3:
                    return m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3));
                case 4:
                    return m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestData4));
                default:
                    throw new Exception("Test case setup error.");
            }
        }

        EntityQuery PrepareQuery_Buffer(int entityCount)
        {
            switch (entityCount)
            {
                case 1:
                    return m_Manager.CreateEntityQuery(typeof(EcsIntElement));
                case 2:
                    return m_Manager.CreateEntityQuery(typeof(EcsIntElement), typeof(EcsIntElement2));
                case 3:
                    return m_Manager.CreateEntityQuery(typeof(EcsIntElement), typeof(EcsIntElement2), typeof(EcsIntElement3));
                case 4:
                    return m_Manager.CreateEntityQuery(typeof(EcsIntElement), typeof(EcsIntElement2), typeof(EcsIntElement3), typeof(EcsIntElement4));
                default:
                    throw new Exception("Test case setup error.");
            }
        }

        void CheckResultsAndDispose(NativeArray<Entity> entities, int processCount, bool withEntity)
        {
            m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestData4));

            for (int i = 0; i < entities.Length; i++)
            {
                // These values should remain untouched...
                if (processCount >= 2)
                    Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestData2>(entities[i]).value0);
                if (processCount >= 3)
                    Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestData3>(entities[i]).value1);
                if (processCount >= 4)
                    Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestData4>(entities[i]).value2);

                int expectedResult;
                if (withEntity)
                    expectedResult = i + entities[i].Index + i;
                else
                    expectedResult = i;

                if (processCount >= 2 && expectedResult != m_Manager.GetComponentData<EcsTestData2>(entities[i]).value1)
                    Assert.AreEqual(expectedResult, m_Manager.GetComponentData<EcsTestData2>(entities[i]).value1, $"{i}");
                if (processCount >= 3)
                    Assert.AreEqual(expectedResult, m_Manager.GetComponentData<EcsTestData3>(entities[i]).value2);
                if (processCount >= 4)
                    Assert.AreEqual(expectedResult, m_Manager.GetComponentData<EcsTestData4>(entities[i]).value3);
            }

            entities.Dispose();
        }

        void CheckResultsAndDispose_Buffer(NativeArray<Entity> entities, int processCount)
        {
            m_Manager.CreateArchetype(typeof(EcsIntElement), typeof(EcsIntElement2), typeof(EcsIntElement3), typeof(EcsIntElement4));

            for (int i = 0; i < entities.Length; i++)
            {
                // These values should remain untouched...
                if (processCount < 4)
                    Assert.AreEqual(0, m_Manager.GetBuffer<EcsIntElement4>(entities[i]).Length);
                if (processCount < 3)
                    Assert.AreEqual(0, m_Manager.GetBuffer<EcsIntElement3>(entities[i]).Length);
                if (processCount < 2)
                    Assert.AreEqual(0, m_Manager.GetBuffer<EcsIntElement2>(entities[i]).Length);

                var expectedResult = 1;

                if (processCount >= 4)
                    Assert.AreEqual(expectedResult, m_Manager.GetBuffer<EcsIntElement4>(entities[i]).Length);
                if (processCount >= 3)
                    Assert.AreEqual(expectedResult, m_Manager.GetBuffer<EcsIntElement3>(entities[i]).Length);
                if (processCount >= 2)
                    Assert.AreEqual(expectedResult, m_Manager.GetBuffer<EcsIntElement2>(entities[i]).Length);
            }

            entities.Dispose();
        }

        public partial class ProcessSystem : SystemBase
        {
            public JobHandle Process2_Schedule()
            {
                return
                    Entities.ForEach((ref EcsTestData2 writeHere, in EcsTestData readHere) =>
                    {
                        writeHere.value1 = readHere.value;
                    }).Schedule(default);
            }

            public JobHandle Process2_ScheduleParallel()
            {
                return
                    Entities.ForEach((ref EcsTestData2 writeHere, in EcsTestData readHere) =>
                    {
                        writeHere.value1 = readHere.value;
                    }).ScheduleParallel(default);
            }

            public void Process2_Run()
            {
                Entities.ForEach((ref EcsTestData2 writeHere, in EcsTestData readHere) =>
                {
                    writeHere.value1 = readHere.value;
                }).Run();
            }

            public JobHandle Process3_Schedule()
            {
                return
                    Entities.ForEach((ref EcsTestData2 writeHere, ref EcsTestData3 writeHereToo, in EcsTestData readHere) =>
                    {
                        writeHere.value1 = writeHereToo.value2 = readHere.value;
                    }).Schedule(default);
            }

            public JobHandle Process3_ScheduleParallel()
            {
                return
                    Entities.ForEach((ref EcsTestData2 writeHere, ref EcsTestData3 writeHereToo, in EcsTestData readHere) =>
                    {
                        writeHere.value1 = writeHereToo.value2 = readHere.value;
                    }).ScheduleParallel(default);
            }

            public void Process3_Run()
            {
                Entities.ForEach((ref EcsTestData2 writeHere, ref EcsTestData3 writeHereToo, in EcsTestData readHere) =>
                {
                    writeHere.value1 = writeHereToo.value2 = readHere.value;
                }).Run();
            }

            public JobHandle Process4_ScheduleParallel()
            {
                return
                    Entities.ForEach((ref EcsTestData2 writeHere, ref EcsTestData3 writeHereToo, ref EcsTestData4 andWriteHere, in EcsTestData readHere) =>
                    {
                        writeHere.value1 = writeHereToo.value2 = andWriteHere.value3 = readHere.value;
                    }).ScheduleParallel(default);
            }

            public JobHandle Process4_Schedule()
            {
                return
                    Entities.ForEach((ref EcsTestData2 writeHere, ref EcsTestData3 writeHereToo, ref EcsTestData4 andWriteHere, in EcsTestData readHere) =>
                    {
                        writeHere.value1 = writeHereToo.value2 = andWriteHere.value3 = readHere.value;
                    }).Schedule(default);
            }

            public void Process4_Run()
            {
                Entities.ForEach((ref EcsTestData2 writeHere, ref EcsTestData3 writeHereToo, ref EcsTestData4 andWriteHere, in EcsTestData readHere) =>
                {
                    writeHere.value1 = writeHereToo.value2 = andWriteHere.value3 = readHere.value;
                }).Run();
            }

            public JobHandle Process1Entity_ScheduleParallel()
            {
                return Entities.ForEach((Entity entity, int entityInQueryIndex, ref EcsTestData writeTo) =>
                {
                    writeTo.value += entity.Index + entityInQueryIndex;
                }).ScheduleParallel(default);
            }

            public JobHandle Process1Entity_Schedule()
            {
                return Entities.ForEach((Entity entity,int entityInQueryIndex, ref EcsTestData writeTo) =>
                {
                    writeTo.value += entity.Index + entityInQueryIndex;
                }).Schedule(default);
            }

            public void Process1Entity_Run()
            {
                Entities.ForEach((Entity entity, int entityInQueryIndex, ref EcsTestData writeTo) =>
                {
                    writeTo.value += entity.Index + entityInQueryIndex;
                }).Run();
            }

            public JobHandle Process2Entity_ScheduleParallel()
            {
                return Entities.ForEach((Entity entity,int entityInQueryIndex, ref EcsTestData2 writeTo, in EcsTestData readFrom) =>
                {
                    writeTo.value1 = entity.Index + entityInQueryIndex + readFrom.value;
                }).ScheduleParallel(default);
            }

            public JobHandle Process2Entity_Schedule()
            {
                return Entities.ForEach((Entity entity, int entityInQueryIndex, ref EcsTestData2 writeTo, in EcsTestData readFrom) =>
                {
                    writeTo.value1 = entity.Index + entityInQueryIndex + readFrom.value;
                }).Schedule(default);
            }

            public void Process2Entity_Run()
            {
                Entities.ForEach((Entity entity, int entityInQueryIndex, ref EcsTestData2 writeTo, in EcsTestData readFrom) =>
                {
                    writeTo.value1 = entity.Index + entityInQueryIndex + readFrom.value;
                }).Run();
            }

            public JobHandle Process3Entity_ScheduleParallel()
            {
                return Entities.ForEach((Entity entity, int entityInQueryIndex, ref EcsTestData2 writeTo, ref EcsTestData3 writeHereToo, in EcsTestData readFrom) =>
                {
                    writeTo.value1 = writeHereToo.value2 = entity.Index + entityInQueryIndex + readFrom.value;
                }).ScheduleParallel(default);
            }

            public JobHandle Process3Entity_Schedule()
            {
                return Entities.ForEach((Entity entity,int entityInQueryIndex, ref EcsTestData2 writeTo, ref EcsTestData3 writeHereToo, in EcsTestData readFrom) =>
                {
                    writeTo.value1 = writeHereToo.value2 = entity.Index + entityInQueryIndex + readFrom.value;
                }).Schedule(default);
            }

            public void Process3Entity_Run()
            {
                Entities.ForEach((Entity entity, int entityInQueryIndex, ref EcsTestData2 writeTo, ref EcsTestData3 writeHereToo, in EcsTestData readFrom) =>
                {
                    writeTo.value1 = writeHereToo.value2 = entity.Index + entityInQueryIndex + readFrom.value;
                }).Run();
            }
            public JobHandle Process4Entity_ScheduleParallel()
            {
                return Entities.ForEach((Entity entity, int entityInQueryIndex, ref EcsTestData2 writeTo, ref EcsTestData3 writeHereToo, ref EcsTestData4 writeHereAsWell, in EcsTestData readFrom) =>
                {
                    writeTo.value1 = writeHereToo.value2 = writeHereAsWell.value3 = entity.Index + entityInQueryIndex + readFrom.value;
                }).ScheduleParallel(default);
            }

            public JobHandle Process4Entity_Schedule()
            {
                return Entities.ForEach((Entity entity, int entityInQueryIndex, ref EcsTestData2 writeTo, ref EcsTestData3 writeHereToo, ref EcsTestData4 writeHereAsWell, in EcsTestData readFrom) =>
                {
                    writeTo.value1 = writeHereToo.value2 = writeHereAsWell.value3 = entity.Index + entityInQueryIndex + readFrom.value;
                }).Schedule(default);
            }

            public void Process4Entity_Run()
            {
                Entities.ForEach((Entity entity, int entityInQueryIndex, ref EcsTestData2 writeTo, ref EcsTestData3 writeHereToo, ref EcsTestData4 writeHereAsWell, in EcsTestData readFrom) =>
                {
                    writeTo.value1 = writeHereToo.value2 = writeHereAsWell.value3 = entity.Index + entityInQueryIndex + readFrom.value;
                }).Run();
            }

            public JobHandle Process1Buffer_ScheduleParallel()
            {
                return
                    Entities.ForEach((ref DynamicBuffer<EcsIntElement> buffer) =>
                    {
                        buffer.Add(new EcsIntElement { Value = 1 });
                    }).ScheduleParallel(default);
            }

            public JobHandle Process1Buffer_Schedule()
            {
                return
                    Entities.ForEach((ref DynamicBuffer<EcsIntElement> buffer) =>
                    {
                        buffer.Add(new EcsIntElement { Value = 1 });
                    }).Schedule(default);
            }

            public void Process1Buffer_Run()
            {
                Entities.ForEach((ref DynamicBuffer<EcsIntElement> buffer) =>
                {
                    buffer.Add(new EcsIntElement { Value = 1 });
                }).Run();
            }

            public JobHandle Process2Buffer_ScheduleParallel()
            {
                return
                    Entities.ForEach((ref DynamicBuffer<EcsIntElement> buffer1, ref DynamicBuffer<EcsIntElement2> buffer2) =>
                    {
                        buffer1.Add(new EcsIntElement { Value = 1 });
                        buffer2.Add(new EcsIntElement2 { Value0 = 1, Value1 = 1 });
                    }).ScheduleParallel(default);
            }

            public JobHandle Process2Buffer_Schedule()
            {
                return
                    Entities.ForEach((ref DynamicBuffer<EcsIntElement> buffer1, ref DynamicBuffer<EcsIntElement2> buffer2) =>
                    {
                        buffer1.Add(new EcsIntElement { Value = 1 });
                        buffer2.Add(new EcsIntElement2 { Value0 = 1, Value1 = 1 });
                    }).Schedule(default);
            }

            public void Process2Buffer_Run()
            {
                Entities.ForEach((ref DynamicBuffer<EcsIntElement> buffer1, ref DynamicBuffer<EcsIntElement2> buffer2) =>
                {
                    buffer1.Add(new EcsIntElement { Value = 1 });
                    buffer2.Add(new EcsIntElement2 { Value0 = 1, Value1 = 1 });
                }).Run();
            }
            public JobHandle Process3Buffer_ScheduleParallel()
            {
                return
                    Entities.ForEach((ref DynamicBuffer<EcsIntElement> buffer1, ref DynamicBuffer<EcsIntElement2> buffer2, ref DynamicBuffer<EcsIntElement3> buffer3) =>
                    {
                        buffer1.Add(new EcsIntElement { Value = 1 });
                        buffer2.Add(new EcsIntElement2 { Value0 = 1, Value1 = 1 });
                        buffer3.Add(new EcsIntElement3 { Value0 = 1, Value1 = 1, Value2 = 1 });
                    }).ScheduleParallel(default);
            }

            public JobHandle Process3Buffer_Schedule()
            {
                return
                    Entities.ForEach((ref DynamicBuffer<EcsIntElement> buffer1, ref DynamicBuffer<EcsIntElement2> buffer2, ref DynamicBuffer<EcsIntElement3> buffer3) =>
                    {
                        buffer1.Add(new EcsIntElement { Value = 1 });
                        buffer2.Add(new EcsIntElement2 { Value0 = 1, Value1 = 1 });
                        buffer3.Add(new EcsIntElement3 { Value0 = 1, Value1 = 1, Value2 = 1 });
                    }).Schedule(default);
            }

            public void Process3Buffer_Run()
            {
                Entities.ForEach((ref DynamicBuffer<EcsIntElement> buffer1, ref DynamicBuffer<EcsIntElement2> buffer2, ref DynamicBuffer<EcsIntElement3> buffer3) =>
                {
                    buffer1.Add(new EcsIntElement { Value = 1 });
                    buffer2.Add(new EcsIntElement2 { Value0 = 1, Value1 = 1 });
                    buffer3.Add(new EcsIntElement3 { Value0 = 1, Value1 = 1, Value2 = 1 });
                }).Run();
            }
            public JobHandle Process4Buffer_ScheduleParallel()
            {
                return
                    Entities.ForEach((ref DynamicBuffer<EcsIntElement> buffer1, ref DynamicBuffer<EcsIntElement2> buffer2, ref DynamicBuffer<EcsIntElement3> buffer3, ref DynamicBuffer<EcsIntElement4> buffer4) =>
                    {
                        buffer1.Add(new EcsIntElement { Value = 1 });
                        buffer2.Add(new EcsIntElement2 { Value0 = 1, Value1 = 1 });
                        buffer3.Add(new EcsIntElement3 { Value0 = 1, Value1 = 1, Value2 = 1 });
                        buffer4.Add(new EcsIntElement4 { Value0 = 1, Value1 = 1, Value2 = 1, Value3 = 1});
                    }).ScheduleParallel(default);
            }

            public JobHandle Process4Buffer_Schedule()
            {
                return
                    Entities.ForEach((ref DynamicBuffer<EcsIntElement> buffer1, ref DynamicBuffer<EcsIntElement2> buffer2, ref DynamicBuffer<EcsIntElement3> buffer3, ref DynamicBuffer<EcsIntElement4> buffer4) =>
                    {
                        buffer1.Add(new EcsIntElement { Value = 1 });
                        buffer2.Add(new EcsIntElement2 { Value0 = 1, Value1 = 1 });
                        buffer3.Add(new EcsIntElement3 { Value0 = 1, Value1 = 1, Value2 = 1 });
                        buffer4.Add(new EcsIntElement4 { Value0 = 1, Value1 = 1, Value2 = 1, Value3 = 1});
                    }).Schedule(default);
            }

            public void Process4Buffer_Run()
            {
                Entities.ForEach((ref DynamicBuffer<EcsIntElement> buffer1, ref DynamicBuffer<EcsIntElement2> buffer2, ref DynamicBuffer<EcsIntElement3> buffer3, ref DynamicBuffer<EcsIntElement4> buffer4) =>
                {
                    buffer1.Add(new EcsIntElement { Value = 1 });
                    buffer2.Add(new EcsIntElement2 { Value0 = 1, Value1 = 1 });
                    buffer3.Add(new EcsIntElement3 { Value0 = 1, Value1 = 1, Value2 = 1 });
                    buffer4.Add(new EcsIntElement4 { Value0 = 1, Value1 = 1, Value2 = 1, Value3 = 1});
                }).Run();
            }

            public JobHandle Process6Mixed_ScheduleParallel()
            {
                return Entities.ForEach((Entity entity, int entityInQueryIndex, ref DynamicBuffer<EcsIntElement> buffer1, ref DynamicBuffer<EcsIntElement2> buffer2, ref DynamicBuffer<EcsIntElement3> buffer3, ref EcsTestData ecsTestData, ref EcsTestData2 ecsTestData2, ref EcsTestData3 ecsTestData3) =>
                {
                    buffer1.Add(new EcsIntElement { Value = 1 });
                    buffer2.Add(new EcsIntElement2 { Value0 = 1, Value1 = 1 });
                    buffer3.Add(new EcsIntElement3 { Value0 = 1, Value1 = 1, Value2 = 1 });
                    ecsTestData.value = ecsTestData2.value1 = ecsTestData3.value2 = entityInQueryIndex + entity.Index;
                }).ScheduleParallel(default);
            }

            public JobHandle Process6Mixed_Schedule()
            {
                return Entities.ForEach((Entity entity, int entityInQueryIndex, ref DynamicBuffer<EcsIntElement> buffer1, ref DynamicBuffer<EcsIntElement2> buffer2, ref DynamicBuffer<EcsIntElement3> buffer3, ref EcsTestData ecsTestData, ref EcsTestData2 ecsTestData2, ref EcsTestData3 ecsTestData3) =>
                {
                    buffer1.Add(new EcsIntElement { Value = 1 });
                    buffer2.Add(new EcsIntElement2 { Value0 = 1, Value1 = 1 });
                    buffer3.Add(new EcsIntElement3 { Value0 = 1, Value1 = 1, Value2 = 1 });
                    ecsTestData.value = ecsTestData2.value1 = ecsTestData3.value2 = entityInQueryIndex + entity.Index;
                }).Schedule(default);
            }

            public void Process6Mixed_Run()
            {
                Entities.ForEach((Entity entity, int entityInQueryIndex, ref DynamicBuffer<EcsIntElement> buffer1, ref DynamicBuffer<EcsIntElement2> buffer2, ref DynamicBuffer<EcsIntElement3> buffer3, ref EcsTestData ecsTestData, ref EcsTestData2 ecsTestData2, ref EcsTestData3 ecsTestData3) =>
                {
                    buffer1.Add(new EcsIntElement { Value = 1 });
                    buffer2.Add(new EcsIntElement2 { Value0 = 1, Value1 = 1 });
                    buffer3.Add(new EcsIntElement3 { Value0 = 1, Value1 = 1, Value2 = 1 });
                    ecsTestData.value = ecsTestData2.value1 = ecsTestData3.value2 = entityInQueryIndex + entity.Index;
                }).Run();
            }

            public JobHandle Process1_Schedule()
            {
                return
                    Entities.ForEach((ref EcsTestData data) => { data.value++; }).Schedule(default);
            }

            public JobHandle Process1_ScheduleParallel()
            {
                return Entities.ForEach((ref EcsTestData data) => { data.value++; }).ScheduleParallel(default);
            }

            public void Process1_Run()
            {
                Entities.ForEach((ref EcsTestData data) => { data.value++; }).Run();
            }

            protected override void OnUpdate()
            {
            }
        }

        ProcessSystem _processSystem => World.GetOrCreateSystemManaged<ProcessSystem>();

        [Test]
        public void JobProcessStress_1([Values] ProcessMode mode, [Values(0, 1, 1000)] int entityCount)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            m_Manager.CreateEntity(archetype, entities);

            switch (mode)
            {
                case ProcessMode.Parallel:
                    _processSystem.Process1_ScheduleParallel().Complete();
                    break;
                case ProcessMode.Run:
                    _processSystem.Process1_Run();
                    break;
                default:
                    _processSystem.Process1_Schedule().Complete();
                    break;
            }

            for (int i = 0; i < entities.Length; i++)
                Assert.AreEqual(1, m_Manager.GetComponentData<EcsTestData>(entities[i]).value);

            entities.Dispose();
        }

        [Test]
        public void JobProcessStress_1_WithEntity([Values] ProcessMode mode, [Values(0, 1, 1000)] int entityCount)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            m_Manager.CreateEntity(archetype, entities);

            switch (mode)
            {
                case ProcessMode.Parallel:
                    _processSystem.Process1Entity_ScheduleParallel().Complete();
                    break;
                case ProcessMode.Run:
                    _processSystem.Process1Entity_Run();
                    break;
                default:
                    _processSystem.Process1Entity_Schedule().Complete();
                    break;
            }

            for (int i = 0; i < entities.Length; i++)
                Assert.AreEqual(i + entities[i].Index, m_Manager.GetComponentData<EcsTestData>(entities[i]).value);

            entities.Dispose();
        }

        [Test]
        public void JobProcessStress_2([Values] ProcessMode mode, [Values(0, 1, 1000)] int entityCount)
        {
            var entities = PrepareData(entityCount);
            switch (mode)
            {
                case ProcessMode.Parallel:
                    _processSystem.Process2_ScheduleParallel().Complete();
                    break;
                case ProcessMode.Run:
                    _processSystem.Process2_Run();
                    break;
                default:
                    _processSystem.Process2_Schedule().Complete();
                    break;
            }
            CheckResultsAndDispose(entities, 2, false);
        }

        [Test]
        public void JobProcessStress_2_WithEntity([Values] ProcessMode mode, [Values(0, 1, 1000)] int entityCount)
        {
            var entities = PrepareData(entityCount);

            switch (mode)
            {
                case ProcessMode.Parallel:
                    _processSystem.Process2Entity_ScheduleParallel().Complete();
                    break;
                case ProcessMode.Run:
                    _processSystem.Process2Entity_Run();
                    break;
                default:
                    _processSystem.Process2Entity_Schedule().Complete();
                    break;
            }
            CheckResultsAndDispose(entities, 2, true);
        }

        [Test]
        public void JobProcessStress_3([Values] ProcessMode mode, [Values(0, 1, 1000)] int entityCount)
        {
            var entities = PrepareData(entityCount);

            switch (mode)
            {
                case ProcessMode.Parallel:
                    _processSystem.Process3_ScheduleParallel().Complete();
                    break;
                case ProcessMode.Run:
                    _processSystem.Process3_Run();
                    break;
                default:
                    _processSystem.Process3_Schedule().Complete();
                    break;
            }
            CheckResultsAndDispose(entities, 3, false);
        }

        [Test]
        public void JobProcessStress_3_WithEntity([Values] ProcessMode mode, [Values(0, 1, 1000)] int entityCount)
        {
            var entities = PrepareData(entityCount);
            switch (mode)
            {
                case ProcessMode.Parallel:
                    _processSystem.Process3Entity_ScheduleParallel().Complete();
                    break;
                case ProcessMode.Run:
                    _processSystem.Process3Entity_Run();
                    break;
                default:
                    _processSystem.Process3Entity_Schedule().Complete();
                    break;
            }
            CheckResultsAndDispose(entities, 3, true);
        }

        [Test]
        public void JobProcessStress_4([Values] ProcessMode mode, [Values(0, 1, 1000)] int entityCount)
        {
            var entities = PrepareData(entityCount);

            switch (mode)
            {
                case ProcessMode.Parallel:
                    _processSystem.Process4_ScheduleParallel().Complete();
                    break;
                case ProcessMode.Run:
                    _processSystem.Process4_Run();
                    break;
                default:
                    _processSystem.Process4_Schedule().Complete();
                    break;
            }
            CheckResultsAndDispose(entities, 4, false);
        }

        [Test]
        public void JobProcessStress_4_WithEntity([Values] ProcessMode mode, [Values(0, 1, 1000)] int entityCount)
        {
            var entities = PrepareData(entityCount);
            switch (mode)
            {
                case ProcessMode.Parallel:
                    _processSystem.Process4Entity_ScheduleParallel().Complete();
                    break;
                case ProcessMode.Run:
                    _processSystem.Process4Entity_Run();
                    break;
                default:
                    _processSystem.Process4Entity_Schedule().Complete();
                    break;
            }
            CheckResultsAndDispose(entities, 4, true);
        }

        [Test]
        public void JobProcessBufferStress_1([Values] ProcessMode mode, [Values(0, 1, 1000)] int entityCount)
        {
            var entities = PrepareData_Buffer(entityCount);

            switch (mode)
            {
                case ProcessMode.Parallel:
                    _processSystem.Process1Buffer_ScheduleParallel().Complete();
                    break;
                case ProcessMode.Run:
                    _processSystem.Process1Buffer_Run();
                    break;
                default:
                    _processSystem.Process1Buffer_Schedule().Complete();
                    break;
            }

            for (int i = 0; i < entities.Length; i++)
                Assert.AreEqual(1, m_Manager.GetBuffer<EcsIntElement>(entities[i]).Length);

            entities.Dispose();
        }

        [Test]
        public void JobProcessBufferStress_2([Values] ProcessMode mode, [Values(0, 1, 1000)] int entityCount)
        {
            var entities = PrepareData_Buffer(entityCount);
            switch (mode)
            {
                case ProcessMode.Parallel:
                    _processSystem.Process2Buffer_ScheduleParallel().Complete();
                    break;
                case ProcessMode.Run:
                    _processSystem.Process2Buffer_Run();
                    break;
                default:
                    _processSystem.Process2Buffer_Schedule().Complete();
                    break;
            }
            CheckResultsAndDispose_Buffer(entities, 2);

            entities.Dispose();
        }

        [Test]
        public void JobProcessBufferStress_3([Values] ProcessMode mode, [Values(0, 1, 1000)] int entityCount)
        {
            var entities = PrepareData_Buffer(entityCount);
            switch (mode)
            {
                case ProcessMode.Parallel:
                    _processSystem.Process3Buffer_ScheduleParallel().Complete();
                    break;
                case ProcessMode.Run:
                    _processSystem.Process3Buffer_Run();
                    break;
                default:
                    _processSystem.Process3Buffer_Schedule().Complete();
                    break;
            }

            CheckResultsAndDispose_Buffer(entities, 3);

            entities.Dispose();
        }

        [Test]
        public void JobProcessBufferStress_4([Values] ProcessMode mode, [Values(0, 1, 1000)] int entityCount)
        {
            var entities = PrepareData_Buffer(entityCount);

            switch (mode)
            {
                case ProcessMode.Parallel:
                    _processSystem.Process4Buffer_ScheduleParallel().Complete();
                    break;
                case ProcessMode.Run:
                    _processSystem.Process4Buffer_Run();
                    break;
                default:
                    _processSystem.Process4Buffer_Schedule().Complete();
                    break;
            }
            CheckResultsAndDispose_Buffer(entities, 4);

            entities.Dispose();
        }

        [Test]
        public void JobProcessMixedStress_6([Values] ProcessMode mode, [Values(0, 1, 1000)] int entityCount)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsIntElement), typeof(EcsIntElement2), typeof(EcsIntElement3), typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3));

            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            m_Manager.CreateEntity(archetype, entities);

            switch (mode)
            {
                case ProcessMode.Parallel:
                    _processSystem.Process6Mixed_ScheduleParallel().Complete();
                    break;
                case ProcessMode.Run:
                    _processSystem.Process6Mixed_Run();
                    break;
                default:
                    _processSystem.Process6Mixed_Schedule().Complete();
                    break;
            }

            for (int i = 0; i < entities.Length; i++)
            {
                {
                    var expectedResult = 1;
                    Assert.AreEqual(expectedResult, m_Manager.GetBuffer<EcsIntElement>(entities[i]).Length);
                    Assert.AreEqual(expectedResult, m_Manager.GetBuffer<EcsIntElement2>(entities[i]).Length);
                    Assert.AreEqual(expectedResult, m_Manager.GetBuffer<EcsIntElement3>(entities[i]).Length);
                }

                {
                    var expectedResult = entities[i].Index + i;
                    Assert.AreEqual(expectedResult, m_Manager.GetComponentData<EcsTestData>(entities[i]).value);
                    Assert.AreEqual(expectedResult, m_Manager.GetComponentData<EcsTestData2>(entities[i]).value1);
                    Assert.AreEqual(expectedResult, m_Manager.GetComponentData<EcsTestData3>(entities[i]).value2);
                }
            }
            entities.Dispose();
        }

    }
}
