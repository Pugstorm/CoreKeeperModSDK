using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Conversion;
using Unity.Entities.Hybrid.Baking;
using Unity.Entities.Hybrid.EndToEnd.Tests;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;

namespace Unity.Entities.Tests
{
}

#if !UNITY_DISABLE_MANAGED_COMPONENTS
namespace Unity.Entities.Tests.Conversion
{
    class CompanionComponentBakingTests : BakingTestFixture
    {
        string m_TempAssetDir;

        [OneTimeSetUp]
        public void SetUp()
        {
            var guid = AssetDatabase.CreateFolder("Assets", nameof(CompanionComponentBakingTests));
            m_TempAssetDir = AssetDatabase.GUIDToAssetPath(guid);

            BakingUtility.AddAdditionalCompanionComponentType(typeof(ConversionTestCompanionComponent));
            BakingUtility.AddAdditionalCompanionComponentType(typeof(ConversionTestCompanionComponentWithEntity));
            BakingUtility.AddAdditionalCompanionComponentType(typeof(ConversionTestCompanionComponentA));
            BakingUtility.AddAdditionalCompanionComponentType(typeof(ConversionTestCompanionComponentB));
            BakingUtility.AddAdditionalCompanionComponentType(typeof(ConversionTestCompanionComponentC));
            BakingUtility.AddAdditionalCompanionComponentType(typeof(ConversionTestCompanionComponentPrefabReference));
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            AssetDatabase.DeleteAsset(m_TempAssetDir);
        }

        class ConversionTestCompanionComponentWithEntityBaker : Baker<ConversionTestCompanionComponentWithEntity>
        {
            public override void Bake(ConversionTestCompanionComponentWithEntity authoring)
            {
                // This test might require transform components
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponentObject(entity, authoring);
            }
        }

        class ConversionTestCompanionComponentABaker : Baker<ConversionTestCompanionComponentA>
        {
            public override void Bake(ConversionTestCompanionComponentA authoring)
            {
                // This test might require transform components
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponentObject(entity, authoring);
            }
        }

        class ConversionTestCompanionComponentBBaker : Baker<ConversionTestCompanionComponentB>
        {
            public override void Bake(ConversionTestCompanionComponentB authoring)
            {
                // This test might require transform components
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponentObject(entity, authoring);
            }
        }

        class ConversionTestCompanionComponentCBaker : Baker<ConversionTestCompanionComponentC>
        {
            public override void Bake(ConversionTestCompanionComponentC authoring)
            {
                // This test might require transform components
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponentObject(entity, authoring);
            }
        }

        [Test]
        public void ManagedComponentSimple()
        {
            var gameObject = CreateGameObject();
            gameObject.AddComponent<ConversionTestCompanionComponent>().SomeValue = 123;

            using var blobAssetStore = new BlobAssetStore(128);
            var bakingSettings = MakeDefaultSettings();
            bakingSettings.BlobAssetStore = blobAssetStore;
            BakingUtility.BakeGameObjects(World, new []{gameObject}, bakingSettings);
            var query = m_Manager.CreateEntityQuery(new EntityQueryDesc
                {All = new[] {new ComponentType(typeof(ConversionTestCompanionComponent))}});
            var entities = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(1, entities.Length);
            var entity = entities[0];
            entities.Dispose();

            gameObject.GetComponent<ConversionTestCompanionComponent>().SomeValue = 234;
            Assert.AreEqual(123, m_Manager.GetComponentObject<ConversionTestCompanionComponent>(entity).SomeValue);

            var instance = m_Manager.Instantiate(entity);

            m_Manager.GetComponentObject<ConversionTestCompanionComponent>(entity).SomeValue = 345;
            Assert.AreEqual(123, m_Manager.GetComponentObject<ConversionTestCompanionComponent>(instance).SomeValue);

            var instances = new NativeArray<Entity>(2, Allocator.Temp);
            m_Manager.Instantiate(entity, instances);

            Assert.AreEqual(345, m_Manager.GetComponentObject<ConversionTestCompanionComponent>(instances[0]).SomeValue);
            Assert.AreEqual(345, m_Manager.GetComponentObject<ConversionTestCompanionComponent>(instances[1]).SomeValue);
        }

