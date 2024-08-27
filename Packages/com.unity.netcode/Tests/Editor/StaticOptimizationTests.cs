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
using System.Collections.Generic;

namespace Unity.NetCode.Tests
{
    public class StaticOptimizationTestConverter : TestNetCodeAuthoring.IConverter
    {
        public void Bake(GameObject gameObject, IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent(entity, new GhostOwner());
        }
    }

    [DisableAutoCreation]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial class StaticOptimizationTestSystem : SystemBase
    {
        public static int s_ModifyNetworkId;
        protected override void OnUpdate()
        {
            int modifyNetworkId = s_ModifyNetworkId;
            Entities.ForEach((ref LocalTransform trans, in GhostOwner ghostOwner) => {
                if (ghostOwner.NetworkId != modifyNetworkId)
                    return;
                trans.Position.x += 1;
            }).ScheduleParallel();
        }
    }

    public class StaticOptimizationTests
    {
        const float frameTime = 1.0f / 60.0f;
        void SetupBasicTest(NetCodeTestWorld testWorld, int entitiesToSpawn = 1)
        {
            var ghostGameObject = new GameObject();
            ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new StaticOptimizationTestConverter();
            var ghostConfig = ghostGameObject.AddComponent<GhostAuthoringComponent>();
            ghostConfig.OptimizationMode = GhostOptimizationMode.Static;

            Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

            testWorld.CreateWorlds(true, 1);

            for (int i = 0; i < entitiesToSpawn; ++i)
            {
                var serverEnt = testWorld.SpawnOnServer(ghostGameObject);
                Assert.AreNotEqual(Entity.Null, serverEnt);
            }

            // Connect and make sure the connection could be established
            testWorld.Connect(frameTime);

            // Go in-game
            testWorld.GoInGame();

            // Let the game run for a bit so the ghosts are spawned on the client
            for (int i = 0; i < 16; ++i)
                testWorld.Tick(frameTime);
        }
        [Test]
        public void StaticGhostsAreNotSent()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                SetupBasicTest(testWorld, 16);

                var clientEntityManager = testWorld.ClientWorlds[0].EntityManager;
                using var clientQuery = clientEntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostOwner>());
                var clientEntities = clientQuery.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(16, clientEntities.Length);

                var lastSnapshot = new NativeArray<NetworkTick>(clientEntities.Length, Allocator.Temp);
                for (int i = 0; i < clientEntities.Length; ++i)
                {
                    var clientEnt = clientEntities[i];
                    // Store the last tick we got for this
                    var clientSnapshotBuffer = clientEntityManager.GetBuffer<SnapshotDataBuffer>(clientEnt);
                    var clientSnapshot = clientEntityManager.GetComponentData<SnapshotData>(clientEnt);
                    lastSnapshot[i] = clientSnapshot.GetLatestTick(clientSnapshotBuffer);
                }

