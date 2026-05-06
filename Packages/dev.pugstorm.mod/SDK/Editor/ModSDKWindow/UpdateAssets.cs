using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

            private readonly List<string> _sdkAssemblyMetaFilesToCopy = new List<string>
            {
                "PugSprite.dll.meta",
                "ScriptableData.dll.meta",
            };

            private DropdownField _assetRipperPathDropDown;
            private Button _browseButton;
            private Button _updateAssetsButton;

            private static readonly Regex GUIDRegex = new(@"guid:\s*([a-f0-9]{32})", RegexOptions.Compiled);

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
                        Path.Combine(TEMP_IMPORT_PATH, "Data"),
                        Path.Combine(TEMP_IMPORT_PATH, "Art"),
                        Path.Combine(TEMP_IMPORT_PATH, "Prefabs"),
                        Path.Combine(TEMP_IMPORT_PATH, "Shader"),
                        Path.Combine(TEMP_IMPORT_PATH, "Material"),
                    };

                    foreach (var folder in foldersToFixGUIDFor)
                    {
                        AddressablesGUIDRestorer.RestoreGUIDsFromAddressablesCatalog(
                        Path.Combine(assetRipperPath, ImporterSettings.Instance.assetRipperAddressablesCatalogPath), folder);
                    }

                    var scriptRemaps = RemapScripts();

                    string[] prefabsToRemap = Directory.GetFiles(TEMP_IMPORT_PATH, "*.*", SearchOption.AllDirectories).Where(prefab => prefab.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)).ToArray();

                    foreach (string prefab in prefabsToRemap)
                    {
                        string prefabYAML = File.ReadAllText(prefab);
                        bool modifiedPrefab = false;

                        foreach (var remap in scriptRemaps)
                        {
                            if (prefabYAML.Contains(remap.Key))
                            {
                                prefabYAML = prefabYAML.Replace(remap.Key, remap.Value);
                                modifiedPrefab = true;
                            }
                        }

                        if (modifiedPrefab)
                        {
                            File.WriteAllText(prefab, prefabYAML);
                        }
                    }
                    EditorPrefs.SetBool(PENDING_PACKAGING_FLAG, true);
                }
                catch (Exception ex)
                {
                    EditorPrefs.DeleteKey(PENDING_PACKAGING_FLAG);
                    ShowError($"An error occurred during asset processing: {ex.Message}");
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                }
            }
            private Dictionary<string, string> RemapScripts() // For non-compiled assemblies we can't import their .dll.meta files to fix references, so we do this instead.
            {
                return new Dictionary<string, string>()
                {
                    // GhostAuthoringComponent
                    { "fileID: 1928767635, guid: ec628a550dac988fecc0ebcb3605f68c", "fileID: 11500000, guid: 7c79d771cedb4794bf100ce60df5f764" },
                    
                    // PhysicsShapeAuthoring
                    { "fileID: -2046304025, guid: e9c46758b721c46dac5f2259b9351b17", "fileID: 11500000, guid: b275e5f92732148048d7b77e264ac30e" },
                    
                    // LinkedEntityGroupAuthoring
                    { "fileID: 1626740080, guid: fbbadae9385c14e32dcf65cf1eba05dd", "fileID: 11500000, guid: c16549610bfe4458aa9389201d072bb6" },

                    // Physics Body
                    { "fileID: -327389624, guid: e9c46758b721c46dac5f2259b9351b17", "fileID: 11500000, guid: ccea9ea98e38942e0b0938c27ed1903e" },

                    // Ghost Authoring Inspection Component
                    { "fileID: 2065810304, guid: ec628a550dac988fecc0ebcb3605f68c", "fileID: 11500000, guid: bfdaa6c06fe64fbda2b16e07a4ee0b25" },
                };
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
                    { settings.assetRipperPrefabsPath, "Prefabs" },
                    { settings.assetRipperShadersPath, "Shader" },
                    { settings.assetRipperMaterialsPath, "Material" },
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

                var guidRemap = new Dictionary<string, string>();
                var sdkDestinationPath = settings.sdkAssemblyPath;
                foreach (var metaFileName in _sdkAssemblyMetaFilesToCopy)
                {
                    var sourcePath = Path.Combine(auxiliaryFilesPath, metaFileName);
                    var destPath = Path.Combine(sdkDestinationPath, metaFileName);
                    if (File.Exists(sourcePath))
                    {
                        var oldGUID = GetGUIDFromMetaFile(destPath);
                        File.Copy(sourcePath, destPath, true);
                        var newGUID = GetGUIDFromMetaFile(destPath);

                        if (!string.IsNullOrEmpty(oldGUID) && !string.IsNullOrEmpty(newGUID) && oldGUID != newGUID)
                        {
                            guidRemap[oldGUID] = newGUID;
                        }
                    }
                }

                var gameDestinationPath = settings.gameAssemblyPath;
                if (Directory.Exists(gameDestinationPath))
                {
                    string[] gameDlls = Directory.GetFiles(gameDestinationPath, "*.dll");

                    foreach (string dllPath in gameDlls)
                    {
                        string metaFileName = Path.GetFileName(dllPath) + ".meta";
                        var sourcePath = Path.Combine(auxiliaryFilesPath, metaFileName);
                        var destPath = Path.Combine(gameDestinationPath, metaFileName);

                        if (File.Exists(sourcePath))
                        {
                            var oldGuid = GetGUIDFromMetaFile(destPath);
                            File.Copy(sourcePath, destPath, true);
                            var newGuid = GetGUIDFromMetaFile(destPath);

                            if (!string.IsNullOrEmpty(oldGuid) && !string.IsNullOrEmpty(newGuid) && oldGuid != newGuid)
                                guidRemap[oldGuid] = newGuid;
                        }
                    }
                }

                if (guidRemap.Count > 0)
                {
                    RemapGUIDs("Assets", guidRemap);
                }
            }

            public static string GetGUIDFromMetaFile(string metaFilePath)
            {
                if (!File.Exists(metaFilePath))
                {
                    return null;
                }

                foreach (var line in File.ReadLines(metaFilePath))
                {
                    var match = GUIDRegex.Match(line);

                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
                return null;
            }

            public static void RemapGUIDs(string rootFolder, Dictionary<string, string> guidRemap)
            {
                if (guidRemap == null || guidRemap.Count == 0)
                {
                    return;
                }

                var importedAssets = Directory.GetFiles(rootFolder, "*.*", SearchOption.AllDirectories);
                int guidsSwapped = 0;

                foreach (var asset in importedAssets)
                {
                    if (!asset.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var yaml = File.ReadAllText(asset);
                    bool swappedGUID = false;

                    foreach (var guid in guidRemap)
                    {
                        var newYaml = yaml.Replace($"guid: {guid.Key}", $"guid: {guid.Value}");
                        if (newYaml != yaml)
                        {
                            yaml = newYaml;
                            swappedGUID = true;
                        }
                    }

                    if (swappedGUID)
                    {
                        File.WriteAllText(asset, yaml);
                        guidsSwapped++;
                    }
                }

                if (guidsSwapped > 0)
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                }
            }

            private void ShowError(string message)
            {
                EditorUtility.DisplayDialog("Asset Update Error", message, "OK");
            }
        }
    }
}
