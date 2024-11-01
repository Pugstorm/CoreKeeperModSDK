using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unity.Scenes.Editor.Tests
{
    [Serializable]
    public struct TestWithTempAssets
    {
        public string TempAssetDir;
        public int AssetCounter;

        public void SetUp()
        {
            var guid = AssetDatabase.CreateFolder("Assets", nameof(TestWithTempAssets));
            TempAssetDir = AssetDatabase.GUIDToAssetPath(guid);
        }

        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempAssetDir);
        }

        public string GetNextPath() => Path.Combine(TempAssetDir, (AssetCounter++).ToString());
        public string GetNextPath(string ext) => GetNextPath() + ext;
    }
}
