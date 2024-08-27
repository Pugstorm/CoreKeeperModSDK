using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.NetCode.LowLevel.Unsafe;
using System;
using Unity.Assertions;
using Unity.Burst.Intrinsics;
using Unity.Jobs;

namespace Unity.NetCode
{
    /// <summary>
    /// Tag added to the singleton entity that contains the <see cref="PredictedGhostSpawn"/> buffer.
    /// </summary>
    public struct PredictedGhostSpawnList : IComponentData
    {}

    /// <summary>
    /// Added to a <see cref="PredictedGhostSpawnList"/> singleton entity.
    /// Contains a transient list of ghosts that should be pre-spawned.
    /// Expects to be handled during the <see cref="GhostSpawnClassificationSystem"/> step.
    /// InternalBufferCapacity allocated to almost max out chunk memory.
    /// In practice, this capacity just needs to hold the maximum number of client-authored
    /// ghost entities per frame, which is typically in the range 0 - 1.
    /// </summary>
    [InternalBufferCapacity(950)]
    public struct PredictedGhostSpawn : IBufferElementData
    {
        /// <summary>
        /// The Entity that has been spawned.
        /// </summary>
        public Entity entity;
        /// <summary>
        /// The index of the ghost type in the <seealso cref="GhostCollectionPrefab"/> collection. Used to classify the ghost (<see cref="GhostSpawnClassificationSystem"/>).
        /// </summary>
        public int ghostType;
        /// <summary>
        /// The server tick the entity has been spawned.
        /// </summary>
        public NetworkTick spawnTick;
    }

