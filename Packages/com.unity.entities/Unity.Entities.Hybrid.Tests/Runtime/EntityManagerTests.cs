using NUnit.Framework;

namespace Unity.Entities.Tests
{
    class EntityManagerTests : CompanionComponentsRuntimeTestFixture
    {
        [Test]
        public unsafe void ArchetypeIsManaged()
        {
            var types = new ComponentType[]
            {
                typeof(EcsTestData),
                typeof(ConversionTestCompanionComponent),
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                typeof(EcsTestManagedComponent)
#endif
            };

            var archetype = m_Manager.CreateArchetype(types).Archetype;

            Assert.IsFalse(archetype->IsManaged(ChunkDataUtility.GetIndexInTypeArray(archetype, types[0].TypeIndex)));
            Assert.IsTrue(archetype->IsManaged(ChunkDataUtility.GetIndexInTypeArray(archetype, types[1].TypeIndex)));
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            Assert.IsTrue(archetype->IsManaged(ChunkDataUtility.GetIndexInTypeArray(archetype, types[2].TypeIndex)));
#endif
        }
    }
}
