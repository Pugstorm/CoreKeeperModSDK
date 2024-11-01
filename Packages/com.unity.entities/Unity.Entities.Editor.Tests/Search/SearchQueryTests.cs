using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine.TestTools;
using Unity.Entities;

namespace Unity.Entities.Editor.Tests.Search
{
    [System.Serializable]
    public struct SearchBoid : ISharedComponentData
    {
        public float CellRadius;
        public float SeparationWeight;
        public float AlignmentWeight;
        public float TargetWeight;
        public float ObstacleAversionDistance;
        public float MoveSpeed;
    }

    [System.Serializable]
    public struct SearchBoidTarget : ISharedComponentData
    {
        public float CellRadius;
        public float SeparationWeight;
        public float AlignmentWeight;
        public float TargetWeight;
        public float ObstacleAversionDistance;
        public float MoveSpeed;
    }

    [System.Serializable]
    public struct SearchSimulate : ISharedComponentData
    {
        public float CellRadius;
        public float SeparationWeight;
        public float AlignmentWeight;
        public float TargetWeight;
        public float ObstacleAversionDistance;
        public float MoveSpeed;
    }
}

namespace Unity.Entities.Editor.Tests.Search
{
    public class QueryTestCase
    {
        public string query;
        public string processedQuery;
        public string filter;
        public string op;
        public object value;

        internal HierarchySearchProvider.HierarchyQueryDescriptor queryDescriptor;

        public QueryTestCase(string query)
        {
            this.query = query;
        }

        public override string ToString()
        {
            return query;
        }
    }

    public class SearchQueryTests
    {
        static ComponentType[] GetComponentTypes(IEnumerable<string> strs)
        {
            var dummy = "";
            return HierarchySearchProvider.GetComponentTypes(strs, ref dummy);
        }

        public static IEnumerable<QueryTestCase> GetConvertToHierarchySearchTests()
        {
            

            var s = "c=SearchBoid";
            yield return new QueryTestCase(s)
            {
                queryDescriptor = new HierarchySearchProvider.HierarchyQueryDescriptor(s)
                {
                    entityQuery = new EntityQueryDesc()
                    {
                        All = GetComponentTypes(new[] { "Searchboid" })
                    }
                }
            };

            s = "c=SearchBoid c=SearchBoidTarget -c=SearchSimulate";
            yield return new QueryTestCase(s)
            {
                queryDescriptor = new HierarchySearchProvider.HierarchyQueryDescriptor(s)
                {
                    entityQuery = new EntityQueryDesc()
                    {
                        All = GetComponentTypes(new[] { "Searchboid", "SearchboidTarget" }),
                        None = GetComponentTypes(new[] { "SearchSimulate" }),
                    }
                }
            };

            s = "all=SearchBoid any=SearchBoidTarget none=SearchSimulate";
            yield return new QueryTestCase(s)
            {
                queryDescriptor = new HierarchySearchProvider.HierarchyQueryDescriptor(s)
                {
                    entityQuery = new EntityQueryDesc()
                    {
                        All = GetComponentTypes(new[] { "Searchboid" }),
                        Any = GetComponentTypes(new[] { "SearchBoidTarget" }),
                        None = GetComponentTypes(new[] { "SearchSimulate" })
                    }
                }
            };

            s = "this all=SearchBoid is any=SearchBoidTarget text none=SearchSimulate values";
            yield return new QueryTestCase(s)
            {
                queryDescriptor = new HierarchySearchProvider.HierarchyQueryDescriptor(s)
                {
                    entityQuery = new EntityQueryDesc()
                    {
                        All = GetComponentTypes(new[] { "Searchboid" }),
                        Any = GetComponentTypes(new[] { "SearchBoidTarget" }),
                        None = GetComponentTypes(new[] { "SearchSimulate" })
                    },
                    searchValueTokens = new[] { "this", "is", "text", "values" },
                    searchValueTokenStr = "this is text values"
                }
            };

            s = "this is text k=GameObject";
            yield return new QueryTestCase(s)
            {
                queryDescriptor = new HierarchySearchProvider.HierarchyQueryDescriptor(s)
                {
                    entityQuery = null,
                    searchValueTokens = new[] { "this", "is", "text" },
                    searchValueTokenStr = "this is text",
                    kind = NodeKind.GameObject
                }
            };

            s = "ei=12 all=SearchBoid";
            yield return new QueryTestCase(s)
            {
                queryDescriptor = new HierarchySearchProvider.HierarchyQueryDescriptor(s)
                {
                    entityQuery = new EntityQueryDesc()
                    {
                        All = GetComponentTypes(new[] { "Searchboid" })
                    },
                    entityIndex = 12
                }

            };

            s = "k=GameObject  disabled:true all=SearchBoid depth=12";
            yield return new QueryTestCase(s)
            {
                queryDescriptor = new HierarchySearchProvider.HierarchyQueryDescriptor(s)
                {
                    entityQuery = new EntityQueryDesc()
                    {
                        All = GetComponentTypes(new[] { "Searchboid" })
                    },
                    kind = NodeKind.GameObject,
                    unusedFilters = "disabled:true depth=12"
                }
            };

            s = "w=editor sea disabled:true none=SearchBoid";
            yield return new QueryTestCase(s)
            {
                queryDescriptor = new HierarchySearchProvider.HierarchyQueryDescriptor(s)
                {
                    entityQuery = new EntityQueryDesc()
                    {
                        None = GetComponentTypes(new[] { "Searchboid" })
                    },
                    world = "editor",
                    searchValueTokens = new[] { "sea" },
                    searchValueTokenStr = "sea",
                    unusedFilters = "disabled:true"
                }
            };

            s = "w=editor sea disabled:true +includeprefab none=SearchBoid";
            yield return new QueryTestCase(s)
            {
                queryDescriptor = new HierarchySearchProvider.HierarchyQueryDescriptor(s)
                {
                    entityQuery = new EntityQueryDesc()
                    {
                        None = GetComponentTypes(new[] { "Searchboid" }),
                        Options = EntityQueryOptions.IncludePrefab
                    },
                    world = "editor",
                    searchValueTokens = new[] { "sea" },
                    searchValueTokenStr = "sea",
                    unusedFilters = "disabled:true"
                }
            };

            s = "w=editor sea disabled:true +includeprefab +IncludeDisabled none=SearchBoid";
            yield return new QueryTestCase(s)
            {
                queryDescriptor = new HierarchySearchProvider.HierarchyQueryDescriptor(s)
                {
                    entityQuery = new EntityQueryDesc()
                    {
                        None = GetComponentTypes(new[] { "Searchboid" }),
                        Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities
                    },
                    world = "editor",
                    searchValueTokens = new[] { "sea" },
                    searchValueTokenStr = "sea",
                    unusedFilters = "disabled:true"
                }
            };

            s = "none=Droid";
            yield return new QueryTestCase(s)
            {
                queryDescriptor = new HierarchySearchProvider.HierarchyQueryDescriptor(s)
                {
                    entityQuery = null,
                    entityQueryResult = HierarchyQueryBuilder.Result.Invalid("Unknown components"),
                }
            };
        }

