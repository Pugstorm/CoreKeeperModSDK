#if UNITY_EDITOR && !NETCODE_NDEBUG
#define NETCODE_DEBUG
#endif
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.NetCode.LowLevel.Unsafe;
using Unity.Scenes;
using Unity.Burst;

namespace Unity.NetCode
{
    /// <summary>
    /// InitializePrespawnGhostSystem systems is responsible to prepare and initialize all sub-scenes pre-spawned ghosts
    /// The initialization process is quite involved and need multiple steps:
    /// - perform component stripping based on the ghost prefab metadata (MAJOR STRUCTURAL CHANGES)
    /// - kickoff baseline serialization
    /// - compute and assign the compound baseline hash to each subscene
    ///
    /// The process start by finding the subscenes subset that has all the ghost archetype serializer ready.
    /// A component stripping, serialization and baseline assignment jobs is started for each subscene in parallel.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(PrespawnGhostSystemGroup))]
    [BurstCompile]
    partial struct PrespawnGhostInitializationSystem : ISystem, ISystemStartStop
    {
        EntityQuery m_PrespawnBaselines;
        EntityQuery m_UninitializedScenes;
        EntityQuery m_Prespawns;

        Entity m_SubSceneListPrefab;

        ComponentLookup<GhostPrefabMetaData> m_GhostPrefabMetaDataLookup;
        BufferTypeHandle<LinkedEntityGroup> m_LinkedEntityGroupHandle;
        ComponentTypeHandle<GhostType> m_GhostTypeComponentHandle;

