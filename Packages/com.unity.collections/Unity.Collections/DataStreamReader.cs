using System;
using System.Diagnostics;
using Unity.Collections.LowLevel.Unsafe;
#if !UNITY_DOTSRUNTIME
using UnityEngine.Scripting.APIUpdating;
#endif

namespace Unity.Collections
{
    /// <summary>
    /// Writes data in an endian format to deserialize data.
    /// </summary>
    /// <remarks>
    /// The DataStreamReader class is the counterpart of the
    /// <see cref="DataStreamWriter"/> class and can be be used to deserialize
    /// data which was prepared with it.
    /// 
    /// DataStreamWriter writes this data in the endian format native
    /// to the current machine architecture.
    /// <br/>
    /// For network byte order use the so named methods.
    /// <br/>
    /// Simple usage example:
    /// <code>
    /// using (var dataWriter = new DataStreamWriter(16, Allocator.Persistent))
    /// {
    ///     dataWriter.Write(42);
    ///     dataWriter.Write(1234);
    ///     // Length is the actual amount of data inside the writer,
    ///     // Capacity is the total amount.
    ///     var dataReader = new DataStreamReader(dataWriter, 0, dataWriter.Length);
    ///     var context = default(DataStreamReader.Context);
    ///     var myFirstInt = dataReader.ReadInt(ref context);
    ///     var mySecondInt = dataReader.ReadInt(ref context);
    /// }
    /// </code>
    ///
    /// DataStreamReader carries the position of the read pointer inside the struct,
    /// taking a copy of the reader will also copy the read position. This includes passing the
    /// reader to a method by value instead of by ref.
    ///
    /// <seealso cref="DataStreamWriter"/>
    /// <seealso cref="IsLittleEndian"/>
    /// </remarks>
#if !UNITY_DOTSRUNTIME
    [MovedFrom(true, "Unity.Networking.Transport")]
#endif
    [GenerateTestsForBurstCompatibility]
    public unsafe struct DataStreamReader
    {
        struct Context
        {
            public int m_ReadByteIndex;
            public int m_BitIndex;
            public ulong m_BitBuffer;
            public int m_FailedReads;
        }

        [NativeDisableUnsafePtrRestriction] byte* m_BufferPtr;
        Context m_Context;
        int m_Length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle m_Safety;
#endif

        /// <summary>
        /// Initializes a new instance of the DataStreamReader struct with a NativeArray&lt;byte&gt;
        /// </summary>
        /// <param name="array">The buffer to attach to the DataStreamReader.</param>
        public DataStreamReader(NativeArray<byte> array)
        {
            Initialize(out this, array);
        }

        static void Initialize(out DataStreamReader self, NativeArray<byte> array)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            self.m_Safety = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(array);
#endif
            self.m_BufferPtr = (byte*)array.GetUnsafeReadOnlyPtr();
            self.m_Length = array.Length;
            self.m_Context = default;
        }

        /// <summary>
        /// Show the byte order in which the current computer architecture stores data.
        /// </summary>
        /// <remarks>
        /// Different computer architectures store data using different byte orders.
        /// <list type="bullet">
        /// <item>Big-endian: the most significant byte is at the left end of a word.</item>
        /// <item>Little-endian: means the most significant byte is at the right end of a word.</item>
        /// </list>
        /// </remarks>
        public static bool IsLittleEndian { get { return DataStreamWriter.IsLittleEndian; } }

        static short ByteSwap(short val)
        {
            return (short)(((val & 0xff) << 8) | ((val >> 8) & 0xff));
        }

        static int ByteSwap(int val)
        {
            return (int)(((val & 0xff) << 24) | ((val & 0xff00) << 8) | ((val >> 8) & 0xff00) | ((val >> 24) & 0xff));
        }

        /// <summary>
        /// If there is a read failure this returns true. A read failure might happen if this attempts to read more than there is capacity for.
        /// </summary>
        public readonly bool HasFailedReads => m_Context.m_FailedReads > 0;

