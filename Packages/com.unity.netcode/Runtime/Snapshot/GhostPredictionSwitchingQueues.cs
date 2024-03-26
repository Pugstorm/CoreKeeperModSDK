using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Profiling;
using Unity.Transforms;

namespace Unity.NetCode
{
    /// <summary>
    /// Singleton component with APIs and collections required for converting client ghosts <see cref="GhostMode"/> to <see cref="GhostMode.Predicted"/> &amp; <see cref="GhostMode.Interpolated"/>.
    /// <see cref="GhostPredictionSwitchingSystem"/>
    /// </summary>
    public struct GhostPredictionSwitchingQueues : IComponentData
    {
        /// <summary><see cref="GhostPredictionSwitchingSystem.ConvertGhostToPredicted"/></summary>
        public NativeQueue<ConvertPredictionEntry>.ParallelWriter ConvertToPredictedQueue;
        /// <summary><see cref="GhostPredictionSwitchingSystem.ConvertGhostToInterpolated"/></summary>
        public NativeQueue<ConvertPredictionEntry>.ParallelWriter ConvertToInterpolatedQueue;
    }

    /// <summary>Struct storing settings for an individual queue entry in the <see cref="GhostPredictionSwitchingQueues"/>.</summary>
    [NoAlias]
    public struct ConvertPredictionEntry
    {
        /// <summary>The entity you are converting.</summary>
        public Entity TargetEntity;

        /// <summary>
        /// We smooth the <see cref="LocalToWorld"/> of the target entity via <see cref="GhostPredictionSmoothing"/> system (and component <see cref="SwitchPredictionSmoothing"/>).
        /// How gentle should this smooth transformation be? Sensible default: 1.0s.
        /// Note: Also prevents converting the ghost again until complete.
        /// </summary>
        public float TransitionDurationSeconds;
    }

    /// <summary>System that applies the prediction switching on the queued entities (via <see cref="GhostPredictionSwitchingQueues"/>).</summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostSpawnSystemGroup))]
    [UpdateAfter(typeof(GhostSpawnSystem))]
    public partial struct GhostPredictionSwitchingSystem : ISystem
    {
        NativeQueue<ConvertPredictionEntry> m_ConvertToInterpolatedQueue;
        NativeQueue<ConvertPredictionEntry> m_ConvertToPredictedQueue;

        public void OnCreate(ref SystemState state)
        {
            m_ConvertToInterpolatedQueue = new NativeQueue<ConvertPredictionEntry>(Allocator.Persistent);
            m_ConvertToPredictedQueue = new NativeQueue<ConvertPredictionEntry>(Allocator.Persistent);

            var singletonEntity = state.EntityManager.CreateEntity(ComponentType.ReadOnly<GhostPredictionSwitchingQueues>());
            state.EntityManager.SetName(singletonEntity, (FixedString64Bytes)"GhostPredictionQueues");
            SystemAPI.SetSingleton(new GhostPredictionSwitchingQueues
            {
                ConvertToInterpolatedQueue = m_ConvertToInterpolatedQueue.AsParallelWriter(),
                ConvertToPredictedQueue = m_ConvertToPredictedQueue.AsParallelWriter(),
            });
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            m_ConvertToPredictedQueue.Dispose();
            m_ConvertToInterpolatedQueue.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (m_ConvertToPredictedQueue.Count + m_ConvertToInterpolatedQueue.Count > 0)
            {
                var netDebug = SystemAPI.GetSingleton<NetDebug>();
                var ghostUpdateVersion = SystemAPI.GetSingleton<GhostUpdateVersion>();
                var prefabs = SystemAPI.GetSingletonBuffer<GhostCollectionPrefab>().ToNativeArray(Allocator.Temp);

                FixedList64Bytes<Entity> batchedDeletedWarnings = default;
                uint batchedDeletedCount = 0;
                while (m_ConvertToPredictedQueue.TryDequeue(out var conversion))
                {
                    ConvertGhostToPredicted(state.EntityManager, ghostUpdateVersion, netDebug, prefabs, conversion.TargetEntity, conversion.TransitionDurationSeconds, ref batchedDeletedWarnings, ref batchedDeletedCount);
                }

                while (m_ConvertToInterpolatedQueue.TryDequeue(out var conversion))
                {
                    ConvertGhostToInterpolated(state.EntityManager, ghostUpdateVersion, netDebug, prefabs, conversion.TargetEntity, conversion.TransitionDurationSeconds, ref batchedDeletedWarnings, ref batchedDeletedCount);
                }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (batchedDeletedWarnings.Length > 0)
                {
                    FixedString512Bytes batchedWarning = $"Failed to 'switch prediction' on {batchedDeletedCount} entities as they don't exist! Likely destroyed after added to the queue. Subset of destroyed entities:[";
                    foreach (var entity in batchedDeletedWarnings)
                    {
                        batchedWarning.Append(entity.ToFixedString());
                        batchedWarning.Append(',');
                    }
                    if (batchedDeletedWarnings.Length == batchedWarning.Capacity)
                        batchedWarning.Append((FixedString32Bytes)"etc");
                    batchedWarning.Append((FixedString32Bytes)"].");
                    netDebug.DebugLog(batchedWarning);
                }
#endif
            }
        }

