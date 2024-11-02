using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Tests;
using Unity.Jobs;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.Entities.Hybrid.Tests.Baking
{
    struct UnityObjectRefTestJob : IJob
    {
        [ReadOnly]
        public UnityObjectRefTestComponent unityObjRefComponent;
        [ReadOnly]
        public UnityObjectRefTestComponent unityObjRefComponent2;

        // The code actually running on the job
        public void Execute()
        {
            Assert.AreEqual(unityObjRefComponent, unityObjRefComponent2);
            Assert.AreEqual(unityObjRefComponent.Texture, unityObjRefComponent2.Texture);
        }
    }

    struct UnityObjectRefTestComponent : IComponentData
    {
        public UnityObjectRef<Texture2D> Texture;
    }

    public class UnityObjectRefTests : ECSTestsCommonBase
    {
        [SetUp]
        public override void Setup()
        {
            base.Setup();
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        [Test]
        public void UnityObjectRef_Access()
        {
            Texture2D texture = Texture2D.whiteTexture;
            UnityObjectRefTestComponent unityObjRefComponent = new UnityObjectRefTestComponent() {Texture = Texture2D.whiteTexture};
            UnityObjectRefTestComponent unityObjRefComponent2 = new UnityObjectRefTestComponent() {Texture = Texture2D.whiteTexture};

            Assert.AreEqual(unityObjRefComponent, unityObjRefComponent2);
            Assert.AreNotEqual(unityObjRefComponent.Texture, texture);
            Assert.AreEqual(unityObjRefComponent.Texture, unityObjRefComponent2.Texture);
            Assert.AreEqual(unityObjRefComponent.Texture.Value, texture);

            var job = new UnityObjectRefTestJob()
            {
                unityObjRefComponent = unityObjRefComponent,
                unityObjRefComponent2 = unityObjRefComponent2
            };

            job.Run();

            var jobHandle = job.Schedule();
            jobHandle.Complete();
        }

        [Test]
        public void UnityObjectRef_WorksWithNativeHashSet()
        {
            UnityObjectRefTestComponent unityObjRefComponent = new UnityObjectRefTestComponent() {Texture = Texture2D.whiteTexture};
            UnityObjectRefTestComponent unityObjRefComponent2 = new UnityObjectRefTestComponent() {Texture = Texture2D.whiteTexture};

            var set = new NativeHashSet<UnityObjectRef<Texture2D>>(2, Allocator.Temp);
            set.Add(unityObjRefComponent.Texture);
            set.Add(unityObjRefComponent2.Texture);

            Assert.AreEqual(1, set.Count);
        }

#if UNITY_EDITOR

        static T LoadAsset<T>(string name) where T : UnityObject
        {
            var path = $"Packages/com.unity.entities/Unity.Entities.Hybrid.Tests/Prefabs/{name}";
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new Exception($"Failed to load asset {typeof(T).Name} at '{path}'");

            return asset;
        }

        [Test(Description = "Validate that the UnityObjectRef is still valid after the referenced asset has been reloaded during its lifetime")]
        public void UnityObjectRef_UnloadedAsset_Works()
        {
            var prefab = LoadAsset<GameObject>("Prefab.prefab");
            var myObject = ScriptableObject.CreateInstance<TestScriptableObject>();
            myObject.Prefab = prefab;
            var assetPath = "Assets/myObject.asset";
            UnityEditor.AssetDatabase.CreateAsset(myObject, assetPath);
            UnityEditor.AssetDatabase.Refresh();

            Assert.True(UnityEditor.AssetDatabase.IsMainAssetAtPathLoaded(assetPath), "The asset does not appear to be loaded");

            try
            {
                UnityObjectRef<TestScriptableObject> objectRef = myObject;

                Resources.UnloadAsset(myObject);
                myObject = null;

                Assert.False(UnityEditor.AssetDatabase.IsMainAssetAtPathLoaded(assetPath));

                Assert.NotNull(objectRef.Value, "The UnityObjectRef is null");
                Assert.NotNull(objectRef.Value.Prefab, "The referenced prefab is null");

                Assert.True(UnityEditor.AssetDatabase.IsMainAssetAtPathLoaded(assetPath));
            }
            finally
            {
                UnityEditor.AssetDatabase.DeleteAsset(assetPath);
            }
        }
#endif
    }
}
