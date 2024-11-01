#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
using System;
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
        /// Array of <see cref="EntityView"/>.
        /// </summary>
        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "(UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING")]
        [DebuggerDisplay("Length = {Length}")]
        [DebuggerTypeProxy(typeof(EntityViewArrayDebugView))]
        [StructLayout(LayoutKind.Sequential)]
        public unsafe readonly struct EntityViewArray : IEnumerable<EntityView>
        {
            [NativeDisableUnsafePtrRestriction] readonly ulong* m_WorldSequencePtr;
            [NativeDisableUnsafePtrRestriction] readonly Entity* m_EntityPtr;
            readonly int m_EntityCount;

            /// <summary>
            /// Number of entity in the array.
            /// </summary>
            public int Length => m_EntityCount;

            /// <summary>
            /// Get the entity at the specified index.
            /// </summary>
            /// <param name="index">The element index.</param>
            public EntityView this[int index]
            {
                get
                {
                    CollectionHelper.CheckIndexInRange(index, Length);
                    return new EntityView(&m_EntityPtr[CollectionHelper.AssumePositive(index)], m_WorldSequencePtr);
                }
            }

            /// <summary>
            /// Retrieve the index of the entity view in the array.
            /// </summary>
            /// <param name="entityView">The entity view.</param>
            /// <returns>The index of the entity view if found, otherwise -1.</returns>
            public int IndexOf(EntityView entityView)
            {
                if (entityView.m_EntityPtr < m_EntityPtr || entityView.m_EntityPtr >= m_EntityPtr + m_EntityCount)
                    return -1;

                return (int)(entityView.m_EntityPtr - m_EntityPtr);
            }

            /// <summary>
            /// Convert array to native array.
            /// </summary>
            /// <param name="allocator">The Allocator of the NativeArray.</param>
            /// <returns>A native array of entity views.</returns>
            public NativeArray<EntityView> ToNativeArray(AllocatorManager.AllocatorHandle allocator)
            {
                // Todo: When NativeArray supports custom allocators, remove these .ToAllocator callsites DOTS-7695
                var array = new NativeArray<EntityView>(m_EntityCount, allocator.ToAllocator);
                for (var i = 0; i < m_EntityCount; ++i)
                    array[i] = this[i];
                return array;
            }

            /// <summary>
            /// Convert array to managed array.
            /// </summary>
            /// <returns>A managed array of entity views.</returns>
            [ExcludeFromBurstCompatTesting("Returns managed array")]
            public EntityView[] ToArray()
            {
                var array = new EntityView[m_EntityCount];
                for (var i = 0; i < m_EntityCount; ++i)
                    array[i] = this[i];
                return array;
            }

            /// <summary>
            /// Returns an enumerator that can iterate through the <see cref="EntityViewArray"/>.
            /// </summary>
            public Enumerator GetEnumerator() => new Enumerator(this);

            /// <summary>
            /// Returns an enumerator that can iterate through the <see cref="EntityViewArray"/>.
            /// </summary>
            [ExcludeFromBurstCompatTesting("Returns managed object")]
            IEnumerator<EntityView> IEnumerable<EntityView>.GetEnumerator() => GetEnumerator();

            /// <summary>
            /// Returns an enumerator that can iterate through the <see cref="EntityViewArray"/>.
            /// </summary>
            [ExcludeFromBurstCompatTesting("Returns managed object")]
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            /// <summary>
            /// Enumerator that can iterate through the <see cref="EntityViewArray"/>.
            /// </summary>
            [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "(UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING")]
            public struct Enumerator : IEnumerator<EntityView>
            {
                readonly EntityViewArray m_RecordViewArray;
                int m_Index;

                public EntityView Current => m_RecordViewArray[m_Index];

                [ExcludeFromBurstCompatTesting("Returns managed object")]
                object IEnumerator.Current => Current;

                internal Enumerator(EntityViewArray recordViewArray)
                {
                    m_RecordViewArray = recordViewArray;
                    m_Index = -1;
                }

                public void Dispose() { }
                public bool MoveNext() => ++m_Index < m_RecordViewArray.m_EntityCount;
                public void Reset() => m_Index = -1;
            }

            internal EntityViewArray(Entity* entityPtr, int entityCount, ulong* worldSequencePtr)
            {
                m_WorldSequencePtr = worldSequencePtr;
                m_EntityPtr = entityPtr;
                m_EntityCount = entityCount;
            }
        }

        internal sealed class EntityViewArrayDebugView
        {
            readonly EntityViewArray m_EntityViewArray;

            public EntityViewArrayDebugView(EntityViewArray entityViewArray)
            {
                m_EntityViewArray = entityViewArray;
            }

            public EntityView[] Items => m_EntityViewArray.ToArray();
        }
    }
}
#endif
