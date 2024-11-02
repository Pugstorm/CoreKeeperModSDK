#if ENABLE_UNITY_COLLECTIONS_CHECKS

using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Burst;

namespace Unity.Entities.Tests
{
    public unsafe class ScratchpadAllocatorTests : ECSTestsCommonBase
    {
        World m_world;
        Scratchpad m_scratchpad;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            m_world = new World("ScratchpadAllocatorTests");

#if UNITY_2022_2_14F1_OR_NEWER
            int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
            int maxThreadCount = JobsUtility.MaxJobThreadCount + 1;
#endif
            m_scratchpad = new Scratchpad(maxThreadCount);
        }

        [TearDown]
        public override void TearDown()
        {
            m_scratchpad.Dispose();
            m_world.Dispose();
            base.TearDown();
        }

        struct RewindInvalidatesNativeArrayJob : IJobParallelFor
        {
            internal Scratchpad m_jobScratchpad;
            public void Execute(int index)
            {
                ref var allocator = ref m_jobScratchpad.GetScratchpadAllocator();
                var array = allocator.AllocateNativeArray<float>(100);
                allocator.Rewind();
                array[0] = index;  // should throw an exception - isn't safe
            }
        }

        [Test]
        public void RewindInvalidatesNativeArrayInJob()
        {
            m_scratchpad.Rewind();
            RewindInvalidatesNativeArrayJob job = new RewindInvalidatesNativeArrayJob
            {
                m_jobScratchpad = m_scratchpad,
            };

            // temporary check to be replaced with the one below following an editor version promotion that includes
            // a NativeArray and NativeSlice atomic safety handle check revert
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException"));

            job.Schedule(1, 1).Complete();
        }

        struct RewindInvalidatesNativeListJob : IJobParallelFor
        {
            internal Scratchpad m_jobScratchpad;

            public void Execute(int index)
            {
                ref var allocator = ref m_jobScratchpad.GetScratchpadAllocator();
                var list = allocator.AllocateNativeList<float>(100);
                allocator.Rewind();
                list.Add(0); // should throw an exception - isn't safe
            }
        }

        [Test]
        public void RewindInvalidatesNativeListInJob()
        {
            m_scratchpad.Rewind();
            RewindInvalidatesNativeListJob job = new RewindInvalidatesNativeListJob
            {
                m_jobScratchpad = m_scratchpad,
            };
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException"));
            job.Schedule(1, 1).Complete();
        }

        struct ScratchpadNativeListCanResizeJob : IJobParallelFor
        {
            internal Scratchpad m_jobScratchpad;

            public void Execute(int index)
            {
                ref var allocator = ref m_jobScratchpad.GetScratchpadAllocator();
                allocator.Rewind();
                var list = new NativeList<float>(20, (Allocator)allocator.Handle.Value);
                list.Length = 200;
                list.Length = 2000;
                list.Add(0); // should be okay.
            }
        }

        [Test]
        public void ScratchpadNativeListCanResizeInJob()
        {
            m_scratchpad.Rewind();
            ScratchpadNativeListCanResizeJob job = new ScratchpadNativeListCanResizeJob
            {
                m_jobScratchpad = m_scratchpad,
            };
            job.Schedule(1, 1).Complete();
        }

        struct AccumulatorJob : IJobParallelFor
        {
            internal Scratchpad m_jobScratchpad;
            internal NativeArray<float> m_results;
            public void Execute(int index)
            {
                ref var allocator = ref m_jobScratchpad.GetScratchpadAllocator();
                allocator.Rewind();
                var array = allocator.AllocateNativeArray<float>(100);
                for (var i = 0; i < 100; ++i)
                    array[i] = index;
                float total = 0;
                for (var i = 0; i < 100; ++i)
                    total += array[i];
                m_results[index] = total;
            }
        }

        [Test]
        public void CanCheaplyAllocateTemporaryMemoryInJob()
        {
            m_scratchpad.Rewind();
            using (var results = new NativeArray<float>(100, Allocator.Persistent))
            {
                AccumulatorJob job = new AccumulatorJob
                {
                    m_jobScratchpad = m_scratchpad,
                    m_results = results,
                };
                job.Schedule(100, 1).Complete();
                for (var i = 0; i < 100; ++i)
                    Assert.AreEqual(100 * i, results[i]);
            }
        }

