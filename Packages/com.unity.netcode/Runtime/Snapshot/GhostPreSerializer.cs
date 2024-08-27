using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System;
using System.Diagnostics;
using Unity.Assertions;
using Unity.Burst.Intrinsics;
using Unity.NetCode.LowLevel.Unsafe;
using Unity.Jobs;

namespace Unity.NetCode
{

    /// <summary>
    /// Adding this component to a ghost will trigger pre-serialization for that ghost.
    /// Pre-serialization means that part of the serialization process happens before
    /// the regular serialization pass and can be done once for all connections.
    /// This can save some CPU time if the the ghost will generally be sent to more than
    /// one player every frame and it contains complex serialization (serialized data on
    /// child entities or buffers).
    /// </summary>
    public struct PreSerializedGhost : IComponentData
    {}

    internal unsafe struct SnapshotPreSerializeData
    {
        public void* Data;
        public int DynamicSize;
        public int Capacity;
        public int DynamicCapacity;
    }
    internal unsafe struct GhostPreSerializer : IDisposable
    {
        public NativeParallelHashMap<ArchetypeChunk, SnapshotPreSerializeData> SnapshotData;
        private NativeParallelHashMap<ArchetypeChunk, SnapshotPreSerializeData> PreviousSnapshotData;
        private EntityQuery m_Query;

        public GhostPreSerializer(EntityQuery query)
        {
            SnapshotData = new NativeParallelHashMap<ArchetypeChunk, SnapshotPreSerializeData>(1024, Allocator.Persistent);
            PreviousSnapshotData = new NativeParallelHashMap<ArchetypeChunk, SnapshotPreSerializeData>(1024, Allocator.Persistent);
            m_Query = query;
        }
        void CleanupSnapshotData()
        {
            // FIXME: this could be a job too
            var chunks = PreviousSnapshotData.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < chunks.Length; ++i)
            {
                if (!SnapshotData.ContainsKey(chunks[i]))
                {
                    // Free the data stored for this key in PreviousSnapshotData
                    PreviousSnapshotData.TryGetValue(chunks[i], out var snapshot);
                    UnsafeUtility.Free(snapshot.Data, Allocator.Persistent);
                }
            }
            PreviousSnapshotData.Clear();
            var temp = SnapshotData;
            SnapshotData = PreviousSnapshotData;
            PreviousSnapshotData = temp;
        }
        public void Dispose()
        {
            CleanupSnapshotData();
            var snapshots = PreviousSnapshotData.GetValueArray(Allocator.Temp);
            for (int i = 0; i < snapshots.Length; ++i)
                UnsafeUtility.Free(snapshots[i].Data, Allocator.Persistent);
            PreviousSnapshotData.Dispose();
            SnapshotData.Dispose();
        }

        public JobHandle Schedule(JobHandle dependency,
            BufferLookup<GhostComponentSerializer.State> GhostComponentCollectionFromEntity,
            BufferLookup<GhostCollectionPrefabSerializer> GhostTypeCollectionFromEntity,
            BufferLookup<GhostCollectionComponentIndex> GhostComponentIndexFromEntity,
            Entity GhostCollectionSingleton,
            BufferLookup<GhostCollectionPrefab> GhostCollectionFromEntity,
            BufferTypeHandle<LinkedEntityGroup> linkedEntityGroupType,
            EntityStorageInfoLookup childEntityLookup,
            ComponentTypeHandle<GhostInstance> ghostComponentType,
            ComponentTypeHandle<GhostType> ghostTypeComponentType,
            EntityTypeHandle entityType,
            ComponentLookup<GhostInstance> ghostFromEntity,
            NativeArray<ConnectionStateData> connectionStateData,
            NetDebug netDebug,
            NetworkTick currentTick,
            int useCustomSerializer,
            ref SystemState system,
            DynamicBuffer<GhostCollectionComponentType> ghostCollection)
        {
            CleanupSnapshotData();
            var job = new GhostPreSerializeJob
            {
                SnapshotData = SnapshotData.AsParallelWriter(),
                PreviousSnapshotData = PreviousSnapshotData,
                GhostComponentCollectionFromEntity = GhostComponentCollectionFromEntity,
                GhostTypeCollectionFromEntity = GhostTypeCollectionFromEntity,
                GhostComponentIndexFromEntity = GhostComponentIndexFromEntity,
                GhostCollectionSingleton = GhostCollectionSingleton,
                GhostCollectionFromEntity = GhostCollectionFromEntity,
                entityType = entityType,
                linkedEntityGroupType = linkedEntityGroupType,
                childEntityLookup = childEntityLookup,
                ghostComponentType = ghostComponentType,
                ghostTypeComponentType = ghostTypeComponentType,
                ghostFromEntity = ghostFromEntity,
                connectionStateData = connectionStateData,
                netDebug = netDebug,
                currentTick = currentTick,
                useCustomSerializer = useCustomSerializer
            };
            DynamicTypeList.PopulateList(ref system, ghostCollection, true, ref job.dynamicTypeList);
            return job.ScheduleParallelByRef(m_Query, dependency);
        }