        /// <summary>
        /// The total size of the buffer space this reader is working with.
        /// </summary>
        public readonly int Length
        {
            get
            {
                CheckRead();
                return m_Length;
            }
        }

        /// <summary>
        /// True if the reader has been pointed to a valid buffer space. This
        /// would be false if the reader was created with no arguments.
        /// </summary>
        public readonly bool IsCreated
        {
            get { return m_BufferPtr != null; }
        }

        void ReadBytesInternal(byte* data, int length)
        {
            CheckRead();
            if (GetBytesRead() + length > m_Length)
            {
                ++m_Context.m_FailedReads;
#if (ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG) && !UNITY_DOTSRUNTIME
                UnityEngine.Debug.LogError($"Trying to read {length} bytes from a stream where only {m_Length - GetBytesRead()} are available");
#endif
                UnsafeUtility.MemClear(data, length);
                return;
            }
            // Restore the full bytes moved to the bit buffer but no consumed
            m_Context.m_ReadByteIndex -= (m_Context.m_BitIndex >> 3);
            m_Context.m_BitIndex = 0;
            m_Context.m_BitBuffer = 0;
            UnsafeUtility.MemCpy(data, m_BufferPtr + m_Context.m_ReadByteIndex, length);
            m_Context.m_ReadByteIndex += length;
        }

        /// <summary>
        /// Read and copy data into the given NativeArray of bytes, an error will
        /// be logged if not enough bytes are available.
        /// </summary>
        /// <param name="array"></param>
        public void ReadBytes(NativeArray<byte> array)
        {
            ReadBytesInternal((byte*)array.GetUnsafePtr(), array.Length);
        }

        /// <summary>
        /// Gets the number of bytes read from the data stream.
        /// </summary>
        /// <returns>Number of bytes read.</returns>
        public int GetBytesRead()
        {
            return m_Context.m_ReadByteIndex - (m_Context.m_BitIndex >> 3);
        }

        /// <summary>
        /// Gets the number of bits read from the data stream.
        /// </summary>
        /// <returns>Number of bits read.</returns>
        public int GetBitsRead()
        {
            return (m_Context.m_ReadByteIndex << 3) - m_Context.m_BitIndex;
        }

        /// <summary>
        /// Sets the current position of this stream to the given value.
        /// An error will be logged if <paramref name="pos"/> is outside the length of the stream.
        /// <br/>
        /// In addition this will reset the bit index and the bit buffer.
        /// </summary>
        /// <param name="pos">Seek position.</param>
        public void SeekSet(int pos)
        {
            if (pos > m_Length)
            {
                ++m_Context.m_FailedReads;
#if (ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG) && !UNITY_DOTSRUNTIME
                UnityEngine.Debug.LogError($"Trying to seek to {pos} in a stream of length {m_Length}");
#endif
                return;
            }
            m_Context.m_ReadByteIndex = pos;
            m_Context.m_BitIndex = 0;
            m_Context.m_BitBuffer = 0UL;
        }

        /// <summary>
        /// Reads an unsigned byte from the current stream and advances the current position of the stream by one byte.
        /// </summary>
        /// <returns>The next byte read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public byte ReadByte()
        {
            byte data;
            ReadBytesInternal((byte*)&data, sizeof(byte));
            return data;
        }

        /// <summary>
        /// Reads a 2-byte signed short from the current stream and advances the current position of the stream by two bytes.
        /// </summary>
        /// <returns>A 2-byte signed short read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public short ReadShort()
        {
            short data;
            ReadBytesInternal((byte*)&data, sizeof(short));
            return data;
        }

        /// <summary>
        /// Reads a 2-byte unsigned short from the current stream and advances the current position of the stream by two bytes.
        /// </summary>
        /// <returns>A 2-byte unsigned short read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public ushort ReadUShort()
        {
            ushort data;
            ReadBytesInternal((byte*)&data, sizeof(ushort));
            return data;
        }

        /// <summary>
        /// Reads a 4-byte signed integer from the current stream and advances the current position of the stream by four bytes.
        /// </summary>
        /// <returns>A 4-byte signed integer read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public int ReadInt()
        {
            int data;
            ReadBytesInternal((byte*)&data, sizeof(int));
            return data;
        }

