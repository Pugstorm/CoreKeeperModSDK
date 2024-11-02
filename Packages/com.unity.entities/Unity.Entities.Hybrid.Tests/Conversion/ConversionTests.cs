using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Hybrid.Baking;
using Unity.Entities.TestComponents;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;
using UnityObject = UnityEngine.Object;

namespace Unity.Entities.Tests.Conversion
{
    class BakingTests : BakingTestFixture
    {
        private BakingSystem m_BakingSystem;

        [SetUp]
        public void SetUp(){
            m_BakingSystem = World.GetOrCreateSystemManaged<BakingSystem>();
            var blobAssetStore = new BlobAssetStore(128);
            var bakingSettings = MakeDefaultSettings();
            bakingSettings.BlobAssetStore = blobAssetStore;
            m_BakingSystem.PrepareForBaking(bakingSettings, default);
            base.Setup();
        }

        [TearDown]
        public new void TearDown()
        {
            base.TearDown();
            if (m_BakingSystem != null)
            {
                var assetStore = m_BakingSystem.BlobAssetStore;
                if (assetStore.IsCreated)
                    assetStore.Dispose();
            }
            m_BakingSystem = null;
        }

        [Test]
        public void LinkedEntityGroupAuthoringAddsLinkedEntityGroup()
        {
            var go = CreateGameObject();
            var child = CreateGameObject();
            var childChild = CreateGameObject();

            var comp = go.AddComponent<EntityRefTestDataAuthoring>();
            go.AddComponent<LinkedEntityGroupAuthoring>();
            comp.Value = child;
            child.transform.parent = go.transform;
            childChild.transform.parent = child.transform;

            BakingUtility.BakeGameObjects(World, new[] {go}, m_BakingSystem.BakingSettings);
            m_BakingSystem = World.GetOrCreateSystemManaged<BakingSystem>();
            var entity = m_BakingSystem.GetEntity(go);
            var manager = World.EntityManager;
            var childEntity = manager.GetComponentData<EntityRefTestData>(entity).Value;
            var buffer = manager.GetBuffer<LinkedEntityGroup>(entity);
            Assert.IsTrue(buffer.Length == 3);
            Assert.IsTrue(entity == buffer[0].Value); //First element in a LinkedEntityGroup should be the root entity

            EntitiesAssert.ContainsOnly(manager,
                EntityMatch.Exact<AdditionalEntitiesBakingData, EntityRefTestData, LinkedEntityGroup, LinkedEntityGroupBakingData, TransformAuthoring, Simulate>(entity),
                EntityMatch.Exact<AdditionalEntitiesBakingData, TransformAuthoring, Simulate>(childEntity),
                EntityMatch.Exact<AdditionalEntitiesBakingData, TransformAuthoring, Simulate>());
        }

        [Test]
        public void LinkedEntityGroupNotCreatedIfRootIsBakingOnlyEntity()
        {
            var go = CreateGameObject();
            var child = CreateGameObject();

            go.AddComponent<BakingOnlyEntityAuthoring>();
            go.AddComponent<LinkedEntityGroupAuthoring>();
            child.transform.parent = go.transform;

            BakingUtility.BakeGameObjects(World, new[] {go}, m_BakingSystem.BakingSettings);
            m_BakingSystem = World.GetOrCreateSystemManaged<BakingSystem>();
            var entity = m_BakingSystem.GetEntity(go);
            var manager = World.EntityManager;

            Assert.IsFalse(manager.HasBuffer<LinkedEntityGroup>(entity),
                "Root Entities that are marked as BakingOnlyEntity should not have a Linked Entity Group");
        }

