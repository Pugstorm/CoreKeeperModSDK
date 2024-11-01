using NUnit.Framework;
using Unity.Collections;
using Unity.Scenes.Editor.Tests;
using UnityEngine;

namespace Unity.Entities.Hybrid.Tests.Baking
{
    /// <summary>
    /// Test suite for validating the data contract of baked entities.
    /// These data contracts are expected by baking systems to produce deterministic results.
    /// </summary>
    internal class BakingDataTests : BakingSystemFixtureBase
    {
        private BakingSystem m_BakingSystem;
        private GameObject m_Prefab;
        private TestLiveConversionSettings m_Settings;

        [SetUp]
        public override void Setup()
        {
            m_Settings.Setup(true);
            base.Setup();

            m_BakingSystem = World.GetOrCreateSystemManaged<BakingSystem>();
            m_BakingSystem.PrepareForBaking(MakeDefaultSettings(), default);

            m_Manager = World.EntityManager;
            m_Prefab = InstantiatePrefab("Prefab");
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
            m_BakingSystem = null;
            m_Settings.TearDown();
        }

        class ContractValidationBaker : Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent authoring)
            {
                CreateAdditionalEntity(TransformUsageFlags.None);
            }
        }

        [UpdateInGroup(typeof(PostBakingSystemGroup))]
        [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
        partial class PrimaryEntity_AdditionalEntities_Queries_System : SystemBase
        {
            EntityQuery _PrimaryEntityQuery;
            EntityQuery _BakedEnttiesQuery;

            protected override void OnCreate()
            {
                _PrimaryEntityQuery = new EntityQueryBuilder(Allocator.Temp).WithOptions(EntityQueryOptions.IncludePrefab).WithAll<BakedEntity, Unity.Entities.Hybrid.Baking.AdditionalEntitiesBakingData>().Build(this);
                _BakedEnttiesQuery = new EntityQueryBuilder(Allocator.Temp).WithOptions(EntityQueryOptions.IncludePrefab).WithAll<BakedEntity>().Build(this);
            }

            protected override void OnUpdate()
            {
                Assert.AreEqual(1, _PrimaryEntityQuery.CalculateEntityCount(), "Expected one primary entity");
                Assert.AreEqual(2, _BakedEnttiesQuery.CalculateEntityCount(), "Expected baked entities (primary + additional)");
            }
        }

        [Description("Validate the standard data contract of baking entities.")]
        [Test]
        public void Baker_ChangedEntities_Have_BakedEntity_Tag()
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(ContractValidationBaker));
            var com = m_Prefab.AddComponent<DefaultAuthoringComponent>();

            using var blobAssetStore = new BlobAssetStore(128);
            var bakingSettings = MakeDefaultSettings();
            bakingSettings.ExtraSystems.Add(typeof(PrimaryEntity_AdditionalEntities_Queries_System));
            bakingSettings.BlobAssetStore = blobAssetStore;

            Assert.DoesNotThrow(() => BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, bakingSettings));
        }
    }
}