        [Test]
        public void ConvertToHierarchySearch([ValueSource(nameof(GetConvertToHierarchySearchTests))] QueryTestCase test)
        {
            var entityQueryDescriptor = HierarchySearchProvider.CreateHierarchyQueryDescriptor(test.query);
            Assert.NotNull(test.queryDescriptor);
            Assert.IsTrue(entityQueryDescriptor.query.valid, "Query is invalid");

            Assert.AreEqual(test.queryDescriptor.kind, entityQueryDescriptor.kind, "Kind is different");
            Assert.AreEqual(test.queryDescriptor.entityIndex, entityQueryDescriptor.entityIndex, "entityIndex is different");
            Assert.AreEqual(test.queryDescriptor.world, entityQueryDescriptor.world, "World is different");

            Assert.AreEqual(test.queryDescriptor.originalQueryStr, entityQueryDescriptor.originalQueryStr, "OriginalQuery is different");
            Assert.AreEqual(test.queryDescriptor.processedQueryStr, entityQueryDescriptor.processedQueryStr, "processedQueryStr is different");
            Assert.AreEqual(test.queryDescriptor.unusedFilters, entityQueryDescriptor.unusedFilters, "UnusedFilters is different");

            CollectionAssert.AreEqual(test.queryDescriptor.searchValueTokens, entityQueryDescriptor.searchValueTokens, "searchValueTokens are different");
            Assert.AreEqual(test.queryDescriptor.searchValueTokenStr, entityQueryDescriptor.searchValueTokenStr, "searchValueTokenStr is different");

            Assert.IsTrue((test.queryDescriptor.entityQuery == null && entityQueryDescriptor.entityQuery == null) || (test.queryDescriptor.entityQuery != null && entityQueryDescriptor.entityQuery != null), "EntityQuery doesn't match");

            if (test.queryDescriptor.entityQueryResult.Filter != null)
                Assert.AreEqual(test.queryDescriptor.entityQueryResult.IsValid, entityQueryDescriptor.entityQueryResult.IsValid, "entityQueryResult is different");

            if (test.queryDescriptor.entityQuery != null)
            {
                CollectionAssert.AreEqual(test.queryDescriptor.entityQuery.All, entityQueryDescriptor.entityQuery.All, "entityQuery.All is different");
                CollectionAssert.AreEqual(test.queryDescriptor.entityQuery.None, entityQueryDescriptor.entityQuery.None, "entityQuery.None is different");
                CollectionAssert.AreEqual(test.queryDescriptor.entityQuery.Any, entityQueryDescriptor.entityQuery.Any, "entityQuery.Any is different");
            }
        }

