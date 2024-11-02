using System;
using System.Linq;
using Unity.Editor.Bridge;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.Entities.Editor
{
    partial class HierarchyContextMenu
    {
        static readonly MenuWrapper k_MenuWrapper = new MenuWrapper();

        void AddCreateGameObjectItemsToSubSceneMenu(DropdownMenu menu, Scene scene)
        {
            AddCreateGameObjectItemsToMenu(menu, Array.Empty<Object>(), false, true, true, scene.handle, MenuUtilsBridge.ContextMenuOrigin.Subscene);
        }

        void AddCreateGameObjectItemsToSceneMenu(DropdownMenu menu, Scene scene)
        {
            AddCreateGameObjectItemsToMenu(menu, Selection.transforms.Select(t => t.gameObject).ToArray(), false, false, true, scene.handle, MenuUtilsBridge.ContextMenuOrigin.Scene);
        }

        void BuildGameObjectContextMenu(DropdownMenu menu, GameObject gameObject, VisualElement element)
        {
            menu.AppendAction(L10n.Tr("Cut"), _ => ClipboardUtilityBridge.CutGameObject(), gameObject != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction(L10n.Tr("Copy"), _ => ClipboardUtilityBridge.CopyGameObject(), gameObject != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction(L10n.Tr("Paste"), _ => ClipboardUtilityBridge.PasteGameObject(null),
                              ClipboardUtilityBridge.CanGameObjectsBePasted() || ClipboardUtilityBridge.CanPasteGameObjectsFromPasteboard()
                                  ? DropdownMenuAction.Status.Normal
                                  : DropdownMenuAction.Status.Disabled);
            menu.AppendAction(L10n.Tr("Paste As Child"), _ => ClipboardUtilityBridge.PasteGameObjectAsChild(),
                              ClipboardUtilityBridge.CanPasteAsChild()
                                  ? DropdownMenuAction.Status.Normal
                                  : DropdownMenuAction.Status.Disabled);

            menu.AppendSeparator();

            var item = element as HierarchyListViewItem;

            menu.AppendAction(L10n.Tr("Rename"), _ => item.BeginRename(), gameObject != null && null != item ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction(L10n.Tr("Duplicate"), _ => ClipboardUtilityBridge.DuplicateGameObject(null), gameObject != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction(L10n.Tr("Delete"), _=> DeleteGameObject(gameObject), gameObject != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            menu.AppendSeparator();

            // All Create GameObject menu items
            {
                menu.AppendSeparator();

                var targetSceneForCreation = SceneManager.GetActiveScene().handle;

                // Set the context of each MenuItem to the current selection, so the created gameobjects will be added as children
                // Sets includeCreateEmptyChild to false, since that item is superfluous here (the normal "Create Empty" is added as a child anyway)
                AddCreateGameObjectItemsToMenu(menu,
                                               gameObject != null ? new[] { gameObject } : Array.Empty<GameObject>(),
                                               false,
                                               false,
                                               false,
                                               targetSceneForCreation,
                                               gameObject == null
                                                   ? MenuUtilsBridge.ContextMenuOrigin.None
                                                   : MenuUtilsBridge.ContextMenuOrigin.GameObject);
            }

            SceneHierarchyHooksBridge.AddCustomGameObjectContextMenuItems(menu, gameObject);

            if (item != null)
            {
                menu.AppendSeparator();
                menu.AppendAction(L10n.Tr("Properties..."), _ => PropertyEditorBridge.OpenPropertyEditorOnSelection());
            }
        }

        void AddCreateGameObjectItemsToMenu(DropdownMenu menu,
                                            UnityEngine.Object[] context,
                                            bool includeCreateEmptyChild,
                                            bool useCreateEmptyParentMenuItem,
                                            bool includeGameObjectInPath,
                                            int targetSceneHandle,
                                            MenuUtilsBridge.ContextMenuOrigin origin)
        {
            MenuUtilsBridge.AddCreateGameObjectItemsToMenu(k_MenuWrapper.GenericMenu, context, includeCreateEmptyChild, useCreateEmptyParentMenuItem, includeGameObjectInPath, targetSceneHandle, origin);
            k_MenuWrapper.ApplyGenericMenuItemsTo(menu);
        }

        static void DeleteGameObject(GameObject gameObject)
        {
            Assert.AreEqual(gameObject, Selection.activeObject);
            // Intentional use of `Unsupported.DeleteGameObjectSelection` instead of `Object.DestroyImmediate`
            // It takes care of warning the user about removing objects from prefabs, handles Undo, etc.
            Unsupported.DeleteGameObjectSelection();
        }

        void SelectPrefabRoot(GameObject gameObject)
        {
            if (!gameObject)
                return;

            var prefabInstanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
            if (prefabInstanceRoot)
            {
                var handle = HierarchyNodeHandle.FromGameObject(prefabInstanceRoot);
                if (m_Hierarchy.GetNodes().Exists(handle))
                    m_HierarchyElement.SetSelection(handle);
            }
        }
    }
}

