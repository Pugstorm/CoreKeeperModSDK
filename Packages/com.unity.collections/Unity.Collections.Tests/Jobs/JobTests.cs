using System;
using NUnit.Framework;
using UnityEngine.Scripting;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Burst;
using System.Diagnostics;
using Unity.Collections.Tests;

[assembly: RegisterGenericJobType(typeof(Unity.Jobs.Tests.ManagedJobs.MyGenericJobDefer<int>))]
[assembly: RegisterGenericJobType(typeof(Unity.Jobs.Tests.ManagedJobs.MyGenericJobDefer<double>))]
[assembly: RegisterGenericJobType(typeof(Unity.Jobs.Tests.ManagedJobs.MyGenericJobDefer<float>))]
[assembly: RegisterGenericJobType(typeof(Unity.Jobs.Tests.ManagedJobs.GenericContainerJobDefer<NativeList<int>, int>))]

namespace Unity.Jobs.Tests.ManagedJobs
{
    internal enum JobRunType
    {
        Schedule,
        ScheduleByRef,
        Run,
        RunByRef,
    }

#if UNITY_DOTSRUNTIME
    internal class DotsRuntimeFixmeAttribute : IgnoreAttribute
    {
        public DotsRuntimeFixmeAttribute(string msg = null) : base(msg == null ? "Test should work in DOTS Runtime but currently doesn't. Ignoring until fixed..." : msg)
        {
        }
    }
#else
    internal class DotsRuntimeFixmeAttribute : Attribute
	{
        public DotsRuntimeFixmeAttribute(string msg = null)
        {
        }
	}
#endif

	[JobProducerType(typeof(IJobTestExtensions.JobTestProducer<>))]
    internal interface IJobTest
	{
		void Execute();
	}

    internal interface IJobTestInherit : IJob
    {
    }

    internal static class IJobTestExtensions
	{
        internal struct JobTestWrapper<T> where T : struct
        {
            internal T JobData;

            [NativeDisableContainerSafetyRestriction]
            internal NativeArray<byte> ProducerResourceToClean;
        }

		internal struct JobTestProducer<T> where T : struct, IJobTest
		{
			internal static readonly SharedStatic<IntPtr> s_JobReflectionData = SharedStatic<IntPtr>.GetOrCreate<JobTestProducer<T>>();

            [BurstDiscard]
            internal static void Initialize()
			{
				if (s_JobReflectionData.Data == IntPtr.Zero)
					s_JobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobTestWrapper<T>), typeof(T), (ExecuteJobFunction)Execute);
			}

			public delegate void ExecuteJobFunction(ref JobTestWrapper<T> jobWrapper, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);
			public unsafe static void Execute(ref JobTestWrapper<T> jobWrapper, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
			{
				jobWrapper.JobData.Execute();
			}
		}

        public static void EarlyJobInit<T>()
            where T : struct, IJobTest
        {
            JobTestProducer<T>.Initialize();
        }

        static IntPtr GetReflectionData<T>()
            where T : struct, IJobTest
        {
            JobTestProducer<T>.Initialize();
            var reflectionData = JobTestProducer<T>.s_JobReflectionData.Data;
            CollectionHelper.CheckReflectionDataCorrect<T>(reflectionData);
            return reflectionData;
        }

        public static unsafe JobHandle ScheduleTest<T>(this T jobData, NativeArray<byte> dataForProducer, JobHandle dependsOn = new JobHandle()) where T : struct, IJobTest
		{
			JobTestWrapper<T> jobTestWrapper = new JobTestWrapper<T>
			{
				JobData = jobData,
				ProducerResourceToClean = dataForProducer
			};

			var scheduleParams = new JobsUtility.JobScheduleParameters(
				UnsafeUtility.AddressOf(ref jobTestWrapper),
				GetReflectionData<T>(),
				dependsOn,
				ScheduleMode.Parallel
			);

			return JobsUtility.Schedule(ref scheduleParams);
		}
    }

    // DOTS Runtime doesn't support multiple producers
