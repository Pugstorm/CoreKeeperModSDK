using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Scenes;
using Unity.Scenes.Editor;
using UnityEditor.Experimental;
using UnityEngine;
using UnityEditor;
using System.Linq;
using Unity.Entities.Build;

namespace Unity.Entities.Content
{
    /// <summary>
    /// Utility class for creating remote content data.
    /// </summary>
    public static class RemoteContentCatalogBuildUtility
    {
        [MenuItem("Assets/Publish/Existing Build")]
        static void ExistingBuildMenuItem()
        {
            var buildFolder = EditorUtility.OpenFolderPanel("Select Build To Publish", Path.GetDirectoryName(Application.dataPath), "Builds");
            if (!string.IsNullOrEmpty(buildFolder))
            {
                var streamingAssetsPath = $"{buildFolder}/{PlayerSettings.productName}_Data/StreamingAssets";
                PublishContent(streamingAssetsPath, $"{buildFolder}-RemoteContent", f => new string[] { "all" });
            }
        }

        [MenuItem("Assets/Publish/Content Update")]
        static void ContentUpdateMenuItem()
        {
            var buildFolder = EditorUtility.OpenFolderPanel("Select Build To Publish", Path.GetDirectoryName(Application.dataPath), "Builds");
            if (!string.IsNullOrEmpty(buildFolder))
            {
                var buildTarget = EditorUserBuildSettings.activeBuildTarget;
                var tmpBuildFolder = Path.Combine(Path.GetDirectoryName(Application.dataPath), $"Library/ContentUpdateBuildDir/{PlayerSettings.productName}");

                var instance = DotsGlobalSettings.Instance;
                var playerGuid = instance.GetPlayerType() == DotsGlobalSettings.PlayerType.Client ? instance.GetClientGUID() : instance.GetServerGUID();
                if (!playerGuid.IsValid)
                    throw new Exception("Invalid Player GUID");

                var subSceneGuids = new HashSet<Hash128>();
                for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
                {
                    var ssGuids = EditorEntityScenes.GetSubScenes(EditorBuildSettings.scenes[i].guid);
                    foreach (var ss in ssGuids)
                        subSceneGuids.Add(ss);
                }

                BuildContent(subSceneGuids, playerGuid, buildTarget, tmpBuildFolder);

                var publishFolder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds", $"{buildFolder}-RemoteContent");
                PublishContent(tmpBuildFolder, publishFolder, f => new string[] { "all" });
            }
        }

        /// <summary>
        /// Builds content for the player.  This can be used in conjunction with PublishContent to prepare a content update.
        /// </summary>
        /// <param name="subScenes">The subscenes to include in the build.</param>
        /// <param name="playerGUID">The player guid.  This can be provided from <seealso cref="DotsGlobalSettings"/> by calling GetClientGUID() or GetServerGUID().</param>
        /// <param name="target">The build target. <seealso cref="EditorUserBuildSettings.activeBuildTarget"/> can be used.</param>
        /// <param name="buildFolder">The folder to build the content into.</param>
        public static void BuildContent(HashSet<Hash128> subScenes, Hash128 playerGUID, BuildTarget target, string buildFolder)
        {
            Directory.CreateDirectory(buildFolder);

            var artifactKeys = new Dictionary<Hash128, ArtifactKey>();
            EntitySceneBuildUtility.PrepareEntityBinaryArtifacts(playerGUID, subScenes, artifactKeys);
            EntitySceneBuildUtility.PrepareAdditionalFiles(playerGUID, artifactKeys.Keys.ToArray(), artifactKeys.Values.ToArray(), target, (s, d) => DoCopy(s, Path.Combine(buildFolder, d)));
        }

        static void DoCopy(string src, string dst)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(src, dst, true);
        }

