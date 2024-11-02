using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Search;
using UnityEngine.TestTools;
using static Unity.Entities.Editor.HierarchySearchProvider;

namespace Unity.Entities.Editor.Tests.Search
{
    struct SearchTestSharedComponent : ISharedComponentData
    {
        public int intValue;
        public float floatValue;
    }
    
    struct DummyComp : IComponentData
    {
        public int value;
    }

    public class EntitySearchTestCase
    {
        public string Query;
        public int ExpectedResultsCount;

        public EntitySearchTestCase(string query, int expectedResultsCount)
        {
            Query = query;
            ExpectedResultsCount = expectedResultsCount;
        }

        public override string ToString()
        {
            return Query;
        }
    }

    public class HierarchySearchProviderTests
    {
        public class QueryDescriptorTestCase
        {
            public string Query;
            internal HierarchyQueryDescriptor Expected;
            public QueryDescriptorTestCase(string query)
            {
                Query = query;
                Expected = new HierarchyQueryDescriptor(query);
            }

            public override string ToString()
            {
                return Query;
            }
        }

        static IEnumerable<QueryDescriptorTestCase> GeQueryDescriptorTestCase()
        {
            {
                yield return new QueryDescriptorTestCase("t:DummyComp");
            }
            {
                var tc = new QueryDescriptorTestCase("k=Entity");
                tc.Expected.kind = NodeKind.Entity;
                yield return tc;
            }
            {
                var tc = new QueryDescriptorTestCase("prefabtype=PrefabPart");
                tc.Expected.unusedFilters = "prefabtype=PrefabPart";
                yield return tc;
            }
            {
                var tc = new QueryDescriptorTestCase("w=\"Editor World\"");
                tc.Expected.world = "Editor World";
                yield return tc;
            }
            {
                var tc = new QueryDescriptorTestCase("ei=1");
                tc.Expected.entityIndex = 1;
                yield return tc;
            }
            {
                var tc = new QueryDescriptorTestCase("+FilterWriteGroup");
                yield return tc;
            }
            {
                var tc = new QueryDescriptorTestCase("ent aut");
                tc.Expected.searchValueTokenStr = "ent aut";
                tc.Expected.searchValueTokens = new[] { "ent", "aut" };
                yield return tc;
            }
            {
                var query = "#Unity.Entities.SceneSection.Section=0";
                var tc = new QueryDescriptorTestCase(query);
                var mod = new SharedComponentModifierDesc();
                mod.AddPropertyModifier(SearchUtils.GetSharedComponentPropertyDesc("Unity.Entities.SceneSection.Section"), "0");
                tc.Expected.sharedComponentModifiers = new[] { mod };
                yield return tc;

            }
            {
                var query = "#Unity.Entities.Editor.Tests.Search.SearchTestSharedComponent.floatValue=23.4";
                var tc = new QueryDescriptorTestCase(query);
                var mod = new SharedComponentModifierDesc();
                mod.AddPropertyModifier(SearchUtils.GetSharedComponentPropertyDesc("SearchTestSharedComponent.floatValue"), "23.4");
                tc.Expected.sharedComponentModifiers = new[] { mod };
                yield return tc;
            }
            {
                var query = "#SearchTestSharedComponent.intValue=42";
                var tc = new QueryDescriptorTestCase(query);
                var mod = new SharedComponentModifierDesc();
                mod.AddPropertyModifier(SearchUtils.GetSharedComponentPropertyDesc("SearchTestSharedComponent.intValue"), "42");
                tc.Expected.sharedComponentModifiers = new[] { mod };
                yield return tc;
            }
            {
                var query = "#SearchTestSharedComponent.intValue=42 #SearchTestSharedComponent.floatValue=23.4";
                var tc = new QueryDescriptorTestCase(query);
                var mod = new SharedComponentModifierDesc();
                mod.AddPropertyModifier(SearchUtils.GetSharedComponentPropertyDesc("SearchTestSharedComponent.intValue"), "42");
                mod.AddPropertyModifier(SearchUtils.GetSharedComponentPropertyDesc("SearchTestSharedComponent.floatValue"), "23.4");

                tc.Expected.sharedComponentModifiers = new[] { mod };
                yield return tc;
            }
        }

