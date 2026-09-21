#if PUG_MOD_SDK
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PugMod
{
	internal sealed class ImportedGameAssetsReadOnly : AssetModificationProcessor
	{
		internal const string ProtectedRoot = "Packages/" + AssetPackager.ASSET_PACKAGE_NAME;

		private const string LockedMessage =
			"This asset is part of the imported Core Keeper base game assets and is read-only. " +
			"Modify a copy of it in your own mod instead of editing it directly.";

		// Set by AssetPackager while it performs its own controlled writes to ProtectedRoot.
		internal static bool AllowInternalWrites;

		private static bool IsProtected(string path)
		{
			if (string.IsNullOrEmpty(path)) return false;
			var normalized = path.Replace('\\', '/');
			return normalized.Equals(ProtectedRoot, StringComparison.OrdinalIgnoreCase) ||
				normalized.StartsWith(ProtectedRoot + "/", StringComparison.OrdinalIgnoreCase);
		}

		// Controls the read-only padlock overlay and whether the Inspector allows editing.
		public static bool IsOpenForEdit(string assetOrMetaFilePath, out string message)
		{
			message = null;
			if (AllowInternalWrites || !IsProtected(assetOrMetaFilePath)) return true;
			message = LockedMessage;
			return false;
		}

		private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
		{
			if (!AllowInternalWrites && IsProtected(assetPath))
			{
				Debug.LogWarning($"[Asset Update] Blocked delete of read-only asset: {assetPath}");
				return AssetDeleteResult.FailedDelete;
			}
			return AssetDeleteResult.DidNotDelete;
		}

		private static AssetMoveResult OnWillMoveAsset(string sourcePath, string destinationPath, string[] renameConflicts, string[] deleteConflicts)
		{
			if (!AllowInternalWrites && (IsProtected(sourcePath) || IsProtected(destinationPath)))
			{
				Debug.LogWarning($"[Asset Update] Blocked move of read-only asset: {sourcePath} -> {destinationPath}");
				return AssetMoveResult.FailedMove;
			}
			return AssetMoveResult.DidNotMove;
		}

		private static string[] OnWillSaveAssets(string[] paths)
		{
			if (AllowInternalWrites || paths == null || paths.Length == 0) return paths;

			var blockedAny = false;
			foreach (var path in paths)
			{
				if (!IsProtected(path)) continue;
				blockedAny = true;
				Debug.LogWarning($"[Asset Update] Blocked save of read-only asset: {path}");
			}
			if (!blockedAny) return paths;

			var allowed = new List<string>(paths.Length);
			foreach (var path in paths)
			{
				if (!IsProtected(path)) allowed.Add(path);
			}
			return allowed.ToArray();
		}
	}
}
#endif