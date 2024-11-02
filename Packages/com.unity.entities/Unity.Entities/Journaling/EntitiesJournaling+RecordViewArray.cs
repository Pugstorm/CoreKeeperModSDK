#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.LowLevel.Unsafe;

namespace Unity.Entities
{
    partial class EntitiesJournaling
    {
        /// <summary>
        /// Array of <see cref="RecordView"/>.
        /// </summary>
        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "(UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING")]
        [DebuggerDisplay("Length = {Length}")]
        [DebuggerTypeProxy(typeof(RecordViewArrayDebugView))]
        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct RecordViewArray : IEnumerable<RecordView>
        {
            [NativeDisableUnsafePtrRestriction] readonly Record* m_RecordsPtr;
            [NativeDisableUnsafePtrRestriction] readonly byte* m_BufferPtr;
            readonly ulong m_RecordIndex;
            readonly int m_RecordsLength;
            readonly int m_BufferLength;
            readonly Ordering m_Ordering;

            /// <summary>
            /// Number of record in the array.
            /// </summary>
            public int Length => m_RecordsLength;

            /// <summary>
            /// Whether or not the buffer pointer is still valid.
            /// </summary>
            /// <remarks>
            /// Any changes to the journal buffer will cause record view arrays to become invalid.
            /// </remarks>
            public bool IsValid => m_RecordIndex == RecordIndex;

            /// <summary>
            /// The ordering of records in the array.
            /// </summary>
            public Ordering Ordering => m_Ordering;

            /// <summary>
            /// Get the record at the specified index.
            /// </summary>
            /// <param name="index">The element index.</param>
            public RecordView this[int index] => GetRecord(index);

            /// <summary>
            /// Retrieve the index of the entity view in the array.
            /// </summary>
            /// <param name="entityView">The entity view.</param>
            /// <returns>The index of the entity view if found, otherwise -1.</returns>
            public int IndexOf(RecordView recordView)
            {
                if (recordView.m_BufferPtr < m_BufferPtr || recordView.m_BufferPtr >= m_BufferPtr + m_BufferLength)
                    return -1;

                if (m_Ordering == Ordering.Ascending)
                {
                    return (int)(recordView.Index - this[0].Index);
                }
                else
                {
                    return (int)(this[0].Index - recordView.Index);
                }
            }

            /// <summary>
            /// Convert array to native array.
            /// </summary>
            /// <param name="allocator">The Allocator of the NativeArray.</param>
            public NativeArray<RecordView> ToNativeArray(AllocatorManager.AllocatorHandle allocator)
            {
                // Todo: When NativeArray supports custom allocators, remove these .ToAllocator callsites DOTS-7695
                var array = new NativeArray<RecordView>(m_RecordsLength, allocator.ToAllocator);
                for (var i = 0; i < m_RecordsLength; ++i)
                    array[i] = this[i];
                return array;
            }

            /// <summary>
            /// Convert array to managed array.
            /// </summary>
            [ExcludeFromBurstCompatTesting("Returns managed array")]
            public RecordView[] ToArray()
            {
                var array = new RecordView[m_RecordsLength];
                for (var i = 0; i < m_RecordsLength; ++i)
                    array[i] = this[i];
                return array;
            }

            /// <summary>
            /// Returns an enumerator that can iterate through the <see cref="RecordViewArray"/>.
            /// </summary>
            public Enumerator GetEnumerator() => new Enumerator(this);

            /// <summary>
            /// Returns an enumerator that can iterate through the <see cref="RecordViewArray"/>.
            /// </summary>
            [ExcludeFromBurstCompatTesting("Returns managed object")]
            IEnumerator<RecordView> IEnumerable<RecordView>.GetEnumerator() => GetEnumerator();

            /// <summary>
            /// Returns an enumerator that can iterate through the <see cref="RecordViewArray"/>.
            /// </summary>
            [ExcludeFromBurstCompatTesting("Returns managed object")]
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            /// <summary>
            /// Enumerator that can iterate through the <see cref="RecordViewArray"/>.
            /// </summary>
            [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "(UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING")]
            public struct Enumerator : IEnumerator<RecordView>
            {
                readonly RecordViewArray m_RecordViewArray;
                int m_Index;

                public RecordView Current => m_RecordViewArray[m_Index];

                [ExcludeFromBurstCompatTesting("Returns managed object")]
                object IEnumerator.Current => Current;

                internal Enumerator(RecordViewArray recordViewArray)
                {
                    m_RecordViewArray = recordViewArray;
                    m_Index = -1;
                }

                public void Dispose() { }
                public bool MoveNext() => ++m_Index < m_RecordViewArray.m_RecordsLength;
                public void Reset() => m_Index = -1;
            }

            internal RecordViewArray(ulong recordIndex, in UnsafeCircularBuffer<Record> records, in UnsafeCircularBuffer<byte> buffer, Ordering ordering)
            {
                ThrowIfFrontIndexIsNotZero(in records);
                ThrowIfFrontIndexIsNotZero(in buffer);
                m_RecordIndex = recordIndex;
                m_RecordsPtr = records.Ptr;
                m_RecordsLength = records.Count;
                m_BufferPtr = buffer.Ptr;
                m_BufferLength = buffer.Count;
                m_Ordering = ordering;
            }

            RecordView GetRecord(int index)
            {
                ThrowIfNotValid();
                CollectionHelper.CheckIndexInRange(index, Length);

                var record = m_RecordsPtr[m_Ordering == Ordering.Ascending ? index : m_RecordsLength - 1 - index];
                return new RecordView(m_BufferPtr + record.Position);
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
            void ThrowIfNotValid()
            {
                if (!IsValid)
                    throw new InvalidOperationException("Buffer pointer is no longer valid.");
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
            static void ThrowIfFrontIndexIsNotZero<T>(in UnsafeCircularBuffer<T> buffer) where T : unmanaged
            {
                if (buffer.FrontIndex != 0)
                    throw new InvalidOperationException("Buffer front index is not zero.");
            }
        }

        internal sealed class RecordViewArrayDebugView
        {
            readonly RecordViewArray m_RecordViewArray;

            public RecordViewArrayDebugView(RecordViewArray recordViewArray)
            {
                m_RecordViewArray = recordViewArray;
            }

            public RecordView[] Items => m_RecordViewArray.ToArray();
        }
    }
}
#endif
