using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport.Logging;

namespace Unity.Networking.Transport.Relay
{
    /// <summary>Encrypted data that the Relay server uses to describe a connection.</summary>
    public unsafe struct RelayConnectionData
    {
        /// <summary>Length of the connection data.</summary>
        /// <value>Length in bytes.</value>
        public const int k_Length = 255;

        /// <summary>Raw value of the connection data.</summary>
        /// <value>Connection data as a fixed byte array.</value>
        public fixed byte Value[k_Length];

        /// <summary>Convert a raw buffer to a connection data structure.</summary>
        /// <param name="dataPtr">Raw pointer to buffer to convert.</param>
        /// <param name="length">Length of the buffer to convert.</param>
        /// <returns>New connection data.</returns>
        public static RelayConnectionData FromBytePointer(byte* dataPtr, int length)
        {
            if (length > k_Length)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new ArgumentException($"Provided byte array length is invalid, must be less or equal to {k_Length} but got {length}.");
#else
                DebugLog.ErrorRelayWrongBufferSizeLess(k_Length, length);
                return default;
#endif
            }
            
            var connectionData = new RelayConnectionData();
            UnsafeUtility.MemCpy(connectionData.Value, dataPtr, length);
            return connectionData;
        }

        /// <summary>Convert a byte array to a connection data structure.</summary>
        /// <param name="data">Array to convert.</param>
        /// <returns>New connection data.</returns>
        public static RelayConnectionData FromByteArray(byte[] data)
        {
            fixed(byte* ptr = data)
            {
                return RelayConnectionData.FromBytePointer(ptr, data.Length);
            }
        }
    }
}
