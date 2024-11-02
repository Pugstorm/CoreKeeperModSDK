using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    class AutoCompleteTests
    {
        EditorWindow m_Window;
        TextField m_TextField;
        AutoComplete m_AutoComplete;
        VisualElement m_CompletionBox;

        [OneTimeSetUp]
        public void GlobalSetUp()
        {
            m_Window = EditorWindow.CreateInstance<EditorWindow>();
            m_Window.Show();
        }

        [OneTimeTearDown]
        public void GlobalTeardown()
        {
            m_Window.Close();
        }

        [SetUp]
        public void SetUp()
        {
            m_TextField = new TextField();
            m_Window.rootVisualElement.Add(m_TextField);
            m_AutoComplete = m_TextField.EnableAutoComplete(TestCompletionBehavior.Instance);
            m_CompletionBox = m_Window.rootVisualElement.Q(className: AutoComplete.AutoCompleteContainerUssClass);
            TestCompletionBehavior.Instance.CompletionItems = new List<string>
            {
                "Hello",
                "World",
                "Bonjour",
                "Bonsoir"
            };
        }

        [TearDown]
        public void Teardown()
        {
            m_Window.rootVisualElement.Clear();
        }

        [Test]
        public void AutoComplete_CompletionBoxCreated()
        {
            Assert.That(m_CompletionBox, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator AutoComplete_ShowCompletionBox()
        {
            m_TextField.Focus();
            m_TextField.Q(TextField.textInputUssName).Focus();

            m_TextField.SetValueWithoutNotify("Bon Hello");
            m_TextField.SelectRange(3, 3);

            yield return null;

            m_Window.SendEvent(new Event
            {
                keyCode = KeyCode.Space,
                modifiers = EventModifiers.Control,
                type = EventType.KeyDown
            });

            Assert.That(m_CompletionBox.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(m_CompletionBox.Q<ListView>().itemsSource, Is.EquivalentTo(new[] { "Bonjour", "Bonsoir" }));
        }

        [Test]
        public void AutoComplete_HideCompletionBox()
        {
            AutoComplete_ShowCompletionBox();

            m_AutoComplete.Clear();
            Assert.That(m_CompletionBox.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [UnityTest]
        public IEnumerator AutoComplete_CompletesToken()
        {
            m_TextField.Focus();
            m_TextField.Q(TextField.textInputUssName).Focus();

            m_TextField.SetValueWithoutNotify("Hello Wor 42");
            m_TextField.SelectRange(9, 9);

            yield return null;

            m_Window.SendEvent(new Event
            {
                type = EventType.KeyDown,
                keyCode = KeyCode.Space,
                modifiers = EventModifiers.Control,
            });
            m_Window.SendEvent(new Event
            {
                type = EventType.KeyDown,
                keyCode = KeyCode.DownArrow
            });
            m_Window.SendEvent(new Event
            {
                type = EventType.KeyDown,
                keyCode = KeyCode.Return
            });

            Assert.That(m_TextField.text, Is.EqualTo("Hello World 42"));
        }

        class TestCompletionBehavior : AutoComplete.IAutoCompleteBehavior
        {
            static readonly Regex k_Regex = new Regex(@"\b(?<word>(\S)*)$", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture);
            static TestCompletionBehavior s_Instance;

            TestCompletionBehavior() { }

            public static TestCompletionBehavior Instance => s_Instance ?? (s_Instance = new TestCompletionBehavior());

            public List<string> CompletionItems = new List<string>();

            public bool ShouldStartAutoCompletion(string input, int caretPosition)
            {
                return GetToken(input, caretPosition).Length >= 1;
            }

            public string GetToken(string input, int caretPosition)
            {
                var match = k_Regex.Match(input, 0, caretPosition);
                if (!match.Success)
                    return string.Empty;

                var type = match.Groups["word"];
                return type.Value;
            }

            public IEnumerable<string> GetCompletionItems(string input, int caretPosition)
            {
                var token = GetToken(input, caretPosition);
                return CompletionItems.Where(s => s.StartsWith(token, StringComparison.OrdinalIgnoreCase));
            }

            public (string newInput, int caretPosition) OnCompletion(string completedToken, string input, int caretPosition)
            {
                var match = k_Regex.Match(input, 0, caretPosition);
                var componentType = match.Groups["word"];

                var final = input.Substring(0, componentType.Index) + completedToken + input.Substring(componentType.Index + componentType.Length);
                return (final, componentType.Index + completedToken.Length);
            }
        }
    }

    public class AutoCompleteTestCase
    {
        public string token;
        public bool shouldAutoComplete;

        public string input;
        public string expectedToken;


        public override string ToString()
        {
            if (token != null)
                return $"{token} -> {shouldAutoComplete}";
            return $"{input} -> {expectedToken}";
        }
    }

    class AutoCompleteComponentTests
    {        
        static AutoCompleteTestCase[] testCases = new []
        {
            new AutoCompleteTestCase() { token = "c", shouldAutoComplete = false },
            new AutoCompleteTestCase() { token = "C", shouldAutoComplete = false },

            new AutoCompleteTestCase() { token = "c=", shouldAutoComplete = false },
            new AutoCompleteTestCase() { token = "c=a", shouldAutoComplete = true },
            new AutoCompleteTestCase() { token = "c=acc", shouldAutoComplete = true },
            new AutoCompleteTestCase() { token = "C=", shouldAutoComplete = false },
            new AutoCompleteTestCase() { token = "C=acc", shouldAutoComplete = true },

            new AutoCompleteTestCase() { token = "c:", shouldAutoComplete = false },
            new AutoCompleteTestCase() { token = "c:a", shouldAutoComplete = false },
            new AutoCompleteTestCase() { token = "C:", shouldAutoComplete = false },
            new AutoCompleteTestCase() { token = "C:a", shouldAutoComplete = false },

            new AutoCompleteTestCase() { token = "k=a", shouldAutoComplete = false },
            new AutoCompleteTestCase() { token = "dummy=a", shouldAutoComplete = false },
        };

        [Test]
        public void ShouldAutocompleteType([ValueSource(nameof(testCases))] AutoCompleteTestCase testCase)
        {
            var autoComplete = ComponentTypeAutoComplete.Instance;
            Assert.AreEqual(autoComplete.ShouldStartAutoCompletion(testCase.token, testCase.token.Length), testCase.shouldAutoComplete);
        }

        static AutoCompleteTestCase[] advancedTestCases = new[]
        {
            new AutoCompleteTestCase() { token = "c=a", shouldAutoComplete = true },
            new AutoCompleteTestCase() { token = "c=acc", shouldAutoComplete = true },
            new AutoCompleteTestCase() { token = "C=acc", shouldAutoComplete = true },

            new AutoCompleteTestCase() { token = "none=acc", shouldAutoComplete = true },
            new AutoCompleteTestCase() { token = "nOne=acc", shouldAutoComplete = true },

            new AutoCompleteTestCase() { token = "any=acc", shouldAutoComplete = true },
            new AutoCompleteTestCase() { token = "Any=acc", shouldAutoComplete = true },

            new AutoCompleteTestCase() { token = "all=acc", shouldAutoComplete = true },
            new AutoCompleteTestCase() { token = "aLL=acc", shouldAutoComplete = true },

            new AutoCompleteTestCase() { token = "none:acc", shouldAutoComplete = false },
            new AutoCompleteTestCase() { token = "all:acc", shouldAutoComplete = false },
            new AutoCompleteTestCase() { token = "any:acc", shouldAutoComplete = false },

            new AutoCompleteTestCase() { token = "c:a", shouldAutoComplete = false },
            new AutoCompleteTestCase() { token = "C:a", shouldAutoComplete = false },

            new AutoCompleteTestCase() { token = "c", shouldAutoComplete = false },
            new AutoCompleteTestCase() { token = "C", shouldAutoComplete = false },

            new AutoCompleteTestCase() { token = "c=", shouldAutoComplete = false },
            new AutoCompleteTestCase() { token = "C=", shouldAutoComplete = false },

            new AutoCompleteTestCase() { token = "c:", shouldAutoComplete = false },
            new AutoCompleteTestCase() { token = "C:", shouldAutoComplete = false },

            new AutoCompleteTestCase() { token = "k=a", shouldAutoComplete = false },
            new AutoCompleteTestCase() { token = "dummy=a", shouldAutoComplete = false },
        };

        [Test]
        public void ShouldAutocompleteTypeAdvance([ValueSource(nameof(advancedTestCases))] AutoCompleteTestCase testCase)
        {
            var autoComplete = ComponentTypeAutoComplete.EntityQueryInstance;
            Assert.AreEqual(autoComplete.ShouldStartAutoCompletion(testCase.token, testCase.token.Length), testCase.shouldAutoComplete);
        }

        static AutoCompleteTestCase[] sharedComponentCases = new[]
        {
            new AutoCompleteTestCase() { token = "all=", shouldAutoComplete = false },
            new AutoCompleteTestCase() { token = "#", shouldAutoComplete = true },
            new AutoCompleteTestCase() { token = "#u", shouldAutoComplete = true },
        };

        [Test]
        public void ShouldAutocompleteSharedComponent([ValueSource(nameof(sharedComponentCases))] AutoCompleteTestCase testCase)
        {
            var autoComplete = SharedComponentTypeAutoComplete.Instance;
            Assert.AreEqual(autoComplete.ShouldStartAutoCompletion(testCase.token, testCase.token.Length), testCase.shouldAutoComplete);
        }

        static AutoCompleteTestCase[] typeTokenCases = new[]
        {
            new AutoCompleteTestCase() { input = "all=Search", expectedToken = "Search" },
            new AutoCompleteTestCase() { input = "all=ScriptableObject all=Boid", expectedToken = "Boid" },
        };

        [Test]
        public void GetTokenTypeAdvance([ValueSource(nameof(typeTokenCases))] AutoCompleteTestCase testCase)
        {
            var autoComplete = ComponentTypeAutoComplete.EntityQueryInstance;
            Assert.AreEqual(autoComplete.GetToken(testCase.input, testCase.input.Length), testCase.expectedToken);
        }

        static AutoCompleteTestCase[] sharedComponentTokenCases = new[]
        {
            new AutoCompleteTestCase() { input = "#Search", expectedToken = "Search" },
            new AutoCompleteTestCase() { input = "#SearchTestSharedComponent.intValue=0 #Boid", expectedToken = "Boid" },
        };

        [Test]
        public void GetTokenSharedComponent([ValueSource(nameof(sharedComponentTokenCases))] AutoCompleteTestCase testCase)
        {
            var autoComplete = SharedComponentTypeAutoComplete.Instance;
            Assert.AreEqual(autoComplete.GetToken(testCase.input, testCase.input.Length), testCase.expectedToken);
        }
    }
}
