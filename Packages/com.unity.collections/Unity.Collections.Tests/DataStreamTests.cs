using System;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine.TestTools;
using UnityEngine;

namespace Unity.Collections.Tests
{
    internal class DataStreamTests
    {
        internal class ReadAndWrite
        {
            [TestCase(ushort.MaxValue)]
            [TestCase(ushort.MinValue)]
            public void UShort(ushort expected)
            {
                bool Write(ref DataStreamWriter writer) => writer.WriteUShort(expected);
                CheckReadWrite(sizeof(ushort), reader => reader.ReadUShort(), Write, expected);
            }

            [TestCase((uint)0b101010)]
            [TestCase((uint)0b111111)]
            public void RawBits(uint expected)
            {
                bool Write(ref DataStreamWriter writer) => writer.WriteRawBits(expected, 6);
                CheckReadWrite(6, reader => reader.ReadRawBits(6), Write, expected);
            }

            [Test]
            public void RawBits_OutOfCapacity()
            {
                const uint expected = 0b101011001;
                var writer = new DataStreamWriter(1, Allocator.Temp);
                Assert.False(writer.WriteRawBits(expected, 9));
                Assert.True(writer.HasFailedWrites);
            }

            [TestCase(uint.MaxValue)]
            [TestCase(uint.MinValue)]
            public void UInt(uint expected)
            {
                bool Write(ref DataStreamWriter writer) => writer.WriteUInt(expected);
                CheckReadWrite(sizeof(uint), reader => reader.ReadUInt(), Write, expected);
            }

            [TestCase(float.MaxValue)]
            [TestCase(float.MinValue)]
            public void Float(float expected)
            {
                bool Write(ref DataStreamWriter writer) => writer.WriteFloat(expected);
                CheckReadWrite(sizeof(float), reader => reader.ReadFloat(), Write, expected);
            }

            [TestCase(short.MaxValue)]
            [TestCase(short.MinValue)]
            public void Short(short expected)
            {
                bool Write(ref DataStreamWriter writer) => writer.WriteShort(expected);
                CheckReadWrite(sizeof(short), reader => reader.ReadShort(), Write, expected);
            }

            [Test]
            public void FixedString32()
            {
                var expected = new FixedString32Bytes("This is a string");
                bool Write(ref DataStreamWriter writer) => writer.WriteFixedString32(expected);
                CheckReadWrite(expected.Length + FixedStringHeader, reader => reader.ReadFixedString32(), Write, expected);
            }

            [Test]
            public void FixedString64()
            {
                var expected = new FixedString64Bytes("This is a string");
                bool Write(ref DataStreamWriter writer) => writer.WriteFixedString64(expected);
                CheckReadWrite(expected.Length + FixedStringHeader, reader => reader.ReadFixedString64(), Write, expected);
            }

            [Test]
            public void FixedString128()
            {
                var expected = new FixedString128Bytes("This is a string");
                bool Write(ref DataStreamWriter writer) => writer.WriteFixedString128(expected);
                CheckReadWrite(expected.Length + FixedStringHeader, reader => reader.ReadFixedString128(), Write, expected);
            }

            [Test]
            public void FixedString512()
            {
                var expected = new FixedString512Bytes("This is a string");
                bool Write(ref DataStreamWriter writer) => writer.WriteFixedString512(expected);
                CheckReadWrite(expected.Length + FixedStringHeader, reader => reader.ReadFixedString512(), Write, expected);
            }

            [Test]
            public void FixedString4096()
            {
                var expected = new FixedString4096Bytes("This is a string");
                bool Write(ref DataStreamWriter writer) => writer.WriteFixedString4096(expected);
                CheckReadWrite(expected.Length + FixedStringHeader, reader => reader.ReadFixedString4096(), Write,
                    expected);
            }

            [Test]
            public void LongLooped()
            {
                const long baseVal = -99;
                const long expected = -1979;
                bool Write(ref DataStreamWriter writer, long value) => writer.WriteLong(value);
                long Read(ref DataStreamReader reader) => reader.ReadLong();
                CheckReadWriteLooped(sizeof(long), baseVal, expected, Write, Read, (l, u) => l + u);
            }
        }

