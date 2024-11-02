using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities.Tests;
using Unity.Jobs;
using Unity.PerformanceTesting;

namespace Unity.Entities.PerformanceTests
{
    [Category("Performance")]
    [BurstCompile]
    public sealed class ComponentJobSafetyManagerPerformanceTests : ECSTestsFixture
    {
        [Test, Performance]
        public void AddGetBufferComponentLoop()
        {
            const int entityCount = 16 * 1024;
            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            Measure.Method(() =>
            {
                for (int i = 0; i < entityCount; ++i)
                {
                    var entity = entities[i];
                    m_Manager.AddBuffer<EcsIntElement>(entity);
                    var buffer = m_Manager.GetBuffer<EcsIntElement>(entity);
                    buffer.Add(i);
                }
            })
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype, entities);
                })
                .CleanUp(() =>
                {
                    m_Manager.DestroyEntity(entities);
                })
                .Run();

            entities.Dispose();
        }

        [BurstCompile(CompileSynchronously = true)]
        struct DummyJob : IJob
        {
            public void Execute()
            {
            }
        }
        struct EcsDummyData1  : IComponentData { public int value; };
        struct EcsDummyData2  : IComponentData { public int value; };
        struct EcsDummyData3  : IComponentData { public int value; };
        struct EcsDummyData4  : IComponentData { public int value; };
        struct EcsDummyData5  : IComponentData { public int value; };
        struct EcsDummyData6  : IComponentData { public int value; };
        struct EcsDummyData7  : IComponentData { public int value; };
        struct EcsDummyData8  : IComponentData { public int value; };
        struct EcsDummyData9  : IComponentData { public int value; };
        struct EcsDummyData10 : IComponentData { public int value; };
        struct EcsDummyData11 : IComponentData { public int value; };
        struct EcsDummyData12 : IComponentData { public int value; };
        struct EcsDummyData13 : IComponentData { public int value; };
        struct EcsDummyData14 : IComponentData { public int value; };
        struct EcsDummyData15 : IComponentData { public int value; };
        struct EcsDummyData16 : IComponentData { public int value; };

        [Test, Performance]
        public unsafe void CompleteAllJobs_Performance([Values(1,4,8,16)] int typeCount,
            [Values(1,4,8,16)] int readerCountForType)
        {
            var cdm = m_Manager.GetCheckedEntityDataAccess()->DependencyManager;
            const int kNumTagTypes = 16;
            TypeIndex *types = stackalloc TypeIndex[kNumTagTypes];
            types[0]  = ComponentType.ReadWrite<EcsDummyData1>().TypeIndex;
            types[1]  = ComponentType.ReadWrite<EcsDummyData2>().TypeIndex;
            types[2]  = ComponentType.ReadWrite<EcsDummyData3>().TypeIndex;
            types[3]  = ComponentType.ReadWrite<EcsDummyData4>().TypeIndex;
            types[4]  = ComponentType.ReadWrite<EcsDummyData5>().TypeIndex;
            types[5]  = ComponentType.ReadWrite<EcsDummyData6>().TypeIndex;
            types[6]  = ComponentType.ReadWrite<EcsDummyData7>().TypeIndex;
            types[7]  = ComponentType.ReadWrite<EcsDummyData8>().TypeIndex;
            types[8]  = ComponentType.ReadWrite<EcsDummyData9>().TypeIndex;
            types[9]  = ComponentType.ReadWrite<EcsDummyData10>().TypeIndex;
            types[10] = ComponentType.ReadWrite<EcsDummyData11>().TypeIndex;
            types[11] = ComponentType.ReadWrite<EcsDummyData12>().TypeIndex;
            types[12] = ComponentType.ReadWrite<EcsDummyData13>().TypeIndex;
            types[13] = ComponentType.ReadWrite<EcsDummyData14>().TypeIndex;
            types[14] = ComponentType.ReadWrite<EcsDummyData15>().TypeIndex;
            types[15] = ComponentType.ReadWrite<EcsDummyData16>().TypeIndex;
            Measure.Method(() =>
                {
                    cdm->CompleteAllJobs();
                })
                .SetUp(() =>
                {
                    for (int t = 0; t < typeCount; ++t)
                    {
                        var writerJob = new DummyJob().Schedule();
                        cdm->AddDependency(types + t, 0, types + t, 1, writerJob);
                        for (int i = 0; i < readerCountForType; ++i)
                        {
                            var readerJob = new DummyJob().Schedule();
                            cdm->AddDependency(types + t, 1, types + t, 0, readerJob);
                        }
                    }
                })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup("CompleteAllJobs", SampleUnit.Microsecond))
                .Run();
        }

    }
}
