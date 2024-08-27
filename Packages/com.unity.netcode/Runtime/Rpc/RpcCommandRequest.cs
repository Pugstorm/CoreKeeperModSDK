#if UNITY_EDITOR && !NETCODE_NDEBUG
#define NETCODE_DEBUG
#endif
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Unity.NetCode
{
    /// <summary>
    /// Temporary type, used to upgrade to new component type, to be removed before final 1.0
    /// </summary>
    [Obsolete("SendRpcCommandRequestComponent has been deprecated. Use SendRpcCommandRequest instead (UnityUpgradable) -> SendRpcCommandRequest", true)]
    public struct SendRpcCommandRequestComponent : IComponentData
    {}
    /// <summary>
    /// Temporary type, used to upgrade to new component type, to be removed before final 1.0
    /// </summary>
    [Obsolete("ReceiveRpcCommandRequestComponent has been deprecated. Use ReceiveRpcCommandRequest instead (UnityUpgradable) -> ReceiveRpcCommandRequest", true)]
    public struct ReceiveRpcCommandRequestComponent : IComponentData
    {}

    /// <summary>
    /// A component used to signal that an RPC is supposed to be sent to a remote connection and should *not* be processed.
    /// </summary>
    public struct SendRpcCommandRequest : IComponentData
    {
        /// <summary>
        /// The "NetworkConnection" entity that this RPC should be sent specifically to, or Entity.Null to broadcast to all connections.
        /// </summary>
        public Entity TargetConnection;
    }
    /// <summary>
    /// A component used to signal that an RPC has been received from a remote connection and should be processed.
    /// </summary>
    public struct ReceiveRpcCommandRequest : IComponentData
    {
        /// <summary>
        /// The connection which sent the RPC being processed.
        /// </summary>
        public Entity SourceConnection;

#if NETCODE_DEBUG
        /// <inheritdoc cref="Consume"/>
        public ushort Age;

#endif
        /// <inheritdoc cref="Consume"/>
        public bool IsConsumed
        {
            get
            {
#if NETCODE_DEBUG
                return Age == ushort.MaxValue;
#else
                return false;
#endif
            }
        }

        /// <summary>
        ///     <see cref="ReceiveRpcCommandRequest"/> has a <see cref="WarnAboutStaleRpcSystem"/> which will log a warning if this <see cref="Age"/> value exceeds <see cref="WarnAboutStaleRpcSystem.MaxRpcAgeFrames"/>.
        ///     Counts simulation frames.
        ///     0 is the simulation frame it is received on.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Consume()
        {
#if NETCODE_DEBUG
            Age = ushort.MaxValue;
#endif
        }
    }

    /// <summary>
    /// A group used to make sure all processing on command request entities happens in the correct place.
    /// This is used by code-gen and should only be used directly when implementing custom command request processors.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation,
        WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(RpcSystem))]
    [UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial class RpcCommandRequestSystemGroup : ComponentSystemGroup
    {
        EntityQuery m_Query;
        protected override void OnCreate()
        {
            base.OnCreate();
            m_Query = GetEntityQuery(ComponentType.ReadOnly<SendRpcCommandRequest>());
        }
        protected override void OnUpdate()
        {
            if (!m_Query.IsEmptyIgnoreFilter)
                base.OnUpdate();
        }
    }

    /// <summary>
    /// Helper struct for implementing systems to process RPC command request entities.
    /// This is generally used by code-gen, and should only be used directly in special cases.
    /// </summary>
    /// <typeparam name="TActionSerializer">Unmanaged type of <see cref="IRpcCommandSerializer{TActionRequest}"/></typeparam>
    /// <typeparam name="TActionRequest">Unmanaged type of <see cref="IComponentData"/></typeparam>
    public struct RpcCommandRequest<TActionSerializer, TActionRequest>
        where TActionRequest : unmanaged, IComponentData
        where TActionSerializer : unmanaged, IRpcCommandSerializer<TActionRequest>
    {
        /// <summary>
        /// A struct that can be embedded into your system job, and should be used to delegate the rpc handling.
        /// Example of use:
        /// <code>
        /// [BurstCompile]
        /// struct SendRpc : IJobChunk
        /// {
        ///     public RpcCommandRequest{MyRpcCommand, MyRpcCommand}.SendRpcData data;
        ///     public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        ///     {
        ///         data.Execute(chunk, unfilteredChunkIndex);
        ///     }
        /// }
        /// </code>
        /// Always use the <see cref="RpcCommandRequest{TActionSerializer,TActionRequest}.InitJobData"/> method to construct
        /// a valid instance.
        /// </summary>
        public struct SendRpcData
        {
            internal EntityCommandBuffer.ParallelWriter commandBuffer;
            [ReadOnly] internal EntityTypeHandle entitiesType;
            [ReadOnly] internal ComponentTypeHandle<SendRpcCommandRequest> rpcRequestType;
            [ReadOnly] internal ComponentTypeHandle<TActionRequest> actionRequestType;
            [ReadOnly] internal ComponentLookup<GhostInstance> ghostFromEntity;
            internal BufferLookup<OutgoingRpcDataStreamBuffer> rpcFromEntity;
            internal RpcQueue<TActionSerializer, TActionRequest> rpcQueue;
            [ReadOnly] internal NativeList<Entity> connections;

            void LambdaMethod(Entity entity, int orderIndex, in SendRpcCommandRequest dest, in TActionRequest action)
            {
                commandBuffer.DestroyEntity(orderIndex, entity);
                if (connections.Length > 0)
                {
                    if (dest.TargetConnection != Entity.Null)
                    {
                        if (!rpcFromEntity.HasBuffer(dest.TargetConnection))
                        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                            UnityEngine.Debug.LogWarning("Cannot send RPC with no remote connection.");
#endif
                            return;
                        }
                        var buffer = rpcFromEntity[dest.TargetConnection];
                        rpcQueue.Schedule(buffer, ghostFromEntity, action);
                    }
                    else
                    {
                        for (var i = 0; i < connections.Length; ++i)
                        {
                            var buffer = rpcFromEntity[connections[i]];
                            rpcQueue.Schedule(buffer, ghostFromEntity, action);
                        }
                    }
                }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                else
                {
                    UnityEngine.Debug.LogWarning("Cannot send RPC with no remote connection.");

                    return;
                }
#endif
            }

            /// <summary>
            /// Call this from an <see cref="IJobChunk.Execute"/> method to handle the rpc requests.
            /// </summary>
            /// <param name="chunk"></param>
            /// <param name="orderIndex"></param>
            public void Execute(ArchetypeChunk chunk, int orderIndex)
            {
                var entities = chunk.GetNativeArray(entitiesType);
                var rpcRequests = chunk.GetNativeArray(ref rpcRequestType);
                if (ComponentType.ReadOnly<TActionRequest>().IsZeroSized)
                {
                    TActionRequest action = default;
                    for (int i = 0, chunkEntityCount = chunk.Count; i < chunkEntityCount; ++i)
                    {
                        LambdaMethod(entities[i], orderIndex, rpcRequests[i], action);
                    }
                }
                else
                {
                    var actions = chunk.GetNativeArray(ref actionRequestType);
                    for (int i = 0, chunkEntityCount = chunk.Count; i < chunkEntityCount; ++i)
                    {
                        LambdaMethod(entities[i], orderIndex, rpcRequests[i], actions[i]);
                    }
                }
            }
        }

        private RpcQueue<TActionSerializer, TActionRequest> m_RpcQueue;
        private EntityQuery m_ConnectionsQuery;
        private EntityQuery m_CommandBufferQuery;
        /// <summary>
        /// The query to use when scheduling the processing job.
        /// </summary>
        public EntityQuery Query;

        EntityTypeHandle m_EntityTypeHandle;
        ComponentTypeHandle<SendRpcCommandRequest> m_SendRpcCommandRequestComponentHandle;
        ComponentTypeHandle<TActionRequest> m_TActionRequestHandle;
        ComponentLookup<GhostInstance> m_GhostComponentFromEntity;
        BufferLookup<OutgoingRpcDataStreamBuffer> m_OutgoingRpcDataStreamBufferComponentFromEntity;

        /// <summary>
        /// Initialize the helper struct, should be called from OnCreate in an ISystem.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp).WithAll<RpcCollection>();
            var collectionQuery = state.GetEntityQuery(builder);
            var rpcCollection = collectionQuery.GetSingleton<RpcCollection>();
            rpcCollection.RegisterRpc<TActionSerializer, TActionRequest>();
            m_RpcQueue = rpcCollection.GetRpcQueue<TActionSerializer, TActionRequest>();
            builder.Reset();
            builder.WithAll<SendRpcCommandRequest, TActionRequest>();
            Query = state.GetEntityQuery(builder);
            builder.Reset();
            builder.WithAll<NetworkId>();
            m_ConnectionsQuery = state.GetEntityQuery(builder);
            builder.Reset();
            builder.WithAll<BeginSimulationEntityCommandBufferSystem.Singleton>();
            builder.WithOptions(EntityQueryOptions.IncludeSystems);
            m_CommandBufferQuery = state.GetEntityQuery(builder);

            m_EntityTypeHandle = state.GetEntityTypeHandle();
            m_SendRpcCommandRequestComponentHandle = state.GetComponentTypeHandle<SendRpcCommandRequest>(true);
            m_TActionRequestHandle = state.GetComponentTypeHandle<TActionRequest>(true);
            m_GhostComponentFromEntity = state.GetComponentLookup<GhostInstance>(true);
            m_OutgoingRpcDataStreamBufferComponentFromEntity = state.GetBufferLookup<OutgoingRpcDataStreamBuffer>();

            state.RequireForUpdate(Query);
        }

        /// <summary>
        /// Initialize the internal state of a processing job. Should be called from OnUpdate of an ISystem.
        /// </summary>
        /// <param name="state">Raw entity system state.</param>
        /// <returns><see cref="SendRpcData"/> initialized using <paramref name="state"/></returns>
        public SendRpcData InitJobData(ref SystemState state)
        {
            m_EntityTypeHandle.Update(ref state);
            m_SendRpcCommandRequestComponentHandle.Update(ref state);
            m_TActionRequestHandle.Update(ref state);
            m_GhostComponentFromEntity.Update(ref state);
            m_OutgoingRpcDataStreamBufferComponentFromEntity.Update(ref state);
            var connections = m_ConnectionsQuery.ToEntityListAsync(state.WorldUpdateAllocator,
                out var connectionsHandle);
            var sendJob = new SendRpcData
            {
                commandBuffer = m_CommandBufferQuery.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
                entitiesType = m_EntityTypeHandle,
                rpcRequestType = m_SendRpcCommandRequestComponentHandle,
                actionRequestType = m_TActionRequestHandle,
                ghostFromEntity = m_GhostComponentFromEntity,
                rpcFromEntity = m_OutgoingRpcDataStreamBufferComponentFromEntity,
                rpcQueue = m_RpcQueue,
                connections = connections,
            };
            state.Dependency = JobHandle.CombineDependencies(state.Dependency, connectionsHandle);
            return sendJob;
        }
    }
}
