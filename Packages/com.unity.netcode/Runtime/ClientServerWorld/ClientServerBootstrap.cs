using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.Scenes;

namespace Unity.NetCode
{
    /// <summary>
    /// ClientServerBootstrap is responsible to configure and create the Server and Client worlds at runtime when
    /// the game start (in the editor when entering PlayMode).
    /// The ClientServerBootstrap is meant to be a base class for your own custom boostrap code and provides utility methods
    /// that make it easy creating the client and server worlds.
    /// It also support connecting the client to server automatically, using the <see cref="AutoConnectPort"/> port and
    /// <see cref="DefaultConnectAddress"/>.
    /// For the server, it allow binding the server transport to a specific listening port and address (especially useful
    /// when running the server on some cloud provider) via <see cref="DefaultListenAddress"/>.
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public class ClientServerBootstrap : ICustomBootstrap
    {
        /// <summary>
        /// The maximum number of thin clients that can be created in the editor.
        /// </summary>
        public const int k_MaxNumThinClients = 32;

#if UNITY_EDITOR || !UNITY_SERVER
        private static int NextThinClientId;
        /// <summary>
        /// Initialize the bootstrap class and reset the static data everytime a new instance is created.
        /// </summary>
        public ClientServerBootstrap()
        {
            NextThinClientId = 1;
        }
#endif
#if UNITY_SERVER && UNITY_CLIENT
        public ClientServerBootstrap()
        {
            UnityEngine.Debug.LogError("Both UNITY_SERVER and UNITY_CLIENT defines are present. This is not allowed and will lead to undefined behaviour, they are for dedicated server or client only logic so can't work together.");
        }
#endif

        /// <summary>
        /// Utility method for creating a local world without any NetCode systems.
        /// <param name="defaultWorldName">Name of the world instantiated.</param>
        /// <returns>World with default systems added, set to run as the Main Live world.
        /// See <see cref="WorldFlags"/>.<see cref="WorldFlags.Game"/></returns>
        /// </summary>
        /// <param name="defaultWorldName">The name to use for the default world.</param>
        /// <returns>A new world instance.</returns>
        public static World CreateLocalWorld(string defaultWorldName)
        {
            // The default world must be created before generating the system list in order to have a valid TypeManager instance.
            // The TypeManage is initialised the first time we create a world.
            var world = new World(defaultWorldName, WorldFlags.Game);
            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = world;

            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
#if !UNITY_DOTSRUNTIME
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
#endif
            return world;
        }
#if UNITY_DOTSRUNTIME
        private static void CreateTickWorld()
        {
            if (World.DefaultGameObjectInjectionWorld == null)
            {
                World.DefaultGameObjectInjectionWorld = new World("NetcodeTickWorld", WorldFlags.Game);

                var systems = new Type[]{
#if !UNITY_SERVER
                    typeof(TickClientInitializationSystem), typeof(TickClientSimulationSystem), typeof(TickClientPresentationSystem),
#endif
#if !UNITY_CLIENT
                    typeof(TickServerInitializationSystem), typeof(TickServerSimulationSystem),
#endif
                    typeof(WorldUpdateAllocatorResetSystem)
                };
                DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World.DefaultGameObjectInjectionWorld, systems);
            }
        }
#if !UNITY_CLIENT
        private static void AppendWorldToServerTickWorld(World childWorld)
        {
            CreateTickWorld();
            var initializationTickSystem = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<TickServerInitializationSystem>();
            var simulationTickSystem = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<TickServerSimulationSystem>();

            //Bind main world group to tick systems (DefaultWorld tick the client world)
            if (initializationTickSystem == null || simulationTickSystem == null)
                throw new InvalidOperationException("Tying to add a world to the tick systems of the default world, but the default world does not have the tick systems");

            var initializationGroup = childWorld.GetExistingSystemManaged<InitializationSystemGroup>();
            var simulationGroup = childWorld.GetExistingSystemManaged<SimulationSystemGroup>();

            if (initializationGroup != null)
                initializationTickSystem.AddSystemGroupToTickList(initializationGroup);
            if (simulationGroup != null)
                simulationTickSystem.AddSystemGroupToTickList(simulationGroup);
        }
#endif
#if !UNITY_SERVER
        private static void AppendWorldToClientTickWorld(World childWorld)
        {
            CreateTickWorld();
            var initializationTickSystem = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<TickClientInitializationSystem>();
            var simulationTickSystem = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<TickClientSimulationSystem>();
            var presentationTickSystem = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<TickClientPresentationSystem>();

            //Bind main world group to tick systems (DefaultWorld tick the client world)
            if (initializationTickSystem == null || simulationTickSystem == null || presentationTickSystem == null)
                throw new InvalidOperationException("Tying to add a world to the tick systems of the default world, but the default world does not have the tick systems");

            var initializationGroup = childWorld.GetExistingSystemManaged<InitializationSystemGroup>();
            var simulationGroup = childWorld.GetExistingSystemManaged<SimulationSystemGroup>();
            var presentationGroup = childWorld.GetExistingSystemManaged<PresentationSystemGroup>();

            if (initializationGroup != null)
                initializationTickSystem.AddSystemGroupToTickList(initializationGroup);
            if (simulationGroup != null)
                simulationTickSystem.AddSystemGroupToTickList(simulationGroup);
            if (presentationGroup != null)
                presentationTickSystem.AddSystemGroupToTickList(presentationGroup);
        }
#endif
#endif

