using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Unity.Transforms;
using UnityEngine.Networking.PlayerConnection;
using UnityEngine.TestTools;

namespace Unity.NetCode.Tests
{
    [GhostComponent(PrefabType = GhostPrefabType.All, SendTypeOptimization = GhostSendType.OnlyPredictedClients,
        OwnerSendType = SendToOwnerType.SendToNonOwner)]
    public struct TestInput : ICommandData
    {
        [GhostField] public NetworkTick Tick { get; set; }
        [GhostField] public int Value;
    }

    public struct TestInput2 : ICommandData
    {
        [GhostField] public NetworkTick Tick { get; set; }
        [GhostField] public int Value2;
    }

    public class TestInputConverter : TestNetCodeAuthoring.IConverter
    {
        public void Bake(GameObject gameObject, IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent(entity, new GhostOwner());
            baker.AddComponent<GhostGen_IntStruct>(entity);
            baker.AddBuffer<TestInput>(entity);
        }
    }

    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    [DisableAutoCreation]
    public partial class PredictionSystem : SystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<NetworkStreamInGame>();
        }
        protected override void OnUpdate()
        {
            var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
            Entities
                .WithAll<Simulate>()
                .ForEach((Entity entity, ref LocalTransform transform, in DynamicBuffer<TestInput> inputBuffer) =>
                {
                    if (!inputBuffer.GetDataAtTick(tick, out var input))
                        return;

                    transform.Position.y += 1.0f * input.Value;
                }).Run();
        }
    }

    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class InputSystem : SystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<NetworkStreamInGame>();
            RequireForUpdate<GhostOwner>();
        }
        protected override void OnUpdate()
        {
            var connection = SystemAPI.GetSingletonEntity<NetworkStreamInGame>();
            var commandTarget = EntityManager.GetComponentData<CommandTarget>(connection);
            if (commandTarget.targetEntity == Entity.Null)
                return;
            var inputBuffer = EntityManager.GetBuffer<TestInput>(commandTarget.targetEntity);
            inputBuffer.AddCommandData(new TestInput
            {
                Tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick,
                Value = 1
            });
        }
    }
    public class CommandBufferTests
    {
        private const float deltaTime = 1.0f / 60.0f;

        [Test]
        [TestCase(GhostModeMask.All, GhostMode.OwnerPredicted)]
        [TestCase(GhostModeMask.All, GhostMode.Interpolated)]
        [TestCase(GhostModeMask.All, GhostMode.Predicted)]
        [TestCase(GhostModeMask.Interpolated, GhostMode.Interpolated)]
        [TestCase(GhostModeMask.Predicted, GhostMode.Predicted)]
        public void CommandDataBuffer_GhostOwner_WillNotReceiveTheBuffer(GhostModeMask modeMask,
            GhostMode mode)
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(InputSystem));

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new TestInputConverter();
                var ghostConfig = ghostGameObject.AddComponent<GhostAuthoringComponent>();
                ghostConfig.SupportedGhostModes = modeMask;
                ghostConfig.DefaultGhostMode = mode;
                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));
                testWorld.CreateWorlds(true, 1);
                testWorld.Connect(deltaTime);
                testWorld.GoInGame();

                var serverEnt = SpawnEntityAndAssignOwnerOnServer(testWorld, ghostGameObject, 0);
                var clientEnt = WaitEntitySpawnedOnClientsAndAssignOwner(testWorld, 1, 0);

                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(deltaTime);

                var clientBuffer = testWorld.ClientWorlds[0].EntityManager.GetBuffer<TestInput>(clientEnt[0]);
                var serverBuffer = testWorld.ServerWorld.EntityManager.GetBuffer<TestInput>(serverEnt);
                // Server can drop commands which arrive too late, but it should get at least half of the commands
                Assert.GreaterOrEqual(serverBuffer.Length, clientBuffer.Length / 2);
                //Because of the redundancy the server always has more imputs
                int firstServerTick = 0;
                Assert.Less(firstServerTick, serverBuffer.Length);
                Assert.AreNotEqual(0, serverBuffer[firstServerTick].Value);
                // server cannot have commands which are older than what the client has
                Assert.GreaterOrEqual(serverBuffer[firstServerTick].Tick.TicksSince(clientBuffer[0].Tick), 0);
                for (int i = firstServerTick; i < serverBuffer.Length; ++i)
                    Assert.AreEqual(1, serverBuffer[i].Value);
                for (int i = 0; i < clientBuffer.Length; ++i)
                    Assert.AreEqual(1, clientBuffer[i].Value);
                //now rewrite the server buffer and confirm that is not changing on the client side
                serverBuffer.Length = 4;
                for (int i = 0; i < serverBuffer.Length; ++i)
                    serverBuffer[i] = new TestInput {Tick = serverBuffer[i].Tick, Value = 2};

                for (int i = 0; i < 10; ++i)
                    testWorld.Tick(deltaTime);

                clientBuffer = testWorld.ClientWorlds[0].EntityManager.GetBuffer<TestInput>(clientEnt[0]);
                Assert.Less(serverBuffer.Length, clientBuffer.Length);
                for (int i = 0; i < clientBuffer.Length; ++i)
                    Assert.AreEqual(1, clientBuffer[i].Value);

            }
        }

        [Test]
        [TestCase(GhostModeMask.All, GhostMode.Predicted)]
        [TestCase(GhostModeMask.Predicted, GhostMode.Predicted)]
        public void CommandDataBuffer_NonOwner_WillReceiveTheBuffer(GhostModeMask modeMask,
            GhostMode mode)
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(InputSystem));

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new TestInputConverter();
                var ghostConfig = ghostGameObject.AddComponent<GhostAuthoringComponent>();
                ghostConfig.SupportedGhostModes = modeMask;
                ghostConfig.DefaultGhostMode = mode;

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 2);
                testWorld.Connect(deltaTime);
                testWorld.GoInGame();

                var serverEnt = SpawnEntityAndAssignOwnerOnServer(testWorld, ghostGameObject, 0);
                var clientEnt = WaitEntitySpawnedOnClientsAndAssignOwner(testWorld, 2, 0);

                //Run a series of full ticks and check that the buffers are replicated to the non owner
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(deltaTime);

                var clientBuffer0 = testWorld.ClientWorlds[0].EntityManager.GetBuffer<TestInput>(clientEnt[0]);
                var clientBuffer1 = testWorld.ClientWorlds[1].EntityManager.GetBuffer<TestInput>(clientEnt[1]);
                var serverBuffer = testWorld.ServerWorld.EntityManager.GetBuffer<TestInput>(serverEnt);
                Assert.GreaterOrEqual(serverBuffer.Length, clientBuffer1.Length/2);
                Assert.GreaterOrEqual(serverBuffer.Length, clientBuffer0.Length/2);
                for (int i = 4; i < serverBuffer.Length; ++i)
                    Assert.AreEqual(serverBuffer[i].Value, clientBuffer0[i-4].Value);
                var bufferCopy = new TestInput[serverBuffer.Length];
                serverBuffer.AsNativeArray().CopyTo(bufferCopy);
                //run some partials tick and check that the buffer is preserved correctly
                for (int i = 0; i < 3; ++i)
                {
                    testWorld.Tick((1.0f / 60.0f) / 4.0f);
                    clientBuffer1 = testWorld.ClientWorlds[1].EntityManager.GetBuffer<TestInput>(clientEnt[1]);
                    serverBuffer = testWorld.ServerWorld.EntityManager.GetBuffer<TestInput>(serverEnt);
                    Assert.AreEqual(serverBuffer.Length, clientBuffer1.Length);
                    for (int k = 0; k < serverBuffer.Length; ++k)
                        Assert.AreEqual(bufferCopy[k].Value, clientBuffer1[k].Value);
                }
                //Do last partial tick and check the buffer are again in sync
                testWorld.Tick((1.0f / 60.0f) / 4.0f);
                Assert.AreEqual(serverBuffer.Length, clientBuffer1.Length);
                Assert.Greater(clientBuffer1.Length, bufferCopy.Length);
            }
        }


        [Test]
        [TestCase(GhostModeMask.All, GhostMode.OwnerPredicted)]
        [TestCase(GhostModeMask.All, GhostMode.Interpolated)]
        [TestCase(GhostModeMask.Interpolated, GhostMode.Interpolated)]
        public void CommandDataBuffer_NonOwner_ShouldNotReceiveTheBuffer(GhostModeMask modeMask,
            GhostMode mode)
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(InputSystem));

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new TestInputConverter();
                var ghostConfig = ghostGameObject.AddComponent<GhostAuthoringComponent>();
                ghostConfig.SupportedGhostModes = modeMask;
                ghostConfig.DefaultGhostMode = mode;

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));
                int numClients = 2;

                testWorld.CreateWorlds(true, numClients);
                testWorld.Connect(deltaTime);
                testWorld.GoInGame();

                var serverEnt = SpawnEntityAndAssignOwnerOnServer(testWorld, ghostGameObject, 0);
                var clientEnt = WaitEntitySpawnedOnClientsAndAssignOwner(testWorld, numClients, 0);

                //Run a series of full ticks and check that the buffers are replicated to the non owner
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(deltaTime);

                var serverBuffer = testWorld.ServerWorld.EntityManager.GetBuffer<TestInput>(serverEnt);
                for (int i = 0; i < numClients; ++i)
                {
                    var clientBuffer = testWorld.ClientWorlds[i].EntityManager.GetBuffer<TestInput>(clientEnt[i]);
                    if (i != 0)
                    {
                        Assert.AreNotEqual(serverBuffer.Length, clientBuffer.Length);
                        Assert.AreEqual(0, clientBuffer.Length);
                    }
                }
            }
        }

        //A extended version of the previous test, with an entity for each active client and one "spectator"
        [Test]
        public void CommandDataBuffer_OwnerPredicted_InterpolatedClientes_ShouldNotReceiveTheBuffer()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(InputSystem));

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new TestInputConverter();
                var ghostConfig = ghostGameObject.AddComponent<GhostAuthoringComponent>();
                ghostConfig.SupportedGhostModes = GhostModeMask.All;
                ghostConfig.DefaultGhostMode = GhostMode.OwnerPredicted;

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));
                int numClients = 3;

                testWorld.CreateWorlds(true, numClients);
                testWorld.Connect(deltaTime);
                testWorld.GoInGame();

                var serverEnt1 = SpawnEntityAndAssignOwnerOnServer(testWorld, ghostGameObject, 0);
                var serverEnt2 = SpawnEntityAndAssignOwnerOnServer(testWorld, ghostGameObject, 1);
                var clientEnt = new Entity[2];
                //Tick a little and wait all entities spawns
                for(int i=0;i<16;++i)
                    testWorld.Tick(deltaTime);
                //Assign the owner on the respective clients. Client3 is  passive (no entity)
                for(int i=0;i<2;++i)
                {
                    using var query = testWorld.ClientWorlds[i].EntityManager.CreateEntityQuery(typeof(GhostOwner));
                    var entities = query.ToEntityArray(Allocator.Temp);
                    var owners = query.ToComponentDataArray<GhostOwner>(Allocator.Temp);
                    using var connQuery = testWorld.ClientWorlds[i].EntityManager.CreateEntityQuery(typeof(NetworkId));
                    var conn = connQuery.GetSingletonEntity();
                    var networkId = connQuery.GetSingleton<NetworkId>();
                    for(int e=0;e<entities.Length;++e)
                    {
                        if (owners[e].NetworkId == networkId.Value)
                        {
                            clientEnt[i] = entities[e];
                            testWorld.ClientWorlds[i].EntityManager.SetComponentData(conn, new CommandTarget {targetEntity = entities[e]});
                        }
                    }
                }
                //Run a series of full ticks and check that the buffers are not replicated to the interpolated clients ghost
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(deltaTime);

                for (int i = 0; i < 3; ++i)
                {
                    using var query = testWorld.ClientWorlds[i].EntityManager.CreateEntityQuery(typeof(GhostOwner));
                    var entities = query.ToEntityArray(Allocator.Temp);
                    for (int e = 0; e < entities.Length; ++e)
                    {
                        var clientBuffer = testWorld.ClientWorlds[i].EntityManager.GetBuffer<TestInput>(entities[e]);
                        if(i == 2 || entities[e] != clientEnt[i])
                        {
                            Assert.AreEqual(0, clientBuffer.Length, $"Client {i} entity {e}");
                        }
                        else
                        {
                            Assert.AreNotEqual(0, clientBuffer.Length, $"Client {i} entity {e}");
                        }

                    }
                }
            }
        }

        private static Entity[] WaitEntitySpawnedOnClientsAndAssignOwner(NetCodeTestWorld testWorld, int numClients, int owner)
        {
            bool entitiesAreNotSpawned;
            var clientEnt = new Entity[numClients];
            int iterations = 0;
            do
            {
                ++iterations;
                testWorld.Tick(deltaTime);
                entitiesAreNotSpawned = false;
                for (int i = 0; i < numClients; ++i)
                {
                    clientEnt[i] = testWorld.TryGetSingletonEntity<TestInput>(testWorld.ClientWorlds[i]);
                    entitiesAreNotSpawned |= clientEnt[i] == Entity.Null;
                }
            } while (entitiesAreNotSpawned && iterations < 128);

            var clientConn = testWorld.TryGetSingletonEntity<NetworkStreamInGame>(testWorld.ClientWorlds[owner]);
            testWorld.ClientWorlds[owner].EntityManager.SetComponentData(clientConn, new CommandTarget {targetEntity = clientEnt[owner]});
            return clientEnt;
        }

        private static Entity SpawnEntityAndAssignOwnerOnServer(NetCodeTestWorld testWorld, GameObject ghostGameObject, int clientOwner)
        {
            var serverEnt = testWorld.SpawnOnServer(ghostGameObject);
            var net1 = testWorld.TryGetSingletonEntity<NetworkId>(testWorld.ClientWorlds[clientOwner]);
            var netId1 = testWorld.ClientWorlds[clientOwner].EntityManager.GetComponentData<NetworkId>(net1);

            //TODO: dispose this
            using var entitiesQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
            var entities = entitiesQuery.ToEntityArray(Allocator.Temp);
            testWorld.ServerWorld.EntityManager.SetComponentData(serverEnt, new GhostOwner {NetworkId = netId1.Value});
            testWorld.ServerWorld.EntityManager.SetComponentData(serverEnt, new GhostGen_IntStruct {IntValue = 1000});
            for (int i = 0; i < entities.Length; ++i)
            {
                var netId = testWorld.ServerWorld.EntityManager.GetComponentData<NetworkId>(entities[i]);
                if (netId.Value == netId1.Value)
                    testWorld.ServerWorld.EntityManager.SetComponentData(entities[i], new CommandTarget {targetEntity = serverEnt});
            }

            return serverEnt;
        }
    }
}
