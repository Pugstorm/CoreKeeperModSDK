using System.IO;
using NUnit.Framework;
using Unity.Scenes;

public class EntityScenePathsTests
{
    [Test]
    public void TestGetSectionIndexFromPath()
    {
        Assert.AreEqual(0, EntityScenesPaths.GetSectionIndexFromPath(""));
        Assert.AreEqual(5, EntityScenesPaths.GetSectionIndexFromPath("somelongpath.5.entities"));
        Assert.AreEqual(100, EntityScenesPaths.GetSectionIndexFromPath("somelongpath.100.entities"));
        Assert.AreEqual(99, EntityScenesPaths.GetSectionIndexFromPath("somelongpathwith.dots.in.the.name.98.99.entities"));
        Assert.AreEqual(0, EntityScenesPaths.GetSectionIndexFromPath("pathwith.dots.but.no.number.entities"));
    }

    [Test]
    public void GetFileNameWithoutExtension()
    {
        var result = ResourceCatalogData.GetFileNameWithoutExtension("SomePath/HelloWorld.unity");
        Assert.AreEqual("SomePath/HelloWorld", result);
    }
}
