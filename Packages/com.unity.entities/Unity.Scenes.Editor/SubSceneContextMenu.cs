using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Unity.Scenes.Hybrid.Tests.Editmode.Content")]
[assembly: InternalsVisibleTo("Unity.Environment.Editor")]
namespace Unity.Scenes.Editor
{
    internal class SubSceneContextMenu
    {
        private const string k_DefaultFilename = "New Sub Scene";

        internal enum NewSubSceneMode
        {
            EmptyScene,
            MoveSelectionToScene
        }

        internal struct NewSubSceneArgs
        {
            public NewSubSceneArgs(GameObject target, Scene parentScene, NewSubSceneMode mode, string defaultFilename = null)
            {
                if (target == null && !parentScene.isLoaded)
                    throw new ArgumentException("Missing info for new Sub Scene: Neither GameObject target nor parent scene is valid");
                this.target = target;
                this.parentScene = target != null ? target.scene : parentScene;
                newSubSceneMode = mode;
                this.defaultFilename = defaultFilename;
            }

            public GameObject target;
            public Scene parentScene;
            public NewSubSceneMode newSubSceneMode;
            public string defaultFilename;
        }

        internal static SubScene CreateNewSubSceneAtPath(string path, NewSubSceneArgs args, InteractionMode interactionMode)
        {
            GameObject targetGameObject;
            Scene parentScene;
            GameObject[] topLevelGameObjects;
            CheckInputArguments(args, out parentScene, out targetGameObject, out topLevelGameObjects);

            return CreateSubSceneAtPathAndMoveObjectsInside(parentScene, targetGameObject?.transform, topLevelGameObjects, path, interactionMode);
        }

        internal static SubScene CreateNewSubScene(string name, NewSubSceneArgs args, InteractionMode interactionMode)
        {
            GameObject targetGameObject;
            Scene parentScene;
            GameObject[] topLevelGameObjects;
            CheckInputArguments(args, out parentScene, out targetGameObject, out topLevelGameObjects);

            return CreateSubSceneAndMoveObjectsInside(parentScene, targetGameObject?.transform, topLevelGameObjects, name, interactionMode);
        }

        static void CheckInputArguments(NewSubSceneArgs args, out Scene parentScene, out GameObject targetGameObject, out GameObject[] topLevelGameObjects)
        {
            targetGameObject = args.target;
            parentScene = targetGameObject != null ? targetGameObject.scene : args.parentScene;
            if (!parentScene.isLoaded && args.target == null)
                throw new InvalidOperationException("Creating a Sub Scene needs a target GameObject or a valid parent Scene");

            switch (args.newSubSceneMode)
            {
                case NewSubSceneMode.EmptyScene:
                    topLevelGameObjects = new GameObject[0];
                    break;
                case NewSubSceneMode.MoveSelectionToScene:
                    topLevelGameObjects = GetValidSelectedGameObjectsForSubSceneCreation(targetGameObject);
                    if (topLevelGameObjects == null)
                        throw new InvalidOperationException("Cannot create Sub Scene from Selection");
                    break;
                default:
                    throw new InvalidOperationException("Unhandled enum");
            }
        }

        internal static SubScene CreateSubSceneAndAddSelection(GameObject gameObject, InteractionMode interactionMode = InteractionMode.AutomatedAction)
        {
            var args = new NewSubSceneArgs(gameObject, default(Scene), NewSubSceneMode.MoveSelectionToScene, "New Sub Scene");

            return CreateNewSubScene(gameObject.name, args, interactionMode);
        }

        static void AddNewSubSceneMenuItems(GenericMenu menu, GameObject target)
        {
            var validTarget = GetValidSelectedGameObjectsForSubSceneCreation(target) != null;

            menu.AddSeparator("");

            var newEmptySubScene = EditorGUIUtility.TrTextContent("New Sub Scene/Empty Scene...");
            var parentScene = target != null ? target.scene : GetLastRootScene();
            var validForEmptyScene = validTarget || !string.IsNullOrEmpty(parentScene.path);
            if (!EditorApplication.isPlaying && validForEmptyScene)
                menu.AddItem(newEmptySubScene, false, OnMenuItemForNewSubScene, new NewSubSceneArgs(target, parentScene, NewSubSceneMode.EmptyScene, k_DefaultFilename));
            else
                menu.AddDisabledItem(newEmptySubScene);

            var addSubSceneContent = EditorGUIUtility.TrTextContent("New Sub Scene/From Selection...");
            if (!EditorApplication.isPlaying && validTarget)
                menu.AddItem(addSubSceneContent, false, OnMenuItemForNewSubScene, new NewSubSceneArgs(target, default(Scene), NewSubSceneMode.MoveSelectionToScene, k_DefaultFilename));
            else
                menu.AddDisabledItem(addSubSceneContent);
        }

