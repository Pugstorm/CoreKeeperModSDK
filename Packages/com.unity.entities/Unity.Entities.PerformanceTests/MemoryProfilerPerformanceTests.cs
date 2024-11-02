using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.Profiling;

namespace Unity.Entities.PerformanceTests
{
    [Category("Performance")]
    class MemoryProfilerPerformanceTests : EntityPerformanceTestFixture
    {
        struct TestData00 : IComponentData { public int Value; }
        struct TestData01 : IComponentData { public int Value; }
        struct TestData02 : IComponentData { public int Value; }
        struct TestData03 : IComponentData { public int Value; }
        struct TestData04 : IComponentData { public int Value; }
        struct TestData05 : IComponentData { public int Value; }
        struct TestData06 : IComponentData { public int Value; }
        struct TestData07 : IComponentData { public int Value; }
        struct TestData08 : IComponentData { public int Value; }
        struct TestData09 : IComponentData { public int Value; }
        struct TestData10 : IComponentData { public int Value; }
        struct TestData11 : IComponentData { public int Value; }
        struct TestData12 : IComponentData { public int Value; }
        struct TestData13 : IComponentData { public int Value; }
        struct TestData14 : IComponentData { public int Value; }
        struct TestData15 : IComponentData { public int Value; }
        struct TestData16 : IComponentData { public int Value; }
        struct TestData17 : IComponentData { public int Value; }
        struct TestData18 : IComponentData { public int Value; }
        struct TestData19 : IComponentData { public int Value; }
        struct TestData20 : IComponentData { public int Value; }
        struct TestData21 : IComponentData { public int Value; }
        struct TestData22 : IComponentData { public int Value; }
        struct TestData23 : IComponentData { public int Value; }
        struct TestData24 : IComponentData { public int Value; }
        struct TestData25 : IComponentData { public int Value; }
        struct TestData26 : IComponentData { public int Value; }
        struct TestData27 : IComponentData { public int Value; }
        struct TestData28 : IComponentData { public int Value; }
        struct TestData29 : IComponentData { public int Value; }
        struct TestData30 : IComponentData { public int Value; }
        struct TestData31 : IComponentData { public int Value; }

        static readonly Type[] DataTypes =
        {
            typeof(TestData00),
            typeof(TestData01),
            typeof(TestData02),
            typeof(TestData03),
            typeof(TestData04),
            typeof(TestData05),
            typeof(TestData06),
            typeof(TestData07),
            typeof(TestData08),
            typeof(TestData09),
            typeof(TestData10),
            typeof(TestData11),
            typeof(TestData12),
            typeof(TestData13),
            typeof(TestData14),
            typeof(TestData15),
            typeof(TestData16),
            typeof(TestData17),
            typeof(TestData18),
            typeof(TestData19),
            typeof(TestData20),
            typeof(TestData21),
            typeof(TestData22),
            typeof(TestData23),
            typeof(TestData24),
            typeof(TestData25),
            typeof(TestData26),
            typeof(TestData27),
            typeof(TestData28),
            typeof(TestData29),
            typeof(TestData30),
            typeof(TestData31),
        };

        NativeArray<EntityArchetype> CreateUniqueArchetypes(int size, Allocator allocator)
        {
            using (var componentTypes = new NativeList<ComponentType>(DataTypes.Length, Allocator.Temp))
            {
                var archetypes = new NativeArray<EntityArchetype>(size, allocator);
                for (var i = 0; i < size; ++i)
                {
                    componentTypes.Clear();
                    for (var j = 0; j < DataTypes.Length; ++j)
                    {
                        var mask = 1 << j;
                        if (((i + 1) & mask) == mask)
                            componentTypes.Add(DataTypes[j]);
                    }
                    archetypes[i] = m_Manager.CreateArchetype(componentTypes.AsArray());
                }
                return archetypes;
            }
        }

        bool m_LastProfilerEnabled;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            MemoryProfiler.Initialize();
            m_LastProfilerEnabled = Profiler.enabled;
            Profiler.enabled = true;
        }

        [TearDown]
        public override void TearDown()
        {
            Profiler.enabled = m_LastProfilerEnabled;
            MemoryProfiler.Shutdown();
            base.TearDown();
        }

		public static IEnumerable<TestCaseData> DetermineMemoryProfilerUpdateParameters
        {
            get
            {
                for (int i = 100; i <= 10000; i *= 10)
                {
                    int entitiesMax = 10000;

                    // TODO: Calculate dynamic upper range parameter values based on reported available system memeory
                    if (i == 10000 && SystemInfo.deviceType != DeviceType.Desktop)
                    {
                        entitiesMax = 1000; // limiting non-desktop platforms to a lower maximum to account for generally lower system memory
                    }

                    for (int j = 100; j <= entitiesMax; j *= 10)
                    {
                        yield return new TestCaseData(i, j);
                    }
                }
			}
		}

        [Test, Performance]
		[TestCaseSource(nameof(DetermineMemoryProfilerUpdateParameters))]
        public void MemoryProfiler_Update(int archetypeCount, int entityCount)
        {
            using (var archetypes = CreateUniqueArchetypes(archetypeCount, Allocator.Temp))
            {
                for (var i = 0; i < archetypes.Length; ++i)
                    m_Manager.CreateEntity(archetypes[i], entityCount);

                Measure.Method(() =>
                {
                    MemoryProfiler.Internal_UpdateWorld(m_World);
                })
                .WarmupCount(3)
                .MeasurementCount(100)
                .Run();
            }
        }

        [Test, Performance]
        public void MemoryProfiler_Update_EmptyArchetypes([Values(100, 1000, 10000)] int archetypeCount)
        {
            using (var archetypes = CreateUniqueArchetypes(archetypeCount, Allocator.Temp))
            {
                Measure.Method(() =>
                {
                    MemoryProfiler.Internal_UpdateWorld(m_World);
                })
                .WarmupCount(3)
                .MeasurementCount(100)
                .Run();
            }
        }
    }
}
