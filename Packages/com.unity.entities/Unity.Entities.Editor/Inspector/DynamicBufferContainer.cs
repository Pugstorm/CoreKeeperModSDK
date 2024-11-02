using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Properties;
using Unity.Properties.Internal;

namespace Unity.Entities
{
    readonly unsafe struct DynamicBufferContainer<TElement> : IList<TElement>
        where TElement : unmanaged, IBufferElementData
    {
        static DynamicBufferContainer()
        {
            PropertyBag.Register(new IndexedCollectionPropertyBag<DynamicBufferContainer<TElement>, TElement>());
        }

        public bool IsReadOnly { get; }
        TypeIndex TypeIndex { get; }
        EntityContainer EntityContainer { get; }
        EntityManager EntityManager { get; }
        Entity Entity { get; }

        bool Exists() => EntityContainer.Exists() && EntityContainer.EntityManager.HasComponentRaw(Entity, TypeIndex);

        BufferHeader* Header
        {
            get
            {
                if (Exists())
                    return (BufferHeader*) EntityManager.GetComponentDataRawRO(Entity, TypeIndex);
                return null;
            }
        }

        void* ReadOnlyBuffer => EntityManager.GetBufferRawRO(Entity, TypeIndex);
        void* ReadWriteBuffer => EntityManager.GetBufferRawRW(Entity, TypeIndex);
        public int Count => null != Header ? Header->Length : 0;

        public DynamicBufferContainer(EntityContainer entityContainer, TypeIndex typeIndex, bool readOnly = true)
        {
            IsReadOnly = readOnly;
            EntityContainer = entityContainer;
            TypeIndex = typeIndex;
            EntityManager = EntityContainer.EntityManager;
            Entity = EntityContainer.Entity;
        }

        public TElement this[int index]
        {
            get
            {
                CheckBounds(index);
                return UnsafeUtility.ReadArrayElement<TElement>(ReadOnlyBuffer, index);
            }
            set
            {
                // @FIXME
                //
                // In C# despite being `readonly` a list can have it's elements mutated, however for ECS data we have strict access writes.
                // For now we opt to silently skip until a proper fix is implemented.
                //
                // In order to properly fix this we need either:
                //
                // 1) A custom property bag for DynamicBufferContainer`1 which correctly sets IsReadOnly per element property.
                //    * While this is a more elegant solution we lose the built in machinery around ListPropertyBag`1. e.g. UI would not be quite right.
                //
                // 2) A fix directly in ListPropertyBag`1 to allow controlling IsReadOnly per element.
                //    * This is a best place to fix it but requires a package update of properties.
                //
                if (IsReadOnly)
                    return;

                CheckBounds(index);
                UnsafeUtility.WriteArrayElement(ReadWriteBuffer, index, value);
            }
        }

        public IEnumerator<TElement> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(TElement item)
        {
            var buffer = EntityManager.GetBuffer<TElement>(Entity);
            buffer.Add(item);
        }

        public void Clear()
        {
            var buffer = EntityManager.GetBuffer<TElement>(Entity);
            buffer.Clear();
        }

        public bool Contains(TElement item)
        {
            for (var i = 0; i < Count; ++i)
            {
                if (EqualityComparer<TElement>.Default.Equals(item, this[i]))
                    return true;
            }

            return false;
        }

        public void CopyTo(TElement[] array, int arrayIndex)
        {
            for (int toIndex = arrayIndex, fromIndex = 0; toIndex < arrayIndex + Count; ++toIndex, ++fromIndex)
            {
                array[toIndex] = this[fromIndex];
            }
        }

        public bool Remove(TElement item)
        {
            for (var i = 0; i < Count; ++i)
            {
                if (!EqualityComparer<TElement>.Default.Equals(item, this[i]))
                    continue;

                EntityManager.GetBuffer<TElement>(Entity).RemoveAt(i);
                return true;
            }
            return true;
        }

        public int IndexOf(TElement item)
        {
            for (var i = 0; i < Count; ++i)
            {
                if (!EqualityComparer<TElement>.Default.Equals(item, this[i]))
                    continue;

                return i;
            }
            return -1;
        }

        public void Insert(int index, TElement item)
        {
            var buffer = EntityManager.GetBuffer<TElement>(Entity);
            buffer.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            var buffer = EntityManager.GetBuffer<TElement>(Entity);
            buffer.RemoveAt(index);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckBounds(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if ((uint)index >= (uint)Count)
                throw new IndexOutOfRangeException($"Index {index} is out of range in DynamicBufferContainer of '{Count}' Count.");
#endif
        }

        public override int GetHashCode()
        {
            return (int)math.hash(new uint2((uint)ReadOnlyBuffer, (uint)Count));
        }
    }
}
