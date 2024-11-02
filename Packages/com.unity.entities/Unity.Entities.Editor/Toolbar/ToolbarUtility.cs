using System;
using System.Reflection;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    static class ToolbarUtility
    {
        static VisualElement s_MainToolbarElement;
        static VisualElement s_PlayModeToolbarElement;

        public static VisualElement GetMainToolbarRoot()
        {
            if (s_MainToolbarElement != null)
                return s_MainToolbarElement;

            // Get Toolbar static instance
            var toolbar = Type.GetType("UnityEditor.Toolbar, UnityEditor")?.GetField("get", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (toolbar == null)
                return null;

            // Get Toolbar.windowBackend
            var windowBackend = Type.GetType("UnityEditor.GUIView, UnityEditor")?.GetProperty("windowBackend", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(toolbar);
            if (windowBackend == null)
                return null;

            // Get Toolbar.windowBackend.visualTree
            var visualTree = (VisualElement)Type.GetType("UnityEditor.IWindowBackend, UnityEditor")?.GetProperty("visualTree")?.GetValue(windowBackend);
            if (visualTree == null)
                return null;

            s_MainToolbarElement = visualTree;
            return s_MainToolbarElement;
        }

        public static VisualElement GetPlayModeToolbarRoot()
        {
            if (s_PlayModeToolbarElement != null)
                return s_PlayModeToolbarElement;

            s_PlayModeToolbarElement = GetMainToolbarRoot()?.Q("ToolbarZonePlayMode");
            return s_PlayModeToolbarElement;
        }
    }
}
