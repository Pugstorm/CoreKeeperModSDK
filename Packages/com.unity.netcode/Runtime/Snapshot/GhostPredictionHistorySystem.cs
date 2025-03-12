using System;
using Unity.Assertions;
using Unity.Entities;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.NetCode.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Jobs;

namespace Unity.NetCode
{
    // The header of prediction backup state
    // The header is followed by:
    // Entity[Capacity] the entity this history applies to (to prevent errors on structural changes)
    // ulong[Capacity*enabledBits] each enabled bit is stored as a contiguous array of all entities, aligned to ulong
    // int[root components + Capacity * num_child_component] chunk (and child chunk) version numbers.
    // byte*[Capacity * sizeof(IComponentData)] the raw backup data for all replicated components in this ghost type. For buffers an uint pair (size, offset) is stored instead.
    // [Opt]byte*[BuffersDataSize] the raw buffers element data present in the chunk if present. The total buffers size is computed at runtime and the
    // backup state resized accordingly. All buffers contents start to a 16 bytes aligned offset: Align(b1Elem*b1ElemSize, 16), Align(b2Elem*b2ElemSize, 16) ...

    internal unsafe struct PredictionBackupState
	{
		public const int PredictionHistorySize = 12;
		
        // If ghost type has changed the data must be discarded as the chunk is now used for something else
        public int ghostType;
        public int entityCapacity;
        public int entitiesOffset;
        public int enabledBitOffset;
        public int enabledBits;
        public int ghostOwnerOffset;
        //the ghost component serialized size
        public int dataOffset;
        public int dataSize;
        //the capacity for the dynamic data. Dynamic Buffers are store after the component backup
        public int bufferDataCapacity;
        public int bufferDataOffset;

        public static IntPtr AllocNew(int ghostTypeId, int enabledBits,
            int numComponents, int dataSize, int entityCapacity, int buffersDataCapacity, int predictionOwnerOffset)
        {
            var entitiesSize = (ushort)GetEntitiesSize(entityCapacity, out var _);
            var headerSize = GetHeaderSize();
            // each enabled bit is a unique array big enough to fit all entities
            var enabledBitSize = GetEnabledBitsSize(entityCapacity, enabledBits);
			var size = headerSize + entitiesSize + (enabledBitSize + dataSize + buffersDataCapacity) * PredictionHistorySize;
            var state = (PredictionBackupState*)UnsafeUtility.Malloc(size, 16, Allocator.Persistent);
            state->ghostType = ghostTypeId;
            state->entityCapacity = entityCapacity;
            state->entitiesOffset = headerSize;
			// Clear the entities since that is what we use to detect empty backup slots
			UnsafeUtility.MemClear(((byte*)state) + state->entitiesOffset, entitiesSize);
            state->enabledBitOffset = headerSize + entitiesSize;
            state->ghostOwnerOffset = predictionOwnerOffset;
            state->enabledBits = enabledBits;
            state->dataOffset = state->enabledBitOffset + enabledBitSize * PredictionHistorySize;
            state->dataSize = dataSize;
            state->bufferDataCapacity = buffersDataCapacity;
            state->bufferDataOffset = state->dataOffset + dataSize * PredictionHistorySize;
            return (IntPtr)state;
        }

        public static IntPtr Realloc(IntPtr oldState, int ghostTypeId, int enabledBits, int numComponents, int dataSize, int entityCapacity, int buffersDataCapacity, int predictionOwnerOffset)
        {
			if (oldState == IntPtr.Zero)
			{
				return AllocNew(ghostTypeId, enabledBits, numComponents, dataSize, entityCapacity, buffersDataCapacity, predictionOwnerOffset);
			}
			
			var oldPs = (PredictionBackupState*)oldState;
			if (buffersDataCapacity <= oldPs->bufferDataCapacity)
			{
				return oldState;
			}
			
            var entitiesSize = (ushort)GetEntitiesSize(entityCapacity, out var _);
            var headerSize = GetHeaderSize();
            // each enabled bit is a unique array big enough to fit all entities
            var enabledBitSize = GetEnabledBitsSize(entityCapacity, enabledBits);
			
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (oldPs->ghostType != ghostTypeId)
				throw new InvalidOperationException($"Ghost type has changed from {oldPs->ghostType} to {ghostTypeId}.");
			if (oldPs->entityCapacity != entityCapacity)
				throw new InvalidOperationException($"Entity capacity has changed from {oldPs->entityCapacity} to {entityCapacity}.");
			if (oldPs->enabledBits != enabledBits)
				throw new InvalidOperationException($"Enabled bits has changed from {oldPs->enabledBits} to {enabledBits}.");
			if (oldPs->dataSize != dataSize)
				throw new InvalidOperationException($"Data size has changed from {oldPs->dataSize} to {dataSize}.");
			if (oldPs->ghostOwnerOffset != predictionOwnerOffset)
				throw new InvalidOperationException($"Ghost owner offset has changed from {oldPs->ghostOwnerOffset} to {predictionOwnerOffset}.");
#endif

			var size = headerSize + entitiesSize + (enabledBitSize + dataSize + buffersDataCapacity) * PredictionHistorySize;
            var state = (PredictionBackupState*)UnsafeUtility.Malloc(size, 16, Allocator.Persistent);
			// Clear the entities since that is what we use to detect empty backup slots
			UnsafeUtility.MemClear(((byte*)state) + ((PredictionBackupState*)oldState)->entitiesOffset, entitiesSize);
			
			// move everything except buffer
			UnsafeUtility.MemCpy(state, (void*)oldState, headerSize + entitiesSize + (enabledBitSize + dataSize) * PredictionHistorySize);
            state->bufferDataCapacity = buffersDataCapacity;

			// buffer sizes has increased
			for (int i = 0; i < PredictionHistorySize; ++i)
			{
				var oldBufferPtr = GetBufferDataPtr(oldState, i);
				var newBufferPtr = GetBufferDataPtr((IntPtr)state, i);
				UnsafeUtility.MemCpy(newBufferPtr, oldBufferPtr, GetBufferDataCapacity(oldState));
			}
			
			UnsafeUtility.Free((void*)oldState, Allocator.Persistent);
			
            return (IntPtr)state;
        }
		