    /// <summary>
    /// Consume all the <see cref="PredictedGhostSpawnRequest"/> requests by initializing the predicted spawned ghost
    /// and adding it to the <see cref="PredictedGhostSpawn"/> buffer.
    /// All the predicted spawned ghosts are initialized with a invalid ghost id (-1) but a valid ghost type and spawnTick.
    /// </summary>
    [UpdateInGroup(typeof(GhostSpawnSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateAfter(typeof(GhostSpawnSystem))]
    [BurstCompile]
    public partial struct PredictedGhostSpawnSystem : ISystem
    {
        EntityQuery m_GhostInitQuery;
        NetworkTick m_SpawnTick;
        NativeReference<int> m_ListHasData;

        BufferLookup<PredictedGhostSpawn> m_PredictedGhostSpawnFromEntity;

        BufferLookup<GhostComponentSerializer.State> m_GhostComponentSerializerStateFromEntity;
        BufferLookup<GhostCollectionPrefabSerializer> m_GhostCollectionPrefabSerializerFromEntity;
        BufferLookup<GhostCollectionComponentIndex> m_GhostCollectionComponentIndexFromEntity;
        BufferLookup<GhostCollectionPrefab> m_GhostCollectionPrefabFromEntity;
        EntityTypeHandle m_EntityTypeHandle;
        ComponentTypeHandle<SnapshotData> m_SnapshotDataHandle;
        BufferTypeHandle<SnapshotDataBuffer> m_SnapshotDataBufferHandle;
        BufferTypeHandle<SnapshotDynamicDataBuffer> m_SnapshotDynamicDataBufferHandle;
        BufferTypeHandle<LinkedEntityGroup> m_LinkedEntityGroupHandle;
        ComponentLookup<GhostInstance> m_GhostComponentFromEntity;
        ComponentLookup<PredictedGhost> m_PredictedGhostFromEntity;
        ComponentLookup<GhostType> m_GhostTypeComponentFromEntity;

        [BurstCompile]
        struct InitGhostJob : IJobChunk
        {
            public DynamicTypeList DynamicTypeList;

            public Entity GhostCollectionSingleton;
            [ReadOnly] public BufferLookup<GhostComponentSerializer.State> GhostComponentCollectionFromEntity;
            [ReadOnly] public BufferLookup<GhostCollectionPrefabSerializer> GhostTypeCollectionFromEntity;
            [ReadOnly] public BufferLookup<GhostCollectionComponentIndex> GhostComponentIndexFromEntity;
            [ReadOnly] public BufferLookup<GhostCollectionPrefab> GhostCollectionFromEntity;

            [ReadOnly] public EntityTypeHandle entityType;
            public ComponentTypeHandle<SnapshotData> snapshotDataType;
            public BufferTypeHandle<SnapshotDataBuffer> snapshotDataBufferType;
            public BufferTypeHandle<SnapshotDynamicDataBuffer> snapshotDynamicDataBufferType;

            public BufferLookup<PredictedGhostSpawn> spawnListFromEntity;
            public Entity spawnListEntity;

            public ComponentLookup<GhostInstance> ghostFromEntity;
            public ComponentLookup<PredictedGhost> predictedGhostFromEntity;
            [ReadOnly] public ComponentLookup<GhostType> ghostTypeFromEntity;

            public EntityCommandBuffer commandBuffer;
            public NetworkTick spawnTick;

            [ReadOnly] public BufferTypeHandle<LinkedEntityGroup> linkedEntityGroupType;
            [ReadOnly] public EntityStorageInfoLookup childEntityLookup;

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // This job is not written to support queries with enableable component types.
                Assert.IsFalse(useEnabledMask);

                DynamicComponentTypeHandle* ghostChunkComponentTypesPtr = DynamicTypeList.GetData();
                var entityList = chunk.GetNativeArray(entityType);
                var snapshotDataList = chunk.GetNativeArray(ref snapshotDataType);
                var snapshotDataBufferList = chunk.GetBufferAccessor(ref snapshotDataBufferType);
                var snapshotDynamicDataBufferList = chunk.GetBufferAccessor(ref snapshotDynamicDataBufferType);

                var GhostCollection = GhostCollectionFromEntity[GhostCollectionSingleton];
                var GhostTypeCollection = GhostTypeCollectionFromEntity[GhostCollectionSingleton];
                var ghostTypeComponent = ghostTypeFromEntity[entityList[0]];
                int ghostType;
                for (ghostType = 0; ghostType < GhostCollection.Length; ++ghostType)
                {
                    if (GhostCollection[ghostType].GhostType == ghostTypeComponent)
                        break;
                }
                if (ghostType >= GhostCollection.Length)
                    throw new InvalidOperationException("Could not find ghost type in the collection");
                if (ghostType >= GhostTypeCollection.Length)
                    return; // serialization data has not been loaded yet

                var spawnList = spawnListFromEntity[spawnListEntity];
                var typeData = GhostTypeCollection[ghostType];
                var snapshotSize = typeData.SnapshotSize;
                int changeMaskUints = GhostComponentSerializer.ChangeMaskArraySizeInUInts(typeData.ChangeMaskBits);
                int enableableMaskUints = GhostComponentSerializer.ChangeMaskArraySizeInUInts(typeData.EnableableBits);
                int snapshotBaseOffset = GhostComponentSerializer.SnapshotSizeAligned(sizeof(uint) + changeMaskUints*sizeof(uint) + enableableMaskUints*sizeof(uint));

                var helper = new GhostSerializeHelper
                {
                    serializerState = new GhostSerializerState { GhostFromEntity = ghostFromEntity },
                    ghostChunkComponentTypesPtr = ghostChunkComponentTypesPtr,
                    GhostComponentIndex = GhostComponentIndexFromEntity[GhostCollectionSingleton],
                    GhostComponentCollection = GhostComponentCollectionFromEntity[GhostCollectionSingleton],
                    childEntityLookup = childEntityLookup,
                    linkedEntityGroupType = linkedEntityGroupType,
                    ghostChunkComponentTypesPtrLen = DynamicTypeList.Length,
                    changeMaskUints = changeMaskUints
                };

                var bufferSizes = new NativeArray<int>(chunk.Count, Allocator.Temp);
                var hasBuffers = GhostTypeCollection[ghostType].NumBuffers > 0;
                if (hasBuffers)
                    helper.GatherBufferSize(chunk, 0, typeData, ref bufferSizes);

                for (int i = 0; i < entityList.Length; ++i)
                {
                    var entity = entityList[i];

                    var ghostComponent = ghostFromEntity[entity];
                    //Set a valid spawn tick but invalid ghost id for predicted spawned ghosts.
                    //This will let distinguish them from invalid ghost instances
                    ghostComponent.ghostId = 0;
                    ghostComponent.ghostType = ghostType;
                    ghostComponent.spawnTick = spawnTick;
                    ghostFromEntity[entity] = ghostComponent;
                    predictedGhostFromEntity[entity] = new PredictedGhost{AppliedTick = spawnTick, PredictionStartTick = spawnTick};
                    // Set initial snapshot data
                    // Get the buffers, fill in snapshot size etc
                    snapshotDataList[i] = new SnapshotData{SnapshotSize = snapshotSize, LatestIndex = 0};
                    var snapshotDataBuffer = snapshotDataBufferList[i];
                    snapshotDataBuffer.ResizeUninitialized(snapshotSize * GhostSystemConstants.SnapshotHistorySize);
                    var snapshotPtr = (byte*)snapshotDataBuffer.GetUnsafePtr();
                    UnsafeUtility.MemClear(snapshotPtr, snapshotSize * GhostSystemConstants.SnapshotHistorySize);
                    *(uint*)snapshotPtr = spawnTick.SerializedData;

                    helper.snapshotOffset = snapshotBaseOffset;
                    helper.snapshotPtr = snapshotPtr;
                    helper.snapshotSize = snapshotSize;
                    if (hasBuffers)
                    {
                        var dynamicDataCapacity= SnapshotDynamicBuffersHelper.CalculateBufferCapacity((uint)bufferSizes[i],
                            out var dynamicSnapshotSize);
                        var snapshotDynamicDataBuffer = snapshotDynamicDataBufferList[i];
                        var headerSize = SnapshotDynamicBuffersHelper.GetHeaderSize();
                        snapshotDynamicDataBuffer.ResizeUninitialized((int)dynamicDataCapacity);

                        helper.snapshotDynamicPtr = (byte*)snapshotDynamicDataBuffer.GetUnsafePtr();
                        helper.dynamicSnapshotDataOffset = (int)headerSize;
                        //add the header size so that the boundary check that into the consideration the header size
                        helper.dynamicSnapshotCapacity = (int)(dynamicSnapshotSize + headerSize);
                    }
                    helper.CopyEntityToSnapshot(chunk, i, typeData, GhostSerializeHelper.ClearOption.DontClear);

                    // Remove request component
                    commandBuffer.RemoveComponent<PredictedGhostSpawnRequest>(entity);
                    // Add to list of predictive spawn component - maybe use a singleton for this so spawn systems can just access it too
                    spawnList.Add(new PredictedGhostSpawn{entity = entity, ghostType = ghostType, spawnTick = spawnTick});
                }

                bufferSizes.Dispose();
            }
        }
        [BurstCompile]
        struct CleanupPredictedSpawn : IJob
        {
            public Entity spawnListEntity;
            public BufferLookup<PredictedGhostSpawn> spawnListFromEntity;
            public NativeReference<int> listHasData;
            public NetworkTick interpolatedTick;
            public EntityCommandBuffer commandBuffer;
            public void Execute()
            {
                var spawnList = spawnListFromEntity[spawnListEntity];
                for (int i = 0; i < spawnList.Length; ++i)
                {
                    var ghost = spawnList[i];
                    if (interpolatedTick.IsValid && interpolatedTick.IsNewerThan(ghost.spawnTick))
                    {
                        // Destroy entity and remove from list
                        commandBuffer.DestroyEntity(ghost.entity);
                        spawnList[i] = spawnList[spawnList.Length - 1];
                        spawnList.RemoveAt(spawnList.Length - 1);
                        --i;
                    }
                }
                listHasData.Value = spawnList.Length;
            }
        }
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var ent = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(ent, (FixedString64Bytes)"PredictedGhostSpawnList");
            state.EntityManager.AddComponentData(ent, default(PredictedGhostSpawnList));
            state.EntityManager.AddBuffer<PredictedGhostSpawn>(ent);
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PredictedGhostSpawnRequest, GhostType>()
                .WithAllRW<GhostInstance>();
            m_GhostInitQuery = state.GetEntityQuery(builder);

            m_ListHasData = new NativeReference<int>(Allocator.Persistent);
            m_PredictedGhostSpawnFromEntity = state.GetBufferLookup<PredictedGhostSpawn>();

            m_GhostComponentSerializerStateFromEntity = state.GetBufferLookup<GhostComponentSerializer.State>(true);
            m_GhostCollectionPrefabSerializerFromEntity = state.GetBufferLookup<GhostCollectionPrefabSerializer>(true);
            m_GhostCollectionComponentIndexFromEntity = state.GetBufferLookup<GhostCollectionComponentIndex>(true);
            m_GhostCollectionPrefabFromEntity = state.GetBufferLookup<GhostCollectionPrefab>(true);

            m_EntityTypeHandle = state.GetEntityTypeHandle();
            m_SnapshotDataHandle = state.GetComponentTypeHandle<SnapshotData>();
            m_SnapshotDataBufferHandle = state.GetBufferTypeHandle<SnapshotDataBuffer>();
            m_SnapshotDynamicDataBufferHandle = state.GetBufferTypeHandle<SnapshotDynamicDataBuffer>();
            m_LinkedEntityGroupHandle = state.GetBufferTypeHandle<LinkedEntityGroup>();

            m_GhostComponentFromEntity = state.GetComponentLookup<GhostInstance>();
            m_PredictedGhostFromEntity = state.GetComponentLookup<PredictedGhost>();
            m_GhostTypeComponentFromEntity = state.GetComponentLookup<GhostType>(true);

            state.RequireForUpdate<PredictedGhostSpawnList>();
        }
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            m_ListHasData.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            bool hasExisting = m_ListHasData.Value != 0;
            bool hasNew = !m_GhostInitQuery.IsEmptyIgnoreFilter;
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            if (!hasNew && !hasExisting)
            {
                m_SpawnTick = NetworkTimeHelper.LastFullServerTick(networkTime);
                return;
            }

            var spawnListEntity = SystemAPI.GetSingletonEntity<PredictedGhostSpawnList>();
            m_PredictedGhostSpawnFromEntity.Update(ref state);

            EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            if (hasNew)
            {
                m_GhostComponentSerializerStateFromEntity.Update(ref state);
                m_GhostCollectionPrefabSerializerFromEntity.Update(ref state);
                m_GhostCollectionComponentIndexFromEntity.Update(ref state);
                m_GhostCollectionPrefabFromEntity.Update(ref state);

                m_EntityTypeHandle.Update(ref state);
                m_SnapshotDataHandle.Update(ref state);
                m_SnapshotDataBufferHandle.Update(ref state);
                m_SnapshotDynamicDataBufferHandle.Update(ref state);
                m_LinkedEntityGroupHandle.Update(ref state);

                m_GhostComponentFromEntity.Update(ref state);
                m_PredictedGhostFromEntity.Update(ref state);
                m_GhostTypeComponentFromEntity.Update(ref state);
                m_ListHasData.Value = 1;
                var initJob = new InitGhostJob
                {
                    GhostCollectionSingleton = SystemAPI.GetSingletonEntity<GhostCollection>(),
                    GhostComponentCollectionFromEntity = m_GhostComponentSerializerStateFromEntity,
                    GhostTypeCollectionFromEntity = m_GhostCollectionPrefabSerializerFromEntity,
                    GhostComponentIndexFromEntity = m_GhostCollectionComponentIndexFromEntity,
                    GhostCollectionFromEntity = m_GhostCollectionPrefabFromEntity,

                    entityType = m_EntityTypeHandle,
                    snapshotDataType = m_SnapshotDataHandle,
                    snapshotDataBufferType = m_SnapshotDataBufferHandle,
                    snapshotDynamicDataBufferType = m_SnapshotDynamicDataBufferHandle,

                    spawnListFromEntity = m_PredictedGhostSpawnFromEntity,
                    spawnListEntity = spawnListEntity,

                    ghostFromEntity = m_GhostComponentFromEntity,
                    predictedGhostFromEntity = m_PredictedGhostFromEntity,
                    ghostTypeFromEntity = m_GhostTypeComponentFromEntity,

                    commandBuffer = commandBuffer,
                    spawnTick = m_SpawnTick,
                    linkedEntityGroupType = m_LinkedEntityGroupHandle,
                    childEntityLookup = state.GetEntityStorageInfoLookup()
                };
                var ghostComponentCollection = state.EntityManager.GetBuffer<GhostCollectionComponentType>(initJob.GhostCollectionSingleton);
                DynamicTypeList.PopulateList(ref state, ghostComponentCollection, true, ref initJob.DynamicTypeList);
                // Intentionally using non-parallel .ScheduleByRef()
                state.Dependency = initJob.ScheduleByRef(m_GhostInitQuery, state.Dependency);
            }

            if (hasExisting)
            {
                // Validate all ghosts in the list of predictive spawn ghosts and destroy the ones which are too old
                var cleanupJob = new CleanupPredictedSpawn
                {
                    spawnListEntity = spawnListEntity,
                    spawnListFromEntity = m_PredictedGhostSpawnFromEntity,
                    listHasData = m_ListHasData,
                    interpolatedTick = networkTime.InterpolationTick,
                    commandBuffer = commandBuffer
                };
                state.Dependency = cleanupJob.Schedule(state.Dependency);
            }
            m_SpawnTick = NetworkTimeHelper.LastFullServerTick(networkTime);
        }
    }
}
