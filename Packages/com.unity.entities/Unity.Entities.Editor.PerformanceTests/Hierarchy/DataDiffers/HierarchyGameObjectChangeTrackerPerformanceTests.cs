using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Editor.Bridge;
using Unity.Jobs;
using Unity.PerformanceTesting;

namespace Unity.Entities.Editor.PerformanceTests
{
    [TestFixture]
    [Category(Categories.Performance)]
    class HierarchyGameObjectChangeTrackerPerformanceTests
    {
        const int k_MinId = -4_352_596;

        static int[] GenerateIds(int count) => Enumerable.Range(0, count).Select(i => k_MinId + i * 50_000).ToArray();

        [Test, Performance]
        public void HierarchyGameObjectChangeTracker_MergeEvents_AddNewEvents([Values(0, 10_000, 100_000)] int existingEventCount, [Values(500, 4000, 10_000, 50_000)] int eventsToAddCount)
        {
            NativeList<GameObjectChangeTrackerEvent> events = default;
            NativeArray<GameObjectChangeTrackerEvent> eventsToAdd = default;
            NativeParallelHashMap<int, int> eventsIndex = default;

            var ids = GenerateIds(existingEventCount + eventsToAddCount);

            Measure.Method(() =>
            {
                var mergeJob = new HierarchyGameObjectChangeTracker.MergeEventsJob { Events = events, EventsToAdd = eventsToAdd, EventsIndex = eventsIndex };
                mergeJob.Run();
            })
            .SetUp(() =>
            {
                events = new NativeList<GameObjectChangeTrackerEvent>(existingEventCount, Allocator.TempJob);
                eventsToAdd = new NativeArray<GameObjectChangeTrackerEvent>(eventsToAddCount, Allocator.TempJob);
                eventsIndex = new NativeParallelHashMap<int, int>(existingEventCount, Allocator.TempJob);

                for (var i = 0; i < existingEventCount; i++)
                {
                    eventsIndex.Add(ids[i], events.Length);
                    events.Add(new GameObjectChangeTrackerEvent(ids[i], GameObjectChangeTrackerEventType.CreatedOrChanged));
                }

                for (var i = 0; i < eventsToAddCount; i++)
                {
                    eventsToAdd[i] = new GameObjectChangeTrackerEvent(ids[existingEventCount + i], GameObjectChangeTrackerEventType.CreatedOrChanged);
                }
            })
            .CleanUp(() =>
            {
                eventsIndex.Dispose();
                events.Dispose();
                eventsToAdd.Dispose();
            })
            .WarmupCount(5)
            .MeasurementCount(50)
            .Run();
        }

        [Test, Performance]
        public void HierarchyGameObjectChangeTracker_MergeEvents_AddExistingEvents([Values(10_000, 100_000)] int existingEventCount, [Values(500, 4000)] int eventsToAddCount)
        {
            NativeList<GameObjectChangeTrackerEvent> events = default;
            NativeArray<GameObjectChangeTrackerEvent> eventsToAdd = default;
            NativeParallelHashMap<int, int> eventsIndex = default;

            var ids = GenerateIds(existingEventCount);

            Measure.Method(() =>
            {
                var mergeJob = new HierarchyGameObjectChangeTracker.MergeEventsJob { Events = events, EventsToAdd = eventsToAdd, EventsIndex = eventsIndex };
                mergeJob.Run();
            })
            .SetUp(() =>
            {
                events = new NativeList<GameObjectChangeTrackerEvent>(existingEventCount, Allocator.TempJob);
                eventsToAdd = new NativeArray<GameObjectChangeTrackerEvent>(eventsToAddCount, Allocator.TempJob);
                eventsIndex = new NativeParallelHashMap<int, int>(existingEventCount, Allocator.TempJob);

                for (var i = 0; i < existingEventCount; i++)
                {
                    eventsIndex.Add(ids[i], events.Length);
                    events.Add(new GameObjectChangeTrackerEvent(ids[i], GameObjectChangeTrackerEventType.CreatedOrChanged));
                }

                for (var i = 0; i < eventsToAddCount; i++)
                {
                    eventsToAdd[i] = new GameObjectChangeTrackerEvent(ids[i], GameObjectChangeTrackerEventType.OrderChanged);
                }
            })
            .CleanUp(() =>
            {
                eventsIndex.Dispose();
                events.Dispose();
                eventsToAdd.Dispose();
            })
            .WarmupCount(5)
            .MeasurementCount(50)
            .Run();
        }

