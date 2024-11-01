using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Properties;
using Unity.Entities.UI;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Editor.Tests
{
    [TestFixture]
    sealed partial class SearchElementTests
    {
        EditorWindow m_Window;

        SearchElement m_SearchElement;
        PropertyElement m_PropertyElement;

        [OneTimeSetUp]
        public void GlobalSetUp()
        {
            m_Window = EditorWindow.CreateInstance<EditorWindow>();
            m_Window.Show();
        }

        [OneTimeTearDown]
        public void GlobalTeardown()
        {
            m_Window.Close();
        }

        [SetUp]
        public void SetUp()
        {
            m_SearchElement = new SearchElement {SearchDelay = 0};
            m_PropertyElement = new PropertyElement();
            m_Window.rootVisualElement.Add(m_SearchElement);
            m_Window.rootVisualElement.Add(m_PropertyElement);
        }

        [TearDown]
        public void Teardown()
        {
            m_Window.rootVisualElement.Clear();
        }

        class TestDataContainer
        {
            public static readonly PropertyPath ValidSourceDataPath = new PropertyPath(nameof(ValidSourceData));
            public static readonly PropertyPath ValidDestinationDataPath = new PropertyPath(nameof(ValidDestinationData));
            public static readonly PropertyPath ReadOnlyDestinationDataPath = new PropertyPath(nameof(ReadOnlyDestinationData));
            public static readonly PropertyPath NonCollectionSourceDataPath = new PropertyPath(nameof(NonCollectionSourceData));
            public static readonly PropertyPath NonCollectionDestinationDataPath = new PropertyPath(nameof(NonCollectionDestinationData));

#pragma warning disable 649
            [CreateProperty] public TestData[] ValidSourceData;
            [CreateProperty] public List<TestData> ValidDestinationData;
            [CreateProperty] public TestData[] ReadOnlyDestinationData;
            [CreateProperty] public TestData NonCollectionSourceData;
            [CreateProperty] public TestData NonCollectionDestinationData;
#pragma warning restore 649
        }

        [Test]
        public void Search_WithGlobalStringComparisonOption_IsPropagatedToExistingBackends()
        {
            var backend = CreateSearchBackend<TestData>();
            backend.GlobalStringComparison = StringComparison.CurrentCulture;
            m_SearchElement.RegisterSearchBackend(backend);

            m_SearchElement.GlobalStringComparison = StringComparison.Ordinal;

            Assert.That(backend.GlobalStringComparison, Is.EqualTo(StringComparison.Ordinal), "Existing backends GlobalStringComparison should be overwritten with the one specified in the SearchElement");

            var searchEngine = m_SearchElement.GetSearchEngine();
            Assert.That(searchEngine.GlobalStringComparison, Is.EqualTo(StringComparison.Ordinal));
            Assert.That(searchEngine.GetRegisteredBackends().Single().GlobalStringComparison, Is.EqualTo(StringComparison.Ordinal));
        }

        [Test]
        public void Search_WithGlobalStringComparisonOption_IsPropagatedToAddedBackends()
        {
            m_SearchElement.GlobalStringComparison = StringComparison.Ordinal;
            var backend = CreateSearchBackend<TestData>();
            backend.GlobalStringComparison = StringComparison.CurrentCulture;

            m_SearchElement.RegisterSearchBackend(backend);
            Assert.That(backend.GlobalStringComparison, Is.EqualTo(StringComparison.Ordinal), "Registering a backend should overwrite its GlobalStringComparison with the one specified in the SearchElement");

            var searchEngine = m_SearchElement.GetSearchEngine();
            Assert.That(searchEngine.GlobalStringComparison, Is.EqualTo(StringComparison.Ordinal));
            Assert.That(searchEngine.GetRegisteredBackends().Single().GlobalStringComparison, Is.EqualTo(StringComparison.Ordinal));
        }

        [Test]
        public void Search_WithGlobalStringComparisonOption_IsPropagatedToDefaultBackend()
        {
            m_SearchElement.GlobalStringComparison = StringComparison.Ordinal;

            var searchEngine = m_SearchElement.GetSearchEngine();
            Assert.That(searchEngine.GlobalStringComparison, Is.EqualTo(StringComparison.Ordinal));
            Assert.That(searchEngine.GetRegisteredBackends(), Is.Empty);

            // Registering a default backend
            m_SearchElement.RegisterSearchQueryHandler<TestData>(s => s.Apply(Array.Empty<TestData>()));
            m_SearchElement.value = "blop";

            Assert.That(searchEngine.GetRegisteredBackends().Single().GlobalStringComparison, Is.EqualTo(StringComparison.Ordinal));
        }

        [Test]
        public void Search_WithGlobalStringComparisonOption_IsUsedByBackend()
        {
            var testdata = new[]
            {
                new TestData { Name = "value" },
                new TestData { Name = "VALUE" },
                new TestData { Name = "vAlUe" },
            };

            var filteredData = Array.Empty<TestData>();

            m_SearchElement.GlobalStringComparison = StringComparison.CurrentCulture;
            m_SearchElement.AddSearchDataProperty(new PropertyPath("Name"));
            m_SearchElement.RegisterSearchQueryHandler<TestData>(search => { filteredData = search.Apply(testdata).ToArray(); });
            m_SearchElement.value = "value";

            Assert.That(filteredData, Is.EquivalentTo(new[] { testdata[0] }));

            m_SearchElement.GlobalStringComparison = StringComparison.OrdinalIgnoreCase;

            m_SearchElement.value = "VALUE";

            Assert.That(filteredData, Is.EquivalentTo(testdata));
        }

        [Test]
        public void Search_WithNoSearchDataProperties_DoesNotReturnAnyResults()
        {
            var originalData = Generate(100);
            var filteredData = default(TestData[]);

            m_SearchElement.RegisterSearchQueryHandler<TestData>(search => { filteredData = search.Apply(originalData).ToArray(); });
            m_SearchElement.value = "Mesh";

            Assert.That(filteredData.Length, Is.EqualTo(0));
        }

        [Test]
        [TestCase("Mesh", "Name", 25)]
        [TestCase("1", "Id",19)]
        [TestCase("nested0", "Nested.Value",1)]
        public void Search_WithSearchDataProperties_ReturnsFilteredResults(string searchString, string searchDataProperties, int expectedCount)
        {
            var originalData = Generate(100);
            var filteredData = default(TestData[]);

            m_SearchElement.RegisterSearchQueryHandler<TestData>(search => { filteredData = search.Apply(originalData).ToArray(); });

            foreach (var path in searchDataProperties.Split(' '))
                m_SearchElement.AddSearchDataProperty(new PropertyPath(path));

            m_SearchElement.value = searchString;

            Assert.That(filteredData.Length, Is.EqualTo(expectedCount));
        }

        [Test]
        [TestCase("id<10", "id:Id", 10)]
        [TestCase("x<50", "x:Position.x", 5)]
        public void Search_WithSearchFilterProperties_ReturnsFilteredResults(string searchString, string searchFilterProperties, int expectedCount)
        {
            var originalData = Generate(100);
            var filteredData = default(TestData[]);

            foreach (var searchFilter in searchFilterProperties.Split(' '))
            {
                var tokenAndPath = searchFilter.Split(':');

                var token = tokenAndPath[0];
                var path = tokenAndPath[1];

                m_SearchElement.AddSearchFilterProperty(token, new PropertyPath(path));
            }

            m_SearchElement.RegisterSearchQueryHandler<TestData>(search => { filteredData = search.Apply(originalData).ToArray(); });
            m_SearchElement.value = searchString;

            Assert.That(filteredData.Length, Is.EqualTo(expectedCount));
        }

        [Test]
        public void RegisterHandler_WithInvalidSourceDataPath_Throw()
        {
            m_PropertyElement.SetTarget(new TestDataContainer
            {
                ValidSourceData = Generate(100),
                ValidDestinationData = new List<TestData>()
            });

            Assert.Throws<InvalidBindingException>(() =>
            {
                m_SearchElement.RegisterSearchQueryHandler(m_PropertyElement, new PropertyPath("SomeUnknownPath"), TestDataContainer.ValidDestinationDataPath);
            });
        }

        [Test]
        public void RegisterHandler_WithInvalidDestinationDataPath_Throws()
        {
            m_PropertyElement.SetTarget(new TestDataContainer
            {
                ValidSourceData = Generate(100),
                ValidDestinationData = new List<TestData>()
            });

            Assert.Throws<InvalidBindingException>(() =>
            {
                m_SearchElement.RegisterSearchQueryHandler(m_PropertyElement, TestDataContainer.ValidSourceDataPath, new PropertyPath("SomeUnknownPath"));
            });
        }

        [Test]
        public void RegisterHandler_WithInvalidSourceDataType_Throws()
        {
            m_PropertyElement.SetTarget(new TestDataContainer
            {
                ValidSourceData = Generate(100),
                ValidDestinationData = new List<TestData>()
            });

            Assert.Throws<InvalidBindingException>(() =>
            {
                m_SearchElement.RegisterSearchQueryHandler(m_PropertyElement, TestDataContainer.NonCollectionSourceDataPath, TestDataContainer.ValidDestinationDataPath);
            });
        }

        [Test]
        public void RegisterHandler_WithInvalidDestinationDataType_Throws()
        {
            m_PropertyElement.SetTarget(new TestDataContainer
            {
                ValidSourceData = Generate(100),
                ValidDestinationData = new List<TestData>()
            });

            Assert.Throws<InvalidBindingException>(() =>
            {
                m_SearchElement.RegisterSearchQueryHandler(m_PropertyElement, TestDataContainer.ValidSourceDataPath, TestDataContainer.NonCollectionDestinationDataPath);
            });
        }

        [Test]
        public void RegisterHandler_WithReadOnlyDestinationDataType_Throws()
        {
            m_PropertyElement.SetTarget(new TestDataContainer());

            Assert.Throws<InvalidBindingException>(() =>
            {
                m_SearchElement.RegisterSearchQueryHandler(m_PropertyElement, TestDataContainer.ValidSourceDataPath, TestDataContainer.ReadOnlyDestinationDataPath);
            });
        }

        [Test]
        public void RegisterHandler_WithNullSourceData_Throws()
        {
            m_PropertyElement.SetTarget(new TestDataContainer
            {
                ValidSourceData = null,
                ValidDestinationData = new List<TestData>()
            });

            Assert.Throws<InvalidBindingException>(() =>
            {
                m_SearchElement.RegisterSearchQueryHandler(m_PropertyElement, TestDataContainer.ValidSourceDataPath, TestDataContainer.ValidDestinationDataPath);
            });
        }

        [Test]
        public void RegisterHandler_WithNullDestinationData_Throws()
        {
            m_PropertyElement.SetTarget(new TestDataContainer
            {
                ValidSourceData = Generate(100),
                ValidDestinationData = null
            });

            Assert.Throws<InvalidBindingException>(() =>
            {
                m_SearchElement.RegisterSearchQueryHandler(m_PropertyElement, TestDataContainer.ValidSourceDataPath, TestDataContainer.ValidDestinationDataPath);
            });
        }

        [Test]
        public void Search_WithValidBindings_ResultsAreWrittenToDestinationData()
        {
            var container = new TestDataContainer
            {
                ValidSourceData = Generate(100),
                ValidDestinationData = new List<TestData>()
            };

            m_PropertyElement.SetTarget(container);
            m_SearchElement.RegisterSearchQueryHandler(m_PropertyElement, TestDataContainer.ValidSourceDataPath, TestDataContainer.ValidDestinationDataPath);
            m_SearchElement.AddSearchDataProperty(new PropertyPath("Name"));

            Assert.That(container.ValidDestinationData.Count, Is.EqualTo(0));

            m_SearchElement.Search("");
            Assert.That(container.ValidDestinationData.Count, Is.EqualTo(container.ValidSourceData.Length));

            m_SearchElement.Search("Mesh");
            Assert.That(container.ValidDestinationData.Count, Is.EqualTo(25));
        }

        [Test]
        public void Search_WithCollectionSearchData_ResultsAreWrittenToDestinationData()
        {
            var container = new TestDataContainer
            {
                ValidSourceData = Generate(100),
                ValidDestinationData = new List<TestData>()
            };

            container.ValidSourceData[0].StringArray = new[]
            {
                "one", "two", "three", "four"
            };

            container.ValidSourceData[1].StringArray = new[]
            {
                "two", "three", "four"
            };

            container.ValidSourceData[2].StringArray = new[]
            {
                "three", "four"
            };

            container.ValidSourceData[3].StringArray = new[]
            {
                "four"
            };

            m_PropertyElement.SetTarget(container);
            m_SearchElement.RegisterSearchQueryHandler(m_PropertyElement, TestDataContainer.ValidSourceDataPath, TestDataContainer.ValidDestinationDataPath);
            m_SearchElement.AddSearchDataProperty(new PropertyPath("StringArray"));

            Assert.That(container.ValidDestinationData.Count, Is.EqualTo(0));

            m_SearchElement.Search("two");
            Assert.That(container.ValidDestinationData.Count, Is.EqualTo(2));

            m_SearchElement.Search("three");
            Assert.That(container.ValidDestinationData.Count, Is.EqualTo(3));
        }

        [Test]
        public void Search_TokensShouldBeEmptyOnEmptySearchString()
        {
            var searchElement = new SearchElement();
            searchElement.RegisterSearchBackend(CreateSearchBackend<TestData>());

            searchElement.RegisterSearchQueryHandler<TestData>(q =>
            {
                Assert.DoesNotThrow(() =>
                {
                    var i = q.Tokens.Count;
                });
            });
            searchElement.AddSearchDataProperty(new PropertyPath(nameof(TestData.Name)));

            searchElement.Search(string.Empty);
            searchElement.Search(null);
            searchElement.Search("   ");
        }

        [Test]
        public void Search_BackendTokensParserHandlesDoubleQuotes()
        {
            SearchBackend<string> backend =  new QuickSearchBackend<string>();

            var query = backend.Parse("filter:\"Hello World\" \"Hello\" World filter:\"  Hello filter:\"World");
            Assert.That(query.Tokens, Is.EquivalentTo(new []
            {
                "filter:\"Hello World\"",
                "\"Hello\"",
                "World",
                "filter:\"  Hello filter:\"",
                "World"
            }));
        }

        [Test]
        public void Search_WhenSearchDataCallbackReturnsNull_DoesNotThrow()
        {
            var container = new TestDataContainer
            {
                ValidSourceData = Generate(100),
                ValidDestinationData = new List<TestData>()
            };

            m_PropertyElement.SetTarget(container);
            m_SearchElement.RegisterSearchQueryHandler(m_PropertyElement, TestDataContainer.ValidSourceDataPath, TestDataContainer.ValidDestinationDataPath);
            m_SearchElement.AddSearchDataCallback<TestData>(_ => null);

            Assert.DoesNotThrow(() =>
            {
                m_SearchElement.Search("");
            });
        }

        [Test]
        public void Search_WhenSearchDataCallbackReturnsNullElement_DoesNotThrow()
        {
            var container = new TestDataContainer
            {
                ValidSourceData = Generate(100),
                ValidDestinationData = new List<TestData>()
            };

            m_PropertyElement.SetTarget(container);
            m_SearchElement.RegisterSearchQueryHandler(m_PropertyElement, TestDataContainer.ValidSourceDataPath, TestDataContainer.ValidDestinationDataPath);
            m_SearchElement.AddSearchDataCallback<TestData>(_ =>
            {
                return new [] { "a", "b", null, "d" };
            });

            Assert.DoesNotThrow(() =>
            {
                m_SearchElement.Search("");
            });
        }

        [Test]
        public void Search_AddSearchFilterCallback_WithCustomOperatorHandler()
        {
            var container = new TestDataContainer()
            {
                ValidSourceData = Generate(100),
                ValidDestinationData = new List<TestData>()
            };

            container.ValidSourceData[0].NestedEnumerable = new[]
            {
                new TestData.NestedStructWithValues { A = 1, B = 10, C = 2.3f }
            };

            container.ValidSourceData[1].NestedEnumerable = new[]
            {
                new TestData.NestedStructWithValues { A = 2, B = 10, C = 2.3f },
                new TestData.NestedStructWithValues { A = 2, B = 20, C = 2.4f }
            };

            container.ValidSourceData[2].NestedEnumerable = new[]
            {
                new TestData.NestedStructWithValues { A = 3, B = 10, C = 2.3f },
                new TestData.NestedStructWithValues { A = 3, B = 20, C = 2.4f },
                new TestData.NestedStructWithValues { A = 3, B = 30, C = 2.5f }
            };

            m_PropertyElement.SetTarget(container);
            m_SearchElement.RegisterSearchQueryHandler(m_PropertyElement, TestDataContainer.ValidSourceDataPath, TestDataContainer.ValidDestinationDataPath);
            m_SearchElement.AddSearchFilterCallback<TestData, IEnumerable<int>>("a", SearchFilterCallback);
            m_SearchElement.AddSearchOperatorHandler<IEnumerable<int>, int>(">=", (lhs, rhs) => lhs.Any(e => e >= rhs));
            m_SearchElement.Search("a>=2");

            Assert.That(container.ValidDestinationData.Count, Is.EqualTo(2));
        }

        IEnumerable<int> SearchFilterCallback(TestData data)
        {
            if (null == data.NestedEnumerable)
                yield break;

            foreach (var value in data.NestedEnumerable)
                yield return value.A;
        }

        static ISearchBackend<TData> CreateSearchBackend<TData>()
        {
            return new QuickSearchBackend<TData>();
        }

        class TestData
        {
            public struct NestedStruct
            {
                public string Value;
            }

            public struct NestedStructWithValues
            {
                public int A;
                public int B;
                public float C;
            }

            [CreateProperty] public int Id { get; set; }
            [CreateProperty] public string Name { get; set; }
            [CreateProperty] public Vector2 Position { get; set; }
            [CreateProperty] public bool Active { get; set; }
            [CreateProperty] public NestedStruct Nested { get; set; }
            [CreateProperty] public string[] StringArray { get; set; }
            [CreateProperty] public IEnumerable<string> StringEnumerable { get; set; }
            [CreateProperty] public IEnumerable<NestedStructWithValues> NestedEnumerable { get; set; }
            [CreateProperty] public int[] IntArray { get; set; }
        }

        class EquatableAndComparableTestData : IEquatable<EquatableAndComparableTestData>, IComparable<EquatableAndComparableTestData>
        {
            public string Name;

            public bool Equals(EquatableAndComparableTestData other)
                => throw new NotImplementedException();

            public int CompareTo(EquatableAndComparableTestData other)
                => throw new NotImplementedException();
        }

        static TestData[] Generate(int size)
        {
            var data = new TestData[size];

            for (var i = 0; i < size; ++i)
            {
                var posX = i * 10;
                var posY = i * -25;

                string name;

                switch (i % 4)
                {
                    case 0:
                        name = $"Material {i}";
                        break;
                    case 1:
                        name = $"Mesh {i}";
                        break;
                    case 2:
                        name = $"Camera {i}";
                        break;
                    default:
                        name = $"Object {i}";
                        break;
                }

                data[i] = new TestData {Id = i, Name = name, Position = new Vector2(posX, posY), Active = i % 2 == 0, Nested = new TestData.NestedStruct { Value = $"nested{i}"}};
            }

            return data;
        }
    }
}
