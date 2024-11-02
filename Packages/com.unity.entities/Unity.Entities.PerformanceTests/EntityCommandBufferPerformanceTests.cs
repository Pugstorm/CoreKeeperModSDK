using System.Collections.Generic;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities.Tests;
using Unity.PerformanceTesting;

namespace Unity.Entities.PerformanceTests
{
    [TestFixture]
    [Category("Performance")]
    [BurstCompile(CompileSynchronously = true)]
    public sealed partial class EntityCommandBufferPerformanceTests : EntityPerformanceTestFixture
    {
        EntityArchetype archetype1;
        EntityArchetype archetype2;
        EntityArchetype archetype3;
        NativeArray<Entity> entities1;
        NativeArray<Entity> entities2;
        NativeArray<Entity> entities3;
        EntityQuery query;

        const int count = 1024 * 128;

        public override void Setup()
        {
            base.Setup();

            archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData));
            archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            archetype3 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestManagedComponent));
#endif
            entities1 = new NativeArray<Entity>(count, Allocator.Persistent);
            entities2 = new NativeArray<Entity>(count, Allocator.Persistent);
            entities3 = new NativeArray<Entity>(count, Allocator.Persistent);
            query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
        }

        [TearDown]
        public override void TearDown()
        {
            if (m_World.IsCreated)
            {
                entities1.Dispose();
                entities2.Dispose();
                entities3.Dispose();
                query.Dispose();
            }
            base.TearDown();
        }

        struct EcsTestDataWithEntity : IComponentData
        {
            public int value;
            public Entity entity;
        }

        void FillWithEcsTestDataWithEntity(EntityCommandBuffer cmds, int repeat)
        {
            for (int i = repeat; i != 0; --i)
            {
                var e = cmds.CreateEntity();
                cmds.AddComponent(e, new EcsTestDataWithEntity {value = i});
            }
        }

        void FillWithEcsTestData(EntityCommandBuffer cmds, int repeat)
        {
            for (int i = repeat; i != 0; --i)
            {
                var e = cmds.CreateEntity();
                cmds.AddComponent(e, new EcsTestData {value = i});
            }
        }

        void FillWithCreateEntityCommands(EntityCommandBuffer cmds, int repeat)
        {
            for (int i = repeat; i != 0; --i)
            {
                cmds.CreateEntity();
            }
        }

        void FillWithInstantiateEntityCommands(EntityCommandBuffer cmds, int repeat, Entity prefab)
        {
            for (int i = repeat; i != 0; --i)
            {
                cmds.Instantiate(prefab);
            }
        }

        void FillWithAddComponentCommands(EntityCommandBuffer cmds, NativeArray<Entity> entities, ComponentType componentType)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.AddComponent(entities[i], componentType);
            }
        }

        void FillWithRemoveComponentCommands(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.RemoveComponent(entities[i], typeof(EcsTestData));
            }
        }

        void FillWithSetComponentCommands(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.SetComponent(entities[i], new EcsTestData {value = i});
            }
        }

        void FillWithDestroyEntityCommands(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.DestroyEntity(entities[i]);
            }
        }

        void FillWithEcsTestSharedComp(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.AddSharedComponent(entities[i], new EcsTestSharedComp {value = 1});
            }
        }

        void FillWithSetEcsTestSharedComp(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.SetSharedComponent(entities[i], new EcsTestSharedComp {value = 2});
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        void FillWithEcsTestManagedComp(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.AddComponent(entities[i], new EcsTestManagedComponent {value = "string1"});
            }
        }

        void FillWithSetEcsTestManagedComp(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.SetComponent(entities[i], new EcsTestManagedComponent {value = "string2"});
            }
        }