        /// <summary>
        /// Convert an interpolated ghost to a predicted ghost. The ghost must support both interpolated and predicted mode,
        /// and it cannot be owner predicted. The new components added as a result of this operation will have the inital
        /// values from the ghost prefab.
        /// </summary>
        static bool ConvertGhostToPredicted(EntityManager entityManager, GhostUpdateVersion ghostUpdateVersion, NetDebug netDbg, NativeArray<GhostCollectionPrefab> ghostCollectionPrefabs, Entity entity, float transitionDuration, ref FixedList64Bytes<Entity> destroyedEntities, ref uint batchedDeletedCount)
        {
            if (!entityManager.Exists(entity))
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if(destroyedEntities.Length < destroyedEntities.Capacity)
                    destroyedEntities.Add(entity);
                batchedDeletedCount++;
#endif
                return false;
            }
            if (!entityManager.HasComponent<GhostInstance>(entity))
            {
                netDbg.LogError($"Trying to convert a ghost to predicted, but this is not a ghost entity! {entity.ToFixedString()}");
                return false;
            }
            if (entityManager.HasComponent<Prefab>(entity))
            {
                netDbg.LogError($"Trying to convert a ghost to predicted, but this is a prefab! {entity.ToFixedString()}");
                return false;
            }
            if (entityManager.HasComponent<PredictedGhost>(entity))
            {
                netDbg.LogWarning($"Trying to convert a ghost to predicted, but it is already predicted! {entity.ToFixedString()}");
                return false;
            }
            var ghost = entityManager.GetComponentData<GhostInstance>(entity);
            var prefab = ghostCollectionPrefabs[ghost.ghostType].GhostPrefab;
            if (!entityManager.HasComponent<GhostPrefabMetaData>(prefab))
            {
                netDbg.LogWarning($"Trying to convert a ghost to predicted, but did not find a prefab with meta data! {entity.ToFixedString()}");
                return false;
            }
            ref var ghostMetaData = ref entityManager.GetComponentData<GhostPrefabMetaData>(prefab).Value.Value;
            if (ghostMetaData.SupportedModes != GhostPrefabBlobMetaData.GhostMode.Both)
            {
                netDbg.LogWarning($"Trying to convert a ghost to predicted, but it does not support both modes! {entity.ToFixedString()}");
                return false;
            }
            if (ghostMetaData.DefaultMode == GhostPrefabBlobMetaData.GhostMode.Both)
            {
                netDbg.LogWarning($"Trying to convert a ghost to predicted, but it is owner predicted and owner predicted ghosts cannot be switched on demand! {entity.ToFixedString()}");
                return false;
            }