        [Test]
        public void LinkedEntityGroupIgnoresBakingOnlyEntityAndChildren()
        {

            var go = CreateGameObject();
            for (int i = 0; i < 2; i++)
            {
                var child = CreateGameObject();
                child.transform.parent = go.transform;

                // Make one of the children + subchildren Bake Only
                if (i == 0)
                    child.AddComponent<BakingOnlyEntityAuthoring>();

                for (int j = 0; j < 2; j++)
                {
                    var childChild = CreateGameObject();
                    childChild.transform.parent = child.transform;
                }
            }

            go.AddComponent<LinkedEntityGroupAuthoring>();

            BakingUtility.BakeGameObjects(World, new[] {go}, m_BakingSystem.BakingSettings);
            m_BakingSystem = World.GetOrCreateSystemManaged<BakingSystem>();
            var entity = m_BakingSystem.GetEntity(go);
            var manager = World.EntityManager;
            var buffer = manager.GetBuffer<LinkedEntityGroup>(entity);

            Assert.AreEqual(4, buffer.Length,
                "Children Entities that are marked as BakingOnlyEntity should be ignored by a Linked Entity Group");
        }

        [Test]
        public void LinkedEntityGroupAddsAdditionalEntitiesFromBakingOnlyEntity()
        {

            var go = CreateGameObject();
            for (int i = 0; i < 2; i++)
            {
                var child = CreateGameObject();
                child.transform.parent = go.transform;
                child.AddComponent<BakingOnlyPrimaryWithAdditionalEntitiesTestAuthoring>();

                // Make one of the children + subchildren Bake Only
                if (i == 0)
                    child.AddComponent<BakingOnlyEntityAuthoring>();

                for (int j = 0; j < 2; j++)
                {
                    var childChild = CreateGameObject();
                    childChild.transform.parent = child.transform;
                    childChild.AddComponent<BakingOnlyPrimaryWithAdditionalEntitiesTestAuthoring>();
                }
            }

            go.AddComponent<LinkedEntityGroupAuthoring>();

            BakingUtility.BakeGameObjects(World, new[] {go}, m_BakingSystem.BakingSettings);
            m_BakingSystem = World.GetOrCreateSystemManaged<BakingSystem>();
            var entity = m_BakingSystem.GetEntity(go);
            var manager = World.EntityManager;
            var buffer = manager.GetBuffer<LinkedEntityGroup>(entity);

            Assert.AreEqual(10, buffer.Length,
                "Children Entities that are marked as BakingOnlyEntity should be ignored by a Linked Entity Group");
        }

        [Test]
        public void CreatesAdditionalEntitiesAddsTemporaryType()
        {
            var go = CreateGameObject();
            var comp = go.AddComponent<CreateAdditionalEntitiesAuthoring>();
            int noOfAdditionalEntities = 3;
            comp.number = noOfAdditionalEntities;

            BakingUtility.BakeGameObjects(World, new[] {go}, m_BakingSystem.BakingSettings);
            m_BakingSystem = World.GetOrCreateSystemManaged<BakingSystem>();
            var entity = m_BakingSystem.GetEntity(go);
            var manager = World.EntityManager;

            Assert.IsTrue(manager.HasComponent<AdditionalEntitiesBakingData>(entity));

            var entities = manager.GetBuffer<AdditionalEntitiesBakingData>(entity);
            Assert.IsTrue(entities.Length == noOfAdditionalEntities);

#if !DOTS_DISABLE_DEBUG_NAMES
            for (int i = 0; i < noOfAdditionalEntities; i++)
            {
                Assert.AreEqual(go.name, manager.GetName(entities[i].Value), "The default name of the Additional Entity was not set correctly.");
            }
#endif
        }

