using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;

namespace Unity.Editor.Bridge
{
    static class SubSceneGUIBridge
    {
        static readonly MenuWrapper k_MenuWrapper = new MenuWrapper();

        public static void CreateClosedSubSceneContextClick(DropdownMenu menu, SceneAsset subSceneSceneAsset)
        {
            SubSceneGUI.CreateClosedSubSceneContextClick(k_MenuWrapper.GenericMenu, new SceneHierarchyHooks.SubSceneInfo() { sceneAsset = subSceneSceneAsset });
            k_MenuWrapper.ApplyGenericMenuItemsTo(menu);
        }
    }
}
