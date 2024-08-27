using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode.LowLevel.Unsafe;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Error;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.NetCode.Tests
{
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial class CheckConnectionSystem : SystemBase
    {
        public int numConnected;
        public int numInGame;
        private EntityQuery inGame;
        private EntityQuery connected;
        protected override void OnCreate()
        {
            connected = GetEntityQuery(ComponentType.ReadOnly<NetworkId>());
            inGame = GetEntityQuery(ComponentType.ReadOnly<NetworkStreamInGame>());
        }

        protected override void OnUpdate()
        {
            numConnected = connected.CalculateEntityCount();
            numInGame = inGame.CalculateEntityCount();
        }
    }
    public class ConnectionTests
    {
        [Test]
        public void ConnectSingleClient()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(CheckConnectionSystem));
                testWorld.CreateWorlds(true, 1);

                var ep = NetworkEndpoint.LoopbackIpv4;
                ep.Port = 7979;
                testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ServerWorld).ValueRW.Listen(ep);
                testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ClientWorlds[0]).ValueRW.Connect(testWorld.ClientWorlds[0].EntityManager, ep);

                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(16f/1000f);

                Assert.AreEqual(1, testWorld.ServerWorld.GetExistingSystemManaged<CheckConnectionSystem>().numConnected);
                Assert.AreEqual(1, testWorld.ClientWorlds[0].GetExistingSystemManaged<CheckConnectionSystem>().numConnected);

                testWorld.GoInGame();
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(16f/1000f);

                Assert.AreEqual(1, testWorld.ServerWorld.GetExistingSystemManaged<CheckConnectionSystem>().numConnected);
                Assert.AreEqual(1, testWorld.ServerWorld.GetExistingSystemManaged<CheckConnectionSystem>().numInGame);
                Assert.AreEqual(1, testWorld.ClientWorlds[0].GetExistingSystemManaged<CheckConnectionSystem>().numConnected);
                Assert.AreEqual(1, testWorld.ClientWorlds[0].GetExistingSystemManaged<CheckConnectionSystem>().numInGame);
            }
        }

        [TestCase(60, 60, 1)]
        [TestCase(40, 20, 2)]
        public void ClientTickRate_ServerAndClientsUseTheSameRateSettings(
            int simulationTickRate, int networkTickRate, int predictedFixedStepRatio)
        {
            using var testWorld = new NetCodeTestWorld();
            var tickRate = new ClientServerTickRate
            {
                SimulationTickRate = simulationTickRate,
                PredictedFixedStepSimulationTickRatio = predictedFixedStepRatio,
                NetworkTickRate = networkTickRate,
            };
            SetupTickRate(tickRate, testWorld);
            //Check that the predicted fixed step rate is also set accordingly.
            LogAssert.NoUnexpectedReceived();
            Assert.AreEqual(tickRate.PredictedFixedStepSimulationTimeStep, testWorld.ServerWorld.GetExistingSystemManaged<PredictedFixedStepSimulationSystemGroup>().Timestep);
            Assert.AreEqual(tickRate.PredictedFixedStepSimulationTimeStep, testWorld.ClientWorlds[0].GetExistingSystemManaged<PredictedFixedStepSimulationSystemGroup>().Timestep);
        }

        static void SetupTickRate(ClientServerTickRate tickRate, NetCodeTestWorld testWorld)
        {
            testWorld.Bootstrap(true);
            testWorld.CreateWorlds(true, 1);
            testWorld.ServerWorld.EntityManager.CreateSingleton(tickRate);
            tickRate.ResolveDefaults();
            tickRate.Validate();
            // Connect and make sure the connection could be established
            const float frameTime = 1f / 60f;
            testWorld.Connect(frameTime);

            //Check that the simulation tick rate are the same
            var serverRate = testWorld.GetSingleton<ClientServerTickRate>(testWorld.ServerWorld);
            var clientRate = testWorld.GetSingleton<ClientServerTickRate>(testWorld.ClientWorlds[0]);
            Assert.AreEqual(tickRate.SimulationTickRate, serverRate.SimulationTickRate);
            Assert.AreEqual(tickRate.SimulationTickRate, clientRate.SimulationTickRate);
            Assert.AreEqual(tickRate.PredictedFixedStepSimulationTickRatio, serverRate.PredictedFixedStepSimulationTickRatio);
            Assert.AreEqual(tickRate.PredictedFixedStepSimulationTickRatio, clientRate.PredictedFixedStepSimulationTickRatio);

            //Do one last step so all the new settings are applied
            testWorld.Tick(frameTime);
        }

        [Test]
        public void IncorrectlyDisposingAConnectionLogsError()
        {
            const float dt = 1f / 60f;

            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 1);
                Test(testWorld, testWorld.ClientWorlds[0]);
            }
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 1);
                Test(testWorld, testWorld.ServerWorld);
            }

            static void Test(NetCodeTestWorld testWorld, World worldBeingTested)
            {
                testWorld.Connect(dt);
                var connEntity = testWorld.TryGetSingletonEntity<NetworkStreamConnection>(worldBeingTested);
                Assert.IsTrue(worldBeingTested.EntityManager.Exists(connEntity));
                LogAssert.Expect(LogType.Error, new Regex($@"(has been incorrectly disposed)(.*)({worldBeingTested.Name})"));
                worldBeingTested.EntityManager.DestroyEntity(connEntity);
                testWorld.Tick(dt); // This tick will raise the error.
                testWorld.Tick(dt); // This tick should NOT raise it again.
            }
        }

        [Test]
        public void ConnectionEventsAreRaised()
        {
            const float dt = 1f / 60f;
            const NetworkStreamDisconnectReason invalidDisconnectReason = default;

            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 3);

                // Manually connect them:
                var ep = NetworkEndpoint.LoopbackIpv4;
                ep.Port = 7979;
                testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ServerWorld).ValueRW.Listen(ep);
                var connectionEntities = new Entity[testWorld.ClientWorlds.Length];
                for (int i = 0; i < testWorld.ClientWorlds.Length; ++i)
                {
                    var clientWorld = testWorld.ClientWorlds[i];
                    connectionEntities[i] = testWorld.GetSingletonRW<NetworkStreamDriver>(clientWorld).ValueRW.Connect(clientWorld.EntityManager, ep);
                }
                testWorld.Tick(dt);

                // Client connecting:
                for (var i = 0; i < testWorld.ClientWorlds.Length; i++)
                {
                    var world = testWorld.ClientWorlds[i];
                    var connectionEventsForClient = testWorld.GetSingleton<NetworkStreamDriver>(world).ConnectionEventsForTick;
                    var evt = connectionEventsForClient.FirstOrDefault();
                    var s = $"[{i}] {evt.ToFixedString()}";
                    Assert.IsTrue(evt.ConnectionId.IsCreated, s);
                    Assert.AreEqual(1, connectionEventsForClient.Length, $"Client should only know about self connecting: {s}!");
                    Assert.AreEqual(new NetworkId {Value = 0}, evt.Id, $"No NetworkId when connecting: {s}!");
                    Assert.AreEqual(ConnectionState.State.Connecting, evt.State, s);
                    Assert.AreEqual(invalidDisconnectReason, evt.DisconnectReason, s);
                    Assert.IsFalse(world.EntityManager.HasComponent<NetworkId>(evt.ConnectionEntity), s);

                    world.EntityManager.CompleteAllTrackedJobs();
                    Assert.AreEqual(ConnectionState.State.Connecting, testWorld.GetSingleton<NetworkStreamConnection>(world).CurrentState, s);
                }

                // Server should have all join events, so wait for it to get them:
                NativeArray<NetCodeConnectionEvent>.ReadOnly connectionEventsForServerWorld;
                int counter = 0;
                do
                {
                    connectionEventsForServerWorld = testWorld.GetSingleton<NetworkStreamDriver>(testWorld.ServerWorld).ConnectionEventsForTick;
                    if (connectionEventsForServerWorld.Length == 0)
                        testWorld.Tick(dt);
                } while (++counter < 2);

                Entity lastClientsConnectionEntity = default;
                Assert.AreEqual(testWorld.ClientWorlds.Length, connectionEventsForServerWorld.Length, $"Server should know about all clients: NO Handshake, ONLY Connected. First: {connectionEventsForServerWorld.FirstOrDefault().ToFixedString().ToString()}");
                for (var i = 0; i < testWorld.ClientWorlds.Length; i++)
                {
                    var evt = connectionEventsForServerWorld[i];
                    var s = $"[{i}] {evt.ToFixedString()}";
                    Assert.IsTrue(evt.ConnectionId.IsCreated, s);
                    Assert.AreEqual(new NetworkId {Value = i + 1}, evt.Id, s);
                    Assert.AreEqual(ConnectionState.State.Connected, evt.State, s);
                    Assert.IsTrue(testWorld.ServerWorld.EntityManager.Exists(evt.ConnectionEntity), s);
                    var networkIdFromEntity = testWorld.ServerWorld.EntityManager.GetComponentData<NetworkId>(evt.ConnectionEntity);
                    Assert.AreEqual(networkIdFromEntity, evt.Id, s);
                    lastClientsConnectionEntity = evt.ConnectionEntity;
                    Assert.AreEqual(invalidDisconnectReason, evt.DisconnectReason);

                    testWorld.ServerWorld.EntityManager.CompleteAllTrackedJobs();
                    Assert.AreEqual(ConnectionState.State.Connected, testWorld.ServerWorld.EntityManager.GetComponentData<NetworkStreamConnection>(evt.ConnectionEntity).CurrentState, s);
                }

                // Ensure each client gets it's own Handshake & Connection events (i.e. it's 'self' events):
                do
                {
                    var connectionEventsForClient = testWorld.GetSingleton<NetworkStreamDriver>(testWorld.ClientWorlds[0]).ConnectionEventsForTick;
                    if (connectionEventsForClient.Length == 0)
                        testWorld.Tick(dt);
                } while (++counter < 2);

                // Client - Handshakes:
                for (var i = 0; i < testWorld.ClientWorlds.Length; i++)
                {
                    var world = testWorld.ClientWorlds[i];
                    var connectionEventsForClient = testWorld.GetSingleton<NetworkStreamDriver>(world).ConnectionEventsForTick;
                    Assert.AreEqual(1, connectionEventsForClient.Length, $"Client[{i}] should only know about self! First: {connectionEventsForClient.FirstOrDefault().ToFixedString()}!");
                    var evt = connectionEventsForClient[0];
                    var s = $"[{i}] {evt.ToFixedString()}";
                    Assert.IsTrue(evt.ConnectionId.IsCreated, s);
                    Assert.AreEqual(new NetworkId {Value = 0}, evt.Id, s);
                    Assert.AreEqual(ConnectionState.State.Handshake, evt.State, s);
                    Assert.AreEqual(invalidDisconnectReason, evt.DisconnectReason, s);
                    Assert.IsTrue(world.EntityManager.Exists(evt.ConnectionEntity), s);
                    Assert.IsFalse(world.EntityManager.HasComponent<NetworkId>(evt.ConnectionEntity), s);
                    Assert.AreEqual(ConnectionState.State.Handshake, world.EntityManager.GetComponentData<NetworkStreamConnection>(evt.ConnectionEntity).CurrentState, s);
                }

                // Client - Connected:
                testWorld.Tick(dt);
                for (var i = 0; i < testWorld.ClientWorlds.Length; i++)
                {
                    var world = testWorld.ClientWorlds[i];
                    var connectionEventsForClient = testWorld.GetSingleton<NetworkStreamDriver>(world).ConnectionEventsForTick;
                    Assert.AreEqual(1, connectionEventsForClient.Length, $"Client[{i}] should only know about self! First: {connectionEventsForClient.FirstOrDefault().ToFixedString()}!");
                    var evt = connectionEventsForClient[0];
                    var s = $"[{i}] {evt.ToFixedString()}";
                    Assert.IsTrue(evt.ConnectionId.IsCreated, s);
                    Assert.AreEqual(new NetworkId {Value = i + 1}, evt.Id, s);
                    Assert.AreEqual(ConnectionState.State.Connected, evt.State, s);
                    Assert.AreEqual(invalidDisconnectReason, evt.DisconnectReason, s);
                    var networkIdFromEntity = world.EntityManager.GetComponentData<NetworkId>(evt.ConnectionEntity);
                    Assert.AreEqual(networkIdFromEntity, evt.Id, s);

                    world.EntityManager.CompleteAllTrackedJobs();
                    Assert.AreEqual(ConnectionState.State.Connected, world.EntityManager.GetComponentData<NetworkStreamConnection>(evt.ConnectionEntity).CurrentState, s);
                }

                // Disconnect the last client, but do it via a server kick, so that we can also test the disconnect reason:
                {
                    var conn = testWorld.ServerWorld.EntityManager.GetComponentData<NetworkStreamConnection>(lastClientsConnectionEntity);
                    testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ServerWorld).ValueRW.DriverStore.Disconnect(conn);
                    testWorld.Tick(dt); // Disconnect is applied, event is raised later on the same frame (NetworkGroupCommandBufferSystem).
                }

                // Server should have 1 event (for the one player who DCs):
                connectionEventsForServerWorld = testWorld.GetSingleton<NetworkStreamDriver>(testWorld.ServerWorld).ConnectionEventsForTick;
                Assert.AreEqual(1, connectionEventsForServerWorld.Length, $"Server should know about all clients! FirstOrDefault: {connectionEventsForServerWorld.FirstOrDefault().ToFixedString()}!");
                {
                    var evt = connectionEventsForServerWorld[0];
                    var s = evt.ToFixedString().ToString();
                    Assert.IsTrue(evt.ConnectionId.IsCreated, s);
                    Assert.AreEqual(new NetworkId { Value = testWorld.ClientWorlds.Length }, evt.Id, s);
                    Assert.AreEqual(ConnectionState.State.Disconnected, evt.State, s);
                    Assert.AreEqual(NetworkStreamDisconnectReason.ConnectionClose, evt.DisconnectReason, s); // The server closed the connection.
                    Assert.IsFalse(testWorld.ServerWorld.EntityManager.Exists(evt.ConnectionEntity), s);
                }

                // Ensure ONLY that one client gets the disconnecting event... But ONLY once (on tick 0)...
                for (int tick = 0; tick < 3; tick++)
                {
                    for (int i = 0; i < testWorld.ClientWorlds.Length; i++)
                    {
                        var world = testWorld.ClientWorlds[i];
                        var connectionEventsForClient = testWorld.GetSingleton<NetworkStreamDriver>(world).ConnectionEventsForTick;
                        if (i >= testWorld.ClientWorlds.Length - 1)
                        {
                            if (tick == 0)
                            {
                                Assert.AreEqual(1, connectionEventsForClient.Length, $"Client[{i}] (on tick: {tick}) (the last client!) should know it DC'd, but no event!");
                                var evt = connectionEventsForClient[0];
                                var s = evt.ToFixedString().ToString();
                                Assert.IsTrue(evt.ConnectionId.IsCreated, $"Client[{i}] (on tick: {tick}) (the last client!) should have a valid ConnectionId, even though it has just been cleared.");
                                Assert.AreEqual(new NetworkId {Value = i + 1}, evt.Id, s);
                                Assert.AreEqual(ConnectionState.State.Disconnected, evt.State, s);
                                Assert.AreEqual(NetworkStreamDisconnectReason.ClosedByRemote, evt.DisconnectReason, $"Server closed us, so ClosedByRemote is expected. {s}!");
                                Assert.IsFalse(world.EntityManager.Exists(evt.ConnectionEntity), s);
                            }
                            else
                            {
                                Assert.AreEqual(0, connectionEventsForClient.Length, $"Client[{i}] (on tick: {tick}) should have no DC event raised, but raised {connectionEventsForClient.Length} events! FirstOrDefault: {connectionEventsForClient.FirstOrDefault().ToFixedString()}!");
                                world.EntityManager.CompleteAllTrackedJobs();
                                Assert.AreEqual(Entity.Null, testWorld.TryGetSingletonEntity<NetworkStreamConnection>(testWorld.ClientWorlds[i]), $"Client[{i}] (on tick: {tick}) should have no entity left!");
                            }
                        }
                        else
                        {
                            Assert.AreEqual(0, connectionEventsForClient.Length, $"Client[{i}] (on tick: {tick}) should have no DC event raised, but raised {connectionEventsForClient.Length} events! FirstOrDefault: {connectionEventsForClient.FirstOrDefault().ToFixedString()}!");
                            world.EntityManager.CompleteAllTrackedJobs();
                            var clientConn = testWorld.GetSingleton<NetworkStreamConnection>(testWorld.ClientWorlds[i]);
                            Assert.IsTrue(clientConn.Value.IsCreated, $"Client[{i}] (on tick: {tick}) should still have a Connection!");
                            Assert.AreEqual(ConnectionState.State.Connected, clientConn.CurrentState, $"Client[{i}] (on tick: {tick}) should still be connected!");
                        }
                    }
                    testWorld.Tick(dt);
                }

                // Now tick for a few more frames, ensuring there are no errant events:
                for (int i = 0; i < 3; i++)
                {
                    testWorld.Tick(dt);
                    connectionEventsForServerWorld = testWorld.GetSingleton<NetworkStreamDriver>(testWorld.ServerWorld).ConnectionEventsForTick;
                    Assert.AreEqual(0, connectionEventsForServerWorld.Length, $"Server(on tick: {i}) should have no events as nothing has happened! FirstOrDefault: {connectionEventsForServerWorld.FirstOrDefault().ToFixedString()}!");
                    using var connQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>());
                    connQuery.CompleteDependency();
                    foreach (var conn in connQuery.ToComponentDataArray<NetworkStreamConnection>(Allocator.Temp))
                    {
                        Assert.IsTrue(conn.Value.IsCreated, $"ServerWorld (on tick: {i}) should have valid NetworkConnection values!");
                        Assert.AreEqual(ConnectionState.State.Connected, conn.CurrentState, $"ServerWorld (on tick: {i}) did not expect any NetworkStreamConnection to be anything other than Connected!");
                    }
                    for (int j = 0; j < testWorld.ClientWorlds.Length; j++)
                    {
                        var world = testWorld.ClientWorlds[i];
                        var connectionEventsForClientWorld = testWorld.GetSingleton<NetworkStreamDriver>(testWorld.ServerWorld).ConnectionEventsForTick;
                        Assert.AreEqual(0, connectionEventsForClientWorld.Length, $"Client world [{j}] (on tick: {i}) should have no events as nothing has happened! FirstOrDefault: {connectionEventsForClientWorld.FirstOrDefault().ToFixedString()}!");
                        world.EntityManager.CompleteAllTrackedJobs();
                        if(i >= testWorld.ClientWorlds.Length - 1)
                            Assert.AreEqual(Entity.Null, testWorld.TryGetSingletonEntity<NetworkStreamConnection>(world), $"Client[{i}] (on tick: {i}) (the last client) should have no connection entity anymore!");
                        else Assert.AreEqual(ConnectionState.State.Connected, testWorld.GetSingleton<NetworkStreamConnection>(world).CurrentState, $"Client[{i}] (on tick: {i}) Should be connected!");
                    }
                }
            }
        }

        [Test]
        public void ConnectionStateIsCorrect()
        {
            const float dt = 1f / 60f;

            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 3);

                // Manually connect them:
                var ep = NetworkEndpoint.LoopbackIpv4;
                ep.Port = 7979;
                testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ServerWorld).ValueRW.Listen(ep);
                var connectionEntities = new Entity[testWorld.ClientWorlds.Length];
                for (int i = 0; i < testWorld.ClientWorlds.Length; ++i)
                {
                    var clientWorld = testWorld.ClientWorlds[i];
                    connectionEntities[i] = testWorld.GetSingletonRW<NetworkStreamDriver>(clientWorld).ValueRW.Connect(clientWorld.EntityManager, ep);
                    clientWorld.EntityManager.AddComponent<ConnectionState>(connectionEntities[i]);
                }
                testWorld.Tick(dt);

                // Client connecting:
                for (var i = 0; i < testWorld.ClientWorlds.Length; i++)
                {
                    var world = testWorld.ClientWorlds[i];
                    var connState = testWorld.GetSingleton<ConnectionState>(world);
                    Assert.AreEqual(ConnectionState.State.Connecting, connState.CurrentState);
                    Assert.AreEqual(default(NetworkStreamDisconnectReason), connState.DisconnectReason);
                }

                // Server should have all join events, so wait for it to get them:
                NativeArray<NetCodeConnectionEvent>.ReadOnly connectionEventsForServerWorld;
                int counter = 0;
                do
                {
                    connectionEventsForServerWorld = testWorld.GetSingleton<NetworkStreamDriver>(testWorld.ServerWorld).ConnectionEventsForTick;
                    if (connectionEventsForServerWorld.Length == 0)
                        testWorld.Tick(dt);
                } while (++counter < 2);

                Assert.AreEqual(testWorld.ClientWorlds.Length, connectionEventsForServerWorld.Length, $"Server should know about all clients: NO Handshake, ONLY Connected. FirstOrDefault: {connectionEventsForServerWorld.FirstOrDefault().ToFixedString().ToString()}");
                Entity lastClientsConnectionEntity = connectionEventsForServerWorld.Last().ConnectionEntity;

                // Check server status:
                {
                    using var serverQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
                    Assert.AreEqual(testWorld.ClientWorlds.Length, serverQuery.CalculateEntityCount());
                    testWorld.ServerWorld.EntityManager.AddComponent<ConnectionState>(serverQuery);
                }

                // Ensure each client gets it's own Handshake & Connection events (i.e. it's 'self' events):
                do
                {
                    var connectionEventsForClient = testWorld.GetSingleton<NetworkStreamDriver>(testWorld.ClientWorlds[0]).ConnectionEventsForTick;
                    if (connectionEventsForClient.Length == 0)
                        testWorld.Tick(dt);
                } while (++counter < 2);

                // Client - Handshakes:
                for (var i = 0; i < testWorld.ClientWorlds.Length; i++)
                {
                    var world = testWorld.ClientWorlds[i];
                    var connState = testWorld.GetSingleton<ConnectionState>(world);
                    Assert.AreEqual(ConnectionState.State.Handshake, connState.CurrentState);
                    // We don't test the NetworkId, nor DisconnectReason, as the user can technically pass any values here.
                }

                // Client - Connected:
                testWorld.Tick(dt);
                for (var i = 0; i < testWorld.ClientWorlds.Length; i++)
                {
                    var world = testWorld.ClientWorlds[i];
                    var connState = testWorld.GetSingleton<ConnectionState>(world);
                    Assert.AreEqual(ConnectionState.State.Connected, connState.CurrentState);
                    Assert.AreEqual(i + 1, connState.NetworkId);
                    Assert.AreEqual(default(NetworkStreamDisconnectReason), connState.DisconnectReason);
                }

                // Disconnect the last client, but do it via a server kick, so that we can also test the disconnect reason:
                {
                    var conn = testWorld.ServerWorld.EntityManager.GetComponentData<NetworkStreamConnection>(lastClientsConnectionEntity);
                    testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ServerWorld).ValueRW.DriverStore.Disconnect(conn);
                    testWorld.Tick(dt); // Disconnect is applied, event is raised later on the same frame (NetworkGroupCommandBufferSystem).
                }

                // Check server:
                {
                    using var serverQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ConnectionState>());
                    Assert.AreEqual(testWorld.ClientWorlds.Length, serverQuery.CalculateEntityCount());
                    var connectionStates = serverQuery.ToComponentDataArray<ConnectionState>(Allocator.Temp);
                    for (var i = 0; i < connectionStates.Length; i++)
                    {
                        var connState = connectionStates[i];
                        if (i < connectionStates.Length - 1)
                        {
                            Assert.AreEqual(ConnectionState.State.Connected, connState.CurrentState);
                            Assert.AreEqual(i + 1, connState.NetworkId);
                            Assert.AreEqual(default(NetworkStreamDisconnectReason), connState.DisconnectReason);
                        }
                        else
                        {
                            Assert.AreEqual(ConnectionState.State.Disconnected, connState.CurrentState);
                            Assert.AreEqual(i + 1, connState.NetworkId);
                            Assert.AreEqual(NetworkStreamDisconnectReason.ConnectionClose, connState.DisconnectReason);
                        }
                    }
                }

                // Check clients:
                for (var i = 0; i < testWorld.ClientWorlds.Length; i++)
                {
                    var world = testWorld.ClientWorlds[i];
                    var connState = testWorld.GetSingleton<ConnectionState>(world);
                    if (i < testWorld.ClientWorlds.Length - 1)
                    {
                        Assert.AreEqual(ConnectionState.State.Connected, connState.CurrentState);
                        Assert.AreEqual(i + 1, connState.NetworkId);
                        Assert.AreEqual(default(NetworkStreamDisconnectReason), connState.DisconnectReason);
                    }
                    else
                    {
                        Assert.AreEqual(ConnectionState.State.Disconnected, connState.CurrentState);
                        Assert.AreEqual(i + 1, connState.NetworkId);
                        Assert.AreEqual(NetworkStreamDisconnectReason.ClosedByRemote, connState.DisconnectReason);
                    }
                }
            }
        }
    }

    public class VersionTests
    {
        [Test]
        public void SameVersion_ConnectSuccessfully()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                //Don't tick the world after creation. that will generate the default protocol version.
                //We want to use a custom one here
                testWorld.CreateWorlds(true, 1, false);
                var serverVersion = testWorld.ServerWorld.EntityManager.CreateEntity(typeof(NetworkProtocolVersion));
                testWorld.ServerWorld.EntityManager.SetComponentData(serverVersion, new NetworkProtocolVersion
                {
                    NetCodeVersion = 1,
                    GameVersion = 0,
                    RpcCollectionVersion = 1,
                    ComponentCollectionVersion = 1
                });
                var clientVersion = testWorld.ClientWorlds[0].EntityManager.CreateEntity(typeof(NetworkProtocolVersion));
                testWorld.ClientWorlds[0].EntityManager.SetComponentData(clientVersion, new NetworkProtocolVersion
                {
                    NetCodeVersion = 1,
                    GameVersion = 0,
                    RpcCollectionVersion = 1,
                    ComponentCollectionVersion = 1
                });

                var ep = NetworkEndpoint.LoopbackIpv4;
                ep.Port = 7979;
                testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ServerWorld).ValueRW.Listen(ep);
                testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ClientWorlds[0]).ValueRW.Connect(testWorld.ClientWorlds[0].EntityManager, ep);

                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(16f/1000f);

                using var query = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>());
                Assert.AreEqual(1, query.CalculateEntityCount());
            }
        }

        [Test]
        public void DifferentVersions_AreDisconnnected()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                //Don't tick the world after creation. that will generate the default protocol version.
                //We want to use a custom one here
                testWorld.CreateWorlds(true, 1, false);
                var serverVersion = testWorld.ServerWorld.EntityManager.CreateEntity(typeof(NetworkProtocolVersion));
                testWorld.ServerWorld.EntityManager.SetComponentData(serverVersion, new NetworkProtocolVersion
                {
                    NetCodeVersion = 1,
                    GameVersion = 0,
                    RpcCollectionVersion = 1,
                    ComponentCollectionVersion = 1
                });
                var ep = NetworkEndpoint.LoopbackIpv4;
                ep.Port = 7979;
                testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ServerWorld).ValueRW.Listen(ep);

                //Different NetCodeVersion
                var clientVersion = testWorld.ClientWorlds[0].EntityManager.CreateEntity(typeof(NetworkProtocolVersion));
                using var query = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>());
                testWorld.ClientWorlds[0].EntityManager.SetComponentData(clientVersion, new NetworkProtocolVersion
                {
                    NetCodeVersion = 2,
                    GameVersion = 0,
                    RpcCollectionVersion = 1,
                    ComponentCollectionVersion = 1
                });
                testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ClientWorlds[0]).ValueRW.Connect(testWorld.ClientWorlds[0].EntityManager, ep);

                // The ordering of the protocol version error messages can be scrambled, so we can't log.expect exact ordering
                LogAssert.ignoreFailingMessages = true;
                LogAssert.Expect(LogType.Error, new Regex("\\[(.*)\\] RpcSystem received bad protocol version from NetworkConnection\\[id0,v\\d\\]"));
                LogAssert.Expect(LogType.Error, new Regex("\\[(.*)\\] RpcSystem received bad protocol version from NetworkConnection\\[id0,v\\d\\]"));
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(16f/1000f);

                Assert.AreEqual(0, query.CalculateEntityCount());
                //Different GameVersion
                testWorld.ClientWorlds[0].EntityManager.SetComponentData(clientVersion, new NetworkProtocolVersion
                {
                    NetCodeVersion = 1,
                    GameVersion = 1,
                    RpcCollectionVersion = 1,
                    ComponentCollectionVersion = 1
                });
                testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ClientWorlds[0]).ValueRW.Connect(testWorld.ClientWorlds[0].EntityManager, ep);

                LogAssert.Expect(LogType.Error, new Regex("\\[(.*)\\] RpcSystem received bad protocol version from NetworkConnection\\[id0,v\\d\\]"));
                LogAssert.Expect(LogType.Error, new Regex("\\[(.*)\\] RpcSystem received bad protocol version from NetworkConnection\\[id0,v\\d\\]"));
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(16f/1000f);

                Assert.AreEqual(0, query.CalculateEntityCount());
                //Different Rpcs
                testWorld.ClientWorlds[0].EntityManager.SetComponentData(clientVersion, new NetworkProtocolVersion
                {
                    NetCodeVersion = 1,
                    GameVersion = 0,
                    RpcCollectionVersion = 2,
                    ComponentCollectionVersion = 1
                });
                testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ClientWorlds[0]).ValueRW.Connect(testWorld.ClientWorlds[0].EntityManager, ep);

                LogAssert.Expect(LogType.Error, new Regex("\\[(.*)\\] RpcSystem received bad protocol version from NetworkConnection\\[id0,v\\d\\]"));
                LogAssert.Expect(LogType.Error, new Regex("\\[(.*)\\] RpcSystem received bad protocol version from NetworkConnection\\[id0,v\\d\\]"));
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(16f/1000f);

                Assert.AreEqual(0, query.CalculateEntityCount());

                //Different Ghost
                testWorld.ClientWorlds[0].EntityManager.SetComponentData(clientVersion, new NetworkProtocolVersion
                {
                    NetCodeVersion = 1,
                    GameVersion = 0,
                    RpcCollectionVersion = 1,
                    ComponentCollectionVersion = 2
                });
                testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ClientWorlds[0]).ValueRW.Connect(testWorld.ClientWorlds[0].EntityManager, ep);

                LogAssert.Expect(LogType.Error, new Regex("\\[(.*)\\] RpcSystem received bad protocol version from NetworkConnection\\[id0,v\\d\\]"));
                LogAssert.Expect(LogType.Error, new Regex("\\[(.*)\\] RpcSystem received bad protocol version from NetworkConnection\\[id0,v\\d\\]"));
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(16f/1000f);

                Assert.AreEqual(0, query.CalculateEntityCount());
            }
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void ProtocolVersionDebugInfoAppearsOnMismatch(bool debugServer)
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                // Only print the protocol version debug errors in one world, so the output can be deterministically validated
                // if it's printed in both worlds (client and server) the output can interweave and log checks will fail
                testWorld.EnableLogsOnServer = debugServer;
                testWorld.EnableLogsOnClients = !debugServer;
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 1, false);

                float dt = 16f / 1000f;
                var entity = testWorld.ClientWorlds[0].EntityManager.CreateEntity(ComponentType.ReadWrite<GameProtocolVersion>());
                testWorld.ClientWorlds[0].EntityManager.SetComponentData(entity, new GameProtocolVersion(){Version = 9000});
                testWorld.Tick(dt);
                testWorld.Tick(dt);

                var ep = NetworkEndpoint.LoopbackIpv4;
                ep.Port = 7979;
                testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ServerWorld).ValueRW.Listen(ep);
                testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ClientWorlds[0]).ValueRW.Connect(testWorld.ClientWorlds[0].EntityManager, ep);

                // This error obviously logs twice, so expecting only once doesn't work.
                LogExpectProtocolError(testWorld, testWorld.ServerWorld, debugServer);

                // Allow disconnect to happen
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(16f/1000f);

                // Verify client connection is disconnected
                using var query = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>());
                Assert.AreEqual(0, query.CalculateEntityCount());
            }
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void DisconnectEventAndRPCVersionErrorProcessedInSameFrame(bool checkServer)
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                // Only print the protocol version debug errors in one world, so the output can be deterministically validated
                // if it's printed in both worlds (client and server) the output can interweave and log checks will fail
                testWorld.EnableLogsOnServer = checkServer;
                testWorld.EnableLogsOnClients = !checkServer;
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 1, false);

                float dt = 16f / 1000f;
                var entity = testWorld.ClientWorlds[0].EntityManager.CreateEntity(ComponentType.ReadWrite<GameProtocolVersion>());
                testWorld.ClientWorlds[0].EntityManager.SetComponentData(entity, new GameProtocolVersion(){Version = 9000});
                testWorld.Tick(dt);

                var ep = NetworkEndpoint.LoopbackIpv4;
                ep.Port = 7979;
                testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ServerWorld).ValueRW.Listen(ep);
                testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ClientWorlds[0]).ValueRW.Connect(testWorld.ClientWorlds[0].EntityManager, ep);
                testWorld.Tick(dt);

                LogExpectProtocolError(testWorld, testWorld.ServerWorld, checkServer);
                for (int i = 0; i < 8; ++i)
                    testWorld.Tick(dt);

                // Verify client connection is disconnected
                using var query = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>());
                Assert.AreEqual(0, query.CalculateEntityCount());
            }
        }

        void LogExpectProtocolError(NetCodeTestWorld testWorld, World world, bool checkServer)
        {
            LogAssert.Expect(LogType.Error, new Regex($"\\[{(checkServer ? "Server" : "Client")}Test(.*)\\] RpcSystem received bad protocol version from NetworkConnection\\[id0,v1\\]"
                                                      + $"\nLocal protocol: NetCode=1 Game={(checkServer ? "0" : "9000")} RpcCollection=(\\d+) ComponentCollection=(\\d+)"
                                                      + $"\nRemote protocol: NetCode=1 Game={(!checkServer ? "0" : "9000")} RpcCollection=(\\d+) ComponentCollection=(\\d+)"));
            var rpcs = testWorld.GetSingleton<RpcCollection>(world).Rpcs;
            LogAssert.Expect(LogType.Error, "RPC List (for above 'bad protocol version' error): " + rpcs.Length);
            for (int i = 0; i < rpcs.Length; ++i)
                LogAssert.Expect(LogType.Error, new Regex("Unity.NetCode"));
            using var collection = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostCollection>());
            var serializers = world.EntityManager.GetBuffer<GhostComponentSerializer.State>(collection.ToEntityArray(Allocator.Temp)[0]);
            LogAssert.Expect(LogType.Error, "Component serializer data (for above 'bad protocol version' error): " + serializers.Length);
            for (int i = 0; i < serializers.Length; ++i)
                LogAssert.Expect(LogType.Error, new Regex("Type:"));
        }

        public class TestConverter : TestNetCodeAuthoring.IConverter
        {
            public void Bake(GameObject gameObject, IBaker baker)
            {
                var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
                baker.AddComponent(entity, new GhostOwner());
                baker.AddComponent(entity, new GhostGenTestUtils.GhostGenTestType_IComponentData());
                // TODO (flag in review): Add the other types (Input, RPC etc) to this test
            }
        }
        [Test]
        public void GhostCollectionGenerateSameHashOnClientAndServer()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                var ghost1 = new GameObject();
                ghost1.AddComponent<TestNetCodeAuthoring>().Converter = new TestConverter();
                ghost1.AddComponent<GhostAuthoringComponent>().DefaultGhostMode = GhostMode.Predicted;
                var ghost2 = new GameObject();
                ghost2.AddComponent<TestNetCodeAuthoring>().Converter = new TestConverter();
                ghost2.AddComponent<GhostAuthoringComponent>().DefaultGhostMode = GhostMode.Interpolated;

                testWorld.Bootstrap(true);
                testWorld.CreateGhostCollection(ghost1, ghost2);

                testWorld.CreateWorlds(true, 1);
                float frameTime = 1.0f / 60.0f;
                var serverCollectionSingleton = testWorld.TryGetSingletonEntity<GhostCollection>(testWorld.ServerWorld);
                var clientCollectionSingleton = testWorld.TryGetSingletonEntity<GhostCollection>(testWorld.ClientWorlds[0]);
                //First tick: compute on both client and server the ghost collection hash
                testWorld.Tick(frameTime);
                Assert.AreEqual(GhostCollectionSystem.CalculateComponentCollectionHash(testWorld.ServerWorld.EntityManager.GetBuffer<GhostComponentSerializer.State>(serverCollectionSingleton)),
                    GhostCollectionSystem.CalculateComponentCollectionHash(testWorld.ClientWorlds[0].EntityManager.GetBuffer<GhostComponentSerializer.State>(clientCollectionSingleton)));

                // compare the list of loaded prefabs
                Assert.AreNotEqual(Entity.Null, serverCollectionSingleton);
                Assert.AreNotEqual(Entity.Null, clientCollectionSingleton);
                var serverCollection = testWorld.ServerWorld.EntityManager.GetBuffer<GhostCollectionPrefab>(serverCollectionSingleton);
                var clientCollection = testWorld.ClientWorlds[0].EntityManager.GetBuffer<GhostCollectionPrefab>(clientCollectionSingleton);
                Assert.AreEqual(serverCollection.Length, clientCollection.Length);
                for (int i = 0; i < serverCollection.Length; ++i)
                {
                    Assert.AreEqual(serverCollection[i].GhostType, clientCollection[i].GhostType);
                    Assert.AreEqual(serverCollection[i].Hash, clientCollection[i].Hash);
                }

                //Check that and server can connect (same component hash)
                testWorld.Connect(frameTime);

                testWorld.GoInGame();
                for(int i=0;i<10;++i)
                    testWorld.Tick(frameTime);

                Assert.IsTrue(testWorld.TryGetSingletonEntity<NetworkId>(testWorld.ClientWorlds[0]) != Entity.Null);
            }
        }

        [Test]
        public void DefaultVariantHashAreCalculatedCorrectly()
        {
            var realHash = GhostVariantsUtility.UncheckedVariantHash(typeof(LocalTransform).FullName, typeof(LocalTransform).FullName);
            Assert.AreEqual(realHash, GhostVariantsUtility.CalculateVariantHashForComponent(ComponentType.ReadWrite<LocalTransform>()));
            var compName = new FixedString512Bytes(typeof(LocalTransform).FullName);
            Assert.AreEqual(realHash, GhostVariantsUtility.UncheckedVariantHash(compName, compName));
            Assert.AreEqual(realHash, GhostVariantsUtility.UncheckedVariantHash(compName, ComponentType.ReadWrite<LocalTransform>()));
            Assert.AreEqual(realHash, GhostVariantsUtility.UncheckedVariantHashNBC(typeof(LocalTransform), ComponentType.ReadWrite<LocalTransform>()));
        }
        [Test]
        public void tVariantHashAreCalculatedCorrectly()
        {
            var realHash = GhostVariantsUtility.UncheckedVariantHash(typeof(TransformDefaultVariant).FullName, typeof(LocalTransform).FullName);
            var compName = new FixedString512Bytes(typeof(LocalTransform).FullName);
            var variantName = new FixedString512Bytes(typeof(TransformDefaultVariant).FullName);
            Assert.AreEqual(realHash, GhostVariantsUtility.UncheckedVariantHash(variantName, compName));
            Assert.AreEqual(realHash, GhostVariantsUtility.UncheckedVariantHash(variantName, ComponentType.ReadWrite<LocalTransform>()));
            Assert.AreEqual(realHash, GhostVariantsUtility.UncheckedVariantHashNBC(typeof(TransformDefaultVariant), ComponentType.ReadWrite<LocalTransform>()));
        }
        [Test]
        public void RuntimeAndCodeGeneratedVariantHashMatch()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 1);
                //Grab all the serializers we have and recalculate locally the hash and verify they match.
                //TODO: to have a complete end-to-end test we have a missing piece: we don't have the original variant System.Type.
                //Either we add that in code-gen (as string, for test/debug purpose only) or we need to store somehow the type
                //when we register the serialiser itself. It is not a priority, but great to have.
                //Right now I exposed a a VariantTypeFullHashName in the serialiser that allow at lest to do the most
                //important verification: the hash matches!
                var data = testWorld.GetSingleton<GhostComponentSerializerCollectionData>(testWorld.ServerWorld);
                for (int i = 0; i < data.Serializers.Length; ++i)
                {
                    var variantTypeHash = data.Serializers.ElementAt(i).VariantTypeFullNameHash;
                    var componentType = data.Serializers.ElementAt(i).ComponentType;
                    var variantHash = GhostVariantsUtility.UncheckedVariantHash(variantTypeHash, componentType);
                    Assert.AreEqual(data.Serializers.ElementAt(i).VariantHash, variantHash,
                        $"Expect variant hash for code-generated serializer is identical to the" +
                        $"calculated at runtime for component {componentType.GetManagedType().FullName}." +
                        $"generated: {data.Serializers.ElementAt(i).VariantHash} runtime:{variantHash}");
                }
            }
        }
    }
}
