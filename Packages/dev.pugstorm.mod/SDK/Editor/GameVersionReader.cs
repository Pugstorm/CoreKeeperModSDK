using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine; // Optional: Only used for Debug.Log

namespace PugMod
{
	public static class GameVersionReader
	{
		// Regex to find "bundleVersion: 1.0.0"
		// \s* matches indentation
		// (.*) captures the version string
		private static readonly Regex BundleVersionRegex = new Regex(@"^\s*bundleVersion:\s*(.*)$", RegexOptions.Multiline | RegexOptions.Compiled);

		/// <summary>
		/// Reads the bundleVersion from the ProjectSettings.asset file in the given project folder.
		/// </summary>
		/// <param name="projectRootPath">The root folder of the exported project (containing the Assets and ProjectSettings folders).</param>
		/// <returns>The version string (e.g., "1.0.2") or null if not found.</returns>
		public static string GetGameVersion(string projectRootPath)
		{
			try
			{
				string settingsPath = Path.Combine(projectRootPath, "ProjectSettings", "ProjectSettings.asset");

				if (!File.Exists(settingsPath))
				{
					Debug.LogError($"[GameVersionReader] Could not find settings file at: {settingsPath}");
					return null;
				}

				string content = File.ReadAllText(settingsPath);
				Match match = BundleVersionRegex.Match(content);

				if (match.Success)
				{
					// Group 1 contains the version string
					string version = match.Groups[1].Value.Trim();
					return version;
				}
				else
				{
					Debug.LogWarning($"[GameVersionReader] 'bundleVersion' key not found in {settingsPath}");
					return null;
				}
			}
			catch (Exception e)
			{
				Debug.LogError($"[GameVersionReader] Error reading version: {e.Message}");
				return null;
			}
		}
	}
}