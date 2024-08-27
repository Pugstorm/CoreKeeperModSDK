#if UNITY_EDITOR && !NETCODE_NDEBUG
#define NETCODE_DEBUG
#endif
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;

namespace Unity.NetCode
{
    /// <summary>
    /// Responsible for assigning a unique <see cref="GhostInstance.ghostId"/> to each pre-spawned ghost,
    /// and and adding the ghosts to the spawned ghosts maps.
    /// Relies on the previous initializations step to determine the subscene subset to process.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The server is authoritative and it is responsible for assigning unique id ranges to the each scene.
    /// For each section that present prespawn ghosts, the prespawn hash, id range and baseline hash are sent to client
    /// as part of the streaming protocol.
    /// Clients will use the received subscene hash and baseline hash for validation and the ghost range to assign
    /// the ghost id to the pre-spawned ghosts like the server. This remove any necessity for loading order determinism.
    /// Finally, clients will ack the server about the loaded scenes and the server, upon ack receipt,
    /// will start streaming the pre-spawned ghosts
    /// </para>
    /// <para>### The Full Prespawn Subscene Sync Protocol</para>
    /// <para>
    /// The Server calculates the prespawn baselines.
    /// The Server assigns runtime ghost IDs to the prespawned ghosts.
    /// The Server stores the `SubSceneHash`, `BaselineHash`, `FirstGhostId`, and `PrespawnCount` inside the the `PrespawnSceneLoaded` collection.
    /// The Server creates a new ghost with a `PrespawnSceneLoaded` buffer that is serialized to the clients.
    /// </para>
    /// <seealso cref="ClientPopulatePrespawnedGhostsSystem"/>
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PrespawnGhostSystemGroup))]
    [UpdateAfter(typeof(PrespawnGhostInitializationSystem))]
    [BurstCompile]
    public partial struct ServerPopulatePrespawnedGhostsSystem : ISystem
    {
        EntityQuery m_UninitializedScenes;
        EntityQuery m_Prespawns;
        EntityQuery m_PrefabQuery;
        Entity m_GhostIdAllocator;

        EntityTypeHandle m_EntityTypeHandle;
        ComponentTypeHandle<PreSpawnedGhostIndex> m_PreSpawnedGhostIndexHandle;
        ComponentTypeHandle<GhostInstance> m_GhostComponentHandle;
        ComponentTypeHandle<GhostCleanup> m_GhostCleanupComponentHandle;
        BufferLookup<PrespawnGhostIdRange> m_PrespawnGhostIdRangeFromEntity;

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
            builder.Reset();
            builder.WithAll<PrespawnSceneLoaded, Prefab>();
            m_PrefabQuery = state.GetEntityQuery(builder);

            m_EntityTypeHandle = state.GetEntityTypeHandle();
            m_PreSpawnedGhostIndexHandle = state.GetComponentTypeHandle<PreSpawnedGhostIndex>(true);
            m_GhostComponentHandle = state.GetComponentTypeHandle<GhostInstance>();
            m_GhostCleanupComponentHandle = state.GetComponentTypeHandle<GhostCleanup>();
            m_PrespawnGhostIdRangeFromEntity = state.GetBufferLookup<PrespawnGhostIdRange>();

            var atype = new NativeArray<ComponentType>(1, Allocator.Temp);
            atype[0] = ComponentType.ReadWrite<PrespawnGhostIdRange>();
            m_GhostIdAllocator = state.EntityManager.CreateEntity(state.EntityManager.CreateArchetype(atype));
            state.EntityManager.SetName(m_GhostIdAllocator, (FixedString64Bytes)"PrespawnGhostIdAllocator");
            state.RequireForUpdate(m_UninitializedScenes);
            state.RequireForUpdate(m_Prespawns);
            // Require any number of in-game tags, server can have one per client
            state.RequireForUpdate<NetworkStreamInGame>();
            state.RequireForUpdate<GhostCollection>();
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<PrespawnSceneLoaded>(out var prespawnSceneListEntity))
            {
                var prefab = m_PrefabQuery.GetSingletonEntity();
                prespawnSceneListEntity = state.EntityManager.Instantiate(prefab);
                state.EntityManager.RemoveComponent<GhostPrefabMetaData>(prespawnSceneListEntity);
                state.EntityManager.GetBuffer<PrespawnSceneLoaded>(prespawnSceneListEntity).EnsureCapacity(128);
            }
            var subScenesWithGhosts = m_UninitializedScenes.ToComponentDataArray<SubSceneWithPrespawnGhosts>(Allocator.Temp);
            var subSceneEntities = m_UninitializedScenes.ToEntityArray(Allocator.Temp);
            // Add GhostCleanup to all ghosts
            // After some measurement this is the fastest way to achieve it. Is roughly 5/6x faster than
            // adding all the components change one by one via command buffer in a job
            // with a decent amount of entities (> 3000)
            for (int i = 0; i < subScenesWithGhosts.Length; ++i)
            {
                var sharedFilter = new SubSceneGhostComponentHash {Value = subScenesWithGhosts[i].SubSceneHash};
                m_Prespawns.SetSharedComponentFilter(sharedFilter);
                state.EntityManager.AddComponent<GhostCleanup>(m_Prespawns);
            }
            var netDebug = SystemAPI.GetSingleton<NetDebug>();
            //This temporary list is necessary because we forcibly re-assign the entity to spawn maps for both client and server in case the
            //ghost is already registered.
            var totalPrespawns = 0;
            for (int i = 0; i < subScenesWithGhosts.Length; ++i)
                totalPrespawns += subScenesWithGhosts[i].PrespawnCount;
            var spawnedGhosts = new NativeList<SpawnedGhostMapping>(totalPrespawns, state.WorldUpdateAllocator);
            //Kick a job for each sub-scene that assign the ghost id to all scene prespawn ghosts.
            //It also fill the array of prespawned ghosts that is going to be used to populate the ghost maps in the send/receive systems.
            var subsceneCollection = state.EntityManager.GetBuffer<PrespawnSceneLoaded>(prespawnSceneListEntity);
            var entityCommandBuffer = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            ref var spawnedGhostEntityMap = ref SystemAPI.GetSingletonRW<SpawnedGhostEntityMap>().ValueRW;

            m_EntityTypeHandle.Update(ref state);
            m_PreSpawnedGhostIndexHandle.Update(ref state);
            m_GhostComponentHandle.Update(ref state);
            m_GhostCleanupComponentHandle.Update(ref state);
            m_PrespawnGhostIdRangeFromEntity.Update(ref state);
            for (int i = 0; i < subScenesWithGhosts.Length; ++i)
            {
                LogAssignPrespawnGhostIds(ref netDebug, subScenesWithGhosts[i]);
                var sharedFilter = new SubSceneGhostComponentHash {Value = subScenesWithGhosts[i].SubSceneHash};
                m_Prespawns.SetSharedComponentFilter(sharedFilter);
                //Allocate or reuse an id-range for that subscene and assign the ids to the ghosts
                int startId = AllocatePrespawnGhostRange(ref netDebug, ref spawnedGhostEntityMap, subScenesWithGhosts[i].SubSceneHash, subScenesWithGhosts[i].PrespawnCount);
                var assignPrespawnGhostIdJob = new AssignPrespawnGhostIdJob
                {
                    entityType = m_EntityTypeHandle,
                    prespawnIndexType = m_PreSpawnedGhostIndexHandle,
                    ghostComponentType = m_GhostComponentHandle,
                    ghostStateTypeHandle = m_GhostCleanupComponentHandle,
                    startGhostId = startId,
                    spawnedGhosts = spawnedGhosts.AsParallelWriter(),
                    netDebug = netDebug
                };
                state.Dependency = assignPrespawnGhostIdJob.ScheduleParallel(m_Prespawns, state.Dependency);
                //add the subscene to the collection. This will be synchronized to the clients
                subsceneCollection.Add(new PrespawnSceneLoaded
                {
                    SubSceneHash = subScenesWithGhosts[i].SubSceneHash,
                    BaselineHash = subScenesWithGhosts[i].BaselinesHash,
                    FirstGhostId = startId,
                    PrespawnCount = subScenesWithGhosts[i].PrespawnCount
                });

                //Mark scenes as initialized and add tracking.
                var sceneSectionData = default(SceneSectionData);
#if UNITY_EDITOR
                if (state.EntityManager.HasComponent<LiveLinkPrespawnSectionReference>(subSceneEntities[i]))
                {
                    var sceneSectionRef = state.EntityManager.GetComponentData<LiveLinkPrespawnSectionReference>(subSceneEntities[i]);
                    sceneSectionData.SceneGUID = sceneSectionRef.SceneGUID;
                    sceneSectionData.SubSectionIndex = sceneSectionRef.Section;
                }
                else
#endif
                    sceneSectionData = state.EntityManager.GetComponentData<SceneSectionData>(subSceneEntities[i]);

                entityCommandBuffer.AddComponent<PrespawnsSceneInitialized>(subSceneEntities[i]);
                entityCommandBuffer.AddComponent(subSceneEntities[i], new SubSceneWithGhostCleanup
                {
                    SubSceneHash = subScenesWithGhosts[i].SubSceneHash,
                    FirstGhostId = startId,
                    PrespawnCount = subScenesWithGhosts[i].PrespawnCount,
                    SceneGUID = sceneSectionData.SceneGUID,
                    SectionIndex = sceneSectionData.SubSectionIndex
                });
            }
            m_Prespawns.ResetFilter();
            //Wait for all ghost ids jobs assignments completed and populate the spawned ghost map
            var addJob = new ServerAddPrespawn
            {
                netDebug = netDebug,
                spawnedGhosts = spawnedGhosts,
                ghostMap = spawnedGhostEntityMap.SpawnedGhostMapRW
            };
            state.Dependency = addJob.Schedule(state.Dependency);
        }

        [BurstCompile]
        struct ServerAddPrespawn : IJob
        {
            public NetDebug netDebug;
            public NativeList<SpawnedGhostMapping> spawnedGhosts;
            public NativeParallelHashMap<SpawnedGhost, Entity> ghostMap;
            public void Execute()
            {
                for (int i = 0; i < spawnedGhosts.Length; ++i)
                {
                    if (spawnedGhosts[i].ghost.ghostId == 0)
                    {
                        netDebug.LogError($"Prespawn ghost id not assigned.");
                        return;
                    }
                    var newGhost = spawnedGhosts[i];
                    if (!ghostMap.TryAdd(newGhost.ghost, newGhost.entity))
                    {
                        netDebug.LogError($"GhostID {newGhost.ghost.ghostId} already present in the spawned ghost entity map.");
                        //Force a reassignment.
                        ghostMap[newGhost.ghost] = newGhost.entity;
                    }
                }
            }
        }

        /// <summary>
        /// Return the start ghost id for the subscene. Id ranges are re-used by the same subscene if it is loaded again
        /// </summary>
        //TODO: the allocation may become a little more advanced by re-using ids later
        private int AllocatePrespawnGhostRange(ref NetDebug netDebug, ref SpawnedGhostEntityMap spawnedGhostEntityMap, ulong subSceneHash, int prespawnCount)
        {
            var allocatedRanges = m_PrespawnGhostIdRangeFromEntity[m_GhostIdAllocator];
            for (int r = 0; r < allocatedRanges.Length; ++r)
            {
                if (allocatedRanges[r].SubSceneHash == subSceneHash)
                {
                    //This is an error or an hash collision.
                    if (allocatedRanges[r].Reserved != 0)
                        throw new System.InvalidOperationException($"prespawn ids range already present for subscene with hash {subSceneHash}");

                    netDebug.DebugLog($"reusing prespawn ids range from {allocatedRanges[r].FirstGhostId} to {allocatedRanges[r].FirstGhostId + prespawnCount} for subscene with hash {subSceneHash}");
                    allocatedRanges[r] = new PrespawnGhostIdRange
                    {
                        SubSceneHash = subSceneHash,
                        FirstGhostId = allocatedRanges[r].FirstGhostId,
                        Count = (short)prespawnCount,
                        Reserved = 1
                    };
                    return allocatedRanges[r].FirstGhostId;
                }
            }

            var nextGhostId = 1;
            if (allocatedRanges.Length > 0)
                nextGhostId = allocatedRanges[allocatedRanges.Length - 1].FirstGhostId +
                              allocatedRanges[allocatedRanges.Length - 1].Count;

            var newRange = new PrespawnGhostIdRange
            {
                SubSceneHash = subSceneHash,
                FirstGhostId = nextGhostId,
                Count = (short)prespawnCount,
                Reserved = 1
            };
            allocatedRanges.Add(newRange);
            LogAllocatedIdRange(ref netDebug, newRange);
            //Update the prespawn allocated ids
            spawnedGhostEntityMap.SetServerAllocatedPrespawnGhostId(nextGhostId + prespawnCount);
            return newRange.FirstGhostId;
        }

        [Conditional("NETCODE_DEBUG")]
        private void LogAllocatedIdRange(ref NetDebug netDebug, PrespawnGhostIdRange rangeAlloc)
        {
            netDebug.DebugLog($"Assigned id-range [{rangeAlloc.FirstGhostId}-{rangeAlloc.FirstGhostId + rangeAlloc.Count}] to scene section with hash {NetDebug.PrintHex(rangeAlloc.SubSceneHash)}");
        }

        [Conditional("NETCODE_DEBUG")]
        void LogAssignPrespawnGhostIds(ref NetDebug netDebug, in SubSceneWithPrespawnGhosts subScenesWithGhosts)
        {
            netDebug.DebugLog(FixedString.Format("Assigning prespawn ghost ids for scene Hash:{0} Count:{1}",
                NetDebug.PrintHex(subScenesWithGhosts.SubSceneHash), subScenesWithGhosts.PrespawnCount));
        }
    }
}