#if !UNITY_DOTSRUNTIME
    [JobProducerType(typeof(IJobTestInheritProducerExtensions.JobTestProducer<>))]
    internal interface IJobTestInheritWithProducer : IJob
    {
        void Execute(bool empty);
    }

    internal static class IJobTestInheritProducerExtensions
    {
        internal struct JobTestWrapper<T> where T : struct
        {
            internal T JobData;
            internal byte Empty;
        }

        internal struct JobTestProducer<T> where T : struct, IJobTestInheritWithProducer
        {
            internal static readonly SharedStatic<IntPtr> jobReflectionData = SharedStatic<IntPtr>.GetOrCreate<JobTestProducer<T>>();

            [BurstDiscard]
            internal static void Initialize()
            {
                if (jobReflectionData.Data == IntPtr.Zero)
                    jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobTestWrapper<T>), typeof(T), (ExecuteJobFunction)Execute);
            }

            public delegate void ExecuteJobFunction(ref JobTestWrapper<T> jobWrapper, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);
            public unsafe static void Execute(ref JobTestWrapper<T> jobWrapper, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                jobWrapper.JobData.Execute(jobWrapper.Empty != 0);
            }
        }

        public static void EarlyJobInit<T>()
            where T : struct, IJobTestInheritWithProducer
        {
            JobTestProducer<T>.Initialize();
        }

        static IntPtr GetReflectionData<T>()
            where T : struct, IJobTestInheritWithProducer
        {
            JobTestProducer<T>.Initialize();
            var reflectionData = JobTestProducer<T>.jobReflectionData.Data;
            CollectionHelper.CheckReflectionDataCorrect<T>(reflectionData);
            return reflectionData;
        }

        unsafe public static JobHandle Schedule<T>(this T jobData, bool empty, JobHandle dependsOn = new JobHandle()) where T : struct, IJobTestInheritWithProducer
        {
            JobTestWrapper<T> jobTestWrapper = new JobTestWrapper<T>
            {
                JobData = jobData,
                Empty = (byte)(empty ? 1 : 0)
            };

            var scheduleParams = new JobsUtility.JobScheduleParameters(
                UnsafeUtility.AddressOf(ref jobTestWrapper),
                GetReflectionData<T>(),
                dependsOn,
                ScheduleMode.Parallel
            );

            return JobsUtility.Schedule(ref scheduleParams);
        }
    }
