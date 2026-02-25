using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace PugMod
{
	[InitializeOnLoad]
	public static class AssetPackager
	{
		private static PackRequest _packRequest;
		private static AddRequest _addRequest;
		private static RemoveRequest _removeRequest;
		private static ListRequest _listRequest;
		private static Action<bool> _removePackageCallback;

		private const string ASSET_PACKAGE_NAME = "dev.pugstorm.corekeeper.assets";
		private const string ASSET_PACKAGE_DISPLAY_NAME = "Core Keeper Assets";
		private const string ASSET_PACKAGE_VERSION = "1.2.0"; // TODO: Get from AssetRipper files using GameVersionReader (not tested)

		static AssetPackager()
		{
			EditorApplication.delayCall += CheckForPendingPackaging;
		}

		private static void CheckForPendingPackaging()
		{
			if (EditorPrefs.GetBool(ModSDKWindow.UpdateAssets.PENDING_PACKAGING_FLAG, false))
			{
				EditorPrefs.DeleteKey(ModSDKWindow.UpdateAssets.PENDING_PACKAGING_FLAG);
				PackPackage(Path.GetFullPath(ModSDKWindow.UpdateAssets.TEMP_IMPORT_PATH));
			}
		}

		private static void PackPackage(string packageSourcePath)
		{
			if (!Directory.Exists(packageSourcePath))
			{
				return;
			}

			var jsonPath = Path.Combine(packageSourcePath, "package.json");
			var jsonContent = $@"{{
			""name"": ""{ASSET_PACKAGE_NAME}"", ""version"": ""{ASSET_PACKAGE_VERSION}"",
			""displayName"": ""{ASSET_PACKAGE_DISPLAY_NAME}"", ""description"": ""Core Keeper game assets."",
			""unity"": ""2021.3"", ""hideInEditor"": false
			}}";
			File.WriteAllText(jsonPath, jsonContent);

			AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
			var projectRoot = Directory.GetParent(Application.dataPath).FullName;
			var tarballFolder = Path.Combine(projectRoot, "ImportedGameFolders");

			if (!Directory.Exists(tarballFolder))
			{
				Directory.CreateDirectory(tarballFolder);
			}

			Debug.Log($"packing assets from {packageSourcePath} to {tarballFolder}");
			_packRequest = Client.Pack(packageSourcePath, tarballFolder);
			EditorApplication.update += PackProgress;
		}

		private static void PackProgress()
		{
			EditorUtility.DisplayProgressBar("Packaging Assets", "compressing package...", 0.25f);
			if (_packRequest == null || !_packRequest.IsCompleted)
			{
				return;
			}
			EditorApplication.update -= PackProgress;

			if (_packRequest.Status != StatusCode.Success)
			{
				Debug.LogError($"Failed to pack package {_packRequest.Error.message}");
				return;
			}
			
			FileUtil.DeleteFileOrDirectory(ModSDKWindow.UpdateAssets.TEMP_IMPORT_PATH);
			FileUtil.DeleteFileOrDirectory(ModSDKWindow.UpdateAssets.TEMP_IMPORT_PATH + ".meta");//folders are just .meta files iirc? so this should work
			
			// Make sure we don't have any existing files with clashing GUID before installing package
			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
			EditorApplication.delayCall += () =>
			{
				InstallPackage(_packRequest.Result.tarballPath);
			};
		}

		// TODO: Move this somewhere else (separate package utility?)
		public static void RemoveExistingPackage(Action<bool> callback)
		{
			_removePackageCallback = callback;
			_listRequest = Client.List(true);
			EditorApplication.update += CheckExistingPackageProgress;
		}

		private static void CheckExistingPackageProgress()
		{
			EditorUtility.DisplayProgressBar("Packaging Assets", "Removing any existing package", 0f);

			if (_listRequest == null)
			{
				Debug.LogError("List request null unexpectedly");
				EditorApplication.update -= CheckExistingPackageProgress;
				return;
			}
			
			if (!_listRequest.IsCompleted)
			{
				return;
			}

			EditorApplication.update -= CheckExistingPackageProgress;

			if (_listRequest.Status != StatusCode.Success)
			{
				Debug.LogError($"Failed to get package list {_listRequest.Error.message}");
				_listRequest = null;
				_removePackageCallback(false);
				return;
			}
			
			var existingPackage = _listRequest.Result.FirstOrDefault(p => p.name == ASSET_PACKAGE_NAME);
			if (existingPackage == null)
			{
				Debug.Log("No existing package found");
				_listRequest = null;
				_removePackageCallback(true);
				return;
			}
			
			_removeRequest = Client.Remove(ASSET_PACKAGE_NAME);
			EditorApplication.update += RemoveExistingPackageProgress;
		}

		private static void RemoveExistingPackageProgress()
		{
			EditorUtility.DisplayProgressBar("Packaging Assets", "Removing old package...", 0.75f);
			if (_removeRequest == null)
			{
				Debug.LogError("remove request null unexpectedly");
				EditorApplication.update -= RemoveExistingPackageProgress;
				return;
			}
			
			if (!_removeRequest.IsCompleted)
			{
				return;
			}

			EditorApplication.update -= RemoveExistingPackageProgress;

			if (_removeRequest.Status != StatusCode.Success)
			{
				Debug.LogError($"Failed to remove existing package: {_removeRequest.Error.message}");
				_removeRequest = null;
				_removePackageCallback(false);
				return;
			}
			
			Debug.Log($"Removed old package {_removeRequest.PackageIdOrName}");
			
			_removeRequest = null;
			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

			EditorApplication.delayCall += () =>
			{
				_removePackageCallback(true);
			};
		}

		private static void InstallPackage(string tarballPath)
		{
			_addRequest = Client.Add($"file:{tarballPath}");
			EditorApplication.update += InstallProgress;
		}

		private static void InstallProgress()
		{
			EditorUtility.DisplayProgressBar("Packaging Assets", "installing new package...", 0.99f);
			if (_addRequest == null || !_addRequest.IsCompleted)
			{
				return;
			}

			EditorApplication.update -= InstallProgress;

			EditorUtility.ClearProgressBar();
			if (_addRequest.Status == StatusCode.Success)
			{
				EditorUtility.DisplayDialog("Success", "Core Keeper assets have been packaged and installed!", "Ok");
				//Debug.Log("Core Keeper assets have been packaged and installed, setting art textures as addressables next");
				//EditorApplication.update += SetTexturesAsAddressable;
			}
        }

		//private static void SetTexturesAsAddressable()
		//{
		//	EditorApplication.update -= SetTexturesAsAddressable;

		//	var addressableSettings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
		//	var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { $"Packages/{ASSET_PACKAGE_NAME}/Art" });

		//	foreach (var guid in guids)
		//	{
		//		var path = AssetDatabase.GUIDToAssetPath(guid);
		//		addressableSettings.CreateOrMoveEntry(guid, addressableSettings.DefaultGroup).address = Path.GetFileName(path);
		//	}

		//	AssetDatabase.SaveAssets();


		//	EditorUtility.DisplayDialog("Success", "Core Keeper assets have been packaged and installed!", "Ok");
		//}
	}
}
