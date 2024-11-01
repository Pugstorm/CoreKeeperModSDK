using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Tests;
using Unity.PerformanceTesting;
using static Unity.Entities.PerformanceTests.EntityCommandsInEntitiesForEachPerformanceTests;

namespace Unity.Entities.PerformanceTests
{
    public partial class EntityCommandsPerformanceTestingSystem : SystemBase
    {
        protected override void OnUpdate()
        {
        }

        public void AddComponentData_WithImmediatePlayback(OperationType operationType)
        {
            switch (operationType)
            {
                case OperationType.EntityCommands:
                {
                    Entities
                        .WithImmediatePlayback()
                        .ForEach(
                            (Entity e, EntityCommandBuffer entityCommandBuffer, in EntityCommandsPerformanceTestTag tag) =>
                            {
                                entityCommandBuffer.AddComponent(e, new EcsTestData(10));
                            })
                        .Run();
                    break;
                }
                case OperationType.MainThreadEcbUse:
                {
                    // We call CompleteDependency() to ensure parity with the test for OperationType.EntityCommands.
                    // When an EntityCommands parameter is used inside Entities.ForEach((EntityCommands _) => {}).Run(),
                    // the generated code includes a call to CompleteDependency().
                    CompleteDependency();

                    var ecb = new EntityCommandBuffer(Allocator.Temp);

                    using (var entities = EntityManager.CreateEntityQuery(typeof(EntityCommandsPerformanceTestTag)).ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        foreach (var entity in entities)
                        {
                            ecb.AddComponent(entity, new EcsTestData(10));
                        }
                    }

                    ecb.Playback(EntityManager);
                    ecb.Dispose();
                    break;
                }
                case OperationType.EntitiesForEachEcbUse:
                {
                    var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

                    Entities.ForEach((Entity entity, in EntityCommandsPerformanceTestTag tag) =>
                    {
                        ecb.AddComponent(entity, new EcsTestData(10));
                    }).Run();

                    ecb.Playback(EntityManager);
                    ecb.Dispose();
                    break;
                }
            }
        }

        public void AddComponentData_WithDeferredPlayback(ScheduleType scheduleType)
        {
            switch (scheduleType)
            {
                case ScheduleType.Run:
                {
                    Entities
                        .WithAll<EntityCommandsPerformanceTestTag>()
                        .WithDeferredPlaybackSystem<PlaybackSystem>()
                        .ForEach(
                            (Entity e, EntityCommandBuffer entityCommandBuffer) =>
                            {
                                entityCommandBuffer.AddComponent(e, new EcsTestData(10));
                            })
                        .Run();
                    break;
                }
                case ScheduleType.Schedule:
                {
                    Entities
                        .WithAll<EntityCommandsPerformanceTestTag>()
                        .WithDeferredPlaybackSystem<PlaybackSystem>()
                        .ForEach(
                            (Entity e, EntityCommandBuffer entityCommandBuffer) =>
                            {
                                entityCommandBuffer.AddComponent(e, new EcsTestData(10));
                            })
                        .Schedule();
                    Dependency.Complete();
                    break;
                }
                case ScheduleType.ScheduleParallel:
                {
                    Entities
                        .WithAll<EntityCommandsPerformanceTestTag>()
                        .WithDeferredPlaybackSystem<PlaybackSystem>()
                        .ForEach(
                            (Entity e, EntityCommandBuffer entityCommandBuffer) =>
                            {
                                entityCommandBuffer.AddComponent(e, new EcsTestData(10));
                            })
                        .ScheduleParallel();
                    Dependency.Complete();
                    break;
                }
            }
        }

