using NUnit.Framework;
using System;
using Unity.Jobs;
using Unity.Collections;
using Unity.Jobs.Tests.ManagedJobs;
#pragma warning disable 0219

internal class ParallelFilterJobTests : JobTestsFixtureBasic
{
    struct NativeListAddMod7Job : IJob
    {
        NativeList<int>     list;
        int produceCount;
        public NativeListAddMod7Job(NativeList<int> list, int produceCount)
        {
            this.list = list;
            this.produceCount = produceCount;
        }

        public void Execute()
        {
            for (int index = 0; index != produceCount; index++)
            {
                if (index % 7 == 0)
                    list.Add(index);
            }
        }
    }

    struct FilterMod7Job : IJobFilter
    {
        public bool Execute(int index)
        {
            return index % 7 == 0;
        }
    }

    struct FilterAllJob : IJobFilter
    {
        public bool Execute(int index)
        {
            return true;
        }
    }

    [Test]
    public void AddElementForEach([Values] bool userFilterJob)
    {
        var list = new NativeList<int>(0, RwdAllocator.ToAllocator);
        list.Add(-1);
        list.Add(-2);

        if (userFilterJob)
        {
            var job = new FilterMod7Job();
            job.ScheduleAppend(list, 1000).Complete();
        }
        else
        {
            var job = new NativeListAddMod7Job(list, 1000);
            job.Schedule().Complete();
        }

        int counter = 2;
        for (int i = 0; i != 1000; i++)
        {
            if (i % 7 == 0)
            {
                Assert.AreEqual(i, list[counter]);
                counter++;
            }
        }

        Assert.AreEqual(-1, list[0]);
        Assert.AreEqual(-2, list[1]);

        Assert.AreEqual(counter, list.Length);
    }

    [Test]
    public void FilterProduceChained()
    {
        var list = new NativeList<int>(3, RwdAllocator.ToAllocator);
        var jobHandle = new FilterMod7Job().ScheduleAppend(list, 14);
        jobHandle = new FilterAllJob().ScheduleAppend(list, 2, jobHandle);

        jobHandle.Complete();

        Assert.AreEqual(0, list[0]);
        Assert.AreEqual(7, list[1]);
        Assert.AreEqual(0, list[2]);
        Assert.AreEqual(1, list[3]);
        Assert.AreEqual(4, list.Length);
    }

    [Test]
    public void FilterAppendChained()
    {
        var list = new NativeList<int>(3, RwdAllocator.ToAllocator);
        var jobHandle = new FilterMod7Job().ScheduleAppend(list, 14);
        jobHandle = new FilterAllJob().ScheduleAppend(list, 2, jobHandle);

        jobHandle.Complete();

        Assert.AreEqual(0, list[0]);
        Assert.AreEqual(7, list[1]);
        Assert.AreEqual(0, list[2]);
        Assert.AreEqual(1, list[3]);
        Assert.AreEqual(4, list.Length);
    }

    [Test]
    public void FilterPreviousChained()
    {
        var list = new NativeList<int>(3, RwdAllocator.ToAllocator);
        var jobHandle = new FilterAllJob().ScheduleAppend(list, 14);
        jobHandle = new FilterMod7Job().ScheduleFilter(list, jobHandle);

        jobHandle.Complete();

        Assert.AreEqual(2, list.Length);
        Assert.AreEqual(0, list[0]);
        Assert.AreEqual(7, list[1]);
    }

    struct MinMaxRestrictionJob : IJobFilter
    {
        public NativeArray<float> array;
        public MinMaxRestrictionJob(NativeArray<float> array) { this.array = array; }
        public bool Execute(int index)
        {
            array[index] = 5;

            var localArray = array;
            Assert.Throws<IndexOutOfRangeException>(() => { localArray[50] = 5; });

            return true;
        }
    }

    [Test]
    [Ignore("DOTS-1959 Currently thats legal, but only because filter jobs aren't implemented as parallel for right now...")]
    public void AccessingWritable()
    {
        var list = new NativeList<int>(0, Allocator.Persistent);
        var array = new NativeArray<float>(51, Allocator.Persistent);

        var jobHandle = new MinMaxRestrictionJob(array).ScheduleAppend(list, 50);
        new MinMaxRestrictionJob(array).ScheduleFilter(list, jobHandle).Complete();

        Assert.AreEqual(50, list.Length);

        list.Dispose();
        array.Dispose();
    }
}
