using Unity.Assertions;
using Unity.Collections;
using Unity.Entities;
#if UNITY_EDITOR
using Unity.NetCode.Analytics;
#endif
using Unity.Networking.Transport;
#if ENABLE_MANAGED_UNITYTLS
using Unity.Networking.Transport.TLS;
#endif
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;

namespace Unity.NetCode
{
    /// <summary>
    /// Default helper method implementation for constructing <see cref="NetworkDriverStore.NetworkDriverInstance"/>,
    /// default <see cref="NetworkSettings"/> and registering these on the <see cref="NetworkDriverStore"/>.
    /// </summary>
    public static class DefaultDriverBuilder
    {
        const int DefaultPayloadCapacity = 16 * 1024;
        const int MaxFrameTimeMS = 100;
        const int DefaultWindowSize = 32;

        /// <summary>
        /// Return an instance of the <see cref="IPCAndSocketDriverConstructor"/> constructor
        /// </summary>
        public static INetworkStreamDriverConstructor DefaultDriverConstructor => new IPCAndSocketDriverConstructor();

        /// <summary>
        /// Return a set of internal default settings. This will use the NetworkSimulator parameters set by Multiplayer PlayMode Tools.
        /// </summary>
        /// <returns>A new <see cref="NetworkDriverStore.NetworkDriverInstance"/></returns>
        public static NetworkSettings GetNetworkSettings()
        {
            var settings = new NetworkSettings();
            settings.WithReliableStageParameters(windowSize: DefaultWindowSize)
                .WithFragmentationStageParameters(payloadCapacity: DefaultPayloadCapacity);
#if UNITY_EDITOR || NETCODE_DEBUG
            settings.WithNetworkConfigParameters(maxFrameTimeMS: MaxFrameTimeMS);
            if (NetworkSimulatorSettings.Enabled)
            {
                NetworkSimulatorSettings.SetSimulatorSettings(ref settings);
            }
#endif
            return settings;
        }

        /// <summary>
        /// Return a set of internal default settings. This will use the NetworkSimulator parameters set by Multiplayer PlayMode Tools.
        /// </summary>
        /// <param name="playerCount">Amount of players the server should allocate receive and send queue for. The estimation is that each player will receive 4 packets.</param>
        /// <returns>Parameters that describe the network configuration.</returns>
        public static NetworkSettings GetNetworkServerSettings(int playerCount = 0)
        {
            var settings = new NetworkSettings();
#if UNITY_EDITOR
            NetCodeAnalyticsState.SetPlayerCount(playerCount);
#endif
            settings.WithReliableStageParameters(windowSize: DefaultWindowSize)
                .WithFragmentationStageParameters(payloadCapacity: DefaultPayloadCapacity);
#if UNITY_EDITOR || NETCODE_DEBUG
            settings.WithNetworkConfigParameters(maxFrameTimeMS: MaxFrameTimeMS,
                receiveQueueCapacity: QueueSizeFromPlayerCount(playerCount),
                sendQueueCapacity: QueueSizeFromPlayerCount(playerCount));
#else
            settings.WithNetworkConfigParameters(
                    receiveQueueCapacity: QueueSizeFromPlayerCount(playerCount),
                    sendQueueCapacity: QueueSizeFromPlayerCount(playerCount));
#endif
            return settings;
        }

        /// <summary>
        /// Helper method for creating NetworkDriver suitable for client.
        /// The driver will use the the specified <paramref name="netIf">INetworkInterface</paramref> and is configured
        /// using the internal defaults. See: <see cref="GetNetworkSettings"/>.
        /// </summary>
        /// <typeparam name="T">the <see cref="INetworkInterface"/> type ot use</typeparam>
        /// <param name="netIf">the instance of a <see cref="INetworkInterface"/> to use to create the driver</param>
        /// <returns>A new <see cref="NetworkDriverStore.NetworkDriverInstance"/></returns>
        public static NetworkDriverStore.NetworkDriverInstance CreateClientNetworkDriver<T>(T netIf) where T : unmanaged, INetworkInterface
        {
            return CreateClientNetworkDriver(netIf, GetNetworkSettings());
        }

