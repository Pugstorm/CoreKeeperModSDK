using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Scenes;
using Unity.Burst;

namespace Unity.NetCode
{
    /// <summary>
    /// The ClientTrackLoadedPrespawnSections is responsible for tracking when a scene section is unloaded and
    /// removing the pre-spawned ghosts from the client ghosts maps
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(PrespawnGhostSystemGroup))]
    [UpdateAfter(typeof(PrespawnGhostInitializationSystem))]
    [BurstCompile]
    public partial struct ClientTrackLoadedPrespawnSections : ISystem
    {
        private EntityQuery m_UnloadedSubscenes;
        private EntityQuery m_Prespawns;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SubSceneWithGhostCleanup>()
                .WithNone<IsSectionLoaded>();
            m_UnloadedSubscenes = state.GetEntityQuery(builder);
            builder.Reset();
            builder.WithAll<PreSpawnedGhostIndex, SubSceneGhostComponentHash>();
            m_Prespawns = state.GetEntityQuery(builder);

            state.RequireForUpdate<GhostCollection>();
            state.RequireForUpdate(m_UnloadedSubscenes);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var unloadedScenes = m_UnloadedSubscenes.ToEntityArray(Allocator.Temp);

            if(unloadedScenes.Length == 0)
                return;

            //Only process scenes for which all prefabs have been already destroyed
            var ghostsToRemove = new NativeList<SpawnedGhost>(128, state.WorldUpdateAllocator);
            var entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);
            for(int i=0;i<unloadedScenes.Length;++i)
            {
                var stateComponent = state.EntityManager.GetComponentData<SubSceneWithGhostCleanup>(unloadedScenes[i]);
                m_Prespawns.SetSharedComponentFilter(new SubSceneGhostComponentHash { Value = stateComponent.SubSceneHash });
                if (m_Prespawns.IsEmpty)
                {
                    var firstId = PrespawnHelper.PrespawnGhostIdBase + stateComponent.FirstGhostId;
                    for (int p = 0; p < stateComponent.PrespawnCount; ++p)
                    {
                        ghostsToRemove.Add(new SpawnedGhost
                        {
                            ghostId = (int) (firstId + p),
                            spawnTick = NetworkTick.Invalid
                        });
                    }

                    entityCommandBuffer.RemoveComponent<PrespawnsSceneInitialized>(unloadedScenes[i]);
                    entityCommandBuffer.RemoveComponent<SubScenePrespawnBaselineResolved>(unloadedScenes[i]);
                    entityCommandBuffer.RemoveComponent<SubSceneWithGhostCleanup>(unloadedScenes[i]);
                }
            }
            entityCommandBuffer.Playback(state.EntityManager);

            if (ghostsToRemove.Length == 0)
                return;

            //Remove the ghosts from the spawn maps
            ref readonly var ghostMapSingleton = ref SystemAPI.GetSingletonRW<SpawnedGhostEntityMap>().ValueRO;
            var removeJob = new RemovePrespawnedGhosts
            {
                ghostsToRemove = ghostsToRemove,
                spawnedGhostEntityMap = ghostMapSingleton.SpawnedGhostMapRW,
                ghostEntityMap = ghostMapSingleton.ClientGhostEntityMap
            };
            state.Dependency = removeJob.Schedule(state.Dependency);
        }
        [BurstCompile]
        struct RemovePrespawnedGhosts : IJob
        {
            public NativeList<SpawnedGhost> ghostsToRemove;
            public NativeParallelHashMap<SpawnedGhost, Entity> spawnedGhostEntityMap;
            public NativeParallelHashMap<int, Entity> ghostEntityMap;
            public void Execute()
            {
                for(int i=0;i<ghostsToRemove.Length;++i)
                {
                    spawnedGhostEntityMap.Remove(ghostsToRemove[i]);
                    ghostEntityMap.Remove(ghostsToRemove[i].ghostId);
                }
            }
        }
    }
}
