using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Editor
{
    class HierarchySelectionContext : ScriptableObject
    {
        [SerializeField] HierarchyNodeHandle m_HierarchyNodeHandle;

        public HierarchyNodeHandle Handle => m_HierarchyNodeHandle;
        
        public static HierarchySelectionContext CreateInstance(HierarchyNodeHandle handle)
        {
            var context = CreateInstance<HierarchySelectionContext>();
            context.m_HierarchyNodeHandle = handle;
            context.hideFlags = HideFlags.HideAndDontSave;
            Undo.RegisterCreatedObjectUndo(context, "Create HierarchySelectionContext");
            return context;
        }
    }
}