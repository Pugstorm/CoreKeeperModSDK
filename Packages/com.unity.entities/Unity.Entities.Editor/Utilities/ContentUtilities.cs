using System;
using System.Collections.Generic;
using Unity.Properties;
using Unity.Entities.UI;
using UnityEditor;

namespace Unity.Entities.Editor
{
    static class ContentUtilities
    {
        static readonly Dictionary<Type, string> s_NicifiedSystemNameDict = new Dictionary<Type, string>();

        public static World FindLastWorld(string worldName)
        {
            foreach (var world in World.All)
            {
                if (world.Name != worldName)
                {
                    if (worldName == "Editor World" && world.Name == "Default World")
                    {
                        return world;
                    }

                    if (worldName == "Default World" && world.Name == "Editor World")
                    {
                        return world;
                    }

                    continue;
                }

                return world;
            }

            return null;
        }

        public static string NicifySystemTypeName(Type systemType)
        {
            if (systemType == null)
                return string.Empty;

            if (!s_NicifiedSystemNameDict.TryGetValue(systemType, out var nicifyName))
                s_NicifiedSystemNameDict[systemType] = nicifyName = ObjectNames.NicifyVariableName(TypeUtility.GetTypeDisplayName(systemType).Replace(".", " | "));

            return nicifyName;
        }

        public static void ShowSystemInspectorContent(SystemProxy systemProxy)
        {
            SelectionUtility.ShowInInspector(new SystemContentProvider
            {
                World = systemProxy.World,
                SystemProxy = systemProxy
            }, new InspectorContentParameters
            {
                UseDefaultMargins = false,
                ApplyInspectorStyling = false
            });
        }

        public static void ShowComponentInspectorContent(Type componentType)
        {
            SelectionUtility.ShowInInspector(new ComponentContentProvider
            {
                ComponentType = componentType
            }, new InspectorContentParameters
            {
                ApplyInspectorStyling = false,
                UseDefaultMargins = false
            });
        }

        public static string NicifyTypeName(string typeName)
        {
            return ObjectNames.NicifyVariableName(typeName.Replace("<", "[").Replace(">", "]"))
                .Replace("_", " | ")
                .Replace(".", " | ")
                .Replace("[", "<")
                .Replace("]", ">");
        }
    }
}
