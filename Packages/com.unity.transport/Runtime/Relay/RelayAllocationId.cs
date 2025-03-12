using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport.Logging;

namespace Unity.Networking.Transport.Relay
{
    /// <summary>
    /// The allocation ID is a unique identifier for a connected client/host to a Relay server.
    /// </summary>
    public unsafe struct RelayAllocationId : IEquatable<RelayAllocationId>, IComparable<RelayAllocationId>
    {
        /// <summary>Length of an allocation ID.</summary>
        /// <value>Length in bytes.</value>
        public const int k_Length = 16;

        /// <summary>Raw value of the allocation ID.</summary>
        /// <value>Allocation ID as a fixed byte array.</value>
        public fixed byte Value[k_Length];

        /// <summary>Convert a raw buffer to an allocation ID.</summary>
        /// <param name="dataPtr">Raw pointer to buffer to convert.</param>
        /// <param name="length">Length of the buffer to convert.</param>
        /// <returns>New allocation ID.</returns>
        public static RelayAllocationId FromBytePointer(byte* dataPtr, int length)
        {
            if (length != k_Length)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new ArgumentException($"Provided byte array length is invalid, must be {k_Length} but got {length}.");
#else
                DebugLog.ErrorRelayWrongBufferSize(k_Length, length);
                return default;
#endif
            }
            
            var allocationId = new RelayAllocationId();
            UnsafeUtility.MemCpy(allocationId.Value, dataPtr, k_Length);
            return allocationId;
        }

        /// <summary>Convert a byte array to an allocation ID.</summary>
        /// <param name="data">Array to convert.</param>
        /// <returns>New allocation ID.</returns>
        public static RelayAllocationId FromByteArray(byte[] data)
        {
            fixed(byte* ptr = data)
            {
                return RelayAllocationId.FromBytePointer(ptr, data.Length);
            }
        }

        internal NetworkEndpoint ToNetworkEndpoint()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (UnsafeUtility.SizeOf<NetworkEndpoint>() < UnsafeUtility.SizeOf<RelayAllocationId>())
                throw new InvalidOperationException($"RellayAllocationId ({UnsafeUtility.SizeOf<RelayAllocationId>()} bytes) does not fit into a NetworkEndpoint ({UnsafeUtility.SizeOf<NetworkEndpoint>()} bytes)");
#endif
            var endpoint = default(NetworkEndpoint);
            *(RelayAllocationId*)&endpoint = this;
            return endpoint;
        }

        public static bool operator==(RelayAllocationId lhs, RelayAllocationId rhs)
        {
            return lhs.Compare(rhs) == 0;
        }

        public static bool operator!=(RelayAllocationId lhs, RelayAllocationId rhs)
        {
            return lhs.Compare(rhs) != 0;
        }

        public bool Equals(RelayAllocationId other)
        {
            return Compare(other) == 0;
        }

        public int CompareTo(RelayAllocationId other)
        {
            return Compare(other);
        }

        public override bool Equals(object other)
        {
            return other != null && this == (RelayAllocationId)other;
        }

        public override int GetHashCode()
        {
            fixed(byte* p = Value)
            unchecked
            {
                var result = 0;

                for (int i = 0; i < k_Length; i++)
                {
                    result = (result * 31) ^ (int)p[i];
                }

                return result;
            }
        }

        int Compare(RelayAllocationId other)
        {
            fixed(void* p = Value)
            {
                return UnsafeUtility.MemCmp(p, other.Value, k_Length);
            }
        }
    }

    internal static class RelayAllocationIdExtensions
    {
        public static unsafe ref RelayAllocationId AsRelayAllocationId(this ref NetworkEndpoint address)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (UnsafeUtility.SizeOf<NetworkEndpoint>() < UnsafeUtility.SizeOf<RelayAllocationId>())
                throw new InvalidOperationException($"RellayAllocationId ({UnsafeUtility.SizeOf<RelayAllocationId>()} bytes) does not fit into a NetworkEndpoint ({UnsafeUtility.SizeOf<NetworkEndpoint>()} bytes)");
#endif
            fixed(NetworkEndpoint* addressPtr = &address)
            {
                return ref *(RelayAllocationId*)addressPtr;
            }
        }
    }
}