        /// <summary>
        /// Implement the ICustomBootstrap interface. Create the default client and serer worlds by
        /// based on the <see cref="RequestedPlayType"/>.
        /// In the editor, it also create thin clients worlds, if <see cref="RequestedNumThinClients"/> is not 0.
        /// As part of the initialization process, if the
        /// </summary>
        /// <param name="defaultWorldName">The name to use for the default world. Unused, can be null or empty</param>
        /// <returns></returns>
        public virtual bool Initialize(string defaultWorldName)
        {
            CreateDefaultClientServerWorlds();
            return true;
        }

        /// <summary>
        /// Utility method for creating the default client and server worlds based on the settings
        /// in the playmode tools in the editor or client / server defined in a player.
        /// Should be used in custom implementations of `Initialize`.
        /// </summary>
        protected virtual void CreateDefaultClientServerWorlds()
        {
            var requestedPlayType = RequestedPlayType;
            if (requestedPlayType != PlayType.Client)
            {
                CreateServerWorld("ServerWorld");
            }

            if (requestedPlayType != PlayType.Server)
            {
                CreateClientWorld("ClientWorld");

#if UNITY_EDITOR
                var requestedNumThinClients = RequestedNumThinClients;
                for (var i = 0; i < requestedNumThinClients; i++)
                {
                    CreateThinClientWorld();
                }
#endif
            }
        }

        /// <summary>
        /// Utility method for creating thin clients worlds.
        /// Can be used in custom implementations of `Initialize` as well at runtime,
        /// to add new clients dynamically.
        /// </summary>
        /// <returns></returns>
        public static World CreateThinClientWorld()
        {
#if UNITY_SERVER && !UNITY_EDITOR
            throw new NotImplementedException();
#else
            var world = new World("ThinClientWorld" + NextThinClientId++, WorldFlags.GameThinClient);

            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ThinClientSimulation);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);

#if UNITY_DOTSRUNTIME
            AppendWorldToClientTickWorld(world);
#else
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
#endif

            return world;
#endif
        }

        /// <summary>
        /// Utility method for creating new clients worlds.
        /// Can be used in custom implementations of `Initialize` as well at runtime, to add new clients dynamically.
        /// </summary>
        /// <param name="name">The client world name</param>
        /// <returns></returns>
        public static World CreateClientWorld(string name)
        {
#if UNITY_SERVER && !UNITY_EDITOR
            throw new NotImplementedException();
#else
            var world = new World(name, WorldFlags.GameClient);

            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Presentation);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);

#if UNITY_DOTSRUNTIME
            AppendWorldToClientTickWorld(world);
#else
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
#endif

            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = world;

            return world;
