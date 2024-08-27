using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine.Assertions;

namespace Unity.NetCode.LowLevel.Unsafe
{
    /// <summary>
    /// Contains helper methods to write custom chunk serializers.
    /// See <see cref="GhostPrefabCustomSerializer"/> for more information about what a custom
    /// chunk serializer function pointer should be used for.
    /// </summary>
    public static unsafe class CustomGhostSerializerHelpers
    {
        /// <summary>
        /// Copy the component data to the snapshot buffer for the whole chunk starting from
        /// index <see cref="GhostPrefabCustomSerializer.Context.startIndex"/> to
        /// <see cref="GhostPrefabCustomSerializer.Context.endIndex"/>.
        /// </summary>
        /// <param name="chunk"></param>
        /// <param name="context"></param>
        /// <param name="typeHandles"></param>
        /// <param name="index"></param>
        /// <param name="snapshotData"></param>
        /// <param name="snapshotOffset"></param>
        /// <param name="serializer"></param>
        /// <typeparam name="T"></typeparam>
        public static void CopyComponentToSnapshot<T>(
            this T serializer,
            ArchetypeChunk chunk,
            ref GhostPrefabCustomSerializer.Context context,
            DynamicComponentTypeHandle* typeHandles,
            in GhostCollectionComponentIndex index,
            IntPtr snapshotData,
            ref int snapshotOffset) where T: unmanaged, IGhostSerializer
        {
            var data = (IntPtr)chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref typeHandles[index.ComponentIndex], index.ComponentSize).GetUnsafeReadOnlyPtr();
            var snapshot = snapshotData + snapshotOffset;
            for (int ent = context.startIndex; ent < context.endIndex; ++ent)
            {
                serializer.CopyToSnapshot(context.serializerState, snapshot, data + index.ComponentSize*ent);
                snapshot += context.snapshotStride;
            }
            snapshotOffset += GhostComponentSerializer.SnapshotSizeAligned(index.SnapshotSize);
            Assert.IsTrue(snapshotOffset <= context.snapshotStride);
        }

