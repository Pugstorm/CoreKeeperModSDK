using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;

namespace Unity.Entities.Editor
{
    [GenerateTestsForBurstCompatibility]
    readonly partial struct HierarchyNode
    {
        /// <summary>
        /// The <see cref="Immutable"/> struct represents a high level node over the <see cref="HierarchyNodeStore.Immutable"/> out hierarchy model.
        /// </summary>
        [GenerateTestsForBurstCompatibility]
        public readonly struct Immutable
        {
            readonly HierarchyNodeStore.Immutable m_Hierarchy;
            readonly int m_Index;
            readonly int m_ChangeVersion;

            internal Immutable(HierarchyNodeStore.Immutable hierarchy, int index, int changeVersion)
            {
                m_Hierarchy = hierarchy;
                m_Index = index;
                m_ChangeVersion = changeVersion;
            }

            void CheckValid()
            {
                if (m_Hierarchy.ChangeVersion != m_ChangeVersion)
                    throw new InvalidOperationException("HierarchyPackedNodeStore version has changed. This is likely due to a repacking.");
            }

            /// <summary>
            /// Gets the handle this node was created from.
            /// </summary>
            /// <returns>The handle for this node.</returns>
            public HierarchyNodeHandle GetHandle()
            {
                CheckValid();
                return m_Hierarchy[m_Index].Handle;
            }

            /// <summary>
            /// Gets the change version for this node.
            /// </summary>
            /// <returns>The change version for this node.</returns>
            public int GetChangeVersion()
            {
                return m_ChangeVersion;
            }

            /// <summary>
            /// Gets the depth for the node.
            /// </summary>
            /// <returns>The depth for the node.</returns>
            public int GetDepth()
            {
                CheckValid();
                return m_Hierarchy[m_Index].Depth;
            }

            /// <summary>
            /// Gets the number of children for this node.
            /// </summary>
            /// <returns>The number of children this node has.</returns>
            public int GetChildCount()
            {
                CheckValid();
                return m_Hierarchy[m_Index].ChildCount;
            }

            /// <summary>
            /// Gets the parent for this node.
            /// </summary>
            /// <returns>The parent for this node.</returns>
            public Immutable GetParent()
            {
                CheckValid();
                return new Immutable(m_Hierarchy, m_Index + m_Hierarchy[m_Index].ParentOffset, m_ChangeVersion);
            }

            /// <summary>
            /// Gets all children for this node and returns it as a new array.
            /// </summary>
            /// <returns>A new array containing all children for this node.</returns>
            public Immutable[] GetChildren()
            {
                CheckValid();

                var data = m_Hierarchy[m_Index];

                var children = new Immutable[data.ChildCount];
                var childIndex = m_Index + 1;

                for (var i = 0; i < data.ChildCount; i++)
                {
                    children[i] = new Immutable(m_Hierarchy, childIndex, m_ChangeVersion);
                    childIndex += m_Hierarchy[childIndex].NextSiblingOffset;
                }

                return children;
            }

            /// <summary>
            /// Gets all children for this node and adds them to the given list.
            /// </summary>
            /// <param name="children">The list to add children to.</param>
            public void GetChildren(List<Immutable> children)
            {
                CheckValid();

                children.Clear();

                var data = m_Hierarchy[m_Index];
                var childIndex = m_Index + 1;

                for (var i = 0; i < data.ChildCount; i++)
                {
                    children.Add(new Immutable(m_Hierarchy, childIndex, m_ChangeVersion));
                    childIndex += m_Hierarchy[childIndex].NextSiblingOffset;
                }
            }

            public Enumerator GetEnumerator()
            {
                CheckValid();

                var data = m_Hierarchy[m_Index];
                var childIndex = m_Index + 1;
                return new Enumerator(m_Hierarchy, data.ChildCount, m_ChangeVersion, childIndex);
            }

            internal struct Enumerator : IEnumerator<Immutable>
            {
                readonly HierarchyNodeStore.Immutable m_Hierarchy;
                readonly int m_TotalChildrenCount;
                readonly int m_ChangeVersion;

                int m_ChildIndex;
                int m_CurrentIndex;

                public Enumerator(HierarchyNodeStore.Immutable hierarchy, int totalChildrenCount, int changeVersion, int childIndex)
                {
                    m_Hierarchy = hierarchy;
                    m_ChildIndex = childIndex;
                    m_TotalChildrenCount = totalChildrenCount;
                    m_ChangeVersion = changeVersion;
                    m_CurrentIndex = -1;
                    Current = default;
                }

                public bool MoveNext()
                {
                    if (++m_CurrentIndex < m_TotalChildrenCount)
                    {
                        Current = new Immutable(m_Hierarchy, m_ChildIndex, m_ChangeVersion);
                        m_ChildIndex += m_Hierarchy[m_ChildIndex].NextSiblingOffset;
                        return true;
                    }

                    return false;
                }

                public Immutable Current { get; private set; }

                public void Reset() => throw new NotSupportedException();

                object IEnumerator.Current => throw new NotSupportedException();

                public void Dispose() { }
            }
        }
    }
}
