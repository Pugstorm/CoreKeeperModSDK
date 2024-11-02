#if !UNITY_DISABLE_MANAGED_COMPONENTS
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using Unity.Loading;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Scenes.Editor;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Unity.Entities.Tests.Content
{
    public class RuntimeContentManagerPerformanceTests
    {
        class TestLoader : RuntimeContentManager.IAlternativeLoader
        {
            public bool IsCreated => true;
            UnityEngine.GameObject obj;
            public int LoadedCount = 0;
            public TestLoader()
            {
                obj = new UnityEngine.GameObject("TestObject");
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }

            public object GetObject(UntypedWeakReferenceId objectReferenceId)
            {
                return obj;
            }

            public bool LoadObject(UntypedWeakReferenceId referenceId)
            {
                LoadedCount++;
                return true;
            }

            public void Unload(UntypedWeakReferenceId referenceId)
            {
                LoadedCount--;
            }

            public bool WaitForCompletion(UntypedWeakReferenceId referenceId)
            {
                return true;
            }

            public ObjectLoadingStatus GetObjectLoadStatus(UntypedWeakReferenceId referenceId)
            {
                return ObjectLoadingStatus.Completed;
            }

            public Scene LoadScene(UntypedWeakReferenceId sceneReferenceId, ContentSceneParameters loadParams)
            {
                throw new System.NotImplementedException();
            }

            public void UnloadScene(ref Scene scene)
            {
                throw new System.NotImplementedException();
            }

            public bool WaitForCompletion(UntypedWeakReferenceId referenceId, int timeoutMs)
            {
                throw new System.NotImplementedException();
            }

            public bool LoadInstance(RuntimeContentManager.InstanceHandle handle)
            {
                throw new System.NotImplementedException();
            }

            public bool WaitForCompletion(RuntimeContentManager.InstanceHandle handle, int timeoutMs)
            {
                throw new System.NotImplementedException();
            }

            public ObjectLoadingStatus GetInstanceLoadStatus(RuntimeContentManager.InstanceHandle handle)
            {
                throw new System.NotImplementedException();
            }

            public UnityEngine.Object GetInstance(RuntimeContentManager.InstanceHandle handle)
            {
                throw new System.NotImplementedException();
            }

            public void ReleaseInstance(RuntimeContentManager.InstanceHandle handle)
            {
                throw new System.NotImplementedException();
            }
        }

        [UnityTest, Performance]
        public IEnumerator LoadObjectAsyncTest([Values(10000, 50000)] int loadCount, [Values(1000, 10000)] int objCount)
        {
            TestLoader loader = default;
            UntypedWeakReferenceId[] ids = default;

            Measure.Method(() =>
            {
                LoadObjects(loadCount, objCount, ids);
            })
            .SetUp(() =>
            {
                SetupTestData(objCount, out loader, out ids);
            })
            .WarmupCount(1)
            .MeasurementCount(3)
            .CleanUp(() =>
            {
                WaitForLoadsToComplete(loadCount, objCount, ids);
                GetLoadedValues(loadCount, objCount, ids);
                ReleaseLoadedObjects(loadCount, objCount, ids);
                CleanupTestData(loader);
            })
            .Run();
            yield return null;
        }

        [UnityTest, Performance]
        public IEnumerator TestIsLoaded([Values(10000, 50000)] int loadCount, [Values(1000, 10000)] int objCount)
        {
            TestLoader loader = default;
            UntypedWeakReferenceId[] ids = default;

            Measure.Method(() =>
            {
                WaitForLoadsToComplete(loadCount, objCount, ids);
            })
            .SetUp(()=>
            {
                SetupTestData(objCount, out loader, out ids);
                LoadObjects(loadCount, objCount, ids);
            })
            .WarmupCount(1)
            .MeasurementCount(3)
            .CleanUp(()=>
            {
                GetLoadedValues(loadCount, objCount, ids);
                ReleaseLoadedObjects(loadCount, objCount, ids);
                CleanupTestData(loader);
            })
            .Run();
            yield return null;
        }

        [UnityTest, Performance]
        public IEnumerator TestGetValue([Values(10000, 50000)] int loadCount, [Values(1000, 10000)] int objCount)
        {
            TestLoader loader = default;
            UntypedWeakReferenceId[] ids = default;

            Measure.Method(() =>
            {
                GetLoadedValues(loadCount, objCount, ids);
            })
            .SetUp(() =>
            {
                SetupTestData(objCount, out loader, out ids);
                LoadObjects(loadCount, objCount, ids);
                WaitForLoadsToComplete(loadCount, objCount, ids);
            })
            .WarmupCount(1)
            .MeasurementCount(3)
            .CleanUp(() =>
            {
                ReleaseLoadedObjects(loadCount, objCount, ids);
                CleanupTestData(loader);
            })
            .Run();
            yield return null;
        }

        [UnityTest, Performance]
        public IEnumerator TestRelease([Values(10000, 50000)] int loadCount, [Values(1000, 10000)] int objCount)
        {
            TestLoader loader = default;
            UntypedWeakReferenceId[] ids = default;

            Measure.Method(() =>
            {
                ReleaseLoadedObjects(loadCount, objCount, ids);
            })
            .SetUp(() =>
            {
                SetupTestData(objCount, out loader, out ids);
                LoadObjects(loadCount, objCount, ids);
                WaitForLoadsToComplete(loadCount, objCount, ids);
                GetLoadedValues(loadCount, objCount, ids);
            })
            .WarmupCount(1)
            .MeasurementCount(3)
            .CleanUp(() =>
            {
                CleanupTestData(loader);
            })
            .Run();
            yield return null;
        }

        private static void LoadObjects(int loadCount, int objCount, UntypedWeakReferenceId[] ids)
        {
            for (int i = 0; i < loadCount; i++)
                RuntimeContentManager.LoadObjectAsync(ids[i % objCount]);
        }

        private static void ReleaseLoadedObjects(int loadCount, int objCount, UntypedWeakReferenceId[] ids)
        {
            for (int i = 0; i < loadCount; i++)
                RuntimeContentManager.ReleaseObjectAsync(ids[i % objCount]);
        }

        private static void GetLoadedValues(int loadCount, int objCount, UntypedWeakReferenceId[] ids)
        {
            for (int i = 0; i < loadCount; i++)
                RuntimeContentManager.GetObjectValue<UnityEngine.GameObject>(ids[i % objCount]);
        }

        private static void SetupTestData(int objCount, out TestLoader loader, out UntypedWeakReferenceId[] ids)
        {
            RuntimeContentManager.Cleanup(out var _);
            var rand = new Random(245452451);
            RuntimeContentManager.Initialize();
            loader = new TestLoader();
            RuntimeContentManager.OverrideLoader = loader;
            ids = new UntypedWeakReferenceId[objCount];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = new UntypedWeakReferenceId { GlobalId = new RuntimeGlobalObjectId { AssetGUID = new Hash128(rand.NextUInt4()) }, GenerationType = WeakReferenceGenerationType.UnityObject };
        }

        private static void WaitForLoadsToComplete(int loadCount, int objCount, UntypedWeakReferenceId[] ids)
        {
            bool allLoaded = true;
            do
            {
                allLoaded = true;
                RuntimeContentManager.ProcessQueuedCommands();
                for (int i = 0; i < loadCount; i++)
                    if (RuntimeContentManager.GetObjectLoadingStatus(ids[i % objCount]) < ObjectLoadingStatus.Completed)
                        allLoaded = false;
            } while (!allLoaded);
        }

        private static void CleanupTestData(TestLoader loader)
        {
            RuntimeContentManager.ProcessQueuedCommands();
            Assert.AreEqual(0, loader.LoadedCount);
            RuntimeContentManager.OverrideLoader.Dispose();
            RuntimeContentManager.Cleanup(out var unrleasedCount);
            Assert.AreEqual(0, unrleasedCount);
            // We must initialise to return the static state for other tests to normal operation
            RuntimeContentManager.Initialize();
            // We must also assign the override loader back to default
            RuntimeContentManager.OverrideLoader = new EditorPlayModeLoader();
        }
    }
}
#endif
