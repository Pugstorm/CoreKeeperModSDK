using System.Diagnostics;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using UnityEngine;

namespace Unity.NetCode.Tests
{
    public class WorldMigrationTests
    {
        void StepTicks(NetCodeTestWorld world, int count, float dt)
        {
                for(int i = 0; i < count; ++i)
                    world.Tick(dt);
        }
        void PrepareSend(int count)
        {
            var serverCmd = new SerializedRpcCommand {intValue = 1234567, shortValue = 32154, floatValue = 12345.67f};
            var clientCmd = new SerializedRpcCommand {intValue = 7654321, shortValue = 12345, floatValue = 76543.21f};
            ResetClientSend(count, serverCmd);
            ResetServerSend(count, clientCmd);
        }

        void ValidateSend(int count)
        {
            var serverCmd = new SerializedRpcCommand {intValue = 1234567, shortValue = 32154, floatValue = 12345.67f};
            var clientCmd = new SerializedRpcCommand {intValue = 7654321, shortValue = 12345, floatValue = 76543.21f};

            Assert.AreEqual(count, SerializedServerRpcReceiveSystem.ReceivedCount);
            Assert.AreEqual(serverCmd, SerializedServerRpcReceiveSystem.ReceivedCmd);
            Assert.AreEqual(count, SerializedClientRpcReceiveSystem.ReceivedCount);
            Assert.AreEqual(clientCmd, SerializedClientRpcReceiveSystem.ReceivedCmd);
        }

        static void ResetServerSend(int count, SerializedRpcCommand cmd)
        {
            SerializedServerRcpSendSystem.SendCount = count;
            SerializedServerRcpSendSystem.Cmd = cmd;
            SerializedClientRpcReceiveSystem.ReceivedCount = 0;
        }

        static void ResetClientSend(int count, SerializedRpcCommand cmd)
        {
            SerializedClientRcpSendSystem.SendCount = count;
            SerializedClientRcpSendSystem.Cmd = cmd;
            SerializedServerRpcReceiveSystem.ReceivedCount = 0;
        }

        [Test]
        public void WorldMigration_ResetWorlds_Works()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                float frameTime = 1.0f / 60.0f;

                testWorld.Bootstrap(true,
                    typeof(SerializedClientRcpSendSystem),
                    typeof(DriverMigrationSystem),
                    typeof(SerializedServerRcpSendSystem),
                    typeof(SerializedServerRpcReceiveSystem),
                    typeof(SerializedClientRpcReceiveSystem),
                    typeof(SerializedRpcCommandRequestSystem));
                testWorld.CreateWorlds(true, 1);

                testWorld.Connect(frameTime);

                PrepareSend(1);
                StepTicks(testWorld, 15, frameTime);
                ValidateSend(1);

                var sseqn = testWorld.ServerWorld.SequenceNumber;
                var cseqn = testWorld.ClientWorlds[0].SequenceNumber;
                testWorld.MigrateServerWorld();
                testWorld.MigrateClientWorld(0);
                Assert.AreNotEqual(sseqn, testWorld.ServerWorld.SequenceNumber);
                Assert.AreNotEqual(cseqn, testWorld.ClientWorlds[0].SequenceNumber);

                StepTicks(testWorld, 15, frameTime);

                PrepareSend(1);
                StepTicks(testWorld, 15, frameTime);
                ValidateSend(1);
            }
        }

        [Test]
        public void WorldMigration_MigrateToOwnWorld_Works()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                float frameTime = 1.0f / 60.0f;

                testWorld.Bootstrap(true,
                    typeof(DriverMigrationSystem),
                    typeof(SerializedClientRcpSendSystem),
                    typeof(SerializedServerRcpSendSystem),
                    typeof(SerializedServerRpcReceiveSystem),
                    typeof(SerializedClientRpcReceiveSystem),
                    typeof(SerializedRpcCommandRequestSystem));
                testWorld.CreateWorlds(true, 1);

                testWorld.Connect(frameTime);

                PrepareSend(1);
                StepTicks(testWorld, 15, frameTime);
                ValidateSend(1);

                var sseqn = testWorld.ServerWorld.SequenceNumber;
                var cseqn = testWorld.ClientWorlds[0].SequenceNumber;

                var bananaWorld = new World("BananaWorld", WorldFlags.GameServer);
                var oldName = testWorld.ServerWorld.Name;

                testWorld.MigrateServerWorld(bananaWorld);

                Assert.AreNotEqual(testWorld.ServerWorld.Name, oldName);
                Assert.AreEqual(testWorld.ServerWorld.Name, "BananaWorld");

                testWorld.MigrateClientWorld(0);
                Assert.AreNotEqual(sseqn, testWorld.ServerWorld.SequenceNumber);
                Assert.AreNotEqual(cseqn, testWorld.ClientWorlds[0].SequenceNumber);

                StepTicks(testWorld, 15, frameTime);

                PrepareSend(1);
                StepTicks(testWorld, 15, frameTime);
                ValidateSend(1);
            }
        }

        [Test]
        public void WorldMigration_ResetWorldsWithMultipleClients_Works()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                float frameTime = 1.0f / 60.0f;

                testWorld.Bootstrap(true,
                    typeof(DriverMigrationSystem),
                    typeof(SerializedClientRcpSendSystem),
                    typeof(SerializedServerRcpSendSystem),
                    typeof(SerializedServerRpcReceiveSystem),
                    typeof(SerializedClientRpcReceiveSystem),
                    typeof(SerializedRpcCommandRequestSystem));
                testWorld.CreateWorlds(true, 10);

                testWorld.Connect(frameTime);
                Unity.Mathematics.Random random = new Unity.Mathematics.Random(42);
                var rndClient = random.NextInt(0, 10);

                var c = testWorld.TryGetSingletonEntity<NetworkId>(testWorld.ClientWorlds[rndClient]);
                var id = testWorld.ClientWorlds[rndClient].EntityManager.GetComponentData<NetworkId>(c).Value;

                var name = testWorld.ClientWorlds[3].Name;
                StepTicks(testWorld, 5, frameTime);
                testWorld.RestartClientWorld(rndClient);

                StepTicks(testWorld, 5, frameTime);
                testWorld.MigrateServerWorld();

                StepTicks(testWorld, 5, frameTime);

                var ep = NetworkEndpoint.LoopbackIpv4;
                ep.Port = 7979;
                testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ClientWorlds[rndClient]).ValueRW.Connect(testWorld.ClientWorlds[rndClient].EntityManager, ep);

                var con = Entity.Null;
                for (int i = 0;
                    i < 15 && ((con = testWorld.TryGetSingletonEntity<NetworkId>(testWorld.ClientWorlds[rndClient])) ==
                              Entity.Null);
                    ++i)
                {
                    testWorld.Tick(frameTime);
                }
                var connectionId = testWorld.ClientWorlds[rndClient].EntityManager.GetComponentData<NetworkId>(con).Value;
                Assert.AreEqual(id , connectionId);
            }
        }

    }
}
