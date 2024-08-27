using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Transforms;

namespace Unity.NetCode
{
    /// <summary>
    /// Singleton component with APIs and collections required for converting client ghosts <see cref="GhostMode"/> to <see cref="GhostMode.Predicted"/> &amp; <see cref="GhostMode.Interpolated"/>.
    /// <see cref="GhostPredictionSwitchingSystem"/>
    /// </summary>
    public struct GhostPredictionSwitchingQueues : IComponentData
    {
        /// <summary><see cref="PredictionSwitchingUtilities.ConvertGhostToPredicted"/></summary>
        public NativeQueue<ConvertPredictionEntry>.ParallelWriter ConvertToPredictedQueue;
        /// <summary><see cref="PredictionSwitchingUtilities.ConvertGhostToInterpolated"/></summary>
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

    /// <summary>
    /// Optional component that can be added either on a per entity or on per-chunk basis that allow
    /// to customise the transition time when converting from predicted to interpolated <see cref="GhostMode"/>.
    /// If the component is present, the <see cref="TransitionDurationSeconds"/> take precendence over the settings passed to
    /// the <see cref="ConvertPredictionEntry.TransitionDurationSeconds"/>.
    /// </summary>
    public struct PredictionSwitchingSmoothing : IComponentData
    {
        /// <inheritdoc cref="ConvertPredictionEntry.TransitionDurationSeconds"/>
        public float TransitionDurationSeconds;
    }

    /// <summary>
    /// Struct storing the setting for the <see cref="GhostOwnerPredictedSwitchingQueue"/> queue.
    /// </summary>
    internal struct OwnerSwithchingEntry
    {
        /// <summary>
        /// The current value of the <see cref="GhostOwner"/> component.
        /// </summary>
        public int CurrentOwner;

        /// <summary>
        /// The new ghost owner. Can be either a valid <see cref="NetworkId"/> or invalid one (0 or negative).
        /// </summary>
        public int NewOwner;

        /// <summary>
        /// The ghost that will need to be converted to either predicted or interpolated.
        /// </summary>
        public Entity TargetEntity;
    }

    /// <summary>
    /// Singleton component, used to track when an ghost with mode set to <see cref="GhostMode.OwnerPredicted"/> has changed
    /// owner and require changing how it is simulated on the client. In particular:
    /// <list type="buller">
    /// <li>If the owner is the same as client <see cref="NetworkId"/> the ghost will become predicted</li>
    /// <li>If the owner is not the same as client <see cref="NetworkId"/> the ghost will become interpolated</li>
    /// </list>
    /// </summary>
    internal struct GhostOwnerPredictedSwitchingQueue : IComponentData
    {
        /// <summary>
        /// The list of owner-predicted ghosts for which the <see cref="GhostOwner"/> has changed and that
        /// requires to be converted to the respective interpolated or predicted version.
        /// </summary>
        public NativeQueue<OwnerSwithchingEntry> SwitchOwnerQueue;
    }

#if UNITY_EDITOR
    internal struct PredictionSwitchingAnalyticsData : IComponentData
    {
        public long NumTimesSwitchedToPredicted;
        public long NumTimesSwitchedToInterpolated;
        //TODO: this field need to have changes in the Analytics schema in order to be reported. JIRA MTT-7267
        public long NumTimesSwitchedOwner;
    }
#endif

    /// <summary>System that applies the prediction switching on the queued entities (via <see cref="GhostPredictionSwitchingQueues"/>).</summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [UpdateAfter(typeof(GhostReceiveSystem))]
    [UpdateBefore(typeof(GhostUpdateSystem))]
    public partial struct GhostPredictionSwitchingSystem : ISystem
    {
        NativeQueue<ConvertPredictionEntry> m_ConvertToInterpolatedQueue;
        NativeQueue<ConvertPredictionEntry> m_ConvertToPredictedQueue;
        NativeQueue<OwnerSwithchingEntry> m_OwnerPredictedQueue;
        ComponentLookup<PredictionSwitchingSmoothing> m_PredictionSwitchingSmoothingLookup;

