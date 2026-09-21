using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace PugMod
{
	[InitializeOnLoad]
	public static class AssetPackager
	{
		internal const string ASSET_PACKAGE_NAME = "dev.pugstorm.corekeeper.assets";
		private const string ASSET_PACKAGE_DISPLAY_NAME = "Core Keeper Assets";
		private const string ASSET_PACKAGE_VERSION = "1.3.0"; // TODO: Get from AssetRipper files using GameVersionReader (not tested)

		private static string PackageDestination =>
			Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Packages", ASSET_PACKAGE_NAME);

		static AssetPackager()
		{
			EditorApplication.delayCall += CheckForPendingPackaging;
			ScriptableDataEditorUtility.onDataBlockCacheInvalidated += ClearDataBlockCaches;
		}

		private static void CheckForPendingPackaging()
		{
			if (EditorPrefs.GetBool(ModSDKWindow.UpdateAssets.PENDING_PACKAGING_FLAG, false))
			{
				EditorPrefs.DeleteKey(ModSDKWindow.UpdateAssets.PENDING_PACKAGING_FLAG);
				InstallEmbeddedPackage(Path.GetFullPath(ModSDKWindow.UpdateAssets.TEMP_IMPORT_PATH));
			}
		}

		private static void InstallEmbeddedPackage(string packageSourcePath)
		{
			if (!Directory.Exists(packageSourcePath))
			{
				return;
			}

			EditorUtility.DisplayProgressBar("Packaging Assets", "installing new package...", 0.75f);
#if PUG_MOD_SDK
			ImportedGameAssetsReadOnly.AllowInternalWrites = true;
#endif
			try
			{
				var jsonPath = Path.Combine(packageSourcePath, "package.json");
				var jsonContent = $@"{{
				""name"": ""{ASSET_PACKAGE_NAME}"", ""version"": ""{ASSET_PACKAGE_VERSION}"",
				""displayName"": ""{ASSET_PACKAGE_DISPLAY_NAME}"", ""description"": ""Core Keeper game assets."",
				""unity"": ""2021.3"", ""hideInEditor"": false
				}}";
				File.WriteAllText(jsonPath, jsonContent);

				var destination = PackageDestination;
				if (Directory.Exists(destination))
				{
					Directory.Delete(destination, true);
				}
				if (File.Exists(destination + ".meta"))
				{
					File.Delete(destination + ".meta");
				}

				Directory.Move(packageSourcePath, destination);
				if (File.Exists(packageSourcePath + ".meta"))
				{
					File.Delete(packageSourcePath + ".meta");
				}

				AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
				UnityEditor.PackageManager.Client.Resolve();

				// Hide these layers in the Scene view by default after importing game assets.
				foreach (var layerName in new[] { "ShadowsOnly", "TransparentFX", "CeilingHole" })
				{
					var layer = LayerMask.NameToLayer(layerName);
					if (layer >= 0)
					{
						Tools.visibleLayers &= ~(1 << layer);
					}
				}
				SceneView.RepaintAll();

				EditorUtility.DisplayDialog("Success", "Core Keeper assets have been packaged and installed!", "Ok");
				EditorUtility.RequestScriptReload();
			}
			finally
			{
#if PUG_MOD_SDK
				ImportedGameAssetsReadOnly.AllowInternalWrites = false;
#endif
				EditorUtility.ClearProgressBar();
			}
		}

		// TODO: Move this somewhere else (separate package utility?)
		public static void RemoveExistingPackage(Action<bool> callback)
		{
#if PUG_MOD_SDK
			ImportedGameAssetsReadOnly.AllowInternalWrites = true;
#endif
			try
			{
				if (Directory.Exists(PackageDestination))
				{
					AssetDatabase.DeleteAsset("Packages/" + ASSET_PACKAGE_NAME);
					AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
					ScriptableDataEditorUtility.InvalidateDataBlockCache();
				}
			}
			finally
			{
#if PUG_MOD_SDK
				ImportedGameAssetsReadOnly.AllowInternalWrites = false;
#endif
			}
			callback(true);
		}

		private static void ClearDataBlockCaches()
		{
			ScriptableDataEditorUtility.s_dataBlockCache.Clear();
			ScriptableDataEditorUtility.s_dataBlockCacheT.Clear();
		}

		private static void SetTexturesAsAddressable()
		{
			EditorApplication.update -= SetTexturesAsAddressable;

			var addressableSettings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
			var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { $"Packages/{ASSET_PACKAGE_NAME}/Art" });

			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				addressableSettings.CreateOrMoveEntry(guid, addressableSettings.DefaultGroup).address = Path.GetFileName(path);
			}

			AssetDatabase.SaveAssets();


			EditorUtility.DisplayDialog("Success", "Core Keeper assets have been packaged and installed!", "Ok");
		}
	}
}
