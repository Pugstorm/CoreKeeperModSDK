using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using System.Runtime.InteropServices;
namespace Unity.NetCode
{
    /// <summary>
    /// For internal use only.
    /// The interface for all the code-generated ISystems responsible for registering all the generated component
    /// serializers into the <see cref="GhostComponentSerializerCollectionSystemGroup"/>.
    /// </summary>
    public interface IGhostComponentSerializerRegistration
    {}
}

namespace Unity.NetCode.LowLevel.Unsafe
{
    /// <summary>
    /// Mostly for internal use. A collection helper functions used by code-gen and some runtime systems.
    /// See <see cref="GhostSendSystem"/>, <see cref="GhostReceiveSystem"/>, and others.
    /// To work with ghost snapshots, see <see cref="SnapshotData"/> and <see cref="SnapshotDynamicDataBuffer"/>.
    /// It also declares all the ghost component/buffers serializers delegate methods, that are used to register
    /// (at runtime) the code-generated serializers (to the <see cref="GhostComponentSerializer.State"/> collection).
    /// </summary>
    public unsafe struct GhostComponentSerializer
    {
        ///<summary>
        /// Dynamic Buffer have a special entry in the snapshot data that is used to track the len and offset of the
        /// the buffer data inside the <see cref="SnapshotDynamicDataBuffer"/> buffer. This shadow component entry has the
        /// following format:
        /// <list type="bullet">
        /// <li>uint Length: the length of the buffer</li>
        /// <li>uint Offset: the position in bytes from the beginning of the dynamic data buffer (for that specific history slot)</li>
        /// </list>
        /// </summary>
        public const int DynamicBufferComponentSnapshotSize = sizeof(uint) + sizeof(uint);
        /// <summary>
        /// The number of change mask bits used the shadow buffer data. The change mask for the buffer is like this:
        /// <list type="bullet">
        /// <li>00 : nothing change</li>
        /// <li>01 : len is the same, content has changed.</li>
        /// <li>10 : len is changed, we consider the content has changed too. (may change in the future).</li>
        /// </list>
        /// </summary>
        public const int DynamicBufferComponentMaskBits = 2;
        /// <summary>
        /// A bitflag used to mark to which ghost type a component should be serialized to.
        /// </summary>
        /// <remarks>Duplicates <see cref="GhostSendType"/>, which should be used instead.</remarks>
        [Flags]
        [Obsolete("Due to changes to the source generator, this enum is now both redundant and deprecated, as it duplicates `GhostSendType`. Unfortunately, not UnityUpgradable to GhostSendType as enum names have changed. (RemovedAfter Entities 1.0)", false)]
        public enum SendMask
        {
            /// <summary>
            /// The component should be not replicated.
            /// </summary>
            /// <remarks>Maps to <see cref="GhostSendType.DontSend"/>.</remarks>
            None = 0,
            /// <summary>
            /// The component is replicated only to interpolated ghosts.
            /// </summary>
            /// <remarks>Maps to <see cref="GhostSendType.OnlyInterpolatedClients"/>.</remarks>
            Interpolated = 1,
            /// <summary>
            /// The component is replicated only to predicted ghosts.
            /// </summary>
            /// <remarks>Maps to <see cref="GhostSendType.OnlyPredictedClients"/>.</remarks>
            Predicted = 2,
        }