        public void RemoveComponentData_WithImmediatePlayback(OperationType operationType)
        {
            switch (operationType)
            {
                case OperationType.EntityCommands:
                {
                    Entities
                        .WithImmediatePlayback()
                        .ForEach(
                            (Entity e, EntityCommandBuffer entityCommandBuffer, in EcsTestData testData) =>
                            {
                                entityCommandBuffer.RemoveComponent<EcsTestData>(e);
                            })
                        .Run();
                    break;
                }
                case OperationType.MainThreadEcbUse:
                {
                    // We call CompleteDependency() to ensure parity with the test for OperationType.EntityCommands.
                    // When an EntityCommands parameter is used inside Entities.ForEach((EntityCommands _) => {}).Run(),
                    // the generated code includes a call to CompleteDependency().
                    CompleteDependency();

                    var ecb = new EntityCommandBuffer(Allocator.Temp);

                    using (var entities = EntityManager.CreateEntityQuery(typeof(EcsTestData)).ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        foreach (var entity in entities)
                        {
                            ecb.RemoveComponent(entity, typeof(EcsTestData));
                        }
                    }
                    ecb.Playback(EntityManager);
                    ecb.Dispose();
                    break;
                }
                case OperationType.EntitiesForEachEcbUse:
                {
                    var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

                    Entities.ForEach((Entity entity, in EcsTestData testData) =>
                    {
                        ecb.RemoveComponent<EcsTestData>(entity);
                    }).Run();

                    ecb.Playback(EntityManager);
                    ecb.Dispose();
                    break;
                }
            }
        }

        public void RemoveComponentData_WithDeferredPlayback(ScheduleType scheduleType)
        {
            switch (scheduleType)
            {
                case ScheduleType.Run:
                {
                    Entities
                        .WithDeferredPlaybackSystem<PlaybackSystem>()
                        .ForEach(
                            (Entity e, EntityCommandBuffer entityCommandBuffer, in EcsTestData testData) =>
                            {
                                entityCommandBuffer.RemoveComponent<EcsTestData>(e);
                            })
                        .Run();
                    break;
                }
                case ScheduleType.Schedule:
                {
                    Entities
                        .WithDeferredPlaybackSystem<PlaybackSystem>()
                        .ForEach(
                            (Entity e, EntityCommandBuffer entityCommandBuffer, in EcsTestData testData) =>
                            {
                                entityCommandBuffer.RemoveComponent<EcsTestData>(e);
                            })
                        .Schedule();
                    Dependency.Complete();
                    break;
                }
                case ScheduleType.ScheduleParallel:
                {
                    Entities
                        .WithDeferredPlaybackSystem<PlaybackSystem>()
                        .ForEach(
                            (Entity e, EntityCommandBuffer entityCommandBuffer, in EcsTestData testData) =>
                            {
                                entityCommandBuffer.RemoveComponent<EcsTestData>(e);
                            })
                        .ScheduleParallel();
                    Dependency.Complete();
                    break;
                }
            }
        }

        public void SetComponentData_WithImmediatePlayback(OperationType operationType)
        {
            switch (operationType)
            {
                case OperationType.EntityCommands:
                {
                    Entities
                        .WithImmediatePlayback()
                        .ForEach(
                            (Entity e, EntityCommandBuffer entityCommandBuffer, in EcsTestData data) =>
                            {
                                entityCommandBuffer.SetComponent(e, new EcsTestData(11));
                            })
                        .Run();
                    break;
                }
                case OperationType.MainThreadEcbUse:
                {
                    // We call CompleteDependency() to ensure parity with the test for OperationType.EntityCommands.
                    // When an EntityCommands parameter is used inside Entities.ForEach((EntityCommands _) => {}).Run(),
                    // the generated code includes a call to CompleteDependency().
                    CompleteDependency();

                    var ecb = new EntityCommandBuffer(Allocator.Temp);

                    using (var entities = EntityManager.CreateEntityQuery(typeof(EcsTestData)).ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        foreach (var entity in entities)
                        {
                            ecb.SetComponent(entity, new EcsTestData(11));
                        }
                    }

                    ecb.Playback(EntityManager);
                    ecb.Dispose();
                    break;
                }
                case OperationType.EntitiesForEachEcbUse:
                {
                    var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

                    Entities.ForEach((Entity entity, in EcsTestData data) =>
                    {
                        ecb.SetComponent(entity, new EcsTestData(11));
                    }).Run();

                    ecb.Playback(EntityManager);
                    ecb.Dispose();
                    break;
                }
            }
        }

