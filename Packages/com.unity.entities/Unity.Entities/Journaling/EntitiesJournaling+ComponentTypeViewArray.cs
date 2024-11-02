#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities
{
    partial class EntitiesJournaling
    {
        /// <summary>
        /// Array of <see cref="ComponentTypeView"/>.
        /// </summary>
        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "(UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING")]
        [DebuggerDisplay("Length = {Length}")]
        [DebuggerTypeProxy(typeof(ComponentTypeViewArrayDebugView))]
        [StructLayout(LayoutKind.Sequential)]
        public unsafe readonly struct ComponentTypeViewArray : IEnumerable<ComponentTypeView>
        {
            [NativeDisableUnsafePtrRestriction] readonly TypeIndex* m_TypeIndexPtr;
            readonly int m_TypeIndexCount;

            /// <summary>
            /// Number of component type in the array.
            /// </summary>
            public int Length => m_TypeIndexCount;

            /// <summary>
            /// Get the component type at the specified index.
            /// </summary>
            /// <param name="index">The element index.</param>
            public ComponentTypeView this[int index]
            {
                get
                {
                    CollectionHelper.CheckIndexInRange(index, Length);
                    return new ComponentTypeView(&m_TypeIndexPtr[CollectionHelper.AssumePositive(index)]);
                }
            }

            /// <summary>
            /// Retrieve the index of the component type view in the array.
            /// </summary>
            /// <param name="componentTypeView">The component type view.</param>
            /// <returns>The index of the component type view if found, otherwise -1.</returns>
            public int IndexOf(ComponentTypeView componentTypeView)
            {
                if (componentTypeView.m_TypeIndexPtr < m_TypeIndexPtr || componentTypeView.m_TypeIndexPtr >= m_TypeIndexPtr + m_TypeIndexCount)
                    return -1;

                return (int)(componentTypeView.m_TypeIndexPtr - m_TypeIndexPtr);
            }

            /// <summary>
            /// Convert array to native array.
            /// </summary>
            /// <param name="allocator">The Allocator of the NativeArray.</param>
            public NativeArray<ComponentTypeView> ToNativeArray(AllocatorManager.AllocatorHandle allocator)
            {
                // Todo: When NativeArray supports custom allocators, remove these .ToAllocator callsites DOTS-7695
                var array = new NativeArray<ComponentTypeView>(m_TypeIndexCount, allocator.ToAllocator);
                for (var i = 0; i < m_TypeIndexCount; ++i)
                    array[i] = this[i];
                return array;
            }

            /// <summary>
            /// Convert array to managed array.
            /// </summary>
            [ExcludeFromBurstCompatTesting("Returns managed array")]
            public ComponentTypeView[] ToArray()
            {
                var array = new ComponentTypeView[m_TypeIndexCount];
                for (var i = 0; i < m_TypeIndexCount; ++i)
                    array[i] = this[i];
                return array;
            }

            /// <summary>
            /// Returns an enumerator that can iterate through the <see cref="ComponentTypeViewArray"/>.
            /// </summary>
            public Enumerator GetEnumerator() => new Enumerator(this);

            /// <summary>
            /// Returns an enumerator that can iterate through the <see cref="ComponentTypeViewArray"/>.
            /// </summary>
            [ExcludeFromBurstCompatTesting("Returns managed object")]
            IEnumerator<ComponentTypeView> IEnumerable<ComponentTypeView>.GetEnumerator() => GetEnumerator();

            /// <summary>
            /// Returns an enumerator that can iterate through the <see cref="ComponentTypeViewArray"/>.
            /// </summary>
            [ExcludeFromBurstCompatTesting("Returns managed object")]
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            /// <summary>
            /// Enumerator that can iterate through the <see cref="ComponentTypeViewArray"/>.
            /// </summary>
            [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "(UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING")]
            public struct Enumerator : IEnumerator<ComponentTypeView>
            {
                readonly ComponentTypeViewArray m_ComponentTypeViewArray;
                int m_Index;

                public ComponentTypeView Current => m_ComponentTypeViewArray[m_Index];

                [ExcludeFromBurstCompatTesting("Returns managed object")]
                object IEnumerator.Current => Current;

                internal Enumerator(ComponentTypeViewArray recordViewArray)
                {
                    m_ComponentTypeViewArray = recordViewArray;
                    m_Index = -1;
                }

                public void Dispose() { }
                public bool MoveNext() => ++m_Index < m_ComponentTypeViewArray.m_TypeIndexCount;
                public void Reset() => m_Index = -1;
            }

            internal ComponentTypeViewArray(TypeIndex* typeIndexPtr, int typeIndexCount)
            {
                m_TypeIndexPtr = typeIndexPtr;
                m_TypeIndexCount = typeIndexCount;
            }
        }

        internal sealed class ComponentTypeViewArrayDebugView
        {
            readonly ComponentTypeViewArray m_ComponentTypeViewArray;

            public ComponentTypeViewArrayDebugView(ComponentTypeViewArray entityViewArray)
            {
                m_ComponentTypeViewArray = entityViewArray;
            }

            public ComponentTypeView[] Items => m_ComponentTypeViewArray.ToArray();
        }
    }
}
#endif
