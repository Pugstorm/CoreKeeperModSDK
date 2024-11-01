using NUnit.Framework;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    class QueryViewTests
    {
        // We use this TestComponent as a dummy component type to test out displays in the views.
        struct TestComponent : IComponentData
        {
#pragma warning disable 649
            public int value;
#pragma warning restore 649
        }

        [Test]
        public void QueryView_GeneratesCorrectVisualHierarchy()
        {
            var data = new QueryViewData(1, new[]
            {
                new ComponentViewData(typeof(TestComponent),"component", ComponentType.AccessMode.ReadOnly, ComponentViewData.ComponentKind.Buffer),
                new ComponentViewData(typeof(TestComponent),"component", ComponentType.AccessMode.Exclude, ComponentViewData.ComponentKind.Buffer)
            });
            var el = new QueryView(in data);

            Assert.That(el.HeaderName.text, Is.EqualTo("Query #1"));
            Assert.That(el.Query<ComponentView>().Visible().ToList().Count, Is.EqualTo(2));
        }

        [Test]
        public void QueryView_UpdatesCorrectly()
        {
            var el = new QueryView(new QueryViewData(1, new[]
            {
                new ComponentViewData(typeof(TestComponent), "component", ComponentType.AccessMode.ReadOnly, ComponentViewData.ComponentKind.Buffer),
                new ComponentViewData(typeof(TestComponent),"component", ComponentType.AccessMode.Exclude, ComponentViewData.ComponentKind.Buffer)
            }));

            var queryId = el.HeaderName;
            Assert.That(queryId.text, Is.EqualTo("Query #1"));
            Assert.That(el.Query<ComponentView>().Visible().ToList().Count, Is.EqualTo(2));

            el.Update(new QueryViewData(2, new[]
            {
                new ComponentViewData(typeof(TestComponent),"component", ComponentType.AccessMode.ReadOnly, ComponentViewData.ComponentKind.Buffer),
                new ComponentViewData(typeof(TestComponent),"component", ComponentType.AccessMode.Exclude, ComponentViewData.ComponentKind.Buffer),
                new ComponentViewData(typeof(TestComponent),"component", ComponentType.AccessMode.Exclude, ComponentViewData.ComponentKind.Buffer)
            }));

            Assert.That(queryId.text, Is.EqualTo("Query #2"));
            Assert.That(el.Query<ComponentView>().Visible().ToList().Count, Is.EqualTo(3));
        }

        [Test]
        public void QueryView_ShowEmptyMessageForQueryWithNoComponents()
        {
            var el = new QueryView(new QueryViewData(1, new[]
            {
                new ComponentViewData(typeof(TestComponent),"component", ComponentType.AccessMode.ReadOnly, ComponentViewData.ComponentKind.Buffer),
                new ComponentViewData(typeof(TestComponent),"component", ComponentType.AccessMode.Exclude, ComponentViewData.ComponentKind.Buffer)
            }));

            var queryId = el.HeaderName;
            Assert.That(queryId.text, Is.EqualTo("Query #1"));
            Assert.That(el.Query<ComponentView>().Visible().ToList().Count, Is.EqualTo(2));
            Assert.That(el.Q<Label>(className: UssClasses.QueryView.EmptyMessage), UIToolkitTestHelper.Is.Display(DisplayStyle.None));

            el.Update(new QueryViewData(2, new ComponentViewData[0]));

            Assert.That(queryId.text, Is.EqualTo("Query #2"));
            Assert.That(el.Query<ComponentView>().Visible().ToList(), Is.Empty);
            Assert.That(el.Q<Label>(className: UssClasses.QueryView.EmptyMessage), UIToolkitTestHelper.Is.Display(DisplayStyle.Flex));
        }
    }
}
