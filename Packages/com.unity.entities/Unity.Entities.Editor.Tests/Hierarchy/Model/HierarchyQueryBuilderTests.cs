using NUnit.Framework;
using System;
using System.Collections;
using System.Linq;
using Unity.Collections;

namespace Unity.Entities.Editor.Tests
{
    class HierarchyQueryBuilderTests
    {
        public static IEnumerable ParseQueryCaseSource()
        {
            yield return new TestCaseData("       ", false);
            yield return new TestCaseData("", false);
            yield return new TestCaseData(string.Empty, false);
            yield return new TestCaseData("some random string", false);
            yield return new TestCaseData("c=SomeTypeThatDoesntExist", false);
            yield return new TestCaseData($"c={nameof(EcsTestData)}", true);
            yield return new TestCaseData($"c={nameof(EcsTestData)}", true);
            yield return new TestCaseData($"C={nameof(EcsTestData)}", true);
            yield return new TestCaseData($"C={nameof(EcsTestData)}", true);
            yield return new TestCaseData($"someothertextc={nameof(EcsTestData)}", false);
            yield return new TestCaseData($"c=!{nameof(EcsTestData)}", false);
            yield return new TestCaseData($"C=!{nameof(EcsTestData)}", false);
            yield return new TestCaseData($"c=!{nameof(EcsTestData)}", false);
            yield return new TestCaseData($"c={nameof(EcsTestData2)} c={nameof(EcsTestData)}", true);
            yield return new TestCaseData($"c={nameof(EcsTestData2)} c={nameof(EcsTestSharedComp)}", true);
            yield return new TestCaseData($"with c={nameof(EcsTestData2)} some c={nameof(EcsTestData)} text c={nameof(EcsTestSharedComp)} in between", true);
        }

        [TestCaseSource(nameof(ParseQueryCaseSource))]
        public void QueryBuilder_ParseQuery(string input, bool isQueryExpected)
        {
            var r = HierarchyQueryBuilder.BuildQuery(input);
            if (isQueryExpected)
                Assert.That(r.EntityQueryDesc, Is.Not.Null, $"input \"{input}\" should be a valid input to build a query");
            else
                Assert.That(r.EntityQueryDesc, Is.Null, $"input \"{input}\" should not be a valid input to build a query");
        }

        [Test]
        [TestCaseSource(nameof(EnumerateAllCategories))]
        public void QueryBuilder_ResultQueryContainsExpectedTypes(TypeManager.TypeCategory typeCategory)
        {
            var componentType = TypeManager.AllTypes.First(x => x.Category == typeCategory && x.Type != null && x.Type.Name != x.Type.FullName).Type;

            var r = HierarchyQueryBuilder.BuildQuery($"c={componentType.FullName}");
            Assert.That(r.IsValid, Is.True);
            if (typeCategory == TypeManager.TypeCategory.EntityData)
            {
                Assert.That(r.EntityQueryDesc.All, Is.Empty);
            }
            else
            {
                Assert.That(r.EntityQueryDesc.All, Is.EquivalentTo(new ComponentType[] { componentType }));
            }
            Assert.That(r.EntityQueryDesc.None, Is.Empty);
            Assert.That(r.EntityQueryDesc.Any, Is.Empty);
        }

        [Test]
        public void QueryBuilder_ResultQueryIncludesPrefabsAndDisabledEntities()
        {
            var r = HierarchyQueryBuilder.BuildQuery($"c={typeof(EntityGuid).FullName}");
            Assert.That(r.EntityQueryDesc.Options & EntityQueryOptions.IncludeDisabledEntities, Is.EqualTo(EntityQueryOptions.IncludeDisabledEntities));
            Assert.That(r.EntityQueryDesc.Options & EntityQueryOptions.IncludePrefab, Is.EqualTo(EntityQueryOptions.IncludePrefab));
        }

        [Test]
        public void QueryBuilder_ExtractUnmatchedString()
        {
            var r = HierarchyQueryBuilder.BuildQuery($"with c={nameof(EcsTestData2)} some c={nameof(EcsTestData)} text c={nameof(EcsTestSharedComp)} in between");
            Assert.That(r.Filter, Is.EqualTo("with  some  text  in between"));
        }

        [Test]
        public void QueryBuilder_ReportStatusAndErrorComponentType()
        {
            var erroneousComponentType = nameof(EcsTestData) + Guid.NewGuid().ToString("N");
            var r = HierarchyQueryBuilder.BuildQuery($"c={erroneousComponentType}");

            Assert.That(r, Is.EqualTo(HierarchyQueryBuilder.Result.Invalid(erroneousComponentType)));
        }

        [Test]
        public void QueryBuilder_ExtractUnmatchedStringWhenNotMatchingAnything()
        {
            var r = HierarchyQueryBuilder.BuildQuery($"hola");
            Assert.That(r.EntityQueryDesc, Is.Null);
            Assert.That(r.Filter, Is.EqualTo("hola"));
        }

        static IEnumerable EnumerateAllCategories()
        {
            var values = Enum.GetValues(typeof(TypeManager.TypeCategory));
            foreach (var value in values)
            {
                yield return value;
            }
        }

        [Test]
        [TestCaseSource(nameof(EnumerateAllCategories))]
        public void QueryBuilder_EnsureAtLeastOneTypeExistsPerCategory(TypeManager.TypeCategory category)
        {
            Assert.That(TypeManager.AllTypes.Where(x => x.Category == category), Is.Not.Empty);
        }

        [Test]
        [TestCaseSource(nameof(EnumerateAllCategories))]
        public void QueryBuilder_EnsureAllCategoriesAreSupportedInQuery(TypeManager.TypeCategory category)
        {
            var w = new World("test");
            try
            {
                var t = TypeManager.AllTypes.First(x => x.Category == category && x.Type != null);
                var desc = new EntityQueryDesc();
                // Entity is implicitly included in EntityQuery, it should not be explicitly added.
                if (t.TypeIndex != TypeManager.GetTypeIndex<Entity>())
                {
                    desc.All = new ComponentType[] { t.Type };
                }

                using (var q = w.EntityManager.CreateEntityQuery(desc))
                {
                    Assert.DoesNotThrow(() =>
                    {
                        var entities = q.ToEntityArray(w.UpdateAllocator.ToAllocator);
                        entities.Dispose();
                    });
                }
            }
            finally
            {
                w.Dispose();
            }
        }
    }
}
