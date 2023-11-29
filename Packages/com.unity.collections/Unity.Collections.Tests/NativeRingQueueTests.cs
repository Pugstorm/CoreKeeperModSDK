using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.Tests;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.TestTools;

internal class NativeRingQueueTests
{

    [Test, DotsRuntimeIgnore]
    public void NativeRingQueue_UseAfterFree_UsesCustomOwnerTypeName()
    {
        var test = new NativeRingQueue<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        test.Dispose();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        NUnit.Framework.Assert.That(() => test.Dequeue(),
            Throws.Exception.TypeOf<ObjectDisposedException>()
                .With.Message.Contains($"The {test.GetType()} has been deallocated"));
#endif
    }

    [Test, DotsRuntimeIgnore]
    public void NativeRingQueue_AtomicSafetyHandle_AllocatorTemp_UniqueStaticSafetyIds()
    {
        var test = new NativeRingQueue<int>(1, Allocator.Temp, NativeArrayOptions.ClearMemory);

        // All collections that use Allocator.Temp share the same core AtomicSafetyHandle.
        // This test verifies that containers can proceed to assign unique static safety IDs to each
        // AtomicSafetyHandle value, which will not be shared by other containers using Allocator.Temp.
        var test0 = new NativeRingQueue<int>(1, Allocator.Temp, NativeArrayOptions.ClearMemory);
        var test1 = new NativeRingQueue<int>(1, Allocator.Temp, NativeArrayOptions.ClearMemory);
        test0.Enqueue(123);
        test0.Dispose();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        NUnit.Framework.Assert.That(() => test0.Dequeue(),
            Throws.Exception.With.TypeOf<ObjectDisposedException>()
                .With.Message.Contains($"The {test0.GetType()} has been deallocated"));
#endif

        test.Enqueue(123);
        test1.Dispose();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        NUnit.Framework.Assert.That(() => test1.Dequeue(),
            Throws.Exception.With.TypeOf<ObjectDisposedException>()
                .With.Message.Contains($"The {test1.GetType()} has been deallocated"));
#endif
    }

    [BurstCompile(CompileSynchronously = true)]
    struct NativeRingQueueCreateAndUseAfterFreeBurst : IJob
    {
        public void Execute()
        {
            var test = new NativeRingQueue<int>(1, Allocator.Temp, NativeArrayOptions.ClearMemory);
            test.Enqueue(123);
            test.Dispose();

            test.Enqueue(456);
        }
    }

    [Test, DotsRuntimeIgnore]
    [TestRequiresCollectionChecks]
    public void NativeRingQueue_CreateAndUseAfterFreeInBurstJob_UsesCustomOwnerTypeName()
    {
        var test = new NativeRingQueue<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        test.Dispose();

        var job = new NativeRingQueueCreateAndUseAfterFreeBurst
        {
        };

        // Two things:
        // 1. This exception is logged, not thrown; thus, we use LogAssert to detect it.
        // 2. Calling write operation after container.Dispose() emits an unintuitive error message. For now, all this test cares about is whether it contains the
        //    expected type name.
        job.Run();
        LogAssert.Expect(LogType.Exception,
            new Regex($"InvalidOperationException: The {Regex.Escape(test.GetType().ToString())} has been declared as \\[ReadOnly\\] in the job, but you are writing to it"));
    }

    struct NativeRingQueueUseInJob : IJob
    {
        public NativeRingQueue<int> Test;

        public void Execute()
        {
            Test.Enqueue(456);
            Test.Enqueue(789);
        }
    }

    [Test]
    public void NativeRingQueue_UseInJob()
    {
        var container = new NativeRingQueue<int>(10, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        Assert.AreEqual(0, container.Length);

        var job = new NativeRingQueueUseInJob
        {
            Test = container,
        };

        Assert.DoesNotThrow(() => container.Enqueue(123));
        Assert.AreEqual(1, container.Length);

        var handle = job.Schedule();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        Assert.Throws<InvalidOperationException>(() => container.Enqueue(321));
        Assert.Throws<InvalidOperationException>(() => container.TryDequeue(out _));
#endif

        handle.Complete();
        Assert.AreEqual(3, container.Length);

        Assert.DoesNotThrow(() => container.Enqueue(987));
        Assert.AreEqual(4, container.Length);

        int item;
        Assert.True(container.TryDequeue(out item));
        Assert.AreEqual(123, item);
        Assert.AreEqual(3, container.Length);

        Assert.AreEqual(456, container.Dequeue());
        Assert.AreEqual(2, container.Length);

        Assert.AreEqual(789, container.Dequeue());
        Assert.AreEqual(1, container.Length);

        Assert.AreEqual(987, container.Dequeue());
        Assert.AreEqual(0, container.Length);
    }
}