        public static IEnumerable<QueryTestCase> GetPreprocessQueryTests()
        {
            yield return new QueryTestCase("c=Boid") { processedQuery = "all=Boid" };
            yield return new QueryTestCase("-c=Boid") { processedQuery = "none=Boid" };
            yield return new QueryTestCase("pow c=Acceleration -c=Boid") { processedQuery = "pow all=Acceleration none=Boid" };
            yield return new QueryTestCase("pow all=Acceleration none=Boid") { processedQuery = "pow all=Acceleration none=Boid" };
            yield return new QueryTestCase("pow c=Acceleration any=Ping -c=Boid") { processedQuery = "pow all=Acceleration any=Ping none=Boid" };

            yield return new QueryTestCase("c=Acceleration pow -c=Boid") { processedQuery = "all=Acceleration pow none=Boid" };
            yield return new QueryTestCase("all=Acceleration pow none=Boid") { processedQuery = "all=Acceleration pow none=Boid" };
            yield return new QueryTestCase("c=Acceleration pow any=Ping -c=Boid") { processedQuery = "all=Acceleration pow any=Ping none=Boid" };

            yield return new QueryTestCase("c=Acceleration -c=Boid pow") { processedQuery = "all=Acceleration none=Boid pow" };
            yield return new QueryTestCase("all=Acceleration none=Boid pow") { processedQuery = "all=Acceleration none=Boid pow" };
            yield return new QueryTestCase("c=Acceleration any=Ping -c=Boid pow") { processedQuery = "all=Acceleration any=Ping none=Boid pow" };
        }

        [Test]
        public void PreprocessQuery([ValueSource(nameof(GetPreprocessQueryTests))] QueryTestCase test)
        {
            var preprocessQuery = HierarchySearchProvider.PreprocessQuery(test.query);
            Assert.AreEqual(test.processedQuery, preprocessQuery);
        }

        public static IEnumerable<QueryTestCase> GetReplaceFilterInQuery()
        {
            yield return new QueryTestCase("p:") { filter = "ei", op = "=", value = 12, processedQuery = "p:ei=12" };
            yield return new QueryTestCase("p:ei=233") { filter = "ei", op = "=", value = 12, processedQuery = "p:ei=12" };
            yield return new QueryTestCase("p:ei=12") { filter = "ei", op = "=", value = 12, processedQuery = "p:ei=12" };

            yield return new QueryTestCase("p:tweak") { filter = "ei", op = "=", value = 12, processedQuery = "p:tweak ei=12" };
            yield return new QueryTestCase("p:tweak ei=233") { filter = "ei", op = "=", value = 12, processedQuery = "p:tweak ei=12" };
            yield return new QueryTestCase("p:ei=233 tweak") { filter = "ei", op = "=", value = 12, processedQuery = "p:ei=12 tweak" };

            yield return new QueryTestCase("p:tweak fo=12") { filter = "ei", op = "=", value = 12, processedQuery = "p:tweak fo=12 ei=12" };
            yield return new QueryTestCase("p:tweak ei=233 fo=12") { filter = "ei", op = "=", value = 12, processedQuery = "p:tweak ei=12 fo=12" };
            yield return new QueryTestCase("p:tweak fo=12 ei=233") { filter = "ei", op = "=", value = 12, processedQuery = "p:tweak fo=12 ei=12" };


            yield return new QueryTestCase("") { filter = "ei", op = "=", value = 12, processedQuery = "ei=12" };
            yield return new QueryTestCase("ei=233") { filter = "ei", op = "=", value = 12, processedQuery = "ei=12" };
            yield return new QueryTestCase("ei=12") { filter = "ei", op = "=", value = 12, processedQuery = "ei=12" };

            yield return new QueryTestCase("tweak") { filter = "ei", op = "=", value = 12, processedQuery = "tweak ei=12" };
            yield return new QueryTestCase("tweak ei=233") { filter = "ei", op = "=", value = 12, processedQuery = "tweak ei=12" };
            yield return new QueryTestCase("ei=233 tweak") { filter = "ei", op = "=", value = 12, processedQuery = "ei=12 tweak" };

            yield return new QueryTestCase("tweak fo=12") { filter = "ei", op = "=", value = 12, processedQuery = "tweak fo=12 ei=12" };
            yield return new QueryTestCase("tweak ei=233 fo=12") { filter = "ei", op = "=", value = 12, processedQuery = "tweak ei=12 fo=12" };
            yield return new QueryTestCase("tweak fo=12 ei=233") { filter = "ei", op = "=", value = 12, processedQuery = "tweak fo=12 ei=12" };
        }

        [Test]
        public void ReplaceFilterInQuery([ValueSource(nameof(GetReplaceFilterInQuery))] QueryTestCase test)
        {
            var newQuery = SearchUtils.AddOrReplaceFilterInQuery(test.query, test.filter, test.op, test.value);
            Assert.AreEqual(test.processedQuery, newQuery);
        }
    }

    public class ComponentsSearchProviderTests : QuickSearchTests
    {
        
    }
}
