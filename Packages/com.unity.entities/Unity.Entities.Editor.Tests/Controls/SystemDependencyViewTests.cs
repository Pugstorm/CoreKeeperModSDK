using NUnit.Framework;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    class SystemDependencyViewTests : ControlsTestFixture
    {
        [Test]
        public void SystemDependencyView_GeneratesCorrectVisualHierarchy()
        {
            var data = new SystemDependencyViewData(new SystemProxy(m_SystemA, m_WorldProxy), "System A");
            var el = new SystemDependencyView(data);

            Assert.That(el.Q<Label>(className: UssClasses.SystemDependencyView.Name).text, Is.EqualTo("System A"));
        }

        [Test]
        public void SystemDependencyView_UpdatesCorrectly()
        {
            var el = new SystemDependencyView(new SystemDependencyViewData(new SystemProxy(m_SystemA, m_WorldProxy),"System A"));

            var name = el.Q<Label>(className: UssClasses.SystemDependencyView.Name);
            Assert.That(name.text, Is.EqualTo("System A"));

            el.Update(new SystemDependencyViewData(new SystemProxy(m_SystemB, m_WorldProxy),"System B"));
            Assert.That(name.text, Is.EqualTo("System B"));
        }
    }
}
