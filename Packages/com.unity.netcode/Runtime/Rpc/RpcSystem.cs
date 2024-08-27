using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.NetCode.LowLevel.Unsafe;
using Unity.Networking.Transport;

using System.Runtime.InteropServices;
using Unity.Assertions;
using Unity.Burst.Intrinsics;

namespace Unity.NetCode
{
    /// <summary>
    /// Struct that can be used to simplify writing systems and jobs that deserialize and execute received rpc commands.
    /// </summary>
    public struct RpcExecutor
    {
        /// <summary>
        /// Struct used as argument to the rpc execute method (see the <see cref="ExecuteDelegate"/> delegate).
        /// Contains the input data stream, the receiving connection, and other useful data that can be used
        /// to decode and write your rpc logic.
        /// </summary>
        public struct Parameters
        {
            /// <summary>
            /// The data-stream that contains the rpc data.
            /// </summary>
            public DataStreamReader Reader;
            /// <summary>
            /// The connection that received the rpc.
            /// </summary>
            public Entity Connection;
            /// <summary>
            /// A command buffer that be used to make structural changes.
            /// </summary>
            public EntityCommandBuffer.ParallelWriter CommandBuffer;
            /// <summary>
            /// The sort order that must be used to add commands to command buffer.
            /// </summary>
            public int JobIndex;
            /// <summary>
            /// A pointer to a <see cref="RpcDeserializerState"/> instance.
            /// </summary>
            internal IntPtr State;
            /// <summary>
            /// An instance of <see cref="RpcDeserializerState"/> that can be used to deserialize the rpcs.
            /// </summary>
            public RpcDeserializerState DeserializerState
            {
                get { unsafe { return UnsafeUtility.AsRef<RpcDeserializerState>((void*)State); } }
            }
        }

        /// <summary>
        /// The reference to static burst-compatible method that is invoked when an rpc has been received.
        /// For example:
        /// <code>
        ///     [BurstCompile(DisableDirectCall = true)]
        ///     [AOT.MonoPInvokeCallback(typeof(RpcExecutor.ExecuteDelegate))]
        ///     private static void InvokeExecute(ref RpcExecutor.Parameters parameters)
        /// </code>
        /// </summary>
        /// <remarks>
        /// The <code>DisableDirectCall = true</code> was necessary to workaround an issue with burst and function delegate.
        /// If you are implementing your custom rpc serializer, please remember to disable the direct call.
        /// </remarks>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ExecuteDelegate(ref Parameters parameters);

        /// <summary>
        /// Helper method that can be used to implement the execute method for the <see cref="IRpcCommandSerializer{T}"/>
        /// interface.
        /// By calling the ExecuteCreateRequestComponent, a new entity (with a <typeparamref name="TActionRequest"/> and
        /// a <see cref="ReceiveRpcCommandRequest"/> component) is created.
        /// It is the users responsibility to write a system that consumes the created rpcs entities. For example:
        /// <code>
        /// public struct MyRpcConsumeSystem : ISystem
        /// {
        ///    private Query rcpQuery;
        ///    public void OnCreate(ref SystemState state)
        ///    {
        ///        var builder = new EntityQueryBuilder(Allocator.Temp).WithAll&lt;MyRpc, ReceiveRpcCommandRequestComponent&gt;();
        ///        rcpQuery = state.GetEntityQuery(builder);
        ///    }
        ///    public void OnUpdate(ref SystemState state)
        ///    {
        ///         foreach(var rpc in SystemAPI.Query&lt;MyRpc&gt;().WithAll&lt;ReceiveRpcCommandRequestComponent&gt;())
        ///         {
        ///             //do something with the rpc
        ///         }
        ///         //Consumes all of them
        ///         state.EntityManager.DestroyEntity(rpcQuery);
        ///    }
        /// }
        /// </code>
        /// </summary>
        /// <param name="parameters">Container for <see cref="EntityCommandBuffer"/>, JobIndex, as well as connection entity.</param>
        /// <typeparam name="TActionSerializer">Struct of type <see cref="IRpcCommandSerializer{TActionRequest}"/>.</typeparam>
        /// <typeparam name="TActionRequest">Unmanaged type of <see cref="IComponentData"/>.</typeparam>
        /// <returns>Created entity for RPC request. Name of the Entity is set as 'NetCodeRPC'.</returns>
        public static Entity ExecuteCreateRequestComponent<TActionSerializer, TActionRequest>(ref Parameters parameters)
            where TActionRequest : unmanaged, IComponentData
            where TActionSerializer : struct, IRpcCommandSerializer<TActionRequest>
        {
            var rpcData = default(TActionRequest);

            var rpcSerializer = default(TActionSerializer);
            rpcSerializer.Deserialize(ref parameters.Reader, parameters.DeserializerState, ref rpcData);
            var entity = parameters.CommandBuffer.CreateEntity(parameters.JobIndex);
            parameters.CommandBuffer.AddComponent(parameters.JobIndex, entity, new ReceiveRpcCommandRequest {SourceConnection = parameters.Connection});
            parameters.CommandBuffer.AddComponent(parameters.JobIndex, entity, rpcData);

#if !DOTS_DISABLE_DEBUG_NAMES
            parameters.CommandBuffer.SetName(parameters.JobIndex, entity, "NetCodeRPC");
#endif
            return entity;
        }
    }

