using NUnit.Framework;

namespace Unity.Entities.Tests
{
    public class CleanupBufferElementDataInstantiateTests : ECSTestsFixture
    {
        [Test]
        public unsafe void InstantiateDoesNotCreatesCopy()
        {
            var original = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(original);
            buffer.CopyFrom(new EcsIntCleanupElement[] { 1, 2, 3 }); // smaller than 8
            var clone = m_Manager.Instantiate(original);

            buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(original);
            Assert.IsFalse(m_Manager.HasComponent<EcsIntCleanupElement>(clone));
        }
    }
}
