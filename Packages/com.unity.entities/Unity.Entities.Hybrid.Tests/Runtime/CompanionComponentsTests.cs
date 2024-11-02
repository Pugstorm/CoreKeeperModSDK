#if !UNITY_DISABLE_MANAGED_COMPONENTS
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Hybrid.Tests;
using Unity.Entities.Tests.Conversion;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Unity.Entities.Tests
{
    class CompanionComponentsRuntimeTests_Baking : CompanionComponentTestFixture
    {
        protected BakingSettings MakeDefaultBakingSettings() => new BakingSettings
        {
            BakingFlags = BakingUtility.BakingFlags.AssignName
        };

        [Test]
        public unsafe void CompanionComponent_TransformSync_OutOfOrder()
        {
            BakingUtility.AddAdditionalCompanionComponentType(typeof(ConversionTestCompanionComponent));

            // Convert to create companions
            var gameObjects = new GameObject[3];
            for (int i = 0; i < gameObjects.Length; i++)
            {
                var gameObject = CreateGameObject();
                gameObject.AddComponent<ConversionTestCompanionComponent>().SomeValue = 123;
                gameObjects[i] = gameObject;
            }

            using var blobAssetStore = new BlobAssetStore(128);
            var bakingSettings = MakeDefaultBakingSettings();
            bakingSettings.BlobAssetStore = blobAssetStore;
            BakingUtility.BakeGameObjects(m_World, gameObjects, bakingSettings);

            // Verify we have created the correct number of companions
            var query = m_Manager.CreateEntityQuery(typeof(CompanionLink));
            Assert.AreEqual(3, query.CalculateEntityCount());

            var entities = query.ToEntityArray(Allocator.Persistent);
            m_World.EntityManager.SetComponentData(entities[0], LocalTransform.FromPosition(0.0f, 1, 0.0f));
            m_World.EntityManager.SetComponentData(entities[1], LocalTransform.FromPosition(0.0f, 2, 0.0f));
            m_World.EntityManager.SetComponentData(entities[2], LocalTransform.FromPosition(0.0f, 3, 0.0f));

            var companionGameObjectUpdateTransformSystem = m_World.GetExistingSystem<CompanionGameObjectUpdateTransformSystem>();

            // Validate positions not moved
            for (int i = 0; i < 3; i++)
            {
                var companionLink = m_World.EntityManager.GetComponentData<CompanionLink>(entities[i]);
                Assert.AreEqual(0f, companionLink.Companion.Value.transform.localPosition.y);
            }

            // Re-order the entities
            m_World.EntityManager.AddComponent<EcsTestData>(entities[1]);

            m_World.Update();
            var statePtr = m_World.Unmanaged.ResolveSystemState(companionGameObjectUpdateTransformSystem);
            statePtr->CompleteDependencyInternal();

            // Validate things moved and to the correct place
            var companionLink0 = m_World.EntityManager.GetComponentData<CompanionLink>(entities[0]);
            var companionLink1 = m_World.EntityManager.GetComponentData<CompanionLink>(entities[1]);
            var companionLink2 = m_World.EntityManager.GetComponentData<CompanionLink>(entities[2]);

            Assert.AreEqual(1, companionLink0.Companion.Value.transform.localPosition.y);
            Assert.AreEqual(2, companionLink1.Companion.Value.transform.localPosition.y);
            Assert.AreEqual(3, companionLink2.Companion.Value.transform.localPosition.y);

            entities.Dispose();
        }

        private float DiffMatrices(Matrix4x4 a, Matrix4x4 b)
        {
            float diff = 0f;
            for (int i = 0; i < 4; ++i)
            for (int j = 0; j < 4; ++j)
                diff += math.abs(b[i, j] - a[i, j]);
            return diff;
        }

        [Test]
        public unsafe void CompanionComponent_TransformSync_NegativeScale()
        {
            BakingUtility.AddAdditionalCompanionComponentType(typeof(ConversionTestCompanionComponent));

            // Convert to create companions
            var gameObjects = new GameObject[2];
            for (int i = 0; i < gameObjects.Length; i++)
            {
                var gameObject = CreateGameObject();
                gameObject.AddComponent<ConversionTestCompanionComponent>().SomeValue = 123;
                // Add non uniform scale so the post transform matrix is added
                gameObject.transform.localScale = new Vector3(1, 2, 3);
                gameObjects[i] = gameObject;
            }
            // We parent the second gameobject to the first one
            gameObjects[1].transform.parent = gameObjects[0].transform;

            using var blobAssetStore = new BlobAssetStore(128);
            var bakingSettings = MakeDefaultBakingSettings();
            bakingSettings.BlobAssetStore = blobAssetStore;
            // We only bake the root
            BakingUtility.BakeGameObjects(m_World, new []{gameObjects[0]}, bakingSettings);

            // Verify we have created the correct number of companions
            var query = m_Manager.CreateEntityQuery(typeof(CompanionLink));
            Assert.AreEqual(gameObjects.Length, query.CalculateEntityCount());

            var entities = query.ToEntityArray(Allocator.Persistent);
            m_World.EntityManager.SetComponentData(entities[0], LocalTransform.FromPositionRotation(new float3(0.1f, 0.5f, -0.3f), quaternion.Euler(math.PI * new float3(0.1f, 0.5f, -0.75f))));
            m_World.EntityManager.SetComponentData(entities[1], LocalTransform.FromPositionRotation(new float3(-1f, 2f, 3f), quaternion.Euler(math.PI * new float3(-0.5f, -0.3f, -0.1f))));

            // Add a negative scale to the parent
            m_World.EntityManager.SetComponentData(entities[0], new PostTransformMatrix {Value = float4x4.Scale(-1, 1, 1)});

            var companionGameObjectUpdateTransformSystem = m_World.GetExistingSystem<CompanionGameObjectUpdateTransformSystem>();

            // Validate positions not moved
            for (int i = 0; i < gameObjects.Length; i++)
            {
                var companionLink = m_World.EntityManager.GetComponentData<CompanionLink>(entities[i]);
                Assert.AreEqual(0f, companionLink.Companion.Value.transform.localPosition.y);
            }

            m_World.Update();
            var statePtr = m_World.Unmanaged.ResolveSystemState(companionGameObjectUpdateTransformSystem);
            statePtr->CompleteDependencyInternal();

            // Validate things moved and to the correct place
            var companionLink0 = m_World.EntityManager.GetComponentData<CompanionLink>(entities[0]);
            var companionLink1 = m_World.EntityManager.GetComponentData<CompanionLink>(entities[1]);

            var localToWorld0 = m_World.EntityManager.GetComponentData<LocalToWorld>(entities[0]);
            var localToWorld1 = m_World.EntityManager.GetComponentData<LocalToWorld>(entities[1]);

            Assert.IsTrue(DiffMatrices(localToWorld0.Value, companionLink0.Companion.Value.transform.localToWorldMatrix) < 0.0001f);
            Assert.IsTrue(DiffMatrices(localToWorld1.Value, companionLink1.Companion.Value.transform.localToWorldMatrix) < 0.0001f);
            entities.Dispose();
        }

        [Test]
        public void CompanionComponent_OnDisabledGameObject_IsConvertedAndDisabled()
        {
            BakingUtility.AddAdditionalCompanionComponentType(typeof(ConversionTestCompanionComponent));
            var gameObject = CreateGameObject();
            gameObject.AddComponent<ConversionTestCompanionComponent>().SomeValue = 123;
            gameObject.SetActive(false);

            using var blobAssetStore = new BlobAssetStore(128);
            var bakingSettings = MakeDefaultBakingSettings();
            bakingSettings.BlobAssetStore = blobAssetStore;
            BakingUtility.BakeGameObjects(m_World, new[] {gameObject}, bakingSettings);
            var query = m_World.EntityManager.CreateEntityQuery(new EntityQueryDesc{ All = new ComponentType[]{typeof(ConversionTestCompanionComponent)}, Options = EntityQueryOptions.IncludeDisabledEntities});
            var entities = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(1, entities.Length);

            var companionComponent = m_World.EntityManager.GetComponentObject<ConversionTestCompanionComponent>(entities[0]);
            Assert.AreEqual(123, companionComponent.SomeValue);

            // give the hybrid component system a chance to activate this object, and check it did not in fact do it.
            m_World.Update();

            var companion = m_World.EntityManager.GetComponentData<CompanionLink>(entities[0]);
            Assert.IsFalse(companion.Companion.Value.activeSelf);

            entities.Dispose();
        }
    }
}
#endif
