#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using static Unity.Entities.EntitiesJournaling;

namespace Unity.Entities.Editor
{
    partial class EntitiesJournalingWindow
    {
        internal class ReadOnlyRecordViewList : IList<RecordView>, IList
        {
            readonly RecordViewArray m_Records;

            public bool IsValid => m_Records.IsValid;

            public ReadOnlyRecordViewList(RecordViewArray records)
            {
                m_Records = records;
            }

            public void RunPostProcess()
            {
                RecordViewArrayUtility.ConvertGetRWsToSets(in m_Records);
            }

            public RecordView this[int index]
            {
                get => m_Records[index];
                set => throw new NotSupportedException();
            }

            object IList.this[int index]
            {
                get => m_Records[index];
                set => throw new NotSupportedException();
            }

            public int Count => m_Records.Length;
            public bool IsReadOnly => true;
            public bool IsFixedSize => true;
            public bool Contains(RecordView item) => m_Records.Contains(item);
            public bool Contains(object value) => Contains((RecordView)value);
            public int IndexOf(RecordView item) => m_Records.IndexOf(item);
            public int IndexOf(object value) => IndexOf((RecordView)value);

            public void CopyTo(RecordView[] array, int startIndex)
            {
                for (int i = startIndex, count = m_Records.Length; i < count; ++i)
                    array[i - startIndex] = m_Records[i];
            }

            public void CopyTo(Array array, int startIndex)
            {
                for (int i = startIndex, count = m_Records.Length; i < count; ++i)
                    array.SetValue(m_Records[i], i - startIndex);
            }

            public RecordViewArray.Enumerator GetEnumerator() => m_Records.GetEnumerator();
            IEnumerator<RecordView> IEnumerable<RecordView>.GetEnumerator() => m_Records.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public bool IsSynchronized => throw new NotSupportedException();
            public object SyncRoot => throw new NotSupportedException();
            public void Add(RecordView item) => throw new NotSupportedException();
            public int Add(object value) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
            public void Insert(int index, RecordView item) => throw new NotSupportedException();
            public void Insert(int index, object value) => throw new NotSupportedException();
            public bool Remove(RecordView item) => throw new NotSupportedException();
            public void Remove(object value) => throw new NotSupportedException();
            public void RemoveAt(int index) => throw new NotSupportedException();
        }
    }
}
#endif
