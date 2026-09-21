using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine; // Optional: Only used for Debug.Log

namespace PugMod
{
	public static class GameVersionReader
	{
        private static readonly Regex VersionRegex = new(@"(?<!\d)\d{1,2}\.\d{1,3}\.\d{1,4}(?!\d)", RegexOptions.Compiled);

        public static string TryGetGameVersionTag(string gamePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
                {
                    Debug.LogError($"[GameVersionReader] Invalid or non-existent game path: {gamePath}");
                    return null;
                }

                string globalGameManagerPath = Path.Combine(gamePath, "globalgamemanagers");

                if (!File.Exists(globalGameManagerPath))
                {
                    foreach (string dataDir in Directory.GetDirectories(gamePath, "*_Data"))
                    {
                        string globalGameManager = Path.Combine(dataDir, "globalgamemanagers");

                        if (File.Exists(globalGameManager))
                        {
                            globalGameManagerPath = globalGameManager;
                            break;
                        }
                    }
                }

                if (!File.Exists(globalGameManagerPath))
                {
                    Debug.LogWarning($"[GameVersionReader] 'globalgamemanagers' file not found in: {gamePath}");
                    return null;
                }

                string content = File.ReadAllText(globalGameManagerPath);

                Match match = VersionRegex.Match(content);

                if (match.Success)
                {
                    return match.Value;
                }

                Debug.LogWarning($"[GameVersionReader] Game version pattern not found inside: {globalGameManagerPath}");
                return null;
            }

            catch (Exception e)
            {
                Debug.LogError($"[GameVersionReader] Error reading version: {e.Message}");
                return null;
            }
        }
    }
}