    /// <summary>
    /// <para>
    /// The system responsible for sending and receiving RPCs.
    /// </para>
    /// <para>
    /// The RpcSystem flushes all the outgoing RPCs scheduled in the <see cref="OutgoingRpcDataStreamBuffer"/> for all the active connections.
    /// Multiple RPCs can be raised by a world (to be sent in a single frame) to each connection. Therefore, in order to reduce the number of in-flight reliable messages,
    /// the system tries to coalesce multiple RPCs into a single packet.
    /// </para>
    /// <para>
    /// Because packet queue size is limited (<see cref="NetworkParameterConstants.SendQueueCapacity"/> and <seealso cref="NetworkConfigParameter"/>), the
    /// number of available packets may not be sufficient to flush the queue entirely. In that case, the pending messages are going to attempt to be
    /// sent during the next frame (recursively) (or when a resource is available).
    /// </para>
    /// <para>
    /// When an rpc packet is received, it is first handled by the <see cref="NetworkStreamReceiveSystem"/>, which decodes the incoming network packet
    /// and appends it to the <see cref="IncomingRpcDataStreamBuffer"/> for the connection that received the message.
    /// The RpcSystem will then dequeue all the received messages, and dispatch them by invoking their execute method (<see cref="IRpcCommandSerializer{T}"/>
    /// and <see cref="RpcExecutor"/>).
    /// </para>
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
    [BurstCompile]
    public partial struct RpcSystem : ISystem
    {
        /// <summary>
        /// During the initial handshake, the client and server exchanges their respective <see cref="NetworkProtocolVersion"/> by using
        /// a internal rpc.
        /// When received, the RpcSystem will perform a protocol check, that verifies that the versions are compatible.
        /// If the verification fails, a new entity with a <see cref="ProtocolVersionError"/> component is created;
        /// the generated error is then handled by the <see cref="RpcSystemErrors"/> system.
        /// </summary>
        internal struct ProtocolVersionError : IComponentData
        {
            public Entity connection;
            public NetworkProtocolVersion remoteProtocol;
        }

        private NativeList<RpcCollection.RpcData> m_RpcData;
        private NativeParallelHashMap<ulong, int> m_RpcTypeHashToIndex;
        private NativeReference<byte> m_DynamicAssemblyList;

        private EntityQuery m_RpcBufferGroup;

        private EntityTypeHandle m_EntityTypeHandle;
        private ComponentTypeHandle<NetworkStreamConnection> m_NetworkStreamConnectionHandle;
        private BufferTypeHandle<IncomingRpcDataStreamBuffer> m_IncomingRpcDataStreamBufferComponentHandle;
        private BufferTypeHandle<OutgoingRpcDataStreamBuffer> m_OutgoingRpcDataStreamBufferComponentHandle;
        private ComponentTypeHandle<NetworkSnapshotAck> m_NetworkSnapshotAckComponentHandle;

