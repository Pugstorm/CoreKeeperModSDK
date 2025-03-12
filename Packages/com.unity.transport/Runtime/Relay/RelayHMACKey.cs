using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport.Logging;

namespace Unity.Networking.Transport.Relay
{
    /// <summary>HMAC key that the Relay server uses to authentify a connection.</summary>
    public unsafe struct RelayHMACKey
    {
        /// <summary>Length of the HMAC key.</summary>
        /// <value>Length in bytes.</value>
        public const int k_Length = 64;

        /// <summary>Raw value of the HMAC key.</summary>
        /// <value>HMAC key as a fixed byte array.</value>
        public fixed byte Value[k_Length];

        /// <summary>Convert a raw buffer to an HMAC key.</summary>
        /// <param name="dataPtr">Raw pointer to buffer to convert.</param>
        /// <param name="length">Length of the buffer to convert.</param>
        /// <returns>New HMAC key.</returns>
        public static RelayHMACKey FromBytePointer(byte* data, int length)
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

            var hmacKey = new RelayHMACKey();
            UnsafeUtility.MemCpy(hmacKey.Value, data, length);
            return hmacKey;
        }

        /// <summary>Convert a byte array to an HMAC key.</summary>
        /// <param name="data">Array to convert.</param>
        /// <returns>New HMAC key.</returns>
        public static RelayHMACKey FromByteArray(byte[] data)
        {
            fixed(byte* ptr = data)
            {
                return RelayHMACKey.FromBytePointer(ptr, data.Length);
            }
        }
    }
}