            ref var toAdd = ref ghostMetaData.DisableOnInterpolatedClient;
            ref var toRemove = ref ghostMetaData.DisableOnPredictedClient;
            return AddRemoveComponents(entityManager, ref ghostUpdateVersion, entity, prefab, ref toAdd, ref toRemove, transitionDuration);
        }

        /// <summary>
        /// Convert a predicted ghost to an interpolated ghost. The ghost must support both interpolated and predicted mode,
        /// and it cannot be owner predicted. The new components added as a result of this operation will have the inital
        /// values from the ghost prefab.
        /// </summary>
        static bool ConvertGhostToInterpolated(EntityManager entityManager, GhostUpdateVersion ghostUpdateVersion, NetDebug netDbg, NativeArray<GhostCollectionPrefab> ghostCollectionPrefabs, Entity entity, float transitionDuration, ref FixedList64Bytes<Entity> destroyedEntities, ref uint batchedDeletedCount)
        {
            if (!entityManager.Exists(entity))
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if(destroyedEntities.Length < destroyedEntities.Capacity)
                    destroyedEntities.Add(entity);
                batchedDeletedCount++;
#endif
                return false;
            }
            if (!entityManager.HasComponent<GhostInstance>(entity))
            {
                netDbg.LogError($"Trying to convert a ghost to interpolated, but this is not a ghost entity! {entity.ToFixedString()}");
                return false;
            }
            if (entityManager.HasComponent<Prefab>(entity))
            {
                netDbg.LogError($"Trying to convert a ghost to interpolated, but this is a prefab! {entity.ToFixedString()}");
                return false;
            }
            if (!entityManager.HasComponent<PredictedGhost>(entity))
            {
                netDbg.LogWarning($"Trying to convert a ghost to interpolated, but it is already interpolated! {entity.ToFixedString()}");
                return false;
            }

            var ghost = entityManager.GetComponentData<GhostInstance>(entity);
            var prefab = ghostCollectionPrefabs[ghost.ghostType].GhostPrefab;
            if (!entityManager.HasComponent<GhostPrefabMetaData>(prefab))
            {
                netDbg.LogWarning($"Trying to convert a ghost to interpolated, but did not find a prefab with meta data! {entity.ToFixedString()}");
                return false;
            }
            if (!entityManager.HasComponent<PredictedGhost>(entity))
            {
                //netDbg.LogWarning("Trying to convert a ghost to interpolated, but it is already interpolated");
                return false;
            }

            ref var ghostMetaData = ref entityManager.GetComponentData<GhostPrefabMetaData>(prefab).Value.Value;
            if (ghostMetaData.SupportedModes != GhostPrefabBlobMetaData.GhostMode.Both)
            {
                netDbg.LogWarning($"Trying to convert a ghost to interpolated, but it does not support both modes! {entity.ToFixedString()}");
                return false;
            }
            if (ghostMetaData.DefaultMode == GhostPrefabBlobMetaData.GhostMode.Both)
            {
                netDbg.LogWarning($"Trying to convert a ghost to interpolated, but it is owner predicted and owner predicted ghosts cannot be switched on demand! {entity.ToFixedString()}");
                return false;
            }

