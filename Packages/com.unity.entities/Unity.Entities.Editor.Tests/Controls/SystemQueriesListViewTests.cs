using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    class SystemQueriesListViewTests : ControlsTestFixture
    {
        [Test]
        public void SystemQueriesListView_GeneratesCorrectVisualHierarchy()
        {
            var systemQueriesViewDataList = new List<SystemQueriesViewData>
            {
                new SystemQueriesViewData(new SystemProxy(m_SystemA, m_WorldProxy), SystemQueriesViewData.SystemKind.Regular, new[]
                {
                    new QueryViewData(1, new ComponentViewData[0]),
                    new QueryViewData(2, new ComponentViewData[0]),
                }),
                new SystemQueriesViewData(new SystemProxy(m_SystemB, m_WorldProxy), SystemQueriesViewData.SystemKind.Unmanaged, new[]
                {
                    new QueryViewData(1, new ComponentViewData[0]),
                    new QueryViewData(2, new ComponentViewData[0]),
                    new QueryViewData(3, new ComponentViewData[0]),
                }),
            };

            var searchTerm = new List<string>{""};
            var systemQueriesListView = new SystemQueriesListView(systemQueriesViewDataList, searchTerm, "", "");
            Assert.That(systemQueriesListView.Q<Label>(className: UssClasses.SystemListView.MoreLabel).style.display.value, Is.EqualTo(DisplayStyle.None));

            var sectionElement = systemQueriesListView.Q<FoldoutWithoutActionButton>();
            Assert.That(sectionElement, Is.Not.Null);
        }

        [Test]
        public void SystemQueriesListView_UpdatesCorrectly()
        {
            var systemQueriesViewDataList = new List<SystemQueriesViewData>
            {
                new SystemQueriesViewData(new SystemProxy(m_SystemA, m_WorldProxy), SystemQueriesViewData.SystemKind.CommandBufferBegin, new[]
                {
                    new QueryViewData(1, new ComponentViewData[0]),
                    new QueryViewData(2, new ComponentViewData[0]),
                }),
                new SystemQueriesViewData(new SystemProxy(m_SystemB, m_WorldProxy), SystemQueriesViewData.SystemKind.Unmanaged, new[]
                {
                    new QueryViewData(1, new ComponentViewData[0]),
                    new QueryViewData(2, new ComponentViewData[0]),
                    new QueryViewData(3, new ComponentViewData[0]),
                }),
            };

            var searchTerm = new List<string>{""};
            var systemQueriesListView = new SystemQueriesListView(systemQueriesViewDataList, searchTerm, "", "");
            systemQueriesListView.Update(systemQueriesViewDataList);

            Assert.That(systemQueriesListView.Q<Label>(className: UssClasses.SystemListView.MoreLabel).style.display.value, Is.EqualTo(DisplayStyle.None));

            var sectionElement = systemQueriesListView.Q<FoldoutWithoutActionButton>();
            var systemList = sectionElement.Query<SystemQueriesView>().ToList();
            Assert.That(sectionElement, Is.Not.Null);
            Assert.That(systemList.Count, Is.EqualTo(2));

            Assert.That(systemList[0].HeaderName.text, Is.EqualTo("Test Systems For Controls | System A"));
            Assert.That(systemList[0].HeaderIcon.GetClasses(), Does.Contain("begin-command-buffer"));

            Assert.That(systemList[1].HeaderName.text, Is.EqualTo("Test Systems For Controls | System B"));
            Assert.That(systemList[1].HeaderIcon.GetClasses(), Does.Contain("unmanaged-system"));
        }
    }
}