        /// <summary>
        /// Reads a 4-byte unsigned integer from the current stream and advances the current position of the stream by four bytes.
        /// </summary>
        /// <returns>A 4-byte unsigned integer read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public uint ReadUInt()
        {
            uint data;
            ReadBytesInternal((byte*)&data, sizeof(uint));
            return data;
        }

        /// <summary>
        /// Reads an 8-byte signed long from the stream and advances the current position of the stream by eight bytes.
        /// </summary>
        /// <returns>An 8-byte signed long read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public long ReadLong()
        {
            long data;
            ReadBytesInternal((byte*)&data, sizeof(long));
            return data;
        }

        /// <summary>
        /// Reads an 8-byte unsigned long from the stream and advances the current position of the stream by eight bytes.
        /// </summary>
        /// <returns>An 8-byte unsigned long read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public ulong ReadULong()
        {
            ulong data;
            ReadBytesInternal((byte*)&data, sizeof(ulong));
            return data;
        }

        /// <summary>
        /// Reads a 2-byte signed short from the current stream in Big-endian byte order and advances the current position of the stream by two bytes.
        /// If the current endianness is in little-endian order, the byte order will be swapped.
        /// </summary>
        /// <returns>A 2-byte signed short read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public short ReadShortNetworkByteOrder()
        {
            short data;
            ReadBytesInternal((byte*)&data, sizeof(short));
            return IsLittleEndian ? ByteSwap(data) : data;
        }

        /// <summary>
        /// Reads a 2-byte unsigned short from the current stream in Big-endian byte order and advances the current position of the stream by two bytes.
        /// If the current endianness is in little-endian order, the byte order will be swapped.
        /// </summary>
        /// <returns>A 2-byte unsigned short read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public ushort ReadUShortNetworkByteOrder()
        {
            return (ushort)ReadShortNetworkByteOrder();
        }

        /// <summary>
        /// Reads a 4-byte signed integer from the current stream in Big-endian byte order and advances the current position of the stream by four bytes.
        /// If the current endianness is in little-endian order, the byte order will be swapped.
        /// </summary>
        /// <returns>A 4-byte signed integer read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public int ReadIntNetworkByteOrder()
        {
            int data;
            ReadBytesInternal((byte*)&data, sizeof(int));
            return IsLittleEndian ? ByteSwap(data) : data;
        }

        /// <summary>
        /// Reads a 4-byte unsigned integer from the current stream in Big-endian byte order and advances the current position of the stream by four bytes.
        /// If the current endianness is in little-endian order, the byte order will be swapped.
        /// </summary>
        /// <returns>A 4-byte unsigned integer read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public uint ReadUIntNetworkByteOrder()
        {
            return (uint)ReadIntNetworkByteOrder();
        }

        /// <summary>
        /// Reads a 4-byte floating point value from the current stream and advances the current position of the stream by four bytes.
        /// </summary>
        /// <returns>A 4-byte floating point value read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public float ReadFloat()
        {
            UIntFloat uf = new UIntFloat();
            uf.intValue = (uint)ReadInt();
            return uf.floatValue;
        }

        /// <summary>
        /// Reads a 8-byte floating point value from the current stream and advances the current position of the stream by four bytes.
        /// </summary>
        /// <returns>A 8-byte floating point value read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public double ReadDouble()
        {
            UIntFloat uf = new UIntFloat();
            uf.longValue = (ulong)ReadLong();
            return uf.doubleValue;
        }

        /// <summary>
        /// Reads a 4-byte unsigned integer from the current stream using a <see cref="StreamCompressionModel"/> and advances the current position the number of bits depending on the model.
        /// </summary>
        /// <param name="model"><see cref="StreamCompressionModel"/> model for reading value in a packed manner.</param>
        /// <returns>A 4-byte unsigned integer read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public uint ReadPackedUInt(StreamCompressionModel model)
        {
            return ReadPackedUIntInternal(StreamCompressionModel.k_MaxHuffmanSymbolLength, model.decodeTable, model.bucketOffsets, model.bucketSizes);
        }