        [Test]
        public void CompanionGameObjectTransform_WithScale_IsSetFromLocalToWorld()
        {
            var gameObject = CreateGameObject("source", typeof(ConversionTestCompanionComponent));
            gameObject.transform.localPosition = new UnityEngine.Vector3(1, 2, 3);
            gameObject.transform.localRotation = UnityEngine.Quaternion.Euler(10, 20, 30);
            gameObject.transform.localScale = new UnityEngine.Vector3(4, 5, 6);
            var reference = gameObject.transform.localToWorldMatrix;

            using var blobAssetStore = new BlobAssetStore(128);
            var bakingSettings = MakeDefaultSettings();
            bakingSettings.BlobAssetStore = blobAssetStore;
            BakingUtility.BakeGameObjects(World, new []{gameObject}, bakingSettings);
            var query = m_Manager.CreateEntityQuery(new EntityQueryDesc
                {All = new[] {new ComponentType(typeof(ConversionTestCompanionComponent))}});
            var entities = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(1, entities.Length);
            var entity = entities[0];
            entities.Dispose();

            TestUtilities.RegisterSystems(World, TestUtilities.SystemCategories.CompanionComponents);

            var companion = m_Manager.GetComponentData<CompanionLink>(entity).Companion.Value;

            Assert.AreNotEqual(gameObject, companion);
            AssertEqual(reference, companion.transform.localToWorldMatrix);

            World.Update();

            AssertEqual(reference, companion.transform.localToWorldMatrix);

            var matrix = float4x4.TRS(new float3(2, 3, 4), quaternion.Euler(3, 4, 5), new float3(4, 5, 6));
            m_Manager.SetComponentData(entity, new LocalToWorld {Value = matrix});

            World.Update();

            AssertEqual(matrix, companion.transform.localToWorldMatrix);

            void AssertEqual(UnityEngine.Matrix4x4 a, UnityEngine.Matrix4x4 b)
            {
                for (int i = 0; i < 16; ++i)
                {
                    Assert.AreEqual(a[i], b[i], 0.001f);
                }
            }
        }

        [Test]
        public void CompanionGameObject_ActivatesIfNotPrefabOrDisabled()
        {
            // Create a prefab asset with an Companion Component
            var prefab = CreateGameObject("prefab", typeof(ConversionTestCompanionComponent));
            var prefabPath = m_TempAssetDir + "/TestPrefab.prefab";
            Assert.IsFalse(prefab.IsPrefab());
            prefab = PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath, out var success);
            Assert.IsTrue(success && prefab.IsPrefab());

            // Create a GameObject that references the prefab, in order to trigger the conversion of the prefab
            var gameObject = CreateGameObject("prefab_ref", typeof(ConversionTestCompanionComponentPrefabReference));
            gameObject.GetComponent<ConversionTestCompanionComponentPrefabReference>().Prefab = prefab;

            // Run the actual conversion, we only care about the prefab so we destroy the other entity
            using var blobAssetStore = new BlobAssetStore(128);
            var bakingSettings = MakeDefaultSettings();
            bakingSettings.BlobAssetStore = blobAssetStore;
            BakingUtility.BakeGameObjects(World, new []{gameObject}, bakingSettings);

            var query = m_Manager.CreateEntityQuery(new EntityQueryDesc
            {
                None = new[] {new ComponentType(typeof(Prefab))},
                Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab
            });
            var entities = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(1, entities.Length);
            var dummy = entities[0];
            entities.Dispose();
            m_Manager.DestroyEntity(dummy);

            EntitiesAssert.ContainsOnly(m_Manager, EntityMatch.Exact<CompanionLink, CompanionLinkTransform, CompanionReference, ConversionTestCompanionComponent, Prefab, LinkedEntityGroup, AdditionalEntitiesBakingData, LinkedEntityGroupBakingData, TransformAuthoring>(k_CommonComponents));