        public void SetComponentData_WithDeferredPlaybackSystem(ScheduleType scheduleType)
        {
            switch (scheduleType)
            {
                case ScheduleType.Run:
                {
                    Entities
                        .WithDeferredPlaybackSystem<PlaybackSystem>()
                        .ForEach(
                            (Entity e, EntityCommandBuffer entityCommandBuffer) =>
                            {
                                entityCommandBuffer.SetComponent(e, new EcsTestData(10));
                            })
                        .Run();
                    break;
                }
                case ScheduleType.Schedule:
                {
                    Entities
                        .WithDeferredPlaybackSystem<PlaybackSystem>()
                        .ForEach(
                            (Entity e, EntityCommandBuffer entityCommandBuffer) =>
                            {
                                entityCommandBuffer.SetComponent(e, new EcsTestData(10));
                            })
                        .Schedule();
                    Dependency.Complete();
                    break;
                }
                case ScheduleType.ScheduleParallel:
                {
                    Entities
                        .WithDeferredPlaybackSystem<PlaybackSystem>()
                        .ForEach(
                            (Entity e, EntityCommandBuffer entityCommandBuffer) =>
                            {
                                entityCommandBuffer.SetComponent(e, new EcsTestData(10));
                            })
                        .ScheduleParallel();
                    Dependency.Complete();
                    break;
                }
            }
        }

        public void InstantiateEntity_WithImmediatePlayback(Entity prefab, int numEntitiesToInstantiate, OperationType operationType)
        {
            switch (operationType)
            {
                case OperationType.EntitiesForEachEcbUse:
                {
                    var ecb = new EntityCommandBuffer(Allocator.Temp);

                    Entities
                        .WithImmediatePlayback()
                        .WithAll<EcsTestData>()
                        .ForEach(
                            () =>
                            {
                                for (int i = 0; i < numEntitiesToInstantiate; i++)
                                {
                                    ecb.Instantiate(prefab);
                                }
                            })
                        .Run();

                    ecb.Playback(EntityManager);
                    ecb.Dispose();
                    break;
                }
                case OperationType.EntityCommands:
                {
                    Entities
                        .WithImmediatePlayback()
                        .WithAll<EcsTestData>()
                        .ForEach(
                            (EntityCommandBuffer entityCommandBuffer) =>
                            {
                                for (int i = 0; i < numEntitiesToInstantiate; i++)
                                {
                                    entityCommandBuffer.Instantiate(prefab);
                                }
                            })
                        .Run();
                    break;
                }
                case OperationType.MainThreadEcbUse:
                {
                    // We call CompleteDependency() to ensure parity with the test for OperationType.EntityCommands.
                    // When an EntityCommands parameter is used inside Entities.ForEach((EntityCommands _) => {}).Run(),
                    // the generated code includes a call to CompleteDependency().
                    CompleteDependency();

                    var ecb = new EntityCommandBuffer(Allocator.Temp);
                    for (int i = 0; i < numEntitiesToInstantiate; i++)
                    {
                        ecb.Instantiate(prefab);
                    }
                    ecb.Playback(EntityManager);
                    ecb.Dispose();
                    break;
                }
            }
        }

        public void InstantiateEntity_WithDeferredPlaybackSystem(int numEntitiesToInstantiate, ScheduleType scheduleType)
        {
            switch (scheduleType)
            {
                case ScheduleType.Run:
                {
                    Entities
                        .WithDeferredPlaybackSystem<PlaybackSystem>()
                        .ForEach(
                            (Entity e, EntityCommandBuffer entityCommandBuffer, in EcsTestData data) =>
                            {
                                for (int i = 0; i < numEntitiesToInstantiate; i++)
                                {
                                    entityCommandBuffer.Instantiate(e);
                                }
                            })
                        .Run();
                    break;
                }
                case ScheduleType.Schedule:
                {
                    Entities
                        .WithDeferredPlaybackSystem<PlaybackSystem>()
                        .ForEach(
                            (Entity e, EntityCommandBuffer entityCommandBuffer, in EcsTestData data) =>
                            {
                                for (int i = 0; i < numEntitiesToInstantiate; i++)
                                {
                                    entityCommandBuffer.Instantiate(e);
                                }
                            })
                        .Schedule();
                    Dependency.Complete();
                    break;
                }
                case ScheduleType.ScheduleParallel:
                {
                    Entities
                        .WithDeferredPlaybackSystem<PlaybackSystem>()
                        .ForEach(
                            (Entity e, EntityCommandBuffer entityCommandBuffer, in EcsTestData data) =>
                            {
                                for (int i = 0; i < numEntitiesToInstantiate; i++)
                                {
                                    entityCommandBuffer.Instantiate(e);
                                }
                            })
                        .ScheduleParallel();
                    Dependency.Complete();
                    break;
                }
            }
        }