            ref var toAdd = ref ghostMetaData.DisableOnPredictedClient;
            ref var toRemove = ref ghostMetaData.DisableOnInterpolatedClient;
            return AddRemoveComponents(entityManager, ref ghostUpdateVersion, entity, prefab, ref toAdd, ref toRemove, transitionDuration);
        }

        static unsafe bool AddRemoveComponents(EntityManager entityManager, ref GhostUpdateVersion ghostUpdateVersion, Entity entity, Entity prefab, ref BlobArray<GhostPrefabBlobMetaData.ComponentReference> toAdd, ref BlobArray<GhostPrefabBlobMetaData.ComponentReference> toRemove, float duration)
        {
            var linkedEntityGroup = entityManager.GetBuffer<LinkedEntityGroup>(entity).ToNativeArray(Allocator.Temp);
            var prefabLinkedEntityGroup = entityManager.GetBuffer<LinkedEntityGroup>(prefab).ToNativeArray(Allocator.Temp);
            //Need copy because removing component will invalidate the buffer pointer, since introduce structural changes
            for (int add = 0; add < toAdd.Length; ++add)
            {
                var compType = ComponentType.ReadWrite(TypeManager.GetTypeIndexFromStableTypeHash(toAdd[add].StableHash));
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (compType.IsChunkComponent || compType.IsSharedComponent)
                {
                    throw new InvalidOperationException($"Ghosts with chunk or shared components cannot switch prediction. {entity.ToFixedString()}");
                }
#endif
                // TODO: Investigate batched AddComponent (i.e. 2 passes).
                entityManager.AddComponent(linkedEntityGroup[toAdd[add].EntityIndex].Value, compType);
                if (compType.IsZeroSized)
                    continue;
                var typeInfo = TypeManager.GetTypeInfo(compType.TypeIndex);
                var typeHandle = entityManager.GetDynamicComponentTypeHandle(compType);
                var sizeInChunk = typeInfo.SizeInChunk;
                var srcInfo = entityManager.GetStorageInfo(prefabLinkedEntityGroup[toAdd[add].EntityIndex].Value);
                var dstInfo = entityManager.GetStorageInfo(linkedEntityGroup[toAdd[add].EntityIndex].Value);
                if (compType.IsBuffer)
                {
                    var srcBuffer = srcInfo.Chunk.GetUntypedBufferAccessor(ref typeHandle);
                    var dstBuffer = dstInfo.Chunk.GetUntypedBufferAccessor(ref typeHandle);
                    dstBuffer.ResizeUninitialized(dstInfo.IndexInChunk, srcBuffer.Length);
                    var dstDataPtr = dstBuffer.GetUnsafeReadOnlyPtr(dstInfo.IndexInChunk);
                    var srcDataPtr = srcBuffer.GetUnsafeReadOnlyPtrAndLength(srcInfo.IndexInChunk, out var bufLen);
                    UnsafeUtility.MemCpy(dstDataPtr, srcDataPtr, typeInfo.ElementSize * bufLen);
                }
                else
                {
                    byte* src = (byte*)srcInfo.Chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref typeHandle, sizeInChunk).GetUnsafeReadOnlyPtr();
                    byte* dst = (byte*)dstInfo.Chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref typeHandle, sizeInChunk).GetUnsafePtr();
                    UnsafeUtility.MemCpy(dst + dstInfo.IndexInChunk*sizeInChunk, src + srcInfo.IndexInChunk*sizeInChunk, sizeInChunk);
                }
            }
            for (int rm = 0; rm < toRemove.Length; ++rm)
            {
                // TODO: Investigate batched RemoveComponent (i.e. 2 passes).
                var compType = ComponentType.ReadWrite(TypeManager.GetTypeIndexFromStableTypeHash(toRemove[rm].StableHash));
                entityManager.RemoveComponent(linkedEntityGroup[toRemove[rm].EntityIndex].Value, compType);
            }
            if (duration > 0 &&
                entityManager.HasComponent<LocalToWorld>(entity) &&
                entityManager.HasComponent<LocalTransform>(entity))
            {
                entityManager.AddComponent(entity, new ComponentTypeSet(ComponentType.ReadWrite<SwitchPredictionSmoothing>()));
                var localTransform = entityManager.GetComponentData<LocalTransform>(entity);
                entityManager.SetComponentData(entity, new SwitchPredictionSmoothing
                {
                    InitialPosition = localTransform.Position,
                    InitialRotation = localTransform.Rotation,
                    CurrentFactor = 0,
                    Duration = duration,
                    SkipVersion = ghostUpdateVersion.LastSystemVersion
                });
            }
            return true;
        }
    }
}
