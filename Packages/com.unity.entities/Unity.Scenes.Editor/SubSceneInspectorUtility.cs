using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes.Editor
{
    [InitializeOnLoad]
    public static class SubSceneInspectorUtility
    {
        internal delegate void RepaintAction();

        internal static event RepaintAction WantsRepaint;

        static SubSceneInspectorUtility()
        {
            Unsupported.SetUsingAuthoringScenes(true);
        }

        public static Transform GetUncleanHierarchyObject(SubScene[] subscenes)
        {
            foreach (var scene in subscenes)
            {
                var res = GetUncleanHierarchyObject(scene.transform);
                if (res != null)
                    return res;
            }

            return null;
        }

        public static Transform GetUncleanHierarchyObject(Transform child)
        {
            while (child)
            {
                if (child.localPosition != Vector3.zero)
                    return child;
                if (child.localRotation != Quaternion.identity)
                    return child;
                if (child.localScale != Vector3.one)
                    return child;

                child = child.parent;
            }

            return null;
        }

        public static bool HasChildren(SubScene[] scenes)
        {
            foreach (var scene in scenes)
            {
                if (scene.transform.childCount != 0)
                    return true;
            }

            return false;
        }

        public static void CloseSceneWithoutSaving(params SubScene[] scenes)
        {
            foreach (var scene in scenes)
                EditorSceneManager.CloseScene(scene.EditingScene, true);
        }

        public struct LoadableScene
        {
            public Entity Scene;
            public string Name;
            public SubScene SubScene;
            public int SectionIndex;
            public bool IsLoaded;
            public bool Section0IsLoaded;
            public int NumSubSceneSectionsLoaded;
        }

        static unsafe NativeArray<Entity> GetActiveWorldSections(World world, Hash128 sceneGUID)
        {
            if (world == null || !world.IsCreated) return default;

            var sceneSystem = world.GetExistingSystem<SceneSystem>();
            var statePtr = world.Unmanaged.ResolveSystemState(sceneSystem);
            if (statePtr == null)
                return default;

            var entities = world.EntityManager;

            var sceneEntity = SceneSystem.GetSceneEntity(world.Unmanaged, sceneGUID);

            if (!entities.HasComponent<ResolvedSectionEntity>(sceneEntity))
                return default;

            return entities.GetBuffer<ResolvedSectionEntity>(sceneEntity).Reinterpret<Entity>().AsNativeArray();
        }

        // Return the loadable sections of a given subScene
        internal static LoadableScene[] GetLoadableSections(SubScene subScene, LoadableScene[] loadableScenes)
        {
            var scenes = new List<LoadableScene>();
            foreach (var scene in loadableScenes)
            {
                if (scene.SubScene.SceneGUID == subScene.SceneGUID)
                {
                    scenes.Add(scene);
                }
            }

            return scenes.ToArray();
        }

        public static SubSceneInspectorUtility.LoadableScene[] GetLoadableScenes(SubScene[] scenes)
        {
            var loadables = new List<SubSceneInspectorUtility.LoadableScene>();
            DefaultWorldInitialization.DefaultLazyEditModeInitialize(); // workaround for occasional null World at this point
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            foreach (var scene in scenes)
            {
                bool section0IsLoaded = false;
                var numSections = 0;
                var numSectionsLoaded = 0;
                foreach (var section in GetActiveWorldSections(world, scene.SceneGUID))
                {
                    if (entityManager.HasComponent<SceneSectionData>(section))
                    {
                        var name = scene.SceneAsset != null ? scene.SceneAsset.name : "Missing Scene Asset";
                        var sectionIndex = entityManager.GetComponentData<SceneSectionData>(section).SubSectionIndex;
                        if (sectionIndex != 0)
                            name += $" Section: {sectionIndex}";

                        numSections += 1;
                        var isLoaded = entityManager.HasComponent<RequestSceneLoaded>(section);
                        if (isLoaded)
                            numSectionsLoaded += 1;
                        if (sectionIndex == 0)
                            section0IsLoaded = isLoaded;

                        loadables.Add(new SubSceneInspectorUtility.LoadableScene
                        {
                            Scene = section,
                            Name = name,
                            SubScene = scene,
                            SectionIndex = sectionIndex,
                            IsLoaded = isLoaded,
                            Section0IsLoaded = section0IsLoaded,
                        });
                    }
                }

                // Go over all sections of this subscene and set the number of sections that are loaded.
                // This is needed to decide whether are able to unload section 0.
                for (int i = 0; i < numSections; i++)
                {
                    var idx = numSections - 1 - i;
                    var l = loadables[idx];
                    l.NumSubSceneSectionsLoaded = numSectionsLoaded;
                    loadables[idx] = l;
                }
            }

            return loadables.ToArray();
        }

        [InitializeOnLoadMethod]
        static void SetupForceReimportOnLightBaking()
        {
            Lightmapping.bakeCompleted += () =>
            {
                ForceReimport(UnityEngine.Object.FindObjectsByType<SubScene>(FindObjectsSortMode.None));
            };
        }

        public static unsafe void ForceReimport(params SubScene[] scenes)
        {
            bool needRefresh = false;
            foreach (var world in World.All)
            {
                var sceneSystem = world.GetExistingSystem<SceneSystem>();
                var statePtr = world.Unmanaged.ResolveSystemState(sceneSystem);
                if (statePtr != null)
                {
                    var buildConfigGuid = world.EntityManager.GetComponentData<SceneSystemData>(sceneSystem).BuildConfigurationGUID;
                    foreach (var scene in scenes)
                        needRefresh |= SceneWithBuildConfigurationGUIDs.Dirty(scene.SceneGUID, buildConfigGuid);
                }
            }
            if (needRefresh)
                AssetDatabase.Refresh();
        }

        public static bool CanEditScene(SubScene subScene)
        {
            if (!subScene.CanBeLoaded())
                return false;

            return !subScene.IsLoaded;
        }

        public static void SetSceneAsSubScene(Scene scene)
        {
            scene.isSubScene = true;
        }

        public static void CloseAndAskSaveIfUserWantsTo(params SubScene[] subScenes)
        {
            if (!Application.isPlaying)
            {
                var dirtyScenes = new List<Scene>();
                foreach (var scene in subScenes)
                {
                    if (scene.EditingScene.isLoaded && scene.EditingScene.isDirty)
                    {
                        dirtyScenes.Add(scene.EditingScene);
                    }
                }

                if (dirtyScenes.Count != 0)
                {
                    if (!EditorSceneManager.SaveModifiedScenesIfUserWantsTo(dirtyScenes.ToArray()))
                        return;
                }

                CloseSceneWithoutSaving(subScenes);
            }
            else
            {
                foreach (var scene in subScenes)
                {
                    if (scene.EditingScene.isLoaded)
                        EditorSceneManager.UnloadSceneAsync(scene.EditingScene);
                }
            }
        }

        public static void SaveScene(SubScene scene)
        {
            if (scene.EditingScene.isLoaded && scene.EditingScene.isDirty)
            {
                EditorSceneManager.SaveScene(scene.EditingScene);
            }
        }

        public static MinMaxAABB GetActiveWorldMinMax(World world, UnityEngine.Object[] targets)
        {
            MinMaxAABB bounds = MinMaxAABB.Empty;

            if (world == null)
                return bounds;

            var entities = world.EntityManager;
            foreach (SubScene subScene in targets)
            {
                foreach (var section in GetActiveWorldSections(World.DefaultGameObjectInjectionWorld, subScene.SceneGUID))
                {
                    if (entities.HasComponent<SceneBoundingVolume>(section))
                        bounds.Encapsulate(entities.GetComponentData<SceneBoundingVolume>(section).Value);
                }
            }

            return bounds;
        }

        // Visualize SubScene using bounding volume when it is selected.
        public static void DrawSubsceneBounds(SubScene scene)
        {
            var isEditing = scene.IsLoaded;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
                return;

            var entities = world.EntityManager;
            foreach (var section in GetActiveWorldSections(World.DefaultGameObjectInjectionWorld, scene.SceneGUID))
            {
                if (!entities.HasComponent<SceneBoundingVolume>(section))
                    continue;

                if (isEditing)
                    Gizmos.color = Color.green;
                else
                    Gizmos.color = Color.gray;

                AABB aabb = entities.GetComponentData<SceneBoundingVolume>(section).Value;
                Gizmos.DrawWireCube(aabb.Center, aabb.Size);
            }
        }

        /// <summary>
        /// Forces a Repaint event on the <see cref="SubSceneInspector"/> editor which are currently active.
        /// </summary>
        internal static void RepaintSubSceneInspector()
        {
            WantsRepaint?.Invoke();
        }
    }
}