        public static int GetHeaderSize()
        {
            return (UnsafeUtility.SizeOf<PredictionBackupState>() + 15) & (~15);
        }
        public static int GetEntitiesSize(int chunkCapacity, out int singleEntitySize)
        {
            singleEntitySize = UnsafeUtility.SizeOf<Entity>();
            return ((singleEntitySize * chunkCapacity * PredictionHistorySize) + 15) & (~15);
        }
#if false
		public static int GetEntityCount(IntPtr state, int index)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (index < 0 || index >= PredictionHistorySize)
				throw new InvalidOperationException("prediction backup history index");
#endif
			
            var ps = ((PredictionBackupState*) state);
			byte* entityCounts = ((byte*)state) + ps->entityCountOffset;
			return entityCounts[index];
		}
		public static void SetEntityCount(IntPtr state, int count, int index)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (index < 0 || index >= PredictionHistorySize)
				throw new InvalidOperationException("prediction backup history index");
			if (count > 128)
				throw new InvalidOperationException("prediction backup history entity count");
#endif
			
			var ps = ((PredictionBackupState*) state);
			byte* entityCounts = ((byte*)state) + ps->entityCountOffset;
			entityCounts[index] = (byte)count;
		}
#endif
        public static int GetDataSize(int componentSize, int chunkCapacity)
        {
            return (componentSize * chunkCapacity + 15) &(~15);
        }
        public static Entity* GetEntities(IntPtr state, int index)
        {
            var ps = ((PredictionBackupState*) state);
			return (Entity*)(((byte*)state) + ps->entitiesOffset + UnsafeUtility.SizeOf<Entity>() * ps->entityCapacity * index);
        }
        public static bool MatchEntity(IntPtr state, int ent, in Entity entity, int index)
        {
            var ps = ((PredictionBackupState*) state);
            return ((Entity*)(((byte*)state) + ps->entitiesOffset + UnsafeUtility.SizeOf<Entity>() * ps->entityCapacity * index))[ent] == entity;
        }
        public static byte* GetData(IntPtr state, int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (index < 0 || index >= PredictionHistorySize)
				throw new InvalidOperationException("prediction backup history index");
#endif
            var ps = ((PredictionBackupState*) state);
            return ((byte*) state) + ps->dataOffset + ps->dataSize * index;
        }

        public static int GetBufferDataCapacity(IntPtr state)
        {
            return ((PredictionBackupState*) state)->bufferDataCapacity;
        }
        public static byte* GetBufferDataPtr(IntPtr state, int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (index < 0 || index >= PredictionHistorySize)
				throw new InvalidOperationException("prediction backup history index");
#endif
            var ps = ((PredictionBackupState*) state);
            return ((byte*) state) + ps->bufferDataOffset + ps->bufferDataCapacity * index;
        }
        public static byte* GetNextData(byte* data, int componentSize, int chunkCapacity)
        {
            return data + GetDataSize(componentSize, chunkCapacity);
        }
        public static ulong* GetEnabledBits(IntPtr state, int index)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (index < 0 || index >= PredictionHistorySize)
				throw new InvalidOperationException("prediction backup history index");