        [Test]
        public void ValidatePropertyDescShortNameVsFullNameSearch()
        {
            Assert.IsTrue(SearchUtils.TryGetSharedComponentPropertyDesc("Unity.Entities.Editor.Tests.Search.SearchTestSharedComponent.intValue", out var dummy));
            Assert.IsTrue(SearchUtils.TryGetSharedComponentPropertyDesc("SearchTestSharedComponent.intValue", out var dummy2));
        }

        [Test]
        public void CreateHierarchyQueryDescriptor([ValueSource(nameof(GeQueryDescriptorTestCase))] QueryDescriptorTestCase tc)
        {
            var desc = HierarchySearchProvider.CreateHierarchyQueryDescriptor(tc.Query);
            Assert.AreEqual(tc.Query, desc.originalQueryStr);
            Assert.AreEqual(tc.Expected.originalQueryStr, desc.originalQueryStr);

            Assert.AreEqual(tc.Expected.parsingErrors, desc.parsingErrors);
            Assert.AreEqual(tc.Expected.world, desc.world);
            Assert.AreEqual(tc.Expected.dataMode, desc.dataMode);
            Assert.AreEqual(tc.Expected.entityIndex, desc.entityIndex);
            Assert.AreEqual(tc.Expected.kind, desc.kind);
            Assert.AreEqual(tc.Expected.searchValueTokenStr, desc.searchValueTokenStr);
            Assert.AreEqual(tc.Expected.unusedFilters, desc.unusedFilters);
            CollectionAssert.AreEquivalent(tc.Expected.searchValueTokens, desc.searchValueTokens);

            Assert.AreEqual(tc.Expected.sharedComponentModifiers.Length, desc.sharedComponentModifiers.Length);
            for (var i = 0; i < tc.Expected.sharedComponentModifiers.Length; ++i)
            {
                var expected = tc.Expected.sharedComponentModifiers[i];
                var actual = desc.sharedComponentModifiers[i];
                Assert.AreEqual(expected.componentType, actual.componentType);
                Assert.AreEqual(expected.propertyDescs.Count, actual.propertyDescs.Count);
                CollectionAssert.AreEqual(expected.propertyValues, actual.propertyValues);
                for (var j = 0; j < tc.Expected.sharedComponentModifiers.Length; ++j)
                {
                    var expectedPropDesc = expected.propertyDescs[j];
                    var actualPropDesc = actual.propertyDescs[j];
                    Assert.AreEqual(expectedPropDesc, actualPropDesc);
                }
            }
        }

        [Test]
        public void CreateSharedComponentWithValue()
        {
            var modDesc = new SharedComponentModifierDesc();
            modDesc.AddPropertyModifier(SearchUtils.GetSharedComponentPropertyDesc("SearchTestSharedComponent.intValue"), "42");
            modDesc.AddPropertyModifier(SearchUtils.GetSharedComponentPropertyDesc("SearchTestSharedComponent.floatValue"), "23.4");
            Assert.IsTrue(modDesc.SetupSharedComponent(out var createdSharedComponent, out var errors));
            Assert.AreEqual("", errors);

            var actualSharedComponent = (SearchTestSharedComponent)createdSharedComponent;
            var expectedSharedComponent = new SearchTestSharedComponent { intValue = 42, floatValue = 23.4f };
            Assert.AreEqual(expectedSharedComponent.intValue, actualSharedComponent.intValue);
            Assert.AreEqual(expectedSharedComponent.floatValue, actualSharedComponent.floatValue);
        }

        public class SharedComponentQueryTestCase
        {
            public object sharedComponent;
            public string expectedQuery;

            public SharedComponentQueryTestCase(object sharedComponent, string expectedQuery)
            {
                this.sharedComponent = sharedComponent;
                this.expectedQuery = expectedQuery; 
            }

            public override string ToString()
            {
                return expectedQuery;
            }
        }