        internal class ReadAndWriteNetworkOrder
        {
            [TestCase(int.MaxValue)]
            [TestCase(int.MinValue)]
            public void Int(int expected)
            {
                bool Write(ref DataStreamWriter writer) => writer.WriteIntNetworkByteOrder(expected);
                CheckReadWrite(sizeof(int), reader => reader.ReadIntNetworkByteOrder(), Write, expected);
            }

            [TestCase(uint.MaxValue)]
            [TestCase(uint.MinValue)]
            public void UInt(uint expected)
            {
                bool Write(ref DataStreamWriter writer) => writer.WriteUIntNetworkByteOrder(expected);
                CheckReadWrite(sizeof(uint), reader => reader.ReadUIntNetworkByteOrder(), Write, expected);
            }

            [TestCase(short.MaxValue)]
            [TestCase(short.MinValue)]
            public void Short(short expected)
            {
                bool Write(ref DataStreamWriter writer) => writer.WriteShortNetworkByteOrder(expected);
                CheckReadWrite(sizeof(short), reader => reader.ReadShortNetworkByteOrder(), Write, expected);
            }

            [TestCase(ushort.MaxValue)]
            [TestCase(ushort.MinValue)]
            public void UShort(ushort expected)
            {
                bool Write(ref DataStreamWriter writer) => writer.WriteUShortNetworkByteOrder(expected);
                CheckReadWrite(sizeof(ushort), reader => reader.ReadUShortNetworkByteOrder(), Write, expected);
            }

            [Test]
            public void ReadIncorrect()
            {
                var dataStream = new DataStreamWriter(4, Allocator.Temp);
                dataStream.WriteIntNetworkByteOrder(1979);
                dataStream.Flush();
                var reader = new DataStreamReader(dataStream.AsNativeArray());
                Assert.AreNotEqual(1979, reader.ReadInt());
            }
        }

        internal class ReadWritePacked
        {
            [Test]
            public void UInt()
            {
                const uint expected = 1979;
                const uint baseVal = 2000;
                bool Write(ref DataStreamWriter writer) => writer.WriteUInt(expected);
                bool WritePacked(ref DataStreamWriter writer, StreamCompressionModel model, uint value) => writer.WritePackedUInt(value, model);
                uint Read(ref DataStreamReader reader) => reader.ReadUInt();
                uint ReadPacked(ref DataStreamReader reader, StreamCompressionModel model) => reader.ReadPackedUInt(model);
                CheckReadWritePackedLooped(sizeof(uint), baseVal, expected, Write, WritePacked, Read, ReadPacked, (l, u) => l + u);
            }

            [Test]
            public void IntExistingData()
            {
                unsafe
                {
                    var n = 300 * 4;
                    var data = stackalloc byte[n];
                    var compressionModel = StreamCompressionModel.Default;
                    var dataStream = new DataStreamWriter(data, n);
                    const int base_val = -10;
                    const int count = 20;
                    for (int i = 0; i < count; ++i)
                        dataStream.WritePackedInt(base_val + i, compressionModel);

                    dataStream.WriteInt((int)1979);
                    dataStream.Flush();

                    var na = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(data, n, Allocator.Invalid);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref na, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
                    var reader = new DataStreamReader(na);
                    for (int i = 0; i < count; ++i)
                    {
                        var val = reader.ReadPackedInt(compressionModel);
                        Assert.AreEqual(base_val + i, val);
                    }

                    Assert.AreEqual(1979, reader.ReadInt());
                }
            }

            [Test]
            public void Int()
            {
                const int expected = -1979;
                const int baseVal = -10;
                bool Write(ref DataStreamWriter writer) => writer.WriteInt(expected);
                bool WritePacked(ref DataStreamWriter writer, StreamCompressionModel model, int value) => writer.WritePackedInt(value, model);
                int Read(ref DataStreamReader reader) => reader.ReadInt();
                int ReadPacked(ref DataStreamReader reader, StreamCompressionModel model) => reader.ReadPackedInt(model);
                CheckReadWritePackedLooped(sizeof(int), baseVal, expected, Write, WritePacked, Read, ReadPacked, (i, u) => (int)(i + u));
            }