        uint ReadPackedUIntInternal(int maxSymbolLength, ushort* decodeTable, uint* bucketOffsets, byte* bucketSizes)
        {
            CheckRead();
            FillBitBuffer();
            uint peekMask = (1u << maxSymbolLength) - 1u;
            uint peekBits = (uint)m_Context.m_BitBuffer & peekMask;
            ushort huffmanEntry = decodeTable[(int)peekBits];
            int symbol = huffmanEntry >> 8;
            int length = huffmanEntry & 0xFF;

            if (m_Context.m_BitIndex < length)
            {
                ++m_Context.m_FailedReads;
#if (ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG) && !UNITY_DOTSRUNTIME
                UnityEngine.Debug.LogError($"Trying to read {length} bits from a stream where only {m_Context.m_BitIndex} are available");
#endif
                return 0;
            }

            // Skip Huffman bits
            m_Context.m_BitBuffer >>= length;
            m_Context.m_BitIndex -= length;

            uint offset = bucketOffsets[symbol];
            byte bits = bucketSizes[symbol];
            return ReadRawBitsInternal(bits) + offset;
        }

        void FillBitBuffer()
        {
            while (m_Context.m_BitIndex <= 56 && m_Context.m_ReadByteIndex < m_Length)
            {
                m_Context.m_BitBuffer |= (ulong)m_BufferPtr[m_Context.m_ReadByteIndex++] << m_Context.m_BitIndex;
                m_Context.m_BitIndex += 8;
            }
        }

        uint ReadRawBitsInternal(int numbits)
        {
            CheckBits(numbits);
            if (m_Context.m_BitIndex < numbits)
            {
                ++m_Context.m_FailedReads;
#if (ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG) && !UNITY_DOTSRUNTIME
                UnityEngine.Debug.LogError($"Trying to read {numbits} bits from a stream where only {m_Context.m_BitIndex} are available");
#endif
                return 0;
            }
            uint res = (uint)(m_Context.m_BitBuffer & ((1UL << numbits) - 1UL));
            m_Context.m_BitBuffer >>= numbits;
            m_Context.m_BitIndex -= numbits;
            return res;
        }

        /// <summary>
        /// Reads a specified number of bits from the data stream.
        /// </summary>
        /// <param name="numbits">A positive number of bytes to write.</param>
        /// <returns>A 4-byte unsigned integer read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public uint ReadRawBits(int numbits)
        {
            CheckRead();
            FillBitBuffer();
            return ReadRawBitsInternal(numbits);
        }

        /// <summary>
        /// Reads an 8-byte unsigned long value from the data stream using a <see cref="StreamCompressionModel"/>.
        /// </summary>
        /// <param name="model"><see cref="StreamCompressionModel"/> model for reading value in a packed manner.</param>
        /// <returns>An 8-byte unsigned long read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public ulong ReadPackedULong(StreamCompressionModel model)
        {
            ulong value;
            ((uint*)&value)[0] = ReadPackedUInt(model);
            ((uint*)&value)[1] = ReadPackedUInt(model);
            return value;
        }

        /// <summary>
        /// Reads a 4-byte signed integer value from the data stream using a <see cref="StreamCompressionModel"/>.
        /// <br/>
        /// Negative values de-interleaves from positive values before returning, for example (0, -1, 1, -2, 2) -> (-2, -1, 0, 1, 2)
        /// </summary>
        /// <param name="model"><see cref="StreamCompressionModel"/> model for reading value in a packed manner.</param>
        /// <returns>A 4-byte signed integer read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public int ReadPackedInt(StreamCompressionModel model)
        {
            uint folded = ReadPackedUInt(model);
            return (int)(folded >> 1) ^ -(int)(folded & 1);    // Deinterleave values from [0, -1, 1, -2, 2...] to [..., -2, -1, -0, 1, 2, ...]
        }

