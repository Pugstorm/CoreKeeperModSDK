using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.NetCode.LowLevel.Unsafe;
using Unity.Networking.Transport;
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
    public unsafe partial struct NetworkStreamConnectSystem : ISystem
    {
        EntityQuery m_ConnectionRequestConnectQuery;
        ComponentLookup<ConnectionState> m_ConnectionStateFromEntity;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_ConnectionRequestConnectQuery = state.GetEntityQuery(ComponentType.ReadWrite<NetworkStreamRequestConnect>());
            m_ConnectionStateFromEntity = state.GetComponentLookup<ConnectionState>();
            state.RequireForUpdate(m_ConnectionRequestConnectQuery);
            state.RequireForUpdate<NetworkStreamDriver>();
            state.RequireForUpdate<NetDebug>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState systemState)
        {
            var netDebug = SystemAPI.GetSingleton<NetDebug>();
            ref var networkStreamDriver = ref SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRW;
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
            state.RequireForUpdate(m_ConnectionRequestListenQuery);
            state.RequireForUpdate<NetworkStreamDriver>();
            state.RequireForUpdate<NetDebug>();
        }
        public void OnUpdate(ref SystemState systemState)
        {
            var netDebug = SystemAPI.GetSingleton<NetDebug>();
            ref var networkStreamDriver = ref SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRW;

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
    [CreateAfter(typeof(NetDebugSystem))]
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
        ComponentLookup<ClientServerTickRate> m_ClientServerTickRateFromEntity;
        ComponentLookup<NetworkStreamRequestDisconnect> m_RequestDisconnectFromEntity;
        ComponentLookup<NetworkStreamInGame> m_InGameFromEntity;
        BufferLookup<OutgoingRpcDataStreamBuffer> m_OutgoingRpcBufferFromEntity;
        BufferLookup<IncomingRpcDataStreamBuffer> m_RpcBufferFromEntity;
        BufferLookup<IncomingCommandDataStreamBuffer> m_CmdBufferFromEntity;
        BufferLookup<IncomingSnapshotDataStreamBuffer> m_SnapshotBufferFromEntity;

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

            m_RpcQueue = SystemAPI.GetSingleton<RpcCollection>().GetRpcQueue<RpcSetNetworkId, RpcSetNetworkId>();
            m_ConnectionStateFromEntity = state.GetComponentLookup<ConnectionState>(false);
            m_GhostComponentFromEntity = state.GetComponentLookup<GhostInstance>(true);
            m_NetworkIdFromEntity = state.GetComponentLookup<NetworkId>(true);
            m_ClientServerTickRateFromEntity = state.GetComponentLookup<ClientServerTickRate>();
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
            SystemAPI.SetSingleton(new NetworkStreamDriver((void*)m_DriverPointers, m_NumNetworkIds, m_FreeNetworkIds, lastEp));
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
            var commandBuffer = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
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
                SystemAPI.TryGetSingleton<ClientServerTickRate>(out var tickRate);
                var acceptJob = new ConnectionAcceptJob
                {
                    driverStore = DriverStore,
                    commandBuffer = commandBuffer,
                    numNetworkId = m_NumNetworkIds,
                    freeNetworkIds = m_FreeNetworkIds,
                    rpcQueue = m_RpcQueue,
                    ghostFromEntity = m_GhostComponentFromEntity,
                    tickRate = tickRate,
                    protocolVersion = SystemAPI.GetSingleton<NetworkProtocolVersion>(),
                    netDebug = netDebug,
                    debugPrefix = debugPrefix
                };
                acceptJob.tickRate.ResolveDefaults();
                k_Scheduling.Begin();
                state.Dependency = acceptJob.Schedule(state.Dependency);
                k_Scheduling.End();
            }
            else
            {
                if (!state.WorldUnmanaged.IsServer() && !SystemAPI.HasSingleton<ClientServerTickRate>())
                {
                    var newEntity = state.EntityManager.CreateEntity();
                    var tickRate = new ClientServerTickRate();
                    tickRate.ResolveDefaults();
                    state.EntityManager.AddComponentData(newEntity, tickRate);
                }
                if (!m_RefreshTickRateQuery.IsEmptyIgnoreFilter)
                {
                    m_ClientServerTickRateFromEntity.Update(ref state);
                    var refreshJob = new RefreshClientServerTickRate
                    {
                        commandBuffer = commandBuffer,
                        netDebug = netDebug,
                        debugPrefix = debugPrefix,
                        tickRateEntity = SystemAPI.GetSingletonEntity<ClientServerTickRate>(),
                        dataFromEntity = m_ClientServerTickRateFromEntity
                    };
                    k_Scheduling.Begin();
                    state.Dependency = refreshJob.ScheduleByRef(state.Dependency);
                    k_Scheduling.End();
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

                outgoingRpcBuffer = m_OutgoingRpcBufferFromEntity,
                rpcBuffer = m_RpcBufferFromEntity,
                cmdBuffer = m_CmdBufferFromEntity,
                snapshotBuffer = m_SnapshotBufferFromEntity,

                protocolVersion = SystemAPI.GetSingleton<NetworkProtocolVersion>(),
                localTime = NetworkTimeSystem.TimestampMS,
                serverTick = networkTime.ServerTick
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
                            DriverId = i
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
                            simTickRate = tickRate.SimulationTickRate
                        });
                        netDebug.DebugLog(FixedString.Format("{0} Accepted new connection {1} NetworkId={2}", debugPrefix, connection.Value.ToFixedString(), nid));
                    }
                }
            }
        }

        [BurstCompile]
        [StructLayout(LayoutKind.Sequential)]
        partial struct RefreshClientServerTickRate : IJobEntity
        {
            public EntityCommandBuffer commandBuffer;
            public NetDebug netDebug;
            public FixedString128Bytes debugPrefix;
            public Entity tickRateEntity;
            public ComponentLookup<ClientServerTickRate> dataFromEntity;
            public void Execute(Entity entity, in ClientServerTickRateRefreshRequest req)
            {
                var tickRate = dataFromEntity[tickRateEntity];
                tickRate.MaxSimulationStepsPerFrame = req.MaxSimulationStepsPerFrame;
                tickRate.NetworkTickRate = req.NetworkTickRate;
                tickRate.SimulationTickRate = req.SimulationTickRate;
                tickRate.MaxSimulationStepBatchSize = req.MaxSimulationStepBatchSize;
                dataFromEntity[tickRateEntity] = tickRate;
                var dbgMsg = FixedString.Format("Using SimulationTickRate={0} NetworkTickRate={1} MaxSimulationStepsPerFrame={2} TargetFrameRateMode={3}", tickRate.SimulationTickRate, tickRate.NetworkTickRate, tickRate.MaxSimulationStepsPerFrame, (int)tickRate.TargetFrameRateMode);
                netDebug.DebugLog(FixedString.Format("{0} {1}", debugPrefix, dbgMsg));
                commandBuffer.RemoveComponent<ClientServerTickRateRefreshRequest>(entity);
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

            public BufferLookup<OutgoingRpcDataStreamBuffer> outgoingRpcBuffer;
            public BufferLookup<IncomingRpcDataStreamBuffer> rpcBuffer;
            public BufferLookup<IncomingCommandDataStreamBuffer> cmdBuffer;
            public BufferLookup<IncomingSnapshotDataStreamBuffer> snapshotBuffer;

            public NetworkProtocolVersion protocolVersion;

            public uint localTime;
            public NetworkTick serverTick;
#if UNITY_EDITOR || NETCODE_DEBUG
            public NativeArray<uint> netStats;
#endif
            public void Execute(Entity entity, ref NetworkStreamConnection connection,
                ref NetworkSnapshotAck snapshotAck)
            {
                if (requestDisconnectFromEntity.HasComponent(entity))
                {
                    var disconnect = requestDisconnectFromEntity[entity];
                    var id = -1;
                    if (networkIdFromEntity.HasComponent(entity))
                    {
                        id = networkIdFromEntity[entity].Value;
                        freeNetworkIds.Enqueue(id);
                    }
                    driverStore.Disconnect(connection);
                    if (connectionStateFromEntity.HasComponent(entity))
                    {
                        var state = connectionStateFromEntity[entity];
                        state.DisconnectReason = disconnect.Reason;
                        state.CurrentState = ConnectionState.State.Disconnected;
                        connectionStateFromEntity[entity] = state;
                    }
                    commandBuffer.DestroyEntity(entity); // This can cause issues if some other system adds components while it is in the queue
                    netDebug.DebugLog(FixedString.Format("{0} Disconnecting {1} NetworkId={2} Reason={3}", debugPrefix, connection.Value.ToFixedString(), id, DisconnectReasonEnumToString.Convert((int)disconnect.Reason)));
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

                if (!connection.Value.IsCreated)
                    return;
                if (!outgoingRpcBuffer.HasBuffer(entity))
                {
                    var buf = commandBuffer.AddBuffer<OutgoingRpcDataStreamBuffer>(entity);
                    RpcCollection.SendProtocolVersion(buf, protocolVersion);
                }
                var driver = driverStore.GetNetworkDriver(connection.DriverId);
                if (connectionStateFromEntity.HasComponent(entity))
                {
                    var state = connectionStateFromEntity[entity];
                    var newState = state;
                    switch (driver.GetConnectionState(connection.Value))
                    {
                    case NetworkConnection.State.Disconnected:
                        newState.CurrentState = ConnectionState.State.Disconnected;
                        break;
                    case NetworkConnection.State.Connecting:
                        newState.CurrentState = ConnectionState.State.Connecting;
                        break;
                    case NetworkConnection.State.Connected:
                        newState.CurrentState = ConnectionState.State.Connected;
                        break;
                    default:
                        newState.CurrentState = ConnectionState.State.Unknown;
                        break;
                    }
                    if (newState.CurrentState == ConnectionState.State.Connected)
                    {
                        if (networkIdFromEntity.HasComponent(entity))
                            newState.NetworkId = networkIdFromEntity[entity].Value;
                        else
                            newState.CurrentState = ConnectionState.State.Handshake;
                    }
                    if (!state.Equals(newState))
                        connectionStateFromEntity[entity] = newState;
                }
                DataStreamReader reader;
                NetworkEvent.Type evt;
                while ((evt = driver.PopEventForConnection(connection.Value, out reader)) != NetworkEvent.Type.Empty)
                {
                    switch (evt)
                    {
                        case NetworkEvent.Type.Connect:
                            break;
                        case NetworkEvent.Type.Disconnect:
                            var reason = NetworkStreamDisconnectReason.ConnectionClose;
                            if (reader.Length == 1)
                                reason = (NetworkStreamDisconnectReason)reader.ReadByte();

                            if (connectionStateFromEntity.HasComponent(entity))
                            {
                                var state = connectionStateFromEntity[entity];
                                state.CurrentState = ConnectionState.State.Disconnected;
                                state.DisconnectReason = reason;
                                connectionStateFromEntity[entity] = state;
                            }

                            commandBuffer.DestroyEntity(entity);

                            if (cmdBuffer.HasBuffer(entity))
                                cmdBuffer[entity].Clear();
                            connection.Value = default(NetworkConnection);
                            var id = -1;
                            if (networkIdFromEntity.HasComponent(entity))
                            {
                                id = networkIdFromEntity[entity].Value;
                                freeNetworkIds.Enqueue(id);
                            }
                            netDebug.DebugLog(FixedString.Format("{0} {1} closed NetworkId={2} Reason={3}", debugPrefix, connection.Value.ToFixedString(), id, DisconnectReasonEnumToString.Convert((int)reason)));
                            return;
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
                                    netStats[0] = serverTick.SerializedData;
                                    netStats[1] = (uint)reader.Length - 1u;
                                    if (!isValidCmdTick || buffer.Length > 0)
                                        netStats[2] = netStats[2] + 1;
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
                                    if (!snapshotBuffer.HasBuffer(entity))
                                        break;
                                    uint remoteTime = reader.ReadUInt();
                                    uint localTimeMinusRTT = reader.ReadUInt();
                                    snapshotAck.ServerCommandAge = reader.ReadInt();
                                    snapshotAck.UpdateRemoteTime(remoteTime, localTimeMinusRTT, localTime);

                                    var buffer = snapshotBuffer[entity];
#if UNITY_EDITOR || NETCODE_DEBUG
                                    if (buffer.Length > 0)
                                        netStats[2] = netStats[2] + 1;
#endif
                                    buffer.Clear();
                                    buffer.Add(ref reader);
                                    break;
                                }
                                case NetworkStreamProtocol.Rpc:
                                {
                                    uint remoteTime = reader.ReadUInt();
                                    uint localTimeMinusRTT = reader.ReadUInt();
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
            }
        }
    }
}
