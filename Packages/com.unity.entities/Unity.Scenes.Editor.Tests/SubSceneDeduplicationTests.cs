using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Entities;
using Unity.Entities.Hybrid.Tests;
using Unity.Entities.Tests;
using Unity.Scenes.Editor;
using Unity.Scenes.Editor.Tests;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEngine;
using UnityEngine.TestTools;
using Hash128 = Unity.Entities.Hash128;

public class SubSceneDeduplicationTests
{
    static object[] DedupeSubScenesTestData =
    {
        new object[]
        {
            "Simple",
            new(string, int[])[]
            {
                ("t1.png", new int[] {}),     //texture with no refs
                ("a1.txt", new int[] { 0 }),     //asset with ref to t1.png
            },
            new(int, (int, int[]))[]
            {
                (0, (0, new int[] { 1 })),     //section 0, in subscene 0, with ref to a1.asset
                (0, (1, new int[] { 1 })),     //section 1, in subscene 0, with ref to a1.asset
            },
            (1, new int[] {1, 1})           //expecting 1 dedupe bundle, each section has 1 dependency
        },
        new object[]
        {
            "Slightly Complex",
            new(string, int[])[]
            {
                ("t1.png", new int[] {}),     //texture with no refs
                ("t2.png", new int[] {}),     //texture with no refs
                ("a1.txt", new int[] { 0, 1 }),     //asset with ref to t1.png & t2.png
                ("a2.txt", new int[] { 0 }),     //asset with ref to t1.png
            },
            new(int, (int, int[]))[]
            {
                (0, (0, new int[] { 2 })),     //section 0, in subscene 0, with ref to a1.asset
                (0, (1, new int[] { 3 })),     //section 1, in subscene 0, with ref to a2.asset
            },
            (1, new int[] {1, 1})           //expecting 1 dedupe bundle, each section has 1 dependency
        },
        new object[]
        {
            "More Complex",
            new(string, int[])[]
            {
                ("t1.png", new int[] {}),     //texture with no refs
                ("t2.png", new int[] {}),     //texture with no refs
                ("t3.png", new int[] {}),     //texture with no refs
                ("a1.txt", new int[] { 0, 1 }),     //asset with ref to t1.png & t2.png
                ("a2.txt", new int[] { 0 }),     //asset with ref to t1.png
                ("a3.txt", new int[] { 1, 2 }),     //asset with ref to t2.png & t3.png
            },
            new(int, (int, int[]))[]
            {
                (0, (0, new int[] { 3 })),     //section 0, in subscene 0, with ref to a1.asset
                (0, (1, new int[] { 4 })),     //section 1, in subscene 0, with ref to a2.asset
                (1, (0, new int[] { 5 })),     //section 0, in subscene 1, with ref to a3.asset
            },
            (2, new int[] {2, 1, 1})           //expecting 2 dedupe bundles
        },
        new object[]
        {
            "Most Complex",
            new(string, int[])[]
            {
                ("t1.png", new int[] {}),     //texture with no refs
                ("t2.png", new int[] {}),     //texture with no refs
                ("t3.png", new int[] {}),     //texture with no refs
                ("t4.png", new int[] {}),     //texture with no refs
                ("a1.txt", new int[] { 0 }),     //asset with ref to t1.png
                ("a2.txt", new int[] { 1 }),     //asset with ref to t2.png
                ("a3.txt", new int[] { 2 }),     //asset with ref to t3.png
                ("a4.txt", new int[] { 3 }),     //asset with ref to t4.png
            },
            new(int, (int, int[]))[]
            {
                (0, (0, new int[] { 4, 5 })),     //section 0, in subscene 0, with ref to a1.asset & t4.png
                (0, (1, new int[] { 5 })),     //section 1, in subscene 0, with ref to a2.asset
                (1, (0, new int[] { 4, 5, 6, 7 })),     //section 0, in subscene 1, with ref to a3.asset
                (1, (1, new int[] { 6, 7 })),     //section 0, in subscene 1, with ref to t4.png
                (1, (2, new int[] { 4, 6, 7 })),     //section 0, in subscene 1, with ref to t4.png
            },
            (3, new int[] {2, 1, 3, 1, 2})           //expecting 3 dedupe bundles
        },
    };

