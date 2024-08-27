using Unity.Assertions;
using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.NetCode.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.NetCode
{
#if UNITY_EDITOR || NETCODE_DEBUG
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(GhostPredictionSmoothingSystem))]
    [UpdateBefore(typeof(GhostPredictionHistorySystem))]
    [BurstCompile]
    public unsafe partial struct GhostPredictionDebugSystem : ISystem
    {
        NativeList<float> m_PredictionErrors;

        EntityQuery m_PredictionQuery;

        ComponentTypeHandle<GhostInstance> m_GhostComponentHandle;
        ComponentTypeHandle<PredictedGhost> m_PredictedGhostHandle;
        BufferTypeHandle<LinkedEntityGroup> m_LinkedEntityGroupHandle;
        EntityTypeHandle m_EntityTypeHandle;
        BufferLookup<GhostComponentSerializer.State> m_GhostComponentSerializerStateFromEntity;
        BufferLookup<GhostCollectionPrefabSerializer> m_GhostCollectionPrefabSerializerFromEntity;
        BufferLookup<GhostCollectionComponentIndex> m_GhostCollectionComponentIndexFromEntity;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PredictedGhost, GhostInstance>();
            m_PredictionQuery = state.GetEntityQuery(builder);
            m_PredictionErrors = new NativeList<float>(128, Allocator.Persistent);

            m_GhostComponentHandle = state.GetComponentTypeHandle<GhostInstance>(true);
            m_PredictedGhostHandle = state.GetComponentTypeHandle<PredictedGhost>(true);
            m_LinkedEntityGroupHandle = state.GetBufferTypeHandle<LinkedEntityGroup>(true);
            m_EntityTypeHandle = state.GetEntityTypeHandle();

            m_GhostComponentSerializerStateFromEntity = state.GetBufferLookup<GhostComponentSerializer.State>(true);
            m_GhostCollectionPrefabSerializerFromEntity = state.GetBufferLookup<GhostCollectionPrefabSerializer>(true);
            m_GhostCollectionComponentIndexFromEntity = state.GetBufferLookup<GhostCollectionComponentIndex>(true);

            state.RequireForUpdate<GhostCollection>();
        }
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            m_PredictionErrors.Dispose();
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var lastBackupTime = SystemAPI.GetSingleton<GhostSnapshotLastBackupTick>();
            if (networkTime.ServerTick != lastBackupTime.Value || !SystemAPI.GetSingleton<GhostStats>().IsConnected)
                return;

