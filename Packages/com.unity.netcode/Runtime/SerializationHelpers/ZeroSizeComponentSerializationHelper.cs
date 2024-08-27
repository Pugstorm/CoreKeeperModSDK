using System;
using AOT;
using Unity.Burst;
using Unity.Collections;
using Unity.NetCode.LowLevel.Unsafe;

namespace Unity.NetCode
{
    [BurstCompile]
    static class ZeroSizeComponentSerializationHelper
    {
        public static bool SetupFunctionPointers(ref GhostComponentSerializer.State state)
        {
            if (state.ComponentType.IsBuffer)
            {
                state.PostSerializeBuffer = new PortableFunctionPointer<GhostComponentSerializer.PostSerializeBufferDelegate>(PostSerializeBuffer);
                state.SerializeBuffer = new PortableFunctionPointer<GhostComponentSerializer.SerializeBufferDelegate>(SerializeBuffer);
            }
            else
            {
                state.PostSerialize = new PortableFunctionPointer<GhostComponentSerializer.PostSerializeDelegate>(PostSerialize);
                state.Serialize = new PortableFunctionPointer<GhostComponentSerializer.SerializeDelegate>(Serialize);
            }
            state.CopyToSnapshot = new PortableFunctionPointer<GhostComponentSerializer.CopyToFromSnapshotDelegate>(CopyToSnapshot);
            state.CopyFromSnapshot = new PortableFunctionPointer<GhostComponentSerializer.CopyToFromSnapshotDelegate>(CopyFromSnapshot);
            state.RestoreFromBackup = new PortableFunctionPointer<GhostComponentSerializer.RestoreFromBackupDelegate>(RestoreFromBackup);
            state.PredictDelta = new PortableFunctionPointer<GhostComponentSerializer.PredictDeltaDelegate>(PredictDelta);
            state.Deserialize = new PortableFunctionPointer<GhostComponentSerializer.DeserializeDelegate>(Deserialize);
#if UNITY_EDITOR || NETCODE_DEBUG
            state.ReportPredictionErrors = new PortableFunctionPointer<GhostComponentSerializer.ReportPredictionErrorsDelegate>(ReportPredictionErrors);
            state.PredictionErrorNames = new FixedString512Bytes();
#endif
            return true;
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostComponentSerializer.PostSerializeDelegate))]
        private static void PostSerialize(IntPtr snapshotData, int snapshotOffset, int snapshotStride,
            int maskOffsetInBits, int count, IntPtr baselines, ref DataStreamWriter writer,
            ref StreamCompressionModel compressionModel, IntPtr entityStartBit)
        {
            // TODO: Move this outside code-gen, as we really dont need to do this here!
            for (int i = 0; i < count; ++i)
            {
                const int IntSize = 4;
                ref var startuint = ref GhostComponentSerializer.TypeCast<int>(entityStartBit, IntSize*2*i);
                startuint = writer.Length/IntSize;
                startuint = ref GhostComponentSerializer.TypeCast<int>(entityStartBit, IntSize*2*i+IntSize);
                startuint = 0;
            }
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostComponentSerializer.SerializeDelegate))]
        private static void Serialize(IntPtr stateData,
            IntPtr snapshotData, int snapshotOffset, int snapshotStride, int maskOffsetInBits,
            IntPtr componentData, int count, IntPtr baselines,
            ref DataStreamWriter writer, ref StreamCompressionModel compressionModel, IntPtr entityStartBit)
        {
            // TODO: Move this outside code-gen, as we really dont need to do this here!
            for (int ent = 0; ent < count; ++ent)
            {
                const int IntSize = 4;
                ref var startuint = ref GhostComponentSerializer.TypeCast<int>(entityStartBit, IntSize*2*ent);
                startuint = writer.Length/IntSize;
                startuint = ref GhostComponentSerializer.TypeCast<int>(entityStartBit, IntSize*2*ent+IntSize);
                startuint = 0;
            }
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostComponentSerializer.PostSerializeDelegate))]
        private static void PostSerializeBuffer(
            IntPtr snapshotData, int snapshotOffset,
            int snapshotStride, int maskOffsetInBits, int changeMaskBits, int count, IntPtr baselines, ref DataStreamWriter writer,
            ref StreamCompressionModel compressionModel, IntPtr entityStartBit, IntPtr snapshotDynamicDataPtr,
            IntPtr dynamicSizePerEntity, int dynamicSnapshotMaxOffset)
        {
            // TODO: Move this outside code-gen, as we really dont need to do this here!
            for (int i = 0; i < count; ++i)
            {
                const int IntSize = 4;
                ref var startuint = ref GhostComponentSerializer.TypeCast<int>(entityStartBit, IntSize*2*i);
                startuint = writer.Length/IntSize;
                startuint = ref GhostComponentSerializer.TypeCast<int>(entityStartBit, IntSize*2*i+IntSize);
                startuint = 0;
            }
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostComponentSerializer.SerializeBufferDelegate))]
        private static void SerializeBuffer(
            IntPtr stateData,
            IntPtr snapshotData, int snapshotOffset, int snapshotStride, int maskOffsetInBits, int changeMaskBits,
            IntPtr componentData, IntPtr componentDataLen, int count, IntPtr baselines,
            ref DataStreamWriter writer, ref StreamCompressionModel compressionModel,
            IntPtr entityStartBit, IntPtr snapshotDynamicDataPtr, ref int dynamicSnapshotDataOffset,
            IntPtr dynamicSizePerEntity, int dynamicSnapshotMaxOffset)
        {
            // TODO: Move this outside code-gen, as we really dont need to do this here!
            for (int i = 0; i < count; ++i)
            {
                const int IntSize = 4;
                ref var startuint = ref GhostComponentSerializer.TypeCast<int>(entityStartBit, IntSize*2*i);
                startuint = writer.Length/IntSize;
                startuint = ref GhostComponentSerializer.TypeCast<int>(entityStartBit, IntSize*2*i+IntSize);
                startuint = 0;
            }
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostComponentSerializer.RestoreFromBackupDelegate))]
        private static void RestoreFromBackup(IntPtr componentData, IntPtr backupData)
        {
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostComponentSerializer.PredictDeltaDelegate))]
        private static void PredictDelta(IntPtr snapshotData, IntPtr baseline1Data, IntPtr baseline2Data, ref GhostDeltaPredictor predictor)
        {
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostComponentSerializer.DeserializeDelegate))]
        private static void Deserialize(IntPtr snapshotData, IntPtr baselineData,
            ref DataStreamReader reader, ref StreamCompressionModel compressionModel, IntPtr changeMaskData, int startOffset)
        {
        }

#if UNITY_EDITOR || NETCODE_DEBUG
        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostComponentSerializer.ReportPredictionErrorsDelegate))]
        private static void ReportPredictionErrors(IntPtr componentData, IntPtr backupData, IntPtr errorsList, int errorsCount)
        {
        }
#endif

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostComponentSerializer.CopyToFromSnapshotDelegate))]
        private static void CopyToSnapshot(IntPtr stateData, IntPtr snapshotData, int snapshotOffset, int snapshotStride, IntPtr componentData, int componentStride, int count)
        {
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostComponentSerializer.CopyToFromSnapshotDelegate))]
        private static void CopyFromSnapshot(IntPtr stateData, IntPtr snapshotData, int snapshotOffset, int snapshotStride, IntPtr componentData, int componentStride, int count)
        {
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostComponentSerializer.CopyToFromSnapshotDelegate))]
        private static void CopyBufferFromSnapshot(IntPtr stateData, IntPtr snapshotData, int snapshotOffset, int snapshotStride, IntPtr componentData, int componentStride, int bufferLen)
        {
        }
    }
}