        public void DestroyEntities_WithImmediatePlayback(OperationType operationType)
        {
            switch (operationType)
            {
                case OperationType.EntityCommands:
                {
                    Entities
                        .WithImmediatePlayback()
                        .ForEach(
                            (Entity e, EntityCommandBuffer entityCommandBuffer, in EcsTestData data) =>
                            {
                                entityCommandBuffer.DestroyEntity(e);
                            })
                        .Run();
                    break;
                }
                case OperationType.MainThreadEcbUse:
                {
                    // We call CompleteDependency() to ensure parity with the test for OperationType.EntityCommands.
                    // When an EntityCommands parameter is used inside Entities.ForEach((EntityCommands _) => {}).Run(),
                    // the generated code includes a call to CompleteDependency().
                    CompleteDependency();

                    var ecb = new EntityCommandBuffer(Allocator.Temp);

                    using (var entities = EntityManager.CreateEntityQuery(typeof(EcsTestData)).ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        foreach (var entity in entities)
                        {
                            ecb.DestroyEntity(entity);
                        }
                    }

                    ecb.Playback(EntityManager);
                    ecb.Dispose();
                    break;
                }
                case OperationType.EntitiesForEachEcbUse:
                {
                    var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

                    Entities.ForEach((Entity entity, in EcsTestData data) =>
                    {
                        ecb.DestroyEntity(entity);
                    }).Run();

                    ecb.Playback(EntityManager);
                    ecb.Dispose();
                    break;
                }
            }

        }

        public void DestroyEntities_WithDeferredPlaybackSystem(ScheduleType scheduleType)
        {
            switch (scheduleType)
            {
                case ScheduleType.Run:
                {
                    Entities
                        .WithDeferredPlaybackSystem<PlaybackSystem>()
                        .ForEach(
                            (Entity entity, EntityCommandBuffer entityCommandBuffer, in EcsTestData data) =>
                            {
                                entityCommandBuffer.DestroyEntity(entity);
                            })
                        .Run();
                    break;
                }
                case ScheduleType.ScheduleParallel:
                {
                    Entities
                        .WithDeferredPlaybackSystem<PlaybackSystem>()
                        .ForEach(
                            (Entity entity, EntityCommandBuffer entityCommandBuffer, in EcsTestData data) =>
                            {
                                entityCommandBuffer.DestroyEntity(entity);
                            })
                        .ScheduleParallel();

                    Dependency.Complete();
                    break;
                }
                case ScheduleType.Schedule:
                {
                    Entities
                        .WithDeferredPlaybackSystem<PlaybackSystem>()
                        .ForEach(
                            (Entity entity, EntityCommandBuffer entityCommandBuffer, in EcsTestData data) =>
                            {
                                entityCommandBuffer.DestroyEntity(entity);
                            })
                        .Schedule();

                    Dependency.Complete();
                    break;
                }
            }
        }
    }

    public partial class PlaybackSystem : EntityCommandBufferSystem
    {
    }

    public class EntityCommandsInEntitiesForEachPerformanceTests : ECSTestsFixture
    {
        EntityCommandsPerformanceTestingSystem System => World.GetOrCreateSystemManaged<EntityCommandsPerformanceTestingSystem>();

