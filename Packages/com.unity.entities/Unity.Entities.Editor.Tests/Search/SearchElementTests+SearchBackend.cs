using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Properties;

namespace Unity.Entities.Editor.Tests
{
    sealed partial class SearchElementTests
    {
        class TestSearchBackend : ISearchBackend<TestData>
        {
            class SearchQuery : ISearchQuery<TestData>
            {
                public string SearchString { get; internal set; }
                public ICollection<string> Tokens { get; }

                public IEnumerable<TestData> Apply(IEnumerable<TestData> data)
                {
                    return data;
                }
            }

            public StringComparison GlobalStringComparison { get; set; }

            public void AddSearchDataProperty(PropertyPath path)
            {
            }

            public void AddSearchFilterProperty(string token, PropertyPath path, SearchFilterOptions options)
            {
            }

            public void AddSearchDataCallback(Func<TestData, IEnumerable<string>> getSearchDataFunc)
            {
            }

            public void AddSearchFilterCallback<TFilter>(string token, Func<TestData, TFilter> getFilterDataFunc, SearchFilterOptions options)
            {
            }

            public void AddSearchOperatorHandler<TFilterVariable, TFilterConstant>(string op, Func<TFilterVariable, TFilterConstant, bool> handler)
            {
            }
            public void AddSearchOperatorHandler<TFilterVariable, TFilterConstant>(string op, Func<TFilterVariable, TFilterConstant, StringComparison, bool> handler)
            {
            }

            public ISearchQuery<TestData> Parse(string text)
            {
                return new SearchQuery {SearchString = text};
            }
        }

        [Test]
        public void Search_WithCustomBackend()
        {
            // Register a custom search backend
            m_SearchElement.RegisterSearchBackend(new TestSearchBackend());

            var originalData = Generate(1000);
            var filteredData = default(TestData[]);

            m_SearchElement.RegisterSearchQueryHandler<TestData>(search =>
            {
                filteredData = search.Apply(originalData).ToArray();
            });

            m_SearchElement.Search("Mat");

            Assert.That(filteredData.Length, Is.EqualTo(1000));
        }
    }
}
