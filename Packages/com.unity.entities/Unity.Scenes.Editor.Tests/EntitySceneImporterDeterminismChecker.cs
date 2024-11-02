using System.IO;
using NUnit.Framework;
using Unity.Scenes.Editor;
using UnityEditor;
using UnityEditor.Experimental;

namespace Unity.Scenes.Tests
{
    public class EntitySceneImporterDeterminismChecker
    {
        public static void Check(string path)
        {
            var guid = AssetDatabaseCompatibility.PathToGUID(path);
            Assert.IsFalse(guid.Empty());

            var configGUID = SceneWithBuildConfigurationGUIDs.EnsureExistsFor(guid, default, true, out var requireRefresh);
            requireRefresh |= SceneWithBuildConfigurationGUIDs.Dirty(guid, default);

            if (requireRefresh)
                AssetDatabase.Refresh();

            var artifactKey = new ArtifactKey(configGUID, typeof(SubSceneImporter));
            var artifact = AssetDatabaseCompatibility.ProduceArtifact(artifactKey);
            AssetDatabaseCompatibility.GetArtifactPaths(artifact, out var firstPaths);

            for (int i = 0; i != 1; i++)
            {
                if (SceneWithBuildConfigurationGUIDs.Dirty(guid, default))
                    AssetDatabase.Refresh();

                var newArtifact = AssetDatabaseCompatibility.ProduceArtifact(artifactKey);
                AssetDatabaseCompatibility.GetArtifactPaths(newArtifact, out var paths);

                Assert.AreEqual(firstPaths.Length, paths.Length);
                for (int j = 0; j != firstPaths.Length; j++)
                {
                    Assert.AreEqual(File.ReadAllBytes(firstPaths[j]), File.ReadAllBytes(paths[j]), $"Comparing '{Path.GetFileName(firstPaths[j])}' and '{Path.GetFileName(paths[j])}'");
                }
            }
        }
    }
}