        internal static void AddExtraGameObjectContextMenuItems(GenericMenu menu, GameObject target)
        {
            AddNewSubSceneMenuItems(menu, target);
        }

        internal static void AddExtraItemsToCreateDropdown(GenericMenu menu)
        {
            AddNewSubSceneMenuItems(menu, Selection.activeGameObject);
        }

        internal static void AddExtraSceneHeaderContextMenuItems(GenericMenu menu, Scene target)
        {
            menu.AddSeparator("");
            var newEmptySubScene = EditorGUIUtility.TrTextContent("New Empty Sub Scene...");
            var validTarget = target.isLoaded;
            if (!EditorApplication.isPlaying && validTarget)
                menu.AddItem(newEmptySubScene, false, OnMenuItemForNewSubScene, new NewSubSceneArgs(null, target,  NewSubSceneMode.EmptyScene, k_DefaultFilename));
            else
                menu.AddDisabledItem(newEmptySubScene);
        }

        static bool AreEqualOrSubDirectory(string parent, string child)
        {
            DirectoryInfo parentDir = new DirectoryInfo(parent);
            DirectoryInfo childDir = new DirectoryInfo(child);
            if (parentDir.FullName == childDir.FullName)
                return true;
            bool isParent = false;
            while (childDir.Parent != null)
            {
                if (childDir.Parent.FullName == parentDir.FullName)
                {
                    isParent = true;
                    break;
                }
                else
                    childDir = childDir.Parent;
            }

            return isParent;
        }

        static void DeleteCreatedStartFolderIfNotUsed(string startFolder, string savePath)
        {
            if (!string.IsNullOrEmpty(savePath))
            {
                var userSelectedFolder = Path.GetDirectoryName(savePath);
                if (!AreEqualOrSubDirectory(startFolder, userSelectedFolder))
                {
                    Directory.Delete(startFolder);
                }
            }
            else
            {
                Directory.Delete(startFolder);
            }
        }

        static void OnMenuItemForNewSubScene(object userData)
        {
            var args = (NewSubSceneArgs)userData;
            PromptUserForNewSubscene(args);
        }

        /// <summary>
        /// Display a file picker dialog for the user to create a new Sub Scene
        /// </summary>
        /// <param name="args">The creation settings to use</param>
        /// <returns>The created Sub Scene or null if the operation failed or was canceled</returns>
        internal static SubScene PromptUserForNewSubscene(NewSubSceneArgs args)
        {
            if (args.newSubSceneMode == NewSubSceneMode.MoveSelectionToScene)
            {
                var topLevelSelection = GetValidSelectedGameObjectsForSubSceneCreation(args.target);
                if (topLevelSelection == null)
                    return null;
                try { ThrowIfAnyGameObjectsCannotBeMovedToSubScene(topLevelSelection); }
                catch (ArgumentException e)
                {
                    EditorUtility.DisplayDialog("Cannot Move Selection To Sub Scene", e.Message, "OK");
                    return null;
                }
            }

            var parentScene = args.parentScene.isLoaded ? args.parentScene : args.target.scene;
            var parentSceneName = Path.GetFileNameWithoutExtension(parentScene.path);
            var startFolder = Path.Combine(Path.GetDirectoryName(parentScene.path), parentSceneName);
            var startPath = GetActualPathName(Path.Combine(startFolder, args.defaultFilename ?? k_DefaultFilename + ".unity"));
            if (Directory.Exists(startFolder))
                startPath = AssetDatabase.GenerateUniqueAssetPath(startPath);

            var startFolderExisted = Directory.Exists(startFolder);
            if (!startFolderExisted)
                Directory.CreateDirectory(startFolder);

            string savePath = string.Empty;
            while(true)
            {
                savePath = EditorUtility.SaveFilePanelInProject("Create new Sub Scene", Path.GetFileNameWithoutExtension(startPath), "unity", "", startFolder);
                if (string.IsNullOrEmpty(savePath))
                    break;

                if (SubScene.AllSubScenes.Any(x => x.EditableScenePath == savePath))
                {
                    EditorUtility.DisplayDialog(L10n.Tr("Sub Scene Found"), L10n.Tr("Cannot overwrite a Sub Scene that is already part of the Hierarchy. Select another file path"), L10n.Tr("Ok"));
                }
                else if (SceneManager.GetSceneByPath(savePath).IsValid())
                {
                    EditorUtility.DisplayDialog(L10n.Tr("Scene Already Open"), L10n.Tr("Cannot overwrite a Scene that is already open in the Hierarchy. Select another file path"), L10n.Tr("Ok"));
                }
                else
                {
                    break;
                }
            }

            if (!startFolderExisted)
                DeleteCreatedStartFolderIfNotUsed(startFolder, savePath);

            if (!string.IsNullOrEmpty(savePath))
            {
                // If a file exists at 'savePath' the user has already accepted to overwrite that file
                // during the SaveFilePanelInProject() call above.
                if (File.Exists(savePath))
                    AssetDatabase.DeleteAsset(savePath);

                return CreateNewSubSceneAtPath(savePath, args, InteractionMode.UserAction);
            }
            return null;
        }

