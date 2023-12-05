using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.Tests;
using Unity.Mathematics;

[TestFixture]
internal class UnsafeUtilityTests : CollectionsTestCommonBase
{
#pragma warning disable 649
    struct DummyVec
    {
        public uint A, B, C, D;
    }
#pragma warning restore

    private NativeArray<T> MakeTestArray<T>(params T[] data)
        where T : unmanaged
    {
        return CollectionHelper.CreateNativeArray<T>(data, CommonRwdAllocator.Handle);
    }

    [Test]
    public void ReinterpretUIntFloat()
    {
        using (var src = MakeTestArray(1.0f, 2.0f, 3.0f))
        {
            var dst = src.Reinterpret<float, uint>();
            Assert.AreEqual(src.Length, dst.Length);
            Assert.AreEqual(0x3f800000u, dst[0]);
            Assert.AreEqual(0x40000000u, dst[1]);
            Assert.AreEqual(0x40400000u, dst[2]);
        }
    }

    [Test]
    public void ReinterpretUInt4Float()
    {
        using (var src = MakeTestArray(1.0f, 2.0f, 3.0f, -1.0f))
        {
            var dst = src.Reinterpret<float, DummyVec>();
            Assert.AreEqual(1, dst.Length);

            var e = dst[0];
            Assert.AreEqual(0x3f800000u, e.A);
            Assert.AreEqual(0x40000000u, e.B);
            Assert.AreEqual(0x40400000u, e.C);
            Assert.AreEqual(0xbf800000u, e.D);
        }
    }

