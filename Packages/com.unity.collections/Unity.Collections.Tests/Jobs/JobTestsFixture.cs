using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.Jobs.Tests.ManagedJobs
{
    internal class JobTestsFixtureBasic
    {
        AllocatorHelper<RewindableAllocator> m_AllocatorHelper;
        protected ref RewindableAllocator RwdAllocator => ref m_AllocatorHelper.Allocator;

        [OneTimeSetUp]
        public virtual void OneTimeSetUp()
        {
            m_AllocatorHelper = new AllocatorHelper<RewindableAllocator>(Allocator.Persistent);
            m_AllocatorHelper.Allocator.Initialize(128 * 1024, true);
        }

        [OneTimeTearDown]
        public virtual void OneTimeTearDown()
        {
            m_AllocatorHelper.Allocator.Dispose();
            m_AllocatorHelper.Dispose();
        }

        [SetUp]
        public void Setup()
        {
#if UNITY_DOTSRUNTIME
            Unity.Runtime.TempMemoryScope.EnterScope();
#endif
        }

        [TearDown]
        public void Teardown()
        {
            RwdAllocator.Rewind();
            // This is test only behavior for determinism.  Rewind twice such that all
            // tests start with an allocator containing only one memory block.
            RwdAllocator.Rewind();

#if UNITY_DOTSRUNTIME
            Unity.Runtime.TempMemoryScope.ExitScope();
#endif
        }
    }

    internal class JobTestsFixture : JobTestsFixtureBasic
    {
        /*
         * this used to test both, and maybe it should again, but we have to make GetExecuteMethod() work with
         * multiple interfaces, hopefully in a non-global way
         */
        public struct SumDataParallelForJob : /*IJob,*/ IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> input0;

            [ReadOnly] public NativeArray<int> input1;

            public NativeArray<int> output;

            /*  public void Execute()
              {
                  for (var i = 0; i < output.Length; ++i)
                      output[i] = input0[i] + input1[i];
              }*/

            public void Execute(int i)
            {
                output[i] = input0[i] + input1[i];
            }
        }

        public struct CopyAndDestroyNativeArrayParallelForJob : IJobParallelFor
        {
            [ReadOnlyAttribute]
            public NativeArray<int> input;

            public NativeArray<int> output;

            public void Execute(int i)
            {
                output[i] = input[i];
            }
        }

        public SumDataParallelForJob data;

        public int[] expectedInput0;

        public NativeArray<int> input0;
        public NativeArray<int> input1;
        public NativeArray<int> input2;
        public NativeArray<int> output;
        public NativeArray<int> output2;

        [SetUp]
        public void Init()
        {
            expectedInput0 = new int[10];
            input0 = new NativeArray<int>(10, Allocator.Persistent);
            input1 = new NativeArray<int>(10, Allocator.Persistent);
            input2 = new NativeArray<int>(10, Allocator.Persistent);
            output = new NativeArray<int>(10, Allocator.Persistent);
            output2 = new NativeArray<int>(10, Allocator.Persistent);

            for (var i = 0; i < output.Length; i++)
            {
                expectedInput0[i] = i;
                input0[i] = i;
                input1[i] = 10 * i;
                input2[i] = 100 * i;
                output[i] = 0;
                output2[i] = 0;
            }

            data.input0 = input0;
            data.input1 = input1;
            data.output = output;
        }

        public void ExpectOutputSumOfInput0And1()
        {
            for (var i = 0; i != output.Length; i++)
                Assert.AreEqual(input0[i] + input1[i], output[i]);
        }

        public void ExpectOutputSumOfInput0And1And2()
        {
            for (var i = 0; i != output.Length; i++)
                Assert.AreEqual(input0[i] + input1[i] + input2[i], output[i]);
        }

        [TearDown]
        public void Cleanup()
        {
            try
            {
                input0.Dispose();
            }
            catch
            {
            }

            try
            {
                input1.Dispose();
            }
            catch
            {
            }

            try
            {
                input2.Dispose();
            }
            catch
            {
            }

            try
            {
                output.Dispose();
            }
            catch
            {
            }

            try
            {
                output2.Dispose();
            }
            catch
            {
            }
        }
    }
}
