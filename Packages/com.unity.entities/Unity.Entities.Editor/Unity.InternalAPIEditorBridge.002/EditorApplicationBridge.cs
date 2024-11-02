using UnityEditor;

namespace Unity.Editor.Bridge
{
    static class EditorApplicationBridge
    {
        public static void RequestRepaintAllViews() => EditorApplication.RequestRepaintAllViews();
    }
}
