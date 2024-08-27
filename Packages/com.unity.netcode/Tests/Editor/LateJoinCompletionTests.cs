using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using Unity.Collections;

namespace Unity.NetCode.Tests
{
    public class LateJoinCompletionConverter : TestNetCodeAuthoring.IConverter
    {
        public void Bake(GameObject gameObject, IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent( entity, new GhostOwner());
        }
    }
    public class LateJoinCompletionTests
    {
        [Test]
        public void ServerGhostCountIsVisibleOnClient()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new LateJoinCompletionConverter();

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);

                for (int i = 0; i < 8; ++i)
                    testWorld.SpawnOnServer(ghostGameObject);

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                // Go in-game
                testWorld.GoInGame();

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 4; ++i)
                    testWorld.Tick(frameTime);

                var ghostCount = testWorld.GetSingleton<GhostCount>(testWorld.ClientWorlds[0]);
                // Validate that the ghost was deleted on the cliet
                Assert.AreEqual(8, ghostCount.GhostCountOnServer);
                Assert.AreEqual(8, ghostCount.GhostCountOnClient);

                // Spawn a few more and verify taht the count is updated
                for (int i = 0; i < 8; ++i)
                    testWorld.SpawnOnServer(ghostGameObject);
                for (int i = 0; i < 4; ++i)
                    testWorld.Tick(frameTime);
                Assert.AreEqual(16, ghostCount.GhostCountOnServer);
                Assert.AreEqual(16, ghostCount.GhostCountOnClient);
            }
        }
        [Test]
        public void ServerGhostCountOnlyIncludesRelevantSet()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new LateJoinCompletionConverter();

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                for (int i = 0; i < 8; ++i)
                    testWorld.SpawnOnServer(ghostGameObject);

                // Go in-game
                testWorld.GoInGame();

                testWorld.Tick(frameTime);

                // Setup relevancy
                ref var ghostRelevancy = ref testWorld.GetSingletonRW<GhostRelevancy>(testWorld.ServerWorld).ValueRW;
                ghostRelevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsRelevant;
                var serverConnectionEnt = testWorld.TryGetSingletonEntity<NetworkId>(testWorld.ServerWorld);
                var serverConnectionId = testWorld.ServerWorld.EntityManager.GetComponentData<NetworkId>(serverConnectionEnt).Value;
                using var query = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<GhostInstance>());
                var ghosts = query.ToComponentDataArray<GhostInstance>(Allocator.Temp);
                Assert.AreEqual(ghosts.Length, 8);
                for (int i = 0; i < 6; ++i)
                    ghostRelevancy.GhostRelevancySet.TryAdd(new RelevantGhostForConnection{Ghost = ghosts[i].ghostId, Connection = serverConnectionId}, 1);

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 4; ++i)
                    testWorld.Tick(frameTime);

                var ghostCount = testWorld.GetSingleton<GhostCount>(testWorld.ClientWorlds[0]);
                // Validate that the ghost was deleted on the cliet
                Assert.AreEqual(6, ghostCount.GhostCountOnServer);
                Assert.AreEqual(6, ghostCount.GhostCountOnClient);

                // Spawn a few more and verify taht the count is updated
                for (int i = 0; i < 8; ++i)
                    testWorld.SpawnOnServer(ghostGameObject);
                for (int i = 0; i < 4; ++i)
                    testWorld.Tick(frameTime);
                Assert.AreEqual(6, ghostCount.GhostCountOnServer);
                Assert.AreEqual(6, ghostCount.GhostCountOnClient);
            }
        }
        [Test]
        public void ServerGhostCountDoesNotIncludeIrrelevantSet()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new LateJoinCompletionConverter();

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                for (int i = 0; i < 8; ++i)
                    testWorld.SpawnOnServer(ghostGameObject);

                // Go in-game
                testWorld.GoInGame();

                testWorld.Tick(frameTime);

                // Setup relevancy
                ref var ghostRelevancy = ref testWorld.GetSingletonRW<GhostRelevancy>(testWorld.ServerWorld).ValueRW;
                ghostRelevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsIrrelevant;
                var serverConnectionEnt = testWorld.TryGetSingletonEntity<NetworkId>(testWorld.ServerWorld);
                var serverConnectionId = testWorld.ServerWorld.EntityManager.GetComponentData<NetworkId>(serverConnectionEnt).Value;
                using var query = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<GhostInstance>());
                var ghosts = query.ToComponentDataArray<GhostInstance>(Allocator.Temp);
                Assert.AreEqual(ghosts.Length, 8);
                for (int i = 0; i < 6; ++i)
                    ghostRelevancy.GhostRelevancySet.TryAdd(new RelevantGhostForConnection{Ghost = ghosts[i].ghostId, Connection = serverConnectionId}, 1);

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 4; ++i)
                    testWorld.Tick(frameTime);

                var ghostCount = testWorld.GetSingleton<GhostCount>(testWorld.ClientWorlds[0]);
                // Validate that the ghost was deleted on the cliet
                Assert.AreEqual(2, ghostCount.GhostCountOnServer);
                Assert.AreEqual(2, ghostCount.GhostCountOnClient);

                // Spawn a few more and verify taht the count is updated
                for (int i = 0; i < 8; ++i)
                    testWorld.SpawnOnServer(ghostGameObject);
                for (int i = 0; i < 4; ++i)
                    testWorld.Tick(frameTime);
                Assert.AreEqual(10, ghostCount.GhostCountOnServer);
                Assert.AreEqual(10, ghostCount.GhostCountOnClient);
            }
        }
    }
}
