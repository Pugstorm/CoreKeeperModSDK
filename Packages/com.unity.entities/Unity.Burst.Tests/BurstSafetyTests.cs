using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;
using UnityEngine.TestTools;
using System.Diagnostics;
using Unity.Jobs.LowLevel.Unsafe;

public class BurstSafetyTests
{
        static string SafetyChecksMenu = "Jobs > Burst > Safety Checks";
        [SetUp]
        public virtual void Setup()
        {
            Assert.IsTrue(BurstCompiler.Options.EnableBurstSafetyChecks, $"Burst safety tests must have Burst safety checks enabled! To enable, go to {SafetyChecksMenu}");
        }

    [BurstCompile(CompileSynchronously = true)]
    struct ThrowExceptionJob : IJobParallelFor
    {
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void Throw()
        {
            throw new System.ArgumentException("Blah");
        }

        public void Execute(int index)
        {
            Throw();
        }
    }

    [Test]
    public void ThrowExceptionParallelForStress()
    {
        var messageCount = 0;

        void OnMessage(string message, string stackTrace, LogType type)
        {
            Assert.AreEqual(LogType.Exception, type);
            StringAssert.Contains("ArgumentException: Blah", message);
            messageCount++;
        }

        LogAssert.ignoreFailingMessages = true;
        Application.logMessageReceivedThreaded += OnMessage;

        var jobData = new ThrowExceptionJob();
        try
        {
            jobData.Schedule(100, 1).Complete();

            Assert.GreaterOrEqual(messageCount, 1);
        }
        finally
        {
            Application.logMessageReceivedThreaded -= OnMessage;
            LogAssert.ignoreFailingMessages = false;
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct WriteToReadOnlyArrayJob : IJob
    {
        [ReadOnly]
        public NativeArray<int> test;
        public void Execute()
        {
            test[0] = 5;
        }
    }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    [Test]
    public void WriteToReadOnlyArray()
    {
        LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException"));

        var jobData = new WriteToReadOnlyArrayJob();
        jobData.test = new NativeArray<int>(1, Allocator.Persistent);

        jobData.Run();

        jobData.test.Dispose();
    }
#endif

    [BurstCompile(CompileSynchronously = true)]
    struct ParallelForIndexChecksJob : IJobParallelFor
    {
        public NativeArray<int> test;

        public void Execute(int index)
        {
            test[0] = 5;
        }
    }

    [Test]
    public void ParallelForMinMaxChecks()
    {
        var messageCount = 0;

        void OnMessage(string message, string stackTrace, LogType type)
        {
            Assert.AreEqual(LogType.Exception, type);
            StringAssert.Contains("IndexOutOfRangeException", message);
            messageCount++;
        }

        LogAssert.ignoreFailingMessages = true;
        Application.logMessageReceivedThreaded += OnMessage;

        var jobData = new ParallelForIndexChecksJob();
        jobData.test = new NativeArray<int>(2, Allocator.Persistent);

        try
        {
            jobData.Schedule(100, 1).Complete();

            Assert.GreaterOrEqual(messageCount, 1);
        }
        finally
        {
            Application.logMessageReceivedThreaded -= OnMessage;
            LogAssert.ignoreFailingMessages = false;

            jobData.test.Dispose();
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct AccessNullNativeArrayJob : IJobParallelFor
    {
        public void Execute(int index)
        {
            var array = new NativeArray<float>();
            array[0] = 5;
        }
    }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    [Test]
    [Ignore("Crashes Unity - Important")]
    public void AccessNullNativeArray()
    {
        LogAssert.Expect(LogType.Exception, new Regex("NullReferenceException"));

        new AccessNullNativeArrayJob().Schedule(100, 1).Complete();
    }
#endif

    [BurstCompile(CompileSynchronously = true)]
    unsafe struct AccessNullUnsafePtrJob : IJob
    {
#pragma warning disable 649
        [NativeDisableUnsafePtrRestriction] float* myArray;
#pragma warning restore 649

        public void Execute()
        {
            myArray[0] = 5;
        }
    }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    [Test]
    [Ignore("Crashes Unity - No user is supposed to write code like this, so not very important")]
    public void AccessNullUnsafePtr()
    {
        LogAssert.Expect(LogType.Exception, new Regex("NullReferenceException"));

        new AccessNullUnsafePtrJob().Run();
    }
#endif
}