        /// <summary>
        /// Helper method for creating NetworkDriver suitable for client.
        /// The driver will use the specified <see cref="INetworkInterface"/> and is configured
        /// using the provided <paramref name="settings"/>.
        /// </summary>
        /// <typeparam name="T">the <see cref="INetworkInterface"/> type ot use</typeparam>
        /// <param name="netIf">the instance of a <see cref="INetworkInterface"/> to use to create the driver</param>
        /// <param name="settings">A list of the parameters that describe the network configuration.</param>
        /// <returns>A new <see cref="NetworkDriverStore.NetworkDriverInstance"/></returns>
        public static NetworkDriverStore.NetworkDriverInstance CreateClientNetworkDriver<T>(T netIf, NetworkSettings settings) where T : unmanaged, INetworkInterface
        {
            var driverInstance = new NetworkDriverStore.NetworkDriverInstance();
#if UNITY_EDITOR || NETCODE_DEBUG
            if (NetworkSimulatorSettings.Enabled)
            {
                driverInstance.simulatorEnabled = true;
                driverInstance.driver = NetworkDriver.Create(netIf, settings);
                CreateClientSimulatorPipelines(ref driverInstance);
            }
            else
#endif
            {
                driverInstance.simulatorEnabled = false;
                driverInstance.driver = NetworkDriver.Create(netIf, settings);
                CreateClientPipelines(ref driverInstance);
            }
            return driverInstance;
        }

        private static int QueueSizeFromPlayerCount(int playerCount)
        {
            if (playerCount == 0)
            {
                playerCount = 16;
            }
            return playerCount * 4;
        }

        /// <summary>
        /// Helper method for creating server NetworkDriver given the specified <paramref name="netIf">INetworkInterface</paramref>
        /// The driver is configured with the internal defaults. See: <see cref="GetNetworkServerSettings"/>.
        /// </summary>
        /// <typeparam name="T">the <see cref="INetworkInterface"/> type ot use</typeparam>
        /// <param name="netIf">the instance of a <see cref="INetworkInterface"/> to use to create the driver</param>
        /// <param name="playerCount">Amount of players the server should allocate receive and send queue for. The estimation is that each player will receive 4 packets.</param>
        /// <returns>A new <see cref="NetworkDriverStore.NetworkDriverInstance"/></returns>
        public static NetworkDriverStore.NetworkDriverInstance CreateServerNetworkDriver<T>(T netIf, int playerCount = 0) where T : unmanaged, INetworkInterface
        {
            return CreateServerNetworkDriver(netIf, GetNetworkServerSettings(playerCount));
        }

