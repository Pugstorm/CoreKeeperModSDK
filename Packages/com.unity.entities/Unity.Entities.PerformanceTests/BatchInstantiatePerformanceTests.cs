using System.Collections.Generic;
using NUnit.Framework;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities.Tests;
using Unity.Jobs;
using Unity.PerformanceTesting;


namespace Unity.Entities.PerformanceTests
{
    [TestFixture]
    [Category("Performance")]
    public sealed partial class BatchInstantiatePerformanceTests : EntityPerformanceTestFixture
    {
        [Test, Performance]
        public void BatchInstantiateAndTranslate_MainThread([Values(10, 1000, 100000)] int entityCount)
        {
            // This variant instantiates and translates the entities entirely on the main thread using EntityManager commands.
            var prefabEntity = m_Manager.CreateEntity(typeof(EcsTestFloatData3), typeof(Prefab));
            using var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestFloatData3>());
            using var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, World.UpdateAllocator.ToAllocator);
            Measure.Method(
                    () =>
                    {
                        m_Manager.Instantiate(prefabEntity, entities);
                        for (int i = 0; i < entities.Length; ++i)
                        {
                            m_Manager.SetComponentData(entities[i],
                                new EcsTestFloatData3 {Value0 = i, Value1 = i, Value2 = i});
                        }
                    })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() => { })
                .CleanUp(() => { m_Manager.DestroyEntity(query); })
                .Run();
        }

        [BurstCompile(CompileSynchronously = true)]
        struct TranslateJob : IJobChunk
        {
            public ComponentTypeHandle<EcsTestFloatData3> PosHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                var positions = chunk.GetNativeArray(ref PosHandle);
                for (int i = 0; i < positions.Length; ++i)
                {
                    positions[i] = new EcsTestFloatData3 {Value0 = i, Value1 = 1, Value2 = i};
                }
            }
        }

        [Test, Performance]
        public void BatchInstantiateAndTranslate_Hybrid([Values(10, 1000, 100000)] int entityCount)
        {
            // This variant instantiates the entities on the main thread, and uses a parallel job to apply a random translation to each entity.
            var prefabEntity = m_Manager.CreateEntity(typeof(EcsTestFloatData3), typeof(Prefab));
            using var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestFloatData3>());
            using var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, World.UpdateAllocator.ToAllocator);
            Measure.Method(
                    () =>
                    {
                        m_Manager.Instantiate(prefabEntity, entities);
                        new TranslateJob
                        {
                            PosHandle = m_Manager.GetComponentTypeHandle<EcsTestFloatData3>(false),
                        }.ScheduleParallel(query, default).Complete();
                    })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                })
                .CleanUp(() =>
                {
                    m_Manager.DestroyEntity(query);
                })
                .Run();
        }

        [BurstCompile(CompileSynchronously = true)]
        struct EcbInstantiateAndTranslateJob : IJobParallelFor
        {
            public Entity PrefabEntity;
            public EntityCommandBuffer.ParallelWriter ecb;
            public void Execute(int index)
            {
                var ent = ecb.Instantiate(index, PrefabEntity);
                ecb.SetComponent(index, ent, new EcsTestFloatData3 {Value0 = index, Value1 = index, Value2 = index});
            }
        }

        [Test, Performance]
        public void BatchInstantiateAndTranslate_EntityCommandBuffer_Serial([Values(10, 1000, 100000)] int entityCount)
        {
            // This variant does all instantiation & translation through individual non-batched ECB commands, recorded in a parallel job.
            var prefabEntity = m_Manager.CreateEntity(typeof(EcsTestFloatData3), typeof(Prefab));
            EntityCommandBuffer ecb = default;
            using var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestFloatData3>());
            Measure.Method(
                    () =>
                    {
                        ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                        new EcbInstantiateAndTranslateJob
                        {
                            PrefabEntity = prefabEntity,
                            ecb = ecb.AsParallelWriter(),
                        }.Schedule(entityCount, 64).Complete();
                        ecb.Playback(m_Manager);
                    })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                })
                .CleanUp(() =>
                {
                    ecb.Dispose();
                    m_Manager.DestroyEntity(query);
                    World.UpdateAllocator.Rewind();
                })
                .Run();
        }

        [BurstCompile(CompileSynchronously = true)]
        struct EcbTranslateJob : IJobParallelFor
        {
            public NativeArray<Entity> Entities;
            public EntityCommandBuffer.ParallelWriter ecb;
            public void Execute(int index)
            {
                ecb.SetComponent(index, Entities[index], new EcsTestFloatData3 {Value0 = index, Value1 = index, Value2 = index});
            }
        }

        [Test, Performance]
        public void BatchInstantiateAndTranslate_EntityCommandBuffer_Hybrid([Values(10, 1000, 100000)] int entityCount)
        {
            // This variant instantiates the entities on the main thread, and uses a parallel job to apply a random translation to each entity.
            var prefabEntity = m_Manager.CreateEntity(typeof(EcsTestFloatData3), typeof(Prefab));
            EntityCommandBuffer ecb = default;
            using var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, World.UpdateAllocator.ToAllocator);
            using var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestFloatData3>());
            Measure.Method(
                    () =>
                    {
                        m_Manager.Instantiate(prefabEntity, entities);
                        ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                        new EcbTranslateJob
                        {
                            Entities = entities,
                            ecb = ecb.AsParallelWriter(),
                        }.Schedule(entityCount, 64).Complete();
                        ecb.Playback(m_Manager);
                    })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                })
                .CleanUp(() =>
                {
                    ecb.Dispose();
                    m_Manager.DestroyEntity(query);
                })
                .Run();
        }

        partial class BatchInstantiateSystem : SystemBase
        {
            private Entity _prefabEntity;

            protected override void OnCreate()
            {
                _prefabEntity = EntityManager.CreateEntity(typeof(EcsTestFloatData3), typeof(Prefab));
            }

            protected override void OnUpdate()
            {
                var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                var ecbWriter = ecb.AsParallelWriter();
                var prefab = _prefabEntity;
                Entities
                    .ForEach((Entity e, int entityInQueryIndex, in EcsTestData spawnCount) =>
                    {
                        var entities = new NativeArray<Entity>(spawnCount.value, Allocator.Temp);
                        ecbWriter.Instantiate(entityInQueryIndex, prefab, entities);
                        for (int i = 0; i < entities.Length; ++i)
                        {
                            ecbWriter.SetComponent(entityInQueryIndex, entities[i],
                                new EcsTestFloatData3 {Value0 = i, Value1 = i, Value2 = i});
                        }
                        entities.Dispose();
                        ecbWriter.DestroyEntity(entityInQueryIndex, e);
                    }).ScheduleParallel(Dependency).Complete();
                ecb.Playback(EntityManager);
                ecb.Dispose();
            }
        }

        [Test]
        public void TestBatchInstantiate()
        {
            var spawnerEntity = m_Manager.CreateEntity(typeof(EcsTestData));
            int spawnCount = 100000;
            m_Manager.SetComponentData(spawnerEntity, new EcsTestData {value = spawnCount});

            var sys = m_World.CreateSystemManaged<BatchInstantiateSystem>();
            sys.Update();

            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestFloatData3));
            using var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(spawnCount, entities.Length);
            for (int i = 0; i < entities.Length; ++i)
            {
                var f3 = m_Manager.GetComponentData<EcsTestFloatData3>(entities[i]);
                Assert.AreEqual(new EcsTestFloatData3 {Value0 = i, Value1 = i, Value2 = i}, f3);
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        struct EcbInstantiateOneAndTranslateJob : IJob
        {
            public Entity PrefabEntity;
            public int SpawnCount;
            public EntityCommandBuffer ecb;
            public void Execute()
            {
                for (int i = 0; i < SpawnCount; ++i)
                {
                    var e = ecb.Instantiate(PrefabEntity);
                    ecb.SetComponent(e, new EcsTestFloatData3 {Value0 = i, Value1 = i, Value2 = i});
                }
            }
        }

        [Test, Performance]
        public void BatchInstantiateAndTranslate_EntityCommandBuffer_InstantiateOne([Values(10, 1000, 100000)] int entityCount)
        {
            // This variant schedules an IJob that instantiates all entities at once, and then individually sets the
            // component for each one (all through an ECB)
            var prefabEntity = m_Manager.CreateEntity(typeof(EcsTestFloatData3), typeof(Prefab));
            EntityCommandBuffer ecb = default;
            using var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestFloatData3>());
            Measure.Method(
                    () =>
                    {
                        ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                        new EcbInstantiateOneAndTranslateJob
                        {
                            PrefabEntity = prefabEntity,
                            SpawnCount = entityCount,
                            ecb = ecb,
                        }.Run();
                        ecb.Playback(m_Manager);
                    })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                })
                .CleanUp(() =>
                {
                    ecb.Dispose();
                    m_Manager.DestroyEntity(query);
                    World.UpdateAllocator.Rewind();
                })
                .Run();
        }

        [BurstCompile(CompileSynchronously = true)]
        struct EcbInstantiateAllAndTranslateJob : IJob
        {
            public Entity PrefabEntity;
            public int SpawnCount;
            public EntityCommandBuffer ecb;
            public void Execute()
            {
                var entities = new NativeArray<Entity>(SpawnCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                ecb.Instantiate(PrefabEntity, entities);
                for (int i = 0; i < entities.Length; ++i)
                {
                    ecb.SetComponent(entities[i], new EcsTestFloatData3 {Value0 = i, Value1 = i, Value2 = i});
                }
                entities.Dispose();
            }
        }

        [Test, Performance]
        public void BatchInstantiateAndTranslate_EntityCommandBuffer_InstantiateAll([Values(10, 1000, 100000)] int entityCount)
        {
            // This variant schedules an IJob that instantiates all entities at once, and then individually sets the
            // component for each one (all through an ECB)
            var prefabEntity = m_Manager.CreateEntity(typeof(EcsTestFloatData3), typeof(Prefab));
            EntityCommandBuffer ecb = default;
            using var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestFloatData3>());
            Measure.Method(
                    () =>
                    {
                        ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                        new EcbInstantiateAllAndTranslateJob
                        {
                            PrefabEntity = prefabEntity,
                            SpawnCount = entityCount,
                            ecb = ecb,
                        }.Run();
                        ecb.Playback(m_Manager);
                    })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                })
                .CleanUp(() =>
                {
                    ecb.Dispose();
                    m_Manager.DestroyEntity(query);
                    World.UpdateAllocator.Rewind();
                })
                .Run();
        }
    }
}
