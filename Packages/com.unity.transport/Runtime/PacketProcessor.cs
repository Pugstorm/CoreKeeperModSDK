using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport.Logging;

namespace Unity.Networking.Transport
{
    /// <summary>
    /// An API representing a packet acquired from a <see cref="PacketsQueue"/>, and which allows
    /// modifying the packet. The packet is represented as a slice inside a fixed-size buffer, which
    /// is why APIs like <see cref="BytesAvailableAtEnd"/> are offered.
    /// </summary>
    public unsafe struct PacketProcessor
    {
        internal PacketsQueue m_Queue;

        internal int m_BufferIndex;

        private ref PacketMetadata PacketMetadataRef => ref m_Queue.GetMetadataRef(m_BufferIndex);
        private PacketBuffer PacketBuffer => m_Queue.GetPacketBuffer(m_BufferIndex);

        /// <summary>
        /// Whether the packet processor was obtained from a valid <see cref="PacketsQueue"/>.
        /// </summary>
        /// <value>True if obtained from a valid queue, false otherwise.</value>
        public bool IsCreated => m_Queue.IsCreated;

        /// <summary>Size of the packet's data inside the buffer.</summary>
        /// <value>Size in bytes.</value>
        public int Length => PacketMetadataRef.DataLength;

        /// <summary>Offset of the packet's first byte inside the buffer.</summary>
        /// <value>Offset in bytes.</value>
        public int Offset => PacketMetadataRef.DataOffset;

        /// <summary>Size of the buffer containing the packet.</summary>
        /// <value>Size in bytes.</value>
        public int Capacity => PacketMetadataRef.DataCapacity;

        /// <summary>Bytes available in the buffer after the end of the packet.</summary>
        /// <value>Size in bytes.</value>
        public int BytesAvailableAtEnd => Capacity - (Offset + Length);

        /// <summary>Bytes available in the buffer before the start of the packet.</summary>
        /// <value>Size in bytes.</value>
        public int BytesAvailableAtStart => Offset;

        /// <summary>
        /// A reference to the endpoint of the packet. For packets in the receive queue, this is
        /// the endpoint from which the packet was received. For packets in the send queue, this is
        /// the endpoint the packet is destined to. This must be set appropriately for
        /// newly-enqueued packets.
        /// </summary>
        /// <value>Endpoint associated with the packet.</value>
        public ref NetworkEndpoint EndpointRef => ref *((NetworkEndpoint*)PacketBuffer.Endpoint);

        internal ref ConnectionId ConnectionRef => ref PacketMetadataRef.Connection;

        /// <summary>Get a reference to the payload data reinterpreted to the type T.</summary>
        /// <typeparam name="T">Type of the data.</typeparam>
        /// <param name="offset">Offset from the start of the payload.</param>
        /// <returns>Returns a reference to the payload data</returns>
        /// <exception cref="ArgumentException">
        /// If there is not enough bytes in the payload for the specified type. Only thrown when
        /// collections checks are enabled (i.e. in the editor). Otherwise the obtained reference
        /// may be partially corrupted.
        /// </exception>
        public ref T GetPayloadDataRef<T>(int offset = 0) where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (UnsafeUtility.SizeOf<T>() + offset > Length)
                throw new ArgumentException($"The requested type {typeof(T).ToString()} does not fit in the payload data ({Length - offset})");
#endif
            return ref *(T*)(((byte*)GetUnsafePayloadPtr()) + Offset + offset);
        }

