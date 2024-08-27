#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using Unity.Entities;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hash128 = UnityEngine.Hash128;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Unity.NetCode.Tests
{
    public class SubSceneHelper
    {
        static public GameObject CreatePrefabVariant(GameObject prefab, string variantName = null)
        {
            //Use default name with space
            if (string.IsNullOrEmpty(variantName))
                variantName = $"{prefab.name} Variant.prefab";
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var variantAssetPath = $"{Path.GetDirectoryName(AssetDatabase.GetAssetPath(prefab))}/{variantName}";
            var prefabVariant = PrefabUtility.SaveAsPrefabAsset(instance, variantAssetPath);
            return prefabVariant;
        }

        static private Scene CreateSubScene(string subScenePath)
        {
            var createSceneAssetMethod = typeof(EditorSceneManager).GetMethod("CreateSceneAsset", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var addDefaultGameObjects = false;
            createSceneAssetMethod?.Invoke(null, new object[] { subScenePath, addDefaultGameObjects });
            return EditorSceneManager.OpenScene(subScenePath, OpenSceneMode.Additive);
        }

        public static void WaitUntilSceneEntityPresent(Hash128 subSceneGUID, World world, int timeoutMs = 10000)
        {
            var ent = SceneSystem.LoadSceneAsync(world.Unmanaged,
                subSceneGUID,
                new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.DisableAutoLoad,
                });
            while(timeoutMs > 0)
            {
                world.Update();
                if (ent != Entity.Null && world.EntityManager.HasComponent<ResolvedSectionEntity>(ent))
                    return;
                //Just yield the current thread for a bit and respin.. at t
                Thread.Sleep(100);
                timeoutMs -= 100;
            }
            throw new InvalidOperationException("WaitUntilSceneEntityPresent timeout");
        }

        static IEnumerable<GameObject> CreatePrefabAlongAxis(float startZOffset, GameObject[] prefabs, int countPerPrefabs)
        {
            float zoffset = startZOffset;
            foreach (var prefab in prefabs)
            {
                for (int i = 0; i < countPerPrefabs; ++i)
                {
                    var obj = (GameObject) PrefabUtility.InstantiatePrefab(prefab);
                    obj.transform.SetPositionAndRotation(new Vector3(i*2.0f, 0.0f, zoffset), Random.rotation);
                    yield return obj;
                }
                zoffset += 2.0f;
            }
        }

        static IEnumerable<GameObject> CreatePrefabInGrid(int numRows, int numCols, Vector3 startOffset, GameObject prefab)
        {
            //Create a bunch of gameobject in the subscene
            float xOffset = startOffset.x;
            float zOffset = startOffset.z;
            for (int i = 0; i < numRows; ++i)
            {
                for (int j = 0; j < numCols; ++j)
                {
                    var obj = (GameObject) PrefabUtility.InstantiatePrefab(prefab);
                    obj.transform.SetPositionAndRotation(new Vector3(j + xOffset, startOffset.y, i + zOffset), Quaternion.identity);
                    yield return obj;
                }
            }
        }

        //Create a row for each prefab in the list along the X-axis by spacing each ghost 2 mt apart.
        //Each row is offset along the Z-Axis by 2.0 mt
        static public SubScene CreateSubSceneWithPrefabs(Scene parentScene, string scenePath, string subSceneName,
            GameObject[] prefabs, int countPerPrefabs, float startZOffset=0.0f)
        {
            var subScene = CreateSubScene($"{scenePath}/{subSceneName}.unity");
            subScene.isSubScene = true;
            SceneManager.SetActiveScene(parentScene);
            foreach (var obj in CreatePrefabAlongAxis(startZOffset, prefabs, countPerPrefabs))
                SceneManager.MoveGameObjectToScene(obj, subScene);

            EditorSceneManager.MarkSceneDirty(subScene);
            EditorSceneManager.SaveScene(subScene);
            var subSceneGo = new GameObject("SubScene");
            subSceneGo.SetActive(false);
            var subSceneComponent = subSceneGo.AddComponent<SubScene>();
            var subSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(subScene.path);
            subSceneComponent.AutoLoadScene = false;
            subSceneComponent.SceneAsset = subSceneAsset;
            subSceneGo.SetActive(true);
            EditorSceneManager.MarkSceneDirty(parentScene);
            EditorSceneManager.SaveScene(parentScene, parentScene.path);
            EditorSceneManager.CloseScene(subScene, false);
            AssetDatabase.Refresh();
            return subSceneComponent;
        }

        //Create a xz grid of object with 1 mt spacing starting from offset startOffset.
        static public SubScene CreateSubScene(Scene parentScene, string scenePath, string subSceneName, int numRows, int numCols, GameObject prefab,
            Vector3 startOffsets)
        {
            //Create the sub and parent scenes
            var subScene = CreateSubScene($"{scenePath}/{subSceneName}.unity");
            subScene.isSubScene = true;
            SceneManager.SetActiveScene(parentScene);
            if (prefab != null)
            {
                foreach(var obj in CreatePrefabInGrid(numRows, numCols, startOffsets, prefab))
                    SceneManager.MoveGameObjectToScene(obj, subScene);
            }
            EditorSceneManager.MarkSceneDirty(subScene);
            EditorSceneManager.SaveScene(subScene);
            var subSceneGo = new GameObject("SubScene");
            subSceneGo.SetActive(false);
            var subSceneComponent = subSceneGo.AddComponent<SubScene>();
            var subSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(subScene.path);
            subSceneComponent.AutoLoadScene = false;
            subSceneComponent.SceneAsset = subSceneAsset;
            EditorSceneManager.MarkSceneDirty(parentScene);
            EditorSceneManager.SaveScene(parentScene, parentScene.path);
            EditorSceneManager.CloseScene(subScene, false);
            AssetDatabase.Refresh();
            subSceneGo.SetActive(true);
            return subSceneComponent;
        }

        static public void AddSubSceneToParentScene(Scene parentScene, Scene subScene)
        {
        }

        static public Scene CreateEmptyScene(string scenePath, string name)
        {
            //Create the parent scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = name;
            EditorSceneManager.SaveScene(scene, $"{scenePath}/{name}.unity");
            if (!Directory.Exists(Path.Combine(scenePath, name)))
                Directory.CreateDirectory(Path.Combine(scenePath, name));
            return scene;
        }

        static public GameObject CreateSimplePrefab(string path, string name, params System.Type[] componentTypes)
        {
            //Create a prefab
            GameObject go = new GameObject(name, componentTypes);
            return CreatePrefab(path, go);
        }

        static public GameObject CreatePrefab(string path, GameObject go)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{path}/{go.name}.prefab");
            Object.DestroyImmediate(go);

            return prefab;
        }

        //Load into the terget world a list of subscene.
        //if the subScenes list is empty, a list of all the SubScene gameobjects in the active scene is retrieved
        //and loaded in the target world instead.
        public static void LoadSubScene(World world, params SubScene[] subScenes)
        {
            if (subScenes.Length == 0)
            {
                subScenes = Object.FindObjectsByType<SubScene>(FindObjectsSortMode.None);
            }

            var sceneEntities = new Entity[subScenes.Length];
            foreach (var subScene in subScenes)
            {
                WaitUntilSceneEntityPresent(subScene.SceneGUID, world);
            }
            for (int i = 0; i < subScenes.Length; ++i)
            {
                sceneEntities[i] = SceneSystem.LoadSceneAsync(world.Unmanaged, subScenes[i].SceneGUID, new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                });
            }
            var loaded = false;
            for (int i = 0; i < 128 && !loaded; ++i)
            {
                Thread.Sleep(100);
                world.Update();
                loaded = sceneEntities.All(s => SceneSystem.IsSceneLoaded(world.Unmanaged, s));
            }

            if (!loaded)
            {
                throw new Exception($"Failed to load SubScenes in world. {world.Name}");
            }
        }

        static public Entity LoadSubSceneAsync(World world, in NetCodeTestWorld testWorld, Hash128 subSceneGUID, float frameTime, int maxTicks=256)
        {
            WaitUntilSceneEntityPresent(subSceneGUID, world);
            var subSceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, subSceneGUID, new SceneSystem.LoadParameters
            {
                Flags = 0
            });
            for (int i = 0; i < maxTicks; ++i)
            {
                testWorld.Tick(frameTime);
                if(SceneSystem.IsSceneLoaded(world.Unmanaged, subSceneEntity))
                    return subSceneEntity;
            }
            throw new System.Exception($"Failed to load subscene in world. {world.Name}");
        }

        // Load the specified SubScene in the both clients and server world.
        // if the subScenes list is empty, a list of all the SubScene GameObjects in the active scene is retrieved
        // and loaded in the worlds.
        public static void LoadSubSceneInWorlds(in NetCodeTestWorld testWorld, params SubScene[] subScenes)
        {
            LoadSubScene(testWorld.ServerWorld, subScenes);
            foreach (var clientWorld in testWorld.ClientWorlds)
            {
                LoadSubScene(clientWorld, subScenes);
            }
        }

        //Load the scene entity and resolve the sections but not load the content.
        static public void LoadSceneSceneProxies(Hash128 subSceneGUID, NetCodeTestWorld testWorld, float frameTime, int maxTicks)
        {
            WaitUntilSceneEntityPresent(subSceneGUID, testWorld.ServerWorld);
            foreach (var client in testWorld.ClientWorlds)
                WaitUntilSceneEntityPresent(subSceneGUID, client);
        }
    }
}
#endif