            [Test]
            public void Long()
            {
                const long expected = -1979;
                const long baseVal = -99;
                bool Write(ref DataStreamWriter writer) => writer.WriteLong(expected);
                bool WritePacked(ref DataStreamWriter writer, StreamCompressionModel model, long value) => writer.WritePackedLong(value, model);
                long Read(ref DataStreamReader reader) => reader.ReadLong();
                long ReadPacked(ref DataStreamReader reader, StreamCompressionModel model) => reader.ReadPackedLong(model);

                CheckReadWritePackedLooped(sizeof(long), baseVal, expected, Write, WritePacked, Read, ReadPacked, (l, u) => l + u);
            }

            [Test]
            public void ULong()
            {
                const ulong expected = 1979;
                const ulong baseVal = 2000;
                bool Write(ref DataStreamWriter writer) => writer.WriteULong(expected);
                bool WritePacked(ref DataStreamWriter writer, StreamCompressionModel model, ulong value) => writer.WritePackedULong(value, model);
                ulong Read(ref DataStreamReader reader) => reader.ReadULong();
                ulong ReadPacked(ref DataStreamReader reader, StreamCompressionModel model) => reader.ReadPackedULong(model);
                CheckReadWritePackedLooped(sizeof(ulong), baseVal, expected, Write, WritePacked, Read, ReadPacked, (l, u) => l + u);
            }

            [Test]
            public void Float()
            {
                const float expected = 1979.1f;
                const float baseVal = 2000.1f;
                bool Write(ref DataStreamWriter writer) => writer.WriteFloat(expected);
                bool WritePacked(ref DataStreamWriter writer, StreamCompressionModel model, float value) => writer.WritePackedFloat(value, model);
                float Read(ref DataStreamReader reader) => reader.ReadFloat();
                float ReadPacked(ref DataStreamReader reader, StreamCompressionModel model) => reader.ReadPackedFloat(model);
                CheckReadWritePackedLooped(sizeof(float), baseVal, expected, Write, WritePacked, Read, ReadPacked, (l, u) => l + u);
            }

            [Test]
            public void Double()
            {
                const double expected = 1979.1989;
                const double baseVal = 2000.2000;
                bool Write(ref DataStreamWriter writer) => writer.WriteDouble(expected);
                bool WritePacked(ref DataStreamWriter writer, StreamCompressionModel model, double value) => writer.WritePackedDouble(value, model);
                double Read(ref DataStreamReader reader) => reader.ReadDouble();
                double ReadPacked(ref DataStreamReader reader, StreamCompressionModel model) => reader.ReadPackedDouble(model);
                CheckReadWritePackedLooped(sizeof(double), baseVal, expected, Write, WritePacked, Read, ReadPacked, (l, u) => l + u);
            }

            [Test]
            public void WriteOutSideOfCapacity_Fails()
            {
                var model = StreamCompressionModel.Default;
                var writer = new DataStreamWriter(sizeof(uint) / 2, Allocator.Temp);
                Assert.False(writer.HasFailedWrites);
                Assert.False(writer.WritePackedUInt(uint.MaxValue, model), "Writing a uint where there is no room should fail.");
                Assert.That(writer.HasFailedWrites);
            }

            [Test, DotsRuntimeIgnore]
            [TestRequiresDotsDebugOrCollectionChecks]
            public void ReadOutSideOfCapacity_Fails()
            {
                var model = StreamCompressionModel.Default;
                var reader = new DataStreamReader(new NativeArray<byte>(0, Allocator.Temp));
                LogAssert.Expect(LogType.Error, "Trying to read 2 bits from a stream where only 0 are available");
                Assert.That(reader.ReadPackedUInt(model), Is.EqualTo(0));
                Assert.That(reader.HasFailedReads);
            }
        }

        internal class ReadWritePackedDelta
        {
            [Test]
            public void Int()
            {
                const int expected = int.MaxValue;
                const int baseline = int.MinValue;
                bool WritePacked(ref DataStreamWriter writer, StreamCompressionModel model) => writer.WritePackedIntDelta(expected, baseline, model);
                int ReadPacked(ref DataStreamReader reader, StreamCompressionModel model) => reader.ReadPackedIntDelta(baseline, model);
                CheckReadWritePacked(sizeof(int), ReadPacked, WritePacked, expected);
            }

