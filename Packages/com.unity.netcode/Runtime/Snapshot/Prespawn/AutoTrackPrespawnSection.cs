#if UNITY_EDITOR && !NETCODE_NDEBUG
#define NETCODE_DEBUG
#endif
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;
using Unity.Burst;

namespace Unity.NetCode
{
    /// <summary>
    /// RPCs to control the prespawn streaming. Sent by the client to the server when a scene is loaded/unloaded.
    /// Server react by sending new snapshot udpdates for the pre-spawned ghosts that belong to the scenes for
    /// which streaming is enabled.
    /// </summary>
    internal struct StartStreamingSceneGhosts : IRpcCommand
    {
        /// <summary>
        /// Deterministic unique Hash for each sub-scene that contains pre-spawned ghost. See <see cref="SubSceneWithPrespawnGhosts"/>
        /// </summary>
        public ulong SceneHash;
    }

    /// <summary>
    /// RPCs to control the prespawn streaming. Sent by the client to the server when a scene is unloaded.
    /// Server react by not sending any new snapshot udpdate for the pre-spawned ghosts that belong to the scenes for
    /// which streaming is disabled.
    /// </summary>
    internal struct StopStreamingSceneGhosts : IRpcCommand
    {
        /// <summary>
        /// Deterministic unique Hash for each sub-scene that contains pre-spawned ghost. See <see cref="SubSceneWithPrespawnGhosts"/>
        /// </summary>
        public ulong SceneHash;
    }

