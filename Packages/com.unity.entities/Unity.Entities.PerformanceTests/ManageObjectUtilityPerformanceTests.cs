using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.PerformanceTesting;

namespace Unity.Entities.PerformanceTests
{
#if !UNITY_DISABLE_MANAGED_COMPONENTS
    [TestFixture]
    [Category("Performance")]
    public class ManageObjectUtilityPerformanceTests
    {
        class TestComplexObject
        {
            [Properties.CreateProperty]
            public TestScriptableObject ScriptableObject;

            [Properties.CreateProperty]
            public Dictionary<Hash128, ITestInterface> Options;
        }

        class TestScriptableObject : UnityEngine.ScriptableObject
        {
            
        }

        interface ITestInterface
        {
            
        }

        class TestImpl : ITestInterface
        {
#pragma warning disable 649
            public string A;
            public string B;
#pragma warning restore 649
        }

        [Test, Performance]
        public void GatherBlobAssetReferencesPerformanceTest()
        {
            var obj = UnityEngine.ScriptableObject.CreateInstance<TestScriptableObject>();

            try
            {
                var src = new TestComplexObject
                {
                    ScriptableObject = obj,
                    Options = new Dictionary<Hash128, ITestInterface>
                    {
                        {new Hash128(new uint4(0)), new TestImpl()},
                        {new Hash128(new uint4(1)), new TestImpl()},
                        {new Hash128(new uint4(2)), new TestImpl()},
                        {new Hash128(new uint4(3)), new TestImpl()},
                        {new Hash128(new uint4(4)), new TestImpl()},
                        {new Hash128(new uint4(5)), new TestImpl()},
                        {new Hash128(new uint4(6)), new TestImpl()},
                    }
                };

                using (var blobAssets = new NativeList<BlobAssetPtr>(1, Allocator.Temp))
                using (var blobAssetsMap = new NativeParallelHashMap<BlobAssetPtr, int>(1, Allocator.Temp))
                {
                    var managedObjectBlobs = new ManagedObjectBlobs();

                    Measure.Method(() =>
                        {
                            managedObjectBlobs.GatherBlobAssetReferences(src, blobAssets, blobAssetsMap);
                        })
                        .IterationsPerMeasurement(200)
                        .MeasurementCount(100)
                        .WarmupCount(1)
                        .Run();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }
    }
#endif
}