        static string GetSubSceneFilePathUnderParentSceneFilePath(Scene parentScene, string newSubSceneName)
        {
            newSubSceneName = newSubSceneName.Trim();
            var srcPath = parentScene.path;
            var dstDirectory = Path.Combine(Path.GetDirectoryName(srcPath), Path.GetFileNameWithoutExtension(parentScene.path));
            var dstPath = GetActualPathName(Path.Combine(dstDirectory, newSubSceneName + ".unity"));
            return dstPath;
        }

        static GameObject[] GetValidSelectedGameObjectsForSubSceneCreation(GameObject target)
        {
            if (target == null)
                return null;
            if (!target.scene.IsValid())
                return null;
            if (string.IsNullOrEmpty(target.scene.path))
                return null;

            var selection = Selection.GetFiltered<GameObject>(SelectionMode.TopLevel);
            if (selection.Any(x => EditorUtility.IsPersistent(x)))
                return null;

            if (!selection.Contains(target))
                return null;

            return selection;
        }

        static void ThrowIfInvalidSubSceneFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || fileName.Trim() == string.Empty)
            {
                throw new ArgumentException("The provided name for the Sub Scene is empty. This is not allowed since the name is used when creating the Scene asset file.");
            }

            var invalidIndex = fileName.IndexOfAny(GetInvalidFileNameChars());
            if (invalidIndex >= 0)
            {
                char invalidChar = fileName[invalidIndex];
                var errorMessage = $"The name '{fileName}' contains the invalid character: '{invalidChar}'. This is not allowed since the name is used when creating the Scene asset file.";
                throw new ArgumentException(errorMessage);
            }
        }

        static void ThrowIfFileExists(string destinationPath)
        {
            if (File.Exists(destinationPath))
            {
                var fileName = Path.GetFileNameWithoutExtension(destinationPath);
                var errorMessage = $"A Scene already exists at '{destinationPath}'.\n\nRename '{fileName}' to prevent overwriting the existing Scene file.";
                throw new ArgumentException(errorMessage);
            }

            if (SceneManager.GetSceneByPath(destinationPath).IsValid())
            {
                var fileName = Path.GetFileNameWithoutExtension(destinationPath);
                var errorMessage = $"A Scene with path '{destinationPath}' already exists in the SceneManager.\n\nRename '{fileName}' to prevent overwriting the existing Scene.";
                throw new ArgumentException(errorMessage);
            }
        }

        static void ThrowIfAnyGameObjectsCannotBeMovedToSubScene(GameObject[] gameObjectsMovedToSubScene)
        {
            if (gameObjectsMovedToSubScene == null)
                throw new ArgumentNullException("gameObjectsMovedToSubScene");

            if (gameObjectsMovedToSubScene.Any(x => PrefabUtility.IsPartOfAnyPrefab(x) && !PrefabUtility.IsOutermostPrefabInstanceRoot(x)))
            {
                throw new ArgumentException("Cannot create a Sub Scene from a part of a Prefab instance. Select the outermost Prefab root.");
            }
        }

        static string GetActualPathName(string path)
        {
            //@TODO: GetActualPathName is expected to become public in 2020.1
            var getActualPathName = typeof(FileUtil).GetMethod("GetActualPathName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            return (string)getActualPathName?.Invoke(null, new object[] { path });
        }

        static char[] GetInvalidFileNameChars()
        {
            // Unity uses its own list of invalid file name chars (to ensure valid asset file names across OS'es)
            var getInvalidFileNameCharsMethod = typeof(EditorUtility).GetMethod("GetInvalidFilenameChars", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var result = (string)getInvalidFileNameCharsMethod.Invoke(null, null);
            return result.ToCharArray();
        }

        static bool CreateSceneFile(string scenePath)
        {
            var createSceneAssetMethod = typeof(EditorSceneManager).GetMethod("CreateSceneAsset", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var addDefaultGameObjects = false;
            return (bool)createSceneAssetMethod.Invoke(null, new object[] { scenePath, addDefaultGameObjects });
        }

        static Scene GetLastRootScene()
        {
            if (StageUtility.GetCurrentStageHandle() == StageUtility.GetMainStageHandle())
            {
                for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.isLoaded && !scene.isSubScene)
                        return scene;
                }
            }

            return default;
        }

        static SubScene CreateSubSceneAndMoveObjectsInside(Scene parentScene, Transform targetTransform, GameObject[] topLevelObjects, string name, InteractionMode interactionMode)
        {
            ThrowIfInvalidSubSceneFileName(name);
            var dstPath = GetSubSceneFilePathUnderParentSceneFilePath(parentScene, name);
            return CreateSubSceneAtPathAndMoveObjectsInside(parentScene, targetTransform, topLevelObjects, dstPath, interactionMode);
        }

        static SubScene CreateSubSceneAtPathAndMoveObjectsInside(Scene parentScene, Transform targetTransform, GameObject[] topLevelObjects, string dstPath, InteractionMode interactionMode)
        {
            ThrowIfFileExists(dstPath);
            ThrowIfAnyGameObjectsCannotBeMovedToSubScene(topLevelObjects);

            // Cache needed info before moving topLevelObjects to Sub Scene
            int siblingIndex = targetTransform != null ? targetTransform.GetSiblingIndex() : -1;
            var targetParent = targetTransform != null ? targetTransform.parent : null;

            Directory.CreateDirectory(Path.GetDirectoryName(dstPath));
            bool succes = CreateSceneFile(dstPath);
            if (!succes)
                throw new InvalidOperationException("Creating Sub Scene failed.");

            var scene = EditorSceneManager.OpenScene(dstPath, OpenSceneMode.Additive);
            SubSceneInspectorUtility.SetSceneAsSubScene(scene);

            try
            {
                var undoName = "Create Sub Scene";

                // Move GameObjects to SubScene (if any)
                switch (interactionMode)
                {
                    case InteractionMode.AutomatedAction:
                        foreach (var go in topLevelObjects)
                        {
                            go.transform.SetParent(null, true);
                            SceneManager.MoveGameObjectToScene(go, scene);
                        }
                        break;
                    case InteractionMode.UserAction:
                        foreach (var go in topLevelObjects)
                        {
                            Undo.SetTransformParent(go.transform, null, undoName);
                            Undo.MoveGameObjectToScene(go, scene, undoName);
                        }
                        break;
                    default:
                        Debug.LogError("Enum not handled");
                        break;
                }
                EditorSceneManager.SaveScene(scene, dstPath);

                // Create GameObject with SubScene component
                var name = Path.GetFileNameWithoutExtension(dstPath);
                var gameObject = new GameObject(name, typeof(SubScene));
                gameObject.SetActive(false);
                var subSceneComponent = gameObject.GetComponent<SubScene>();
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(dstPath);
                subSceneComponent.SceneAsset = sceneAsset;

                if (targetParent)
                    gameObject.transform.parent = targetParent;
                else
                    SceneManager.MoveGameObjectToScene(gameObject, parentScene);

                if (siblingIndex >= 0)
                    gameObject.transform.SetSiblingIndex(siblingIndex);

                gameObject.SetActive(true);

                if (interactionMode == InteractionMode.UserAction)
                    Undo.RegisterCreatedObjectUndo(gameObject, undoName);

                Selection.activeObject = gameObject;

                EditorSceneManager.MarkSceneDirty(parentScene);
                return subSceneComponent;
            }
            catch
            {
                EditorSceneManager.CloseScene(scene, true);
                throw;
            }
        }
    }
}