        static IEnumerable<SharedComponentQueryTestCase> GetSharedComponentQueryTestCase()
        {
            yield return new SharedComponentQueryTestCase(new SearchTestSharedComponent()
            {
                intValue = 42,
                floatValue = 23.4f                
            }, "#SearchTestSharedComponent.intValue=42 #SearchTestSharedComponent.floatValue=23.4");

            yield return new SharedComponentQueryTestCase(new SceneSection()
            {
                SceneGUID = new Hash128("8c91bc4eab64d2840bd8688ced1ace09"),
                Section = 42
            }, "#SceneSection.SceneGUID=8c91bc4eab64d2840bd8688ced1ace09 #SceneSection.Section=42");
        }

        [Test]
        public void CreateSharedComponentQueryFromComponent([ValueSource(nameof(GetSharedComponentQueryTestCase))] SharedComponentQueryTestCase tc)
        {
            var sharedComponent = tc.sharedComponent;
            var actualQuery = SearchUtils.CreateSharedComponentQuery((ISharedComponentData)sharedComponent);
            Assert.AreEqual(tc.expectedQuery, actualQuery);
        }

        enum SearchTestSharedComponentEnum
        {
            Value1,
            Value2,
            Value3
        }

        // SearchTestASFSC => SearchTestAllSupportedFieldsSharedComponent
        struct SearchTestASFSC : ISharedComponentData, IEquatable<SearchTestASFSC>
        {
            public int intValue;
            public float floatValue;
            public uint uintValue;
            public double doubleValue;
            public SearchTestSharedComponentEnum enumValue;
            public bool boolValue;
            public Hash128 hash128Value;
            public string stringValue;
            public Entity entityValue;

            public bool Equals(SearchTestASFSC other)
            {
                return intValue == other.intValue &&
                    floatValue == other.floatValue &&
                    uintValue == other.uintValue &&
                    doubleValue == other.doubleValue &&
                    enumValue == other.enumValue &&
                    boolValue == other.boolValue &&
                    hash128Value == other.hash128Value &&
                    stringValue == other.stringValue &&
                    entityValue == other.entityValue;
            }

            public override bool Equals(object obj)
            {
                if (obj is SearchTestASFSC s)
                    return Equals(s);
                return false;
            }

            public override int GetHashCode()
            {
                return stringValue.GetHashCode();
            }

            public static int GetSearchTestASFSCFieldCount()
            {
                return typeof(SearchTestASFSC).GetFields().Length;
            }

            public static string GetSearchTestASFSCFullQuery()
            {
                var queryParts = new[] {
                    "#SearchTestASFSC.intValue=42",
                    "#SearchTestASFSC.floatValue=23.4",
                    "#SearchTestASFSC.uintValue=22",
                    "#SearchTestASFSC.doubleValue=12.3",
                    "#SearchTestASFSC.enumValue=Value2",
                    "#SearchTestASFSC.boolValue=True",
                    "#SearchTestASFSC.hash128Value=8c91bc4eab64d2840bd8688ced1ace09",
                    "#SearchTestASFSC.stringValue=\"hello world\"",
                    "#SearchTestASFSC.entityValue=12-34",
                };
                return string.Join(" ", queryParts);
            }

            public static SearchTestASFSC CreatePopulatedSearchTestASFSC()
            {
                return new SearchTestASFSC()
                {
                    intValue = 42,
                    floatValue = 23.4f,
                    uintValue = 22,
                    doubleValue = 12.3,
                    enumValue = SearchTestSharedComponentEnum.Value2,
                    boolValue = true,
                    hash128Value = new Hash128("8c91bc4eab64d2840bd8688ced1ace09"),
                    stringValue = "hello world",
                    entityValue = new Entity()
                    {
                        Index = 12,
                        Version = 34
                    }
                };
            }
        }

        [Test]
        public void CreateSharedComponentDesc()
        {
            var typeInfo = TypeManager.GetTypeInfo<SearchTestASFSC>();
            var componentDesc = new SharedComponentDesc(typeInfo);
            Assert.AreEqual(SearchTestASFSC.GetSearchTestASFSCFieldCount(), componentDesc.properties.Count);
        }
        
