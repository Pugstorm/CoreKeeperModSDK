using System.Linq;
using Unity.Editor.Bridge;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    partial class HierarchyContextMenu
    {
        void BuildSceneContextMenu(DropdownMenu menu, Scene scene)
        {
            var hasMultipleScenes = EditorSceneManager.sceneCount > 1;

            // Set active
            if (scene.isLoaded)
            {
                menu.AppendAction(L10n.Tr("Set Active Scene"),
                                  a => EditorSceneManager.SetActiveScene((Scene)a.userData),
                                  a=> hasMultipleScenes && SceneManagerBridge.CanSetAsActiveScene(scene)
                                      ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled,
                                  scene);
                menu.AppendSeparator();
            }

            // Save
            if (scene.isLoaded)
            {
                // Boxing once here instead of in each AppendAction
                object userData = (scene, isNotPlayingAndSceneIsAuthoring: !EditorApplication.isPlaying || EditorSceneManagerBridge.IsAuthoringScene(scene), hasMultipleScenes);

                menu.AppendAction(L10n.Tr("Save Scene"), SaveScene,
                                  a =>
                                  {
                                      var (_, isNotPlayingAndSceneIsAuthoring, _) = ((Scene, bool, bool))a.userData;
                                      return isNotPlayingAndSceneIsAuthoring ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
                                  },
                                  userData);

                menu.AppendAction(L10n.Tr("Save Scene As"),
                                  SaveSceneAs,
                                  a =>
                                  {
                                      var (scene, isNotPlayingAndSceneIsAuthoring, _) = ((Scene, bool, bool))a.userData;
                                      return isNotPlayingAndSceneIsAuthoring && !scene.isSubScene ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
                                  }, userData);

                menu.AppendAction(L10n.Tr("Save All"),
                                  SaveAllScenes,
                                  a =>
                                  {
                                      var (_, isNotPlayingAndSceneIsAuthoring, hasMultipleScenes) = ((Scene, bool, bool))a.userData;
                                      return isNotPlayingAndSceneIsAuthoring && hasMultipleScenes ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
                                  }, userData);

                menu.AppendSeparator();
            }

            if (!scene.isSubScene)
            {
                if (scene.isLoaded)
                {
                    // Unload
                    object userData = (scene, canUnloadScenes: !EditorApplication.isPlaying && !string.IsNullOrEmpty(scene.path) && SceneManager.sceneCount > 1);

                    menu.AppendAction(L10n.Tr("Unload Scene"), UnloadScene, a =>
                    {
                        var (_, canUnloadScenes) = ((Scene, bool))a.userData;
                        return canUnloadScenes ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
                    }, userData);
                }
                else
                {
                    // Load
                    menu.AppendAction(L10n.Tr("Load Scene"), LoadScene,
                                      a => EditorApplication.isPlaying ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal,
                                      scene);
                }

                // Remove
                menu.AppendAction(L10n.Tr("Remove Scene"), RemoveScene,
                                  a => EditorApplication.isPlaying || SceneManager.sceneCount == 1 ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal,
                                  scene);
            }

            // Discard changes
            if (scene.isLoaded)
            {
                bool canReload = scene.isDirty && CanSceneBeReloaded(scene);
                bool canDiscardChanges = !EditorApplication.isPlaying && canReload;
                var userData = (scene, canDiscardChanges);

                menu.AppendAction(L10n.Tr("Discard changes"), DiscardChanges, a =>
                {
                    var (_, canDiscardChanges) = ((Scene, bool))a.userData;
                    return canDiscardChanges ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
                }, userData);
            }

            // Ping Scene Asset
            menu.AppendSeparator();
            menu.AppendAction(L10n.Tr("Select Scene Asset"), SelectSceneAsset,
                              a => string.IsNullOrEmpty(((Scene)a.userData).path) ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal,
                              scene);

            if (!scene.isSubScene)
            {
                menu.AppendAction(L10n.Tr("Add New Scene"), AddNewScene,
                                  a => EditorApplication.isPlaying ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal,
                                  scene);
            }

            menu.AppendSeparator();
            menu.AppendAction(L10n.Tr("Prefab/Check for Unused Overrides"), RemoveAllPrefabInstancesUnusedOverridesFromSceneForMenuItem,
                              _ => DropdownMenuAction.Status.Normal,
                              scene);

            if (scene.isLoaded)
            {
                menu.AppendSeparator();
                if (scene.isSubScene)
                    AddCreateGameObjectItemsToSubSceneMenu(menu, scene);
                else
                    AddCreateGameObjectItemsToSceneMenu(menu, scene);
            }
        }

        static void RemoveAllPrefabInstancesUnusedOverridesFromSceneForMenuItem(DropdownMenuAction obj)
            => EditorSceneManagerBridge.RemoveAllPrefabInstancesUnusedOverridesFromSceneForMenuItem((Scene)obj.userData);

        static void AddNewScene(DropdownMenuAction obj)
        {
            // Check for existing untitled scene
            var untitledScene = EditorSceneManager.GetSceneByPath("");
            if (untitledScene.IsValid())
            {
                var title = L10n.Tr("Save Untitled Scene");
                var subTitle = L10n.Tr("Existing Untitled scene needs to be saved before creating a new scene. Only one untitled scene is supported at a time.");
                if (EditorUtility.DisplayDialog(title, subTitle,  L10n.Tr("Save"), L10n.Tr("Cancel")))
                {
                    if (!EditorSceneManager.SaveScene(untitledScene))
                        return;
                }
                else
                    return;
            }

            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);

            // Move new scene after context clicked scene
            Scene scene = (Scene)obj.userData;
            if (scene.IsValid())
                EditorSceneManager.MoveSceneAfter(newScene, scene);
        }

        static void SelectSceneAsset(DropdownMenuAction obj)
        {
            var scene = (Scene)obj.userData;
            var sceneObject = AssetDatabase.LoadMainAssetAtPath(scene.path);
            Selection.activeObject = sceneObject;
            EditorGUIUtility.PingObject(sceneObject);
        }

        static void SaveScene(DropdownMenuAction obj)
        {
            var (scene, _, _) = ((Scene, bool, bool))obj.userData;
            if (scene.isLoaded)
                EditorSceneManager.SaveScene(scene);
        }

        static void SaveSceneAs(DropdownMenuAction obj)
        {
            var (scene, _, _) = ((Scene, bool, bool))obj.userData;
            if (scene.isLoaded)
                EditorSceneManagerBridge.SaveSceneAs(scene);
        }

        static void SaveAllScenes(DropdownMenuAction obj) => EditorSceneManager.SaveOpenScenes();

        static void UnloadScene(DropdownMenuAction obj)
        {
            var (scene, _) = ((Scene, bool))obj.userData;
            CloseScene(removeScene: false, scene);
        }

        static void LoadScene(DropdownMenuAction obj)
        {
            var scene = (Scene)obj.userData;
            if (!scene.isLoaded)
                EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Additive);

            EditorApplicationBridge.RequestRepaintAllViews();
        }

        static void RemoveScene(DropdownMenuAction obj)
            => CloseScene(removeScene: true, (Scene)obj.userData);

        static void CloseScene(bool removeScene, Scene scene)
        {
            if (scene.isDirty)
            {
                var userCancelled = !EditorSceneManager.SaveModifiedScenesIfUserWantsTo(new[] { scene });
                if (userCancelled)
                    return;
            }

            EditorSceneManager.CloseScene(scene, removeScene);

            EditorApplicationBridge.RequestRepaintAllViews();
        }

        static bool CanSceneBeReloaded(Scene scene)
        {
            var path = scene.path;
            return !string.IsNullOrEmpty(path) && System.IO.File.Exists(path);
        }

        static bool UserAllowedDiscardingChanges(params Scene[] modifiedScenes)
        {
            string title = LocalizationDatabaseBridge.GetLocalizedString("Discard Changes");
            string message = LocalizationDatabaseBridge.GetLocalizedString("Are you sure you want to discard the changes in the following scenes:\n\n   {0}\n\nYour changes will be lost.");

            string sceneNames = string.Join("\n   ", modifiedScenes.Select(scene => scene.name).ToArray());
            message = string.Format(message, sceneNames);

            return EditorUtility.DisplayDialog(title, message, LocalizationDatabaseBridge.GetLocalizedString("OK"), LocalizationDatabaseBridge.GetLocalizedString("Cancel"));
        }

        static void DiscardChanges(DropdownMenuAction obj)
        {
            var (scene, _) = ((Scene, bool))obj.userData;

            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogWarning("Discarding changes in a scene that have not yet been saved is not supported. Save the scene first or create a new scene.");
                return;
            }

            if (!UserAllowedDiscardingChanges(scene))
                return;

            EditorSceneManagerBridge.ReloadScene(scene);
            EditorApplicationBridge.RequestRepaintAllViews();
        }
    }
}