        /// <summary>
        /// Delegate method to use to post-serialize the component when the ghost use pre-serialization optimization.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void PostSerializeDelegate(IntPtr snapshotData, int snapshotOffset, int snapshotStride, int maskOffsetInBits, int count, IntPtr baselines, ref DataStreamWriter writer, ref StreamCompressionModel compressionModel, IntPtr entityStartBit);
        /// <summary>
        /// Delegate method to use to post-serialize buffers when the ghost use pre-serialization optimization.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void PostSerializeBufferDelegate(IntPtr snapshotData, int snapshotOffset, int snapshotStride, int maskOffsetInBits, int changeMaskBits, int count, IntPtr baselines, ref DataStreamWriter writer, ref StreamCompressionModel compressionModel, IntPtr entityStartBit, IntPtr snapshotDynamicDataPtr, IntPtr dynamicSizePerEntity, int dynamicSnapshotMaxOffset);
        /// <summary>
        /// Delegate method used to serialize the component data for the root entity into the outgoing data stream.
        /// Works in batches.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SerializeDelegate(IntPtr stateData, IntPtr snapshotData, int snapshotOffset, int snapshotStride, int maskOffsetInBits, IntPtr componentData, int count, IntPtr baselines, ref DataStreamWriter writer, ref StreamCompressionModel compressionModel, IntPtr entityStartBit);
        /// <summary>
        /// Delegate method used to serialize the component data present in the child entity into the outgoing data stream.
        /// Works on a single entity at time.
        /// </summary>
        [Obsolete("The SerializeChildDelegate delegate has been deprecated and will be removed. Please use only use the SerializeDelegate instead", false)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SerializeChildDelegate(IntPtr stateData, IntPtr snapshotData, int snapshotOffset, int snapshotStride, int maskOffsetInBits, IntPtr componentData, int count, IntPtr baselines, ref DataStreamWriter writer, ref StreamCompressionModel compressionModel, IntPtr entityStartBit);
        /// <summary>
        /// Delegate method used to serialize the buffer content for the whole chunk.
        /// Works in batches.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SerializeBufferDelegate(IntPtr stateData, IntPtr snapshotData, int snapshotOffset, int snapshotStride, int maskOffsetInBits, int changeMaskBits, IntPtr componentData, IntPtr componentDataLen, int count, IntPtr baselines, ref DataStreamWriter writer, ref StreamCompressionModel compressionModel, IntPtr entityStartBit, IntPtr snapshotDynamicDataPtr, ref int snapshotDynamicDataOffset, IntPtr dynamicSizePerEntity, int dynamicSnapshotMaxOffset);
        /// <summary>
        /// Delegate method used to transfer the component data to/from the snapshot buffer.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CopyToFromSnapshotDelegate(IntPtr stateData, IntPtr snapshotData, int snapshotOffset, int snapshotStride, IntPtr componentData, int componentStride, int count);
        /// <summary>
        /// Delegate method used to restore the state of a replicated component from the <see cref="GhostPredictionHistoryState"/>
        /// buffer. Because the history buffer perform a memory copy of the whole component data, it is necessary to call this method to
        /// ensure only the replicated portion of the component is actually restored.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void RestoreFromBackupDelegate(IntPtr componentData, IntPtr backupData);
        /// <summary>
        /// Calculate the prediction delta for components and buffer. Used for delta-compression.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void PredictDeltaDelegate(IntPtr snapshotData, IntPtr baseline1Data, IntPtr baseline2Data, ref GhostDeltaPredictor predictor);
        /// <summary>
        /// Deserialize the component and buffer data from the received snapshot and store it inside the <see cref="SnapshotDataBuffer"/>.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DeserializeDelegate(IntPtr snapshotData, IntPtr baselineData, ref DataStreamReader reader, ref StreamCompressionModel compressionModel, IntPtr changeMaskData, int startOffset);
        /// <summary>
        /// Delegate used by the <see cref="GhostPredictionDebugSystem"/>, collect and report the prediction error
        /// for all the replicated fields.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ReportPredictionErrorsDelegate(IntPtr componentData, IntPtr backupData, IntPtr errorsList, int errorsCount);

        /// <summary>
        ///     This buffer is added to the GhostCollection singleton entity.
        ///     Stores serialization meta-data for the ghost.
        ///     Too large to be stored in chunk memory.
        ///     Values are generated by the Source Generators.
        /// </summary>
        [InternalBufferCapacity(0)]
        public struct State : IBufferElementData
        {
            /// <summary>
            /// An unique hash computed by source generator that identify the serializer type.
            /// </summary>
            public ulong SerializerHash;
            /// <summary>
            /// The hash of all serializer fields, along with their <see cref="GhostFieldAttribute"/> options properties.
            /// Used to calculate the <see cref="NetworkProtocolVersion"/>.
            /// </summary>
            public ulong GhostFieldsHash;
            /// <summary>
            /// An hash identifying the specific variation used for this serializer (see <see cref="GhostComponentVariationAttribute"/>).
            /// If no variation is used, this will be the hash of the <see cref="ComponentType"/> itself, and <see cref="IsDefaultSerializer"/> will be true.
            /// </summary>
            public ulong VariantHash;
            /// <summary>
            /// The type of component this serializer act on.
            /// </summary>
            public ComponentType ComponentType;
            /// <summary>
            /// Internal. Indexer into the <see cref="GhostComponentSerializerCollectionData.SerializationStrategies"/> list.
            /// </summary>
            public short SerializationStrategyIndex;
            /// <summary>
            /// The size of the component, as reported by the <see cref="Entities.TypeManager"/>.
            /// </summary>
            public int ComponentSize;
            /// <summary>
            /// The size of the component inside the snapshot buffer.
            /// </summary>
            public int SnapshotSize;
            /// <summary>
            /// Whether SnapshotSize is greater than zero.
            /// </summary>
            public bool HasGhostFields => SnapshotSize > 0;
            /// <summary>
            /// The number of bits necessary for the change mask.
            /// </summary>
            public int ChangeMaskBits;
            /// <summary>True if this component has the <see cref="GhostEnabledBitAttribute"/> and thus should replicate the enable bit flag.</summary>
            /// <remarks>Note that serializing the enabled bit is different from the main "serializer". I.e. "Empty Variants" can have serialized enable bits.</remarks>
            public byte SerializesEnabledBit;
            /// <summary>
            /// Store the <see cref="GhostComponentAttribute.PrefabType"/> if the attribute is present on the component. Otherwise is set
            /// to <see cref="GhostPrefabType.All"/>.
            /// TODO - Try to deduplicate this data by reading the ComponentTypeSerializationStrategy directly.
            /// </summary>
            public GhostPrefabType PrefabType;
            /// <summary>
            /// Indicates for which type of ghosts the component should be replicated. The mask is set by code-gen base on the
            /// <see cref="PrefabType"/> constraint.
            /// </summary>
            public GhostSendType SendMask;
            /// <summary>
            /// Store the <see cref="GhostComponentAttribute.OwnerSendType"/> if the attribute is present on the component. Otherwise is set
            /// to <see cref="SendToOwnerType.All"/>.
            /// </summary>
            public SendToOwnerType SendToOwner;
            /// <summary>
            /// Delegate method to use to post-serialize the component when the ghost use pre-serialization optimization.
            /// </summary>
            public PortableFunctionPointer<PostSerializeDelegate> PostSerialize;
            /// <summary>
            /// Delegate method to use to post-serialize buffers when the ghost use pre-serialization optimization.
            /// </summary>
            public PortableFunctionPointer<PostSerializeBufferDelegate> PostSerializeBuffer;
            /// <summary>
            /// Delegate method used to serialize the component data for the root entity into the outgoing data stream. Work in batch.
            /// </summary>
            public PortableFunctionPointer<SerializeDelegate> Serialize;
            /// <summary>
            /// Delegate method used to serialize the component data present in the child entity into the outgoing data stream.
            /// Work on a single entity at time.
            /// </summary>
            [Obsolete("The SerializeChild method has been deprecated. Please use only Serialize instead", false)]
            public PortableFunctionPointer<SerializeChildDelegate> SerializeChild;
            /// <summary>
            /// Delegate method used to serialize the buffer content for the whole chunk. Work in batch for the whole chunk.
            /// </summary>
            public PortableFunctionPointer<SerializeBufferDelegate> SerializeBuffer;
            /// <summary>
            /// Delegate method used to transfer the component data to the snapshot buffer.
            /// </summary>
            public PortableFunctionPointer<CopyToFromSnapshotDelegate> CopyToSnapshot;
            /// <summary>
            /// Delegate method used to transfer data from the snapshot buffer to the destination component.
            /// </summary>
            public PortableFunctionPointer<CopyToFromSnapshotDelegate> CopyFromSnapshot;
            /// <summary>
            /// Delegate method used to restore the state of a replicated component from the <see cref="GhostPredictionHistoryState"/>
            /// buffer. Because the history buffer perform a memory copy of the whole component data, it is necessary to call this method to
            /// ensure only the replicated portion of the component is actually restored.
            /// </summary>
            public PortableFunctionPointer<RestoreFromBackupDelegate> RestoreFromBackup;
            /// <summary>
            /// Calculate the prediction delta for components and buffer. Used for delta-compression.
            /// </summary>
            public PortableFunctionPointer<PredictDeltaDelegate> PredictDelta;
            /// <summary>
            /// Deserialize the component and buffer data from the received snapshot and store it inside the <see cref="SnapshotDataBuffer"/>.
            /// </summary>
            public PortableFunctionPointer<DeserializeDelegate> Deserialize;
            #if UNITY_EDITOR || NETCODE_DEBUG
            /// <summary>
            /// Used by the <see cref="GhostPredictionDebugSystem"/>, collect and report the prediction error for all the replicated
            /// fields.
            /// </summary>
            public PortableFunctionPointer<ReportPredictionErrorsDelegate> ReportPredictionErrors;
            /// <summary>
            /// Marker used to profile the performance of the serializer.
            /// </summary>
            public Unity.Profiling.ProfilerMarker ProfilerMarker;
            #endif
#if UNITY_EDITOR || NETCODE_DEBUG
            /// <summary>
            /// String buffer, containing the list of all replicated field names. Empty for component type that can be only interpolated.
            /// (see <see cref="PrefabType"/>).
            /// </summary>
            public FixedString512Bytes PredictionErrorNames;
            /// <summary>
            /// The length of the <see cref="PredictionErrorNames"/> list.
            /// </summary>
            internal int NumPredictionErrorNames;
            /// <summary>
            /// The number of predicted errors that is calculated by the  <see cref="ReportPredictionErrorsDelegate"/> method.
            /// Can be larger then the <see cref="NumPredictionErrorNames"/>, since the name list is capped to 512 bytes.
            /// </summary>
            public int NumPredictionErrors;
            /// <summary>
            /// For internal use only. The index inside the prediction error names cache (see <see cref="GhostCollectionSystem"/>).
            /// </summary>
            internal int FirstNameIndex;
            /// <summary>
            /// For internal use only. The hash of the ghost variation type fullname. Used mostly for validation
            /// </summary>
            public ulong VariantTypeFullNameHash;
#endif
        }

        /// <summary>
        /// Helper that returns the size in bytes (aligned to 16 bytes boundary) used to store the component data inside <see cref="SnapshotData"/>.
        /// </summary>
        /// <remarks>
        /// For buffers in particular, the <see cref="SnapshotData"/> contains only offset and length information (the buffer data resides inside the
        /// <see cref="SnapshotDynamicDataBuffer"/>), and the reported size is always equal to the <see cref="GhostComponentSerializer.DynamicBufferComponentSnapshotSize"/>.
        /// </remarks>
        /// <param name="serializer"></param>
        /// <returns></returns>
        public static int SizeInSnapshot(in State serializer)
        {
            return serializer.ComponentType.IsBuffer
                ? SnapshotSizeAligned(GhostComponentSerializer.DynamicBufferComponentSnapshotSize)
                : SnapshotSizeAligned(serializer.SnapshotSize);
        }

        /// <summary>
        /// Helper method to get a reference to a struct data from its address in memory.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="offset"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static ref T TypeCast<T>(IntPtr value, int offset = 0) where T: struct
        {
            return ref UnsafeUtility.AsRef<T>((byte*)value+offset);
        }
        /// <summary>
        /// Helper method to get a reference to a struct data from its address in memory.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="offset"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static ref readonly T TypeCastReadonly<T>(IntPtr value, int offset = 0) where T: struct
        {
            return ref UnsafeUtility.AsRef<T>((byte*)value+offset);
        }
        /// <summary>
        /// Return a pointer to the memory address for the given <paramref name="value"/> instance.
        /// </summary>
        /// <param name="value"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IntPtr IntPtrCast<T>(ref T value) where T: struct
        {
            return (IntPtr)UnsafeUtility.AddressOf(ref value);
        }

        /// <summary>
        /// The compressed size in bits necessary to encode a given unsigned int <paramref name="value"/> in delta in respect
        /// to the given <paramref name="baseline"/>.
        /// </summary>
        /// <param name="value">the value to encode</param>
        /// <param name="baseline">the baseline used to calculate the delta</param>
        /// <param name="model">the compression model to use</param>
        /// <returns>the number of bits necessary to encode the value</returns>
        static public int GetDeltaCompressedSizeInBits(uint value, uint baseline, in StreamCompressionModel model)
        {
            int delta = (int)(baseline - value);
            uint zigZagEncoded = (uint)((delta >> 31) ^ (delta << 1));
            return model.GetCompressedSizeInBits(zigZagEncoded);
        }

        /// <summary>
        /// For internal use only, copy the <paramref name="src"/> bitmask to a destination buffer,
        /// to the given <paramref name="offset"/> and for the required number of bits.
        /// </summary>
        /// <param name="bitData"></param>
        /// <param name="src"></param>
        /// <param name="offset"></param>
        /// <param name="numBits"></param>
        public static void CopyToChangeMask(IntPtr bitData, uint src, int offset, int numBits)
        {
            Assertions.Assert.IsTrue(offset >= 0);
            Assertions.Assert.IsTrue(numBits >= 0);
            Assertions.Assert.IsTrue(numBits <= 32);
            //Expect the src[31:numBits] to be equals to 0.
            var bits = (uint*)bitData;
            int idx = offset >> 5;
            int bitIdx = offset & 0x1f;
            // Clear the bits we are about to write so this function sets them to the correct value even if they are not already zero
            bits[idx] &= (uint)(((1UL << bitIdx)-1) | ~((1UL << (bitIdx+numBits))-1));
            // Align so the first bit of source starts at the specified index and copy the source bits
            bits[idx] |= src << bitIdx;
            // Check how many bits were actually copied, if the source contains more bits than the was copied,
            // align the remaining bits to start at index 0 in the next uint and copy them
            int usedBits = 32 - bitIdx;
            if (numBits > usedBits && usedBits < 32)
            {
                // Clear the bits we are about to write so this function sets them to the correct value even if they are not already zero
                bits[idx+1] &= ~((1u << (numBits-usedBits))-1);
                bits[idx+1] |= src >> usedBits;
            }
        }

        /// <summary>
        /// For internal use only, reset the <paramref name="bitData"/> bitmask bits from the given <paramref name="offset"/>
        /// and for the required number of bits.
        /// </summary>
        /// <param name="bitData"></param>
        /// <param name="offset"></param>
        /// <param name="numBits"></param>
        static public void ResetChangeMask(IntPtr bitData, int offset, int numBits)
        {
            Assertions.Assert.IsTrue(offset >= 0);
            Assertions.Assert.IsTrue(numBits >= 0);
            var bits = (uint*)bitData;
            int idx = offset >> 5;
            int bitIdx = offset & 0x1f;
            var remainingBits = 32 - bitIdx;
            //If the bits fit in the current int mask out the region to 0
            if (numBits < remainingBits)
            {
                bits[idx] &= (uint)(((1UL << bitIdx)-1) | ~((1UL << (bitIdx+numBits))-1));
            }
            else
            {
                //reset up to the next 32-offset bits to 0 (align to the next work)
                bits[idx] &= (uint)(((1UL << bitIdx)-1));
                numBits -= remainingBits;
                //fill to 0 all mask words
                while (numBits > 32)
                {
                    bits[++idx] = 0;
                    numBits -=32;
                }
                //clear the remaining bits in the next change mask uint (starting from offset 0)
                if (numBits > 0)
                {
                    bits[++idx] &= ~((1u << numBits)-1);
                }
            }
        }

        /// <summary>
        /// Reset the changemask and the snapshot data to the default value (all 0)
        /// </summary>
        /// <param name="snapshot"></param>
        /// <param name="snapshotOffset"></param>
        /// <param name="snapshotSize"></param>
        /// <param name="changeMask"></param>
        /// <param name="maskOffset"></param>
        /// <param name="changeMaskBits"></param>
        static public void ClearSnapshotDataAndMask(IntPtr snapshot, int snapshotOffset, int snapshotSize, IntPtr changeMask, int maskOffset,
            int changeMaskBits)
        {
            ResetChangeMask(changeMask, maskOffset, changeMaskBits);
            var componentUintSize = SnapshotSizeAligned(snapshotSize)/4;
            var snapshotData = (uint*)(snapshot + snapshotOffset);
            for(int i=0;i<componentUintSize;++i) snapshotData[i] = 0;
        }

        /// <summary>
        /// For internal use only, reset one bit in the bitmask array at the given <param name="offset"></param>
        /// </summary>
        /// <param name="bitData"></param>
        /// <param name="offset"></param>
        static internal void ResetChangeMaskBit(IntPtr bitData, int offset)
        {
            Assertions.Assert.IsTrue(offset >= 0);
            var bits = (uint*)bitData;
            int idx = offset >> 5;
            int bitIdx = offset & 0x1f;
            bits[idx] &= ~(1U << bitIdx);
        }

        /// <summary>
        /// Extract from the source buffer an unsigned integer, representing a portion of a bitmask
        /// starting from the given offset and number of bits (up to 32 bits max).
        /// </summary>
        /// <param name="bitData"></param>
        /// <param name="offset"></param>
        /// <param name="numBits"></param>
        /// <returns></returns>
        public static uint CopyFromChangeMask(IntPtr bitData, int offset, int numBits)
        {
            Assertions.Assert.IsTrue(offset >= 0);
            Assertions.Assert.IsTrue(numBits >= 0);
            var bits = (uint*)bitData;
            int idx = offset >> 5;
            int bitIdx = offset & 0x1f;
            // Align so the first bit of the big array starts at index 0 in the copied bit mask
            uint result = bits[idx] >> bitIdx;
            // Check how many bits were actually copied, if the source contains more bits than the was copied,
            // align the remaining bits to start at index 0 in the next uint and copy them
            int usedBits = 32 - bitIdx;
            if (numBits > usedBits && usedBits < 32)
                result |= bits[idx+1] << usedBits;
            return result;
        }

        /// <summary>
        /// Helper method to construct an <see cref="UnsafeList{T}"/> from a given IntPtr and length.
        /// </summary>
        /// <param name="floatData"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        public static UnsafeList<float> ConvertToUnsafeList(IntPtr floatData, int len)
        {
            return new UnsafeList<float>((float*)floatData.ToPointer(), len);
        }

        internal static int SnapshotHeaderSizeInBytes(in GhostCollectionPrefabSerializer prefabSerializer)
        {
            return SnapshotSizeAligned(sizeof(uint) + ChangeMaskArraySizeInBytes(prefabSerializer.ChangeMaskBits) + ChangeMaskArraySizeInBytes(prefabSerializer.EnableableBits));
        }

        /// <summary>
        /// Compute the number of uint necessary to encode the required number of bits
        /// </summary>
        /// <param name="numBits"></param>
        /// <returns>The uint mask to encode this number of bits.</returns>
        public static int ChangeMaskArraySizeInUInts(int numBits)
        {
            return (numBits + 31)>>5;
        }

        /// <summary>
        /// Compute the number of bytes necessary to encode the required number of bits
        /// </summary>
        /// <param name="numBits"></param>
        /// <returns>The min number of bytes to store this number of bits, rounded to the nearest 4 bytes (for data-alignment).</returns>
        public static int ChangeMaskArraySizeInBytes(int numBits)
        {
            return ((numBits + 31)>>3) & ~0x3;
        }

        /// <summary>
        /// Align the give size to 16 byte boundary.
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static int SnapshotSizeAligned(int size)
        {
            //TODO: we can use the CollectionHelper.Align for that
            return (size + 15) & (~15);
        }

        /// <summary>
        /// Align the give size to 16 byte boundary
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static uint SnapshotSizeAligned(uint size)
        {
            return (size + 15u) & (~15u);
        }
    }

    internal static class DynamicBufferExtensions
    {
        /// <summary>
        /// Get a readonly reference to the element at the given index.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="index"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns>A readonly reference to the element</returns>
        public static ref readonly T ElementAtRO<T>(this DynamicBuffer<T> buffer, int index) where T: unmanaged, IBufferElementData
        {
            unsafe
            {
                var ptr = (T*)buffer.GetUnsafeReadOnlyPtr();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if(index < 0 || index >= buffer.Length)
                    throw new IndexOutOfRangeException($"Index {index} is out of range in DynamicBuffer of '{buffer.Length}' Length.");
#endif
                return ref ptr[index];
            }
        }
    }
}
