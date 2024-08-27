using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using AOT;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.NetCode.LowLevel.Unsafe;

namespace Unity.NetCode
{
    /// <summary>
    /// Helper class used by code-gen to setup the serialisation function pointers.
    /// </summary>
    /// <typeparam name="TComponentType">The unmanaged buffer the helper serialise</typeparam>
    /// <typeparam name="TSnapshot">The snaphost data struct that contains the <see cref="IBufferElementData"/> data.</typeparam>
    /// <typeparam name="TSerializer">A concrete type that implement the <see cref="IGhostSerializer"/> interface.</typeparam>
    [BurstCompile]
    public static class BufferSerializationHelper<TComponentType, TSnapshot, TSerializer>
        where TComponentType: unmanaged
        where TSnapshot: unmanaged
        where TSerializer: unmanaged, IGhostSerializer
    {
        /// <summary>
        /// Setup all the <see cref="GhostComponentSerializer.State"/> data and function pointers.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="systemState"></param>
        /// <returns>if the <param name="state"></param>/> has been initialised.</returns>
        public static bool SetupFunctionPointers(ref GhostComponentSerializer.State state,
            ref SystemState systemState)
        {
            // Optimization: Creating burst functions is expensive.
            // We don't need to do it in literally any other words as they're never called.
            if ((systemState.WorldUnmanaged.Flags & WorldFlags.GameServer) != WorldFlags.GameServer
                && (systemState.WorldUnmanaged.Flags & WorldFlags.GameClient) != WorldFlags.GameClient
                && (systemState.WorldUnmanaged.Flags & WorldFlags.GameThinClient) != WorldFlags.GameThinClient)
                return false;

            if(state.SnapshotSize == 0)
            {
                ZeroSizeComponentSerializationHelper.SetupFunctionPointers(ref state);
                return true;
            }

            state.PostSerializeBuffer = new PortableFunctionPointer<GhostComponentSerializer.PostSerializeBufferDelegate>(
                PostSerializeBuffer);
            state.SerializeBuffer = new PortableFunctionPointer<GhostComponentSerializer.SerializeBufferDelegate>(
                SerializeBuffer);
            state.CopyFromSnapshot = new PortableFunctionPointer<GhostComponentSerializer.CopyToFromSnapshotDelegate>(
                CopyBufferFromSnapshot);
            state.CopyToSnapshot = new PortableFunctionPointer<GhostComponentSerializer.CopyToFromSnapshotDelegate>(
                CopyBufferToSnapshot);
            state.RestoreFromBackup = new PortableFunctionPointer<GhostComponentSerializer.RestoreFromBackupDelegate>(
                RestoreFromBackup);
            state.Deserialize = new PortableFunctionPointer<GhostComponentSerializer.DeserializeDelegate>(
                Deserialize);
#if UNITY_EDITOR || NETCODE_DEBUG
            state.ReportPredictionErrors = new PortableFunctionPointer<GhostComponentSerializer.ReportPredictionErrorsDelegate>(
                ReportPredictionErrors);
#endif
            return true;
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostComponentSerializer.RestoreFromBackupDelegate))]
        private static void RestoreFromBackup([NoAlias]IntPtr componentData, [NoAlias]IntPtr backupData)
        {
            default(TSerializer).RestoreFromBackup(componentData, backupData);
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostComponentSerializer.PredictDeltaDelegate))]
        private static void PredictDelta([NoAlias]IntPtr snapshotData,
            [NoAlias][ReadOnly]IntPtr baseline1Data, [NoAlias][ReadOnly]IntPtr baseline2Data, ref GhostDeltaPredictor predictor)
        {
            default(TSerializer).PredictDelta(snapshotData, baseline1Data, baseline2Data, ref predictor);
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostComponentSerializer.DeserializeDelegate))]
        private static void Deserialize([NoAlias]IntPtr snapshotData, [NoAlias]IntPtr baselineData, ref DataStreamReader reader, ref StreamCompressionModel compressionModel, [NoAlias]IntPtr changeMaskData, int startOffset)
        {
            default(TSerializer).Deserialize(ref reader, compressionModel, changeMaskData, startOffset, snapshotData, baselineData);
        }

#if UNITY_EDITOR || NETCODE_DEBUG
        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostComponentSerializer.ReportPredictionErrorsDelegate))]
        private static void ReportPredictionErrors([NoAlias]IntPtr componentData, [NoAlias]IntPtr backupData, [NoAlias]IntPtr errorsList, int errorsCount)
        {
            default(TSerializer).ReportPredictionErrors(componentData, backupData, errorsList, errorsCount);
        }
