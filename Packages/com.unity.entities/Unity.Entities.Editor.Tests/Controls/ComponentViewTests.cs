using System;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    class ComponentViewTests
    {
        // We use this TestComponent as a dummy component type to test out displays in the views.
        struct TestComponent : IComponentData
        {
#pragma warning disable 649
            public int value;
#pragma warning restore 649
        }

        [Test]
        public void ComponentView_GeneratesCorrectVisualHierarchy(
            [Values] ComponentType.AccessMode accessMode,
            [Values] ComponentViewData.ComponentKind componentKind)
        {
            var data = new ComponentViewData(typeof(TestComponent), "Component type", accessMode, componentKind);
            var el = new ComponentView(in data);

            Assert.That(el.Q<Label>(className: UssClasses.ComponentView.Name).text, Is.EqualTo(data.Name));
            if (componentKind != ComponentViewData.ComponentKind.Default)
                Assert.That(el.Q(className: UssClasses.ComponentView.Icon).GetClasses(), Contains.Item(MapKindToUssClass(componentKind)));
            Assert.That(el.Q<Label>(className: UssClasses.ComponentView.AccessMode).text, Is.EqualTo(AccessModeToString(accessMode)));

            static string MapKindToUssClass(ComponentViewData.ComponentKind kind) => kind switch
            {
                ComponentViewData.ComponentKind.Tag => "tag",
                ComponentViewData.ComponentKind.Buffer => "buffer",
                ComponentViewData.ComponentKind.Shared => "shared",
                ComponentViewData.ComponentKind.Chunk => "chunk",
                ComponentViewData.ComponentKind.Managed => "managed",
                _ => throw new NotSupportedException()
            };

            static string AccessModeToString(ComponentType.AccessMode accessMode) => accessMode switch
            {
                ComponentType.AccessMode.ReadWrite => "Read & Write",
                ComponentType.AccessMode.ReadOnly => "Read",
                ComponentType.AccessMode.Exclude => "Exclude",
                _ => throw new NotSupportedException()
            };
        }

        [Test]
        public void ComponentView_UpdatesCorrectly()
        {
            var el = new ComponentView(new ComponentViewData(typeof(TestComponent),"Component type", ComponentType.AccessMode.ReadWrite, ComponentViewData.ComponentKind.Tag));

            var name = el.Q<Label>(className: UssClasses.ComponentView.Name);
            var icon = el.Q(className: UssClasses.ComponentView.Icon);
            var accessMode = el.Q<Label>(className: UssClasses.ComponentView.AccessMode);

            Assert.That(name.text, Is.EqualTo("Component type"));
            Assert.That(icon.GetClasses(), Contains.Item("tag"));
            Assert.That(accessMode.text, Is.EqualTo("Read & Write"));

            el.Update(new ComponentViewData(typeof(TestComponent),"Other component", ComponentType.AccessMode.ReadOnly, ComponentViewData.ComponentKind.Buffer));

            Assert.That(name.text, Is.EqualTo("Other component"));
            Assert.That(icon.GetClasses(), Does.Contain("buffer").And.Not.Contain("tag"));
            Assert.That(accessMode.text, Is.EqualTo("Read"));
        }
    }
}