        /// <summary>
        /// Reads an 8-byte signed long value from the data stream using a <see cref="StreamCompressionModel"/>.
        /// <br/>
        /// Negative values de-interleaves from positive values before returning, for example (0, -1, 1, -2, 2) -> (-2, -1, 0, 1, 2)
        /// </summary>
        /// <param name="model"><see cref="StreamCompressionModel"/> model for reading value in a packed manner.</param>
        /// <returns>An 8-byte signed long read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public long ReadPackedLong(StreamCompressionModel model)
        {
            ulong folded = ReadPackedULong(model);
            return (long)(folded >> 1) ^ -(long)(folded & 1);    // Deinterleave values from [0, -1, 1, -2, 2...] to [..., -2, -1, -0, 1, 2, ...]
        }

        /// <summary>
        /// Reads a 4-byte floating point value from the data stream using a <see cref="StreamCompressionModel"/>.
        /// </summary>
        /// <param name="model"><see cref="StreamCompressionModel"/> model for reading value in a packed manner.</param>
        /// <returns>A 4-byte floating point value read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public float ReadPackedFloat(StreamCompressionModel model)
        {
            return ReadPackedFloatDelta(0, model);
        }

        /// <summary>
        /// Reads a 8-byte floating point value from the data stream using a <see cref="StreamCompressionModel"/>.
        /// </summary>
        /// <param name="model"><see cref="StreamCompressionModel"/> model for reading value in a packed manner.</param>
        /// <returns>A 8-byte floating point value read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public double ReadPackedDouble(StreamCompressionModel model)
        {
            return ReadPackedDoubleDelta(0, model);
        }

        /// <summary>
        /// Reads a 4-byte signed integer delta value from the data stream using a <see cref="StreamCompressionModel"/>.
        /// </summary>
        /// <param name="baseline">The previous 4-byte signed integer value, used to compute the diff.</param>
        /// <param name="model"><see cref="StreamCompressionModel"/> model for reading value in a packed manner.</param>
        /// <returns>A 4-byte signed integer read from the current stream, or 0 if the end of the stream has been reached.
        /// If the data did not change, this also returns 0.
        /// <br/>
        /// See: <see cref="HasFailedReads"/> to verify if the read failed.</returns>
        public int ReadPackedIntDelta(int baseline, StreamCompressionModel model)
        {
            int delta = ReadPackedInt(model);
            return baseline - delta;
        }

        /// <summary>
        /// Reads a 4-byte unsigned integer delta value from the data stream using a <see cref="StreamCompressionModel"/>.
        /// </summary>
        /// <param name="baseline">The previous 4-byte unsigned integer value, used to compute the diff.</param>
        /// <param name="model"><see cref="StreamCompressionModel"/> model for reading value in a packed manner.</param>
        /// <returns>A 4-byte unsigned integer read from the current stream, or 0 if the end of the stream has been reached.
        /// If the data did not change, this also returns 0.
        /// <br/>
        /// See: <see cref="HasFailedReads"/> to verify if the read failed.</returns>
        public uint ReadPackedUIntDelta(uint baseline, StreamCompressionModel model)
        {
            uint delta = (uint)ReadPackedInt(model);
            return baseline - delta;
        }

        /// <summary>
        /// Reads an 8-byte signed long delta value from the data stream using a <see cref="StreamCompressionModel"/>.
        /// </summary>
        /// <param name="baseline">The previous 8-byte signed long value, used to compute the diff.</param>
        /// <param name="model"><see cref="StreamCompressionModel"/> model for reading value in a packed manner.</param>
        /// <returns>An 8-byte signed long read from the current stream, or 0 if the end of the stream has been reached.
        /// If the data did not change, this also returns 0.
        /// <br/>
        /// See: <see cref="HasFailedReads"/> to verify if the read failed.</returns>
        public long ReadPackedLongDelta(long baseline, StreamCompressionModel model)
        {
            long delta = ReadPackedLong(model);
            return baseline - delta;
        }

