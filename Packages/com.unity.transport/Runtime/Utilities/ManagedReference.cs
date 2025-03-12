using System;
using System.Collections.Generic;
using Unity.Collections;

namespace Unity.Networking.Transport.Utilities
{
    /// <summary>
    /// Stores a managed object into a static array and keeps a reference to it into
    /// a unmanaged struct, so the managed reference can be passed to burst context.
    /// Access to the referenced is still not burst compatible.
    /// </summary>
    /// <typeparam name="T">The type of the object to store.</typeparam>
    internal struct ManagedReference<T> : IDisposable
    {
        private class ElementSlot
        {
            public T Element;
        }

        private static List<ElementSlot> s_ElementList = new List<ElementSlot>();

        private static int AllocateElement(ref T element)
        {
            var count = s_ElementList.Count;
            var slot = new ElementSlot { Element = element };
            var index = s_ElementList.FindIndex(0, count, e => e == null);

            if (index >= 0)
            {
                s_ElementList[index] = slot;
                return index;
            }

            s_ElementList.Add(slot);
            return count;
        }

        private static void DeallocateElement(int index)
        {
            s_ElementList[index] = null;
        }

        private NativeReference<int> m_ElementIndex;

        public ref T Element => ref s_ElementList[m_ElementIndex.Value].Element;

        public ManagedReference(ref T element)
        {
            m_ElementIndex = new NativeReference<int>(AllocateElement(ref element), Allocator.Persistent);
        }

        public void Dispose()
        {
            if (m_ElementIndex.IsCreated)
            {
                DeallocateElement(m_ElementIndex.Value);
                m_ElementIndex.Dispose();
            }
        }
    }
}
