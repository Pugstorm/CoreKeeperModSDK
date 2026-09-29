using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace PugMod
{
	public partial class ModSDKWindow
	{
		private class SteamWorkshopTab
		{
			private VisualElement _steamWorkshopView;
			private Button _steamConfigButton;

			private DropdownField _steamModList;
			private DropdownField _steamVisibility;
			private DropdownField _steamWorkshopTags;

			private VisualElement _steamWorkshopTagsList;

			private Button _steamUploadButton;
			private Button _steamGoToPageButton;

			private List<SteamWorkshopModSettings> _steamWorkshopModSettings;

			private TextField _steamWorkshopFileID;
			private TextField _steamWorkshopFolderName;

			private Button _descriptionStatusButton;

			private Image _steamThumbnailUpload;
			private Button _steamThumbnailUploadButton;

			private Label _steamModInstallPath;

			private string _selectedWorkshopPath;
			private string _thumbnailPath;

			private List<ModBuilderSettings> _modSettings;
			private List<string> _steamWorkshopTagsToList = new();

			public void Refresh()
			{
				if (EditorPrefs.HasKey(CHOSEN_MOD_KEY))
				{
					_steamModList.index = _steamModList.choices.IndexOf(EditorPrefs.GetString(CHOSEN_MOD_KEY));
					UpdateSelectedWorkshopPath(_steamModList.value);
					GetInfoFromSteamModSettings(_steamModList.value);
				}
			}
			public void OnEnable(VisualElement root)
				{
					var steamWorkshopTagType = root.Q<EnumField>("SteamWorkshopTagType");
					steamWorkshopTagType.Init(TagType.Category);

				_steamConfigButton = root.Q<Button>("SteamConfigButton");
				_steamWorkshopView = root.Q<VisualElement>("SteamWorkshopViewContainer");
				_steamModList = root.Q<DropdownField>("SteamBuiltModsDropdown");
				_steamVisibility = root.Q<DropdownField>("SteamVisibility");

				_modSettings = new List<ModBuilderSettings>(AssetDatabase.FindAssets("t:PugMod.ModBuilderSettings")
				.Select(guid => AssetDatabase.GUIDToAssetPath(guid))
				.Select(path => AssetDatabase.LoadAssetAtPath<ModBuilderSettings>(path)));
				_steamModList.choices.AddRange(_modSettings.Select(x => x.metadata.name));

				_steamModList.RegisterCallback<ChangeEvent<string>>(evt =>
				{
					UpdateSelectedWorkshopPath(evt.newValue);
					GetInfoFromSteamModSettings(evt.newValue);
					RefreshSteamWorkshopUploadButton();
				});

				_steamWorkshopTags = root.Q<DropdownField>("SteamWorkshopTags");
				_steamWorkshopTagsList = root.Q<VisualElement>("SteamWorkshopTagsList");
				_steamUploadButton = root.Q<Button>("SteamUploadModButton");
				_steamGoToPageButton = root.Q<Button>("SteamGoToPageButton");
				_steamModInstallPath = root.Q<Label>("SteamExportGamePath");

				_descriptionStatusButton = root.Q<Button>("SteamDescriptionStatus");
				_descriptionStatusButton.clicked += OnDescriptionStatusClicked;

				_steamWorkshopFileID = root.Q<TextField>("SteamWorkshopFileID");
				_steamWorkshopFolderName = root.Q<TextField>("SteamWorkshopFolderName");

				_steamThumbnailUpload = root.Q<Image>("SteamThumbnailUpload");
				_steamThumbnailUploadButton = root.Q<Button>("SteamThumbnailUploadButton");

				_steamWorkshopModSettings = new List<SteamWorkshopModSettings>(AssetDatabase.FindAssets("t:SteamWorkshopModSettings")
				.Select(guid => AssetDatabase.GUIDToAssetPath(guid))
				.Select(path => AssetDatabase.LoadAssetAtPath<SteamWorkshopModSettings>(path)));

				_steamVisibility.choices = new List<string> { "Public", "Friends Only", "Private" };

					steamWorkshopTagType.RegisterValueChangedCallback(evt =>
					{
						UpdateTagChoices((TagType)evt.newValue);
					});

					_steamWorkshopTags.RegisterValueChangedCallback(evt =>
					{
						if (string.IsNullOrWhiteSpace(evt.newValue))
						{
							return;
						}

						if (_steamWorkshopTagsToList.Contains(evt.newValue, StringComparer.OrdinalIgnoreCase))
						{
							return;
						}

						_steamWorkshopTagsToList.Add(evt.newValue);
						RefreshTags();
					});

			
				if (EditorPrefs.HasKey(CHOSEN_MOD_KEY))
				{
					_steamModList.index = _steamModList.choices.IndexOf(EditorPrefs.GetString(CHOSEN_MOD_KEY));
				}
				else if (_steamModList.choices.Count > 0)
				{
					_steamModList.index = 0;
				}

				if (_steamModList.index == -1)
				{
					_steamModList.index = 0;
				}

				UpdateSelectedWorkshopPath(_steamModList.value);
				GetInfoFromSteamModSettings(_steamModList.value);

				_steamUploadButton.SetEnabled(!string.IsNullOrEmpty(_selectedWorkshopPath));

				_steamWorkshopFolderName.RegisterValueChangedCallback(evt =>
				{
					RefreshSteamWorkshopUploadButton();
				});

				_steamWorkshopFileID.RegisterValueChangedCallback(evt =>
				{
					RefreshSteamWorkshopUploadButton();
				});

				_steamThumbnailUploadButton.clicked += () =>
				{
					string thumbnailPath = EditorUtility.OpenFilePanel("Select Thumbnail for Mod", "", "png,jpg,jpeg");

					if (string.IsNullOrEmpty(thumbnailPath))
					{
						return;
					}

					var error = ValidateImage(thumbnailPath, maxBytes: 1 * 1024 * 1024, minWidth: 0, minHeight: 0);
					if (error != null)
					{
						ShowError(error);
						return;
					}

					_thumbnailPath = thumbnailPath;
					Texture2D thumbnailPreviewTexture = new(1,1);
					thumbnailPreviewTexture.LoadImage(File.ReadAllBytes(thumbnailPath));
					_steamThumbnailUpload.image = thumbnailPreviewTexture;
				};

				_steamUploadButton.clicked += () =>
				{
					UploadOrUpdateMod();
				};

				_steamGoToPageButton.clicked += () =>
				{
					if (ModHasBeenUploadedToSteamWorkshop())
					{
						Application.OpenURL($"https://steamcommunity.com/sharedfiles/filedetails/?id={_steamWorkshopFileID.value}");
					}
				};

				_steamConfigButton.clicked += () =>
				{
					OpenSteamConfig();
				};

				UpdateTagChoices(TagType.Category);
				RefreshSteamWorkshopUploadButton();
			}
			private void GetInfoFromSteamModSettings(string modName)
			{
				_steamWorkshopModSettings = new List<SteamWorkshopModSettings>(AssetDatabase.FindAssets("t:SteamWorkshopModSettings")
				.Select(guid => AssetDatabase.GUIDToAssetPath(guid))
				.Select(path => AssetDatabase.LoadAssetAtPath<SteamWorkshopModSettings>(path)));

				var steamWorkshopModSettings = FindSteamWorkshopModSettings(modName);

				if (steamWorkshopModSettings != null)
				{
					SelectSteamWorkshopModSettings(modName);
				}
				else
				{
					_steamWorkshopFileID.value = "";
					_steamWorkshopFolderName.value = "";
					_steamWorkshopTagsToList.Clear();
					RefreshTags();
				}
			}

			private void UpdateManifestDisplayName(string buildPath, string newDisplayName)
			{
				var manifestPath = Path.Combine(buildPath, Constants.MOD_MANIFEST_FILE);

				try
				{
					var oldJson = File.ReadAllText(manifestPath);
					var modmetadata = JsonUtility.FromJson<ModMetadata>(oldJson);

					modmetadata.displayName = newDisplayName;

					var newJson = JsonUtility.ToJson(modmetadata, true);
					File.WriteAllText(manifestPath, newJson);
				}
				catch (Exception ex)
				{
					Debug.LogError($"Failed to update display name: {ex.Message}");
				}
			}

			private void UpdateTagChoices(TagType tagType)
			{
				_steamWorkshopTags.choices = GetTagChoices(tagType);
				_steamWorkshopTags.SetValueWithoutNotify(string.Empty);
			}

			private void OpenSteamConfig()
			{
				var steamConfiguration = AssetDatabase.LoadAssetAtPath<SteamConfiguration>("Packages/dev.pugstorm.mod/SDK/Editor/SteamConfiguration.asset");

				EditorGUIUtility.PingObject(steamConfiguration);
				Selection.activeObject = steamConfiguration;
			}

			private void RefreshSteamWorkshopUploadButton()
			{
				_steamUploadButton.SetEnabled(!string.IsNullOrEmpty(_selectedWorkshopPath) &&
					(ModHasBeenUploadedToSteamWorkshop() || !string.IsNullOrEmpty(_steamWorkshopFolderName.value)));

				if(ModHasBeenUploadedToSteamWorkshop())
				{
					_steamUploadButton.text = "Update Mod on Steam Workshop";
					_steamGoToPageButton.style.display = DisplayStyle.Flex;
				}
				else
				{
					_steamUploadButton.text = "Upload Mod to Steam Workshop";
					_steamGoToPageButton.style.display = DisplayStyle.None;
				}
			}
			private bool ModHasBeenUploadedToSteamWorkshop()
			{
				if (string.IsNullOrEmpty(_steamWorkshopFileID.value) || _steamWorkshopFileID.value.Length < 9)
				{
					return false;
				}
				return true;
			}

            private const string DESCRIPTION_FILE_NAME = "description.txt";

            private string GetDescriptionTxtPath()
            {
                var modName = _steamModList.value;
                var modBuilderSettings = _modSettings.FirstOrDefault(x => x.metadata.name == modName);
                if (modBuilderSettings != null && !string.IsNullOrEmpty(modBuilderSettings.modPath))
                {
                    return Path.Combine(modBuilderSettings.modPath, DESCRIPTION_FILE_NAME);
                }

                return null;
            }

            private string GetDescriptionFromFile()
            {
                var descriptionTxtPath = GetDescriptionTxtPath();

                if (!string.IsNullOrEmpty(descriptionTxtPath) && File.Exists(descriptionTxtPath))
                {
                    return File.ReadAllText(descriptionTxtPath);
                }

                return null;
            }

            private void RefreshDescriptionStatus()
            {
                if (_descriptionStatusButton == null)
                {
                    return;
                }

                var descriptionTxtPath = GetDescriptionTxtPath();

                if (string.IsNullOrEmpty(descriptionTxtPath))
                {
                    _descriptionStatusButton.text = "Select a built mod to manage its description.txt";
                    _descriptionStatusButton.SetEnabled(false);
                    return;
                }

                _descriptionStatusButton.SetEnabled(true);
                _descriptionStatusButton.text = File.Exists(descriptionTxtPath)
                    ? "Open description.txt"
                    : "Create description.txt";
            }

            private void OnDescriptionStatusClicked()
            {
                var descriptionTxtPath = GetDescriptionTxtPath();

                if (string.IsNullOrEmpty(descriptionTxtPath))
                {
                    return;
                }

                try
                {
                    if (!File.Exists(descriptionTxtPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(descriptionTxtPath));
                        File.WriteAllText(descriptionTxtPath, string.Empty);
                        RefreshDescriptionStatus();
                    }

                    Process.Start(new ProcessStartInfo(descriptionTxtPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    ShowError($"Failed to open/create description.txt: {ex.Message}");
                }
            }

            private string SetDescription()
			{
				return GetDescriptionFromFile() ?? "";
			}

			private void UploadOrUpdateMod()
			{
                if (string.IsNullOrEmpty(_selectedWorkshopPath))
                {
                    ShowError($"No built mod found for: {_steamWorkshopFolderName.value}. \nPlease build your mod first.");
                    return;
                }

				if (!Directory.Exists(_selectedWorkshopPath))
				{
					ShowError($"Mod folder no longer exists at:\n{_selectedWorkshopPath}\n\nPlease rebuild your mod using 'Build and Install Mod' or 'Build Mod' in the Mod Management tab.");
					return;
				}

				if (!string.IsNullOrEmpty(_steamWorkshopFolderName.value))
				{
					UpdateManifestDisplayName(_selectedWorkshopPath, _steamWorkshopFolderName.value);
				}

				UploadToSteamWorkshop();
			}

			private void RefreshTags()
			{
				if (_steamWorkshopTagsList == null)
				{
					return;
				}

				_steamWorkshopTagsList.Clear();

				foreach (var tag in _steamWorkshopTagsToList)
				{
					var tagButton = new Button(() =>
					{
						_steamWorkshopTagsToList.Remove(tag);
						RefreshTags();
					})
					{
						text = ($"{tag}")
					};
					tagButton.AddToClassList("TagBase");
					tagButton.AddToClassList(GetTagTypeUssClass(GetTagTypeForValue(tag, GetTagChoices)));
					tagButton.style.fontSize = 10;
					_steamWorkshopTagsList.Add(tagButton);
				}
			}

			private SteamWorkshopModSettings FindSteamWorkshopModSettings(string modName)
			{
				return _steamWorkshopModSettings.FirstOrDefault(x => x.modId == modName) ??
					_steamWorkshopModSettings.FirstOrDefault(x => string.IsNullOrEmpty(x.modId) &&
						(x.modName == modName || (!string.IsNullOrEmpty(_selectedWorkshopPath) && x.selectedPath == _selectedWorkshopPath)));
			}

			private void SelectSteamWorkshopModSettings(string modName)
			{
				var steamWorkshopModSettings = FindSteamWorkshopModSettings(modName);

				_steamWorkshopFileID.value = Convert.ToString(steamWorkshopModSettings.fileId);
				_steamWorkshopFolderName.value = steamWorkshopModSettings.modName;
				_selectedWorkshopPath = steamWorkshopModSettings.selectedPath;
				_steamWorkshopTagsToList.Clear();
				_steamWorkshopTagsToList.AddRange(steamWorkshopModSettings.tags);
				RefreshTags();
			}
			private void UpdateSelectedWorkshopPath(string modName)
			{
				var modPaths = GetModPaths();

				var modBuildPaths = modPaths.latestBuildOrInstallPaths
				.Where(x => x.EndsWith(modName))
				.ToList();

				string tempBuildRoot = Path.Combine(Path.GetTempPath(), "BuiltMods");

				_selectedWorkshopPath =
                modBuildPaths.LastOrDefault(x => !x.StartsWith(tempBuildRoot) && Directory.Exists(x)) ?? modBuildPaths.LastOrDefault(x => Directory.Exists(x)) ?? modBuildPaths.LastOrDefault();

				RefreshDescriptionStatus();
			}

			[Serializable]
			private class UploadConfig
			{
				public uint AppId;
				public ulong FileId;
				public string ContentPath;
				public string PreviewPath;
				public string Title;
				public string Description;
				public string Visibility;
				public List<string> Tags;
			}

			private async void UploadToSteamWorkshop()
			{
				var uploaderPath = Path.GetFullPath(Path.Combine("PugModUploader", "dist",
					Application.platform == RuntimePlatform.WindowsEditor ? "win/PugModUploader.exe" : "linux/PugModUploader"));
				if (!File.Exists(uploaderPath))
				{
					ShowError($"Steam Workshop uploader not found at:\n{uploaderPath}");
					return;
				}

				var modName = _steamModList.value;
				var configPath = Path.Combine(Path.GetTempPath(), $"PugModUploader-{Guid.NewGuid():N}.json");
				_steamWorkshopView.SetEnabled(false);
				EditorApplication.LockReloadAssemblies();
				try
				{
					var configuration = AssetDatabase.LoadAssetAtPath<SteamConfiguration>("Packages/dev.pugstorm.mod/SDK/Editor/SteamConfiguration.asset");
					var config = new UploadConfig
					{
						AppId = configuration.CoreKeeperAppID,
						FileId = ModHasBeenUploadedToSteamWorkshop() ? Convert.ToUInt64(_steamWorkshopFileID.value) : 0,
						ContentPath = Path.GetFullPath(_selectedWorkshopPath),
						PreviewPath = string.IsNullOrEmpty(_thumbnailPath) ? null : Path.GetFullPath(_thumbnailPath),
						Title = _steamWorkshopFolderName.value,
						Description = SetDescription(),
						Visibility = _steamVisibility.value,
						Tags = new List<string>(_steamWorkshopTagsToList)
					};
					var currentVersion = GameVersionTagRegistry.GetCurrentVersion();
					if (!string.IsNullOrEmpty(currentVersion) && !config.Tags.Contains(currentVersion))
					{
						config.Tags.Add(currentVersion);
					}
					File.WriteAllText(configPath, JsonUtility.ToJson(config));

					using var process = new Process
					{
						StartInfo = new ProcessStartInfo(uploaderPath)
						{
							Arguments = $"\"{configPath}\"",
							WorkingDirectory = Path.GetDirectoryName(uploaderPath),
							UseShellExecute = false,
							CreateNoWindow = true,
							RedirectStandardOutput = true,
							RedirectStandardError = true
						}
					};
					process.Start();
					var stderr = process.StandardError.ReadToEndAsync();
					ulong fileId = 0;
					string owner = null;
					string error = null;
					EditorUtility.DisplayProgressBar("Steam Workshop", "Starting upload...", 0);
					string line;
					while ((line = await process.StandardOutput.ReadLineAsync()) != null)
					{
						if (line.StartsWith("PROGRESS:") && float.TryParse(line.Substring(9), NumberStyles.Float, CultureInfo.InvariantCulture, out var progress))
						{
							EditorUtility.DisplayProgressBar("Steam Workshop", $"Uploading: {progress:P0}", progress);
						}
						else if (line.StartsWith("SUCCESS_FILE_ID:"))
						{
							ulong.TryParse(line.Substring(16), out fileId);
						}
						else if (line.StartsWith("OWNER:"))
						{
							owner = line.Substring(6);
						}
						else if (line.StartsWith("ERROR:"))
						{
							error = line.Substring(6).Trim();
						}
					}
					await System.Threading.Tasks.Task.Run(() => process.WaitForExit());
					var errorOutput = await stderr;
					EditorUtility.ClearProgressBar();
					if (process.ExitCode != 0 || fileId == 0)
					{
						ShowError(error ?? $"Steam Workshop upload failed (exit code {process.ExitCode}).\n{errorOutput}");
						return;
					}

					SaveSteamWorkshopSettings(fileId, modName, config.ContentPath, config.Tags, config.Title, owner);
					if (_steamModList.value == modName)
					{
						_steamWorkshopFileID.value = fileId.ToString();
						RefreshSteamWorkshopUploadButton();
					}
					EditorUtility.DisplayDialog("Steam Workshop", $"Mod uploaded successfully. Published file ID: {fileId}.", "OK");
				}
				catch (Exception ex)
				{
					ShowError($"Steam Workshop upload failed: {ex.Message}");
				}
				finally
				{
					EditorUtility.ClearProgressBar();
					EditorApplication.UnlockReloadAssemblies();
					_steamWorkshopView.SetEnabled(true);
					File.Delete(configPath);
				}
			}

			private void SaveSteamWorkshopSettings(ulong FileID, string ModName, string SelectedPath, List<string> Tags, string Title, string Owner)
			{
				SteamWorkshopModSettings steamSettings;
				var existingSettings = _steamWorkshopModSettings.FirstOrDefault(x => x.fileId == FileID);

				if (_steamWorkshopModSettings == null)
				{
					_steamWorkshopModSettings = new List<SteamWorkshopModSettings>(Resources.FindObjectsOfTypeAll<SteamWorkshopModSettings>());
				}
				if (existingSettings != null)
				{
					steamSettings = existingSettings;
				}
				else
				{
					steamSettings = CreateSteamWorkshopSettings(ModName);
					_steamWorkshopModSettings.Add(steamSettings);
				}
				steamSettings.fileId = FileID;
				steamSettings.tags = new List<string>(Tags);
				steamSettings.modId = ModName;
				if (!string.IsNullOrEmpty(Title))
				{
					steamSettings.modName = Title;
				}
				steamSettings.selectedPath = SelectedPath;
				steamSettings.modOwner = Owner;

				EditorUtility.SetDirty(steamSettings);
				AssetDatabase.SaveAssets();
			}

			private static SteamWorkshopModSettings CreateSteamWorkshopSettings(string modName)
			{
				var steamSettings = ScriptableObject.CreateInstance<SteamWorkshopModSettings>();
				steamSettings.modName = modName;

				string assetFolder = $"Assets/{modName}";

				if (!Directory.Exists(assetFolder))
				{
					Directory.CreateDirectory(assetFolder);
				}

				string path = AssetDatabase.GenerateUniqueAssetPath($"{assetFolder}/{modName}_Steam.asset");
				AssetDatabase.CreateAsset(steamSettings, path);
				AssetDatabase.SaveAssets();

				//if path doesn't exist, create a folder so that the path does exist

				ShowError($"{modName} File ID and more will be stored in {path}");

				return steamSettings;
			}
		}
	}
}