            // Accessing the prefab entity and its companion GameObject can't be directly done with GetSingleton because it requires EntityQueryOptions.IncludePrefab
            var companionQuery = EmptySystem.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] {ComponentType.ReadOnly<CompanionLink>()},
                Options = EntityQueryOptions.IncludePrefab
            });
            var prefabEntity = companionQuery.GetSingletonEntity();
            var prefabCompanion = m_Manager.GetComponentData<CompanionLink>(prefabEntity).Companion.Value;

            // Create an instance, the expectation is that the prefab remains inactive, but the instance activates
            var instanceEntity = m_Manager.Instantiate(prefabEntity);
            var instanceCompanion = m_Manager.GetComponentData<CompanionLink>(instanceEntity).Companion.Value;

            // Activation happens through a system, so before the first update everything is inactive
            Assert.IsFalse(prefabCompanion.activeSelf);
            Assert.IsFalse(instanceCompanion.activeSelf);

            // Register all the Companion Component related systems, including the one that deals with activation
            TestUtilities.RegisterSystems(World, TestUtilities.SystemCategories.CompanionComponents);

            // After an update, the prefab should remain inactive, but the instance should be active
            World.Update();
            Assert.IsFalse(prefabCompanion.activeSelf);
            Assert.IsTrue(instanceCompanion.activeSelf);

            // Let's reverse the test, demote the prefab to a regular entity, and disable the instance
            m_Manager.RemoveComponent<Prefab>(prefabEntity);
            m_Manager.AddComponent<Disabled>(instanceEntity);

            // After an update, the prefab which isn't one anymore should be active, and the disabled entity should be inactive
            World.Update();
            Assert.IsTrue(prefabCompanion.activeSelf);
            Assert.IsFalse(instanceCompanion.activeSelf);

            // Let's reverse once more and get back to the initial state
            m_Manager.AddComponent<Prefab>(prefabEntity);
            m_Manager.RemoveComponent<Disabled>(instanceEntity);

            // After an update, the prefab should be inactive again, and the instance should be active again
            World.Update();
            Assert.IsFalse(prefabCompanion.activeSelf);
            Assert.IsTrue(instanceCompanion.activeSelf);
        }

        public class ConversionTestCompanionComponentWithEntity : UnityEngine.MonoBehaviour, UnityEngine.ISerializationCallbackReceiver
        {
            public static Entity DefaultEntity;
            public Entity SomeEntity;

            public void OnBeforeSerialize()
            {
            }

            public void OnAfterDeserialize()
            {
                SomeEntity = DefaultEntity;
            }
        }

        [Test]
        public void CompanionComponents_AreNotBeRemapped_WhenInstantiated()
        {
            // Setup a simple entity with a Companion Component that contains an Entity field
            var gameObjectPrefab = CreateGameObject("prefab", typeof(ConversionTestCompanionComponentWithEntity));
            using var blobAssetStore = new BlobAssetStore(128);
            var bakingSettings = MakeDefaultSettings();
            bakingSettings.BlobAssetStore = blobAssetStore;
            BakingUtility.BakeGameObjects(World, new []{gameObjectPrefab}, bakingSettings);
            var query = m_Manager.CreateEntityQuery(new EntityQueryDesc
                {All = new[] {new ComponentType(typeof(ConversionTestCompanionComponentWithEntity))}});
            var entities = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(1, entities.Length);
            var entityPrefab = entities[0];
            entities.Dispose();

            // Add a managed and an unmanaged component, each with an entity field pointing to their own entity
            m_Manager.AddComponentData(entityPrefab, new EcsTestDataEntity {value1 = entityPrefab});
            m_Manager.AddComponentData(entityPrefab, new EcsTestManagedDataEntity {value1 = entityPrefab});

            // This is necessary because there is a bug in Instantiate that only remaps if a LinkedEntityGroup is present
            var buffer = m_Manager.AddBuffer<LinkedEntityGroup>(entityPrefab);
            buffer.Add(entityPrefab);

            // Since Entity contents aren't serialized, GameObject.Instantiate won't copy the Entity field
            // But we need that field to be set to the prefab entity in order to check if the remapping happens
            // So we're abusing ISerializationCallbackReceiver with a static field to pretend it got cloned
            ConversionTestCompanionComponentWithEntity.DefaultEntity = entityPrefab;

            // Create a bunch of instances
            var instances = m_Manager.Instantiate(entityPrefab, 10, Allocator.Temp);

            foreach (var instance in instances)
            {
                var companionComponent = m_Manager.GetComponentObject<ConversionTestCompanionComponentWithEntity>(instance);
                var unmanaged = m_Manager.GetComponentData<EcsTestDataEntity>(instance);
                var managed = m_Manager.GetComponentData<EcsTestManagedDataEntity>(instance);

                // The hybrid component SHOULD NOT be remapped, so it still contains the prefab entity
                Assert.AreEqual(companionComponent.SomeEntity, entityPrefab);

                // The other two components SHOULD be remapped, so they now point to the instanced entity
                Assert.AreEqual(unmanaged.value1, instance);
                Assert.AreEqual(managed.value1, instance);
            }
        }

        class ConversionTestCompanionComponentPrefabReference : UnityEngine.MonoBehaviour
        {
            public UnityEngine.GameObject Prefab;
        }

        class ConversionTestCompanionComponentPrefabReferenceBaker : Baker<ConversionTestCompanionComponentPrefabReference>
        {
            public override void Bake(ConversionTestCompanionComponentPrefabReference authoring)
            {
                // This test could require transform components
                GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic);
            }
        }

        public class ConversionTestCompanionComponentA : UnityEngine.MonoBehaviour
        {
            public int SomeValue;
        }
        public class ConversionTestCompanionComponentB : UnityEngine.MonoBehaviour
        {
            public int SomeValue;
        }
        public class ConversionTestCompanionComponentC : UnityEngine.MonoBehaviour
        {
            public int SomeValue;
        }

        [Test]
        public void MultipleCompanionComponents_DontGetMixedUp_WhenInstantiated()
        {
            // Setup a simple entity with multiple hybrid components
            var gameObjectPrefab = CreateGameObject("prefab",
                typeof(ConversionTestCompanionComponentA),
                typeof(ConversionTestCompanionComponentB),
                typeof(ConversionTestCompanionComponentC));

            using var blobAssetStore = new BlobAssetStore(128);
            var bakingSettings = MakeDefaultSettings();
            bakingSettings.BlobAssetStore = blobAssetStore;
            BakingUtility.BakeGameObjects(World, new []{gameObjectPrefab}, bakingSettings);
            var findEntityQuery = m_Manager.CreateEntityQuery(new EntityQueryDesc
                {All = new[] {new ComponentType(typeof(ConversionTestCompanionComponentA))}});
            var entities = findEntityQuery.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(1, entities.Length);
            var entityPrefab = entities[0];
            entities.Dispose();

            // Create a bunch of instances
            var instances = m_Manager.Instantiate(entityPrefab, 10, Allocator.Temp);

            var query = EmptySystem.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<ConversionTestCompanionComponentA>(),
                    ComponentType.ReadOnly<ConversionTestCompanionComponentB>(),
                    ComponentType.ReadOnly<ConversionTestCompanionComponentC>(),
                }
            });

            var a = query.ToComponentArray<ConversionTestCompanionComponentA>();
            var b = query.ToComponentArray<ConversionTestCompanionComponentB>();
            var c = query.ToComponentArray<ConversionTestCompanionComponentC>();

            // The source doesn't have the Prefab tag, so it also gets picked up by the query
            Assert.AreEqual(instances.Length + 1, a.Length);
            Assert.AreEqual(instances.Length + 1, b.Length);
            Assert.AreEqual(instances.Length + 1, c.Length);

            CollectionAssert.AllItemsAreUnique(a);
            CollectionAssert.AllItemsAreUnique(b);
            CollectionAssert.AllItemsAreUnique(c);
        }

        [Test]
        public void CompanionComponentConversion_DoesNotThrow_WhenCompanionIsDestroyed()
        {
            var gameObject = InstantiateGameObject(LoadPrefab("Prefab_MissingMB"));
            gameObject.AddComponent<ConversionTestCompanionComponent>().SomeValue = 123;

            Entity entity = default;
            Assert.DoesNotThrow(() =>
            {
                using var blobAssetStore = new BlobAssetStore(128);
                var bakingSettings = MakeDefaultSettings();
                bakingSettings.BlobAssetStore = blobAssetStore;
                BakingUtility.BakeGameObjects(World, new []{gameObject}, bakingSettings);
                var findEntityQuery = m_Manager.CreateEntityQuery(new EntityQueryDesc
                    {All = new[] {new ComponentType(typeof(ConversionTestCompanionComponent))}});
                var entities = findEntityQuery.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(1, entities.Length);
                entity = entities[0];
                entities.Dispose();
            });
            Assert.That(m_Manager.HasComponent<CompanionLink>(entity), Is.True);
        }

        [Test]
        public void CompanionComponentConversion_DoesNotThrow_WhenRemovingComponentsWithRequireComponentAttribute()
        {
            var gameObject = CreateGameObject("RemoveComponentRequiredByAttribute");

            gameObject.AddComponent<ConversionTestCompanionComponent>();
            gameObject.AddComponent<ConversionTestCompanionComponentRequiredByAnotherComponent>();
            gameObject.AddComponent<ConversionTestCompanionComponentWithRequireComponentAttribute>();

            Entity entity = default;
            Assert.DoesNotThrow(() =>
            {
                using var blobAssetStore = new BlobAssetStore(128);
                var bakingSettings = MakeDefaultSettings();
                bakingSettings.BlobAssetStore = blobAssetStore;
                BakingUtility.BakeGameObjects(World, new []{gameObject}, bakingSettings);
                var findEntityQuery = m_Manager.CreateEntityQuery(new EntityQueryDesc
                    {All = new[] {new ComponentType(typeof(ConversionTestCompanionComponent))}});
                var entities = findEntityQuery.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(1, entities.Length);
                entity = entities[0];
                entities.Dispose();
            });

            var component = m_Manager.GetComponentObject<ConversionTestCompanionComponent>(entity);
            var companion = component.gameObject;

            Assert.IsTrue(companion.GetComponent<ConversionTestCompanionComponentWithRequireComponentAttribute>() == null);
            Assert.IsTrue(companion.GetComponent<ConversionTestCompanionComponentRequiredByAnotherComponent>() == null);
        }
    }
}
#endif
