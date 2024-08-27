using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using Unity.Collections;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using Unity.NetCode.Editor;
using UnityEngine;
#endif
#if USING_UNITY_LOGGING
using Unity.Logging;
using Unity.Logging.Sinks;
#endif

namespace Unity.NetCode.Tests
{
    public struct NetCodeTestPrefabCollection : IComponentData
    {}
    public struct NetCodeTestPrefab : IBufferElementData
    {
        public Entity Value;
    }

    public class NetCodeTestWorld : IDisposable, INetworkStreamDriverConstructor
    {
        /// <summary>True if you want to forward all netcode logs from the server, to allow <see cref="LogAssert"/> usage.</summary>
        /// <remarks>Defaults to true. <seealso cref="DebugPackets"/> and <seealso cref="LogLevel"/>.</remarks>
        public bool EnableLogsOnServer = true;
        /// <summary>True if you want to forward all netcode logs from the client, to allow <see cref="LogAssert"/> usage.</summary>
        /// <remarks>Defaults to true. <seealso cref="DebugPackets"/> and <seealso cref="LogLevel"/>.</remarks>
        public bool EnableLogsOnClients = true;

        /// <summary>Enable packet dumping in tests? Useful to ensure serialization doesn't fail.</summary>
        /// <remarks>Note: Packet dump files will not be cleaned up!</remarks>
        public bool DebugPackets = false;
        /// <summary>If you want to test extremely verbose logs, you can modify this flag.</summary>
        public NetDebug.LogLevelType LogLevel = NetDebug.LogLevelType.Notify;

        static readonly ProfilerMarker k_TickServerInitializationSystem = new ProfilerMarker("TickServerInitializationSystem");
        static readonly ProfilerMarker k_TickClientInitializationSystem = new ProfilerMarker("TickClientInitializationSystem");
        static readonly ProfilerMarker k_TickServerSimulationSystem = new ProfilerMarker("TickServerSimulationSystem");
        static readonly ProfilerMarker k_TickClientSimulationSystem = new ProfilerMarker("TickClientSimulationSystem");
        static readonly ProfilerMarker k_TickClientPresentationSystem = new ProfilerMarker("TickClientPresentationSystem");

        public World DefaultWorld => m_DefaultWorld;
        public World ServerWorld => m_ServerWorld;
        public World[] ClientWorlds => m_ClientWorlds;

        private World m_DefaultWorld;
        private World[] m_ClientWorlds;
        private World m_ServerWorld;
        private ushort m_OldBootstrapAutoConnectPort;
        private bool m_DefaultWorldInitialized;
        private double m_ElapsedTime;
        public int DriverFixedTime = 16;
        public int DriverSimulatedDelay = 0;
        public int DriverSimulatedJitter = 0;
        public int DriverSimulatedDrop = 0;
        public int UseMultipleDrivers = 0;
        public int UseFakeSocketConnection = 1;
        private int WorldCreationIndex = 0;

        public int[] DriverFuzzFactor;
        public int DriverFuzzOffset = 0;
        public uint DriverRandomSeed = 0;

#if UNITY_EDITOR
        private List<GameObject> m_GhostCollection;
        private BlobAssetStore m_BlobAssetStore;
#endif

        /// <summary>Configure how logging should occur in tests. We apply <see cref="LogLevel"/> and <see cref="DebugPackets"/> here.</summary>
        /// <param name="world">World to apply this config on.</param>
        private void SetupNetDebugConfig(World world)
        {
            var shouldLog = (world.IsServer() && EnableLogsOnServer) || (world.IsClient() && EnableLogsOnClients);
            world.EntityManager.CreateSingleton(new NetCodeDebugConfig
            {
                // Hack essentially disabling all logging for this world, as we should never have exceptions going via this logger anyway.
                LogLevel = shouldLog ? LogLevel : NetDebug.LogLevelType.Exception,
                DumpPackets = DebugPackets,
            });
        }

        public NetCodeTestWorld()
        {
#if UNITY_EDITOR

            // Not having a default world means RegisterUnloadOrPlayModeChangeShutdown has not been called which causes memory leaks
            DefaultWorldInitialization.DefaultLazyEditModeInitialize();
#endif
            m_OldBootstrapAutoConnectPort = ClientServerBootstrap.AutoConnectPort;
            ClientServerBootstrap.AutoConnectPort = 0;
            m_DefaultWorld = new World("NetCodeTest");
            m_ElapsedTime = 42;
        }

