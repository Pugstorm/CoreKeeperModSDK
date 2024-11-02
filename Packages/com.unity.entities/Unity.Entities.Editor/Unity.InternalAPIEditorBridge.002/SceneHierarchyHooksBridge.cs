using System;
using Unity.Scenes;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Unity.Editor.Bridge
{
    class SceneHierarchyHooksBridge
    {
        static readonly MenuWrapper k_MenuWrapper = new MenuWrapper();

        internal static void AddCustomSceneHeaderContextMenuItems(DropdownMenu menu, Scene scene)
        {
            SceneHierarchyHooks.AddCustomSceneHeaderContextMenuItems(k_MenuWrapper.GenericMenu, scene);
            k_MenuWrapper.ApplyGenericMenuItemsTo(menu);
        }

        public static void AddCustomGameObjectContextMenuItems(DropdownMenu menu, GameObject gameObject)
        {
            SceneHierarchyHooks.AddCustomGameObjectContextMenuItems(k_MenuWrapper.GenericMenu, gameObject);
            k_MenuWrapper.ApplyGenericMenuItemsTo(menu);
        }

        public static void AddCustomSubSceneHeaderContextMenuItems(DropdownMenu menu, SubScene subScene)
        {
            SceneHierarchyHooks.AddCustomSubSceneHeaderContextMenuItems(k_MenuWrapper.GenericMenu, new SceneHierarchyHooks.SubSceneInfo()
            {
                scene = subScene.EditingScene,
                sceneAsset = subScene.SceneAsset,
                sceneName = subScene.SceneAsset ? subScene.SceneName : string.Empty
            });
            k_MenuWrapper.ApplyGenericMenuItemsTo(menu);
        }
    }
}
