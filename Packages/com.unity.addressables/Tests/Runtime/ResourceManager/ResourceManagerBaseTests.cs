using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnityEngine.ResourceManagement.Tests
{
    public abstract class ResourceManagerBaseTests
    {
        const int kAssetCount = 25;

        protected string RootFolder
        {
            get { return string.Format("Assets/{0}_AssetsToDelete", GetType().Name); }
        }

        List<IResourceLocation> m_Locations = new List<IResourceLocation>();

        protected virtual string AssetPathPrefix
        {
            get { return ""; }
        }

        protected abstract IResourceLocation[] SetupLocations(KeyValuePair<string, string>[] assets);

        string GetAssetPath(int i)
        {
            return RootFolder + "/" + AssetPathPrefix + "asset" + i + ".prefab";
        }

        protected ResourceManager m_ResourceManager;


        [OneTimeTearDown]
        public void Cleanup()
        {
            m_ResourceManager.Dispose();
#if UNITY_EDITOR
            AssetDatabase.DeleteAsset(RootFolder);
#endif
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            m_ResourceManager = new ResourceManager();
#if UNITY_EDITOR
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < kAssetCount; i++)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "asset" + i;
                var assetPath = GetAssetPath(i);
                if (!Directory.Exists(Path.GetDirectoryName(assetPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(assetPath));

                PrefabUtility.SaveAsPrefabAsset(go, assetPath);

                Object.DestroyImmediate(go, false);
            }

            AssetDatabase.StopAssetEditing();
#endif

            var assetList = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < kAssetCount; i++)
                assetList.Add(new KeyValuePair<string, string>("asset" + i, GetAssetPath(i)));

            m_Locations.AddRange(SetupLocations(assetList.ToArray()));
        }

        [SetUp]
        public void SetUp()
        {
            Assert.Zero(m_ResourceManager.OperationCacheCount);
        }

        [TearDown]
        public void TearDown()
        {
            Assert.Zero(m_ResourceManager.OperationCacheCount);
        }

        [UnityTest]
        public IEnumerator CanProvideWithCallback()
        {
            GameObject[] goResult = new GameObject[] {null};
            bool[] wasCallbackCalled = new bool[] {false};
            var op = m_ResourceManager.ProvideResource<GameObject>(m_Locations[0]);
            op.Completed += (x) =>
            {
                wasCallbackCalled[0] = true;
                goResult[0] = x.Result;
            };
            yield return op;
            Assert.IsTrue(wasCallbackCalled[0]);
            op.Release();
        }

        private void Op_Completed(AsyncOperationHandle<GameObject> obj)
        {
            throw new NotImplementedException();
        }

        [UnityTest]
        public IEnumerator CanProvideWithYield()
        {
            var op = m_ResourceManager.ProvideResource<GameObject>(m_Locations[0]);
            yield return op;
            Assert.IsNotNull(op.Result);
            op.Release();
        }

        [UnityTest]
        public IEnumerator CanGetOperationFromCache()
        {
            AsyncOperationHandle<GameObject> op = m_ResourceManager.ProvideResource<GameObject>(m_Locations[0]);
            try
            {
                var cachedOp = m_ResourceManager.GetOperationFromCache(m_Locations[0], typeof(GameObject));
                Assert.IsNotNull(cachedOp);
                yield return op;
                Assert.IsNotNull(op.Result);
                Assert.AreEqual(op.m_InternalOp, cachedOp);
            }
            finally
            {
                op.Release();
            }
        }

        [UnityTest]
        public IEnumerator CanProvideMultipleResources()
        {
            AsyncOperationHandle<IList<GameObject>> op = m_ResourceManager.ProvideResources<GameObject>(m_Locations);
            yield return op;
            Assert.AreEqual(op.Result.Count, m_Locations.Count);
            op.Release();
        }

        class AsyncComponent : MonoBehaviour
        {
            public ResourceManager resourceManager;
            public IResourceLocation location;
            public GameObject result;
            public bool done = false;
            public AsyncOperationHandle<GameObject> operation;

            async void Start()
            {
                operation = resourceManager.ProvideResource<GameObject>(location);
                await operation.Task;
                result = operation.Result;
                done = true;
            }
        }


        [UnityTest]
        public IEnumerator AsyncOperationHandle_Await_BlocksUntilCompletion()
        {
            var go = new GameObject("test", typeof(AsyncComponent));
            var comp = go.GetComponent<AsyncComponent>();
            comp.resourceManager = m_ResourceManager;
            comp.location = m_Locations[0];
            while (!comp.done)
                yield return null;
            Assert.IsNotNull(comp.result);
            comp.operation.Release();
            GameObject.Destroy(go);
        }

        class AsyncAwaitMultipleComponent : MonoBehaviour
        {
            public ResourceManager resourceManager;
            public IResourceLocation location;
            public GameObject result;
            public bool done = false;
            public AsyncOperationHandle<GameObject> operation;
            public bool addCompletedCallback;
            public bool callbackDone = false;

            async void Start()
            {
                operation = resourceManager.ProvideResource<GameObject>(location);
                if (addCompletedCallback)
                    operation.Completed += handle => { callbackDone = true; };

                await operation.Task;
                result = operation.Result;
                done = true;
            }
        }

        [UnityTest]
        public IEnumerator WhenAsyncOperationIsDone_TaskIsCompleted()
        {
            // Setup
            var go = new GameObject("test", typeof(AsyncAwaitMultipleComponent));
            var comp = go.GetComponent<AsyncAwaitMultipleComponent>();
            comp.resourceManager = m_ResourceManager;
            comp.location = m_Locations[0];

            // Test
            while (!comp.done)
                yield return null;
            Assert.IsNotNull(comp.result);
            Assert.True(comp.operation.PercentComplete == 1 && comp.operation.IsDone);
            Assert.True(comp.operation.Task.IsCompleted);

            // Cleanup
            comp.operation.Release();
            GameObject.Destroy(go);
        }

        [UnityTest]
        public IEnumerator WhenAsyncOperationIsDone_TasksAndCallbackIsCompleted()
        {
            // Setup
            var go = new GameObject("test", typeof(AsyncAwaitMultipleComponent));
            var comp = go.GetComponent<AsyncAwaitMultipleComponent>();
            comp.resourceManager = m_ResourceManager;
            comp.location = m_Locations[0];
            comp.addCompletedCallback = true;

            // Test
            while (!comp.done)
                yield return null;
            Assert.IsNotNull(comp.result);
            Assert.True(comp.operation.PercentComplete == 1 && comp.operation.IsDone);
            Assert.True(comp.operation.Task.IsCompleted, "Task has not completed before component was done");
            Assert.True(comp.callbackDone, "Callback had not completed before component was done");

            // Cleanup
            comp.operation.Release();
            GameObject.Destroy(go);
        }
    }
}