        public void Dispose()
        {
            if (m_ClientWorlds != null)
            {
                for (int i = 0; i < m_ClientWorlds.Length; ++i)
                {
                    if (m_ClientWorlds[i] != null)
                    {
                        m_ClientWorlds[i].Dispose();
                    }
                }
            }

            if (m_ServerWorld != null)
                m_ServerWorld.Dispose();
            if (m_DefaultWorld != null)
                m_DefaultWorld.Dispose();
            m_ClientWorlds = null;
            m_ServerWorld = null;
            m_DefaultWorld = null;
            ClientServerBootstrap.AutoConnectPort = m_OldBootstrapAutoConnectPort;

#if UNITY_EDITOR
            if (m_GhostCollection != null)
                m_BlobAssetStore.Dispose();
#endif
        }

        public void DisposeAllClientWorlds()
        {
            for (int i = 0; i < m_ClientWorlds.Length; ++i)
            {
                m_ClientWorlds[i].Dispose();
            }

            m_ClientWorlds = null;
        }

        public void DisposeServerWorld()
        {
            m_ServerWorld.Dispose();
            m_ServerWorld = null;
        }

        public void DisposeDefaultWorld()
        {
            m_DefaultWorld.Dispose();
            m_DefaultWorld = null;
        }

        public void SetServerTick(NetworkTick tick)
        {
            var ent = TryGetSingletonEntity<NetworkTime>(m_ServerWorld);
            var networkTime = m_ServerWorld.EntityManager.GetComponentData<NetworkTime>(ent);
            networkTime.ServerTick = tick;
            m_ServerWorld.EntityManager.SetComponentData(ent, networkTime);
        }

        public NetworkTime GetNetworkTime(World world)
        {
            var ent = TryGetSingletonEntity<NetworkTime>(world);
            return world.EntityManager.GetComponentData<NetworkTime>(ent);
        }

        private static IReadOnlyList<Type> s_AllClientSystems;
        private static IReadOnlyList<Type> s_AllThinClientSystems;
        private static IReadOnlyList<Type> s_AllServerSystems;

        private static List<Type> m_ControlSystems;
        private static List<Type> m_ClientSystems;
        private static List<Type> m_ThinClientSystems;
        private static List<Type> m_ServerSystems;

        public List<Type> TestSpecificAdditionalSystems = new List<Type>(8);
        public List<string> TestSpecificAdditionalAssemblies = new List<string>(8);

        int m_NumClients = -1;

        private static bool IsFromNetCodeAssembly(Type sys)
        {
            return sys.Assembly.FullName.StartsWith("Unity.NetCode,", StringComparison.Ordinal) ||
                sys.Assembly.FullName.StartsWith("Unity.Entities,", StringComparison.Ordinal) ||
                sys.Assembly.FullName.StartsWith("Unity.Transforms,", StringComparison.Ordinal) ||
                sys.Assembly.FullName.StartsWith("Unity.Scenes,", StringComparison.Ordinal) ||
                sys.Assembly.FullName.StartsWith("Unity.NetCode.EditorTests,", StringComparison.Ordinal) ||
                sys.Assembly.FullName.StartsWith("Unity.NetCode.TestsUtils,", StringComparison.Ordinal) ||
                sys.Assembly.FullName.StartsWith("Unity.NetCode.Physics.EditorTests,", StringComparison.Ordinal) ||
                typeof(IGhostComponentSerializerRegistration).IsAssignableFrom(sys);
        }

