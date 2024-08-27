using NUnit.Framework;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.NetCode.Tests;
using Unity.Jobs;
using UnityEngine;
using Unity.NetCode;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

namespace Unity.NetCode.Tests
{
    public class GhostPredictedOnlyConverter : TestNetCodeAuthoring.IConverter
    {
        public void Bake(GameObject gameObject, IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent(entity, new GhostPredictedOnly());
            baker.AddComponent(entity, new GhostInterpolatedOnly());
            baker.AddComponent(entity, new GhostOwner());
        }
    }

    [GhostComponent(SendTypeOptimization = GhostSendType.OnlyPredictedClients)]
    public struct GhostPredictedOnly : IComponentData
    {
        [GhostField] public int Value;
    }
    [GhostComponent(SendTypeOptimization = GhostSendType.OnlyInterpolatedClients)]
    public struct GhostInterpolatedOnly : IComponentData
    {
        [GhostField] public int Value;
    }
    public class PartialSendTests
    {
        [Test]
        public void OwnerPredictedSendsDataToPredicted()
        {
            TestHelper(true, true);
        }
        [Test]
        public void OwnerPredictedSendsDataToInterpolated()
        {
            TestHelper(false, true);
        }
        [Test]
        public void AlwaysPredictedSendsDataToPredicted()
        {
            TestHelper(true, false);
        }
        [Test]
        public void AlwaysInterpolatedSendsDataToInterpolated()
        {
            TestHelper(false, false);
        }
        private void TestHelper(bool predicted, bool ownerPrediction)
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new GhostPredictedOnlyConverter();
                var ghostConfig = ghostGameObject.AddComponent<GhostAuthoringComponent>();
                if (ownerPrediction)
                {
                    ghostConfig.DefaultGhostMode = GhostMode.OwnerPredicted;
                }
                else
                {
                    ghostConfig.SupportedGhostModes = predicted ? GhostModeMask.Predicted : GhostModeMask.Interpolated;
                }

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);

                testWorld.SpawnOnServer(ghostGameObject);
                var serverEnt = testWorld.TryGetSingletonEntity<GhostPredictedOnly>(testWorld.ServerWorld);
                Assert.AreNotEqual(Entity.Null, serverEnt);
                testWorld.ServerWorld.EntityManager.SetComponentData(serverEnt, new GhostPredictedOnly{Value = 1});
                testWorld.ServerWorld.EntityManager.SetComponentData(serverEnt, new GhostInterpolatedOnly{Value = 1});
                testWorld.ServerWorld.EntityManager.SetComponentData(serverEnt, new GhostOwner{NetworkId = predicted ? 1 : 2});

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                // Check the clients network id
                var serverCon = testWorld.TryGetSingletonEntity<NetworkId>(testWorld.ServerWorld);
                Assert.AreNotEqual(Entity.Null, serverCon);
                Assert.AreEqual(1, testWorld.ServerWorld.EntityManager.GetComponentData<NetworkId>(serverCon).Value);

                // Go in-game
                testWorld.GoInGame();

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 64; ++i)
                    testWorld.Tick(frameTime);

                // Check that the client world has the right thing and value
                var clientEnt = testWorld.TryGetSingletonEntity<GhostOwner>(testWorld.ClientWorlds[0]);
                Assert.AreNotEqual(Entity.Null, clientEnt);
                Assert.AreEqual(predicted, testWorld.ClientWorlds[0].EntityManager.HasComponent<PredictedGhost>(clientEnt));
                Assert.AreEqual(predicted ? 0 : 1, testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostInterpolatedOnly>(clientEnt).Value);
                Assert.AreEqual(predicted ? 1 : 0, testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostPredictedOnly>(clientEnt).Value);
            }
        }
    }
}
