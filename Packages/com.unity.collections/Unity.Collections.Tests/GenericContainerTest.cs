using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.Tests;
using Unity.Jobs;
using System;
using Unity.Jobs.Tests.ManagedJobs;

[assembly: RegisterGenericJobType(typeof(GenericContainerJob<NativeArray<int>>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<UnsafeAppendBuffer>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<NativeBitArray>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<UnsafeBitArray>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<NativeHashMap<int, int>>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<UnsafeHashMap<int, int>>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<NativeHashSet<int>>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<UnsafeHashSet<int>>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<NativeList<int>>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<UnsafeList<int>>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<UnsafePtrList<int>>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<NativeParallelHashMap<int, int>>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<UnsafeParallelHashMap<int, int>>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<NativeParallelHashSet<int>>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<UnsafeParallelHashSet<int>>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<NativeParallelMultiHashMap<int, int>>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<UnsafeParallelMultiHashMap<int, int>>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<NativeQueue<int>>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<UnsafeQueue<int>>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<NativeReference<int>>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<NativeRingQueue<int>>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<UnsafeRingQueue<int>>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<NativeStream>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<UnsafeStream>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<NativeText>))]
[assembly: RegisterGenericJobType(typeof(GenericContainerJob<UnsafeText>))]

internal struct GenericContainerJob<T> : IJob
{
    public T data;
    public void Execute()
    {
        // We just care about creating job dependencies
    }
}

internal struct GenericContainerReadonlyJob<T> : IJob
{
    [ReadOnly] public T data;
    public void Execute()
    {
        // We just care about creating job dependencies
    }
}

