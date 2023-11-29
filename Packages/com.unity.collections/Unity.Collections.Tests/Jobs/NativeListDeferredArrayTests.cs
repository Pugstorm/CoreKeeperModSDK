using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Jobs.Tests.ManagedJobs;

internal class NativeListDeferredArrayTests : JobTestsFixtureBasic
{
    private bool JobsDebuggerWasEnabled;
    struct AliasJob : IJob
    {
        public NativeArray<int> array;
        public NativeList<int> list;

        public void Execute()
        {
        }
    }

    struct SetListLengthJob : IJob
    {
        public int ResizeLength;
        public NativeList<int> list;

        public void Execute()
        {
            list.Resize(ResizeLength, NativeArrayOptions.UninitializedMemory);
        }
    }

    struct SetArrayValuesJobParallel : IJobParallelForDefer
    {
        public NativeArray<int> array;

        public void Execute(int index)
        {
            array[index] = array.Length;
        }
    }

    struct GetArrayValuesJobParallel : IJobParallelForDefer
    {
        [ReadOnly]
        public NativeArray<int> array;

        public void Execute(int index)
        {
        }
    }


    struct ParallelForWithoutList : IJobParallelForDefer
    {
        public void Execute(int index)
        {
        }
    }

    [SetUp]
    public void NativeListDeferredArrayTestsSetup()
    {
        // Many ECS tests will only pass if the Jobs Debugger enabled;
        // force it enabled for all tests, and restore the original value at teardown.
        JobsDebuggerWasEnabled = JobsUtility.JobDebuggerEnabled;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        JobsUtility.JobDebuggerEnabled = true;
#endif

#if UNITY_DOTSRUNTIME
        Unity.Runtime.TempMemoryScope.EnterScope();
#endif
    }

    [Test]
    public void ResizedListToDeferredJobArray([Values(0, 1, 2, 3, 4, 5, 6, 42, 97, 1023)] int length)
    {
        var list = new NativeList<int>(RwdAllocator.ToAllocator);

        var setLengthJob = new SetListLengthJob { list = list, ResizeLength = length };
        var jobHandle = setLengthJob.Schedule();

        var setValuesJob = new SetArrayValuesJobParallel { array = list.AsDeferredJobArray() };
        setValuesJob.Schedule(list, 3, jobHandle).Complete();

        Assert.AreEqual(length, list.Length);
        for (int i = 0; i != list.Length; i++)
            Assert.AreEqual(length, list[i]);
    }

    [Test]
    public unsafe void DeferredParallelForFromIntPtr()
    {
        int length = 10;

        var lengthValue = CollectionHelper.CreateNativeArray<int>(1, RwdAllocator.ToAllocator);
        lengthValue[0] = length;
        var array = CollectionHelper.CreateNativeArray<int>(length, RwdAllocator.ToAllocator);

        var setValuesJob = new SetArrayValuesJobParallel { array = array };
        setValuesJob.Schedule((int*)lengthValue.GetUnsafePtr(), 3).Complete();

        for (int i = 0; i != array.Length; i++)
            Assert.AreEqual(length, array[i]);
    }

    [Test]
    public void ResizeListBeforeSchedule([Values(5)] int length)
    {
        var list = new NativeList<int>(RwdAllocator.ToAllocator);

        var setLengthJob = new SetListLengthJob { list = list, ResizeLength = length }.Schedule();
        var setValuesJob = new SetArrayValuesJobParallel { array = list.AsDeferredJobArray() };
        setLengthJob.Complete();

        setValuesJob.Schedule(list, 3).Complete();

        Assert.AreEqual(length, list.Length);
        for (int i = 0; i != list.Length; i++)
            Assert.AreEqual(length, list[i]);
    }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    [Test]
    public void ResizedListToDeferredJobArray()
    {
        var list = new NativeList<int>(RwdAllocator.ToAllocator);
        list.Add(1);

        var array = list.AsDeferredJobArray();
#pragma warning disable 0219 // assigned but its value is never used
        Assert.Throws<IndexOutOfRangeException>(() => { var value = array[0]; });
#pragma warning restore 0219
        Assert.AreEqual(0, array.Length);
    }

    [Test]
    public void ResizeListWhileJobIsRunning()
    {
        var list = new NativeList<int>(RwdAllocator.ToAllocator);
        list.Resize(42, NativeArrayOptions.UninitializedMemory);

        var setValuesJob = new GetArrayValuesJobParallel { array = list.AsDeferredJobArray() };
        var jobHandle = setValuesJob.Schedule(list, 3);

        Assert.Throws<InvalidOperationException>(() => list.Resize(1, NativeArrayOptions.UninitializedMemory));

        jobHandle.Complete();
    }

    [Test]
    public void AliasArrayThrows()
    {
        var list = new NativeList<int>(RwdAllocator.ToAllocator);

        var aliasJob = new AliasJob { list = list, array = list.AsDeferredJobArray() };
        Assert.Throws<InvalidOperationException>(() => aliasJob.Schedule());
    }

    [Test]
    public void DeferredListMustExistInJobData()
    {
        var list = new NativeList<int>(RwdAllocator.ToAllocator);

        var job = new ParallelForWithoutList();
        Assert.Throws<InvalidOperationException>(() => job.Schedule(list, 64));
    }

    [Test]
    public void DeferredListCantBeDeletedWhileJobIsRunning()
    {
        var list = new NativeList<int>(RwdAllocator.ToAllocator);
        list.Resize(42, NativeArrayOptions.UninitializedMemory);

        var setValuesJob = new GetArrayValuesJobParallel { array = list.AsDeferredJobArray() };
        var jobHandle = setValuesJob.Schedule(list, 3);

        Assert.Throws<InvalidOperationException>(() => list.Dispose());

        jobHandle.Complete();
    }

    [Test]
    public void DeferredArrayCantBeAccessedOnMainthread()
    {
        var list = new NativeList<int>(RwdAllocator.ToAllocator);
        list.Add(1);

        var defer = list.AsDeferredJobArray();

        Assert.AreEqual(0, defer.Length);
        Assert.Throws<IndexOutOfRangeException>(() => defer[0] = 5);
    }
#endif

    [TearDown]
    public void TearDown()
    {
#if UNITY_DOTSRUNTIME
        Unity.Runtime.TempMemoryScope.ExitScope();
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        JobsUtility.JobDebuggerEnabled = JobsDebuggerWasEnabled;
#endif
    }
}