    [Test]
    public void ReinterpretFloatUint4()
    {
        var dummies = new DummyVec[]
        {
            new DummyVec { A = 0x3f800000u, B = 0x40000000u, C = 0x40400000u, D = 0xbf800000u },
            new DummyVec { A = 0xbf800000u, B = 0xc0000000u, C = 0xc0400000u, D = 0x3f800000u },
        };

        using (var src = MakeTestArray(dummies))
        {
            var dst = src.Reinterpret<DummyVec, float>();
            Assert.AreEqual(8, dst.Length);

            Assert.AreEqual(1.0f, dst[0]);
            Assert.AreEqual(2.0f, dst[1]);
            Assert.AreEqual(3.0f, dst[2]);
            Assert.AreEqual(-1.0f, dst[3]);
            Assert.AreEqual(-1.0f, dst[4]);
            Assert.AreEqual(-2.0f, dst[5]);
            Assert.AreEqual(-3.0f, dst[6]);
            Assert.AreEqual(1.0f, dst[7]);
        }
    }

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks]
    public void MismatchThrows1()
    {
        using (var src = MakeTestArray(0.0f, 1.0f, 2.0f))
        {
            Assert.Throws<InvalidOperationException>(() => src.Reinterpret<float, DummyVec>());
        }
    }

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks]
    public void MismatchThrows2()
    {
        using (var src = MakeTestArray(12))
        {
            Assert.Throws<InvalidOperationException>(() => src.Reinterpret<int, double>());
        }
    }

    [Test]
    public void AliasCanBeDisposed()
    {
        using (var src = MakeTestArray(12))
        {
            using (var dst = src.Reinterpret<int, float>())
            {
            }
        }
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void CannotUseAliasAfterSourceIsDisposed()
    {
        NativeArray<float> alias;
        var src = MakeTestArray(12);
        alias = src.Reinterpret<int, float>();

        // `Free` of memory allocated by world update allocator is an no-op.
        // World update allocator needs to be rewound in order to free the memory.
        CommonRwdAllocator.Rewind();

        Assert.Throws<ObjectDisposedException>(
            () => alias[0] = 1.0f);
    }

    [Test]
    public void MutabilityWorks()
    {
        using (var src = MakeTestArray(0.0f, -1.0f))
        {
            var alias = src.Reinterpret<float, uint>();
            alias[0] = 0x3f800000;
            Assert.AreEqual(1.0f, src[0]);
            Assert.AreEqual(-1.0f, src[1]);
        }
    }

#pragma warning disable 0169 // field is never used
    struct AlignOfX
    {
        float x;
        bool y;
    }

    struct AlignOfY
    {
        float x;
        bool y;
        float z;
        bool w;
    }

    struct AlignOfZ
    {
        float4 x;
        bool y;
    }

    struct AlignOfW
    {
        float4 x;
        bool y;
        float4x4 z;
        bool w;
    }

    struct BoolLong
    {
        bool x;
        long y;
    }

    struct BoolPtr
    {
        bool x;
        unsafe void* y;
    }
#pragma warning restore 0169 // field is never used

    [Test]
    public void UnsafeUtility_AlignOf()
    {
        Assert.AreEqual(UnsafeUtility.SizeOf<byte>(), UnsafeUtility.AlignOf<byte>());
        Assert.AreEqual(UnsafeUtility.SizeOf<short>(), UnsafeUtility.AlignOf<short>());
        Assert.AreEqual(UnsafeUtility.SizeOf<ushort>(), UnsafeUtility.AlignOf<ushort>());
        Assert.AreEqual(UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>());
        Assert.AreEqual(UnsafeUtility.SizeOf<uint>(), UnsafeUtility.AlignOf<uint>());
        Assert.AreEqual(UnsafeUtility.SizeOf<long>(), UnsafeUtility.AlignOf<long>());
        Assert.AreEqual(UnsafeUtility.SizeOf<ulong>(), UnsafeUtility.AlignOf<ulong>());
        Assert.AreEqual(4, UnsafeUtility.AlignOf<float4>());

        Assert.AreEqual(4, UnsafeUtility.AlignOf<AlignOfX>());
        Assert.AreEqual(4, UnsafeUtility.AlignOf<AlignOfY>());
        Assert.AreEqual(4, UnsafeUtility.AlignOf<AlignOfZ>());
        Assert.AreEqual(4, UnsafeUtility.AlignOf<AlignOfW>());
        Assert.AreEqual(8, UnsafeUtility.AlignOf<BoolLong>());
        Assert.AreEqual(UnsafeUtility.SizeOf<IntPtr>(), UnsafeUtility.AlignOf<BoolPtr>());
    }

    [Test]
    public unsafe void UnsafeUtility_MemSwap()
    {
        using (var array0 = MakeTestArray(0x12345678, 0x12345678, 0x12345678, 0x12345678, 0x12345678, 0x12345678))
        using (var array1 = MakeTestArray(0x21436587, 0x21436587, 0x21436587, 0x21436587, 0x21436587, 0x21436587))
        {
            UnsafeUtilityExtensions.MemSwap(NativeArrayUnsafeUtility.GetUnsafePtr(array0), NativeArrayUnsafeUtility.GetUnsafePtr(array1), array0.Length*UnsafeUtility.SizeOf<int>());

            foreach (var b in array0) { Assert.AreEqual(0x21436587, b); }
            foreach (var b in array1) { Assert.AreEqual(0x12345678, b); }
        }
    }

    [Test]
    [TestRequiresCollectionChecks]
    public unsafe void UnsafeUtility_MemSwap_DoesThrow_Overlapped()
    {
        using (var array0 = MakeTestArray(0x12345678, 0x12345678, 0x12345678, 0x12345678, 0x12345678, 0x12345678))
        {
            var mem = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(array0);
            var len = array0.Length * UnsafeUtility.SizeOf<int>();

            Assert.DoesNotThrow(() => { UnsafeUtilityExtensions.MemSwap(mem + 10, mem, 10); });
            Assert.Throws<InvalidOperationException>(() => { UnsafeUtilityExtensions.MemSwap(mem + 10, mem, len - 10); });

            Assert.DoesNotThrow(() => { UnsafeUtilityExtensions.MemSwap(mem, mem + 10, 10); });
            Assert.Throws<InvalidOperationException>(() => { UnsafeUtilityExtensions.MemSwap(mem, mem + 10, len - 10); });
        }
    }

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks]
    public unsafe void UnsafeUtility_ReadArrayElementBoundsChecked_Works()
    {
        using (var array0 = MakeTestArray(0x12345678, 0x12345678, 0x12345678, 0x12345678, 0x12345678, 0x12345678))
        {
            var mem = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(array0);
            var len = array0.Length;

            Assert.DoesNotThrow(() => { UnsafeUtilityExtensions.ReadArrayElementBoundsChecked<int>(mem, 5, len); });
            Assert.Throws<IndexOutOfRangeException>(() => { UnsafeUtilityExtensions.ReadArrayElementBoundsChecked<int>(mem, 6, len); });
            Assert.Throws<IndexOutOfRangeException>(() => { UnsafeUtilityExtensions.ReadArrayElementBoundsChecked<int>(mem, -1, len); });
        }
    }


    [Test]
    [TestRequiresDotsDebugOrCollectionChecks]
    public unsafe void UnsafeUtility_WriteArrayElementBoundsChecked_Works()
    {
        using (var array0 = MakeTestArray(0x12345678, 0x12345678, 0x12345678, 0x12345678, 0x12345678, 0x12345678))
        {
            var mem = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(array0);
            var len = array0.Length;

            Assert.DoesNotThrow(() => { UnsafeUtilityExtensions.WriteArrayElementBoundsChecked(mem, 5, -98765432, len); });
            Assert.Throws<IndexOutOfRangeException>(() => { UnsafeUtilityExtensions.WriteArrayElementBoundsChecked(mem, 6, -98765432, len); });
            Assert.Throws<IndexOutOfRangeException>(() => { UnsafeUtilityExtensions.WriteArrayElementBoundsChecked(mem, -1, -98765432, len); });
        }
    }

    [Test]
    public unsafe void UnsafeUtility_AsRefAddressOfIn_Works()
    {
        DummyVec thing = default;

        void* thingInPtr = UnsafeUtilityExtensions.AddressOf(in thing);
        ref DummyVec thingRef = ref UnsafeUtilityExtensions.AsRef(in thing);
        void* thingInRefPtr = UnsafeUtility.AddressOf(ref thingRef);
        void* thingRefPtr = UnsafeUtility.AddressOf(ref thing);
        void* thingPtr = &thing;

        Assert.AreEqual((IntPtr) thingPtr, (IntPtr) thingInPtr);
        Assert.AreEqual((IntPtr) thingPtr, (IntPtr) thingRefPtr);
        Assert.AreEqual((IntPtr) thingPtr, (IntPtr) thingInRefPtr);
    }
}
