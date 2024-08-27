#if UNITY_EDITOR && !NETCODE_NDEBUG
#define NETCODE_DEBUG
#endif
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

namespace Unity.NetCode
{
    /// <summary>
    /// Responsible for assigning a unique <see cref="GhostInstance.ghostId"/> to each pre-spawned ghost,
    /// and adding the ghost to the spawned ghosts maps.
    /// Relies on the previous initializations step to determine the subscene subset to process.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Clients expect to receive the following as part ot the protocol:
    /// <para>- The subscene hash and baseline hash for validation.</para>
    /// <para>- The ghost id range for each subscene.</para>
    /// </para>
    /// <para>### The Full Prespawn Subscene Sync Protocol</para>
    /// <para>
    /// The Client will eventually receive the subscene data and will store it into the `PrespawnSceneLoaded` collection.
    /// The Client (in parallel, before or after) will serialize the prespawn baseline when a new scene is loaded.
    /// The Client should validate that:
    /// <para>- The prespawn scenes are present on the server.</para>
    /// <para>- That the prespawn ghost count, subscene hash and baseline hash match the one on the server.</para>
    /// The Client will assign the ghost ids to the prespawns.
    /// The Client must notify the server what scene sections has been loaded and initialized.
    /// </para>
    /// <seealso cref="ServerPopulatePrespawnedGhostsSystem"/>
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(PrespawnGhostSystemGroup))]
    [UpdateAfter(typeof(PrespawnGhostInitializationSystem))]
    [BurstCompile]
    public partial struct ClientPopulatePrespawnedGhostsSystem : ISystem
    {
        private EntityQuery m_UninitializedScenes;
        private EntityQuery m_Prespawns;

        private EntityTypeHandle m_EntityTypeHandle;
        private ComponentTypeHandle<PreSpawnedGhostIndex> m_PreSpawnedGhostIndexHandle;
        private ComponentTypeHandle<GhostInstance> m_GhostComponentHandle;
        private ComponentTypeHandle<GhostCleanup> m_GhostCleanupComponentHandle;

        enum ValidationResult
        {
            ValidationSucceed = 0,
            SubSceneNotFound,
            MetadataNotMatch
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SubSceneWithPrespawnGhosts, SubScenePrespawnBaselineResolved>()
                .WithNone<PrespawnsSceneInitialized>();
            m_UninitializedScenes = state.GetEntityQuery(builder);
            builder.Reset();
            builder.WithAll<PreSpawnedGhostIndex, SubSceneGhostComponentHash>();
            m_Prespawns = state.GetEntityQuery(builder);

            m_EntityTypeHandle = state.GetEntityTypeHandle();
            m_PreSpawnedGhostIndexHandle = state.GetComponentTypeHandle<PreSpawnedGhostIndex>(true);
            m_GhostComponentHandle = state.GetComponentTypeHandle<GhostInstance>();
            m_GhostCleanupComponentHandle = state.GetComponentTypeHandle<GhostCleanup>();

            state.RequireForUpdate(m_UninitializedScenes);
            state.RequireForUpdate(m_Prespawns);
            state.RequireForUpdate<NetworkStreamInGame>();
            state.RequireForUpdate<GhostCollection>();
            state.RequireForUpdate<PrespawnSceneLoaded>();
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var subsceneCollection = SystemAPI.GetSingletonBuffer<PrespawnSceneLoaded>();
            //Early exit. Nothing to process (the list is empty). That means the server has not sent yet the data OR the
            //subscene must be unloaded. In either cases, the client can't assign ids.
            if(subsceneCollection.Length == 0)
                return;

            var subScenesWithGhosts = m_UninitializedScenes.ToComponentDataArray<SubSceneWithPrespawnGhosts>(Allocator.Temp);
            //x -> the subScene index
            //y -> the collection index
            var validScenes = new NativeList<int2>(subScenesWithGhosts.Length, Allocator.Temp);
            //First validate all the data before scheduling any job
            //We are not checking for missing sub scenes on the client that are present on the server. By design it is possible
            //for a client to load just a subset of all server's subscene at any given time.
            var totalValidPrespawns = 0;
            var hasValidationError = false;
            var netDebug = SystemAPI.GetSingleton<NetDebug>();
            for (int i = 0; i < subScenesWithGhosts.Length; ++i)
            {
                var validationResult = ValidatePrespawnGhostSubSceneData(ref netDebug, subScenesWithGhosts[i].SubSceneHash,
                    subScenesWithGhosts[i].BaselinesHash, subScenesWithGhosts[i].PrespawnCount, subsceneCollection,
                    out var collectionIndex);
                if (validationResult == ValidationResult.SubSceneNotFound)
                {
                    //What that means:
                    // - Client loaded the scene at the same time or before the server did and the updated scene list
                    //   has been not received yet.
                    // - The server has unloaded the scene. In that case, it is responsibility of the client to unloading it
                    //   (usually using a higher level protocol that is user/game dependent). Most likely
                    // On both cases is not really an error. The client should just wait for the new list in the first case and remove
                    // the scene in the second
                    // Would be nice being able to differentiate in between the two cases.
                    continue;
                }
                if (validationResult == ValidationResult.MetadataNotMatch)
                {
                    //We log all the errors first and the we will request a disconnection
                    hasValidationError = true;
                    continue;
                }
                validScenes.Add(new int2(i, collectionIndex));
                totalValidPrespawns += subScenesWithGhosts[i].PrespawnCount;
            }
            if(hasValidationError)
            {
                //Disconnect the client
                state.EntityManager.AddComponent<NetworkStreamRequestDisconnect>(SystemAPI.GetSingletonEntity<NetworkId>());
                return;
            }
            //Kick a job for each sub-scene that assign the ghost id to all scene prespawn ghosts.
            var subscenes = m_UninitializedScenes.ToEntityArray(Allocator.Temp);
            var entityCommandBuffer = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            //This temporary list is necessary because we forcibly re-assign the entity to spawn maps in case the ghost is already registered.
            var spawnedGhosts = new NativeList<SpawnedGhostMapping>(totalValidPrespawns, state.WorldUpdateAllocator);
            m_EntityTypeHandle.Update(ref state);
            m_PreSpawnedGhostIndexHandle.Update(ref state);
            m_GhostComponentHandle.Update(ref state);
            m_GhostCleanupComponentHandle.Update(ref state);
            for (int i = 0; i < validScenes.Length; ++i)
            {
                var sceneIndex = validScenes[i].x;
                var collectionIndex = validScenes[i].y;
                var sharedFilter = new SubSceneGhostComponentHash {Value = subScenesWithGhosts[sceneIndex].SubSceneHash};
                m_Prespawns.SetSharedComponentFilter(sharedFilter);
                LogAssignPrespawnGhostIds(ref netDebug, subScenesWithGhosts[sceneIndex]);
                var assignPrespawnGhostIdJob = new AssignPrespawnGhostIdJob
                {
                    entityType = m_EntityTypeHandle,
                    prespawnIndexType = m_PreSpawnedGhostIndexHandle,
                    ghostComponentType = m_GhostComponentHandle,
                    ghostStateTypeHandle = m_GhostCleanupComponentHandle,
                    startGhostId = subsceneCollection[collectionIndex].FirstGhostId,
                    spawnedGhosts = spawnedGhosts.AsParallelWriter(),
                    netDebug = netDebug
                };
                state.Dependency = assignPrespawnGhostIdJob.ScheduleParallel(m_Prespawns, state.Dependency);
                //Add a state component to track the scene lifetime.
                var sceneSectionData = default(SceneSectionData);
#if UNITY_EDITOR
                if (state.EntityManager.HasComponent<LiveLinkPrespawnSectionReference>(subscenes[i]))
                {
                    var sceneSectionRef = state.EntityManager.GetComponentData<LiveLinkPrespawnSectionReference>(subscenes[i]);
                    sceneSectionData.SceneGUID = sceneSectionRef.SceneGUID;
                    sceneSectionData.SubSectionIndex = sceneSectionRef.Section;
                }
                else
#endif
                    sceneSectionData = state.EntityManager.GetComponentData<SceneSectionData>(subscenes[sceneIndex]);
                entityCommandBuffer.AddComponent(subscenes[sceneIndex], new SubSceneWithGhostCleanup
                {
                    SubSceneHash = subScenesWithGhosts[sceneIndex].SubSceneHash,
                    FirstGhostId = subsceneCollection[collectionIndex].FirstGhostId,
                    PrespawnCount = subScenesWithGhosts[sceneIndex].PrespawnCount,
                    SceneGUID =  sceneSectionData.SceneGUID,
                    SectionIndex =  sceneSectionData.SubSectionIndex,
                });
                entityCommandBuffer.AddComponent<PrespawnsSceneInitialized>(subscenes[sceneIndex]);
            }
            m_Prespawns.ResetFilter();
            ref readonly var spawnedGhostEntityMap = ref SystemAPI.GetSingletonRW<SpawnedGhostEntityMap>().ValueRO;
            var addJob = new ClientAddPrespawn
            {
                netDebug = netDebug,
                spawnedGhosts = spawnedGhosts,
                ghostMap = spawnedGhostEntityMap.SpawnedGhostMapRW,
                ghostEntityMap = spawnedGhostEntityMap.ClientGhostEntityMap
            };
            state.Dependency = addJob.Schedule(state.Dependency);
        }
        [BurstCompile]
        struct ClientAddPrespawn : IJob
        {
            public NetDebug netDebug;
            public NativeList<SpawnedGhostMapping> spawnedGhosts;
            public NativeParallelHashMap<SpawnedGhost, Entity> ghostMap;
            public NativeParallelHashMap<int, Entity> ghostEntityMap;
            public void Execute()
            {
                for (int i = 0; i < spawnedGhosts.Length; ++i)
                {
                    var newGhost = spawnedGhosts[i];
                    if (newGhost.ghost.ghostId == 0)
                    {
                        netDebug.LogError("Prespawn ghost id not assigned.");
                        return;
                    }

                    if (!ghostMap.TryAdd(newGhost.ghost, newGhost.entity))
                    {
                        netDebug.LogError($"GhostID {newGhost.ghost.ghostId} already present in the spawned ghost entity map.");
                        ghostMap[newGhost.ghost] = newGhost.entity;
                    }

                    if (!ghostEntityMap.TryAdd(newGhost.ghost.ghostId, newGhost.entity))
                    {
                        netDebug.LogError($"GhostID {newGhost.ghost.ghostId} already present in the ghost entity map. Overwrite");
                        ghostEntityMap[newGhost.ghost.ghostId] = newGhost.entity;
                    }
                }
            }
        }

        ValidationResult ValidatePrespawnGhostSubSceneData(ref NetDebug netDebug, ulong subSceneHash, ulong subSceneBaselineHash, int prespawnCount,
            in DynamicBuffer<PrespawnSceneLoaded> serverPrespawnHashBuffer, out int index)
        {
            //find a matching entry
            index = -1;
            for (int i = 0; i < serverPrespawnHashBuffer.Length; ++i)
            {
                if (serverPrespawnHashBuffer[i].SubSceneHash == subSceneHash)
                {
                    //check if the baseline matches
                    if (serverPrespawnHashBuffer[i].BaselineHash != subSceneBaselineHash)
                    {
                        netDebug.LogError(
                            $"Subscene {subSceneHash} baseline mismatch. Server:{serverPrespawnHashBuffer[i].BaselineHash} Client:{subSceneBaselineHash}");
                        return ValidationResult.MetadataNotMatch;
                    }

                    if (serverPrespawnHashBuffer[i].PrespawnCount != prespawnCount)
                    {
                        netDebug.LogError(
                            $"Subscene {subSceneHash} has different prespawn count. Server:{serverPrespawnHashBuffer[i].PrespawnCount} Client:{prespawnCount}");
                        return ValidationResult.MetadataNotMatch;
                    }

                    index = i;
                    return ValidationResult.ValidationSucceed;
                }
            }
            return ValidationResult.SubSceneNotFound;
        }

        [Conditional("NETCODE_DEBUG")]
        void LogAssignPrespawnGhostIds(ref NetDebug netDebug, in SubSceneWithPrespawnGhosts subScenesWithGhosts)
        {
            netDebug.DebugLog(FixedString.Format("Assigning prespawn ghost ids for scene Hash:{0} Count:{1}",
                NetDebug.PrintHex(subScenesWithGhosts.SubSceneHash), subScenesWithGhosts.PrespawnCount));
        }
    }
}
