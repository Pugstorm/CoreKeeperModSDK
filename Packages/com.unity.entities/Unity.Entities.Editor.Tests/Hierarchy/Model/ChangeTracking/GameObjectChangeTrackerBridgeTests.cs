using System;
using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.NotBurstCompatible;
using Unity.Editor.Bridge;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Entities.Editor.Tests
{
    sealed class GameObjectChangeTrackerBridgeTests
    {
        [UnityTest]
        public IEnumerator ShouldReceiveEvents()
        {
            var receivedEvents = new NativeList<GameObjectChangeTrackerEvent>(Allocator.TempJob);

            try
            {
                GameObjectChangeTrackerBridge.GameObjectsChanged += OnGameObjectsChanged;

                var go = new GameObject();
                yield return null;
                var events = receivedEvents.ToArrayNBC();
                Assert.That(events, Is.Not.Empty);
                Assert.That(events, Does.Contain(new GameObjectChangeTrackerEvent(go.GetInstanceID(), GameObjectChangeTrackerEventType.CreatedOrChanged)));
            }
            finally
            {
                GameObjectChangeTrackerBridge.GameObjectsChanged -= OnGameObjectsChanged;
                receivedEvents.Dispose();
            }

            void OnGameObjectsChanged(in NativeArray<GameObjectChangeTrackerEvent> events)
            {
                receivedEvents.Clear();
                receivedEvents.Resize(events.Length, NativeArrayOptions.UninitializedMemory);
                events.CopyTo(receivedEvents.AsArray());
            }
        }
    }
}