        [Test, Performance]
        public void RemoveComponentData_WithImmediatePlayback(
            [Values(10, 100, 1000, 10000)] int numEntities,
            [Values] OperationType operationType)
        {
            using (var entities = CollectionHelper.CreateNativeArray<Entity>(numEntities, World.UpdateAllocator.ToAllocator))
            {
                Measure
                    .Method(() => System.RemoveComponentData_WithImmediatePlayback(operationType))
                    .SetUp(() =>
                    {
                        var archetype = System.EntityManager.CreateArchetype(typeof(EcsTestData));
                        System.EntityManager.CreateEntity(archetype, entities);
                    })
                    .WarmupCount(1)
                    .MeasurementCount(10)
                    .CleanUp(() => System.EntityManager.DestroyEntity(System.EntityManager.UniversalQuery))
                    .Run();
            }
        }

        [Test, Performance]
        public void RemoveComponentData_WithDeferredPlaybackSystem(
            [Values(10, 100, 1000, 10000)] int numEntities,
            [Values] ScheduleType scheduleType)
        {
            using (var entities = CollectionHelper.CreateNativeArray<Entity>(numEntities, World.UpdateAllocator.ToAllocator))
            {
                Measure
                    .Method(() => System.RemoveComponentData_WithDeferredPlayback(scheduleType))
                    .SetUp(() =>
                    {
                        var archetype = System.EntityManager.CreateArchetype(typeof(EcsTestData));
                        System.EntityManager.CreateEntity(archetype, entities);
                    })
                    .WarmupCount(1)
                    .MeasurementCount(10)
                    .CleanUp(() => System.EntityManager.DestroyEntity(System.EntityManager.UniversalQuery))
                    .Run();
            }
        }

        [Test, Performance]
        public void DestroyEntities_WithImmediatePlayback(
            [Values(10, 100, 1000, 10000)] int numEntities,
            [Values] OperationType operationType)
        {
            using (var entities = CollectionHelper.CreateNativeArray<Entity>(numEntities, World.UpdateAllocator.ToAllocator))
            {
                Measure
                    .Method(() => System.DestroyEntities_WithImmediatePlayback(operationType))
                    .SetUp(() =>
                    {
                        var archetype = System.EntityManager.CreateArchetype(typeof(EcsTestData));
                        System.EntityManager.CreateEntity(archetype, entities);
                    })
                    .WarmupCount(1)
                    .MeasurementCount(10)
                    .Run();
            }
        }

        [Test, Performance]
        public void DestroyEntities_WithDeferredPlaybackSystem(
            [Values(10, 100, 1000, 10000)] int numEntities,
            [Values] ScheduleType scheduleType)
        {
            using (var entities = CollectionHelper.CreateNativeArray<Entity>(numEntities, World.UpdateAllocator.ToAllocator))
            {
                Measure
                    .Method(() => System.DestroyEntities_WithDeferredPlaybackSystem(scheduleType))
                    .SetUp(() =>
                    {
                        var archetype = System.EntityManager.CreateArchetype(typeof(EcsTestData));
                        System.EntityManager.CreateEntity(archetype, entities);
                    })
                    .WarmupCount(1)
                    .MeasurementCount(10)
                    .Run();
            }
        }

        [Test, Performance]
        public void SetComponentData_WithImmediatePlayback(
            [Values(10, 100, 1000, 10000)] int numEntities,
            [Values] OperationType operationType)
        {
            using (var entities = CollectionHelper.CreateNativeArray<Entity>(numEntities, World.UpdateAllocator.ToAllocator))
            {
                Measure
                    .Method(() => System.SetComponentData_WithImmediatePlayback(operationType))
                    .SetUp(() =>
                    {
                        var archetype = System.EntityManager.CreateArchetype(typeof(EcsTestData));
                        System.EntityManager.CreateEntity(archetype, entities);
                    })
                    .WarmupCount(1)
                    .MeasurementCount(10)
                    .CleanUp(() => System.EntityManager.DestroyEntity(System.GetEntityQuery(typeof(EcsTestData))))
                    .Run();
            }
        }