#endif
            var ps = ((PredictionBackupState*) state);
			var size = GetEnabledBitsSize(ps->entityCapacity, ps->enabledBits);
            return (ulong*)(((byte*) state) + ps->enabledBitOffset + size * index);
        }
        public static ulong* GetNextEnabledBits(ulong* data, int chunkCapacity, int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (index < 0 || index >= PredictionHistorySize)
				throw new InvalidOperationException("prediction backup history index");
#endif
            return data + (chunkCapacity+63)/64;
        }
		private static int GetEnabledBitsSize(int entityCapacity, int enabledBits)
		{
            return (((entityCapacity+63)&(~63))/8 * enabledBits + 15) & (~15);
		}
        public static int GetGhostOwner(IntPtr state, int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (index < 0 || index >= PredictionHistorySize)
				throw new InvalidOperationException("prediction backup history index");
#endif
            var ps = ((PredictionBackupState*) state);
            if (ps->ghostOwnerOffset != -1)
                return *(((byte*)state) + ps->dataOffset + ps->dataSize * index + ps->ghostOwnerOffset);
            //return an invalid owner (0)
            return 0;
        }
    }

    /// <summary>
    /// The last full tick for which a snapshot backup is avaiable. Only present on the client world
    /// </summary>
    internal struct GhostSnapshotLastBackupTick : IComponentData
    {
        public NetworkTick Value;
    }

    internal struct GhostPredictionHistoryState : IComponentData
    {
        public NativeParallelHashMap<ulong, System.IntPtr>.ReadOnly PredictionState;
    }

    /// <summary>
    /// A system used to make a backup of the current predicted state, right after the last full (not fractional)
    /// tick in a prediction loop for a frame has been completed.
    /// The backup does a memcopy of all ghost components (into a separate memory area connected to the chunk).
    /// The backup is used to restore the last full tick, to continue prediction when no new data has arrived.
    /// Note: When this happens, only the fields which are actually serialized as part of the snapshot are copied back,
    /// not the full component. Thus, preserving any non-GhostField state.
    /// The backup data is also used to:
    /// - Detect errors in the prediction.
    /// - To add smoothing of predicted values.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup), OrderLast = true)]
    [BurstCompile]
    public unsafe partial struct GhostPredictionHistorySystem : ISystem, ISystemStartStop
    {
        struct PredictionStateEntry
        {
            public ArchetypeChunk chunk;
            public System.IntPtr data;
        }

        NativeParallelHashMap<ulong, System.IntPtr> m_PredictionState;
        NativeParallelHashMap<ulong, int> m_StillUsedPredictionState;
        NativeQueue<PredictionStateEntry> m_NewPredictionState;
        NativeQueue<PredictionStateEntry> m_UpdatedPredictionState;
        EntityQuery m_PredictionQuery;

        ComponentTypeHandle<GhostInstance> m_GhostComponentHandle;
        ComponentTypeHandle<GhostType> m_GhostTypeComponentHandle;
        ComponentTypeHandle<PreSpawnedGhostIndex> m_PreSpawnedGhostIndexHandle;
        BufferTypeHandle<LinkedEntityGroup> m_LinkedEntityGroupHandle;
        EntityTypeHandle m_EntityTypeHandle;

        BufferLookup<GhostComponentSerializer.State> m_GhostComponentSerializerStateFromEntity;
        BufferLookup<GhostCollectionPrefabSerializer> m_GhostCollectionPrefabSerializerFromEntity;
        BufferLookup<GhostCollectionComponentIndex> m_GhostCollectionComponentIndexFromEntity;
        BufferLookup<GhostCollectionPrefab> m_GhostCollectionPrefabFromEntity;
        
        DynamicTypeList m_DynamicTypeList;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_PredictionState = new NativeParallelHashMap<ulong, System.IntPtr>(128, Allocator.Persistent);
            m_StillUsedPredictionState = new NativeParallelHashMap<ulong, int>(128, Allocator.Persistent);
            m_NewPredictionState = new NativeQueue<PredictionStateEntry>(Allocator.Persistent);
            m_UpdatedPredictionState = new NativeQueue<PredictionStateEntry>(Allocator.Persistent);
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PredictedGhost, GhostInstance>();
            m_PredictionQuery = state.GetEntityQuery(builder);

            state.RequireForUpdate<GhostCollection>();

            m_GhostComponentHandle = state.GetComponentTypeHandle<GhostInstance>(true);
            m_GhostTypeComponentHandle = state.GetComponentTypeHandle<GhostType>(true);
            m_PreSpawnedGhostIndexHandle = state.GetComponentTypeHandle<PreSpawnedGhostIndex>(true);
            m_LinkedEntityGroupHandle = state.GetBufferTypeHandle<LinkedEntityGroup>(true);
            m_EntityTypeHandle = state.GetEntityTypeHandle();

            m_GhostComponentSerializerStateFromEntity = state.GetBufferLookup<GhostComponentSerializer.State>(true);
            m_GhostCollectionPrefabSerializerFromEntity = state.GetBufferLookup<GhostCollectionPrefabSerializer>(true);
            m_GhostCollectionComponentIndexFromEntity = state.GetBufferLookup<GhostCollectionComponentIndex>(true);
            m_GhostCollectionPrefabFromEntity = state.GetBufferLookup<GhostCollectionPrefab>(true);

            var atype = new NativeArray<ComponentType>(1, Allocator.Temp);
            atype[0] = ComponentType.ReadWrite<GhostPredictionHistoryState>();
            var historySingleton = state.EntityManager.CreateEntity(state.EntityManager.CreateArchetype(atype));
            FixedString64Bytes singletonName = "GhostPredictionHistoryState-Singleton";
            state.EntityManager.SetName(historySingleton, singletonName);
            // Declare that we are writing to GhostPredictionHistoryState, so we depend on all readers of this singleton during OnUpdate
            SystemAPI.GetSingletonRW<GhostPredictionHistoryState>().ValueRW.PredictionState = m_PredictionState.AsReadOnly();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            var values = m_PredictionState.GetValueArray(Allocator.Temp);
            for (int i = 0; i < values.Length; ++i)
            {
                UnsafeUtility.Free((void*)values[i], Allocator.Persistent);
            }
            m_PredictionState.Dispose();
            m_StillUsedPredictionState.Dispose();
            m_NewPredictionState.Dispose();
            m_UpdatedPredictionState.Dispose();
        }

        [BurstCompile]
        public void OnStartRunning(ref SystemState state)
        {
            if (m_DynamicTypeList.Length > 0)
            {
                // Already initialized
                return;
            }
            var ghostComponentCollection = SystemAPI.GetSingletonBuffer<GhostCollectionComponentType>();
            DynamicTypeList.PopulateList(ref state, ghostComponentCollection, true, ref m_DynamicTypeList);
        }

        [BurstCompile]
        public void OnStopRunning(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
			// Save all full prediction ticks
            if (!networkTime.IsInPredictionLoop || networkTime.IsPartialTick)
                return;
            SystemAPI.SetSingleton(new GhostSnapshotLastBackupTick { Value = networkTime.ServerTick });

            var predictionState = m_PredictionState;
            var newPredictionState = m_NewPredictionState;
            var stillUsedPredictionState = m_StillUsedPredictionState;
            var updatedPredictionState = m_UpdatedPredictionState;

            var setCapacityJob = new SetCapacityJob
            {
                predictionState = predictionState,
                stillUsedPredictionState = stillUsedPredictionState,
            };
            state.Dependency = setCapacityJob.Schedule(state.Dependency);

            m_GhostComponentHandle.Update(ref state);
            m_GhostTypeComponentHandle.Update(ref state);
            m_PreSpawnedGhostIndexHandle.Update(ref state);
            m_LinkedEntityGroupHandle.Update(ref state);
            m_EntityTypeHandle.Update(ref state);
            m_GhostComponentSerializerStateFromEntity.Update(ref state);
            m_GhostCollectionPrefabSerializerFromEntity.Update(ref state);
            m_GhostCollectionComponentIndexFromEntity.Update(ref state);
            m_GhostCollectionPrefabFromEntity.Update(ref state);
            m_DynamicTypeList.Update(ref state);
            
            var backupJob = new PredictionBackupJob
            {
                DynamicTypeList = m_DynamicTypeList,
                
                predictionState = predictionState,
                stillUsedPredictionState = stillUsedPredictionState.AsParallelWriter(),
                newPredictionState = newPredictionState.AsParallelWriter(),
                updatedPredictionState = updatedPredictionState.AsParallelWriter(),
                ghostComponentType = m_GhostComponentHandle,
                ghostType = m_GhostTypeComponentHandle,
                prespawnIndexType = m_PreSpawnedGhostIndexHandle,
                entityType = m_EntityTypeHandle,

                GhostCollectionSingleton = SystemAPI.GetSingletonEntity<GhostCollection>(),
                GhostComponentCollectionFromEntity = m_GhostComponentSerializerStateFromEntity,
                GhostTypeCollectionFromEntity = m_GhostCollectionPrefabSerializerFromEntity,
                GhostComponentIndexFromEntity = m_GhostCollectionComponentIndexFromEntity,
                GhostPrefabCollectionFromEntity = m_GhostCollectionPrefabFromEntity,

                childEntityLookup = state.GetEntityStorageInfoLookup(),
                linkedEntityGroupType = m_LinkedEntityGroupHandle,
				
				serverTick = networkTime.ServerTick,
            };

            state.Dependency = backupJob.ScheduleParallelByRef(m_PredictionQuery, state.Dependency);

            var cleanupJob = new CleanupPredictionStateJob
            {
                predictionState = predictionState,
                stillUsedPredictionState = stillUsedPredictionState,
                newPredictionState = newPredictionState,
                updatedPredictionState = updatedPredictionState
            };
            state.Dependency = cleanupJob.Schedule(state.Dependency);
        }

        [BurstCompile]
        struct CleanupPredictionStateJob : IJob
        {
            public NativeParallelHashMap<ulong, System.IntPtr> predictionState;
            [ReadOnly] public NativeParallelHashMap<ulong, int> stillUsedPredictionState;
            public NativeQueue<PredictionStateEntry> newPredictionState;
            public NativeQueue<PredictionStateEntry> updatedPredictionState;
            public void Execute()
            {
                var keys = predictionState.GetKeyArray(Allocator.Temp);
                for (int i = 0; i < keys.Length; ++i)
                {
                    if (!stillUsedPredictionState.TryGetValue(keys[i], out var temp))
                    {
                        // Free the memory and remove the chunk from the lookup
                        predictionState.TryGetValue(keys[i], out var alloc);
                        UnsafeUtility.Free((void*)alloc, Allocator.Persistent);
                        predictionState.Remove(keys[i]);
                    }
                }
                while (newPredictionState.TryDequeue(out var newState))
                {
                    if (!predictionState.TryAdd(newState.chunk.SequenceNumber, newState.data))
                    {
                        // Remove the old value, free it and add the new one - this happens when a chunk is reused too quickly
                        predictionState.TryGetValue(newState.chunk.SequenceNumber, out var alloc);
                        UnsafeUtility.Free((void*)alloc, Allocator.Persistent);
                        predictionState.Remove(newState.chunk.SequenceNumber);
                        // And add it again
                        predictionState.TryAdd(newState.chunk.SequenceNumber, newState.data);
                    }
                }
                while (updatedPredictionState.TryDequeue(out var updatedState))
                {
                    if(!predictionState.ContainsKey(updatedState.chunk.SequenceNumber))
                        throw new InvalidOperationException($"Prediction backup state has been updated but is not present in the map.");
                    predictionState[updatedState.chunk.SequenceNumber] = updatedState.data;
                }
            }
        }

        [BurstCompile]
        struct SetCapacityJob : IJob
        {
            public NativeParallelHashMap<ulong, System.IntPtr> predictionState;
            public NativeParallelHashMap<ulong, int> stillUsedPredictionState;
            public void Execute()
            {
                stillUsedPredictionState.Clear();
                if (stillUsedPredictionState.Capacity < predictionState.Capacity)
                    stillUsedPredictionState.Capacity = predictionState.Capacity;
            }
        }

        [BurstCompile]
        struct PredictionBackupJob : IJobChunk
        {
            public DynamicTypeList DynamicTypeList;

            [ReadOnly]public NativeParallelHashMap<ulong, System.IntPtr> predictionState;
            public NativeParallelHashMap<ulong, int>.ParallelWriter stillUsedPredictionState;
            public NativeQueue<PredictionStateEntry>.ParallelWriter newPredictionState;
            public NativeQueue<PredictionStateEntry>.ParallelWriter updatedPredictionState;
            [ReadOnly] public ComponentTypeHandle<GhostInstance> ghostComponentType;
            [ReadOnly] public ComponentTypeHandle<GhostType> ghostType;
            [ReadOnly] public ComponentTypeHandle<PreSpawnedGhostIndex> prespawnIndexType;
            [ReadOnly] public EntityTypeHandle entityType;

            public Entity GhostCollectionSingleton;
            [ReadOnly] public BufferLookup<GhostComponentSerializer.State> GhostComponentCollectionFromEntity;
            [ReadOnly] public BufferLookup<GhostCollectionPrefabSerializer> GhostTypeCollectionFromEntity;
            [ReadOnly] public BufferLookup<GhostCollectionComponentIndex> GhostComponentIndexFromEntity;
            [ReadOnly] public BufferLookup<GhostCollectionPrefab> GhostPrefabCollectionFromEntity;


            [ReadOnly] public EntityStorageInfoLookup childEntityLookup;
            [ReadOnly] public BufferTypeHandle<LinkedEntityGroup> linkedEntityGroupType;

			public NetworkTick serverTick;

            const GhostSendType requiredSendMask = GhostSendType.OnlyPredictedClients;

            //Sum up all the dynamic buffers raw data content size. Each buffer content size is aligned to 16 bytes
            private int GetChunkBuffersDataSize(GhostCollectionPrefabSerializer typeData, ArchetypeChunk chunk,
                DynamicComponentTypeHandle* ghostChunkComponentTypesPtr, int ghostChunkComponentTypesLength, DynamicBuffer<GhostCollectionComponentIndex> GhostComponentIndex, DynamicBuffer<GhostComponentSerializer.State> GhostComponentCollection)
            {
                int numBaseComponents = typeData.NumComponents - typeData.NumChildComponents;
                int bufferTotalSize = 0;
                int baseOffset = typeData.FirstComponent;
                for (int comp = 0; comp < numBaseComponents; ++comp)
                {
                    int compIdx = GhostComponentIndex[baseOffset + comp].ComponentIndex;
                    int serializerIdx = GhostComponentIndex[baseOffset + comp].SerializerIndex;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    if (compIdx >= ghostChunkComponentTypesLength)
                        throw new System.InvalidOperationException($"Component index {comp} (of numBaseComponents: {numBaseComponents}) out of range for root component in method 'GetChunkBuffersDataSize'. ghostChunkComponentTypesLength is {ghostChunkComponentTypesLength}.");
#endif
                    if ((GhostComponentIndex[baseOffset + comp].SendMask & requiredSendMask) == 0)
                        continue;

                    ref readonly var ghostSerializer = ref GhostComponentCollection.ElementAtRO(serializerIdx);
                    if (!ghostSerializer.ComponentType.IsBuffer)
                        continue;

                    if (chunk.Has(ref ghostChunkComponentTypesPtr[compIdx]))
                    {
                        var bufferData = chunk.GetUntypedBufferAccessor(ref ghostChunkComponentTypesPtr[compIdx]);
                        for (int i = 0; i < bufferData.Length; ++i)
                        {
                            bufferTotalSize += bufferData.GetBufferCapacity(i) * ghostSerializer.ComponentSize;
                        }
                        bufferTotalSize = GhostComponentSerializer.SnapshotSizeAligned(bufferTotalSize);
                    }
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
                            throw new System.InvalidOperationException($"Component index {comp} (of numBaseComponents: {numBaseComponents}) out of range for child component in method 'GetChunkBuffersDataSize'. ghostChunkComponentTypesLength is {ghostChunkComponentTypesLength}.");
#endif
                        if ((GhostComponentIndex[baseOffset + comp].SendMask & requiredSendMask) == 0)
                            continue;

                        ref readonly var ghostSerializer = ref GhostComponentCollection.ElementAtRO(serializerIdx);
                        if (!ghostSerializer.ComponentType.IsBuffer)
                            continue;

                        for (int ent = 0, chunkEntityCount = chunk.Count; ent < chunkEntityCount; ++ent)
                        {
                            var linkedEntityGroup = linkedEntityGroupAccessor[ent];
                            var childEnt = linkedEntityGroup[GhostComponentIndex[baseOffset + comp].EntityIndex].Value;
                            if (childEntityLookup.TryGetValue(childEnt, out var childChunk) && childChunk.Chunk.Has(ref ghostChunkComponentTypesPtr[compIdx]))
                            {
                                var bufferData = childChunk.Chunk.GetUntypedBufferAccessor(ref ghostChunkComponentTypesPtr[compIdx]);
                                bufferTotalSize += bufferData.GetBufferCapacity(childChunk.IndexInChunk) * ghostSerializer.ComponentSize;
                            }
                            bufferTotalSize = GhostComponentSerializer.SnapshotSizeAligned(bufferTotalSize);
                        }
                    }
                }

                return bufferTotalSize;
            }

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // This job is not written to support queries with enableable component types.
                Assert.IsFalse(useEnabledMask);

                DynamicComponentTypeHandle* ghostChunkComponentTypesPtr = DynamicTypeList.GetData();
                int ghostChunkComponentTypesLength = DynamicTypeList.Length;
                var GhostTypeCollection = GhostTypeCollectionFromEntity[GhostCollectionSingleton];
                var GhostComponentIndex = GhostComponentIndexFromEntity[GhostCollectionSingleton];
                var GhostComponentCollection = GhostComponentCollectionFromEntity[GhostCollectionSingleton];
                var GhostPrefabCollection = GhostPrefabCollectionFromEntity[GhostCollectionSingleton];

                var ghostComponents = chunk.GetNativeArray(ref ghostComponentType);
                var ghostTypes = chunk.GetNativeArray(ref ghostType);
                int ghostTypeId = ghostComponents.GetFirstGhostTypeId();
                if (ghostTypeId < 0)
                {
                    if(!chunk.Has(ref prespawnIndexType))
                        return;

                    //Prespawn chunk that hasn't been received/processed yet. Since it is predicted we still
                    //need to store the entities in the history buffer. This is why we are resolving the the ghost type
                    //here
                    for (ghostTypeId = 0; ghostTypeId < GhostTypeCollection.Length; ++ghostTypeId)
                    {
                        if (GhostPrefabCollection[ghostTypeId].GhostType == ghostTypes[0])
                            break;
                    }

                    if (ghostTypeId < 0)
                        throw new InvalidOperationException($"Cannot find ghostTypeId in GhostPrefabCollection as expected to match {ghostTypes[0]} but didn't. (GhostPrefabCollection.Length: {GhostPrefabCollection.Length}).");
                }

                var typeData = GhostTypeCollection[ghostTypeId];

                var singleEntitySize = UnsafeUtility.SizeOf<Entity>();
                int baseOffset = typeData.FirstComponent;
                int predictionOwnerOffset = -1;
                var ghostOwnerTypeIndex = TypeManager.GetTypeIndex<GhostOwner>();
				var predictionBackupIndex = (int)(serverTick.TickIndexForValidTick % PredictionBackupState.PredictionHistorySize);
                if (!predictionState.TryGetValue(chunk.SequenceNumber, out var state) ||
                    (*(PredictionBackupState*)state).ghostType != ghostTypeId ||
                    (*(PredictionBackupState*)state).entityCapacity != chunk.Capacity)
                {
                    int dataSize = 0;
                    int enabledBits = 0;
                    // Sum up the size of all components rounded up
                    // RULES:
                    // - if the component/buffer send mask not match PredictedClient neither the data, nor the enable bits are present in the backup.
                    // - if the component/buffer replicated enablebits, the bits are present in the backup
                    // - if the component has no ghost fields the data not present in the backup

                    for (int comp = 0; comp < typeData.NumComponents; ++comp)
                    {
                        int compIdx = GhostComponentIndex[baseOffset + comp].ComponentIndex;
                        int serializerIdx = GhostComponentIndex[baseOffset + comp].SerializerIndex;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        if (compIdx >= ghostChunkComponentTypesLength)
                            throw new System.InvalidOperationException($"Component index {comp} (of numBaseComponents: {typeData.NumComponents}) out of range in method 'Execute'. ghostChunkComponentTypesLength is {ghostChunkComponentTypesLength}.");
#endif
                        if ((GhostComponentIndex[baseOffset + comp].SendMask&requiredSendMask) == 0)
                            continue;

                        ref readonly var ghostSerializer = ref GhostComponentCollection.ElementAtRO(serializerIdx);
                        if (ghostSerializer.SerializesEnabledBit != 0)
                            ++enabledBits;

                        if (!ghostSerializer.HasGhostFields)
                            continue;

                        if (ghostSerializer.ComponentType.TypeIndex == ghostOwnerTypeIndex)
                            predictionOwnerOffset = dataSize;

                        //for buffers we store a a pair of uint:
                        // uint length: the num of elements
                        // uint backupDataOffset: the start position in the backup buffer
                        if (!ghostSerializer.ComponentType.IsBuffer)
                            dataSize += PredictionBackupState.GetDataSize(
                                ghostSerializer.ComponentSize, chunk.Capacity);
                        else
                            dataSize += PredictionBackupState.GetDataSize(GhostComponentSerializer.DynamicBufferComponentSnapshotSize, chunk.Capacity);
                    }

                    //compute the space necessary to store the dynamic buffers data for the chunk
                    int buffersDataCapacity = 0;
                    if (typeData.NumBuffers > 0)
                        buffersDataCapacity = GetChunkBuffersDataSize(typeData, chunk, ghostChunkComponentTypesPtr, ghostChunkComponentTypesLength, GhostComponentIndex, GhostComponentCollection);

                    // Chunk does not exist in the history, or has changed ghost type in which case we need to create a new one
                    state = PredictionBackupState.AllocNew(ghostTypeId, enabledBits, typeData.NumComponents, dataSize, chunk.Capacity, buffersDataCapacity, predictionOwnerOffset);
                    newPredictionState.Enqueue(new PredictionStateEntry{chunk = chunk, data = state});
                }
                else
                {
                    stillUsedPredictionState.TryAdd(chunk.SequenceNumber, 1);
                    if (typeData.NumBuffers > 0)
                    {
                        //resize the backup state to fit the dynamic buffers contents
                        var buffersDataCapacity = GetChunkBuffersDataSize(typeData, chunk, ghostChunkComponentTypesPtr, ghostChunkComponentTypesLength, GhostComponentIndex, GhostComponentCollection);
                        int bufferBackupDataCapacity = PredictionBackupState.GetBufferDataCapacity(state);
                        if (bufferBackupDataCapacity < buffersDataCapacity)
                        {
                            var dataSize = ((PredictionBackupState*)state)->dataSize;
                            var enabledBits = ((PredictionBackupState*)state)->enabledBits;
                            var ghostOwnerOffset = ((PredictionBackupState*)state)->ghostOwnerOffset;
                            state = PredictionBackupState.Realloc(state, ghostTypeId, enabledBits, typeData.NumComponents, dataSize, chunk.Capacity, buffersDataCapacity, ghostOwnerOffset);
							updatedPredictionState.Enqueue(new PredictionStateEntry { chunk = chunk, data = state });
						}
                    }
                }
                Entity* entities = PredictionBackupState.GetEntities(state, predictionBackupIndex);
                var srcEntities = chunk.GetNativeArray(entityType).GetUnsafeReadOnlyPtr();
                UnsafeUtility.MemCpy(entities, srcEntities, chunk.Count * singleEntitySize);
                if (chunk.Count < chunk.Capacity)
                    UnsafeUtility.MemClear(entities + chunk.Count, (chunk.Capacity - chunk.Count) * singleEntitySize);

                byte* dataPtr = PredictionBackupState.GetData(state, predictionBackupIndex);
                byte* bufferBackupDataPtr = PredictionBackupState.GetBufferDataPtr(state, predictionBackupIndex);
                ulong* enabledBitPtr = PredictionBackupState.GetEnabledBits(state, predictionBackupIndex);

                int numBaseComponents = typeData.NumComponents - typeData.NumChildComponents;
                int bufferBackupDataOffset = 0;
                for (int comp = 0; comp < numBaseComponents; ++comp)
                {
                    int compIdx = GhostComponentIndex[baseOffset + comp].ComponentIndex;
                    int serializerIdx = GhostComponentIndex[baseOffset + comp].SerializerIndex;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    if (compIdx >= ghostChunkComponentTypesLength)
                        throw new System.InvalidOperationException($"Component index {comp} (of numBaseComponents: {numBaseComponents}) out of range for root component in method 'Execute'. ghostChunkComponentTypesLength is {ghostChunkComponentTypesLength}.");
#endif
                    if ((GhostComponentIndex[baseOffset + comp].SendMask&requiredSendMask) == 0)
                        continue;
                    uint chunkVersion = chunk.GetChangeVersion(ref ghostChunkComponentTypesPtr[compIdx]);
                    ref readonly var ghostSerializer = ref GhostComponentCollection.ElementAtRO(serializerIdx);
                    var compSize = ghostSerializer.ComponentType.IsBuffer
                        ? GhostComponentSerializer.DynamicBufferComponentSnapshotSize
                        : ghostSerializer.ComponentSize;

                    if (ghostSerializer.SerializesEnabledBit != 0)
                    {
                        var handle = ghostChunkComponentTypesPtr[compIdx];
                        var bitArray = chunk.GetEnableableBits(ref handle);
                        UnsafeUtility.MemCpy(enabledBitPtr, &bitArray, ((chunk.Count+63)&(~63))/8);

                        enabledBitPtr = PredictionBackupState.GetNextEnabledBits(enabledBitPtr, chunk.Capacity, predictionBackupIndex);
                    }

                    // Note that `HasGhostFields` reads the `SnapshotSize` of this type, BUT we're saving the entire component.
                    // The reason we use this is: Why bother memcopy-ing the entire component state, if we're never actually going to be writing any data back?
                    // I.e. Only the GhostFields will be written back anyway.
                    if (!ghostSerializer.HasGhostFields)
                        continue;

                    if (!chunk.Has(ref ghostChunkComponentTypesPtr[compIdx]))
                    {
                        UnsafeUtility.MemClear(dataPtr, chunk.Count * compSize);
                    }
                    else if (!ghostSerializer.ComponentType.IsBuffer)
                    {
                        var compData = (byte*) chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref ghostChunkComponentTypesPtr[compIdx], compSize).GetUnsafeReadOnlyPtr();
                        UnsafeUtility.MemCpy(dataPtr, compData, chunk.Count * compSize);
                    }
                    else
                    {
                        var bufferData = chunk.GetUntypedBufferAccessor(ref ghostChunkComponentTypesPtr[compIdx]);
                        var bufElemSize = ghostSerializer.ComponentSize;
                        //Use local variable to iterate and set the buffer offset and length. The dataptr must be
                        //advanced "per chunk" to the next correct position
                        var tempDataPtr = dataPtr;
                        for (int i = 0; i < bufferData.Length; ++i)
                        {
                            //Retrieve an copy each buffer data. Set size and offset in the backup buffer in the component backup
                            var bufferPtr = bufferData.GetUnsafeReadOnlyPtrAndLength(i, out var size);
                            ((int*) tempDataPtr)[0] = size;
                            ((int*) tempDataPtr)[1] = bufferBackupDataOffset;
                            if (size > 0)
                                UnsafeUtility.MemCpy(bufferBackupDataPtr + bufferBackupDataOffset, (byte*) bufferPtr, size * bufElemSize);
                            bufferBackupDataOffset += size * bufElemSize;
                            tempDataPtr += compSize;
                        }

                        bufferBackupDataOffset = GhostComponentSerializer.SnapshotSizeAligned(bufferBackupDataOffset);
                    }
                    dataPtr = PredictionBackupState.GetNextData(dataPtr, compSize, chunk.Capacity);
                }
                if (typeData.NumChildComponents > 0)
                {
                    var linkedEntityGroupAccessor = chunk.GetBufferAccessor(ref linkedEntityGroupType);
                    //for child component we store a one version entry, per component type for each entity in the chunk
                    //the layout looks like
                    //ChildComp1     ChildComp2
                    //e1, e2 .. en | e1, e2 .. en
                    for (int comp = numBaseComponents; comp < typeData.NumComponents; ++comp)
                    {
                        int compIdx = GhostComponentIndex[baseOffset + comp].ComponentIndex;
                        int serializerIdx = GhostComponentIndex[baseOffset + comp].SerializerIndex;
                        var handle = ghostChunkComponentTypesPtr[compIdx];
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        if (compIdx >= ghostChunkComponentTypesLength)
                            throw new System.InvalidOperationException($"Component index {comp} (of numBaseComponents: {numBaseComponents}) out of range for child component in method 'Execute'. ghostChunkComponentTypesLength is {ghostChunkComponentTypesLength}.");
#endif
                        if ((GhostComponentIndex[baseOffset + comp].SendMask&requiredSendMask) == 0)
                            continue;

                        ref readonly var ghostSerializer = ref GhostComponentCollection.ElementAtRO(serializerIdx);
                        if (ghostSerializer.SerializesEnabledBit != 0)
                        {
                            for (int rootEnt = 0, chunkEntityCount = chunk.Count; rootEnt < chunkEntityCount; ++rootEnt)
                            {
                                ulong isSet = 0;
                                var linkedEntityGroup = linkedEntityGroupAccessor[rootEnt];
                                var childEnt = linkedEntityGroup[GhostComponentIndex[baseOffset + comp].EntityIndex].Value;
                                if (childEntityLookup.TryGetValue(childEnt, out var childChunk))
                                {
                                    var arr = childChunk.Chunk.GetEnableableBits(ref handle);
                                    var bits = new UnsafeBitArray(&arr, sizeof(v128));
                                    isSet = bits.IsSet(childChunk.IndexInChunk) ? 1u : 0u;
                                }
                                enabledBitPtr[rootEnt>>6] &= ~(1ul<<(rootEnt&0x3f));
                                enabledBitPtr[rootEnt>>6] |= (isSet<<(rootEnt&0x3f));
                            }
                            enabledBitPtr = PredictionBackupState.GetNextEnabledBits(enabledBitPtr, chunk.Capacity, predictionBackupIndex);
                        }
                        var isBuffer = ghostSerializer.ComponentType.IsBuffer;
                        var compSize = isBuffer ? GhostComponentSerializer.DynamicBufferComponentSnapshotSize : ghostSerializer.ComponentSize;

                        if (!ghostSerializer.HasGhostFields)
                            continue;

                        if (!ghostSerializer.ComponentType.IsBuffer)
                        {
                            //use a temporary for the iteration here. Otherwise when the dataptr is offset for the chunk, we
                            //end up in the wrong position
                            var tempDataPtr = dataPtr;

                            for (int rootEnt = 0, chunkEntityCount = chunk.Count; rootEnt < chunkEntityCount; ++rootEnt)
                            {
                                var linkedEntityGroup = linkedEntityGroupAccessor[rootEnt];
                                var childEnt = linkedEntityGroup[GhostComponentIndex[baseOffset + comp].EntityIndex].Value;
                                if (childEntityLookup.TryGetValue(childEnt, out var childChunk) && childChunk.Chunk.Has(ref ghostChunkComponentTypesPtr[compIdx]))
                                {
                                    var compData = (byte*) childChunk.Chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref ghostChunkComponentTypesPtr[compIdx], compSize).GetUnsafeReadOnlyPtr();
                                    UnsafeUtility.MemCpy(tempDataPtr, compData + childChunk.IndexInChunk * compSize, compSize);
                                }
                                else
                                {
                                    UnsafeUtility.MemClear(tempDataPtr, compSize);
                                }
                                tempDataPtr += compSize;
                            }
                        }
                        else
                        {
                            var bufElemSize = ghostSerializer.ComponentSize;
                            var tempDataPtr = dataPtr;

                            for (int rootEnt = 0, chunkEntityCount = chunk.Count; rootEnt < chunkEntityCount; ++rootEnt)
                            {
                                var linkedEntityGroup = linkedEntityGroupAccessor[rootEnt];
                                var childEnt = linkedEntityGroup[GhostComponentIndex[baseOffset + comp].EntityIndex].Value;
                                if (childEntityLookup.TryGetValue(childEnt, out var childChunk) && childChunk.Chunk.Has(ref ghostChunkComponentTypesPtr[compIdx]))
                                {
                                    var bufferData = childChunk.Chunk.GetUntypedBufferAccessor(ref ghostChunkComponentTypesPtr[compIdx]);
                                    //Retrieve an copy each buffer data. Set size and offset in the backup buffer in the component backup
                                    var bufferPtr = bufferData.GetUnsafeReadOnlyPtrAndLength(childChunk.IndexInChunk, out var size);
                                    ((int*) tempDataPtr)[0] = size;
                                    ((int*) tempDataPtr)[1] = bufferBackupDataOffset;
                                    if (size > 0)
                                        UnsafeUtility.MemCpy(bufferBackupDataPtr + bufferBackupDataOffset, (byte*) bufferPtr, size * bufElemSize);
                                    bufferBackupDataOffset += size * bufElemSize;
                                }
                                else
                                {
                                    //reset the entry to 0. Don't use memcpy in this case (is faster this way)
                                    ((long*) tempDataPtr)[0] = 0;
                                }

                                tempDataPtr += compSize;
                            }

                            bufferBackupDataOffset = GhostComponentSerializer.SnapshotSizeAligned(bufferBackupDataOffset);
                        }

                        dataPtr = PredictionBackupState.GetNextData(dataPtr, compSize, chunk.Capacity);
                    }
                }
            }
        }
    }
}