    string tempAssetDir;
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var subFolderPath = $"TestAssets_{GUID.Generate()}";
        if (!AssetDatabase.IsValidFolder(subFolderPath))
        {
            var folderGUID = AssetDatabase.CreateFolder("Assets", subFolderPath);
            tempAssetDir = AssetDatabase.GUIDToAssetPath(folderGUID);
        }
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (AssetDatabase.IsValidFolder(tempAssetDir))
            AssetDatabase.DeleteAsset(tempAssetDir);
    }

    [TestCaseSource(nameof(DedupeSubScenesTestData))]
    public void SubSceneBundleDedupeTest(string descr, (string, int[])[] objects, (int, (int, int[]))[] sections, (int, int[]) expected)
    {
        var allObjects = objects.Select(o => (GetObjectIdentifier(tempAssetDir, o.Item1), o.Item2)).ToArray();
        var sectionToIncludedObjects = new Dictionary<SceneSection, EntitySceneBuildUtility.SectionDependencyInfo>();
        var subScenes = new List<Hash128>();
        var sectionsList = new List<SceneSection>();
        foreach (var s in sections)
        {
            if (subScenes.Count <= s.Item1)
                subScenes.Add(new Hash128(GUID.Generate().ToString()));
            var sec = new SceneSection() { SceneGUID = subScenes[s.Item1], Section = s.Item2.Item1 };
            sectionsList.Add(sec);
            sectionToIncludedObjects.Add(sec, CreateTestDependencyInfo(s.Item2.Item2.Select(i => allObjects[i].Item1), allObjects));
        }

        var dependencyMapping = new Dictionary<Hash128, Dictionary<SceneSection, List<Hash128>>>();
        var layout = new Dictionary<Hash128, List<ObjectIdentifier>>();

        EntitySceneBuildUtility.CreateAssetLayoutData(sectionToIncludedObjects, dependencyMapping, layout);

        Assert.AreEqual(expected.Item1, layout.Count, $"{descr} - Did not create the expected bundle layout");
        Assert.AreEqual(subScenes.Count, dependencyMapping.Count, $"{descr} - Did not create the expected number of mappings - this should be equal to the number of sub scenes");
        for (int i = 0; i < sectionsList.Count; i++)
        {
            var ssData = dependencyMapping[sectionsList[i].SceneGUID];
            var depsForSection = ssData[sectionsList[i]];
            Assert.AreEqual(expected.Item2[i], depsForSection.Count, $"{descr} - Sub scene defined at index {i} did not get the expected number of dependency bundles.");
        }
    }

    private EntitySceneBuildUtility.SectionDependencyInfo CreateTestDependencyInfo(IEnumerable<ObjectIdentifier> ids, (ObjectIdentifier, int[])[] allObjects)
    {
        var deps = GetObjectTestDependencies(ids, allObjects);
        var paths = deps.Select(d => AssetDatabase.GUIDToAssetPath(d.guid.ToString())).ToArray();
        var types = paths.Select(p => AssetDatabase.GetMainAssetTypeAtPath(p)).ToArray();
        return new EntitySceneBuildUtility.SectionDependencyInfo()
        {
            Dependencies = deps,
            Types = types,
            Paths = paths
        };
    }

    static ObjectIdentifier GetObjectIdentifier(string dir, string path)
    {
        path = Path.Combine(dir, path);
        if (File.Exists(path))
            File.Delete(path);

		var ext = Path.GetExtension(path);
		switch (ext)
		{
			case ".png":
				CreateTestTexture(path);
				break;
			case ".txt":
				CreateTestAsset(path);
				break;
		}

        var ids = ContentBuildInterface.GetPlayerObjectIdentifiersInAsset(new GUID(AssetDatabase.AssetPathToGUID(path)), EditorUserBuildSettings.activeBuildTarget);
        return ids.Length == 0 ? default : ids[0];
    }
    private static void CreateTestAsset(string path)
    {
        File.WriteAllText(path, "asdf");
        AssetDatabase.ImportAsset(path, ImportAssetOptions.Default);
    }

    private static void CreateTestTexture(string path)
    {
        var tex = new Texture2D(8, 8);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.Default);
    }

    static ObjectIdentifier[] GetObjectTestDependencies(IEnumerable<ObjectIdentifier> ids, (ObjectIdentifier, int[])[] ad)
    {
        var deps = new HashSet<ObjectIdentifier>();
        foreach (var id in ids)
        {
            foreach (var a in ad)
                if (a.Item1.Equals(id))
                    foreach (var d in a.Item2)
                        deps.Add(ad[d].Item1);
        }
        return deps.ToArray();
    }

    [Test]
    public void SubSceneDeduplicationValidation_WithNullInput_ReturnsFalse()
    {
        Assert.IsFalse(EntitySceneBuildUtility.ValidateInput(null, out var error));
        Assert.IsNotNull(error);
    }

    [Test]
    public void SubSceneDeduplicationValidation_WithInvalidSceneInput_ReturnsFalse()
    {
        var input = new Dictionary<SceneSection, EntitySceneBuildUtility.SectionDependencyInfo>();
        input.Add(new SceneSection() { SceneGUID = default }, new EntitySceneBuildUtility.SectionDependencyInfo());
        Assert.IsFalse(EntitySceneBuildUtility.ValidateInput(input, out var error));
        Assert.IsNotNull(error);
    }

    [Test]
    public void SubSceneDeduplicationValidation_WithInvalidSectionInput_ReturnsFalse()
    {
        var input = new Dictionary<SceneSection, EntitySceneBuildUtility.SectionDependencyInfo>();
        input.Add(new SceneSection() { SceneGUID = new Hash128(1, 2, 3, 4), Section = -1 }, new EntitySceneBuildUtility.SectionDependencyInfo());
        Assert.IsFalse(EntitySceneBuildUtility.ValidateInput(input, out var error));
        Assert.IsNotNull(error);
    }

    [Test]
    public void SubSceneDeduplicationValidation_WithNullDependenciesInput_ReturnsFalse()
    {
        var input = new Dictionary<SceneSection, EntitySceneBuildUtility.SectionDependencyInfo>();
        input.Add(new SceneSection() { SceneGUID = new Hash128(1, 2, 3, 4), Section = 0 }, new EntitySceneBuildUtility.SectionDependencyInfo());
        Assert.IsFalse(EntitySceneBuildUtility.ValidateInput(input, out var error));
        Assert.IsNotNull(error);
    }

    [Test]
    public void SubSceneDeduplicationValidation_WithNullTypesInput_ReturnsFalse()
    {
        var input = new Dictionary<SceneSection, EntitySceneBuildUtility.SectionDependencyInfo>();
        input.Add(new SceneSection() { SceneGUID = new Hash128(1, 2, 3, 4), Section = 0 }, new EntitySceneBuildUtility.SectionDependencyInfo() { Dependencies = new ObjectIdentifier[1] });
        Assert.IsFalse(EntitySceneBuildUtility.ValidateInput(input, out var error));
        Assert.IsNotNull(error);
    }

    [Test]
    public void SubSceneDeduplicationValidation_WithNullPathsInput_ReturnsFalse()
    {
        var input = new Dictionary<SceneSection, EntitySceneBuildUtility.SectionDependencyInfo>();
        input.Add(new SceneSection() { SceneGUID = new Hash128(1, 2, 3, 4), Section = 0 }, new EntitySceneBuildUtility.SectionDependencyInfo() { Dependencies = new ObjectIdentifier[1], Types = new Type[2] });
        Assert.IsFalse(EntitySceneBuildUtility.ValidateInput(input, out var error));
        Assert.IsNotNull(error);
    }

    [Test]
    public void SubSceneDeduplicationValidation_WithMismatchedDataInput_ReturnsFalse()
    {
        var input = new Dictionary<SceneSection, EntitySceneBuildUtility.SectionDependencyInfo>();
        input.Add(new SceneSection() { SceneGUID = new Hash128(1, 2, 3, 4), Section = 0 }, new EntitySceneBuildUtility.SectionDependencyInfo() { Dependencies = new ObjectIdentifier[1], Types = new Type[2], Paths = new string[3] });
        Assert.IsFalse(EntitySceneBuildUtility.ValidateInput(input, out var error));
        Assert.IsNotNull(error);
    }

    [Test]
    public void SubSceneDeduplicationValidation_WithInvalidGUIDInput_ReturnsFalse()
    {
        var input = new Dictionary<SceneSection, EntitySceneBuildUtility.SectionDependencyInfo>();
        input.Add(new SceneSection() { SceneGUID = new Hash128(1, 2, 3, 4), Section = 0 }, new EntitySceneBuildUtility.SectionDependencyInfo() { Dependencies = new ObjectIdentifier[] { new ObjectIdentifier() }, Types = new Type[] { typeof(object) }, Paths = new string[] { "notEmpty" } });
        Assert.IsFalse(EntitySceneBuildUtility.ValidateInput(input, out var error));
        Assert.IsNotNull(error);
    }

    [Test]
    public void SubSceneDeduplicationValidation_WithNullTypeInput_ReturnsFalse()
    {
        var input = new Dictionary<SceneSection, EntitySceneBuildUtility.SectionDependencyInfo>();
        input.Add(new SceneSection() { SceneGUID = new Hash128(1, 2, 3, 4), Section = 0 }, new EntitySceneBuildUtility.SectionDependencyInfo() { Dependencies = new ObjectIdentifier[] { GetObjectIdentifier(tempAssetDir, "blah.png") }, Types = new Type[] { null }, Paths = new string[] { "notEmpty" } });
        Assert.IsFalse(EntitySceneBuildUtility.ValidateInput(input, out var error));
        Assert.IsNotNull(error);
    }

    [Test]
    public void SubSceneDeduplicationValidation_WithEmptyPathInput_ReturnsFalse()
    {
        var input = new Dictionary<SceneSection, EntitySceneBuildUtility.SectionDependencyInfo>();
        input.Add(new SceneSection() { SceneGUID = new Hash128(1, 2, 3, 4), Section = 0 }, new EntitySceneBuildUtility.SectionDependencyInfo() { Dependencies = new ObjectIdentifier[] { GetObjectIdentifier(tempAssetDir, "blah.png") }, Types = new Type[] { typeof(object) }, Paths = new string[] { "" } });
        Assert.IsFalse(EntitySceneBuildUtility.ValidateInput(input, out var error));
        Assert.IsNotNull(error);
    }
}
