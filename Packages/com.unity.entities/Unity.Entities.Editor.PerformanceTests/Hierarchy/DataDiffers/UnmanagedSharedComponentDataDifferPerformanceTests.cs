using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Editor.Tests;
using Unity.PerformanceTesting;

namespace Unity.Entities.Editor.PerformanceTests
{
    [TestFixture]
    [Category(Categories.Performance)]
    class UnmanagedSharedComponentDataDifferPerformanceTests : DifferTestFixture
    {
        [Test, Performance]
        public void UnmanagedSharedComponentDataDiffer_Spawn([Values(2, 1000, 10_000, 100_000, 250_000, 500_000)] int entityCount)
        {
            var entities = CreateEntitiesWithMockSharedComponentData(entityCount, World.UpdateAllocator.ToAllocator, i => i % 100, typeof(EcsTestData), typeof(EcsTestSharedComp));
            var sharedComponentCount = World.EntityManager.GetSharedComponentCount();
            var query = World.EntityManager.CreateEntityQuery(typeof(EcsTestData));
            UnmanagedSharedComponentDataDiffer differ = null;

            Measure.Method(() =>
                {
                    var result = differ.GatherComponentChanges(World.EntityManager, query, World.UpdateAllocator.ToAllocator);
                    result.Dispose();
                })
                .SetUp(() =>
                {
                    differ = new UnmanagedSharedComponentDataDiffer(typeof(EcsTestSharedComp));
                })
                .CleanUp(() =>
                {
                    differ.Dispose();
                })
                .SampleGroup($"First check over {entityCount} entities using {sharedComponentCount} different shared components")
                .WarmupCount(10)
                .MeasurementCount(100)
                .Run();

            entities.Dispose();
            query.Dispose();
        }

        [Test, Performance]
        public void UnmanagedSharedComponentDataDiffer_NoChanges([Values(0, 1000, 10_000, 100_000, 500_000)] int entityCount)
        {
            var entities = CreateEntitiesWithMockSharedComponentData(entityCount, World.UpdateAllocator.ToAllocator, i => i % 100, typeof(EcsTestData), typeof(EcsTestSharedComp));
            var sharedComponentCount = World.EntityManager.GetSharedComponentCount();
            var query = World.EntityManager.CreateEntityQuery(typeof(EcsTestData));
            var differ = new UnmanagedSharedComponentDataDiffer(typeof(EcsTestSharedComp));

            Measure.Method(() =>
                {
                    var result = differ.GatherComponentChanges(World.EntityManager, query, World.UpdateAllocator.ToAllocator);
                    result.Dispose();
                })
                .SampleGroup($"Check over {entityCount} entities using {sharedComponentCount} different shared components")
                .WarmupCount(10)
                .MeasurementCount(100)
                .Run();

            differ.Dispose();
            entities.Dispose();
            query.Dispose();
        }

        [Test, Performance]
        public unsafe void UnmanagedSharedComponentDataDiffer_Change(
            [Values(1000, 10_000, 100_000, 500_000)] int entityCount,
            [Values(1000, 5000, 10_000)] int changeCount)
        {
            var entities = CreateEntitiesWithMockSharedComponentData(entityCount, World.UpdateAllocator.ToAllocator, i => i % 100, typeof(EcsTestData), typeof(EcsTestSharedComp));
            var sharedComponentCount = World.EntityManager.GetSharedComponentCount();
            var query = World.EntityManager.CreateEntityQuery(typeof(EcsTestData));
            var differ = new UnmanagedSharedComponentDataDiffer(typeof(EcsTestSharedComp));
            var counter = entities.Length;
            if (changeCount > entityCount)
                changeCount = entityCount;

            Measure.Method(() =>
                {
                    var result = differ.GatherComponentChanges(World.EntityManager, query, World.UpdateAllocator.ToAllocator);
                    result.Dispose();
                })
                .SetUp(() =>
                {
                    World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
                    for (var i = 0; i < changeCount; i++)
                    {
                        World.EntityManager.SetSharedComponent(entities[i], new EcsTestSharedComp { value = counter++ % 100 });
                    }
                })
                .SampleGroup($"{changeCount} changes over {entityCount} entities using {sharedComponentCount} different shared components")
                .WarmupCount(10)
                .MeasurementCount(100)
                .Run();

            entities.Dispose();
            query.Dispose();
            differ.Dispose();
        }
    }
}
