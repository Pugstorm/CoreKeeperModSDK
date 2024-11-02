using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Unity.Entities.Editor
{
    class ComponentTypeAutoComplete : AutoComplete.IAutoCompleteBehavior
    {
        static ComponentTypeAutoComplete s_Instance;
        static ComponentTypeAutoComplete s_EntityQueryInstance;
        static string k_FilterToken = $"\\b([{Constants.ComponentSearch.TokenCaseInsensitive}]{Constants.ComponentSearch.Op})(?<componentType>(\\S)*)$";
        static string k_EntityQueryFilterToken = $"\\b([{Constants.ComponentSearch.TokenCaseInsensitive}]|{Constants.ComponentSearch.Any}|{Constants.ComponentSearch.All}|{Constants.ComponentSearch.None}){Constants.ComponentSearch.Op}(?<componentType>(\\S)*)$";
        public static ComponentTypeAutoComplete Instance => s_Instance ?? (s_Instance = new ComponentTypeAutoComplete(false));
        public static ComponentTypeAutoComplete EntityQueryInstance => s_EntityQueryInstance ?? (s_EntityQueryInstance = new ComponentTypeAutoComplete(true));

        Regex m_Regex = new Regex(k_FilterToken, RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

        static ComponentTypeAutoComplete()
            => ComponentTypesTrie.Initialize();

        ComponentTypeAutoComplete(bool supportsEntityQuery)
        {
            m_Regex = new Regex(supportsEntityQuery ? k_EntityQueryFilterToken : k_FilterToken, RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);
        }

        public bool ShouldStartAutoCompletion(string input, int caretPosition)
        {
            return GetToken(input, caretPosition).Length >= 1;
        }

        public string GetToken(string input, int caretPosition)
        {
            var match = m_Regex.Match(input, 0, caretPosition);
            if (!match.Success)
                return string.Empty;
            var type = match.Groups["componentType"];
            return type.Value;
        }

        public IEnumerable<string> GetCompletionItems(string input, int caretPosition)
        {
            var token = GetToken(input, caretPosition);
            return ComponentTypesTrie.SearchType(token);
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