        [BurstCompile]
        struct GhostPreSerializeJob : IJobChunk
        {
            [ReadOnly] public NativeParallelHashMap<ArchetypeChunk, SnapshotPreSerializeData> PreviousSnapshotData;
            [ReadOnly] public BufferLookup<GhostComponentSerializer.State> GhostComponentCollectionFromEntity;
            [ReadOnly] public BufferLookup<GhostCollectionPrefabSerializer> GhostTypeCollectionFromEntity;
            [ReadOnly] public BufferLookup<GhostCollectionComponentIndex> GhostComponentIndexFromEntity;
            [ReadOnly] public BufferLookup<GhostCollectionPrefab> GhostCollectionFromEntity;
            [ReadOnly] public BufferTypeHandle<LinkedEntityGroup> linkedEntityGroupType;
            [ReadOnly] public EntityStorageInfoLookup childEntityLookup;
            [ReadOnly] public ComponentTypeHandle<GhostInstance> ghostComponentType;
            [ReadOnly] public ComponentTypeHandle<GhostType> ghostTypeComponentType;
            [ReadOnly] public ComponentLookup<GhostInstance> ghostFromEntity;
            [ReadOnly] public NativeArray<ConnectionStateData> connectionStateData;
            [ReadOnly] public EntityTypeHandle entityType;

            public NetDebug netDebug;
            public NetworkTick currentTick;
            public NativeParallelHashMap<ArchetypeChunk, SnapshotPreSerializeData>.ParallelWriter SnapshotData;
            public Entity GhostCollectionSingleton;
            public DynamicTypeList dynamicTypeList;
            public int useCustomSerializer;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                if(connectionStateData.Length == 0)
                    return;
                var GhostTypeCollection = GhostTypeCollectionFromEntity[GhostCollectionSingleton];
                var ghosts = chunk.GetNativeArray(ref ghostComponentType);
                //I need to check if the ghost has been processed in order to serialize the chunk data.
                // Find the ghost type for this chunk
                var ghostType = ghosts[0].ghostType;
                // Pre spawned ghosts might not have a proper ghost type index yet, we calculate it here for pre spawns
                if (ghostType < 0)
                {
                    var GhostCollection = GhostCollectionFromEntity[GhostCollectionSingleton];
                    var ghostTypeComponent = chunk.GetNativeArray(ref ghostTypeComponentType)[0];
                    for (ghostType = 0; ghostType < GhostCollection.Length; ++ghostType)
                    {
                        if (GhostCollection[ghostType].GhostType == ghostTypeComponent)
                            break;
                    }
                    if(ghostType >= GhostTypeCollection.Length)
                    {
                        netDebug.LogError($"Could not find ghost type {(Hash128)ghostTypeComponent} in the GhostCollectionPrefab list.");
                        return;
                    }
                }
                //If the chunk is not a prespawn one and the ghost has invalid spanw tick means the chunk has been just spawned
                //and there was not enough information to process it. As such, the chunk will be skipped.
                else if(!ghosts[0].spawnTick.IsValid)
                    return;

                //The type is not present in the collection. While an edge case this is still possible in certain scenario
                if(ghostType >= GhostTypeCollection.Length)
                    return;

                //what if there are entities that are not valid (like spawn tick == 0)
                //We should never be able to reach this point with an invalid ghost if the server has run its update.
                var typeData = GhostTypeCollection[ghostType];
                int dynamicDataCapacity = 0;
                int dynamicDataHeaderSize = 0;

                var helper = new GhostSerializeHelper
                {
                    serializerState = new GhostSerializerState { GhostFromEntity = ghostFromEntity },
                    ghostChunkComponentTypesPtr = dynamicTypeList.GetData(),
                    GhostComponentIndex = GhostComponentIndexFromEntity[GhostCollectionSingleton],
                    GhostComponentCollection = GhostComponentCollectionFromEntity[GhostCollectionSingleton],
                    childEntityLookup = childEntityLookup,
                    linkedEntityGroupType = linkedEntityGroupType,
                    ghostChunkComponentTypesPtrLen = dynamicTypeList.Length,
                };

                if (typeData.NumBuffers != 0)
                {
                    // figure out how much data is required for the buffer dynamic data
                    dynamicDataCapacity = helper.GatherBufferSize(chunk, 0, typeData);
                    dynamicDataHeaderSize = GhostChunkSerializationState.GetDynamicDataHeaderSize(chunk.Capacity);
                }
                int snapshotDataCapacity = typeData.SnapshotSize * chunk.Capacity;
                // Determine the required allocation size
                if (!PreviousSnapshotData.TryGetValue(chunk, out var snapshot) || snapshot.Capacity != snapshotDataCapacity || snapshot.DynamicCapacity < dynamicDataCapacity)
                {
                    // Allocate a new snapshot
                    if (snapshot.Data != null)
                    {
                        UnsafeUtility.Free(snapshot.Data, Allocator.Persistent);
                    }
                    snapshot.Capacity = snapshotDataCapacity;
                    // Round up to an even number of kb
                    snapshot.DynamicCapacity = (dynamicDataCapacity + 1023) & (~1023);
                    snapshot.Data = UnsafeUtility.Malloc(snapshot.Capacity + snapshot.DynamicCapacity, 16, Allocator.Persistent);
                }
                snapshot.DynamicSize = dynamicDataCapacity;
                // Add to the new snapshot data lookup
                if (!SnapshotData.TryAdd(chunk, snapshot))
                {
                    netDebug.LogError("Could not register snapshot data for pre-serialization");
                    UnsafeUtility.Free(snapshot.Data, Allocator.Persistent);
                    return;
                }