            [Test]
            public void Long()
            {
                const long expected = long.MaxValue;
                const long baseline = long.MinValue;
                bool WritePacked(ref DataStreamWriter writer, StreamCompressionModel model) => writer.WritePackedLongDelta(expected, baseline, model);
                long ReadPacked(ref DataStreamReader reader, StreamCompressionModel model) => reader.ReadPackedLongDelta(baseline, model);
                CheckReadWritePacked(sizeof(long), ReadPacked, WritePacked, expected);
            }

            [Test]
            public void ULong()
            {
                const ulong expected = ulong.MaxValue;
                const ulong baseline = ulong.MinValue;
                bool WritePacked(ref DataStreamWriter writer, StreamCompressionModel model) => writer.WritePackedULongDelta(expected, baseline, model);
                ulong ReadPacked(ref DataStreamReader reader, StreamCompressionModel model) => reader.ReadPackedULongDelta(baseline, model);
                CheckReadWritePacked(sizeof(ulong), ReadPacked, WritePacked, expected);
            }

            [Test]
            public void FixedString32()
            {
                var expected = new FixedString32Bytes("This is a string");
                var baseline = new FixedString32Bytes("This is another string");
                bool WritePacked(ref DataStreamWriter writer, StreamCompressionModel model) => writer.WritePackedFixedString32Delta(expected, baseline, model);
                FixedString32Bytes ReadPacked(ref DataStreamReader reader, StreamCompressionModel model) => reader.ReadPackedFixedString32Delta(baseline, model);
                CheckReadWritePacked(expected.Length + FixedStringHeader, ReadPacked, WritePacked, expected);
            }

            [Test]
            public void FixedString32_LargerBaseline()
            {
                var expected = new FixedString32Bytes("This is another string");
                var baseline = new FixedString32Bytes("This is a string");
                bool WritePacked(ref DataStreamWriter writer, StreamCompressionModel model) => writer.WritePackedFixedString32Delta(expected, baseline, model);
                FixedString32Bytes ReadPacked(ref DataStreamReader reader, StreamCompressionModel model) => reader.ReadPackedFixedString32Delta(baseline, model);
                CheckReadWritePacked(expected.Length + FixedStringHeader, ReadPacked, WritePacked, expected);
            }

            [Test]
            public void FixedString64()
            {
                var expected = new FixedString64Bytes("This is a string");
                var baseline = new FixedString64Bytes("This is another string");
                bool WritePacked(ref DataStreamWriter writer, StreamCompressionModel model) => writer.WritePackedFixedString64Delta(expected, baseline, model);
                FixedString64Bytes ReadPacked(ref DataStreamReader reader, StreamCompressionModel model) => reader.ReadPackedFixedString64Delta(baseline, model);
                CheckReadWritePacked(expected.Length + FixedStringHeader, ReadPacked, WritePacked, expected);
            }

            [Test]
            public void FixedString128()
            {
                var expected = new FixedString128Bytes("This is a string");
                var baseline = new FixedString128Bytes("This is another string");
                bool WritePacked(ref DataStreamWriter writer, StreamCompressionModel model) => writer.WritePackedFixedString128Delta(expected, baseline, model);
                FixedString128Bytes ReadPacked(ref DataStreamReader reader, StreamCompressionModel model) => reader.ReadPackedFixedString128Delta(baseline, model);
                CheckReadWritePacked(expected.Length + FixedStringHeader, ReadPacked, WritePacked, expected);
            }

            [Test]
            public void FixedString512()
            {
                var expected = new FixedString512Bytes("This is a string");
                var baseline = new FixedString512Bytes("This is another string");
                bool WritePacked(ref DataStreamWriter writer, StreamCompressionModel model) => writer.WritePackedFixedString512Delta(expected, baseline, model);
                FixedString512Bytes ReadPacked(ref DataStreamReader reader, StreamCompressionModel model) => reader.ReadPackedFixedString512Delta(baseline, model);
                CheckReadWritePacked(expected.Length + FixedStringHeader, ReadPacked, WritePacked, expected);
            }

            [Test]
            public void FixedString4096()
            {
                var expected = new FixedString4096Bytes("This is a string");
                var baseline = new FixedString4096Bytes("This is another string");
                bool WritePacked(ref DataStreamWriter writer, StreamCompressionModel model) => writer.WritePackedFixedString4096Delta(expected, baseline, model);
                FixedString4096Bytes ReadPacked(ref DataStreamReader reader, StreamCompressionModel model) => reader.ReadPackedFixedString4096Delta(baseline, model);
                CheckReadWritePacked(expected.Length + FixedStringHeader, ReadPacked, WritePacked, expected);
            }

