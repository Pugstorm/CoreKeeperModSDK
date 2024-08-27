using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;

namespace Unity.NetCode.LowLevel.Unsafe
{
    static internal class DefaultBufferSerialization
    {
        public static unsafe void SerializeBufferToStream<T>(
            T serializer,
            [NoAlias]IntPtr baselinePtr, int snapshotOffset,
            [NoAlias]IntPtr changeMaskData, int maskOffsetInBits, int changeMaskBits,
            [NoAlias]IntPtr snapshotDynamicDataPtr, [NoAlias]IntPtr baselineDynamicDataPtr,
            int len, int dynamicSnapshotDataOffset, int dynamicDataSize, int maskSize,
            ref DataStreamWriter writer, in StreamCompressionModel compressionModel)
            where T: unmanaged, IGhostSerializer
        {
            const int IntSize = 4;
            int baseLen = 0;
            int baseOffset = 0;
            if (baselinePtr != IntPtr.Zero)
            {
                baseLen = (int)GhostComponentSerializer.TypeCast<uint>(baselinePtr, snapshotOffset);
                baseOffset = (int)GhostComponentSerializer.TypeCast<uint>(baselinePtr, snapshotOffset+IntSize);
            }

            // Calculate change masks for dynamic data
            var dynamicMaskUints = GhostComponentSerializer.ChangeMaskArraySizeInUInts(changeMaskBits * len);
            var dynamicMaskBitsPtr = snapshotDynamicDataPtr + dynamicSnapshotDataOffset;

            var dynamicMaskOffset = 0;
            var offset = dynamicSnapshotDataOffset;
            var bOffset = baseOffset;
            if (len == baseLen)
            {
                for (int j = 0; j < len; ++j)
                {
                    CheckDynamicMaskOffset(dynamicMaskOffset, maskSize);
                    serializer.CalculateChangeMask(
                        snapshotDynamicDataPtr + maskSize + offset,
                        baselineDynamicDataPtr + maskSize + bOffset,
                        dynamicMaskBitsPtr, dynamicMaskOffset);
                    offset += dynamicDataSize;
                    bOffset += dynamicDataSize;
                    dynamicMaskOffset += changeMaskBits;
                }
                // Calculate any change mask and set the dynamic snapshot mask
                uint anyChangeMask = 0;

                //Cleanup the remaining bits for the changemasks
                var changeMaskLenInBits = changeMaskBits * len;
                var remaining = (changeMaskBits * len)&31;
                if(remaining > 0)
                    GhostComponentSerializer.CopyToChangeMask(snapshotDynamicDataPtr + dynamicSnapshotDataOffset, 0, changeMaskLenInBits, 32-remaining);
                for (int mi = 0; mi < dynamicMaskUints; ++mi)
                {
                    uint changeMaskUint = GhostComponentSerializer.TypeCast<uint>(snapshotDynamicDataPtr + dynamicSnapshotDataOffset, mi*IntSize);
                    anyChangeMask |= (changeMaskUint!=0)?1u:0;
                }
                GhostComponentSerializer.CopyToChangeMask(changeMaskData, anyChangeMask, maskOffsetInBits, 2);
                //We can early exit here if the buffer has zero changes. There is no need to serialize on the network
                //a stream of zeros. Neither the changemask nor the buffer contents need to be written in this case.
                if (anyChangeMask == 0)
                    return;

                // Write the bits to the data stream
                for (int mi = 0; mi < dynamicMaskUints; ++mi)
                {
                    uint changeMaskUint = GhostComponentSerializer.TypeCast<uint>(snapshotDynamicDataPtr + dynamicSnapshotDataOffset, mi*IntSize);
                    uint changeBaseMaskUint = GhostComponentSerializer.TypeCast<uint>(baselineDynamicDataPtr + baseOffset, mi*IntSize);
                    writer.WritePackedUIntDelta(changeMaskUint, changeBaseMaskUint, compressionModel);
                }
            }
            else
            {
                // Clear the dynamic change mask to all 1
                // var remaining = changeMaskBits * len;
                // while (remaining > 32)
                // {
                //     GhostComponentSerializer.CopyToChangeMask(dynamicMaskBitsPtr, ~0u, dynamicMaskOffset, 32);
                //     dynamicMaskOffset += 32;
                //     remaining -= 32;
                // }
                // if (remaining > 0)
                //     GhostComponentSerializer.CopyToChangeMask(dynamicMaskBitsPtr, (1u<<remaining)-1, dynamicMaskOffset, remaining);
                // // FIXME: setting the bits as above is more correct, but requires changes to the receive system making it incompatible with the v1 serializer
                for (int j = 0; j < maskSize; ++j)
                    GhostComponentSerializer.TypeCast<byte>(dynamicMaskBitsPtr, j) = 0xff;
                // Set the dynamic snapshot mask
                GhostComponentSerializer.CopyToChangeMask(changeMaskData, 3, maskOffsetInBits, 2);

                baselineDynamicDataPtr = IntPtr.Zero;
                writer.WritePackedUIntDelta((uint)len, (uint)baseLen, compressionModel);

                //Assume all changed so no changemask are present (they will be considered all 1s)
            }
            //Serialize the elements contents
            dynamicMaskOffset = 0;
            offset = dynamicSnapshotDataOffset;
            bOffset = baseOffset;
            if (baselineDynamicDataPtr != IntPtr.Zero)
            {
                for (int j = 0; j < len; ++j)
                {
                    var baselineData = baselineDynamicDataPtr + maskSize + bOffset;
                    serializer.Serialize(
                        snapshotDynamicDataPtr + maskSize + offset,
                        baselineData, dynamicMaskBitsPtr, dynamicMaskOffset, ref writer, compressionModel);
                    offset += dynamicDataSize;
                    bOffset += dynamicDataSize;
                    dynamicMaskOffset += changeMaskBits;
                }
            }
            else
            {
                var defaulteElementBaseline = stackalloc byte[serializer.SizeInSnapshot];
                for (int j = 0; j < len; ++j)
                {
                    serializer.Serialize(
                        snapshotDynamicDataPtr + maskSize + offset,
                        (IntPtr)defaulteElementBaseline, dynamicMaskBitsPtr, dynamicMaskOffset, ref writer, compressionModel);
                    offset += dynamicDataSize;
                    bOffset += dynamicDataSize;
                    dynamicMaskOffset += changeMaskBits;
                }
            }
        }
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckDynamicMaskOffset(int offset, int sizeInBytes)
        {
            if (offset > sizeInBytes*8)
                throw new InvalidOperationException("writing dynamic mask bits outside out of bound");
        }
    }
}