        /// <summary>
        /// Reads an 8-byte unsigned long delta value from the data stream using a <see cref="StreamCompressionModel"/>.
        /// </summary>
        /// <param name="baseline">The previous 8-byte unsigned long value, used to compute the diff.</param>
        /// <param name="model"><see cref="StreamCompressionModel"/> model for reading value in a packed manner.</param>
        /// <returns>An 8-byte unsigned long read from the current stream, or 0 if the end of the stream has been reached.
        /// If the data did not change, this also returns 0.
        /// <br/>
        /// See: <see cref="HasFailedReads"/> to verify if the read failed.</returns>
        public ulong ReadPackedULongDelta(ulong baseline, StreamCompressionModel model)
        {
            ulong delta = (ulong)ReadPackedLong(model);
            return baseline - delta;
        }

        /// <summary>
        /// Reads a 4-byte floating point value from the data stream.
        ///
        /// If the first bit is 0, the data did not change and <paramref name="baseline"/> will be returned.
        /// </summary>
        /// <param name="baseline">The previous 4-byte floating point value.</param>
        /// <param name="model">Not currently used.</param>
        /// <returns>A 4-byte floating point value read from the current stream, or <paramref name="baseline"/> if there are no changes to the value.
        /// <br/>
        /// See: <see cref="HasFailedReads"/> to verify if the read failed.</returns>
        public float ReadPackedFloatDelta(float baseline, StreamCompressionModel model)
        {
            CheckRead();
            FillBitBuffer();
            if (ReadRawBitsInternal(1) == 0)
                return baseline;

            var bits = 32;
            UIntFloat uf = new UIntFloat();
            uf.intValue = ReadRawBitsInternal(bits);
            return uf.floatValue;
        }

        /// <summary>
        /// Reads a 8-byte floating point value from the data stream.
        ///
        /// If the first bit is 0, the data did not change and <paramref name="baseline"/> will be returned.
        /// </summary>
        /// <param name="baseline">The previous 8-byte floating point value.</param>
        /// <param name="model">Not currently used.</param>
        /// <returns>A 8-byte floating point value read from the current stream, or <paramref name="baseline"/> if there are no changes to the value.
        /// <br/>
        /// See: <see cref="HasFailedReads"/> to verify if the read failed.</returns>
        public double ReadPackedDoubleDelta(double baseline, StreamCompressionModel model)
        {
            CheckRead();
            FillBitBuffer();
            if (ReadRawBitsInternal(1) == 0)
                return baseline;

            var bits = 32;
            UIntFloat uf = new UIntFloat();
            var data = (uint*)&uf.longValue;
            data[0] = ReadRawBitsInternal(bits);
            FillBitBuffer();
            data[1] |= ReadRawBitsInternal(bits);
            return uf.doubleValue;
        }

        /// <summary>
        /// Reads a <c>FixedString32Bytes</c> value from the current stream and advances the current position of the stream by the length of the string.
        /// </summary>
        /// <returns>A <c>FixedString32Bytes</c> value read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public unsafe FixedString32Bytes ReadFixedString32()
        {
            FixedString32Bytes str;
            byte* data = ((byte*)&str) + 2;
            *(ushort*)&str = ReadFixedStringInternal(data, str.Capacity);
            return str;
        }

        /// <summary>
        /// Reads a <c>FixedString64Bytes</c> value from the current stream and advances the current position of the stream by the length of the string.
        /// </summary>
        /// <returns>A <c>FixedString64Bytes</c> value read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public unsafe FixedString64Bytes ReadFixedString64()
        {
            FixedString64Bytes str;
            byte* data = ((byte*)&str) + 2;
            *(ushort*)&str = ReadFixedStringInternal(data, str.Capacity);
            return str;
        }

        /// <summary>
        /// Reads a <c>FixedString128Bytes</c> value from the current stream and advances the current position of the stream by the length of the string.
        /// </summary>
        /// <returns>A <c>FixedString128Bytes</c> value read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public unsafe FixedString128Bytes ReadFixedString128()
        {
            FixedString128Bytes str;
            byte* data = ((byte*)&str) + 2;
            *(ushort*)&str = ReadFixedStringInternal(data, str.Capacity);
            return str;
        }

