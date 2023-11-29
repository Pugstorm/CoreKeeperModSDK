using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;

namespace Doc.CodeSamples.Collections.Tests
{
    struct ExamplesCollections
    {
        public void foo()
        {
            #region parallel_writer

            NativeList<int> nums = new NativeList<int>(1000, Allocator.TempJob);

            // The parallel writer shares the original list's AtomicSafetyHandle.
            var job = new MyParallelJob {NumsWriter = nums.AsParallelWriter()};

            #endregion
        }

        #region parallel_writer_job

        public struct MyParallelJob : IJobParallelFor
        {
            public NativeList<int>.ParallelWriter NumsWriter;

            public void Execute(int i)
            {
                // A NativeList<T>.ParallelWriter can append values
                // but not grow the capacity of the list.
                NumsWriter.AddNoResize(i);
            }
        }

        #endregion

        public void foo2()
        {
            #region enumerator
            NativeList<int> nums = new NativeList<int>(10, Allocator.Temp);

            // Calculate the sum of all elements in the list.
            int sum = 0;
            var enumerator = nums.GetEnumerator();

            // The first MoveNext call advances the enumerator to the first element.
            // MoveNext returns false when the enumerator has advanced past the last element.
            while (enumerator.MoveNext())
            {
                sum += enumerator.Current;
            }

            // The enumerator is no longer valid to use after the array is disposed.
            nums.Dispose();
            #endregion
        }

        #region read_only
        public struct MyJob : IJob
        {
            // This array can only be read in the job.
            [ReadOnly] public NativeArray<int> nums;

            public void Execute()
            {
                // If safety checks are enabled, an exception is thrown here
                // because the array is read only.
                nums[0] = 100;
            }
        }
        #endregion
    }
}
