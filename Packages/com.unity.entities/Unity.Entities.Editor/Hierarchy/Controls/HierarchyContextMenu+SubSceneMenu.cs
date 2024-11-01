using System.Linq;
using Unity.Editor.Bridge;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    partial class HierarchyContextMenu
    {
        void BuildSubSceneContextMenu(DropdownMenu menu)
        {
            // For Sub Scenes GameObjects, have menu items for cut, paste and delete.
            // Not copy or duplicate, since multiple of the same Sub Scene is not supported anyway.

            menu.AppendAction(L10n.Tr("Cut"), _ => ClipboardUtilityBridge.CutGameObject());
            menu.AppendAction(L10n.Tr("Paste"), _ => ClipboardUtilityBridge.PasteGameObject(null), // TODO: Use custom parent when in prefab stage
                              ClipboardUtilityBridge.CanGameObjectsBePasted() || ClipboardUtilityBridge.CanPasteGameObjectsFromPasteboard()
                                  ? DropdownMenuAction.Status.Normal
                                  : DropdownMenuAction.Status.Disabled);
            menu.AppendAction(L10n.Tr("Paste As Child"), _ => ClipboardUtilityBridge.PasteGameObjectAsChild(),
                              ClipboardUtilityBridge.CanPasteAsChild()
                                  ? DropdownMenuAction.Status.Normal
                                  : DropdownMenuAction.Status.Disabled);

            menu.AppendSeparator();

            menu.AppendAction(L10n.Tr("Delete"), _=> Object.DestroyImmediate(Selection.activeGameObject));
        }
    }
}
