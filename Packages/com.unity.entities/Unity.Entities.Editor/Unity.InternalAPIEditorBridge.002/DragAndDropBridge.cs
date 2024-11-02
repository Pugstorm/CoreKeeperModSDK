using UnityEditor;
using UnityEngine;

namespace Unity.Editor.Bridge
{
    static class DragAndDropBridge
    {
        public static DragAndDropVisualMode DropOnHierarchyWindow(int dropTargetInstanceID, HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform)
        {
            return DragAndDrop.DropOnHierarchyWindow(dropTargetInstanceID, dropMode, parentForDraggedObjects, perform);
        }
    }
}