        struct RewindJob : IJobParallelFor
        {
            internal Scratchpad m_jobScratchpad;
            internal NativeArray<float> m_results;
            public void Execute(int index)
            {
                ref var allocator = ref m_jobScratchpad.GetScratchpadAllocator();
                var array = allocator.AllocateNativeArray<float>(8000); // 32000 bytes
                allocator.Rewind();
                array = allocator.AllocateNativeArray<float>(8000); // another 32000 bytes!
                for (var i = 0; i < 8000; ++i)
                    array[i] = index;
                float total = 0;
                for (var i = 0; i < 8000; ++i)
                    total += array[i];
                m_results[index] = total;
                allocator.Rewind();
            }
        }

        [Test]
        public void CanRewindTemporaryMemoryInJob()
        {
            m_scratchpad.Rewind();
            using (var results = new NativeArray<float>(100, Allocator.Persistent))
            {
                RewindJob job = new RewindJob
                {
                    m_jobScratchpad = m_scratchpad,
                    m_results = results,
                };
                job.Schedule(100, 1).Complete();
                for (var i = 0; i < 100; ++i)
                    Assert.AreEqual(8000 * i, results[i]);
            }
        }


        [BurstCompile]
        struct NoRewindBurstCompileJob : IJobParallelFor
        {
            internal Scratchpad m_jobScratchpad;
            internal NativeArray<float> m_results;
            public void Execute(int index)
            {
                ref var allocator = ref m_jobScratchpad.GetScratchpadAllocator();
                var array = allocator.AllocateNativeArray<float>(8000); // 32000 bytes
                for (var i = 0; i < 8000; ++i)
                    array[i] = index;
                float total = 0;
                for (var i = 0; i < 8000; ++i)
                    total += array[i];
                m_results[index] = total;
            }
        }

        [Test]
        public void NoRewindBurstCompileInJob()
        {
            m_scratchpad.Rewind();
            using (var results = new NativeArray<float>(100, Allocator.Persistent))
            {
                RewindJob job = new RewindJob
                {
                    m_jobScratchpad = m_scratchpad,
                    m_results = results,
                };
                job.Schedule(100, 1).Complete();
                for (var i = 0; i < 100; ++i)
                    Assert.AreEqual(8000 * i, results[i]);
            }
            m_scratchpad.Rewind();
        }

