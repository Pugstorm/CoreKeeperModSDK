using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using Hash128 = Unity.Entities.Hash128;
using System.Collections.Generic;
using UnityEditor.Experimental;
using System.IO;
using Unity.Entities.Build;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Unity.Scenes.Editor
{
    /// <summary>   Interface to force the addition of unreferenced SubScenes into a build. </summary>
    public interface IEntitySceneBuildAdditions
    {
        /// <summary>   Implement this method to receive a callback at build time.  </summary>
        ///
        /// <returns>   A set of SubScene hashes that should be included in the build. </returns>
        public HashSet<Unity.Entities.Hash128> RegisterAdditionalEntityScenesToBuild();
    }

    internal sealed class EntitySectionBundlesInBuild
    {
        internal List<Hash128> SceneGUIDs = new List<Hash128>();
        internal List<ArtifactKey> ArtifactKeys = new List<ArtifactKey>();

        public void Add(Hash128 sceneGUID, ArtifactKey artifactKey)
        {
            SceneGUIDs.Add(sceneGUID);
            ArtifactKeys.Add(artifactKey);
        }

        public void Add(IEnumerable<Hash128> sceneGUIDs, IEnumerable<ArtifactKey> artifactKeys)
        {
            SceneGUIDs.AddRange(sceneGUIDs);
            ArtifactKeys.AddRange(artifactKeys);
        }
    }

    internal struct RootSceneInfo
    {
        public string Path;
        public Hash128 Guid;
    }

    internal interface IEntitySceneBuildCustomizer
    {
        public void RegisterAdditionalFilesToDeploy(Hash128[] rootSceneGUIDs, Hash128[] entitySceneGUIDs, EntitySectionBundlesInBuild additionalImports, Action<string, string> addAdditionalFile);
    }

    internal class EntitySceneBuildPlayerProcessor : BuildPlayerProcessor
    {
        private string m_BuildWorkingDir = $"../Library/BuildWorkingDir/{PlayerSettings.productName}";
        private BuildPlayerContext m_BuildPlayerContext;
        // This file is read by unity\Platforms\WebGL\WebGLPlayerBuildProgram\WebGLPlayerBuildProgram.cs when bundling the virtual filesystem
        private static string k_WebGLStreamingAssetFilesManifest = "Library/PlayerDataCache/WebGLPreloadedStreamingAssets.manifest";
        private bool m_IsWebGLBuild;

        void RegisterAdditionalFileToDeploy(string from, string to)
        {
            var realPath = Path.GetFullPath(from);

            if (m_IsWebGLBuild)
            {
                // Record this file to a manifest so that WebGL virtual filesystem packager
                // will know to preload it for synchronous filesystem access.
                StreamWriter writer = File.AppendText(k_WebGLStreamingAssetFilesManifest);
                writer.WriteLine(to);
                writer.Close();
            }

            m_BuildPlayerContext.AddAdditionalPathToStreamingAssets(realPath, to);
        }

        [InitializeOnLoadMethod]
        public static void Init()
        {
            BuildPlayerWindow.RegisterGetBuildPlayerOptionsHandler(HandleGetBuild);
        }

        static BuildPlayerOptions HandleGetBuild(BuildPlayerOptions opts)
        {
            opts = BuildPlayerWindow.DefaultBuildMethods.GetBuildPlayerOptions(opts);
            var instance = DotsGlobalSettings.Instance;

            if (instance.GetPlayerType() == DotsGlobalSettings.PlayerType.Server)
            {
                //If a server provider is not present use at least the default setting for the client build.
                var provider = instance.ServerProvider;
                if (provider == null)
                {
                    LogMissingServerProvider();
                    provider = instance.ClientProvider;
                }
                opts.extraScriptingDefines = provider.GetExtraScriptingDefines();
                // Adding EnableHeadlessMode as an option will switch the platform to dedicated server that defines UNITY_SERVER in the Editor as well.
                // We may want to switch back to the original platform at the end of the build to prevent it if we don't support switching to dedicated server. Currently the Editor fails to compile after switching to the dedicated server subtarget.
                opts.options |= provider.GetExtraBuildOptions();
            }
            else
            {
                opts.extraScriptingDefines = instance.ClientProvider.GetExtraScriptingDefines();
                opts.options |= instance.ClientProvider.GetExtraBuildOptions();
            }
            return opts;
        }

        static void LogMissingServerProvider()
        {
            UnityEngine.Debug.LogWarning(
                $"No available DotsPlayerSettingsProvider for the current platform ({EditorUserBuildSettings.activeBuildTarget}). Using the client settings instead.");
        }

        // This method is mostly doing the same as the engine method BuildPlayer::SaveScenesBeforeBuildIfNeeded except that it saves all unsaved opened scenes as content management requires it before building
        void SaveScenesBeforeBuildIfNeeded(string [] scenesToBuild)
        {
            if (Application.isBatchMode || EditorPrefs.GetBool("SaveScenesBeforeBuilding"))
            {
                // In batch mode we cannot show a dialog asking to save changes.
                // Auto-save any scene even if they are not part of the build as content management requires it.
                var scenesToSave = new List<Scene>();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.isDirty)
                    {
                        scenesToSave.Add(scene);
                    }
                }

                if (scenesToSave.Count > 0)
                    EditorSceneManager.SaveScenes(scenesToSave.ToArray());
            }
            else
            {
                if (scenesToBuild.Length == 0)
                {
                    // If we have no scenes in the build, we will build the currently active scenes.
                    // In that case, if we have an untitled scene, always mark it as dirty so we can ask the user to save it
                    for (int i = 0; i < SceneManager.sceneCount; i++)
                    {
                        var scene = SceneManager.GetSceneAt(i);
                        if (String.IsNullOrEmpty(scene.path))
                            EditorSceneManager.MarkSceneDirty(scene);
                    }
                }

                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            }
        }

        public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
        {
            // Content pipeline requires the opened scenes in the Editor to be saved to be able to build or it will fail with the exception ContentCatalogBuildUtility.BuildContentArchives failed with status 'UnsavedChanges'.
            SaveScenesBeforeBuildIfNeeded(buildPlayerContext.BuildPlayerOptions.scenes);

            // For WebGL, we need to prepare a manifest list of all generated data files under StreamingAssets/
            // that need to be synchronously accessible.
            if (buildPlayerContext.BuildPlayerOptions.target == BuildTarget.WebGL)
            {
                m_IsWebGLBuild = true;
                if (File.Exists(k_WebGLStreamingAssetFilesManifest)) File.Delete(k_WebGLStreamingAssetFilesManifest); // We'll build the list by appending items one at a time, so clear any old list.
                else Directory.CreateDirectory(new FileInfo(k_WebGLStreamingAssetFilesManifest).Directory.FullName);
            }

            if(Directory.Exists(m_BuildWorkingDir))
                Directory.Delete(m_BuildWorkingDir, true);

            m_BuildPlayerContext = buildPlayerContext;

            // Retrieve list of subscenes to import from the root scenes added to the player settings
            var rootSceneInfos = new List<RootSceneInfo>();
            var rootSceneGUIDs = new List<Hash128>();
            var subSceneGuids = new HashSet<Hash128>();

            // If Scenes is empty, the default behaviour is to build the current scene, so we cannot use the GetSubScenes call as it is an importer
            if (buildPlayerContext.BuildPlayerOptions.scenes == null ||
                buildPlayerContext.BuildPlayerOptions.scenes.Length == 0)
            {
                var subScenes = SubScene.AllSubScenes;
                subSceneGuids = new HashSet<Hash128>(subScenes.Where(x => x.SceneGUID.IsValid).Select(x => x.SceneGUID).Distinct());

                var rootScenePath = EditorSceneManager.GetActiveScene().path;

                if(string.IsNullOrEmpty(rootScenePath))
                    throw new SystemException("Entities currently cannot build a Scene that has not yet been saved for the first time.");

                var rootSceneGUID = AssetDatabase.GUIDFromAssetPath(rootScenePath);
                rootSceneInfos.Add(new RootSceneInfo()
                {
                    Path = rootScenePath,
                    Guid = rootSceneGUID
                });
            }
            else
            {
                for (int i = 0; i < buildPlayerContext.BuildPlayerOptions.scenes.Length; i++)
                {
                    var rootScenePath = buildPlayerContext.BuildPlayerOptions.scenes[i];
                    var rootSceneGUID = AssetDatabase.GUIDFromAssetPath(rootScenePath);

                    rootSceneGUIDs.Add(rootSceneGUID);
                    rootSceneInfos.Add(new RootSceneInfo()
                    {
                        Path = rootScenePath,
                        Guid = rootSceneGUID
                    });
                }
                subSceneGuids = new HashSet<Hash128>(rootSceneInfos.SelectMany(rootScene => EditorEntityScenes.GetSubScenes(rootScene.Guid)));
            }

            var types = TypeCache.GetTypesDerivedFrom<IEntitySceneBuildAdditions>();
            foreach (var type in types)
            {
                var sceneAdditions = (IEntitySceneBuildAdditions)Activator.CreateInstance(type);
                var otherSubScenes = sceneAdditions.RegisterAdditionalEntityScenesToBuild();
                if (otherSubScenes != null)
                {
                    subSceneGuids.UnionWith(otherSubScenes);
                }
            }

            // Import subscenes and deploy entity scene files and bundles
            var artifactKeys = new Dictionary<Hash128, ArtifactKey>();
            var binaryFiles = new EntitySectionBundlesInBuild();

            var instance = DotsGlobalSettings.Instance;
            var playerGuid = instance.GetClientGUID();

            if (instance.GetPlayerType() == DotsGlobalSettings.PlayerType.Server)
            {
                playerGuid = instance.GetServerGUID();
                if (!playerGuid.IsValid)
                {
                    LogMissingServerProvider();
                    playerGuid = instance.GetClientGUID();
                }
            }

            if(!playerGuid.IsValid)
                throw new BuildFailedException("Invalid Player GUID");

            EntitySceneBuildUtility.PrepareEntityBinaryArtifacts(playerGuid, subSceneGuids, artifactKeys);
            binaryFiles.Add(artifactKeys.Keys, artifactKeys.Values);

            var target = EditorUserBuildSettings.activeBuildTarget;

            var entitySceneGUIDs = binaryFiles.SceneGUIDs.ToArray();

            // Add any other from customizers
            types = TypeCache.GetTypesDerivedFrom<IEntitySceneBuildCustomizer>();
            foreach (var type in types)
            {
                var buildCustomizer = (IEntitySceneBuildCustomizer)Activator.CreateInstance(type);
                buildCustomizer.RegisterAdditionalFilesToDeploy(rootSceneGUIDs.ToArray(), entitySceneGUIDs, binaryFiles, RegisterAdditionalFileToDeploy);
            }

            EntitySceneBuildUtility.PrepareAdditionalFiles(playerGuid, binaryFiles.SceneGUIDs.ToArray(), binaryFiles.ArtifactKeys.ToArray(), target, RegisterAdditionalFileToDeploy);

            // Create and deploy resource catalog containing data of gameobject scenes that needs to be loaded in SceneSystem.Create
            AddResourceCatalog(rootSceneInfos.ToArray(), RegisterAdditionalFileToDeploy);
        }

        void AddResourceCatalog(RootSceneInfo[] sceneInfos, Action<string, string> registerAdditionalFileToDeploy)
        {
            var tempFile = Path.GetFullPath(Path.Combine(Application.dataPath, m_BuildWorkingDir, EntityScenesPaths.RelativePathForSceneInfoFile));
            ResourceCatalogBuildCode.WriteCatalogFile(sceneInfos, tempFile);
            registerAdditionalFileToDeploy(tempFile, EntityScenesPaths.RelativePathForSceneInfoFile);
        }
    }
}
