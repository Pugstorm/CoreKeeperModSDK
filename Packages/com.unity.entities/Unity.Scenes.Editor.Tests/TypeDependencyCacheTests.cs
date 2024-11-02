using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Entities.Conversion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes.Editor.Tests
{

    [Serializable]
    class TypeDependencyCacheTests
    {
        [SerializeField]
        private string m_OriginalHash;
        [SerializeField]
        private string m_BuildSettingsGuid;
        [SerializeField]
        private string m_Guid;

        private static string m_ScriptFilePath = "Packages/com.unity.entities/Unity.Scenes.Editor.Tests/TypeDependencyCacheTestAssembly/TypeDependencyCacheAuthoring.cs";

        [SerializeField]
        private string m_ScriptFileFullPath;

        [SerializeField] TestWithTempAssets m_TempAssets;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            if (m_TempAssets.TempAssetDir != null)
                yield return null;

            m_TempAssets.SetUp();

            m_ScriptFileFullPath = Path.GetFullPath(m_ScriptFilePath);

            m_BuildSettingsGuid = default(Unity.Entities.Hash128).ToString();

            var tempScene = SubSceneTestsHelper.CreateTmpScene(ref m_TempAssets);
            var subscene = SubSceneTestsHelper.CreateSubSceneInSceneFromObjects("SubScene", false, tempScene, () =>
            {
                var go = new GameObject();
                return new List<GameObject> {go};
            });
            m_Guid = subscene.SceneGUID.ToString();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            m_TempAssets.TearDown();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

            SceneWithBuildConfigurationGUIDs.ClearBuildSettingsCache();
            yield return null;
        }

        private IEnumerator UpdatingScriptFile()
        {
            using (StreamWriter sw = File.AppendText(m_ScriptFileFullPath))
            {
                sw.WriteLine("\n");
                sw.Close();
            }
            AssetDatabase.ImportAsset(m_ScriptFilePath, ImportAssetOptions.ForceSynchronousImport);
            yield return null;
        }

        private IEnumerator AddingBakingAttribute(bool excluded = false)
        {
            var bakerDeclaration = "    public class TypeDependencyCacheBaker : Baker<TypeDependencyCacheAuthoring>";
            var lineToAdd = "[BakingVersion(\"\", 1)]";
            if(excluded)
                lineToAdd = "[BakingVersion(true)]";
            var allLines = File.ReadAllLines(m_ScriptFileFullPath).ToList();
            allLines.Insert(allLines.IndexOf(bakerDeclaration), lineToAdd);
            File.WriteAllLines(m_ScriptFileFullPath, allLines);
            AssetDatabase.ImportAsset(m_ScriptFilePath, ImportAssetOptions.ForceSynchronousImport);
            yield return null;
        }

        private IEnumerator UpdateBakingAttribute()
        {
            var lineToRemove = "[BakingVersion(\"\", 1)]";
            var lineToAdd = "[BakingVersion(\"\", 2)]";
            var allLines = File.ReadAllLines(m_ScriptFileFullPath).ToList();
            allLines.Insert(allLines.IndexOf(lineToRemove), lineToAdd);
            allLines.Remove(lineToRemove);
            File.WriteAllLines(m_ScriptFileFullPath, allLines);
            AssetDatabase.ImportAsset(m_ScriptFilePath, ImportAssetOptions.ForceSynchronousImport);
            yield return null;
        }

        private void RemoveBakingAttribute()
        {
            var allLines = File.ReadAllLines(m_ScriptFileFullPath).ToList();
            for (int i = allLines.Count - 1; i >= 0 ; i--)
            {
                // remove baking attribute or extra added lines
                if(allLines[i].Contains("BakingVersion") || allLines[i] == "" || allLines[i] == "\\n")
                    allLines.RemoveAt(i);
            }
            File.WriteAllLines(m_ScriptFileFullPath, allLines);
            AssetDatabase.ImportAsset(m_ScriptFilePath, ImportAssetOptions.ForceSynchronousImport);
        }

        [UnityTest]
        [Ignore("TODO: Unstable on CI https://jira.unity3d.com/browse/DOTS-7758")]
        public IEnumerator TypeDependencyCache_UpdateBakerScript_TriggersReimport()
        {
            //reset script state
            RemoveBakingAttribute();
            yield return new WaitForDomainReload();

            m_OriginalHash = EntityScenesPaths.GetSubSceneArtifactHash(new Hash128(m_Guid), new Hash128(m_BuildSettingsGuid), true, ImportMode.Synchronous).ToString();

            // Updating an assembly containing a baker should re-trigger an import
            yield return UpdatingScriptFile();
            yield return new WaitForDomainReload();

            var newHash = EntityScenesPaths.GetSubSceneArtifactHash(new Hash128(m_Guid), new Hash128(m_BuildSettingsGuid), true, ImportMode.Synchronous).ToString();
            Assert.AreNotEqual(m_OriginalHash, newHash);
        }

        [UnityTest]
        [Ignore("TODO: Unstable on CI https://jira.unity3d.com/browse/DOTS-7758")]
        public IEnumerator TypeDependencyCache_UsingBakingAttribute_Excluded_DoesTriggerReimport()
        {
            //reset script state
            RemoveBakingAttribute();

            yield return AddingBakingAttribute(true);
            yield return new WaitForDomainReload();

            m_OriginalHash = EntityScenesPaths.GetSubSceneArtifactHash(new Hash128(m_Guid), new Hash128(m_BuildSettingsGuid), true, ImportMode.Synchronous).ToString();

            // Updating an assembly that has a baker with a baking version attribute shouldn't trigger an import
            yield return UpdatingScriptFile();
            yield return new WaitForDomainReload();

            var newHash = EntityScenesPaths.GetSubSceneArtifactHash(new Hash128(m_Guid), new Hash128(m_BuildSettingsGuid), true, ImportMode.Synchronous).ToString();

            Assert.AreNotEqual(m_OriginalHash, newHash);
        }

        [UnityTest]
        [Ignore("TODO: Unstable on CI https://jira.unity3d.com/browse/DOTS-7758")]
        public IEnumerator TypeDependencyCache_UsingBakingAttribute_DoesntTriggerReimport()
        {
            //reset script state
            RemoveBakingAttribute();

            yield return AddingBakingAttribute();
            yield return new WaitForDomainReload();

            m_OriginalHash = EntityScenesPaths.GetSubSceneArtifactHash(new Hash128(m_Guid), new Hash128(m_BuildSettingsGuid), true, ImportMode.Synchronous).ToString();

            // Updating an assembly that has a baker with a baking version attribute shouldn't trigger an import
            yield return UpdatingScriptFile();
            yield return new WaitForDomainReload();

            var newHash = EntityScenesPaths.GetSubSceneArtifactHash(new Hash128(m_Guid), new Hash128(m_BuildSettingsGuid), true, ImportMode.Synchronous).ToString();

            Assert.AreEqual(m_OriginalHash, newHash);
        }

        [UnityTest]
        [Ignore("TODO: Unstable on CI https://jira.unity3d.com/browse/DOTS-7758")]
        public IEnumerator TypeDependencyCache_UpdatingBakingVersionAttribute_DoesReimport()
        {
            //reset script state
            RemoveBakingAttribute();

            // Using a baking version attribute on a baker shouldn't re-trigger an import
            yield return AddingBakingAttribute();
            yield return new WaitForDomainReload();

            m_OriginalHash = EntityScenesPaths.GetSubSceneArtifactHash(new Hash128(m_Guid), new Hash128(m_BuildSettingsGuid), true, ImportMode.Synchronous).ToString();

            // Bumping a baking version attribute on a baker should re-trigger an import
            yield return UpdateBakingAttribute();
            yield return new WaitForDomainReload();

            var newHash = EntityScenesPaths.GetSubSceneArtifactHash(new Hash128(m_Guid), new Hash128(m_BuildSettingsGuid), true, ImportMode.Synchronous).ToString();

            Assert.AreNotEqual(m_OriginalHash, newHash);
        }
    }
}