    /// <summary>
    /// Track prespawn section load/unload events and send rpc to server to ack the loaded scene for that client
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(PrespawnGhostSystemGroup))]
    [UpdateBefore(typeof(ClientTrackLoadedPrespawnSections))]
    [BurstCompile]
    partial struct ClientPrespawnAckSystem : ISystem
    {
        ComponentLookup<IsSectionLoaded> m_SectionLoadedFromEntity;
        private EntityQuery m_InitializedSections;
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<NetworkId>()
                .WithNone<NetworkStreamRequestDisconnect>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
            m_InitializedSections = state.GetEntityQuery(ComponentType.ReadOnly<SubSceneWithGhostCleanup>());
            state.RequireForUpdate(m_InitializedSections);

            m_SectionLoadedFromEntity = state.GetComponentLookup<IsSectionLoaded>(true);
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<DisableAutomaticPrespawnSectionReporting>())
            {
                state.Enabled = false;
                return;
            }

            m_SectionLoadedFromEntity.Update(ref state);
            var ackJob = new ClientPrespawnAck
            {
                sectionLoadedFromEntity = m_SectionLoadedFromEntity,
                netDebug = SystemAPI.GetSingleton<NetDebug>(),
                entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged)
            };
            state.Dependency = ackJob.Schedule(state.Dependency);
        }
        [BurstCompile]
        partial struct ClientPrespawnAck : IJobEntity
        {
            [ReadOnly] public ComponentLookup<IsSectionLoaded> sectionLoadedFromEntity;
            public NetDebug netDebug;
            public EntityCommandBuffer entityCommandBuffer;
            public void Execute(Entity entity, ref SubSceneWithGhostCleanup stateComponent)
            {
                bool isLoaded = sectionLoadedFromEntity.HasComponent(entity);
                if (!isLoaded && stateComponent.Streaming != 0)
                {
                    var reqUnload = entityCommandBuffer.CreateEntity();
                    entityCommandBuffer.AddComponent(reqUnload, new StopStreamingSceneGhosts
                    {
                        SceneHash = stateComponent.SubSceneHash,
                    });
                    entityCommandBuffer.AddComponent(reqUnload, new SendRpcCommandRequest());
                    stateComponent.Streaming = 0;
                    LogStopStreaming(netDebug, stateComponent);
                }
                else if (isLoaded && stateComponent.Streaming == 0)
                {
                    var reqUnload = entityCommandBuffer.CreateEntity();
                    entityCommandBuffer.AddComponent(reqUnload, new StartStreamingSceneGhosts
                    {
                        SceneHash = stateComponent.SubSceneHash
                    });
                    entityCommandBuffer.AddComponent(reqUnload, new SendRpcCommandRequest());
                    stateComponent.Streaming = 1;
                    LogStartStreaming(netDebug, stateComponent);
                }
            }
        }

        [Conditional("NETCODE_DEBUG")]
        private static void LogStopStreaming(in NetDebug netDebug, in SubSceneWithGhostCleanup stateComponent)
        {
            netDebug.DebugLog(FixedString.Format("Request stop streaming scene {0}",
                NetDebug.PrintHex(stateComponent.SubSceneHash)));
        }
        [Conditional("NETCODE_DEBUG")]
        private static void LogStartStreaming(in NetDebug netDebug, in SubSceneWithGhostCleanup stateComponent)
        {
            netDebug.DebugLog(FixedString.Format("Request start streaming scene {0}",
                NetDebug.PrintHex(stateComponent.SubSceneHash)));
        }
    }

    /// <summary>
    /// Handle the StartStreaming/StopStreaming rpcs from the client and update the list of streamin/acked scenes.
    /// It is possible to add user-defined behaviors by consuming or reading the rpc before that system runs.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PrespawnGhostSystemGroup))]
    [UpdateBefore(typeof(ServerTrackLoadedPrespawnSections))]
    [BurstCompile]
    partial struct ServerPrespawnAckSystem : ISystem
    {
        BufferLookup<PrespawnSectionAck> m_PrespawnSectionAckFromEntity;
        ComponentLookup<NetworkId> m_NetworkIdLookup;
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_PrespawnSectionAckFromEntity = state.GetBufferLookup<PrespawnSectionAck>();
            m_NetworkIdLookup = state.GetComponentLookup<NetworkId>(true);
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ReceiveRpcCommandRequest>()
                .WithAny<StartStreamingSceneGhosts, StopStreamingSceneGhosts>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<DisableAutomaticPrespawnSectionReporting>())
            {
                state.Enabled = false;
                return;
            }
            m_PrespawnSectionAckFromEntity.Update(ref state);
            m_NetworkIdLookup.Update(ref state);
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var netDebug = SystemAPI.GetSingleton<NetDebug>();
            var startJob = new StartStreamingScene
            {
                prespawnSectionAckFromEntity = m_PrespawnSectionAckFromEntity,
                networkIdLookup = m_NetworkIdLookup,
                ecb = ecb,
                netDebug = netDebug
            };
            state.Dependency = startJob.Schedule(state.Dependency);
            var stopJob = new StopStreamingScene
            {
                prespawnSectionAckFromEntity = m_PrespawnSectionAckFromEntity,
                networkIdLookup = m_NetworkIdLookup,
                ecb = ecb,
                netDebug = netDebug
            };
            state.Dependency = stopJob.Schedule(state.Dependency);
        }
        [BurstCompile]
        partial struct StartStreamingScene : IJobEntity
        {
            public BufferLookup<PrespawnSectionAck> prespawnSectionAckFromEntity;
            [ReadOnly] public ComponentLookup<NetworkId> networkIdLookup;
            public EntityCommandBuffer ecb;
            public NetDebug netDebug;
            public void Execute(Entity entity, in StartStreamingSceneGhosts streamingReq, in ReceiveRpcCommandRequest requestComponent)
            {
                var prespawnSceneAcks = prespawnSectionAckFromEntity[requestComponent.SourceConnection];
                int ackIdx = prespawnSceneAcks.IndexOf(streamingReq.SceneHash);
                if (ackIdx == -1)
                {
                    LogStartStreaming(netDebug, networkIdLookup[requestComponent.SourceConnection].Value, streamingReq.SceneHash);
                    prespawnSceneAcks.Add(new PrespawnSectionAck { SceneHash = streamingReq.SceneHash });
                }
                ecb.DestroyEntity(entity);
            }
        }
        [BurstCompile]
        partial struct StopStreamingScene : IJobEntity
        {
            public BufferLookup<PrespawnSectionAck> prespawnSectionAckFromEntity;
            [ReadOnly] public ComponentLookup<NetworkId> networkIdLookup;
            public EntityCommandBuffer ecb;
            public NetDebug netDebug;
            public void Execute(Entity entity, in StopStreamingSceneGhosts streamingReq, in ReceiveRpcCommandRequest requestComponent)
            {
                var prespawnSceneAcks = prespawnSectionAckFromEntity[requestComponent.SourceConnection];
                int ackIdx = prespawnSceneAcks.IndexOf(streamingReq.SceneHash);
                if (ackIdx != -1)
                {
                    LogStopStreaming(netDebug, networkIdLookup[requestComponent.SourceConnection].Value, streamingReq.SceneHash);
                    prespawnSceneAcks.RemoveAtSwapBack(ackIdx);
                }
                ecb.DestroyEntity(entity);
            }
        }

        [Conditional("NETCODE_DEBUG")]
        private static void LogStopStreaming(in NetDebug netDebug, int connection, ulong sceneHash)
        {
            netDebug.DebugLog(FixedString.Format("Connection {0} stop streaming scene {1}", connection, NetDebug.PrintHex(sceneHash)));
        }
        [Conditional("NETCODE_DEBUG")]
        private static void LogStartStreaming(in NetDebug netDebug, int connection, ulong sceneHash)
        {
            netDebug.DebugLog(FixedString.Format("Connection {0} start streaming scene {1}", connection, NetDebug.PrintHex(sceneHash)));
        }
    }
}