        private bool IsFromTestSpecificAdditionalAssembly(Type sys)
        {
            var sysAssemblyFullName = sys.Assembly.FullName;
            foreach (var extraNetcodeAssembly in TestSpecificAdditionalAssemblies)
            {
                if (sysAssemblyFullName.StartsWith(extraNetcodeAssembly, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public void Bootstrap(bool includeNetCodeSystems, params Type[] userSystems)
        {
            m_ControlSystems = new List<Type>(256);
            m_ClientSystems = new List<Type>(256);
            m_ThinClientSystems = new List<Type>(256);
            m_ServerSystems = new List<Type>(256);
#if !UNITY_SERVER
            m_ControlSystems.Add(typeof(TickClientInitializationSystem));
            m_ControlSystems.Add(typeof(TickClientSimulationSystem));
            m_ControlSystems.Add(typeof(TickClientPresentationSystem));
#endif
#if !UNITY_CLIENT
            m_ControlSystems.Add(typeof(TickServerInitializationSystem));
            m_ControlSystems.Add(typeof(TickServerSimulationSystem));
#endif
            m_ControlSystems.Add(typeof(DriverMigrationSystem));

            s_AllClientSystems ??= DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ClientSimulation);
            s_AllThinClientSystems ??= DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ThinClientSimulation);
            s_AllServerSystems ??= DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ServerSimulation);

            bool IncludeNetcodeSystemsFilter(Type x) => IsFromNetCodeAssembly(x) || IsFromTestSpecificAdditionalAssembly(x);

            Func<Type, bool> filter = includeNetCodeSystems
                ? IncludeNetcodeSystemsFilter
                : IsFromTestSpecificAdditionalAssembly;

            m_ClientSystems.AddRange(s_AllClientSystems.Where(filter));
            m_ThinClientSystems.AddRange(s_AllThinClientSystems.Where(filter));
            m_ServerSystems.AddRange(s_AllServerSystems.Where(filter));

            m_ClientSystems.Add(typeof(Unity.Entities.UpdateWorldTimeSystem));
            m_ThinClientSystems.Add(typeof(Unity.Entities.UpdateWorldTimeSystem));
            m_ServerSystems.Add(typeof(Unity.Entities.UpdateWorldTimeSystem));

            m_ClientSystems.AddRange(TestSpecificAdditionalSystems);
            m_ThinClientSystems.AddRange(TestSpecificAdditionalSystems);
            m_ServerSystems.AddRange(TestSpecificAdditionalSystems);

            foreach (var sys in userSystems)
            {
                var flags = WorldSystemFilterFlags.Default;
                var attrs = TypeManager.GetSystemAttributes(sys, typeof(WorldSystemFilterAttribute));
                if (attrs != null && attrs.Length == 1)
                    flags = ((WorldSystemFilterAttribute) attrs[0]).FilterFlags;
                var grp = sys;
                while ((flags & WorldSystemFilterFlags.Default) != 0)
                {
                    attrs = TypeManager.GetSystemAttributes(grp, typeof(UpdateInGroupAttribute));
                    if (attrs != null && attrs.Length == 1)
                        grp = ((UpdateInGroupAttribute) attrs[0]).GroupType;
                    else
                    {
                        flags &= ~WorldSystemFilterFlags.Default;
                        flags |= WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation;
                        break;
                    }

                    attrs = TypeManager.GetSystemAttributes(grp, typeof(WorldSystemFilterAttribute));
                    if (attrs != null && attrs.Length == 1)
                    {
                        flags &= ~WorldSystemFilterFlags.Default;
                        flags |= ((WorldSystemFilterAttribute) attrs[0]).ChildDefaultFilterFlags;
                    }
                }

                if ((flags & WorldSystemFilterFlags.ClientSimulation) != 0)
                    m_ClientSystems.Add(sys);
                if ((flags & WorldSystemFilterFlags.ThinClientSimulation) != 0)
                    m_ThinClientSystems.Add(sys);
                if ((flags & WorldSystemFilterFlags.ServerSimulation) != 0)
                    m_ServerSystems.Add(sys);
            }
        }

        public void CreateWorlds(bool server, int numClients, bool tickWorldAfterCreation = true, bool useThinClients = false)
        {
            m_NumClients = numClients;
            var oldConstructor = NetworkStreamReceiveSystem.DriverConstructor;
            NetworkStreamReceiveSystem.DriverConstructor = this;
#if UNITY_EDITOR || NETCODE_DEBUG
            var oldDebugPort = GhostStatsConnection.Port;
            GhostStatsConnection.Port = 0;
#endif
            if (!m_DefaultWorldInitialized)
            {
                TypeManager.SortSystemTypesInCreationOrder(m_ControlSystems); // Ensure CreationOrder is respected.
                DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(m_DefaultWorld,
                    m_ControlSystems);
                m_DefaultWorldInitialized = true;
            }

            var testMethodName = NUnit.Framework.TestContext.CurrentContext.Test.MethodName;
            if (server)
            {
                if (m_ServerWorld != null)
                    throw new InvalidOperationException("Server world already created");
                m_ServerWorld = CreateServerWorld($"ServerTest-{testMethodName}");
#if UNITY_EDITOR
                BakeGhostCollection(m_ServerWorld);
#endif

                SetupNetDebugConfig(m_ServerWorld);
            }

            if (numClients > 0)
            {
                if (m_ClientWorlds != null)
                    throw new InvalidOperationException("Client worlds already created");
                WorldCreationIndex = 0;
                m_ClientWorlds = new World[numClients];
                for (int i = 0; i < numClients; ++i)
                {
                    try
                    {
                        WorldCreationIndex = i;

                        m_ClientWorlds[i] = CreateClientWorld($"ClientTest{i}-{testMethodName}", useThinClients);

                        SetupNetDebugConfig(m_ClientWorlds[i]);
                    }
                    catch (Exception)
                    {
                        m_ClientWorlds = null;
                        throw;
                    }
#if UNITY_EDITOR
                    BakeGhostCollection(m_ClientWorlds[i]);
#endif
                }
            }

#if UNITY_EDITOR || NETCODE_DEBUG
            GhostStatsConnection.Port = oldDebugPort;
#endif
            NetworkStreamReceiveSystem.DriverConstructor = oldConstructor;

            //Run 1 tick so that all the ghost collection and the ghost collection component run once.
            if (tickWorldAfterCreation)
                Tick(1.0f / 60.0f);

            TrySetSuppressRunInBackgroundWarning(true);
        }

        /// <summary>
        /// Tests will fail on CI due to `runInBackground = false`, so we must suppress the warning:
        /// Note that if netcode systems don't exist (i.e. no NetDebug), no suppression is necessary.
        /// </summary>
        /// <param name="suppress"></param>
        /// <remarks>Called multiple times as some tests don't tick until they've established a collection.</remarks>
        public bool TrySetSuppressRunInBackgroundWarning(bool suppress)
        {
            var success = TryGetSingletonEntity<NetDebug>(ServerWorld) != default;
            if (success)
                GetSingletonRW<NetDebug>(ServerWorld).ValueRW.SuppressApplicationRunInBackgroundWarning = suppress;

            if (ClientWorlds != null)
            {
                foreach (var clientWorld in ClientWorlds)
                {
                    success &= TryGetSingletonEntity<NetDebug>(clientWorld) != default;
                    if(success)
                        GetSingletonRW<NetDebug>(clientWorld).ValueRW.SuppressApplicationRunInBackgroundWarning = suppress;
                }
            }
            return success;
        }

        private World CreateServerWorld(string name, World world = null)
        {
            if (world == null)
                world = new World(name, WorldFlags.GameServer);
            TypeManager.SortSystemTypesInCreationOrder(m_ServerSystems); // Ensure CreationOrder is respected.
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, m_ServerSystems);
            var initializationGroup = world.GetExistingSystemManaged<InitializationSystemGroup>();
            var simulationGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            var presentationGroup = world.GetExistingSystemManaged<PresentationSystemGroup>();
            world.GetExistingSystemManaged<UpdateWorldTimeSystem>().Enabled = false;
#if !UNITY_SERVER
            var initializationTickSystem = m_DefaultWorld.GetExistingSystemManaged<TickClientInitializationSystem>();
            var simulationTickSystem = m_DefaultWorld.GetExistingSystemManaged<TickClientSimulationSystem>();
            var presentationTickSystem = m_DefaultWorld.GetExistingSystemManaged<TickClientPresentationSystem>();
            initializationTickSystem.AddSystemGroupToTickList(initializationGroup);
            simulationTickSystem.AddSystemGroupToTickList(simulationGroup);
            presentationTickSystem.AddSystemGroupToTickList(presentationGroup);
#endif
            ClientServerBootstrap.ServerWorlds.Add(world);
            return world;
        }

