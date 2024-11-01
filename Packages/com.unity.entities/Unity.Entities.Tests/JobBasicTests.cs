using System;
using System.Threading;
using NUnit.Framework;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.Entities.Tests
{
    public enum JobRunType
    {
        Schedule,
        ScheduleByRef,
        Run,
        RunByRef,
        RunWithoutJobs,
    }

    public struct JobRunTypeComp : IComponentData
    {
        public JobRunType type;
    }

    // These are very basic tests. As we bring up the Tiny system,
    // it's useful to have super simple tests to make sure basics
    // are working.
    public class JobBasicTests : ECSTestsFixture
    {
        // TODO calling nUnit Assert on a job thread may be causing errors.
        // Until sorted out, pull a simple exception out for use by the worker threads.
        static void AssertOnThread(bool test)
        {
            if (!test)
            {
                Console.WriteLine("AssertOnThread failed.");
                throw new Exception("AssertOnThread Failed.");
            }
        }

        public struct SimpleJob : IJob
        {
            public const int N = 1000;

            public int a;
            public int b;

            [WriteOnly]
            public NativeArray<int> result;

            public void Execute()
            {
                for (int i = 0; i < N; ++i)
                    result[i] = a + b;
            }
        }

        [Test]
        public void RunSimpleJob()
        {
            SimpleJob job = new SimpleJob()
            {
                a = 5,
                b = 10
            };
            NativeArray<int> jobResult = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(SimpleJob.N, ref World.UpdateAllocator);
            job.result = jobResult;

            job.Run();

            for (int i = 0; i < SimpleJob.N; ++i)
            {
                Assert.AreEqual(15, jobResult[i]);
            }
        }

        [Test]
        public void ScheduleSimpleJob()
        {
            SimpleJob job = new SimpleJob()
            {
                a = 5,
                b = 10
            };

            NativeArray<int> jobResult = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(SimpleJob.N, ref World.UpdateAllocator);
            job.result = jobResult;

            JobHandle handle = job.Schedule();
            handle.Complete();

            for (int i = 0; i < SimpleJob.N; ++i)
            {
                Assert.AreEqual(15, jobResult[i]);
            }
        }

        public struct SimpleAddSerial : IJob
        {
            public const int N = 1000;

            public int a;

            [ReadOnly]
            public NativeArray<int> input;

            [WriteOnly]
            public NativeArray<int> result;

            public void Execute()
            {
                for (int i = 0; i < N; ++i)
                    result[i] = a + input[i];
            }
        }

        [Test]
        public void Run3SimpleJobsInSerial()
        {
            NativeArray<int> input = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(SimpleAddSerial.N, ref World.UpdateAllocator);
            NativeArray<int> jobResult1 = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(SimpleAddSerial.N, ref World.UpdateAllocator);
            NativeArray<int> jobResult2 = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(SimpleAddSerial.N, ref World.UpdateAllocator);
            NativeArray<int> jobResult3 = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(SimpleAddSerial.N, ref World.UpdateAllocator);

            for (int i = 0; i < SimpleAddSerial.N; ++i)
            {
                input[i] = i;
            }

            SimpleAddSerial job1 = new SimpleAddSerial() {a = 1, input = input, result = jobResult1};
            SimpleAddSerial job2 = new SimpleAddSerial() {a = 2, input = jobResult1, result = jobResult2};
            SimpleAddSerial job3 = new SimpleAddSerial() {a = 3, input = jobResult2, result = jobResult3};

            Assert.IsFalse(JobsUtility.IsExecutingJob);

            JobHandle handle1 = job1.Schedule();
            JobHandle handle2 = job2.Schedule(handle1);
            JobHandle handle3 = job3.Schedule(handle2);
            handle3.Complete();

            Assert.IsFalse(JobsUtility.IsExecutingJob);

            for (int i = 0; i < SimpleAddSerial.N; ++i)
            {
                Assert.AreEqual(i + 1 + 2 + 3, jobResult3[i]);
            }
        }

        public struct SimpleAddParallel : IJob
        {
            public const int N = 1000;

            public int a;

            [ReadOnly]
            public NativeArray<int> input;

            [WriteOnly]
            public NativeArray<int> result;

            public void Execute()
            {
                for (int i = 0; i < N; ++i)
                    result[i] = a + input[i];
            }
        }

        [Test]
        public void Schedule3SimpleJobsInParallel()
        {
            NativeArray<int> input = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(SimpleAddParallel.N, ref World.UpdateAllocator);
            NativeArray<int> jobResult1 = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(SimpleAddParallel.N, ref World.UpdateAllocator);
            NativeArray<int> jobResult2 = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(SimpleAddParallel.N, ref World.UpdateAllocator);
            NativeArray<int> jobResult3 = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(SimpleAddParallel.N, ref World.UpdateAllocator);

            for (int i = 0; i < SimpleAddParallel.N; ++i)
            {
                input[i] = i;
            }

            SimpleAddParallel job1 = new SimpleAddParallel() {a = 1, input = input, result = jobResult1};
            SimpleAddParallel job2 = new SimpleAddParallel() {a = 2, input = input, result = jobResult2};
            SimpleAddParallel job3 = new SimpleAddParallel() {a = 3, input = input, result = jobResult3};

            JobHandle handle1 = job1.Schedule();
            JobHandle handle2 = job2.Schedule();
            JobHandle handle3 = job3.Schedule();

            JobHandle[] arr = {handle1, handle2, handle3};
            NativeArray<JobHandle> group = CollectionHelper.CreateNativeArray<JobHandle, RewindableAllocator>(arr, ref World.UpdateAllocator);
            JobHandle handle = JobHandle.CombineDependencies(group);

            handle.Complete();

            for (int i = 0; i < SimpleAddParallel.N; ++i)
            {
                Assert.AreEqual(i + 1, jobResult1[i]);
                Assert.AreEqual(i + 2, jobResult2[i]);
                Assert.AreEqual(i + 3, jobResult3[i]);
            }
        }

        public struct SimpleListAdd : IJob
        {
            public const int N = 1000;

            public int a;

            [ReadOnly] public NativeList<int> input;
            [WriteOnly] public NativeList<int> result;

            public void Execute()
            {
                for (int i = 0; i < N; ++i)
                    result.Add(a + input[i]);
            }
        }

        [Test]
        public void Schedule3SimpleListJobsInParallel()
        {
            NativeList<int> input = new NativeList<int>(World.UpdateAllocator.ToAllocator);
            NativeList<int> jobResult1 = new NativeList<int>(World.UpdateAllocator.ToAllocator);
            NativeList<int> jobResult2 = new NativeList<int>(World.UpdateAllocator.ToAllocator);
            NativeList<int> jobResult3 = new NativeList<int>(World.UpdateAllocator.ToAllocator);

            for (int i = 0; i < SimpleListAdd.N; ++i)
            {
                input.Add(i);
            }

            SimpleListAdd job1 = new SimpleListAdd() {a = 11, input = input, result = jobResult1};
            SimpleListAdd job2 = new SimpleListAdd() {a = 22, input = input, result = jobResult2};
            SimpleListAdd job3 = new SimpleListAdd() {a = 33, input = input, result = jobResult3};

            JobHandle handle1 = job1.Schedule();
            JobHandle handle2 = job2.Schedule();
            JobHandle handle3 = job3.Schedule();

            JobHandle[] arr = {handle1, handle2, handle3};
            NativeArray<JobHandle> group = CollectionHelper.CreateNativeArray<JobHandle, RewindableAllocator>(arr, ref World.UpdateAllocator);

            JobHandle handle = JobHandle.CombineDependencies(group);

            handle.Complete();

            for (int i = 0; i < SimpleListAdd.N; ++i)
            {
                Assert.AreEqual(i + 11, jobResult1[i]);
                Assert.AreEqual(i + 22, jobResult2[i]);
                Assert.AreEqual(i + 33, jobResult3[i]);
            }
        }

        public struct SimpleParallelFor : IJobParallelFor
        {
            public const int N = 1000;

            [ReadOnly]
            public NativeArray<int> a;

            [ReadOnly]
            public NativeArray<int> b;

            [WriteOnly]
            public NativeArray<int> result;

            public void Execute(int i)
            {
                result[i] = a[i] + b[i];
            }
        }

        [Test]
        // The parameter variants are intended to check "a little more and less" than typical thread counts, to
        // confirm work ranges are assigned - at least correctly enough - so that each index is called once.
        public void ScheduleSimpleParallelFor([Values(1, 3, 4, 5, 7, 8, 9, 11, 12, 13, 15, 16, 17, 1000)] int arrayLen)
        {
            NativeArray<int> a = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(arrayLen, ref World.UpdateAllocator);
            NativeArray<int> b = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(arrayLen, ref World.UpdateAllocator);
            NativeArray<int> result = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(arrayLen, ref World.UpdateAllocator);

            for (int i = 0; i < arrayLen; ++i)
            {
                a[i] = 100 + i;
                b[i] = 200 + i;
            }

            SimpleParallelFor job = new SimpleParallelFor() {a = a, b = b, result = result};
            job.a = a;
            job.b = b;
            job.result = result;

            JobHandle handle = job.Schedule(result.Length, 100);
            handle.Complete();

            for (int i = 0; i < arrayLen; ++i)
            {
                Assert.AreEqual(300 + i * 2, result[i]);
            }
        }

        public struct HashWriter : IJobParallelFor
        {
            [WriteOnly]
            public NativeParallelHashMap<int, int>.ParallelWriter result;

            public void Execute(int i)
            {
                result.TryAdd(i, 17);
            }
        }

        [Test]
        public void ScheduleHashWriter()
        {
            NativeParallelHashMap<int, int> result = new NativeParallelHashMap<int, int>(100, World.UpdateAllocator.ToAllocator);

            HashWriter job = new HashWriter
            {
                result = result.AsParallelWriter()
            };
            JobHandle handle = job.Schedule(100, 10);
            handle.Complete();

            for (int i = 0; i < 100; ++i)
            {
                Assert.AreEqual(17, result[i]);
            }
        }

        [BurstCompile]
        public struct HashWriterParallelFor : IJobParallelFor
        {
            [WriteOnly]
            public NativeParallelHashMap<int, int>.ParallelWriter result;

            [WriteOnly]
            public NativeParallelHashMap<int, bool>.ParallelWriter threadMap;

            public void Execute(int i)
            {
                result.TryAdd(i, 17);
                threadMap.TryAdd(threadMap.ThreadIndex, true);
            }
        }

        [Test]
        public void RunHashWriterParallelFor()
        {
            const int MAPSIZE = 100;
#if UNITY_2022_2_14F1_OR_NEWER
            int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
            int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
            // Make sure that each iteration was called and the parallel write worked.
            NativeParallelHashMap<int, int> map = new NativeParallelHashMap<int, int>(MAPSIZE, World.UpdateAllocator.ToAllocator);
            // Tracks the threadIndex used for each job.
            NativeParallelHashMap<int, bool> threadMap = new NativeParallelHashMap<int, bool>(maxThreadCount, World.UpdateAllocator.ToAllocator);

            HashWriterParallelFor job = new HashWriterParallelFor()
            {
                result = map.AsParallelWriter(),
                threadMap = threadMap.AsParallelWriter()
            };

            job.Schedule(MAPSIZE, 5).Complete();

            for (int i = 0; i < MAPSIZE; ++i)
            {
                Assert.AreEqual(17, map[i]);
            }
        }

        [BurstCompile]
        public struct MultiHashWriterParallelFor : IJobParallelFor
        {
            [WriteOnly]
            public NativeParallelMultiHashMap<int, int>.ParallelWriter result;

            [WriteOnly]
            public NativeParallelHashMap<int, bool>.ParallelWriter threadMap;

            public void Execute(int i)
            {
                result.Add(i, 17);
                threadMap.TryAdd(threadMap.ThreadIndex, true);
            }
        }

        [Test]
        public void RunMultiHashWriterParallelFor()
        {
            const int MAPSIZE = 100;
#if UNITY_2022_2_14F1_OR_NEWER
            int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
            int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
            // Make sure that each iteration was called and the parallel write worked.
            NativeParallelHashMap<int, int> map = new NativeParallelHashMap<int, int>(MAPSIZE, World.UpdateAllocator.ToAllocator);
            // Tracks the threadIndex used for each job.
            NativeParallelHashMap<int, bool> threadMap = new NativeParallelHashMap<int, bool>(maxThreadCount, World.UpdateAllocator.ToAllocator);

            HashWriterParallelFor job = new HashWriterParallelFor()
            {
                result = map.AsParallelWriter(),
                threadMap = threadMap.AsParallelWriter()
            };

            job.Schedule(MAPSIZE, 5).Complete();

            for (int i = 0; i < MAPSIZE; ++i)
            {
                Assert.AreEqual(17, map[i]);
            }
        }

        public struct SimpleParallelForDefer : IJobParallelForDefer
        {
            public const int N = 1000;

            [ReadOnly] public NativeList<int> a;
            [ReadOnly] public NativeArray<int> b;

            [WriteOnly] public NativeArray<int> result;

            public void Execute(int i)
            {
                result[i] = a[i] + b[i];
            }
        }

        [Test]
        public void ScheduleSimpleParallelForDefer_1()
        {
            NativeList<int> a = new NativeList<int>(SimpleParallelForDefer.N, World.UpdateAllocator.ToAllocator);
            NativeArray<int> b = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(SimpleParallelForDefer.N, ref World.UpdateAllocator);
            NativeArray<int> result = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(SimpleParallelForDefer.N, ref World.UpdateAllocator);

            for (int i = 0; i < SimpleParallelForDefer.N; ++i)
            {
                a.Add(100 + i);
                b[i] = 200 + i;
            }

            SimpleParallelForDefer job = new SimpleParallelForDefer() {a = a, b = b, result = result};
            job.a = a;
            job.b = b;
            job.result = result;

            JobHandle handle = job.Schedule(a, 300);
            handle.Complete();

            for (int i = 0; i < SimpleParallelFor.N; ++i)
            {
                Assert.AreEqual(300 + i * 2, result[i]);
            }
        }

        [Test]
        public unsafe void ScheduleSimpleParallelForDefer_2()
        {
            NativeList<int> a = new NativeList<int>(SimpleParallelForDefer.N, World.UpdateAllocator.ToAllocator);
            NativeArray<int> b = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(SimpleParallelForDefer.N, ref World.UpdateAllocator);
            NativeArray<int> result = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(SimpleParallelForDefer.N, ref World.UpdateAllocator);

            for (int i = 0; i < SimpleParallelForDefer.N; ++i)
            {
                a.Add(100 + i);
                b[i] = 200 + i;
            }

            SimpleParallelForDefer job = new SimpleParallelForDefer() {a = a, b = b, result = result};
            job.a = a;
            job.b = b;
            job.result = result;

            var lengthValue = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(1, ref World.UpdateAllocator);
            lengthValue[0] = SimpleParallelForDefer.N;

            JobHandle handle = job.Schedule((int*)lengthValue.GetUnsafePtr(), 300);
            handle.Complete();

            for (int i = 0; i < SimpleParallelFor.N; ++i)
            {
                Assert.AreEqual(300 + i * 2, result[i]);
            }
        }

        public struct SimpleParallelForBatch : IJobParallelForBatch
        {
            public const int N = 1000;

            [ReadOnly] public NativeArray<int> a;
            [ReadOnly] public NativeArray<int> b;

            [WriteOnly] public NativeArray<int> result;

            public void Execute(int index, int count)
            {
                for (int i = 0; i < count; ++i, ++index)
                {
                    result[index] = a[index] + b[index];
                }
            }
        }

        public enum ParallelForBatchedScheduleTypes
        {
            Batched, // remove when we deprecate this API
            BatchedByRef, // remove when we deprecate this API
            Parallel,
            ParallelByRef,
            Single,
            SingleByRef,
            Run,
            RunByRef,
            RunBatched, // remove when we deprecate this API
            RunBatchedByRef, // remove when we deprecate this API
        }

        [Test]
        public void ScheduleSimpleParallelForBatch([Values] ParallelForBatchedScheduleTypes mode)
        {
            NativeArray<int> a = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(SimpleParallelForDefer.N, ref World.UpdateAllocator);
            NativeArray<int> b = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(SimpleParallelForDefer.N, ref World.UpdateAllocator);
            NativeArray<int> result = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(SimpleParallelForDefer.N, ref World.UpdateAllocator);

            for (int i = 0; i < SimpleParallelForBatch.N; ++i)
            {
                a[i] = 100 + i;
                b[i] = 200 + i;
            }

            SimpleParallelForBatch job = new SimpleParallelForBatch() {a = a, b = b, result = result};
            job.a = a;
            job.b = b;
            job.result = result;

            JobHandle handle = default;
            switch (mode)
            {
                case ParallelForBatchedScheduleTypes.Batched:
                    handle = job.ScheduleBatch(SimpleParallelForBatch.N, 20); break;
                case ParallelForBatchedScheduleTypes.BatchedByRef:
                    handle = job.ScheduleBatchByRef(SimpleParallelForBatch.N, 20); break;
                case ParallelForBatchedScheduleTypes.Parallel:
                    handle = job.ScheduleParallel(SimpleParallelForBatch.N, 20); break;
                case ParallelForBatchedScheduleTypes.ParallelByRef:
                    handle = job.ScheduleParallelByRef(SimpleParallelForBatch.N, 20); break;
                case ParallelForBatchedScheduleTypes.Single:
                    handle = job.Schedule(SimpleParallelForBatch.N, 20); break;
                case ParallelForBatchedScheduleTypes.SingleByRef:
                    handle = job.ScheduleByRef(SimpleParallelForBatch.N, 20); break;
                case ParallelForBatchedScheduleTypes.RunBatched:
                    IJobParallelForBatchExtensions.RunBatch(job, SimpleParallelForBatch.N); break;
                case ParallelForBatchedScheduleTypes.RunBatchedByRef:
                    IJobParallelForBatchExtensions.RunBatchByRef(ref job, SimpleParallelForBatch.N); break;
                case ParallelForBatchedScheduleTypes.Run:
                    job.Run(SimpleParallelForBatch.N, 20); break;
                case ParallelForBatchedScheduleTypes.RunByRef:
                    job.RunByRef(SimpleParallelForBatch.N, 20); break;
                default: throw new ArgumentException($"Invalid schedule mode for {nameof(IJobParallelForBatch)}");
            }
            
            handle.Complete();

            for (int i = 0; i < SimpleParallelFor.N; ++i)
            {
                Assert.AreEqual(300 + i * 2, result[i]);
            }
        }

        public struct HashWriterJob : IJob
        {
            public const int N = 1000;
            // Don't declare [WriteOnly]. Write only is "automatic" for the ParallelWriter
            public NativeParallelHashMap<int, int>.ParallelWriter result;

            public void Execute()
            {
                for (int i = 0; i < N; ++i)
                {
                    result.TryAdd(i, 47);
                }
            }
        }

        [Test]
        public void RunHashWriterJob()
        {
            NativeParallelHashMap<int, int> map = new NativeParallelHashMap<int, int>(HashWriterJob.N, World.UpdateAllocator.ToAllocator);

            HashWriterJob job = new HashWriterJob();
            job.result = map.AsParallelWriter();
            JobHandle handle = job.Schedule();
            handle.Complete();

            for (int i = 0; i < HashWriterJob.N; ++i)
            {
                Assert.AreEqual(map[i], 47);
            }
        }

        internal struct SimpleChunkJob : IJobChunk
        {
            public ComponentTypeHandle<EcsTestData> TestTypeHandle;

            [ReadOnly]
            public NativeList<int> listOfInt;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // This job is not written to support queries with enableable component types.
                Assert.IsFalse(useEnabledMask);

                NativeArray<EcsTestData> chunkData = chunk.GetNativeArray(ref TestTypeHandle);

                for (int i = 0; i < chunk.Count; ++i)
                {
                    chunkData[i] = new EcsTestData() {value = 100 + chunkData[i].value};
                }
            }
        }

        [Test]
        public void TestSimpleIJobChunk([Values(0, 1, 2)] int mode, [Values(1, 100)] int n)
        {
            NativeArray<Entity> eArr = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(n, ref World.UpdateAllocator);
            var arch = m_Manager.CreateArchetype(typeof(EcsTestData));

            m_Manager.CreateEntity(arch, eArr);

            for (int i = 0; i < n; ++i)
            {
                m_Manager.SetComponentData(eArr[i], new EcsTestData() {value = 10 + i});
            }

            NativeList<int> listOfInt = new NativeList<int>(1, World.UpdateAllocator.ToAllocator);

            EntityQuery query = EmptySystem.GetEntityQuery(typeof(EcsTestData));
            var job = new SimpleChunkJob
            {
                TestTypeHandle = m_Manager.GetComponentTypeHandle<EcsTestData>(false),
                listOfInt = listOfInt
            };
            switch (mode)
            {
                case 0:
                    job.Schedule(query, default).Complete();
                    break;
                case 1:
                    job.ScheduleParallel(query, default).Complete();
                    break;
                case 2:
                    job.Run(query);
                    break;
            }

            for (int i = 0; i < n; ++i)
            {
                EcsTestData data = m_Manager.GetComponentData<EcsTestData>(eArr[i]);
                Assert.AreEqual(10 + i + 100, data.value);
            }
        }

        public struct SimpleJobFor : IJobFor
        {
            [WriteOnly]
            public NativeParallelHashMap<int, int>.ParallelWriter result;

            public void Execute(int i)
            {
                result.TryAdd(i, 123);
            }
        }

        [Test]
        public void TestIJobFor([Values(0, 1, 2)] int mode)
        {
            const int N = 1000;

            NativeParallelHashMap<int, int> output = new NativeParallelHashMap<int, int>(N, World.UpdateAllocator.ToAllocator);
            SimpleJobFor job = new SimpleJobFor()
            {
                result = output.AsParallelWriter()
            };

            if (mode == 0)
            {
                job.Run(N);
            }
            else if (mode == 1)
            {
                job.Schedule(N, new JobHandle()).Complete();
            }
            else
            {
                job.ScheduleParallel(N, 13, new JobHandle()).Complete();
            }

            Assert.AreEqual(N, output.Count());
            for (int i = 0; i < N; ++i)
            {
                Assert.AreEqual(123, output[i]);
            }
        }

    }
}
