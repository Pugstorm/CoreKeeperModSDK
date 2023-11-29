using NUnit.Framework;
using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.Tests;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using Assert = FastAssert;

internal class NativeBitArrayTests : CollectionsTestFixture
{
    [Test]
    public void NativeBitArray_Init()
    {
        var container = new NativeBitArray(0, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        Assert.DoesNotThrow(() => container.Dispose());
    }

    [Test]
    public void NativeBitArray_Get_Set_Long()
    {
        var numBits = 256;

        var test = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        Assert.False(test.IsSet(123));
        test.Set(123, true);
        Assert.True(test.IsSet(123));

        Assert.False(test.TestAll(0, numBits));
        Assert.False(test.TestNone(0, numBits));
        Assert.True(test.TestAny(0, numBits));
        Assert.AreEqual(1, test.CountBits(0, numBits));

        Assert.False(test.TestAll(0, 122));
        Assert.True(test.TestNone(0, 122));
        Assert.False(test.TestAny(0, 122));

        test.Clear();
        Assert.False(test.IsSet(123));
        Assert.AreEqual(0, test.CountBits(0, numBits));

        test.SetBits(40, true, 4);
        Assert.AreEqual(4, test.CountBits(0, numBits));

        test.SetBits(0, true, numBits);
        Assert.False(test.TestNone(0, numBits));
        Assert.True(test.TestAll(0, numBits));

        test.SetBits(0, false, numBits);
        Assert.True(test.TestNone(0, numBits));
        Assert.False(test.TestAll(0, numBits));

        test.SetBits(123, true, 7);
        Assert.True(test.TestAll(123, 7));

        test.Clear();
        test.SetBits(64, true, 64);
        Assert.AreEqual(false, test.IsSet(63));
        Assert.AreEqual(true, test.TestAll(64, 64));
        Assert.AreEqual(false, test.IsSet(128));
        Assert.AreEqual(64, test.CountBits(64, 64));
        Assert.AreEqual(64, test.CountBits(0, numBits));

        test.Clear();
        test.SetBits(65, true, 62);
        Assert.AreEqual(false, test.IsSet(64));
        Assert.AreEqual(true, test.TestAll(65, 62));
        Assert.AreEqual(false, test.IsSet(127));
        Assert.AreEqual(62, test.CountBits(64, 64));
        Assert.AreEqual(62, test.CountBits(0, numBits));

        test.Clear();
        test.SetBits(66, true, 64);
        Assert.AreEqual(false, test.IsSet(65));
        Assert.AreEqual(true, test.TestAll(66, 64));
        Assert.AreEqual(false, test.IsSet(130));
        Assert.AreEqual(64, test.CountBits(66, 64));
        Assert.AreEqual(64, test.CountBits(0, numBits));

        test.Dispose();
    }

    [Test]
    public void NativeBitArray_Get_Set_Short()
    {
        var numBits = 31;

        var test = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        Assert.False(test.IsSet(13));
        test.Set(13, true);
        Assert.True(test.IsSet(13));

        Assert.False(test.TestAll(0, numBits));
        Assert.False(test.TestNone(0, numBits));
        Assert.True(test.TestAny(0, numBits));
        Assert.AreEqual(1, test.CountBits(0, numBits));

        Assert.False(test.TestAll(0, 12));
        Assert.True(test.TestNone(0, 12));
        Assert.False(test.TestAny(0, 12));

        test.Clear();
        Assert.False(test.IsSet(13));
        Assert.AreEqual(0, test.CountBits(0, numBits));

        test.SetBits(4, true, 4);
        Assert.AreEqual(4, test.CountBits(0, numBits));

        test.SetBits(0, true, numBits);
        Assert.False(test.TestNone(0, numBits));
        Assert.True(test.TestAll(0, numBits));

        test.SetBits(0, false, numBits);
        Assert.True(test.TestNone(0, numBits));
        Assert.False(test.TestAll(0, numBits));

        test.SetBits(13, true, 7);
        Assert.True(test.TestAll(13, 7));

        test.Clear();
        test.SetBits(4, true, 4);
        Assert.AreEqual(false, test.IsSet(3));
        Assert.AreEqual(true, test.TestAll(4, 4));
        Assert.AreEqual(false, test.IsSet(18));
        Assert.AreEqual(4, test.CountBits(4, 4));
        Assert.AreEqual(4, test.CountBits(0, numBits));

        test.Clear();
        test.SetBits(5, true, 2);
        Assert.AreEqual(false, test.IsSet(4));
        Assert.AreEqual(true, test.TestAll(5, 2));
        Assert.AreEqual(false, test.IsSet(17));
        Assert.AreEqual(2, test.CountBits(4, 4));
        Assert.AreEqual(2, test.CountBits(0, numBits));

        test.Clear();
        test.SetBits(6, true, 4);
        Assert.AreEqual(false, test.IsSet(5));
        Assert.AreEqual(true, test.TestAll(6, 4));
        Assert.AreEqual(false, test.IsSet(10));
        Assert.AreEqual(4, test.CountBits(6, 4));
        Assert.AreEqual(4, test.CountBits(0, numBits));

        test.Dispose();
    }

    [Test]
    public void NativeBitArray_Get_Set_Tiny()
    {
        var numBits = 7;

        var test = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        Assert.False(test.IsSet(3));
        test.Set(3, true);
        Assert.True(test.IsSet(3));

        Assert.False(test.TestAll(0, numBits));
        Assert.False(test.TestNone(0, numBits));
        Assert.True(test.TestAny(0, numBits));
        Assert.AreEqual(1, test.CountBits(0, numBits));

        Assert.False(test.TestAll(0, 2));
        Assert.True(test.TestNone(0, 2));
        Assert.False(test.TestAny(0, 2));

        test.Clear();
        Assert.False(test.IsSet(3));
        Assert.AreEqual(0, test.CountBits(0, numBits));

        test.SetBits(3, true, 4);
        Assert.AreEqual(4, test.CountBits(0, numBits));

        test.SetBits(0, true, numBits);
        Assert.False(test.TestNone(0, numBits));
        Assert.True(test.TestAll(0, numBits));

        test.SetBits(0, false, numBits);
        Assert.True(test.TestNone(0, numBits));
        Assert.False(test.TestAll(0, numBits));

        test.Dispose();
    }

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks]
    public unsafe void NativeBitArray_Throws()
    {
        var numBits = 256;

        Assert.Throws<ArgumentException>(() => { new NativeBitArray(numBits, Allocator.None); });

        using (var test = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory))
        {
            Assert.DoesNotThrow(() => { test.TestAll(0, numBits); });
            Assert.DoesNotThrow(() => { test.TestAny(numBits - 1, numBits); });

            Assert.Throws<ArgumentException>(() => { test.IsSet(-1); });
            Assert.Throws<ArgumentException>(() => { test.IsSet(numBits); });
            Assert.Throws<ArgumentException>(() => { test.TestAny(0, 0); });
            Assert.Throws<ArgumentException>(() => { test.TestAny(numBits, 1); });
            Assert.Throws<ArgumentException>(() => { test.TestAny(numBits - 1, 0); });

            // GetBits numBits must be 1-64.
            Assert.Throws<ArgumentException>(() => { test.GetBits(0, 0); });
            Assert.Throws<ArgumentException>(() => { test.GetBits(0, 65); });
            Assert.DoesNotThrow(() => { test.GetBits(63, 2); });
        }
    }

    void GetBitsTest(ref NativeBitArray test, int pos, int numBits)
    {
        test.SetBits(pos, true, numBits);
        Assert.AreEqual(numBits, test.CountBits(0, test.Length));
        Assert.AreEqual(0xfffffffffffffffful >> (64 - numBits), test.GetBits(pos, numBits));
        test.Clear();
    }

    [Test]
    public void NativeBitArray_GetBits()
    {
        var numBits = 256;

        var test = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        GetBitsTest(ref test, 0, 5);
        GetBitsTest(ref test, 1, 3);
        GetBitsTest(ref test, 0, 63);
        GetBitsTest(ref test, 0, 64);
        GetBitsTest(ref test, 1, 63);
        GetBitsTest(ref test, 1, 64);
        GetBitsTest(ref test, 62, 5);
        GetBitsTest(ref test, 127, 3);
        GetBitsTest(ref test, 250, 6);
        GetBitsTest(ref test, 254, 2);

        test.Dispose();
    }

    static void SetBitsTest(ref NativeBitArray test, int pos, ulong value, int numBits)
    {
        test.SetBits(pos, value, numBits);
        if (value != test.GetBits(pos, numBits))
            throw new Exception("Assert.Equals(value, test.GetBits(pos, numBits)) failed");
        test.Clear();
    }

    [Test]
    public void NativeBitArray_SetBits()
    {
        var numBits = 256;

        var test = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        SetBitsTest(ref test, 0, 16, 5);
        SetBitsTest(ref test, 1, 7, 3);
        SetBitsTest(ref test, 1, 32, 64);
        SetBitsTest(ref test, 62, 6, 5);
        SetBitsTest(ref test, 127, 1, 3);
        SetBitsTest(ref test, 60, 0xaa, 8);

        test.Dispose();
    }

    static void CopyBitsTest(ref NativeBitArray test, int dstPos, int srcPos, int numBits)
    {
        for (int pos = 0; pos < test.Length; pos += 64)
        {
            test.SetBits(pos, 0xaaaaaaaaaaaaaaaaul, 64);
        }

        test.SetBits(srcPos, true, numBits);
        test.Copy(dstPos, srcPos, numBits);
        Assert.AreEqual(true, test.TestAll(dstPos, numBits));

        for (int pos = 0; pos < test.Length; ++pos)
        {
            if ((pos >= dstPos && pos < dstPos + numBits) ||
                (pos >= srcPos && pos < srcPos + numBits))
            {
                Assert.AreEqual(true, test.IsSet(pos));
            }
            else
            {
                Assert.AreEqual((0 != (pos & 1)), test.IsSet(pos));
            }
        }

        test.Clear();
    }

    static void CopyBitsTests(ref NativeBitArray test)
    {
        CopyBitsTest(ref test, 1, 16, 12); // short up to 64-bits copy
        CopyBitsTest(ref test, 1, 80, 63); // short up to 64-bits copy
        CopyBitsTest(ref test, 1, 11, 12); // short up to 64-bits copy overlapped
        CopyBitsTest(ref test, 11, 1, 12); // short up to 64-bits copy overlapped

        CopyBitsTest(ref test, 1, 16, 76); // short up to 128-bits copy
        CopyBitsTest(ref test, 1, 80, 127); // short up to 128-bits copy
        CopyBitsTest(ref test, 1, 11, 76); // short up to 128-bits copy overlapped
        CopyBitsTest(ref test, 11, 1, 76); // short up to 128-bits copy overlapped

        CopyBitsTest(ref test, 1, 81, 255); // long copy aligned
        CopyBitsTest(ref test, 8, 0, 255); // long copy overlapped aligned
        CopyBitsTest(ref test, 1, 80, 255); // long copy unaligned
        CopyBitsTest(ref test, 80, 1, 255); // long copy overlapped unaligned
    }

    [Test]
    public void NativeBitArray_Copy()
    {
        var numBits = 512;

        var test = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        CopyBitsTests(ref test);

        test.Dispose();
    }

    [Test]
    public void UnsafeBitArray_Resize()
    {
        var test = new NativeBitArray(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        Assert.AreEqual(1, test.Length);
        Assert.AreEqual(64, test.Capacity);

        test.SetCapacity(200); // expand
        Assert.AreEqual(1, test.Length);
        Assert.AreEqual(256, test.Capacity);

        test.Resize(100, NativeArrayOptions.ClearMemory);
        Assert.True(test.TestNone(0, test.Length));

        // prepare survival test
        test.Set(0, true);
        test.Set(99, true);
        Assert.True(test.IsSet(0));
        Assert.True(test.TestNone(1, 98));
        Assert.True(test.IsSet(99));

        test.SetCapacity(1000); // expand
        Assert.AreEqual(100, test.Length);
        Assert.AreEqual(1024, test.Capacity);

        // test resize survival
        Assert.True(test.IsSet(0));
        Assert.True(test.TestNone(1, 98));
        Assert.True(test.IsSet(99));

        // manual clear
        test.Resize(1);
        test.Set(0, false);

        test.SetCapacity(200); // truncate capacity
        Assert.AreEqual(1, test.Length);
        Assert.AreEqual(256, test.Capacity);

        test.Resize(512, NativeArrayOptions.ClearMemory); // resize
        Assert.AreEqual(512, test.Length);
        Assert.AreEqual(512, test.Capacity);
        Assert.True(test.TestNone(0, test.Length));

        CopyBitsTests(ref test);

        test.Resize(256); // truncate length
        Assert.AreEqual(256, test.Length);
        Assert.AreEqual(512, test.Capacity);

        test.TrimExcess();
        Assert.AreEqual(256, test.Length);
        Assert.AreEqual(256, test.Capacity);

        test.Dispose();
    }

    [Test]
    public unsafe void NativeBitArray_CopyBetweenBitArrays()
    {
        var numBits = 512;

        var str = new FixedString128Bytes("aAbBcCdDeEfFgGhHiIjJkKlLmMnNoOpPqQrRsStTuUvVwWxXyYzZ");

        var test0 = NativeBitArrayUnsafeUtility.ConvertExistingDataToNativeBitArray(&str.bytes.offset0000, 64, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle ash = CollectionHelper.CreateSafetyHandle(Allocator.Temp);
        NativeBitArrayUnsafeUtility.SetAtomicSafetyHandle(ref test0, ash);
#endif

        var test1 = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        var test2 = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        for (int pos = 0; pos < test0.Length; pos += 64)
        {
            test1.SetBits(pos, 0x5555555555555555ul, 64);
            test2.SetBits(pos, 0xaaaaaaaaaaaaaaaaul, 64);
        }

        test1.Copy(1, ref test0, 205, 211);
        test1.Copy(214, ref test0, 0, 205);

        test2.Copy(205, ref test1, 1, 211);
        test2.Copy(0, ref test1, 214, 205);

        test0.Copy(0, ref test2, 0, 512);

        Assert.AreEqual(str, "aAbBcCdDeEfFgGhHiIjJkKlLmMnNoOpPqQrRsStTuUvVwWxXyYzZ");

        test0.Dispose();
        test1.Dispose();
        test2.Dispose();
    }

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks]
    public unsafe void NativeBitArray_Copy_Throws()
    {
        var numBits = 512;

        var test = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        Assert.Throws<ArgumentException>(() => { CopyBitsTest(ref test, 0, numBits - 1, 16); }); // short up to 64-bits copy out of bounds
        Assert.Throws<ArgumentException>(() => { CopyBitsTest(ref test, numBits - 1, 0, 16); }); // short up to 64-bits copy out of bounds

        Assert.Throws<ArgumentException>(() => { CopyBitsTest(ref test, 0, numBits - 1, 80); }); // short up to 128-bits copy out of bounds
        Assert.Throws<ArgumentException>(() => { CopyBitsTest(ref test, numBits - 1, 0, 80); }); // short up to 128-bits copy out of bounds

        Assert.Throws<ArgumentException>(() => { CopyBitsTest(ref test, 1, numBits - 7, 127); }); // long copy aligned
        Assert.Throws<ArgumentException>(() => { CopyBitsTest(ref test, numBits - 7, 1, 127); }); // long copy aligned

        Assert.Throws<ArgumentException>(() => { CopyBitsTest(ref test, 2, numBits - 1, 127); }); // long copy unaligned
        Assert.Throws<ArgumentException>(() => { CopyBitsTest(ref test, numBits - 1, 2, 127); }); // long copy unaligned

        test.Dispose();
    }

    [Test]
    public unsafe void NativeBitArray_Find()
    {
        var numBits = 512;

        using (var test = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory))
        {
            test.SetBits(0, true, 11);

            for (var i = 0; i < 256; ++i)
            {
                Assert.AreEqual(11, test.Find(0, i + 1));
            }

            for (var j = 0; j < 64; ++j)
            {
                for (var i = 0; i < 256; ++i)
                {
                    var numBitsToFind = 7 + i;
                    var pos = 37 + j;

                    test.SetBits(0, true, test.Length);
                    test.SetBits(pos, false, numBitsToFind);

                    Assert.AreEqual(pos, test.Find(0, numBitsToFind)); //, $"{j}/{i}: pos {pos}, numBitsToFind {numBitsToFind}");
                    Assert.AreEqual(pos, test.Find(pos, numBitsToFind)); //, $"{j}/{i}:pos {pos}, numBitsToFind {numBitsToFind}");

                    Assert.AreEqual(pos, test.Find(0, numBitsToFind)); //, $"{j}/{i}: pos {pos}, numBitsToFind {numBitsToFind}");
                    Assert.AreEqual(pos, test.Find(pos, numBitsToFind)); //, $"{j}/{i}: pos {pos}, numBitsToFind {numBitsToFind}");

                    Assert.IsTrue(test.TestNone(test.Find(0, numBitsToFind), numBitsToFind));

                    Assert.AreEqual(int.MaxValue, test.Find(pos + 1, numBitsToFind)); //, $"{j}/{i}: pos {pos}, numBitsToFind {numBitsToFind}");
                }
            }
        }
    }

    [Test]
    public unsafe void NativeBitArray_Find_With_Begin_End()
    {
        var numBits = 512;

        using (var test = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory))
        {
            Assert.AreEqual(0, test.Find(0, 2, 1));
            Assert.AreEqual(1, test.Find(1, 2, 1));
            test.SetBits(0, true, 6);
            Assert.AreEqual(int.MaxValue, test.Find(0, 2, 1));

            for (var j = 0; j < 64; ++j)
            {
                for (var i = 0; i < 256; ++i)
                {
                    var numBitsToFind = 7 + i;
                    var padding = 11;
                    var begin = 37 + j;
                    var end = begin + padding + numBitsToFind;
                    var count = end - begin;

                    test.Clear();
                    test.SetBits(begin, true, count);
                    test.SetBits(begin + padding + 1, false, numBitsToFind - 1);

                    Assert.AreEqual(begin + padding + 1, test.Find(begin, count, numBitsToFind - 1)); //, $"{j}/{i}: begin {begin}, end {end}, count {count}, numBitsToFind {numBitsToFind}");
                    Assert.AreEqual(int.MaxValue, test.Find(begin, count, numBitsToFind)); //, $"{j}/{i}: begin {begin}, end {end}, count {count}, numBitsToFind {numBitsToFind}");
                }
            }
        }
    }

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks]
    public unsafe void NativeBitArray_Find_Throws()
    {
        var numBits = 512;

        using (var test = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory))
        {
            Assert.Throws<ArgumentException>(() => { test.Find(0, 0, 1); });   // empty range
            Assert.Throws<ArgumentException>(() => { test.Find(0, 1, 0); });   // zero bits
            Assert.Throws<ArgumentException>(() => { test.Find(0, 1, 2); });   // numBits is larger than range
            Assert.Throws<ArgumentException>(() => { test.Find(10, 0, 0); });  // empty range, numBits is less than 1
            Assert.Throws<ArgumentException>(() => { test.Find(1, 10, -2); }); // numBits can't be negative
        }
    }

    [Test]
    public void NativeBitArray_AsNativeArray_Byte()
    {
        var numBits = 64;

        var test0 = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        var test1 = test0.AsNativeArray<byte>();
        Assert.AreEqual(numBits / 8, test1.Length);

        test1[0] = 0x10;
        test1[1] = 0x32;
        test1[2] = 0x54;
        test1[3] = 0x76;
        test1[4] = 0x98;
        test1[5] = 0xba;
        test1[6] = 0xdc;
        test1[7] = 0xfe;

        for (var i = 0; i < 16; ++i)
        {
            NUnit.Framework.Assert.AreEqual(i, test0.GetBits(i * 4, 4));
        }

        test1.Dispose();
        test0.Dispose();
    }

    [Test]
    public void NativeBitArray_AsNativeArray_Uint()
    {
        var numBits = 64;

        var test0 = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        var test1 = test0.AsNativeArray<uint>();
        Assert.AreEqual(numBits / 32, test1.Length);

        test1[0] = 0x76543210;
        test1[1] = 0xfedcba98;

        for (var i = 0; i < 16; ++i)
        {
            NUnit.Framework.Assert.AreEqual(i, test0.GetBits(i * 4, 4));
        }

        test1.Dispose();
        test0.Dispose();
    }

    [Test]
    public void NativeBitArray_AsNativeArray_Ulong()
    {
        var numBits = 64;

        var test0 = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        var test1 = test0.AsNativeArray<ulong>();
        Assert.AreEqual(numBits / 64, test1.Length);

        test1[0] = 0xfedcba9876543210;

        for (var i = 0; i < 16; ++i)
        {
            NUnit.Framework.Assert.AreEqual(i, test0.GetBits(i * 4, 4));
        }

        test1.Dispose();
        test0.Dispose();
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct SizeMismatch128
    {
    }

    [StructLayout(LayoutKind.Explicit, Size=7)]
    internal struct SizeMismatch56
    {
    }

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks]
    public void NativeBitArray_AsNativeArray_ThrowsOnSizeMismatch()
    {
        var numBits = 64;

        var test0 = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        Assert.Throws<InvalidOperationException>(() => { test0.AsNativeArray<SizeMismatch128>(); });
        Assert.Throws<InvalidOperationException>(() => { test0.AsNativeArray<SizeMismatch56>(); });

        test0.Dispose();
    }

    // Burst error BC1071: Unsupported assert type
    // [BurstCompile(CompileSynchronously = true)]
    struct NativeBitArrayTestReadOnly : IJob
    {
        public NativeBitArray.ReadOnly reader;

        public void Execute()
        {
            var rd = reader;
            Assert.True(reader.IsSet(7));
            Assert.AreEqual(1, reader.CountBits(0, reader.Length));
        }
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeBitArray_ReadOnly()
    {
        var numBits = 256;

        var reader = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        reader.Set(7, true);

        var readerJob = new NativeBitArrayTestReadOnly { reader = reader.AsReadOnly() }.Schedule();

        var from = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        Assert.Throws<InvalidOperationException>(() => { reader.Copy(7, ref from, 30, 10); } /* attempt to write into reader after job is scheduled */);
        from.Dispose();

        reader.Dispose(readerJob);
        readerJob.Complete();
    }

    // These tests require:
    // - JobsDebugger support for static safety IDs (added in 2020.1)
    // - Asserting throws
#if !UNITY_DOTSRUNTIME
    [Test,DotsRuntimeIgnore]
    [TestRequiresCollectionChecks]
    public void NativeBitArray_UseAfterFree_UsesCustomOwnerTypeName()
    {
        var numBits = 256;

        var test = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        SetBitsTest(ref test, 0, 16, 5);
        test.Dispose();
        NUnit.Framework.Assert.That(() => test.IsSet(0),
            Throws.Exception.TypeOf<ObjectDisposedException>()
                .With.Message.Contains($"The {test.GetType()} has been deallocated"));
    }

    [Test,DotsRuntimeIgnore]
    [TestRequiresCollectionChecks]
    public void NativeBitArray_AtomicSafetyHandle_AllocatorTemp_UniqueStaticSafetyIds()
    {
        var numBits = 256;

        var test = new NativeBitArray(numBits, Allocator.Temp, NativeArrayOptions.ClearMemory);

        // All collections that use Allocator.Temp share the same core AtomicSafetyHandle.
        // This test verifies that containers can proceed to assign unique static safety IDs to each
        // AtomicSafetyHandle value, which will not be shared by other containers using Allocator.Temp.
        var test0 = new NativeBitArray(numBits, Allocator.Temp, NativeArrayOptions.ClearMemory);
        var test1 = new NativeBitArray(numBits, Allocator.Temp, NativeArrayOptions.ClearMemory);
        SetBitsTest(ref test0, 0, 16, 5);
        test0.Dispose();

        NUnit.Framework.Assert.That(() => test0.IsSet(0),
            Throws.Exception.With.TypeOf<ObjectDisposedException>()
                .With.Message.Contains($"The {test0.GetType()} has been deallocated"));
        SetBitsTest(ref test1, 0, 16, 5);
        test1.Dispose();
        NUnit.Framework.Assert.That(() => test1.IsSet(0),
            Throws.Exception.With.TypeOf<ObjectDisposedException>()
                .With.Message.Contains($"The {test1.GetType()} has been deallocated"));
    }
#endif

    [BurstCompile(CompileSynchronously = true)]
    struct NativeBitArrayCreateAndUseAfterFreeBurst : IJob
    {
        public void Execute()
        {
            var numBits = 256;

            var test = new NativeBitArray(numBits, Allocator.Temp, NativeArrayOptions.ClearMemory);
            SetBitsTest(ref test, 0, 16, 5);
            test.Dispose();

            SetBitsTest(ref test, 0, 16, 5);
        }
    }

#if !UNITY_DOTSRUNTIME    // DOTS-Runtime does throw an exception.
    [Test]
    [TestRequiresCollectionChecks]
    public void NativeBitArray_CreateAndUseAfterFreeInBurstJob_UsesCustomOwnerTypeName()
    {
        // Make sure this isn't the first container of this type ever created, so that valid static safety data exists
        var numBits = 256;

        var test = new NativeBitArray(numBits, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        test.Dispose();

        var job = new NativeBitArrayCreateAndUseAfterFreeBurst
        {
        };

        // Two things:
        // 1. This exception is logged, not thrown; thus, we use LogAssert to detect it.
        // 2. Calling write operation after container.Dispose() emits an unintuitive error message. For now, all this test cares about is whether it contains the
        //    expected type name.
        job.Run();
        LogAssert.Expect(LogType.Exception,
            new Regex($"InvalidOperationException: The {Regex.Escape(test.GetType().ToString())} has been declared as \\[ReadOnly\\] in the job, but you are writing to it"));
    }
#endif

    void findWithPattern(ref NativeBitArray test, byte pattern, int numBits)
    {
        for (int pos = 0; pos < test.Length; pos += 8)
        {
            test.SetBits(pos, pattern, 8);
        }

        var bitCount = math.countbits((int)pattern);
        var numEmptyBits = test.Length - (test.Length / 8 * bitCount);

        for (int i = 0; i < numEmptyBits; i += numBits)
        {
            var pos = test.Find(0, numBits);
            Assert.AreNotEqual(int.MaxValue, pos, $"{i}");
            test.SetBits(pos, true, numBits);
        }

        Assert.True(test.TestAll(0, test.Length));
    }

    [Test]
    public void NativeBitArray_FindWithPattern()
    {
        var test = new NativeBitArray(512, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        // Separated test for some more interesting patterns
        findWithPattern(ref test, 0x81, 1);
        findWithPattern(ref test, 0x81, 2);
        findWithPattern(ref test, 0x81, 3);
        findWithPattern(ref test, 0x81, 6);
        findWithPattern(ref test, 0x88, 3);
        findWithPattern(ref test, 0x99, 2);
        findWithPattern(ref test, 0xaa, 1);
        findWithPattern(ref test, 0xc3, 1);
        findWithPattern(ref test, 0xc3, 2);
        findWithPattern(ref test, 0xc3, 4);
        findWithPattern(ref test, 0xe7, 1);
        findWithPattern(ref test, 0xe7, 2);

        // Test all patterns
        for (int i = 0; i < 256; i++)
        {
            findWithPattern(ref test, (byte)i, 1);
        }

        test.Dispose();
    }

    [Test]
    public void NativeBitArray_FindInTinyBitArray()
    {
        var test = new NativeBitArray(3, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        Assert.AreEqual(3, test.Length);

        test.SetBits(0, 0x55, test.Length);

        Assert.AreEqual(1, test.Find(0, 1));
        Assert.AreEqual(1, test.Find(0, test.Length, 1));
        test.SetBits(1, true, 1);
        Assert.True(test.TestAll(0, test.Length));
        Assert.AreEqual(int.MaxValue, test.Find(0, test.Length, 1));

        test.Dispose();
    }

    [Test]
    public void NativeBitArray_FindLastUnsetBit([NUnit.Framework.Range(1, 64)] int numBits)
    {
        using (var bits = new NativeBitArray(numBits, Allocator.Persistent))
        {
            // Set all bits to one then unset a single bit to find.
            for (int i = 0; i < numBits; ++i)
            {
                bits.SetBits(0, true, numBits);
                bits.Set(i, false);
                Assert.AreEqual(i, bits.Find(0, 1));
            }
        }
    }

    [Test]
    public void NativeBitArray_CustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        allocatorHelper.Allocator.Initialize();
        using (var bitset = new NativeBitArray(1, allocatorHelper.Allocator.Handle))
        {
        }

        Assert.IsTrue(allocatorHelper.Allocator.WasUsed);
        allocatorHelper.Allocator.Dispose();
        allocatorHelper.Dispose();
        AllocatorManager.Shutdown();
    }

    [BurstCompile]
    struct BurstedCustomAllocatorJob : IJob
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe CustomAllocatorTests.CountingAllocator* Allocator;

        public void Execute()
        {
            unsafe
            {
                using (var container = new NativeBitArray(1, Allocator->Handle))
                {
                }
            }
        }
    }

    [Test]
    public unsafe void NativeBitArray_BurstedCustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocatorHelper.Allocator.Initialize();

        var allocatorPtr = (CustomAllocatorTests.CountingAllocator*)UnsafeUtility.AddressOf<CustomAllocatorTests.CountingAllocator>(ref allocator);
        unsafe
        {
            var handle = new BurstedCustomAllocatorJob {Allocator = allocatorPtr}.Schedule();
            handle.Complete();
        }

        Assert.IsTrue(allocator.WasUsed);
        allocator.Dispose();
        allocatorHelper.Dispose();
        AllocatorManager.Shutdown();
    }
}