        private World CreateClientWorld(string name, bool thinClient, World world = null)
        {
            if (world == null)
                world = new World(name, thinClient ? WorldFlags.GameThinClient : WorldFlags.GameClient);

            // TODO: GameThinClient for ThinClientSystem for ultra thin
            TypeManager.SortSystemTypesInCreationOrder(m_ClientSystems); // Ensure CreationOrder is respected.
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, m_ClientSystems);
            var initializationGroup = world.GetExistingSystemManaged<InitializationSystemGroup>();
            var simulationGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            var presentationGroup = world.GetExistingSystemManaged<PresentationSystemGroup>();
            world.GetExistingSystemManaged<UpdateWorldTimeSystem>().Enabled = false;
#if !UNITY_SERVER
            var initializationTickSystem = m_DefaultWorld.GetExistingSystemManaged<TickClientInitializationSystem>();
            var simulationTickSystem = m_DefaultWorld.GetExistingSystemManaged<TickClientSimulationSystem>();
            var presentationTickSystem = m_DefaultWorld.GetExistingSystemManaged<TickClientPresentationSystem>();
            initializationTickSystem.AddSystemGroupToTickList(initializationGroup);
            simulationTickSystem.AddSystemGroupToTickList(simulationGroup);
            presentationTickSystem.AddSystemGroupToTickList(presentationGroup);
#endif
            ClientServerBootstrap.ClientWorlds.Add(world);
            return world;
        }

