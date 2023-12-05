using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.Tests;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

[TestFixture]
[BurstCompile]
internal class xxHash3Tests : CollectionsTestCommonBase
{
    private unsafe void* SanityBuffer;
    private unsafe void* DestinationBuffer;

    private const int SANITY_BUFFER_SIZE = 2367;

    [SetUp]
    public unsafe override void Setup()
    {
        base.Setup();

        unchecked
        {
            uint prime = 2654435761U;
            ulong prime64 = 11400714785074694797UL;
            ulong byteGen = prime;

            SanityBuffer = Memory.Unmanaged.Allocate(SANITY_BUFFER_SIZE, 64, Allocator.Persistent);
            byte* buffer = (byte*)SanityBuffer;

            DestinationBuffer = Memory.Unmanaged.Allocate(SANITY_BUFFER_SIZE, 64, Allocator.Persistent);

            int i;
            for (i=0; i<SANITY_BUFFER_SIZE; i++) {
                buffer[i] = (byte)(byteGen>>56);
                byteGen *= prime64;
            }
        }
    }

    [TearDown]
    public unsafe override void TearDown()
    {
        Memory.Unmanaged.Free(SanityBuffer, Allocator.Persistent);
        Memory.Unmanaged.Free(DestinationBuffer, Allocator.Persistent);
        base.TearDown();
    }

    [BurstCompile(CompileSynchronously = true)]
    struct xxHash3Hash64SanityCheckJob : IJob
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* SanityBuffer;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* DestinationBuffer;

        public long Length;
        public ulong Seed;

        public NativeArray<uint2> Result;

