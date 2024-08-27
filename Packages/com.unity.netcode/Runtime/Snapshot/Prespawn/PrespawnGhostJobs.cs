using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.NetCode.LowLevel.Unsafe;

namespace Unity.NetCode
{
    /// <summary>
    /// Build the ghost baseline for all the pre-spawned ghosts present in the world.
    /// The job will add to the entities a new buffer, PrespawGhostBaseline, witch will contains the
    /// a pre-serialized snapshot of the entity at the time the job run.
    ///
    /// NOTE: The serialization does not depend on component stripping (it is only dependent on the ghost type archetype
    /// serializer /omponent that is guarantee to be same on both client and server and that is handled by the GhostCollectionSystem)
    /// </summary>
    // baseline snapshot data layout:
    // -------------------------------------------------------------
    // [COMPONENT DATA][SIZE][PADDING (3UINT)][DYNAMIC BUFFER DATA]
    // -------------------------------------------------------------
    [BurstCompile]
    internal struct PrespawnGhostSerializer : IJobChunk
    {
        [ReadOnly] public BufferLookup<GhostComponentSerializer.State> GhostComponentCollectionFromEntity;
        [ReadOnly] public BufferLookup<GhostCollectionPrefabSerializer> GhostTypeCollectionFromEntity;
        [ReadOnly] public BufferLookup<GhostCollectionComponentIndex> GhostComponentIndexFromEntity;
        [ReadOnly] public BufferLookup<GhostCollectionPrefab> GhostCollectionFromEntity;
        [ReadOnly] public ComponentTypeHandle<GhostType> ghostTypeComponentType;
        [ReadOnly] public EntityTypeHandle entityType;
        [ReadOnly] public EntityStorageInfoLookup childEntityLookup;
        [ReadOnly] public BufferTypeHandle<LinkedEntityGroup> linkedEntityGroupType;
        [ReadOnly] public ComponentLookup<GhostInstance> ghostFromEntity;
        [ReadOnly] public DynamicTypeList ghostChunkComponentTypes;
        public NativeList<ulong>.ParallelWriter baselineHashes;
        [NativeDisableParallelForRestriction]
        public BufferTypeHandle<PrespawnGhostBaseline> prespawnBaseline;
        public Entity GhostCollectionSingleton;

        public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            // This job is not written to support queries with enableable component types.
            Assert.IsFalse(useEnabledMask);

            var entities = chunk.GetNativeArray(entityType);
            var GhostCollection = GhostCollectionFromEntity[GhostCollectionSingleton];
            var GhostTypeCollection = GhostTypeCollectionFromEntity[GhostCollectionSingleton];
            var ghostTypeComponent = chunk.GetNativeArray(ref ghostTypeComponentType)[0];
            int ghostType;
            for (ghostType = 0; ghostType < GhostCollection.Length; ++ghostType)
            {
                if (GhostCollection[ghostType].GhostType == ghostTypeComponent)
                    break;
            }
            //the type has not been processed yet. There isn't much we can do about it
            if (ghostType >= GhostCollection.Length || ghostType >= GhostTypeCollection.Length)
            {
                UnityEngine.Debug.LogError($"Cannot serialize prespawn ghost baselines as the `GhostCollection` didn't correctly process some prefabs. GhostTypeCollection.Length: {GhostTypeCollection.Length}.");
                return;
            }

            var buffersSize = new NativeArray<int>(chunk.Count, Allocator.Temp);
            var ghostChunkComponentTypesPtr = ghostChunkComponentTypes.GetData();
            var helper = new GhostSerializeHelper
            {
                serializerState = new GhostSerializerState { GhostFromEntity = ghostFromEntity },
                ghostChunkComponentTypesPtr = ghostChunkComponentTypesPtr,
                GhostComponentIndex = GhostComponentIndexFromEntity[GhostCollectionSingleton],
                GhostComponentCollection = GhostComponentCollectionFromEntity[GhostCollectionSingleton],
                childEntityLookup = childEntityLookup,
                linkedEntityGroupType = linkedEntityGroupType,
                ghostChunkComponentTypesPtrLen = ghostChunkComponentTypes.Length
            };

            var typeData = GhostTypeCollection[ghostType];
            //collect the buffers size for each entity (and children)
            if (GhostTypeCollection[ghostType].NumBuffers > 0)
                helper.GatherBufferSize(chunk, 0, typeData, ref buffersSize);

            var snapshotSize = typeData.SnapshotSize;
            int changeMaskUints = GhostComponentSerializer.ChangeMaskArraySizeInUInts(typeData.ChangeMaskBits);
            int enableableMaskUints = GhostComponentSerializer.ChangeMaskArraySizeInUInts(typeData.EnableableBits);
            var snapshotBaseOffset = GhostComponentSerializer.SnapshotSizeAligned(sizeof(uint) + changeMaskUints*sizeof(uint) + enableableMaskUints*sizeof(uint));

