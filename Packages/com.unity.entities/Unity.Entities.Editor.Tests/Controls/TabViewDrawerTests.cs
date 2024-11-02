using System;
using JetBrains.Annotations;
using NUnit.Framework;
using Unity.Entities.UI;
using Unity.Serialization.Editor;
using UnityEditor;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.Entities.Editor.Tests
{
    [TestFixture]
    class TabViewDrawerTests
    {
        EditorWindow m_Window;

        [SetUp]
        public void Setup()
        {
            m_Window = EditorWindow.CreateWindow<EditorWindow>();
        }

        [TearDown]
        public void Teardown()
        {
            m_Window.Close();
            Object.DestroyImmediate(m_Window);
            SessionState<TabViewDrawer.State>.Clear(TestContainer.TabViewId);
        }

        [Test]
        public void TabViewDrawer_PropagateVisibility()
        {
            var pe = new PropertyElement();
            m_Window.rootVisualElement.Add(pe);
            var tabs = new[]
            {
                new TestTab("tab1"),
                new TestTab("tab2"),
                new TestTab("tab3"),
            };
            var container = new TestContainer { Tabs = tabs };
            pe.SetTarget(container);

            var tabView = pe.Q<TabView>();
            Assert.That(tabView, Is.Not.Null);

            Assert.That(tabs[0].IsVisible, Is.True);
            Assert.That(tabs[1].IsVisible, Is.False);
            Assert.That(tabs[2].IsVisible, Is.False);

            tabView.value = 2;
            Assert.That(tabs[0].IsVisible, Is.False);
            Assert.That(tabs[1].IsVisible, Is.False);
            Assert.That(tabs[2].IsVisible, Is.True);
        }

        [Test]
        public void TabViewDrawer_LazilyInitializePropertyElements()
        {
            var pe = new PropertyElement();
            m_Window.rootVisualElement.Add(pe);
            var tabs = new[]
            {
                new TestTab("tab1"),
                new TestTab("tab2"),
                new TestTab("tab3"),
            };
            var container = new TestContainer { Tabs = tabs };
            pe.SetTarget(container);

            var tabView = pe.Q<TabView>();
            Assert.That(tabView, Is.Not.Null);

            Assert.That(tabs[0].InspectorBuilt, Is.True);
            Assert.That(tabs[1].InspectorBuilt, Is.False);
            Assert.That(tabs[2].InspectorBuilt, Is.False);

            tabView.value = 2;
            Assert.That(tabs[0].InspectorBuilt, Is.True);
            Assert.That(tabs[1].InspectorBuilt, Is.False);
            Assert.That(tabs[2].InspectorBuilt, Is.True);
        }

        class TestContainer
        {
            public const string TabViewId = nameof(TestContainer);

            [UsedImplicitly, TabView(TabViewId)]
            public ITabContent[] Tabs;
        }

        class TestTab : ITabContent
        {
            public string TabName { get; }

            public TestTab(string tabName) => TabName = tabName;

            public bool IsVisible { get; set; }

            public bool InspectorBuilt { get; set; }

            public void OnTabVisibilityChanged(bool isVisible) => IsVisible = isVisible;

            [UsedImplicitly]
            class TestInspector : PropertyInspector<TestTab>
            {
                public override VisualElement Build()
                {
                    Target.InspectorBuilt = true;
                    return new VisualElement();
                }
            }
        }
    }
}
