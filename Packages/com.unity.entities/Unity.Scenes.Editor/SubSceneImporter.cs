using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Core.Compression;
using Unity.Entities.Build;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.SceneManagement;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes.Editor
{
    [ScriptedImporter(122, "extDontMatter", AllowCaching = true)]
    [InitializeOnLoad]
    class SubSceneImporter : ScriptedImporter
    {
        static SubSceneImporter()
        {
            EntityScenesPaths.SubSceneImporterType = typeof(SubSceneImporter);
        }

        static unsafe NativeList<Hash128> ReferencedUnityObjectsToGUIDs(ReferencedUnityObjects referencedUnityObjects, AssetImportContext ctx)
        {
            var globalObjectIds = new GlobalObjectId[referencedUnityObjects.Array.Length];
            var guids = new NativeList<Hash128>(globalObjectIds.Length, Allocator.Temp);

            GlobalObjectId.GetGlobalObjectIdsSlow(referencedUnityObjects.Array, globalObjectIds);

            for (int i = 0; i != globalObjectIds.Length; i++)
            {
                var assetGUID = globalObjectIds[i].assetGUID;
                // Skip most built-ins, except for BuiltInExtra which we need to depend on
                if (GUIDHelper.IsBuiltin(assetGUID))
                {
                    if (GUIDHelper.IsBuiltinExtraResources(assetGUID))
                    {
                        var objectIdentifiers = ContentBuildInterface.GetPlayerObjectIdentifiersInAsset(assetGUID, ctx.selectedBuildTarget);

                        foreach (var objectIdentifier in objectIdentifiers)
                        {
                            var packedGUID = assetGUID;
                            GUIDHelper.PackBuiltinExtraWithFileIdent(ref packedGUID, objectIdentifier.localIdentifierInFile);

                            guids.Add(packedGUID);
                        }
                    }
                    else if (GUIDHelper.IsBuiltinResources(assetGUID))
                    {
                        guids.Add(assetGUID);
                    }
                }
                else if(!assetGUID.Empty())
                {
                    guids.Add(assetGUID);
                }
            }

            return guids;
        }

        static void WriteAssetDependencyGUIDs(List<ReferencedUnityObjects> referencedUnityObjects, SceneSectionData[] sectionData, AssetImportContext ctx)
        {
            for (var index = 0; index < referencedUnityObjects.Count; index++)
            {
                var sectionIndex = sectionData[index].SubSectionIndex;

                var objRefs = referencedUnityObjects[index];
                if (objRefs == null)
                    continue;

                var path = ctx.GetOutputArtifactFilePath($"{sectionIndex}.{EntityScenesPaths.GetExtension(EntityScenesPaths.PathType.EntitiesAssetDependencyGUIDs)}");
                var assetDependencyGUIDs = ReferencedUnityObjectsToGUIDs(objRefs, ctx);

                using (var writer = new StreamBinaryWriter(path))
                {
                    writer.Write(assetDependencyGUIDs.Length);
                    writer.WriteArray(assetDependencyGUIDs.AsArray());
                }

                assetDependencyGUIDs.Dispose();
            }
        }

        static unsafe void WriteGlobalUsageArtifact(BuildUsageTagGlobal globalUsage, AssetImportContext ctx)
        {
            var path = ctx.GetOutputArtifactFilePath(EntityScenesPaths.GetExtension(EntityScenesPaths.PathType.EntitiesGlobalUsage));
            using (var writer = new StreamBinaryWriter(path))
            {
                writer.WriteBytes(&globalUsage, sizeof(BuildUsageTagGlobal));
            }
        }

        void ImportBaking(AssetImportContext ctx, Scene scene, SceneWithBuildConfigurationGUIDs sceneWithBuildConfiguration, IEntitiesPlayerSettings settingsAsset, GameObject prefab)
        {
            // Grab the used lighting and fog values from the scenes lighting & render settings.
            // If autobake is enabled, this function will iterate through objects to predict the outcome.
            var globalUsage = ContentBuildInterface.GetGlobalUsageFromActiveScene(ctx.selectedBuildTarget);

            var flags = BakingUtility.BakingFlags.AddEntityGUID |
                        BakingUtility.BakingFlags.AssignName;

            var settings = new BakingSettings(flags, default)
            {
                SceneGUID = sceneWithBuildConfiguration.SceneGUID,
                DotsSettings = settingsAsset,
            };

            if (!sceneWithBuildConfiguration.IsBuildingForEditor)
                settings.BakingFlags |= BakingUtility.BakingFlags.IsBuildingForPlayer;

            settings.PrefabRoot = prefab;
            settings.AssetImportContext = ctx;
            settings.FilterFlags = WorldSystemFilterFlags.BakingSystem;

            WriteEntitySceneSettings writeEntitySettings = new WriteEntitySceneSettings();

            var sectionRefObjs = new List<ReferencedUnityObjects>();
            var sectionData = EditorEntityScenes.BakeAndWriteEntityScene(scene, settings, sectionRefObjs, writeEntitySettings);

            WriteAssetDependencyGUIDs(sectionRefObjs, sectionData, ctx);
            WriteGlobalUsageArtifact(globalUsage, ctx);

            foreach (var objRefs in sectionRefObjs)
                DestroyImmediate(objRefs);
        }

        void GetBuildConfigurationOrDotsSettings(SceneWithBuildConfigurationGUIDs sceneWithBuildConfiguration, out IEntitiesPlayerSettings settingsAsset)
        {
            settingsAsset = null;
            // ensure the settings objects are updated and contain the latest changes from the editor
            DotsGlobalSettings.Instance.ReloadSettingsObjects();

            if (sceneWithBuildConfiguration.BuildConfiguration.IsValid)
            {
                settingsAsset = DotsGlobalSettings.Instance.GetSettingsAsset(sceneWithBuildConfiguration.BuildConfiguration);
                if (settingsAsset == null)
                {
                    // the build configuration ID is not a default configuration stored in the ProjectSettings
                    // attempt to load it using the AssetDatabase
                    var path = AssetDatabase.GUIDToAssetPath(sceneWithBuildConfiguration.BuildConfiguration);
                    settingsAsset = AssetDatabase.LoadMainAssetAtPath(path) as IEntitiesPlayerSettings;
                }
            }
            if (settingsAsset == null)
            {
                // if the build config could not be resolved, default to the standard entities client settings asset
                switch (DotsGlobalSettings.Instance.GetPlayerType())
                {
                    case DotsGlobalSettings.PlayerType.Server:
                        settingsAsset = DotsGlobalSettings.Instance.GetServerSettingAsset();
                        break;
                    case DotsGlobalSettings.PlayerType.Client:
                    default:
                        settingsAsset = DotsGlobalSettings.Instance.GetClientSettingAsset();
                        break;
                }
            }
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
#if ENABLE_CLOUD_SERVICES_ANALYTICS
            var watch = System.Diagnostics.Stopwatch.StartNew();
#endif
            try
            {
                var sceneWithBuildConfiguration = SceneWithBuildConfigurationGUIDs.ReadFromFile(ctx.assetPath);

                // Ensure we have as many dependencies as possible registered early in case an exception is thrown
                EditorEntityScenes.AddEntityBinaryFileDependencies(ctx, sceneWithBuildConfiguration.BuildConfiguration);
                EditorEntityScenes.DependOnSceneGameObjects(sceneWithBuildConfiguration.SceneGUID, ctx);

                GetBuildConfigurationOrDotsSettings(sceneWithBuildConfiguration, out var settingsAsset);
                if(settingsAsset != null)
                    ctx.DependsOnCustomDependency(settingsAsset.CustomDependency);

                var scenePath = AssetDatabaseCompatibility.GuidToPath(sceneWithBuildConfiguration.SceneGUID);

                UnityEngine.SceneManagement.Scene scene;
                bool isScene = scenePath.EndsWith(".unity");
                GameObject prefab = null;
                if (!isScene)
                {
                    var prefabGUID = sceneWithBuildConfiguration.SceneGUID;
                    scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    scene.name = prefabGUID.ToString();
                    prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(AssetDatabase.GUIDToAssetPath(prefabGUID.ToString()));
                }
                else
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }

                try
                {
                    EditorSceneManager.SetActiveScene(scene);
                    ImportBaking(ctx, scene, sceneWithBuildConfiguration, settingsAsset, prefab);
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            // Currently it's not acceptable to let the asset database catch the exception since it will create a default asset without any dependencies
            // This means a reimport will not be triggered if the scene is subsequently modified
            catch (Exception e)
            {
                Debug.Log($"Exception thrown during SubScene import: {e}");
            }

#if ENABLE_CLOUD_SERVICES_ANALYTICS
            watch.Stop();
            BakingAnalytics.SendAnalyticsEvent(watch.ElapsedMilliseconds, BakingAnalytics.EventType.BackgroundImporter);
#endif
        }
    }
}
