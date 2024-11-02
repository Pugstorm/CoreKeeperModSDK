using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Mathematics;

namespace Unity.Entities.Tests
{
    public class BufferAllocatorTestsBase
    {
        static AllocatorHelper<RewindableAllocator> m_AllocatorHelper;
        protected static ref RewindableAllocator RwdAllocator => ref m_AllocatorHelper.Allocator;

        [OneTimeSetUp]
        public virtual void OneTimeSetUp()
        {
            m_AllocatorHelper = new AllocatorHelper<RewindableAllocator>(Allocator.Persistent);
            m_AllocatorHelper.Allocator.Initialize(128 * 1024, true);
        }

        [OneTimeTearDown]
        public virtual void OneTimeTearDown()
        {
            m_AllocatorHelper.Allocator.Dispose();
            m_AllocatorHelper.Dispose();
        }

        [TearDown]
        public virtual void TearDown()
        {
            RwdAllocator.Rewind();
            // This is test only behavior for determinism.  Rewind twice such that all
            // tests start with an allocator containing only one memory block.
            RwdAllocator.Rewind();
        }

        internal delegate void RunTestDelegate(IBufferAllocator allocator);

        // This function exists to put the allocator type name in the callstack to make it easier to identify
        // which implementation failed.
        internal static void TestBufferAllocatorHeap(BufferAllocatorHeap allocator, RunTestDelegate f)
        {
            f(allocator);
        }

        // This function exists to put the allocator type name in the callstack to make it easier to identify
        // which implementation failed.
        internal static void TestBufferAllocator(BufferAllocator allocator, RunTestDelegate f)
        {
            f(allocator);
        }

        /// <summary>
        /// Run the BufferAllocator tests on concrete implementations, BufferAllocatorHeap and BufferAllocatorVirtualMemory.
        /// </summary>
        /// <remarks>If virtual memory is supported, then we will run both implementations. If not, only the heap version
        /// will be tested.</remarks>
        /// <param name="bufferCount">Buffer count to initialize the allocator with.</param>
        /// <param name="f">Delegate to execute with the allocator.</param>
        internal static void RunTest(int bufferBytes, int bufferCount, RunTestDelegate f)
        {
            // Explicitly test the heap BufferAllocator, even if virtual memory is supported by the platform.
            using (var allocator = new BufferAllocatorHeap(bufferCount * bufferBytes, bufferBytes, RwdAllocator.ToAllocator))
            {
                TestBufferAllocatorHeap(allocator, f);
            }

            RwdAllocator.Rewind();

            // Test the generic BufferAllocator which selects either virtual memory or heap backed versions, depending on the platform.
            using (var allocator = new BufferAllocator(bufferCount * bufferBytes, bufferBytes, RwdAllocator.ToAllocator))
            {
                TestBufferAllocator(allocator, f);
            }

            RwdAllocator.Rewind();
        }

        /// <summary>
        /// Shuffles array elements.
        /// </summary>
        /// <param name="array">Array to shuffle.</param>
        /// <param name="rng">Random instance to use for shuffling.</param>
        internal static void FisherYatesShuffle(NativeArray<int> array, ref Random rng)
        {
            int length = array.Length;
            for (int i = 0; i < length; ++i)
            {
                int j = rng.NextInt(i, length);
                int t = array[i];
                array[i] = array[j];
                array[j] = t;
            }
        }
    }

    public class BufferAllocatorTests : BufferAllocatorTestsBase
    {
        const int kBufferBytes = 16 * 1024;

        [Test]
        public void Create([Values(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 128)] int bufferCount)
        {
            RunTest(kBufferBytes, bufferCount, (allocator) =>
            {
                Assert.AreEqual(bufferCount, allocator.BufferCapacity);
                Assert.False(allocator.IsEmpty);
            });
        }

        [Test]
        public void IsEmpty()
        {
            RunTest(kBufferBytes, 1, (allocator) =>
            {
                Assert.IsFalse(allocator.IsEmpty);
                var index = allocator.Allocate();
                Assert.AreEqual(0, index);
                Assert.IsTrue(allocator.IsEmpty);
            });
        }

        [Test]
        public void AllocateAndFreeRepeatedly()
        {
            const int kBufferCount = 5;
            const int kAllocateCount = 3000;
            RunTest(kBufferBytes, kBufferCount, (allocator) =>
            {
                for (int i = 0; i < kAllocateCount; ++i)
                {
                    var bufferIndex = allocator.Allocate();
                    Assert.LessOrEqual(0, bufferIndex);
                    Assert.Less(bufferIndex, allocator.BufferCapacity);
                    allocator.Free(bufferIndex);
                }
            });
        }

        [Test]
        public void AllocateLastFreeBuffer([Values(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 15, 16, 17, 31, 32, 33, 63, 64, 65, 127, 128, 129)]
            int bufferCount)
        {
            RunTest(kBufferBytes, bufferCount, (allocator) =>
            {
                // Out of bufferCount, pick one buffer index to be the last free buffer.
                // Each loop iteration will allocate everything, then free this index and then
                // allocate it again. The goal is to make sure that we can always allocate
                // the last buffer regardless of its position/index.
                for (int lastFreeIndex = 0; lastFreeIndex < bufferCount; ++lastFreeIndex)
                {
                    // First, allocate everything.
                    for (int i = 0; i < bufferCount; ++i)
                    {
                        allocator.Allocate();
                    }

                    Assert.IsTrue(allocator.IsEmpty);
                    allocator.Free(lastFreeIndex);
                    Assert.IsFalse(allocator.IsEmpty);

                    // Allocate the last buffer which should be lastFreeIndex.
                    int index = allocator.Allocate();
                    Assert.AreEqual(lastFreeIndex, index);
                    Assert.IsTrue(allocator.IsEmpty);

                    // Free everything.
                    for (int i = 0; i < bufferCount; ++i)
                    {
                        allocator.Free(i);
                    }
                }
            });
        }

        [Test]
        public void AllocateAndFreeRandomly([Values(32, 64, 128)] int iterations)
        {
            const int kBufferCount = 2048;

            RunTest(kBufferBytes, kBufferCount, (allocator) =>
            {
                var indices = CollectionHelper.CreateNativeArray<int>(kBufferCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                for (int iteration = 0; iteration < iterations; ++iteration)
                {
                    // Allocate everything and keep track of the indices allocated.
                    for (int i = 0; i < kBufferCount; ++i)
                    {
                        indices[i] = allocator.Allocate();
                    }

                    Assert.IsTrue(allocator.IsEmpty);

                    // Shuffle all the allocated indices and free them in a random order.
                    const uint kArbitrarySeed = 98281u;
                    var rng = new Random(kArbitrarySeed);
                    FisherYatesShuffle(indices, ref rng);

                    for (int i = 0; i < kBufferCount; ++i)
                    {
                        allocator.Free(indices[i]);
                    }

                    Assert.IsFalse(allocator.IsEmpty);

                    RwdAllocator.Rewind();
                }

                indices.Dispose();
            });
        }
    }
}
