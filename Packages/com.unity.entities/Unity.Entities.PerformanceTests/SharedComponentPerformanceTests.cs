using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.PerformanceTesting;

namespace Unity.Entities.PerformanceTests
{
    [Category("Performance")]
    public sealed class SharedComponentPerformanceTests : EntityPerformanceTestFixture
    {
        struct TestSharedManaged : ISharedComponentData, IEquatable<TestSharedManaged>
        {
            public int value;
            public string name; //

            public bool Equals(TestSharedManaged other)
            {
                return value == other.value && name == other.name;
            }

            public override bool Equals(object obj)
            {
                return obj is TestSharedManaged other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(value, name);
            }
        }

        struct TestShared1 : ISharedComponentData
        {
            public int value;
        }

        struct TestShared2 : ISharedComponentData
        {
            public int value;
        }

        unsafe struct TestData1 : IComponentData
        {
            public fixed long value[16];
        }

        struct TestData2 : IComponentData
        {
#pragma warning disable 649
            public int value;
#pragma warning restore 649
        }


        [Test, Performance]
        public void SetSharedComponentManaged_Perf()
        {
            var archetype = m_Manager.CreateArchetype(typeof(TestData1), typeof(TestShared1), typeof(TestShared2));
            var setSharedComponentData = new SampleGroup("SetSharedComponentData");

            NativeArray<Entity> entities = new NativeArray<Entity>(16384, Allocator.Temp);

            Measure.Method(() =>
            {
                for (int i = 0; i < entities.Length; ++i)
                {
                    m_Manager.SetSharedComponentManaged(entities[i], new TestShared1 {value = i & 0x003F});
                    m_Manager.SetSharedComponentManaged(entities[i], new TestShared2 {value = i & 0x0FC0});
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
                .WarmupCount(1)
                .MeasurementCount(100)
                .Run();

            entities.Dispose();
        }

        [Test, Performance]
        public void AddComponentPerformanceTest()
        {
            var archetype = m_Manager.CreateArchetype(typeof(TestData1), typeof(TestShared1), typeof(TestShared2));
            var addComponent = new SampleGroup("AddComponent");

            NativeArray<Entity> entities = new NativeArray<Entity>(16384, Allocator.Temp);

            Measure.Method(() =>
            {
                for (int i = 0; i < entities.Length; ++i)
                {
                    m_Manager.AddComponentData(entities[i], new TestData2());
                }
            })
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype, entities);
                    for (int i = 0; i < entities.Length; ++i)
                    {
                        m_Manager.SetSharedComponentManaged(entities[i], new TestShared1 {value = i & 0x003F});
                        m_Manager.SetSharedComponentManaged(entities[i], new TestShared2 {value = i & 0x0FC0});
                    }
                })
                .CleanUp(() =>
                {
                    m_Manager.DestroyEntity(entities);
                })
                .WarmupCount(1)
                .MeasurementCount(100)
                .Run();

            entities.Dispose();
        }

        [Test, Performance]
        public void AddSharedComponent_ToEntityArray_Perf()
        {
            var archetype = m_Manager.CreateArchetype(typeof(TestData1));

            using var entities = CollectionHelper.CreateNativeArray<Entity>(1024, World.UpdateAllocator.ToAllocator);

            Measure.Method(() =>
                {
                    for (int i = 0; i < entities.Length; ++i)
                    {
                        m_Manager.AddSharedComponent(entities[i], new TestShared1{value=17});
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
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup($"Unmanaged_Loop_{entities.Length}x", SampleUnit.Microsecond))
                .Run();

            Measure.Method(() =>
                {
                    m_Manager.AddSharedComponent(entities, new TestShared1 { value = 17 });
                })
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype, entities);
                })
                .CleanUp(() =>
                {
                    m_Manager.DestroyEntity(entities);
                })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup($"Unmanaged_Array_{entities.Length}x", SampleUnit.Microsecond))
                .Run();

            Measure.Method(() =>
                {
                    for (int i = 0; i < entities.Length; ++i)
                    {
                        m_Manager.AddSharedComponentManaged(entities[i], new TestSharedManaged{value=17,name="Bob"});
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
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup($"Managed_Loop_{entities.Length}x", SampleUnit.Microsecond))
                .Run();

            Measure.Method(() =>
                {
                    m_Manager.AddSharedComponentManaged(entities, new TestSharedManaged { value = 17,name="Bob" });
                })
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype, entities);
                })
                .CleanUp(() =>
                {
                    m_Manager.DestroyEntity(entities);
                })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup($"Managed_Array_{entities.Length}x", SampleUnit.Microsecond))
                .Run();
        }

        [Test, Performance]
        public void SetSharedComponent_ToEntityArray_Perf()
        {
            var archetype = m_Manager.CreateArchetype(typeof(TestData1), typeof(TestShared1), typeof(TestSharedManaged));

            using var entities = m_Manager.CreateEntity(archetype, 1024, World.UpdateAllocator.ToAllocator);

            Measure.Method(() =>
                {
                    for (int i = 0; i < entities.Length; ++i)
                    {
                        m_Manager.SetSharedComponent(entities[i], new TestShared1{value=17});
                    }
                })
                .CleanUp(() =>
                {
                    m_Manager.SetSharedComponent(entities, default(TestShared1));
                })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup($"Unmanaged_Loop_{entities.Length}x", SampleUnit.Microsecond))
                .Run();

            Measure.Method(() =>
                {
                    m_Manager.SetSharedComponent(entities, new TestShared1 { value = 17 });
                })
                .CleanUp(() =>
                {
                    m_Manager.SetSharedComponent(entities, default(TestShared1));
                })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup($"Unmanaged_Array_{entities.Length}x", SampleUnit.Microsecond))
                .Run();

            Measure.Method(() =>
                {
                    for (int i = 0; i < entities.Length; ++i)
                    {
                        m_Manager.SetSharedComponentManaged(entities[i], new TestSharedManaged{value=17,name="Bob"});
                    }
                })
                .CleanUp(() =>
                {
                    m_Manager.SetSharedComponentManaged(entities, default(TestSharedManaged));
                })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup($"Managed_Loop_{entities.Length}x", SampleUnit.Microsecond))
                .Run();

            Measure.Method(() =>
                {
                    m_Manager.SetSharedComponentManaged(entities, new TestSharedManaged { value = 17,name="Bob" });
                })
                .CleanUp(() =>
                {
                    m_Manager.SetSharedComponentManaged(entities, default(TestSharedManaged));
                })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup($"Managed_Array_{entities.Length}x", SampleUnit.Microsecond))
                .Run();
        }
    }
}
