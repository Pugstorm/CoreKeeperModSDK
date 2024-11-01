using System.IO;
using Unity.Scenes.Editor;
using UnityEditor;
using UnityEngine;

namespace Unity.Scenes
{
#if UNITY_EDITOR
    internal static class EntitiesCacheUtility
    {
        internal const string globalEntitiesDependencyDir = "Assets/GlobalEntitiesDependencies/";
        internal const string globalEntitySceneDependencyPath = globalEntitiesDependencyDir + "GlobalEntitySceneDependency.asset";

        internal static GlobalEntitiesDependency entitySceneDependency;

        internal static void UpdateEntitySceneGlobalDependency()
        {
            if (entitySceneDependency == null)
                LoadOrCreateGlobalEntitySceneDependency();

            entitySceneDependency.cacheGUID = GUID.Generate();
            EditorUtility.SetDirty(entitySceneDependency);
            AssetDatabase.SaveAssets();
        }

        static void LoadOrCreateGlobalEntitySceneDependency()
        {
            entitySceneDependency = AssetDatabase.LoadAssetAtPath<GlobalEntitiesDependency>(globalEntitySceneDependencyPath);

            if (entitySceneDependency == null)
            {
                entitySceneDependency = ScriptableObject.CreateInstance<GlobalEntitiesDependency>();
                Directory.CreateDirectory(globalEntitiesDependencyDir);
                AssetDatabase.CreateAsset(entitySceneDependency, globalEntitySceneDependencyPath);
            }
        }
    }
    #endif
}
