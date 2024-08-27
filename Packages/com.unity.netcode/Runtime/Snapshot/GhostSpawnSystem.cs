using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.NetCode
{
    /// <summary>
    /// System responsible for spawning all the ghost entities for the client world.
    /// <para>
    /// When a ghost snapshot is received from the server, the <see cref="GhostReceiveSystem"/> add a spawning request to the <see cref="GhostSpawnBuffer"/>.
    /// After the spawning requests has been classified (see <see cref="GhostSpawnClassificationSystem"/>),
    /// the <see cref="GhostSpawnSystem"/> start processing the spawning queue.
    /// </para>
    /// <para>
    /// Based on the spawning (<see cref="GhostSpawnBuffer.Type"/>), the requests are handled quite differently.
    /// <para>When the mode is set to <see cref="GhostSpawnBuffer.Type.Interpolated"/>, the ghost creation is delayed
    /// until the <see cref="NetworkTime.InterpolationTick"/> match (or is greater) the actual spawning tick on the server.
    /// A temporary entity, holding the spawning information, the received snapshot data from the server, and tagged with the <seealso cref="PendingSpawnPlaceholder"/>
    /// is created. The entity will exists until the real ghost instance is spawned (or a de-spawn request has been received),
    /// and its sole purpose of receiving new incoming snapshots (even though they are not applied to the entity, since it is not a real ghost).
    /// </para>
    /// <para>
    /// When the mode is set to <see cref="GhostSpawnBuffer.Type.Predicted"/>, a new ghost instance in spawned immediately if the
    /// current simulated <see cref="NetworkTime.ServerTick"/> is greater or equals the spawning tick reported by the server.
    /// <remarks>
    /// This condition is usually the norm, since the client timeline (the current simulated tick) should be ahead of the server.
    /// </remarks>
    /// <para>
    /// Otherwise, the ghost creation is delayed until the the <see cref="NetworkTime.ServerTick"/> is greater or equals the required spawning tick.
    /// Like to interpolated ghost, a temporary placeholder entity is created to hold spawning information and for holding new received snapshots.
    /// </para>
    /// </para>
    /// </para>
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(GhostSpawnSystemGroup))]
    public partial struct GhostSpawnSystem : ISystem
    {
        struct DelayedSpawnGhost
        {
            public int ghostId;
            public int ghostType;
            public NetworkTick clientSpawnTick;
            public NetworkTick serverSpawnTick;
            public Entity oldEntity;
            public Entity predictedSpawnEntity;
        }
        NativeQueue<DelayedSpawnGhost> m_DelayedInterpolatedGhostSpawnQueue;
        NativeQueue<DelayedSpawnGhost> m_DelayedPredictedGhostSpawnQueue;

        EntityQuery m_InGameGroup;
        EntityQuery m_NetworkIdQuery;

        public void OnCreate(ref SystemState state)
        {
            m_DelayedInterpolatedGhostSpawnQueue = new NativeQueue<DelayedSpawnGhost>(Allocator.Persistent);
            m_DelayedPredictedGhostSpawnQueue = new NativeQueue<DelayedSpawnGhost>(Allocator.Persistent);
            m_InGameGroup = state.GetEntityQuery(ComponentType.ReadOnly<NetworkStreamInGame>());
            m_NetworkIdQuery = state.GetEntityQuery(ComponentType.ReadOnly<NetworkId>(), ComponentType.Exclude<NetworkStreamRequestDisconnect>());

            var ent = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(ent, "GhostSpawnQueue");
            state.EntityManager.AddComponentData(ent, default(GhostSpawnQueue));
            state.EntityManager.AddBuffer<GhostSpawnBuffer>(ent);
            state.EntityManager.AddBuffer<SnapshotDataBuffer>(ent);
            state.RequireForUpdate<GhostCollection>();
            state.RequireForUpdate<GhostSpawnQueue>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            m_DelayedPredictedGhostSpawnQueue.Dispose();
            m_DelayedInterpolatedGhostSpawnQueue.Dispose();
        }

        [BurstCompile]
        public unsafe void OnUpdate(ref SystemState state)
        {
            state.Dependency.Complete(); // For ghost map access
            if (state.WorldUnmanaged.IsThinClient())
                return;
            var stateEntityManager = state.EntityManager;
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var interpolationTargetTick = networkTime.InterpolationTick;
            if (networkTime.InterpolationTickFraction < 1 && interpolationTargetTick.IsValid)
                interpolationTargetTick.Decrement();
            var predictionTargetTick = networkTime.ServerTick;
            var prefabsEntity = SystemAPI.GetSingletonEntity<GhostCollection>();
            var prefabs = stateEntityManager.GetBuffer<GhostCollectionPrefab>(prefabsEntity).ToNativeArray(Allocator.Temp);

            var ghostSpawnEntity = SystemAPI.GetSingletonEntity<GhostSpawnQueue>();
            var ghostSpawnBufferComponent = stateEntityManager.GetBuffer<GhostSpawnBuffer>(ghostSpawnEntity);
            var snapshotDataBufferComponent = stateEntityManager.GetBuffer<SnapshotDataBuffer>(ghostSpawnEntity);

            //Avoid adding new ghost if the stream is not in game
            if (m_InGameGroup.IsEmptyIgnoreFilter)
            {
                ghostSpawnBufferComponent.ResizeUninitialized(0);
                snapshotDataBufferComponent.ResizeUninitialized(0);
                m_DelayedPredictedGhostSpawnQueue.Clear();
                m_DelayedInterpolatedGhostSpawnQueue.Clear();
                return;
            }

            var ghostSpawnBuffer = ghostSpawnBufferComponent.ToNativeArray(Allocator.Temp);
            var snapshotDataBuffer = snapshotDataBufferComponent.ToNativeArray(Allocator.Temp);
            ghostSpawnBufferComponent.ResizeUninitialized(0);
            snapshotDataBufferComponent.ResizeUninitialized(0);

            var spawnedGhosts = new NativeList<SpawnedGhostMapping>(16, Allocator.Temp);
            var nonSpawnedGhosts = new NativeList<NonSpawnedGhostMapping>(16, Allocator.Temp);
            var ghostCollectionSingleton = SystemAPI.GetSingletonEntity<GhostCollection>();
            for (int i = 0; i < ghostSpawnBuffer.Length; ++i)
            {
                var ghost = ghostSpawnBuffer[i];
                Entity entity = Entity.Null;
                byte* snapshotData = null;

                var ghostTypeCollection = stateEntityManager.GetBuffer<GhostCollectionPrefabSerializer>(ghostCollectionSingleton);
                var snapshotSize = ghostTypeCollection[ghost.GhostType].SnapshotSize;
                bool hasBuffers = ghostTypeCollection[ghost.GhostType].NumBuffers > 0;

                if (ghost.SpawnType == GhostSpawnBuffer.Type.Interpolated)
                {
                    entity = AddToDelayedSpawnQueue(ref stateEntityManager, m_DelayedInterpolatedGhostSpawnQueue, ghost, ref snapshotDataBuffer, ghostTypeCollection);

                    nonSpawnedGhosts.Add(new NonSpawnedGhostMapping { ghostId = ghost.GhostID, entity = entity });
                }
                else if (ghost.SpawnType == GhostSpawnBuffer.Type.Predicted)
                {
                    // can it be spawned immediately?
                    if (!ghost.ClientSpawnTick.IsNewerThan(predictionTargetTick))
                    {
                        // TODO: this could allow some time for the prefab to load before giving an error
                        if (prefabs[ghost.GhostType].GhostPrefab == Entity.Null)
                        {
                            ReportMissingPrefab(ref stateEntityManager);
                            continue;
                        }
                        // Spawn directly
                        entity = ghost.PredictedSpawnEntity != Entity.Null ? ghost.PredictedSpawnEntity : stateEntityManager.Instantiate(prefabs[ghost.GhostType].GhostPrefab);
                        if(stateEntityManager.HasComponent<PredictedGhostSpawnRequest>(entity))
                            stateEntityManager.RemoveComponent<PredictedGhostSpawnRequest>(entity);
                        if (stateEntityManager.HasComponent<GhostPrefabMetaData>(prefabs[ghost.GhostType].GhostPrefab))
                        {
                            ref var toRemove = ref stateEntityManager.GetComponentData<GhostPrefabMetaData>(prefabs[ghost.GhostType].GhostPrefab).Value.Value.DisableOnPredictedClient;
                            //Need copy because removing component will invalidate the buffer pointer, since introduce structural changes
                            var linkedEntityGroup = stateEntityManager.GetBuffer<LinkedEntityGroup>(entity).ToNativeArray(Allocator.Temp);
                            for (int rm = 0; rm < toRemove.Length; ++rm)
                            {
                                var compType = ComponentType.ReadWrite(TypeManager.GetTypeIndexFromStableTypeHash(toRemove[rm].StableHash));
                                stateEntityManager.RemoveComponent(linkedEntityGroup[toRemove[rm].EntityIndex].Value, compType);
                            }
                        }
                    	stateEntityManager.SetComponentData(entity, new GhostInstance {ghostId = ghost.GhostID, ghostType = ghost.GhostType, spawnTick = ghost.ServerSpawnTick});
                        if (PrespawnHelper.IsPrespawnGhostId(ghost.GhostID))
                            ConfigurePrespawnGhost(ref stateEntityManager, entity, ghost);
                        var newBuffer = stateEntityManager.GetBuffer<SnapshotDataBuffer>(entity);
                        newBuffer.ResizeUninitialized(snapshotSize * GhostSystemConstants.SnapshotHistorySize);
                        snapshotData = (byte*)newBuffer.GetUnsafePtr();
                        stateEntityManager.SetComponentData(entity, new SnapshotData{SnapshotSize = snapshotSize, LatestIndex = 0});
                        spawnedGhosts.Add(new SpawnedGhostMapping{ghost = new SpawnedGhost{ghostId = ghost.GhostID, spawnTick = ghost.ServerSpawnTick}, entity = entity});

                        UnsafeUtility.MemClear(snapshotData, snapshotSize * GhostSystemConstants.SnapshotHistorySize);
                        UnsafeUtility.MemCpy(snapshotData, (byte*)snapshotDataBuffer.GetUnsafeReadOnlyPtr() + ghost.DataOffset, snapshotSize);
                        if (hasBuffers)
                        {
                            //Resize and copy the associated dynamic buffer snapshot data
                            var snapshotDynamicBuffer = stateEntityManager.GetBuffer<SnapshotDynamicDataBuffer>(entity);
                            var dynamicDataCapacity= SnapshotDynamicBuffersHelper.CalculateBufferCapacity(ghost.DynamicDataSize, out var _);
                            snapshotDynamicBuffer.ResizeUninitialized((int)dynamicDataCapacity);
                            var dynamicSnapshotData = (byte*)snapshotDynamicBuffer.GetUnsafePtr();
                            if(dynamicSnapshotData == null)
                                throw new InvalidOperationException("snapshot dynamic data buffer not initialized but ghost has dynamic buffer contents");

                            // Update the dynamic data header (uint[GhostSystemConstants.SnapshotHistorySize)]) by writing the used size for the current slot
                            // (for new spawned entity is 0). Is un-necessary to initialize all the header slots to 0 since that information is only used
                            // for sake of delta compression and, because that depend on the acked tick, only initialized and relevant slots are accessed in general.
                            // For more information about the layout, see SnapshotData.cs.
                            ((uint*)dynamicSnapshotData)[0] = ghost.DynamicDataSize;
                            var headerSize = SnapshotDynamicBuffersHelper.GetHeaderSize();
                            UnsafeUtility.MemCpy(dynamicSnapshotData + headerSize, (byte*)snapshotDataBuffer.GetUnsafeReadOnlyPtr() + ghost.DataOffset + snapshotSize, ghost.DynamicDataSize);
                        }
                    }
                    else
                    {
                        // Add to delayed spawning queue
                        entity = AddToDelayedSpawnQueue(ref stateEntityManager, m_DelayedPredictedGhostSpawnQueue, ghost, ref snapshotDataBuffer, ghostTypeCollection);

                        nonSpawnedGhosts.Add(new NonSpawnedGhostMapping { ghostId = ghost.GhostID, entity = entity });
                    }
                }
            }
            var netDebug = SystemAPI.GetSingleton<NetDebug>();
            ref var ghostEntityMap = ref SystemAPI.GetSingletonRW<SpawnedGhostEntityMap>().ValueRW;
            ghostEntityMap.AddClientNonSpawnedGhosts(nonSpawnedGhosts.AsArray(), netDebug);
            ghostEntityMap.AddClientSpawnedGhosts(spawnedGhosts.AsArray(), netDebug);

            spawnedGhosts.Clear();
            while (m_DelayedInterpolatedGhostSpawnQueue.Count > 0 &&
                   !m_DelayedInterpolatedGhostSpawnQueue.Peek().clientSpawnTick.IsNewerThan(interpolationTargetTick))
            {
                var ghost = m_DelayedInterpolatedGhostSpawnQueue.Dequeue();
                if (TrySpawnFromDelayedQueue(ref stateEntityManager, ghost, GhostSpawnBuffer.Type.Interpolated, prefabs, ghostCollectionSingleton, out var entity))
                {
                    spawnedGhosts.Add(new SpawnedGhostMapping { ghost = new SpawnedGhost { ghostId = ghost.ghostId, spawnTick = ghost.serverSpawnTick }, entity = entity, previousEntity = ghost.oldEntity });
                }
            }
            while (m_DelayedPredictedGhostSpawnQueue.Count > 0 &&
                   !m_DelayedPredictedGhostSpawnQueue.Peek().clientSpawnTick.IsNewerThan(predictionTargetTick))
            {
                var ghost = m_DelayedPredictedGhostSpawnQueue.Dequeue();
                if (TrySpawnFromDelayedQueue(ref stateEntityManager, ghost, GhostSpawnBuffer.Type.Predicted, prefabs, ghostCollectionSingleton, out var entity))
                {
                    spawnedGhosts.Add(new SpawnedGhostMapping { ghost = new SpawnedGhost { ghostId = ghost.ghostId, spawnTick = ghost.serverSpawnTick }, entity = entity, previousEntity = ghost.oldEntity });
                }
            }
            ghostEntityMap.UpdateClientSpawnedGhosts(spawnedGhosts.AsArray(), netDebug);
        }

        void ConfigurePrespawnGhost(ref EntityManager entityManager, Entity entity, in GhostSpawnBuffer ghost)
        {
            if(ghost.PrespawnIndex == -1)
                throw new InvalidOperationException("respawning a pre-spawned ghost requires a valid prespawn index");
            entityManager.AddComponentData(entity, new PreSpawnedGhostIndex {Value = ghost.PrespawnIndex});
            entityManager.AddSharedComponent(entity, new SceneSection
            {
                SceneGUID = ghost.SceneGUID,
                Section = ghost.SectionIndex
            });
        }

        void ReportMissingPrefab(ref EntityManager entityManager)
        {
            SystemAPI.GetSingleton<NetDebug>().LogError($"Trying to spawn with a prefab which is not loaded");

            // TODO: Use entityManager.AddComponentData(EntityQuery, T); when it's available.
            using var entities = m_NetworkIdQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                entityManager.AddComponentData(entity, new NetworkStreamRequestDisconnect {Reason = NetworkStreamDisconnectReason.BadProtocolVersion});
            }
        }

        unsafe Entity AddToDelayedSpawnQueue(ref EntityManager entityManager, NativeQueue<DelayedSpawnGhost> delayedSpawnQueue, in GhostSpawnBuffer ghost, ref NativeArray<SnapshotDataBuffer> snapshotDataBuffer, in DynamicBuffer<GhostCollectionPrefabSerializer> ghostTypeCollection)
        {
            var snapshotSize = ghostTypeCollection[ghost.GhostType].SnapshotSize;
            bool hasBuffers = ghostTypeCollection[ghost.GhostType].NumBuffers > 0;

            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, new GhostInstance { ghostId = ghost.GhostID, ghostType = ghost.GhostType, spawnTick = ghost.ServerSpawnTick });
            entityManager.AddComponent<PendingSpawnPlaceholder>(entity);
            if (PrespawnHelper.IsPrespawnGhostId(ghost.GhostID))
                ConfigurePrespawnGhost(ref entityManager, entity, ghost);

            var newBuffer = entityManager.AddBuffer<SnapshotDataBuffer>(entity);
            newBuffer.ResizeUninitialized(snapshotSize * GhostSystemConstants.SnapshotHistorySize);
            var snapshotData = (byte*)newBuffer.GetUnsafePtr();
            //Add also the SnapshotDynamicDataBuffer if the entity has buffers to copy the dynamic contents
            if (hasBuffers)
                entityManager.AddBuffer<SnapshotDynamicDataBuffer>(entity);
            entityManager.AddComponentData(entity, new SnapshotData { SnapshotSize = snapshotSize, LatestIndex = 0 });

            delayedSpawnQueue.Enqueue(new GhostSpawnSystem.DelayedSpawnGhost { ghostId = ghost.GhostID, ghostType = ghost.GhostType, clientSpawnTick = ghost.ClientSpawnTick, serverSpawnTick = ghost.ServerSpawnTick, oldEntity = entity, predictedSpawnEntity = ghost.PredictedSpawnEntity });

            UnsafeUtility.MemClear(snapshotData, snapshotSize * GhostSystemConstants.SnapshotHistorySize);
            UnsafeUtility.MemCpy(snapshotData, (byte*)snapshotDataBuffer.GetUnsafeReadOnlyPtr() + ghost.DataOffset, snapshotSize);
            if (hasBuffers)
            {
                //Resize and copy the associated dynamic buffer snapshot data
                var snapshotDynamicBuffer = entityManager.GetBuffer<SnapshotDynamicDataBuffer>(entity);
                var dynamicDataCapacity = SnapshotDynamicBuffersHelper.CalculateBufferCapacity(ghost.DynamicDataSize, out var _);
                snapshotDynamicBuffer.ResizeUninitialized((int)dynamicDataCapacity);
                var dynamicSnapshotData = (byte*)snapshotDynamicBuffer.GetUnsafePtr();
                if (dynamicSnapshotData == null)
                    throw new InvalidOperationException("snapshot dynamic data buffer not initialized but ghost has dynamic buffer contents");

                // Update the dynamic data header (uint[GhostSystemConstants.SnapshotHistorySize)]) by writing the used size for the current slot
                // (for new spawned entity is 0). Is un-necessary to initialize all the header slots to 0 since that information is only used
                // for sake of delta compression and, because that depend on the acked tick, only initialized and relevant slots are accessed in general.
                // For more information about the layout, see SnapshotData.cs.
                ((uint*)dynamicSnapshotData)[0] = ghost.DynamicDataSize;
                var headerSize = SnapshotDynamicBuffersHelper.GetHeaderSize();
                UnsafeUtility.MemCpy(dynamicSnapshotData + headerSize, (byte*)snapshotDataBuffer.GetUnsafeReadOnlyPtr() + ghost.DataOffset + snapshotSize, ghost.DynamicDataSize);
            }

            return entity;
        }

        unsafe bool TrySpawnFromDelayedQueue(ref EntityManager entityManager, in DelayedSpawnGhost ghost, GhostSpawnBuffer.Type spawnType, in NativeArray<GhostCollectionPrefab> prefabs, Entity ghostCollectionSingleton, out Entity entity)
        {
            entity = Entity.Null;

            // TODO: this could allow some time for the prefab to load before giving an error
            if (prefabs[ghost.ghostType].GhostPrefab == Entity.Null)
            {
                ReportMissingPrefab(ref entityManager);
                return false;
            }
            //Entity has been destroyed meawhile it was in the queue
            if (!entityManager.HasComponent<GhostInstance>(ghost.oldEntity))
                return false;

            // Spawn actual entity
            entity = ghost.predictedSpawnEntity != Entity.Null ? ghost.predictedSpawnEntity : entityManager.Instantiate(prefabs[ghost.ghostType].GhostPrefab);
            if(entityManager.HasComponent<PredictedGhostSpawnRequest>(entity))
                entityManager.RemoveComponent<PredictedGhostSpawnRequest>(entity);
            if (entityManager.HasComponent<GhostPrefabMetaData>(prefabs[ghost.ghostType].GhostPrefab))
            {
                ref var toRemove = ref entityManager.GetComponentData<GhostPrefabMetaData>(prefabs[ghost.ghostType].GhostPrefab).Value.Value.DisableOnInterpolatedClient;
                if (spawnType == GhostSpawnBuffer.Type.Predicted)
                    toRemove = ref entityManager.GetComponentData<GhostPrefabMetaData>(prefabs[ghost.ghostType].GhostPrefab).Value.Value.DisableOnPredictedClient;
                var linkedEntityGroup = entityManager.GetBuffer<LinkedEntityGroup>(entity).ToNativeArray(Allocator.Temp);
                //Need copy because removing component will invalidate the buffer pointer, since introduce structural changes
                for (int rm = 0; rm < toRemove.Length; ++rm)
                {
                    var compType = ComponentType.ReadWrite(TypeManager.GetTypeIndexFromStableTypeHash(toRemove[rm].StableHash));
                    entityManager.RemoveComponent(linkedEntityGroup[toRemove[rm].EntityIndex].Value, compType);
                }
            }
            entityManager.SetComponentData(entity, entityManager.GetComponentData<SnapshotData>(ghost.oldEntity));
            if (PrespawnHelper.IsPrespawnGhostId(ghost.ghostId))
            {
                entityManager.AddComponentData(entity, entityManager.GetComponentData<PreSpawnedGhostIndex>(ghost.oldEntity));
                entityManager.AddSharedComponent(entity, entityManager.GetSharedComponent<SceneSection>(ghost.oldEntity));
            }
            var ghostComponentData = entityManager.GetComponentData<GhostInstance>(ghost.oldEntity);
            entityManager.SetComponentData(entity, ghostComponentData);
            var oldBuffer = entityManager.GetBuffer<SnapshotDataBuffer>(ghost.oldEntity);
            var newBuffer = entityManager.GetBuffer<SnapshotDataBuffer>(entity);
            newBuffer.ResizeUninitialized(oldBuffer.Length);
            UnsafeUtility.MemCpy(newBuffer.GetUnsafePtr(), oldBuffer.GetUnsafeReadOnlyPtr(), oldBuffer.Length);
            //copy the old buffers content to the new entity.
            //Perf FIXME: if we can introduce a "move" like concept for buffer to transfer ownership we can avoid a lot of copies and
            //allocations
            var ghostTypeCollection = entityManager.GetBuffer<GhostCollectionPrefabSerializer>(ghostCollectionSingleton);
            bool hasBuffers = ghostTypeCollection[ghost.ghostType].NumBuffers > 0;
            if (hasBuffers)
            {
                var oldDynamicBuffer = entityManager.GetBuffer<SnapshotDynamicDataBuffer>(ghost.oldEntity);
                var newDynamicBuffer = entityManager.GetBuffer<SnapshotDynamicDataBuffer>(entity);
                newDynamicBuffer.ResizeUninitialized(oldDynamicBuffer.Length);
                UnsafeUtility.MemCpy(newDynamicBuffer.GetUnsafePtr(), oldDynamicBuffer.GetUnsafeReadOnlyPtr(), oldDynamicBuffer.Length);
            }
            entityManager.DestroyEntity(ghost.oldEntity);

            return true;
        }
    }
}
