using NUnit.Framework;
using Unity.Transforms;

namespace Unity.Entities.Editor.Tests.Utilities
{
    sealed class WorldListChangeTrackerTests
    {
        WorldListChangeTracker m_Tracker;

        [SetUp]
        public void SetUp()
        {
            m_Tracker = new WorldListChangeTracker();
        }

        [Test]
        public void DetectCreatedAndDestroyedWorld()
        {
            Assert.That(m_Tracker.HasChanged(), Is.True);
            Assert.That(m_Tracker.HasChanged(), Is.False);
            using (new World("test world", WorldFlags.None))
            {
                Assert.That(m_Tracker.HasChanged(), Is.True);
                Assert.That(m_Tracker.HasChanged(), Is.False);
            }
            Assert.That(m_Tracker.HasChanged(), Is.True);
            Assert.That(m_Tracker.HasChanged(), Is.False);
        }

        [Test]
        public void DoNotDetectChangesWithinWorlds()
        {
            using var w = new World("test world", WorldFlags.None);
            Assert.That(m_Tracker.HasChanged(), Is.True);
            Assert.That(m_Tracker.HasChanged(), Is.False);

            var nextSequenceNumber = World.NextSequenceNumber;
            using var q = w.EntityManager.CreateEntityQuery(typeof(LocalTransform));

            Assert.That(nextSequenceNumber, Is.Not.EqualTo(World.NextSequenceNumber), "World.NextSequenceNumber must be different to the previously captured one because we created a query.");
            Assert.That(m_Tracker.HasChanged(), Is.False, "No changes should be detected by the tracker since no world has been created or destroyed");
        }
    }
}