        BufferLookup<GhostComponentSerializer.State> m_GhostComponentSerializerStateHandle;
        BufferLookup<GhostCollectionPrefabSerializer> m_GhostCollectionPrefabSerializerHandle;
        BufferLookup<GhostCollectionComponentIndex> m_GhostCollectionComponentIndexHandle;
        BufferLookup<GhostCollectionPrefab> m_GhostCollectionPrefabHandle;
        BufferTypeHandle<PrespawnGhostBaseline> m_PrespawnGhostBaselineHandle;
        EntityTypeHandle m_EntityTypeHandle;
        ComponentLookup<GhostInstance> m_GhostComponentFromEntity;
        ComponentLookup<SubSceneWithPrespawnGhosts> m_SubSceneWithPrespawnGhostsFromEntity;


        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<PrespawnGhostBaseline>()
                .WithAll<SubSceneGhostComponentHash>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities);
            m_PrespawnBaselines = state.GetEntityQuery(builder);
            builder.Reset();
            builder.WithAllRW<PreSpawnedGhostIndex>()
                .WithAll<SubSceneGhostComponentHash, GhostType>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities);
            m_Prespawns = state.GetEntityQuery(builder);
            builder.Reset();
            builder.WithAll<SubSceneWithPrespawnGhosts, IsSectionLoaded>()
                .WithNone<SubScenePrespawnBaselineResolved>();
            m_UninitializedScenes = state.GetEntityQuery(builder);

            m_GhostPrefabMetaDataLookup = state.GetComponentLookup<GhostPrefabMetaData>(true);
            m_LinkedEntityGroupHandle = state.GetBufferTypeHandle<LinkedEntityGroup>(true);
            m_GhostTypeComponentHandle = state.GetComponentTypeHandle<GhostType>(true);
            m_GhostComponentSerializerStateHandle = state.GetBufferLookup<GhostComponentSerializer.State>(true);
            m_GhostCollectionPrefabSerializerHandle = state.GetBufferLookup<GhostCollectionPrefabSerializer>(true);
            m_GhostCollectionComponentIndexHandle = state.GetBufferLookup<GhostCollectionComponentIndex>(true);
            m_GhostCollectionPrefabHandle = state.GetBufferLookup<GhostCollectionPrefab>(true);
            m_PrespawnGhostBaselineHandle = state.GetBufferTypeHandle<PrespawnGhostBaseline>();
            m_EntityTypeHandle = state.GetEntityTypeHandle();
            m_GhostComponentFromEntity = state.GetComponentLookup<GhostInstance>(true);
            m_SubSceneWithPrespawnGhostsFromEntity = state.GetComponentLookup<SubSceneWithPrespawnGhosts>();

            state.RequireForUpdate<GhostCollection>();
            // Ignore scene loaded in the query for running so the singleton is created in time
            builder.Reset();
            builder.WithAll<SubSceneWithPrespawnGhosts>()
                .WithNone<SubScenePrespawnBaselineResolved>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if(m_SubSceneListPrefab != Entity.Null)
                state.EntityManager.DestroyEntity(m_SubSceneListPrefab);
        }

        public void OnStartRunning(ref SystemState state)
        {
            //This need to be delayed here to avoid creating this entity if not required (so no prespawn presents)
            if (m_SubSceneListPrefab == Entity.Null)
            {
                m_SubSceneListPrefab = PrespawnHelper.CreatePrespawnSceneListGhostPrefab(state.EntityManager);
                state.RequireForUpdate(m_Prespawns);
            }
        }
        public void OnStopRunning(ref SystemState state)
        {}

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (m_UninitializedScenes.IsEmptyIgnoreFilter)
                return;
            var collectionEntity = SystemAPI.GetSingletonEntity<GhostCollection>();
            var ghostPrefabTypes = state.EntityManager.GetBuffer<GhostCollectionPrefab>(collectionEntity);
            //No data loaded yet. This condition can be true for both client and server.
            //Server in particular can be in this state until at least one connection enter the in-game state.
            //Client can hit this until it receives the prefabs to process from the Server.
            if(ghostPrefabTypes.Length == 0)
                return;

            var processedPrefabs = new NativeParallelHashMap<GhostType, Entity>(256, state.WorldUpdateAllocator);
            var subSceneWithPrespawnGhosts = m_UninitializedScenes.ToComponentDataArray<SubSceneWithPrespawnGhosts>(Allocator.Temp);
            var subScenesSections = m_UninitializedScenes.ToEntityArray(Allocator.Temp);
            var readySections = new NativeList<int>(subScenesSections.Length, Allocator.Temp);

            //Populate a map for faster retrieval and used also by component stripping job
            for (int i = 0; i < ghostPrefabTypes.Length; ++i)
            {
                if(ghostPrefabTypes[i].GhostPrefab != Entity.Null)
                    processedPrefabs.Add(ghostPrefabTypes[i].GhostType, ghostPrefabTypes[i].GhostPrefab);
            }

            //Find out all the scenes that have all their prespawn ghost type resolved by the ghost collection.
            //(so we have the serializer ready)
            for (int i = 0; i < subScenesSections.Length; ++i)
            {
                //For large number would make sense to schedule a job for that
                var sharedFilter = new SubSceneGhostComponentHash {Value = subSceneWithPrespawnGhosts[i].SubSceneHash};
                m_Prespawns.SetSharedComponentFilter(sharedFilter);
                var ghostTypes = m_Prespawns.ToComponentDataArray<GhostType>(Allocator.Temp);
                bool allArchetypeProcessed = true;
                for(int t=0;t<ghostTypes.Length && allArchetypeProcessed;++t)
                    allArchetypeProcessed &= processedPrefabs.ContainsKey(ghostTypes[t]);
                if(allArchetypeProcessed)
                    readySections.Add(i);
            }
            m_Prespawns.ResetFilter();

            //If not scene has resolved the ghost prefab, or has been loaded early exit
            if (readySections.Length == 0)
                return;

            //Remove the disable components. Is faster this way than using command buffers because this
            //will affect the whole chunk at once
            for (int readyScene = 0; readyScene < readySections.Length; ++readyScene)
            {
                var sceneIndex = readySections[readyScene];
                var sharedFilter = new SubSceneGhostComponentHash {Value = subSceneWithPrespawnGhosts[sceneIndex].SubSceneHash};
                m_Prespawns.SetSharedComponentFilter(sharedFilter);
                state.EntityManager.RemoveComponent<Disabled>(m_Prespawns);
            }
            var netDebug = SystemAPI.GetSingleton<NetDebug>();
            m_GhostPrefabMetaDataLookup.Update(ref state);
            m_LinkedEntityGroupHandle.Update(ref state);
            m_GhostTypeComponentHandle.Update(ref state);
            //kickoff strip components jobs on all the prefabs for each subscene
            var jobs = new NativeList<JobHandle>(readySections.Length, Allocator.Temp);
            for (int readyScene = 0; readyScene < readySections.Length; ++readyScene)
            {
                var sceneIndex = readySections[readyScene];
                var sharedFilter = new SubSceneGhostComponentHash {Value = subSceneWithPrespawnGhosts[sceneIndex].SubSceneHash};
                m_Prespawns.SetSharedComponentFilter(sharedFilter);
                //Strip components this can be a large chunks of major structural changes and it is scheduled
                //at the beginning of the next simulation update
                var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
                LogStrippingPrespawn(ref netDebug, subSceneWithPrespawnGhosts[sceneIndex]);
                var stripPrespawnGhostJob = new PrespawnGhostStripComponentsJob
                {
                    metaDataFromEntity = m_GhostPrefabMetaDataLookup,
                    linkedEntityTypeHandle = m_LinkedEntityGroupHandle,
                    ghostTypeHandle = m_GhostTypeComponentHandle,
                    prefabFromType = processedPrefabs,
                    commandBuffer = ecb.AsParallelWriter(),
                    netDebug = netDebug,
                    server = (byte) (state.WorldUnmanaged.IsServer() ? 1 : 0),
                };
                jobs.Add(stripPrespawnGhostJob.ScheduleParallel(m_Prespawns, state.Dependency));
            }
            state.Dependency = JobHandle.CombineDependencies(jobs.AsArray());
            m_Prespawns.ResetFilter();

            //In case the prespawn baselines are not present just mark everything as resolved
            if (m_PrespawnBaselines.IsEmptyIgnoreFilter)
            {
                for (int readyScene = 0; readyScene < readySections.Length; ++readyScene)
                {
                    var sceneIndex = readySections[readyScene];
                    var subScene = subScenesSections[sceneIndex];
                    state.EntityManager.AddComponent<SubScenePrespawnBaselineResolved>(subScene);
                }
                return;
            }

            m_GhostComponentSerializerStateHandle.Update(ref state);
            m_GhostCollectionPrefabSerializerHandle.Update(ref state);
            m_GhostCollectionComponentIndexHandle.Update(ref state);
            m_GhostCollectionPrefabHandle.Update(ref state);
            m_PrespawnGhostBaselineHandle.Update(ref state);
            m_EntityTypeHandle.Update(ref state);
            m_GhostComponentFromEntity.Update(ref state);

            //Serialize the baseline and add the resolved tag.
            var serializerJob = new PrespawnGhostSerializer
            {
                GhostComponentCollectionFromEntity = m_GhostComponentSerializerStateHandle,
                GhostTypeCollectionFromEntity = m_GhostCollectionPrefabSerializerHandle,
                GhostComponentIndexFromEntity = m_GhostCollectionComponentIndexHandle,
                GhostCollectionFromEntity = m_GhostCollectionPrefabHandle,
                ghostTypeComponentType = m_GhostTypeComponentHandle,
                prespawnBaseline = m_PrespawnGhostBaselineHandle,
                entityType = m_EntityTypeHandle,
                childEntityLookup = state.GetEntityStorageInfoLookup(),
                linkedEntityGroupType = m_LinkedEntityGroupHandle,
                ghostFromEntity = m_GhostComponentFromEntity,
                GhostCollectionSingleton = collectionEntity
            };
            var ghostComponentCollection = state.EntityManager.GetBuffer<GhostCollectionComponentType>(collectionEntity);
            DynamicTypeList.PopulateList(ref state, ghostComponentCollection, true, ref serializerJob.ghostChunkComponentTypes);

            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            m_SubSceneWithPrespawnGhostsFromEntity.Update(ref state);
            for (int readyScene = 0; readyScene < readySections.Length; ++readyScene)
            {
                var sceneIndex = readySections[readyScene];
                LogSerializingBaselines(ref netDebug, subSceneWithPrespawnGhosts[sceneIndex]);
                var subScene = subScenesSections[sceneIndex];
                var subSceneWithGhost = subSceneWithPrespawnGhosts[sceneIndex];
                var sharedFilter = new SubSceneGhostComponentHash {Value = subSceneWithGhost.SubSceneHash};
                m_PrespawnBaselines.SetSharedComponentFilter(sharedFilter);
                // Serialize the baselines and store the baseline hashes
                var baselinesHashes = new NativeList<ulong>(subSceneWithGhost.PrespawnCount, state.WorldUpdateAllocator);
                serializerJob.baselineHashes = baselinesHashes.AsParallelWriter();
                var serializeJobHandle = serializerJob.ScheduleParallelByRef(m_PrespawnBaselines, state.Dependency);
                // Calculate the aggregate baseline hash for all the ghosts in the scene
                var aggregateJob = new AggregateHash
                {
                    baselinesHashes = baselinesHashes,
                    subSceneWithGhostFromEntity = m_SubSceneWithPrespawnGhostsFromEntity,
                    subSceneWithGhost = subSceneWithGhost,
                    subScene = subScene
                };
                state.Dependency = aggregateJob.Schedule(serializeJobHandle);
                //mark as resolved
                commandBuffer.AddComponent<SubScenePrespawnBaselineResolved>(subScene);
            }
            //Playback immediately the resolved scenes
            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();
        }

        [BurstCompile]
        struct AggregateHash : IJob
        {
            public NativeList<ulong> baselinesHashes;
            public ComponentLookup<SubSceneWithPrespawnGhosts> subSceneWithGhostFromEntity;
            public SubSceneWithPrespawnGhosts subSceneWithGhost;
            public Entity subScene;
            public void Execute()
            {
                //Sort to maintain consistent order
                baselinesHashes.Sort();
                ulong baselineHash;
                unsafe
                {
                    baselineHash = Unity.Core.XXHash.Hash64((byte*)baselinesHashes.GetUnsafeReadOnlyPtr(),
                        baselinesHashes.Length * sizeof(ulong));
                }
                subSceneWithGhost.BaselinesHash = baselineHash;
                subSceneWithGhostFromEntity[subScene] = subSceneWithGhost;
            }
        }

        [Conditional("NETCODE_DEBUG")]
        private void LogStrippingPrespawn(ref NetDebug netDebug, in SubSceneWithPrespawnGhosts subSceneWithPrespawnGhosts)
        {
            netDebug.DebugLog(FixedString.Format("Initializing prespawn scene Hash:{0} Count:{1}",
                NetDebug.PrintHex(subSceneWithPrespawnGhosts.SubSceneHash),
                subSceneWithPrespawnGhosts.PrespawnCount));
        }
        [Conditional("NETCODE_DEBUG")]
        private void LogSerializingBaselines(ref NetDebug netDebug, in SubSceneWithPrespawnGhosts subSceneWithPrespawnGhosts)
        {
            netDebug.DebugLog(FixedString.Format("Serializing baselines for prespawn scene Hash:{0} Count:{1}",
                NetDebug.PrintHex(subSceneWithPrespawnGhosts.SubSceneHash),
                subSceneWithPrespawnGhosts.PrespawnCount));
        }
    }
}
