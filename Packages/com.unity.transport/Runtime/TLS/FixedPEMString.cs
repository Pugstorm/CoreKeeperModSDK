using System;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Networking.Transport.TLS
{
    /// <summary>
    /// Fixed representation of a string containing a certificate/key in the PEM format, suitable
    /// for usage within Burst-compiled code and with native bindings (e.g. with UnityTLS).
    /// </summary>
    public unsafe struct FixedPEMString
    {
        private const int k_BufferLength = 16 * 1024;

        private fixed byte m_Buffer[k_BufferLength];
        private int m_Length;

        /// <summary>Maximum length of the string that can be stored in this type.</summary>
        public const int MaxLength = k_BufferLength - 1;

        /// <summary>Length of the fixed string.</summary>
        public int Length => m_Length;

        /// <summary>Construct a FixedPEMString from a managed string.</summary>
        /// <param name="pem">String containing the certificate/key in the PEM format.</param>
        /// <exception cref="ArgumentException">If the string is too large.</exception>
        public FixedPEMString(string pem)
        {
            var bytes = Encoding.ASCII.GetBytes(pem);

            if (bytes.Length > MaxLength)
                throw new ArgumentException($"String is too large to fit in {nameof(FixedPEMString)} (length: {bytes.Length}, capacity: {MaxLength}).");

            fixed(byte* bytesPtr = bytes)
            fixed(byte* bufferPtr = m_Buffer)
            {
                UnsafeUtility.MemCpy(bufferPtr, bytesPtr, bytes.Length);
                bufferPtr[bytes.Length] = (byte)0;
            }

            m_Length = bytes.Length;
        }

        /// <summary>Construct a FixedPEMString from a <see cref="FixedString4096Bytes"/>.</summary>
        /// <param name="pem">String containing the certificate/key in the PEM format.</param>
        /// <remarks>Will not perform any kind of string validation.</remarks>
        internal FixedPEMString(ref FixedString4096Bytes pem)
        {
            fixed(byte* bufferPtr = m_Buffer)
            {
                UnsafeUtility.MemCpy(bufferPtr, pem.GetUnsafePtr(), pem.Length);
                bufferPtr[pem.Length] = (byte)0;
            }

            m_Length = pem.Length;
        }

        /// <summary>Get the unsafe pointer to the string's data.</summary>
        /// <remarks>Only reliable if the structure itself is already fixed in memory.</remarks>
        internal byte* GetUnsafePtr()
        {
            fixed(byte* bufferPtr = m_Buffer) return bufferPtr;
        }
    }
}