            [Test]
            public void Float_OutOfBoundsFails()
            {
                const float expected = float.MaxValue;
                const float baseline = float.MinValue;
                var model = StreamCompressionModel.Default;
                var writer = new DataStreamWriter(sizeof(float) / 2, Allocator.Temp);
                Assert.False(writer.HasFailedWrites);
                Assert.False(writer.WritePackedFloatDelta(expected, baseline, model));
                Assert.That(writer.HasFailedWrites);
            }

            [Test]
            public void Float_UnchangedData()
            {
                const float expected = float.MaxValue;
                const float baseline = float.MaxValue;
                var model = StreamCompressionModel.Default;
                var writer = new DataStreamWriter(1, Allocator.Temp);
                Assert.True(writer.WritePackedFloatDelta(expected, baseline, model));
                var reader = new DataStreamReader(writer.AsNativeArray());
                Assert.AreEqual(baseline, reader.ReadPackedFloatDelta(baseline, model));
                Assert.That(reader.GetBitsRead(), Is.EqualTo(1));
            }

            [Test]
            public void Float_ChangedData()
            {
                const float expected = float.MaxValue;
                const float baseline = float.MinValue;
                var model = StreamCompressionModel.Default;
                var writer = new DataStreamWriter(sizeof(float) + 1, Allocator.Temp);
                Assert.True(writer.WritePackedFloatDelta(expected, baseline, model));
                var reader = new DataStreamReader(writer.AsNativeArray());
                Assert.AreEqual(1, reader.ReadRawBits(1));
                var uf = new UIntFloat
                {
                    intValue = reader.ReadRawBits(32)
                };
                Assert.AreEqual(expected, uf.floatValue);
            }

            [Test, DotsRuntimeIgnore]
            [TestRequiresDotsDebugOrCollectionChecks]
            public void UInt_OutOfCapacity()
            {
                var model = StreamCompressionModel.Default;
                var writer = new DataStreamWriter(0, Allocator.Temp);
                var reader = new DataStreamReader(writer.AsNativeArray());
                LogAssert.Expect(LogType.Error, "Trying to read 2 bits from a stream where only 0 are available");
                Assert.That(reader.ReadPackedUInt(model), Is.EqualTo(0));
                Assert.That(reader.HasFailedReads);
            }
        }

        [Test]
        public void IsCreated_ReturnsTrueAfterConstructor()
        {
            var dataStream = new DataStreamWriter(4, Allocator.Temp);
            Assert.True(dataStream.IsCreated, "Buffer must be created after calling constructor.");
        }

        [Test]
        public void LengthInBits_MatchesWrittenCount()
        {
            var dataStream = new DataStreamWriter(4, Allocator.Temp);
            dataStream.WriteByte(0);
            Assert.That(dataStream.LengthInBits, Is.EqualTo(1 * 8));
            dataStream.WriteByte(1);
            dataStream.WriteByte(1);
            Assert.That(dataStream.LengthInBits, Is.EqualTo(3 * 8));
        }

