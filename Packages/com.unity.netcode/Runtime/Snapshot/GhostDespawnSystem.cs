using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Unity.NetCode
{
    /// <summary>
    /// Present only in client worlds. Responsible for destroying spawned ghosts when a despawn
    /// request/command is received from the server.
    /// <para>Clients are not responsible for destroying ghost entities (and thus should never). The server is
    /// responsible for notifying the client about which ghosts should be destroyed (as part of the snapshot protocol).
    /// </para>
    /// <para>
    /// When a despawn command is received, the ghost entity is queued into a despawn queue. Two distinct despawn
    /// queues exist: one for interpolated, and one for the predicted ghosts.
    /// </para>
    /// <para>
    /// The above distinction is necessary because interpolated ghosts timeline (<see cref="NetworkTime.InterpolationTick"/>)
    /// is in the past in respect to both the server and client timeline (the current simulated tick).
    /// When a snapshot with a despawn command (for an interpolated ghost) is received, the server tick at which the entity has been destroyed
    /// (on the server) may be still in the future (for this client), and therefore the client must wait until the <see cref="NetworkTime.InterpolationTick"/>
    /// is greater or equal the despawning tick to actually despawn the ghost.
    /// </para>
    /// <para>
    /// Predicted entities, on the other hand, can be despawned only when the current <see cref="NetworkTime.ServerTick"/> is
    /// greater than or equal to the despawn tick of the server. Therefore, if the client is running ahead (as it should be),
    /// predicted ghosts will be destroyed as soon as their despawn request is pulled out of the snapshot
    /// (i.e. later on that same frame).
    /// </para>
    /// </summary>
    [BurstCompile]
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation|WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct GhostDespawnSystem : ISystem
    {
        NativeQueue<DelayedDespawnGhost> m_InterpolatedDespawnQueue;
        NativeQueue<DelayedDespawnGhost> m_PredictedDespawnQueue;

        internal struct DelayedDespawnGhost
        {
            public SpawnedGhost ghost;
            public NetworkTick tick;
        }

        public void OnCreate(ref SystemState state)
        {
            var singleton = state.EntityManager.CreateEntity(ComponentType.ReadWrite<GhostDespawnQueues>());
            state.EntityManager.SetName(singleton, "GhostLifetimeComponent-Singleton");
            m_InterpolatedDespawnQueue = new NativeQueue<DelayedDespawnGhost>(Allocator.Persistent);
            m_PredictedDespawnQueue = new NativeQueue<DelayedDespawnGhost>(Allocator.Persistent);
            SystemAPI.SetSingleton(new GhostDespawnQueues
            {
                InterpolatedDespawnQueue = m_InterpolatedDespawnQueue,
                PredictedDespawnQueue = m_PredictedDespawnQueue,
            });
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            m_InterpolatedDespawnQueue.Dispose();
            m_PredictedDespawnQueue.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if(!SystemAPI.HasSingleton<NetworkStreamInGame>())
            {
                state.CompleteDependency();
                m_PredictedDespawnQueue.Clear();
                m_InterpolatedDespawnQueue.Clear();
                return;
            }
            if (state.WorldUnmanaged.IsThinClient())
                return;

            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            state.Dependency = new DespawnJob
            {
                spawnedGhostMap = SystemAPI.GetSingletonRW<SpawnedGhostEntityMap>().ValueRO.SpawnedGhostMapRW,
                interpolatedDespawnQueue = m_InterpolatedDespawnQueue,
                predictedDespawnQueue = m_PredictedDespawnQueue,
                interpolatedTick = networkTime.InterpolationTick,
                predictedTick = networkTime.ServerTick,
                commandBuffer = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        struct DespawnJob : IJob
        {
            public NativeQueue<DelayedDespawnGhost> interpolatedDespawnQueue;
            public NativeParallelHashMap<SpawnedGhost, Entity> spawnedGhostMap;
            public NativeQueue<DelayedDespawnGhost> predictedDespawnQueue;
            public NetworkTick interpolatedTick, predictedTick;
            public EntityCommandBuffer commandBuffer;

            [BurstCompile]
            public void Execute()
            {
                {
                    while (interpolatedDespawnQueue.Count > 0 &&
                           !interpolatedDespawnQueue.Peek().tick.IsNewerThan(interpolatedTick))
                    {
                        var spawnedGhost = interpolatedDespawnQueue.Dequeue();
                        if (spawnedGhostMap.TryGetValue(spawnedGhost.ghost, out var ent))
                        {
                            commandBuffer.DestroyEntity(ent);
                            spawnedGhostMap.Remove(spawnedGhost.ghost);
                        }
                    }

                    while (predictedDespawnQueue.Count > 0 &&
                           !predictedDespawnQueue.Peek().tick.IsNewerThan(predictedTick))
                    {
                        var spawnedGhost = predictedDespawnQueue.Dequeue();
                        if (spawnedGhostMap.TryGetValue(spawnedGhost.ghost, out var ent))
                        {
                            commandBuffer.DestroyEntity(ent);
                            spawnedGhostMap.Remove(spawnedGhost.ghost);
                        }
                    }
                }
            }
        }
    }
}