        /// <summary>
        /// Copy a single component data for a child component to the snapshot buffer.
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="chunk"></param>
        /// <param name="indexInChunk"></param>
        /// <param name="context"></param>
        /// <param name="typeHandles"></param>
        /// <param name="index"></param>
        /// <param name="snapshotData"></param>
        /// <param name="snapshotOffset"></param>
        /// <typeparam name="T"></typeparam>
        public static void CopyChildComponentToSnapshot<T>(
            this T serializer,
            ArchetypeChunk chunk,
            int indexInChunk,
            ref GhostPrefabCustomSerializer.Context context,
            DynamicComponentTypeHandle* typeHandles,
            in GhostCollectionComponentIndex index,
            IntPtr snapshotData, ref int snapshotOffset) where T: unmanaged, IGhostSerializer
        {
            var data = (IntPtr)chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref typeHandles[index.ComponentIndex], index.ComponentSize).GetUnsafeReadOnlyPtr();
            serializer.CopyToSnapshot(context.serializerState, snapshotData + snapshotOffset, data + index.ComponentSize*indexInChunk);
            snapshotOffset += GhostComponentSerializer.SnapshotSizeAligned(index.SnapshotSize);
            Assert.IsTrue(snapshotOffset <= context.snapshotStride);
        }

        /// <summary>
        /// Copy all buffers in the chunk for a given <see cref="DynamicComponentTypeHandle"/> to the snapshot buffer,
        /// starting from index <see cref="GhostPrefabCustomSerializer.Context.startIndex"/> to
        /// <see cref="GhostPrefabCustomSerializer.Context.endIndex"/>.
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="chunk"></param>
        /// <param name="context"></param>
        /// <param name="typeHandles"></param>
        /// <param name="index"></param>
        /// <param name="snapshotData"></param>
        /// <param name="snapshotOffset"></param>
        /// <param name="dynamicSnapshotDataOffset"></param>
        /// <typeparam name="T"></typeparam>
        public static void CopyBufferToSnapshot<T>(
            this T serializer,
            ArchetypeChunk chunk, ref GhostPrefabCustomSerializer.Context context,
            DynamicComponentTypeHandle* typeHandles,
            in GhostCollectionComponentIndex index,
            IntPtr snapshotData,
            ref int snapshotOffset, ref int dynamicSnapshotDataOffset) where T: unmanaged, IGhostSerializer
        {
            var bufAccessor = chunk.GetUntypedBufferAccessor(ref typeHandles[index.ComponentIndex]);
            var snapshot = snapshotData + snapshotOffset;
            for (int ent = context.startIndex; ent < context.endIndex; ++ent)
            {
                var bufData = (IntPtr)bufAccessor.GetUnsafeReadOnlyPtrAndLength(ent, out var bufLen);
                CopyBufferDataToSnapshot(context, ref dynamicSnapshotDataOffset, index.ComponentSize, index.SnapshotSize,
                    serializer, snapshot, bufData, bufLen);
                snapshot += context.snapshotStride;
            }
            snapshotOffset += GhostComponentSerializer.SnapshotSizeAligned(GhostComponentSerializer.DynamicBufferComponentSnapshotSize);
            Assert.IsTrue(snapshotOffset <= context.snapshotStride);
        }

        /// <summary>
        /// Copy a single buffer on a child entity for a given <see cref="DynamicComponentTypeHandle"/> to the snapshot buffer.
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="chunk"></param>
        /// <param name="indexInChunk"></param>
        /// <param name="context"></param>
        /// <param name="typeHandles"></param>
        /// <param name="index"></param>
        /// <param name="snapshotData"></param>
        /// <param name="snapshotOffset"></param>
        /// <param name="dynamicSnapshotOffset"></param>
        /// <typeparam name="T"></typeparam>
        public static void CopyChildBufferToSnapshot<T>(
            this T serializer,
            ArchetypeChunk chunk, int indexInChunk,
            ref GhostPrefabCustomSerializer.Context context,
            DynamicComponentTypeHandle* typeHandles,
            in GhostCollectionComponentIndex index,
            IntPtr snapshotData,ref int snapshotOffset, ref int dynamicSnapshotOffset)
            where T: unmanaged, IGhostSerializer
        {
            var bufAccessor = chunk.GetUntypedBufferAccessor(ref typeHandles[index.ComponentIndex]);
            var snapshot = snapshotData + snapshotOffset;
            var bufData = (IntPtr)bufAccessor.GetUnsafeReadOnlyPtrAndLength(indexInChunk, out var bufLen);
            CopyBufferDataToSnapshot(context, ref dynamicSnapshotOffset,
                index.ComponentSize, index.SnapshotSize, serializer,
                snapshot, bufData, bufLen);
            snapshotOffset += GhostComponentSerializer.SnapshotSizeAligned(GhostComponentSerializer.DynamicBufferComponentSnapshotSize);
            Assert.IsTrue(snapshotOffset <= context.snapshotStride);
        }

        private static void CopyBufferDataToSnapshot<T>(GhostPrefabCustomSerializer.Context context,
            ref int dynamicSnapshotOffset, int componentSize, int snapshotSize, T serializer,
            IntPtr snapshot, IntPtr bufData, int bufLen) where T : unmanaged, IGhostSerializer
        {
            var dynamicSnapshot = context.snapshotDynamicDataPtr + dynamicSnapshotOffset;
            //Set the elements count and the buffer content offset inside the dynamic data history buffer
            *(uint*)snapshot = (uint)bufLen;
            *(uint*)(snapshot + 4) = (uint)dynamicSnapshotOffset;
            if (bufLen > 0)
            {
                //Copy the buffer contents. Skip the change mask (later)
                var maskSize = SnapshotDynamicBuffersHelper.GetDynamicDataChangeMaskSize(serializer.ChangeMaskSizeInBits, bufLen);
                dynamicSnapshot += maskSize;
                for (int el = 0; el < bufLen; ++el)
                {
                    serializer.CopyToSnapshot(context.serializerState, dynamicSnapshot + snapshotSize * el, bufData + componentSize * el);
                }

                var dynamicSize = GhostComponentSerializer.SnapshotSizeAligned(maskSize + snapshotSize * bufLen);
                dynamicSnapshotOffset += dynamicSize;
                Assert.IsTrue(dynamicSnapshotOffset <= context.dynamicDataCapacity);
            }
        }


        /// <summary>
        /// Copy all the enable bits state for the given <see cref="DynamicComponentTypeHandle"/> to
        /// the snapshot buffer.
        /// </summary>
        /// <param name="chunk"></param>
        /// <param name="startIndex"></param>
        /// <param name="endIndex"></param>
        /// <param name="snapshotStride"></param>
        /// <param name="componentTypeHandle"></param>
        /// <param name="enableMasks"></param>
        /// <param name="maskOffset"></param>
        public static void CopyEnableBits(ArchetypeChunk chunk, int startIndex, int endIndex,
            int snapshotStride, ref DynamicComponentTypeHandle componentTypeHandle, byte* enableMasks,
            ref int maskOffset)
        {
            var array = chunk.GetEnableableBits(ref componentTypeHandle);
            var bitArray = new UnsafeBitArray(&array, 2 * sizeof(ulong));
            var entityMask = ((uint*)enableMasks) + maskOffset / 32;
            var bitOffset = maskOffset % 32;
            snapshotStride /= 4;
            for (int ent = startIndex; ent < endIndex; ++ent)
            {
                if (bitOffset == 0)
                    *entityMask = 0;
                var isSetOnServer = bitArray.IsSet(ent);
                if (isSetOnServer)
                    *entityMask |= 1U << bitOffset;
                entityMask += snapshotStride;
            }
            ++maskOffset;
        }
    }

    /// <summary>
    /// Extension methods for all unmanaged types implementing the
    /// <see cref="IGhostSerializer"/> interface.
    /// </summary>
    static public class GhostCustomSerializerExtensions
    {
        /// <summary>
        /// Serialize the given component into the data stream by using a single baseline.
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="snapshot"></param>
        /// <param name="baseline"></param>
        /// <param name="changeMaskData"></param>
        /// <param name="startOffset"></param>
        /// <param name="snapshotOffset"></param>
        /// <param name="writer"></param>
        /// <param name="compressionModel"></param>
        /// <param name="sendComponent"></param>
        /// <typeparam name="TSerializer"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public int SerializeComponentSingleBaseline<TSerializer>(
            this TSerializer serializer,
            IntPtr snapshot, in IntPtr baseline,
            [NoAlias] IntPtr changeMaskData, ref int startOffset, ref int snapshotOffset,
            ref DataStreamWriter writer, in StreamCompressionModel compressionModel,
            int sendComponent=1)
            where TSerializer : unmanaged, IGhostSerializer
        {
            if(Burst.CompilerServices.Hint.Unlikely(sendComponent == 0))
            {
                if(Burst.CompilerServices.Hint.Likely(serializer.HasGhostFields))
                {
                    var snapshotSize = GhostComponentSerializer.SnapshotSizeAligned(serializer.SizeInSnapshot);
                    GhostComponentSerializer.ClearSnapshotDataAndMask(snapshot, snapshotOffset, snapshotSize,
                        changeMaskData, startOffset, serializer.ChangeMaskSizeInBits);
                    snapshotOffset += snapshotSize;
                    startOffset += serializer.ChangeMaskSizeInBits;
                }
                return 0;
            }
            else
            {
                var currentBits = writer.LengthInBits;
                if (Burst.CompilerServices.Hint.Likely(serializer.HasGhostFields))
                {
                    serializer.SerializeCombined(
                        snapshot + snapshotOffset,
                        baseline + snapshotOffset,
                        changeMaskData, startOffset, ref writer, compressionModel);
                    snapshotOffset += GhostComponentSerializer.SnapshotSizeAligned(serializer.SizeInSnapshot);
                    startOffset += serializer.ChangeMaskSizeInBits;
                }
                return writer.LengthInBits - currentBits;
            }
        }

        /// <summary>
        /// Serialize the given component into the data stream by using three baselines.
        /// The <see cref="GhostDeltaPredictor"/> will calculate new predicted baseline that
        /// will be used for delta compression.
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="snapshot"></param>
        /// <param name="baseline0"></param>
        /// <param name="baseline1"></param>
        /// <param name="baseline2"></param>
        /// <param name="changeMaskData"></param>
        /// <param name="startOffset"></param>
        /// <param name="snapshotOffset"></param>
        /// <param name="predictor"></param>
        /// <param name="writer"></param>
        /// <param name="compressionModel"></param>
        /// <param name="sendComponent"></param>
        /// <typeparam name="TSerializer"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public int SerializeComponentThreeBaseline<TSerializer>(
            this TSerializer serializer,
            IntPtr snapshot, IntPtr baseline0,
            IntPtr baseline1, IntPtr baseline2,
            [NoAlias] IntPtr changeMaskData, ref int startOffset, ref int snapshotOffset,
            ref GhostDeltaPredictor predictor, ref DataStreamWriter writer, in StreamCompressionModel compressionModel,
            int sendComponent=1)
            where TSerializer : unmanaged, IGhostSerializer
        {
            if(Burst.CompilerServices.Hint.Unlikely(sendComponent == 0))
            {
                if(Burst.CompilerServices.Hint.Likely(serializer.HasGhostFields))
                {
                    var snapshotSize = GhostComponentSerializer.SnapshotSizeAligned(serializer.SizeInSnapshot);
                    GhostComponentSerializer.ClearSnapshotDataAndMask(snapshot, snapshotOffset, snapshotSize,
                        changeMaskData, startOffset, serializer.ChangeMaskSizeInBits);
                    snapshotOffset += snapshotSize;
                    startOffset += serializer.ChangeMaskSizeInBits;
                }
                return 0;
            }
            else
            {
                var currentBits = writer.LengthInBits;
                if(Burst.CompilerServices.Hint.Likely(serializer.HasGhostFields))
                {
                    serializer.SerializeWithPredictedBaseline(
                        snapshot + snapshotOffset, baseline0 + snapshotOffset, baseline1 + snapshotOffset,
                        baseline2 + snapshotOffset, ref predictor,
                        changeMaskData, startOffset, ref writer, compressionModel);
                    snapshotOffset += GhostComponentSerializer.SnapshotSizeAligned(serializer.SizeInSnapshot);
                    startOffset += serializer.ChangeMaskSizeInBits;
                }
                return writer.LengthInBits - currentBits;
            }
        }

        /// <summary>
        /// Serialize a single buffer to the datastream using the default buffer serialisation
        /// strategy.
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="snapshot"></param>
        /// <param name="baseline"></param>
        /// <param name="snapshotDynamicData"></param>
        /// <param name="baselineDynamicData"></param>
        /// <param name="changeMaskData"></param>
        /// <param name="startOffset"></param>
        /// <param name="snapshotOffset"></param>
        /// <param name="dynamicSize"></param>
        /// <param name="writer"></param>
        /// <param name="compressionModel"></param>
        /// <param name="sendBuffer"></param>
        /// <typeparam name="TSerializer"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public int SerializeBuffer<TSerializer>(
            this TSerializer serializer,
            IntPtr snapshot, IntPtr baseline,
            [NoAlias] IntPtr snapshotDynamicData,
            [NoAlias] IntPtr baselineDynamicData,
            [NoAlias] IntPtr changeMaskData, ref int startOffset, ref int snapshotOffset,
            ref int dynamicSize, ref DataStreamWriter writer, in StreamCompressionModel compressionModel,
            int sendBuffer = 1)
            where TSerializer : unmanaged, IGhostSerializer
        {
            int snapshotSize = serializer.SizeInSnapshot;
            int len = GhostComponentSerializer.TypeCast<int>(snapshot, snapshotOffset);
            int dynamicSnapshotDataOffset = GhostComponentSerializer.TypeCast<int>(snapshot, snapshotOffset + 4);
            var maskSize = SnapshotDynamicBuffersHelper.GetDynamicDataChangeMaskSize(serializer.ChangeMaskSizeInBits, len);
            var dataSize = GhostComponentSerializer.SnapshotSizeAligned(maskSize + len * snapshotSize);
            var currentBits = writer.LengthInBits;
            if(Burst.CompilerServices.Hint.Unlikely(sendBuffer == 0))
            {
                GhostComponentSerializer.ResetChangeMask(changeMaskData, startOffset, 2);
                var sizeInSnapshot = GhostComponentSerializer.SnapshotSizeAligned(GhostComponentSerializer.DynamicBufferComponentSnapshotSize);
                GhostComponentSerializer.ClearSnapshotDataAndMask(snapshot, snapshotOffset, sizeInSnapshot,
                    changeMaskData, startOffset, serializer.ChangeMaskSizeInBits);
                dynamicSize += dataSize;
                snapshotOffset += sizeInSnapshot;
                startOffset += GhostComponentSerializer.DynamicBufferComponentMaskBits;
                return 0;
            }
            else
            {
                DefaultBufferSerialization.SerializeBufferToStream(serializer,
                    baseline, snapshotOffset,
                    changeMaskData, startOffset, serializer.ChangeMaskSizeInBits,
                    snapshotDynamicData, baselineDynamicData,
                    len, dynamicSnapshotDataOffset, snapshotSize, maskSize,
                    ref writer, compressionModel);
                dynamicSize += dataSize;
                snapshotOffset += GhostComponentSerializer.SnapshotSizeAligned(GhostComponentSerializer.DynamicBufferComponentSnapshotSize);
                startOffset += GhostComponentSerializer.DynamicBufferComponentMaskBits;
            }
            return writer.LengthInBits - currentBits;
        }
    }
}
