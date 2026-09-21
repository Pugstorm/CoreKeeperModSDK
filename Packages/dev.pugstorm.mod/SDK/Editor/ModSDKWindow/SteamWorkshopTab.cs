using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Steamworks;
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
			private Button _steamInitButton;
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
				RefreshSteamWorkshopUI();
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

					_steamInitButton = root.Q<Button>("SteamInitButton");
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

				_steamInitButton.clicked += () =>
				{
					var steamConfiguration = AssetDatabase.LoadAssetAtPath<SteamConfiguration>("Packages/dev.pugstorm.mod/SDK/Editor/SteamConfiguration.asset");

					try
					{
						SteamClient.Init(steamConfiguration.CoreKeeperAppID);
						Debug.Log("Steam initialized successfully for Mod SDK");
						RefreshSteamWorkshopUI();
					}
					catch (System.Exception e)
					{
						Debug.LogError($"Failed to initialize Steam for Mod SDK: {e.Message}");
					}

				};

				_steamConfigButton.clicked += () =>
				{
					OpenSteamConfig();
				};

				UpdateTagChoices(TagType.Category);
				RefreshSteamWorkshopUploadButton();
				RefreshSteamWorkshopUI();
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

			private void RefreshSteamWorkshopUI()
			{
				if (SteamClient.IsValid)
				{
					_steamInitButton.style.display = DisplayStyle.None;
					_steamConfigButton.style.display = DisplayStyle.None;
					_steamWorkshopView.style.display = DisplayStyle.Flex;
				}
				else
				{
					_steamInitButton.style.display = DisplayStyle.Flex;
					_steamConfigButton.style.display = DisplayStyle.Flex;
					_steamWorkshopView.style.display = DisplayStyle.None;
				}
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

				if (ModHasBeenUploadedToSteamWorkshop())
				{
					UpdateSteamWorkshopMod();
				}
				else
				{
					UploadToSteamWorkshop();
				}
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

			private class ProgressClass : IProgress<float>
			{
				public float lastValue = 0;
				private string methodType;

				public ProgressClass(string _methodType)
				{
					methodType = _methodType;
				}
				public void Report(float value)
				{
					if (lastValue >= value) return;
					lastValue = value;

					string operation = methodType switch
					{
						"Upload" => "uploading mod to steam workshop",
						"Update" => "updating mod on steam workshop",
						_ => null
					};

					EditorUtility.DisplayProgressBar(operation, $"progress: {value * 100:F1}%", value);

					if (Math.Abs(value - 1f) < 0.001f)
					{
						EditorUtility.ClearProgressBar();
					}
				}
			}

			private async void UploadToSteamWorkshop()
			{
				if (!SteamClient.IsValid)
				{
					ShowError("Steam client hasn't been initialized, initialize it first or start Steam.");
					return;
				}
				try
				{
					var description = SetDescription();

					var mod = Steamworks.Ugc.Editor.NewCommunityFile
								.WithContent(_selectedWorkshopPath)
								.WithPreviewFile(_thumbnailPath);

					if (!string.IsNullOrEmpty(_steamWorkshopFolderName.value))
					{
						mod = mod.WithTitle(_steamWorkshopFolderName.value);
					}

					if (!string.IsNullOrEmpty(description))
					{
						mod = mod.WithDescription(description);
					}

					foreach (var tag in _steamWorkshopTagsToList)
					{
						mod = mod.WithTag(tag);
					}

					var currentVersion = GameVersionTagRegistry.GetCurrentVersion();
					if (!string.IsNullOrEmpty(currentVersion))
					{
						mod = mod.WithTag(currentVersion);
					}

					mod = _steamVisibility.value switch
					{
						"Private" => mod.WithPrivateVisibility(),
						"Friends Only" => mod.WithFriendsOnlyVisibility(),
						"Public" => mod.WithPublicVisibility(),
						_ => mod.WithPrivateVisibility()
					};

					var result = await mod.SubmitAsync(new ProgressClass("Upload"));

					if (result.Success)
					{
						EditorUtility.DisplayDialog("the mod was uploaded via steam workshop!", $"published file ID: {result.FileId}.", "OK.");//could add more info here next to the published file ID
						SaveSteamWorkshopSettings(result.FileId, _steamModList.value, _selectedWorkshopPath, _steamWorkshopTagsToList);
						_steamWorkshopFileID.value = Convert.ToString(result.FileId);
						RefreshSteamWorkshopUploadButton();
					}
					else
					{
						ShowError($"failed to upload Mod to Steam Workshop: {result.Result}");
					}
				}
				catch (Exception ex)
				{
					ShowError($"an error occurred: {ex.Message}");
				}
			}
			private async void UpdateSteamWorkshopMod()
			{
				if (!SteamClient.IsValid)
				{
					ShowError("Steam client hasn't been initialized, initialize it first or start Steam.");
					return;
				}

				try
				{
					var fileId = Convert.ToUInt64(_steamWorkshopFileID.value);
					var fileInfo = await Steamworks.Ugc.Item.GetAsync(fileId);

					if (!fileInfo.HasValue)
					{
						ShowError("Could not find this Workshop item. Please verify the File ID is correct.");
						return;
					}

					if (fileInfo.Value.Owner.Id != SteamClient.SteamId)
					{
						ShowError("You don't own this Steam Workshop item.");
						return;
					}

					var description = SetDescription();

					var mod = new Steamworks.Ugc.Editor(fileId)
								.WithContent(_selectedWorkshopPath)
								.WithPreviewFile(_thumbnailPath);

					if (!string.IsNullOrEmpty(_steamWorkshopFolderName.value))
					{
						mod = mod.WithTitle(_steamWorkshopFolderName.value);
					}

					if (!string.IsNullOrEmpty(description))
					{
						mod = mod.WithDescription(description);
					}

					foreach (var tag in _steamWorkshopTagsToList)
					{
						mod = mod.WithTag(tag);
					}

					var currentVersion = GameVersionTagRegistry.GetCurrentVersion();
					if (!string.IsNullOrEmpty(currentVersion))
					{
						mod = mod.WithTag(currentVersion);
					}

					mod = _steamVisibility.value switch
					{
						"Private" => mod.WithPrivateVisibility(),
						"Friends Only" => mod.WithFriendsOnlyVisibility(),
						"Public" => mod.WithPublicVisibility(),
						_ => mod.WithPrivateVisibility()
					};

					var result = await mod.SubmitAsync(new ProgressClass("Update"));

					if (result.Success)
					{
						EditorUtility.DisplayDialog("the mod was updated successfully", $"updated file id: {result.FileId}.", "OK.");//could add more info here next to the published file ID
						SaveSteamWorkshopSettings(result.FileId, _steamModList.value, _selectedWorkshopPath, _steamWorkshopTagsToList);
					}
					else
					{
						ShowError($"failed to update mod on Steam Workshop: {result.Result}");
					}
				}
				catch (Exception ex)
				{
					ShowError($"an error occurred: {ex.Message}");
				}
			}
			private void SaveSteamWorkshopSettings(ulong FileID, string ModName, string SelectedPath, List<string> Tags)
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
				if (!string.IsNullOrEmpty(_steamWorkshopFolderName.value))
				{
					steamSettings.modName = _steamWorkshopFolderName.value;
				}
				steamSettings.selectedPath = _selectedWorkshopPath;
				steamSettings.modOwner = SteamApps.AppOwner.ToString();
				//steamSettings.Change(SteamApps.AppOwner.ToString()); if we want to serialize modOnwer ID but don't want it visible in inspector, uncomment Change method first in SteamWorkshopSettings.cs

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