        public void OnCreate(ref SystemState state)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            UnityEngine.Debug.Assert(UnsafeUtility.SizeOf<OutgoingRpcDataStreamBuffer>() == 1);
            UnityEngine.Debug.Assert(UnsafeUtility.SizeOf<IncomingRpcDataStreamBuffer>() == 1);
#endif

            m_RpcData = new NativeList<RpcCollection.RpcData>(16, Allocator.Persistent);
            m_RpcTypeHashToIndex = new NativeParallelHashMap<ulong, int>(16, Allocator.Persistent);
            m_DynamicAssemblyList = new NativeReference<byte>(Allocator.Persistent);
            var rpcSingleton = state.EntityManager.CreateEntity(ComponentType.ReadWrite<RpcCollection>());
            state.EntityManager.SetName(rpcSingleton, "RpcCollection-Singleton");
            state.EntityManager.SetComponentData(rpcSingleton, new RpcCollection
            {
                m_DynamicAssemblyList = m_DynamicAssemblyList,
                m_RpcData = m_RpcData,
                m_RpcTypeHashToIndex = m_RpcTypeHashToIndex,
                m_IsFinal = 0
            });

            m_RpcBufferGroup = state.GetEntityQuery(
                ComponentType.ReadWrite<IncomingRpcDataStreamBuffer>(),
                ComponentType.ReadWrite<OutgoingRpcDataStreamBuffer>(),
                ComponentType.ReadWrite<NetworkStreamConnection>(),
                ComponentType.ReadOnly<NetworkSnapshotAck>());
            state.RequireForUpdate(m_RpcBufferGroup);

            m_EntityTypeHandle = state.GetEntityTypeHandle();
            m_NetworkStreamConnectionHandle = state.GetComponentTypeHandle<NetworkStreamConnection>();
            m_IncomingRpcDataStreamBufferComponentHandle = state.GetBufferTypeHandle<IncomingRpcDataStreamBuffer>();
            m_OutgoingRpcDataStreamBufferComponentHandle = state.GetBufferTypeHandle<OutgoingRpcDataStreamBuffer>();
            m_NetworkSnapshotAckComponentHandle = state.GetComponentTypeHandle<NetworkSnapshotAck>(true);

            SystemAPI.GetSingleton<RpcCollection>().RegisterRpc(ComponentType.ReadWrite<RpcSetNetworkId>(), default(RpcSetNetworkId).CompileExecute());
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            m_RpcData.Dispose();
            m_RpcTypeHashToIndex.Dispose();
            m_DynamicAssemblyList.Dispose();
        }

        [BurstCompile]
        struct RpcExecJob : IJobChunk
        {
            public EntityCommandBuffer.ParallelWriter commandBuffer;
            [ReadOnly] public EntityTypeHandle entityType;
            public ComponentTypeHandle<NetworkStreamConnection> connectionType;
            public BufferTypeHandle<IncomingRpcDataStreamBuffer> inBufferType;
            public BufferTypeHandle<OutgoingRpcDataStreamBuffer> outBufferType;
            [ReadOnly] public NativeList<RpcCollection.RpcData> execute;
            [ReadOnly] public NativeParallelHashMap<ulong, int> hashToIndex;
            [ReadOnly] public NativeParallelHashMap<SpawnedGhost, Entity>.ReadOnly ghostMap;

            [ReadOnly] public ComponentTypeHandle<NetworkSnapshotAck> ackType;
            public uint localTime;

            public ConcurrentDriverStore concurrentDriverStore;
            public NetworkProtocolVersion protocolVersion;
            public byte dynamicAssemblyList;
            public FixedString128Bytes worldName;
            public NetDebug netDebug;

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // This job is not written to support queries with enableable component types.
                Assert.IsFalse(useEnabledMask);

