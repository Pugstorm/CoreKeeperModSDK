using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace PugMod
{
    public class GameVersionRegistry : ScriptableObject
    {
        public string currentVersion;
    }

    public static class GameVersionTagRegistry
    {
        private const string AssetPath = "Assets/ModSDK/GameVersionRegistry.asset";
        private const string LegacyVersionTagPrefix = "GameVersion:";

        private static readonly Regex CoreVersionRegex = new(@"\d+\.\d+\.\d+", RegexOptions.Compiled);

        public static string GetCurrentVersion()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameVersionRegistry>(AssetPath);
            return registry != null ? registry.currentVersion : null;
        }

        private static string NormalizeVersion(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return string.Empty;
            }

            var normalized = rawValue.Trim();

            if (normalized.StartsWith(LegacyVersionTagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[LegacyVersionTagPrefix.Length..].Trim();
            }

            var match = CoreVersionRegex.Match(normalized);
            return match.Success ? match.Value : normalized;
        }

        public static void TryRegisterFromGamePath(string gamePath)
        {
            string versionTag = GameVersionReader.TryGetGameVersionTag(gamePath);

            if (string.IsNullOrEmpty(versionTag))
            {
                return;
            }

            versionTag = NormalizeVersion(versionTag);

            if (string.IsNullOrEmpty(versionTag))
            {
                return;
            }

            string directoryPath = Path.GetDirectoryName(AssetPath);

            if (!string.IsNullOrEmpty(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var registry = AssetDatabase.LoadAssetAtPath<GameVersionRegistry>(AssetPath);

            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<GameVersionRegistry>();
                AssetDatabase.CreateAsset(registry, AssetPath);
            }

            if (registry.currentVersion != versionTag)
            {
                registry.currentVersion = versionTag;
                EditorUtility.SetDirty(registry);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
