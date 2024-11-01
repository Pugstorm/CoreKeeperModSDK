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
        class ReadOnlyEntityViewList : IList<EntitiesJournaling.EntityView>, IList
        {
            readonly EntityViewArray m_Entities;

            public ReadOnlyEntityViewList(EntityViewArray entities)
            {
                m_Entities = entities;
            }

            public EntitiesJournaling.EntityView this[int index]
            {
                get => m_Entities[index];
                set => throw new NotSupportedException();
            }

            object IList.this[int index]
            {
                get => m_Entities[index];
                set => throw new NotSupportedException();
            }

            public int Count => m_Entities.Length;
            public bool IsReadOnly => true;
            public bool IsFixedSize => true;
            public bool Contains(EntitiesJournaling.EntityView item) => m_Entities.Contains(item);
            public bool Contains(object value) => Contains((EntitiesJournaling.EntityView)value);
            public int IndexOf(EntitiesJournaling.EntityView item) => m_Entities.IndexOf(item);
            public int IndexOf(object value) => IndexOf((EntitiesJournaling.EntityView)value);

            public void CopyTo(EntitiesJournaling.EntityView[] array, int startIndex)
            {
                for (int i = startIndex, count = m_Entities.Length; i < count; ++i)
                    array[i - startIndex] = m_Entities[i];
            }

            public void CopyTo(Array array, int startIndex)
            {
                for (int i = startIndex, count = m_Entities.Length; i < count; ++i)
                    array.SetValue(m_Entities[i], i - startIndex);
            }

            public EntityViewArray.Enumerator GetEnumerator() => m_Entities.GetEnumerator();
            IEnumerator<EntitiesJournaling.EntityView> IEnumerable<EntitiesJournaling.EntityView>.GetEnumerator() => m_Entities.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public bool IsSynchronized => throw new NotSupportedException();
            public object SyncRoot => throw new NotSupportedException();
            public void Add(EntitiesJournaling.EntityView item) => throw new NotSupportedException();
            public int Add(object value) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
            public void Insert(int index, EntitiesJournaling.EntityView item) => throw new NotSupportedException();
            public void Insert(int index, object value) => throw new NotSupportedException();
            public bool Remove(EntitiesJournaling.EntityView item) => throw new NotSupportedException();
            public void Remove(object value) => throw new NotSupportedException();
            public void RemoveAt(int index) => throw new NotSupportedException();
        }
    }
}
#endif