        /// <summary>
        /// Helper method for creating server NetworkDriver given the specified <param name="netIf">INetworkInterface</param>
        /// The driver is configured using the <param name="settings">NetworkSettings</param>.
        /// </summary>
        /// <typeparam name="T">the <see cref="INetworkInterface"/> type ot use</typeparam>
        /// <param name="netIf"></param>
        /// <param name="settings">A list of the parameters that describe the network configuration.</param>
        /// <returns>A new <see cref="NetworkDriverStore.NetworkDriverInstance"/></returns>
        public static NetworkDriverStore.NetworkDriverInstance CreateServerNetworkDriver<T>(T netIf, NetworkSettings settings) where T : unmanaged, INetworkInterface
        {
            var driverInstance = new NetworkDriverStore.NetworkDriverInstance
            {
                driver = NetworkDriver.Create(netIf, settings)
            };
            CreateServerPipelines(ref driverInstance);

            return driverInstance;
        }

#if !UNITY_CLIENT
        static bool UseSocketDriver(NetDebug netDebug)
        {
            bool FoundServerWorldInstance()
            {
                foreach (var otherWorld in World.All)
                {
                    if (!otherWorld.IsCreated || !otherWorld.IsServer()) { continue; }
                    netDebug.DebugLog("Found server world instance. Prefer use IPC network interface");
                    return true;
                }

                return false;
            }

#if UNITY_EDITOR || NETCODE_DEBUG
            //if the emulator is enabled we always force to use sockets. It also work with IPC but this is preferred choice.
            if (NetworkSimulatorSettings.Enabled)
            {
                netDebug.DebugLog("Network simulator enabled. Forcing client to use a socket network driver, rather than an IPC.");
                return true;
            }
#endif
            //The client playmode is always set if UNITY_CLIENT define is present
            if (ClientServerBootstrap.RequestedPlayType == ClientServerBootstrap.PlayType.Client)
            {
                return true;
            }
            //PlayMode is client server the simulator is disabled. We are in client-server mode
            Assert.IsTrue(ClientServerBootstrap.RequestedPlayType == ClientServerBootstrap.PlayType.ClientAndServer);
            netDebug.DebugLog("Lookup for a server world instance in the same process");
            return !FoundServerWorldInstance();
        }
#endif

        /// <summary>
        /// Register a NetworkDriver instance in <paramref name="driverStore"/>:<br/>
        ///     - a single  <see cref="IPCNetworkInterface"/> NetworkDriver if the both client and server worlds are present in the same process<br/>
        ///     - a single <see cref="UDPNetworkInterface"/> driver in all other cases<br/>
        /// These are configured using internal defaults. See: <see cref="GetNetworkSettings"/>.
        /// </summary>
        /// <param name="world">Used for determining whether we are running in a client or server world.</param>
        /// <param name="driverStore">Store for NetworkDriver.</param>
        /// <param name="netDebug">For handling logging.</param>
        public static void RegisterClientDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            RegisterClientDriver(world, ref driverStore, netDebug, GetNetworkSettings());
        }

