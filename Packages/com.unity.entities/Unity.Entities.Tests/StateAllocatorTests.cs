using System;
using System.Diagnostics;
using NUnit.Framework;
using Unity.Burst;
using Unity.Mathematics;

namespace Unity.Entities.Tests
{
    [BurstCompile]
    public unsafe class StateAllocatorTests
    {
        private struct SystemDummy
        {
            public fixed byte Bytes[4097];
        }

        private WorldUnmanagedImpl.StateAllocator alloc;
        private SystemDummy systems;

        [SetUp]
        public void SetUp()
        {
            alloc.Init();
        }

        [TearDown]
        public void TearDown()
        {
            alloc.Dispose();
        }

        internal static int CountLiveByBits(ref WorldUnmanagedImpl.StateAllocator alloc)
        {
            int live = 0;

            for (int i = 0; i < 64; ++i)
            {
                live += math.countbits(~alloc.m_Level1[i].FreeBits);
            }

            return live;
        }

        internal static int CountLiveByTypeHash(ref WorldUnmanagedImpl.StateAllocator alloc)
        {
            int live = 0;

            for (int i = 0; i < 64; ++i)
            {
                for (int s = 0; s < 64; ++s)
                {
                    live += alloc.m_Level1[i].TypeHash[s] != 0 ? 1 : 0;
                }
            }

            return live;
        }

        internal static void SanityCheck(ref WorldUnmanagedImpl.StateAllocator alloc)
        {
        }

        [Test]
        public void BasicConstruction()
        {
            Assert.AreEqual(0, CountLiveByBits(ref alloc));
        }

        [Test]
        public void SimpleTest()
        {
            var p1 = alloc.Alloc(out var h1, out var v1, 987);
            var p2 = alloc.Alloc(out var h2, out var v2, 986);
            var p3 = alloc.Alloc(out var h3, out var v3, 985);

            Assert.AreNotEqual((IntPtr)p1, (IntPtr)p2);
            Assert.AreNotEqual((IntPtr)p2, (IntPtr)p3);
            Assert.AreNotEqual((IntPtr)p1, (IntPtr)p3);

            Assert.AreNotEqual(0, v1);
            Assert.AreNotEqual(0, v2);
            Assert.AreNotEqual(0, v3);

            Assert.AreNotEqual(h1, h2);
            Assert.AreNotEqual(h2, h3);
            Assert.AreNotEqual(h3, h1);

            Assert.AreEqual(3, CountLiveByBits(ref alloc));
            Assert.AreEqual(3, CountLiveByTypeHash(ref alloc));

            alloc.Free(h2);

            Assert.AreEqual(2, CountLiveByBits(ref alloc));
            Assert.AreEqual(2, CountLiveByTypeHash(ref alloc));

            var p2_ = alloc.Alloc(out var h2_, out var v2_, 981);

            Assert.AreEqual(3, CountLiveByBits(ref alloc));
            Assert.AreEqual(3, CountLiveByTypeHash(ref alloc));

            Assert.AreEqual((IntPtr)p2, (IntPtr)p2_);
            Assert.AreEqual(h2, h2_);
            Assert.AreNotEqual(v2, v2_);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void ThrowCountIsWrong()
        {
            throw new InvalidOperationException("count is wrong");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void ThrowResolveFailed()
        {
            throw new InvalidOperationException("resolve failed");
        }

        internal delegate void RunBurstTest(IntPtr allocPtr, IntPtr sysPtr);
        [BurstCompile(CompileSynchronously = true)]
        static void RunStressTest(IntPtr allocPtr, IntPtr sys_)
        {
            var alloc = (WorldUnmanagedImpl.StateAllocator*)allocPtr;
            ushort* handles = stackalloc ushort[4096];
            ushort* versions = stackalloc ushort[4096];

            // Fill the allocator completely
            for (int i = 0; i < 4096; ++i)
            {
                var p = alloc->Alloc(out handles[i], out versions[i], i + 1);
            }

            if (CountLiveByBits(ref *alloc) != 4096)
                ThrowCountIsWrong();

            if (CountLiveByTypeHash(ref *alloc) != 4096)
                ThrowCountIsWrong();

            // They should all resolve
            for (int i = 0; i < 4096; ++i)
            {
                if (null == alloc->Resolve(handles[i], versions[i]))
                    ThrowResolveFailed();
            }

            // Free every other system
            for (int i = 0; i < 4096; i += 2)
            {
                alloc->Free(handles[i]);
            }

            // Every other system should resolve
            for (int i = 0; i < 4096; i += 2)
            {
                bool freed = 0 == (i & 1);
                if (freed)
                {
                    if (null != alloc->Resolve(handles[i], versions[i]))
                        ThrowResolveFailed();
                }
                else
                {
                    if (null == alloc->Resolve(handles[i], versions[i]))
                        ThrowResolveFailed();
                }
            }

            if (CountLiveByBits(ref *alloc) != 2048)
                ThrowCountIsWrong();

            if (CountLiveByTypeHash(ref *alloc) != 2048)
                ThrowCountIsWrong();
        }

        [Test]
        public void StressTestFromBurst()
        {
            fixed(WorldUnmanagedImpl.StateAllocator* p = &alloc)
            fixed(byte* s = systems.Bytes)
            {
                BurstCompiler.CompileFunctionPointer<RunBurstTest>(RunStressTest).Invoke((IntPtr)p, (IntPtr)s);
            }
        }

        [Test]
        public void StressTestFromMono()
        {
            fixed(WorldUnmanagedImpl.StateAllocator* p = &alloc)
            fixed(byte* s = systems.Bytes)
            {
                RunStressTest((IntPtr)p, (IntPtr)s);
            }
        }
    }
}
