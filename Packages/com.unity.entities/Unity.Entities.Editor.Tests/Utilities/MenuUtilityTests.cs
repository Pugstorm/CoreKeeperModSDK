using NUnit.Framework;
using System.Linq;
using System.Numerics;
using Unity.Serialization.Json;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    [TestFixture]
    class MenuUtilityTests
    {
        const string prefix = "Copy/";

        class CopyAllTheFields
        {
            public float FloatValue = 570.0f;
            public string StringValue = "This is a test string";
            public Vector3 Vector3Value = new Vector3(25, 475, 2347);
        }

        DropdownMenu m_Menu;
        DropdownMenuAction[] m_Actions;
        CopyAllTheFields m_Instance;

        [SetUp]
        public void Setup()
        {
            m_Instance = new CopyAllTheFields();
            m_Menu = new DropdownMenu();
            m_Menu.AddCopyValue(m_Instance);
            m_Actions = m_Menu.MenuItems().OfType<DropdownMenuAction>().ToArray();
        }

        [Test]
        public void CopyingFields_WhenGivenInstance_PopulateFieldsCorrectly()
        {
            Assert.That(m_Actions[0].name, Is.EqualTo(prefix + "All"));
            Assert.That(m_Actions[1].name, Is.EqualTo(prefix + nameof(CopyAllTheFields.FloatValue)));
            Assert.That(m_Actions[2].name, Is.EqualTo(prefix + nameof(CopyAllTheFields.StringValue)));
            Assert.That(m_Actions[3].name, Is.EqualTo(prefix + nameof(CopyAllTheFields.Vector3Value)));
        }

        [Test]
        public void CopyMenuItems_WhenExecuted_CopiesToSystemBuffer()
        {
            m_Actions[0].Execute();
            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(JsonSerialization.ToJson(m_Instance)));

            m_Actions[1].Execute();
            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(JsonSerialization.ToJson(m_Instance.FloatValue)));

            m_Actions[2].Execute();
            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(JsonSerialization.ToJson(m_Instance.StringValue)));

            m_Actions[3].Execute();
            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(JsonSerialization.ToJson(m_Instance.Vector3Value)));
        }
    }
}