        public unsafe void Execute()
        {
            var resultIndex = 1;
            // Compute Hash from buffer
            Result[resultIndex++] = xxHash3.Hash64(SanityBuffer, Length, Seed);

            // Hash & copy and Streaming API is currently not supported with Hash64

            // // Compute/Copy (TODO API still not developed)
            // if (DestinationBuffer != null)
            // {
            //     CopySingleCallHashResult = xxHash3.Hash64(SanityBuffer, DestinationBuffer, Length, Seed);
            // }

            // Streaming API Test
            {
                var state = new xxHash3.StreamingState(true, Seed);
                state.Update(SanityBuffer, (int)Length);
                Result[resultIndex++] = state.DigestHash64();
            }

            // 2 updates
            if (Length > 3)
            {
                var state = new xxHash3.StreamingState(true, Seed);
                {
                    state.Update(SanityBuffer, 3);
                    state.Update((byte*)SanityBuffer+3, (int)Length-3);
                    Result[resultIndex++] = state.DigestHash64();
                }
            }

            // byte per byte update
            if (Length > 0) {
                var state = new xxHash3.StreamingState(true, Seed);
                {
                    var bBuffer = (byte*) SanityBuffer;
                    for (int i = 0; i < Length; i++)
                    {
                        state.Update(bBuffer + i, 1);
                    }
                    Result[resultIndex++] = state.DigestHash64();
                }
            }

            Result[0] = new uint2(resultIndex - 1);
        }
    }

    const ulong Prime = 2654435761U;
    const ulong Prime64 = 11400714785074694797UL;

    private unsafe void TestHash64(long length, ulong seed, ulong result, ulong resultWithSeed)
    {
        var job = new xxHash3Hash64SanityCheckJob
        {
            SanityBuffer = SanityBuffer,
            DestinationBuffer = DestinationBuffer,
            Result = CollectionHelper.CreateNativeArray<uint2>(10, CommonRwdAllocator.Handle),
            Seed = 0,
            Length = length
        };

        var b = xxHash3.ToUint2(result);
        job.Schedule().Complete();
        var resultCount = job.Result[0].x;
        for (int i = 0; i < resultCount; i++)
        {
            var a = job.Result[i+1];
            Assert.That(a, Is.EqualTo(b), $"Failed on entry {i}");
        }

        job.Seed = seed;
        job.Schedule().Complete();

        b = xxHash3.ToUint2(resultWithSeed);
        resultCount = job.Result[0].x;
        for (int i = 0; i < resultCount; i++)
        {
            Assert.That(job.Result[i+1], Is.EqualTo(b), $"Failed on entry {i}");
        }

        job.Result.Dispose();
    }

    [Test]
    public void xxHash3_Hash_64_Length0000()
    {
        TestHash64(0, Prime64, 0x2D06800538D394C2UL, 0xA8A6B918B2F0364AUL);
    }

    [Test]
    public void xxHash3_Hash_64_Length0001()
    {
        TestHash64(1, Prime64, 0xC44BDFF4074EECDBUL, 0x032BE332DD766EF8UL);
    }

    [Test]
    public void xxHash3_Hash_64_Length0006()
    {
        TestHash64(6, Prime64, 0x27B56A84CD2D7325UL, 0x84589C116AB59AB9UL);
    }

    [Test]
    public void xxHash3_Hash_64_Length0012()
    {
        TestHash64(12, Prime64, 0xA713DAF0DFBB77E7UL, 0xE7303E1B2336DE0EUL);
    }

    [Test]
    public void xxHash3_Hash_64_Length0024()
    {
        TestHash64(24, Prime64, 0xA3FE70BF9D3510EBUL, 0x850E80FC35BDD690UL);
    }

    [Test]
    public void xxHash3_Hash_64_Length0048()
    {
        TestHash64(48, Prime64, 0x397DA259ECBA1F11UL, 0xADC2CBAA44ACC616UL);
    }


    [Test]
    public void xxHash3_Hash_64_Length0080()
    {
        TestHash64(80, Prime64, 0xBCDEFBBB2C47C90AUL, 0xC6DD0CB699532E73UL);
    }

    [Test]
    public void xxHash3_Hash_64_Length0195()
    {
        TestHash64(195, Prime64, 0xCD94217EE362EC3AUL, 0xBA68003D370CB3D9UL);
    }

    [Test]
    public void xxHash3_Hash_64_Length0403()
    {
        TestHash64(403, Prime64, 0xCDEB804D65C6DEA4UL, 0x6259F6ECFD6443FDUL);
    }

    [Test]
    public void xxHash3_Hash_64_Length0512()
    {
        TestHash64(512, Prime64, 0x617E49599013CB6BUL, 0x3CE457DE14C27708UL);
    }

    [Test]
    public void xxHash3_Hash_64_Length2048()
    {
        TestHash64(2048, Prime64, 0xDD59E2C3A5F038E0UL, 0x66F81670669ABABCUL);
    }

    [Test]
    public void xxHash3_Hash_64_Length2240()
    {
        TestHash64(2240, Prime64, 0x6E73A90539CF2948UL, 0x757BA8487D1B5247UL);
    }

    [Test]
    public void xxHash3_Hash_64_Length2243()
    {
        TestHash64(2367, Prime64, 0xCB37AEB9E5D361EDUL, 0xD2DB3415B942B42AUL);
    }

    [BurstCompile(CompileSynchronously = true)]
    struct xxHash3Hash128SanityCheckJob : IJob
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* SanityBuffer;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* DestinationBuffer;

        public long Length;
        public ulong Seed;

        public NativeArray<uint4> Result;

        public unsafe void Execute()
        {
            var resultIndex = 1;
            // Compute Hash from buffer
            Result[resultIndex++] = xxHash3.Hash128(SanityBuffer, Length, Seed);

            // Compute/Copy
            if (DestinationBuffer != null)
            {
                Result[resultIndex++] = xxHash3.Hash128(SanityBuffer, DestinationBuffer, Length, Seed);
            }

            // Streaming API Test
            {
                var state = new xxHash3.StreamingState(false, Seed);
                state.Update(SanityBuffer, (int)Length);
                Result[resultIndex++] = state.DigestHash128();
            }

            // 2 updates
            if (Length > 3)
            {
                var state = new xxHash3.StreamingState(false, Seed);
                {
                    state.Update(SanityBuffer, 3);
                    state.Update((byte*)SanityBuffer+3, (int)Length-3);
                    Result[resultIndex++] = state.DigestHash128();
                }
            }

            // byte per byte update
            if (Length > 0) {
                var state = new xxHash3.StreamingState(false, Seed);
                {
                    var bBuffer = (byte*) SanityBuffer;
                    for (int i = 0; i < Length; i++)
                    {
                        state.Update(bBuffer + i, 1);
                    }
                    Result[resultIndex++] = state.DigestHash128();
                }
            }

            Result[0] = new uint4(resultIndex - 1);
        }
    }

    private unsafe void TestHash128(long length, ulong seed, uint4 result, uint4 resultWithSeed)
    {
        var job = new xxHash3Hash128SanityCheckJob
        {
            SanityBuffer = SanityBuffer,
            DestinationBuffer = DestinationBuffer,
            Result = CollectionHelper.CreateNativeArray<uint4>(10, CommonRwdAllocator.Handle),
            Seed = 0,
            Length = length
        };

        job.Schedule().Complete();

        var resultCount = (int)job.Result[0].x;
        for (int i = 0; i < resultCount; i++)
        {
            Assert.That(job.Result[i+1], Is.EqualTo(result), $"Failed on entry {i}");
        }

        job.Seed = seed;
        job.Schedule().Complete();

        resultCount = (int)job.Result[0].x;
        for (int i = 0; i < resultCount; i++)
        {
            Assert.That(job.Result[i+1], Is.EqualTo(resultWithSeed), $"Failed on entry {i}");
        }

        job.Result.Dispose();
    }

    [Test]
    public unsafe void xxHash3_Hash_128_Length0000()
    {
        TestHash128(0, Prime,
            xxHash3.ToUint4(0x6001C324468D497FUL, 0x99AA06D3014798D8UL),
            xxHash3.ToUint4(0x5444F7869C671AB0UL, 0x92220AE55E14AB50UL));
    }

    [Test]
    public void xxHash3_Hash_128_Length0001()
    {
        TestHash128(1, Prime,
            xxHash3.ToUint4(0xC44BDFF4074EECDBUL, 0xA6CD5E9392000F6AUL),
            xxHash3.ToUint4(0xB53D5557E7F76F8DUL, 0x89B99554BA22467CUL));
    }

    [Test]
    public void xxHash3_Hash_128_Length0006()
    {
            // Length 6
            TestHash128(6, Prime,
                xxHash3.ToUint4(0x3E7039BDDA43CFC6UL, 0x082AFE0B8162D12AUL),
                xxHash3.ToUint4(0x269D8F70BE98856EUL, 0x5A865B5389ABD2B1UL));
    }

    [Test]
    public void xxHash3_Hash_128_Length0012()
    {
        // Length 12
        TestHash128(12, Prime,
            xxHash3.ToUint4(0x061A192713F69AD9UL, 0x6E3EFD8FC7802B18UL),
            xxHash3.ToUint4(0x9BE9F9A67F3C7DFBUL, 0xD7E09D518A3405D3UL));
    }

    [Test]
    public void xxHash3_Hash_128_Length0024()
    {
        // Length 24
        TestHash128(24, Prime,
            xxHash3.ToUint4(0x1E7044D28B1B901DUL, 0x0CE966E4678D3761UL),
            xxHash3.ToUint4(0xD7304C54EBAD40A9UL, 0x3162026714A6A243UL));
    }

    [Test]
    public void xxHash3_Hash_128_Length0048()
    {
        // Length 48
        TestHash128(48, Prime,
            xxHash3.ToUint4(0xF942219AED80F67BUL, 0xA002AC4E5478227EUL),
            xxHash3.ToUint4(0x7BA3C3E453A1934EUL, 0x163ADDE36C072295UL));
    }

    [Test]
    public void xxHash3_Hash_128_Length0081()
    {
        // Length 81
        TestHash128(81, Prime,
            xxHash3.ToUint4(0x5E8BAFB9F95FB803UL, 0x4952F58181AB0042UL),
            xxHash3.ToUint4(0x703FBB3D7A5F755CUL, 0x2724EC7ADC750FB6UL));
    }

    [Test]
    public void xxHash3_Hash_128_Length0222()
    {
        // Length 222
        TestHash128(222, Prime,
            xxHash3.ToUint4(0xF1AEBD597CEC6B3AUL, 0x337E09641B948717UL),
            xxHash3.ToUint4(0xAE995BB8AF917A8DUL, 0x91820016621E97F1UL));
    }

    [Test]
    public void xxHash3_Hash_128_Length0403()
    {
        // Length 403
        TestHash128(403, Prime64,
            xxHash3.ToUint4(0xCDEB804D65C6DEA4UL, 0x1B6DE21E332DD73DUL),
            xxHash3.ToUint4(0x6259F6ECFD6443FDUL, 0xBED311971E0BE8F2UL));
    }

    [Test]
    public void xxHash3_Hash_128_Length0512()
    {
        // Length 512
        TestHash128(512, Prime64,
            xxHash3.ToUint4(0x617E49599013CB6BUL, 0x18D2D110DCC9BCA1UL),
            xxHash3.ToUint4(0x3CE457DE14C27708UL, 0x925D06B8EC5B8040UL));
    }

    [Test]
    public void xxHash3_Hash_128_Length2048()
    {
        // Length 2048
        TestHash128(2048, Prime,
            xxHash3.ToUint4(0xDD59E2C3A5F038E0UL, 0xF736557FD47073A5UL),
            xxHash3.ToUint4(0x230D43F30206260BUL, 0x7FB03F7E7186C3EAUL));
    }

    [Test]
    public void xxHash3_Hash_128_Length2240()
    {
        // Length 2240
        TestHash128(2240, Prime,
            xxHash3.ToUint4(0x6E73A90539CF2948UL, 0xCCB134FBFA7CE49DUL),
            xxHash3.ToUint4(0xED385111126FBA6FUL, 0x50A1FE17B338995FUL));
    }

    [Test]
    public void xxHash3_Hash_128_Length2367()
    {
        // Length 2367
        TestHash128(2367, Prime,
            xxHash3.ToUint4(0xCB37AEB9E5D361EDUL, 0xE89C0F6FF369B427UL),
            xxHash3.ToUint4(0x6F5360AE69C2F406UL, 0xD23AAE4B76C31ECBUL));
    }
}