        /// <summary>
        /// Reads a <c>FixedString512Bytes</c> value from the current stream and advances the current position of the stream by the length of the string.
        /// </summary>
        /// <returns>A <c>FixedString512Bytes</c> value read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public unsafe FixedString512Bytes ReadFixedString512()
        {
            FixedString512Bytes str;
            byte* data = ((byte*)&str) + 2;
            *(ushort*)&str = ReadFixedStringInternal(data, str.Capacity);
            return str;
        }

        /// <summary>
        /// Reads a <c>FixedString4096Bytes</c> value from the current stream and advances the current position of the stream by the length of the string.
        /// </summary>
        /// <returns>A <c>FixedString4096Bytes</c> value read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public unsafe FixedString4096Bytes ReadFixedString4096()
        {
            FixedString4096Bytes str;
            byte* data = ((byte*)&str) + 2;
            *(ushort*)&str = ReadFixedStringInternal(data, str.Capacity);
            return str;
        }

        /// <summary>
        /// Read and copy data into the given NativeArray of bytes, an error will
        /// be logged if not enough bytes are available in the array.
        /// </summary>
        /// <param name="array">Buffer to write the string bytes to.</param>
        /// <returns>Length of data read into byte array, or zero if error occurred.</returns>
        public ushort ReadFixedString(NativeArray<byte> array)
        {
            return ReadFixedStringInternal((byte*)array.GetUnsafePtr(), array.Length);
        }

        unsafe ushort ReadFixedStringInternal(byte* data, int maxLength)
        {
            ushort length = ReadUShort();
            if (length > maxLength)
            {
#if (ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG) && !UNITY_DOTSRUNTIME
                UnityEngine.Debug.LogError($"Trying to read a string of length {length} but max length is {maxLength}");
#endif
                return 0;
            }
            ReadBytesInternal(data, length);
            return length;
        }

        /// <summary>
        /// Reads a <c>FixedString32Bytes</c> delta value to the data stream using a <see cref="StreamCompressionModel"/>.
        /// </summary>
        /// <param name="baseline">The previous <c>FixedString32Bytes</c> value, used to compute the diff.</param>
        /// <param name="model"><see cref="StreamCompressionModel"/> model for writing value in a packed manner.</param>
        /// <returns>A <c>FixedString32Bytes</c> value read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public unsafe FixedString32Bytes ReadPackedFixedString32Delta(FixedString32Bytes baseline, StreamCompressionModel model)
        {
            FixedString32Bytes str;
            byte* data = ((byte*)&str) + 2;
            *(ushort*)&str = ReadPackedFixedStringDeltaInternal(data, str.Capacity, ((byte*)&baseline) + 2, *((ushort*)&baseline), model);
            return str;
        }

        /// <summary>
        /// Reads a <c>FixedString64Bytes</c> delta value to the data stream using a <see cref="StreamCompressionModel"/>.
        /// </summary>
        /// <param name="baseline">The previous <c>FixedString64Bytes</c> value, used to compute the diff.</param>
        /// <param name="model"><see cref="StreamCompressionModel"/> model for writing value in a packed manner.</param>
        /// <returns>A <c>FixedString64Bytes</c> value read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public unsafe FixedString64Bytes ReadPackedFixedString64Delta(FixedString64Bytes baseline, StreamCompressionModel model)
        {
            FixedString64Bytes str;
            byte* data = ((byte*)&str) + 2;
            *(ushort*)&str = ReadPackedFixedStringDeltaInternal(data, str.Capacity, ((byte*)&baseline) + 2, *((ushort*)&baseline), model);
            return str;
        }

        /// <summary>
        /// Reads a <c>FixedString128Bytes</c> delta value to the data stream using a <see cref="StreamCompressionModel"/>.
        /// </summary>
        /// <param name="baseline">The previous <c>FixedString128Bytes</c> value, used to compute the diff.</param>
        /// <param name="model"><see cref="StreamCompressionModel"/> model for writing value in a packed manner.</param>
        /// <returns>A <c>FixedString128Bytes</c> value read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public unsafe FixedString128Bytes ReadPackedFixedString128Delta(FixedString128Bytes baseline, StreamCompressionModel model)
        {
            FixedString128Bytes str;
            byte* data = ((byte*)&str) + 2;
            *(ushort*)&str = ReadPackedFixedStringDeltaInternal(data, str.Capacity, ((byte*)&baseline) + 2, *((ushort*)&baseline), model);
            return str;
        }

