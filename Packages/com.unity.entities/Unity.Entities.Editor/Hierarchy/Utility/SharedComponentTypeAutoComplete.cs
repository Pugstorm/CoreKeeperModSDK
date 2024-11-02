using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.Entities.Editor;
using UnityEngine;

namespace Unity.Entities.Editor
{
    class SharedComponentTypeAutoComplete : AutoComplete.IAutoCompleteBehavior
    {
        static SharedComponentTypeAutoComplete s_Instance;
        static string k_FilterToken = @"#(?<componentType>\S*)$";
        public static SharedComponentTypeAutoComplete Instance => s_Instance ?? (s_Instance = new SharedComponentTypeAutoComplete());
        private static Regex m_Regex = new Regex(k_FilterToken, RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

        static SharedComponentTypeAutoComplete()
            => SharedComponentTypesTrie.Initialize();

        public bool ShouldStartAutoCompletion(string input, int caretPosition)
        {            
            return GetToken(input, caretPosition) != null;
        }

        public IEnumerable<string> GetCompletionItems(string input, int caretPosition)
        {
            var token = GetToken(input, caretPosition);
            return SharedComponentTypesTrie.SearchType(token);
        }

        public string GetToken(string input, int caretPosition)
        {
            var match = m_Regex.Match(input, 0, caretPosition);
            if (!match.Success)
                return null;
            var type = match.Groups["componentType"];
            return type.Value;
        }

        public (string newInput, int caretPosition) OnCompletion(string completedToken, string input, int caretPosition)
        {
            var match = m_Regex.Match(input, 0, caretPosition);
            var componentType = match.Groups["componentType"];

            var indexOfNextSpace = input.IndexOf(' ', componentType.Index);
            var final = string.Concat(input.Substring(0, componentType.Index), completedToken);
            if (indexOfNextSpace != -1)
                final += input.Substring(indexOfNextSpace);

            return (final, componentType.Index + completedToken.Length);
        }
    }

}
