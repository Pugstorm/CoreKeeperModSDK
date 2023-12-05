using Unity.Collections;
using Unity.Jobs;

namespace Doc.CodeSamples.Collections.Tests
{
    class AliasingExample
    {
        public void foo()
        {
            #region allocation_aliasing
            NativeList<int> nums = new NativeList<int>(10, Allocator.TempJob);
            nums.Length = 5;

            // Create an array of 5 ints that aliases the content of the list.
            NativeArray<int> aliasedNums = nums.AsArray();

            // Modify the first element of both the array and the list.
            aliasedNums[0] = 99;

            // Only the original need be disposed.
            nums.Dispose();

            // Throws an ObjectDisposedException because disposing
            // the original deallocates the aliased memory.
            aliasedNums[0] = 99;
            #endregion
        }

        public void foo2()
        {
            #region allocation_reinterpretation
            NativeArray<int> ints = new NativeArray<int>(10, Allocator.Temp);

            // Length of the reinterpreted array is 20
            // (because it has two shorts per one int of the original).
            NativeArray<short> shorts = ints.Reinterpret<int, short>();

            // Modifies the first 4 bytes of the array.
            shorts[0] = 1;
            shorts[1] = 1;

            int val = ints[0];   // val is 65537 (2^16 + 2^0)

            // Like with other aliased collections, only the original
            // needs to be disposed.
            ints.Dispose();

            // Throws an ObjectDisposedException because disposing
            // the original deallocates the aliased memory.
            shorts[0] = 1;
            #endregion
        }

        public void foo3()
        {
            #region allocation_dispose_job
            NativeArray<int> nums = new NativeArray<int>(10, Allocator.TempJob);

            // Create and schedule a job that uses the array.
            ExampleJob job = new ExampleJob { Nums = nums };
            JobHandle handle = job.Schedule();

            // Create and schedule a job that will dispose the array after the ExampleJob has run.
            // Returns the handle of the new job.
            handle = nums.Dispose(handle);
            #endregion
        }
    }

    struct ExampleJob : IJob
    {
        public NativeArray<int> Nums;
        public void Execute()
        {
            throw new System.NotImplementedException();
        }
    }

}