#endif

        [BurstCompile(CompileSynchronously = true)]
        partial struct CreateEmptyEcbSystem : ISystem
        {
            private bool _createParallelWriters;
            private int _ecbCount;

            [BurstCompile(CompileSynchronously = true)]
            public void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate<EcsTestData2>();
                state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
                EcsTestData2 ecbParams = SystemAPI.GetSingleton<EcsTestData2>();
                _createParallelWriters = ecbParams.value0 != 0;
                _ecbCount = ecbParams.value1;
            }

            [BurstCompile(CompileSynchronously = true)]
            public void OnUpdate(ref SystemState state)
            {
                if (_createParallelWriters)
                    for (int i = 0; i < _ecbCount; ++i)
                    {
                        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                            .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
                    }
                else
                    for (int i = 0; i < _ecbCount; ++i)
                    {
                        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                            .CreateCommandBuffer(state.WorldUnmanaged);
                    }
            }
        }

        [Test, Performance]
        public void EntityCommandBuffer_Create([Values] bool createParallelWriters)
        {
            const int kEcbCount = 100;
            // Use a singleton to communicate whether the system should create the ECBs
            // as ParallelWriters or not, and how many.
            var e = m_Manager.CreateEntity(typeof(EcsTestData2));
            m_Manager.SetComponentData(e,
                new EcsTestData2 { value0 = createParallelWriters ? 1 : 0, value1 = kEcbCount });

            var createEcbSystem = World.CreateSystem<CreateEmptyEcbSystem>();
            var ecbSystem = World.CreateSystem<EndSimulationEntityCommandBufferSystem>();
            Measure.Method(() => { createEcbSystem.Update(World.Unmanaged); })
                .CleanUp(() => {
                    ecbSystem.Update(World.Unmanaged);
                    World.UpdateAllocator.Rewind();
                })
                .SampleGroup(new SampleGroup($"Create_{kEcbCount}x", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_Dispose([Values] bool createParallelWriters)
        {
            const int kEcbCount = 100;
            // Use a singleton to communicate whether the system should create the ECBs
            // as ParallelWriters or not, and how many.
            var e = m_Manager.CreateEntity(typeof(EcsTestData2));
            m_Manager.SetComponentData(e,
                new EcsTestData2 { value0 = createParallelWriters ? 1 : 0, value1 = kEcbCount });

            var createEcbSystem = World.CreateSystem<CreateEmptyEcbSystem>();
            var ecbSystem = World.CreateSystem<EndSimulationEntityCommandBufferSystem>();
            Measure.Method(() => { ecbSystem.Update(World.Unmanaged); })
                .SetUp(() => { createEcbSystem.Update(World.Unmanaged); })
                .CleanUp(() => { World.UpdateAllocator.Rewind(); })
                .SampleGroup(new SampleGroup($"PlaybackAndDispose_{kEcbCount}x", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_512SimpleEntities()
        {
            const int kCreateLoopCount = 512;
            const int kPlaybackLoopCount = 1000;

            var ecbs = new List<EntityCommandBuffer>(kPlaybackLoopCount);
            Measure.Method(
                () =>
                {
                    for (int repeat = 0; repeat < kPlaybackLoopCount; ++repeat)
                    {
                        var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                        FillWithEcsTestData(cmds, kCreateLoopCount);
                        ecbs.Add(cmds);
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(0)
                .MeasurementCount(1)
                .Run();

            Measure.Method(
                () =>
                {
                    for (int repeat = 0; repeat < kPlaybackLoopCount; ++repeat)
                    {
                        ecbs[repeat].Playback(m_Manager);
                    }
                })
                .SampleGroup("Playback")
                .WarmupCount(0)
                .MeasurementCount(1)
                .CleanUp(() =>
                {
                })
                .Run();

            foreach (var ecb in ecbs)
            {
                ecb.Dispose();
            }
        }

        [Test, Performance]
        public void EntityCommandBuffer_512EntitiesWithEmbeddedEntity()
        {
            const int kCreateLoopCount = 512;
            const int kPlaybackLoopCount = 1000;

            var ecbs = new List<EntityCommandBuffer>(kPlaybackLoopCount);
            Measure.Method(
                () =>
                {
                    for (int repeat = 0; repeat < kPlaybackLoopCount; ++repeat)
                    {
                        var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                        FillWithEcsTestDataWithEntity(cmds, kCreateLoopCount);
                        ecbs.Add(cmds);
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(0)
                .MeasurementCount(1)
                .Run();
            Measure.Method(
                () =>
                {
                    for (int repeat = 0; repeat < kPlaybackLoopCount; ++repeat)
                    {
                        ecbs[repeat].Playback(m_Manager);
                    }
                })
                .SampleGroup("Playback")
                .WarmupCount(0)
                .MeasurementCount(1)
                .Run();
            foreach (var ecb in ecbs)
            {
                ecb.Dispose();
            }
        }

        [Test, Performance]
        public void EntityCommandBuffer_OneEntityWithEmbeddedEntityAnd512SimpleEntities()
        {
            // This test should not be any slower than EntityCommandBuffer_SimpleEntities_512x1000
            // It shows that adding one component that needs fix up will not make the fast
            // path any slower

            const int kCreateLoopCount = 512;
            const int kPlaybackLoopCount = 1000;


            var ecbs = new List<EntityCommandBuffer>(kPlaybackLoopCount);
            Measure.Method(
                () =>
                {
                    for (int repeat = 0; repeat < kPlaybackLoopCount; ++repeat)
                    {
                        var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                        Entity e0 = cmds.CreateEntity();
                        cmds.AddComponent(e0, new EcsTestDataWithEntity {value = -1, entity = e0 });
                        FillWithEcsTestData(cmds, kCreateLoopCount);
                        ecbs.Add(cmds);
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(0)
                .MeasurementCount(1)
                .Run();
            Measure.Method(
                () =>
                {
                    for (int repeat = 0; repeat < kPlaybackLoopCount; ++repeat)
                        ecbs[repeat].Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(0)
                .MeasurementCount(1)
                .Run();
            foreach (var ecb in ecbs)
            {
                ecb.Dispose();
            }
        }

        // ----------------------------------------------------------------------------------------------------------
        // BLITTABLE
        // ----------------------------------------------------------------------------------------------------------
        [Test, Performance]
        public void EntityCommandBuffer_DestroyEntity([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithDestroyEntityCommands(ecb, entities);
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithDestroyEntityCommands(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_CreateEntities([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    FillWithCreateEntityCommands(ecb, size);
                })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    FillWithCreateEntityCommands(ecb, size);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_InstantiateEntities([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            var prefabEntity = m_Manager.CreateEntity(archetype1);
            Measure.Method(
                () =>
                {
                    FillWithInstantiateEntityCommands(ecb, size, prefabEntity);
                })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    prefabEntity = m_Manager.CreateEntity(archetype1);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    prefabEntity = m_Manager.CreateEntity(archetype1);
                    FillWithInstantiateEntityCommands(ecb, size, prefabEntity);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_AddComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithAddComponentCommands(ecb, entities, typeof(EcsTestData2));
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithAddComponentCommands(ecb, entities, typeof(EcsTestData2));
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_SetComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithSetComponentCommands(ecb, entities);
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithSetComponentCommands(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_RemoveComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithRemoveComponentCommands(ecb, entities);
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithRemoveComponentCommands(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        // ----------------------------------------------------------------------------------------------------------
        // MANAGED
        // ----------------------------------------------------------------------------------------------------------
        [Test, Performance]
        public void EntityCommandBuffer_AddSharedComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithEcsTestSharedComp(ecb, entities);
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithEcsTestSharedComp(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test, Performance]
        public void EntityCommandBuffer_AddManagedComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithEcsTestManagedComp(ecb, entities);
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithEcsTestManagedComp(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

#endif

        [Test, Performance]
        public void EntityCommandBuffer_SetSharedComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithSetEcsTestSharedComp(ecb, entities);
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype2);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype2);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithSetEcsTestSharedComp(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test, Performance]
        public void EntityCommandBuffer_SetManagedComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithSetEcsTestManagedComp(ecb, entities);
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype3);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype3);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithSetEcsTestManagedComp(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

#endif

        [Test, Performance]
        public void EntityCommandBuffer_AddComponentToEntityQuery([Values(10, 1000, 10000)] int size, [Values] EntityQueryCaptureMode captureMode)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    ecb.AddComponent(query, typeof(EcsTestTag), captureMode);
                })
                .SampleGroup(new SampleGroup("Record", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype1, size);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup(new SampleGroup("Playback", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype1, size);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    ecb.AddComponent(query, typeof(EcsTestTag), captureMode);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_RemoveComponentFromEntityQuery([Values(10, 1000, 10000)] int size, [Values] EntityQueryCaptureMode captureMode)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    ecb.RemoveComponent(query, typeof(EcsTestData), captureMode);
                })
                .SampleGroup(new SampleGroup("Record", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype1, size);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup(new SampleGroup("Playback", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype1, size);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    ecb.RemoveComponent(query, typeof(EcsTestData), captureMode);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_DestroyEntitiesInEntityQuery([Values(10, 1000, 10000)] int size, [Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    ecb.DestroyEntity(query, queryCaptureMode);
                })
                .SampleGroup(new SampleGroup("Record", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype1, size);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup(new SampleGroup("Playback", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype1, size);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    ecb.DestroyEntity(query, EntityQueryCaptureMode.AtPlayback);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_AddSharedComponentToEntityQuery([Values(10, 1000, 10000)] int size, [Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    ecb.AddSharedComponent(query, new EcsTestSharedComp {value = 1}, queryCaptureMode);
                })
                .SampleGroup(new SampleGroup("Record", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype1, size);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup(new SampleGroup("Playback", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype1, size);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    ecb.AddSharedComponent(query, new EcsTestSharedComp {value = 1}, queryCaptureMode);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_SetSharedComponentToEntityQuery([Values(10, 1000, 10000)] int size, [Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    ecb.SetSharedComponent(query, new EcsTestSharedComp {value = 1}, queryCaptureMode);
                })
                .SampleGroup(new SampleGroup("Record", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype2, size);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup(new SampleGroup("Playback", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype2, size);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    ecb.SetSharedComponent(query, new EcsTestSharedComp {value = 1}, queryCaptureMode);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_AddComponent_SingleVsMultiple([Values(10, 100, 1000, 10000)] int size)
        {
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var ecb = default(EntityCommandBuffer);
            NativeArray<Entity> entities = default;
            Measure.Method(
                () =>
                {
                    for (int i = 0; i < size; ++i)
                        ecb.AddComponent<EcsTestData2>(entities[i]);
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Individual_Packed")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    entities = m_Manager.CreateEntity(archetype1, size, World.UpdateAllocator.ToAllocator);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    m_Manager.DestroyEntity(entities);
                    entities.Dispose();
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        for (int i = 0; i < size; ++i)
                            ecb.AddComponent<EcsTestData2>(entities[i]);
                        ecb.Playback(m_Manager);
                    })
                .SampleGroup("Individual_Sparse")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    var allEntities = m_Manager.CreateEntity(archetype1, 2*size, World.UpdateAllocator.ToAllocator);
                    entities = CollectionHelper.CreateNativeArray<Entity>(size, World.UpdateAllocator.ToAllocator,
                        NativeArrayOptions.UninitializedMemory);
                    for (int i = 0; i < size; ++i)
                        entities[i] = allEntities[2 * i];
                    allEntities.Dispose();
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    m_Manager.DestroyEntity(query);
                    entities.Dispose();
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        ecb.AddComponent<EcsTestData2>(entities);
                        ecb.Playback(m_Manager);
                    })
                .SampleGroup("Batched_Packed")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    entities = m_Manager.CreateEntity(archetype1, size, World.UpdateAllocator.ToAllocator);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    m_Manager.DestroyEntity(entities);
                    entities.Dispose();
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        ecb.AddComponent<EcsTestData2>(entities);
                        ecb.Playback(m_Manager);
                    })
                .SampleGroup("Batched_Sparse")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    var allEntities = m_Manager.CreateEntity(archetype1, 2*size, World.UpdateAllocator.ToAllocator);
                    entities = CollectionHelper.CreateNativeArray<Entity>(size, World.UpdateAllocator.ToAllocator,
                        NativeArrayOptions.UninitializedMemory);
                    for (int i = 0; i < size; ++i)
                        entities[i] = allEntities[2 * i];
                    allEntities.Dispose();
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    m_Manager.DestroyEntity(query);
                    entities.Dispose();
                    ecb.Dispose();
                })
                .Run();
        }
    }
}
