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
				_steamInitButton = root.Q<Button>("SteamInitButton");
				_steamConfigButton = root.Q<Button>("SteamConfigButton");
				_steamWorkshopView = root.Q<VisualElement>("SteamWorkshopViewContainer");
				_steamModList = root.Q<DropdownField>("SteamBuiltModsDropdown");

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

				_steamWorkshopTags.choices = new List<string> { "World (Tag)", "Music (Tag)", "Tweaks (Tag)", "NPC (Tag)", "Language (Tag)", "Overhaul (Category)", "Other (Category)", "Visual (Category)", "Audio (Category)", "Item (Category)", "Quality of Life (Category)", "Library (Category)", "Client (App. Type)", "Server (App. Type)", "Asset (Access Type)", "Script (Access Type)", "Script (Elevated Access) (Access Type)" };

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

			private void UploadOrUpdateMod()
			{
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
				_steamWorkshopFolderName.value = steamWorkshopModSettings.modName;
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
					var mod = Steamworks.Ugc.Editor.NewCommunityFile
								.WithTitle(_steamWorkshopFolderName.value)
								.WithDescription(_summaryTextField.value)
								.WithContent(_selectedWorkshopPath)
								.WithPreviewFile(_thumbnailPath);

					foreach (var tag in _steamWorkshopTagsToList)
					{
						mod = mod.WithTag(tag);
					}

					var result = await mod.SubmitAsync(new ProgressClass("Upload"));

					if (result.Success)
					{
						EditorUtility.DisplayDialog("the mod was uploaded via steam workshop!", $"published file ID: {result.FileId}.", "OK.");//could add more info here next to the published file ID
						SaveSteamWorkshopSettings(result.FileId, _steamWorkshopFolderName.value, _selectedWorkshopPath, _steamWorkshopTagsToList);
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

				var steamWorkshopModSettings = _steamWorkshopModSettings.FirstOrDefault(x => x.fileId == Convert.ToUInt64(_steamWorkshopFileID.value));

				if (steamWorkshopModSettings == null || steamWorkshopModSettings.modOwner != SteamApps.AppOwner.ToString())
				{
					ShowError("you don't own this SteamWorkshop item.");
					return;
				}

				try
				{
					var mod = new Steamworks.Ugc.Editor(Convert.ToUInt64(_steamWorkshopFileID.value))
								.WithTitle(_steamWorkshopFolderName.value)
								.WithContent(_selectedWorkshopPath)
								.WithDescription(_summaryTextField.value)
								.WithPreviewFile(_thumbnailPath);

					foreach (var tag in _steamWorkshopTagsToList)
					{
						mod = mod.WithTag(tag);
					}

					var result = await mod.SubmitAsync(new ProgressClass("Update"));

					if (result.Success)
					{
						EditorUtility.DisplayDialog("the mod was updated successfully", $"updated file id: {result.FileId}.", "OK.");//could add more info here next to the published file ID
						SaveSteamWorkshopSettings(result.FileId, _steamWorkshopFolderName.value, _selectedWorkshopPath, _steamWorkshopTagsToList);
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
				steamSettings.tags = Tags;
				steamSettings.modName = ModName;
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