        [Test]
        public void CreateSharedComponentFromQuery()
        {
            var query = SearchTestASFSC.GetSearchTestASFSCFullQuery();

            var queryDesc = HierarchySearchProvider.CreateHierarchyQueryDescriptor(query);
            Assert.NotNull(queryDesc.sharedComponentModifiers);
            Assert.AreEqual(1, queryDesc.sharedComponentModifiers.Length);
            Assert.AreEqual(SearchTestASFSC.GetSearchTestASFSCFieldCount(), queryDesc.sharedComponentModifiers[0].propertyDescs.Count);
            Assert.IsTrue(queryDesc.sharedComponentModifiers[0].SetupSharedComponent(out var actualComponentObj, out var errors));
            Assert.AreEqual("", errors);

            var actualComponent = (SearchTestASFSC)actualComponentObj;
            var expectedComponent = SearchTestASFSC.CreatePopulatedSearchTestASFSC();

            Assert.AreEqual(expectedComponent.intValue, actualComponent.intValue);
            Assert.AreEqual(expectedComponent.floatValue, actualComponent.floatValue);
            Assert.AreEqual(expectedComponent.uintValue, actualComponent.uintValue);
            Assert.AreEqual(expectedComponent.doubleValue, actualComponent.doubleValue);
            Assert.AreEqual(expectedComponent.boolValue, actualComponent.boolValue);
            Assert.AreEqual(expectedComponent.enumValue, actualComponent.enumValue);
            Assert.AreEqual(expectedComponent.hash128Value, actualComponent.hash128Value);
        }

        [Test]
        public void CreateQueryFromSharedComponent()
        {
            var srcComponent = SearchTestASFSC.CreatePopulatedSearchTestASFSC();
            var expectedQuery = SearchTestASFSC.GetSearchTestASFSCFullQuery();
            var actualQuery = SearchUtils.CreateSharedComponentQuery(srcComponent);
            Assert.AreEqual(expectedQuery, actualQuery);
        }        
    }

    public class SearchEntitiesTests : QuickSearchTests
    {
        static World m_PreviousWorld;
        static World m_World;
        static EntityManager m_Manager;
        static string s_SearchWorld = "SearchTestWorld";

        private static void InitWorld()
        {
            m_PreviousWorld = World.DefaultGameObjectInjectionWorld;
            m_World = World.DefaultGameObjectInjectionWorld = new World(s_SearchWorld);
            m_World.UpdateAllocatorEnableBlockFree = true;
            m_Manager = m_World.EntityManager;
        }

        private static void DisposeWorld()
        {
            if (m_World != null && m_World.IsCreated)
            {
                m_World.Dispose();
                m_World = null;

                World.DefaultGameObjectInjectionWorld = m_PreviousWorld;
                m_PreviousWorld = null;
                m_Manager = default;
            }
        }

        private static void CreateEntities()
        {
            {
                var entity = m_Manager.CreateEntity(typeof(SearchTestSharedComponent));
                m_Manager.SetSharedComponentManaged(entity, new SearchTestSharedComponent { intValue = 1 });

                entity = m_Manager.CreateEntity(typeof(SearchTestSharedComponent), typeof(DummyComp));
                m_Manager.SetSharedComponentManaged(entity, new SearchTestSharedComponent { intValue = 1 });
                m_Manager.AddComponentData(entity, new DummyComp { value = 1 });
            }

            {
                var entity = m_Manager.CreateEntity(typeof(SearchTestSharedComponent));
                m_Manager.SetSharedComponentManaged(entity, new SearchTestSharedComponent { floatValue = 3.3f });
            }

            {
                var entity = m_Manager.CreateEntity(typeof(SearchTestSharedComponent));
                m_Manager.SetSharedComponentManaged(entity, new SearchTestSharedComponent { floatValue = 42f, intValue = 42 });
            }
        }   

        [OneTimeSetUp]
        public void SceneEntitiesSetup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            InitWorld();
            CreateEntities();
        }

        [OneTimeTearDown]
        public void SceneEntitiesTearDown()
        {
            DisposeWorld();
        }