        public void Tick(float dt)
        {
            // Use fixed timestep in network time system to prevent time dependencies in tests
            NetworkTimeSystem.s_FixedTimestampMS += (uint) (dt * 1000.0f);
            m_ElapsedTime += dt;
            m_DefaultWorld.SetTime(new TimeData(m_ElapsedTime, dt));
            if (m_ServerWorld != null)
                m_ServerWorld.SetTime(new TimeData(m_ElapsedTime, dt));
            if (m_ClientWorlds != null)
            {
                for (int i = 0; i < m_ClientWorlds.Length; ++i)
                    m_ClientWorlds[i].SetTime(new TimeData(m_ElapsedTime, dt));
            }

            // Make sure the log flush does not run
            // FIXME: Fix this so that the test world updates the below systems in the same order as the package simulation does. (Server first, then client).
#if !UNITY_CLIENT
            k_TickServerInitializationSystem.Begin();
            m_DefaultWorld.GetExistingSystemManaged<TickServerInitializationSystem>().Update();
            k_TickServerInitializationSystem.End();
            k_TickServerSimulationSystem.Begin();
            m_DefaultWorld.GetExistingSystemManaged<TickServerSimulationSystem>().Update();
            k_TickServerSimulationSystem.End();
#endif
#if !UNITY_SERVER
            k_TickClientInitializationSystem.Begin();
            m_DefaultWorld.GetExistingSystemManaged<TickClientInitializationSystem>().Update();
            k_TickClientInitializationSystem.End();
            k_TickClientSimulationSystem.Begin();
            m_DefaultWorld.GetExistingSystemManaged<TickClientSimulationSystem>().Update();
            k_TickClientSimulationSystem.End();
            k_TickClientPresentationSystem.Begin();
            m_DefaultWorld.GetExistingSystemManaged<TickClientPresentationSystem>().Update();
            k_TickClientPresentationSystem.End();
#endif
#if USING_UNITY_LOGGING
            // Flush the pending logs since the system doing that might not have run yet which means Log.Expect does not work
            Logging.Internal.LoggerManager.ScheduleUpdateLoggers().Complete();
#endif
        }

        public void MigrateServerWorld(World suppliedWorld = null)
        {
            DriverMigrationSystem migrationSystem = default;

            foreach (var world in World.All)
            {
                if ((migrationSystem = world.GetExistingSystemManaged<DriverMigrationSystem>()) != null)
                    break;
            }

            var ticket = migrationSystem.StoreWorld(ServerWorld);
            ServerWorld.Dispose();

            Assert.True(suppliedWorld == null || suppliedWorld.IsServer());
            var newWorld = migrationSystem.LoadWorld(ticket, suppliedWorld);
            m_ServerWorld = CreateServerWorld(newWorld.Name, newWorld);

            Assert.True(newWorld.Name == m_ServerWorld.Name);

            TrySetSuppressRunInBackgroundWarning(true);
        }

        public void MigrateClientWorld(int index, World suppliedWorld = null)
        {
            if (index > ClientWorlds.Length)
                throw new IndexOutOfRangeException($"ClientWorlds only contain {ClientWorlds.Length} items, you are trying to read index {index} that is out of range.");

            DriverMigrationSystem migrationSystem = default;

            foreach (var world in World.All)
            {
                if ((migrationSystem = world.GetExistingSystemManaged<DriverMigrationSystem>()) != null)
                    break;
            }

            var ticket = migrationSystem.StoreWorld(ClientWorlds[index]);
            ClientWorlds[index].Dispose();

            var newWorld = migrationSystem.LoadWorld(ticket, suppliedWorld);
            m_ClientWorlds[index] = CreateClientWorld(newWorld.Name, false, newWorld);

            Assert.True(newWorld.Name == m_ClientWorlds[index].Name);

            TrySetSuppressRunInBackgroundWarning(true);
        }

        public void RestartClientWorld(int index)
        {
            var oldConstructor = NetworkStreamReceiveSystem.DriverConstructor;
            NetworkStreamReceiveSystem.DriverConstructor = this;

            var name = m_ClientWorlds[index].Name;
            m_ClientWorlds[index].Dispose();

            m_ClientWorlds[index] = CreateClientWorld(name, false);
            NetworkStreamReceiveSystem.DriverConstructor = oldConstructor;
        }

        public void CreateClientDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            var packetDelay = DriverSimulatedDelay;
            int networkRate = 60;

            // All 3 packet types every frame stored for maximum delay, doubled for safety margin
            int maxPackets = 2 * (networkRate * 3 * (packetDelay + DriverSimulatedJitter) + 999) / 1000;

            var fuzzFactor = 0;
            // We name it "ClientTestXX-NameOfTest", so extract the XX.
            var worldId = CalculateWorldId(world);
            if (DriverFuzzFactor?.Length >= worldId + 1)
            {
                fuzzFactor = DriverFuzzFactor[worldId];
            }

            var simParams = new SimulatorUtility.Parameters
            {
                Mode = ApplyMode.AllPackets,
                MaxPacketSize = NetworkParameterConstants.MTU, MaxPacketCount = maxPackets,
                PacketDelayMs = packetDelay,
                PacketJitterMs = DriverSimulatedJitter,
                PacketDropInterval = DriverSimulatedDrop,
                FuzzFactor = fuzzFactor,
                FuzzOffset = DriverFuzzOffset,
                RandomSeed = DriverRandomSeed
            };
            var networkSettings = new NetworkSettings();
            networkSettings
                .WithReliableStageParameters(windowSize:32)
                .WithNetworkConfigParameters
            (
                maxFrameTimeMS: 100,
                fixedFrameTimeMS: DriverFixedTime
            );
            networkSettings.AddRawParameterStruct(ref simParams);

