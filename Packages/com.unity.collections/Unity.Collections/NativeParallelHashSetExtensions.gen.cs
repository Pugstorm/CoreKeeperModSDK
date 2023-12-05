using System;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Collections
{
    /// <summary>
    /// Provides extension methods for sets.
    /// </summary>
    public unsafe static class HashSetExtensions
    {
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeHashSet<T> container, FixedList128Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeHashSet<T> container, FixedList128Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count, Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeHashSet<T> container, FixedList128Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeHashSet<T> container, FixedList32Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeHashSet<T> container, FixedList32Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count, Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeHashSet<T> container, FixedList32Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeHashSet<T> container, FixedList4096Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeHashSet<T> container, FixedList4096Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count, Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeHashSet<T> container, FixedList4096Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeHashSet<T> container, FixedList512Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeHashSet<T> container, FixedList512Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count, Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeHashSet<T> container, FixedList512Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeHashSet<T> container, FixedList64Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeHashSet<T> container, FixedList64Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count, Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeHashSet<T> container, FixedList64Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeHashSet<T> container, NativeArray<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeHashSet<T> container, NativeArray<T> other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count, Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeHashSet<T> container, NativeArray<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeHashSet<T> container, NativeHashSet<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeHashSet<T> container, NativeHashSet<T> other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count, Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeHashSet<T> container, NativeHashSet<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeHashSet<T> container, NativeHashSet<T>.ReadOnly other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeHashSet<T> container, NativeHashSet<T>.ReadOnly other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count, Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeHashSet<T> container, NativeHashSet<T>.ReadOnly other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeHashSet<T> container, NativeParallelHashSet<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeHashSet<T> container, NativeParallelHashSet<T> other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count, Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeHashSet<T> container, NativeParallelHashSet<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeHashSet<T> container, NativeParallelHashSet<T>.ReadOnly other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeHashSet<T> container, NativeParallelHashSet<T>.ReadOnly other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count, Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeHashSet<T> container, NativeParallelHashSet<T>.ReadOnly other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeHashSet<T> container, NativeList<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeHashSet<T> container, NativeList<T> other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count, Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeHashSet<T> container, NativeList<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeParallelHashSet<T> container, FixedList128Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeParallelHashSet<T> container, FixedList128Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count(), Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeParallelHashSet<T> container, FixedList128Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeParallelHashSet<T> container, FixedList32Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeParallelHashSet<T> container, FixedList32Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count(), Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeParallelHashSet<T> container, FixedList32Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeParallelHashSet<T> container, FixedList4096Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeParallelHashSet<T> container, FixedList4096Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count(), Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeParallelHashSet<T> container, FixedList4096Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeParallelHashSet<T> container, FixedList512Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeParallelHashSet<T> container, FixedList512Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count(), Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeParallelHashSet<T> container, FixedList512Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeParallelHashSet<T> container, FixedList64Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeParallelHashSet<T> container, FixedList64Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count(), Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeParallelHashSet<T> container, FixedList64Bytes<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeParallelHashSet<T> container, NativeArray<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeParallelHashSet<T> container, NativeArray<T> other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count(), Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeParallelHashSet<T> container, NativeArray<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeParallelHashSet<T> container, NativeHashSet<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeParallelHashSet<T> container, NativeHashSet<T> other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count(), Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeParallelHashSet<T> container, NativeHashSet<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeParallelHashSet<T> container, NativeHashSet<T>.ReadOnly other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeParallelHashSet<T> container, NativeHashSet<T>.ReadOnly other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count(), Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeParallelHashSet<T> container, NativeHashSet<T>.ReadOnly other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeParallelHashSet<T> container, NativeParallelHashSet<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeParallelHashSet<T> container, NativeParallelHashSet<T> other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count(), Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeParallelHashSet<T> container, NativeParallelHashSet<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeParallelHashSet<T> container, NativeParallelHashSet<T>.ReadOnly other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeParallelHashSet<T> container, NativeParallelHashSet<T>.ReadOnly other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count(), Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeParallelHashSet<T> container, NativeParallelHashSet<T>.ReadOnly other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref NativeParallelHashSet<T> container, NativeList<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref NativeParallelHashSet<T> container, NativeList<T> other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count(), Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        /// <summary>
        /// Adds all values from a collection to this set.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to add values to.</param>
        /// <param name="other">The collection to copy values from.</param>
        public static void UnionWith<T>(this ref NativeParallelHashSet<T> container, NativeList<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
    }
}