#endif
        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostComponentSerializer.PostSerializeBufferDelegate))]
        private static void PostSerializeBuffer([NoAlias]IntPtr snapshotData, int snapshotOffset, int snapshotStride,
            int maskOffsetInBits, int changeMaskBits, int count, [NoAlias]IntPtr baselines, ref DataStreamWriter writer, ref StreamCompressionModel compressionModel,
            [NoAlias]IntPtr entityStartBit, [NoAlias]IntPtr snapshotDynamicDataPtr, [NoAlias]IntPtr dynamicSizePerEntity, int dynamicSnapshotMaxOffset)
        {
            int dynamicDataSize = UnsafeUtility.SizeOf<TSnapshot>();
            for (int i = 0; i < count; ++i)
            {
                // Get the elements count and the buffer content offset inside the dynamic data history buffer from the pre-serialized snapshot
                int len = GhostComponentSerializer.TypeCast<int>(snapshotData + snapshotStride*i, snapshotOffset);
                int dynamicSnapshotDataOffset = GhostComponentSerializer.TypeCast<int>(snapshotData + snapshotStride*i, snapshotOffset+4);
                var maskSize = SnapshotDynamicBuffersHelper.GetDynamicDataChangeMaskSize(changeMaskBits, len);
                CheckDynamicDataRange(dynamicSnapshotDataOffset, maskSize, len, dynamicDataSize, dynamicSnapshotMaxOffset);
                SerializeOneBuffer(i, snapshotData, snapshotOffset, snapshotStride, maskOffsetInBits, changeMaskBits, baselines, ref writer,
                    compressionModel, entityStartBit, snapshotDynamicDataPtr, dynamicSizePerEntity, len, ref dynamicSnapshotDataOffset, dynamicDataSize, maskSize);
            }
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostComponentSerializer.SerializeBufferDelegate))]
        private static void SerializeBuffer([NoAlias]IntPtr stateData,
            [NoAlias]IntPtr snapshotData, int snapshotOffset, int snapshotStride,
            int maskOffsetInBits, int changeMaskBits,
            [NoAlias]IntPtr componentData, [NoAlias]IntPtr componentDataLen, int count, [NoAlias]IntPtr baselines,
            ref DataStreamWriter writer, ref StreamCompressionModel compressionModel,
            [NoAlias]IntPtr entityStartBit, [NoAlias]IntPtr snapshotDynamicDataPtr, ref int dynamicSnapshotDataOffset,
            [NoAlias]IntPtr dynamicSizePerEntity, int dynamicSnapshotMaxOffset)
        {
            int dynamicDataSize = UnsafeUtility.SizeOf<TSnapshot>();
            for (int i = 0; i < count; ++i)
            {
                int len = GhostComponentSerializer.TypeCast<int>(componentDataLen, i*4);
                //Set the elements count and the buffer content offset inside the dynamic data history buffer
                GhostComponentSerializer.TypeCast<uint>(snapshotData + snapshotStride*i, snapshotOffset) = (uint)len;
                GhostComponentSerializer.TypeCast<uint>(snapshotData + snapshotStride*i, snapshotOffset+4) = (uint)dynamicSnapshotDataOffset;

                var maskSize = SnapshotDynamicBuffersHelper.GetDynamicDataChangeMaskSize(changeMaskBits, len);
                CheckDynamicDataRange(dynamicSnapshotDataOffset, maskSize, len, dynamicDataSize, dynamicSnapshotMaxOffset);

                if (len > 0)
                {
                    //Copy the buffer contents
                    IntPtr curCompData = GhostComponentSerializer.TypeCast<IntPtr>(componentData, UnsafeUtility.SizeOf<IntPtr>()*i);
                    CopyBufferToSnapshot(stateData, snapshotDynamicDataPtr + maskSize, dynamicSnapshotDataOffset, dynamicDataSize, curCompData, UnsafeUtility.SizeOf<TComponentType>(), len);
                }
                SerializeOneBuffer(i,
                    snapshotData, snapshotOffset, snapshotStride,
                    maskOffsetInBits, changeMaskBits, baselines,
                    ref writer, compressionModel, entityStartBit, snapshotDynamicDataPtr,
                    dynamicSizePerEntity, len,
                    ref dynamicSnapshotDataOffset, dynamicDataSize, maskSize);
            }
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostComponentSerializer.CopyToFromSnapshotDelegate))]
        private static void CopyBufferToSnapshot([NoAlias]IntPtr stateData, [NoAlias]IntPtr snapshotData, int snapshotOffset, int snapshotStride, [NoAlias]IntPtr componentData, int componentStride, int count)
        {
            var serializer = default(TSerializer);
            for (int i = 0; i < count; ++i)
            {
                ref readonly var serializerState = ref GhostComponentSerializer.TypeCastReadonly<GhostSerializerState>(stateData);
                serializer.CopyToSnapshot(serializerState, snapshotData + snapshotOffset + snapshotStride*i, componentData + componentStride*i);
            }
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostComponentSerializer.CopyToFromSnapshotDelegate))]
        private static void CopyBufferFromSnapshot([NoAlias]IntPtr stateData, [NoAlias]IntPtr snapshotData, int snapshotOffset, int snapshotStride,
            [NoAlias]IntPtr componentData, int componentStride, int count)
        {
            var deserializerState = GhostComponentSerializer.TypeCast<GhostDeserializerState>(stateData);
            var serializer = default(TSerializer);
            ref var snapshotInterpolationData = ref GhostComponentSerializer.TypeCast<SnapshotData.DataAtTick>(snapshotData);
            deserializerState.SnapshotTick = snapshotInterpolationData.Tick;
            for (int i = 0; i < count; ++i)
            {
                //For buffers the function iterate over the element in the buffers not entities.
                var snapshotBefore = snapshotInterpolationData.SnapshotBefore + snapshotOffset +snapshotStride * i;
                serializer.CopyFromSnapshot(deserializerState, componentData + componentStride*i,
                    snapshotInterpolationData.InterpolationFactor, snapshotInterpolationData.InterpolationFactor,
                    snapshotBefore, snapshotBefore);
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckDynamicDataRange(int dynamicSnapshotDataOffset, int maskSize, int len, int dynamicDataSize, int dynamicSnapshotMaxOffset)
        {
            if ((dynamicSnapshotDataOffset + maskSize + len*dynamicDataSize) > dynamicSnapshotMaxOffset)
                throw new InvalidOperationException("writing snapshot dyanmicdata outside of memory history buffer memory boundary");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckDynamicMaskOffset(int offset, int sizeInBytes)
        {
            if (offset > sizeInBytes*8)
                throw new InvalidOperationException("writing dynamic mask bits outside out of bound");
        }

        const int IntSize = 4;
        const int BaselinesPerEntity = 4;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SerializeOneBuffer(int ent, [NoAlias]IntPtr snapshotData,
            int snapshotOffset, int snapshotStride,
            int maskOffsetInBits, int changeMaskBits,
            [NoAlias]IntPtr baselines,
            ref DataStreamWriter writer, in StreamCompressionModel compressionModel, [NoAlias]IntPtr entityStartBit,
            [NoAlias]IntPtr snapshotDynamicDataPtr, [NoAlias]IntPtr dynamicSizePerEntity,
            int len, ref int dynamicSnapshotDataOffset, int dynamicDataSize, int maskSize)
        {
            int PtrSize = UnsafeUtility.SizeOf<IntPtr>();
            var baseline0Ptr = GhostComponentSerializer.TypeCast<IntPtr>(baselines, PtrSize*ent*BaselinesPerEntity);
            var baselineDynamicDataPtr = GhostComponentSerializer.TypeCast<IntPtr>(baselines, PtrSize*(ent*BaselinesPerEntity+3));
            var changeMaskPtr = snapshotData + sizeof(int) + ent * snapshotStride;
            ref var startuint = ref GhostComponentSerializer.TypeCast<int>(entityStartBit, IntSize*2*ent);
            startuint = writer.Length/IntSize;

            var serializer = default(TSerializer);
            DefaultBufferSerialization.SerializeBufferToStream(
                serializer,
                baseline0Ptr, snapshotOffset,
                changeMaskPtr, maskOffsetInBits, changeMaskBits,
                snapshotDynamicDataPtr, baselineDynamicDataPtr, len, dynamicSnapshotDataOffset,
                dynamicDataSize, maskSize, ref writer, compressionModel);

            var dynamicSize = GhostComponentSerializer.SnapshotSizeAligned(maskSize + dynamicDataSize * len);
            GhostComponentSerializer.TypeCast<int>(dynamicSizePerEntity, ent*IntSize) += dynamicSize;
            dynamicSnapshotDataOffset += dynamicSize;
            ref var sbit = ref GhostComponentSerializer.TypeCast<int>(entityStartBit, IntSize*2*ent+IntSize);
            sbit = writer.LengthInBits - startuint*32;
            var missing = 32-writer.LengthInBits&31;
            if (missing < 32)
                writer.WriteRawBits(0, missing);
        }
    }
}
