using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Unity.Properties;
using UnityEditor;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// Interface that allows to declare a class type as a setting.
    /// </summary>
    interface ISetting
    {
        /// <summary>
        /// Method called when a change is detected in the UI.
        /// </summary>
        /// <param name="path">Path to the changed property.</param>
        void OnSettingChanged(PropertyPath path);

        /// <summary>
        /// Get the searchable keywords in this Settings group.
        /// </summary>
        /// <returns></returns>
        string[] GetSearchKeywords();

        static IEnumerable<string> GetSearchKeywordsFromProperties(System.Type type)
        {
            return type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(prop => ObjectNames.NicifyVariableName(prop.Name));
        }

        static IEnumerable<string> GetSearchKeywordsFromFields(System.Type type)
        {
            return type.GetFields(BindingFlags.Instance | BindingFlags.Public).Select(prop => ObjectNames.NicifyVariableName(prop.Name));
        }

        static string[] GetSearchKeywordsFromType(System.Type type)
        {
            return GetSearchKeywordsFromProperties(type).Concat(GetSearchKeywordsFromFields(type)).ToArray();
        }
    }
}
