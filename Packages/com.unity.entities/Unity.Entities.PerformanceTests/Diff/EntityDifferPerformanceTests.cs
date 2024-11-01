using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Tests;
using Unity.PerformanceTesting;

namespace Unity.Entities.PerformanceTests
{
    [TestFixture]
    [Category("Performance")]
    class EntityDifferPerformanceTests : EntityDifferPerformanceTestFixture
    {
        /// <summary>
        /// Performance test for getting changes with all options.
        /// </summary>
        [Test, Performance]
        [TestCase(100)]
        [TestCase(1000)]
        public void PerformanceTest_EntityDiffer_GetChanges_DefaultOptions(int entityCount)
        {
            CreateEntitiesWithMockComponentData(SrcEntityManager, entityCount, typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));

            Measure.Method(() =>
            {
                using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    using (differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                    {
                    }
                }
            })
                .SampleGroup("EntityDiffer")
                .WarmupCount(1)
                .MeasurementCount(100)
                .CleanUp(() => { SrcWorld.UpdateAllocator.Rewind(); })
                .Run();
        }

        /// <summary>
        /// Performance test for getting changes with referential equality.
        /// </summary>
        [Test, Performance]
        [TestCase(1000)]
        public void PerformanceTest_EntityDiffer_GetChanges_ReferentialEquality(int entityCount)
        {
            CreateEntitiesWithMockComponentData(SrcEntityManager, entityCount, typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp), typeof(EcsTestDataEntity));

            Measure.Method(() =>
            {
                using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    using (differ.GetChanges(EntityManagerDifferOptions.Default | EntityManagerDifferOptions.UseReferentialEquality, SrcWorld.UpdateAllocator.ToAllocator))
                    {
                    }
                }
            })
                .SampleGroup("EntityDiffer")
                .WarmupCount(1)
                .MeasurementCount(100)
                .CleanUp(() => { SrcWorld.UpdateAllocator.Rewind(); })
                .Run();
        }

        /// <summary>
        /// Test to generate a forward change set for a given number of entities.
        /// </summary>
        /// <param name="entityCount"></param>
        [Test, Performance]
        [TestCase(1000)]
        public void PerformanceTest_EntityDiffer_GetChanges_ForwardChangesOnly(int entityCount)
        {
            CreateEntitiesWithMockComponentData(SrcEntityManager, entityCount, typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));

            Measure.Method(() =>
            {
                using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    using (differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet, SrcWorld.UpdateAllocator.ToAllocator))
                    {
                    }
                }
            })
                .SampleGroup("EntityDiffer")
                .WarmupCount(1)
                .MeasurementCount(100)
                .CleanUp(() => { SrcWorld.UpdateAllocator.Rewind(); })
                .Run();
        }

        /// <summary>
        /// Performance test for fast forwarding the shadow world without generating changes.
        /// </summary>
        [Test, Performance]
        [TestCase(1000)]
        [TestCase(10000)]
        public void PerformanceTest_EntityDiffer_GetChanges_FastForwardOnly(int entityCount)
        {
            CreateEntitiesWithMockComponentData(SrcEntityManager, entityCount, typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));

            Measure.Method(() =>
            {
                using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    using (differ.GetChanges(EntityManagerDifferOptions.FastForwardShadowWorld, SrcWorld.UpdateAllocator.ToAllocator))
                    {
                    }
                }
            })
                .SampleGroup("EntityDiffer")
                .WarmupCount(1)
                .MeasurementCount(100)
                .CleanUp(() => { SrcWorld.UpdateAllocator.Rewind(); })
                .Run();
        }

        /// <summary>
        /// Test case for an incremental update where no changes happen. This is the best case scenario where we can skip all work.
        /// </summary>
        [Test, Performance]
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        public void PerformanceTest_EntityDiffer_GetChanges_NoChanges(int entityCount)
        {
            CreateEntitiesWithMockComponentData(SrcEntityManager, entityCount, typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));

            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                // Fast forward the shadow world
                using (differ.GetChanges(EntityManagerDifferOptions.FastForwardShadowWorld, SrcWorld.UpdateAllocator.ToAllocator))
                {
                }

                Measure.Method(() =>
                {
                    // Get changes with all options selected
                    using (differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                    {
                    }
                })
                    .SampleGroup("EntityDiffer")
                    .WarmupCount(1)
                    .MeasurementCount(100)
                    .Run();
            }
        }

        /// <summary>
        /// Test case for an incremental update where at least one change happens.
        ///
        /// This is the most common use case where chunk diffing is incremental BUT lookups must be generated.
        /// </summary>
        [Test, Performance]
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        public void PerformanceTest_EntityDiffer_GetChanges_ForwardChangesOnly_SingleChange(int entityCount)
        {
            CreateEntitiesWithMockComponentData(SrcEntityManager, entityCount, typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));

            var entity = default(Entity);

            using (var entities = SrcEntityManager.GetAllEntities(Allocator.Temp))
            {
                entity = entities[0];
            }

            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                // Fast forward the shadow world
                using (differ.GetChanges(EntityManagerDifferOptions.FastForwardShadowWorld, SrcWorld.UpdateAllocator.ToAllocator))
                {
                }

                var startValue = 99;

                Measure.Method(() =>
                {
                    SrcEntityManager.SetComponentData(entity, new EcsTestData(startValue++));

                    // Get changes with all options selected
                    using (differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet, SrcWorld.UpdateAllocator.ToAllocator))
                    {
                    }
                })
                    .SampleGroup("EntityDiffer")
                    .WarmupCount(1)
                    .MeasurementCount(100)
                    .Run();
            }
        }
    }
}
