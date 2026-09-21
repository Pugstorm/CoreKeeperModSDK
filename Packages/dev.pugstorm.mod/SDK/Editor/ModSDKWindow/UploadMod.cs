#if PUG_USE_MODIO
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModIO;
using PugMod.ModIO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PugMod
{
	public partial class ModSDKWindow
	{
		private class UploadMod
		{
			private DropdownField _modList;
            private VisualElement _uploadModForm;
            private VisualElement _modLogo;
			private Button _modLogoButton;
			private Button _uploadButton;
			private Button _goToProfileButton;
			private Button _createModProfileButton;

			internal List<ModBuilderSettings> _modSettings;
			private List<ModSettings> _modIOSettings;

			private TextField _idTextField;
			private TextField _nameTextField;
			private TextField _titleTextField;
			private TextField _summaryTextField;

			private DropdownField _tagsDropdown;
			private DropdownField _visibilityDropdown;
			private VisualElement _gameVersionTagsList;
			private List<string> _tagsToList = new();

			public void Refresh()
			{
				if (EditorPrefs.HasKey(CHOSEN_MOD_KEY))
				{
					_modList.index = _modList.choices.IndexOf(EditorPrefs.GetString(CHOSEN_MOD_KEY));
					UpdateSelection();
				}
			}
			public void OnEnable(VisualElement root)
			{
				_modList = root.Q<DropdownField>("UploadModModList");
                _uploadModForm = root.Q<VisualElement>("UploadModForm");
                _modLogo = root.Q<VisualElement>("UploadModLogo");
				_modLogoButton = root.Q<Button>("UploadModLogoButton");
				_uploadButton = root.Q<Button>("UploadModButton");
				_goToProfileButton = root.Q<Button>("GoToProfileButton");
				_createModProfileButton = root.Q<Button>("CreateModProfile");

				_idTextField = root.Q<TextField>("UploadModID");
				_nameTextField = root.Q<TextField>("UploadModName");
				_titleTextField = root.Q<TextField>("UploadModTitle");
				_summaryTextField = root.Q<TextField>("UploadModSummary");

				_visibilityDropdown = root.Q<DropdownField>("UploadModVisibility");
				_visibilityDropdown.choices = new List<string> { "Public", "Hidden" };
				_visibilityDropdown.SetValueWithoutNotify("Public");

				var tagTypeField = root.Q<EnumField>("UploadModTagType");
				tagTypeField.Init(TagType.Category);

				_tagsDropdown = root.Q<DropdownField>("UploadModTagsDropdown");
				_gameVersionTagsList = root.Q<VisualElement>("UploadModTagsList");

				UpdateTagChoices(TagType.Category);

				tagTypeField.RegisterValueChangedCallback(evt =>
				{
					UpdateTagChoices((TagType)evt.newValue);
				});

				_tagsDropdown.RegisterValueChangedCallback(evt =>
				{
					if (string.IsNullOrWhiteSpace(evt.newValue))
					{
						return;
					}

					if (_tagsToList.Contains(evt.newValue, StringComparer.OrdinalIgnoreCase))
					{
						return;
					}

					_tagsToList.Add(evt.newValue);
					RefreshTagsList();
					SaveCurrentTags();
				});

				_modIOSettings = new List<ModSettings>(AssetDatabase.FindAssets("t:PugMod.ModIO.ModSettings")
				.Select(guid => AssetDatabase.GUIDToAssetPath(guid))
				.Select(path => AssetDatabase.LoadAssetAtPath<ModSettings>(path)));

				_modSettings = new List<ModBuilderSettings>(AssetDatabase.FindAssets("t:PugMod.ModBuilderSettings")
				.Select(guid => AssetDatabase.GUIDToAssetPath(guid))
				.Select(path => AssetDatabase.LoadAssetAtPath<ModBuilderSettings>(path)));

				_modList.choices = new List<string>(_modSettings.Select(x => x.metadata.name));

				var defaultImage = AssetDatabase.LoadAssetAtPath<Texture2D>(DEFAULT_LOGO_PATH);
				_modLogo.style.backgroundImage = defaultImage;

				if (GetModSettings(out _, out var currentModIOSetting) && currentModIOSetting != null)
				{
					_modLogo.style.backgroundImage = currentModIOSetting.logo;
					if (!string.IsNullOrEmpty(currentModIOSetting.summary))
					{
						_summaryTextField.value = currentModIOSetting.summary;
					}
				}

				_nameTextField.RegisterValueChangedCallback(evt =>
				{
					if (!GetModSettings(out var modBuilderSettings, out _))
					{
						return;
					}

					if (modBuilderSettings.metadata.name.Equals(evt.newValue))
					{
						return;
					}

					string newName = evt.newValue;

					int collisionCount = 0;
					while (_modSettings.FindIndex(x => x.metadata.name.Equals(newName) && x != modBuilderSettings) != -1)
					{
						newName = evt.newValue + (++collisionCount);
					}

					if (collisionCount > 0)
					{
						Debug.Log($"Name exists, using {newName} instead");
						_nameTextField.value = newName;
					}

					modBuilderSettings.metadata.name = evt.newValue;
				});

				_modLogoButton.clicked += () =>
				{
					if (!GetModSettings(out var modBuilderSettings, out var modIOSettings, createModIOSettingsIfMissing: true))
					{
						return;
					}

					var path = EditorUtility.OpenFilePanel("Select image", "", "png,jpg,jpeg");

					if (string.IsNullOrEmpty(path))
					{
						return;
					}

					var ext = Path.GetExtension(path);

					if (!ext.Equals(".png") && !ext.Equals(".jpeg") && !ext.Equals(".jpg"))
					{
						ShowError("Needs to be an image in .png or .jpeg/.jpg format");
						return;
					}

					var imageError = ValidateImage(path, maxBytes: 8 * 1024 * 1024, minWidth: 512, minHeight: 288);
					if (imageError != null)
					{
						ShowError(imageError);
						return;
					}

					try
					{
						var textureAssetDir = Path.Combine(LOGO_PATH, modBuilderSettings.metadata.name);
						var textureAssetPath = Path.Combine(textureAssetDir, Path.GetFileName(path));

						if (!Directory.Exists(textureAssetDir))
						{
							Directory.CreateDirectory(textureAssetDir);
						}

						File.Copy(path, textureAssetPath, true);
						AssetDatabase.Refresh();

						var logoImporter = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;

						if (logoImporter == null)
						{
							throw new InvalidOperationException("No logo texture importer at path");
						}

						logoImporter.isReadable = true;
						logoImporter.textureCompression = TextureImporterCompression.Uncompressed;

						AssetDatabase.ImportAsset(textureAssetPath);
						AssetDatabase.Refresh();

						var logo = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);

						if (logo == null)
						{
							throw new InvalidOperationException("No logo at path");
						}

						_modLogo.style.backgroundImage = modIOSettings.logo = logo;

						UpdateModIOSettings(modIOSettings);
					}
					catch (Exception e)
					{
						Debug.LogException(e);
						ShowError("Failed to load image");
					}
				};

				_createModProfileButton.clicked += () =>
				{
					GetModSettings(out var modBuilderSettings, out var modIOSettings, createModIOSettingsIfMissing: true);

					if (modBuilderSettings == null)
					{
						Debug.LogError("no mod settings for chosen mod");
						return;
					}

					if (string.IsNullOrEmpty(_summaryTextField.value))
						{
							ShowError("Summary has to be set");
							return;
						}

						var currentLogo = _modLogo.style.backgroundImage.value.texture as Texture2D;
						if (currentLogo == null)
						{
							ShowError("A logo image is required. Please set one before creating a mod profile.");
							return;
						}

						var modProfileDetails = new ModProfileDetails
						{
							name = string.IsNullOrWhiteSpace(_titleTextField.value) ? modBuilderSettings.metadata.name : _titleTextField.value,
							logo = currentLogo,
							summary = _summaryTextField.value,
							visible = _visibilityDropdown.value == "Public",
							tags = GetTagsWithVersion(),
							};

						if (!InitializeModIO())
						{
							return;
						}

						var creationToken = ModIOUnity.GenerateCreationToken();

						ModIOUnity.CreateModProfile(creationToken, modProfileDetails, result =>
						{
							if (!result.result.Succeeded())
							{
								ShowError($"Failed to create mod on mod.io: {result.result.message}");
								return;
							}

							modIOSettings.modId = result.value;
							modIOSettings.title = _titleTextField.value;
							modIOSettings.summary = _summaryTextField.value;
							modIOSettings.visible = _visibilityDropdown.value == "Public";
							modIOSettings.logo = currentLogo;

							EditorUtility.SetDirty(modIOSettings);

							AssetDatabase.SaveAssets();

						UpdateSelection();

						EditorUtility.DisplayDialog("Register successful", $"Successfully registered new mod with ID={modIOSettings.modId}", "OK");
					});
				};

				_uploadButton.clicked += () =>
				{
					if (!GetModSettings(out var modBuilderSettings, out var modIOSettings) || modIOSettings == null)
					{
						return;
					}

					var metadata = modIOSettings.modSettings.metadata;

					if (modIOSettings.modId == 0)
					{
						ShowError($"ModId not set for mod {metadata.name}");
					}

					if (!InitializeModIO())
					{
						return;
					}

					ModIOUnity.GetMod(new ModId(modIOSettings.modId), result =>
					{
						if (!result.result.Succeeded())
						{
							ShowError($"Couldn't find mod {modIOSettings.modSettings.metadata.name} at mod.io");
							return;
						}

						Debug.Assert(modIOSettings.modId == result.value.id);

						var currentLogo = _modLogo.style.backgroundImage.value.texture as Texture2D;

						var modProfileDetails = new ModProfileDetails
						{
							modId = new ModId(modIOSettings.modId),
							name = string.IsNullOrWhiteSpace(_titleTextField.value) ? modBuilderSettings.metadata.name : _titleTextField.value,
							logo = currentLogo,
							summary = _summaryTextField.value,
							visible = _visibilityDropdown.value == "Public",
							tags = GetTagsWithVersion(),
							};

						ModIOUnity.EditModProfile(modProfileDetails, result =>
						{
							if (!result.Succeeded())
							{
								ShowError("Failed to update mod details");
								return;
							}

							modIOSettings.title = _titleTextField.value;
							modIOSettings.summary = _summaryTextField.value;
							modIOSettings.visible = _visibilityDropdown.value == "Public";
							modIOSettings.logo = currentLogo;
							EditorUtility.SetDirty(modIOSettings);

							var modPath = TempExport(modIOSettings.modSettings, modIOSettings);
							if (string.IsNullOrEmpty(modPath))
							{
								return;
							}

							Upload(modIOSettings, modPath);
						});
					});
				};

				_goToProfileButton.clicked += () =>
				{
					if (!GetModSettings(out var modBuilderSettings, out var modIOSettings) || modIOSettings == null)
					{
						return;
					}

					var metadata = modIOSettings.modSettings.metadata;

					if (modIOSettings.modId == 0)
					{
						ShowError($"ModId not set for mod {metadata.name}");
					}

					if (!InitializeModIO())
					{
						return;
					}

					ModIOUnity.GetMod(new ModId(modIOSettings.modId), result =>
					{
						if (!result.result.Succeeded())
						{
							ShowError($"Couldn't find mod {modIOSettings.modSettings.metadata.name} at mod.io");
							return;
						}

						Debug.Assert(modIOSettings.modId == result.value.id);

						if (string.IsNullOrEmpty(result.value.profilePageUrl))
						{
							ShowError($"No profile url for mod {modIOSettings.modSettings.metadata.name}");
							return;
						}

						Application.OpenURL(result.value.profilePageUrl);
					});
				};

				if (EditorPrefs.HasKey(CHOSEN_MOD_KEY))
				{
					_modList.index = _modList.choices.IndexOf(EditorPrefs.GetString(CHOSEN_MOD_KEY));
				}

				UpdateSelection();

				_modList.RegisterValueChangedCallback(evt =>
				{
					UpdateSelection();
				});
			}
			
			private void UpdateModIOSettings(ModSettings modIOSettings)
				{
					bool dirty = false;

					if (modIOSettings.logo != _modLogo.style.backgroundImage.value.texture &&
						_modLogo.style.backgroundImage.value.texture != null)
					{
						modIOSettings.logo = _modLogo.style.backgroundImage.value.texture;
						dirty = true;
					}

					if (modIOSettings.logo == null)
					{
						modIOSettings.logo = AssetDatabase.LoadAssetAtPath<Texture2D>(DEFAULT_LOGO_PATH);
					}

					if (dirty)
						EditorUtility.SetDirty(modIOSettings);
				}

			private string TempExport(ModBuilderSettings modBuilderSettings, ModSettings modIOSettings)
			{
				if (modBuilderSettings == null || modIOSettings == null)
				{
					Debug.LogError("Called upload without settings");
					return null;
				}

				var tempDirectory = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString());

				Directory.CreateDirectory(tempDirectory);

				bool gotSuccess = false;

				ModBuilder.BuildMod(modBuilderSettings, tempDirectory, success =>
				{
					gotSuccess = success;

					if (!success)
					{
						ShowError($"Failed to export mod to {tempDirectory}");
						return;
					}
				}, false);

				return gotSuccess ? tempDirectory : null;
			}

			private void Upload(ModSettings modIOSettings, string path)
			{
				var fileDetails = new ModfileDetails
				{
					modId = new ModId(modIOSettings.modId),
					directory = path,
				};

				ModIOUnity.UploadModfile(fileDetails, result =>
				{
					if (!result.Succeeded())
					{
						ShowError($"Failed to upload mod to mod.io {result.message}");
						return;
					}

					EditorUtility.DisplayDialog("Upload successful", "Successfully uploaded new mod version", "OK");
				});
			}

			private ModSettings CreateNewModIOSettings(ModBuilderSettings modBuilderSettings)
			{
				AssetDatabase.StartAssetEditing();

				try
				{
					var dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(modBuilderSettings));

					var settings = CreateInstance<ModSettings>();

					settings.modSettings = modBuilderSettings;

					AssetDatabase.CreateAsset(settings, Path.Combine(dir, $"{modBuilderSettings.metadata.name}_modio.asset"));
					_modIOSettings.Add(settings);

					return settings;
				}
				catch (Exception e)
				{
					Debug.LogException(e);
				}
				finally
				{
					AssetDatabase.StopAssetEditing();
					AssetDatabase.Refresh();
				}

				return null;
			}

			internal bool GetModSettings(out ModBuilderSettings modBuilderSettings, out ModSettings modIOSettings, string modName = null, bool createModIOSettingsIfMissing = false)
			{
				if (modName == null)
				{
					if (_modList.choices == null || _modList.index < 0 || _modList.index >= _modList.choices.Count)
					{
						modBuilderSettings = null;
						modIOSettings = null;
						return false;
					}

					modName = _modList.choices[_modList.index];
				}

				modBuilderSettings = _modSettings.Find(x => x.metadata.name.Equals(modName));
				modIOSettings = _modIOSettings.Find(x => x.modSettings.metadata.name.Equals(modName));

				if (createModIOSettingsIfMissing && modIOSettings == null && modBuilderSettings != null)
				{
					modIOSettings = CreateNewModIOSettings(modBuilderSettings);
				}

				return modBuilderSettings != null;
			}

			private void UpdateTagChoices(TagType tagType)
			{
				_tagsDropdown.choices = GetModIOTagChoices(tagType);
				_tagsDropdown.SetValueWithoutNotify(string.Empty);
			}

			private string[] GetTagsWithVersion()
			{
				var tags = new List<string>(_tagsToList);
				var currentVersion = GameVersionTagRegistry.GetCurrentVersion();
				if (!string.IsNullOrEmpty(currentVersion) && !tags.Contains(currentVersion))
				{
					tags.Add(currentVersion);
				}
				return tags.ToArray();
			}

			private void RefreshTagsList()
			{
				if (_gameVersionTagsList == null)
				{
					return;
				}

				_gameVersionTagsList.Clear();

				foreach (var tag in _tagsToList)
				{
					var tagButton = new Button(() =>
					{
						_tagsToList.Remove(tag);
						RefreshTagsList();
						SaveCurrentTags();
					})
					{
						text = tag
					};
					tagButton.AddToClassList("TagBase");
					tagButton.AddToClassList(GetTagTypeUssClass(GetTagTypeForValue(tag, GetModIOTagChoices)));
					tagButton.style.fontSize = 10;
					_gameVersionTagsList.Add(tagButton);
				}
			}

			private void SaveCurrentTags()
			{
				if (!GetModSettings(out _, out var modIOSettings))
				{
					return;
				}

				if (modIOSettings == null)
				{
					return;
				}

				modIOSettings.tags = new List<string>(_tagsToList);
				EditorUtility.SetDirty(modIOSettings);
				AssetDatabase.SaveAssets();
			}

            internal void UpdateSelection()
            {
                _uploadModForm.style.display = DisplayStyle.None;

                _modLogo.style.backgroundImage = AssetDatabase.LoadAssetAtPath<Texture2D>(DEFAULT_LOGO_PATH);
                _uploadButton.style.display = DisplayStyle.None;
                _goToProfileButton.style.display = DisplayStyle.None;
                _createModProfileButton.style.display = DisplayStyle.None;
                _summaryTextField.style.display = DisplayStyle.None;
                _modLogo.style.display = DisplayStyle.None;

                if (!GetModSettings(out var modBuilderSettings, out var modIOSettings))
                {
                    EditorPrefs.DeleteKey(CHOSEN_MOD_KEY);
                    return;
                }

                EditorPrefs.SetString(CHOSEN_MOD_KEY, modBuilderSettings.metadata.name);

                _uploadModForm.style.display = DisplayStyle.Flex;

                _summaryTextField.style.display = DisplayStyle.Flex;
                _modLogo.style.display = DisplayStyle.Flex;
                _nameTextField.value = modBuilderSettings.metadata.name;
                _createModProfileButton.style.display = DisplayStyle.Flex;

                _tagsToList.Clear();
                if (modIOSettings != null && modIOSettings.tags != null)
                {
                    _tagsToList.AddRange(modIOSettings.tags);
                }
                RefreshTagsList();

                _visibilityDropdown.SetValueWithoutNotify(
                    modIOSettings != null && modIOSettings.visible ? "Public" : "Hidden");

                _titleTextField.SetValueWithoutNotify(
                    modIOSettings != null ? modIOSettings.title ?? string.Empty : string.Empty);

                _summaryTextField.SetValueWithoutNotify(
                    modIOSettings != null ? modIOSettings.summary ?? string.Empty : string.Empty);

                if (modIOSettings?.logo != null)
                {
                    _modLogo.style.backgroundImage = modIOSettings.logo;
                }

                if (modIOSettings == null || modIOSettings.modId <= 0)
				{
					return;
				}

				InitializeModIO();

				ModIOUnity.GetMod(new ModId(modIOSettings.modId), result =>
				{
					if (result.result.errorCode == 20303)
					{
						Debug.LogError($"couldn't find mod with id {modIOSettings.modId}");
						return;
					}

					if (!result.result.Succeeded())
					{
						Debug.LogError($"failed to fetch mod info for mod with ID {modIOSettings.modId}: {result.result.message} (code: {result.result.errorCode}");
						return;
					}

					if (!GetModSettings(out modBuilderSettings, out var currentModIOSettings) || currentModIOSettings != modIOSettings)
					{
						return;
					}

					_uploadButton.style.display = DisplayStyle.Flex;
					_goToProfileButton.style.display = DisplayStyle.Flex;
					_createModProfileButton.style.display = DisplayStyle.None;

					modIOSettings.summary = result.value.summary;
					if (!string.IsNullOrEmpty(result.value.logoImage_Original.filename))
					{
						try
						{
							ModIOUnity.DownloadTexture(result.value.logoImage_Original, downloadTextureResult =>
							{
								if (!downloadTextureResult.result.Succeeded())
								{
									return;
								}

								modIOSettings.logo = downloadTextureResult.value;
								if (modIOSettings.logo != null)
									_modLogo.style.backgroundImage = modIOSettings.logo;
							});
						}
						catch (Exception e)
						{
							Debug.LogException(e);
						}
					}

					_idTextField.value = modIOSettings.modId.ToString();
					_idTextField.focusable = false;

					_summaryTextField.value = modIOSettings.summary;
					_visibilityDropdown.SetValueWithoutNotify(result.value.visible ? "Public" : "Hidden");
				});
			}
		}
	}
}
#endif
