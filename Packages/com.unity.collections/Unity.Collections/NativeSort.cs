using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.Collections
{
    /// <summary>
    /// Extension methods for sorting collections.
    /// </summary>
    [GenerateTestsForBurstCompatibility]
    public static class NativeSortExtension
    {
        /// <summary>
        /// A comparer that uses IComparable.CompareTo(). For primitive types, this is an ascending sort.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
        public struct DefaultComparer<T> : IComparer<T> where T : IComparable<T>
        {
            /// <summary>
            /// Compares two values.
            /// </summary>
            /// <param name="x">First value to compare.</param>
            /// <param name="y">Second value to compare.</param>
            /// <returns>A signed integer that denotes the relative values of `x` and `y`:
            /// 0 if they're equal, negative if `x &lt; y`, and positive if `x &gt; y`.</returns>
            public int Compare(T x, T y) => x.CompareTo(y);
        }

        /// <summary>
        /// Sorts an array in ascending order.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="array">The array to sort.</param>
        /// <param name="length">The number of elements to sort in the array.
        /// Indexes greater than or equal to `length` won't be included in the sort.</param>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe static void Sort<T>(T* array, int length)
            where T : unmanaged, IComparable<T>
        {
            IntroSort<T, DefaultComparer<T>>(array, length, new DefaultComparer<T>());
        }

        /// <summary>
        /// Sorts an array using a custom comparison.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <typeparam name="U">The type of the comparer.</typeparam>
        /// <param name="array">The array to sort.</param>
        /// <param name="length">The number of elements to sort in the array.
        /// Indexes greater than or equal to `length` won't be included in the sort.</param>
        /// <param name="comp">The comparison function used to determine the relative order of the elements.</param>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static void Sort<T, U>(T* array, int length, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            IntroSort<T, U>(array, length, comp);
        }

        /// <summary>
        /// Returns a job which will sort an array in ascending order.
        /// </summary>
        /// <remarks>This method does not schedule the job. Scheduling the job is left to you.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="array">The array to sort.</param>
        /// <param name="length">The number of elements to sort in the array.
        /// Indexes greater than or equal to `length` won't be included in the sort.</param>
        /// <returns>A job for sorting the array.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) }, RequiredUnityDefine = "UNITY_2020_2_OR_NEWER" /* Due to job scheduling on 2020.1 using statics */)]
        public unsafe static SortJob<T, DefaultComparer<T>> SortJob<T>(T* array, int length)
            where T : unmanaged, IComparable<T>
        {
            return new SortJob<T, DefaultComparer<T>> {Data = array, Length = length, Comp = new DefaultComparer<T>()};
        }

        /// <summary>
        /// Returns a job which will sort an array using a custom comparison.
        /// </summary>
        /// <remarks>This method does not schedule the job. Scheduling the job is left to you.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <typeparam name="U">The type of the comparer.</typeparam>
        /// <param name="array">The array to sort.</param>
        /// <param name="length">The number of elements to sort in the array.
        /// Indexes greater than or equal to `length` won't be included in the sort.</param>
        /// <param name="comp">The comparison function used to determine the relative order of the elements.</param>
        /// <returns>A job for sorting the array.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) }, RequiredUnityDefine = "UNITY_2020_2_OR_NEWER" /* Due to job scheduling on 2020.1 using statics */)]
        public unsafe static SortJob<T, U> SortJob<T, U>(T* array, int length, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            CheckComparer(array, length, comp);
            return new SortJob<T, U>() {Data = array, Length = length, Comp = comp};
        }

        /// <summary>
        /// Finds a value in a sorted array by binary search.
        /// </summary>
        /// <remarks>If the array is not sorted, the value might not be found, even if it's present in the array.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="ptr">The array to search.</param>
        /// <param name="value">The value to locate.</param>
        /// <param name="length">The number of elements to search. Indexes greater than or equal to `length` won't be searched.</param>
        /// <returns>If found, the index of the located value. If not found, the return value is negative.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe static int BinarySearch<T>(T* ptr, int length, T value)
            where T : unmanaged, IComparable<T>
        {
            return BinarySearch(ptr, length, value, new DefaultComparer<T>());
        }

        /// <summary>
        /// Finds a value in a sorted array by binary search using a custom comparison.
        /// </summary>
        /// <remarks>If the array is not sorted, the value might not be found, even if it's present in the array.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <typeparam name="U">The type of the comparer.</typeparam>
        /// <param name="ptr">The array to search.</param>
        /// <param name="value">The value to locate.</param>
        /// <param name="length">The number of elements to search. Indexes greater than or equal to `length` won't be searched.</param>
        /// <param name="comp">The comparison function used to determine the relative order of the elements.</param>
        /// <returns>If found, the index of the located value. If not found, the return value is negative.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static int BinarySearch<T, U>(T* ptr, int length, T value, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            CheckComparer(ptr, length, comp);
            var offset = 0;

            for (var l = length; l != 0; l >>= 1)
            {
                var idx = offset + (l >> 1);
                var curr = ptr[idx];
                var r = comp.Compare(value, curr);
                if (r == 0)
                {
                    return idx;
                }

                if (r > 0)
                {
                    offset = idx + 1;
                    --l;
                }
            }

            return ~offset;
        }

        /// <summary>
        /// Sorts this array in ascending order.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="array">The array to sort.</param>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe static void Sort<T>(this NativeArray<T> array)
            where T : unmanaged, IComparable<T>
        {
            IntroSortStruct<T, DefaultComparer<T>>(array.GetUnsafePtr(), array.Length, new DefaultComparer<T>());
        }

        /// <summary>
        /// Sorts this array using a custom comparison.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <typeparam name="U">The type of the comparer.</typeparam>
        /// <param name="array">The array to sort.</param>
        /// <param name="comp">The comparison function used to determine the relative order of the elements.</param>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static void Sort<T, U>(this NativeArray<T> array, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            var ptr = (T*)array.GetUnsafePtr();
            var len = array.Length;
            CheckComparer(ptr, len, comp);
            IntroSortStruct<T, U>(ptr, len, comp);
        }

        /// <summary>
        /// Returns a job which will sort this array in ascending order.
        /// </summary>
        /// <remarks>This method does not schedule the job. Scheduling the job is left to you.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="array">The array to sort.</param>
        /// <returns>A job for sorting this array.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) }, RequiredUnityDefine = "UNITY_2020_2_OR_NEWER" /* Due to job scheduling on 2020.1 using statics */)]
        public unsafe static SortJob<T, DefaultComparer<T>> SortJob<T>(this NativeArray<T> array)
            where T : unmanaged, IComparable<T>
        {
            return SortJob((T*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array), array.Length, new DefaultComparer<T>());
        }

        /// <summary>
        /// Returns a job which will sort this array using a custom comparison.
        /// </summary>
        /// <remarks>This method does not schedule the job. Scheduling the job is left to you.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <typeparam name="U">The type of the comparer.</typeparam>
        /// <param name="array">The array to sort.</param>
        /// <param name="comp">The comparison function used to determine the relative order of the elements.</param>
        /// <returns>A job for sorting the array.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) }, RequiredUnityDefine = "UNITY_2020_2_OR_NEWER" /* Due to job scheduling on 2020.1 using statics */)]
        public unsafe static SortJob<T, U> SortJob<T, U>(this NativeArray<T> array, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            var ptr = (T*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array);
            var len = array.Length;
            CheckComparer(ptr, len, comp);

            return new SortJob<T, U>
            {
                Data = ptr,
                Length = len,
                Comp = comp
            };
        }

        /// <summary>
        /// Finds a value in this sorted array by binary search.
        /// </summary>
        /// <remarks>If the array is not sorted, the value might not be found, even if it's present in this array.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="array">The array to search.</param>
        /// <param name="value">The value to locate.</param>
        /// <returns>If found, the index of the located value. If not found, the return value is negative.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
        public static int BinarySearch<T>(this NativeArray<T> array, T value)
            where T : unmanaged, IComparable<T>
        {
            return array.BinarySearch(value, new DefaultComparer<T>());
        }

        /// <summary>
        /// Finds a value in this sorted array by binary search using a custom comparison.
        /// </summary>
        /// <remarks>If the array is not sorted, the value might not be found, even if it's present in this array.
        /// </remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="array">The array to search.</param>
        /// <param name="value">The value to locate.</param>
        /// <param name="comp">The comparison function used to determine the relative order of the elements.</param>
        /// <returns>If found, the index of the located value. If not found, the return value is negative.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static int BinarySearch<T, U>(this NativeArray<T> array, T value, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            return BinarySearch((T*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array), array.Length, value, comp);
        }

        /// <summary>
        /// Finds a value in this sorted array by binary search.
        /// </summary>
        /// <remarks>If the array is not sorted, the value might not be found, even if it's present in this array.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="array">The array to search.</param>
        /// <param name="value">The value to locate.</param>
        /// <returns>If found, the index of the located value. If not found, the return value is negative.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
        public static int BinarySearch<T>(this NativeArray<T>.ReadOnly array, T value)
            where T : unmanaged, IComparable<T>
        {
            return array.BinarySearch(value, new DefaultComparer<T>());
        }

        /// <summary>
        /// Finds a value in this sorted array by binary search using a custom comparison.
        /// </summary>
        /// <remarks>If the array is not sorted, the value might not be found, even if it's present in this array.
        /// </remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="array">The array to search.</param>
        /// <param name="value">The value to locate.</param>
        /// <param name="comp">The comparison function used to determine the relative order of the elements.</param>
        /// <returns>If found, the index of the located value. If not found, the return value is negative.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static int BinarySearch<T, U>(this NativeArray<T>.ReadOnly array, T value, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            return BinarySearch((T*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array), array.Length, value, comp);
        }

        /// <summary>
        /// Sorts this list in ascending order.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="list">The list to sort.</param>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe static void Sort<T>(this NativeList<T> list)
            where T : unmanaged, IComparable<T>
        {
            list.Sort(new DefaultComparer<T>());
        }

        /// <summary>
        /// Sorts this list using a custom comparison.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <typeparam name="U">The type of the comparer.</typeparam>
        /// <param name="list">The list to sort.</param>
        /// <param name="comp">The comparison function used to determine the relative order of the elements.</param>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static void Sort<T, U>(this NativeList<T> list, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            IntroSort<T, U>(list.GetUnsafePtr(), list.Length, comp);
        }

        /// <summary>
        /// Returns a job which will sort this list in ascending order.
        /// </summary>
        /// <remarks>This method does not schedule the job. Scheduling the job is left to you.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="list">The list to sort.</param>
        /// <returns>A job for sorting this list.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) }, RequiredUnityDefine = "UNITY_2020_2_OR_NEWER" /* Due to job scheduling on 2020.1 using statics */)]
        public unsafe static SortJob<T, DefaultComparer<T>> SortJob<T>(this NativeList<T> list)
            where T : unmanaged, IComparable<T>
        {
            return SortJob(list.GetUnsafePtr(), list.Length,new DefaultComparer<T>());
        }

        /// <summary>
        /// Returns a job which will sort this list using a custom comparison.
        /// </summary>
        /// <remarks>This method does not schedule the job. Scheduling the job is left to you.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <typeparam name="U">The type of the comparer.</typeparam>
        /// <param name="list">The list to sort.</param>
        /// <param name="comp">The comparison function used to determine the relative order of the elements.</param>
        /// <returns>A job for sorting this list.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) }, RequiredUnityDefine = "UNITY_2020_2_OR_NEWER" /* Due to job scheduling on 2020.1 using statics */)]
        public unsafe static SortJob<T, U> SortJob<T, U>(this NativeList<T> list, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            return SortJob(list.GetUnsafePtr(), list.Length, comp);
        }

        /// <summary>
        /// Finds a value in this sorted list by binary search.
        /// </summary>
        /// <remarks>If this list is not sorted, the value might not be found, even if it's present in this list.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="list">The list to search.</param>
        /// <param name="value">The value to locate.</param>
        /// <returns>If found, the index of the located value. If not found, the return value is negative.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
        public static int BinarySearch<T>(this NativeList<T> list, T value)
            where T : unmanaged, IComparable<T>
        {
            return list.AsReadOnly().BinarySearch(value, new DefaultComparer<T>());
        }

        /// <summary>
        /// Finds a value in this sorted list by binary search using a custom comparison.
        /// </summary>
        /// <remarks>If this list is not sorted, the value may not be found, even if it's present in this list.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <typeparam name="U">The type of the comparer.</typeparam>
        /// <param name="list">The list to search.</param>
        /// <param name="value">The value to locate.</param>
        /// <param name="comp">The comparison function used to determine the relative order of the elements.</param>
        /// <returns>If found, the index of the located value. If not found, the return value is negative.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static int BinarySearch<T, U>(this NativeList<T> list, T value, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            return list.AsReadOnly().BinarySearch(value, comp);
        }

        /// <summary>
        /// Sorts this list in ascending order.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="list">The list to sort.</param>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe static void Sort<T>(this UnsafeList<T> list) where T : unmanaged, IComparable<T>
        {
            list.Sort(new DefaultComparer<T>());
        }

        /// <summary>
        /// Sorts the list using a custom comparison.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <typeparam name="U">The type of the comparer.</typeparam>
        /// <param name="list">The list to sort.</param>
        /// <param name="comp">The comparison function used to determine the relative order of the elements.</param>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static void Sort<T, U>(this UnsafeList<T> list, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            IntroSort<T, U>(list.Ptr, list.Length, comp);
        }

        /// <summary>
        /// Returns a job which will sort this list in ascending order.
        /// </summary>
        /// <remarks>This method does not schedule the job. Scheduling the job is left to you.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="list">The list to sort.</param>
        /// <returns>A job for sorting this list.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) }, RequiredUnityDefine = "UNITY_2020_2_OR_NEWER" /* Due to job scheduling on 2020.1 using statics */)]
        public unsafe static SortJob<T, DefaultComparer<T>> SortJob<T>(this UnsafeList<T> list)
            where T : unmanaged, IComparable<T>
        {
            return SortJob(list.Ptr, list.Length, new DefaultComparer<T>());
        }

        /// <summary>
        /// Returns a job which will sort this list using a custom comparison.
        /// </summary>
        /// <remarks>This method does not schedule the job. Scheduling the job is left to you.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <typeparam name="U">The type of the comparer.</typeparam>
        /// <param name="list">The list to sort.</param>
        /// <param name="comp">The comparison function used to determine the relative order of the elements.</param>
        /// <returns>A job for sorting this list.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) }, RequiredUnityDefine = "UNITY_2020_2_OR_NEWER" /* Due to job scheduling on 2020.1 using statics */)]
        public unsafe static SortJob<T, U> SortJob<T, U>(this UnsafeList<T> list, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            return SortJob(list.Ptr, list.Length, comp);
        }

        /// <summary>
        /// Finds a value in this sorted list by binary search.
        /// </summary>
        /// <remarks>If this list is not sorted, the value might not be found, even if it's present in this list.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="list">The list to search.</param>
        /// <param name="value">The value to locate.</param>
        /// <returns>If found, the index of the located value. If not found, the return value is negative.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
        public static int BinarySearch<T>(this UnsafeList<T> list, T value)
            where T : unmanaged, IComparable<T>
        {
            return list.BinarySearch(value, new DefaultComparer<T>());
        }

        /// <summary>
        /// Finds a value in this sorted list by binary search using a custom comparison.
        /// </summary>
        /// <remarks>If this list is not sorted, the value might not be found, even if it's present in this list.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <typeparam name="U">The type of the comparer.</typeparam>
        /// <param name="list">The list to search.</param>
        /// <param name="value">The value to locate.</param>
        /// <param name="comp">The comparison function used to determine the relative order of the elements.</param>
        /// <returns>If found, the index of the located value. If not found, the return value is negative.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static int BinarySearch<T, U>(this UnsafeList<T> list, T value, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            return BinarySearch(list.Ptr, list.Length, value, comp);
        }

        /// <summary>
        /// Sorts this slice in ascending order.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="slice">The slice to sort.</param>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe static void Sort<T>(this NativeSlice<T> slice)
            where T : unmanaged, IComparable<T>
        {
            slice.Sort(new DefaultComparer<T>());
        }

        /// <summary>
        /// Sorts this slice using a custom comparison.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <typeparam name="U">The type of the comparer.</typeparam>
        /// <param name="slice">The slice to sort.</param>
        /// <param name="comp">The comparison function used to determine the relative order of the elements.</param>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static void Sort<T, U>(this NativeSlice<T> slice, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            var ptr = (T*)slice.GetUnsafePtr();
            var len = slice.Length;
            CheckComparer(ptr, len, comp);

            CheckStrideMatchesSize<T>(slice.Stride);
            IntroSortStruct<T, U>(ptr, len, comp);
        }

        /// <summary>
        /// Returns a job which will sort this slice in ascending order.
        /// </summary>
        /// <remarks>This method does not schedule the job. Scheduling the job is left to you.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="slice">The slice to sort.</param>
        /// <returns>A job for sorting this slice.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) }, RequiredUnityDefine = "UNITY_2020_2_OR_NEWER" /* Due to job scheduling on 2020.1 using statics */)]
        public unsafe static SortJob<T, DefaultComparer<T>> SortJob<T>(this NativeSlice<T> slice)
            where T : unmanaged, IComparable<T>
        {
            CheckStrideMatchesSize<T>(slice.Stride);
            return SortJob((T*)slice.GetUnsafePtr(), slice.Length, new DefaultComparer<T>());
        }

        /// <summary>
        /// Returns a job which will sort this slice using a custom comparison.
        /// </summary>
        /// <remarks>This method does not schedule the job. Scheduling the job is left to you.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <typeparam name="U">The type of the comparer.</typeparam>
        /// <param name="slice">The slice to sort.</param>
        /// <param name="comp">The comparison function used to determine the relative order of the elements.</param>
        /// <returns>A job for sorting this slice.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) }, RequiredUnityDefine = "UNITY_2020_2_OR_NEWER" /* Due to job scheduling on 2020.1 using statics */)]
        public unsafe static SortJob<T, U> SortJob<T, U>(this NativeSlice<T> slice, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            CheckStrideMatchesSize<T>(slice.Stride);
            return SortJob((T*)slice.GetUnsafePtr(), slice.Length, comp);
        }

        /// <summary>
        /// Finds a value in this sorted slice by binary search.
        /// </summary>
        /// <remarks>If this slice is not sorted, the value might not be found, even if it's present in this slice.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="slice">The slice to search.</param>
        /// <param name="value">The value to locate.</param>
        /// <returns>If found, the index of the located value. If not found, the return value is negative.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
        public static int BinarySearch<T>(this NativeSlice<T> slice, T value)
            where T : unmanaged, IComparable<T>
        {
            return slice.BinarySearch(value, new DefaultComparer<T>());
        }

        /// <summary>
        /// Finds a value in this sorted slice by binary search using a custom comparison.
        /// </summary>
        /// <remarks>If this slice is not sorted, the value might not be found, even if it's present in this slice.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <typeparam name="U">The type of the comparer.</typeparam>
        /// <param name="slice">The slice to search.</param>
        /// <param name="value">The value to locate.</param>
        /// <param name="comp">The comparison function used to determine the relative order of the elements.</param>
        /// <returns>If found, the index of the located value. If not found, the return value is negative.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static int BinarySearch<T, U>(this NativeSlice<T> slice, T value, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            return BinarySearch((T*)slice.GetUnsafeReadOnlyPtr(), slice.Length, value, comp);
        }

        /// -- Internals

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        unsafe internal static void IntroSort<T, U>(void* array, int length, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            CheckComparer((T*)array, length, comp);
            IntroSort_R<T, U>(array, 0, length - 1, 2 * CollectionHelper.Log2Floor(length), comp);
        }

        const int k_IntrosortSizeThreshold = 16;

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        unsafe internal static void IntroSort_R<T, U>(void* array, int lo, int hi, int depth, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            while (hi > lo)
            {
                int partitionSize = hi - lo + 1;
                if (partitionSize <= k_IntrosortSizeThreshold)
                {
                    if (partitionSize == 1)
                    {
                        return;
                    }
                    if (partitionSize == 2)
                    {
                        SwapIfGreaterWithItems<T, U>(array, lo, hi, comp);
                        return;
                    }
                    if (partitionSize == 3)
                    {
                        SwapIfGreaterWithItems<T, U>(array, lo, hi - 1, comp);
                        SwapIfGreaterWithItems<T, U>(array, lo, hi, comp);
                        SwapIfGreaterWithItems<T, U>(array, hi - 1, hi, comp);
                        return;
                    }

                    InsertionSort<T, U>(array, lo, hi, comp);
                    return;
                }

                if (depth == 0)
                {
                    HeapSort<T, U>(array, lo, hi, comp);
                    return;
                }
                depth--;

                int p = Partition<T, U>(array, lo, hi, comp);
                IntroSort_R<T, U>(array, p + 1, hi, depth, comp);
                hi = p - 1;
            }
        }

        unsafe static void InsertionSort<T, U>(void* array, int lo, int hi, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            int i, j;
            T t;
            for (i = lo; i < hi; i++)
            {
                j = i;

                t = UnsafeUtility.ReadArrayElement<T>(array, i + 1);
                while (j >= lo && comp.Compare(t, UnsafeUtility.ReadArrayElement<T>(array, j)) < 0)
                {
                    UnsafeUtility.WriteArrayElement(array, j + 1, UnsafeUtility.ReadArrayElement<T>(array, j));
                    j--;
                }

                UnsafeUtility.WriteArrayElement(array, j + 1, t);
            }
        }

        unsafe static int Partition<T, U>(void* array, int lo, int hi, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            int mid = lo + ((hi - lo) / 2);
            SwapIfGreaterWithItems<T, U>(array, lo, mid, comp);
            SwapIfGreaterWithItems<T, U>(array, lo, hi, comp);
            SwapIfGreaterWithItems<T, U>(array, mid, hi, comp);

            T pivot = UnsafeUtility.ReadArrayElement<T>(array, mid);
            Swap<T>(array, mid, hi - 1);
            int left = lo, right = hi - 1;

            while (left < right)
            {
                while (left < hi && comp.Compare(pivot, UnsafeUtility.ReadArrayElement<T>(array, ++left)) > 0)
                {
                }

                while (right > left && comp.Compare(pivot, UnsafeUtility.ReadArrayElement<T>(array, --right)) < 0)
                {
                }

                if (left >= right)
                    break;

                Swap<T>(array, left, right);
            }

            Swap<T>(array, left, (hi - 1));
            return left;
        }

        unsafe static void HeapSort<T, U>(void* array, int lo, int hi, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            int n = hi - lo + 1;

            for (int i = n / 2; i >= 1; i--)
            {
                Heapify<T, U>(array, i, n, lo, comp);
            }

            for (int i = n; i > 1; i--)
            {
                Swap<T>(array, lo, lo + i - 1);
                Heapify<T, U>(array, 1, i - 1, lo, comp);
            }
        }

        unsafe static void Heapify<T, U>(void* array, int i, int n, int lo, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            T val = UnsafeUtility.ReadArrayElement<T>(array, lo + i - 1);
            int child;
            while (i <= n / 2)
            {
                child = 2 * i;

                if (child < n && (comp.Compare(UnsafeUtility.ReadArrayElement<T>(array, lo + child - 1), UnsafeUtility.ReadArrayElement<T>(array, (lo + child))) < 0))
                {
                    child++;
                }

                if (comp.Compare(UnsafeUtility.ReadArrayElement<T>(array, (lo + child - 1)), val) < 0)
                {
                    break;
                }

                UnsafeUtility.WriteArrayElement(array, lo + i - 1, UnsafeUtility.ReadArrayElement<T>(array, lo + child - 1));
                i = child;
            }

            UnsafeUtility.WriteArrayElement(array, lo + i - 1, val);
        }

        unsafe static void Swap<T>(void* array, int lhs, int rhs) where T : unmanaged
        {
            T val = UnsafeUtility.ReadArrayElement<T>(array, lhs);
            UnsafeUtility.WriteArrayElement(array, lhs, UnsafeUtility.ReadArrayElement<T>(array, rhs));
            UnsafeUtility.WriteArrayElement(array, rhs, val);
        }

        unsafe static void SwapIfGreaterWithItems<T, U>(void* array, int lhs, int rhs, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            if (lhs != rhs)
            {
                if (comp.Compare(UnsafeUtility.ReadArrayElement<T>(array, lhs), UnsafeUtility.ReadArrayElement<T>(array, rhs)) > 0)
                {
                    Swap<T>(array, lhs, rhs);
                }
            }
        }

        unsafe static void IntroSortStruct<T, U>(void* array, int length, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            IntroSortStruct_R<T, U>(array, 0, length - 1, 2 * CollectionHelper.Log2Floor(length), comp);
        }

        unsafe static void IntroSortStruct_R<T, U>(void* array, in int lo, in int _hi, int depth, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            var hi = _hi;

            while (hi > lo)
            {
                int partitionSize = hi - lo + 1;
                if (partitionSize <= k_IntrosortSizeThreshold)
                {
                    if (partitionSize == 1)
                    {
                        return;
                    }
                    if (partitionSize == 2)
                    {
                        SwapIfGreaterWithItemsStruct<T, U>(array, lo, hi, comp);
                        return;
                    }
                    if (partitionSize == 3)
                    {
                        SwapIfGreaterWithItemsStruct<T, U>(array, lo, hi - 1, comp);
                        SwapIfGreaterWithItemsStruct<T, U>(array, lo, hi, comp);
                        SwapIfGreaterWithItemsStruct<T, U>(array, hi - 1, hi, comp);
                        return;
                    }

                    InsertionSortStruct<T, U>(array, lo, hi, comp);
                    return;
                }

                if (depth == 0)
                {
                    HeapSortStruct<T, U>(array, lo, hi, comp);
                    return;
                }
                depth--;

                int p = PartitionStruct<T, U>(array, lo, hi, comp);
                IntroSortStruct_R<T, U>(array, p + 1, hi, depth, comp);
                hi = p - 1;
            }
        }

        unsafe static void InsertionSortStruct<T, U>(void* array, in int lo, in int hi, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            int i, j;
            T t;
            for (i = lo; i < hi; i++)
            {
                j = i;
                t = UnsafeUtility.ReadArrayElement<T>(array, i + 1);
                while (j >= lo && comp.Compare(t, UnsafeUtility.ReadArrayElement<T>(array, j)) < 0)
                {
                    UnsafeUtility.WriteArrayElement(array, j + 1, UnsafeUtility.ReadArrayElement<T>(array, j));
                    j--;
                }
                UnsafeUtility.WriteArrayElement(array, j + 1, t);
            }
        }

        unsafe static int PartitionStruct<T, U>(void* array, in int lo, in int hi, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            int mid = lo + ((hi - lo) / 2);
            SwapIfGreaterWithItemsStruct<T, U>(array, lo, mid, comp);
            SwapIfGreaterWithItemsStruct<T, U>(array, lo, hi, comp);
            SwapIfGreaterWithItemsStruct<T, U>(array, mid, hi, comp);

            T pivot = UnsafeUtility.ReadArrayElement<T>(array, mid);
            SwapStruct<T>(array, mid, hi - 1);
            int left = lo, right = hi - 1;

            while (left < right)
            {
                while (left < hi && comp.Compare(pivot, UnsafeUtility.ReadArrayElement<T>(array, ++left)) > 0)
                {
                }

                while (right > left && comp.Compare(pivot, UnsafeUtility.ReadArrayElement<T>(array, --right)) < 0)
                {
                }

                if (left >= right)
                    break;

                SwapStruct<T>(array, left, right);
            }

            SwapStruct<T>(array, left, (hi - 1));
            return left;
        }

        unsafe static void HeapSortStruct<T, U>(void* array, in int lo, in int hi, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            int n = hi - lo + 1;

            for (int i = n / 2; i >= 1; i--)
            {
                HeapifyStruct<T, U>(array, i, n, lo, comp);
            }

            for (int i = n; i > 1; i--)
            {
                SwapStruct<T>(array, lo, lo + i - 1);
                HeapifyStruct<T, U>(array, 1, i - 1, lo, comp);
            }
        }

        unsafe static void HeapifyStruct<T, U>(void* array, int i, int n, in int lo, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            T val = UnsafeUtility.ReadArrayElement<T>(array, lo + i - 1);
            int child;
            while (i <= n / 2)
            {
                child = 2 * i;

                if (child < n && (comp.Compare(UnsafeUtility.ReadArrayElement<T>(array, lo + child - 1), UnsafeUtility.ReadArrayElement<T>(array, (lo + child))) < 0))
                {
                    child++;
                }

                if (comp.Compare(UnsafeUtility.ReadArrayElement<T>(array, (lo + child - 1)), val) < 0)
                {
                    break;
                }

                UnsafeUtility.WriteArrayElement(array, lo + i - 1, UnsafeUtility.ReadArrayElement<T>(array, lo + child - 1));
                i = child;
            }

            UnsafeUtility.WriteArrayElement(array, lo + i - 1, val);
        }

        unsafe static void SwapStruct<T>(void* array, int lhs, int rhs)
            where T : unmanaged
        {
            T val = UnsafeUtility.ReadArrayElement<T>(array, lhs);
            UnsafeUtility.WriteArrayElement(array, lhs, UnsafeUtility.ReadArrayElement<T>(array, rhs));
            UnsafeUtility.WriteArrayElement(array, rhs, val);
        }

        unsafe static void SwapIfGreaterWithItemsStruct<T, U>(void* array, int lhs, int rhs, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            if (lhs != rhs)
            {
                if (comp.Compare(UnsafeUtility.ReadArrayElement<T>(array, lhs), UnsafeUtility.ReadArrayElement<T>(array, rhs)) > 0)
                {
                    SwapStruct<T>(array, lhs, rhs);
                }
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        static void CheckStrideMatchesSize<T>(int stride) where T : unmanaged
        {
            if (stride != UnsafeUtility.SizeOf<T>())
            {
                throw new InvalidOperationException("Sort requires that stride matches the size of the source type");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        unsafe static void CheckComparer<T, U>(T* array, int length, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            if (length > 0)
            {
                T a = array[0];

                if (0 != comp.Compare(a, a))
                {
                    throw new InvalidOperationException("Comparison function is incorrect. Compare(a, a) must return 0/equal.");
                }

                for (int i = 1, len = math.min(length, 8); i < len; ++i)
                {
                    T b = array[i];

                    if (0 == comp.Compare(a, b) &&
                        0 == comp.Compare(b, a))
                    {
                        continue;
                    }

                    if (0 == comp.Compare(a, b))
                    {
                        throw new InvalidOperationException("Comparison function is incorrect. Compare(a, b) of two different values should not return 0/equal.");
                    }

                    if (0 == comp.Compare(b, a))
                    {
                        throw new InvalidOperationException("Comparison function is incorrect. Compare(b, a) of two different values should not return 0/equal.");
                    }

                    if (comp.Compare(a, b) == comp.Compare(b, a))
                    {
                        throw new InvalidOperationException("Comparison function is incorrect. Compare(a, b) when a and b are different values should not return the same value as Compare(b, a).");
                    }

                    break;
                }
            }
        }
    }

    /// <summary>
    /// Returned by the `SortJob` methods of <see cref="Unity.Collections.NativeSortExtension"/>. Call `Schedule` to schedule the sorting.
    /// </summary>
    /// <remarks>
    /// When `RegisterGenericJobType` is used on SortJob, to complete registration you must register `SortJob&lt;T,U&gt;.SegmentSort` and `SortJob&lt;T,U&gt;.SegmentSortMerge`.
    /// </remarks>
    /// <typeparam name="T">The type of the elements to sort.</typeparam>
    /// <typeparam name="U">The type of the comparer.</typeparam>
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(NativeSortExtension.DefaultComparer<int>) }, RequiredUnityDefine = "UNITY_2020_2_OR_NEWER" /* Due to job scheduling on 2020.1 using statics */)]
    public unsafe struct SortJob<T, U>
        where T : unmanaged
        where U : IComparer<T>
    {
        /// <summary>
        /// The data to sort.
        /// </summary>
        public T* Data;

        /// <summary>
        /// Comparison function.
        /// </summary>
        public U Comp;

        /// <summary>
        /// The length to sort.
        /// </summary>
        public int Length;

        /// <summary>
        /// <undoc />
        /// </summary>
        [BurstCompile]
        public struct SegmentSort : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            internal T* Data;
            internal U Comp;

            internal int Length;
            internal int SegmentWidth;

            /// <summary>
            /// <undoc />
            /// </summary>
            /// <param name="index"><undoc /></param>
            public void Execute(int index)
            {
                var startIndex = index * SegmentWidth;
                var segmentLength = ((Length - startIndex) < SegmentWidth) ? (Length - startIndex) : SegmentWidth;
                NativeSortExtension.Sort(Data + startIndex, segmentLength, Comp);
            }
        }

        /// <summary>
        /// <undoc />
        /// </summary>
        [BurstCompile]
        public struct SegmentSortMerge : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            internal T* Data;
            internal U Comp;

            internal int Length;
            internal int SegmentWidth;

            /// <summary>
            /// <undoc />
            /// </summary>
            public void Execute()
            {
                var segmentCount = (Length + (SegmentWidth - 1)) / SegmentWidth;
                var segmentIndex = stackalloc int[segmentCount];

                var resultCopy = (T*)Memory.Unmanaged.Allocate(UnsafeUtility.SizeOf<T>() * Length, 16, Allocator.Temp);

                for (int sortIndex = 0; sortIndex < Length; sortIndex++)
                {
                    // find next best
                    int bestSegmentIndex = -1;
                    T bestValue = default(T);

                    for (int i = 0; i < segmentCount; i++)
                    {
                        var startIndex = i * SegmentWidth;
                        var offset = segmentIndex[i];
                        var segmentLength = ((Length - startIndex) < SegmentWidth) ? (Length - startIndex) : SegmentWidth;
                        if (offset == segmentLength)
                            continue;

                        var nextValue = Data[startIndex + offset];
                        if (bestSegmentIndex != -1)
                        {
                            if (Comp.Compare(nextValue, bestValue) > 0)
                                continue;
                        }

                        bestValue = nextValue;
                        bestSegmentIndex = i;
                    }

                    segmentIndex[bestSegmentIndex]++;
                    resultCopy[sortIndex] = bestValue;
                }

                UnsafeUtility.MemCpy(Data, resultCopy, UnsafeUtility.SizeOf<T>() * Length);
            }
        }

        /// <summary>
        /// Schedules this job.
        /// </summary>
        /// <param name="inputDeps">Handle of a job to depend upon.</param>
        /// <returns>The handle of this newly scheduled job.</returns>
        public JobHandle Schedule(JobHandle inputDeps = default)
        {
            if (Length == 0)
                return inputDeps;
            var segmentCount = (Length + 1023) / 1024;

#if UNITY_2022_2_14F1_OR_NEWER
            int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
            int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
            var workerCount = math.max(1, maxThreadCount);
            var workerSegmentCount = segmentCount / workerCount;
            var segmentSortJob = new SegmentSort { Data = Data, Comp = Comp, Length = Length, SegmentWidth = 1024 };
            var segmentSortJobHandle = segmentSortJob.Schedule(segmentCount, workerSegmentCount, inputDeps);
            var segmentSortMergeJob = new SegmentSortMerge { Data = Data, Comp = Comp, Length = Length, SegmentWidth = 1024 };
            var segmentSortMergeJobHandle = segmentSortMergeJob.Schedule(segmentSortJobHandle);
            return segmentSortMergeJobHandle;
        }
    }
}
