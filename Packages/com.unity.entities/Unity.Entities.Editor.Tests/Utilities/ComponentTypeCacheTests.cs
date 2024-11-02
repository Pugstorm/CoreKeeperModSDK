using System;
using NUnit.Framework;
using System.Linq;
using Unity.Entities;

namespace Unity.Entities.Editor.Tests
{
    class ComponentTypeCacheTests
    {
        [Test]
        public void ComponentTypeCache_ExactMatch_MatchSingleType_FullName()
        {
            var type = typeof(Unity.Tests.NamespaceA.UniqueType);
            Assert.That(ComponentTypeCache.GetExactMatchingTypes(type.FullName), Is.EquivalentTo(new[] { type }));
            Assert.That(ComponentTypeCache.GetExactMatchingTypes(type.FullName.ToLower()), Is.EquivalentTo(new[] { type }));
        }

        [Test]
        public void ComponentTypeCache_ExactMatch_MatchMultipleTypes_FullName()
        {
            var type = typeof(Unity.Tests.NamespaceA.ComponentTypeCacheTest);

            Assert.That(ComponentTypeCache.GetExactMatchingTypes(type.FullName), Is.EquivalentTo(new[]
            {
                typeof(Unity.Tests.NamespaceA.ComponentTypeCacheTest),
                typeof(Unity.Tests.NamespaceA.COMPONENTTYPECacheTest),
            }));

            Assert.That(ComponentTypeCache.GetExactMatchingTypes(type.FullName.ToLower()), Is.EquivalentTo(new[]
            {
                typeof(Unity.Tests.NamespaceA.ComponentTypeCacheTest),
                typeof(Unity.Tests.NamespaceA.COMPONENTTYPECacheTest),
            }));
        }

        [Test]
        public void ComponentTypeCache_ExactMatch_MatchMultipleTypes_TypeName()
        {
            Assert.That(ComponentTypeCache.GetExactMatchingTypes("ComponentTypeCacheTest"), Is.EquivalentTo(new[]
            {
                typeof(Unity.Tests.NamespaceA.ComponentTypeCacheTest),
                typeof(Unity.Tests.NamespaceA.COMPONENTTYPECacheTest),
                typeof(Unity.Tests.NamespaceB.ComponentTypeCacheTest),
                typeof(Unity.Tests.NamespaceB.COMPONENTTYPECacheTest),
                typeof(global::COMPONENTTYPECacheTest),
                typeof(global::ComponentTypeCacheTest),
            }));

            Assert.That(ComponentTypeCache.GetExactMatchingTypes("componenttypecachetest"), Is.EquivalentTo(new[]
            {
                typeof(Unity.Tests.NamespaceA.ComponentTypeCacheTest),
                typeof(Unity.Tests.NamespaceA.COMPONENTTYPECacheTest),
                typeof(Unity.Tests.NamespaceB.ComponentTypeCacheTest),
                typeof(Unity.Tests.NamespaceB.COMPONENTTYPECacheTest),
                typeof(global::COMPONENTTYPECacheTest),
                typeof(global::ComponentTypeCacheTest),
            }));
        }

        [Test]
        public void ComponentTypeCache_FuzzyMatching()
        {
            var fuzzyTypes = ComponentTypeCache.GetFuzzyMatchingTypes("FuzzyT").ToArray();
            Assert.That(fuzzyTypes, Is.EquivalentTo(new[]
            {
                typeof(Unity.Tests.NamespaceA.FuzzyTest),
                typeof(Unity.Tests.NamespaceB.FuzzyTest),
                typeof(global::FuzzyTest)
            }));

            var type = ComponentTypeCache.GetFuzzyMatchingTypes(typeof(Unity.Tests.NamespaceA.FuzzyTest).FullName);
            Assert.That(type, Is.EquivalentTo(new[] { typeof(Unity.Tests.NamespaceA.FuzzyTest) }));

            type = ComponentTypeCache.GetFuzzyMatchingTypes(typeof(Unity.Tests.NamespaceB.FuzzyTest).FullName);
            Assert.That(type, Is.EquivalentTo(new[] { typeof(Unity.Tests.NamespaceB.FuzzyTest) }));

            type = ComponentTypeCache.GetFuzzyMatchingTypes(typeof(global::FuzzyTest).FullName);
            Assert.That(type, Is.EquivalentTo(new[]
            {
                typeof(Unity.Tests.NamespaceA.FuzzyTest),
                typeof(Unity.Tests.NamespaceB.FuzzyTest),
                typeof(global::FuzzyTest)
            }));
        }
    }
}

namespace Unity.Tests.NamespaceA
{
    struct FuzzyTest : IComponentData { }

    struct UniqueType : IComponentData{}

    struct ComponentTypeCacheTest : IComponentData{}

    // ReSharper disable once InconsistentNaming
    // ReSharper disable once UnusedType.Global
    struct COMPONENTTYPECacheTest : IComponentData{}
}

namespace Unity.Tests.NamespaceB
{
    struct FuzzyTest : IComponentData { }

    struct ComponentTypeCacheTest : IComponentData{}

    // ReSharper disable once InconsistentNaming
    // ReSharper disable once UnusedType.Global
    struct COMPONENTTYPECacheTest : IComponentData{}
}

// Global component type for component type cache tests
struct FuzzyTest : IComponentData { }
struct ComponentTypeCacheTest : IComponentData{}

// ReSharper disable once InconsistentNaming
// ReSharper disable once UnusedType.Global
struct COMPONENTTYPECacheTest : IComponentData{}
