#if !UNITY_DISABLE_MANAGED_COMPONENTS
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Conversion;
using Unity.Entities.Hybrid.Tests;
using Unity.Entities.Tests;
using Unity.Entities.Tests.Conversion;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Scenes.Editor.Tests;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Entities.Hybrid.PerformanceTests
{
    class ConversionTestCompanionComponentBaker : Baker<ConversionTestCompanionComponent>
    {
        public override void Bake(ConversionTestCompanionComponent authoring)
        {
            // This test might require transform components
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject(entity, authoring);
        }
    }

    [Serializable]
    [TestFixture]
    public class CompanionComponentPerformanceTests_Baking
    {
        [SerializeField] TestWithCustomDefaultGameObjectInjectionWorld m_DefaultWorld;
        [SerializeField] TestWithObjects m_Objects;
        [SerializeField] TestWithTempAssets m_Assets;

        [SetUp]
        public void SetUp()
        {
            m_DefaultWorld.Setup(true);
            m_Objects.SetUp();
            m_Assets.SetUp();
        }

        [TearDown]
        public void TearDown()
        {
            m_DefaultWorld.TearDown();
            m_Objects.TearDown();
            m_Assets.TearDown();
        }

        internal BakingSettings MakeDefaultSettings() => new BakingSettings
        {
            BakingFlags = BakingUtility.BakingFlags.AssignName |
                              BakingUtility.BakingFlags.AddEntityGUID
        };

        private static IEnumerable<int> GetParameterValuesForCompanionComponent_TransformSync()
        {
            yield return 1;
            yield return 10;
            yield return 100;
            yield return 1000;
#if !UNITY_EDITOR_LINUX // [DOTS-9856] Linux editor performance is much slower than other desktop platforms
            yield return 9000;
#endif
        }

        [Test, Performance]
        public unsafe void CompanionComponent_TransformSync([ValueSource("GetParameterValuesForCompanionComponent_TransformSync")] int companionCount)
        {
            BakingUtility.AddAdditionalCompanionComponentType(typeof(ConversionTestCompanionComponent));

            // Convert to create companions
            var gameObjects = new GameObject[companionCount];
            for (int i = 0; i < companionCount; i++)
            {
                var gameObject = m_Objects.CreateGameObject();
                gameObject.AddComponent<ConversionTestCompanionComponent>().SomeValue = 123;
                gameObjects[i] = gameObject;
            }

            using var blobAssetStore = new BlobAssetStore(128);
            var bakingSettings = MakeDefaultSettings();
            bakingSettings.BlobAssetStore = blobAssetStore;
            BakingUtility.BakeGameObjects(m_DefaultWorld.World, gameObjects, bakingSettings);

            // Verify we have created the correct number of companions
            var query = m_DefaultWorld.EntityManager.CreateEntityQuery(typeof(CompanionLink));
            Assert.AreEqual(companionCount, query.CalculateEntityCount());

            var entities = query.ToEntityArray(Allocator.Persistent);
            for (int i = 0; i < entities.Length; i++)
            {
                m_DefaultWorld.World.EntityManager.SetComponentData(entities[i],
                    LocalTransform.FromPosition(0.0f, 42f, 0.0f));
            }

            var companionGameObjectUpdateTransformSystem =
                m_DefaultWorld.World.GetExistingSystem<CompanionGameObjectUpdateTransformSystem>();
            var statePtr = m_DefaultWorld.World.Unmanaged.ResolveSystemState(companionGameObjectUpdateTransformSystem);
            Measure.ProfilerMarkers(statePtr->GetProfilerMarkerName(m_DefaultWorld.World));

            // Validate positions not moved
            for (int i = 0; i < entities.Length; i++)
            {
                var companionLink = m_DefaultWorld.World.EntityManager.GetComponentData<CompanionLink>(entities[i]);
                Assert.AreEqual(0f, companionLink.Companion.Value.transform.localPosition.y);
            }

            m_DefaultWorld.World.Update();
            statePtr->CompleteDependencyInternal();

            // Validate things moved
            for (int i = 0; i < entities.Length; i++)
            {
                var companionLink = m_DefaultWorld.World.EntityManager.GetComponentData<CompanionLink>(entities[i]);
                Assert.AreEqual(42f, companionLink.Companion.Value.transform.localPosition.y);
            }

            entities.Dispose();
        }

        private static IEnumerable<int> GetParameterValuesForCompanionComponent_ConvertScene()
        {
            yield return 1;
            yield return 10;
            yield return 100;
            yield return 1000;
#if !UNITY_EDITOR_LINUX // [DOTS-9856] Linux editor performance is much slower than other desktop platforms
            yield return 10000;
#endif
        }

        [Test, Performance, Timeout(1000000)]
        public void CompanionComponent_ConvertScene([ValueSource("GetParameterValuesForCompanionComponent_ConvertScene")] int numObjects)
        {
            BakingUtility.AddAdditionalCompanionComponentType(typeof(ConversionTestCompanionComponent));

            var scene = SubSceneTestsHelper.CreateScene(m_Assets.GetNextPath() + ".unity");
            for (int i = 0; i < numObjects; i++)
            {
                var go = m_Objects.CreateGameObject("", typeof(ConversionTestCompanionComponent));
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            using (var bakingWorld = new World("FullBake"))
            using (var blobAssetStore = new BlobAssetStore(128))
            {
                var bakingSettings = MakeDefaultSettings();
                bakingSettings.BlobAssetStore = blobAssetStore;
                bakingSettings.BakingFlags =
                    BakingUtility.BakingFlags.GameViewLiveConversion |
                    BakingUtility.BakingFlags.AddEntityGUID;

                Measure.Method(() => { BakingUtility.BakeScene(bakingWorld, scene, bakingSettings, false, null); })
                    .WarmupCount(2)
                    .MeasurementCount(10)
                    .Run();
            }
        }
    }
}
#endif