        /// <summary>
        /// Register a NetworkDriver instance in and stores it in <paramref name="driverStore"/>:<br/>
        ///     - a single <see cref="IPCNetworkInterface"/> NetworkDriver if the both client and server worlds are present in the same process.<br/>
        ///     - a single <see cref="UDPNetworkInterface"/> driver in all other cases.<br/>
        /// These are configured using the <param name="settings">NetworkSettings</param> passed in.
        /// </summary>
        /// <param name="world">Used for determining whether we are running in a client or server world.</param>
        /// <param name="driverStore">Store for NetworkDriver.</param>
        /// <param name="netDebug">For handling logging.</param>
        /// <param name="settings">A list of the parameters that describe the network configuration.</param>
        public static void RegisterClientDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug, NetworkSettings settings)
        {
#if !UNITY_CLIENT
            if (UseSocketDriver(netDebug))
            {
                RegisterClientUdpDriver(world, ref driverStore, netDebug, settings);
            }
            else
            {
                RegisterClientIpcDriver(world, ref driverStore, netDebug, settings);
            }
#else
            RegisterClientUdpDriver(world, ref driverStore, netDebug, settings);
#endif
        }

        /// <summary>
        /// Register a <see cref="UDPNetworkInterface"/> NetworkDriver instance in <paramref name="driverStore"/>.
        /// This are configured using the <param name="settings">NetworkSettings</param> passed in.
        /// </summary>
        /// <param name="world">Used for determining whether we are running in a client or server world.</param>
        /// <param name="driverStore">Store for NetworkDriver.</param>
        /// <param name="netDebug">For handling logging.</param>
        /// <param name="settings">A list of the parameters that describe the network configuration.</param>
        public static void RegisterClientUdpDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug, NetworkSettings settings)
        {
            Assert.IsTrue(ClientServerBootstrap.RequestedPlayType != ClientServerBootstrap.PlayType.Server);
            Assert.IsTrue(world.IsClient());
            netDebug.DebugLog("Create client default socket network interface driver");
            var driverInstance = DefaultDriverBuilder.CreateClientNetworkDriver(new UDPNetworkInterface(), settings);
            driverStore.RegisterDriver(TransportType.Socket, driverInstance);
        }

        /// <summary>
        /// Register an <see cref="IPCNetworkInterface"/> NetworkDriver instance in <paramref name="driverStore"/>.
        /// This are configured using the <param name="settings">NetworkSettings</param> passed in.
        /// </summary>
        /// <param name="world">Used for determining whether we are running in a client or server world.</param>
        /// <param name="driverStore">Store for NetworkDriver.</param>
        /// <param name="netDebug">For handling logging.</param>
        /// <param name="settings">A list of the parameters that describe the network configuration.</param>
        public static void RegisterClientIpcDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug, NetworkSettings settings)
        {
            Assert.IsTrue(ClientServerBootstrap.RequestedPlayType != ClientServerBootstrap.PlayType.Server);
            Assert.IsTrue(world.IsClient());
            netDebug.DebugLog("Create client default IPC network interface driver");
            var driverInstance = DefaultDriverBuilder.CreateClientNetworkDriver(new IPCNetworkInterface(), settings);
            driverStore.RegisterDriver(TransportType.IPC, driverInstance);
        }

        /// <summary>
        /// Register a NetworkDriver instance in <paramref name="driverStore"/>:<br/>
        /// both <see cref="IPCNetworkInterface"/> and <see cref="UDPNetworkInterface"/> NetworkDriver in the editor and only
        ///   a single <see cref="UDPNetworkInterface"/> driver in the build.<br/>
        /// These are configured using internal defaults. See: <see cref="GetNetworkSettings"/>.
        /// </summary>
        /// <param name="world">Used for determining whether we are running in a client or server world.</param>
        /// <param name="driverStore">Store for NetworkDriver.</param>
        /// <param name="netDebug">For handling logging.</param>
        /// <param name="playerCount">Amount of players the server should allocate receive and send queue for. The estimation is that each player will receive 4 packets.</param>
        public static void RegisterServerDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug, int playerCount = 0)
        {
            RegisterServerDriver(world, ref driverStore, netDebug, GetNetworkServerSettings(playerCount: playerCount));
        }

        /// <summary>
        /// Register a NetworkDriver instance in <paramref name="driverStore"/>:<br/>
        /// both <see cref="IPCNetworkInterface"/> and <see cref="UDPNetworkInterface"/> NetworkDriver in the editor and only
        ///   a single <see cref="UDPNetworkInterface"/> driver in the build.<br/>
        /// These are configured using the <param name="settings">NetworkSettings</param> passed in.
        /// </summary>
        /// <param name="world">Used for determining whether we are running in a client or server world.</param>
        /// <param name="driverStore">Store for NetworkDriver.</param>
        /// <param name="netDebug">For handling logging.</param>
        /// <param name="settings">A list of the parameters that describe the network configuration.</param>
        public static void RegisterServerDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug, NetworkSettings settings)
        {
            RegisterServerIpcDriver(world, ref driverStore, netDebug, settings);
            RegisterServerUdpDriver(world, ref driverStore, netDebug, settings);
        }

        /// <summary>
        /// Register a <see cref="IPCNetworkInterface"/> NetworkDriver instance in <paramref name="driverStore"/>.
        /// This are configured using the <param name="settings">NetworkSettings</param> passed in.
        ///
        /// If the requested <see cref="ClientServerBootstrap.PlayType"/> is <see cref="ClientServerBootstrap.PlayType.Server"/>
        /// this will do nothing as no local clients will ever make use of the IPC mechanism.
        /// </summary>
        /// <param name="world">Used for determining whether we are running in a client or server world.</param>
        /// <param name="driverStore">Store for NetworkDriver.</param>
        /// <param name="netDebug">For handling logging.</param>
        /// <param name="settings">A list of the parameters that describe the network configuration.</param>
        public static void RegisterServerIpcDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug, NetworkSettings settings)
        {
#if UNITY_EDITOR || !UNITY_SERVER
            Assert.IsTrue(world.IsServer());
            if (ClientServerBootstrap.RequestedPlayType == ClientServerBootstrap.PlayType.Server)
            {
                return;
            }

            netDebug.DebugLog("Create server default IPC network interface driver");
            var ipcDriver = CreateServerNetworkDriver(new IPCNetworkInterface(), settings);
            driverStore.RegisterDriver(TransportType.IPC, ipcDriver);
#endif
        }

        /// <summary>
        /// Register a <see cref="UDPNetworkInterface"/> NetworkDriver instance in <paramref name="driverStore"/>.
        /// This are configured using the <param name="settings">NetworkSettings</param> passed in.
        /// </summary>
        /// <param name="world">Used for determining whether we are running in a client or server world.</param>
        /// <param name="driverStore">Store for NetworkDriver.</param>
        /// <param name="netDebug">For handling logging.</param>
        /// <param name="settings">A list of the parameters that describe the network configuration.</param>
        public static void RegisterServerUdpDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug, NetworkSettings settings)
        {
            Assert.IsTrue(world.IsServer());
            netDebug.DebugLog("Create server default socket network interface driver");
            var socketDriver = CreateServerNetworkDriver(new UDPNetworkInterface(), settings);
            driverStore.RegisterDriver(TransportType.Socket, socketDriver);
        }

        /// <summary>
        /// Create the default network pipelines (reliable, unreliable, unreliable fragmented) for the client.
        /// </summary>
        /// <param name="driverInstance">The <see cref="NetworkDriverStore.NetworkDriverInstance"/> instance to configure</param>
        public static void CreateClientPipelines(ref NetworkDriverStore.NetworkDriverInstance driverInstance)
        {
            driverInstance.unreliablePipeline = driverInstance.driver.CreatePipeline(typeof(NullPipelineStage));
            driverInstance.reliablePipeline = driverInstance.driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
            driverInstance.unreliableFragmentedPipeline = driverInstance.driver.CreatePipeline(typeof(FragmentationPipelineStage));
        }

        /// <summary>
        /// Create the default network pipelines (reliable, unreliable, unreliable fragmented) for the server.
        /// </summary>
        /// <param name="driverInstance">The <see cref="NetworkDriverStore.NetworkDriverInstance"/> instance to configure</param>
        public static void CreateServerPipelines(ref NetworkDriverStore.NetworkDriverInstance driverInstance)
        {
            driverInstance.unreliablePipeline = driverInstance.driver.CreatePipeline(typeof(NullPipelineStage));
            driverInstance.reliablePipeline = driverInstance.driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
            driverInstance.unreliableFragmentedPipeline = driverInstance.driver.CreatePipeline(typeof(FragmentationPipelineStage));
        }