                typeData.profilerMarker.Begin();
                int snapshotSize = typeData.SnapshotSize;
                int changeMaskUints = GhostComponentSerializer.ChangeMaskArraySizeInUInts(typeData.ChangeMaskBits);
                int enableableMaskUints = GhostComponentSerializer.ChangeMaskArraySizeInUInts(typeData.EnableableBits);
                int snapshotOffset = GhostComponentSerializer.SnapshotSizeAligned(sizeof(uint) + changeMaskUints*sizeof(uint) + enableableMaskUints*sizeof(uint));
                // Go through all entities and serialize the data to the snapshot store
                helper.snapshotPtr = (byte*)snapshot.Data;
                helper.snapshotOffset = snapshotOffset;
                helper.snapshotSize = snapshotSize;
                helper.changeMaskUints = changeMaskUints;
                if (typeData.NumBuffers != 0)
                {
                    //This require some explanation.
                    // The data layout for the pre-serialized ghost snapshot looks like this:
                    //
                    //   snapshot.Capacity   snapshot.DynamicCapacity
                    // [  SNAPSHOT DATA  ][      DYNAMIC DATA       ]
                    //
                    // The dynamic data will be copied/re-located by the GhostChunkSerializer
                    // inside the chunk DynamicSnapshotBuffer, right after the header.
                    //
                    //   chunk snapshot cap      chunk dynamic capacity
                    // [  SNAPSHOT DATA     ][ HEADER][    DYNAMIC DATA   ]
                    //
                    // Because of that the relative offset that we store in the snapshot data
                    // indicating where the dynamicbuffer contents start from, must be offset by the dynamic header size.
                    //
                    //  [SNAPSHOT DATA]
                    // ..  Buffer ...                Chunk Dynamic Data
                    //   Len, Offset                  [Header][CONTENTS]
                    //    X     |                                 |
                    //          |_________________________________|
                    //
                    // Because the pre-serialized data is actually stored right after the snapshot but
                    // the helper will write in memory at address snapshotDynamicPtr + dynamicSnapshotDataOffset,
                    // we are offsetting the start position of the buffer back by the header capacity
                    helper.snapshotDynamicPtr = (byte*)snapshot.Data + snapshot.Capacity - dynamicDataHeaderSize;
                    helper.dynamicSnapshotDataOffset = dynamicDataHeaderSize;
                    //The max capacity should also be larger (like it contains also the header) so all math make sense
                    helper.dynamicSnapshotCapacity = snapshot.DynamicCapacity + dynamicDataHeaderSize;
                }
                if (useCustomSerializer != 0 && typeData.CustomPreSerializer.Ptr.IsCreated)
                {
                    var context = new GhostPrefabCustomSerializer.Context
                    {
                        startIndex = 0,
                        endIndex = chunk.Count,
                        ghostType = ghostType,
                        childEntityLookup = helper.childEntityLookup,
                        serializerState = helper.serializerState,
                        snapshotDataPtr = (IntPtr)helper.snapshotPtr,
                        snapshotDynamicDataPtr = (IntPtr)helper.snapshotDynamicPtr,
                        snapshotOffset = helper.snapshotOffset,
                        snapshotStride = helper.snapshotSize,
                        dynamicDataOffset = helper.dynamicSnapshotDataOffset,
                        dynamicDataCapacity = helper.dynamicSnapshotCapacity,
                        ghostChunkComponentTypes = (IntPtr)helper.ghostChunkComponentTypesPtr,
                        linkedEntityGroupTypeHandle = helper.linkedEntityGroupType
                        // irrelevant data
                        // networkId = default,
                        // hasPreserializedData = default,
                        // entityStartBit = default,
                        // baselinePerEntityPtr = default,
                        // sameBaselinePerEntityPtr = default,
                        // dynamicDataSizePerEntityPtr = default,
                        // zeroBaseline = default,
                        // ghostInstances = default,
                    };
                    typeData.CustomPreSerializer.Ptr.Invoke(chunk, typeData, helper.GhostComponentIndex, ref context);
                }
                else
                {
                    helper.CopyChunkToSnapshot(chunk, typeData);
                }
                for (int ent = 0, chunkEntityCount = chunk.Count; ent < chunkEntityCount; ++ent)
                {
                    *(uint*)((byte*)snapshot.Data + snapshotSize * ent) = currentTick.SerializedData;
                }
                typeData.profilerMarker.End();
            }
        }
    }
}
