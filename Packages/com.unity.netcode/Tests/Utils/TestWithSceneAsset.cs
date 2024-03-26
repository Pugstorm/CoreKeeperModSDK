#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using Unity.Entities.Build;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.NetCode.Tests
{
    public abstract class TestWithSceneAsset
    {
        public string ScenePath;
        public DateTime LastWriteTime;

        [SetUp]
        public void SetupScene()
        {
            ScenePath = Path.Combine("Assets", Path.GetRandomFileName());
            Directory.CreateDirectory(ScenePath);
            LastWriteTime = Directory.GetLastWriteTime(Application.dataPath + ScenePath);
        }

        [TearDown]
        public void DestroyScenes()
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                UnityEngine.Object.DestroyImmediate(go);

            FileUtil.DeleteFileOrDirectory(ScenePath);
            FileUtil.DeleteFileOrDirectory(ScenePath + ".meta");
            AssetDatabase.Refresh();
            string depCache = Application.dataPath + "/SceneDependencyCache";
            if (Directory.Exists(depCache))
            {
                var currentCache = Directory.GetFiles(depCache);
                foreach (var file in currentCache)
                {
                    if(File.GetCreationTime(file) > LastWriteTime)
                        File.Delete(file);
                }
            }
        }
    }
}
#endif
