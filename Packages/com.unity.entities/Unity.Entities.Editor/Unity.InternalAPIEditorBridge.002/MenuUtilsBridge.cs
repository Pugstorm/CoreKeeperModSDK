using UnityEditor;
using UnityEditor.SceneManagement;

namespace Unity.Editor.Bridge
{
    static class MenuUtilsBridge
    {
        const int kInvalidSceneHandle = 0;

        internal enum ContextMenuOrigin
        {
            GameObject,
            Scene,
            Subscene,
            Toolbar,
            None
        }

        internal static void AddCreateGameObjectItemsToMenu(GenericMenu menu, UnityEngine.Object[] context, bool includeCreateEmptyChild, bool useCreateEmptyParentMenuItem, bool includeGameObjectInPath, int targetSceneHandle, ContextMenuOrigin origin)
        {
            ScriptingMenuItem[] menus = Menu.GetMenuItems("GameObject", true, false);
            int previousMenuItemPosition = -1;

            foreach (var menuItem in menus)
            {
                string path = menuItem.path;

                UnityEngine.Object[] tempContext = context;
                if (!includeCreateEmptyChild && path.ToLower() == "GameObject/Create Empty Child".ToLower())
                    continue;

                if (!useCreateEmptyParentMenuItem && path.ToLower() == "GameObject/Create Empty Parent".ToLower())
                {
                    if (GOCreationCommands.ValidateCreateEmptyParent())
                        menu.AddItem(EditorGUIUtility.TrTextContent("Create Empty Parent"), false, GOCreationCommands.CreateEmptyParent);
                    continue;
                }

                // The first item after the GameObject creation menu items
                if (path.ToLower() == GameObjectUtility.GetFirstItemPathAfterGameObjectCreationMenuItems().ToLower())
                    continue;

                string menupath = path;

                // cut away "GameObject/"
                if (!includeGameObjectInPath)
                    menupath = path.Substring(11);

                MenuUtils.ExtractOnlyEnabledMenuItem(menuItem,
                    menu,
                    menupath,
                    tempContext,
                    targetSceneHandle,
                    BeforeCreateGameObjectMenuItemWasExecuted,
                    AfterCreateGameObjectMenuItemWasExecuted,
                    (MenuUtils.ContextMenuOrigin)origin,
                    previousMenuItemPosition);

                previousMenuItemPosition = menuItem.priority;
            }

            MenuUtils.RemoveInvalidMenuItems(menu);
        }

        static void BeforeCreateGameObjectMenuItemWasExecuted(string menuPath, UnityEngine.Object[] contextObjects, MenuUtils.ContextMenuOrigin origin, int userData)
        {
            int sceneHandle = userData;
            if (origin == MenuUtils.ContextMenuOrigin.Scene || origin == MenuUtils.ContextMenuOrigin.Subscene)
                GOCreationCommands.forcePlaceObjectsAtWorldOrigin = true;
            EditorSceneManager.SetTargetSceneForNewGameObjects(sceneHandle);
        }

        static void AfterCreateGameObjectMenuItemWasExecuted(string menuPath, UnityEngine.Object[] contextObjects, MenuUtils.ContextMenuOrigin origin, int userData)
        {
            EditorSceneManager.SetTargetSceneForNewGameObjects(kInvalidSceneHandle);
            GOCreationCommands.forcePlaceObjectsAtWorldOrigin = false;
            // Ensure framing when creating game objects even if we are locked
            // if (isLocked)
            //     m_FrameOnSelectionSync = true;
        }
    }
}
