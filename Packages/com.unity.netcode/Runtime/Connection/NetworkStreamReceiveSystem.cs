using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.NetCode.LowLevel.Unsafe;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using Unity.Profiling;

namespace Unity.NetCode
{
    /// <summary>
    /// Parent group of all systems that; receive data from the server, deal with connections, and
    /// that need to perform operations before the ghost simulation group.
    /// In particular, <see cref="CommandSendSystemGroup"/>,
    /// <see cref="HeartbeatSendSystem"/>, <see cref="HeartbeatReceiveSystem"/> and the <see cref="NetworkStreamReceiveSystem"/>
    /// update in this group.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation,
        WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(BeginSimulationEntityCommandBufferSystem))]
    [UpdateBefore(typeof(GhostSimulationSystemGroup))]
    public partial class NetworkReceiveSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// Factory interface that needs to be implemented by a concrete class for creating and registering new <see cref="NetworkDriver"/> instances.
    /// </summary>
    public interface INetworkStreamDriverConstructor
    {
        /// <summary>
        /// Register to the driver store a new instance of <see cref="NetworkDriver"/> suitable to be used by clients.
        /// </summary>
        /// <param name="world"></param>
        /// <param name="driver"></param>
        /// <param name="netDebug"></param>
        void CreateClientDriver(World world, ref NetworkDriverStore driver, NetDebug netDebug);
        /// <summary>
        /// Register to the driver store a new instance of <see cref="NetworkDriver"/> suitable to be used by servers.
        /// </summary>
        /// <param name="world"></param>
        /// <param name="driver"></param>
        /// <param name="netDebug"></param>
        void CreateServerDriver(World world, ref NetworkDriverStore driver, NetDebug netDebug);
    }

    /// <summary>
    /// A system processing NetworkStreamRequestConnect components
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(NetworkReceiveSystemGroup))]
    [UpdateBefore(typeof(NetworkStreamReceiveSystem))]
    [BurstCompile]
    public partial struct NetworkStreamConnectSystem : ISystem
    {
        EntityQuery m_ConnectionRequestConnectQuery;
        ComponentLookup<ConnectionState> m_ConnectionStateFromEntity;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_ConnectionRequestConnectQuery = state.GetEntityQuery(ComponentType.ReadWrite<NetworkStreamRequestConnect>());
            m_ConnectionStateFromEntity = state.GetComponentLookup<ConnectionState>();
            state.RequireForUpdate<NetworkStreamDriver>();
            state.RequireForUpdate<NetDebug>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState systemState)
        {
            var netDebug = SystemAPI.GetSingleton<NetDebug>();
            ref var networkStreamDriver = ref SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRW;
            networkStreamDriver.ConnectionEventsList.Clear();

            if (m_ConnectionRequestConnectQuery.IsEmpty) return;
            m_ConnectionStateFromEntity.Update(ref systemState);
            var stateFromEntity = m_ConnectionStateFromEntity;

            var requests = m_ConnectionRequestConnectQuery.ToComponentDataArray<NetworkStreamRequestConnect>(Allocator.Temp);
            var requetEntity = m_ConnectionRequestConnectQuery.ToEntityArray(Allocator.Temp);
            systemState.EntityManager.RemoveComponent<NetworkStreamRequestConnect>(m_ConnectionRequestConnectQuery);
            if (requests.Length > 1)
            {
                //There is more than 1 request. We don't know what was the last queued (there is not way to detect that reliably with
                //chunk ordering). Unless we put something like a Timestamp (that requires users adding it or we need to provide a proper
                //API. We can eventually support that later. For now we just get the first request and discard the others.
                netDebug.LogError($"Found {requests.Length} pending connection requests. It is required that only one NetworkStreamRequestConnect is queued at any time. Only the connect request to {requests[0].Endpoint.ToFixedString()} will be handled.");

                for (int i = 1; i < requests.Length; ++i)
                {
                    if (stateFromEntity.HasComponent(requetEntity[i]))
                    {
                        var state = stateFromEntity[requetEntity[i]];
                        state.DisconnectReason = NetworkStreamDisconnectReason.ConnectionClose;
                        state.CurrentState = ConnectionState.State.Disconnected;
                        stateFromEntity[requetEntity[i]] = state;
                    }
                    systemState.EntityManager.DestroyEntity(requetEntity[i]);
                }
            }
            //TODO: add a proper handling of request connect and connection already connected.
            //It may required disposing the driver and also some problem with NetworkStreamReceiveSystem
            var connection = networkStreamDriver.Connect(systemState.EntityManager, requests[0].Endpoint, requetEntity[0]);
            if(connection == Entity.Null)
            {
                netDebug.LogError($"Connect request for {requests[0].Endpoint.ToFixedString()} failed.");
                if (stateFromEntity.HasComponent(requetEntity[0]))
                {
                    var state = stateFromEntity[requetEntity[0]];
                    state.DisconnectReason = NetworkStreamDisconnectReason.ConnectionClose;
                    state.CurrentState = ConnectionState.State.Disconnected;
                    stateFromEntity[requetEntity[0]] = state;
                }
                systemState.EntityManager.DestroyEntity(requetEntity[0]);
            }
        }
    }
    /// <summary>
    /// A system processing NetworkStreamRequestListen components
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(NetworkReceiveSystemGroup))]
    [UpdateBefore(typeof(NetworkStreamReceiveSystem))]
    [BurstCompile]
    public unsafe partial struct NetworkStreamListenSystem : ISystem
    {
        EntityQuery m_ConnectionRequestListenQuery;
        ComponentLookup<NetworkStreamRequestListenResult> m_ConnectionStateFromEntity;
        public void OnCreate(ref SystemState state)
        {
            m_ConnectionRequestListenQuery = state.GetEntityQuery(ComponentType.ReadWrite<NetworkStreamRequestListen>());
            m_ConnectionStateFromEntity = state.GetComponentLookup<NetworkStreamRequestListenResult>();
            state.RequireForUpdate<NetworkStreamDriver>();
            state.RequireForUpdate<NetDebug>();
        }
        public void OnUpdate(ref SystemState systemState)
        {
            var netDebug = SystemAPI.GetSingleton<NetDebug>();
            ref var networkStreamDriver = ref SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRW;
            networkStreamDriver.ConnectionEventsList.Clear();

            if (m_ConnectionRequestListenQuery.IsEmpty) return;

            m_ConnectionStateFromEntity.Update(ref systemState);
            var stateFromEntity = m_ConnectionStateFromEntity;
            var requestCount = m_ConnectionRequestListenQuery.CalculateEntityCount();
            var requestListens = m_ConnectionRequestListenQuery.ToComponentDataArray<NetworkStreamRequestListen>(Allocator.Temp);
            var requestEntity = m_ConnectionRequestListenQuery.ToEntityArray(Allocator.Temp);
            var endpoint = requestListens[0].Endpoint;
            var requestEnt = requestEntity[0];
            if (requestListens.Length > 1)
            {
                //There is more than 1 request. We don't know what was the last queued (there is not way to detect that reliably with
                //chunk ordering). Unless we put something like a Timestamp (that requires users adding it or we need to provide a proper
                //API). A proper idea can be implemented for 1.1.
                //For now we just get the first request and discard the others.
                netDebug.LogError($"Found {requestCount} pending listen requests. Only one NetworkStreamRequestListen can be queued at any time. Only the request to listen at {requestListens[0].Endpoint.ToFixedString()} will be handled.");
                for (int i = 1; i < requestEntity.Length; ++i)
                {
                    if (stateFromEntity.HasComponent(requestEnt))
                    {
                        stateFromEntity[requestEnt] = new NetworkStreamRequestListenResult
                        {
                            Endpoint = requestListens[0].Endpoint,
                            RequestState = NetworkStreamRequestListenResult.State.RefusedMultipleRequests
                        };
                    }
                }
            }

            var anyInterfaceListening = false;
            for (int i = networkStreamDriver.DriverStore.FirstDriver; i < networkStreamDriver.DriverStore.LastDriver; ++i)
            {
                anyInterfaceListening |= networkStreamDriver.DriverStore.GetDriverInstance(i).driver.Listening;
            }

            //TODO: we can support that but requires some extra work and disposing the drivers.
            //Also because this is done before the NetworkStreamReceiveSystem some stuff may not work.
            if (anyInterfaceListening)
            {
                netDebug.LogError($"Listen request for address {endpoint.ToFixedString()} refused. Driver is already listening");
                if (stateFromEntity.HasComponent(requestEnt))
                {
                    stateFromEntity[requestEnt] = new NetworkStreamRequestListenResult
                    {
                        Endpoint = requestListens[0].Endpoint,
                        RequestState = NetworkStreamRequestListenResult.State.RefusedAlreadyListening
                    };
                }
            }
            else
            {
                if (networkStreamDriver.Listen(endpoint))
                {
                    if (stateFromEntity.HasComponent(requestEnt))
                    {
                        stateFromEntity[requestEnt] = new NetworkStreamRequestListenResult
                        {
                            Endpoint = requestListens[0].Endpoint,
                            RequestState = NetworkStreamRequestListenResult.State.Succeeded
                        };
                    }
                }
                else
                {
                    netDebug.LogError($"Listen request for address {endpoint.ToFixedString()} failed.");
                    if (stateFromEntity.HasComponent(requestEnt))
                    {
                        stateFromEntity[requestEnt] = new NetworkStreamRequestListenResult
                        {
                            Endpoint = requestListens[0].Endpoint,
                            RequestState = NetworkStreamRequestListenResult.State.Failed
                        };
                    }
                }
            }
            //Consume all requests.
            systemState.EntityManager.DestroyEntity(m_ConnectionRequestListenQuery);
        }
    }

    /// <summary>
    /// The NetworkStreamReceiveSystem is one of the most important system of the NetCode package and its fundamental job
    /// is to manage all the <see cref="NetworkStreamConnection"/> life-cycles (creation, update, destruction), and receiving all the
    /// <see cref="NetworkStreamProtocol"/> message types.
    /// It is responsible also responsible for:
    /// <para>- creating the <see cref="NetworkStreamDriver"/> singleton (see also <seealso cref="NetworkDriverStore"/> and <seealso cref="NetworkDriver"/>).</para>
    /// <para>- handling the driver migration (see <see cref="DriverMigrationSystem"/> and <see cref="MigrationTicket"/>).</para>
    /// <para>- listening and accepting incoming connections (server).</para>
    /// <para>- exchanging the <see cref="NetworkProtocolVersion"/> during the initial handshake.</para>
    /// <para>- updating the <see cref="ConnectionState"/> state component if present.</para>
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(NetworkReceiveSystemGroup))]
    [CreateAfter(typeof(RpcSystem))]
    [BurstCompile]
    public unsafe partial struct NetworkStreamReceiveSystem : ISystem
    {
        static INetworkStreamDriverConstructor s_DriverConstructor;
        static readonly ProfilerMarker k_Scheduling = new ProfilerMarker("NetworkStreamReceiveSystem_Scheduling");

        /// <summary>
        /// Assign your <see cref="INetworkStreamDriverConstructor"/> to customize the <see cref="NetworkDriver"/> construction.
        /// </summary>
        public static INetworkStreamDriverConstructor DriverConstructor
        {
            get { return s_DriverConstructor ??= DefaultDriverBuilder.DefaultDriverConstructor; }
            set => s_DriverConstructor = value;
        }

        internal enum DriverState
        {
            Default,
            Migrating
        }

        ref NetworkDriverStore DriverStore => ref UnsafeUtility.AsRef<NetworkStreamDriver.Pointers>((void*)m_DriverPointers).DriverStore;
        NativeReference<int> m_NumNetworkIds;
        NativeQueue<int> m_FreeNetworkIds;
        RpcQueue<RpcSetNetworkId, RpcSetNetworkId> m_RpcQueue;

        EntityQuery m_RefreshTickRateQuery;

        IntPtr m_DriverPointers;
        ComponentLookup<ConnectionState> m_ConnectionStateFromEntity;
        ComponentLookup<GhostInstance> m_GhostComponentFromEntity;
        ComponentLookup<NetworkId> m_NetworkIdFromEntity;
        ComponentLookup<NetworkStreamRequestDisconnect> m_RequestDisconnectFromEntity;
        ComponentLookup<NetworkStreamInGame> m_InGameFromEntity;
        BufferLookup<OutgoingRpcDataStreamBuffer> m_OutgoingRpcBufferFromEntity;
        BufferLookup<IncomingRpcDataStreamBuffer> m_RpcBufferFromEntity;
        BufferLookup<IncomingCommandDataStreamBuffer> m_CmdBufferFromEntity;
        BufferLookup<IncomingSnapshotDataStreamBuffer> m_SnapshotBufferFromEntity;
        NativeList<NetCodeConnectionEvent> m_ConnectionEvents;

        public void OnCreate(ref SystemState state)
        {
            DriverMigrationSystem driverMigrationSystem = default;
            foreach (var world in World.All)
            {
                if ((driverMigrationSystem = world.GetExistingSystemManaged<DriverMigrationSystem>()) != null)
                    break;
            }

            m_NumNetworkIds = new NativeReference<int>(Allocator.Persistent);
            m_FreeNetworkIds = new NativeQueue<int>(Allocator.Persistent);
            m_ConnectionEvents = new NativeList<NetCodeConnectionEvent>(32, Allocator.Persistent);

            m_RpcQueue = SystemAPI.GetSingleton<RpcCollection>().GetRpcQueue<RpcSetNetworkId, RpcSetNetworkId>();
            m_ConnectionStateFromEntity = state.GetComponentLookup<ConnectionState>(false);
            m_GhostComponentFromEntity = state.GetComponentLookup<GhostInstance>(true);
            m_NetworkIdFromEntity = state.GetComponentLookup<NetworkId>(true);
            m_RequestDisconnectFromEntity = state.GetComponentLookup<NetworkStreamRequestDisconnect>();
            m_InGameFromEntity = state.GetComponentLookup<NetworkStreamInGame>();

            m_OutgoingRpcBufferFromEntity = state.GetBufferLookup<OutgoingRpcDataStreamBuffer>();
            m_RpcBufferFromEntity = state.GetBufferLookup<IncomingRpcDataStreamBuffer>();
            m_CmdBufferFromEntity = state.GetBufferLookup<IncomingCommandDataStreamBuffer>();
            m_SnapshotBufferFromEntity = state.GetBufferLookup<IncomingSnapshotDataStreamBuffer>();

            NetworkEndpoint lastEp = default;
            NetworkDriverStore driverStore = default;
            if (SystemAPI.HasSingleton<MigrationTicket>())
            {
                 var ticket = SystemAPI.GetSingleton<MigrationTicket>();
                 // load driver & all the network connection data
                 var driverState = driverMigrationSystem.Load(ticket.Value);
                 driverStore = driverState.DriverStore;
                 lastEp = driverState.LastEp;
                 m_NumNetworkIds.Value = driverState.NextId;
                 foreach (var id in driverState.FreeList)
                     m_FreeNetworkIds.Enqueue(id);
                 driverState.FreeList.Dispose();
            }
            else
            {
                driverStore = new NetworkDriverStore();
                driverStore.BeginDriverRegistration();
                if (state.World.IsServer())
                    DriverConstructor.CreateServerDriver(state.World, ref driverStore, SystemAPI.GetSingleton<NetDebug>());
                else
                    DriverConstructor.CreateClientDriver(state.World, ref driverStore, SystemAPI.GetSingleton<NetDebug>());
                driverStore.EndDriverRegistration();
            }

            m_DriverPointers = (IntPtr)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<NetworkStreamDriver.Pointers>(), UnsafeUtility.AlignOf<NetworkStreamDriver.Pointers>(), Allocator.Persistent);

            ref var store = ref UnsafeUtility.AsRef<NetworkStreamDriver.Pointers>((void*)m_DriverPointers);
            store.DriverStore = driverStore;
            store.ConcurrentDriverStore = driverStore.ToConcurrent();

            var networkStreamEntity = state.EntityManager.CreateEntity(ComponentType.ReadWrite<NetworkStreamDriver>());
            state.EntityManager.SetName(networkStreamEntity, "NetworkStreamDriver");
            SystemAPI.SetSingleton(new NetworkStreamDriver((void*)m_DriverPointers, m_NumNetworkIds, m_FreeNetworkIds, lastEp, m_ConnectionEvents, m_ConnectionEvents.AsReadOnly()));
            state.RequireForUpdate<GhostCollection>();
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<NetDebug>();

            var builder = new EntityQueryBuilder(Allocator.Temp).WithAll<ClientServerTickRateRefreshRequest>();
            m_RefreshTickRateQuery = state.GetEntityQuery(builder);
        }

        public void OnDestroy(ref SystemState state)
        {
            m_NumNetworkIds.Dispose();
            m_FreeNetworkIds.Dispose();
            m_ConnectionEvents.Dispose();

            ref readonly var networkStreamDriver = ref SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRO;
            if ((int)DriverState.Default == networkStreamDriver.DriverState)
            {
                var driverStore = DriverStore;
                foreach (var connection in SystemAPI.Query<RefRO<NetworkStreamConnection>>())
                {
                    driverStore.Disconnect(connection.ValueRO);
                }
                DriverStore.ScheduleUpdateAllDrivers(state.Dependency).Complete();
                DriverStore.Dispose();
            }
            UnsafeUtility.Free((void*)m_DriverPointers, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var commandBuffer = SystemAPI.GetSingleton<NetworkGroupCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var netDebug = SystemAPI.GetSingleton<NetDebug>();
            FixedString128Bytes debugPrefix = $"[{state.WorldUnmanaged.Name}][Connection]";

            if (!SystemAPI.HasSingleton<NetworkProtocolVersion>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.SetName(entity, "NetworkProtocolVersion");
                // RW is required because this call marks the collection as final which means no further rpcs can be registered
                var rpcVersion = SystemAPI.GetSingletonRW<RpcCollection>().ValueRW.CalculateVersionHash();
                var componentsVersion = GhostCollectionSystem.CalculateComponentCollectionHash(SystemAPI.GetSingletonBuffer<GhostComponentSerializer.State>());
                var gameVersion = SystemAPI.HasSingleton<GameProtocolVersion>() ? SystemAPI.GetSingleton<GameProtocolVersion>().Version : 0;
                state.EntityManager.AddComponentData(entity, new NetworkProtocolVersion
                {
                    NetCodeVersion = NetworkProtocolVersion.k_NetCodeVersion,
                    GameVersion = gameVersion,
                    RpcCollectionVersion = rpcVersion,
                    ComponentCollectionVersion = componentsVersion
                });
            }

            var driverListening = DriverStore.DriversCount > 0 && DriverStore.GetDriverInstance(DriverStore.FirstDriver).driver.Listening;
            if (driverListening)
            {
                for (int i = DriverStore.FirstDriver + 1; i < DriverStore.LastDriver; ++i)
                {
                    driverListening &= DriverStore.GetDriverInstance(i).driver.Listening;
                }
                // Detect failed listen by checking if some but not all drivers are listening
                if (!driverListening)
                {
                    for (int i = DriverStore.FirstDriver + 1; i < DriverStore.LastDriver; ++i)
                    {
                        if (DriverStore.GetDriverInstance(i).driver.Listening)
                            DriverStore.GetDriverInstance(i).StopListening();
                    }
                }
            }

            k_Scheduling.Begin();
            state.Dependency = DriverStore.ScheduleUpdateAllDrivers(state.Dependency);
            k_Scheduling.End();

            if (driverListening)
            {
                m_GhostComponentFromEntity.Update(ref state);
                // Schedule accept job
                var acceptJob = new ConnectionAcceptJob
                {
                    driverStore = DriverStore,
                    commandBuffer = commandBuffer,
                    numNetworkId = m_NumNetworkIds,
                    freeNetworkIds = m_FreeNetworkIds,
                    connectionEvents = m_ConnectionEvents,
                    rpcQueue = m_RpcQueue,
                    ghostFromEntity = m_GhostComponentFromEntity,
                    protocolVersion = SystemAPI.GetSingleton<NetworkProtocolVersion>(),
                    netDebug = netDebug,
                    debugPrefix = debugPrefix,
                };
                SystemAPI.TryGetSingleton<ClientServerTickRate>(out var tickRate);
                tickRate.ResolveDefaults();
                acceptJob.tickRate = tickRate;
                k_Scheduling.Begin();
                state.Dependency = acceptJob.Schedule(state.Dependency);
                k_Scheduling.End();
            }
            else
            {
                if (!m_RefreshTickRateQuery.IsEmptyIgnoreFilter)
                {
                    if (!SystemAPI.TryGetSingleton<ClientServerTickRate>(out var tickRate))
                        state.EntityManager.CreateSingleton(tickRate);
                    tickRate.ResolveDefaults();
                    var requests = m_RefreshTickRateQuery.ToComponentDataArray<ClientServerTickRateRefreshRequest>(Allocator.Temp);
                    foreach (var req in requests)
                    {
                        req.ApplyTo(ref tickRate);
                        netDebug.DebugLog($"Using {debugPrefix} SimulationTickRate={tickRate.SimulationTickRate} NetworkTickRate={tickRate.NetworkTickRate} MaxSimulationStepsPerFrame={tickRate.MaxSimulationStepsPerFrame} TargetFrameRateMode={tickRate.TargetFrameRateMode} PredictedPhysicsPerTick={tickRate.PredictedFixedStepSimulationTickRatio}.");
                    }
                    SystemAPI.SetSingleton(tickRate);
                    state.EntityManager.DestroyEntity(m_RefreshTickRateQuery);
                }
                m_FreeNetworkIds.Clear();
            }

            m_ConnectionStateFromEntity.Update(ref state);
            m_NetworkIdFromEntity.Update(ref state);
            m_RequestDisconnectFromEntity.Update(ref state);
            m_InGameFromEntity.Update(ref state);
            m_OutgoingRpcBufferFromEntity.Update(ref state);
            m_RpcBufferFromEntity.Update(ref state);
            m_CmdBufferFromEntity.Update(ref state);
            m_SnapshotBufferFromEntity.Update(ref state);

            // FIXME: because it uses buffer from entity
            var handleJob = new HandleDriverEvents
            {
                commandBuffer = commandBuffer,
                netDebug = netDebug,
                debugPrefix = debugPrefix,
                driverStore = DriverStore,
                networkIdFromEntity = m_NetworkIdFromEntity,
                connectionStateFromEntity = m_ConnectionStateFromEntity,
                requestDisconnectFromEntity = m_RequestDisconnectFromEntity,
                inGameFromEntity = m_InGameFromEntity,
                freeNetworkIds = m_FreeNetworkIds,
                connectionEvents = m_ConnectionEvents,

                outgoingRpcBuffer = m_OutgoingRpcBufferFromEntity,
                rpcBuffer = m_RpcBufferFromEntity,
                cmdBuffer = m_CmdBufferFromEntity,
                snapshotBuffer = m_SnapshotBufferFromEntity,

                protocolVersion = SystemAPI.GetSingleton<NetworkProtocolVersion>(),
                localTime = NetworkTimeSystem.TimestampMS,
                lastServerTick = networkTime.ServerTick,
            };
#if UNITY_EDITOR || NETCODE_DEBUG
            handleJob.netStats = SystemAPI.GetSingletonRW<GhostStatsCollectionCommand>().ValueRO.Value;
#endif
            k_Scheduling.Begin();
            state.Dependency = handleJob.ScheduleByRef(state.Dependency);
            k_Scheduling.End();
        }

        [BurstCompile]
        [StructLayout(LayoutKind.Sequential)]
        struct ConnectionAcceptJob : IJob
        {
            public EntityCommandBuffer commandBuffer;
            public NetworkDriverStore driverStore;
            public NativeReference<int> numNetworkId;
            public NativeQueue<int> freeNetworkIds;
            public NativeList<NetCodeConnectionEvent> connectionEvents;
            public RpcQueue<RpcSetNetworkId, RpcSetNetworkId> rpcQueue;
            public ClientServerTickRate tickRate;
            public NetworkProtocolVersion protocolVersion;
            public NetDebug netDebug;
            public FixedString128Bytes debugPrefix;
            [ReadOnly] public ComponentLookup<GhostInstance> ghostFromEntity;

            public void Execute()
            {
                for (int i = driverStore.FirstDriver; i < driverStore.LastDriver; ++i)
                {
                    var driver = driverStore.GetNetworkDriver(i);
                    NetworkConnection con;
                    while ((con = driver.Accept()) != default(NetworkConnection))
                    {
                        // New connection can never have any events, if this one does - just close it
                        DataStreamReader reader;
                        var evt = con.PopEvent(driver, out reader);
                        if (evt != NetworkEvent.Type.Empty)
                        {
                            con.Disconnect(driver);
                            netDebug.DebugLog(FixedString.Format("[{0}][Connection] Disconnecting stale connection detected as new (has pending event={1}).",debugPrefix, (int)evt));
                            continue;
                        }

                        //TODO: Lookup for any connection that is already connected with the same ip address or any other player identity.
                        //Relying on the IP is pretty weak test but at least is remove already some positives
                        var connection = new NetworkStreamConnection
                        {
                            Value = con,
                            DriverId = i,
                            CurrentState = ConnectionState.State.Connected,
                        };
                        var ent = commandBuffer.CreateEntity();
                        commandBuffer.AddComponent(ent, connection);
                        // rpc send buffer might need to be migrated...
                        commandBuffer.AddComponent(ent, new NetworkSnapshotAck());
                        commandBuffer.AddBuffer<PrespawnSectionAck>(ent);
                        commandBuffer.AddComponent(ent, new CommandTarget());
                        commandBuffer.AddBuffer<IncomingRpcDataStreamBuffer>(ent);
                        var rpcBuffer = commandBuffer.AddBuffer<OutgoingRpcDataStreamBuffer>(ent);
                        commandBuffer.AddBuffer<IncomingCommandDataStreamBuffer>(ent);
                        commandBuffer.AddBuffer<LinkedEntityGroup>(ent).Add(new LinkedEntityGroup{Value = ent});

                        RpcCollection.SendProtocolVersion(rpcBuffer, protocolVersion);

                        // Send RPC - assign network id
                        int nid;
                        if (!freeNetworkIds.TryDequeue(out nid))
                        {
                            // Avoid using 0
                            nid = numNetworkId.Value + 1;
                            numNetworkId.Value = nid;
                        }

                        commandBuffer.AddComponent(ent, new NetworkId {Value = nid});
                        commandBuffer.SetName(ent, new FixedString64Bytes(FixedString.Format("NetworkConnection ({0})", nid)));
                        rpcQueue.Schedule(rpcBuffer, ghostFromEntity, new RpcSetNetworkId
                        {
                            nid = nid,
                            netTickRate = tickRate.NetworkTickRate,
                            simMaxSteps = tickRate.MaxSimulationStepsPerFrame,
                            simMaxStepLength = tickRate.MaxSimulationStepBatchSize,
                            simTickRate = tickRate.SimulationTickRate,
                            fixStepTickRatio = (int)tickRate.PredictedFixedStepSimulationTickRatio
                        });

                        connectionEvents.Add(new NetCodeConnectionEvent
                        {
                            Id = new NetworkId {Value = nid},
                            ConnectionId = connection.Value,
                            State = ConnectionState.State.Connected, // Handshake is not raised on the server, go straight to Connected.
                            DisconnectReason = default,
                            ConnectionEntity = ent,
                        });

                        netDebug.DebugLog(FixedString.Format("{0} Accepted new connection {1} NetworkId={2}.", debugPrefix, connection.Value.ToFixedString(), nid));
                    }
                }
            }
        }

        [BurstCompile]
        partial struct HandleDriverEvents : IJobEntity
        {
            public EntityCommandBuffer commandBuffer;
            public NetDebug netDebug;
            public FixedString128Bytes debugPrefix;
            public NetworkDriverStore driverStore;
            [ReadOnly] public ComponentLookup<NetworkId> networkIdFromEntity;
            public ComponentLookup<ConnectionState> connectionStateFromEntity;
            public ComponentLookup<NetworkStreamRequestDisconnect> requestDisconnectFromEntity;
            public ComponentLookup<NetworkStreamInGame> inGameFromEntity;
            public NativeQueue<int> freeNetworkIds;
            public NativeList<NetCodeConnectionEvent> connectionEvents;

            public BufferLookup<OutgoingRpcDataStreamBuffer> outgoingRpcBuffer;
            public BufferLookup<IncomingRpcDataStreamBuffer> rpcBuffer;
            public BufferLookup<IncomingCommandDataStreamBuffer> cmdBuffer;
            public BufferLookup<IncomingSnapshotDataStreamBuffer> snapshotBuffer;

            public NetworkProtocolVersion protocolVersion;

            public uint localTime;
            public NetworkTick lastServerTick;
#if UNITY_EDITOR || NETCODE_DEBUG
            public NativeArray<uint> netStats;
#endif

            public void Execute(Entity entity, ref NetworkStreamConnection connection,
                ref NetworkSnapshotAck snapshotAck)
            {
                var disconnectReason = NetworkStreamDisconnectReason.ConnectionClose;
                if (Hint.Unlikely(requestDisconnectFromEntity.TryGetComponent(entity, out var disconnectRequest)))
                {
                    disconnectReason = disconnectRequest.Reason;
                    driverStore.Disconnect(connection);
                    // Disconnect cleanup will be handled below.
                }
                else if (!inGameFromEntity.HasComponent(entity))
                {
                    snapshotAck = new NetworkSnapshotAck
                    {
                        LastReceivedRemoteTime = snapshotAck.LastReceivedRemoteTime,
                        LastReceiveTimestamp = snapshotAck.LastReceiveTimestamp,
                        EstimatedRTT = snapshotAck.EstimatedRTT,
                        DeviationRTT = snapshotAck.DeviationRTT
                    };
                }

                if (Hint.Unlikely(!connection.Value.IsCreated))
                {
                    netDebug.LogError($"{debugPrefix} Stale NetworkStreamConnection.Value ({connection.Value.ToFixedString()}, driverId: {connection.DriverId}, protocolVersionReceived: {connection.ProtocolVersionReceived}) found on {entity.ToFixedString()}! Did you modify `Value` in your code?");
                    return;
                }

                if (Hint.Unlikely(!outgoingRpcBuffer.HasBuffer(entity)))
                {
                    var buf = commandBuffer.AddBuffer<OutgoingRpcDataStreamBuffer>(entity);
                    RpcCollection.SendProtocolVersion(buf, protocolVersion);
                }
                var driver = driverStore.GetNetworkDriver(connection.DriverId);

                // Update State:
                var lastState = connection.CurrentState;
                connection.CurrentState = driver.GetConnectionState(connection.Value).ToNetcodeState(networkIdFromEntity.TryGetComponent(entity, out var nid));

                // Event popping:
                DataStreamReader reader;
                NetworkEvent.Type evt;
                while ((evt = driver.PopEventForConnection(connection.Value, out reader)) != NetworkEvent.Type.Empty)
                {
                    switch (evt)
                    {
                        case NetworkEvent.Type.Connect:
                        {
                            // This event is only invoked on the client. The server bypasses, as part of the Accept() call.
                            snapshotAck.SnapshotPacketLoss = default;
                            break;
                        }
                        case NetworkEvent.Type.Disconnect:
                            if (reader.Length == 1)
                                disconnectReason = (NetworkStreamDisconnectReason) reader.ReadByte();
                            // Disconnect cleanup will be handled below.
                            goto doubleBreak;
                        case NetworkEvent.Type.Data:
                            var msgType = reader.ReadByte();
                            switch ((NetworkStreamProtocol)msgType)
                            {
                                case NetworkStreamProtocol.Command:
                                {
                                    if (!cmdBuffer.HasBuffer(entity))
                                        break;
                                    var buffer = cmdBuffer[entity];
                                    var snapshot = new NetworkTick{SerializedData = reader.ReadUInt()};
                                    uint snapshotMask = reader.ReadUInt();
                                    snapshotAck.UpdateReceivedByRemote(snapshot, snapshotMask);
                                    uint remoteTime = reader.ReadUInt();
                                    uint localTimeMinusRTT = reader.ReadUInt();
                                    uint interpolationDelay = reader.ReadUInt();
                                    uint numLoadedPrefabs = reader.ReadUInt();

                                    snapshotAck.UpdateRemoteAckedData(remoteTime, numLoadedPrefabs, interpolationDelay);
                                    snapshotAck.UpdateRemoteTime(remoteTime, localTimeMinusRTT, localTime);
                                    var tickReader = reader;
                                    var cmdTick = new NetworkTick{SerializedData = tickReader.ReadUInt()};
                                    var isValidCmdTick = !snapshotAck.LastReceivedSnapshotByLocal.IsValid || cmdTick.IsNewerThan(snapshotAck.LastReceivedSnapshotByLocal);
#if UNITY_EDITOR || NETCODE_DEBUG
                                    netStats[0] = lastServerTick.SerializedData;
                                    netStats[1] = (uint)reader.Length - 1u;
                                    if (!isValidCmdTick || buffer.Length > 0)
                                    {
                                        netStats[2] = netStats[2] + 1;
                                    }
#endif
                                    // Do not try to process incoming commands which are older than commands we already processed
                                    if (!isValidCmdTick)
                                        break;
                                    snapshotAck.LastReceivedSnapshotByLocal = cmdTick;

                                    buffer.Clear();
                                    buffer.Add(ref reader);
                                    break;
                                }
                                case NetworkStreamProtocol.Snapshot:
                                {
                                    if (Hint.Unlikely(!snapshotBuffer.TryGetBuffer(entity, out var buffer)))
                                        break;

                                    uint remoteTime = reader.ReadUInt();
                                    uint localTimeMinusRTT = reader.ReadUInt();
                                    snapshotAck.ServerCommandAge = reader.ReadInt();
                                    snapshotAck.UpdateRemoteTime(remoteTime, localTimeMinusRTT, localTime);

                                    // SSId:
                                    var currentSnapshotSequenceId = reader.ReadByte();

                                    // Copy the reader here, as we want to pass the ServerTick into the GhostReceiveSystem,
                                    // and that'll fail if we read too far.
                                    var copyOfReader = reader;
                                    var newServerTick = new NetworkTick{SerializedData = copyOfReader.ReadUInt()};

                                    // Skip old snapshots:
                                    var isValid = !snapshotAck.LastReceivedSnapshotByLocal.IsValid || newServerTick.IsNewerThan(snapshotAck.LastReceivedSnapshotByLocal);
                                    UpdatePacketLossStats(ref snapshotAck.SnapshotPacketLoss, isValid, currentSnapshotSequenceId, ref snapshotAck, in netDebug, buffer);
                                    if (!isValid)
                                        break;
                                    snapshotAck.LastReceivedSnapshotByLocal = newServerTick;
                                    snapshotAck.CurrentSnapshotSequenceId = currentSnapshotSequenceId;

                                    // Limitation: Clobber any previous snapshot, even if said snapshot has not been processed yet.
                                    if (buffer.Length > 0)
                                    {
#if UNITY_EDITOR || NETCODE_DEBUG
                                        netStats[2] = netStats[2] + 1;
#endif
                                        buffer.Clear();
                                    }

                                    // Save the new snapshot to the buffer, so we can process it in GhostReceiveSystem.
                                    buffer.Add(ref reader);
                                    break;
                                }
                                case NetworkStreamProtocol.Rpc:
                                {
                                    uint remoteTime = reader.ReadUInt();
                                    uint localTimeMinusRTT = reader.ReadUInt();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                                    UnityEngine.Debug.Assert(reader.GetBytesRead() == RpcCollection.k_RpcCommonHeaderLengthBytes);
#endif
                                    snapshotAck.UpdateRemoteTime(remoteTime, localTimeMinusRTT, localTime);
                                    var buffer = rpcBuffer[entity];
                                    buffer.Add(ref reader);
                                    break;
                                }
                                default:
                                    netDebug.LogError(FixedString.Format("Received unknown message type {0}", msgType));
                                    break;
                            }

                            break;
                        default:
                            netDebug.LogError(FixedString.Format("Received unknown network event {0}", (int)evt));
                            break;
                    }
                }
                doubleBreak:

                // React to changes:
                if(Hint.Unlikely(connection.CurrentState != lastState))
                {
                    // Raise Events: See rules in network-connection.md.
                    // Note that we intentionally bypass states on the server here,
                    // so there are other event evocations scattered around.
                    connectionEvents.Add(new NetCodeConnectionEvent
                    {
                        Id = new NetworkId {Value = nid.Value},
                        ConnectionId = connection.Value,
                        State = connection.CurrentState,
                        DisconnectReason = disconnectReason,
                        ConnectionEntity = entity,
                    });
                }

                // Handle disconnects:
                // Fix for issue where: Transport does not raise the Disconnect event locally for any connection that is manually Disconnected.
                // Thus, we (Netcode) need to duplicate the event via status polling.
                if (Hint.Unlikely(connection.CurrentState == ConnectionState.State.Disconnected))
                {
                    commandBuffer.RemoveComponent<NetworkStreamConnection>(entity);
                    commandBuffer.DestroyEntity(entity);

                    if (cmdBuffer.HasBuffer(entity))
                        cmdBuffer[entity].Clear();

                    var id = -1;
                    if (networkIdFromEntity.HasComponent(entity))
                    {
                        id = networkIdFromEntity[entity].Value;
                        freeNetworkIds.Enqueue(id);
                    }

                    netDebug.DebugLog($"{debugPrefix} {connection.Value.ToFixedString()} closed NetworkId={id} Reason={disconnectReason.ToFixedString()}.");
                    connection.Value = default;
                }

                // Update ConnectionState:
                if (connectionStateFromEntity.TryGetComponent(entity, out var existingState))
                {
                    var newState = existingState;
                    newState.DisconnectReason = disconnectReason;
                    newState.CurrentState = connection.CurrentState;
                    newState.NetworkId = nid.Value;
                    if (Hint.Unlikely(!existingState.Equals(newState)))
                        connectionStateFromEntity[entity] = newState;
                }
            }

            /// <summary>
            /// Records SnapshotSequenceId [SSId] statistics, detecting packet loss, packet duplication, and out of order packets.
            /// </summary>
            // ReSharper disable once UnusedParameter.Local
            private static void UpdatePacketLossStats(ref SnapshotPacketLossStatistics stats, bool snapshotIsConfirmedNewer, in byte currentSnapshotSequenceId, ref NetworkSnapshotAck snapshotAck, in NetDebug netDebug, DynamicBuffer<IncomingSnapshotDataStreamBuffer> buffer)
            {
                if (stats.NumPacketsReceived == 0) snapshotAck.CurrentSnapshotSequenceId = (byte) (currentSnapshotSequenceId - 1);
                stats.NumPacketsReceived++;

                var sequenceIdDelta = snapshotAck.CalculateSequenceIdDelta(currentSnapshotSequenceId, snapshotIsConfirmedNewer);
                if (snapshotIsConfirmedNewer)
                {
                    // Detect packet loss:
                    var numDroppedPackets = sequenceIdDelta - 1;
                    if (numDroppedPackets > 0)
                    {
                        stats.NumPacketsDroppedNeverArrived += (ulong) numDroppedPackets;
#if NETCODE_DEBUG
                        // TODO - Make it possible to access NetDebugPacket.Log((FixedString512Bytes)$"[SSId:{currentSnapshotSequenceId}] Inferred {numDroppedPackets} snapshots dropped!");
#endif
                    }

                    // Netcode limitation: We can only process one snapshot per tick!
                    if (buffer.Length > 0)
                    {
                        stats.NumPacketsCulledAsArrivedOnSameFrame++;
#if NETCODE_DEBUG
                    // TODO - Make it possible to access NetDebugPacket.Log((FixedString512Bytes)$"[SSId:{currentSnapshotSequenceId}] Clobbering previous snapshot ({stats->LastSnapshotSequenceId}) as it arrived on same frame!");
#endif
                    }

                    return;
                }

                // Detect out of order and duplicate packets:
                if (sequenceIdDelta == 0)
                {
                    // We can't track any previous duplicate packets (unless we keep an ack history),
                    // so we don't track it at all. Just log.
#if NETCODE_DEBUG
                        // TODO - Make it possible to access NetDebugPacket.Log((FixedString512Bytes) $"[SSId:{currentSnapshotSequenceId}] Detected duplicated snapshot packet!");
#endif
                    return;
                }

                stats.NumPacketsCulledOutOfOrder++;
                // Technically a packet we skipped over was counted as dropped, but it just arrived.
                // We may not even know about it, as jitter during connection can cause us to detect
                // dropped packets that we should never have received anyway.
                if (stats.NumPacketsDroppedNeverArrived > 0)
                    stats.NumPacketsDroppedNeverArrived--;
#if NETCODE_DEBUG
                        // TODO - Make it possible to access NetDebugPacket.Log((FixedString512Bytes) $"[SSId:{currentSnapshotSequenceId}] Arrived {math.abs(sequenceIdDelta)} ServerTicks late!");
#endif
            }
        }
    }
}
