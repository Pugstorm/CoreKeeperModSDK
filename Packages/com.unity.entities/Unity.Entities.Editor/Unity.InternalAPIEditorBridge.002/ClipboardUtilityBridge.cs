using UnityEditor;
using UnityEngine;

namespace Unity.Editor.Bridge
{
    static class ClipboardUtilityBridge
    {
        public static void CutGameObject() => ClipboardUtility.CutGO();

        public static void CopyGameObject() => ClipboardUtility.CopyGO();

        public static bool CanPasteGameObjectsFromPasteboard() => Unsupported.CanPasteGameObjectsFromPasteboard();

        public static void PasteGameObjectAsChild() => ClipboardUtility.PasteGOAsChild();

        public static bool CanPasteAsChild() => ClipboardUtility.CanPasteAsChild();

        public static bool CanGameObjectsBePasted() => CutBoard.CanGameObjectsBePasted();

        public static void PasteGameObject(Transform fallbackParent) => ClipboardUtility.PasteGO(fallbackParent);

        public static void DuplicateGameObject(Transform fallbackParent) => ClipboardUtility.DuplicateGO(fallbackParent);

        public static void SetString(string value) => Clipboard.stringValue = value;
    }
}
