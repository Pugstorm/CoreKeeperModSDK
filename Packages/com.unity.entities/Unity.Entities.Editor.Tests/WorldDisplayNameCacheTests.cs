using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Unity.Entities.Editor.Tests
{
    class WorldDisplayNameCacheTests
    {
        readonly List<World> m_TestWorlds = new List<World>();

        [TearDown]
        public void Teardown()
        {
            foreach (var w in m_TestWorlds)
            {
                w.Dispose();
            }
        }

        [Test]
        public void GenerateUniqueDisplayName_WhenDuplicateNameDetected()
        {
            var c = new WorldDisplayNameCache(filter: null, GenerateUniqueDisplayName);

            var defaultWorldsNames = GetAllWorldNames().ToArray();
            Assert.That(defaultWorldsNames.Distinct().Count(), Is.EqualTo(World.All.Count), "Existing worlds should all have unique names");

            var testWorld1 = CreateWorld($"{nameof(WorldDisplayNameCacheTests)}.TestWorld");

            // No duplicated names, display names should be same as regular world names
            Assert.That(GetAllWorldsDisplayNames(c), Is.EquivalentTo(GetAllWorldNames()));

            // Introduce a duplicated name, now the display names should contain unique names for both duplicates
            var testWorld2 = CreateWorld(testWorld1.Name);
            var testWorld3 = CreateWorld(testWorld1.Name);
            var worldsDisplayNames = GetAllWorldsDisplayNames(c);
            Assert.That(worldsDisplayNames, Is.EquivalentTo(defaultWorldsNames.Concat(new[] { GenerateUniqueDisplayName(testWorld1), GenerateUniqueDisplayName(testWorld2), GenerateUniqueDisplayName(testWorld3) })));

            // Remove duplicates, display names should be back to default names
            DestroyWorld(testWorld1);
            DestroyWorld(testWorld2);
            Assert.That(GetAllWorldsDisplayNames(c), Is.EquivalentTo(GetAllWorldNames()));
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

        static IEnumerable<string> GetAllWorldNames()
        {
            foreach (var w in World.All)
            {
                yield return w.Name;
            }
        }

        static IEnumerable<string> GetAllWorldsDisplayNames(WorldDisplayNameCache cache)
        {
            cache.RebuildCache();
            foreach (var w in World.All)
            {
                yield return cache.GetWorldDisplayName(w);
            }
        }

        static string GenerateUniqueDisplayName(World w) => $"{w.Name} (#${w.SequenceNumber})";
    }
}
