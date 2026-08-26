using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Steamworks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PugMod
{
	public partial class ModSDKWindow
	{
		private class SteamWorkshopTab
		{
			private VisualElement _steamWorkshopView;
			private Button _steamInitButton;
			private Button _steamConfigButton;

			private VisualElement _steamWorkshopTagsList;

			private DropdownField _steamWorkshopTags;
			private DropdownField _steamModList;
			private DropdownField _steamVisibility;

			private Button _steamUploadButton;

			private List<SteamWorkshopModSettings> _steamWorkshopModSettings;

			private TextField _summaryTextField;
			private TextField _steamWorkshopFileID;
			private TextField _steamWorkshopFolderName;

			private Image _steamThumbnailUpload;
			private Button _steamThumbnailUploadButton;

			private Label _steamModInstallPath;

			private string _selectedWorkshopPath;
			private string _thumbnailPath;

			private List<ModBuilderSettings> _modSettings;
			private List<string> _steamWorkshopTagsToList = new();

			public enum TagType { Category, AppType, AccessType };

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
				_steamModInstallPath = root.Q<Label>("SteamExportGamePath");

				_summaryTextField = root.Q<TextField>("SteamUploadModSummary");
				_steamWorkshopFileID = root.Q<TextField>("SteamWorkshopFileID");
				_steamWorkshopFolderName = root.Q<TextField>("SteamWorkshopFolderName");

				_steamThumbnailUpload = root.Q<Image>("SteamThumbnailUpload");
				_steamThumbnailUploadButton = root.Q<Button>("SteamThumbnailUploadButton");

				_steamWorkshopModSettings = new List<SteamWorkshopModSettings>(AssetDatabase.FindAssets("t:SteamWorkshopModSettings")
				.Select(guid => AssetDatabase.GUIDToAssetPath(guid))
				.Select(path => AssetDatabase.LoadAssetAtPath<SteamWorkshopModSettings>(path)));

				_steamWorkshopTags.choices = new List<string> { "World", "Music", "Tweaks", "NPC", "Language", "Overhaul", "Other", "Visual", "Audio", "Item", "Quality of Life", "Library", "Client", "Server", "Asset", "Script", "Script (Elevated Access)" };

				_steamVisibility.choices = new List<string> { "Public", "Friends Only", "Private" };

				steamWorkshopTagType.RegisterValueChangedCallback(evt =>
				{
					UpdateTagChoices((TagType)evt.newValue);
				});

				_steamWorkshopTags.RegisterValueChangedCallback(evt =>
				{
					if (!_steamWorkshopTagsToList.Contains(evt.newValue))
					{
						_steamWorkshopTagsToList.Add(evt.newValue);
						RefreshTags();
					}
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
					_steamUploadButton.SetEnabled(!string.IsNullOrEmpty(evt.newValue));
					RefreshSteamWorkshopUploadButton();
				});

				_steamWorkshopFileID.RegisterValueChangedCallback(evt =>
				{
					RefreshSteamWorkshopUploadButton();
				});

				_steamThumbnailUploadButton.clicked += () =>
				{
					string thumbnailPath = EditorUtility.OpenFilePanel("Select Thumbnail for Mod", "", "png,jpg,jpeg");
					_thumbnailPath = thumbnailPath;
					Texture2D thumbnailPreviewTexture = new(1,1);
					thumbnailPreviewTexture.LoadImage(File.ReadAllBytes(thumbnailPath));
					_steamThumbnailUpload.image = thumbnailPreviewTexture;
				};

				_steamUploadButton.clicked += () =>
				{
					UploadOrUpdateMod();
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

				RefreshSteamWorkshopUploadButton();
				RefreshSteamWorkshopUI();
			}
			private void GetInfoFromSteamModSettings(string modName)
			{
				_steamWorkshopModSettings = new List<SteamWorkshopModSettings>(AssetDatabase.FindAssets("t:SteamWorkshopModSettings")
				.Select(guid => AssetDatabase.GUIDToAssetPath(guid))
				.Select(path => AssetDatabase.LoadAssetAtPath<SteamWorkshopModSettings>(path)));

				var steamWorkshopModSettings = _steamWorkshopModSettings.FirstOrDefault(x => x.modName == modName);

				if (steamWorkshopModSettings != null)
				{
					SelectSteamWorkshopModSettings(modName);
				}
				else
				{
					_steamWorkshopFileID.value = "";
					_steamWorkshopFolderName.value = modName;
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
				_steamWorkshopTags.choices = tagType switch
				{
					TagType.Category => new List<string> { "World", "Music", "Tweaks", "NPC", "Language", "Overhaul", "Visual", "Audio", "Item", "Quality of Life", "Library", "Other" },
					TagType.AppType => new List<string> { "Client", "Server" },
					TagType.AccessType => new List<string> { "Asset", "Script", "Script (Elevated Access)" },
					_ => null
				};

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
				if(ModHasBeenUploadedToSteamWorkshop())
				{
					_steamUploadButton.text = "Update Mod on Steam Workshop";
				}
				else
				{
					_steamUploadButton.text = "Upload Mod to Steam Workshop";
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

            private string GetDescriptionFromFile()
            {
                var modName = _steamModList.value;
                var modBuilderSettings = _modSettings.FirstOrDefault(x => x.metadata.name == modName);
                if (modBuilderSettings != null && !string.IsNullOrEmpty(modBuilderSettings.modPath))
                {
                    var descriptionTxtPath = Path.Combine(modBuilderSettings.modPath, "description.txt");

                    if (File.Exists(descriptionTxtPath))
                    {
						return File.ReadAllText(descriptionTxtPath);
                    }
                }

                return null;
            }

            private string SetDescription()
			{
				if (!string.IsNullOrEmpty(_summaryTextField.value))
				{
					return _summaryTextField.value;
				}

				var fileDescription = GetDescriptionFromFile();

				if (!string.IsNullOrEmpty(fileDescription))
				{
					return fileDescription;
				}

				return "";
			}

			private void UploadOrUpdateMod()
			{
                if (string.IsNullOrEmpty(_selectedWorkshopPath))
                {
                    ShowError($"No built mod found for: {_steamWorkshopFolderName.value}. \nPlease build your mod first.");
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
					tagButton.style.fontSize = 10;
					_steamWorkshopTagsList.Add(tagButton);
				}
			}

			private void SelectSteamWorkshopModSettings(string modName)
			{
				var steamWorkshopModSettings = _steamWorkshopModSettings.FirstOrDefault(x => x.modName == modName);

				_steamWorkshopFileID.value = Convert.ToString(steamWorkshopModSettings.fileId);
				// Settings with no recorded title keep it in modName.
				var title = steamWorkshopModSettings.title;
				_steamWorkshopFolderName.value = string.IsNullOrEmpty(title) ? steamWorkshopModSettings.modName : title;
				_selectedWorkshopPath = steamWorkshopModSettings.selectedPath;
				_steamWorkshopTagsToList.Clear();
				_steamWorkshopTagsToList.AddRange(steamWorkshopModSettings.tags);
				RefreshTags();
			}
			private void UpdateSelectedWorkshopPath(string modName)
			{
				var modPaths = GetModPaths();

				_selectedWorkshopPath = modPaths.latestBuildOrInstallPaths.LastOrDefault(x => x.EndsWith(modName));
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
						SaveSteamWorkshopSettings(result.FileId, _steamModList.value, _steamWorkshopFolderName.value, _selectedWorkshopPath, _steamWorkshopTagsToList);
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
						SaveSteamWorkshopSettings(result.FileId, _steamModList.value, _steamWorkshopFolderName.value, _selectedWorkshopPath, _steamWorkshopTagsToList);
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
			private void SaveSteamWorkshopSettings(ulong FileID, string ModName, string Title, string SelectedPath, List<string> Tags)
			{
				SteamWorkshopModSettings steamSettings;
				var existingSettings = _steamWorkshopModSettings.FirstOrDefault(x => x.fileId == FileID);

				// The mod dropdown reads null when its selection is out of step with the
				// stored mod preference, and when no mod exists at all. Saving that name
				// would leave the settings unreachable by either lookup, so take it from the
				// asset this file id already belongs to. Settings that still keep their title
				// in modName are left alone: adopting that title as the name would cost them
				// the empty title that identifies them as predating this field.
				if (string.IsNullOrEmpty(ModName) && existingSettings != null && !string.IsNullOrEmpty(existingSettings.title))
				{
					ModName = existingSettings.modName;
				}

				if (string.IsNullOrEmpty(ModName))
				{
					ShowError($"No mod is selected, so the Workshop settings were not saved. The published file id is {FileID}. Pick the mod in the dropdown, put that id in the File ID field, and upload again to store it.");
					return;
				}

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
				steamSettings.modName = ModName;
				steamSettings.title = Title;
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
