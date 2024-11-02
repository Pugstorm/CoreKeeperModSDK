using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    [TestFixture]
    class TabViewTests
    {
        [Test]
        public void CreatingATabView_WithNoTabs_CreatesExpectedHierarchy()
        {
            var tabView = new TabView();
            Assert.That(tabView.value, Is.EqualTo(-1));
            Assert.That(tabView.hierarchy.childCount, Is.EqualTo(2));
            Assert.That(tabView.hierarchy[0].ClassListContains("tab-view__tab-header"), Is.True);
            Assert.That(tabView.hierarchy[1].ClassListContains("tab-view__tab-content"), Is.True);
            Assert.That(tabView.hierarchy[0].childCount, Is.EqualTo(0));
            Assert.That(tabView.hierarchy[1].childCount, Is.EqualTo(0));
        }

        [Test]
        public void TabView_WhenSettingTabs_CreatesExpectedHierarchy()
        {
            var tabView = new TabView();

            var header = tabView.Q(className: "tab-view__tab-header");
            var tabs = new List<TabContent>();
            for (var size = 3; size >= 0; --size, tabs.Clear())
            {
                for (var i = 0; i < size; ++i)
                    tabs.Add(new TabContent{TabName = $"Tab{i}"});
                tabView.Tabs = tabs;

                // Header
                Assert.That(header.childCount, Is.EqualTo(tabs.Count));
                var tabLabels = header.Query<Label>().ToList();
                for (var i = 0; i < tabs.Count; ++i)
                    Assert.That(tabLabels[i].text, Is.EqualTo(tabs[i].TabName));

                // Content
                Assert.That(tabView.contentContainer.childCount, Is.EqualTo(tabs.Count));
                for (var i = 0; i < tabs.Count; ++i)
                    Assert.That(tabView[i], Is.EqualTo(tabs[i]));
            }
        }

        [Test]
        public void TabView_WhenSettingTabs_KeepsCurrentIndexWhenValid()
        {
            var tabView = new TabView();
            Assert.That(tabView.value, Is.EqualTo(-1));
            var tabs = new List<TabContent>();

            for (var beforeSize = 0; beforeSize < 3; ++beforeSize)
            for (var afterSize = 3; afterSize >= 0; --afterSize)
            for (var index = -2; index < 5; ++index)
            {
                tabView.value = -1;

                for (var i = 0; i < beforeSize; ++i)
                    tabs.Add(new TabContent{TabName = $"Tab{i}"});
                tabView.Tabs = tabs;
                tabs.Clear();

                var expectedIndex = index < 0 || index >= beforeSize || index >= afterSize ? -1 : index;
                tabView.value = index;
                for (var i = 0; i < afterSize; ++i)
                    tabs.Add(new TabContent{TabName = $"Tab{i}"});
                tabView.Tabs = tabs;
                tabs.Clear();

                Assert.That(tabView.value, Is.EqualTo(expectedIndex));
            }
        }

        [Test]
        public void TabView_WhenSettingTheActiveTab_KeepsCoherentState()
        {
            var tabView = new TabView();
            Assert.That(tabView.value, Is.EqualTo(-1));
            var tab1 = new TabContent {TabName = "Tab1"};
            var tab2 = new TabContent {TabName = "Tab2"};
            var tab3 = new TabContent {TabName = "Tab3"};
            var tabs = new List<TabContent> { tab1, tab2, tab3};
            tabView.Tabs = tabs;
            Assert.That(tabView.value, Is.EqualTo(-1));
            var header = tabView.Q(className: "tab-view__tab-header");
            var content = tabView.Q(className: "tab-view__tab-content");
            for (var index = -2; index < 5; ++index)
            {
                var expectedIndex = index >= 0 && index < tabs.Count ? index : -1;
                tabView.value = index;
                Assert.That(tabView.value, Is.EqualTo(expectedIndex));

                for (var i = 0; i < tabs.Count; ++i)
                    Assert.That(tabs[i].style.display, Is.EqualTo(new StyleEnum<DisplayStyle>(expectedIndex == i ? DisplayStyle.Flex : DisplayStyle.None)));
            }
        }
    }
}