#if UNITY_EDITOR || NETCODE_DEBUG || UNITY_INCLUDE_TESTS
        /// <summary>
        /// Should be used only for configuring client drivers, create the network pipelines (reliable, unreliable, unreliable fragmented)
        /// with network simulator support.
        /// </summary>
        /// <param name="driverInstance"></param>
        public static void CreateClientSimulatorPipelines(ref NetworkDriverStore.NetworkDriverInstance driverInstance)
        {
            driverInstance.unreliablePipeline = driverInstance.driver.CreatePipeline(
                typeof(SimulatorPipelineStage));
            driverInstance.reliablePipeline = driverInstance.driver.CreatePipeline(
                typeof(ReliableSequencedPipelineStage),
                typeof(SimulatorPipelineStage));
            driverInstance.unreliableFragmentedPipeline = driverInstance.driver.CreatePipeline(
                typeof(FragmentationPipelineStage),
                typeof(SimulatorPipelineStage));
        }
#endif
#if ENABLE_MANAGED_UNITYTLS
        /// <summary>
        /// Register a NetworkDriver instance in and stores it in <paramref name="driverStore"/>:<br/>
        ///     - a single <see cref="IPCNetworkInterface"/> NetworkDriver if the both client and server worlds are present in the same process.<br/>
        ///     - a single <see cref="UDPNetworkInterface"/> driver in all other cases.<br/>
        /// These are configured using the default settings. See <see cref="GetNetworkSettings"/>.
        /// </summary>
        /// <param name="world">Used for determining whether we are running in a client or server world.</param>
        /// <param name="driverStore">Store for NetworkDriver.</param>
        /// <param name="netDebug">For handling logging.</param>
        /// <param name="caCertificate">Signed server certificate.</param>
        /// <param name="serverName">Common name in the server certificate.</param>
        public static void RegisterClientDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug, ref FixedString4096Bytes caCertificate, ref FixedString512Bytes serverName)
        {
            var settings = GetNetworkSettings();
            settings = settings.WithSecureClientParameters(caCertificate: ref caCertificate, serverName: ref serverName);
            RegisterClientDriver(world, ref driverStore, netDebug, settings);
        }

        /// <summary>
        /// Register a NetworkDriver instance in <paramref name="driverStore"/>:<br/>
        /// both <see cref="IPCNetworkInterface"/> and <see cref="UDPNetworkInterface"/> NetworkDriver in the editor and only
        ///   a single <see cref="UDPNetworkInterface"/> driver in the build.<br/>
        /// These are configured using the default settings. See <see cref="GetNetworkServerSettings"/>.
        /// </summary>
        /// <param name="world">Used for determining whether we are running in a client or server world.</param>
        /// <param name="driverStore">Store for NetworkDriver.</param>
        /// <param name="netDebug">For handling logging.</param>
        /// <param name="certificate"></param>
        /// <param name="privateKey"></param>
        /// <param name="playerCount">Amount of players the server should allocate receive and send queue for. The estimation is that each player will receive 4 packets.</param>
        public static void RegisterServerDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug, ref FixedString4096Bytes certificate, ref FixedString4096Bytes privateKey, int playerCount = 0)
        {
            var settings = GetNetworkServerSettings(playerCount: playerCount);
            settings = settings.WithSecureServerParameters(certificate: ref certificate, privateKey: ref privateKey);
            RegisterServerDriver(world, ref driverStore, netDebug, settings);
        }