        public void OnCreate(ref SystemState state)
        {
#if UNITY_EDITOR
            SetupAnalyticsSingleton(state.EntityManager);
#endif
            m_ConvertToInterpolatedQueue = new NativeQueue<ConvertPredictionEntry>(Allocator.Persistent);
            m_ConvertToPredictedQueue = new NativeQueue<ConvertPredictionEntry>(Allocator.Persistent);
            m_PredictionSwitchingSmoothingLookup = state.GetComponentLookup<PredictionSwitchingSmoothing>(true);
            m_OwnerPredictedQueue = new NativeQueue<OwnerSwithchingEntry>(Allocator.Persistent);
            var singletonEntity = state.EntityManager.CreateEntity(
                ComponentType.ReadOnly<GhostPredictionSwitchingQueues>(),
                ComponentType.ReadOnly<GhostOwnerPredictedSwitchingQueue>());
            state.EntityManager.SetName(singletonEntity, (FixedString64Bytes)"GhostPredictionQueues");
            SystemAPI.SetSingleton(new GhostPredictionSwitchingQueues
            {
                ConvertToInterpolatedQueue = m_ConvertToInterpolatedQueue.AsParallelWriter(),
                ConvertToPredictedQueue = m_ConvertToPredictedQueue.AsParallelWriter(),
            });
            SystemAPI.SetSingleton(new GhostOwnerPredictedSwitchingQueue
            {
                SwitchOwnerQueue = m_OwnerPredictedQueue
            });
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            m_ConvertToPredictedQueue.Dispose();
            m_ConvertToInterpolatedQueue.Dispose();
            m_OwnerPredictedQueue.Dispose();
        }

#if UNITY_EDITOR
        static void SetupAnalyticsSingleton(EntityManager entityManager)
        {
            entityManager.CreateSingleton<PredictionSwitchingAnalyticsData>();
        }
#endif

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            //Checking the value for this queues on the main thread requires we are waiting for writers.
            state.CompleteDependency();
            FixedList64Bytes<Entity> batchedDeletedWarnings = default;
            uint batchedDeletedCount = 0;
            //if the client is not connected to the server, or not in game the queue should be empty. Worst case scenario,
            //the client disconnect and in that case the GhostReceiveSystem already destroy all the entities.
            //This is then detected in the ConvertOwnerPredictedGhost method, so it is safe.
            //However, if the client is not in game and asking for converting or switching owner should just skipped and the queues
            //cleared
            if (!SystemAPI.HasSingleton<NetworkStreamInGame>())
            {
                m_ConvertToPredictedQueue.Clear();
                m_ConvertToInterpolatedQueue.Clear();
                m_OwnerPredictedQueue.Clear();
                return;
            }
            if (m_ConvertToPredictedQueue.Count + m_ConvertToInterpolatedQueue.Count + m_OwnerPredictedQueue.Count > 0)
            {
#if UNITY_EDITOR
                UpdateAnalyticsSwitchCount();
#endif
                var netDebug = SystemAPI.GetSingleton<NetDebug>();
                var ghostUpdateVersion = SystemAPI.GetSingleton<GhostUpdateVersion>();
                var prefabs = SystemAPI.GetSingletonBuffer<GhostCollectionPrefab>().ToNativeArray(Allocator.Temp);
                var networkId = SystemAPI.GetSingleton<NetworkId>();
                while (m_OwnerPredictedQueue.TryDequeue(out var ownerSwitching))
                {
                    //This is unfortunately necessary because components are added and removed
                    //invalidating the lookup safety handle. That is really mostly a restriction
                    //(almost a bug i would say).
                    m_PredictionSwitchingSmoothingLookup.Update(ref state);
                    m_PredictionSwitchingSmoothingLookup.TryGetComponent(ownerSwitching.TargetEntity, out var smoothing);
                    PredictionSwitchingUtilities.ConvertOwnerPredictedGhost(state.EntityManager,
                        ownerSwitching.TargetEntity, ownerSwitching.NewOwner, networkId.Value,
                        ghostUpdateVersion, netDebug, prefabs,
                        smoothing.TransitionDurationSeconds, ref batchedDeletedWarnings, ref batchedDeletedCount);
                }
                while (m_ConvertToPredictedQueue.TryDequeue(out var conversion))
                {
                    PredictionSwitchingUtilities.ConvertGhostToPredicted(state.EntityManager, ghostUpdateVersion, netDebug, prefabs, conversion.TargetEntity, conversion.TransitionDurationSeconds, ref batchedDeletedWarnings, ref batchedDeletedCount);
                }
                while (m_ConvertToInterpolatedQueue.TryDequeue(out var conversion))
                {
                    PredictionSwitchingUtilities.ConvertGhostToInterpolated(state.EntityManager, ghostUpdateVersion, netDebug, prefabs, conversion.TargetEntity, conversion.TransitionDurationSeconds, ref batchedDeletedWarnings, ref batchedDeletedCount);
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

#if UNITY_EDITOR
        void UpdateAnalyticsSwitchCount()
        {
            ref var analyticsData = ref SystemAPI.GetSingletonRW<PredictionSwitchingAnalyticsData>().ValueRW;
            analyticsData.NumTimesSwitchedToPredicted += m_ConvertToPredictedQueue.Count;
            analyticsData.NumTimesSwitchedToInterpolated += m_ConvertToInterpolatedQueue.Count;
            analyticsData.NumTimesSwitchedOwner += m_OwnerPredictedQueue.Count;
        }
#endif
    }

    /// <summary>
    /// This system creates and empty prediction switching queue for thin clients. Such
    /// a queue needs to exist for the ghost receive system even though it will not be used
    /// on thin clients (as they have no ghost data).
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    partial struct GhostPredictionSwitchingSystemForThinClient : ISystem
    {
        NativeQueue<OwnerSwithchingEntry> m_OwnerPredictedQueue;
        public void OnCreate(ref SystemState state)
        {
            m_OwnerPredictedQueue = new NativeQueue<OwnerSwithchingEntry>(Allocator.Persistent);
            state.EntityManager.CreateSingleton(new GhostOwnerPredictedSwitchingQueue
            {
                SwitchOwnerQueue = m_OwnerPredictedQueue
            });
        }

        public void OnDestroy(ref SystemState state)
        {
            m_OwnerPredictedQueue.Dispose();
        }
    }

    static internal class PredictionSwitchingUtilities
    {
        /// <summary>
        /// Convert an owner predicted ghost to either an interpolated or predicted ghost, based on the owner.
        /// The ghost must support both interpolated and predicted mode, The new components added as a result of this
        /// operation will have the inital values from the ghost prefab.
        /// </summary>
        static public void ConvertOwnerPredictedGhost(EntityManager entityManager,
            Entity entity, int newOwner, int localNetworkId,
            GhostUpdateVersion ghostUpdateVersion, NetDebug netDbg, NativeArray<GhostCollectionPrefab> ghostCollectionPrefabs,
            float transitionDuration,
            ref FixedList64Bytes<Entity> destroyedEntities, ref uint batchedDeletedCount)
        {
            if (!entityManager.Exists(entity))
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if(destroyedEntities.Length < destroyedEntities.Capacity)
                    destroyedEntities.Add(entity);
                batchedDeletedCount++;
#endif
                return;
            }

            if (!entityManager.HasComponent<GhostInstance>(entity))
            {
                netDbg.LogError($"Trying to switch owner for an owner-predicted ghost, but this is not a ghost entity! {entity.ToFixedString()}");
                return;
            }
            if (entityManager.HasComponent<Prefab>(entity))
            {
                netDbg.LogError($"Trying to switch owner for an owner-predicted ghost, but this is a prefab! {entity.ToFixedString()}");
                return;
            }
            var ghost = entityManager.GetComponentData<GhostInstance>(entity);
            var prefab = ghostCollectionPrefabs[ghost.ghostType].GhostPrefab;
            if (!entityManager.HasComponent<GhostPrefabMetaData>(prefab))
            {
                netDbg.LogWarning($"Trying to switch owner for an owner-predicted ghost, but did not find a prefab with meta data! {entity.ToFixedString()}");
                return;
            }
            ref var ghostMetaData = ref entityManager.GetComponentData<GhostPrefabMetaData>(prefab).Value.Value;
            if (ghostMetaData.SupportedModes != GhostPrefabBlobMetaData.GhostMode.Both)
            {
                netDbg.LogWarning($"Trying to switch owner for an owner-predicted ghost, but do not support switching modes! {entity.ToFixedString()}");
                return;
            }
            if (ghostMetaData.DefaultMode != GhostPrefabBlobMetaData.GhostMode.Both)
            {
                netDbg.LogWarning($"Trying to convert a ghost that is not owner-predicted using the owner-switch queue, that is not allowed!. {entity.ToFixedString()}");
                return;
            }
            bool isPredicted = entityManager.HasComponent<PredictedGhost>(entity);
            if (localNetworkId == newOwner && !isPredicted)
            {
                ref var toAdd = ref ghostMetaData.DisableOnInterpolatedClient;
                ref var toRemove = ref ghostMetaData.DisableOnPredictedClient;
                AddRemoveComponents(entityManager, ref ghostUpdateVersion, entity, prefab, ref toAdd, ref toRemove, transitionDuration);
            }
            else if(localNetworkId != newOwner && isPredicted)
            {
                ref var toAdd = ref ghostMetaData.DisableOnPredictedClient;
                ref var toRemove = ref ghostMetaData.DisableOnInterpolatedClient;
                AddRemoveComponents(entityManager, ref ghostUpdateVersion, entity, prefab, ref toAdd, ref toRemove, transitionDuration);
            }
        }

        /// <summary>
        /// Convert an interpolated ghost to a predicted ghost. The ghost must support both interpolated and predicted mode,
        /// and it cannot be owner predicted. The new components added as a result of this operation will have the inital
        /// values from the ghost prefab.
        /// </summary>
        static public void ConvertGhostToPredicted(EntityManager entityManager, GhostUpdateVersion ghostUpdateVersion,
            NetDebug netDbg, NativeArray<GhostCollectionPrefab> ghostCollectionPrefabs, Entity entity, float transitionDuration,
            ref FixedList64Bytes<Entity> destroyedEntities, ref uint batchedDeletedCount)
        {
            if (!entityManager.Exists(entity))
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if(destroyedEntities.Length < destroyedEntities.Capacity)
                    destroyedEntities.Add(entity);
                batchedDeletedCount++;
#endif
                return;
            }
            if (!entityManager.HasComponent<GhostInstance>(entity))
            {
                netDbg.LogError($"Trying to convert a ghost to predicted, but this is not a ghost entity! {entity.ToFixedString()}");
                return;
            }
            if (entityManager.HasComponent<Prefab>(entity))
            {
                netDbg.LogError($"Trying to convert a ghost to predicted, but this is a prefab! {entity.ToFixedString()}");
                return;
            }
            if (entityManager.HasComponent<PredictedGhost>(entity))
            {
                netDbg.LogWarning($"Trying to convert a ghost to predicted, but it is already predicted! {entity.ToFixedString()}");
                return;
            }
            var ghost = entityManager.GetComponentData<GhostInstance>(entity);
            var prefab = ghostCollectionPrefabs[ghost.ghostType].GhostPrefab;
            if (!entityManager.HasComponent<GhostPrefabMetaData>(prefab))
            {
                netDbg.LogWarning($"Trying to convert a ghost to predicted, but did not find a prefab with meta data! {entity.ToFixedString()}");
                return;
            }
            ref var ghostMetaData = ref entityManager.GetComponentData<GhostPrefabMetaData>(prefab).Value.Value;
            if (ghostMetaData.SupportedModes != GhostPrefabBlobMetaData.GhostMode.Both)
            {
                netDbg.LogWarning($"Trying to convert a ghost to predicted, but it does not support both modes! {entity.ToFixedString()}");
                return;
            }
            if (ghostMetaData.DefaultMode == GhostPrefabBlobMetaData.GhostMode.Both)
            {
                netDbg.LogWarning($"Trying to convert a ghost to predicted, but it is owner predicted and owner predicted ghosts cannot be switched on demand! You must queue a owner-switching change using the GhostOwnerPredictedSwitchingQueue. {entity.ToFixedString()}");
                return;
            }

            ref var toAdd = ref ghostMetaData.DisableOnInterpolatedClient;
            ref var toRemove = ref ghostMetaData.DisableOnPredictedClient;
            AddRemoveComponents(entityManager, ref ghostUpdateVersion, entity, prefab, ref toAdd, ref toRemove, transitionDuration);
        }