        [Test]
        public void AvailalbeBytesCorrect()
        {
#if UNITY_2022_2_14F1_OR_NEWER
            int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
            int maxThreadCount = JobsUtility.MaxJobThreadCount + 1; // account for main thread
#endif
            for (int i = 0; i < 3; i++)
            {
                int size = (i + 1) * 32768;
                var test_scratchpad = new Scratchpad(maxThreadCount, size);
                ref var allocator = ref test_scratchpad.GetScratchpadAllocator();
                var array = allocator.AllocateNativeArray<float>(10); // 40 bytes
                var mask = JobsUtility.CacheLineSize - 1;
                int actualAllocateSize = (sizeof(float) * 10 + mask) & ~mask;
                Assert.AreEqual(allocator.GetAvailableBytes(), size - actualAllocateSize);
                test_scratchpad.Dispose();
            }
        }
    }


    public unsafe class GlobalScratchpadAllocatorTests : ECSTestsCommonBase
    {
        [SetUp]
        public override void Setup()
        {
            GlobalScratchpad.Initialize();
            base.Setup();
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
            GlobalScratchpad.Dispose();
        }

        struct GlobalRewindInvalidatesNativeArrayJob : IJobParallelFor
        {
            public void Execute(int index)
            {
                ref var allocator = ref GlobalScratchpad.GetAllocator();
                var array = allocator.AllocateNativeArray<float>(100);
                allocator.Rewind();
                array[0] = index;  // should throw an exception - isn't safe
            }
        }

        [Test]
        public void GlobalRewindInvalidatesNativeArrayInJob()
        {
            GlobalScratchpad.Rewind();
            GlobalRewindInvalidatesNativeArrayJob job = new GlobalRewindInvalidatesNativeArrayJob { };

            // temporary check to be replaced with the one below following an editor version promotion that includes
            // a NativeArray and NativeSlice atomic safety handle check revert
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException"));

            job.Schedule(1, 1).Complete();
            GlobalScratchpad.Rewind();
        }

        struct GlobalRewindInvalidatesNativeListJob : IJobParallelFor
        {
            public void Execute(int index)
            {
                ref var allocator = ref GlobalScratchpad.GetAllocator();
                var list = allocator.AllocateNativeList<float>(100);
                allocator.Rewind();
                list.Add(0); // should throw an exception - isn't safe
            }
        }

        [Test]
        public void GlobalRewindInvalidatesNativeListInJob()
        {
            GlobalScratchpad.Rewind();
            GlobalRewindInvalidatesNativeListJob job = new GlobalRewindInvalidatesNativeListJob { };

            // temporary check to be replaced with the one below following an editor version promotion that includes
            // a NativeArray and NativeSlice atomic safety handle check revert
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException"));

            job.Schedule(1, 1).Complete();
            GlobalScratchpad.Rewind();
        }

        struct GlobalScratchpadNativeListCanResizeJob : IJobParallelFor
        {
            public void Execute(int index)
            {
                ref var allocator = ref GlobalScratchpad.GetAllocator();
                allocator.Rewind();
                var list = new NativeList<float>(20, (Allocator)allocator.Handle.Value);
                list.Length = 200;
                list.Length = 2000;
                list.Add(0); // should be okay.
            }
        }

        [Test]
        public void GlobalScratchpadNativeListCanResizeInJob()
        {
            GlobalScratchpad.Rewind();
            GlobalScratchpadNativeListCanResizeJob job = new GlobalScratchpadNativeListCanResizeJob { };
            job.Schedule(1, 1).Complete();
            GlobalScratchpad.Rewind();
        }

        struct GlobalAccumulatorJob : IJobParallelFor
        {
            internal NativeArray<float> m_results;
            public void Execute(int index)
            {
                ref var allocator = ref GlobalScratchpad.GetAllocator();
                allocator.Rewind();
                var array = allocator.AllocateNativeArray<float>(10);

                for (var i = 0; i < 10; ++i)
                    array[i] = index;

                float total = 0;
                for (var i = 0; i < 10; ++i)
                    total += array[i];

                m_results[index] = total;
            }
        }

        [Test]
        public void GlobalCanCheaplyAllocateTemporaryMemoryInJob()
        {
            int arrayLen = AllocatorManager.NumGlobalScratchAllocators;
            GlobalScratchpad.Rewind();
            using (var results = new NativeArray<float>(arrayLen, Allocator.Persistent))
            {
                GlobalAccumulatorJob job = new GlobalAccumulatorJob
                {
                    m_results = results,
                };
                job.Schedule(arrayLen, 1).Complete();

                for (var i = 0; i < arrayLen; ++i)
                    Assert.AreEqual(10 * i, results[i]);
            }
            GlobalScratchpad.Rewind();
        }

        struct GlobalRewindJob : IJobParallelFor
        {
            internal NativeArray<float> m_results;
            public void Execute(int index)
            {
                ref var allocator = ref GlobalScratchpad.GetAllocator();
                var array = allocator.AllocateNativeArray<float>(8000); // 32000 bytes
                allocator.Rewind();
                array = allocator.AllocateNativeArray<float>(8000); // another 32000 bytes!
                for (var i = 0; i < 8000; ++i)
                    array[i] = index;

                float total = 0;
                for (var i = 0; i < 8000; ++i)
                    total += array[i];

                m_results[index] = total;
                allocator.Rewind();
            }
        }

        [Test]
        public void GlobalCanRewindTemporaryMemoryInJob()
        {
            int arrayLen = AllocatorManager.NumGlobalScratchAllocators;

            GlobalScratchpad.Rewind();
            using (var results = new NativeArray<float>(arrayLen, Allocator.Persistent))
            {
                GlobalRewindJob job = new GlobalRewindJob
                {
                    m_results = results,
                };
                job.Schedule(arrayLen, 1).Complete();
                for (var i = 0; i < arrayLen; ++i)
                    Assert.AreEqual(8000 * i, results[i]);
            }
            GlobalScratchpad.Rewind();
        }


        [BurstCompile]
        struct GlobalNoRewindBurstCompileJob : IJobParallelFor
        {
            internal NativeArray<float> m_results;
            public void Execute(int index)
            {
                ref var allocator = ref GlobalScratchpad.GetAllocator();
                var array = allocator.AllocateNativeArray<float>(8000); // 32000 bytes
                for (var i = 0; i < 8000; ++i)
                    array[i] = index;
                float total = 0;
                for (var i = 0; i < 8000; ++i)
                    total += array[i];
                m_results[index] = total;
            }
        }

        [Test]
        public void GlobalNoRewindBurstCompileInJob()
        {
            GlobalScratchpad.Rewind();
            int arrayLen = AllocatorManager.NumGlobalScratchAllocators;
            using (var results = new NativeArray<float>(arrayLen, Allocator.Persistent))
            {
                GlobalNoRewindBurstCompileJob job = new GlobalNoRewindBurstCompileJob
                {
                    m_results = results,
                };
                job.Schedule(arrayLen, 1).Complete();
                for (var i = 0; i < arrayLen; ++i)
                    Assert.AreEqual(8000 * i, results[i]);
            }
            GlobalScratchpad.Rewind();
        }
    }
}

#endif