        /// <summary>
        /// Reads a <c>FixedString512Bytes</c> delta value to the data stream using a <see cref="StreamCompressionModel"/>.
        /// </summary>
        /// <param name="baseline">The previous <c>FixedString512Bytes</c> value, used to compute the diff.</param>
        /// <param name="model"><see cref="StreamCompressionModel"/> model for writing value in a packed manner.</param>
        /// <returns>A <c>FixedString512Bytes</c> value read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public unsafe FixedString512Bytes ReadPackedFixedString512Delta(FixedString512Bytes baseline, StreamCompressionModel model)
        {
            FixedString512Bytes str;
            byte* data = ((byte*)&str) + 2;
            *(ushort*)&str = ReadPackedFixedStringDeltaInternal(data, str.Capacity, ((byte*)&baseline) + 2, *((ushort*)&baseline), model);
            return str;
        }

        /// <summary>
        /// Reads a <c>FixedString4096Bytes</c> delta value to the data stream using a <see cref="StreamCompressionModel"/>.
        /// </summary>
        /// <param name="baseline">The previous <c>FixedString4096Bytes</c> value, used to compute the diff.</param>
        /// <param name="model"><see cref="StreamCompressionModel"/> model for writing value in a packed manner.</param>
        /// <returns>A <c>FixedString4096Bytes</c> value read from the current stream, or 0 if the end of the stream has been reached.</returns>
        public unsafe FixedString4096Bytes ReadPackedFixedString4096Delta(FixedString4096Bytes baseline, StreamCompressionModel model)
        {
            FixedString4096Bytes str;
            byte* data = ((byte*)&str) + 2;
            *(ushort*)&str = ReadPackedFixedStringDeltaInternal(data, str.Capacity, ((byte*)&baseline) + 2, *((ushort*)&baseline), model);
            return str;
        }

        /// <summary>
        /// Read and copy data into the given NativeArray of bytes, an error will
        /// be logged if not enough bytes are available in the array.
        /// </summary>
        /// <param name="data">Array for the current fixed string.</param>
        /// <param name="baseData">Array containing the previous value, used to compute the diff.</param>
        /// <param name="model"><see cref="StreamCompressionModel"/> model for writing value in a packed manner.</param>
        /// <returns>Length of data read into byte array, or zero if error occurred.</returns>
        public ushort ReadPackedFixedStringDelta(NativeArray<byte> data, NativeArray<byte> baseData, StreamCompressionModel model)
        {
            return ReadPackedFixedStringDeltaInternal((byte*)data.GetUnsafePtr(), data.Length, (byte*)baseData.GetUnsafePtr(), (ushort)baseData.Length, model);
        }

        unsafe ushort ReadPackedFixedStringDeltaInternal(byte* data, int maxLength, byte* baseData, ushort baseLength, StreamCompressionModel model)
        {
            uint length = ReadPackedUIntDelta(baseLength, model);
            if (length > (uint)maxLength)
            {
#if (ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG) && !UNITY_DOTSRUNTIME
                UnityEngine.Debug.LogError($"Trying to read a string of length {length} but max length is {maxLength}");
#endif
                return 0;
            }
            if (length <= baseLength)
            {
                for (int i = 0; i < length; ++i)
                    data[i] = (byte)ReadPackedUIntDelta(baseData[i], model);
            }
            else
            {
                for (int i = 0; i < baseLength; ++i)
                    data[i] = (byte)ReadPackedUIntDelta(baseData[i], model);
                for (int i = baseLength; i < length; ++i)
                    data[i] = (byte)ReadPackedUInt(model);
            }
            return (ushort)length;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        readonly void CheckRead()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        static void CheckBits(int numbits)
        {
            if (numbits < 0 || numbits > 32)
                throw new ArgumentOutOfRangeException("Invalid number of bits");
        }
    }
}