        /// <summary>
        /// Convert a predicted ghost to an interpolated ghost. The ghost must support both interpolated and predicted mode,
        /// and it cannot be owner predicted. The new components added as a result of this operation will have the inital
        /// values from the ghost prefab.
        /// </summary>
        static public void ConvertGhostToInterpolated(EntityManager entityManager, GhostUpdateVersion ghostUpdateVersion, NetDebug netDbg, NativeArray<GhostCollectionPrefab> ghostCollectionPrefabs, Entity entity, float transitionDuration, ref FixedList64Bytes<Entity> destroyedEntities, ref uint batchedDeletedCount)
        {
            if (!entityManager.Exists(entity))
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if(destroyedEntities.Length < destroyedEntities.Capacity)
                    destroyedEntities.Add(entity);
                batchedDeletedCount++;
#endif
                return;
            }
            if (!entityManager.HasComponent<GhostInstance>(entity))
            {
                netDbg.LogError($"Trying to convert a ghost to interpolated, but this is not a ghost entity! {entity.ToFixedString()}");
                return;
            }
            if (entityManager.HasComponent<Prefab>(entity))
            {
                netDbg.LogError($"Trying to convert a ghost to interpolated, but this is a prefab! {entity.ToFixedString()}");
                return;
            }
            if (!entityManager.HasComponent<PredictedGhost>(entity))
            {
                netDbg.LogWarning($"Trying to convert a ghost to interpolated, but it is already interpolated! {entity.ToFixedString()}");
                return;
            }

