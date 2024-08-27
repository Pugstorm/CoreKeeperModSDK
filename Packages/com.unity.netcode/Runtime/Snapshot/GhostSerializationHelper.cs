using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.LowLevel.Unsafe;

namespace Unity.NetCode
{
    namespace LowLevel.Unsafe
    {
        // Internal serializer helper. Hold a bunch of serializer related data together
        [BurstCompile]
        unsafe struct GhostSerializeHelper
        {
            public byte* snapshotPtr;
            public byte* snapshotDynamicPtr;
            public int snapshotOffset;
            public int dynamicSnapshotDataOffset;
            public int snapshotSize;
            public int dynamicSnapshotCapacity;
            public int changeMaskUints;
            //Constant data
            [ReadOnly] public DynamicComponentTypeHandle* ghostChunkComponentTypesPtr;
            [ReadOnly] public DynamicBuffer<GhostCollectionComponentIndex> GhostComponentIndex;
            [ReadOnly] public DynamicBuffer<GhostComponentSerializer.State> GhostComponentCollection;
            [ReadOnly] public EntityStorageInfoLookup childEntityLookup;
            [ReadOnly] public BufferTypeHandle<LinkedEntityGroup> linkedEntityGroupType;
            public int ghostChunkComponentTypesPtrLen;
            public GhostSerializerState serializerState;