                // Run a bit longer
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);
                // Verify that we did not get any new snapshot
                for (int i = 0; i < clientEntities.Length; ++i)
                {
                    var clientEnt = clientEntities[i];
                    // Store the last tick we got for this
                    var clientSnapshotBuffer = clientEntityManager.GetBuffer<SnapshotDataBuffer>(clientEnt);
                    var clientSnapshot = clientEntityManager.GetComponentData<SnapshotData>(clientEnt);
                    Assert.AreEqual(lastSnapshot[i], clientSnapshot.GetLatestTick(clientSnapshotBuffer));
                }
            }
        }
        [Test]
        public void GhostsCanBeStaticWhenChunksAreDirty()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                // The system will get write access to translation which will dirty the chunk, but not actually write anything
                testWorld.Bootstrap(true, typeof(StaticOptimizationTestSystem));
                StaticOptimizationTestSystem.s_ModifyNetworkId = 1;

                SetupBasicTest(testWorld, 16);

                var clientEntityManager = testWorld.ClientWorlds[0].EntityManager;
                using var clientQuery = clientEntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostOwner>());
                var clientEntities = clientQuery.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(16, clientEntities.Length);

                var lastSnapshot = new NativeArray<NetworkTick>(clientEntities.Length, Allocator.Temp);
                for (int i = 0; i < clientEntities.Length; ++i)
                {
                    var clientEnt = clientEntities[i];
                    // Store the last tick we got for this
                    var clientSnapshotBuffer = clientEntityManager.GetBuffer<SnapshotDataBuffer>(clientEnt);
                    var clientSnapshot = clientEntityManager.GetComponentData<SnapshotData>(clientEnt);
                    lastSnapshot[i] = clientSnapshot.GetLatestTick(clientSnapshotBuffer);
                }

                // Run a bit longer
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);
                // Verify that we did not get any new snapshot
                for (int i = 0; i < clientEntities.Length; ++i)
                {
                    var clientEnt = clientEntities[i];
                    // Store the last tick we got for this
                    var clientSnapshotBuffer = clientEntityManager.GetBuffer<SnapshotDataBuffer>(clientEnt);
                    var clientSnapshot = clientEntityManager.GetComponentData<SnapshotData>(clientEnt);
                    Assert.AreEqual(lastSnapshot[i], clientSnapshot.GetLatestTick(clientSnapshotBuffer));
                }
            }
        }
        [Test]
        public void StaticGhostsAreNotApplied()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                SetupBasicTest(testWorld);

                var clientEnt = testWorld.TryGetSingletonEntity<GhostOwner>(testWorld.ClientWorlds[0]);
                Assert.AreNotEqual(Entity.Null, clientEnt);

                var clientEntityManager = testWorld.ClientWorlds[0].EntityManager;

                // Write some data to a ghost field and verify that it was not touched by the ghost apply
                clientEntityManager.SetComponentData(clientEnt, new GhostOwner{NetworkId = 42});

                // Run a bit longer
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                Assert.AreEqual(42, clientEntityManager.GetComponentData<GhostOwner>(clientEnt).NetworkId);
            }
        }
        [Test]
        public void StaticGhostsAreSentWhenModified()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(StaticOptimizationTestSystem));
                StaticOptimizationTestSystem.s_ModifyNetworkId = 1;

                SetupBasicTest(testWorld);

                var clientEnt = testWorld.TryGetSingletonEntity<GhostOwner>(testWorld.ClientWorlds[0]);
                Assert.AreNotEqual(Entity.Null, clientEnt);

                var clientEntityManager = testWorld.ClientWorlds[0].EntityManager;
                // Store the last tick we got for this
                var clientSnapshotBuffer = clientEntityManager.GetBuffer<SnapshotDataBuffer>(clientEnt);
                var clientSnapshot = clientEntityManager.GetComponentData<SnapshotData>(clientEnt);
                var lastSnapshot = clientSnapshot.GetLatestTick(clientSnapshotBuffer);

                // Run a bit longer
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                // Verify taht we did not get any new snapshot
                clientSnapshotBuffer = clientEntityManager.GetBuffer<SnapshotDataBuffer>(clientEnt);
                clientSnapshot = clientEntityManager.GetComponentData<SnapshotData>(clientEnt);
                Assert.AreEqual(lastSnapshot, clientSnapshot.GetLatestTick(clientSnapshotBuffer));

                // Run one tick with modification
                StaticOptimizationTestSystem.s_ModifyNetworkId = 0;
                testWorld.Tick(frameTime);
                StaticOptimizationTestSystem.s_ModifyNetworkId = 1;

                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                // Verify taht we did not get any new snapshot
                clientSnapshotBuffer = clientEntityManager.GetBuffer<SnapshotDataBuffer>(clientEnt);
                clientSnapshot = clientEntityManager.GetComponentData<SnapshotData>(clientEnt);
                var newLastSnapshot = clientSnapshot.GetLatestTick(clientSnapshotBuffer);
                Assert.AreNotEqual(lastSnapshot, newLastSnapshot);

                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                // Verify that the snapshot stayed static at the new position
                clientSnapshotBuffer = clientEntityManager.GetBuffer<SnapshotDataBuffer>(clientEnt);
                clientSnapshot = clientEntityManager.GetComponentData<SnapshotData>(clientEnt);
                Assert.AreEqual(newLastSnapshot, clientSnapshot.GetLatestTick(clientSnapshotBuffer));
            }
        }
        [Test]
        public void DynamicGhostsInSameChunkAsStaticAreSent()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(StaticOptimizationTestSystem));
                StaticOptimizationTestSystem.s_ModifyNetworkId = 1;

                // Spawn 16 ghosts
                SetupBasicTest(testWorld, 16);
                // Set the ghost id for one of them to 1 so it is modified
                using var serverQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostOwner>());
                var serverEntities = serverQuery.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(16, serverEntities.Length);
                testWorld.ServerWorld.EntityManager.SetComponentData(serverEntities[0], new GhostOwner{NetworkId = 1});

                // Get the changes across to the client
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                var clientEntityManager = testWorld.ClientWorlds[0].EntityManager;
                using var clientQuery = clientEntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostOwner>());
                Entity clientEnt = Entity.Null;
                var clientEntities = clientQuery.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(16, clientEntities.Length);
                for (int i = 0; i < clientEntities.Length; ++i)
                {
                    if (clientEntityManager.GetComponentData<GhostOwner>(clientEntities[i] ).NetworkId == 1)
                    {
                        Assert.AreEqual(Entity.Null, clientEnt);
                        clientEnt = clientEntities[i];
                    }
                }
                Assert.AreNotEqual(Entity.Null, clientEnt);

                // Store the last tick we got for this
                var clientSnapshotBuffer = clientEntityManager.GetBuffer<SnapshotDataBuffer>(clientEnt);
                var clientSnapshot = clientEntityManager.GetComponentData<SnapshotData>(clientEnt);
                var lastSnapshot = clientSnapshot.GetLatestTick(clientSnapshotBuffer);

                // Run a bit longer
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);
                // Verify that we are getting updates for the ghost
                clientSnapshotBuffer = clientEntityManager.GetBuffer<SnapshotDataBuffer>(clientEnt);
                clientSnapshot = clientEntityManager.GetComponentData<SnapshotData>(clientEnt);
                Assert.AreNotEqual(lastSnapshot, clientSnapshot.GetLatestTick(clientSnapshotBuffer));
            }
        }
        [Test]
        public void RelevancyChangesSendsStaticGhosts()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                // Spawn 16 ghosts
                SetupBasicTest(testWorld, 16);
                // Set the ghost id for one of them to 1 so it is modified
                using var serverQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostOwner>());
                int ghostId;
                var serverEntities = serverQuery.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(16, serverEntities.Length);
                ghostId = testWorld.ServerWorld.EntityManager.GetComponentData<GhostInstance>(serverEntities[0]).ghostId;
                var con = testWorld.TryGetSingletonEntity<NetworkId>(testWorld.ServerWorld);
                Assert.AreNotEqual(Entity.Null, con);
                var connectionId = testWorld.ServerWorld.EntityManager.GetComponentData<NetworkId>(con).Value;

                // Get the changes across to the client
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                var clientEntityManager = testWorld.ClientWorlds[0].EntityManager;
                using var clientQuery = clientEntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostOwner>());
                var clientEntities = clientQuery.ToComponentDataArray<GhostOwner>(Allocator.Temp);
                Assert.AreEqual(16, clientEntities.Length);


                // Make one of the ghosts irrelevant
                ref var ghostRelevancy = ref testWorld.GetSingletonRW<GhostRelevancy>(testWorld.ServerWorld).ValueRW;
                ghostRelevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsIrrelevant;
                var key = new RelevantGhostForConnection{Connection = connectionId, Ghost = ghostId};
                ghostRelevancy.GhostRelevancySet.TryAdd(key, 1);

                // Get the changes across to the client
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                clientEntities = clientQuery.ToComponentDataArray<GhostOwner>(Allocator.Temp);
                Assert.AreEqual(15, clientEntities.Length);

                // Allow it to spawn again
                ghostRelevancy.GhostRelevancySet.Remove(key);

                // Get the changes across to the client
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                clientEntities = clientQuery.ToComponentDataArray<GhostOwner>(Allocator.Temp);
                Assert.AreEqual(16, clientEntities.Length);
            }
        }
    }
}