        [Test, DotsRuntimeIgnore]
        public void CreateStreamWithPartOfSourceByteArray()
        {
            byte[] byteArray =
            {
                (byte)'s', (byte)'o', (byte)'m', (byte)'e',
                (byte)' ', (byte)'d', (byte)'a', (byte)'t', (byte)'a'
            };

            DataStreamWriter dataStream;
            dataStream = new DataStreamWriter(4, Allocator.Temp);
            dataStream.WriteBytes(new NativeArray<byte>(byteArray, Allocator.Temp).GetSubArray(0, 4));
            Assert.AreEqual(dataStream.Length, 4);
            var reader = new DataStreamReader(dataStream.AsNativeArray());
            for (int i = 0; i < dataStream.Length; ++i)
            {
                Assert.AreEqual(byteArray[i], reader.ReadByte());
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            LogAssert.Expect(LogType.Error, "Trying to read 1 bytes from a stream where only 0 are available");
            Assert.AreEqual(0, reader.ReadByte());
#endif
        }

        [Test]
        public void CreateStreamWithSourceByteArray()
        {
            byte[] byteArray = new byte[100];
            byteArray[0] = (byte)'a';
            byteArray[1] = (byte)'b';
            byteArray[2] = (byte)'c';

            DataStreamWriter dataStream;
            dataStream = new DataStreamWriter(byteArray.Length, Allocator.Temp);
            dataStream.WriteBytes(new NativeArray<byte>(byteArray, Allocator.Temp));

            var arr = dataStream.AsNativeArray();
            var reader = new DataStreamReader(arr);
            for (var i = 0; i < byteArray.Length; ++i)
            {
                Assert.AreEqual(byteArray[i], reader.ReadByte());
            }

            unsafe
            {
                var reader2 = new DataStreamReader(arr);
                for (var i = 0; i < byteArray.Length; ++i)
                {
                    Assert.AreEqual(byteArray[i], reader2.ReadByte());
                }
            }
        }

        [Test]
        public void ReadIntoExistingByteArray()
        {
            var byteArray = new NativeArray<byte>(100, Allocator.Temp);

            DataStreamWriter dataStream;
            dataStream = new DataStreamWriter(3, Allocator.Temp);
            {
                dataStream.WriteByte((byte)'a');
                dataStream.WriteByte((byte)'b');
                dataStream.WriteByte((byte)'c');
                var reader = new DataStreamReader(dataStream.AsNativeArray());
                reader.ReadBytes(byteArray.GetSubArray(0, dataStream.Length));
                reader = new DataStreamReader(dataStream.AsNativeArray());
                for (int i = 0; i < reader.Length; ++i)
                {
                    Assert.AreEqual(byteArray[i], reader.ReadByte());
                }
            }
        }

        [Test]
        public void ReadingDataFromStreamWithSliceOffset()
        {
            var dataStream = new DataStreamWriter(100, Allocator.Temp);
            dataStream.WriteByte((byte)'a');
            dataStream.WriteByte((byte)'b');
            dataStream.WriteByte((byte)'c');
            dataStream.WriteByte((byte)'d');
            dataStream.WriteByte((byte)'e');
            dataStream.WriteByte((byte)'f');
            var reader = new DataStreamReader(dataStream.AsNativeArray().GetSubArray(3, 3));
            Assert.AreEqual('d', reader.ReadByte());
            Assert.AreEqual('e', reader.ReadByte());
            Assert.AreEqual('f', reader.ReadByte());
        }

        [Test]
        public void WriteOutOfBounds()
        {
            var dataStream = new DataStreamWriter(9, Allocator.Temp);
            Assert.IsTrue(dataStream.WriteInt(42));
            Assert.AreEqual(4, dataStream.Length);
            Assert.IsTrue(dataStream.WriteInt(42));
            Assert.AreEqual(8, dataStream.Length);
            Assert.IsFalse(dataStream.HasFailedWrites);
            Assert.IsFalse(dataStream.WriteInt(42));
            Assert.AreEqual(8, dataStream.Length);
            Assert.IsTrue(dataStream.HasFailedWrites);

            Assert.IsFalse(dataStream.WriteShort(42));
            Assert.AreEqual(8, dataStream.Length);
            Assert.IsTrue(dataStream.HasFailedWrites);

            Assert.IsTrue(dataStream.WriteByte(42));
            Assert.AreEqual(9, dataStream.Length);
            Assert.IsTrue(dataStream.HasFailedWrites);

            Assert.IsFalse(dataStream.WriteByte(42));
            Assert.AreEqual(9, dataStream.Length);
            Assert.IsTrue(dataStream.HasFailedWrites);
        }

        [Test]
        public void ReadWritePackedUIntWithDeferred()
        {
            var compressionModel = StreamCompressionModel.Default;
            var dataStream = new DataStreamWriter(300 * 4, Allocator.Temp);
            uint base_val = 2000;
            uint count = 277;
            var def = dataStream;
            dataStream.WriteInt((int)0);
            for (uint i = 0; i < count; ++i)
                dataStream.WritePackedUInt(base_val + i, compressionModel);

            dataStream.Flush();
            def.WriteInt(1979);
            def = dataStream;
            dataStream.WriteInt((int)0);
            def.WriteInt(1979);
            dataStream.Flush();
            var reader = new DataStreamReader(dataStream.AsNativeArray());
            Assert.AreEqual(1979, reader.ReadInt());
            for (uint i = 0; i < count; ++i)
            {
                var val = reader.ReadPackedUInt(compressionModel);
                Assert.AreEqual(base_val + i, val);
            }

            Assert.AreEqual(1979, reader.ReadInt());
        }

        [Test]
        public void PassDataStreamReaderToJob()
        {
            using (var returnValue = new NativeArray<int>(1, Allocator.TempJob))
            {
                var writer = new DataStreamWriter(sizeof(int), Allocator.Temp);
                writer.WriteInt(42);

                var reader = new DataStreamReader(writer.AsNativeArray());

                new ReaderTestJob
                {
                    Reader = reader,
                    ReturnValue = returnValue
                }.Run();

                Assert.AreEqual(42, returnValue[0]);
            }
        }

        private struct ReaderTestJob : IJob
        {
            public DataStreamReader Reader;
            public NativeArray<int> ReturnValue;

            public void Execute()
            {
                ReturnValue[0] = Reader.ReadInt();
            }
        }

        delegate T Read<out T>(ref DataStreamReader reader);
        delegate T ReadPacked<out T>(ref DataStreamReader reader, StreamCompressionModel model);
        delegate bool Write(ref DataStreamWriter writer);
        delegate T Sum<T>(T x, uint y);
        delegate bool WriteWithValue<in T>(ref DataStreamWriter writer, T value);
        delegate bool WritePacked(ref DataStreamWriter writer, StreamCompressionModel model);
        delegate bool WritePackedWithValue<in T>(ref DataStreamWriter writer, StreamCompressionModel model, T value);

        const int FixedStringHeader = 2;

        static void CheckReadWritePackedLooped<T>(int size, T baseVal, T expected,
            Write write, WritePackedWithValue<T> writePackedWithValue,
            Read<T> read, ReadPacked<T> readPacked, Sum<T> sum)
        {
            var compressionModel = StreamCompressionModel.Default;
            var dataStream = new DataStreamWriter(300 * size, Allocator.Temp);
            const int count = 277;
            for (uint i = 0; i < count; ++i)
            {
                T res = sum(baseVal, i);
                writePackedWithValue(ref dataStream, compressionModel, res);
            }

            write(ref dataStream);
            dataStream.Flush();
            var reader = new DataStreamReader(dataStream.AsNativeArray());
            for (uint i = 0; i < count; ++i)
            {
                var val = readPacked(ref reader, compressionModel);
                Assert.AreEqual(sum(baseVal, i), val);
            }

            Assert.AreEqual(expected, read(ref reader));
        }

        static void CheckReadWritePacked<T>(int size, ReadPacked<T> read, WritePacked write, T value)
        {
            var model = StreamCompressionModel.Default;
            var writer = new DataStreamWriter(size, Allocator.Temp);
            write(ref writer, model);
            writer.Flush();
            var reader = new DataStreamReader(writer.AsNativeArray());
            Assert.That(read(ref reader, model), Is.EqualTo(value));
        }

        static void CheckReadWrite<T>(int size, Func<DataStreamReader, T> read, Write write, T value)
        {
            var writer = new DataStreamWriter(size, Allocator.Temp);
            write(ref writer);
            writer.Flush();
            Assert.That(read(new DataStreamReader(writer.AsNativeArray())), Is.EqualTo(value));
        }

        static void CheckReadWriteLooped<T>(int size, T baseVal, T expected, WriteWithValue<T> write, Read<T> read, Sum<T> sum)
        {
            var writer = new DataStreamWriter(300 * size, Allocator.Temp);
            const int count = 277;
            for (uint i = 0; i < count; ++i)
            {
                write(ref writer, sum(baseVal, i));
            }

            write(ref writer, expected);
            writer.Flush();
            var reader = new DataStreamReader(writer.AsNativeArray());
            for (uint i = 0; i < count; ++i)
            {
                var val = read(ref reader);
                Assert.AreEqual(sum(baseVal, i), val);
            }

            Assert.AreEqual(expected, read(ref reader));
        }

        [Test]
        public void MiNiCheck()
        {
            var model = StreamCompressionModel.Default;
            Assert.That(model.ToString(), Is.EqualTo("Unity.Collections.StreamCompressionModel"));
        }
    }
}
