using NUnit.Framework;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.LowLevel.Unsafe.Tests
{
    unsafe class UnsafeCircularBufferTests
    {
        [Test]
        public void UnsafeCircularBufferT_Init_ClearMemory()
        {
            using var buffer = new UnsafeCircularBuffer<int>(5, Allocator.Temp, NativeArrayOptions.ClearMemory);
            for (var i = 0; i < buffer.Count; ++i)
                Assert.AreEqual(0, UnsafeUtility.ReadArrayElement<int>(buffer.Ptr, i));
        }

        [Test]
        public void UnsafeCircularBufferT_Capacity()
        {
            using var buffer = new UnsafeCircularBuffer<int>(5, Allocator.Temp);
            Assert.AreEqual(5, buffer.Capacity);

            buffer.PushBack(1);
            Assert.AreEqual(5, buffer.Capacity);

            buffer.PushFront(2);
            Assert.AreEqual(5, buffer.Capacity);

            buffer.PopBack();
            Assert.AreEqual(5, buffer.Capacity);

            buffer.PopFront();
            Assert.AreEqual(5, buffer.Capacity);
        }

        [Test]
        public void UnsafeCircularBufferT_Count()
        {
            using var buffer = new UnsafeCircularBuffer<int>(5, Allocator.Temp);
            Assert.AreEqual(0, buffer.Count);

            buffer.PushBack(1);
            Assert.AreEqual(1, buffer.Count);

            buffer.PushBack(new[] { 2, 3, 4, 5 });
            Assert.AreEqual(5, buffer.Count);

            buffer.PushBack(1);
            Assert.AreEqual(5, buffer.Count);

            buffer.PopBack();
            Assert.AreEqual(4, buffer.Count);

            buffer.PopBack(4);
            Assert.AreEqual(0, buffer.Count);

            buffer.PopBack(1);
            Assert.AreEqual(0, buffer.Count);

            buffer.PushFront(1);
            Assert.AreEqual(1, buffer.Count);

            buffer.PushFront(new[] { 2, 3, 4, 5 });
            Assert.AreEqual(5, buffer.Count);

            buffer.PushFront(1);
            Assert.AreEqual(5, buffer.Count);

            buffer.PopFront();
            Assert.AreEqual(4, buffer.Count);

            buffer.PopFront(4);
            Assert.AreEqual(0, buffer.Count);

            buffer.PopFront(1);
            Assert.AreEqual(0, buffer.Count);
        }

        [Test]
        public void UnsafeCircularBufferT_IsEmpty()
        {
            using var buffer = new UnsafeCircularBuffer<int>(2, Allocator.Temp);
            Assert.IsTrue(buffer.IsEmpty);

            buffer.PushBack(1);
            Assert.IsFalse(buffer.IsEmpty);

            buffer.PushBack(1);
            Assert.IsFalse(buffer.IsEmpty);

            buffer.PopBack();
            Assert.IsFalse(buffer.IsEmpty);

            buffer.PopBack();
            Assert.IsTrue(buffer.IsEmpty);
        }

        [Test]
        public void UnsafeCircularBufferT_IsFull()
        {
            using var buffer = new UnsafeCircularBuffer<int>(2, Allocator.Temp);
            Assert.IsFalse(buffer.IsFull);

            buffer.PushBack(1);
            Assert.IsFalse(buffer.IsFull);

            buffer.PushBack(1);
            Assert.IsTrue(buffer.IsFull);

            buffer.PopBack();
            Assert.IsFalse(buffer.IsFull);

            buffer.PopBack();
            Assert.IsFalse(buffer.IsFull);
        }

        [Test]
        public void UnsafeCircularBufferT_FrontIndex()
        {
            using var buffer = new UnsafeCircularBuffer<int>(2, Allocator.Temp);
            Assert.AreEqual(0, buffer.FrontIndex);

            buffer.PushBack(1);
            Assert.AreEqual(0, buffer.FrontIndex);

            buffer.PushBack(1);
            Assert.AreEqual(0, buffer.FrontIndex);

            buffer.PopBack();
            Assert.AreEqual(0, buffer.FrontIndex);

            buffer.PopBack();
            Assert.AreEqual(0, buffer.FrontIndex);

            buffer.PushFront(1);
            Assert.AreEqual(1, buffer.FrontIndex);

            buffer.PushFront(1);
            Assert.AreEqual(0, buffer.FrontIndex);

            buffer.PopFront();
            Assert.AreEqual(1, buffer.FrontIndex);

            buffer.PopFront();
            Assert.AreEqual(0, buffer.FrontIndex);
        }

        [Test]
        public void UnsafeCircularBufferT_BackIndex()
        {
            using var buffer = new UnsafeCircularBuffer<int>(2, Allocator.Temp);
            Assert.AreEqual(0, buffer.BackIndex);

            buffer.PushBack(1);
            Assert.AreEqual(1, buffer.BackIndex);

            buffer.PushBack(1);
            Assert.AreEqual(0, buffer.BackIndex);

            buffer.PopBack();
            Assert.AreEqual(1, buffer.BackIndex);

            buffer.PopBack();
            Assert.AreEqual(0, buffer.BackIndex);

            buffer.PushFront(1);
            Assert.AreEqual(0, buffer.BackIndex);

            buffer.PushFront(1);
            Assert.AreEqual(0, buffer.BackIndex);

            buffer.PopFront();
            Assert.AreEqual(0, buffer.BackIndex);

            buffer.PopFront();
            Assert.AreEqual(0, buffer.BackIndex);
        }

        [Test]
        public void UnsafeCircularBufferT_Indexer()
        {
            using var buffer = new UnsafeCircularBuffer<int>(new[] { 1, 2, 3, 4, 5 }, Allocator.Temp);

            Assert.AreEqual(1, buffer[0]);
            Assert.AreEqual(2, buffer[1]);
            Assert.AreEqual(3, buffer[2]);
            Assert.AreEqual(4, buffer[3]);
            Assert.AreEqual(5, buffer[4]);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.Throws<IndexOutOfRangeException>(() => { var value = buffer[-1]; });
            Assert.Throws<IndexOutOfRangeException>(() => { var value = buffer[5]; });
#endif
        }

        [Test]
        public void UnsafeCircularBufferT_ElementAt()
        {
            using var buffer = new UnsafeCircularBuffer<int>(new[] { 1, 2, 3, 4, 5 }, Allocator.Temp);

            Assert.AreEqual(1, buffer.ElementAt(0));
            Assert.AreEqual(2, buffer.ElementAt(1));
            Assert.AreEqual(3, buffer.ElementAt(2));
            Assert.AreEqual(4, buffer.ElementAt(3));
            Assert.AreEqual(5, buffer.ElementAt(4));

            buffer.ElementAt(2) = 9;

            Assert.AreEqual(1, buffer.ElementAt(0));
            Assert.AreEqual(2, buffer.ElementAt(1));
            Assert.AreEqual(9, buffer.ElementAt(2));
            Assert.AreEqual(4, buffer.ElementAt(3));
            Assert.AreEqual(5, buffer.ElementAt(4));

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.Throws<IndexOutOfRangeException>(() => { var value = buffer.ElementAt(-1); });
            Assert.Throws<IndexOutOfRangeException>(() => { var value = buffer.ElementAt(5); });
#endif
        }

        [Test]
        public void UnsafeCircularBufferT_Front()
        {
            using var buffer = new UnsafeCircularBuffer<int>(new[] { 1, 2, 3, 4, 5 }, Allocator.Temp);

            Assert.AreEqual(1, buffer.Front());

            buffer.PopFront();
            Assert.AreEqual(2, buffer.Front());

            buffer.Clear();

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.Throws<InvalidOperationException>(() => buffer.Front());
#endif
        }

        [Test]
        public void UnsafeCircularBufferT_Back()
        {
            using var buffer = new UnsafeCircularBuffer<int>(new[] { 1, 2, 3, 4, 5 }, Allocator.Temp);

            Assert.AreEqual(5, buffer.Back());

            buffer.PopBack();
            Assert.AreEqual(4, buffer.Back());

            buffer.Clear();

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.Throws<InvalidOperationException>(() => buffer.Back());
#endif
        }

        [Test]
        public void UnsafeCircularBufferT_PushFront()
        {
            using var buffer = new UnsafeCircularBuffer<int>(5, Allocator.Temp);

            Assert.IsTrue(buffer.PushFront(1));
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(1, buffer.Back());

            Assert.IsTrue(buffer.PushFront(2));
            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual(2, buffer.Front());
            Assert.AreEqual(1, buffer.Back());

            Assert.IsTrue(buffer.PushFront(3));
            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(3, buffer.Front());
            Assert.AreEqual(1, buffer.Back());

            Assert.IsTrue(buffer.PushFront(4));
            Assert.AreEqual(4, buffer.Count);
            Assert.AreEqual(4, buffer.Front());
            Assert.AreEqual(1, buffer.Back());

            Assert.IsTrue(buffer.PushFront(5));
            Assert.AreEqual(5, buffer.Count);
            Assert.AreEqual(5, buffer.Front());
            Assert.AreEqual(1, buffer.Back());

            Assert.IsFalse(buffer.PushFront(6));
            Assert.AreEqual(5, buffer.Count);
            Assert.AreEqual(5, buffer.Front());
            Assert.AreEqual(1, buffer.Back());
        }

        [Test]
        public void UnsafeCircularBufferT_PushFront_WithCount()
        {
            using var buffer = new UnsafeCircularBuffer<int>(5, Allocator.Temp);

            Assert.IsTrue(buffer.PushFront(new[] { 1, 2, 3 }));
            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(3, buffer.Back());

            Assert.IsFalse(buffer.PushFront(new[] { 4, 5, 6 }));
            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(3, buffer.Back());

            Assert.IsTrue(buffer.PushFront(new[] { 4, 5 }));
            Assert.AreEqual(5, buffer.Count);
            Assert.AreEqual(4, buffer.Front());
            Assert.AreEqual(3, buffer.Back());

            Assert.IsFalse(buffer.PushFront(new[] { 6, 7, 8 }));
            Assert.AreEqual(5, buffer.Count);
            Assert.AreEqual(4, buffer.Front());
            Assert.AreEqual(3, buffer.Back());
        }

        [Test]
        public void UnsafeCircularBufferT_PushBack()
        {
            using var buffer = new UnsafeCircularBuffer<int>(5, Allocator.Temp);

            Assert.IsTrue(buffer.PushBack(1));
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(1, buffer.Back());

            Assert.IsTrue(buffer.PushBack(2));
            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(2, buffer.Back());

            Assert.IsTrue(buffer.PushBack(3));
            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(3, buffer.Back());

            Assert.IsTrue(buffer.PushBack(4));
            Assert.AreEqual(4, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(4, buffer.Back());

            Assert.IsTrue(buffer.PushBack(5));
            Assert.AreEqual(5, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(5, buffer.Back());

            Assert.IsFalse(buffer.PushBack(6));
            Assert.AreEqual(5, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(5, buffer.Back());
        }

        [Test]
        public void UnsafeCircularBufferT_PushBack_WithCount()
        {
            using var buffer = new UnsafeCircularBuffer<int>(5, Allocator.Temp);

            Assert.IsTrue(buffer.PushBack(new[] { 1, 2, 3 }));
            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(3, buffer.Back());

            Assert.IsFalse(buffer.PushBack(new[] { 4, 5, 6 }));
            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(3, buffer.Back());

            Assert.IsTrue(buffer.PushBack(new[] { 4, 5 }));
            Assert.AreEqual(5, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(5, buffer.Back());

            Assert.IsFalse(buffer.PushBack(new[] { 6, 7, 8 }));
            Assert.AreEqual(5, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(5, buffer.Back());
        }

        [Test]
        public void UnsafeCircularBufferT_PopFront()
        {
            using var buffer = new UnsafeCircularBuffer<int>(new[] { 1, 2, 3, 4, 5 }, Allocator.Temp);

            Assert.IsTrue(buffer.PopFront());
            Assert.AreEqual(4, buffer.Count);
            Assert.AreEqual(2, buffer.Front());
            Assert.AreEqual(5, buffer.Back());

            Assert.IsTrue(buffer.PopFront());
            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(3, buffer.Front());
            Assert.AreEqual(5, buffer.Back());

            Assert.IsTrue(buffer.PopFront());
            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual(4, buffer.Front());
            Assert.AreEqual(5, buffer.Back());

            Assert.IsTrue(buffer.PopFront());
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(5, buffer.Front());
            Assert.AreEqual(5, buffer.Back());

            Assert.IsTrue(buffer.PopFront());
            Assert.AreEqual(0, buffer.Count);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.Throws<InvalidOperationException>(() => buffer.Front());
            Assert.Throws<InvalidOperationException>(() => buffer.Back());
#endif

            Assert.IsFalse(buffer.PopFront());
            Assert.AreEqual(0, buffer.Count);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.Throws<InvalidOperationException>(() => buffer.Front());
            Assert.Throws<InvalidOperationException>(() => buffer.Back());
#endif
        }

        [Test]
        public void UnsafeCircularBufferT_PopFront_WithCount()
        {
            using var buffer = new UnsafeCircularBuffer<int>(new[] { 1, 2, 3, 4, 5 }, Allocator.Temp);

            Assert.IsFalse(buffer.PopFront(6));
            Assert.AreEqual(5, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(5, buffer.Back());

            Assert.IsTrue(buffer.PopFront(2));
            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(3, buffer.Front());
            Assert.AreEqual(5, buffer.Back());

            Assert.IsTrue(buffer.PopFront(2));
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(5, buffer.Front());
            Assert.AreEqual(5, buffer.Back());

            Assert.IsFalse(buffer.PopFront(2));
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(5, buffer.Front());
            Assert.AreEqual(5, buffer.Back());

            Assert.IsTrue(buffer.PopFront(1));
            Assert.AreEqual(0, buffer.Count);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.Throws<InvalidOperationException>(() => buffer.Front());
            Assert.Throws<InvalidOperationException>(() => buffer.Back());
#endif

            Assert.IsFalse(buffer.PopFront(1));
            Assert.AreEqual(0, buffer.Count);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.Throws<InvalidOperationException>(() => buffer.Front());
            Assert.Throws<InvalidOperationException>(() => buffer.Back());
#endif
        }

        [Test]
        public void UnsafeCircularBufferT_PopBack()
        {
            using var buffer = new UnsafeCircularBuffer<int>(new[] { 1, 2, 3, 4, 5 }, Allocator.Temp);

            Assert.IsTrue(buffer.PopBack());
            Assert.AreEqual(4, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(4, buffer.Back());

            Assert.IsTrue(buffer.PopBack());
            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(3, buffer.Back());

            Assert.IsTrue(buffer.PopBack());
            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(2, buffer.Back());

            Assert.IsTrue(buffer.PopBack());
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(1, buffer.Back());

            Assert.IsTrue(buffer.PopBack());
            Assert.AreEqual(0, buffer.Count);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.Throws<InvalidOperationException>(() => buffer.Front());
            Assert.Throws<InvalidOperationException>(() => buffer.Back());
#endif

            Assert.IsFalse(buffer.PopBack());
            Assert.AreEqual(0, buffer.Count);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.Throws<InvalidOperationException>(() => buffer.Front());
            Assert.Throws<InvalidOperationException>(() => buffer.Back());
#endif
        }

        [Test]
        public void UnsafeCircularBufferT_PopBack_WithCount()
        {
            using var buffer = new UnsafeCircularBuffer<int>(new[] { 1, 2, 3, 4, 5 }, Allocator.Temp);

            Assert.IsFalse(buffer.PopBack(6));
            Assert.AreEqual(5, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(5, buffer.Back());

            Assert.IsTrue(buffer.PopBack(2));
            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(3, buffer.Back());

            Assert.IsTrue(buffer.PopBack(2));
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(1, buffer.Back());

            Assert.IsFalse(buffer.PopBack(2));
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(1, buffer.Front());
            Assert.AreEqual(1, buffer.Back());

            Assert.IsTrue(buffer.PopBack(1));
            Assert.AreEqual(0, buffer.Count);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.Throws<InvalidOperationException>(() => buffer.Front());
            Assert.Throws<InvalidOperationException>(() => buffer.Back());
#endif

            Assert.IsFalse(buffer.PopBack(1));
            Assert.AreEqual(0, buffer.Count);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.Throws<InvalidOperationException>(() => buffer.Front());
            Assert.Throws<InvalidOperationException>(() => buffer.Back());
#endif
        }

        [Test]
        public void UnsafeCircularBufferT_Unwind_Unwrapped()
        {
            using var buffer = new UnsafeCircularBuffer<int>(15, Allocator.Temp);
            buffer.PushBack(new int[] { 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            buffer.PopFront(3);

            buffer.Unwind();
            Assert.AreEqual(10, buffer.Count);
            Assert.AreEqual(1, buffer[0]);
            Assert.AreEqual(2, buffer[1]);
            Assert.AreEqual(3, buffer[2]);
            Assert.AreEqual(4, buffer[3]);
            Assert.AreEqual(5, buffer[4]);
            Assert.AreEqual(6, buffer[5]);
            Assert.AreEqual(7, buffer[6]);
            Assert.AreEqual(8, buffer[7]);
            Assert.AreEqual(9, buffer[8]);
            Assert.AreEqual(10, buffer[9]);
        }

        [Test]
        public void UnsafeCircularBufferT_Unwind_WrappedFrontSmall()
        {
            using var buffer = new UnsafeCircularBuffer<int>(15, Allocator.Temp);
            buffer.PushBack(new int[] { 5, 6, 7, 8, 9, 10 });
            buffer.PushFront(new int[] { 1, 2, 3, 4 });

            buffer.Unwind();
            Assert.AreEqual(10, buffer.Count);
            Assert.AreEqual(1, buffer[0]);
            Assert.AreEqual(2, buffer[1]);
            Assert.AreEqual(3, buffer[2]);
            Assert.AreEqual(4, buffer[3]);
            Assert.AreEqual(5, buffer[4]);
            Assert.AreEqual(6, buffer[5]);
            Assert.AreEqual(7, buffer[6]);
            Assert.AreEqual(8, buffer[7]);
            Assert.AreEqual(9, buffer[8]);
            Assert.AreEqual(10, buffer[9]);
        }

        [Test]
        public void UnsafeCircularBufferT_Unwind_WrappedBackSmall()
        {
            using var buffer = new UnsafeCircularBuffer<int>(15, Allocator.Temp);
            buffer.PushBack(new int[] { 7, 8, 9, 10 });
            buffer.PushFront(new int[] { 1, 2, 3, 4, 5, 6 });

            buffer.Unwind();
            Assert.AreEqual(10, buffer.Count);
            Assert.AreEqual(1, buffer[0]);
            Assert.AreEqual(2, buffer[1]);
            Assert.AreEqual(3, buffer[2]);
            Assert.AreEqual(4, buffer[3]);
            Assert.AreEqual(5, buffer[4]);
            Assert.AreEqual(6, buffer[5]);
            Assert.AreEqual(7, buffer[6]);
            Assert.AreEqual(8, buffer[7]);
            Assert.AreEqual(9, buffer[8]);
            Assert.AreEqual(10, buffer[9]);
        }

        [Test]
        public void UnsafeCircularBufferT_Clear()
        {
            using var buffer = new UnsafeCircularBuffer<int>(new[] { 1, 2, 3, 4, 5 }, Allocator.Temp);

            Assert.AreEqual(5, buffer.Count);
            Assert.IsFalse(buffer.IsEmpty);

            buffer.Clear();
            Assert.AreEqual(0, buffer.Count);
            Assert.IsTrue(buffer.IsEmpty);
        }

        [Test]
        public void UnsafeCircularBufferT_ToNativeArray_Unwrapped()
        {
            using var buffer = new UnsafeCircularBuffer<int>(15, Allocator.Temp);
            buffer.PushBack(new int[] { 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            buffer.PopFront(3);

            using var array = buffer.ToNativeArray(Allocator.Temp);
            Assert.AreEqual(10, array.Length);
            Assert.AreEqual(1, array[0]);
            Assert.AreEqual(2, array[1]);
            Assert.AreEqual(3, array[2]);
            Assert.AreEqual(4, array[3]);
            Assert.AreEqual(5, array[4]);
            Assert.AreEqual(6, array[5]);
            Assert.AreEqual(7, array[6]);
            Assert.AreEqual(8, array[7]);
            Assert.AreEqual(9, array[8]);
            Assert.AreEqual(10, array[9]);
        }

        [Test]
        public void UnsafeCircularBufferT_ToNativeArray_WrappedFrontSmall()
        {
            using var buffer = new UnsafeCircularBuffer<int>(15, Allocator.Temp);
            buffer.PushBack(new int[] { 5, 6, 7, 8, 9, 10 });
            buffer.PushFront(new int[] { 1, 2, 3, 4 });

            using var array = buffer.ToNativeArray(Allocator.Temp);
            Assert.AreEqual(10, array.Length);
            Assert.AreEqual(1, array[0]);
            Assert.AreEqual(2, array[1]);
            Assert.AreEqual(3, array[2]);
            Assert.AreEqual(4, array[3]);
            Assert.AreEqual(5, array[4]);
            Assert.AreEqual(6, array[5]);
            Assert.AreEqual(7, array[6]);
            Assert.AreEqual(8, array[7]);
            Assert.AreEqual(9, array[8]);
            Assert.AreEqual(10, array[9]);
        }

        [Test]
        public void UnsafeCircularBufferT_ToNativeArray_WrappedBackSmall()
        {
            using var buffer = new UnsafeCircularBuffer<int>(15, Allocator.Temp);
            buffer.PushBack(new int[] { 7, 8, 9, 10 });
            buffer.PushFront(new int[] { 1, 2, 3, 4, 5, 6 });

            using var array = buffer.ToNativeArray(Allocator.Temp);
            Assert.AreEqual(10, array.Length);
            Assert.AreEqual(1, array[0]);
            Assert.AreEqual(2, array[1]);
            Assert.AreEqual(3, array[2]);
            Assert.AreEqual(4, array[3]);
            Assert.AreEqual(5, array[4]);
            Assert.AreEqual(6, array[5]);
            Assert.AreEqual(7, array[6]);
            Assert.AreEqual(8, array[7]);
            Assert.AreEqual(9, array[8]);
            Assert.AreEqual(10, array[9]);
        }

        [Test]
        public void UnsafeCircularBufferT_ToArray_Unwrapped()
        {
            using var buffer = new UnsafeCircularBuffer<int>(15, Allocator.Temp);
            buffer.PushBack(new int[] { 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            buffer.PopFront(3);

            var array = buffer.ToArray();
            Assert.AreEqual(10, array.Length);
            Assert.AreEqual(1, array[0]);
            Assert.AreEqual(2, array[1]);
            Assert.AreEqual(3, array[2]);
            Assert.AreEqual(4, array[3]);
            Assert.AreEqual(5, array[4]);
            Assert.AreEqual(6, array[5]);
            Assert.AreEqual(7, array[6]);
            Assert.AreEqual(8, array[7]);
            Assert.AreEqual(9, array[8]);
            Assert.AreEqual(10, array[9]);
        }

        [Test]
        public void UnsafeCircularBufferT_ToArray_WrappedFrontSmall()
        {
            using var buffer = new UnsafeCircularBuffer<int>(15, Allocator.Temp);
            buffer.PushBack(new int[] { 5, 6, 7, 8, 9, 10 });
            buffer.PushFront(new int[] { 1, 2, 3, 4 });

            var array = buffer.ToArray();
            Assert.AreEqual(10, array.Length);
            Assert.AreEqual(1, array[0]);
            Assert.AreEqual(2, array[1]);
            Assert.AreEqual(3, array[2]);
            Assert.AreEqual(4, array[3]);
            Assert.AreEqual(5, array[4]);
            Assert.AreEqual(6, array[5]);
            Assert.AreEqual(7, array[6]);
            Assert.AreEqual(8, array[7]);
            Assert.AreEqual(9, array[8]);
            Assert.AreEqual(10, array[9]);
        }

        [Test]
        public void UnsafeCircularBufferT_ToArray_WrappedBackSmall()
        {
            using var buffer = new UnsafeCircularBuffer<int>(15, Allocator.Temp);
            buffer.PushBack(new int[] { 7, 8, 9, 10 });
            buffer.PushFront(new int[] { 1, 2, 3, 4, 5, 6 });

            var array = buffer.ToArray();
            Assert.AreEqual(10, array.Length);
            Assert.AreEqual(1, array[0]);
            Assert.AreEqual(2, array[1]);
            Assert.AreEqual(3, array[2]);
            Assert.AreEqual(4, array[3]);
            Assert.AreEqual(5, array[4]);
            Assert.AreEqual(6, array[5]);
            Assert.AreEqual(7, array[6]);
            Assert.AreEqual(8, array[7]);
            Assert.AreEqual(9, array[8]);
            Assert.AreEqual(10, array[9]);
        }
    }
}
