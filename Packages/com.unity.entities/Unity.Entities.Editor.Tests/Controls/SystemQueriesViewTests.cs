using System;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    class SystemQueriesViewTests : ControlsTestFixture
    {
        [Test]
        public void SystemQueriesView_GeneratesCorrectVisualHierarchy([Values] SystemQueriesViewData.SystemKind systemKind)
        {
            var el = new SystemQueriesView(new SystemQueriesViewData(new SystemProxy(m_SystemA, m_WorldProxy), systemKind, new[]
            {
                new QueryViewData(1, new ComponentViewData[0]),
                new QueryViewData(2, new ComponentViewData[0])
            }));

            Assert.That(el.HeaderName.text, Is.EqualTo("Test Systems For Controls | System A"));
            Assert.That(el.ActionButtonContainer, UIToolkitTestHelper.Is.Display(DisplayStyle.Flex));
            Assert.That(el.ActionButton, UIToolkitTestHelper.Is.Display(DisplayStyle.Flex));
            Assert.That(el.Query<QueryView>().Visible().ToList().Count, Is.EqualTo(2));
            if (systemKind != SystemQueriesViewData.SystemKind.Regular)
                Assert.That(el.HeaderIcon.GetClasses(), Does.Contain(MapKindToUssClass(systemKind)));

            static string MapKindToUssClass(SystemQueriesViewData.SystemKind kind) => kind switch
            {
                SystemQueriesViewData.SystemKind.Unmanaged => "unmanaged-system",
                SystemQueriesViewData.SystemKind.CommandBufferBegin => "begin-command-buffer",
                SystemQueriesViewData.SystemKind.CommandBufferEnd => "end-command-buffer",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
        }

        [Test]
        public void SystemQueriesView_UpdatesCorrectly()
        {
            var el = new SystemQueriesView(new SystemQueriesViewData(new SystemProxy(m_SystemA, m_WorldProxy), SystemQueriesViewData.SystemKind.CommandBufferBegin, new[]
            {
                new QueryViewData(1, new ComponentViewData[0]),
                new QueryViewData(2, new ComponentViewData[0])
            }));

            Assert.That(el.HeaderName.text, Is.EqualTo("Test Systems For Controls | System A"));
            Assert.That(el.HeaderIcon.GetClasses(), Does.Contain("begin-command-buffer"));
            Assert.That(el.Query<QueryView>().Visible().ToList().Count, Is.EqualTo(2));

            el.Update(new SystemQueriesViewData(new SystemProxy(m_SystemB, m_WorldProxy), SystemQueriesViewData.SystemKind.CommandBufferEnd, new[]
            {
                new QueryViewData(1, new ComponentViewData[0]),
                new QueryViewData(2, new ComponentViewData[0]),
                new QueryViewData(2, new ComponentViewData[0])
            }));

            Assert.That(el.HeaderName.text, Is.EqualTo("Test Systems For Controls | System B"));
            Assert.That(el.HeaderIcon.GetClasses(), Does.Contain("end-command-buffer"));
            Assert.That(el.Query<QueryView>().Visible().ToList().Count, Is.EqualTo(3));
        }
    }
}
