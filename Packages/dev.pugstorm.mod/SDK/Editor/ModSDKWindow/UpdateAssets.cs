using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PugMod
{
    public partial class ModSDKWindow
    {
        public class UpdateAssets
        {
            public const string PENDING_PACKAGING_FLAG = "PugMod.PendingPackaging";
            private const string ASSET_RIPPER_PATH_KEY = "PugMod/SDKWindow/AssetRipperPath";
            public const string TEMP_IMPORT_PATH = "Assets/ImportedGameAssets_Temp";

            private readonly List<string> _gameAssemblyMetaFilesToCopy = new List<string>
            {
                "Pug.Other.dll.meta",
                "Pug.Base.dll.meta",
                "Pug.Changelog.dll.meta",

            };

            private readonly List<string> _sdkAssemblyMetaFilesToCopy = new List<string>
            {
                "PugSprite.dll.meta",
                "ScriptableData.dll.meta",
            };

            private DropdownField _assetRipperPathDropDown;
            private Button _browseButton;
            private Button _updateAssetsButton;

            public void OnEnable(VisualElement root)
            {
                _assetRipperPathDropDown = root.Q<DropdownField>("UpdateAssetsChooseAssetRipperPath");
                _browseButton = root.Q<Button>("UpdateAssetsChooseGamePathManually");
                _updateAssetsButton = root.Q<Button>("UpdateAssetsUpdateButton");
                _assetRipperPathDropDown.choices = new List<string>();

                if (EditorPrefs.HasKey(ASSET_RIPPER_PATH_KEY))
                {
                    var chosenPath = EditorPrefs.GetString(ASSET_RIPPER_PATH_KEY);
                    if (!string.IsNullOrEmpty(chosenPath) && VerifyPath(chosenPath, true))
                    {
                        if (!_assetRipperPathDropDown.choices.Contains(chosenPath))
                        {
                            _assetRipperPathDropDown.choices.Add(chosenPath);
                        }
                        _assetRipperPathDropDown.index = _assetRipperPathDropDown.choices.IndexOf(chosenPath);
                    }
                }

                _assetRipperPathDropDown.RegisterCallback<ChangeEvent<string>>(evt =>
                {
                    if (string.IsNullOrEmpty(evt.newValue))
                    {
                        EditorPrefs.DeleteKey(ASSET_RIPPER_PATH_KEY);
                        return;
                    }
                    EditorPrefs.SetString(ASSET_RIPPER_PATH_KEY, evt.newValue);
                });

                _browseButton.clicked += OpenFolderPanel;
                _updateAssetsButton.clicked += UpdateAssetsFromAssetRipper;
            }

            private void OpenFolderPanel()
            {
                string selectedPath = EditorUtility.OpenFolderPanel("Select AssetRipper folder", "", "");

                if (Path.GetFileName(selectedPath) == "ExportedProject")
                {
                    selectedPath = Path.GetDirectoryName(selectedPath);
                }

                if (string.IsNullOrEmpty(selectedPath) || !VerifyPath(selectedPath))
                {
                    return;
                }

                DirectoryInfo dirInfo = new DirectoryInfo(selectedPath);
                if (!_assetRipperPathDropDown.choices.Contains(dirInfo.FullName))
                {
                    var choices = _assetRipperPathDropDown.choices;
                    choices.Add(dirInfo.FullName);
                    choices.Sort();
                    _assetRipperPathDropDown.choices = choices;
                }
                _assetRipperPathDropDown.index = _assetRipperPathDropDown.choices.IndexOf(dirInfo.FullName);
            }

            private bool VerifyPath(string path, bool silent = false)
            {
                if (!Directory.Exists(path)) return false;

                var exportedProjectPath = Path.Combine(path, "ExportedProject");
                if (!Directory.Exists(exportedProjectPath)) return false;

                var assetsPath = Path.Combine(exportedProjectPath, "Assets");
                return Directory.Exists(assetsPath);
            }

            private void UpdateAssetsFromAssetRipper()
            {
                var settings = ImporterSettings.Instance;
                if (settings == null)
                {
                    Debug.LogError("No ImporterSettings instance");
                    return;
                }

                var assetRipperPath = _assetRipperPathDropDown.text;

                if (string.IsNullOrEmpty(assetRipperPath) || !VerifyPath(assetRipperPath))
                {
                    ShowError("Please select a valid AssetRipper path first.");
                    return;
                }

                // Need to remove old package first to avoid any GUID collisions
                AssetPackager.RemoveExistingPackage((success =>
                {
                    if (!success)
                    {
                        ShowError("Error during package removal");
                        return;
                    }

                    ImportAssets(assetRipperPath);
                }));
            }

            private void ImportAssets(string assetRipperPath)
            {
                Debug.Log("Start AssetRipper import");

                // TODO: We might be able to speed up this whole section by wrapping everything in StartAssetEditing,
                // unless the AssetDatabase.Refresh calls are required for some reason.
                try
                {
                    CopyAssemblyMetaFiles(assetRipperPath);

                    if (Directory.Exists(Path.GetFullPath(TEMP_IMPORT_PATH)))
                    {
                        FileUtil.DeleteFileOrDirectory(TEMP_IMPORT_PATH);
                        AssetDatabase.Refresh();
                    }

                    Directory.CreateDirectory(Path.GetFullPath(TEMP_IMPORT_PATH));
                    CopyAssetFolders(assetRipperPath, TEMP_IMPORT_PATH);

                    AssetDatabase.Refresh();

                    ScriptableDataEditorUtility.AddContext(Path.Combine(TEMP_IMPORT_PATH, "Data"), "Core Keeper Assets");
                }
                catch (Exception ex)
                {
                    EditorPrefs.DeleteKey(PENDING_PACKAGING_FLAG);
                    ShowError($"An error occurred during asset import: {ex.Message}");
                    return;
                }

                AssetDatabase.StartAssetEditing();
                try
                {
                    var foldersToFixGUIDFor = new List<string>
                    {
						// Skip "fallback" paths like Texture2D, GameObject or we might get duplicates
						Path.Combine(TEMP_IMPORT_PATH, "Data" ),
                        Path.Combine(TEMP_IMPORT_PATH, "Art" ),
                        Path.Combine(TEMP_IMPORT_PATH, "Prefabs" ),
                    };

                    foreach (var folder in foldersToFixGUIDFor)
                    {
                        AddressablesGUIDRestorer.RestoreGUIDsFromAddressablesCatalog(
                            Path.Combine(assetRipperPath, ImporterSettings.Instance.assetRipperAddressablesCatalogPath), folder);
                    }

                    EditorPrefs.SetBool(PENDING_PACKAGING_FLAG, true);
                }
                catch (Exception ex)
                {
                    EditorPrefs.DeleteKey(PENDING_PACKAGING_FLAG);
                    ShowError($"An error occurred during addressables guid parsing: {ex.Message}");
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                    AssetDatabase.Refresh();
                }
            }

            private void CopyAssetFolders(string assetRipperPath, string destinationRootRelative)
            {
                var settings = ImporterSettings.Instance;
                if (settings == null)
                {
                    return;
                }

                var foldersToCopy = new Dictionary<string, string>
                {
                    { settings.assetRipperDataPath, "Data" },
                    { settings.assetRipperArtPath, "Art" },
                    { settings.assetRipperSpritePath, "Sprite" },
                    { settings.assetRipperTexture2DPath, "Texture2D" },
                    { settings.assetRipperEquipmentSlotPrefabsPath, "Prefabs" },
                };

                foreach (var folderPair in foldersToCopy)
                {
                    var sourcePath = Path.Combine(assetRipperPath, folderPair.Key).Replace('\\', '/');
                    var destPath = Path.GetFullPath(Path.Combine(destinationRootRelative, folderPair.Value)).Replace('\\', '/'); //we do this because Unity is sensitive to forward/backward slashes

                    if (Directory.Exists(sourcePath))
                    {
                        FileUtil.CopyFileOrDirectory(sourcePath, destPath);

                        if (folderPair.Value == "Data")
                        {
                            var foldersToRemove = new List<string>
                            {
                                "ChangelogCategoryDataBlock",
                                "ChangelogCollection",
                                "ChangelogEntryDataBlock",
                                "ChangelogTargetPlatformDataBlock",
                                "ContentBundleDataBlock",
                            };

                            foreach (var folder in foldersToRemove)
                            {
                                var folderPath = Path.Combine(destPath, folder);

                                if (Directory.Exists(folderPath))
                                {
                                    Directory.Delete(folderPath, true);
                                }

                                var metaPath = folderPath + ".meta";

                                if (File.Exists(metaPath))
                                {
                                    File.Delete(metaPath);
                                }
                            }
                        }

                        if (folderPair.Value == "Texture2D")
                        {
                            foreach (var texture in Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories))
                            {
                                if (texture.EndsWith(".meta"))
                                {
                                    continue;
                                }

                                var textureName = Path.GetFileNameWithoutExtension(texture);

                                if (textureName.EndsWith("_0") || textureName.EndsWith("_1"))
                                {
                                    var newTexturePath = Path.Combine(Path.GetDirectoryName(texture), textureName.Substring(0, textureName.Length - 2) + Path.GetExtension(texture));
                                    var textureMetaFile = texture + ".meta";
                                    var newMetaPath = newTexturePath + ".meta";

                                    if (File.Exists(newTexturePath))
                                    {
                                        File.Delete(newTexturePath);
                                    }

                                    if (File.Exists(newMetaPath))
                                    {
                                        File.Delete(newMetaPath);
                                    }

                                    File.Move(texture, newTexturePath);

                                    if (File.Exists(textureMetaFile)) File.Move(textureMetaFile, newMetaPath);
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.Log($"Directory not found skipping {sourcePath}");
                    }
                }
            }

            private void CopyAssemblyMetaFiles(string assetRipperPath)
            {
                var settings = ImporterSettings.Instance;
                if (settings == null) return;

                var auxiliaryFilesPath = Path.Combine(assetRipperPath, settings.assetRipperAssembliesPath);
                if (!Directory.Exists(auxiliaryFilesPath)) return;

                var sdkDestinationPath = settings.sdkAssemblyPath;
                foreach (var metaFileName in _sdkAssemblyMetaFilesToCopy)
                {
                    var sourcePath = Path.Combine(auxiliaryFilesPath, metaFileName);
                    var destPath = Path.Combine(sdkDestinationPath, metaFileName);
                    if (File.Exists(sourcePath))
                    {
                        File.Copy(sourcePath, destPath, true);
                    }
                }

                var gameDestinationPath = settings.gameAssemblyPath;
                foreach (var metaFileName in _gameAssemblyMetaFilesToCopy)
                {
                    var sourcePath = Path.Combine(auxiliaryFilesPath, metaFileName);
                    var destPath = Path.Combine(gameDestinationPath, metaFileName);
                    if (File.Exists(sourcePath))
                    {
                        File.Copy(sourcePath, destPath, true);
                    }
                }
            }

            private void ShowError(string message)
            {
                EditorUtility.DisplayDialog("Asset Update Error", message, "OK");
            }
        }
    }
}
