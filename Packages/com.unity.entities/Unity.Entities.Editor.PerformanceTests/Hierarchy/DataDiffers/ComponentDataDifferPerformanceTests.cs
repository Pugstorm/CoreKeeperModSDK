using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Editor.Tests;
using Unity.PerformanceTesting;

namespace Unity.Entities.Editor.PerformanceTests
{
    [TestFixture]
    [Category(Categories.Performance)]
    unsafe class ComponentDataDifferPerformanceTests : DifferTestFixture
    {
        [Test, Performance]
        public void ComponentDataDiffer_Spawn_PerformanceTest([Values(1000, 10_000, 100_000, 250_000, 500_000)]
                                                              int entityCount)
        {
            var entities = CreateEntitiesWithMockSharedComponentData(entityCount, World.UpdateAllocator.ToAllocator, i => i % 100, typeof(EcsTestData), typeof(EcsTestSharedComp));
            var sharedComponentCount = World.EntityManager.GetSharedComponentCount();
            var query = World.EntityManager.CreateEntityQuery(typeof(EcsTestData));
            var ecs = World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore;
            ComponentDataDiffer componentDiffer = null;

            Measure.Method(() =>
                {
                    var result = componentDiffer.GatherComponentChangesAsync(query, World.UpdateAllocator.ToAllocator, out var handle);
                    handle.Complete();
                    result.Dispose();
                })
                .SetUp(() =>
                {
                    componentDiffer = new ComponentDataDiffer(ecs, typeof(EcsTestData));
                })
                .CleanUp(() =>
                {
                    componentDiffer.Dispose();
                })
                .SampleGroup($"First check over {entityCount} entities using {sharedComponentCount} different shared components")
                .WarmupCount(10)
                .MeasurementCount(100)
                .Run();

            entities.Dispose();
            query.Dispose();
        }

        [Test, Performance]
        public void ComponentDataDiffer_NoChange_PerformanceTest([Values(0, 1000, 10_000, 100_000, 250_000, 500_000)]
                                                                 int entityCount)
        {
            var entities = CreateEntitiesWithMockSharedComponentData(entityCount, World.UpdateAllocator.ToAllocator, i => i % 100, typeof(EcsTestData), typeof(EcsTestSharedComp));
            var sharedComponentCount = World.EntityManager.GetSharedComponentCount();
            var query = World.EntityManager.CreateEntityQuery(typeof(EcsTestData));
            var ecs = World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore;
            var componentDiffer = new ComponentDataDiffer(ecs, typeof(EcsTestData));

            Measure.Method(() =>
                {
                    var result = componentDiffer.GatherComponentChangesAsync(query, World.UpdateAllocator.ToAllocator, out var handle);
                    handle.Complete();
                    result.Dispose();
                })
                .SampleGroup($"First check over {entityCount} entities using {sharedComponentCount} different shared components")
                .WarmupCount(10)
                .MeasurementCount(100)
                .Run();

            entities.Dispose();
            query.Dispose();
            componentDiffer.Dispose();
        }

        [Test, Performance]
        public unsafe void ComponentDataDiffer_Change_PerformanceTest([Values(1000, 10_000, 100_000, 500_000)]
            int entityCount,
            [Values(1000, 5000, 10_000)]
            int changeCount)
        {
            var entities = CreateEntitiesWithMockSharedComponentData(entityCount, World.UpdateAllocator.ToAllocator, typeof(EcsTestData));
            var query = World.EntityManager.CreateEntityQuery(typeof(EcsTestData));
            var ecs = World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore;
            var componentDiffer = new ComponentDataDiffer(ecs, typeof(EcsTestData));
            var counter = entities.Length;
            if (changeCount > entityCount)
                changeCount = entityCount;

            Measure.Method(() =>
            {
                var result = componentDiffer.GatherComponentChangesAsync(query, World.UpdateAllocator.ToAllocator, out var handle);
                handle.Complete();
                result.Dispose();
            })
            .SetUp(() =>
            {
                World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
                for (var i = 0; i < changeCount; i++)
                {
                    World.EntityManager.SetComponentData(entities[i], new EcsTestData { value = counter++ });
                }
            })
            .SampleGroup($"{changeCount} changes over {entityCount} entities")
            .WarmupCount(10)
            .MeasurementCount(100)
            .Run();

            entities.Dispose();
            query.Dispose();
            componentDiffer.Dispose();
        }
    }
}