        /// <summary>
        /// Copy the provided bytes at the end of the packet and increases its size accordingly.
        /// </summary>
        /// <param name="dataPtr">Pointer to the data to copy.</param>
        /// <param name="size">Size in bytes to copy.</param>
        /// <exception cref="ArgumentException">
        /// If there are not enough bytes available at the end of the packet. Only thrown when
        /// collections checks are enabled (i.e. in the editor). Otherwise an error is logged and
        /// nothing is copied.
        /// </exception>
        public void AppendToPayload(void* dataPtr, int size)
        {
            if (size > BytesAvailableAtEnd)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new ArgumentException($"The requested data size ({size}) does not fit at the end of the payload ({BytesAvailableAtEnd} Bytes available)");
#else
                DebugLog.ErrorPayloadNotFitEndSize(size, BytesAvailableAtEnd);
                return;
#endif
            }
            UnsafeUtility.MemCpy(((byte*)PacketBuffer.Payload) + Offset + Length, dataPtr, size);
            PacketMetadataRef.DataLength += size;
        }

        /// <summary>Append the content of the given packet at the end of this one.</summary>
        /// <param name="processor">Packet processor to copy the data from.</param>
        /// <exception cref="ArgumentException">
        /// If there are not enough bytes available at the end of the packet. Only thrown when
        /// collections checks are enabled (i.e. in the editor). Otherwise an error is logged and
        /// nothing is copied.
        /// </exception>
        public void AppendToPayload(PacketProcessor processor)
        {
            AppendToPayload((byte*)processor.GetUnsafePayloadPtr() + processor.Offset, processor.Length);
        }

        /// <summary>
        /// Copy the provided value at the end of the packet and increase its size accordingly.
        /// </summary>
        /// <typeparam name="T">Type of the data to copy.</typeparam>
        /// <param name="value">Value to copy.</param>
        /// <exception cref="ArgumentException">
        /// If there are not enough bytes available at the end of the packet. Only thrown when
        /// collections checks are enabled (i.e. in the editor). Otherwise an error is logged and
        /// nothing is copied.
        /// </exception>
        public void AppendToPayload<T>(T value) where T : unmanaged
        {
            var size = UnsafeUtility.SizeOf<T>();
            if (size > BytesAvailableAtEnd)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new ArgumentException($"The requested data size ({size}) does not fit at the end of the payload ({BytesAvailableAtEnd} Bytes available)");
#else
                DebugLog.ErrorPayloadNotFitEndSize(size, BytesAvailableAtEnd);
                return;
#endif
            }
            UnsafeUtility.MemCpy(((byte*)PacketBuffer.Payload + Offset + Length), &value, size);
            PacketMetadataRef.DataLength += size;
        }

        /// <summary>
        /// Copy the provided value at the start of the packet and increase its size accordingly.
        /// </summary>
        /// <typeparam name="T">Type of the data to copy.</typeparam>
        /// <param name="value">Value to copy.</param>
        /// <exception cref="ArgumentException">
        /// If there are not enough bytes available at the start of the packet. Only thrown when
        /// collections checks are enabled (i.e. in the editor). Otherwise an error is logged and
        /// nothing is copied.
        /// </exception>
        public void PrependToPayload<T>(T value) where T : unmanaged
        {
            var size = UnsafeUtility.SizeOf<T>();
            if (size > BytesAvailableAtStart)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new ArgumentException($"The requested data size ({size}) does not fit at the start of the payload ({BytesAvailableAtStart} Bytes available)");
#else
                DebugLog.ErrorPayloadNotFitStartSize(size, BytesAvailableAtStart);
                return;
#endif
            }

            UnsafeUtility.MemCpy((byte*)PacketBuffer.Payload + Offset - size, &value, size);
            PacketMetadataRef.DataOffset -= size;
            PacketMetadataRef.DataLength += size;
        }

        /// <summary>
        /// Get and remove data at the start of the payload reinterpreted to the type T.
        /// </summary>
        /// <typeparam name="T">Type of the data.</typeparam>
        /// <returns>Extracted data value.</returns>
        /// <exception cref="ArgumentException">
        /// If there are not enough bytes available at the start of the packet. Only thrown when
        /// collections checks are enabled (i.e. in the editor). Otherwise an error is logged and
        /// a default value is returned.
        /// </exception>
        public T RemoveFromPayloadStart<T>() where T : unmanaged
        {
            var size = UnsafeUtility.SizeOf<T>();
            if (size > Length)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new ArgumentException($"The size of the required type ({size}) does not fit in the payload ({Length}).");
#else
                DebugLog.ErrorPayloadNotFitSize(size, Length);
                return default(T);
#endif
            }

            var value = GetPayloadDataRef<T>(0);
            PacketMetadataRef.DataOffset += size;
            PacketMetadataRef.DataLength -= size;
            return value;
        }

        /// <summary>
        /// Fill the provided buffer with the data at the start of the payload, and remove that
        /// data from the packet, decreasing its size accordingly.
        /// </summary>
        /// <param name="ptr">Pointer to the start of the buffer to fill.</param>
        /// <param name="size">Size of the buffer to fill.</param>
        /// <exception cref="ArgumentException">
        /// If the buffer is larger than the packet. Only thrown when collections checks are enabled
        /// (i.e. in the editor). Otherwise an error is logged and nothing is copied.
        /// </exception>
        public void RemoveFromPayloadStart(void* ptr, int size)
        {
            if (size > Length)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new ArgumentException($"The size of the buffer ({size}) is larger than the payload ({Length}).");
#else
                DebugLog.ErrorPayloadWrongSize(size, Length);
                return;
#endif
            }

            UnsafeUtility.MemCpy(ptr, ((byte*)PacketBuffer.Payload) + Offset, size);
            PacketMetadataRef.DataOffset += size;
            PacketMetadataRef.DataLength -= size;
        }

        /// <summary>
        /// Fill the provided buffer with the data at the start of the payload. The copied data
        /// will remain in the packet (compare to <see cref="RemoveFromPayloadStart"/> which
        /// removes the data from the packet).
        /// </summary>
        /// <param name="destinationPtr">Pointer to the buffer data will be copied to.</param>
        /// <returns>Ammount of bytes copied.</returns>
        /// <exception cref="ArgumentException">
        /// If the buffer is larger than the packet. Only thrown when collections checks are enabled
        /// (i.e. in the editor). Otherwise an error is logged and nothing is copied.
        /// </exception>
        public int CopyPayload(void* destinationPtr, int size)
        {
            if (Length <= 0)
                return 0;

            var copiedBytes = Length;

            if (size < Length)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new ArgumentException($"The payload size ({Length}) does not fit in the provided pointer ({size})");
#else
                DebugLog.ErrorCopyPayloadFailure(Length, size);
                copiedBytes = size;
#endif
                
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (Offset < 0)
                throw new OverflowException("Packet DataOffset must be >= 0");
            if (Offset + Length > Capacity)
                throw new OverflowException("Packet data overflows packet capacity");
#endif

            UnsafeUtility.MemCpy(destinationPtr, ((byte*)PacketBuffer.Payload) + Offset, copiedBytes);

            return copiedBytes;
        }

        /// <summary>Get a raw pointer to the packet data.</summary>
        /// <returns>A pointer to the packet data.</returns>
        public void* GetUnsafePayloadPtr()
        {
            return (byte*)PacketBuffer.Payload;
        }

        /// <summary>
        /// Manually sets the packet metadata.
        /// </summary>
        /// <param name="size">The new size of the packet</param>
        /// <param name="offset">The new offset of the packet</param>
        /// <exception cref="ArgumentException">Throws an ArgumentException if the size and offset does not fit in the packet.</exception>
        internal void SetUnsafeMetadata(int size, int offset = 0)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (offset + size > Capacity || offset < 0 || size < 0)
                throw new ArgumentException($"The requested data size ({size}) and offset ({offset}) does not fit in the payload ({Capacity} Bytes available)");
#endif
            PacketMetadataRef.DataLength = size;
            PacketMetadataRef.DataOffset = offset;
        }

        /// <summary>
        /// Drop the packet from its queue by setting its length to 0. Packets with a length of 0
        /// are considered to be dropped from the queue and will be batch-recycled at the end of the
        /// update cycle. This is more performant than properly releasing packets one at a time.
        /// </summary>
        public void Drop()
        {
            SetUnsafeMetadata(0);
        }
    }
}
