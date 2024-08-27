using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.NetCode.Editor
{
    /// <summary>Editor script managing the creation and registration of <see cref="NetCodeConfig"/> ScriptableObjects.</summary>
    [CustomEditor(typeof(NetCodeConfig), true, isFallback = false)]
    internal class NetcodeConfigEditor : UnityEditor.Editor
    {
        private const string k_LiveEditingWarning = " Therefore, be aware that the Global config is applied project-wide automatically:\n - In the Editor; this config is set every frame, enabling live editing. Note that this invalidates (by replacing) any C# code of yours that modifies these NetCode configuration singleton components manually.\n - In a build; this config is applied once (during Server & Client World system creation).";

        private static readonly GUILayoutOption s_ButtonWidth = GUILayout.Width(70);
        private static NetCodeConfig s_EditorGlobalConfigSelection;

        [MenuItem("Multiplayer/Create NetcodeConfig Asset", priority = 100)]
        internal static void CreateNetcodeSettingsAsset()
        {
            var assetPath = AssetDatabase.GenerateUniqueAssetPath("Assets/NetcodeConfig.asset");
            var netCodeConfig = ScriptableObject.CreateInstance<NetCodeConfig>();
            AssetDatabase.CreateAsset(netCodeConfig, assetPath);
            s_EditorGlobalConfigSelection = AssetDatabase.LoadAssetAtPath<NetCodeConfig>(assetPath);
            Selection.activeObject = s_EditorGlobalConfigSelection;
        }

        /// <summary>Internal method to register the provider (with IMGUI for drawing).</summary>
        /// <returns></returns>
        [SettingsProvider]
        public static SettingsProvider CreateNetcodeConfigSettingsProvider()
        {
            // First parameter is the path in the Settings window.
            // Second parameter is the scope of this setting: it only appears in the Project Settings window.
            var provider = new SettingsProvider("Project/NetCodeConfig.asset", SettingsScope.Project)
            {
                // By default the last token of the path is used as display name if no label is provided.
                label = "NetCode",
                // Create the SettingsProvider and initialize its drawing (IMGUI) function in place:
                guiHandler = (searchContext) =>
                {
                    s_EditorGlobalConfigSelection ??= TryFindExistingInPreloadedAssets();

                    GUILayout.BeginHorizontal();
                    {
                        EditorGUI.BeginChangeCheck();
                        GUI.enabled = !Application.isPlaying;
                        s_EditorGlobalConfigSelection = EditorGUILayout.ObjectField(new GUIContent(string.Empty, "Select the asset that NetCode will use, by default."), s_EditorGlobalConfigSelection, typeof(NetCodeConfig), allowSceneObjects: false) as NetCodeConfig;

                        if (GUILayout.Button("Find & Set", s_ButtonWidth))
                        {
                            s_EditorGlobalConfigSelection = TryFindExistingInPreloadedAssets();
                            if (s_EditorGlobalConfigSelection == null)
                            {
                                var configs = AssetDatabase.FindAssets($"t:{nameof(NetCodeConfig)}")
                                    .Select(AssetDatabase.GUIDToAssetPath)
                                    .Select(AssetDatabase.LoadAssetAtPath<NetCodeConfig>)
                                    .ToArray();
                                Array.Sort(configs);
                                s_EditorGlobalConfigSelection = configs.FirstOrDefault();
                                EditorGUIUtility.PingObject(s_EditorGlobalConfigSelection);
                            }
                        }

                        if (GUILayout.Button("Create", s_ButtonWidth))
                        {
                            CreateNetcodeSettingsAsset();
                        }

                        if (EditorGUI.EndChangeCheck())
                        {
                            SaveEditorCachedSetting();
                        }
                    }
                    GUILayout.EndHorizontal();

                    if (!s_EditorGlobalConfigSelection)
                    {
                        EditorGUILayout.HelpBox("No Global NetCodeConfig is set. This is valid, but note that the NetCode package will therefore be configured with default settings, unless otherwise specified (e.g. by modifying the Netcode singleton component values directly in C#).", MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("You have now set a Global NetCodeConfig asset." + k_LiveEditingWarning, MessageType.Warning);
                    }
                    GUILayout.Space(10);
                    EditorGUILayout.LabelField("Preloaded NetCodeConfigs", EditorStyles.boldLabel);
                    GUILayout.Space(5);

                    foreach (var preloaded in PlayerSettings.GetPreloadedAssets().OfType<NetCodeConfig>())
                    {
                        GUILayout.BeginHorizontal();
                        {
                            GUI.color = s_EditorGlobalConfigSelection == preloaded ? GhostAuthoringComponentEditor.netcodeColor : Color.white;
                            GUILayout.Label($" - {preloaded.name} (global: {preloaded.IsGlobalConfig})");
                            if (GUILayout.Button($"Ping", s_ButtonWidth))
                            {
                                EditorGUIUtility.PingObject(preloaded);
                                Selection.activeObject = preloaded;
                            }
                            if (GUILayout.Button($"Set", s_ButtonWidth))
                            {
                                s_EditorGlobalConfigSelection = preloaded;
                                SaveEditorCachedSetting();
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                },

                // Populate the search keywords to enable smart search filtering and label highlighting:
                keywords = new HashSet<string>(new[] {"NetCode", "NetCodeConfig", "TickRate", "SimulationTickRate", "NetworkTickRate", "NetworkSendRate"}),
            };
            return provider;
        }

        private static void SaveEditorCachedSetting()
        {
            // Save the cached value to the Preloaded assets list,
            // BUT don't clobber any existing NetcodeConfigs in there.
            // Just ensure ours is added, and set its IsGlobalConfig to true.
            var preloadedAssets = PlayerSettings.GetPreloadedAssets().Where(x => x);
            if (s_EditorGlobalConfigSelection) preloadedAssets = preloadedAssets.Concat(new[] {s_EditorGlobalConfigSelection}).Distinct();
            // ReSharper disable PossibleMultipleEnumeration
            foreach (var otherConfig in preloadedAssets.OfType<NetCodeConfig>())
            {
                if (otherConfig.IsGlobalConfig && otherConfig != s_EditorGlobalConfigSelection)
                {
                    otherConfig.IsGlobalConfig = false;
                    EditorUtility.SetDirty(otherConfig);
                }
            }

            if (s_EditorGlobalConfigSelection)
            {
                s_EditorGlobalConfigSelection.IsGlobalConfig = true;
                EditorUtility.SetDirty(s_EditorGlobalConfigSelection);
            }

            PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());
        }

        private static NetCodeConfig TryFindExistingInPreloadedAssets() => PlayerSettings.GetPreloadedAssets().OfType<NetCodeConfig>().FirstOrDefault(x => x.IsGlobalConfig);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var config = (NetCodeConfig)target;

            if (config.IsGlobalConfig)
                EditorGUILayout.HelpBox("You have selected this as your Global config." + k_LiveEditingWarning, MessageType.Info);
            if (Application.isPlaying)
                EditorGUILayout.HelpBox("Live tweaking is not supported for some values, and is thus disabled.", MessageType.Warning);

            GUI.enabled = !Application.isPlaying;
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(NetCodeConfig.ClientServerTickRate)));
            GUI.enabled = true;
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(NetCodeConfig.ClientTickRate)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(NetCodeConfig.GhostSendSystemData)));

            serializedObject.ApplyModifiedProperties();
        }

        }
}
