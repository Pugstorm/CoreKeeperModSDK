#if !UNITY_DISABLE_MANAGED_COMPONENTS
using System.Linq;
using NUnit.Framework;
using Unity.Entities.Serialization;
using Unity.Entities.Tests.Conversion;
using Unity.Scenes;

namespace Unity.Entities.Tests
{
    class CompanionComponentSerializeTests_Baking : BakingTestFixture
    {
        [Test]
        public void CompanionComponentSerialize()
        {
            BakingUtility.AddAdditionalCompanionComponentType(typeof(ConversionTestCompanionComponent));

            var root = CreateGameObject();
            var values = new[] { 123, 234, 345 };

            foreach (var value in values)
            {
                var gameObject = CreateGameObject();
                gameObject.transform.parent = root.transform;
                gameObject.AddComponent<ConversionTestCompanionComponent>().SomeValue = value;
            }

            using var blobAssetStore = new BlobAssetStore(128);
            var bakingSettings = MakeDefaultSettings();
            bakingSettings.BlobAssetStore = blobAssetStore;
            BakingUtility.BakeGameObjects(World, new[] {root}, bakingSettings);

            using var world = new World("temp");

            ReferencedUnityObjects objRefs = null;
            var writer = new TestBinaryWriter(world.UpdateAllocator.ToAllocator);
            SerializeUtilityHybrid.Serialize(m_Manager, writer, out objRefs);

            var reader = new TestBinaryReader(writer);
            SerializeUtilityHybrid.Deserialize(world.EntityManager, reader, objRefs);

            var query = world.EntityManager.CreateEntityQuery(typeof(ConversionTestCompanionComponent));
            var components = query.ToComponentArray<ConversionTestCompanionComponent>();

            CollectionAssert.AreEquivalent(components.Select(c => c.SomeValue), values);

            query.Dispose();
        }
    }
}
#endif