            var bufferAccessor = chunk.GetBufferAccessor(ref prespawnBaseline);
            var chunkHashes = stackalloc ulong[entities.Length];
            for (int i = 0; i < entities.Length; ++i)
            {
                //Initialize the baseline buffer. This will contains the component data
                var baselineBuffer = bufferAccessor[i];
                //The first 4 bytes are the size of the dynamic data
                var dynamicDataCapacity = GhostComponentSerializer.SnapshotSizeAligned(sizeof(uint)) + buffersSize[i];
                baselineBuffer.ResizeUninitialized(snapshotSize + dynamicDataCapacity);
                var baselinePtr = baselineBuffer.GetUnsafePtr();
                UnsafeUtility.MemClear(baselinePtr, baselineBuffer.Length);

                helper.changeMaskUints = changeMaskUints;
                helper.snapshotOffset = snapshotBaseOffset;
                helper.snapshotPtr = (byte*) baselinePtr;
                helper.snapshotDynamicPtr = (byte*) baselinePtr + snapshotSize;
                helper.dynamicSnapshotDataOffset = GhostComponentSerializer.SnapshotSizeAligned(sizeof(uint));
                helper.snapshotSize = snapshotSize;
                helper.dynamicSnapshotCapacity = baselineBuffer.Length - snapshotSize;
                helper.CopyEntityToSnapshot(chunk, i, typeData, GhostSerializeHelper.ClearOption.DontClear);

                // Compute the hash for that baseline
                chunkHashes[i] =
                    Unity.Core.XXHash.Hash64((byte*)baselineBuffer.GetUnsafeReadOnlyPtr(), baselineBuffer.Length);
            }
            baselineHashes.AddRangeNoResize(chunkHashes, entities.Length);

