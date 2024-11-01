using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.PerformanceTesting;

namespace Unity.Entities.Editor.PerformanceTests
{
    class TriePerformanceTests
    {
        [Test, Performance]
        public void Trie_IndexTypesPerfTests()
        {
            var listCache = TypeManager.GetAllTypes().Where(t => t.Type != null).Select(t => t.Type.Name.ToLowerInvariant()).Distinct().ToList();
            var trie = new Trie(TypeManager.GetAllTypes().Where(t => t.Type != null).Select(t => t.Type.Name));

            Measure.Method(() =>
            {
                TypeManager.GetAllTypes().Where(t => t.Type != null).Select(t => t.Type.Name.ToLowerInvariant()).Distinct().ToList();
            })
            .SampleGroup("List indexing")
            .WarmupCount(10)
            .MeasurementCount(100)
            .Run();

            Measure.Method(() =>
            {
                var t = new Trie();
                t.Index(TypeManager.GetAllTypes().Where(info => info.Type != null).Select(info => info.Type.Name));
            })
            .SampleGroup("Trie indexing")
            .WarmupCount(10)
            .MeasurementCount(100)
            .Run();

            Measure.Method(() =>
            {
                var items = listCache.Where(x => x.StartsWith("ro")).ToArray();
            })
            .SampleGroup("Search in List index")
            .WarmupCount(10)
            .MeasurementCount(100)
            .Run();

            Measure.Method(() =>
            {
                var items = trie.Search("ro").ToArray();
            })
            .SampleGroup("Search in Trie index")
            .WarmupCount(10)
            .MeasurementCount(100)
            .Run();
        }

        [Test, Performance]
        public void Trie_ScaleTest()
        {
            var allTypes = new HashSet<string>();
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in allAssemblies)
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    allTypes.Add(type.Name.ToLowerInvariant());
                }
            }

            var step = allTypes.Count / 5;
            for (var length = step; length < allTypes.Count; length += step)
            {
                Measure.Method(() =>
                {
                    var t = new Trie(allTypes.Take(length));
                })
                .SampleGroup($"Indexing {length} / {allTypes.Count}")
                .WarmupCount(1)
                .MeasurementCount(5)
                .Run();
            }
        }
    }
}
