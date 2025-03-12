using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.Tests;

internal class NativeParallelHashMapTestsFixture : CollectionsTestFixture
{
    protected const int hashMapSize = 10 * 1024;

    // Burst error BC1005: The `try` construction is not supported
    // [BurstCompile(CompileSynchronously = true)]
    internal struct HashMapWriteJob : IJob
    {
        public NativeParallelHashMap<int, int>.ParallelWriter hashMap;
        public NativeArray<int> status;
        public int keyMod;

        public void Execute()
        {
            for (int i = 0; i < status.Length; i++)
            {
                status[i] = 0;
                try
                {
                    if (!hashMap.TryAdd(i % keyMod, i))
                    {
                        status[i] = -1;
                    }
                }
                catch (System.InvalidOperationException)
                {
                    status[i] = -2;
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    internal struct HashMapReadParallelForJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeParallelHashMap<int, int> hashMap;
        public NativeArray<int>        values;
        public int                     keyMod;

        public void Execute(int i)
        {
            int iSquared;
            values[i] = -1;

            if (hashMap.TryGetValue(i % keyMod, out iSquared))
            {
                values[i] = iSquared;
            }
        }
    }
}