#if UNITY_2022_2_14F1_OR_NEWER
            int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
            int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
            var predictionErrorCount = SystemAPI.GetSingleton<GhostCollection>().NumPredictionErrors;
            if (m_PredictionErrors.Length != predictionErrorCount * maxThreadCount)
            {
                m_PredictionErrors.Resize(predictionErrorCount * maxThreadCount, NativeArrayOptions.ClearMemory);
            }

            m_GhostComponentHandle.Update(ref state);
            m_PredictedGhostHandle.Update(ref state);
            m_LinkedEntityGroupHandle.Update(ref state);
            m_EntityTypeHandle.Update(ref state);

            m_GhostComponentSerializerStateFromEntity.Update(ref state);
            m_GhostCollectionPrefabSerializerFromEntity.Update(ref state);
            m_GhostCollectionComponentIndexFromEntity.Update(ref state);

            var GhostCollectionSingleton = SystemAPI.GetSingletonEntity<GhostCollection>();
            var debugJob = new PredictionDebugJob
            {
                predictionState = SystemAPI.GetSingleton<GhostPredictionHistoryState>().PredictionState,
                ghostType = m_GhostComponentHandle,
                predictedGhostType = m_PredictedGhostHandle,
                entityType = m_EntityTypeHandle,

                GhostCollectionSingleton = GhostCollectionSingleton,
                GhostComponentCollectionFromEntity = m_GhostComponentSerializerStateFromEntity,
                GhostTypeCollectionFromEntity = m_GhostCollectionPrefabSerializerFromEntity,
                GhostComponentIndexFromEntity = m_GhostCollectionComponentIndexFromEntity,

                childEntityLookup = state.GetEntityStorageInfoLookup(),
                linkedEntityGroupType = m_LinkedEntityGroupHandle,
                tick = networkTime.ServerTick,
                transformType = ComponentType.ReadWrite<LocalTransform>(),

                predictionErrors = m_PredictionErrors.AsArray(),
                numPredictionErrors = predictionErrorCount
            };

            var ghostComponentCollection = state.EntityManager.GetBuffer<GhostCollectionComponentType>(GhostCollectionSingleton);
            DynamicTypeList.PopulateList(ref state, ghostComponentCollection, true, ref debugJob.DynamicTypeList);
            state.Dependency = debugJob.ScheduleParallelByRef(m_PredictionQuery, state.Dependency);

            ref readonly var predictionErrorStats = ref SystemAPI.GetSingletonRW<GhostStatsCollectionPredictionError>().ValueRO;
            // Resize job
            var resizeJob = new PredictionErrorsOutpuResize
            {
                PredictionErrorsOutput = predictionErrorStats.Data,
                predictionErrorCount = predictionErrorCount,
            };
            state.Dependency = resizeJob.Schedule(state.Dependency);

            var combineJob = new CombinePredictionErrors
            {
                PredictionErrorsOutput = predictionErrorStats.Data,
                PredictionErrors = m_PredictionErrors.AsArray(),
                predictionErrorCount = predictionErrorCount,

            };
            state.Dependency = combineJob.Schedule(predictionErrorCount, 64, state.Dependency);
        }
        [BurstCompile]
        struct PredictionErrorsOutpuResize : IJob
        {
            public NativeList<float> PredictionErrorsOutput;
            public int predictionErrorCount;
            public void Execute()
            {
                PredictionErrorsOutput.ResizeUninitialized(predictionErrorCount);
            }
        }
        [BurstCompile]
        struct CombinePredictionErrors : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeList<float> PredictionErrorsOutput;
            [NativeDisableParallelForRestriction] public NativeArray<float> PredictionErrors;
            public int predictionErrorCount;
            public void Execute(int i)
            {
#if UNITY_2022_2_14F1_OR_NEWER
                int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
                int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
                for (int job = 1; job < maxThreadCount; ++job)
                {
                    PredictionErrors[i] = math.max(PredictionErrors[i], PredictionErrors[predictionErrorCount*job + i]);
                    PredictionErrors[predictionErrorCount*job + i] = 0;
                }
                PredictionErrorsOutput[i] = PredictionErrors[i];
                PredictionErrors[i] = 0;
            }
        }
        [BurstCompile]
        struct PredictionDebugJob : IJobChunk
        {
            public DynamicTypeList DynamicTypeList;
            public NativeParallelHashMap<ArchetypeChunk, System.IntPtr>.ReadOnly predictionState;

            [ReadOnly] public ComponentTypeHandle<GhostInstance> ghostType;
            [ReadOnly] public ComponentTypeHandle<PredictedGhost> predictedGhostType;
            [ReadOnly] public EntityTypeHandle entityType;

            public Entity GhostCollectionSingleton;
            [ReadOnly] public BufferLookup<GhostComponentSerializer.State> GhostComponentCollectionFromEntity;
            [ReadOnly] public BufferLookup<GhostCollectionPrefabSerializer> GhostTypeCollectionFromEntity;
            [ReadOnly] public BufferLookup<GhostCollectionComponentIndex> GhostComponentIndexFromEntity;

            [ReadOnly] public EntityStorageInfoLookup childEntityLookup;
            [ReadOnly] public BufferTypeHandle<LinkedEntityGroup> linkedEntityGroupType;

            public NetworkTick tick;
            // FIXME: placeholder to show the idea behind prediction smoothing
            public ComponentType transformType;

            const GhostSendType requiredSendMask = GhostSendType.OnlyPredictedClients;

    #pragma warning disable 649
            [NativeSetThreadIndex] public int ThreadIndex;
    #pragma warning restore 649
            [NativeDisableParallelForRestriction] public NativeArray<float> predictionErrors;
            public int numPredictionErrors;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // This job is not written to support queries with enableable component types.
                Assert.IsFalse(useEnabledMask);

                if (!predictionState.TryGetValue(chunk, out var state) ||
                    (*(PredictionBackupState*)state).entityCapacity != chunk.Capacity)
                    return;

                DynamicComponentTypeHandle* ghostChunkComponentTypesPtr = DynamicTypeList.GetData();
                int ghostChunkComponentTypesLength = DynamicTypeList.Length;

                var GhostTypeCollection = GhostTypeCollectionFromEntity[GhostCollectionSingleton];
                var GhostComponentIndex = GhostComponentIndexFromEntity[GhostCollectionSingleton];
                var GhostComponentCollection = GhostComponentCollectionFromEntity[GhostCollectionSingleton];

                var ghostComponents = chunk.GetNativeArray(ref ghostType);
                int ghostTypeId = ghostComponents.GetFirstGhostTypeId();
                if (ghostTypeId < 0)
                    return;
                if (ghostTypeId >= GhostTypeCollection.Length)
                    return; // serialization data has not been loaded yet. This can only happen for prespawn objects
                var typeData = GhostTypeCollection[ghostTypeId];
                int baseOffset = typeData.FirstComponent;

                Entity* backupEntities = PredictionBackupState.GetEntities(state);
                var entities = chunk.GetNativeArray(entityType);

                var PredictedGhosts = chunk.GetNativeArray(ref predictedGhostType);

                byte* dataPtr = PredictionBackupState.GetData(state);
                int numBaseComponents = typeData.NumComponents - typeData.NumChildComponents;
                for (int comp = 0; comp < numBaseComponents; ++comp)
                {
                    int compIdx = GhostComponentIndex[baseOffset + comp].ComponentIndex;
                    int serializerIdx = GhostComponentIndex[baseOffset + comp].SerializerIndex;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    if (compIdx >= ghostChunkComponentTypesLength)
                        throw new System.InvalidOperationException("Component index out of range");
#endif
                    if ((GhostComponentIndex[baseOffset + comp].SendMask&requiredSendMask) == 0)
                        continue;

                    ref readonly var ghostSerializer = ref GhostComponentCollection.ElementAtRO(serializerIdx);
                    var compSize = ghostSerializer.ComponentType.IsBuffer
                        ? GhostComponentSerializer.DynamicBufferComponentSnapshotSize
                        : ghostSerializer.ComponentSize;
                    if (chunk.Has(ref ghostChunkComponentTypesPtr[compIdx]) && ghostSerializer.HasGhostFields)
                    {
                        if (!ghostSerializer.ComponentType.IsBuffer)
                        {
                            var compData = (byte*)chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref ghostChunkComponentTypesPtr[compIdx], compSize).GetUnsafeReadOnlyPtr();
                            for (int ent = 0; ent < entities.Length; ++ent)
                            {
                                // If this entity did not predict anything there was no rollback and no need to debug it
                                if (!PredictedGhosts[ent].ShouldPredict(tick))
                                    continue;
                                if (entities[ent] == backupEntities[ent])
                                {
                                    int errorIndex = GhostComponentIndex[baseOffset + comp].PredictionErrorBaseIndex;

                                    float* errorsPtr = ((float*)predictionErrors.GetUnsafePtr()) + errorIndex + ThreadIndex * numPredictionErrors;
                                    int errorsLength = numPredictionErrors - errorIndex;

                                    ghostSerializer.ReportPredictionErrors.Invoke((System.IntPtr)(compData + compSize * ent), (System.IntPtr)(dataPtr + compSize * ent), (System.IntPtr)(errorsPtr), errorsLength);
                                }
                            }
                        }
                        else
                        {
                            //FIXME Buffers need to report error for the size and an aggregate for each element in the buffer
                        }
                    }

                    dataPtr = PredictionBackupState.GetNextData(dataPtr, compSize, chunk.Capacity);
                }
                if (typeData.NumChildComponents > 0)
                {
                    var linkedEntityGroupAccessor = chunk.GetBufferAccessor(ref linkedEntityGroupType);
                    for (int comp = numBaseComponents; comp < typeData.NumComponents; ++comp)
                    {
                        int compIdx = GhostComponentIndex[baseOffset + comp].ComponentIndex;
                        int serializerIdx = GhostComponentIndex[baseOffset + comp].SerializerIndex;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        if (compIdx >= ghostChunkComponentTypesLength)
                            throw new System.InvalidOperationException("Component index out of range");
#endif
                        if ((GhostComponentIndex[baseOffset + comp].SendMask&requiredSendMask) == 0)
                            continue;

                        ref readonly var ghostSerializer = ref GhostComponentCollection.ElementAtRO(serializerIdx);
                        var compSize = ghostSerializer.ComponentType.IsBuffer
                            ? GhostComponentSerializer.DynamicBufferComponentSnapshotSize
                            : ghostSerializer.ComponentSize;

                        if (ghostSerializer.HasGhostFields)
                        {
                            var entityIdx = GhostComponentIndex[baseOffset + comp].EntityIndex;
                            for (int ent = 0, chunkEntityCount = chunk.Count; ent < chunkEntityCount; ++ent)
                            {
                                // If this entity did not predict anything there was no rollback and no need to debug it
                                if (!PredictedGhosts[ent].ShouldPredict(tick))
                                    continue;
                                if (entities[ent] != backupEntities[ent])
                                    continue;
                                var linkedEntityGroup = linkedEntityGroupAccessor[ent];
                                var childEnt = linkedEntityGroup[entityIdx].Value;
                                if (childEntityLookup.TryGetValue(childEnt, out var childChunk) && childChunk.Chunk.Has(ref ghostChunkComponentTypesPtr[compIdx]))
                                {
                                    if (!ghostSerializer.ComponentType.IsBuffer)
                                    {
                                        var compData = (byte*) childChunk.Chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref ghostChunkComponentTypesPtr[compIdx], compSize).GetUnsafeReadOnlyPtr();
                                        int errorIndex = GhostComponentIndex[baseOffset + comp].PredictionErrorBaseIndex;

                                        float* errorsPtr = ((float*) predictionErrors.GetUnsafePtr()) + errorIndex + ThreadIndex * numPredictionErrors;
                                        int errorsLength = numPredictionErrors - errorIndex;

                                        ghostSerializer.ReportPredictionErrors.Invoke((System.IntPtr)(compData + compSize * childChunk.IndexInChunk), (System.IntPtr)(dataPtr + compSize * ent), (System.IntPtr)(errorsPtr), errorsLength);
                                    }
                                    else
                                    {
                                        //FIXME Buffers need to report error for the size and an aggregate for each element in the buffer
                                    }
                                }
                            }
                        }

                        dataPtr = PredictionBackupState.GetNextData(dataPtr, compSize, chunk.Capacity);
                    }
                }
            }
        }
    }
#endif
}
