using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

namespace Unity.NetCode
{
    /// <summary>
    /// Group that contains all systems that receives commands. Only present in server world.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation, WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(NetworkReceiveSystemGroup))]
    [UpdateAfter(typeof(NetworkStreamReceiveSystem))]
    public partial class CommandReceiveSystemGroup : ComponentSystemGroup
    {
    }

    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(CommandReceiveSystemGroup), OrderLast = true)]
    [BurstCompile]
    internal partial struct CommandReceiveClearSystem : ISystem
    {
        EntityQuery m_NetworkTimeSingleton;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_NetworkTimeSingleton = state.GetEntityQuery(ComponentType.ReadOnly<NetworkTime>());
        }
        [BurstCompile]
        partial struct CommandReceiveClearJob : IJobEntity
        {
            public NetworkTick _currentTick;

            public void Execute(DynamicBuffer<IncomingCommandDataStreamBuffer> buffer, ref NetworkSnapshotAck snapshotAck)
            {
                buffer.Clear();
                if (snapshotAck.LastReceivedSnapshotByLocal.IsValid)
                {
                    int age = _currentTick.TicksSince(snapshotAck.LastReceivedSnapshotByLocal);
                    age *= 256;
                    snapshotAck.ServerCommandAge = (snapshotAck.ServerCommandAge * 7 + age) / 8;
                }
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var networkTime = m_NetworkTimeSingleton.GetSingleton<NetworkTime>();
            var currentTick = networkTime.ServerTick;

            var commandReceiveClearJob = new CommandReceiveClearJob() { _currentTick = currentTick };
            commandReceiveClearJob.ScheduleParallel();
        }
    }

    /// <summary>
    /// Helper struct for implementing systems to receive commands.
    /// This is generally used by code-gen and should only be used directly in special cases.
    /// </summary>
    /// <typeparam name="TCommandDataSerializer">Unmanaged CommandDataSerializer of type ICommandDataSerializer.</typeparam>
    /// <typeparam name="TCommandData">Unmanaged CommandData of type ICommandData.</typeparam>
    public struct CommandReceiveSystem<TCommandDataSerializer, TCommandData>
        where TCommandData : unmanaged, ICommandData
        where TCommandDataSerializer : unmanaged, ICommandDataSerializer<TCommandData>
    {
        /// <summary>
        /// Helper struct used by code-gen for implementing the Execute method of the the generated receiving job.
        /// The ReceiveJobData implement the command deserialization logic, by reading from the data stream the
        /// serialized commands and enqueuing them into the taget entity command buffer.
        /// As part of the command deserialization, if a <see cref="CommandDataInterpolationDelay"/> component is present
        /// on target entity, it will be updated with the latest reported interpolation delay.
        /// </summary>
        public struct ReceiveJobData
        {
            /// <summary>
            /// The output command buffer where the deserialized command are added.
            /// </summary>
            public BufferLookup<TCommandData> commandData;
            /// <summary>
            /// Accessor for retrieving the optional <see cref="CommandDataInterpolationDelay"/> component from the target entity.
            /// </summary>
            public ComponentLookup<CommandDataInterpolationDelay> delayFromEntity;
            /// <summary>
            /// Accessor for retrieving the optional <see cref="GhostOwner"/> component,
            /// and used for lookup the entity target when using <see cref="AutoCommandTarget"/>.
            /// </summary>
            [ReadOnly] public ComponentLookup<GhostOwner> ghostOwnerFromEntity;
            /// <summary>
            /// Accessor for retrieving the optional <see cref="AutoCommandTarget"/> component.
            /// </summary>
            [ReadOnly] public ComponentLookup<AutoCommandTarget> autoCommandTargetFromEntity;
            /// <summary>
            /// The compression model used for decoding the delta compressed commands.
            /// </summary>
            public StreamCompressionModel compressionModel;
            /// <summary>
            /// Read-only type handle for reading the data from the <see cref="IncomingCommandDataStreamBuffer"/> buffer.
            /// </summary>
            [ReadOnly] public BufferTypeHandle<IncomingCommandDataStreamBuffer> cmdBufferType;
            /// <summary>
            /// Read-only type handle to get the <see cref="NetworkSnapshotAck"/> for the connection.
            /// </summary>
            [ReadOnly] public ComponentTypeHandle<NetworkSnapshotAck> snapshotAckType;
            /// <summary>
            /// Read-only type handle to get the <see cref="NetworkId"/> for the connection.
            /// </summary>
            [ReadOnly] public ComponentTypeHandle<NetworkId> networkIdType;
            /// <summary>
            /// Read-only type handle to get the <see cref="CommandTarget"/> for the connection.
            /// </summary>
            [ReadOnly] public ComponentTypeHandle<CommandTarget> commmandTargetType;
            /// <summary>
            /// A readonly mapping to retrieve a ghost entity instance from a <see cref="SpawnedGhost"/> identity.
            /// See <see cref="SpawnedGhostEntityMap"/> for more information.
            /// </summary>
            [ReadOnly] public NativeParallelHashMap<SpawnedGhost, Entity>.ReadOnly ghostMap;
            /// <summary>
            /// The current server tick
            /// </summary>
            public NetworkTick serverTick;
            /// <summary>
            /// The <see cref="NetDebug"/> singleton component instance.
            /// </summary>
            public NetDebug netDebug;
            /// <summary>
            /// The stable hash for the <see cref="ICommandData"/> type. Used to verify the commands are
            /// consistent.
            /// </summary>
            public ulong stableHash;

            /// <summary>
            /// Deserialize all commands present in the packet, and put all the inputs into the entity <see cref="ICommandData"/> buffer.
            /// </summary>
            /// <param name="reader"></param>
            /// <param name="targetEntity"></param>
            /// <param name="tick"></param>
            /// <param name="snapshotAck"></param>
            internal unsafe void Deserialize(ref DataStreamReader reader, Entity targetEntity,
                uint tick, in NetworkSnapshotAck snapshotAck)
            {
                if (delayFromEntity.HasComponent(targetEntity))
                    delayFromEntity[targetEntity] = new CommandDataInterpolationDelay{ Delay = snapshotAck.RemoteInterpolationDelay};

                var deserializeState = new RpcDeserializerState {ghostMap = ghostMap};
                var buffers = new NativeArray<TCommandData>((int)CommandSendSystem<TCommandDataSerializer, TCommandData>.k_InputBufferSendSize, Allocator.Temp);
                var command = commandData[targetEntity];
                var baselineReceivedCommand = default(TCommandData);
                var serializer = default(TCommandDataSerializer);
                baselineReceivedCommand.Tick = new NetworkTick{SerializedData = reader.ReadUInt()};
                serializer.Deserialize(ref reader, deserializeState, ref baselineReceivedCommand);
                // Store received commands in the network command buffer
                buffers[0] = baselineReceivedCommand;
                var inputBufferSendSize = CommandSendSystem<TCommandDataSerializer, TCommandData>.k_InputBufferSendSize;
                for (uint i = 1; i < inputBufferSendSize; ++i)
                {
                    var receivedCommand = default(TCommandData);
                    receivedCommand.Tick = new NetworkTick{SerializedData = reader.ReadPackedUIntDelta(baselineReceivedCommand.Tick.SerializedData, compressionModel)};
                    serializer.Deserialize(ref reader, deserializeState, ref receivedCommand, baselineReceivedCommand,
                        compressionModel);
                    // Store received commands in the network command buffer
                    buffers[(int)i] = receivedCommand;
                }
                // Add the command in the order they were produces instead of the order they were sent
                for (int i = (int)inputBufferSendSize - 1; i >= 0; --i)
                {
                    if (!buffers[i].Tick.IsValid)
                        continue;
                    var input = buffers[i];
                    // This is a special case, since this could be the latest tick we have for the current server tick
                    // it must be stored somehow. Trying to get the data for previous tick also needs to return
                    // what we actually used previous tick. So we fake the tick of the most recent input we got
                    // to point at the current server tick, even though it was actually for a tick we already
                    // simulated
                    // If it turns out there is another tick which is newer and should be used for serverTick
                    // that must be included in this packet and will overwrite the state for serverTick
                    if (serverTick.IsNewerThan(buffers[i].Tick))
                        input.Tick = serverTick;
                    command.AddCommandData(input);
                }
            }

            /// <summary>
            /// Decode the commands present in the <see cref="IncomingCommandDataStreamBuffer"/> for all
            /// the connections present in the chunk and lookup for the target entity where the command should be
            /// enqueued by either using the <see cref="CommandTarget"/> target entity or via
            /// <see cref="AutoCommandTarget"/> if enabled.
            /// </summary>
            /// <param name="chunk"></param>
            /// <param name="orderIndex"></param>
            public void Execute(ArchetypeChunk chunk, int orderIndex)
            {
                var snapshotAcks = chunk.GetNativeArray(ref snapshotAckType);
                var networkIds = chunk.GetNativeArray(ref networkIdType);
                var commandTargets = chunk.GetNativeArray(ref commmandTargetType);
                var cmdBuffers = chunk.GetBufferAccessor(ref cmdBufferType);

                for (int i = 0, chunkEntityCount = chunk.Count; i < chunkEntityCount; ++i)
                {
                    var owner = networkIds[i].Value;
                    var snapshotAck = snapshotAcks[i];
                    var buffer = cmdBuffers[i];
                    if (buffer.Length < 4)
                        continue;
                    DataStreamReader reader = buffer.AsDataStreamReader();
                    var tick = reader.ReadUInt();
                    while (reader.GetBytesRead() + 10 <= reader.Length)
                    {
                        var hash = reader.ReadULong();
                        var len = reader.ReadUShort();
                        var startPos = reader.GetBytesRead();
                        if (hash == stableHash)
                        {
                            // Read ghost id
                            var ghostId = reader.ReadInt();
                            var spawnTick = new NetworkTick{SerializedData = reader.ReadUInt()};
                            var targetEntity = commandTargets[i].targetEntity;
                            if (ghostId != 0)
                            {
                                targetEntity = Entity.Null;
                                if (ghostMap.TryGetValue(new SpawnedGhost{ghostId = ghostId, spawnTick = spawnTick}, out var ghostEnt))
                                {
                                    if (ghostOwnerFromEntity.HasComponent(ghostEnt) && autoCommandTargetFromEntity.HasComponent(ghostEnt) &&
                                        ghostOwnerFromEntity[ghostEnt].NetworkId == owner && autoCommandTargetFromEntity[ghostEnt].Enabled)
                                        targetEntity = ghostEnt;
                                }
                            }
                            if (commandData.HasBuffer(targetEntity))
                            {
                                Deserialize(ref reader, targetEntity, tick, snapshotAck);
                            }
                        }
                        reader.SeekSet(startPos + len);
                    }
                }
            }
        }

        /// <summary>
        /// The query to use when scheduling the processing job.
        /// </summary>
        public EntityQuery Query => m_entityQuery;
        private EntityQuery m_entityQuery;
        private EntityQuery m_SpawnedGhostEntityMapQuery;
        private EntityQuery m_NetworkTimeQuery;
        private EntityQuery m_NetDebugQuery;
        private StreamCompressionModel m_CompressionModel;

        private BufferLookup<TCommandData> m_TCommandDataFromEntity;
        private ComponentLookup<CommandDataInterpolationDelay> m_CommandDataInterpolationDelayFromEntity;
        private ComponentLookup<GhostOwner> m_GhostOwnerLookup;
        private ComponentLookup<AutoCommandTarget> m_AutoCommandTargetFromEntity;
        private BufferTypeHandle<IncomingCommandDataStreamBuffer> m_IncomingCommandDataStreamBufferComponentHandle;
        private ComponentTypeHandle<NetworkSnapshotAck> m_NetworkSnapshotAckComponentHandle;
        private ComponentTypeHandle<NetworkId> m_NetworkIdComponentHandle;
        private ComponentTypeHandle<CommandTarget> m_CommandTargetComponentHandle;

        /// <summary>
        /// Initialize the helper struct, should be called from OnCreate in an ISystem.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            m_CompressionModel = StreamCompressionModel.Default;
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<NetworkStreamInGame, IncomingCommandDataStreamBuffer, NetworkSnapshotAck>()
                .WithAllRW<CommandTarget>();
            m_entityQuery = state.GetEntityQuery(builder);
            builder.Reset();
            builder.WithAll<SpawnedGhostEntityMap>();
            m_SpawnedGhostEntityMapQuery = state.GetEntityQuery(builder);
            builder.Reset();
            builder.WithAll<NetworkTime>();
            m_NetworkTimeQuery = state.GetEntityQuery(builder);
            builder.Reset();
            builder.WithAll<NetDebug>();
            m_NetDebugQuery = state.GetEntityQuery(builder);

            m_TCommandDataFromEntity = state.GetBufferLookup<TCommandData>();
            m_CommandDataInterpolationDelayFromEntity = state.GetComponentLookup<CommandDataInterpolationDelay>();
            m_GhostOwnerLookup = state.GetComponentLookup<GhostOwner>(true);
            m_AutoCommandTargetFromEntity = state.GetComponentLookup<AutoCommandTarget>(true);
            m_IncomingCommandDataStreamBufferComponentHandle = state.GetBufferTypeHandle<IncomingCommandDataStreamBuffer>(true);
            m_NetworkSnapshotAckComponentHandle = state.GetComponentTypeHandle<NetworkSnapshotAck>(true);
            m_NetworkIdComponentHandle = state.GetComponentTypeHandle<NetworkId>(true);
            m_CommandTargetComponentHandle = state.GetComponentTypeHandle<CommandTarget>(true);

            state.RequireForUpdate(m_entityQuery);
            state.RequireForUpdate<TCommandData>();
        }

        /// <summary>
        /// Initialize the internal state of a processing job, should be called from OnUpdate of an ISystem.
        /// </summary>
        /// <param name="state">Raw entity system state.</param>
        /// <returns>Constructed <see cref="ReceiveJobData"/> with initialized state.</returns>
        public ReceiveJobData InitJobData(ref SystemState state)
        {
            m_TCommandDataFromEntity.Update(ref state);
            m_CommandDataInterpolationDelayFromEntity.Update(ref state);
            m_GhostOwnerLookup.Update(ref state);
            m_AutoCommandTargetFromEntity.Update(ref state);
            m_IncomingCommandDataStreamBufferComponentHandle.Update(ref state);
            m_NetworkSnapshotAckComponentHandle.Update(ref state);
            m_NetworkIdComponentHandle.Update(ref state);
            m_CommandTargetComponentHandle.Update(ref state);
            var recvJob = new ReceiveJobData
            {
                commandData = m_TCommandDataFromEntity,
                delayFromEntity = m_CommandDataInterpolationDelayFromEntity,
                ghostOwnerFromEntity = m_GhostOwnerLookup,
                autoCommandTargetFromEntity = m_AutoCommandTargetFromEntity,
                compressionModel = m_CompressionModel,
                cmdBufferType = m_IncomingCommandDataStreamBufferComponentHandle,
                snapshotAckType = m_NetworkSnapshotAckComponentHandle,
                networkIdType = m_NetworkIdComponentHandle,
                commmandTargetType = m_CommandTargetComponentHandle,
                ghostMap = m_SpawnedGhostEntityMapQuery.GetSingleton<SpawnedGhostEntityMap>().Value,
                serverTick = m_NetworkTimeQuery.GetSingleton<NetworkTime>().ServerTick,
                netDebug = m_NetDebugQuery.GetSingleton<NetDebug>(),
                stableHash = TypeManager.GetTypeInfo<TCommandData>().StableTypeHash
            };
            return recvJob;
        }
    }
}