            //We are forcing here the connection type to be a socket but the connection is instead based on IPC.
            //The reason for that is that we want to be able to disable any check/logic that optimise for that use case
            //by default in the test.
            //It is possible however to disable this behavior using the provided opt
            var transportType = UseFakeSocketConnection == 1 ? TransportType.Socket : TransportType.IPC;

            var driverInstance = new NetworkDriverStore.NetworkDriverInstance();
            if (UseMultipleDrivers == 0)
                driverInstance.driver = NetworkDriver.Create(new IPCNetworkInterface(), networkSettings);
            else
            {
                if ((WorldCreationIndex & 0x1) == 0)
                {
                    driverInstance.driver = NetworkDriver.Create(new IPCNetworkInterface(), networkSettings);
                }
                else
                {
                    driverInstance.driver = NetworkDriver.Create(new UDPNetworkInterface(), networkSettings);
                }

            }

            //Fake the driver as it is always using a socket, even though we are also using IPC as a transport medium

            if (DriverSimulatedDelay + fuzzFactor > 0)
            {
                DefaultDriverBuilder.CreateClientSimulatorPipelines(ref driverInstance);
                driverStore.RegisterDriver(transportType, driverInstance);
            }
            else
            {
                DefaultDriverBuilder.CreateClientPipelines(ref driverInstance);
                driverStore.RegisterDriver(transportType, driverInstance);
            }
        }

        public static int CalculateWorldId(World world)
        {
            var regex = new Regex(@"(ClientTest)(\d)", RegexOptions.Singleline);
            var match = regex.Match(world.Name);
            return int.Parse(match.Groups[2].Value);
        }

        static int QueueSizeFromPlayerCount(int playerCount)
        {
            if (playerCount <= 16)
            {
                playerCount = 16;
            }
            return playerCount * 4;
        }

        public void CreateServerDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            var networkSettings = new NetworkSettings();
            networkSettings
                .WithReliableStageParameters(windowSize: 32)
                .WithNetworkConfigParameters(
                maxFrameTimeMS: 100,
                fixedFrameTimeMS: DriverFixedTime,
                receiveQueueCapacity: QueueSizeFromPlayerCount(m_NumClients),
                sendQueueCapacity: QueueSizeFromPlayerCount(m_NumClients)
            );
            var driverInstance = new NetworkDriverStore.NetworkDriverInstance();
            driverInstance.driver = NetworkDriver.Create(new IPCNetworkInterface(), networkSettings);
            DefaultDriverBuilder.CreateServerPipelines(ref driverInstance);
            driverStore.RegisterDriver(TransportType.IPC, driverInstance);
            if (UseMultipleDrivers != 0)
            {
                var socketInstance = new NetworkDriverStore.NetworkDriverInstance();
                socketInstance.driver = NetworkDriver.Create(new UDPNetworkInterface(), networkSettings);
                DefaultDriverBuilder.CreateServerPipelines(ref socketInstance);
                driverStore.RegisterDriver(TransportType.Socket, socketInstance);
            }
        }

        /// <summary>
        /// Will throw if connect fails.
        /// </summary>
        public void Connect(float dt, int maxSteps = 4)
        {
            var ep = NetworkEndpoint.LoopbackIpv4;
            ep.Port = 7979;
            GetSingletonRW<NetworkStreamDriver>(ServerWorld).ValueRW.Listen(ep);
            var connectionEntities = new Entity[ClientWorlds.Length];
            for (int i = 0; i < ClientWorlds.Length; ++i)
                connectionEntities[i] = GetSingletonRW<NetworkStreamDriver>(ClientWorlds[i]).ValueRW.Connect(ClientWorlds[i].EntityManager, ep);
            for (int i = 0; i < ClientWorlds.Length; ++i)
            {
                while (TryGetSingletonEntity<NetworkId>(ClientWorlds[i]) == Entity.Null)
                {
                    if (maxSteps <= 0)
                    {
                        var streamDriver = GetSingleton<NetworkStreamDriver>(ClientWorlds[i]);
                        var nsc = ClientWorlds[i].EntityManager.GetComponentData<NetworkStreamConnection>(connectionEntities[i]);
                        Assert.Fail($"ClientWorld[{i}] failed to connect to the server after {maxSteps} ticks! Driver status: {streamDriver.GetConnectionState(nsc)}!");
                        return;
                    }
                    --maxSteps;
                    Tick(dt);
                }
            }
        }

        public void GoInGame(World w = null)
        {
            if (w == null)
            {
                if (ServerWorld != null)
                {
                    GoInGame(ServerWorld);
                }
                if (ClientWorlds == null) return;
                foreach (var clientWorld in ClientWorlds)
                {
                    GoInGame(clientWorld);
                }

                return;
            }

            var type = ComponentType.ReadOnly<NetworkId>();
            using var query = w.EntityManager.CreateEntityQuery(type);
            var connections = query.ToEntityArray(Allocator.Temp);
            foreach (var connection in connections)
            {
                w.EntityManager.AddComponentData(connection, new NetworkStreamInGame());
            }

            connections.Dispose();
        }

        public void ExitFromGame()
        {
            void RemoveTag(World world)
            {
                var type = ComponentType.ReadOnly<NetworkId>();
                using var query = world.EntityManager.CreateEntityQuery(type);
                var connections = query.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < connections.Length; ++i)
                {
                    world.EntityManager.RemoveComponent<NetworkStreamInGame>(connections[i]);
                }

                connections.Dispose();
            }

            RemoveTag(ServerWorld);
            for (int i = 0; i < ClientWorlds.Length; ++i)
            {
                RemoveTag(ClientWorlds[i]);
            }
        }

        public void SetInGame(int client)
        {
            var type = ComponentType.ReadOnly<NetworkId>();
            using var clientQuery = ClientWorlds[client].EntityManager.CreateEntityQuery(type);
            var clientEntity = clientQuery.ToEntityArray(Allocator.Temp);
            ClientWorlds[client].EntityManager.AddComponent<NetworkStreamInGame>(clientEntity[0]);
            var clientNetId = ClientWorlds[client].EntityManager.GetComponentData<NetworkId>(clientEntity[0]);
            clientEntity.Dispose();

            using var query = ServerWorld.EntityManager.CreateEntityQuery(type);
            var connections = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < connections.Length; ++i)
            {
                var netId = ServerWorld.EntityManager.GetComponentData<NetworkId>(connections[i]);
                if (netId.Value == clientNetId.Value)
                {
                    ServerWorld.EntityManager.AddComponent<NetworkStreamInGame>(connections[i]);
                    break;
                }
            }

            connections.Dispose();
        }

        public void RemoveFromGame(int client)
        {
            var type = ComponentType.ReadOnly<NetworkId>();
            using var clientQuery = ClientWorlds[client].EntityManager.CreateEntityQuery(type);
            var clientEntity = clientQuery.ToEntityArray(Allocator.Temp);
            ClientWorlds[client].EntityManager.RemoveComponent<NetworkStreamInGame>(clientEntity[0]);
            var clientNetId = ClientWorlds[client].EntityManager.GetComponentData<NetworkId>(clientEntity[0]);
            clientEntity.Dispose();

            using var query = ServerWorld.EntityManager.CreateEntityQuery(type);
            var connections = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < connections.Length; ++i)
            {
                var netId = ServerWorld.EntityManager.GetComponentData<NetworkId>(connections[i]);
                if (netId.Value == clientNetId.Value)
                {
                    ServerWorld.EntityManager.RemoveComponent<NetworkStreamInGame>(connections[i]);
                    break;
                }
            }

            connections.Dispose();
        }

        public Entity TryGetSingletonEntity<T>(World w)
        {
            var type = ComponentType.ReadOnly<T>();
            using var query = w.EntityManager.CreateEntityQuery(type);
            int entCount = query.CalculateEntityCount();
#if UNITY_EDITOR
            if (entCount >= 2)
                Debug.LogError("Trying to get singleton, but there are multiple matching entities");
#endif
            if (entCount != 1)
                return Entity.Null;
            return query.GetSingletonEntity();
        }

        public T GetSingleton<T>(World w) where T : unmanaged, IComponentData
        {
            var type = ComponentType.ReadOnly<T>();
            using var query = w.EntityManager.CreateEntityQuery(type);
            return query.GetSingleton<T>();
        }

        public RefRW<T> GetSingletonRW<T>(World w) where T : unmanaged, IComponentData
        {
            var type = ComponentType.ReadWrite<T>();
            using var query = w.EntityManager.CreateEntityQuery(type);
            return query.GetSingletonRW<T>();
        }

        public DynamicBuffer<T> GetSingletonBuffer<T>(World w) where T : unmanaged, IBufferElementData
        {
            var type = ComponentType.ReadOnly<T>();
            using var query = w.EntityManager.CreateEntityQuery(type);
            return query.GetSingletonBuffer<T>();
        }