#endif
        }

        internal static bool TryFindAutoConnectEndPoint(out NetworkEndpoint autoConnectEp)
        {
            autoConnectEp = default;

            switch (RequestedPlayType)
            {
                case PlayType.ClientAndServer:
                {
                    // Allow loopback + AutoConnectPort:
                    if (HasDefaultAddressAndPortSet(out autoConnectEp))
                    {
                        if (!DefaultConnectAddress.IsLoopback)
                        {
                            UnityEngine.Debug.LogWarning($"DefaultConnectAddress is set to `{DefaultConnectAddress.Address}`, but we expected it to be loopback as we're in mode '{RequestedPlayType}`. Using loopback instead!");
                            autoConnectEp = NetworkEndpoint.LoopbackIpv4;
                        }

                        return true;
                    }

                    // Otherwise do nothing.
                    return false;
                }
                case PlayType.Client:
                {
#if UNITY_EDITOR
                    // In the editor, the 'editor window specified' endpoint takes precedence, assuming it's a valid address:
                    if (AutoConnectPort != 0 && MultiplayerPlayModePreferences.IsEditorInputtedAddressValidForConnect(out autoConnectEp))
                        return true;
#endif

                    // Fallback to AutoConnectPort + DefaultConnectAddress.
                    if (HasDefaultAddressAndPortSet(out autoConnectEp))
                        return true;

                    // Otherwise do nothing.
                    return false;
                }
                case PlayType.Server:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(RequestedPlayType), RequestedPlayType, nameof(TryFindAutoConnectEndPoint));
            }
        }

        /// <summary>
        /// Returns true if user-code has specified both a <see cref="AutoConnectPort"/> and <see cref="DefaultConnectAddress"/>.
        /// </summary>
        /// <param name="autoConnectEp">The resulting, combined <see cref="NetworkEndpoint"/>.</param>
        /// <returns>True if user-code has specified both a <see cref="AutoConnectPort"/> and <see cref="DefaultConnectAddress"/>.</returns>
        internal static bool HasDefaultAddressAndPortSet(out NetworkEndpoint autoConnectEp)
        {
            if (AutoConnectPort != 0 && DefaultConnectAddress != NetworkEndpoint.AnyIpv4)
            {
                autoConnectEp = DefaultConnectAddress.WithPort(AutoConnectPort);
                return true;
            }

            autoConnectEp = default;
            return false;
        }

        /// <summary>
        /// Utility method for creating a new server world.
        /// Can be used in custom implementations of `Initialize` as well as in your game logic (in particular client/server build)
        /// when you need to create server programmatically (ex: frontend that allow selecting the role or other logic).
        /// </summary>
        /// <param name="name">The server world name</param>
        /// <returns></returns>
        public static World CreateServerWorld(string name)
        {
#if UNITY_CLIENT && !UNITY_SERVER && !UNITY_EDITOR
            throw new NotImplementedException();
#else

            var world = new World(name, WorldFlags.GameServer);

            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ServerSimulation);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);

#if UNITY_DOTSRUNTIME
            AppendWorldToServerTickWorld(world);
#else
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
#endif

            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = world;

            return world;
#endif
        }

        /// <summary>
        /// The default port to use for auto connection. The default value is zero, which means do not auto connect.
        /// If this is set to a valid port any call to `CreateClientWorld` - including `CreateDefaultWorlds` and `Initialize` -
        /// will try to connect to the specified port and address - assuming `DefaultConnectAddress` is valid.
        /// Any call to `CreateServerWorld` - including `CreateDefaultWorlds` and `Initialize` - will listen on the specified
        /// port and listen address.
        /// </summary>
        public static ushort AutoConnectPort = 0;
        /// <summary>
        /// <para>The default address to connect to when using auto connect (`AutoConnectPort` is not zero).
        /// If this value is `NetworkEndPoint.AnyIpv4` auto connect will not be used, even if the port is specified.
        /// This is to allow auto listen without auto connect.</para>
        /// <para>The address specified in the `Multiplayer PlayMode Tools` window takes precedence over this when running in the editor (in `PlayType.Client`).
        /// If that address is not valid or you are running in a player, then `DefaultConnectAddress` will be used instead.</para>
        /// </summary>
        /// <remarks>Note that the `DefaultConnectAddress.Port` will be clobbered by the `AutoConnectPort` if it's set.</remarks>
        public static NetworkEndpoint DefaultConnectAddress = NetworkEndpoint.LoopbackIpv4;
        /// <summary>
        /// The default address to listen on when using auto connect (`AutoConnectPort` is not zero).
        /// </summary>
        public static NetworkEndpoint DefaultListenAddress = NetworkEndpoint.AnyIpv4;
        /// <summary>
        /// Check if the server should start listening for incoming connection automatically after the world has been created.
        /// <para>
        /// If the <see cref="AutoConnectPort"/> is set, the server should start listening for connection using the <see cref="DefaultConnectAddress"/>
        /// and <see cref="AutoConnectPort"/>.
        /// </para>
        /// </summary>
        public static bool WillServerAutoListen => AutoConnectPort != 0;
        /// <summary>
        /// The current modality
        /// </summary>
        public enum PlayType
        {
            /// <summary>
            /// The application can run as client, server or both. By default, both client and server world are created
            /// and the application can host and play as client at the same time.
            /// <para>
            /// This is the default modality when playing in the editor, unless changed by using the play mode tool.
            /// </para>
            /// </summary>
            ClientAndServer = 0,
            /// <summary>
            /// The application run as a client. Only clients worlds are created and the application should connect to
            /// a server.
            /// </summary>
            Client = 1,
            /// <summary>
            /// The application run as a server. Usually only the server world is created and the application can only
            /// listen for incoming connection.
            /// </summary>
            Server = 2
        }
