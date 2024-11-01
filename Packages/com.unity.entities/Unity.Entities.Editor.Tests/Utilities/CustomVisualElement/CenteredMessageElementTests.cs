using NUnit.Framework;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    class CenteredMessageElementTests
    {
        [Test]
        public void CenteredMessageElement_WithContent()
        {
            var centeredMessageElementWithContent = new CenteredMessageElement { Title = "title", Message = "message" };
            Assert.That(centeredMessageElementWithContent.Title, Is.EqualTo("title"));
            Assert.That(centeredMessageElementWithContent.Message, Is.EqualTo("message"));
            Assert.That(centeredMessageElementWithContent.m_Title, Is.Not.Null);
            Assert.That(centeredMessageElementWithContent.m_Message, Is.Not.Null);
            Assert.That(centeredMessageElementWithContent.m_Title.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(centeredMessageElementWithContent.m_Message.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void CenteredMessageElement_WithNoContent()
        {
            var centeredMessageElementWithNoContent = new CenteredMessageElement();
            Assert.That(centeredMessageElementWithNoContent.Title, Is.Null);
            Assert.That(centeredMessageElementWithNoContent.Message, Is.Null);
            Assert.That(centeredMessageElementWithNoContent.m_Title, Is.Not.Null);
            Assert.That(centeredMessageElementWithNoContent.m_Message, Is.Not.Null);
        }

        [Test]
        public void CenteredMessageElement_WithEmptyContent()
        {
            var centeredMessageElementWithEmptyContent = new CenteredMessageElement { Title = string.Empty, Message = string.Empty };
            Assert.That(centeredMessageElementWithEmptyContent.m_Title.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(centeredMessageElementWithEmptyContent.m_Message.style.display.value, Is.EqualTo(DisplayStyle.None));
        }
    }
}
