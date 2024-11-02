using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Editor;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

// Create the overloads you need here. You can mix the FixedString variants as necessary.
[assembly: RegisterGenericJobType(typeof(SIMDSearch.FindMatchesJob<FixedString64Bytes, FixedString64Bytes>))]
[assembly: RegisterGenericJobType(typeof(SIMDSearch.FindMatchesJob<FixedString128Bytes, FixedString128Bytes>))]

namespace Unity.Entities.Editor
{
    static unsafe class SIMDSearch
    {
        // This exists as a workaround to see the job in the Burst Inspector.
        // See ticket: BUR-1059
        // ReSharper disable once UnusedMember.Local
        static FindMatchesJob<FixedString64Bytes, FixedString64Bytes> __GetReferenceImplementation() => new FindMatchesJob<FixedString64Bytes,FixedString64Bytes>();

        public static NativeBitArray GetFilteredMatches<TSource, TPattern>(
            NativeList<TSource> namesSource,
            NativeList<TPattern> searchPatterns,
            Allocator allocator = Allocator.TempJob)
        where TSource: unmanaged, IUTF8Bytes, INativeList<byte>, IEquatable<TSource>
        where TPattern: unmanaged, IUTF8Bytes, INativeList<byte>, IEquatable<TPattern>
        {
            var matches = new NativeBitArray(namesSource.Length, allocator, NativeArrayOptions.UninitializedMemory);
            var filterJob = new FindMatchesJob<TSource, TPattern>
            {
                MatchesMask = matches,
                Entries = namesSource,
                Patterns = searchPatterns
            };

            // Using batches of size: `CacheLineSize` to avoid false sharing
            filterJob
                .Schedule(namesSource.Length, JobsUtility.CacheLineSize)
                .Complete();

            return matches;
        }

