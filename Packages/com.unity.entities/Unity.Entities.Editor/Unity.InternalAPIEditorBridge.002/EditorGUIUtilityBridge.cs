using UnityEditor;
using UnityEngine;

namespace Unity.Editor.Bridge
{
    static class EditorGUIUtilityBridge
    {
        public static GUIContent TempContent(string s) => EditorGUIUtility.TempContent(s);
        public static GUIContent TempContent(string s, Texture t) => EditorGUIUtility.TempContent(s, t);
    }
}
