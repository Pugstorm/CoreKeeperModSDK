#if !UNITY_DISABLE_MANAGED_COMPONENTS
using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Unity.Entities.Editor;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using UnityEditor;

public class WeakReferencePropertyDrawerTests : MonoBehaviour
{
    [Test]
    public void TestWeakReferenceTestDrawerTypes()
    {
        Assert.AreEqual(typeof(Texture), WeakReferencePropertyDrawerBase.DetermineTargetType(typeof(WeakObjectReference<Texture>)));
        Assert.AreEqual(typeof(Texture), WeakReferencePropertyDrawerBase.DetermineTargetType(typeof(WeakObjectReference<Texture>[])));
        Assert.AreEqual(typeof(Texture), WeakReferencePropertyDrawerBase.DetermineTargetType(typeof(List<WeakObjectReference<Texture>>)));

        Assert.AreEqual(typeof(SceneAsset), WeakReferencePropertyDrawerBase.DetermineTargetType(typeof(WeakObjectSceneReference)));
        Assert.AreEqual(typeof(SceneAsset), WeakReferencePropertyDrawerBase.DetermineTargetType(typeof(WeakObjectSceneReference[])));
        Assert.AreEqual(typeof(SceneAsset), WeakReferencePropertyDrawerBase.DetermineTargetType(typeof(List<WeakObjectSceneReference>)));

        Assert.AreEqual(typeof(SceneAsset), WeakReferencePropertyDrawerBase.DetermineTargetType(typeof(EntitySceneReference)));
        Assert.AreEqual(typeof(SceneAsset), WeakReferencePropertyDrawerBase.DetermineTargetType(typeof(EntitySceneReference[])));
        Assert.AreEqual(typeof(SceneAsset), WeakReferencePropertyDrawerBase.DetermineTargetType(typeof(List<EntitySceneReference>)));

        Assert.AreEqual(typeof(GameObject), WeakReferencePropertyDrawerBase.DetermineTargetType(typeof(EntityPrefabReference)));
        Assert.AreEqual(typeof(GameObject), WeakReferencePropertyDrawerBase.DetermineTargetType(typeof(EntityPrefabReference[])));
        Assert.AreEqual(typeof(GameObject), WeakReferencePropertyDrawerBase.DetermineTargetType(typeof(List<EntityPrefabReference>)));
    }
}
#endif
