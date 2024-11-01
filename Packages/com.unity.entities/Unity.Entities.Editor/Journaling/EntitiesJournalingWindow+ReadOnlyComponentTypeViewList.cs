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
        class ReadOnlyComponentTypeViewList : IList<ComponentTypeView>, IList
        {
            readonly ComponentTypeViewArray m_ComponentTypes;

            public ReadOnlyComponentTypeViewList(ComponentTypeViewArray componentTypes)
            {
                m_ComponentTypes = componentTypes;
            }

            public ComponentTypeView this[int index]
            {
                get => m_ComponentTypes[index];
                set => throw new NotSupportedException();
            }

            object IList.this[int index]
            {
                get => m_ComponentTypes[index];
                set => throw new NotSupportedException();
            }

            public int Count => m_ComponentTypes.Length;
            public bool IsReadOnly => true;
            public bool IsFixedSize => true;
            public bool Contains(ComponentTypeView item) => m_ComponentTypes.Contains(item);
            public bool Contains(object value) => Contains((ComponentTypeView)value);
            public int IndexOf(ComponentTypeView item) => m_ComponentTypes.IndexOf(item);
            public int IndexOf(object value) => IndexOf((ComponentTypeView)value);

            public void CopyTo(ComponentTypeView[] array, int startIndex)
            {
                for (int i = startIndex, count = m_ComponentTypes.Length; i < count; ++i)
                    array[i - startIndex] = m_ComponentTypes[i];
            }

            public void CopyTo(Array array, int startIndex)
            {
                for (int i = startIndex, count = m_ComponentTypes.Length; i < count; ++i)
                    array.SetValue(m_ComponentTypes[i], i - startIndex);
            }

            public ComponentTypeViewArray.Enumerator GetEnumerator() => m_ComponentTypes.GetEnumerator();
            IEnumerator<ComponentTypeView> IEnumerable<ComponentTypeView>.GetEnumerator() => m_ComponentTypes.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public bool IsSynchronized => throw new NotSupportedException();
            public object SyncRoot => throw new NotSupportedException();
            public void Add(ComponentTypeView item) => throw new NotSupportedException();
            public int Add(object value) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
            public void Insert(int index, ComponentTypeView item) => throw new NotSupportedException();
            public void Insert(int index, object value) => throw new NotSupportedException();
            public bool Remove(ComponentTypeView item) => throw new NotSupportedException();
            public void Remove(object value) => throw new NotSupportedException();
            public void RemoveAt(int index) => throw new NotSupportedException();
        }
    }
}
#endif