            var ghost = entityManager.GetComponentData<GhostInstance>(entity);
            var prefab = ghostCollectionPrefabs[ghost.ghostType].GhostPrefab;
            if (!entityManager.HasComponent<GhostPrefabMetaData>(prefab))
            {
                netDbg.LogWarning($"Trying to convert a ghost to interpolated, but did not find a prefab with meta data! {entity.ToFixedString()}");
                return;
            }

            ref var ghostMetaData = ref entityManager.GetComponentData<GhostPrefabMetaData>(prefab).Value.Value;
            if (ghostMetaData.SupportedModes != GhostPrefabBlobMetaData.GhostMode.Both)
            {
                netDbg.LogWarning($"Trying to convert a ghost to interpolated, but it does not support both modes! {entity.ToFixedString()}");
                return;
            }
            if (ghostMetaData.DefaultMode == GhostPrefabBlobMetaData.GhostMode.Both)
            {
                netDbg.LogWarning($"Trying to convert a ghost to interpolated, but it is owner predicted and owner predicted ghosts cannot be switched on demand! You must queue a owner-switching change using the GhostOwnerPredictedSwitchingQueue. {entity.ToFixedString()}");
                return;
            }

            ref var toAdd = ref ghostMetaData.DisableOnPredictedClient;
            ref var toRemove = ref ghostMetaData.DisableOnInterpolatedClient;
            AddRemoveComponents(entityManager, ref ghostUpdateVersion, entity, prefab, ref toAdd, ref toRemove, transitionDuration);
        }

        static unsafe void AddRemoveComponents(EntityManager entityManager, ref GhostUpdateVersion ghostUpdateVersion, Entity entity, Entity prefab, ref BlobArray<GhostPrefabBlobMetaData.ComponentReference> toAdd, ref BlobArray<GhostPrefabBlobMetaData.ComponentReference> toRemove, float duration)
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
        }
    }
}