#if UNITY_EDITOR
        /// <summary>
        /// The current play mode, used to configure drivers and worlds.
        /// </summary>
        public static PlayType RequestedPlayType => MultiplayerPlayModePreferences.RequestedPlayType;
        /// <summary>
        /// The number of thin clients to create. Only available in the Editor.
        /// </summary>
        public static int RequestedNumThinClients => MultiplayerPlayModePreferences.RequestedNumThinClients;
#elif UNITY_SERVER
        /// <summary>
        /// The current play mode, used to configure drivers and worlds.
        /// </summary>
        public static PlayType RequestedPlayType => PlayType.Server;
#elif UNITY_CLIENT
        /// <summary>
        /// The current play mode, used to configure drivers and worlds.
        /// </summary>
        public static PlayType RequestedPlayType => PlayType.Client;
#else
        /// <summary>
        /// The current play mode, used to configure drivers and worlds.
        /// </summary>
        public static PlayType RequestedPlayType => PlayType.ClientAndServer;
#endif
        //Burst compatible counters that be used in job or ISystem to check when clients or server worlds are present
        internal struct ServerClientCount
        {
            public int serverWorlds;
            public int clientWorlds;
        }
        internal static readonly SharedStatic<ServerClientCount> WorldCounts = SharedStatic<ServerClientCount>.GetOrCreate<ClientServerBootstrap>();
        /// <summary>
        /// Check if a world with a <see cref="WorldFlags.GameServer"/> is present.
        /// <returns>If at least one world with <see cref="WorldFlags.GameServer"/> flags has been created.</returns>
        /// </summary>
        public static bool HasServerWorld => WorldCounts.Data.serverWorlds > 0;
        /// <summary>
        /// Check if a world with a <see cref="WorldFlags.GameClient"/> is present.
        /// <returns>If at least one world with <see cref="WorldFlags.GameClient"/> flags has been created.</returns>
        /// </summary>
        public static bool HasClientWorlds => WorldCounts.Data.clientWorlds > 0;
    }

    /// <summary>
    /// Netcode specific extension methods for worlds.
    /// </summary>
    public static class ClientServerWorldExtensions
    {
        /// <summary>
        /// Check if a world is a thin client.
        /// </summary>
        /// <param name="world">A <see cref="World"/> instance</param>
        /// <returns></returns>
        public static bool IsThinClient(this World world)
        {
            return (world.Flags&WorldFlags.GameThinClient) == WorldFlags.GameThinClient;
        }
        /// <summary>
        /// Check if an unmanaged world is a thin client.
        /// </summary>
        /// <param name="world">A <see cref="WorldUnmanaged"/> instance</param>
        /// <returns></returns>
        public static bool IsThinClient(this WorldUnmanaged world)
        {
            return (world.Flags&WorldFlags.GameThinClient) == WorldFlags.GameThinClient;
        }
        /// <summary>
        /// Check if a world is a client, will also return true for thin clients.
        /// </summary>
        /// <param name="world">A <see cref="World"/> instance</param>
        /// <returns></returns>
        public static bool IsClient(this World world)
        {
            return ((world.Flags&WorldFlags.GameClient) == WorldFlags.GameClient) || world.IsThinClient();
        }
        /// <summary>
        /// Check if an unmanaged world is a client, will also return true for thin clients.
        /// </summary>
        /// <param name="world">A <see cref="WorldUnmanaged"/> instance</param>
        /// <returns></returns>
        public static bool IsClient(this WorldUnmanaged world)
        {
            return ((world.Flags&WorldFlags.GameClient) == WorldFlags.GameClient) || world.IsThinClient();
        }
        /// <summary>
        /// Check if a world is a server.
        /// </summary>
        /// <param name="world">A <see cref="World"/> instance</param>
        /// <returns></returns>
        public static bool IsServer(this World world)
        {
            return (world.Flags&WorldFlags.GameServer) == WorldFlags.GameServer;
        }
        /// <summary>
        /// Check if an unmanaged world is a server.
        /// </summary>
        /// <param name="world">A <see cref="WorldUnmanaged"/> instance</param>
        /// <returns></returns>
        public static bool IsServer(this WorldUnmanaged world)
        {
            return (world.Flags&WorldFlags.GameServer) == WorldFlags.GameServer;
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [CreateAfter(typeof(NetworkStreamReceiveSystem))]
    internal partial struct ConfigureServerWorldSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!state.World.IsServer())
                throw new InvalidOperationException("Server worlds must be created with the WorldFlags.GameServer flag");
            var simulationGroup = state.World.GetExistingSystemManaged<SimulationSystemGroup>();
            simulationGroup.SetRateManagerCreateAllocator(new NetcodeServerRateManager(simulationGroup));

            var predictionGroup = state.World.GetExistingSystemManaged<PredictedSimulationSystemGroup>();
            predictionGroup.RateManager = new NetcodeServerPredictionRateManager(predictionGroup);

            ++ClientServerBootstrap.WorldCounts.Data.serverWorlds;
            if (ClientServerBootstrap.WillServerAutoListen)
            {
                SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRW.Listen(ClientServerBootstrap.DefaultListenAddress.WithPort(ClientServerBootstrap.AutoConnectPort));
            }
            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
            --ClientServerBootstrap.WorldCounts.Data.serverWorlds;
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [CreateAfter(typeof(NetworkStreamReceiveSystem))]
    internal partial struct ConfigureClientWorldSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!state.World.IsClient() && !state.World.IsThinClient())
                throw new InvalidOperationException("Client worlds must be created with the WorldFlags.GameClient flag");
            var simulationGroup = state.World.GetExistingSystemManaged<SimulationSystemGroup>();
            simulationGroup.RateManager = new NetcodeClientRateManager(simulationGroup);

            var predictionGroup = state.World.GetExistingSystemManaged<PredictedSimulationSystemGroup>();
            predictionGroup.SetRateManagerCreateAllocator(new NetcodeClientPredictionRateManager(predictionGroup));

            ++ClientServerBootstrap.WorldCounts.Data.clientWorlds;
            if (ClientServerBootstrap.TryFindAutoConnectEndPoint(out var autoConnectEp))
            {
                SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(state.EntityManager, autoConnectEp);
            }
            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
            --ClientServerBootstrap.WorldCounts.Data.clientWorlds;
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ThinClientSimulation)]
    [CreateAfter(typeof(NetworkStreamReceiveSystem))]
    internal partial struct ConfigureThinClientWorldSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!state.World.IsThinClient())
                throw new InvalidOperationException("ThinClient worlds must be created with the WorldFlags.GameThinClient flag");
            var simulationGroup = state.World.GetExistingSystemManaged<SimulationSystemGroup>();
            simulationGroup.RateManager = new NetcodeClientRateManager(simulationGroup);

            ++ClientServerBootstrap.WorldCounts.Data.clientWorlds;
            if(ClientServerBootstrap.TryFindAutoConnectEndPoint(out var autoConnectEp))
            {
                SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(state.EntityManager, autoConnectEp);
            }
            else
            {
                // Thin client has no auto connect endpoint configured to connect to. Check if the client has connected to
                // something already (so it has manually connected), if so then connect to the same address
                for (int i = 0; i < World.All.Count; ++i)
                {
                    var world = World.All[i];
                    if (world.IsClient())
                    {
                        using var driver = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamDriver>());
                        UnityEngine.Assertions.Assert.IsFalse(driver.IsEmpty);
                        var driverData = driver.ToComponentDataArray<NetworkStreamDriver>(Allocator.Temp);
                        UnityEngine.Assertions.Assert.IsTrue(driverData.Length == 1);
                        if (driverData[0].LastEndPoint.IsValid)
                            SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(state.EntityManager, driverData[0].LastEndPoint);
                        break;
                    }
                }
            }

            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
            --ClientServerBootstrap.WorldCounts.Data.clientWorlds;
        }
    }
}