internal class GenericContainerTests : CollectionsTestFixture
{
    UnsafeAppendBuffer CreateEmpty_UnsafeAppendBuffer()
    {
        var container = new UnsafeAppendBuffer(0, 8, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        return container;
    }

    NativeBitArray CreateEmpty_NativeBitArray()
    {
        var container = new NativeBitArray(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        return container;
    }


    UnsafeBitArray CreateEmpty_UnsafeBitArray()
    {
        var container = new UnsafeBitArray(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        return container;
    }

    NativeHashMap<int, int> CreateEmpty_NativeHashMap()
    {
        var container = new NativeHashMap<int, int>(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        return container;
    }

    UnsafeHashMap<int, int> CreateEmpty_UnsafeHashMap()
    {
        var container = new UnsafeHashMap<int, int>(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        return container;
    }

    NativeHashSet<int> CreateEmpty_NativeHashSet()
    {
        var container = new NativeHashSet<int>(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        return container;
    }

    UnsafeHashSet<int> CreateEmpty_UnsafeHashSet()
    {
        var container = new UnsafeHashSet<int>(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        return container;
    }

    NativeList<int> CreateEmpty_NativeList()
    {
        var container = new NativeList<int>(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        return container;
    }

    UnsafeList<int> CreateEmpty_UnsafeList()
    {
        var container = new UnsafeList<int>(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        return container;
    }

    UnsafePtrList<int> CreateEmpty_UnsafePtrList()
    {
        var container = new UnsafePtrList<int>(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        return container;
    }

    NativeParallelHashMap<int, int> CreateEmpty_NativeParallelHashMap()
    {
        var container = new NativeParallelHashMap<int, int>(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        return container;
    }

    UnsafeParallelHashMap<int, int> CreateEmpty_UnsafeParallelHashMap()
    {
        var container = new UnsafeParallelHashMap<int, int>(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        return container;
    }

    NativeParallelHashSet<int> CreateEmpty_NativeParallelHashSet()
    {
        var container = new NativeParallelHashSet<int>(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        return container;
    }

    UnsafeParallelHashSet<int> CreateEmpty_UnsafeParallelHashSet()
    {
        var container = new UnsafeParallelHashSet<int>(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        return container;
    }

    NativeParallelMultiHashMap<int, int> CreateEmpty_NativeParallelMultiHashMap()
    {
        var container = new NativeParallelMultiHashMap<int, int>(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        return container;
    }

    UnsafeParallelMultiHashMap<int, int> CreateEmpty_UnsafeParallelMultiHashMap()
    {
        var container = new UnsafeParallelMultiHashMap<int, int>(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        return container;
    }

    NativeQueue<int> CreateEmpty_NativeQueue()
    {
        var container = new NativeQueue<int>(Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty());
        return container;
    }

    UnsafeQueue<int> CreateEmpty_UnsafeQueue()
    {
        var container = new UnsafeQueue<int>(Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty());
        return container;
    }

    NativeReference<int> CreateEmpty_NativeReference()
    {
        var container = new NativeReference<int>(Allocator.Persistent);
        Assert.True(container.IsCreated);
        return container;
    }

    NativeRingQueue<int> CreateEmpty_NativeRingQueue()
    {
        var container = new NativeRingQueue<int>(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        return container;
    }

    UnsafeRingQueue<int> CreateEmpty_UnsafeRingQueue()
    {
        var container = new UnsafeRingQueue<int>(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        return container;
    }

    NativeStream CreateEmpty_NativeStream()
    {
        var container = new NativeStream(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty());
        return container;
    }

    UnsafeStream CreateEmpty_UnsafeStream()
    {
        var container = new UnsafeStream(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty());
        return container;
    }

    NativeText CreateEmpty_NativeText()
    {
        var container = new NativeText(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        return container;
    }

    UnsafeText CreateEmpty_UnsafeText()
    {
        var container = new UnsafeText(0, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        return container;
    }

    //-------------------------------------------------------------------------------------------------------

    void Test_Dispose_Uninitialized<T>()
        where T : INativeDisposable
    {
        T uninitialized = default;
        Assert.DoesNotThrow(() => uninitialized.Dispose());
        Assert.DoesNotThrow(() => uninitialized.Dispose(default));
    }

    [Test]
    public void INativeDisposable_Dispose_Uninitialized()
    {
        Test_Dispose_Uninitialized<NativeBitArray>();
        Test_Dispose_Uninitialized<NativeHashMap<int, int>>();
        Test_Dispose_Uninitialized<NativeHashSet<int>>();
        Test_Dispose_Uninitialized<NativeList<int>>();
        Test_Dispose_Uninitialized<NativeParallelHashMap<int, int>>();
        Test_Dispose_Uninitialized<NativeParallelHashSet<int>>();
        Test_Dispose_Uninitialized<NativeParallelMultiHashMap<int, int>>();
        Test_Dispose_Uninitialized<NativeQueue<int>>();
        Test_Dispose_Uninitialized<NativeReference<int>>();
        Test_Dispose_Uninitialized<NativeRingQueue<int>>();
        Test_Dispose_Uninitialized<NativeStream>();
        Test_Dispose_Uninitialized<NativeText>();

        Test_Dispose_Uninitialized<UnsafeAppendBuffer>();
        Test_Dispose_Uninitialized<UnsafeBitArray>();
        Test_Dispose_Uninitialized<UnsafeHashMap<int, int>>();
        Test_Dispose_Uninitialized<UnsafeHashSet<int>>();
        Test_Dispose_Uninitialized<UnsafeList<int>>();
        Test_Dispose_Uninitialized<UnsafePtrList<int>>();
        Test_Dispose_Uninitialized<UnsafeParallelHashMap<int, int>>();
        Test_Dispose_Uninitialized<UnsafeParallelHashSet<int>>();
        Test_Dispose_Uninitialized<UnsafeParallelMultiHashMap<int, int>>();
        Test_Dispose_Uninitialized<UnsafeQueue<int>>();
        Test_Dispose_Uninitialized<UnsafeRingQueue<int>>();
        Test_Dispose_Uninitialized<UnsafeStream>();
        Test_Dispose_Uninitialized<UnsafeText>();
    }

    //-------------------------------------------------------------------------------------------------------

    void Test_Unsafe_Double_Dispose<T>(T container)
        where T : INativeDisposable
    {
        Assert.DoesNotThrow(() => container.Dispose());
        Assert.DoesNotThrow(() => container.Dispose());
    }

    void Test_Native_Double_Dispose<T>(T container)
        where T : INativeDisposable
    {
        Assert.DoesNotThrow(() => container.Dispose());
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        Assert.Throws<ObjectDisposedException>(() => container.Dispose());
#else
        Assert.DoesNotThrow(() => container.Dispose());
#endif
    }

    [Test]
    public void INativeDisposable_Init_Double_Dispose()
    {
        Test_Native_Double_Dispose(CreateEmpty_NativeBitArray());
        Test_Native_Double_Dispose(CreateEmpty_NativeHashMap());
        Test_Native_Double_Dispose(CreateEmpty_NativeHashSet());
        Test_Native_Double_Dispose(CreateEmpty_NativeList());
        Test_Native_Double_Dispose(CreateEmpty_NativeParallelHashMap());
        Test_Native_Double_Dispose(CreateEmpty_NativeParallelHashSet());
        Test_Native_Double_Dispose(CreateEmpty_NativeParallelMultiHashMap());
        Test_Native_Double_Dispose(CreateEmpty_NativeQueue());
        Test_Native_Double_Dispose(CreateEmpty_NativeReference());
        Test_Native_Double_Dispose(CreateEmpty_NativeRingQueue());
        Test_Native_Double_Dispose(CreateEmpty_NativeStream());
        Test_Native_Double_Dispose(CreateEmpty_NativeText());

        Test_Unsafe_Double_Dispose(CreateEmpty_UnsafeAppendBuffer());
        Test_Unsafe_Double_Dispose(CreateEmpty_UnsafeBitArray());
        Test_Unsafe_Double_Dispose(CreateEmpty_UnsafeHashMap());
        Test_Unsafe_Double_Dispose(CreateEmpty_UnsafeHashSet());
        Test_Unsafe_Double_Dispose(CreateEmpty_UnsafeList());
        Test_Unsafe_Double_Dispose(CreateEmpty_UnsafePtrList());
        Test_Unsafe_Double_Dispose(CreateEmpty_UnsafeParallelHashMap());
        Test_Unsafe_Double_Dispose(CreateEmpty_UnsafeParallelHashSet());
        Test_Unsafe_Double_Dispose(CreateEmpty_UnsafeParallelMultiHashMap());
        Test_Unsafe_Double_Dispose(CreateEmpty_UnsafeQueue());
        Test_Unsafe_Double_Dispose(CreateEmpty_UnsafeRingQueue());
        Test_Unsafe_Double_Dispose(CreateEmpty_UnsafeStream());
        Test_Unsafe_Double_Dispose(CreateEmpty_UnsafeText());
    }

    //-------------------------------------------------------------------------------------------------------

    void Test_Unsafe_Double_Dispose_Job<T>(T container)
        where T : INativeDisposable
    {
        Assert.DoesNotThrow(() => container.Dispose(default));
        Assert.DoesNotThrow(() => container.Dispose(default));
    }

    void Test_Native_Double_Dispose_Job<T>(T container)
        where T : INativeDisposable
    {
        Assert.DoesNotThrow(() => container.Dispose(default));
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        Assert.Throws<ObjectDisposedException>(() => container.Dispose(default));
#else
        Assert.DoesNotThrow(() => container.Dispose(default));
#endif
    }

    [Test]
    public void INativeDisposable_Init_Double_Dispose_Job()
    {
        Test_Native_Double_Dispose_Job(CreateEmpty_NativeBitArray());
        Test_Native_Double_Dispose_Job(CreateEmpty_NativeHashMap());
        Test_Native_Double_Dispose_Job(CreateEmpty_NativeHashSet());
        Test_Native_Double_Dispose_Job(CreateEmpty_NativeList());
        Test_Native_Double_Dispose_Job(CreateEmpty_NativeParallelHashMap());
        Test_Native_Double_Dispose_Job(CreateEmpty_NativeParallelHashSet());
        Test_Native_Double_Dispose_Job(CreateEmpty_NativeParallelMultiHashMap());
        Test_Native_Double_Dispose_Job(CreateEmpty_NativeQueue());
        Test_Native_Double_Dispose_Job(CreateEmpty_NativeReference());
        Test_Native_Double_Dispose_Job(CreateEmpty_NativeRingQueue());
        Test_Native_Double_Dispose_Job(CreateEmpty_NativeStream());
        Test_Native_Double_Dispose_Job(CreateEmpty_NativeText());

        Test_Unsafe_Double_Dispose_Job(CreateEmpty_UnsafeAppendBuffer());
        Test_Unsafe_Double_Dispose_Job(CreateEmpty_UnsafeBitArray());
        Test_Unsafe_Double_Dispose_Job(CreateEmpty_UnsafeHashMap());
        Test_Unsafe_Double_Dispose_Job(CreateEmpty_UnsafeHashSet());
        Test_Unsafe_Double_Dispose_Job(CreateEmpty_UnsafeList());
        Test_Unsafe_Double_Dispose_Job(CreateEmpty_UnsafePtrList());
        Test_Unsafe_Double_Dispose_Job(CreateEmpty_UnsafeParallelHashMap());
        Test_Unsafe_Double_Dispose_Job(CreateEmpty_UnsafeParallelHashSet());
        Test_Unsafe_Double_Dispose_Job(CreateEmpty_UnsafeParallelMultiHashMap());
        Test_Unsafe_Double_Dispose_Job(CreateEmpty_UnsafeQueue());
        Test_Unsafe_Double_Dispose_Job(CreateEmpty_UnsafeRingQueue());
        Test_Unsafe_Double_Dispose_Job(CreateEmpty_UnsafeStream());
        Test_Unsafe_Double_Dispose_Job(CreateEmpty_UnsafeText());
    }

    //-------------------------------------------------------------------------------------------------------

    void Test_Dispose_Job_Missing_Dependency<T>(T container)
        where T : INativeDisposable
    {
        GenericContainerJob<T> job = new GenericContainerJob<T>() { data = container };
        JobHandle jobHandle = job.Schedule();
        Assert.Throws<InvalidOperationException>(() => container.Dispose(default));
        Assert.DoesNotThrow(() => jobHandle = container.Dispose(jobHandle));
        jobHandle.Complete();
    }

    [Test]
    [TestRequiresCollectionChecks("Tests dispose job while another job is scheduled - crashes without safety system")]
    public void INativeDisposable_Dispose_Job_Missing_Dependency()
    {
        Test_Dispose_Job_Missing_Dependency(new NativeBitArray(16, Allocator.Persistent));
        Test_Dispose_Job_Missing_Dependency(new NativeHashMap<int, int>(16, Allocator.Persistent));
        Test_Dispose_Job_Missing_Dependency(new NativeHashSet<int>(16, Allocator.Persistent));
        Test_Dispose_Job_Missing_Dependency(new NativeList<int>(16, Allocator.Persistent));
        Test_Dispose_Job_Missing_Dependency(new NativeParallelHashMap<int, int>(16, Allocator.Persistent));
        Test_Dispose_Job_Missing_Dependency(new NativeParallelHashSet<int>(16, Allocator.Persistent));
        Test_Dispose_Job_Missing_Dependency(new NativeParallelMultiHashMap<int, int>(16, Allocator.Persistent));
        Test_Dispose_Job_Missing_Dependency(new NativeQueue<int>(Allocator.Persistent));
        Test_Dispose_Job_Missing_Dependency(new NativeReference<int>(16, Allocator.Persistent));
        Test_Dispose_Job_Missing_Dependency(new NativeRingQueue<int>(16, Allocator.Persistent));
        Test_Dispose_Job_Missing_Dependency(new NativeStream(16, Allocator.Persistent));
        Test_Dispose_Job_Missing_Dependency(new NativeText(16, Allocator.Persistent));
    }

    //-------------------------------------------------------------------------------------------------------

    void Test_Dispose_Job_Then_Schedule_Work<T>(T container)
        where T : INativeDisposable
    {
        GenericContainerJob<T> job = new GenericContainerJob<T>() { data = container };
        JobHandle jobHandle = container.Dispose(default);
        Assert.Throws<InvalidOperationException>(() => job.Schedule(jobHandle));
        jobHandle.Complete();
    }

    [Test]
    [TestRequiresCollectionChecks("Tests job depending on a dispose job with same data - crashes without safety system")]
    public void INativeDisposable_Dispose_Job_Then_Schedule_Work()
    {
        Test_Dispose_Job_Then_Schedule_Work(new NativeBitArray(16, Allocator.Persistent));
        Test_Dispose_Job_Then_Schedule_Work(new NativeHashMap<int, int>(16, Allocator.Persistent));
        Test_Dispose_Job_Then_Schedule_Work(new NativeHashSet<int>(16, Allocator.Persistent));
        Test_Dispose_Job_Then_Schedule_Work(new NativeList<int>(16, Allocator.Persistent));
        Test_Dispose_Job_Then_Schedule_Work(new NativeParallelHashMap<int, int>(16, Allocator.Persistent));
        Test_Dispose_Job_Then_Schedule_Work(new NativeParallelHashSet<int>(16, Allocator.Persistent));
        Test_Dispose_Job_Then_Schedule_Work(new NativeParallelMultiHashMap<int, int>(16, Allocator.Persistent));
        Test_Dispose_Job_Then_Schedule_Work(new NativeQueue<int>(Allocator.Persistent));
        Test_Dispose_Job_Then_Schedule_Work(new NativeReference<int>(16, Allocator.Persistent));
        Test_Dispose_Job_Then_Schedule_Work(new NativeRingQueue<int>(16, Allocator.Persistent));
        Test_Dispose_Job_Then_Schedule_Work(new NativeStream(16, Allocator.Persistent));
        Test_Dispose_Job_Then_Schedule_Work(new NativeText(16, Allocator.Persistent));
    }

    //-------------------------------------------------------------------------------------------------------

    // Avoid running this test when on older unity releases since those editor versions
    // used a global safety handle for temp allocations which could lead to invalid safety errors
    // if we were to perform the safety checks when writing the Length that this test validates
    // (The test uses Persistent allocations, but the code in NativeList.Length is conditional on
    // this define so we make the tests conditional as well)
#if UNITY_2022_2_16F1_OR_NEWER
    void Test_Change_Length_Missing_Dependency<T, U>(T container)
        where T : unmanaged, IIndexable<U>
        where U : unmanaged
    {
        int localLength = 0;
        // Readonly Job
        {
            var job = new GenericContainerReadonlyJob<T>() { data = container };
            var jobHandle = job.Schedule();
            Assert.DoesNotThrow(() => localLength = container.Length); // Reading is safe
            Assert.Throws<InvalidOperationException>(() => container.Length = 0); // Writing while a job is in flight it not safe
            jobHandle.Complete();
            Assert.DoesNotThrow(() => container.Length = 0);
        }

        // ReadWrite job
        {
            var job = new GenericContainerJob<T>() { data = container };
            var jobHandle = job.Schedule();
            Assert.Throws<InvalidOperationException>(() => localLength = container.Length); // Reading is not safe
            Assert.Throws<InvalidOperationException>(() => container.Length = 0); // Writing while a job is in flight it not safe
            jobHandle.Complete();
            Assert.DoesNotThrow(() => localLength = container.Length);
            Assert.DoesNotThrow(() => container.Length = 0);
        }
    }

    [Test]
    [TestRequiresCollectionChecks()]
    public void IIndexable_Change_Length_Missing_Dependency()
    {
        var container = new NativeList<int>(16, Allocator.Persistent);
        Test_Change_Length_Missing_Dependency<NativeList<int>, int>(container);
        container.Dispose();
    }
#endif
    //-------------------------------------------------------------------------------------------------------

    struct NativeHashMapJobForEach : IJob
    {
        public NativeHashMap<int, int> input;
        public void Execute()
        {
            foreach (var _ in input) { }
        }
    }

    struct NativeHashMapJobForEachReadOnly : IJob
    {
        public NativeHashMap<int, int>.ReadOnly input;
        public void Execute()
        {
            foreach (var _ in input) { }
        }
    }

    struct NativeHashMapJobForEachEnumerator : IJob
    {
        [ReadOnly]
        public NativeHashMap<int, int>.Enumerator input;

        public void Execute()
        {
            while (input.MoveNext()) { }
        }
    }

    [Test]
    [TestRequiresCollectionChecks("Tests depend on safety system to catch incorrect use.")]
    public void ForEach()
    {
        // CreateEmpty_NativeBitArray();

        {
            var container = CreateEmpty_NativeHashMap();
            var ro = container.AsReadOnly();

            GCAllocRecorder.ValidateNoGCAllocs(() =>
            {
                foreach (var item in container) { }
                foreach (var item in ro) { }
                new NativeHashMapJobForEach { input = container }.Run();
                new NativeHashMapJobForEachReadOnly { input = ro }.Run();
                new NativeHashMapJobForEachEnumerator { input = container.GetEnumerator() }.Run();
                new NativeHashMapJobForEachEnumerator { input = ro.GetEnumerator() }.Run();
            });

            {
                var job = new NativeHashMapJobForEach { input = container }.Schedule();
                Assert.Throws<InvalidOperationException>(() => { container.Add(123, 456); });
                job.Complete();
                Assert.DoesNotThrow(() => container.Add(123, 456));
                container.Clear();
            }

            {
                var job = new NativeHashMapJobForEachReadOnly { input = ro }.Schedule();
                Assert.Throws<InvalidOperationException>(() => { container.Add(123, 456); });
                job.Complete();
                Assert.DoesNotThrow(() => container.Add(123, 456));
                container.Clear();
            }

            {
                var job = new NativeHashMapJobForEachEnumerator { input = container.GetEnumerator() }.Schedule();
                Assert.Throws<InvalidOperationException>(() => { container.Add(123, 456); });
                job.Complete();
                Assert.DoesNotThrow(() => container.Add(123, 456));
                container.Clear();
            }

            {
                var job = new NativeHashMapJobForEachEnumerator { input = ro.GetEnumerator() }.Schedule();
                Assert.Throws<InvalidOperationException>(() => { container.Add(123, 456); });
                job.Complete();
                Assert.DoesNotThrow(() => container.Add(123, 456));
                container.Clear();
            }

            {
                var iter = container.GetEnumerator();
                container.Add(123, 456);
                Assert.Throws<ObjectDisposedException>(() => { while (iter.MoveNext()) { } });
                Assert.DoesNotThrow(() => container.Remove(123));
                Assert.AreEqual(0, container.Count);
            }

            {
                var iter = container.AsReadOnly().GetEnumerator();
                container.Add(123, 456);
                Assert.Throws<ObjectDisposedException>(() => { while (iter.MoveNext()) { } });
                Assert.DoesNotThrow(() => container.Remove(123));
                Assert.AreEqual(0, container.Count);
            }

            container.Dispose();
        }

        {
            var container = CreateEmpty_NativeHashSet();
            var ro = container.AsReadOnly();

            GCAllocRecorder.ValidateNoGCAllocs(() =>
            {
                foreach (var item in container) { }
                foreach (var item in ro) { }
            });

            container.Dispose();
        }

        {
            var container = CreateEmpty_NativeList();
            var ro = container.AsReadOnly();

            GCAllocRecorder.ValidateNoGCAllocs(() =>
            {
                foreach (var item in container) { }
                foreach (var item in ro) { }
            });

            container.Dispose();
        }

        {
            var container = CreateEmpty_NativeParallelHashMap();
            var ro = container.AsReadOnly();

            GCAllocRecorder.ValidateNoGCAllocs(() =>
            {
                foreach (var item in container) { }
                foreach (var item in ro) { }
            });

            container.Dispose();
        }

        {
            var container = CreateEmpty_NativeParallelHashSet();
            var ro = container.AsReadOnly();

            GCAllocRecorder.ValidateNoGCAllocs(() =>
            {
                foreach (var item in container) { }
                foreach (var item in ro) { }
            });

            container.Dispose();
        }

        {
            var container = CreateEmpty_NativeParallelMultiHashMap();
            var ro = container.AsReadOnly();

            GCAllocRecorder.ValidateNoGCAllocs(() =>
            {
                foreach (var item in container) { }
                foreach (var item in ro) { }
            });

            container.Dispose();
        }

        {
            var container = CreateEmpty_NativeQueue();
            var ro = container.AsReadOnly();

            GCAllocRecorder.ValidateNoGCAllocs(() =>
            {
//                foreach (var item in container) { }
                foreach (var item in ro) { }
            });

            container.Dispose();
        }

        // CreateEmpty_NativeReference();
        // CreateEmpty_NativeRingQueue();
        // CreateEmpty_NativeStream();

        {
            var container = CreateEmpty_NativeText();
            var ro = container.AsReadOnly();

            GCAllocRecorder.ValidateNoGCAllocs(() =>
            {
                foreach (var item in container) { }
                foreach (var item in ro) { }
            });

            container.Dispose();
        }

        // CreateEmpty_UnsafeAppendBuffer();
        // CreateEmpty_UnsafeBitArray

        {
            var container = CreateEmpty_UnsafeHashMap();
            var ro = container.AsReadOnly();

            GCAllocRecorder.ValidateNoGCAllocs(() =>
            {
                foreach (var item in container) { }
                foreach (var item in ro) { }
            });

            container.Dispose();
        }

        {
            var container = CreateEmpty_UnsafeHashSet();
            var ro = container.AsReadOnly();

            GCAllocRecorder.ValidateNoGCAllocs(() =>
            {
                foreach (var item in container) { }
                foreach (var item in ro) { }
            });

            container.Dispose();
        }

        {
            var container = CreateEmpty_UnsafeList();
            var ro = container.AsReadOnly();

            GCAllocRecorder.ValidateNoGCAllocs(() =>
            {
                foreach (var item in container) { }
                foreach (var item in ro) { }
            });

            container.Dispose();
        }

        // CreateEmpty_UnsafePtrList(); - not possible to implement intefrace because container returns T*, and interface wants T as return value.

        {
            var container = CreateEmpty_UnsafeParallelHashMap();
            var ro = container.AsReadOnly();

            GCAllocRecorder.ValidateNoGCAllocs(() =>
            {
                foreach (var item in container) { }
                foreach (var item in ro) { }
            });

            container.Dispose();
        }

        {
            var container = CreateEmpty_UnsafeParallelHashSet();
            var ro = container.AsReadOnly();

            GCAllocRecorder.ValidateNoGCAllocs(() =>
            {
                foreach (var item in container) { }
                foreach (var item in ro) { }
            });

            container.Dispose();
        }

        {
            var container = CreateEmpty_UnsafeParallelMultiHashMap();
            var ro = container.AsReadOnly();

            GCAllocRecorder.ValidateNoGCAllocs(() =>
            {
                foreach (var item in container) { }
                foreach (var item in ro) { }
            });

            container.Dispose();
        }

        {
            var container = CreateEmpty_UnsafeQueue();
            var ro = container.AsReadOnly();

            GCAllocRecorder.ValidateNoGCAllocs(() =>
            {
//                foreach (var item in container) { }
                foreach (var item in ro) { }
            });

            container.Dispose();
        }

        // CreateEmpty_UnsafeRingQueue
        // CreateEmpty_UnsafeStream
        // CreateEmpty_UnsafeText
    }
}
