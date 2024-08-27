using System;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace Unity.NetCode
{
    internal unsafe static class GhostComponentSerializerExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static internal void Invoke(this PortableFunctionPointer<LowLevel.Unsafe.GhostComponentSerializer.PostSerializeDelegate> function,
            IntPtr snapshotData, int snapshotOffset, int snapshotStride, int maskOffsetInBits, int count,
            IntPtr baselines,
            ref DataStreamWriter writer, ref StreamCompressionModel compressionModel, IntPtr entityStartBit)
        {
            ((delegate *unmanaged[Cdecl]<IntPtr,int,int,int,int,IntPtr,ref DataStreamWriter,ref StreamCompressionModel,IntPtr, void>)
                function.Ptr.Value)(snapshotData,snapshotOffset,snapshotStride,maskOffsetInBits,count,baselines,ref writer,ref compressionModel,entityStartBit);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static internal void Invoke(this PortableFunctionPointer<LowLevel.Unsafe.GhostComponentSerializer.PostSerializeBufferDelegate> function,
            IntPtr snapshotData, int snapshotOffset, int snapshotStride, int maskOffsetInBits, int changeMaskBits,
            int count, IntPtr baselines, ref DataStreamWriter writer, ref StreamCompressionModel compressionModel,
            IntPtr entityStartBit, IntPtr snapshotDynamicDataPtr, IntPtr dynamicSizePerEntity,
            int dynamicSnapshotMaxOffset)
        {
            ((delegate *unmanaged[Cdecl]<IntPtr,int,int,int,int,int, IntPtr,ref DataStreamWriter,ref StreamCompressionModel,IntPtr,IntPtr,IntPtr,int, void>)function.Ptr.Value)(
                snapshotData,snapshotOffset,snapshotStride,maskOffsetInBits,changeMaskBits,count,baselines,ref writer,ref compressionModel,entityStartBit,snapshotDynamicDataPtr,dynamicSizePerEntity,dynamicSnapshotMaxOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static internal void Invoke(this PortableFunctionPointer<LowLevel.Unsafe.GhostComponentSerializer.SerializeDelegate> function,
            IntPtr stateData, IntPtr snapshotData, int snapshotOffset, int snapshotStride, int maskOffsetInBits,
            IntPtr componentData, int count, IntPtr baselines, ref DataStreamWriter writer,
            ref StreamCompressionModel compressionModel, IntPtr entityStartBit)
        {
            ((delegate *unmanaged[Cdecl]<IntPtr,IntPtr,int,int,int,IntPtr,int,IntPtr,ref DataStreamWriter,ref StreamCompressionModel,IntPtr, void>)function.Ptr.Value)(stateData,snapshotData,snapshotOffset,snapshotStride,maskOffsetInBits,componentData,count,baselines,ref writer,ref compressionModel,entityStartBit);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static internal void Invoke(this PortableFunctionPointer<LowLevel.Unsafe.GhostComponentSerializer.SerializeBufferDelegate> function,
            IntPtr stateData, IntPtr snapshotData, int snapshotOffset, int snapshotStride, int maskOffsetInBits,
            int changeMaskBits, IntPtr componentData, IntPtr componentDataLen, int count, IntPtr baselines,
            ref DataStreamWriter writer, ref StreamCompressionModel compressionModel, IntPtr entityStartBit,
            IntPtr snapshotDynamicDataPtr, ref int snapshotDynamicDataOffset, IntPtr dynamicSizePerEntity,
            int dynamicSnapshotMaxOffset)
        {
            ((delegate *unmanaged[Cdecl]<IntPtr,IntPtr,int,int,int,int,IntPtr,IntPtr,int,IntPtr,ref DataStreamWriter,ref StreamCompressionModel,IntPtr,IntPtr,ref int,IntPtr,int, void>)function.Ptr.Value)(
                stateData,snapshotData,snapshotOffset,snapshotStride,maskOffsetInBits, changeMaskBits, componentData,componentDataLen,count,baselines,ref writer,ref compressionModel,entityStartBit,snapshotDynamicDataPtr,ref snapshotDynamicDataOffset,dynamicSizePerEntity,dynamicSnapshotMaxOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static internal void Invoke(this PortableFunctionPointer<LowLevel.Unsafe.GhostComponentSerializer.CopyToFromSnapshotDelegate> function,
            IntPtr stateData, IntPtr snapshotData, int snapshotOffset, int snapshotStride, IntPtr componentData,
            int componentStride, int count)
        {
            ((delegate *unmanaged[Cdecl]<IntPtr,IntPtr,int,int,IntPtr,int,int, void>)function.Ptr.Value)(stateData,snapshotData,snapshotOffset,snapshotStride,componentData,componentStride,count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static internal void Invoke(this PortableFunctionPointer<LowLevel.Unsafe.GhostComponentSerializer.RestoreFromBackupDelegate> function,
            IntPtr componentData, IntPtr backupData)
        {
            ((delegate *unmanaged[Cdecl]<IntPtr,IntPtr, void>)function.Ptr.Value)(componentData,backupData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static internal void Invoke(this PortableFunctionPointer<LowLevel.Unsafe.GhostComponentSerializer.PredictDeltaDelegate> function,
            IntPtr snapshotData, IntPtr baseline1Data, IntPtr baseline2Data, ref GhostDeltaPredictor predictor)
        {
            ((delegate *unmanaged[Cdecl]<IntPtr,IntPtr,IntPtr,ref GhostDeltaPredictor, void>)function.Ptr.Value)(snapshotData,baseline1Data,baseline2Data,ref predictor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static internal void Invoke(this PortableFunctionPointer<LowLevel.Unsafe.GhostComponentSerializer.DeserializeDelegate> function,
            IntPtr snapshotData, IntPtr baselineData, ref DataStreamReader reader,
            ref StreamCompressionModel compressionModel, IntPtr changeMaskData, int startOffset)
        {
            ((delegate *unmanaged[Cdecl]<IntPtr,IntPtr,ref DataStreamReader,ref StreamCompressionModel,IntPtr,int, void>)function.Ptr.Value)(snapshotData,baselineData,ref reader,ref compressionModel,changeMaskData,startOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static internal void Invoke(this PortableFunctionPointer<LowLevel.Unsafe.GhostComponentSerializer.ReportPredictionErrorsDelegate>
                function, IntPtr componentData, IntPtr backupData, IntPtr errorsList, int errorsCount)
        {
            ((delegate *unmanaged[Cdecl]<IntPtr,IntPtr,IntPtr,int, void>)function.Ptr.Value)(componentData,backupData,errorsList,errorsCount);
        }
    }
}