            buffersSize.Dispose();
        }
    }

    /// <summary>
    ///  Strip from the prespawned ghost instances all the runtime components marked to be removed or disabled
    /// </summary>
    /// <remarks>
    /// This job is not burst compatbile since it uses TypeManager internal static members, that aren't SharedStatic.
    /// </remarks>
    [BurstCompile]
    internal struct PrespawnGhostStripComponentsJob : IJobChunk
    {
        [ReadOnly]public ComponentTypeHandle<GhostType> ghostTypeHandle;
        [ReadOnly]public ComponentLookup<GhostPrefabMetaData> metaDataFromEntity;
        [ReadOnly]public BufferTypeHandle<LinkedEntityGroup> linkedEntityTypeHandle;
        [ReadOnly]public NativeParallelHashMap<GhostType, Entity> prefabFromType;
        public EntityCommandBuffer.ParallelWriter commandBuffer;
        public NetDebug netDebug;
        public byte server;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            // This job is not written to support queries with enableable component types.
            Assert.IsFalse(useEnabledMask);

            var ghostTypes = chunk.GetNativeArray(ref ghostTypeHandle);
            if (!prefabFromType.TryGetValue(ghostTypes[0], out var ghostPrefabEntity))
            {
                netDebug.LogError("Failed to look up ghost type");
                return;
            }
            // Modfy the entity to its proper version
            if (!metaDataFromEntity.HasComponent(ghostPrefabEntity))
            {
                netDebug.LogWarning($"Could not find a valid ghost prefab for the ghostType");
                return;
            }

            ref var ghostMetaData = ref metaDataFromEntity[ghostPrefabEntity].Value.Value;
            var linkedEntityBufferAccessor = chunk.GetBufferAccessor(ref linkedEntityTypeHandle);

            for (int index = 0, chunkEntityCount = chunk.Count; index < chunkEntityCount; ++index)
            {
                var linkedEntityGroup = linkedEntityBufferAccessor[index];
                if (server == 1)
                {
                    for (int rm = 0; rm < ghostMetaData.RemoveOnServer.Length; ++rm)
                    {
                        var childIndexCompHashPair = ghostMetaData.RemoveOnServer[rm];
                        var rmCompType = ComponentType.ReadWrite(TypeManager.GetTypeIndexFromStableTypeHash(childIndexCompHashPair.StableHash));
                        commandBuffer.RemoveComponent(unfilteredChunkIndex, linkedEntityGroup[childIndexCompHashPair.EntityIndex].Value, rmCompType);
                    }
                }
                else
                {
                    for (int rm = 0; rm < ghostMetaData.RemoveOnClient.Length; ++rm)
                    {
                        var childIndexCompHashPair = ghostMetaData.RemoveOnClient[rm];
                        var rmCompType = ComponentType.ReadWrite(TypeManager.GetTypeIndexFromStableTypeHash(childIndexCompHashPair.StableHash));
                        commandBuffer.RemoveComponent(unfilteredChunkIndex,linkedEntityGroup[childIndexCompHashPair.EntityIndex].Value, rmCompType);
                    }
                    // FIXME: should disable instead of removing once we have a way of doing that without structural changes
                    if (ghostMetaData.DefaultMode == GhostPrefabBlobMetaData.GhostMode.Predicted)
                    {
                        for (int rm = 0; rm < ghostMetaData.DisableOnPredictedClient.Length; ++rm)
                        {
                            var childIndexCompHashPair = ghostMetaData.DisableOnPredictedClient[rm];
                            var rmCompType = ComponentType.ReadWrite(TypeManager.GetTypeIndexFromStableTypeHash(childIndexCompHashPair.StableHash));
                            commandBuffer.RemoveComponent(unfilteredChunkIndex,linkedEntityGroup[childIndexCompHashPair.EntityIndex].Value, rmCompType);
                        }
                    }
                    else if (ghostMetaData.DefaultMode == GhostPrefabBlobMetaData.GhostMode.Interpolated)
                    {
                        for (int rm = 0; rm < ghostMetaData.DisableOnInterpolatedClient.Length; ++rm)
                        {
                            var childIndexCompHashPair = ghostMetaData.DisableOnInterpolatedClient[rm];
                            var rmCompType = ComponentType.ReadWrite(TypeManager.GetTypeIndexFromStableTypeHash(childIndexCompHashPair.StableHash));
                            commandBuffer.RemoveComponent(unfilteredChunkIndex,linkedEntityGroup[childIndexCompHashPair.EntityIndex].Value, rmCompType);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Assign to GhostComponent and GhostStateSystemComponent the ghost ids for all the prespawn ghosts.
    /// Also responsible to populate the SpawnedGhostMapping lists with all the spawned ghosts
    /// </summary>
    [BurstCompile]
    internal struct AssignPrespawnGhostIdJob : IJobChunk
    {
        [ReadOnly] public EntityTypeHandle entityType;
        [ReadOnly] public ComponentTypeHandle<PreSpawnedGhostIndex> prespawnIndexType;
        [NativeDisableParallelForRestriction]
        public ComponentTypeHandle<GhostInstance> ghostComponentType;
        [NativeDisableParallelForRestriction]
        public ComponentTypeHandle<GhostCleanup> ghostStateTypeHandle;
        [NativeDisableParallelForRestriction]
        public NativeList<SpawnedGhostMapping>.ParallelWriter spawnedGhosts;
        public int startGhostId;
        public NetDebug netDebug;

        public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            // This job is not written to support queries with enableable component types.
            Assert.IsFalse(useEnabledMask);

            var entities = chunk.GetNativeArray(entityType);
            var preSpawnedIndices = chunk.GetNativeArray(ref prespawnIndexType);
            var ghostComponents = chunk.GetNativeArray(ref ghostComponentType);
            var ghostStates = chunk.GetNativeArray(ref ghostStateTypeHandle);

            var chunkSpawnedGhostMappings = stackalloc SpawnedGhostMapping[chunk.Count];
            int spawnedGhostCount = 0;
            for (int index = 0, chunkEntityCount = chunk.Count; index < chunkEntityCount; ++index)
            {
                var entity = entities[index];
                // Check if this entity has already been handled
                if (ghostComponents[index].ghostId != 0)
                {
                    netDebug.LogWarning($"{entity} already has ghostId={ghostComponents[index].ghostId} PreSpawnedGhostIndex={preSpawnedIndices[index].Value}");
                    continue;
                }
                //Special encoding for prespawn index (sort of "namespace").
                var ghostId = PrespawnHelper.MakePrespawnGhostId(preSpawnedIndices[index].Value + startGhostId);
                if (ghostStates.IsCreated && ghostStates.Length > 0)
                    ghostStates[index] = new GhostCleanup {ghostId = ghostId, despawnTick = NetworkTick.Invalid, spawnTick = NetworkTick.Invalid};

                chunkSpawnedGhostMappings[spawnedGhostCount++] = new SpawnedGhostMapping
                {
                    ghost = new SpawnedGhost {ghostId = ghostId, spawnTick = NetworkTick.Invalid}, entity = entity
                };
                // GhostType -1 is a special case for prespawned ghosts which is converted to a proper ghost id in the send / receive systems
                // once the ghost ids are known
                // Pre-spawned uses spawnTick = 0, if there is a reference to a ghost and it has spawnTick 0 the ref is always resolved
                // This works because there despawns are high priority and we never create pre-spawned ghosts after connection
                ghostComponents[index] = new GhostInstance {ghostId = ghostId, ghostType = -1, spawnTick = NetworkTick.Invalid};
            }
            spawnedGhosts.AddRangeNoResize(chunkSpawnedGhostMappings, spawnedGhostCount);
        }
    }
}
