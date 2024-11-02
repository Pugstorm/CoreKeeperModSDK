using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Editor.Bridge
{
    static class EditorSceneManagerBridge
    {

        public static Scene GetSceneByHandle(int handle) => EditorSceneManager.GetSceneByHandle(handle);

        public static bool IsAuthoringScene(Scene scene) => scene.isSubScene; //TODO: replace by EditorSceneManager.IsAuthoringScene(scene); when non destructive editing PR is in dots/monorepo

        public static void SaveSceneAs(Scene scene) => EditorSceneManager.SaveSceneAs(scene);

        public static void ReloadScene(Scene scene) => EditorSceneManager.ReloadScene(scene);

        public static void RemoveAllPrefabInstancesUnusedOverridesFromSceneForMenuItem(Scene scene)
        {
            if (!scene.IsValid())
                return;

            PrefabUtility.InstanceOverridesInfo[] instanceOverridesInfos = null;

            List<GameObject> gos = GetScenePrefabInstancesWithNonDefaultOverrides(scene);
            if (gos != null && gos.Any())
                instanceOverridesInfos = PrefabUtility.GetPrefabInstancesOverridesInfos(gos.ToArray());

            AskUserToRemovePrefabInstanceUnusedOverrides(instanceOverridesInfos);
        }

        static List<GameObject> GetScenePrefabInstancesWithNonDefaultOverrides(Scene scene)
        {
            List<GameObject> gameObjects = new List<GameObject>();
            TransformVisitor visitor = new TransformVisitor();

            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                visitor.VisitAll(root.transform, (transform, list) => {
                    GameObject go = transform.gameObject;
                    if (PrefabUtility.IsOutermostPrefabInstanceRoot(go) && PrefabUtility.HasPrefabInstanceNonDefaultOverridesOrUnusedOverrides_CachedForUI(go))
                    {
                        gameObjects.Add(go);
                    }
                }, null);
            }

            return gameObjects;
        }

        static bool AskUserToRemovePrefabInstanceUnusedOverrides(PrefabUtility.InstanceOverridesInfo[] instanceOverridesInfos)
        {
            if (PrefabUtility.DoRemovePrefabInstanceUnusedOverridesDialog(instanceOverridesInfos))
            {
                PrefabUtility.RemovePrefabInstanceUnusedOverrides(instanceOverridesInfos);
                return true;
            }

            return false;
        }

    }
}