            public enum ClearOption
            {
                Clear = 0,
                DontClear
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private void CheckValidComponentIndex(int compIdx)
            {
                if (compIdx >= ghostChunkComponentTypesPtrLen)
                    throw new InvalidOperationException($"Component index out of range");
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private void CheckValidDynamicSnapshotOffset(in GhostComponentSerializer.State serializer, int maskSize, int bufferLen)
            {
                if ((dynamicSnapshotDataOffset + serializer.SnapshotSize * bufferLen) > dynamicSnapshotCapacity)
                    throw new InvalidOperationException("Overflow writing data to dynamic snapshot memory buffer");
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private void CheckValidSnapshotOffset(int compSnapshotSize)
            {
                if ((snapshotOffset + compSnapshotSize) > snapshotSize)
                    throw new InvalidOperationException("Overflow writing data to dynamic snapshot memory buffer");
            }

            private void CopyComponentToSnapshot(ArchetypeChunk chunk, int ent,
                ref DynamicComponentTypeHandle typeHandle,
                in GhostComponentSerializer.State serializer)
            {
                if(!serializer.HasGhostFields) return;
                var compSize = serializer.ComponentSize;
                var compData = (byte*) chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref typeHandle, compSize).GetUnsafeReadOnlyPtr();
                CheckValidSnapshotOffset(serializer.SnapshotSize);
                serializer.CopyToSnapshot.Invoke((IntPtr) UnsafeUtility.AddressOf(ref serializerState),
                    (IntPtr) snapshotPtr, snapshotOffset, snapshotSize, (IntPtr) (compData + ent * compSize), compSize, 1);
            }

            private void CopyBufferToSnapshot(ArchetypeChunk chunk, int ent,
                ref DynamicComponentTypeHandle typeHandle,
                in GhostComponentSerializer.State serializer)
            {
                var compSize = serializer.ComponentSize;
                var bufData = chunk.GetUntypedBufferAccessor(ref typeHandle);
                // Collect the buffer data to serialize by storing pointers, offset and size.
                var bufferPointer = (IntPtr) bufData.GetUnsafeReadOnlyPtrAndLength(ent, out var bufferLen);
                var snapshotData = (uint*) (snapshotPtr + snapshotOffset);
                snapshotData[0] = (uint) bufferLen;
                snapshotData[1] = (uint) dynamicSnapshotDataOffset;
                //Serialize the buffer contents
                var maskSize = SnapshotDynamicBuffersHelper.GetDynamicDataChangeMaskSize(serializer.ChangeMaskBits, bufferLen);
                CheckValidDynamicSnapshotOffset(serializer, maskSize, bufferLen);
                serializer.CopyToSnapshot.Invoke(
                    (IntPtr)UnsafeUtility.AddressOf(ref serializerState),
                    (IntPtr)(snapshotDynamicPtr + maskSize), dynamicSnapshotDataOffset, serializer.SnapshotSize,
                    bufferPointer, compSize, bufferLen);
                dynamicSnapshotDataOffset += GhostComponentSerializer.SnapshotSizeAligned(maskSize + serializer.SnapshotSize * bufferLen);
            }

            [BurstCompile]
            public void CopyEntityToSnapshot(ArchetypeChunk chunk, int ent, in GhostCollectionPrefabSerializer typeData, ClearOption option = ClearOption.Clear)
            {
                int numBaseComponents = typeData.NumComponents - typeData.NumChildComponents;
                for (int comp = 0; comp < numBaseComponents; ++comp)
                {
                    int compIdx = GhostComponentIndex[typeData.FirstComponent + comp].ComponentIndex;
                    int serializerIdx = GhostComponentIndex[typeData.FirstComponent + comp].SerializerIndex;
                    CheckValidComponentIndex(compIdx);
                    var typeHandle = ghostChunkComponentTypesPtr[compIdx];
                    ref readonly var ghostSerializer = ref GhostComponentCollection.ElementAtRO(serializerIdx);
                    var sizeInSnapshot = GhostComponentSerializer.SizeInSnapshot(ghostSerializer);
                    if (chunk.Has(ref typeHandle))
                    {
                        if (ghostSerializer.ComponentType.IsBuffer)
                        {
                            CopyBufferToSnapshot(chunk, ent, ref typeHandle, ghostSerializer);
                        }
                        else
                        {
                            CopyComponentToSnapshot(chunk, ent, ref typeHandle, ghostSerializer);
                        }
                    }
                    else if(option == ClearOption.Clear)
                    {
                        if (ghostSerializer.ComponentType.IsBuffer)
                        {
                            *(uint*)(snapshotPtr + snapshotOffset) = (uint)0;
                            *(uint*)(snapshotPtr + snapshotOffset + sizeof(int)) = (uint)(dynamicSnapshotDataOffset);
                        }
                        else
                        {
                            for (int i = 0; i < ghostSerializer.SnapshotSize / 4; ++i)
                            {
                                ((uint*) (snapshotPtr + snapshotOffset))[i] = 0;
                            }
                        }
                    }
                    snapshotOffset += sizeInSnapshot;
                }

                if (typeData.NumChildComponents > 0)
                {
                    var linkedEntityGroupAccessor = chunk.GetBufferAccessor(ref linkedEntityGroupType);
                    var linkedEntityGroup = linkedEntityGroupAccessor[ent];
                    for (int comp = numBaseComponents; comp < typeData.NumComponents; ++comp)
                    {
                        int compIdx = GhostComponentIndex[typeData.FirstComponent + comp].ComponentIndex;
                        int serializerIdx = GhostComponentIndex[typeData.FirstComponent + comp].SerializerIndex;
                        CheckValidComponentIndex(compIdx);
                        var typeHandle = ghostChunkComponentTypesPtr[compIdx];
                        ref readonly var ghostSerializer = ref GhostComponentCollection.ElementAtRO(serializerIdx);
                        var sizeInSnapshot = GhostComponentSerializer.SizeInSnapshot(ghostSerializer);
                        var childEnt = linkedEntityGroup[GhostComponentIndex[typeData.FirstComponent + comp].EntityIndex].Value;
                        if (childEntityLookup.TryGetValue(childEnt, out var childChunk) && childChunk.Chunk.Has(ref typeHandle))
                        {
                            if (ghostSerializer.ComponentType.IsBuffer)
                            {
                                CopyBufferToSnapshot(childChunk.Chunk, childChunk.IndexInChunk, ref typeHandle, ghostSerializer);
                            }
                            else
                            {
                                CopyComponentToSnapshot(childChunk.Chunk,childChunk.IndexInChunk, ref typeHandle, ghostSerializer);
                            }
                        }
                        else if(option == ClearOption.Clear)
                        {
                            if (ghostSerializer.ComponentType.IsBuffer)
                            {
                                *(uint*)(snapshotPtr + snapshotOffset) = (uint)0;
                                *(uint*)(snapshotPtr + snapshotOffset + sizeof(int)) = (uint)(dynamicSnapshotDataOffset);
                            }
                            else
                            {
                                for (int i = 0; i < ghostSerializer.SnapshotSize / 4; ++i)
                                {
                                    ((uint*) (snapshotPtr + snapshotOffset))[i] = 0;
                                }
                            }
                        }
                        snapshotOffset += sizeInSnapshot;
                    }
                }
                //Update the dynamic data total size
                if(typeData.NumBuffers > 0)
                    ((uint*)snapshotDynamicPtr)[0] = (uint)(dynamicSnapshotDataOffset - GhostComponentSerializer.SnapshotSizeAligned(sizeof(uint)));
            }

            [BurstCompile]
            public void CopyChunkToSnapshot(ArchetypeChunk chunk, in GhostCollectionPrefabSerializer typeData)
            {
                // Loop through all components and call the serialize method which will write the snapshot data and serialize the entities to the temporary stream
                int enableableMaskOffset = 0;
                int numBaseComponents = typeData.NumComponents - typeData.NumChildComponents;
                for (int comp = 0; comp < numBaseComponents; ++comp)
                {
                    int compIdx = GhostComponentIndex[typeData.FirstComponent + comp].ComponentIndex;
                    int serializerIdx = GhostComponentIndex[typeData.FirstComponent + comp].SerializerIndex;
                    CheckValidComponentIndex(compIdx);
                    ref readonly var ghostSerializer = ref GhostComponentCollection.ElementAtRO(serializerIdx);
                    var compSize = ghostSerializer.ComponentSize;
                    //Don't access the data but always increment the offset by the component SnapshotSize.
                    //Otherwise, the next serialized component would technically copy the data in the wrong memory slot
                    //It might still work in some cases but if this snapshot is then part of the history and used for
                    //interpolated data we might get incorrect results

                    if (ghostSerializer.SerializesEnabledBit != 0)
                    {
                        var handle = ghostChunkComponentTypesPtr[compIdx];
                        //There is no need to check if the chunk has the component because the chunl.GetEnableableBits will return
                        //default if the component is not present
                        GhostChunkSerializer.UpdateEnableableMasks(chunk, 0, chunk.Count, ref handle, snapshotPtr, changeMaskUints, enableableMaskOffset, snapshotSize);
                        ++enableableMaskOffset;
                        GhostChunkSerializer.ValidateWrittenEnableBits(enableableMaskOffset, typeData.EnableableBits);
                    }

                    if (ghostSerializer.ComponentType.IsBuffer)
                    {
                        if (chunk.Has(ref ghostChunkComponentTypesPtr[compIdx]))
                        {
                            var dynamicDataSize = ghostSerializer.SnapshotSize;
                            var bufData = chunk.GetUntypedBufferAccessor(ref ghostChunkComponentTypesPtr[compIdx]);
                            for (int ent = 0, chunkEntityCount = chunk.Count; ent < chunkEntityCount; ++ent)
                            {
                                var compData = (byte*)bufData.GetUnsafeReadOnlyPtrAndLength(ent, out var len);
                                var maskSize = SnapshotDynamicBuffersHelper.GetDynamicDataChangeMaskSize(ghostSerializer.ChangeMaskBits, len);
                                //Set the elements count and the buffer content offset inside the dynamic data history buffer
                                *(uint*)(snapshotPtr + snapshotOffset + ent * snapshotSize) = (uint)len;
                                *(uint*)(snapshotPtr + snapshotOffset + ent * snapshotSize + sizeof(int)) = (uint)(dynamicSnapshotDataOffset);
                                ghostSerializer.CopyToSnapshot.Invoke((IntPtr)UnsafeUtility.AddressOf(ref serializerState),
                                    (IntPtr)snapshotDynamicPtr, dynamicSnapshotDataOffset + maskSize, dynamicDataSize, (IntPtr)compData, compSize, len);

                                dynamicSnapshotDataOffset += GhostComponentSerializer.SnapshotSizeAligned(maskSize + dynamicDataSize * len);
                            }
                        }
                        else
                        {
                            for (int ent = 0, chunkEntityCount = chunk.Count; ent < chunkEntityCount; ++ent)
                            {
                                *(uint*)(snapshotPtr + snapshotOffset + ent * snapshotSize) = (uint)0;
                                *(uint*)(snapshotPtr + snapshotOffset + ent * snapshotSize + sizeof(int)) = (uint)(dynamicSnapshotDataOffset);
                            }
                        }

                        snapshotOffset += GhostComponentSerializer.SnapshotSizeAligned(GhostComponentSerializer.DynamicBufferComponentSnapshotSize);
                    }
                    else
                    {
                        if (ghostSerializer.HasGhostFields)
                        {
                            if (chunk.Has(ref ghostChunkComponentTypesPtr[compIdx]))
                            {
                                var compData = (byte*) chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref ghostChunkComponentTypesPtr[compIdx], compSize).GetUnsafeReadOnlyPtr();
                                ghostSerializer.CopyToSnapshot.Invoke((IntPtr) UnsafeUtility.AddressOf(ref serializerState),
                                    (IntPtr) snapshotPtr, snapshotOffset, snapshotSize, (IntPtr) compData, compSize, chunk.Count);
                            }
                            else
                            {
                                for (int ent = 0, chunkEntityCount = chunk.Count; ent < chunkEntityCount; ++ent)
                                    UnsafeUtility.MemClear(snapshotPtr + snapshotOffset + ent * snapshotSize, ghostSerializer.SnapshotSize);
                            }

                            snapshotOffset += GhostComponentSerializer.SnapshotSizeAligned(ghostSerializer.SnapshotSize);
                        }
                    }
                }
                if (typeData.NumChildComponents > 0)
                {
                    var linkedEntityGroupAccessor = chunk.GetBufferAccessor(ref linkedEntityGroupType);
                    for (int comp = numBaseComponents; comp < typeData.NumComponents; ++comp)
                    {
                        int compIdx = GhostComponentIndex[typeData.FirstComponent + comp].ComponentIndex;
                        int serializerIdx = GhostComponentIndex[typeData.FirstComponent + comp].SerializerIndex;
                        CheckValidComponentIndex(compIdx);
                        ref readonly var ghostSerializer = ref GhostComponentCollection.ElementAtRO(serializerIdx);
                        var compSize = ghostSerializer.ComponentSize;
                        if(ghostSerializer.ComponentType.IsBuffer)
                        {
                            var dynamicDataSize = ghostSerializer.SnapshotSize;
                            var snapshotDataPtr = snapshotPtr;
                            for (int ent = 0, chunkEntityCount = chunk.Count; ent < chunkEntityCount; ++ent)
                            {
                                var linkedEntityGroup = linkedEntityGroupAccessor[ent];
                                var childEnt = linkedEntityGroup[GhostComponentIndex[typeData.FirstComponent + comp].EntityIndex].Value;
                                if (childEntityLookup.TryGetValue(childEnt, out var childChunk) && childChunk.Chunk.Has(ref ghostChunkComponentTypesPtr[compIdx]))
                                {
                                    var bufData = childChunk.Chunk.GetUntypedBufferAccessor(ref ghostChunkComponentTypesPtr[compIdx]);
                                    var compData = (byte*)bufData.GetUnsafeReadOnlyPtrAndLength(childChunk.IndexInChunk, out var len);

                                    var maskSize = SnapshotDynamicBuffersHelper.GetDynamicDataChangeMaskSize(ghostSerializer.ChangeMaskBits, len);
                                    //Set the elements count and the buffer content offset inside the dynamic data history buffer
                                    *(uint*)(snapshotPtr + snapshotOffset + ent * snapshotSize) = (uint)len;
                                    *(uint*)(snapshotPtr + snapshotOffset + ent * snapshotSize + sizeof(int)) = (uint)(dynamicSnapshotDataOffset);
                                    ghostSerializer.CopyToSnapshot.Invoke((IntPtr)UnsafeUtility.AddressOf(ref serializerState),
                                        (IntPtr)snapshotDynamicPtr, dynamicSnapshotDataOffset + maskSize, dynamicDataSize, (IntPtr)compData, compSize, len);

                                    if (ghostSerializer.SerializesEnabledBit != 0)
                                    {
                                        var handle = ghostChunkComponentTypesPtr[compIdx];
                                        GhostChunkSerializer.UpdateEnableableMasks(childChunk.Chunk, childChunk.IndexInChunk, childChunk.IndexInChunk+1,
                                            ref handle, snapshotDataPtr, changeMaskUints, enableableMaskOffset, snapshotSize);
                                    }

                                    dynamicSnapshotDataOffset += GhostComponentSerializer.SnapshotSizeAligned(maskSize + dynamicDataSize * len);
                                }
                                else
                                {
                                    *(uint*)(snapshotPtr + snapshotOffset + ent * snapshotSize) = (uint)0;
                                    *(uint*)(snapshotPtr + snapshotOffset + ent * snapshotSize + sizeof(int)) = (uint)(dynamicSnapshotDataOffset);
                                }
                                snapshotDataPtr += snapshotSize;
                            }
                            snapshotOffset += GhostComponentSerializer.SnapshotSizeAligned(GhostComponentSerializer.DynamicBufferComponentSnapshotSize);
                            if (ghostSerializer.SerializesEnabledBit != 0)
                            {
                                ++enableableMaskOffset;
                                GhostChunkSerializer.ValidateWrittenEnableBits(enableableMaskOffset, typeData.EnableableBits);
                            }
                        }
                        else
                        {
                            var snapshotDataPtr = snapshotPtr;
                            for (int ent = 0, chunkEntityCount = chunk.Count; ent < chunkEntityCount; ++ent)
                            {
                                var linkedEntityGroup = linkedEntityGroupAccessor[ent];
                                var childEnt = linkedEntityGroup[GhostComponentIndex[typeData.FirstComponent + comp].EntityIndex].Value;
                                //We can skip here, because the memory buffer offset is computed using the start-end entity indices
                                if (childEntityLookup.TryGetValue(childEnt, out var childChunk) && childChunk.Chunk.Has(ref ghostChunkComponentTypesPtr[compIdx]))
                                {
                                    if (ghostSerializer.HasGhostFields)
                                    {
                                        var compData = (byte*) childChunk.Chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref ghostChunkComponentTypesPtr[compIdx], compSize).GetUnsafeReadOnlyPtr();
                                        compData += childChunk.IndexInChunk * compSize;

                                        // TODO: would batching be faster?
                                        ghostSerializer.CopyToSnapshot.Invoke((IntPtr) UnsafeUtility.AddressOf(ref serializerState),
                                            (IntPtr) snapshotPtr + ent * snapshotSize, snapshotOffset, snapshotSize, (IntPtr) compData, compSize, 1);
                                    }

                                    if (ghostSerializer.SerializesEnabledBit != 0)
                                    {
                                        var handle = ghostChunkComponentTypesPtr[compIdx];
                                        GhostChunkSerializer.UpdateEnableableMasks(childChunk.Chunk, childChunk.IndexInChunk, childChunk.IndexInChunk+1,
                                            ref handle, snapshotDataPtr, changeMaskUints, enableableMaskOffset, snapshotSize);
                                    }
                                }
                                else
                                {
                                    UnsafeUtility.MemClear(snapshotPtr + snapshotOffset + ent*snapshotSize, ghostSerializer.SnapshotSize);
                                }
                                snapshotDataPtr += snapshotSize;
                            }
                            snapshotOffset += GhostComponentSerializer.SnapshotSizeAligned(ghostSerializer.SnapshotSize);
                            if (ghostSerializer.SerializesEnabledBit != 0)
                            {
                                ++enableableMaskOffset;
                                GhostChunkSerializer.ValidateWrittenEnableBits(enableableMaskOffset, typeData.EnableableBits);
                            }
                        }
                    }
                }
                GhostChunkSerializer.ValidateAllEnableBitsHasBeenWritten(enableableMaskOffset, typeData.EnableableBits);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GatherBufferSize(ArchetypeChunk chunk, int startIndex, GhostCollectionPrefabSerializer typeData)
            {
                var emptyArray = new NativeArray<int>();
                return GatherBufferSize(chunk, startIndex, typeData, ref emptyArray);
            }

