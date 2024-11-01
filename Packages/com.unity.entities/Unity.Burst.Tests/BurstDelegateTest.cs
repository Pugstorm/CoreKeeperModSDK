using System;
using System.ComponentModel;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Entities;
using UnityEngine.TestTools;

[BurstCompile]
public unsafe class BurstDelegateTest
{
    delegate void DoThingDelegate(ref int value);

    [BurstCompile]
    [AOT.MonoPInvokeCallback(typeof(DoThingDelegate))]
    static void DoThing(ref int value)
    {
        value++;
    }

    static void DoThingMissingBurstCompile(ref int value)
    {
        value++;
    }

    [Test]
    public void ManagedDelegateTest()
    {
        var funcPtr = BurstCompiler.CompileFunctionPointer<DoThingDelegate>(DoThing);

        // NOTE: funcPtr.Invoke allocates GC memory,
        // so in real world use cases we want to cache the managed delegate, not the FunctionPointer
        DoThingDelegate cachableDelegate = funcPtr.Invoke;

        int value = 5;
        cachableDelegate(ref value);
        Assert.AreEqual(6, value);
    }

    [Test]
    public void JobFunctionPointerTest()
    {
        var funcPtr = BurstCompiler.CompileFunctionPointer<DoThingDelegate>(DoThing);

        var job = new MyJob();
        int value = 5;
        job.Blah = &value;
        job.FunctionPointer = funcPtr;

        job.Schedule().Complete();

        Assert.AreEqual(6, value);
    }

    [BurstCompile]
    struct MyJob : IJob
    {
        [NativeDisableUnsafePtrRestriction]
        public int* Blah;
        public FunctionPointer<DoThingDelegate> FunctionPointer;

        unsafe public void Execute()
        {
            FunctionPointer.Invoke(ref *Blah);
        }
    }

    [Test]
#if !UNITY_WEBGL
    [ConditionalIgnore("IgnoreForCoverage", "Fails randonly when ran with code coverage enabled")]
#endif
    public void CompileMissingBurstCompile()
    {
        Assert.Throws<InvalidOperationException>(() => BurstCompiler.CompileFunctionPointer<DoThingDelegate>(DoThingMissingBurstCompile));
    }

    [BurstCompile]
    private struct DivideByZeroJob : IJob
    {
        [NativeDisableUnsafePtrRestriction]
        public int I;

        public void Execute()
        {
            I = 42 / I;

            // This is never hit because the above throws an exception.
            I = 13;
        }
    }

    private delegate void CallJobDelegate(ref DivideByZeroJob job);

    [BurstCompile(CompileSynchronously = true)]
    private static void CallJob(ref DivideByZeroJob job)
	{
        job.Run();

        // Even though job.Run() throws an exception in its body, the job system catches
        // that exception and handles it. So this statement is hit.
        job.I++;
	}

    [Test, Ignore("DOTS-2992")]
    public void CallJobFromFunctionPointer()
    {
        var funcPtr = BurstCompiler.CompileFunctionPointer<CallJobDelegate>(CallJob);
        var job = new DivideByZeroJob { I = 0 };
        funcPtr.Invoke(ref job);
        Assert.AreEqual(job.I, 1);
    }
}
