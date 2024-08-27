using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Scenes;
using Unity.Burst;

namespace Unity.NetCode
{
    /// <summary>
    /// The ServerTrackLoadedPrespawnSections is responsible for tracking when an initialized prespawn sections is unloaded
    /// in order to release any allocated data and freeing ghost id ranges.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PrespawnGhostSystemGroup))]
    [UpdateAfter(typeof(ServerPopulatePrespawnedGhostsSystem))]
    [BurstCompile]
    public partial struct ServerTrackLoadedPrespawnSections : ISystem
    {
        EntityQuery m_UnloadedSubscenes;
        EntityQuery m_Prespawns;
        EntityQuery m_AllPrespawnScenes;

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
            m_AllPrespawnScenes = state.GetEntityQuery(ComponentType.ReadOnly<SubSceneWithPrespawnGhosts>());

            state.RequireForUpdate(m_UnloadedSubscenes);
            state.RequireForUpdate<GhostCollection>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var unloadedSections = m_UnloadedSubscenes.ToEntityArray(Allocator.Temp);

            if (unloadedSections.Length == 0)
                return;

            var entityCommandBuffer = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            //Only process scenes for which all prefabs has been already destroyed
            var subsceneCollection = SystemAPI.GetSingletonBuffer<PrespawnSceneLoaded>();
            var allocatedRanges = SystemAPI.GetSingletonBuffer<PrespawnGhostIdRange>();
            var netDebug = SystemAPI.GetSingleton<NetDebug>();
            var unloadedGhostRange = new NativeList<int2>(state.WorldUpdateAllocator);
            for(int i=0;i<unloadedSections.Length;++i)
            {
                var stateComponent = state.EntityManager.GetComponentData<SubSceneWithGhostCleanup>(unloadedSections[i]);
                m_Prespawns.SetSharedComponentFilter(new SubSceneGhostComponentHash { Value = stateComponent.SubSceneHash });

                //If there are still some ghosts present, don't remove the scene from the scene list yet
                //NOTE:
                //This check can only detect if the ghosts has been despawn. The entity however may be
                //still pending for ack and tracked by the GhostSystemComponent.
                if (!m_Prespawns.IsEmpty)
                    continue;

                //Lookup and remove the scene from the collection
                int idx = 0;
                for (; idx < subsceneCollection.Length; ++idx)
                {
                    if (subsceneCollection[idx].SubSceneHash == stateComponent.SubSceneHash)
                        break;
                }

                if (idx != subsceneCollection.Length)
                {
                    subsceneCollection.RemoveAtSwapBack(idx);
                }
                else
                {
                    netDebug.LogError($"Scene with hash {stateComponent.SubSceneHash} not found in active subscene list");
                }
                //Release the id range for later reuse. For now we allow reuse the same ghost ids for the same scene
                //for sake of simplicity
                unloadedGhostRange.Add(new int2(stateComponent.FirstGhostId, stateComponent.PrespawnCount));
                for (int rangeIdx = 0; i < allocatedRanges.Length; ++rangeIdx)
                {
                    if (allocatedRanges[rangeIdx].Reserved != 0 &&
                        allocatedRanges[rangeIdx].SubSceneHash == stateComponent.SubSceneHash)
                    {
                        allocatedRanges[rangeIdx] = new PrespawnGhostIdRange
                        {
                            SubSceneHash = allocatedRanges[rangeIdx].SubSceneHash,
                            FirstGhostId = allocatedRanges[rangeIdx].FirstGhostId,
                            Count = allocatedRanges[rangeIdx].Count,
                            Reserved = 0
                        };
                        break;
                    }
                }
                entityCommandBuffer.RemoveComponent<PrespawnsSceneInitialized>(unloadedSections[i]);
                entityCommandBuffer.RemoveComponent<SubScenePrespawnBaselineResolved>(unloadedSections[i]);
                entityCommandBuffer.RemoveComponent<SubSceneWithGhostCleanup>(unloadedSections[i]);
            }

            if (unloadedGhostRange.Length == 0)
                return;
            //Schedule a cleanup job for the despawn list in case there are prespawn present
            //Once the range has been release (Reserved == 0) the ghost witch belong to that range
            //are not added to the queue in the GhostSendSystem.
            var cleanupJob = new PrespawnSceneCleanup
            {
                unloadedGhostRange = unloadedGhostRange,
                despawns = SystemAPI.GetSingletonRW<SpawnedGhostEntityMap>().ValueRO.ServerDestroyedPrespawns
            };
            state.Dependency = cleanupJob.Schedule(state.Dependency);

            //If no prespawn scenes present, destroy the prespawn scene list
            if(subsceneCollection.Length == 0 && m_AllPrespawnScenes.IsEmpty)
                entityCommandBuffer.DestroyEntity(SystemAPI.GetSingletonEntity<PrespawnSceneLoaded>());
        }

        [BurstCompile]
        struct PrespawnSceneCleanup : IJob
        {
            public NativeList<int2> unloadedGhostRange;
            public NativeList<int> despawns;
            public void Execute()
            {
                for (int i = 0; i < unloadedGhostRange.Length; ++i)
                {
                    var firstId = unloadedGhostRange[i].x;
                    for (int idx = 0; idx < unloadedGhostRange[i].y; ++idx)
                    {
                        var ghostId = PrespawnHelper.MakePrespawnGhostId(firstId + idx);
                        var found = despawns.IndexOf(ghostId);
                        if (found != -1)
                            despawns.RemoveAtSwapBack(found);
                    }
                }
            }
        }
    }
}
