using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Entities.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.Entities.Editor.Tests
{
    class ComponentsWindowTests
    {
        EditorWindow m_ComponentsWindow;

        struct TestComponent : IComponentData
        {
#pragma warning disable 649
            public float value;
#pragma warning disable 649
        }

        internal struct TestSharedComponent : ISharedComponentData
        {
#pragma warning disable 649
            public int value;
#pragma warning restore 649
        }

        struct TestTagComponent : IComponentData
        {
        }

        #if !UNITY_DISABLE_MANAGED_COMPONENTS
        internal class TestManagedComponent : IComponentData
        {
#pragma warning disable 649
            public float value;
#pragma warning restore 649
        }
        #endif

        struct TestBufferComponent : IBufferElementData
        {
#pragma warning disable 649
            public float value;
#pragma warning restore 649
        }

        [SetUp]
        public void SetUp()
        {
            m_ComponentsWindow = ScriptableObject.CreateInstance<ComponentsWindow>();
            m_ComponentsWindow.Show();
        }

        [TearDown]
        public void TearDown()
        {
            m_ComponentsWindow.Close();
            Object.DestroyImmediate(m_ComponentsWindow);
        }

        public static IEnumerable ComponentsWindow_ComponentTypeViewData_MatchesTypeInfo_TestCaseSource()
        {
            yield return new TestCaseData(typeof(TestComponent)) { ExpectedResult = (ComponentsWindow.DebugTypeCategory.Data, "Components Window Tests | Test Component", "component-icon") };
            yield return new TestCaseData(typeof(TestSharedComponent)) { ExpectedResult = (ComponentsWindow.DebugTypeCategory.Shared, "Components Window Tests | Test Shared Component", "shared-component-icon") };
            yield return new TestCaseData(typeof(TestTagComponent)) { ExpectedResult = (ComponentsWindow.DebugTypeCategory.Tag, "Components Window Tests | Test Tag Component", "tag-component-icon") };
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            yield return new TestCaseData(typeof(TestManagedComponent)) { ExpectedResult = (ComponentsWindow.DebugTypeCategory.Data | ComponentsWindow.DebugTypeCategory.Managed, "Components Window Tests | Test Managed Component", "managed-component-icon") };
#endif
            yield return new TestCaseData(typeof(TestBufferComponent)) { ExpectedResult = (ComponentsWindow.DebugTypeCategory.Buffer, "Components Window Tests | Test Buffer Component", "buffer-component-icon") };
        }

        [Test]
        [TestCaseSource(nameof(ComponentsWindow_ComponentTypeViewData_MatchesTypeInfo_TestCaseSource))]
        public (ComponentsWindow.DebugTypeCategory category, string displayName, string iconClass) ComponentsWindow_ComponentTypeViewData_MatchesTypeInfo(Type componentType)
        {
            var viewData = new ComponentsWindow.ComponentTypeViewData(TypeManager.GetTypeInfo(TypeManager.GetTypeIndex(componentType)));
            return (viewData.Category, viewData.DisplayName, viewData.IconClass);
        }

        [Test]
        public void ComponentsWindow_ContainsAllExpectedComponentTypes(
            [Values(typeof(TestComponent), typeof(TestSharedComponent), typeof(TestTagComponent), typeof(TestBufferComponent)
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                    , typeof(TestManagedComponent))]
#else
                    )]
#endif
            Type componentType)
        {
            Assert.That(ComponentsWindow.s_Types, Does.Contain(new ComponentsWindow.ComponentTypeViewData(TypeManager.GetTypeInfo(TypeManager.GetTypeIndex(componentType)))));
        }

        [Test]
        public void ComponentsWindow_ListViewContainsSearchResults()
        {
            var listView = m_ComponentsWindow.rootVisualElement.Q<ListView>(className: "components-window__list-view");
            var searchElement = m_ComponentsWindow.rootVisualElement.Q<SearchElement>(className: "components-window__search-element");
            Assert.That(listView, Is.Not.Null);
            Assert.That(searchElement, Is.Not.Null);

            const string searchString = "TestComponent";
            searchElement.Search(searchString);
            var items = listView.itemsSource.Cast<ComponentsWindow.ComponentTypeViewData>().ToArray();
            Assert.That(items, Is.EquivalentTo(ComponentsWindow.s_Types.Where(t => t.Name.IndexOf(searchString) >= 0)));
            Assert.That(items, Does.Contain(new ComponentsWindow.ComponentTypeViewData(TypeManager.GetTypeInfo<TestComponent>())));
        }
    }
}