#if UNITY_EDITOR
        public bool CreateGhostCollection(params GameObject[] ghostTypes)
        {
            if (m_GhostCollection != null)
                return false;
            m_GhostCollection = new List<GameObject>(ghostTypes.Length);

            foreach (var ghostObject in ghostTypes)
            {
                var ghost = ghostObject.GetComponent<GhostAuthoringComponent>();
                if (ghost == null)
                {
                    ghost = ghostObject.AddComponent<GhostAuthoringComponent>();
                }
                ghost.prefabId = Guid.NewGuid().ToString().Replace("-", "");
                m_GhostCollection.Add(ghostObject);
            }
            m_BlobAssetStore = new BlobAssetStore(128);
            return true;
        }

        public Entity SpawnOnServer(int prefabIndex)
        {
            if (m_GhostCollection == null)
                throw new InvalidOperationException("Cannot spawn ghost on server without setting up the ghost first");
            var prefabCollection = TryGetSingletonEntity<NetCodeTestPrefabCollection>(ServerWorld);
            if (prefabCollection == Entity.Null)
                throw new InvalidOperationException("Cannot spawn ghost on server if a ghost prefab collection is not created");
            var prefabBuffers = ServerWorld.EntityManager.GetBuffer<NetCodeTestPrefab>(prefabCollection);
            return ServerWorld.EntityManager.Instantiate(prefabBuffers[prefabIndex].Value);
        }

        private Entity BakeGameObject(GameObject go, World world, BlobAssetStore blobAssetStore)
        {
            // We need to use an intermediate world as BakingUtility.BakeGameObjects cleans up previously baked
            // entities. This means that we need to move the entities from the intermediate world into the final
            // world. As BakeGameObject returns the main baked entity, we use the EntityGUID to find that
            // entity in the final world
            using var intermediateWorld = new World("NetCodeBakingWorld");

            var bakingSettings = new BakingSettings(BakingUtility.BakingFlags.AddEntityGUID, blobAssetStore);
            bakingSettings.PrefabRoot = go;
            bakingSettings.ExtraSystems.AddRange(TestSpecificAdditionalSystems);
            BakingUtility.BakeGameObjects(intermediateWorld, new GameObject[] {}, bakingSettings);

            var bakingSystem = intermediateWorld.GetExistingSystemManaged<BakingSystem>();
            var intermediateEntity = bakingSystem.GetEntity(go);
            var intermediateEntityGuid = intermediateWorld.EntityManager.GetComponentData<EntityGuid>(intermediateEntity);

            // Copy all the tracked/baked entities. That TransformAuthoring is present on all entities added by the baker for the
            // converted gameobject. It is sufficient condition to copy all the additional entities as well.
            var builder = new EntityQueryBuilder(Allocator.Temp).WithAll<Prefab, EntityGuid, LocalTransform>();

            using var bakedEntities = intermediateWorld.EntityManager.CreateEntityQuery(builder);
            world.EntityManager.MoveEntitiesFrom(intermediateWorld.EntityManager, bakedEntities);

            // Search for the entity in the final world by comparing the EntityGuid from entity in the intermediate world
            using var query = world.EntityManager.CreateEntityQuery(typeof(EntityGuid), typeof(Prefab));
            var entityArray = query.ToEntityArray(Allocator.Temp);
            var entityGUIDs = query.ToComponentDataArray<EntityGuid>(Allocator.Temp);
            for (int index = 0; index < entityGUIDs.Length; ++index)
            {
                if (entityGUIDs[index] == intermediateEntityGuid)
                {
                    return entityArray[index];
                }
            }

            Debug.LogError($"Copied Entity {intermediateEntityGuid} not found");
            return Entity.Null;
        }

        public Entity SpawnOnServer(GameObject go)
        {
            if (m_GhostCollection == null)
                throw new InvalidOperationException("Cannot spawn ghost on server without setting up the ghost first");
            int index = m_GhostCollection.IndexOf(go);
            if (index >= 0)
                return SpawnOnServer(index);

            return BakeGameObject(go, ServerWorld, m_BlobAssetStore);
        }

        public Entity BakeGhostCollection(World world)
        {
            if (m_GhostCollection == null)
                return Entity.Null;
            NativeList<Entity> prefabs = new NativeList<Entity>(m_GhostCollection.Count, Allocator.Temp);
            foreach (var prefab in m_GhostCollection)
            {
                var ghostAuth = prefab.GetComponent<GhostAuthoringComponent>();
                ghostAuth.ForcePrefabConversion = true;
                var prefabEnt = BakeGameObject(prefab, world, m_BlobAssetStore);
                ghostAuth.ForcePrefabConversion = false;
                world.EntityManager.AddComponentData(prefabEnt, default(Prefab));
                prefabs.Add(prefabEnt);
            }

            var collection = world.EntityManager.CreateEntity();
            world.EntityManager.AddComponentData(collection, default(NetCodeTestPrefabCollection));
            var prefabBuffer = world.EntityManager.AddBuffer<NetCodeTestPrefab>(collection);
            foreach (var prefab in prefabs)
                prefabBuffer.Add(new NetCodeTestPrefab {Value = prefab});
            return collection;
        }
#endif
    }
}
