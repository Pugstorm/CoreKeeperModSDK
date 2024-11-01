using System.Runtime.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Mathematics;

namespace Unity.Entities
{
    /// <summary>
    /// Helper utility to quickly identify the next available entity or component within a chunk (e.g. the index of
    /// the <see cref="NativeArray{T}"/> retrieved via <see cref="ArchetypeChunk.GetComponentDataPtrRW{T}"/> or
    /// <see cref="ArchetypeChunk.GetEntityDataPtrRO"/>)
    /// </summary>
    [GenerateTestsForBurstCompatibility]
    public struct ChunkEntityEnumerator
    {
        private int Iter;
        private ulong mask64_0;
        private ulong mask64_1;
        private readonly bool UseEnabledMask;
        private readonly int ChunkEntityCount;

        /// <summary>
        /// Construct a new instance.
        /// </summary>
        /// <param name="useEnabledMask">If true, <paramref name="chunkEnabledMask"/> contains valid data and should be used
        /// to determine which entities to include in the iteration. If false, the mask is ignored and all entities in the chunk
        /// are included. You can pass this value directly from the argument provided to <see cref="IJobChunk.Execute"/>.</param>
        /// <param name="chunkEnabledMask">A bitmask for all entities in the chunk. If bit N is set, entity N within
        /// this chunk should be included in the iteration. This mask is ignored if <paramref name="useEnabledMask"/>
        /// is false. You can pass this value directly from the argument provided to <see cref="IJobChunk.Execute"/>.</param>
        /// <param name="chunkEntityCount">The number of entities in the chunk.</param>
        public ChunkEntityEnumerator(bool useEnabledMask, v128 chunkEnabledMask, int chunkEntityCount)
        {
            UseEnabledMask = useEnabledMask;
            Iter = 0;
            ChunkEntityCount = chunkEntityCount;
            mask64_0 = chunkEnabledMask.ULong0;
            mask64_1 = chunkEnabledMask.ULong1;
        }

        /// <summary>
        /// Iterates through the given <see cref="ArchetypeChunk"/>, retrieving the index of the next available entity.
        /// This function will pass over any entities whose components implement <see cref="IEnableableComponent"/> and
        /// are currently disabled.
        /// </summary>
        /// <param name="nextIndex">The index of the next available entity in the ArchetypeChunk. when the function
        /// returns false, this result is undefined. </param>
        /// <returns>whether or not there is another available index within the ArchetypeChunk,
        /// based on the last available iteration</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NextEntityIndex(out int nextIndex)
        {
            if (!UseEnabledMask)
            {
                nextIndex = Iter;
                Iter++;
                return Iter <= ChunkEntityCount;
            }


            return NextEntityEnabledMask(out nextIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool NextEntityEnabledMask(out int nextIndex)
        {
            int count = math.min(64,ChunkEntityCount);
            while(Iter < count)
            {
                if ((mask64_0 & 1) != 0)
                {
                    nextIndex = Iter;
                    Iter++;
                    mask64_0 >>= 1;
                    return true;
                }

                mask64_0 >>= 1;
                Iter++;
            }

            while(Iter < ChunkEntityCount)
            {
                if ((mask64_1 & 1) != 0)
                {
                    nextIndex = Iter;
                    Iter++;
                    mask64_1 >>= 1;
                    return true;
                }

                mask64_1 >>= 1;
                Iter++;
            }

            nextIndex = Iter;
            return false;
        }
    }
}
