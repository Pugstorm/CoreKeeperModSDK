using System;
using UnityEditor;
using UnityEngine;

namespace Unity.Editor.Bridge
{
    static class EditorWindowBridge
    {
        public static event Action<UnityEditor.Editor, GameObject> FinishedDefaultHeaderGUIForGameObjectInspector;

        public static void ClearPersistentViewData(EditorWindow window) => window.ClearPersistentViewData();

        public static T[] GetEditorWindowInstances<T>() where T : EditorWindow => Resources.FindObjectsOfTypeAll<T>();

        static EditorWindowBridge()
        {
            UnityEditor.Editor.finishedDefaultHeaderGUI += editor =>
            {
                // Only pass the event if we are inspecting the default inspector
                // for GameObjects and if its main target is valid.
                if (editor is GameObjectInspector && editor.target is GameObject target)
                    FinishedDefaultHeaderGUIForGameObjectInspector?.Invoke(editor, target);
            };
        }

        public static void ReloadHostView(this EditorWindow @this)
        {
            var hostView = @this.m_Parent;
            if (hostView is null)
                throw new ArgumentNullException();

            hostView.Reload(@this);
        }

        public static DockArea GetDockArea(this EditorWindow @this)
        {
            if (@this.m_Parent is UnityEditor.DockArea dockArea)
                return new DockArea(dockArea);

            return null;
        }

        internal class DockArea
        {
            readonly UnityEditor.DockArea m_DockArea;

            public DockArea(UnityEditor.DockArea dockArea)
            {
                m_DockArea = dockArea;
            }

            public void AddTab(EditorWindow windowToDock)
                => m_DockArea.AddTab(windowToDock);

            public bool IsValid => m_DockArea;
        }
    }
}
