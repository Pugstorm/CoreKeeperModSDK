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
    public class GhostRelevancyTestConverter : TestNetCodeAuthoring.IConverter
    {
        public void Bake(GameObject gameObject, IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent(entity, new GhostOwner());
        }
    }

    [DisableAutoCreation]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    internal partial class AutoMarkIrrelevantSystem : SystemBase
    {
        public int ConnectionId;
        public NativeHashSet<int> IrrelevantGhosts;
        protected override void OnCreate()
        {
            IrrelevantGhosts = new NativeHashSet<int>(100, Allocator.TempJob);
        }

        protected override void OnDestroy()
        {
            IrrelevantGhosts.Dispose();
        }

        protected override void OnUpdate()
        {
            ref var ghostRelevancy = ref SystemAPI.GetSingletonRW<GhostRelevancy>().ValueRW;
            var relevancySet = ghostRelevancy.GhostRelevancySet;
            var clearDep = Job.WithCode(() => {
                relevancySet.Clear();
            }).Schedule(Dependency);
            Dependency = JobHandle.CombineDependencies(clearDep, Dependency);
            var connectionId = ConnectionId;
            var irrelevantGhosts = IrrelevantGhosts;
            Entities.ForEach((in GhostInstance ghost, in GhostOwner owner) => {
                if (irrelevantGhosts.Contains(owner.NetworkId))
                    relevancySet.TryAdd(new RelevantGhostForConnection(connectionId, ghost.ghostId), 1);
            }).Schedule();
        }
    }

    public class RelevancyTests
    {
        const float frameTime = 1.0f / 60.0f;
        GameObject bootstrapAndSetup(NetCodeTestWorld testWorld, System.Type additionalSystem = null)
        {
            if (additionalSystem != null)
                testWorld.Bootstrap(true, additionalSystem);
            else
                testWorld.Bootstrap(true);

            var ghostGameObject = new GameObject();
            ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new GhostRelevancyTestConverter();
            var ghostConfig = ghostGameObject.AddComponent<GhostAuthoringComponent>();

            Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

            testWorld.CreateWorlds(true, 1);
            return ghostGameObject;
        }
        Entity spawnAndSetId(NetCodeTestWorld testWorld, GameObject ghostGameObject, int id)
        {
            var serverEnt = testWorld.SpawnOnServer(ghostGameObject);
            Assert.AreNotEqual(Entity.Null, serverEnt);
            testWorld.ServerWorld.EntityManager.SetComponentData(serverEnt, new GhostOwner{NetworkId = id});
            return serverEnt;
        }

        int connectAndGoInGame(NetCodeTestWorld testWorld, int maxFrames = 4)
        {
            // Connect and make sure the connection could be established
            Assert.IsTrue(testWorld.Connect(frameTime, maxFrames));

            // Go in-game
            testWorld.GoInGame();

            var con = testWorld.TryGetSingletonEntity<NetworkId>(testWorld.ServerWorld);
            Assert.AreNotEqual(Entity.Null, con);
            return testWorld.ServerWorld.EntityManager.GetComponentData<NetworkId>(con).Value;
        }
        [Test]
        public void EmptyIsRelevantSetSendsNoGhosts()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                var ghostGameObject = bootstrapAndSetup(testWorld);

                ref var ghostRelevancy = ref testWorld.GetSingletonRW<GhostRelevancy>(testWorld.ServerWorld).ValueRW;
                ghostRelevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsRelevant;

                spawnAndSetId(testWorld, ghostGameObject, 1);

                connectAndGoInGame(testWorld);

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                // Check that the client world has the right thing and value
                var clientEnt = testWorld.TryGetSingletonEntity<GhostOwner>(testWorld.ClientWorlds[0]);
                Assert.AreEqual(Entity.Null, clientEnt);
            }
        }
        [Test]
        public void FullIsRelevantSetSendsAllGhosts()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                var ghostGameObject = bootstrapAndSetup(testWorld);

                ref var ghostRelevancy = ref testWorld.GetSingletonRW<GhostRelevancy>(testWorld.ServerWorld).ValueRW;
                ghostRelevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsRelevant;

                var serverConnectionId = connectAndGoInGame(testWorld);

                var serverEnt = spawnAndSetId(testWorld, ghostGameObject, 1);
                testWorld.Tick(frameTime);
                var serverGhostId = testWorld.ServerWorld.EntityManager.GetComponentData<GhostInstance>(serverEnt).ghostId;
                ghostRelevancy.GhostRelevancySet.TryAdd(new RelevantGhostForConnection(serverConnectionId, serverGhostId), 1);

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                // Check that the client world has the right thing and value
                var clientEnt = testWorld.TryGetSingletonEntity<GhostOwner>(testWorld.ClientWorlds[0]);
                Assert.AreNotEqual(Entity.Null, clientEnt);
                Assert.AreEqual(1, testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostOwner>(clientEnt).NetworkId);
            }
        }
        [Test]
        public void HalfIsRelevantSetSendsHalfGhosts()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                var ghostGameObject = bootstrapAndSetup(testWorld);

                ref var ghostRelevancy = ref testWorld.GetSingletonRW<GhostRelevancy>(testWorld.ServerWorld).ValueRW;
                ghostRelevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsRelevant;

                var serverConnectionId = connectAndGoInGame(testWorld);

                var serverEnt = spawnAndSetId(testWorld, ghostGameObject, 1);
                testWorld.Tick(frameTime);
                var serverGhostId = testWorld.ServerWorld.EntityManager.GetComponentData<GhostInstance>(serverEnt).ghostId;
                ghostRelevancy.GhostRelevancySet.TryAdd(new RelevantGhostForConnection(serverConnectionId, serverGhostId), 1);
                spawnAndSetId(testWorld, ghostGameObject, 2);

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                // Check that the client world has the right thing and value
                var clientEnt = testWorld.TryGetSingletonEntity<GhostOwner>(testWorld.ClientWorlds[0]);
                Assert.AreNotEqual(Entity.Null, clientEnt);
                Assert.AreEqual(1, testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostOwner>(clientEnt).NetworkId);
            }
        }
        [Test]
        public void EmptyIsIrrelevantSetSendsAllGhosts()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                var ghostGameObject = bootstrapAndSetup(testWorld);

                ref var ghostRelevancy = ref testWorld.GetSingletonRW<GhostRelevancy>(testWorld.ServerWorld).ValueRW;
                ghostRelevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsIrrelevant;

                spawnAndSetId(testWorld, ghostGameObject, 1);

                connectAndGoInGame(testWorld);

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                // Check that the client world has the right thing and value
                var clientEnt = testWorld.TryGetSingletonEntity<GhostOwner>(testWorld.ClientWorlds[0]);
                Assert.AreNotEqual(Entity.Null, clientEnt);
                Assert.AreEqual(1, testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostOwner>(clientEnt).NetworkId);
            }
        }
        [Test]
        public void FullIsIrrelevantSetSendsNoGhosts()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                var ghostGameObject = bootstrapAndSetup(testWorld);

                ref var ghostRelevancy = ref testWorld.GetSingletonRW<GhostRelevancy>(testWorld.ServerWorld).ValueRW;
                ghostRelevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsIrrelevant;

                var serverConnectionId = connectAndGoInGame(testWorld);

                var serverEnt = spawnAndSetId(testWorld, ghostGameObject, 1);
                testWorld.Tick(frameTime);
                var serverGhostId = testWorld.ServerWorld.EntityManager.GetComponentData<GhostInstance>(serverEnt).ghostId;
                ghostRelevancy.GhostRelevancySet.TryAdd(new RelevantGhostForConnection(serverConnectionId, serverGhostId), 1);

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                // Check that the client world has the right thing and value
                var clientEnt = testWorld.TryGetSingletonEntity<GhostOwner>(testWorld.ClientWorlds[0]);
                Assert.AreEqual(Entity.Null, clientEnt);
            }
        }
        [Test]
        public void HalfIsIrrelevantSetSendsHalfGhosts()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                var ghostGameObject = bootstrapAndSetup(testWorld);

                ref var ghostRelevancy = ref testWorld.GetSingletonRW<GhostRelevancy>(testWorld.ServerWorld).ValueRW;
                ghostRelevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsIrrelevant;

                var serverConnectionId = connectAndGoInGame(testWorld);

                var serverEnt = spawnAndSetId(testWorld, ghostGameObject, 1);
                testWorld.Tick(frameTime);
                var serverGhostId = testWorld.ServerWorld.EntityManager.GetComponentData<GhostInstance>(serverEnt).ghostId;
                ghostRelevancy.GhostRelevancySet.TryAdd(new RelevantGhostForConnection(serverConnectionId, serverGhostId), 1);
                spawnAndSetId(testWorld, ghostGameObject, 2);

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                // Check that the client world has the right thing and value
                var clientEnt = testWorld.TryGetSingletonEntity<GhostOwner>(testWorld.ClientWorlds[0]);
                Assert.AreNotEqual(Entity.Null, clientEnt);
                Assert.AreEqual(2, testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostOwner>(clientEnt).NetworkId);
            }
        }
        [Test]
        public void MarkedIrrelevantAtSpawnIsNeverSeen()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                var ghostGameObject = bootstrapAndSetup(testWorld, typeof(AutoMarkIrrelevantSystem));

                ref var ghostRelevancy = ref testWorld.GetSingletonRW<GhostRelevancy>(testWorld.ServerWorld).ValueRW;
                ghostRelevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsIrrelevant;

                var serverConnectionId = connectAndGoInGame(testWorld);
                testWorld.ServerWorld.GetExistingSystemManaged<AutoMarkIrrelevantSystem>().ConnectionId = serverConnectionId;

                for (int ghost = 0; ghost < 128; ++ghost)
                {
                    spawnAndSetId(testWorld, ghostGameObject, 2);
                }

                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                var serverEnt = spawnAndSetId(testWorld, ghostGameObject, 1);
                testWorld.ServerWorld.GetExistingSystemManaged<AutoMarkIrrelevantSystem>().IrrelevantGhosts.Add(1);

                using var query = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostOwner>());
                for (int i = 0; i < 16; ++i)
                {
                    var clientValues = query.ToComponentDataArray<GhostOwner>(Allocator.Temp);
                    Assert.AreEqual(128, clientValues.Length);
                    for (int ghost = 0; ghost < clientValues.Length; ++ghost)
                        Assert.AreEqual(2, clientValues[ghost].NetworkId);

                    testWorld.Tick(frameTime);
                }
            }
        }
        [Test]
        public void MarkedIrrelevantIsDespawned()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                var ghostGameObject = bootstrapAndSetup(testWorld, typeof(AutoMarkIrrelevantSystem));

                ref var ghostRelevancy = ref testWorld.GetSingletonRW<GhostRelevancy>(testWorld.ServerWorld).ValueRW;
                ghostRelevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsIrrelevant;

                var serverConnectionId = connectAndGoInGame(testWorld);
                var autoMarkIrrelevantSystem = testWorld.ServerWorld.GetExistingSystemManaged<AutoMarkIrrelevantSystem>();
                autoMarkIrrelevantSystem.ConnectionId = serverConnectionId;

                for (int ghost = 0; ghost < 128; ++ghost)
                {
                    spawnAndSetId(testWorld, ghostGameObject, 2);
                }
                var serverEnt = spawnAndSetId(testWorld, ghostGameObject, 1);

                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                using var query = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostOwner>());
                var clientValues = query.ToComponentDataArray<GhostOwner>(Allocator.Temp);
                Assert.AreEqual(129, clientValues.Length);
                bool foundOne = false;
                for (int ghost = 0; ghost < clientValues.Length; ++ghost)
                {
                    if (!foundOne && clientValues[ghost].NetworkId == 1)
                        foundOne = true;
                    else
                        Assert.AreEqual(2, clientValues[ghost].NetworkId);
                }
                Assert.IsTrue(foundOne);

                testWorld.ServerWorld.GetExistingSystemManaged<AutoMarkIrrelevantSystem>().IrrelevantGhosts.Add(1);

                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                clientValues = query.ToComponentDataArray<GhostOwner>(Allocator.Temp);
                Assert.AreEqual(128, clientValues.Length);
                for (int ghost = 0; ghost < clientValues.Length; ++ghost)
                    Assert.AreEqual(2, clientValues[ghost].NetworkId);
            }
        }
        void checkValidSet(HashSet<int> checkHashSet, NativeArray<GhostOwner> clientValues, int start, int end)
        {
            checkHashSet.Clear();
            Assert.AreEqual(end-start, clientValues.Length);
            for (int ghost = 0; ghost < clientValues.Length; ++ghost)
            {
                var id = clientValues[ghost].NetworkId;
                Assert.IsTrue(id > start && id <= end);
                Assert.IsFalse(checkHashSet.Contains(id));
                checkHashSet.Add(id);
            }
        }
        [Test]
        [TestCase(16)]
        [TestCase(23)]
        public void MarkIrrelevantAtRuntimeReachTheClient(int ghostsPerFrame)
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                var ghostGameObject = bootstrapAndSetup(testWorld, typeof(AutoMarkIrrelevantSystem));

                ref var ghostRelevancy = ref testWorld.GetSingletonRW<GhostRelevancy>(testWorld.ServerWorld).ValueRW;
                ghostRelevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsIrrelevant;

                var serverConnectionId = connectAndGoInGame(testWorld);
                testWorld.ServerWorld.GetExistingSystemManaged<AutoMarkIrrelevantSystem>().ConnectionId = serverConnectionId;

                for (int ghost = 0; ghost < 128; ++ghost)
                {
                    spawnAndSetId(testWorld, ghostGameObject, ghost+1);
                }

                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                using var query = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostOwner>());

                var checkHashSet = new HashSet<int>();
                var clientValues = query.ToComponentDataArray<GhostOwner>(Allocator.Temp);
                checkValidSet(checkHashSet, clientValues, 0, 128);

                // For every update we make ghostsPerFrame new ghosts irrelevant and check that the change was propagated
                for (int start = 0; start+ghostsPerFrame < 128; start += ghostsPerFrame)
                {
                    var autoMarkIrrelevantSystem = testWorld.ServerWorld.GetExistingSystemManaged<AutoMarkIrrelevantSystem>();
                    for (int i = 0; i < ghostsPerFrame; ++i)
                        autoMarkIrrelevantSystem.IrrelevantGhosts.Add(start + i + 1);

                    for (int i = 0; i < 6; ++i)
                        testWorld.Tick(frameTime);

                    clientValues = query.ToComponentDataArray<GhostOwner>(Allocator.Temp);
                    checkValidSet(checkHashSet, clientValues, start+ghostsPerFrame, 128);
                }
            }
        }
        [Test]
        [TestCase(16)]
        [TestCase(23)]
        public void MarkRelevantAtRuntimeReachTheClient(int ghostsPerFrame)
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                var ghostGameObject = bootstrapAndSetup(testWorld, typeof(AutoMarkIrrelevantSystem));

                ref var ghostRelevancy = ref testWorld.GetSingletonRW<GhostRelevancy>(testWorld.ServerWorld).ValueRW;
                ghostRelevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsIrrelevant;

                var serverConnectionId = connectAndGoInGame(testWorld);
                var autoMarkIrrelevantSystem = testWorld.ServerWorld.GetExistingSystemManaged<AutoMarkIrrelevantSystem>();
                autoMarkIrrelevantSystem.ConnectionId = serverConnectionId;

                for (int ghost = 0; ghost < 128; ++ghost)
                {
                    spawnAndSetId(testWorld, ghostGameObject, ghost+1);
                    autoMarkIrrelevantSystem.IrrelevantGhosts.Add(ghost+1);
                }

                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                using var query = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostOwner>());

                var checkHashSet = new HashSet<int>();
                var clientValues = query.ToComponentDataArray<GhostOwner>(Allocator.Temp);
                Assert.AreEqual(0, clientValues.Length);

                // For every update we make ghostsPerFrame new ghosts relevant and check that the change was propagated
                for (int start = 0; start+ghostsPerFrame < 128; start += ghostsPerFrame)
                {
                    // Complete the dependency
                    testWorld.ServerWorld.EntityManager.GetComponentData<GhostRelevancy>(testWorld.TryGetSingletonEntity<GhostRelevancy>(testWorld.ServerWorld));
                    for (int i = 0; i < ghostsPerFrame; ++i)
                        autoMarkIrrelevantSystem.IrrelevantGhosts.Remove(start+i+1);
                    for (int i = 0; i < 4; ++i)
                        testWorld.Tick(frameTime);

                    clientValues = query.ToComponentDataArray<GhostOwner>(Allocator.Temp);
                    checkValidSet(checkHashSet, clientValues, 0, start+ghostsPerFrame);
                }
            }
        }
        [Test]
        [TestCase(16)]
        [TestCase(23)]
        public void ChangeRelevantSetAtRuntimeReachTheClient(int ghostsPerFrame)
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                var ghostGameObject = bootstrapAndSetup(testWorld, typeof(AutoMarkIrrelevantSystem));

                ref var ghostRelevancy = ref testWorld.GetSingletonRW<GhostRelevancy>(testWorld.ServerWorld).ValueRW;
                ghostRelevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsIrrelevant;

                var serverConnectionId = connectAndGoInGame(testWorld);
                var autoMarkIrrelevantSystem = testWorld.ServerWorld.GetExistingSystemManaged<AutoMarkIrrelevantSystem>();
                autoMarkIrrelevantSystem.ConnectionId = serverConnectionId;

                // The relevant set is 3x the changes per frame, this means 1/3 is added, 1/3 is removed and 1/3 remains relevant
                int end = ghostsPerFrame*3;
                for (int ghost = 0; ghost < 128; ++ghost)
                {
                    spawnAndSetId(testWorld, ghostGameObject, ghost+1);
                    if (ghost >= end)
                        autoMarkIrrelevantSystem.IrrelevantGhosts.Add(ghost+1);
                }

                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                using var query = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostOwner>());

                var checkHashSet = new HashSet<int>();
                var clientValues = query.ToComponentDataArray<GhostOwner>(Allocator.Temp);
                checkValidSet(checkHashSet, clientValues, 0, end);

                // For every update we make ghostsPerFrame new ghosts relevant and check that the change was propagated
                for (int start = 0; end+ghostsPerFrame < 128; start += ghostsPerFrame, end += ghostsPerFrame)
                {
                    for (int i = 0; i < ghostsPerFrame; ++i)
                    {
                        autoMarkIrrelevantSystem.IrrelevantGhosts.Add(start+i+1);
                        autoMarkIrrelevantSystem.IrrelevantGhosts.Remove(end+i+1);
                    }
                    for (int i = 0; i < 6; ++i)
                        testWorld.Tick(frameTime);

                    clientValues = query.ToComponentDataArray<GhostOwner>(Allocator.Temp);
                    checkValidSet(checkHashSet, clientValues, start+ghostsPerFrame, end+ghostsPerFrame);
                }
            }
        }
        [Test]
        public void ToggleEveryFrameDoesNotRepetedlySpawn()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.DriverSimulatedDelay = 10;
                var ghostGameObject = bootstrapAndSetup(testWorld, typeof(AutoMarkIrrelevantSystem));

                ref var ghostRelevancy = ref testWorld.GetSingletonRW<GhostRelevancy>(testWorld.ServerWorld).ValueRW;
                ghostRelevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsIrrelevant;

                var serverConnectionId = connectAndGoInGame(testWorld, 16);
                var autoMarkIrrelevantSystem = testWorld.ServerWorld.GetExistingSystemManaged<AutoMarkIrrelevantSystem>();
                autoMarkIrrelevantSystem.ConnectionId = serverConnectionId;

                for (int ghost = 0; ghost < 128; ++ghost)
                {
                    spawnAndSetId(testWorld, ghostGameObject, 2);
                }
                spawnAndSetId(testWorld, ghostGameObject, 1);
                // Start with the ghost irrelevant
                autoMarkIrrelevantSystem.IrrelevantGhosts.Add(1);

                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                using var query = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostOwner>());
                var clientValues = query.ToComponentDataArray<GhostOwner>(Allocator.Temp);
                // Check that the ghost does not exist
                Assert.AreEqual(128, clientValues.Length);
                for (int ghost = 0; ghost < clientValues.Length; ++ghost)
                    Assert.AreEqual(2, clientValues[ghost].NetworkId);


                int sawGhost = 0;
                bool foundOne;
                // Loop unevent number of times so the ghost ends as relevant
                for (int i = 0; i < 63; ++i)
                {
                    clientValues = query.ToComponentDataArray<GhostOwner>(Allocator.Temp);
                    if (clientValues.Length == 128)
                    {
                        Assert.AreEqual(128, clientValues.Length);
                        for (int ghost = 0; ghost < clientValues.Length; ++ghost)
                            Assert.AreEqual(2, clientValues[ghost].NetworkId);
                    }
                    else
                    {
                        Assert.AreEqual(129, clientValues.Length);

                        foundOne = false;
                        for (int ghost = 0; ghost < clientValues.Length; ++ghost)
                        {
                            if (!foundOne && clientValues[ghost].NetworkId == 1)
                                foundOne = true;
                            else
                                Assert.AreEqual(2, clientValues[ghost].NetworkId);
                        }
                        Assert.IsTrue(foundOne);
                        ++sawGhost;
                    }

                    // Toggle the host between relevant and not relevant every frame
                    if ((i&1) == 0)
                        autoMarkIrrelevantSystem.IrrelevantGhosts.Remove(1);
                    else
                        autoMarkIrrelevantSystem.IrrelevantGhosts.Add(1);
                    testWorld.Tick(frameTime);
                }
                // The ghost should have been relevant less than half the frames, since some spawns were skipped to to a pending despawn
                Assert.Less(sawGhost, 32);

                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);
                // Check that it ended up relevant after toggling for many frames since it ended on relevant
                clientValues = query.ToComponentDataArray<GhostOwner>(Allocator.Temp);
                foundOne = false;
                for (int ghost = 0; ghost < clientValues.Length; ++ghost)
                {
                    if (!foundOne && clientValues[ghost].NetworkId == 1)
                        foundOne = true;
                    else
                        Assert.AreEqual(2, clientValues[ghost].NetworkId);
                }
                Assert.IsTrue(foundOne);
            }
        }
        [Test]
        public void ManyEntitiesCanBecomeIrrelevantSameTick()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new GhostValueSerializerConverter();

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);

                var prefabCollection = testWorld.TryGetSingletonEntity<NetCodeTestPrefabCollection>(testWorld.ServerWorld);
                var prefab = testWorld.ServerWorld.EntityManager.GetBuffer<NetCodeTestPrefab>(prefabCollection)[0].Value;
                using (var entities = testWorld.ServerWorld.EntityManager.Instantiate(prefab, 10000, Allocator.Persistent))
                {
                    float frameTime = 1.0f / 60.0f;
                    // Connect and make sure the connection could be established
                    Assert.IsTrue(testWorld.Connect(frameTime, 4));

                    // Go in-game
                    testWorld.GoInGame();

                    // Let the game run for a bit so the ghosts are spawned on the client
                    for (int i = 0; i < 128; ++i)
                        testWorld.Tick(frameTime);

                    var ghostCount = testWorld.GetSingleton<GhostCount>(testWorld.ClientWorlds[0]);
                    Assert.AreEqual(10000, ghostCount.GhostCountOnClient);

                    // Make all 10 000 ghosts irrelevant
                    ref var ghostRelevancy = ref testWorld.GetSingletonRW<GhostRelevancy>(testWorld.ServerWorld).ValueRW;
                    ghostRelevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsRelevant;

                    for (int i = 0; i < 256; ++i)
                        testWorld.Tick(frameTime);

                    // Assert that replicated version is correct
                    Assert.AreEqual(0, ghostCount.GhostCountOnClient);

                    testWorld.ServerWorld.EntityManager.DestroyEntity(entities);

                    for (int i = 0; i < 128; ++i)
                        testWorld.Tick(frameTime);

                    // Assert that replicated version is correct
                    Assert.AreEqual(0, ghostCount.GhostCountOnClient);
                }
            }
        }
    }
}
