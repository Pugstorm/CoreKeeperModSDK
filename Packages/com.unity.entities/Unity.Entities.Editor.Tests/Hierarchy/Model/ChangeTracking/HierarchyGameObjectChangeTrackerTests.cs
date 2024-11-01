using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.NotBurstCompatible;
using Unity.Editor.Bridge;
using Unity.Jobs;

namespace Unity.Entities.Editor.Tests
{
    sealed class HierarchyGameObjectChangeTrackerTests
    {
        [Test]
        public void HierarchyGameObjectChangeTracker_EventQueueFiltering()
        {
            using var l = new NativeList<GameObjectChangeTrackerEvent>(Allocator.TempJob);
            var eventsToAdd = new NativeArray<GameObjectChangeTrackerEvent>(1, Allocator.TempJob);
            var eventsIndex = new NativeParallelHashMap<int, int>(1, Allocator.TempJob);
            try
            {
                eventsToAdd[0] = new GameObjectChangeTrackerEvent(1, GameObjectChangeTrackerEventType.CreatedOrChanged);
                new HierarchyGameObjectChangeTracker.MergeEventsJob { Events = l, EventsToAdd = eventsToAdd, EventsIndex = eventsIndex }.Run();
                eventsToAdd[0] = new GameObjectChangeTrackerEvent(2, GameObjectChangeTrackerEventType.CreatedOrChanged);
                new HierarchyGameObjectChangeTracker.MergeEventsJob { Events = l, EventsToAdd = eventsToAdd, EventsIndex = eventsIndex }.Run();
                eventsToAdd[0] = new GameObjectChangeTrackerEvent(2, GameObjectChangeTrackerEventType.OrderChanged);
                new HierarchyGameObjectChangeTracker.MergeEventsJob { Events = l, EventsToAdd = eventsToAdd, EventsIndex = eventsIndex }.Run();
                eventsToAdd[0] = new GameObjectChangeTrackerEvent(2, GameObjectChangeTrackerEventType.CreatedOrChanged);
                new HierarchyGameObjectChangeTracker.MergeEventsJob { Events = l, EventsToAdd = eventsToAdd, EventsIndex = eventsIndex }.Run();
                eventsToAdd[0] = new GameObjectChangeTrackerEvent(1, GameObjectChangeTrackerEventType.Destroyed);
                new HierarchyGameObjectChangeTracker.MergeEventsJob { Events = l, EventsToAdd = eventsToAdd, EventsIndex = eventsIndex }.Run();

                Assert.That(l.ToArrayNBC(), Is.EquivalentTo(new[]
                {
                    new GameObjectChangeTrackerEvent(1, GameObjectChangeTrackerEventType.CreatedOrChanged | GameObjectChangeTrackerEventType.Destroyed),
                    new GameObjectChangeTrackerEvent(2, GameObjectChangeTrackerEventType.CreatedOrChanged | GameObjectChangeTrackerEventType.OrderChanged)
                }));
            }
            finally
            {
                eventsToAdd.Dispose();
                eventsIndex.Dispose();
            }
        }

    }
}