        [TestCase(true, true)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(false, false)]
        public void CreateAdditionalEntities_NewEntities_MatchActiveAndStaticStateOfPrimaryEntity(bool isActive, bool isStatic)
        {
            var go = CreateGameObject();
            go.SetActive(isActive);
            go.isStatic = isStatic;
            var comp = go.AddComponent<CreateAdditionalEntitiesAuthoring>();
            int noOfAdditionalEntities = 3;
            comp.number = noOfAdditionalEntities;

            BakingUtility.BakeGameObjects(World, new[] {go}, m_BakingSystem.BakingSettings);
            m_BakingSystem = World.GetOrCreateSystemManaged<BakingSystem>();
            var entity = m_BakingSystem.GetEntity(go);
            var manager = World.EntityManager;

            Assert.IsTrue(manager.HasComponent<AdditionalEntitiesBakingData>(entity), "Buffer not found");

            var entities = manager.GetBuffer<AdditionalEntitiesBakingData>(entity);
            Assert.AreEqual(noOfAdditionalEntities, entities.Length);

            var staticQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Static>().WithOptions(EntityQueryOptions.IncludeDisabledEntities).Build(manager);
            var disabledQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Disabled>().Build(manager);

            for (int i = 0; i < noOfAdditionalEntities; i++)
            {
                var additionalEntity = entities[i].Value;
                if (isStatic)
                    Assert.True(staticQuery.Matches(additionalEntity), "The additional entity is not static while the authoring object is static");
                else
                    Assert.False(staticQuery.Matches(additionalEntity), "The additional entity is static while the authoring object is not");

                if (isActive)
                    Assert.False(disabledQuery.Matches(additionalEntity), "The additional entity is disabled while the authoring object is active");
                else
                    Assert.True(disabledQuery.Matches(additionalEntity), "The additional entity is active while the authoring object is disabled");
            }
        }

        [Test]
        public void SetNameOnAdditionalEntitiesSetsName()
        {
#if !DOTS_DISABLE_DEBUG_NAMES
            var go = CreateGameObject();
            var comp = go.AddComponent<SetNameOnAdditionalEntityTestAuthoring>();
            int noOfAdditionalEntities = 3;
            comp.number = noOfAdditionalEntities;

            BakingUtility.BakeGameObjects(World, new[] {go}, m_BakingSystem.BakingSettings);
            m_BakingSystem = World.GetOrCreateSystemManaged<BakingSystem>();
            var entity = m_BakingSystem.GetEntity(go);
            var manager = World.EntityManager;

            Assert.IsTrue(manager.HasComponent<AdditionalEntitiesBakingData>(entity));
            var entities = manager.GetBuffer<AdditionalEntitiesBakingData>(entity);

            Assert.IsTrue(entities.Length == noOfAdditionalEntities);
            string[] expectedNames = new string[noOfAdditionalEntities];
            string[] actualNames = new string[noOfAdditionalEntities];

            for (int i = 0; i < noOfAdditionalEntities; i++)
            {
                expectedNames[i] = $"additionalEntity - {i}";
                actualNames[i] = manager.GetName(entities[i].Value);
            }
            for (int i = 0; i < noOfAdditionalEntities; i++)
            {
                Assert.Contains(expectedNames[i], actualNames, "The manual name of the Additional Entity was not set correctly.");
            }
#endif
        }

        [Test]
        public void AddLinkedEntityRootContainsAllChildrenAndSelfEntities()
        {
            var go = CreateGameObject();
            go.AddComponent<LinkedEntityGroupAuthoring>();
            go.AddComponent<CreateAdditionalEntitiesAuthoring>().number = 2;
            var child = CreateGameObject();
            child.AddComponent<CreateAdditionalEntitiesAuthoring>().number = 3;
            child.transform.parent = go.transform;

            BakingUtility.BakeGameObjects(World, new[] {go}, m_BakingSystem.BakingSettings);
            m_BakingSystem = World.GetOrCreateSystemManaged<BakingSystem>();
            var entity = m_BakingSystem.GetEntity(go);
            var childEntity = m_BakingSystem.GetEntity(child);
            var manager = World.EntityManager;

            Assert.IsTrue(manager.HasComponent<LinkedEntityGroup>(entity));
            Assert.IsFalse(manager.HasComponent<LinkedEntityGroup>(childEntity));

            var leg = manager.GetBuffer<LinkedEntityGroup>(entity);
            Assert.IsTrue(leg.Length == 7);
        }
    }
}
