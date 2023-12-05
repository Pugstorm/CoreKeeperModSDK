using System;
using NUnit.Framework;
using UnityEngine.Scripting;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Burst;
using System.Diagnostics;

namespace Unity.Jobs.Tests.ManagedJobs
{
    internal class BurstScheduleTests : JobTestsFixtureBasic
    {
        [BurstDiscard]
        static public void TestBurstCompiled(ref bool falseIfNot)
        {
            falseIfNot = false;
        }

        [BurstCompile(CompileSynchronously = true)]
        static public bool IsBurstEnabled()
        {
            bool burstCompiled = true;
            TestBurstCompiled(ref burstCompiled);
            return burstCompiled;
        }

        [BurstCompile(CompileSynchronously = true)]
        struct SimpleIJobParallelForDefer : IJobParallelForDefer
        {
            public NativeArray<int> executed;

            public void Execute(int index)
            {
                executed[0] = 1;
            }

            [BurstCompile(CompileSynchronously = true)]
            public static int TestBurstScheduleJob(JobRunType runType, ref RewindableAllocator allocator)
            {
                bool burstCompiled = true;
                TestBurstCompiled(ref burstCompiled);

                var dummyList = new NativeList<int>(Allocator.Temp);
                dummyList.Add(5);

                var job = new SimpleIJobParallelForDefer() { executed = new NativeArray<int>(1, allocator.ToAllocator) };
                switch (runType)
                {
                    case JobRunType.Schedule: job.Schedule(dummyList, 1).Complete(); break;
                    case JobRunType.ScheduleByRef: job.ScheduleByRef(dummyList, 1).Complete(); break;
                }

                dummyList.Dispose();

                int ret = (burstCompiled ? 2 : 0) + job.executed[0];

                job.executed.Dispose();
                return ret;
            }
        }

        [TestCase(JobRunType.Schedule)]
        [TestCase(JobRunType.ScheduleByRef)]
        public unsafe void IJobParallelForDefer_Jobs_FromBurst(JobRunType runType)
        {
            if (!IsBurstEnabled())
                return;

            int ret = SimpleIJobParallelForDefer.TestBurstScheduleJob(runType, ref RwdAllocator);

            Assert.IsTrue((ret & 2) != 0, "Job schedule site not burst compiled");
            Assert.IsTrue((ret & 1) != 0, "Job with burst compiled schedule site didn't execute");
        }

        [BurstCompile(CompileSynchronously = true)]
        struct SimpleIJobParallelForBatch : IJobParallelForBatch
        {
            public NativeArray<int> executed;

            public void Execute(int startIndex, int count)
            {
                executed[0] = 1;
            }

            [BurstCompile(CompileSynchronously = true)]
            public static int TestBurstScheduleJob(JobRunType runType, ref RewindableAllocator allocator)
            {
                bool burstCompiled = true;
                TestBurstCompiled(ref burstCompiled);

                var job = new SimpleIJobParallelForBatch() { executed = new NativeArray<int>(1, allocator.ToAllocator) };
                switch (runType)
                {
                    case JobRunType.Schedule: job.ScheduleBatch(1, 1).Complete(); break;
                    case JobRunType.ScheduleByRef: job.ScheduleBatchByRef(1, 1).Complete(); break;
                    case JobRunType.Run: job.RunBatch(1); break;
                    case JobRunType.RunByRef: job.RunBatchByRef(1); break;
                }

                int ret = (burstCompiled ? 2 : 0) + job.executed[0];

                job.executed.Dispose();
                return ret;
            }
        }

        [TestCase(JobRunType.Schedule)]
        [TestCase(JobRunType.ScheduleByRef)]
        [TestCase(JobRunType.Run)]
        [TestCase(JobRunType.RunByRef)]
        public unsafe void IJobParallelForBatch_Jobs_FromBurst(JobRunType runType)
        {
            if (!IsBurstEnabled())
                return;

            int ret = SimpleIJobParallelForBatch.TestBurstScheduleJob(runType, ref RwdAllocator);

            Assert.IsTrue((ret & 2) != 0, "Job schedule site not burst compiled");
            Assert.IsTrue((ret & 1) != 0, "Job with burst compiled schedule site didn't execute");
        }

        [BurstCompile(CompileSynchronously = true)]
        struct SimpleIJobFilter : IJobFilter
        {
            public NativeArray<int> executed;

            public bool Execute(int index)
            {
                executed[0] = 1;
                return false;
            }

            [BurstCompile(CompileSynchronously = true)]
            public static int TestBurstScheduleJob(JobRunType runType, ref RewindableAllocator allocator)
            {
                bool burstCompiled = true;
                TestBurstCompiled(ref burstCompiled);

                var dummyList = new NativeList<int>(Allocator.Temp);
                dummyList.Add(5);

                var job = new SimpleIJobFilter() { executed = new NativeArray<int>(1, allocator.ToAllocator) };
                switch (runType)
                {
                    case JobRunType.Schedule:
                        job.ScheduleFilter(dummyList).Complete();
                        job.ScheduleAppend(dummyList, 1).Complete();
                        break;
                    case JobRunType.ScheduleByRef:
                        job.ScheduleFilterByRef(dummyList).Complete();
                        job.ScheduleAppendByRef(dummyList, 1).Complete();
                        break;
                    case JobRunType.Run:
                        job.RunFilter(dummyList);
                        job.RunAppend(dummyList, 1);
                        break;
                    case JobRunType.RunByRef:
                        job.RunFilterByRef(dummyList);
                        job.RunAppendByRef(dummyList, 1);
                        break;
                }

                dummyList.Dispose();

                int ret = (burstCompiled ? 2 : 0) + job.executed[0];

                job.executed.Dispose();
                return ret;
            }
        }

        [TestCase(JobRunType.Schedule)]
        [TestCase(JobRunType.ScheduleByRef)]
        [TestCase(JobRunType.Run)]
        [TestCase(JobRunType.RunByRef)]
        public unsafe void IJobFilter_Jobs_FromBurst(JobRunType runType)
        {
            if (!IsBurstEnabled())
                return;

            int ret = SimpleIJobFilter.TestBurstScheduleJob(runType, ref RwdAllocator);

            Assert.IsTrue((ret & 2) != 0, "Job schedule site not burst compiled");
            Assert.IsTrue((ret & 1) != 0, "Job with burst compiled schedule site didn't execute");
        }
    }
}
