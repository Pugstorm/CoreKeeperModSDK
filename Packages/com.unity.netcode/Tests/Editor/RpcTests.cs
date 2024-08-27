using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Collections;

namespace Unity.NetCode.Tests
{
    public class RpcTests
    {
        [Test]
        public void Rpc_UsingBroadcastOnClient_Works()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true,
                    typeof(ClientRcpSendSystem),
                    typeof(ServerRpcReceiveSystem),
                    typeof(NonSerializedRpcCommandRequestSystem));
                testWorld.CreateWorlds(true, 1);

                int SendCount = 10;
                ClientRcpSendSystem.SendCount = SendCount;
                ServerRpcReceiveSystem.ReceivedCount = 0;

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                for (int i = 0; i < 33; ++i)
                    testWorld.Tick(1f / 60f);

                Assert.AreEqual(SendCount, ServerRpcReceiveSystem.ReceivedCount);
            }
        }

        [Test]
        public void Rpc_UsingConnectionEntityOnClient_Works()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true,
                    typeof(ClientRcpSendSystem),
                    typeof(ServerRpcReceiveSystem),
                    typeof(NonSerializedRpcCommandRequestSystem));
                testWorld.CreateWorlds(true, 1);

                int SendCount = 10;
                ClientRcpSendSystem.SendCount = SendCount;
                ServerRpcReceiveSystem.ReceivedCount = 0;

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                var remote = testWorld.TryGetSingletonEntity<NetworkStreamConnection>(testWorld.ClientWorlds[0]);
                testWorld.ClientWorlds[0].GetExistingSystemManaged<ClientRcpSendSystem>().Remote = remote;

                for (int i = 0; i < 33; ++i)
                    testWorld.Tick(1f / 60f);

                Assert.AreEqual(SendCount, ServerRpcReceiveSystem.ReceivedCount);
            }
        }

        [Test]
        public void Rpc_SerializedRpcFlow_Works()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true,
                    typeof(SerializedClientRcpSendSystem),
                    typeof(SerializedServerRpcReceiveSystem),
                    typeof(SerializedRpcCommandRequestSystem));
                testWorld.CreateWorlds(true, 1);

                int SendCount = 1;
                var SendCmd = new SerializedRpcCommand
                    {intValue = 123456, shortValue = 32154, floatValue = 12345.67f};
                SerializedClientRcpSendSystem.SendCount = SendCount;
                SerializedClientRcpSendSystem.Cmd = SendCmd;

                SerializedServerRpcReceiveSystem.ReceivedCount = 0;

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                for (int i = 0; i < 33; ++i)
                    testWorld.Tick(1f / 60f);

                Assert.AreEqual(SendCount, SerializedServerRpcReceiveSystem.ReceivedCount);
                Assert.AreEqual(SendCmd, SerializedServerRpcReceiveSystem.ReceivedCmd);
            }
        }

        [Test]
        public void Rpc_ServerBroadcast_Works()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true,
                    typeof(ServerRpcBroadcastSendSystem),
                    typeof(MultipleClientBroadcastRpcReceiveSystem),
                    typeof(NonSerializedRpcCommandRequestSystem));
                testWorld.CreateWorlds(true, 2);

                ServerRpcBroadcastSendSystem.SendCount = 0;
                MultipleClientBroadcastRpcReceiveSystem.ReceivedCount[0] = 0;
                MultipleClientBroadcastRpcReceiveSystem.ReceivedCount[1] = 0;

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                int SendCount = 5;
                ServerRpcBroadcastSendSystem.SendCount = SendCount;

                for (int i = 0; i < 33; ++i)
                    testWorld.Tick(1f / 60f);

                Assert.AreEqual(SendCount, MultipleClientBroadcastRpcReceiveSystem.ReceivedCount[0]);
                Assert.AreEqual(SendCount, MultipleClientBroadcastRpcReceiveSystem.ReceivedCount[1]);
            }
        }

        [Test]
        public void Rpc_SendingBeforeGettingNetworkId_LogWarning()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true,
                    typeof(FlawedClientRcpSendSystem),
                    typeof(ServerRpcReceiveSystem),
                    typeof(NonSerializedRpcCommandRequestSystem));
                testWorld.CreateWorlds(true, 1);

                int SendCount = 1;
                ServerRpcReceiveSystem.ReceivedCount = 0;
                FlawedClientRcpSendSystem.SendCount = SendCount;

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                LogAssert.Expect(LogType.Warning, new Regex("Cannot send RPC with no remote connection."));
                for (int i = 0; i < 33; ++i)
                    testWorld.Tick(1f / 60f);

                Assert.AreEqual(0, ServerRpcReceiveSystem.ReceivedCount);
            }
        }

        [Test]
        public void Rpc_MalformedPackets_ThrowsAndLogError()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.DriverRandomSeed = 0xbadc0de;
                testWorld.DriverFuzzOffset = 1;
                testWorld.DriverFuzzFactor = new int[2];
                testWorld.DriverFuzzFactor[0] = 10;
                testWorld.Bootstrap(true,
                    typeof(MalformedClientRcpSendSystem),
                    typeof(ServerMultipleRpcReceiveSystem),
                    typeof(MultipleClientSerializedRpcCommandRequestSystem));
                testWorld.CreateWorlds(true, 2);

                int SendCount = 15;
                MalformedClientRcpSendSystem.SendCount[0] = SendCount;
                MalformedClientRcpSendSystem.SendCount[1] = SendCount;

                MalformedClientRcpSendSystem.Cmds[0] = new ClientIdRpcCommand {Id = 0};
                MalformedClientRcpSendSystem.Cmds[1] = new ClientIdRpcCommand {Id = 1};

                ServerMultipleRpcReceiveSystem.ReceivedCount[0] = 0;
                ServerMultipleRpcReceiveSystem.ReceivedCount[1] = 0;

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                LogAssert.Expect(LogType.Error, new Regex("RpcSystem received invalid rpc"));
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(1f / 60f);

                Assert.Less(ServerMultipleRpcReceiveSystem.ReceivedCount[0], SendCount);
                Assert.True(ServerMultipleRpcReceiveSystem.ReceivedCount[1] == SendCount);
            }
        }

        [Test]
        public void Rpc_CanSendMoreThanOnePacketPerFrame()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true,
                    typeof(SerializedClientLargeRcpSendSystem),
                    typeof(SerializedServerLargeRpcReceiveSystem),
                    typeof(SerializedLargeRpcCommandRequestSystem));
                testWorld.CreateWorlds(true, 1);

                int SendCount = 50;
                var SendCmd = new SerializedLargeRpcCommand
                    {stringValue = new FixedString512Bytes("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")};
                SerializedClientLargeRcpSendSystem.SendCount = SendCount;
                SerializedClientLargeRcpSendSystem.Cmd = SendCmd;

                SerializedServerLargeRpcReceiveSystem.ReceivedCount = 0;

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                for (int i = 0; i < 33; ++i)
                    testWorld.Tick(1f / 60f);

                Assert.AreEqual(SendCount, SerializedServerLargeRpcReceiveSystem.ReceivedCount);
                Assert.AreEqual(SendCmd, SerializedServerLargeRpcReceiveSystem.ReceivedCmd);
            }
        }
        [Test]
        public void Rpc_CanPackMultipleRPCs()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true,
                    typeof(SerializedClientLargeRcpSendSystem),
                    typeof(SerializedServerLargeRpcReceiveSystem),
                    typeof(SerializedLargeRpcCommandRequestSystem));
                testWorld.CreateWorlds(true, 1);

                int SendCount = 500;
                var SendCmd = new SerializedLargeRpcCommand
                    {stringValue = new FixedString512Bytes("\0\0\0\0\0\0\0\0\0\0")};
                SerializedClientLargeRcpSendSystem.SendCount = SendCount;
                SerializedClientLargeRcpSendSystem.Cmd = SendCmd;

                SerializedServerLargeRpcReceiveSystem.ReceivedCount = 0;

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                for (int i = 0; i < 33; ++i)
                    testWorld.Tick(1f / 60f);

                Assert.AreEqual(SendCount, SerializedServerLargeRpcReceiveSystem.ReceivedCount);
                Assert.AreEqual(SendCmd, SerializedServerLargeRpcReceiveSystem.ReceivedCmd);
            }
        }

        public class GhostConverter : TestNetCodeAuthoring.IConverter
        {
            public void Bake(GameObject gameObject, IBaker baker)
            {
                var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
                baker.AddComponent(entity, new GhostOwner());
            }
        }

        [Test]
        public void Rpc_CanSendEntityFromClientAndServer()
        {
            void SendRpc(World world, Entity entity)
            {
                var req = world.EntityManager.CreateEntity();
                world.EntityManager.AddComponentData(req, new RpcWithEntity {entity = entity});
                world.EntityManager.AddComponentData(req, new SendRpcCommandRequest {TargetConnection = Entity.Null});
            }

            RpcWithEntity RecvRpc(World world)
            {
                using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<RpcWithEntity>());
                Assert.AreEqual(1, query.CalculateEntityCount());
                var rpcReceived = query.GetSingleton<RpcWithEntity>();
                world.EntityManager.DestroyEntity(query);
                return rpcReceived;
            }


            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(RpcWithEntityRpcCommandRequestSystem));
                var ghostGameObject = new GameObject("SimpleGhost");
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new GhostConverter();
                testWorld.CreateGhostCollection(ghostGameObject);
                testWorld.CreateWorlds(true, 1);

                float frameTime = 1.0f / 60.0f;
                testWorld.Connect(frameTime);
                // Go in-game
                testWorld.GoInGame();

                var serverEntity = testWorld.SpawnOnServer(ghostGameObject);
                //Wait some frame so it is spawned also on the client
                for (int i = 0; i < 8; ++i)
                    testWorld.Tick(1f / 60f);

                var recvGhostMapSingleton = testWorld.TryGetSingletonEntity<SpawnedGhostEntityMap>(testWorld.ClientWorlds[0]);
                // Retrieve the client entity
                var ghost = testWorld.ServerWorld.EntityManager.GetComponentData<GhostInstance>(serverEntity);
                Assert.IsTrue(testWorld.ClientWorlds[0].EntityManager.GetComponentData<SpawnedGhostEntityMap>(recvGhostMapSingleton).Value
                    .TryGetValue(new SpawnedGhost {ghostId = ghost.ghostId, spawnTick = ghost.spawnTick}, out var clientEntity));

                //Send the rpc to the server
                SendRpc(testWorld.ClientWorlds[0], clientEntity);
                for (int i = 0; i < 8; ++i)
                    testWorld.Tick(1f / 60f);
                var rpcReceived = RecvRpc(testWorld.ServerWorld);
                Assert.IsTrue(rpcReceived.entity != Entity.Null);
                Assert.IsTrue(rpcReceived.entity == serverEntity);

                // Server send the rpc to the client
                SendRpc(testWorld.ServerWorld, serverEntity);
                for (int i = 0; i < 8; ++i)
                    testWorld.Tick(1f / 60f);
                rpcReceived = RecvRpc(testWorld.ClientWorlds[0]);
                Assert.IsTrue(rpcReceived.entity != Entity.Null);
                Assert.IsTrue(rpcReceived.entity == clientEntity);

                // Client try to send a client-only entity -> result in a Entity.Null reference
                //Send the rpc to the server
                var clientOnlyEntity = testWorld.ClientWorlds[0].EntityManager.CreateEntity();
                SendRpc(testWorld.ClientWorlds[0], clientOnlyEntity);
                for (int i = 0; i < 8; ++i)
                    testWorld.Tick(1f / 60f);
                rpcReceived = RecvRpc(testWorld.ServerWorld);
                Assert.IsTrue(rpcReceived.entity == Entity.Null);

                // Some Edge cases:
                // 1 - Entity has been or going to be despawned on the client. Expected: server will receive an Entity.Null in the rpc
                // 2 - Entity has been despawn on the server but the client. Server will not be able to resolve the entity correctly
                //     in that window, since the ghost mapping is reset

                //Destroy the entity on the server
                testWorld.ServerWorld.EntityManager.DestroyEntity(serverEntity);
                //Let the client try to send an rpc for it (this mimic sort of latency)
                SendRpc(testWorld.ClientWorlds[0], clientEntity);
                //Entity is destroyed on the server (so no GhostComponent). If server try to send an rpc, the entity will be translated to null
                SendRpc(testWorld.ServerWorld, serverEntity);
                for (int i = 0; i < 4; ++i)
                    testWorld.Tick(1f / 60f);
                //Server should not be able to resolve the reference
                rpcReceived = RecvRpc(testWorld.ServerWorld);
                Assert.IsTrue(rpcReceived.entity == Entity.Null);
                //On the client must but null
                rpcReceived = RecvRpc(testWorld.ClientWorlds[0]);
                Assert.IsTrue(rpcReceived.entity == Entity.Null);
                var sendGhostMapSingleton = testWorld.TryGetSingletonEntity<SpawnedGhostEntityMap>(testWorld.ServerWorld);
                //If client send the rpc now (the entity should not exists anymore and the mapping should be reset on both client and server now)
                Assert.IsFalse(testWorld.ClientWorlds[0].EntityManager.GetComponentData<SpawnedGhostEntityMap>(recvGhostMapSingleton).Value
                    .TryGetValue(new SpawnedGhost {ghostId = ghost.ghostId, spawnTick = ghost.spawnTick}, out var _));
                Assert.IsFalse(testWorld.ServerWorld.EntityManager.GetComponentData<SpawnedGhostEntityMap>(sendGhostMapSingleton).Value
                    .TryGetValue(new SpawnedGhost {ghostId = ghost.ghostId, spawnTick = ghost.spawnTick}, out var _));
                SendRpc(testWorld.ClientWorlds[0], clientEntity);
                for (int i = 0; i < 4; ++i)
                    testWorld.Tick(1f / 60f);
                //The received entity must be null
                rpcReceived = RecvRpc(testWorld.ServerWorld);
                Assert.IsTrue(rpcReceived.entity == Entity.Null);
            }
        }

        [Test]
        public void WarnsIfApplicationRunInBackgroundIsFalse()
        {
            const float dt = 1f/60f;
            var existingRunInBackground = Application.runInBackground;
            try
            {
                using var testWorld = new NetCodeTestWorld();
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 1);

                Application.runInBackground = false;
                testWorld.Connect(dt, 4);
                // Warning is suppressed by default.
                testWorld.Tick(dt);
                // Un-suppress it.
                Assert.IsTrue(testWorld.TrySetSuppressRunInBackgroundWarning(false), "Failed to suppress!");
                // Expect two logs, one per world:
                var regex = new Regex(@"Netcode detected that you don't have Application\.runInBackground enabled");
                LogAssert.Expect(LogType.Error, regex);
                LogAssert.Expect(LogType.Error, regex);
                testWorld.Tick(dt);
                // When the client is DC'd, it should not warn.
                testWorld.DisposeServerWorld();
                testWorld.Tick(dt);
            }
            finally
            {
                Application.runInBackground = existingRunInBackground;
            }
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void Rpc_WarnsIfNotConsumedAfter4Frames()
        {
            const float dt = 1f/60f;

            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 1);

                // Create a dud RPC on client and server. Ideally this test would test a full RPC flow, but trying to isolate dependencies:
                var clientWorld = testWorld.ClientWorlds[0];
                var clientNetDebug = clientWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetDebug>()).GetSingleton<NetDebug>();
                clientNetDebug.LogLevel = NetDebug.LogLevelType.Warning;
                testWorld.GetSingletonRW<NetDebug>(clientWorld).ValueRW.MaxRpcAgeFrames = 4;
                clientWorld.EntityManager.CreateEntity(ComponentType.ReadWrite<ReceiveRpcCommandRequest>());

                var serverWorld = testWorld.ServerWorld;
                var serverNetDebug = serverWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetDebug>()).GetSingleton<NetDebug>();
                serverNetDebug.LogLevel = NetDebug.LogLevelType.Warning;
                testWorld.GetSingletonRW<NetDebug>(serverWorld).ValueRW.MaxRpcAgeFrames = 4;
                serverWorld.EntityManager.CreateEntity(ComponentType.ReadWrite<ReceiveRpcCommandRequest>());

                // 3 ticks before our expected one:
                testWorld.Tick(dt);
                testWorld.Tick(dt);
                testWorld.Tick(dt);

                // Now assert the final tick logs warning on both client and server (server is 1 frame behind):
                var regex = new Regex("NetCode RPC Entity\\(.*\\) has not been consumed or destroyed for '4'");
                LogAssert.Expect(LogType.Warning, regex);
                testWorld.Tick(dt);
                LogAssert.Expect(LogType.Warning, regex);
                testWorld.Tick(dt);
            }
        }
#endif
    }
}
