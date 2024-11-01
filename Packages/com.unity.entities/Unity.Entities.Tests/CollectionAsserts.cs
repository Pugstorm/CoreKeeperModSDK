using System;
using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Tests
{
    static class CollectionAsserts
    {
        internal unsafe static void CompareSorted<T>(T[] expected, T[] actual) where T: unmanaged, IComparable<T>
        {
            Assert.AreEqual(expected.Length, actual.Length, "Length mismatch");

            fixed (T* expectedPtr = expected)
                NativeSortExtension.Sort(expectedPtr, expected.Length);

            fixed (T* actualPtr = actual)
                NativeSortExtension.Sort(actualPtr, actual.Length);

            for (int i = 0; i != expected.Length; i++)
            {
                if (expected[i].CompareTo(actual[i]) != 0)
                    throw new AssertionException($"Expected: {expected[i]}\nbut was: {actual[i]}\nat index: {i}");
            }
        }
    }
}
