#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEditor;

namespace Unity.Scenes
{
    struct SceneWithBuildConfigurationGUIDs
    {
        public Hash128 SceneGUID;
        public Hash128 BuildConfiguration;
        public int     _IsBuildingForEditor;

        public bool IsBuildingForEditor
        {
            get { return _IsBuildingForEditor != 0; }
            set { _IsBuildingForEditor = value ? 1 : 0; }
        }

        // Currently used to allow us to force subscenes to reimport
        public long DirtyValue;

        static HashSet<Hash128> s_BuildConfigurationCreated = new HashSet<Hash128>();
        private static ulong s_AssetRefreshCounter = 0;

        internal const string k_SceneDependencyCachePath = "Assets/SceneDependencyCache";

        internal static void ClearBuildSettingsCache()
        {
            s_BuildConfigurationCreated.Clear();
            AssetDatabase.DeleteAsset(k_SceneDependencyCachePath);
        }

        internal static void ValidateBuildSettingsCache()
        {
            // Invalidate cache if we had an asset refresh
            var refreshDelta = AssetDatabaseCompatibility.GetArtifactDependencyVersion();
            if (s_AssetRefreshCounter != refreshDelta)
                s_BuildConfigurationCreated.Clear();

            s_AssetRefreshCounter = refreshDelta;
        }

        public static string GetSceneWithBuildSettingsPath(Hash128 guid)
        {
            return $"{k_SceneDependencyCachePath}/{guid}.sceneWithBuildSettings";
        }

        static bool DirtyFile(Hash128 sceneGUID, Hash128 buildConfigurationGUID, bool isBuildingForEditor)
        {
            var guid = ComputeBuildConfigurationGUID(sceneGUID, buildConfigurationGUID, isBuildingForEditor);
            var fileName = GetSceneWithBuildSettingsPath(guid);
            if (File.Exists(fileName))
            {
                var sceneWithBuildConfigurationGUIDs = new SceneWithBuildConfigurationGUIDs
                {
                    SceneGUID = sceneGUID,
                    BuildConfiguration = buildConfigurationGUID,
                    IsBuildingForEditor = isBuildingForEditor,
                    DirtyValue = DateTime.UtcNow.Ticks
                };
                WriteEntitySceneWithBuildConfig(guid, sceneWithBuildConfigurationGUIDs, GetSceneWithBuildSettingsPath(guid));
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool Dirty(Hash128 sceneGUID, Hash128 buildConfigurationGUID)
        {
            var dirty = false;
            dirty |= DirtyFile(sceneGUID, buildConfigurationGUID, false);
            dirty |= DirtyFile(sceneGUID, buildConfigurationGUID, true);
            return dirty;
        }

        private static unsafe void WriteEntitySceneWithBuildConfig(Hash128 guid, SceneWithBuildConfigurationGUIDs sceneWithBuildConfigurationGUIDs, string path)
        {
            var previousPath = AssetDatabaseCompatibility.GuidToPath(guid);
            if (!string.IsNullOrEmpty(previousPath) && previousPath != path)
                UnityEngine.Debug.LogError($"EntitySceneWithBuildConfig guid is not unique {guid}. ScenePath: '{AssetDatabaseCompatibility.GuidToPath(sceneWithBuildConfigurationGUIDs.SceneGUID)}' Conflicting GUID: '{previousPath}'");

            Directory.CreateDirectory(k_SceneDependencyCachePath);
            using (var writer = new StreamBinaryWriter(path))
            {
                writer.WriteBytes(&sceneWithBuildConfigurationGUIDs, sizeof(SceneWithBuildConfigurationGUIDs));
            }
            File.WriteAllText(path + ".meta",
                $"fileFormatVersion: 2\nguid: {guid}\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n");
        }

        public static unsafe Hash128 EnsureExistsFor(Hash128 sceneGUID, Hash128 buildConfigurationGUID, bool isBuildingForEditor, out bool mustRequestRefresh)
        {
            mustRequestRefresh = false;
            var guid = ComputeBuildConfigurationGUID(sceneGUID, buildConfigurationGUID, isBuildingForEditor);

            if (s_BuildConfigurationCreated.Contains(guid))
                return guid;

            var fileName = GetSceneWithBuildSettingsPath(guid);
            if (!File.Exists(fileName))
            {
                var sceneWithBuildConfigurationGUIDs = new SceneWithBuildConfigurationGUIDs { SceneGUID = sceneGUID, IsBuildingForEditor = isBuildingForEditor, BuildConfiguration = buildConfigurationGUID, DirtyValue = 0};
                WriteEntitySceneWithBuildConfig(guid, sceneWithBuildConfigurationGUIDs, fileName);
                mustRequestRefresh = true;
            }

            s_BuildConfigurationCreated.Add(guid);

            return guid;
        }

        public static unsafe SceneWithBuildConfigurationGUIDs ReadFromFile(string path)
        {
            SceneWithBuildConfigurationGUIDs sceneWithBuildConfiguration = default;
            using (var reader = new StreamBinaryReader(path))
            {
                reader.ReadBytes(&sceneWithBuildConfiguration, sizeof(SceneWithBuildConfigurationGUIDs));
            }
            return sceneWithBuildConfiguration;
        }

        static Hash128 ComputeBuildConfigurationGUID(in Hash128 sceneGUID, in Hash128 buildConfigurationGUID, bool isBuildingForEditor)
        {
            var guids = new SceneWithBuildConfigurationGUIDs { SceneGUID = sceneGUID, BuildConfiguration = buildConfigurationGUID, IsBuildingForEditor = isBuildingForEditor};
            var hash = xxHash3.Hash128(guids);
            return new Hash128(hash);
        }
    }
}
#endif
