using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace PugMod
{
	public static class AddressablesGUIDRestorer
	{
		// Regex for parsing the extracted catalog text
		private static readonly Regex CatalogMapRegex = new Regex(@"^([a-f0-9]{32})\s->\s(.*)$", RegexOptions.Multiline | RegexOptions.Compiled);

		// Regex for finding the GUID inside the .meta YAML format
		// It captures the "guid: " prefix to ensure we replace the correct line
		private static readonly Regex MetaGuidRegex = new Regex(@"(guid:\s*)([a-f0-9]{32})", RegexOptions.Compiled);

		/// <summary>
		/// Extracts the catalog, finds corresponding files in a folder, and syncs their GUIDs.
		/// </summary>
		public static void RestoreGUIDsFromAddressablesCatalog(string binaryCatalogPath, string targetFolder)
		{
			try
			{
				string catalogExtractedPath = Path.Combine(Application.temporaryCachePath, $"{nameof(AddressablesGUIDRestorer)}_catalog_extracted.txt");
				if (File.Exists(catalogExtractedPath))
				{
					File.Delete(catalogExtractedPath);
				}

				ContentCatalogData.ExtractBinaryCatalog(binaryCatalogPath, catalogExtractedPath);

				if (!File.Exists(catalogExtractedPath))
				{
					Debug.LogError("[RestoreGUIDs] Failed to extract binary catalog.");
					return;
				}

				string content = File.ReadAllText(catalogExtractedPath);
				Dictionary<string, string> fileNameToGuid = ParseCatalogExtracted(content);

				ProcessFolder(targetFolder, fileNameToGuid);
			}
			catch (Exception e)
			{
				Debug.LogError($"[RestoreGUIDs] Failed: {e.Message}");
			}
		}
		
		private static void ProcessFolder(string folderPath, Dictionary<string, string> mappings)
		{
			string[] metaFiles = Directory.GetFiles(folderPath, "*.meta", SearchOption.AllDirectories);
			int changeCount = 0;

			foreach (string metaPath in metaFiles)
			{
				string assetFileName = Path.GetFileNameWithoutExtension(metaPath);

				if (mappings.TryGetValue(assetFileName, out string targetGuid))
				{
					if (UpdateMetaGuid(metaPath, targetGuid))
					{
						changeCount++;
					}
				}
			}
			Debug.Log($"Processed {metaFiles.Length} meta files. Updated {changeCount} GUIDs.");
		}

		/// <summary>
		/// Updates the meta file if the GUID doesn't match the target.
		/// Returns true if the file was actually modified.
		/// </summary>
		private static bool UpdateMetaGuid(string metaPath, string newGuid)
		{
			if (!File.Exists(metaPath)) return false;

			string content = File.ReadAllText(metaPath);
			Match match = MetaGuidRegex.Match(content);

			if (match.Success)
			{
				// Group[2] is the 32-char hex string
				Group guidGroup = match.Groups[2];

				if (guidGroup.Value.Equals(newGuid, StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}
				
				Debug.Log($"Replacing {guidGroup.Value} with {newGuid} in {match.Groups[0].Value}");

				string updatedContent = content.Substring(0, guidGroup.Index) 
					+ newGuid 
					+ content.Substring(guidGroup.Index + guidGroup.Length);

				File.WriteAllText(metaPath, updatedContent);
				return true;
			}

			return false;
		}

		private static Dictionary<string, string> ParseCatalogExtracted(string content)
		{
			var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			var matches = CatalogMapRegex.Matches(content);

			foreach (Match match in matches)
			{
				string guid = match.Groups[1].Value;
				string fullPath = match.Groups[2].Value.Trim();

				string fileName = Path.GetFileName(fullPath);

				if (!string.IsNullOrEmpty(fileName) && !mappings.ContainsKey(fileName))
				{
					mappings.Add(fileName, guid);
				}
			}

			return mappings;
		}
	}
}