        /// <summary>
        /// Publish a folder of files.  This will copy or move files from the build folder to the target folder and rename them to the content hash.  Remote catalogs will also be created.
        /// </summary>
        /// <param name="sourceFolder">The folder with the source data.  Files will not be removed during the publish process.  If desired, the entire source folder can be deleted after this process is complete.</param>
        /// <param name="targetFolder">The target folder for the published data.  The structure is the same as the local cache, so this data can be directly installed on device to preload the cache.</param>
        /// <param name="contentSetFunc">This will be called for each file as it is published.  The returned strings will define the content sets that the file will be a part of.  If null is returned, the content will stay in the source folder and will not be published.</param>
        /// <param name="deleteSrcContent">If true, the src content files will be deleted from the build folder.  Ensure that a build is properly backed up before enabling this.</param>
        /// <returns>True if the publish process succeeds.</returns>
        public static bool PublishContent(string sourceFolder, string targetFolder, Func<string, IEnumerable<string>> contentSetFunc, bool deleteSrcContent = false)
        {
            try
            {
                Directory.CreateDirectory(targetFolder);
                var files = Directory.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories);
                var entries = new List<(RemoteContentId, RemoteContentLocation, IEnumerable<string>)>(files.Length);
                foreach (var f in files)
                {
                    var relPath = f.Substring(sourceFolder.Length + 1);
                    var contentSets = contentSetFunc.Invoke(relPath);
                    if (contentSets == null)
                        continue;
                    var loc = new RemoteContentLocation();
                    using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true))
                    {
                        var buffer = new byte[fs.Length];
                        fs.Read(buffer, 0, buffer.Length);
                        loc.Size = buffer.LongLength;
                        loc.Crc = 0; //TODO: compute CRC here
                        var hashStr = (loc.Hash = UnityEngine.Hash128.Compute(buffer)).ToString();
                        loc.Path = $"{hashStr[0]}{hashStr[1]}/{hashStr}";
                        relPath = relPath.Replace('\\', '/');
                        var remoteId = new RemoteContentId(relPath);
                        entries.Add((remoteId, loc, contentSets));
                    }
                    DoCopy(f, Path.Combine(targetFolder, loc.Path.ToString()));
                    if (deleteSrcContent)
                        File.Delete(f);
                }

                var catalogLocationsPath = ContentDeliveryGlobalState.CreateCatalogLocationPath(targetFolder);
                return CreateRemoteContentCatalogData(targetFolder, catalogLocationsPath, entries.Count, i => entries[i]);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        /// <summary>
        /// The full catalog will be created and saved as a cache entry with its content hash.  Another catalog that contains the location for the full catalog will be created
        /// at the specified locations path.  This file should be loaded without caching to check for updated versions of the full catalog and any content.
        /// </summary>
        /// <param name="targetFolder">The target folder to build the catalog data to.</param>
        /// <param name="catalogLocationsPath">The path of the catalog locations file.  This is NOT the full catalog, but a catalog of the full catalog.</param>
        /// <param name="count">The number of catalog entries.</param>
        /// <param name="indexer">The indexer to retrieve the catalog entry at the specified index.</param>
        /// <returns>True if the catalog data was created.</returns>
        static bool CreateRemoteContentCatalogData(string targetFolder, string catalogLocationsPath, int count, Func<int, (RemoteContentId, RemoteContentLocation, IEnumerable<string>)> indexer)
        {
            try
            {
                var catalogTempPath = Path.Combine(targetFolder, Path.GetRandomFileName());
                CreateRemoteContentLocationData(catalogTempPath, count, indexer);

                var catalogBytes = File.ReadAllBytes(catalogTempPath);
                var catalogSize = catalogBytes.Length;
                var catalogContentHash = UnityEngine.Hash128.Compute(catalogBytes);
                var catalogHashStr = catalogContentHash.ToString();
                var catalogLocationFileName = $"{catalogHashStr[0]}{catalogHashStr[1]}/{catalogHashStr}";

                DoCopy(catalogTempPath, Path.Combine(targetFolder, catalogLocationFileName));

                CreateRemoteContentLocationData(catalogLocationsPath, 1,
                    i => (new RemoteContentId(ContentDeliveryGlobalState.kCatalogLocations), new RemoteContentLocation { Hash = catalogContentHash, Path = catalogLocationFileName, Size = catalogSize, Crc = 0 },
                    new string[] { ContentDeliveryGlobalState.kCatalogLocations }));

                File.Delete(catalogTempPath);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }

        private static void CreateRemoteContentLocationData(string path, int locationCount, Func<int, (RemoteContentId, RemoteContentLocation, IEnumerable<string>)> locationIndexer)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var blobBuilder = new BlobBuilder(Allocator.Temp))
            {
                var setMap = new Dictionary<string, List<int>>();
                ref RemoteContentCatalogData catalog = ref blobBuilder.ConstructRoot<RemoteContentCatalogData>();
                var blobEntries = blobBuilder.Allocate(ref catalog.RemoteContentLocations, locationCount);
                for (int i = 0; i < locationCount; i++)
                {
                    var res = locationIndexer(i);
                    blobEntries[i] = new RemoteContentCatalogData.RemoteContentLocationData { identifier = res.Item1, location = res.Item2 };
                    if (res.Item3 != null)
                    {
                        foreach (var set in res.Item3)
                        {
                            if (!setMap.TryGetValue(set, out var setIndices))
                                setMap.Add(set, setIndices = new List<int>());
                            setIndices.Add(i);
                        }
                    }
                }
                if (setMap.Count > 0)
                {
                    var sets = blobBuilder.Allocate(ref catalog.ContentSets, setMap.Count);
                    int setIndex = 0;
                    foreach (var set in setMap)
                    {
                        sets[setIndex] = new RemoteContentCatalogData.RemoteContentSetData { Name = set.Key };
                        var ids = blobBuilder.Allocate(ref sets[setIndex].Ids, set.Value.Count);
                        for (int j = 0; j < set.Value.Count; j++)
                            ids[j] = set.Value[j];
                        setIndex++;
                    }
                }
                BlobAssetReference<RemoteContentCatalogData>.Write(blobBuilder, path, 1);
            }
        }
    }
}