            [BurstCompile]
            public int GatherBufferSize(ArchetypeChunk chunk, int startIndex, GhostCollectionPrefabSerializer typeData, ref NativeArray<int> buffersSize)
            {
                int numBaseComponents = typeData.NumComponents - typeData.NumChildComponents;
                int totalSize = 0;
                for (int comp = 0; comp < numBaseComponents; ++comp)
                {
                    int compIdx = GhostComponentIndex[typeData.FirstComponent + comp].ComponentIndex;
                    int serializerIdx = GhostComponentIndex[typeData.FirstComponent + comp].SerializerIndex;
                    ref readonly var ghostSerializer = ref GhostComponentCollection.ElementAtRO(serializerIdx);
                    if (!ghostSerializer.ComponentType.IsBuffer || !chunk.Has(ref ghostChunkComponentTypesPtr[compIdx]))
                        continue;

                    for (int ent = startIndex, chunkEntityCount = chunk.Count; ent < chunkEntityCount; ++ent)
                    {
                        var bufferAccessor = chunk.GetUntypedBufferAccessor(ref ghostChunkComponentTypesPtr[compIdx]);
                        var bufferLen = bufferAccessor.GetBufferLength(ent);
                        var maskSize = SnapshotDynamicBuffersHelper.GetDynamicDataChangeMaskSize(ghostSerializer.ChangeMaskBits, bufferLen);
                        var size = GhostComponentSerializer.SnapshotSizeAligned(maskSize + bufferLen * ghostSerializer.SnapshotSize);
                        if(buffersSize.IsCreated)
                            buffersSize[ent] += size;
                        totalSize += size;
                    }
                }

                if (typeData.NumChildComponents > 0)
                {
                    var linkedEntityGroupAccessor = chunk.GetBufferAccessor(ref linkedEntityGroupType);
                    for (int comp = numBaseComponents; comp < typeData.NumComponents; ++comp)
                    {
                        int compIdx = GhostComponentIndex[typeData.FirstComponent + comp].ComponentIndex;
                        int serializerIdx = GhostComponentIndex[typeData.FirstComponent + comp].SerializerIndex;
                        CheckValidComponentIndex(compIdx);
                        ref readonly var ghostSerializer = ref GhostComponentCollection.ElementAtRO(serializerIdx);
                        if (!ghostSerializer.ComponentType.IsBuffer)
                            continue;

                        for (int ent = startIndex, chunkEntityCount = chunk.Count; ent < chunkEntityCount; ++ent)
                        {
                            var linkedEntityGroup = linkedEntityGroupAccessor[ent];
                            var childEnt = linkedEntityGroup[GhostComponentIndex[typeData.FirstComponent + comp].EntityIndex].Value;
                            if (childEntityLookup.TryGetValue(childEnt, out var childChunk) && childChunk.Chunk.Has(ref ghostChunkComponentTypesPtr[compIdx]))
                            {
                                var bufferAccessor = childChunk.Chunk.GetUntypedBufferAccessor(ref ghostChunkComponentTypesPtr[compIdx]);
                                var bufferLen = bufferAccessor.GetBufferLength(childChunk.IndexInChunk);
                                var maskSize = SnapshotDynamicBuffersHelper.GetDynamicDataChangeMaskSize(ghostSerializer.ChangeMaskBits, bufferLen);
                                var size = GhostComponentSerializer.SnapshotSizeAligned(maskSize + bufferLen * ghostSerializer.SnapshotSize);
                                if(buffersSize.IsCreated)
                                    buffersSize[ent] += size;
                                totalSize += size;
                            }
                        }
                    }
                }
                return totalSize;
            }
        }
    }
}
