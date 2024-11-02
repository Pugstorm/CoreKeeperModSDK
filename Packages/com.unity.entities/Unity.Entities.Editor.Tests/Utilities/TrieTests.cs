using NUnit.Framework;
using System.Linq;

namespace Unity.Entities.Editor.Tests
{
    class TrieTests
    {
        [Test]
        public void Trie_IndexAndSearch()
        {
            var trie = new Trie();
            trie.Index("bonjour");
            trie.Index("bon");
            trie.Index("bonsoir");

            Assert.That(trie.Search("bon").ToArray(), Is.EquivalentTo(new[] { "bonjour", "bon", "bonsoir" }));
            Assert.That(trie.Search("bonjour").ToArray(), Is.EquivalentTo(new[] { "bonjour" }));
            Assert.That(trie.Search("hello").ToArray(), Is.Empty);
        }

        [Test]
        public void Trie_SearchOnEmpty()
        {
            var trie = new Trie();
            Assert.That(trie.Search("something"), Is.Empty);
        }

        [Test]
        public void Trie_IndexDuplicate()
        {
            var trie = new Trie<int>();
            trie.Index("bonjour", 10);
            trie.Index("bonjour", 20);
            trie.Index("bonjour", 20);
            trie.Index("bonsoir", 30);

            Assert.That(trie.Search("bon"), Is.EquivalentTo(new[] { 10, 20, 30 }));
        }

        [Test]
        public void Trie_KeyCaseInsensitive()
        {
            var trie = new Trie<int>();
            trie.Index("aBc", 10);
            trie.Index("abc", 10);
            trie.Index("abC", 10);

            {
                var expected = new[] { 10 };
                Assert.That(trie.Search("a"), Is.EquivalentTo(expected));
                Assert.That(trie.Search("aBc"), Is.EquivalentTo(expected));
                Assert.That(trie.Search("abC"), Is.EquivalentTo(expected));
                Assert.That(trie.Search("abc"), Is.EquivalentTo(expected));
            }

            trie.Index("aBc", 20);
            trie.Index("abc", 30);
            trie.Index("abC", 40);

            {
                var expected = new[] { 10, 20, 30, 40 };
                Assert.That(trie.Search("a"), Is.EquivalentTo(expected));
                Assert.That(trie.Search("aBc"), Is.EquivalentTo(expected));
                Assert.That(trie.Search("abC"), Is.EquivalentTo(expected));
                Assert.That(trie.Search("abc"), Is.EquivalentTo(expected));
            }
        }

        [Test]
        public void Trie_IndexBetweenSearch()
        {
            var trie = new Trie<int>();
            trie.Index("bonjour", 10);

            Assert.That(trie.Search("bon"), Is.EquivalentTo(new[] { 10 }));

            trie.Index("bonsoir", 20);

            Assert.That(trie.Search("bon"), Is.EquivalentTo(new[] { 10, 20 }));
        }

        [Test]
        public void Trie_IndexAllTypesAndExportStatistics()
        {
            var allTypes = TypeManager.GetAllTypes().Where(t => t.Type != null).Select(t => t.Type.Name).ToArray();
            var allTypesCount = allTypes.Distinct().Count();

            var trie = new Trie(allTypes);

            var (totalNodeCount, nodeCountHavingValue, subNodesPerNode) = trie.GetStatistics();

            Assert.That(nodeCountHavingValue, Is.EqualTo(allTypesCount));

            var maxSubNodeCount = subNodesPerNode.Max();
            Debug.Log($"Total of {totalNodeCount} nodes, {nodeCountHavingValue} nodes with a value attached, avg of {subNodesPerNode.Average()} children per node, min: {subNodesPerNode.Min()}, max: {maxSubNodeCount}");

            for (var i = 0; i <= maxSubNodeCount; i++)
            {
                Debug.Log($"{i} children: {subNodesPerNode.Count(x => x == i)}");
            }
        }
    }
}
