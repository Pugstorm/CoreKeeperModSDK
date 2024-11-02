using System;
using Unity.Collections;

namespace Unity.Entities.Editor
{
    [Flags]
    enum HierarchyNodeFlags
    {
        None = 0,
        ChildrenRequireSorting = 1 << 0,
        IsPrefabStage = 1 << 1,
        Disabled = 1 << 2
    }

    /// <summary>
    /// The <see cref="HierarchyNode"/> represents a high level node which can be used to mutate the state of the hierarchy.
    /// </summary>
    [GenerateTestsForBurstCompatibility]
    readonly partial struct HierarchyNode : IEquatable<HierarchyNode>
    {
        readonly HierarchyNodeStore m_Hierarchy;
        readonly HierarchyNodeHandle m_Handle;

        internal HierarchyNode(HierarchyNodeStore hierarchy, HierarchyNodeHandle handle)
        {
            m_Hierarchy = hierarchy;
            m_Handle = handle;

            if (!m_Hierarchy.Exists(handle))
                throw new ArgumentException($"Unable to create {nameof(HierarchyNodeHandle)}. The specified handle does not exist in the hierarchy.");
        }

        public HierarchyNodeHandle GetHandle() 
            => m_Handle;

        public int GetSortIndex()
            => m_Hierarchy.GetSortIndex(m_Handle);
        
        public void SetSortIndex(int index)
            => m_Hierarchy.SetSortIndex(m_Handle, index);

        public int GetDepth()
            => m_Hierarchy.GetDepth(m_Handle);
        
        public HierarchyNode GetParent()
            => m_Hierarchy.GetNode(m_Hierarchy.GetParent(m_Handle));
        
        public void SetParent(HierarchyNodeHandle parent)
            => m_Hierarchy.SetParent(m_Handle, parent);

        public void AddChild(HierarchyNodeHandle child)
        {
            if (!m_Hierarchy.Exists(child))
                m_Hierarchy.AddNode(child, m_Handle);
            else
                m_Hierarchy.SetParent(child, m_Handle);
        }

        public int GetChildCount()
            => m_Hierarchy.GetChildCount(m_Handle);

        public HierarchyNode[] GetChildren()
            => m_Hierarchy.GetChildren(m_Handle);

        public HierarchyNodeFlags GetFlags()
            => m_Hierarchy.GetFlags(m_Handle);

        public bool Equals(HierarchyNode other)
            => m_Handle.Equals(other.m_Handle);

        public override bool Equals(object obj)
            => obj is HierarchyNode other && Equals(other);

        public override int GetHashCode()
            => m_Handle.GetHashCode();
        
        public static implicit operator HierarchyNodeHandle(HierarchyNode node) 
            => node.m_Handle;
    }
}