        static IEnumerable<EntitySearchTestCase> GetHierarchySearchProviderTestCase()
        {
            yield return new EntitySearchTestCase($"w={s_SearchWorld} all=SceneSection", 0);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} all=DummyComp", 1);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} all=SearchTestSharedComponent", 4);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} all=SearchTestSharedComponent all=DummyComp", 1);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} any=SearchTestSharedComponent any=DummyComp", 4);

            yield return new EntitySearchTestCase($"w={s_SearchWorld} all=DummyComp #Unity.Entities.Editor.Tests.Search.SearchTestSharedComponent.intValue=1", 1);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} #Unity.Entities.Editor.Tests.Search.SearchTestSharedComponent.intValue=1", 2);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} #Unity.Entities.Editor.Tests.Search.SearchTestSharedComponent.floatValue=3.3", 1);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} #Unity.Entities.Editor.Tests.Search.SearchTestSharedComponent.intValue=42", 0);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} #Unity.Entities.Editor.Tests.Search.SearchTestSharedComponent.floatValue=42", 0);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} #Unity.Entities.Editor.Tests.Search.SearchTestSharedComponent.floatValue=42 #Unity.Entities.Editor.Tests.Search.SearchTestSharedComponent.intValue=42", 1);            
            yield return new EntitySearchTestCase($"w={s_SearchWorld} all=SceneSection #Unity.Entities.Editor.Tests.Search.SearchTestSharedComponent.floatValue=42 #Unity.Entities.Editor.Tests.SearchTestSharedComponent.intValue=42", 0);


            yield return new EntitySearchTestCase($"w={s_SearchWorld} all=DummyComp #SearchTestSharedComponent.intValue=1", 1);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} #SearchTestSharedComponent.intValue=1", 2);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} #SearchTestSharedComponent.floatValue=3.3", 1);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} #SearchTestSharedComponent.intValue=42", 0);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} #SearchTestSharedComponent.floatValue=42", 0);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} #SearchTestSharedComponent.floatValue=42 #Unity.Entities.Editor.Tests.Search.SearchTestSharedComponent.intValue=42", 1);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} #SearchTestSharedComponent.floatValue=0 #SearchTestSharedComponent.intValue=42", 0);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} #SearchTestSharedComponent.floatValue=42 #SearchTestSharedComponent.intValue=0", 0);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} all=SceneSection #SearchTestSharedComponent.floatValue=42 #Unity.Entities.Editor.Tests.Search.SearchTestSharedComponent.intValue=42", 0);

            yield return new EntitySearchTestCase($"w={s_SearchWorld} #SearchTestSharedComponent.intValue=1 SceneSection.Section=0", 0);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} #SharedComponentData.FloatValue=42 #SearchTestSharedComponent.intValue=0", 0);
        }

        [Test]
        public void SetSharedComponentFilter()
        {
            // Note: there is no way to extract the SharedComponent value once it is set into the EntityQuer.
            // But this tests that the reflection used in SetSharedComponentFilter works fine for multiple types.
            var query = m_Manager.CreateEntityQuery(typeof(SearchTestSharedComponent), typeof(EntityContainerTest.SharedComponentData));
            {
                var result = HierarchySearchProvider.SetSharedComponentFilter(ref query, new SearchTestSharedComponent { intValue = 42 });
                Assert.IsTrue(result);
            }

            {
                var result = HierarchySearchProvider.SetSharedComponentFilter(ref query, new EntityContainerTest.SharedComponentData { FloatValue = 24.5f });
                Assert.IsTrue(result);
            }
        }

        [UnityTest]
        public IEnumerator SearchEntities([ValueSource(nameof(GetHierarchySearchProviderTestCase))] EntitySearchTestCase tc)
        {
            var desc = HierarchySearchProvider.CreateHierarchyQueryDescriptor(tc.Query);
            Assert.IsTrue(string.IsNullOrEmpty(desc.parsingErrors), $"Query parsing errors: {desc.parsingErrors}");

            var results = new List<SearchItem>();
            yield return FetchItems(HierarchySearchProvider.type, tc.Query, results);
            Assert.AreEqual(tc.ExpectedResultsCount, results.Count);
        }

        [MenuItem("Tests/Generate Search Test Data")]
        static void GenerateSearchTestsData()
        {
            DisposeWorld();
            InitWorld();
            CreateEntities();
        }
    }
}
