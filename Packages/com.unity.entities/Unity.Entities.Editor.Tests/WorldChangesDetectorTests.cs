using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Unity.Entities.Editor.Tests
{
    class WorldChangesDetectorTests
    {
        readonly List<World> m_TestWorlds = new List<World>();
        readonly WorldListChangeTracker m_WorldListChangeTracker = new WorldListChangeTracker();

        [TearDown]
        public void Teardown()
        {
            foreach (var w in m_TestWorlds)
            {
                w.Dispose();
            }
        }

        [Test]
        public void DetectsChange_WhenWorldIsCreatedOrDestroyed()
        {
            var world1 = CreateWorld($"{nameof(WorldChangesDetectorTests)}.TestWorld1");

            Assert.That(m_WorldListChangeTracker.HasChanged(), Is.True, "First call should detect existing worlds");
            Assert.That(m_WorldListChangeTracker.HasChanged(), Is.False, "Second call on same worlds should not detect any change");

            var world2 = CreateWorld($"{nameof(WorldChangesDetectorTests)}.TestWorld2");
            Assert.That(m_WorldListChangeTracker.HasChanged(), Is.True);
            Assert.That(m_WorldListChangeTracker.HasChanged(), Is.False);

            DestroyWorld(world2);
            Assert.That(m_WorldListChangeTracker.HasChanged(), Is.True);
            Assert.That(m_WorldListChangeTracker.HasChanged(), Is.False);

            DestroyWorld(world1);
        }

        World CreateWorld(string name)
        {
            var w = new World(name);
            m_TestWorlds.Add(w);
            return w;
        }

        void DestroyWorld(World w)
        {
            if (!m_TestWorlds.Remove(w))
                return;
            w.Dispose();
        }
    }
}
