using NUnit.Framework;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.Tests;
using Assert = FastAssert;
using System;

internal class NativeQueueTests_InJobs : CollectionsTestCommonBase
{
// DOTS-6203 Nested containers aren't detected in DOTS Runtime currently
#if !UNITY_DOTSRUNTIME
    struct NestedContainerJob : IJob
    {
        public NativeQueue<NativeQueue<int>> nestedContainer;

        public void Execute()
        {
            nestedContainer.Clear();
        }
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeQueue_NestedJob_Error()
    {
        var container = new NativeQueue<NativeQueue<int>>(CommonRwdAllocator.Handle);

        var nestedJob = new NestedContainerJob
        {
            nestedContainer = container
        };

        JobHandle job = default;
        Assert.Throws<System.InvalidOperationException>(() => { job = nestedJob.Schedule(); });
        job.Complete();

        container.Dispose();
    }
#endif

    [BurstCompile(CompileSynchronously = true)]
    struct ConcurrentEnqueue : IJobParallelFor
    {
        public NativeQueue<int>.ParallelWriter queue;

        [NativeDisableParallelForRestriction]
        public NativeArray<int> result;

        public int StartIndex;

        public void Execute(int index)
        {
            index += StartIndex;
            result[index] = 1;
            queue.Enqueue(index);
        }
    }

    [Test]
    public void Enqueue()
    {
        const int queueSize = 100 * 1024;
        var queue = new NativeQueue<int>(CommonRwdAllocator.Handle);
        var writeStatus = CollectionHelper.CreateNativeArray<int>(queueSize, CommonRwdAllocator.Handle);

        var enqueueJob = new ConcurrentEnqueue()
        {
            queue = queue.AsParallelWriter(),
            result = writeStatus,
            StartIndex = 0,
        };

        var enqueue = enqueueJob.Schedule(queueSize, 1);
        enqueue.Complete();

        Assert.AreEqual(queueSize, queue.Count, "Job enqueued the wrong number of values");
        var allValues = new NativeParallelHashSet<int>(queueSize, Allocator.Persistent);
        for (int i = 0; i < queueSize; ++i)
        {
            Assert.AreEqual(1, writeStatus[i], "Job failed to enqueue value");
            int enqueued = queue.Dequeue();
            Assert.IsTrue(enqueued >= 0 && enqueued < queueSize, "Job enqueued invalid value");
            Assert.IsTrue(allValues.Add(enqueued), "Job enqueued same value multiple times");
        }

        var disposeJob = queue.Dispose(enqueue);
        disposeJob.Complete();

        writeStatus.Dispose();
        allValues.Dispose();
    }

    [BurstCompile(CompileSynchronously = true)]
    struct EnqueueDequeueJob : IJob
    {
        public NativeQueue<int> queue;
        [ReadOnly] public NativeArray<int> arr;
        public int val;

        public void Execute()
        {
            for (int i = 0; i < 10000; ++i)
            {
                queue.Enqueue(0);
                val += arr[queue.Dequeue()];
            }
        }
    }

    [Test]
    public void EnqueueDequeueMultipleQueuesInMultipleJobs()
    {
        var handles = new NativeArray<JobHandle>(4, Allocator.Temp);
        for (int i = 0; i < 10; ++i)
        {
            var q1 = new NativeQueue<int>(CommonRwdAllocator.Handle);
            var q2 = new NativeQueue<int>(CommonRwdAllocator.Handle);
            var q3 = new NativeQueue<int>(CommonRwdAllocator.Handle);
            var q4 = new NativeQueue<int>(CommonRwdAllocator.Handle);
            var rangeCheck = CollectionHelper.CreateNativeArray<int>(1, CommonRwdAllocator.Handle);
            var j1 = new EnqueueDequeueJob {queue = q1, arr = rangeCheck, val = 0};
            var j2 = new EnqueueDequeueJob {queue = q2, arr = rangeCheck, val = 0};
            var j3 = new EnqueueDequeueJob {queue = q3, arr = rangeCheck, val = 0};
            var j4 = new EnqueueDequeueJob {queue = q4, arr = rangeCheck, val = 0};
            handles[0] = j1.Schedule();
            handles[1] = j2.Schedule();
            handles[2] = j3.Schedule();
            handles[3] = j4.Schedule();
            JobHandle.ScheduleBatchedJobs();

            JobHandle.CombineDependencies(handles).Complete();

            q1.Dispose();
            q2.Dispose();
            q3.Dispose();
            q4.Dispose();
            rangeCheck.Dispose();
        }
        handles.Dispose();
    }

    struct EnqueueJob : IJobParallelFor
    {
        public NativeQueue<int>.ParallelWriter Queue;
        public void Execute(int index)
        {
            Queue.Enqueue(index);
        }
    }

    [Test]
    public void ToArray_WorksFromJobs()
    {
        using (var queue = new NativeQueue<int>(CommonRwdAllocator.Handle))
        {
            new EnqueueJob
            {
                Queue = queue.AsParallelWriter()
            }.Schedule(100, 10).Complete();
            Assert.AreEqual(100, queue.Count);
            using (var arr = queue.ToArray(Allocator.Temp))
            {
                Assert.AreEqual(100, arr.Length);
                arr.Sort();
                for (int i = 0; i < arr.Length; i++)
                    Assert.AreEqual(i, arr[i]);
            }
        }
    }

    [Test]
    public void NativeQueue_ParallelWriter()
    {
        const int queueSize = 100 * 1024;
        var queue = new NativeQueue<int>(CommonRwdAllocator.Handle);
        var writeStatus = CollectionHelper.CreateNativeArray<int>(queueSize, CommonRwdAllocator.Handle);

        var jobHandle = new ConcurrentEnqueue()
        {
            queue = queue.AsParallelWriter(),
            result = writeStatus,
            StartIndex = 0,

        }.Schedule(queueSize / 2, 1);

        jobHandle = new ConcurrentEnqueue()
        {
            queue = queue.AsParallelWriter(),
            result = writeStatus,
            StartIndex = queueSize / 2,

        }.Schedule(queueSize / 2, 1, jobHandle);

        jobHandle.Complete();

        Assert.AreEqual(queueSize, queue.Count, "Job enqueued the wrong number of values");
        var allValues = new NativeParallelHashSet<int>(queueSize, Allocator.Persistent);
        for (int i = 0; i < queueSize; ++i)
        {
            Assert.AreEqual(1, writeStatus[i], "Job failed to enqueue value");
            int enqueued = queue.Dequeue();
            Assert.IsTrue(enqueued >= 0 && enqueued < queueSize, "Job enqueued invalid value");
            Assert.IsTrue(allValues.Add(enqueued), "Job enqueued same value multiple times");
        }

        var disposeJob = queue.Dispose(jobHandle);
        disposeJob.Complete();

        writeStatus.Dispose();
        allValues.Dispose();
    }
}