#endif
        /// <summary>
        /// Register a NetworkDriver instance in and stores it in <paramref name="driverStore"/>:<br/>
        ///     - a single <see cref="IPCNetworkInterface"/> NetworkDriver if the both client and server worlds are present in the same process.<br/>
        ///     - a single <see cref="UDPNetworkInterface"/> driver in all other cases.<br/>
        /// These are configured using the default settings. See <see cref="GetNetworkSettings"/>.
        /// </summary>
        /// <param name="world">Used for determining whether we are running in a client or server world.</param>
        /// <param name="driverStore">Store for NetworkDriver.</param>
        /// <param name="netDebug">For handling logging.</param>
        /// <param name="relayData">Server information to make a connection using a relay server.</param>
        public static void RegisterClientDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug, ref RelayServerData relayData)
        {
            var settings = GetNetworkSettings();
#if !UNITY_CLIENT
            if (UseSocketDriver(netDebug))
#endif
            {
                settings = settings.WithRelayParameters(ref relayData);
            }
            RegisterClientDriver(world, ref driverStore, netDebug, settings);
        }

        /// <summary>
        /// Register a NetworkDriver instance in <paramref name="driverStore"/>:<br/>
        /// both <see cref="IPCNetworkInterface"/> and <see cref="UDPNetworkInterface"/> NetworkDriver in the editor and only
        ///   a single <see cref="UDPNetworkInterface"/> driver in the build.<br/>
        /// These are configured using the default settings. See <see cref="GetNetworkServerSettings"/>.
        /// </summary>
        /// <param name="world">Used for determining whether we are running in a client or server world.</param>
        /// <param name="driverStore">Store for NetworkDriver.</param>
        /// <param name="netDebug">For handling logging.</param>
        /// <param name="relayData">Server information to make a connection using a relay server.</param>
        /// <param name="playerCount">Amount of players the server should allocate receive and send queue for. The estimation is that each player will receive 4 packets.</param>
        public static void RegisterServerDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug, ref RelayServerData relayData, int playerCount = 0)
        {
            var settings = GetNetworkServerSettings(playerCount: playerCount);
            RegisterServerIpcDriver(world, ref driverStore, netDebug, settings);
            settings = settings.WithRelayParameters(ref relayData);
            RegisterServerUdpDriver(world, ref driverStore, netDebug, settings);
        }
    }

    /// <summary>
    /// The default NetCode driver constructor. It creates:
    /// - On the server: both <see cref="IPCNetworkInterface"/> and <see cref="UDPNetworkInterface"/> NetworkDriver in the editor and only
    ///   a single <see cref="UDPNetworkInterface"/> driver in the build.<br/>
    /// - On the client:<br/>
    ///     - a single <see cref="IPCNetworkInterface"/> NetworkDriver if the both client and server worlds are present in the same process.<br/>
    ///     - a single <see cref="UDPNetworkInterface"/> driver in all other cases.<br/>
    /// In the Editor and Development build, if the network simulator is enabled, force on the client to use the <see cref="UDPNetworkInterface"/> network driver.
    /// <remarks>
    /// <b>To let the client use the IPC network interface In ClientServer mode it is mandatory to always create the server world first.</b>
    /// </remarks>
    /// </summary>
    public struct IPCAndSocketDriverConstructor : INetworkStreamDriverConstructor
    {
        /// <summary>
        /// Create and register a new <see cref="NetworkDriver"/> suitable for connecting client to server to the destination <see cref="NetworkDriverStore"/>.
        /// The network driver instance will use socket or IPC network interfaces based on the <see cref="ClientServerBootstrap.RequestedPlayType"/> and the
        /// presence of a server instance in the same process.
        /// </summary>
        /// <param name="world">The destination world in which the driver will be created</param>
        /// <param name="driverStore">An instance of a <see cref="NetworkDriverStore"/> where the driver will be registered</param>
        /// <param name="netDebug">The <see cref="netDebug"/> singleton, for logging errors and debug information</param>
        public void CreateClientDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            DefaultDriverBuilder.RegisterClientDriver(world, ref driverStore, netDebug);
        }

        /// <summary>
        /// Create and register one or more network drivers that can be used to listen for incoming connection into the destination <see cref="NetworkDriverStore"/>.
        /// By default, a <see cref="NetworkDriver"/> that uses a socket network interface is always created.
        /// In the Editor or in a Client/Server player build, if the <see cref="ClientServerBootstrap.RequestedPlayType"/> mode is set to
        /// <see cref="ClientServerBootstrap.PlayType.ClientAndServer"/>, a second <see cref="NetworkDriver"/> that use an IPC network interface will be also created and
        /// that will be used for minimizing the latency for the in-proc client connection.
        /// </summary>
        /// <param name="world">The destination world in which the driver will be created</param>
        /// <param name="driverStore">An instance of a <see cref="NetworkDriverStore"/> where the driver will be registered</param>
        /// <param name="netDebug">The <see cref="netDebug"/> singleton, for logging errors and debug information</param>
        public void CreateServerDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            DefaultDriverBuilder.RegisterServerDriver(world, ref driverStore, netDebug);
        }
    }
}