#endif

    internal struct MyGenericResizeJob<T> : IJob where T : unmanaged
    {
		public int m_ListLength;
		public NativeList<T> m_GenericList;
		public void Execute()
		{
			m_GenericList.Resize(m_ListLength, NativeArrayOptions.UninitializedMemory);
		}
	}

    internal struct MyGenericJobDefer<T> : IJobParallelForDefer where T: unmanaged
	{
		public T m_Value;
		[NativeDisableParallelForRestriction]
		public NativeList<T> m_GenericList;
		public void Execute(int index)
		{
			m_GenericList[index] = m_Value;
		}
	}

    internal struct GenericContainerResizeJob<T, U> : IJob
        where T : unmanaged, INativeList<U>
        where U : unmanaged
    {
        public int m_ListLength;
        public T m_GenericList;
        public void Execute()
        {
            m_GenericList.Length = m_ListLength;
        }
    }

    internal struct GenericContainerJobDefer<T, U> : IJobParallelForDefer
        where T : unmanaged, INativeList<U>
        where U : unmanaged
    {
        public U m_Value;
        [NativeDisableParallelForRestriction]
        public T m_GenericList;

        public void Execute(int index)
        {
            m_GenericList[index] = m_Value;
        }
    }

    internal class JobTests : JobTestsFixture
    {
        public void ScheduleGenericContainerJob<T, U>(T container, U value)
            where T : unmanaged, INativeList<U>
            where U : unmanaged
        {
            var j0 = new GenericContainerResizeJob<T, U>();
            var length = 5;
            j0.m_ListLength = length;
            j0.m_GenericList = container;
            var handle0 = j0.Schedule();

            var j1 = new GenericContainerJobDefer<T, U>();
            j1.m_Value = value;
            j1.m_GenericList = j0.m_GenericList;
            INativeList<U> iList = j0.m_GenericList;
            j1.Schedule((NativeList<U>)iList, 1, handle0).Complete();

            Assert.AreEqual(length, j1.m_GenericList.Length);
            for (int i = 0; i != j1.m_GenericList.Length; i++)
                Assert.AreEqual(value, j1.m_GenericList[i]);
        }

        [Test]
        public void ValidateContainerSafetyInGenericJob_ContainerIsGenericParameter()
        {
            var list = new NativeList<int>(1, RwdAllocator.ToAllocator);
            ScheduleGenericContainerJob(list, 5);
        }

        public void GenericScheduleJobPair<T>(T value) where T : unmanaged
        {
            var j0 = new MyGenericResizeJob<T>();
            var length = 5;
            j0.m_ListLength = length;
            j0.m_GenericList = new NativeList<T>(1, RwdAllocator.ToAllocator);
            var handle0 = j0.Schedule();

            var j1 = new MyGenericJobDefer<T>();
            j1.m_Value = value;
            j1.m_GenericList = j0.m_GenericList;
            j1.Schedule(j0.m_GenericList, 1, handle0).Complete();

            Assert.AreEqual(length, j1.m_GenericList.Length);
            for (int i = 0; i != j1.m_GenericList.Length; i++)
                Assert.AreEqual(value, j1.m_GenericList[i]);
        }

        [Test]
        public void ScheduleGenericJobPairFloat()
        {
            GenericScheduleJobPair(10f);
        }

        [Test]
        public void ScheduleGenericJobPairDouble()
        {
            GenericScheduleJobPair<double>(10.0);
        }

        [Test]
        public void ScheduleGenericJobPairInt()
        {
            GenericScheduleJobPair(20);
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
	    public void SchedulingGenericJobUnsafelyThrows()
	    {
		    var j0 = new MyGenericResizeJob<int>();
		    var length = 5;
		    j0.m_ListLength = length;
		    j0.m_GenericList = new NativeList<int>(1, RwdAllocator.ToAllocator);
		    var handle0 = j0.Schedule();
		    var j1 = new MyGenericJobDefer<int>();
		    j1.m_Value = 6;
		    j1.m_GenericList = j0.m_GenericList;
		    Assert.Throws<InvalidOperationException>(()=>j1.Schedule(j0.m_GenericList, 1).Complete());
		    handle0.Complete();
	    }
#endif

        struct DontReferenceThisTypeOutsideOfThisTest { public int v; }
        
        [Test]
        [TestRequiresCollectionChecks]
        [DotsRuntimeFixme("DOTS Runtime doesn't detect safety handles in the generic container")]
        public void SchedulingGenericJobFromGenericContextUnsafelyThrows()
        {
            var list = new NativeList<DontReferenceThisTypeOutsideOfThisTest>(1, RwdAllocator.ToAllocator);
            ScheduleGenericJobUnsafely(list, new DontReferenceThisTypeOutsideOfThisTest { v = 5 });
        }

        void ScheduleGenericJobUnsafely<T, U>(T container, U value)
            where T : unmanaged, INativeList<U>
            where U : unmanaged
        {
            var j0 = new GenericContainerResizeJob<T, U>();
            var length = 5;
            j0.m_ListLength = length;
            j0.m_GenericList = container;
            var handle0 = j0.Schedule();

            var j1 = new GenericContainerJobDefer<T, U>();
            j1.m_Value = value;
            j1.m_GenericList = j0.m_GenericList;
            INativeList<U> iList = j0.m_GenericList;
            Assert.Throws<InvalidOperationException>(()=>j1.Schedule((NativeList<U>)iList, 1).Complete());

            handle0.Complete();  // complete this so we can dispose the nativelist
        }

        /*
	     * these two tests used to test that a job that inherited from both IJob and IJobParallelFor would work as expected
	     * but that's probably crazy.
	     */
        /*[Test]
        public void Scheduling()
        {
            var job = data.Schedule();
            job.Complete();
            ExpectOutputSumOfInput0And1();
        }*/


        /*[Test]

        public void Scheduling_With_Dependencies()
        {
            data.input0 = input0;
            data.input1 = input1;
            data.output = output2;
            var job1 = data.Schedule();

            // Schedule job2 with dependency against the first job
            data.input0 = output2;
            data.input1 = input2;
            data.output = output;
            var job2 = data.Schedule(job1);

            // Wait for completion
            job2.Complete();
            ExpectOutputSumOfInput0And1And2();
        }*/

        [Test]
        public void ForEach_Scheduling_With_Dependencies()
        {
            data.input0 = input0;
            data.input1 = input1;
            data.output = output2;
            var job1 = data.Schedule(output.Length, 1);

            // Schedule job2 with dependency against the first job
            data.input0 = output2;
            data.input1 = input2;
            data.output = output;
            var job2 = data.Schedule(output.Length, 1, job1);

            // Wait for completion
            job2.Complete();
            ExpectOutputSumOfInput0And1And2();
        }

        struct EmptyComputeParallelForJob : IJobParallelFor
        {
            public void Execute(int i)
            {
            }
        }

        [Test]
        public void ForEach_Scheduling_With_Zero_Size()
        {
            var test = new EmptyComputeParallelForJob();
            var job = test.Schedule(0, 1);
            job.Complete();
        }

        [Test]
        public void Deallocate_Temp_NativeArray_From_Job()
        {
            TestDeallocateNativeArrayFromJob(RwdAllocator.ToAllocator);
        }

        [Test]
        public void Deallocate_Persistent_NativeArray_From_Job()
        {
            TestDeallocateNativeArrayFromJob(Allocator.Persistent);
        }

        private void TestDeallocateNativeArrayFromJob(Allocator label)
        {
            var tempNativeArray = CollectionHelper.CreateNativeArray<int>(expectedInput0, label);

            var copyAndDestroyJob = new CopyAndDestroyNativeArrayParallelForJob
            {
                input = tempNativeArray,
                output = output
            };

            // NativeArray can safely be accessed before scheduling
            Assert.AreEqual(10, tempNativeArray.Length);

            tempNativeArray[0] = tempNativeArray[0];

            var job = copyAndDestroyJob.Schedule(copyAndDestroyJob.input.Length, 1);

            job.Complete();

            // Need to dispose because the allocator may be Allocator.Persistent.
            tempNativeArray.Dispose();

            Assert.AreEqual(expectedInput0, copyAndDestroyJob.output.ToArray());
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public struct NestedDeallocateStruct
        {
			public NativeArray<int> input;
        }

        public struct TestNestedDeallocate : IJob
        {
            public NestedDeallocateStruct nested;

            public NativeArray<int> output;

            public void Execute()
            {
                for (int i = 0; i < nested.input.Length; ++i)
                    output[i] = nested.input[i];
            }
        }

        [Test]
        public void TestNestedDeallocateOnJobCompletion()
        {
            var tempNativeArray = CollectionHelper.CreateNativeArray<int>(10, RwdAllocator.ToAllocator);
            var outNativeArray = CollectionHelper.CreateNativeArray<int>(10, RwdAllocator.ToAllocator);
            for (int i = 0; i < 10; i++)
                tempNativeArray[i] = i;

            var job = new TestNestedDeallocate
            {
                nested = new NestedDeallocateStruct() { input = tempNativeArray },
                output = outNativeArray
            };

            var handle = job.Schedule();
            handle.Complete();

            RwdAllocator.Rewind();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Ensure released safety handle indicating invalid buffer
            Assert.Throws<ObjectDisposedException>(() => { AtomicSafetyHandle.CheckExistsAndThrow(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(tempNativeArray)); });
			Assert.Throws<ObjectDisposedException>(() => { AtomicSafetyHandle.CheckExistsAndThrow(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(job.nested.input)); });
#endif
        }

        public struct TestJobProducerJob : IJobTest
        {
			public NativeArray<int> jobStructData;

            public void Execute()
            {
            }
        }

        [Test]
        public void TestJobProducerCleansUp()
        {
            var tempNativeArray = CollectionHelper.CreateNativeArray<int>(10, RwdAllocator.ToAllocator);
            var tempNativeArray2 = CollectionHelper.CreateNativeArray<byte>(16, RwdAllocator.ToAllocator);

            var job = new TestJobProducerJob
            {
                jobStructData = tempNativeArray,
            };

            var handle = job.ScheduleTest(tempNativeArray2);
            handle.Complete();

            RwdAllocator.Rewind();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Check job data
            Assert.Throws<ObjectDisposedException>(() => { AtomicSafetyHandle.CheckExistsAndThrow(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(tempNativeArray)); });
			Assert.Throws<ObjectDisposedException>(() => { AtomicSafetyHandle.CheckExistsAndThrow(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(job.jobStructData)); });
			// Check job producer
			Assert.Throws<ObjectDisposedException>(() => { AtomicSafetyHandle.CheckExistsAndThrow(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(tempNativeArray2)); });
#endif
        }

        public struct CopyJob : IJob
        {
            public NativeList<int> List1;
            public NativeList<int> List2;

            public void Execute()
            {
                List1 = List2;
            }
        }

        [Test]
        public unsafe void TestContainerCopy_EnsureSafetyHandlesCopyAndDisposeProperly()
        {
            var list1 = new NativeList<int>(10, RwdAllocator.ToAllocator);
            var list2 = new NativeList<int>(10, RwdAllocator.ToAllocator);
            list1.Add(1);
            list2.Add(2);

            var job = new CopyJob
            {
                List1 = list1,
                List2 = list2
            };

            job.Schedule().Complete();

            list1.Dispose();
            list2.Dispose();
        }
#endif

        struct LargeJobParallelForDefer : IJobParallelForDefer
        {
            public FixedString4096Bytes StrA;
            public FixedString4096Bytes StrB;
            public FixedString4096Bytes StrC;
            public FixedString4096Bytes StrD;
            [NativeDisableParallelForRestriction]
            public NativeArray<int> TotalLengths;
            [ReadOnly]
            public NativeList<float> Unused; // Schedule() from NativeList.Length requires that the list be passed into the job

            public void Execute(int index)
            {
                TotalLengths[0] = StrA.Length + StrB.Length + StrC.Length + StrD.Length;
            }
        }

        public enum IterationCountMode
        {
            List, Pointer
        }

        [Test]
        public unsafe void IJobParallelForDefer_LargeJobStruct_ScheduleRefWorks(
            [Values(IterationCountMode.List, IterationCountMode.Pointer)] IterationCountMode countMode)
        {
            using(var lengths = CollectionHelper.CreateNativeArray<int>(1, RwdAllocator.ToAllocator))
            {
                var dummyList = new NativeList<float>(RwdAllocator.ToAllocator);
                dummyList.Add(5.0f);
                var job = new LargeJobParallelForDefer
                {
                    StrA = "A",
                    StrB = "BB",
                    StrC = "CCC",
                    StrD = "DDDD",
                    TotalLengths = lengths,
                    Unused = dummyList,
                };

                if (countMode == IterationCountMode.List)
                {
                    Assert.DoesNotThrow(() => job.ScheduleByRef(dummyList, 1).Complete());
                }
                else if (countMode == IterationCountMode.Pointer)
                {
                    var lengthArray = CollectionHelper.CreateNativeArray<int>(1, RwdAllocator.ToAllocator);
                    lengthArray[0] = 1;
                    Assert.DoesNotThrow(() => job.ScheduleByRef((int*)lengthArray.GetUnsafePtr(), 1).Complete());
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct InheritJob : IJobTestInherit
        {
            public NativeList<int> List1;
            public NativeList<int> List2;

            public void Execute()
            {
                List1[0] = List2[0];
            }
        }

        [Test]
        public void InheritInterfaceJobWorks()
        {
            var l1 = new NativeList<int>(4, RwdAllocator.ToAllocator);
            l1.Add(3);
            var l2 = new NativeList<int>(4, RwdAllocator.ToAllocator);
            l2.Add(17);
            var job = new InheritJob { List1 = l1, List2 = l2 };
            job.Schedule().Complete();

            Assert.IsTrue(l1[0] == 17);

            l2.Dispose();
            l1.Dispose();
        }

        // DOTS Runtime doesn't support multiple producers
#if !UNITY_DOTSRUNTIME
        [BurstCompile(CompileSynchronously = true)]
        public struct InheritWithProducerJob : IJobTestInheritWithProducer
        {
            public NativeList<int> List1;
            public NativeList<int> List2;

            public void Execute()
            {
                List2[0] = List1[0];
            }

            public void Execute(bool empty)
            {
                List1[0] = List2[0];
            }
        }

        [Test]
        public void InheritInterfaceWithProducerJobWorks()
        {
            var l1 = new NativeList<int>(4, RwdAllocator.ToAllocator);
            l1.Add(3);
            var l2 = new NativeList<int>(4, RwdAllocator.ToAllocator);
            l2.Add(17);
            var job = new InheritWithProducerJob { List1 = l1, List2 = l2 };
            job.Schedule(false).Complete();
            Assert.IsTrue(l1[0] == 17);

            l1[0] = 3;
            job.Schedule().Complete();
            Assert.IsTrue(l2[0] == 3);

            l2.Dispose();
            l1.Dispose();
        }
#endif
    }
}