        [Test, Performance]
        public void HierarchyGameObjectChangeTracker_MergeEvents_AddDuplicateEvents([Values(10_000, 100_000)] int existingEventCount, [Values(500, 4000)] int eventsToAddCount)
        {
            NativeList<GameObjectChangeTrackerEvent> events = default;
            NativeArray<GameObjectChangeTrackerEvent> eventsToAdd = default;
            NativeParallelHashMap<int, int> eventsIndex = default;

            var ids = GenerateIds(existingEventCount);

            Measure.Method(() =>
            {
                var mergeJob = new HierarchyGameObjectChangeTracker.MergeEventsJob { Events = events, EventsToAdd = eventsToAdd, EventsIndex = eventsIndex };
                mergeJob.Run();
            })
            .SetUp(() =>
            {
                events = new NativeList<GameObjectChangeTrackerEvent>(existingEventCount, Allocator.TempJob);
                eventsToAdd = new NativeArray<GameObjectChangeTrackerEvent>(eventsToAddCount, Allocator.TempJob);
                eventsIndex = new NativeParallelHashMap<int, int>(existingEventCount, Allocator.TempJob);

                for (var i = 0; i < existingEventCount; i++)
                {
                    eventsIndex.Add(ids[i], events.Length);
                    events.Add(new GameObjectChangeTrackerEvent(ids[i], GameObjectChangeTrackerEventType.CreatedOrChanged));
                }

                for (var i = 0; i < eventsToAddCount; i++)
                {
                    eventsToAdd[i] = new GameObjectChangeTrackerEvent(ids[i], GameObjectChangeTrackerEventType.CreatedOrChanged);
                }
            })
            .CleanUp(() =>
            {
                eventsIndex.Dispose();
                events.Dispose();
                eventsToAdd.Dispose();
            })
            .WarmupCount(5)
            .MeasurementCount(50)
            .Run();
        }

        [Test, Performance]
        public void HierarchyGameObjectChangeTracker_MergeEvents_AddNewEvents_Continuously(
            [Values(10_000, 100_000)] int existingEventCount,
            [Values(500, 4000)] int eventsToAddCount)
        {
            const int repeats = 25;

            NativeList<GameObjectChangeTrackerEvent> events = default;
            NativeArray<GameObjectChangeTrackerEvent> eventsToAdd = default;
            NativeArray<GameObjectChangeTrackerEvent> eventsBatchToAdd = default;
            NativeParallelHashMap<int, int> eventsIndex = default;

            var ids = GenerateIds(existingEventCount + eventsToAddCount * repeats);

            Measure.Method(() =>
            {
                for (var i = 0; i < repeats; i++)
                {
                    NativeArray<GameObjectChangeTrackerEvent>.Copy(eventsToAdd, i * eventsToAddCount, eventsBatchToAdd, 0, eventsToAddCount);
                    new HierarchyGameObjectChangeTracker.MergeEventsJob
                    {
                        Events = events,
                        EventsToAdd = eventsBatchToAdd,
                        EventsIndex = eventsIndex
                    }.Run();
                }
            })
            .SetUp(() =>
            {
                events = new NativeList<GameObjectChangeTrackerEvent>(existingEventCount, Allocator.TempJob);
                eventsToAdd = new NativeArray<GameObjectChangeTrackerEvent>(eventsToAddCount * repeats, Allocator.TempJob);
                eventsBatchToAdd = new NativeArray<GameObjectChangeTrackerEvent>(eventsToAddCount, Allocator.TempJob);
                eventsIndex = new NativeParallelHashMap<int, int>(existingEventCount, Allocator.TempJob);

                for (var i = 0; i < existingEventCount; i++)
                {
                    eventsIndex.Add(ids[i], events.Length);
                    events.Add(new GameObjectChangeTrackerEvent(ids[i], GameObjectChangeTrackerEventType.CreatedOrChanged));
                }

                for (var i = 0; i < eventsToAddCount * repeats; i++)
                {
                    eventsToAdd[i] = new GameObjectChangeTrackerEvent( ids[existingEventCount + i], GameObjectChangeTrackerEventType.CreatedOrChanged);
                }
            })
            .CleanUp(() =>
            {
                eventsIndex.Dispose();
                events.Dispose();
                eventsToAdd.Dispose();
                eventsBatchToAdd.Dispose();
            })
            .WarmupCount(5)
            .MeasurementCount(50)
            .Run();
        }
    }
}
