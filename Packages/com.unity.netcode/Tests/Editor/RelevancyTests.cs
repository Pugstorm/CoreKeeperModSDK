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
            testWorld.Connect(frameTime, maxFrames);

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
                    testWorld.Connect(frameTime);

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

        [Test]
        public void TestAlwaysRelevantQuery()
        {
            // basic feature test, custom components set user side in that query should always be relevant
            
            // Setup spawn
            using var testWorld = new NetCodeTestWorld();
            testWorld.Bootstrap(true);
            var ghostGameObject = new GameObject();
            ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new GhostValueSerializerConverter();
            Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));
            testWorld.CreateWorlds(true, 1);
            var prefabCollection = testWorld.TryGetSingletonEntity<NetCodeTestPrefabCollection>(testWorld.ServerWorld);
            var prefab = testWorld.ServerWorld.EntityManager.GetBuffer<NetCodeTestPrefab>(prefabCollection)[0].Value;
            var entity = testWorld.ServerWorld.EntityManager.Instantiate(prefab);

            var serverRelevancyQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(GhostRelevancy));
            var clientGhostQuery = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(GhostValueSerializer));
            var relevancy = serverRelevancyQuery.GetSingletonRW<GhostRelevancy>();
            relevancy.ValueRW.GhostRelevancyMode = GhostRelevancyMode.SetIsRelevant;
            relevancy.ValueRW.GhostRelevancySet.Clear(); // make sure the only way to get the ghost is through the query
            testWorld.Connect(frameTime);
            testWorld.GoInGame();
            for (int i = 0; i < 100; i++)
            {
                testWorld.Tick(frameTime);
            }

            // test nothing is relevant for now
            Assert.That(clientGhostQuery.IsEmpty);
            
            // test add query and check that the ghost is relevant now
            relevancy = serverRelevancyQuery.GetSingletonRW<GhostRelevancy>();
            relevancy.ValueRW.DefaultRelevancyQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(GhostValueSerializer));
            for (int i = 0; i < 4; i++)
            {
                testWorld.Tick(frameTime);
            }
            Assert.That(clientGhostQuery.CalculateEntityCount(), Is.EqualTo(1));
        }
        
        public class GhostRelevancyConverterA : TestNetCodeAuthoring.IConverter
        {
            public void Bake(GameObject gameObject, IBaker baker)
            {
                baker.DependsOn(gameObject);
                var entity = baker.GetEntity(TransformUsageFlags.None);
                baker.AddComponent(entity, new GhostRelevancyA());
            }
        }
        public class GhostRelevancyConverterB : TestNetCodeAuthoring.IConverter
        {
            public void Bake(GameObject gameObject, IBaker baker)
            {
                baker.DependsOn(gameObject);
                var entity = baker.GetEntity(TransformUsageFlags.None);
                baker.AddComponent(entity, new GhostRelevancyB());
            }
        }

        public struct GhostRelevancyA : IComponentData
        {
            [GhostField] public int Value;
        }
        public struct GhostRelevancyB : IComponentData
        {
            [GhostField] public int Value;
        }

        [Test]
        public void TestMoreComplexAlwaysRelevantQuery()
        {
            // Setup spawn
            using var testWorld = new NetCodeTestWorld();
            testWorld.Bootstrap(true);
            var ghostGameObjectPrefabA = new GameObject();
            ghostGameObjectPrefabA.AddComponent<TestNetCodeAuthoring>().Converter = new GhostRelevancyConverterA();
            var authoringA = ghostGameObjectPrefabA.AddComponent<GhostAuthoringComponent>();
            authoringA.DefaultGhostMode = GhostMode.Predicted;
            var ghostGameObjectPrefabB = new GameObject();
            ghostGameObjectPrefabB.AddComponent<TestNetCodeAuthoring>().Converter = new GhostRelevancyConverterB();
            var authoringB = ghostGameObjectPrefabB.AddComponent<GhostAuthoringComponent>();
            authoringB.DefaultGhostMode = GhostMode.Predicted;
            Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObjectPrefabA, ghostGameObjectPrefabB));

            testWorld.CreateWorlds(true, 1);
            var prefabCollection = testWorld.TryGetSingletonEntity<NetCodeTestPrefabCollection>(testWorld.ServerWorld);
            var prefabA = testWorld.ServerWorld.EntityManager.GetBuffer<NetCodeTestPrefab>(prefabCollection)[0].Value;
            var prefabB = testWorld.ServerWorld.EntityManager.GetBuffer<NetCodeTestPrefab>(prefabCollection)[1].Value;
            var ghostEntityA = testWorld.ServerWorld.EntityManager.Instantiate(prefabA);
            var ghostEntityB = testWorld.ServerWorld.EntityManager.Instantiate(prefabB);

            var serverRelevancyQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(GhostRelevancy));
            var clientGhostQueryA = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(GhostRelevancyA));
            var clientGhostQueryB = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(GhostRelevancyB));
            var relevancy = serverRelevancyQuery.GetSingletonRW<GhostRelevancy>();
            relevancy.ValueRW.GhostRelevancyMode = GhostRelevancyMode.SetIsRelevant;
            relevancy.ValueRW.GhostRelevancySet.Clear(); // make sure the only way to get the ghost is through the query
            testWorld.Connect(frameTime);
            testWorld.GoInGame();
            for (int i = 0; i < 100; i++)
            {
                testWorld.Tick(frameTime);
            }

            int tickCountForReplication = 4;
            
            // Clear for next tests
            void Clear()
            {
                relevancy = serverRelevancyQuery.GetSingletonRW<GhostRelevancy>();

                relevancy.ValueRW.GhostRelevancyMode = GhostRelevancyMode.SetIsRelevant;
                relevancy.ValueRW.DefaultRelevancyQuery = default;
                relevancy.ValueRW.GhostRelevancySet.Clear();
                for (int i = 0; i < tickCountForReplication; i++)
                {
                    testWorld.Tick(frameTime);
                }

                Assert.That(clientGhostQueryA.IsEmpty);
                Assert.That(clientGhostQueryB.IsEmpty);
            }

            // test nothing is relevant for now
            Assert.That(clientGhostQueryA.IsEmpty);
            Assert.That(clientGhostQueryB.IsEmpty);

            // test add query for A and check that the ghost is relevant now
            relevancy = serverRelevancyQuery.GetSingletonRW<GhostRelevancy>();
            relevancy.ValueRW.DefaultRelevancyQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(GhostRelevancyA));
            for (int i = 0; i < tickCountForReplication; i++)
            {
                testWorld.Tick(frameTime);
            }
            Assert.That(clientGhostQueryA.CalculateEntityCount(), Is.EqualTo(1));
            Assert.That(clientGhostQueryB.IsEmpty);

            Clear();
            
            // test add query for B and check that the ghost is relevant now
            relevancy = serverRelevancyQuery.GetSingletonRW<GhostRelevancy>();
            relevancy.ValueRW.DefaultRelevancyQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(GhostRelevancyB));
            for (int i = 0; i < tickCountForReplication; i++)
            {
                testWorld.Tick(frameTime);
            }
            Assert.That(clientGhostQueryA.IsEmpty);
            Assert.That(clientGhostQueryB.CalculateEntityCount(), Is.EqualTo(1));

            Clear();
            
            // test add query and check that both ghosts are relevant now
            relevancy = serverRelevancyQuery.GetSingletonRW<GhostRelevancy>();
            relevancy.ValueRW.DefaultRelevancyQuery = new EntityQueryBuilder(Allocator.Temp).WithAny<GhostRelevancyA, GhostRelevancyB>().Build(testWorld.ServerWorld.EntityManager);
            for (int i = 0; i < tickCountForReplication; i++)
            {
                testWorld.Tick(frameTime);
            }
            Assert.That(clientGhostQueryA.CalculateEntityCount(), Is.EqualTo(1));
            Assert.That(clientGhostQueryB.CalculateEntityCount(), Is.EqualTo(1));

            Clear();

            // test hash map is union with query
            var connection = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(NetworkId)).GetSingleton<NetworkId>();
            var ghostIDA = testWorld.ServerWorld.EntityManager.GetComponentData<GhostInstance>(ghostEntityA).ghostId;
            var ghostIDB = testWorld.ServerWorld.EntityManager.GetComponentData<GhostInstance>(ghostEntityB).ghostId;

            relevancy = serverRelevancyQuery.GetSingletonRW<GhostRelevancy>();
            relevancy.ValueRW.GhostRelevancySet.Clear();
            relevancy.ValueRW.GhostRelevancySet.Add(new RelevantGhostForConnection(connection.Value, ghostIDA), 0);
            relevancy.ValueRW.DefaultRelevancyQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(GhostRelevancyB));
            for (int i = 0; i < tickCountForReplication; i++)
            {
                testWorld.Tick(frameTime);
            }
            Assert.That(clientGhostQueryA.CalculateEntityCount(), Is.EqualTo(1));
            Assert.That(clientGhostQueryB.CalculateEntityCount(), Is.EqualTo(1));

            Clear();

            // test hash map has priority over query
            relevancy = serverRelevancyQuery.GetSingletonRW<GhostRelevancy>();
            relevancy.ValueRW.GhostRelevancySet.Clear();
            relevancy.ValueRW.GhostRelevancySet.Add(new RelevantGhostForConnection(connection.Value, ghostIDA), 0);
            relevancy.ValueRW.DefaultRelevancyQuery = new EntityQueryBuilder(Allocator.Temp).WithNone<GhostRelevancyA, GhostRelevancyB>().Build(testWorld.ServerWorld.EntityManager);
            for (int i = 0; i < tickCountForReplication; i++)
            {
                testWorld.Tick(frameTime);
            }
            Assert.That(clientGhostQueryA.CalculateEntityCount(), Is.EqualTo(1));
            Assert.That(clientGhostQueryB.IsEmpty);
            
            // Test same ghost set, but with new relevancy query
            relevancy = serverRelevancyQuery.GetSingletonRW<GhostRelevancy>();
            // A should already be relevant
            relevancy.ValueRW.DefaultRelevancyQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<GhostRelevancyB>().Build(testWorld.ServerWorld.EntityManager);
            for (int i = 0; i < tickCountForReplication; i++)
            {
                testWorld.Tick(frameTime);
            }
            Assert.That(clientGhostQueryA.CalculateEntityCount(), Is.EqualTo(1));
            Assert.That(clientGhostQueryB.CalculateEntityCount(), Is.EqualTo(1));
            
            // Test no query has no relevant ghosts
            relevancy = serverRelevancyQuery.GetSingletonRW<GhostRelevancy>();
            relevancy.ValueRW.DefaultRelevancyQuery = default;
            relevancy.ValueRW.GhostRelevancySet.Clear();
            for (int i = 0; i < tickCountForReplication; i++)
            {
                testWorld.Tick(frameTime);
            }
            Assert.That(clientGhostQueryA.IsEmpty);
            Assert.That(clientGhostQueryB.IsEmpty);

            Clear();

            // make sure relevancy disabled status works as expected with internal vs user facing query
            relevancy = serverRelevancyQuery.GetSingletonRW<GhostRelevancy>();
            relevancy.ValueRW.GhostRelevancyMode = GhostRelevancyMode.Disabled;
            relevancy.ValueRW.DefaultRelevancyQuery = new EntityQueryBuilder(Allocator.Temp).WithNone<GhostRelevancyA, GhostRelevancyB>().Build(testWorld.ServerWorld.EntityManager); // should be ignored
            for (int i = 0; i < tickCountForReplication; i++)
            {
                testWorld.Tick(frameTime);
            }
            Assert.That(clientGhostQueryA.CalculateEntityCount(), Is.EqualTo(1));
            Assert.That(clientGhostQueryB.CalculateEntityCount(), Is.EqualTo(1));

            Clear();

            // test if user marks a ghost as not relevant specifically, that a always relevant query won't override it
            relevancy = serverRelevancyQuery.GetSingletonRW<GhostRelevancy>();
            relevancy.ValueRW.GhostRelevancyMode = GhostRelevancyMode.SetIsIrrelevant;
            relevancy.ValueRW.DefaultRelevancyQuery = new EntityQueryBuilder(Allocator.Temp).WithAny<GhostRelevancyA, GhostRelevancyB>().Build(testWorld.ServerWorld.EntityManager);
            relevancy.ValueRW.GhostRelevancySet.Clear();
            relevancy.ValueRW.GhostRelevancySet.Add(new RelevantGhostForConnection(connection.Value, ghostIDA), 0);
            for (int i = 0; i < tickCountForReplication; i++)
            {
                testWorld.Tick(frameTime);
            }
            Assert.That(clientGhostQueryA.IsEmpty);
            Assert.That(clientGhostQueryB.CalculateEntityCount(), Is.EqualTo(1));

            Clear();
            // test for breaking change, if users set Irrelevant and expect non-included ghosts to be relevant without specifying a query
            relevancy = serverRelevancyQuery.GetSingletonRW<GhostRelevancy>();
            relevancy.ValueRW.GhostRelevancyMode = GhostRelevancyMode.SetIsIrrelevant;
            relevancy.ValueRW.DefaultRelevancyQuery = default; // This should work, since a default query matches everything
            relevancy.ValueRW.GhostRelevancySet.Clear();
            // B is excluded, A is implicitly included
            relevancy.ValueRW.GhostRelevancySet.Add(new RelevantGhostForConnection(connection.Value, ghostIDB), 0);
            for (int i = 0; i < tickCountForReplication; i++)
            {
                testWorld.Tick(frameTime);
            }
            Assert.That(clientGhostQueryA.CalculateEntityCount(), Is.EqualTo(1));
            Assert.That(clientGhostQueryB.IsEmpty);
            
            Clear();
            // test None filter in query vs SetIsIrrelevant
            relevancy = serverRelevancyQuery.GetSingletonRW<GhostRelevancy>();
            relevancy.ValueRW.GhostRelevancyMode = GhostRelevancyMode.SetIsIrrelevant;
            relevancy.ValueRW.DefaultRelevancyQuery = new EntityQueryBuilder(Allocator.Temp).WithNone<GhostRelevancyA>().Build(testWorld.ServerWorld.EntityManager);
            relevancy.ValueRW.GhostRelevancySet.Clear();
            // A is excluded by query, but included by set implicitly?
            relevancy.ValueRW.GhostRelevancySet.Add(new RelevantGhostForConnection(connection.Value, ghostIDB), 0);
            for (int i = 0; i < tickCountForReplication; i++)
            {
                testWorld.Tick(frameTime);
            }
            Assert.That(clientGhostQueryA.CalculateEntityCount(), Is.EqualTo(1));
            Assert.That(clientGhostQueryB.IsEmpty);
        }

        [TestCase(GhostRelevancyMode.SetIsRelevant, true, true, true)]
        [TestCase(GhostRelevancyMode.SetIsRelevant, true, false, true)]
        [TestCase(GhostRelevancyMode.SetIsRelevant, false, true, true)]
        [TestCase(GhostRelevancyMode.SetIsRelevant, false, false, false)]
        [TestCase(GhostRelevancyMode.SetIsIrrelevant, true, true, false)]
        [TestCase(GhostRelevancyMode.SetIsIrrelevant, true, false, true)]
        [TestCase(GhostRelevancyMode.SetIsIrrelevant, false, true, false)]
        [TestCase(GhostRelevancyMode.SetIsIrrelevant, false, false, true)] // if set does not contain, then implicitly we want the ghost replicated
        [TestCase(GhostRelevancyMode.Disabled, true, true, true)]
        [TestCase(GhostRelevancyMode.Disabled, true, false, true)]
        [TestCase(GhostRelevancyMode.Disabled, false, true, true)]
        [TestCase(GhostRelevancyMode.Disabled, false, false, true)]
        public void TestRelevancyScenarios(GhostRelevancyMode mode, bool queryMatchesGhost, bool setContainsGhost, bool expectedRelevancyResult)
        {
            // Setup spawn
            using var testWorld = new NetCodeTestWorld();
            testWorld.Bootstrap(true);
            var ghostGameObjectPrefabA = new GameObject();
            ghostGameObjectPrefabA.AddComponent<TestNetCodeAuthoring>().Converter = new GhostRelevancyConverterA();
            var authoringA = ghostGameObjectPrefabA.AddComponent<GhostAuthoringComponent>();
            authoringA.DefaultGhostMode = GhostMode.Predicted;
            Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObjectPrefabA));

            testWorld.CreateWorlds(true, 1);
            var prefabCollection = testWorld.TryGetSingletonEntity<NetCodeTestPrefabCollection>(testWorld.ServerWorld);
            var prefabA = testWorld.ServerWorld.EntityManager.GetBuffer<NetCodeTestPrefab>(prefabCollection)[0].Value;
            var ghostEntityA = testWorld.ServerWorld.EntityManager.Instantiate(prefabA);

            var serverRelevancyQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(GhostRelevancy));
            var clientGhostQueryA = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(GhostRelevancyA));
            var relevancy = serverRelevancyQuery.GetSingletonRW<GhostRelevancy>();

            relevancy.ValueRW.GhostRelevancyMode = mode;
            relevancy.ValueRW.GhostRelevancySet.Clear(); // make sure the only way to get the ghost is through the query
            testWorld.Connect(frameTime);
            testWorld.GoInGame();
            for (int i = 0; i < 100; i++)
            {
                testWorld.Tick(frameTime);
            }

            var connection = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(NetworkId)).GetSingleton<NetworkId>();
            var ghostIDA = testWorld.ServerWorld.EntityManager.GetComponentData<GhostInstance>(ghostEntityA).ghostId;

            relevancy = serverRelevancyQuery.GetSingletonRW<GhostRelevancy>();
            if (queryMatchesGhost)
            {
                relevancy.ValueRW.DefaultRelevancyQuery = new EntityQueryBuilder(Allocator.Temp).WithAny<GhostRelevancyA>().Build(testWorld.ServerWorld.EntityManager);
            }
            else
            {
                relevancy.ValueRW.DefaultRelevancyQuery = new EntityQueryBuilder(Allocator.Temp).WithNone<GhostRelevancyA>().Build(testWorld.ServerWorld.EntityManager);
            }

            if (setContainsGhost)
            {
                relevancy.ValueRW.GhostRelevancySet.Add(new RelevantGhostForConnection(connection.Value, ghostIDA), 0);
            }

            for (int i = 0; i < 4; i++)
            {
                testWorld.Tick(frameTime);
            }

            Assert.That(clientGhostQueryA.CalculateEntityCount(), expectedRelevancyResult ? Is.EqualTo(1) : Is.EqualTo(0));
        }
    }
}