                var entities = chunk.GetNativeArray(entityType);
                var rpcInBuffer = chunk.GetBufferAccessor(ref inBufferType);
                var rpcOutBuffer = chunk.GetBufferAccessor(ref outBufferType);
                var connections = chunk.GetNativeArray(ref connectionType);
                var acks = chunk.GetNativeArray(ref ackType);
                var deserializeState = new RpcDeserializerState {ghostMap = ghostMap};
                for (int i = 0; i < rpcInBuffer.Length; ++i)
                {
                    var concurrentDriver = concurrentDriverStore.GetConcurrentDriver(connections[i].DriverId);
                    ref var driver = ref concurrentDriver.driver;
                    var conState = concurrentDriver.driver.GetConnectionState(connections[i].Value);

                    // If we're now in a disconnected state check if the protocol version RPC is in the incoming buffer so we can process it and report an error if it's mismatched (reason for the disconnect)
                    if (conState == NetworkConnection.State.Disconnected && rpcInBuffer[i].Length > 0)
                    {
                        ushort rpcIndex = 0;
                        if (dynamicAssemblyList == 1)
                        {
                            var rpcHashPeek = *(ulong*) rpcInBuffer[i].GetUnsafeReadOnlyPtr();
                            rpcIndex = rpcHashPeek == 0 ? ushort.MaxValue : (ushort)0;
                        }
                        else
                        {
                            rpcIndex = *(ushort*) rpcInBuffer[i].GetUnsafeReadOnlyPtr();
                        }

                        if (rpcIndex == ushort.MaxValue)
                            netDebug.DebugLog($"[{worldName}] {connections[i].Value.ToFixedString()} in disconnected state but allowing RPC protocol version message to get processed");
                        else
                            continue;
                    }
                    else if (conState != NetworkConnection.State.Connected)
                    {
                        continue;
                    }

                    var dynArray = rpcInBuffer[i];
                    var parameters = new RpcExecutor.Parameters
                    {
                        Reader = dynArray.AsDataStreamReader(),
                        CommandBuffer = commandBuffer,
                        State = (IntPtr)UnsafeUtility.AddressOf(ref deserializeState),
                        Connection = entities[i],
                        JobIndex = unfilteredChunkIndex
                    };
                    int msgHeaderLen = RpcCollection.GetInnerRpcMessageHeaderLength(dynamicAssemblyList == 1);
                    while (parameters.Reader.GetBytesRead() < parameters.Reader.Length)
                    {
                        int rpcIndex = 0;
                        if (dynamicAssemblyList == 1)
                        {
                            ulong rpcHash = parameters.Reader.ReadULong();
                            if (rpcHash == 0)
                            {
                                rpcIndex = ushort.MaxValue;
                                protocolVersion.RpcCollectionVersion = 0;
                                protocolVersion.ComponentCollectionVersion = 0;
                            }
                            else if (rpcHash != 0 && !hashToIndex.TryGetValue(rpcHash, out rpcIndex))
                            {
                                netDebug.LogError(
                                    $"[{worldName}] RpcSystem received rpc with invalid hash ({rpcHash}) from {connections[i].Value.ToFixedString()}");
                                commandBuffer.AddComponent(unfilteredChunkIndex, entities[i],
                                    new NetworkStreamRequestDisconnect { Reason = NetworkStreamDisconnectReason.InvalidRpc });
                                break;
                            }
                        }
                        else
                        {
                            rpcIndex = parameters.Reader.ReadUShort();
                        }

                        var rpcSize = parameters.Reader.ReadUShort();
                        if (rpcIndex == ushort.MaxValue)
                        {
                            // Special value for NetworkProtocolVersion
                            var netCodeVersion = parameters.Reader.ReadInt();
                            var gameVersion = parameters.Reader.ReadInt();
                            var rpcVersion = parameters.Reader.ReadULong();
                            var componentVersion = parameters.Reader.ReadULong();
                            if (netCodeVersion != protocolVersion.NetCodeVersion ||
                                gameVersion != protocolVersion.GameVersion ||
                                rpcVersion != protocolVersion.RpcCollectionVersion ||
                                componentVersion != protocolVersion.ComponentCollectionVersion)
                            {
                                var ent = commandBuffer.CreateEntity(unfilteredChunkIndex);
                                var connectionEntity = entities[i];
                                if (conState != NetworkConnection.State.Connected)
                                    connectionEntity = Entity.Null;
                                commandBuffer.AddComponent(unfilteredChunkIndex, ent, new ProtocolVersionError
                                {
                                    connection = connectionEntity,
                                    remoteProtocol = new NetworkProtocolVersion()
                                    {
                                        NetCodeVersion = netCodeVersion,
                                        GameVersion = gameVersion,
                                        RpcCollectionVersion = rpcVersion,
                                        ComponentCollectionVersion = componentVersion
                                    }
                                });
                                break;
                            }
                            //The connection has received the version. RpcSystem can't accept any rpc's if the NetworkProtocolVersion
                            //has not been received first.
                            var connection = connections[i];
                            connection.ProtocolVersionReceived = 1;
                            connections[i] = connection;
                        }
                        else if (rpcIndex >= execute.Length)
                        {
                            //If this is the server, we must disconnect the connection
                            netDebug.LogError(
                                $"[{worldName}] RpcSystem received invalid rpc (index {rpcIndex} out of range) from {connections[i].Value.ToFixedString()}");
                            commandBuffer.AddComponent(unfilteredChunkIndex, entities[i],
                                new NetworkStreamRequestDisconnect { Reason = NetworkStreamDisconnectReason.InvalidRpc });
                            break;
                        }
                        else if (connections[i].ProtocolVersionReceived == 0)
                        {
                            netDebug.LogError(
                                $"[{worldName}] RpcSystem received illegal rpc as it has not yet received the protocol version ({connections[i].Value.ToFixedString()})");
                            commandBuffer.AddComponent(unfilteredChunkIndex, entities[i],
                                new NetworkStreamRequestDisconnect { Reason = NetworkStreamDisconnectReason.InvalidRpc });
                            break;
                        }
                        else
                        {
                            execute[rpcIndex].Execute.Ptr.Invoke(ref parameters);
                        }
                    }

                    dynArray.Clear();

                    var sendBuffer = rpcOutBuffer[i];
                    var ack = acks[i];
                    while (sendBuffer.Length > 0)
                    {
                        int result;
                        if ((result = driver.BeginSend(concurrentDriver.reliablePipeline, connections[i].Value, out var tmp)) < 0)
                        {
                            netDebug.DebugLog($"[{worldName}] RPCSystem failed to send message. Will retry later, but this could mean too many messages are being sent. Error: {result}!");
                            break;
                        }
                        tmp.WriteByte((byte) NetworkStreamProtocol.Rpc);
                        tmp.WriteUInt(localTime);
                        uint returnTime = ack.LastReceivedRemoteTime;
                        if (returnTime != 0)
                            returnTime += (localTime - ack.LastReceiveTimestamp);
                        tmp.WriteUInt(returnTime);
                        var headerLength = tmp.Length;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        UnityEngine.Debug.Assert(headerLength == RpcCollection.k_RpcCommonHeaderLengthBytes);
#endif

                        // If sending failed we stop and wait until next frame
                        if (sendBuffer.Length + headerLength > tmp.Capacity)
                        {
                            var sendArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(sendBuffer.GetUnsafePtr(), sendBuffer.Length, Allocator.Invalid);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                            var safety = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(sendBuffer.AsNativeArray());
                            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref sendArray, safety);
#endif
                            var reader = new DataStreamReader(sendArray);
                            if (dynamicAssemblyList == 1)
                                reader.ReadULong();
                            else
                                reader.ReadUShort();
                            var len = reader.ReadUShort() + msgHeaderLen;
                            if (len + headerLength > tmp.Capacity)
                            {
                                sendBuffer.Clear();
                                // Could not fit a single message in the packet, this is a serious error
                                throw new InvalidOperationException($"[{worldName}] An RPC was too big to be sent, reduce the size of your RPCs");
                            }
                            tmp.WriteBytesUnsafe((byte*) sendBuffer.GetUnsafePtr(), len);
                            // Try to fit a few more messages in this packet
                            while (true)
                            {
                                var curTmpDataLength = tmp.Length - headerLength;
                                var subArray = sendArray.GetSubArray(curTmpDataLength, sendArray.Length - curTmpDataLength);
                                reader = new DataStreamReader(subArray);
                                if (dynamicAssemblyList == 1)
                                    reader.ReadULong();
                                else
                                    reader.ReadUShort();
                                len = reader.ReadUShort() + msgHeaderLen;
                                if (tmp.Length + len > tmp.Capacity)
                                    break;
                                tmp.WriteBytesUnsafe((byte*) subArray.GetUnsafeReadOnlyPtr(), len);
                            }
                        }
                        else
                            tmp.WriteBytesUnsafe((byte*) sendBuffer.GetUnsafePtr(), sendBuffer.Length);

                        // If sending failed we stop and wait until next frame
                        if ((result = driver.EndSend(tmp)) <= 0)
                        {
                            netDebug.LogWarning($"[{worldName}] An error occured during RpcSystem EndSend. ErrorCode: {result}!");
                            break;
                        }
                        var tmpDataLength = tmp.Length - headerLength;
                        if (tmpDataLength < sendBuffer.Length)
                        {
                            // Compact the buffer, removing the rpcs we did send
                            for (int cpy = tmpDataLength; cpy < sendBuffer.Length; ++cpy)
                                sendBuffer[cpy - tmpDataLength] = sendBuffer[cpy];
                            sendBuffer.ResizeUninitialized(sendBuffer.Length - tmpDataLength);
                        }
                        else
                            sendBuffer.Clear();
                    }
                }
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Deserialize the command type from the reader stream
            // Execute the RPC
            ref readonly var networkStreamDriver = ref SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRO;
            m_EntityTypeHandle.Update(ref state);
            m_NetworkStreamConnectionHandle.Update(ref state);
            m_IncomingRpcDataStreamBufferComponentHandle.Update(ref state);
            m_OutgoingRpcDataStreamBufferComponentHandle.Update(ref state);
            m_NetworkSnapshotAckComponentHandle.Update(ref state);
            var execJob = new RpcExecJob
            {
                commandBuffer = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
                entityType = m_EntityTypeHandle,
                connectionType = m_NetworkStreamConnectionHandle,
                inBufferType = m_IncomingRpcDataStreamBufferComponentHandle,
                outBufferType = m_OutgoingRpcDataStreamBufferComponentHandle,
                execute = m_RpcData,
                hashToIndex = m_RpcTypeHashToIndex,
                ghostMap = SystemAPI.GetSingleton<SpawnedGhostEntityMap>().Value,
                ackType = m_NetworkSnapshotAckComponentHandle,
                localTime = NetworkTimeSystem.TimestampMS,
                concurrentDriverStore = networkStreamDriver.ConcurrentDriverStore,
                protocolVersion = SystemAPI.GetSingleton<NetworkProtocolVersion>(),
                dynamicAssemblyList = m_DynamicAssemblyList.Value,
                netDebug = SystemAPI.GetSingleton<NetDebug>(),
                worldName = state.WorldUnmanaged.Name
            };
            state.Dependency = execJob.ScheduleParallel(m_RpcBufferGroup, state.Dependency);
            state.Dependency = networkStreamDriver.DriverStore.ScheduleFlushSendAllDrivers(state.Dependency);
        }
    }

    /// <summary>
    /// A system responsible for handling all the <see cref="RpcSystem.ProtocolVersionError"/> created by the
    /// <see cref="RpcSystem"/> while receiving rpcs.
    /// <para>
    /// The connection that generated the <see cref="RpcSystem.ProtocolVersionError"/> will be disconnected, by adding
    /// a <see cref="NetworkStreamRequestDisconnect"/> component, and a verbose error message containing the following
    /// is reported to the application:
    /// <para> - The local protocol.</para>
    /// <para> - The remote protocol.</para>
    /// <para> - The list of all registered rpc.</para>
    /// <para> - The list of all registered serializer.</para>
    /// </para>
    /// </summary>
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [BurstCompile]
    public partial struct RpcSystemErrors : ISystem
    {
        private EntityQuery m_ProtocolErrorQuery;
        private ComponentLookup<NetworkStreamConnection> m_NetworkStreamConnectionFromEntity;

        public void OnCreate(ref SystemState state)
        {
            m_ProtocolErrorQuery = state.GetEntityQuery(ComponentType.ReadOnly<RpcSystem.ProtocolVersionError>());
            state.RequireForUpdate(m_ProtocolErrorQuery);
            state.RequireForUpdate<GhostCollection>();

            m_NetworkStreamConnectionFromEntity = state.GetComponentLookup<NetworkStreamConnection>(true);
        }

        [BurstCompile]
        partial struct ReportRpcErrors : IJobEntity
        {
            public EntityCommandBuffer commandBuffer;
            [ReadOnly] public ComponentLookup<NetworkStreamConnection> connections;
            public NativeArray<FixedString128Bytes> rpcs;
            public NativeArray<FixedString128Bytes> componentInfo;
            public NetDebug netDebug;
            public NetworkProtocolVersion localProtocol;
            public FixedString128Bytes worldName;
            public void Execute(Entity entity, in RpcSystem.ProtocolVersionError rpcError)
            {
                FixedString128Bytes connection = "unknown connection";
                if (rpcError.connection != Entity.Null)
                {
                    commandBuffer.AddComponent(rpcError.connection,
                        new NetworkStreamRequestDisconnect
                            { Reason = NetworkStreamDisconnectReason.InvalidRpc });
                    connection = connections[rpcError.connection].Value.ToFixedString();
                }

                var errorHeader = (FixedString512Bytes)$"[{worldName}] RpcSystem received bad protocol version from {connection}";
                errorHeader.Append((FixedString32Bytes)"\nLocal protocol: ");
                NetDebug.AppendProtocolVersionError(ref errorHeader, localProtocol);
                errorHeader.Append((FixedString32Bytes)"\nRemote protocol: ");
                NetDebug.AppendProtocolVersionError(ref errorHeader, rpcError.remoteProtocol);
                netDebug.LogError(errorHeader);

                var s = (FixedString512Bytes)"RPC List (for above 'bad protocol version' error): ";
                s.Append(rpcs.Length);
                netDebug.LogError(s);

                for (int i = 0; i < rpcs.Length; ++i)
                    netDebug.LogError($"RpcHash[{i}] = {rpcs[i]}");

                s = (FixedString512Bytes)"Component serializer data (for above 'bad protocol version' error): ";
                s.Append(componentInfo.Length);
                netDebug.LogError(s);

                for (int i = 0; i < componentInfo.Length; ++i)
                    netDebug.LogError($"ComponentHash[{i}] = {componentInfo[i]}");

                commandBuffer.DestroyEntity(entity);
            }
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_NetworkStreamConnectionFromEntity.Update(ref state);

            var collectionRpcs = SystemAPI.GetSingleton<RpcCollection>().Rpcs;
            var rpcs = CollectionHelper.CreateNativeArray<FixedString128Bytes>(collectionRpcs.Length, state.WorldUpdateAllocator);
            for (int i = 0; i < collectionRpcs.Length; ++i)
            {
                var typeIndex = TypeManager.GetTypeIndexFromStableTypeHash(collectionRpcs[i].TypeHash);
                rpcs[i] = new FixedString128Bytes(TypeManager.GetTypeInfo(typeIndex).DebugTypeName);
            }
            FixedString128Bytes serializerHashString = default;
            var ghostSerializerCollection = SystemAPI.GetSingletonBuffer<GhostComponentSerializer.State>();
            var componentInfo = CollectionHelper.CreateNativeArray<FixedString128Bytes>(ghostSerializerCollection.Length, state.WorldUpdateAllocator);
            for (int serializerIndex = 0; serializerIndex < ghostSerializerCollection.Length; ++serializerIndex)
            {
                GhostCollectionSystem.GetSerializerHashString(ghostSerializerCollection[serializerIndex],
                    ref serializerHashString);
                componentInfo[serializerIndex] = serializerHashString;
                serializerHashString.Clear();
            }

            var reportJob = new ReportRpcErrors
            {
                commandBuffer = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged),
                connections = m_NetworkStreamConnectionFromEntity,
                rpcs = rpcs,
                componentInfo = componentInfo,
                netDebug = SystemAPI.GetSingleton<NetDebug>(),
                localProtocol = SystemAPI.GetSingleton<NetworkProtocolVersion>(),
                worldName = state.WorldUnmanaged.Name
            };

            state.Dependency = reportJob.Schedule(state.Dependency);
        }
    }
}
