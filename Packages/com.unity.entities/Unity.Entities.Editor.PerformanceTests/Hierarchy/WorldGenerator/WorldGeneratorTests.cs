using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Transforms;

namespace Unity.Entities.Editor.PerformanceTests
{
    [TestFixture]
    [Category(Categories.Performance)]
    class WorldGeneratorTests
    {
        [Test]
        public void GettingAWorldFromAGenerator_ReturnsAFreshInstance()
        {
            var scenario = new EntityHierarchyScenario(
                AmountOfEntities.Low,
                AmountOfChange.None,
                AmountOfFragmentation.Low,
                DepthOfStructure.Shallow,
                ItemsVisibility.AllCollapsed,
                "World Generation Validation");

            using (var generator = new WorldGenerator(scenario))
            {
                var original = generator.Original;
                var instance1 = generator.Get();
                var instance2 = generator.Get();

                // Original world doesn't get overwritten
                Assert.That(original, Is.SameAs(generator.Original));

                // Get() doesn't return the original instance
                Assert.That(generator.Original, Is.Not.SameAs(instance1).And.Not.SameAs(instance2));

                // Get() returns a fresh instance each time
                Assert.That(instance1, Is.Not.SameAs(instance2));
            }
        }

        [Test]
        [Category(Categories.Performance)]
        public void CreatingWorld_MatchesExpectations(
            [Values(AmountOfEntities.Low, AmountOfEntities.Medium)]
            AmountOfEntities amountOfEntities,
            [Values(AmountOfFragmentation.Low, AmountOfFragmentation.High)]
            AmountOfFragmentation amountOfFragmentation,
            [Values(DepthOfStructure.Shallow, DepthOfStructure.Deep)]
            DepthOfStructure depthOfStructure
            )
        {
            var scenario = new EntityHierarchyScenario(
                amountOfEntities,
                AmountOfChange.None,
                amountOfFragmentation,
                depthOfStructure,
                ItemsVisibility.AllCollapsed,
                "World Generation Validation");

            using (var generator = new WorldGenerator(scenario))
            {
                var world = generator.Get();

                // Validate entity count
                var entityCount = world.EntityManager.UniversalQuery.CalculateEntityCount();
                Assert.That(entityCount, Is.EqualTo(scenario.TotalEntities), "Unexpected entity count.");

                // Validate that each generated EntityGuid is unique
                var guidQuery = world.EntityManager.CreateEntityQuery(typeof(EntityGuid));
                var guids = guidQuery.ToComponentDataArray<EntityGuid>(world.UpdateAllocator.ToAllocator);
                Assert.That(guids.Length, Is.EqualTo(scenario.TotalEntities), "Unexpected amount of Entity GUIDs.");
                Assert.That(guids.Distinct().Count(), Is.EqualTo(guids.Length), "Repeat Entity GUIDs found.");

                guidQuery.Dispose();
                guids.Dispose();

                // Validate depth
                var depth = GetStructureDepth(world);
                Assert.That(depth, Is.EqualTo(scenario.MaximumDepth), "Unexpected Depth.");

                // Validate segmentation (how many segments are actually created, and do we have at least one chunk per segment)
                var chunkCount = world.EntityManager.UniversalQuery.CalculateChunkCount();
                Assert.That(chunkCount, Is.GreaterThanOrEqualTo(scenario.SegmentsCount), "Unexpected Chunk count");

                var segmentCount = GetUniqueSharedComponentCount<SegmentId>(world);
                Assert.That(segmentCount, Is.EqualTo(scenario.SegmentsCount), "Unexpected Segment count");

                // Validate fragmentation (average chunk usage)
                var averageChunkUtilization = GetAverageChunkUtilization(world);
                var (low, high) = GetExpectedAverageChunkUtilization(amountOfEntities, amountOfFragmentation, depthOfStructure);
                Assert.That(averageChunkUtilization, Is.GreaterThanOrEqualTo(low).And.LessThanOrEqualTo(high), "Unexpected Chunk utilization.");
            }
        }

        static int GetStructureDepth(World world)
        {
            var entityManager = world.EntityManager;
            var maxDepth = 0;
            using (var query = entityManager.CreateEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(Parent) }, None = new ComponentType[] { typeof(Child) } }))
            using (var parents = query.ToComponentDataArray<Parent>(world.UpdateAllocator.ToAllocator))
            {
                for (var i = 0; i < parents.Length; i++)
                {
                    var current = parents[i].Value;
                    var depth = 1;
                    while (true)
                    {
                        depth++;

                        if (!entityManager.HasComponent<Parent>(current))
                            break;

                        current = entityManager.GetComponentData<Parent>(current).Value;
                    }

                    if (depth > maxDepth)
                        maxDepth = depth;
                }
            }

            return maxDepth;
        }

        static int GetUniqueSharedComponentCount<T>(World world) where T : struct, ISharedComponentData
        {
            var allSharedComponentValues = new List<T>();
            world.EntityManager.GetAllUniqueSharedComponentsManaged(allSharedComponentValues);
            return allSharedComponentValues.Count;
        }

        static float GetAverageChunkUtilization(World world)
        {
            var totalUtilization = 0f;
            var chunks = world.EntityManager.GetAllChunks();
            for (var i = 0; i < chunks.Length; i++)
            {
                var c = chunks[i];
                totalUtilization += (float)c.Count / c.Capacity;
            }

            var averageUtilization = totalUtilization / chunks.Length;

            chunks.Dispose();

            return averageUtilization;
        }

        static (float low, float high) GetExpectedAverageChunkUtilization(AmountOfEntities amountOfEntities, AmountOfFragmentation fragmentation, DepthOfStructure depthOfStructure)
        {
            switch (fragmentation)
            {
                case AmountOfFragmentation.Low
                    // Both conditions can impact fragmentation in uncontrollable ways
                    when amountOfEntities == AmountOfEntities.Low
                      || depthOfStructure == DepthOfStructure.Deep: // We expect chunk to be [66%, 100%] used
                    return (0.66f, 1.00f);
                case AmountOfFragmentation.Low: // We expect chunk to be [80%, 100%] used
                    return (0.80f, 1.00f);
                case AmountOfFragmentation.Medium: // We expect chunks to be [25%, 50%] used
                    return (0.25f, 0.50f);
                case AmountOfFragmentation.High: // We expect chunk to be [0%, 10%] used
                    return (0.00f, 0.10f);
            }

            throw new ArgumentOutOfRangeException(nameof(fragmentation), fragmentation, null);
        }
    }
}