        [Test, Performance]
        public void SetComponentData_WithDeferredPlaybackSystem(
            [Values(10, 100, 1000, 10000)] int numEntities,
            [Values] ScheduleType scheduleType)
        {
            using (var entities = CollectionHelper.CreateNativeArray<Entity>(numEntities, World.UpdateAllocator.ToAllocator))
            {
                Measure
                    .Method(() => System.SetComponentData_WithDeferredPlaybackSystem(scheduleType))
                    .SetUp(() =>
                    {
                        var archetype = System.EntityManager.CreateArchetype(typeof(EcsTestData));
                        System.EntityManager.CreateEntity(archetype, entities);
                    })
                    .WarmupCount(1)
                    .MeasurementCount(10)
                    .CleanUp(() => System.EntityManager.DestroyEntity(System.GetEntityQuery(typeof(EcsTestData))))
                    .Run();
            }
        }

        public struct EntityCommandsPerformanceTestTag : IComponentData { }

        [Test, Performance]
        public void AddComponentData_WithImmediatePlayback(
            [Values(10, 100, 1000, 10000)] int numEntities,
            [Values] OperationType operationType)
        {
            using (var entities = CollectionHelper.CreateNativeArray<Entity>(numEntities, World.UpdateAllocator.ToAllocator))
            {
                Measure
                    .Method(() => System.AddComponentData_WithImmediatePlayback(operationType))
                    .SetUp(() =>
                    {
                        var entityArchetype = System.EntityManager.CreateArchetype(typeof(EntityCommandsPerformanceTestTag));
                        System.EntityManager.CreateEntity(entityArchetype, entities);
                    })
                    .WarmupCount(1)
                    .MeasurementCount(10)
                    .CleanUp(() => System.EntityManager.DestroyEntity(System.GetEntityQuery(typeof(EcsTestData), typeof(EntityCommandsPerformanceTestTag))))
                    .Run();
            }
        }

        [Test, Performance]
        public void AddComponentData_WithDeferredPlaybackSystem(
            [Values(10, 100, 1000, 10000)] int numEntities,
            [Values] ScheduleType scheduleType)
        {
            using (var entities = CollectionHelper.CreateNativeArray<Entity>(numEntities, World.UpdateAllocator.ToAllocator))
            {
                Measure
                    .Method(() => System.AddComponentData_WithDeferredPlayback(scheduleType))
                    .SetUp(() =>
                    {
                        var entityArchetype = System.EntityManager.CreateArchetype(typeof(EntityCommandsPerformanceTestTag));
                        System.EntityManager.CreateEntity(entityArchetype, entities);
                    })
                    .WarmupCount(1)
                    .MeasurementCount(10)
                    .CleanUp(() => System.EntityManager.DestroyEntity(System.GetEntityQuery(typeof(EcsTestData), typeof(EntityCommandsPerformanceTestTag))))
                    .Run();
            }
        }

        [Test, Performance]
        public void InstantiateEntities_WithDeferredPlaybackSystem(
            [Values(10, 100, 1000, 10000)] int numEntities,
            [Values] ScheduleType scheduleType)
        {
            Measure
                .Method(() => System.InstantiateEntity_WithDeferredPlaybackSystem(numEntities, scheduleType))
                .SetUp(() =>
                {
                    var entity = System.EntityManager.CreateEntity();
                    System.EntityManager.AddComponentData(entity, new EcsTestData(10));
                })
                .WarmupCount(1)
                .MeasurementCount(10)
                .CleanUp(() => System.EntityManager.DestroyEntity(System.GetEntityQuery(typeof(EcsTestData))))
                .Run();
        }

        [Test, Performance]
        public void InstantiateEntities_WithImmediatePlayback(
            [Values(10, 100, 1000, 10000)] int numEntities,
            [Values] OperationType operationType)
        {
            Entity entity = default;

            Measure
                .Method(() => System.InstantiateEntity_WithImmediatePlayback(entity, numEntities, operationType))
                .WarmupCount(1)
                .MeasurementCount(10)
                .SetUp(() =>
                {
                    entity = System.EntityManager.CreateEntity();
                    System.EntityManager.AddComponentData(entity, new EcsTestData(10));
                })
                .CleanUp(() => System.EntityManager.DestroyEntity(System.GetEntityQuery(typeof(EcsTestData))))
                .Run();
        }
    }

    public enum OperationType
    {
        EntityCommands,
        MainThreadEcbUse,
        EntitiesForEachEcbUse
    }
}