        [BurstCompile(DisableSafetyChecks = true)]
        public struct FindMatchesJob<TSource, TPattern> : IJobParallelFor
            where TSource: unmanaged, IUTF8Bytes, INativeList<byte>, IEquatable<TSource>
            where TPattern: unmanaged, IUTF8Bytes, INativeList<byte>, IEquatable<TPattern>
        {
            [WriteOnly, NativeDisableParallelForRestriction]
            public NativeBitArray MatchesMask;

            [ReadOnly]
            public NativeList<TSource> Entries;

            [ReadOnly]
            public NativeList<TPattern> Patterns;

            public void Execute(int index)
            {
                var entry = Entries[index];
                var patternIndex = 0;
                var matches = true;
                while (matches && patternIndex < Patterns.Length)
                {
                    matches = Contains(entry, Patterns[patternIndex++]);
                }

                MatchesMask.Set(index, matches);
            }

            static bool Contains(TSource source, TPattern pattern)
            {
                // Burst crashes when compiling `if (!X86.Avx2.IsAvx2Supported)` so we have to keep the if as-is
                // ReSharper disable once InvertIf
                if (X86.Avx2.IsAvx2Supported)
                {
                    const int charactersPerBatch = 256 / 8;

                    // Not sure why R# thinks we are loading from a managed type
                    // ReSharper disable Unity.BurstLoadingManagedType
                    var sourceLength = source.Length;
                    var patternLength = pattern.Length;
                    // ReSharper restore Unity.BurstLoadingManagedType

                    if (sourceLength < patternLength)
                        return false;

                    // Amount of bytes to mem compare
                    // -2 because the first and last characters are already confirmed to match if we get there
                    var comparisonLength = (long) math.max(patternLength - 2, 0);

                    var sourcePtr = source.GetUnsafePtr();
                    var patternPtr = pattern.GetUnsafePtr();

                    var firstCharacter = X86.Avx.mm256_set1_epi8(pattern[0]);
                    var lastCharacter = X86.Avx.mm256_set1_epi8(pattern[patternLength - 1]);

                    // Used for boundary checks
                    var remainder = sourceLength;

                    for (var i = 0; i < sourceLength; i += charactersPerBatch)
                    {
                        var firstCharacterComparisonBuffer = X86.Avx.mm256_loadu_si256(sourcePtr + i);
                        var lastCharacterComparisonBuffer = X86.Avx.mm256_loadu_si256(sourcePtr + i + patternLength - 1);

                        var firstCharacterMatches = X86.Avx2.mm256_cmpeq_epi8(firstCharacter, firstCharacterComparisonBuffer);
                        var lastCharacterMatches = X86.Avx2.mm256_cmpeq_epi8(lastCharacter, lastCharacterComparisonBuffer);

                        var union = X86.Avx2.mm256_and_si256(firstCharacterMatches, lastCharacterMatches);

                        // Boundary check:
                        // this ensures we're not matching anything outside of the string itself
                        var remainderMask = remainder >= 32 ? -1 : (1 << remainder) - 1;
                        var mask = X86.Avx2.mm256_movemask_epi8(union) & remainderMask;

                        while (mask != 0)
                        {
                            // Get index of match (aka least significant bit set)
                            var idx = math.tzcnt(mask);

                            if (UnsafeUtility.MemCmp(sourcePtr + i + idx + 1, patternPtr + 1, comparisonLength) == 0)
                                return true;

                            // Clear match at index
                            mask &= ~(1 << idx);
                        }

                        remainder -= charactersPerBatch;
                    }

                    return false;
                }

                // Fallback
                // NOTE: FixedStringXX returns true when comparing against a `default` pattern, but should return false
                // Sorted the checks in order of likeliness to fail to waste as few cycles as possible
                return source.IndexOf(pattern) != -1
                       && !pattern.Equals(default)
                       && !source.Equals(default);
            }
        }
        
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(FixedString64Bytes), typeof(FixedString64Bytes) })]
        internal static bool Contains<TSource, TPattern>(TSource source, TPattern pattern)
            where TSource: unmanaged, IUTF8Bytes, INativeList<byte>, IEquatable<TSource>
            where TPattern: unmanaged, IUTF8Bytes, INativeList<byte>, IEquatable<TPattern>
        {
            // Burst crashes when compiling `if (!X86.Avx2.IsAvx2Supported)` so we have to keep the if as-is
            // ReSharper disable once InvertIf
            if (X86.Avx2.IsAvx2Supported)
            {
                const int charactersPerBatch = 256 / 8;

                // Not sure why R# thinks we are loading from a managed type
                // ReSharper disable Unity.BurstLoadingManagedType
                var sourceLength = source.Length;
                var patternLength = pattern.Length;
                // ReSharper restore Unity.BurstLoadingManagedType

                if (sourceLength < patternLength)
                    return false;

                // Amount of bytes to mem compare
                // -2 because the first and last characters are already confirmed to match if we get there
                var comparisonLength = (long) math.max(patternLength - 2, 0);

                var sourcePtr = source.GetUnsafePtr();
                var patternPtr = pattern.GetUnsafePtr();

                var firstCharacter = X86.Avx.mm256_set1_epi8(pattern[0]);
                var lastCharacter = X86.Avx.mm256_set1_epi8(pattern[patternLength - 1]);

                // Used for boundary checks
                var remainder = sourceLength;

                for (var i = 0; i < sourceLength; i += charactersPerBatch)
                {
                    var firstCharacterComparisonBuffer = X86.Avx.mm256_loadu_si256(sourcePtr + i);
                    var lastCharacterComparisonBuffer = X86.Avx.mm256_loadu_si256(sourcePtr + i + patternLength - 1);

                    var firstCharacterMatches = X86.Avx2.mm256_cmpeq_epi8(firstCharacter, firstCharacterComparisonBuffer);
                    var lastCharacterMatches = X86.Avx2.mm256_cmpeq_epi8(lastCharacter, lastCharacterComparisonBuffer);

                    var union = X86.Avx2.mm256_and_si256(firstCharacterMatches, lastCharacterMatches);

                    // Boundary check:
                    // this ensures we're not matching anything outside of the string itself
                    var remainderMask = remainder >= 32 ? -1 : (1 << remainder) - 1;
                    var mask = X86.Avx2.mm256_movemask_epi8(union) & remainderMask;

                    while (mask != 0)
                    {
                        // Get index of match (aka least significant bit set)
                        var idx = math.tzcnt(mask);

                        if (UnsafeUtility.MemCmp(sourcePtr + i + idx + 1, patternPtr + 1, comparisonLength) == 0)
                            return true;

                        // Clear match at index
                        mask &= ~(1 << idx);
                    }

                    remainder -= charactersPerBatch;
                }

                return false;
            }

            // Fallback
            // NOTE: FixedStringXX returns true when comparing against a `default` pattern, but should return false
            // Sorted the checks in order of likeliness to fail to waste as few cycles as possible
            return source.IndexOf(pattern) != -1
                   && !pattern.Equals(default)
                   && !source.Equals(default);
        }
    }
}
