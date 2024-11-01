//#define ENABLE_CONTENT_BUILD_DIAGNOSTICS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Collections.NotBurstCompatible;
using Unity.Entities;
using Unity.Entities.Conversion;
using Unity.Entities.Serialization;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Utilities;
using BuildCompression = UnityEngine.BuildCompression;
using BuildPipeline = UnityEditor.BuildPipeline;
using Hash128 = Unity.Entities.Hash128;
using UnityEditor.Experimental;
using Unity.Entities.Content;
using UnityEditor.Build.Player;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Analytics;
using Object = UnityEngine.Object;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using Unity.Entities.Build;
using Unity.Loading;

namespace Unity.Scenes.Editor
{
    internal static class EntitySceneBuildUtility
    {
        internal static string WorkingBuildDir = $"Library/EntitySceneBundles";

        internal static void PrepareEntityBinaryArtifacts(Hash128 buildConfigurationGuid, HashSet<Hash128> sceneGuids, Dictionary<Hash128, ArtifactKey> artifactKeys)
        {
            var sceneBuildConfigGuids = new NativeList<GUID>(sceneGuids.Count, Allocator.TempJob);

            do
            {
                var requiresRefresh = false;
                foreach(var sceneGuid in sceneGuids)
                {
                    var guid = SceneWithBuildConfigurationGUIDs.EnsureExistsFor(sceneGuid, buildConfigurationGuid, false, out var thisRequiresRefresh);
                    sceneBuildConfigGuids.Add(guid);
                    requiresRefresh |= thisRequiresRefresh;
                    artifactKeys.Add(sceneGuid, new ArtifactKey(guid, typeof(SubSceneImporter)));
                }
                if (requiresRefresh)
                    AssetDatabase.Refresh();

                foreach (var sceneGuid in sceneGuids)
                {
                    SceneWithBuildConfigurationGUIDs.EnsureExistsFor(sceneGuid, buildConfigurationGuid, false, out var thisRequiresRefresh);
                    if(thisRequiresRefresh)
                        Debug.LogWarning("Refresh failed");
                }

                AssetDatabaseExperimental.ProduceArtifactsAsync(sceneBuildConfigGuids.ToArrayNBC(), typeof(SubSceneImporter));
                sceneGuids.Clear();

                foreach (var sceneBuildConfigGuid in sceneBuildConfigGuids)
                {
                    var artifactKey = AssetDatabaseExperimental.ProduceArtifact(new ArtifactKey(sceneBuildConfigGuid, typeof(SubSceneImporter)));
                    AssetDatabaseExperimental.GetArtifactPaths(artifactKey, out var paths);
                    var weakAssetRefsPath = EntityScenesPaths.GetLoadPathFromArtifactPaths(paths, EntityScenesPaths.PathType.EntitiesWeakAssetRefs);
                    if (!BlobAssetReference<BlobArray<UntypedWeakReferenceId>>.TryRead(weakAssetRefsPath, 1, out var weakAssets))
                        continue;
                    for(int i=0;i<weakAssets.Value.Length;++i)
                    {
                        var weakAssetRef = weakAssets.Value[i];
                        if (weakAssetRef.GenerationType == WeakReferenceGenerationType.EntityScene || weakAssetRef.GenerationType == WeakReferenceGenerationType.EntityPrefab)
                        {
                            if(!artifactKeys.ContainsKey(weakAssetRef.GlobalId.AssetGUID))
                                sceneGuids.Add(weakAssetRef.GlobalId.AssetGUID);
                        }
                    }
                    weakAssets.Dispose();
                }
                sceneBuildConfigGuids.Clear();
            } while (sceneGuids.Count > 0);

            sceneBuildConfigGuids.Dispose();
        }

        static unsafe BuildUsageTagGlobal ReadGlobalUsageArtifact(string globalUsgExt, string[] artifactPaths)
        {
            var path = artifactPaths.Where(x => x.EndsWith(globalUsgExt, StringComparison.Ordinal)).FirstOrDefault();
            if (string.IsNullOrEmpty(path))
                return default;

            BuildUsageTagGlobal globalUsage = default;
            using (var reader = new StreamBinaryReader(path))
            {
                reader.ReadBytes(&globalUsage, sizeof(BuildUsageTagGlobal));
            }
            return globalUsage;
        }

        // This function is responsible for providing all the entity scenes to the build.
        //
        // The way these files get generated is that we have a SceneWithBuildConfiguration file, (which is a bit of a hack to work around the inability for scriptable importers to take arguments, so
        // instead we create a different file that points to the scene we want to import, and points to the buildconfiguration we want to import it for).   The SubsceneImporter will import this file,
        // and it will make 3 (relevant) kind of files:
        // - headerfile
        // - entitybinaryformat file (the actual entities payloads)
        // - a SerializedFile that has an array of UnityEngine.Object PPtrs that are used by this entity file.
        //
        // The first two we deal with very simply: they just need to be copied into the build, and we're done.
        // the third one, we will feed as input to the Scriptable build pipeline (which is actually about creating assetbundles), and create an assetbundle that
        // has all those objects in it that the 3rd file referred to.  We do this with a batch api, first we loop through all subscenes, and register with this batch
        // api which assetbundles we'd like to see produced, and then at the end, we say "okay make them please".  this assetbundle creation api has a caching system
        // that is separate from the assetpipeline caching system, so if all goes well, the call to produce these assetbundles will return very fast and did nothing.
        //
        // The reason for the strange looking api, where a two callbacks get passed in is to make integration of the new incremental buildpipeline easier, as this code
        // needs to be compatible both with the current buildpipeline in the dots-repo, as well as with the incremental buildpipeline.  When that is merged, we can simplify this.
        internal static void PrepareAdditionalFiles(Hash128 playerGuid, Hash128[] sceneGuids, ArtifactKey[] entitySceneArtifacts, BuildTarget target, Action<string, string> RegisterFileCopy)
        {
            if (target == BuildTarget.NoTarget)
                throw new InvalidOperationException($"Invalid build target '{target.ToString()}'.");

            Assert.AreEqual(sceneGuids.Length, entitySceneArtifacts.Length);

            var subScenePaths = new Dictionary<Hash128, string>();

            var refExt = $".{EntityScenesPaths.GetExtension(EntityScenesPaths.PathType.EntitiesUnityObjectReferences)}";
            var headerExt = $".{EntityScenesPaths.GetExtension(EntityScenesPaths.PathType.EntitiesHeader)}";
            var binaryExt = $".{EntityScenesPaths.GetExtension(EntityScenesPaths.PathType.EntitiesBinary)}";
            string globalUsgExt = $".{EntityScenesPaths.GetExtension(EntityScenesPaths.PathType.EntitiesGlobalUsage)}";
            var weakAssetsExt = $".{EntityScenesPaths.GetExtension(EntityScenesPaths.PathType.EntitiesWeakAssetRefs)}";
            var weakAssetRefs = new HashSet<UntypedWeakReferenceId>();
            int subSceneAssetCount = 0;
            string exportedTypes = $".{EntityScenesPaths.GetExtension(EntityScenesPaths.PathType.EntitiesExportedTypes)}";

            var group = BuildPipeline.GetBuildTargetGroup(target);
            var artifactHashes = new UnityEngine.Hash128[entitySceneArtifacts.Length];
            AssetDatabaseCompatibility.ProduceArtifactsRefreshIfNecessary(entitySceneArtifacts, artifactHashes);
            var pathOverrides = new Dictionary<string, UntypedWeakReferenceId>();
            var customContent = new List<CustomContent>();
            var objIdRemapping = new Dictionary<Hash128, UntypedWeakReferenceId>();

            List<(Hash128, string)> sceneGuidExportedTypePaths = new List<(Hash128, string)>();
            for (int i = 0; i != entitySceneArtifacts.Length; i++)
            {
                var sceneGuid = sceneGuids[i];
                var sceneBuildConfigGuid = entitySceneArtifacts[i].guid;
                var artifactHash = artifactHashes[i];
                var scenePath = AssetDatabaseCompatibility.GuidToPath(sceneGuid);
#if ENABLE_CONTENT_BUILD_DIAGNOSTICS
                Debug.Log($"Processing scene {scenePath} with guid {sceneGuid}, artifact hash: {artifactHash}");
#endif

                if (!artifactHash.isValid)
                    throw new Exception($"Building EntityScene artifact failed: '{AssetDatabaseCompatibility.GuidToPath(sceneGuid)}' ({sceneGuid}). There were exceptions during the entity scene imports.");

                AssetDatabaseCompatibility.GetArtifactPaths(artifactHash, out var artifactPaths);

                // We expect a lot more files, something went wrong, try a forced reimport
                if (artifactPaths.Length <= 1)
                {
                    var needRefresh = SceneWithBuildConfigurationGUIDs.Dirty(sceneGuid, playerGuid);
                    if(needRefresh)
                        AssetDatabase.Refresh();

                    artifactHash = AssetDatabaseCompatibility.ProduceArtifact(entitySceneArtifacts[i]);

                    AssetDatabaseCompatibility.GetArtifactPaths(artifactHash, out artifactPaths);

                    if(artifactPaths.Length <= 1)
                        throw new InvalidOperationException($"Failed to build EntityScene for '{AssetDatabaseCompatibility.GuidToPath(sceneGuid)}'");
                }

                foreach (var artifactPath in artifactPaths)
                {
                    var ext = Path.GetExtension(artifactPath);

                    if (ext == headerExt)
                    {
                        var destinationFile = EntityScenesPaths.RelativePathForSceneFile(sceneGuid, EntityScenesPaths.PathType.EntitiesHeader, -1);
                        if (!string.IsNullOrEmpty(artifactPaths.FirstOrDefault(a => a.EndsWith(refExt, StringComparison.Ordinal))))
                        {
                            subScenePaths[sceneGuid] = artifactPath;
                        }
                        else
                        {
                            //if there are no reference bundles, then deduplication can be skipped
                            RegisterFileCopy(artifactPath, destinationFile);
                        }
                    }
                    else if (ext == binaryExt)
                    {
                        var destinationFile = EntityScenesPaths.RelativePathForSceneFile(sceneGuid, EntityScenesPaths.PathType.EntitiesBinary, EntityScenesPaths.GetSectionIndexFromPath(artifactPath));
                        RegisterFileCopy(artifactPath, destinationFile);
                    }
                    else if (ext == refExt)
                    {
                        /*
                        This section is for unity objects referenced by entity scenes.  On SubScene import, an artifact containing direct references
                        to these objects is created for each section.
                        During the build, the artifact is passed to the build pipeline using the type ReferencedUnityObjects.
                        When the entity scene loads, it will use the scene guid and section index to compute the runtime id of this
                        object to load it and pass the referenced objects into the deserialization code to patch references.
                        CreateAssetEntryForObjectIdentifiers will internally generate an id from the address passed in, so a mapping from that id to
                        the scene section hash is created in order to link the data at runtime.
                        */
                        var sectionIndex = EntityScenesPaths.GetSectionIndexFromPath(artifactPath);
                        var address = $"{artifactHash}.{sectionIndex}";
                        var id = UnityEngine.Hash128.Compute(address);
                        var ssh = SceneHeaderUtility.CreateSceneSectionHash(sceneGuid, sectionIndex, default);
                        objIdRemapping.Add(id, ssh);
                        pathOverrides[artifactPath] = ssh;
#if ENABLE_CONTENT_BUILD_DIAGNOSTICS
                        Debug.Log($"Scene {sceneGuid}: ReferencedUnityObjects section {sectionIndex}, build id {id}, runtime section id {ssh}, path override: {artifactPath}");
#endif
                        var globalUsage = ReadGlobalUsageArtifact(globalUsgExt, artifactPaths);
                        customContent.Add(new CustomContent
                        {
                            Asset = GUID.Generate(),
                            Processor = (guid, processor) =>
                            {
                                var objs = UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(artifactPath);
                                processor.GetObjectIdentifiersAndTypesForSerializedFile(artifactPath, out ObjectIdentifier[] objectIds, out Type[] types, globalUsage);
                                processor.CreateAssetEntryForObjectIdentifiers(objectIds, artifactPath, address, address, typeof(ReferencedUnityObjects));
                                foreach (var obj in objs)
                                {
                                    ReferencedUnityObjects referencedObjects = obj as ReferencedUnityObjects;
                                    if (referencedObjects != null)
                                        subSceneAssetCount += referencedObjects.Array.Length;
                                }
                            }
                        });
                    }
                    else if (ext == exportedTypes)
                    {
                        sceneGuidExportedTypePaths.Add((sceneGuid, artifactPath));
					}
                    else if (ext == weakAssetsExt)
                    {
                        /*
                        This section is for any embedded UntypedWeakReferenceIds in the converted entity scene data.  During the import process
                        of SubScenes, all weak references are collected and saved to this artifact.  These references are combined
                        with the direct references and built together to allow for proper deduplication and data sharing.
                        */
#if ENABLE_CONTENT_BUILD_DIAGNOSTICS
                        Debug.Log($"Scene {sceneGuid}: weak assets artifact found at path {artifactPath}");
#endif

                        if (BlobAssetReference<BlobArray<UntypedWeakReferenceId>>.TryRead(artifactPath, 1, out var weakAssets))
                        {
                            for (int j = 0; j < weakAssets.Value.Length; ++j)
                            {
                                var id = weakAssets.Value[j];
#if ENABLE_CONTENT_BUILD_DIAGNOSTICS
                                Debug.Log($"Scene {sceneGuid}: weak asset found {id}");
#endif
                                _ = weakAssetRefs.Add(id);
                            }
                            weakAssets.Dispose();
                        }
                    }
                }
            }

 			WriteExportedTypesDebugLog(sceneGuidExportedTypePaths);
            Func<Hash128, long, string, UntypedWeakReferenceId> objIdToRTId = (Hash128 guid, long lfid, string path) =>
            {
                if (!guid.IsValid && !string.IsNullOrEmpty(path))
                {
#if ENABLE_CONTENT_BUILD_DIAGNOSTICS
                    Debug.Log($"objIdToRTId {guid}, {lfid}, {path} using path override -> {pathOverrides[path]}");
#endif
                    if (!pathOverrides.TryGetValue(path, out var overrideId))
                    {
#if ENABLE_CONTENT_BUILD_DIAGNOSTICS
                    Debug.Log($"Path override not found for {path}, skipping.");
#endif
                    }
                    return overrideId;
                }
                var id = new UntypedWeakReferenceId { GlobalId = new RuntimeGlobalObjectId { AssetGUID = guid, SceneObjectIdentifier0 = lfid }, GenerationType = WeakReferenceGenerationType.UnityObject };
                if (!weakAssetRefs.Contains(id))
                {
#if ENABLE_CONTENT_BUILD_DIAGNOSTICS
                    Debug.Log($"Id not found in WeakAssetRefs, skipping {id}");
#endif
                    return default;
                }
#if ENABLE_CONTENT_BUILD_DIAGNOSTICS
                Debug.Log($"Id found in WeakAssetRefs, using {id}");
#endif
                return id;
            };
            Func<UntypedWeakReferenceId, UntypedWeakReferenceId> idRemapFunc = (UntypedWeakReferenceId i) =>
            {
                if (objIdRemapping.TryGetValue(i.GlobalId.AssetGUID, out var s))
                {
#if ENABLE_CONTENT_BUILD_DIAGNOSTICS
                    Debug.Log($"IdRemapFunc {i} remapped to {s}");
#endif
                    return s;
                }
#if ENABLE_CONTENT_BUILD_DIAGNOSTICS
                Debug.Log($"IdRemapFunc, not remap for {i}");
#endif

                return i;
            };

            var returnCode = BuildContent(target, weakAssetRefs, customContent, RegisterFileCopy, objIdToRTId, idRemapFunc, sceneGuids.Length, ref subSceneAssetCount);

            if (returnCode < ReturnCode.Success)
                throw new InvalidOperationException($"ContentCatalogBuildUtility.BuildContentArchives failed with status '{returnCode}'.");

            foreach (var ssIter in subScenePaths)
            {
                string headerArtifactPath = ssIter.Value;
                Hash128 sceneGUID = ssIter.Key;

                var tempPath = $"{WorkingBuildDir}/{sceneGUID}.{EntityScenesPaths.GetExtension(EntityScenesPaths.PathType.EntitiesHeader)}";

                if (!ResolveSceneSectionUtility.ReadHeader(headerArtifactPath, out var sceneMetaDataRef, sceneGUID, out var headerBlobOwner))
                    continue;

                UpdateSceneMetaDataDependencies(ref sceneMetaDataRef, tempPath);
                sceneMetaDataRef.Dispose();
                headerBlobOwner.Release();

                var headerDestPath = EntityScenesPaths.RelativePathForSceneFile(sceneGUID, EntityScenesPaths.PathType.EntitiesHeader, -1);
                RegisterFileCopy(tempPath, headerDestPath);
            }
        }

        internal static Action<Dictionary<Hash128, Dictionary<SceneSection, List<Hash128>>>> PostBuildCallback;

        internal static string GetExportedTypesLogsFilePath()
        {
            return Application.dataPath + "/../Logs/" + SerializeUtility.k_ExportedTypesDebugLogFileName;
        }

        internal static void WriteExportedTypesDebugLog(IEnumerable<(Hash128 sceneGuid, string entitiesExportedTypesPath)> scenes)
        {
            StreamWriter writer = File.CreateText(GetExportedTypesLogsFilePath());

            // Write all types
            writer.WriteLine($"::All Types in TypeManager (by stable hash)::");
            IEnumerable<TypeManager.TypeInfo> typesToWrite = TypeManager.AllTypes;
            var debugTypeHashes = typesToWrite.OrderBy(ti => ti.StableTypeHash)
                .Where(ti => ti.Type != null).Select(ti =>
                    $"0x{ti.StableTypeHash:x16} - {ti.StableTypeHash,22} - {ti.Type.FullName}");
            foreach(var type in debugTypeHashes)
                writer.WriteLine(type);
            writer.WriteLine("\n");

            // Write all exported types per scene
            foreach(var scene in scenes)
            {
                var srcLogFile = File.ReadLines(scene.entitiesExportedTypesPath);
                writer.WriteLine($"Exported Types (by stable hash) for scene: {scene.sceneGuid.ToString()}");
                foreach (var line in srcLogFile)
                {
                    if(line.StartsWith("0x"))
                        writer.WriteLine(line);
                }
                writer.WriteLine("\n");
            }
            writer.Close();
        }

        static void UpdateSceneMetaDataDependencies(ref BlobAssetReference<SceneMetaData> sceneMetaData, string outPath)
        {
            var blob = new BlobBuilder(Allocator.Temp);
            ref var root = ref blob.ConstructRoot<SceneMetaData>();
            var sectionDataArray = blob.Construct(ref root.Sections, sceneMetaData.Value.Sections.ToArray());

            // recursively copy scene section metadata
            {
                ref var sceneSectionCustomMetadata = ref sceneMetaData.Value.SceneSectionCustomMetadata;
                var sceneMetaDataLength = sceneSectionCustomMetadata.Length;
                var dstMetadataArray = blob.Allocate(ref root.SceneSectionCustomMetadata, sceneMetaDataLength);

                for (int i = 0; i < sceneMetaDataLength; i++)
                {
                    var metaData = blob.Allocate(ref dstMetadataArray[i], sceneSectionCustomMetadata[i].Length);
                    for (int j = 0; j < metaData.Length; j++)
                    {
                        metaData[j].StableTypeHash = sceneSectionCustomMetadata[i][j].StableTypeHash;
                        blob.Construct(ref metaData[j].Data, sceneSectionCustomMetadata[i][j].Data.ToArray());
                    }
                }
            }

            blob.AllocateString(ref root.SceneName, sceneMetaData.Value.SceneName.ToString());
            EditorEntityScenes.WriteHeader(outPath, ref root, sectionDataArray, blob);
        }

        internal static SectionDependencyInfo CreateDependencyInfo(ObjectIdentifier[] objectIds, BuildTarget target, UnityEditor.Build.Player.TypeDB scriptInfo)
        {
            //TODO: cache this dependency data
            var dependencies = ContentBuildInterface.GetPlayerDependenciesForObjects(objectIds, target, scriptInfo);
            var depTypes = ContentBuildInterface.GetTypeForObjects(dependencies);
            var paths = dependencies.Select(i => AssetDatabaseCompatibility.GuidToPath(i.guid)).ToArray();
            return new SectionDependencyInfo() { Dependencies = dependencies, Paths = paths, Types = depTypes };
        }

        internal struct SectionDependencyInfo
        {
            public ObjectIdentifier[] Dependencies;
            public Type[] Types;
            public string[] Paths;
        }

        static ReturnCode ExtractDuplicateObjects(IBuildParameters parameters, Dictionary<SceneSection, SectionDependencyInfo> dependencyInpuData, BundleExplictObjectLayout layout, HashSet<string> bundleNames, Dictionary<Hash128, Dictionary<SceneSection, List<Hash128>>> result)
        {
            var bundleLayout = new Dictionary<Hash128, List<ObjectIdentifier>>();
            CreateAssetLayoutData(dependencyInpuData, result, bundleLayout);
            ExtractExplicitBundleLayout(bundleLayout, layout, bundleNames);
            return ReturnCode.Success;
        }

        static void ExtractExplicitBundleLayout(Dictionary<Hash128, List<ObjectIdentifier>> bundleLayout, BundleExplictObjectLayout layout, HashSet<string> bundleNames)
        {
            foreach (var sectionIter in bundleLayout)
            {
                var bundleName = $"{sectionIter.Key}.bundle";
                foreach (var i in sectionIter.Value)
                {
                    try
                    { layout.ExplicitObjectLocation.Add(i, bundleName); }
                    catch { Debug.LogError($"Trying to add bundle: '{bundleName}' current value '{layout.ExplicitObjectLocation[i]}' object type '{ContentBuildInterface.GetTypeForObject(i).Name}'"); };
                }
                bundleNames.Add(bundleName);
            }
        }

        /// <summary>
        /// Create bundle layout and depedendency data for subscene bundles
        /// </summary>
        /// <param name="dependencyInputData">Mapping of SceneSection to dependency info for that section.</param>
        /// <param name="dependencyResult">Mapping of subscene id to mapping of section to bundle ids</param>
        /// <param name="bundleLayoutResult">Mapping of bundle ids to included objects</param>
        internal static void CreateAssetLayoutData(Dictionary<SceneSection, SectionDependencyInfo> dependencyInputData, Dictionary<Hash128, Dictionary<SceneSection, List<Hash128>>> dependencyResult, Dictionary<Hash128, List<ObjectIdentifier>> bundleLayoutResult)
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            if (!ValidateInput(dependencyInputData, out var error))
            {
                Debug.Log(error);
                return;
            }

            var depToSections = new Dictionary<ObjectIdentifier, List<SceneSection>>();
            //for each subscene, collect all dependencies and map them to the scenes they are referenced by.
            //also create a mapping from the subscene to all of its depedencies
            foreach (var sectionIter in dependencyInputData)
            {
                foreach (var dependency in sectionIter.Value.Dependencies)
                {
                    // Built In Resources we reference directly
                    if (dependency.guid == GUIDHelper.UnityBuiltinResources)
                        continue;

                    if (!depToSections.TryGetValue(dependency, out List<SceneSection> sectionList))
                    {
                        sectionList = new List<SceneSection>();
                        depToSections.Add(dependency, sectionList);
                    }
                    sectionList.Add(sectionIter.Key);
                }
            }

            //convert each list of scenes into a hash
            var objToSectionUsageHash = new Dictionary<ObjectIdentifier, Hash128>();
            foreach (var objIter in depToSections)
            {
                if (objIter.Value.Count <= 1)
                    continue;

                objToSectionUsageHash.Add(objIter.Key, HashingMethods.Calculate(objIter.Value).ToHash128());
            }

            if (objToSectionUsageHash.Count > 0)
            {
                //create mapping from scene hash to included dependencies
                foreach (var objIter in objToSectionUsageHash)
                {
                    if (!bundleLayoutResult.TryGetValue(objIter.Value, out var ids))
                        bundleLayoutResult.Add(objIter.Value, ids = new List<ObjectIdentifier>());
                    ids.Add(objIter.Key);
                }

                foreach (var sectionIter in dependencyInputData)
                {
                    var bundleHashes = new HashSet<Hash128>();
                    foreach (var dep in dependencyInputData[sectionIter.Key].Dependencies)
                        if (objToSectionUsageHash.TryGetValue(dep, out var sceneHash))
                            bundleHashes.Add(sceneHash);
                    if (!dependencyResult.TryGetValue(sectionIter.Key.SceneGUID, out var sectionMap))
                        dependencyResult.Add(sectionIter.Key.SceneGUID, sectionMap = new Dictionary<SceneSection, List<Hash128>>());
                    sectionMap[sectionIter.Key] = bundleHashes.ToList();
                }
            }

            sw.Stop();
            Console.WriteLine($"CreateAssetLayoutData time: {sw.Elapsed}");
        }

        internal static bool ValidateInput(Dictionary<SceneSection, SectionDependencyInfo> dependencyInputData, out string firstError)
        {
            firstError = null;
            if (dependencyInputData == null)
            {
                firstError = "NULL dependencyInputData.";
                return false;
            }
            foreach (var sec in dependencyInputData)
            {
                if (!sec.Key.SceneGUID.IsValid)
                {
                    firstError = "Invalid scene guid for section.";
                    return false;
                }
                if (sec.Key.Section < 0)
                {
                    firstError = $"Scene {sec.Key.SceneGUID} - Invalid section index {sec.Key.Section}.";
                    return false;
                }
                if (sec.Value.Dependencies == null)
                {
                    firstError = $"Scene {sec.Key.SceneGUID} - null Dependencies.";
                    return false;
                }
                if (sec.Value.Paths == null)
                {
                    firstError = $"Scene {sec.Key.SceneGUID} - null Paths.";
                    return false;
                }
                if (sec.Value.Types == null)
                {
                    firstError = $"Scene {sec.Key.SceneGUID} - null Types.";
                    return false;
                }
                if (sec.Value.Dependencies.Length != sec.Value.Paths.Length || sec.Value.Dependencies.Length != sec.Value.Types.Length)
                {
                    firstError = $"Scene {sec.Key.SceneGUID} - Data length mismatch: Dependencies: {sec.Value.Dependencies.Length}, Types: {sec.Value.Types.Length}, Paths: {sec.Value.Paths.Length}.";
                    return false;
                }
                for (int i = 0; i < sec.Value.Dependencies.Length; i++)
                {
                    if (sec.Value.Dependencies[i].guid.Empty())
                    {
                        firstError = $"Scene {sec.Key.SceneGUID} - Dependencies[{i}] has invalid GUID, path='{sec.Value.Paths[i]}'.";
                        return false;
                    }
                    if (sec.Value.Types[i] == null)
                    {
                        firstError = $"Scene {sec.Key.SceneGUID} - Types[{i}] is NULL, path='{sec.Value.Paths[i]}'.";
                        return false;
                    }
                    if (string.IsNullOrEmpty(sec.Value.Paths[i]))
                    {
                        firstError = $"Scene {sec.Key.SceneGUID} - Paths[{i}] is NULL or empty.";
                        return false;
                    }
                }
            }
            return true;
        }

        internal class UpdateBundlePacking : IBuildTask
        {
            public int Version { get { return 1; } }

#pragma warning disable 649
            [InjectContext]
            IBundleWriteData m_WriteData;

            [InjectContext(ContextUsage.In, true)]
            IBundleExplictObjectLayout m_Layout;

            [InjectContext(ContextUsage.In)]
            IDeterministicIdentifiers m_PackingMethod;
#pragma warning restore 649

            public ReturnCode Run()
            {
                if (m_Layout != null)
                {
                    var extractedBundlesToFileDependencies = new Dictionary<string, HashSet<string>>();
                    foreach (var pair in m_Layout.ExplicitObjectLocation)
                    {
                        ObjectIdentifier objectID = pair.Key;
                        string bundleName = pair.Value;
                        string internalName = string.Format(CommonStrings.AssetBundleNameFormat, m_PackingMethod.GenerateInternalFileName(bundleName));
                        foreach (var assetFilesPair in m_WriteData.AssetToFiles)
                        {
                            if (assetFilesPair.Value.Contains(internalName))
                            {
                                if (!extractedBundlesToFileDependencies.TryGetValue(internalName, out var dependencies))
                                {
                                    extractedBundlesToFileDependencies.Add(internalName, dependencies = new HashSet<string>());
                                    foreach (var afp in assetFilesPair.Value)
                                        dependencies.Add(afp);
                                }
                            }
                        }
                    }
                    Dictionary<string, WriteCommand> fileToCommand = m_WriteData.WriteOperations.ToDictionary(x => x.Command.internalName, x => x.Command);
                    foreach (var pair in extractedBundlesToFileDependencies)
                    {
                        var refMap = m_WriteData.FileToReferenceMap[pair.Key];
                        foreach (var fileDependency in pair.Value)
                        {
                            var cmd = fileToCommand[fileDependency];
                            refMap.AddMappings(fileDependency, cmd.serializeObjects.ToArray());
                        }
                        var cmd2 = fileToCommand[pair.Key];
                        refMap.AddMappings(pair.Key, cmd2.serializeObjects.ToArray(), true);
                    }
                }
                return ReturnCode.Success;
            }
        }

        static IList<IBuildTask> CreateTaskList()
        {
            var taskList = DefaultBuildTasks.Create(DefaultBuildTasks.Preset.AssetBundleBuiltInShaderExtraction);
            // Remove the shader task to use the DOTS dedupe pass only
            taskList.Remove(taskList.First(x => x is CreateBuiltInShadersBundle));
            // Insert the dedupe dependency resolver task
            taskList.Insert(taskList.IndexOf(taskList.First(x => x is GenerateSubAssetPathMaps)), new UpdateBundlePacking());
            return taskList;
        }

        public static Func<IList<IBuildTask>> CustomBuildTaskListCreator;

        public static ReturnCode BuildContent(BuildTarget target, HashSet<UntypedWeakReferenceId> weakAssetRefs, List<CustomContent> customContent, Action<string, string> RegisterFileCopy, Func<Hash128, long, string, UntypedWeakReferenceId> objIdToRTId, Func<UntypedWeakReferenceId, UntypedWeakReferenceId> idRemapFunc, int numberOfSubScenesInBuild, ref int numberOfAssetsInSubScenes)
        {
            if (weakAssetRefs.Count == 0 && customContent.Count == 0)
                return ReturnCode.SuccessNotRun;
            var builtinReferences = new HashSet<UntypedWeakReferenceId>();
            var objReferences = new HashSet<UntypedWeakReferenceId>();
            var sceneReferences = new HashSet<UntypedWeakReferenceId>();
            var sceneGuidStrs = new HashSet<string>();
            foreach (var r in weakAssetRefs)
            {
                if (!r.IsValid)
                    continue;
                if (GUIDHelper.IsBuiltin(r.GlobalId.AssetGUID))
                {
                    builtinReferences.Add(r);
                }
                else if (AssetDatabase.GetMainAssetTypeAtPath(AssetDatabase.GUIDToAssetPath(r.GlobalId.AssetGUID)) == typeof(SceneAsset))
                {
                    sceneReferences.Add(r);
                    sceneGuidStrs.Add(r.GlobalId.AssetGUID.ToString());
                }
                else
                {
                    objReferences.Add(r);
                }
            }

            var contentIds = new ContentFileIdentifiers();
            var clusterOutput = new ClusterOutput();
            //if there is a custom build task list creator, use it instead of the default
            var taskList = CustomBuildTaskListCreator != null ? CustomBuildTaskListCreator() : DefaultBuildTasks.ContentFileCompatible();
            var returnCode = ContentArchivesBuildUtility.BuildContentArchives(target, objReferences, sceneReferences, customContent,
                $"{WorkingBuildDir}/{RuntimeContentManager.k_ContentArchiveDirectory}",
                out var results, taskList, contentIds, clusterOutput);

            int assetCount = clusterOutput.ObjectToLocalID.Keys.Count;

            if (returnCode < ReturnCode.Success)
                throw new InvalidOperationException($"ContentCatalogBuildUtility.BuildContentArchives failed with status '{returnCode}'.");

            //if there were no archives built, skip catalog creation.  This means that if only built in objects are referenced, they will not be loadable.
            if (returnCode != ReturnCode.SuccessNotRun)
            {
                var catalogPath = $"{WorkingBuildDir}/{RuntimeContentManager.RelativeCatalogPath}";
                var verboseCatalogPath = catalogPath.Replace(".bin", ".txt");

                var builtinObjs = builtinReferences.Select(s => (s, (long)s.GlobalId.SceneObjectIdentifier0));
                Func<string, ContentFileId> pathToFileIdFunc = (p) =>
                {
                    if (p.Equals(CommonStrings.UnityDefaultResourcePath, StringComparison.OrdinalIgnoreCase))
                        return default;
                    return new ContentFileId { Value = new Hash128(p) };
                };

                var src = new BuildResultsCatalogDataSource(results, builtinObjs, objIdToRTId, pathToFileIdFunc, clusterOutput, sceneGuidStrs);
                ContentCatalogBuildUtility.BuildCatalogDataRuntime(src, catalogPath, idRemapFunc);
                ContentCatalogBuildUtility.BuildCatalogDataVerbose(src, verboseCatalogPath, idRemapFunc);

                var dstCatalogPath = RuntimeContentManager.RelativeCatalogPath;
                RegisterFileCopy(catalogPath, dstCatalogPath);
                RegisterFileCopy(verboseCatalogPath, dstCatalogPath.Replace(".bin", ".txt"));
                foreach (var f in results.WriteResults)
                    RegisterFileCopy($"{WorkingBuildDir}/{RuntimeContentManager.k_ContentArchiveDirectory}/{f.Key}", $"{RuntimeContentManager.k_ContentArchiveDirectory}/{f.Key}");

                EntitySceneBuildAnalytics.ReportBuildEvent(src, numberOfAssetsInSubScenes,  weakAssetRefs.Count, numberOfSubScenesInBuild, assetCount, true);
            }
            else
            {
                EntitySceneBuildAnalytics.ReportBuildEvent(null, numberOfAssetsInSubScenes, weakAssetRefs.Count, numberOfSubScenesInBuild, assetCount, false);
            }

            return returnCode;
        }
    }

    internal struct EditorPlayModeLoader : RuntimeContentManager.IAlternativeLoader
    {
        class LoadState
        {
            public UntypedWeakReferenceId refId;
            public AsyncOperation loadingOperation;
            public bool IsDone => loadingOperation.isDone;

            public bool IsValid => loadingOperation != null;

            public LoadState(UntypedWeakReferenceId id)
            {
                try
                {
                    refId = id;
                    if (refId.GenerationType == WeakReferenceGenerationType.SubSceneObjectReferences)
                    {
                        AssetDatabaseCompatibility.GetArtifactPaths(refId.GlobalId.AssetGUID, out var artifactPaths);
                        var loadPath = EntityScenesPaths.GetLoadPathFromArtifactPaths(artifactPaths, EntityScenesPaths.PathType.EntitiesUnityObjectReferences, (int)refId.GlobalId.SceneObjectIdentifier0);
                        if (loadPath == null)
                            Debug.LogError($"Failed to find artifact load path for id {id}");
                        else
                        {
                            #if !UNITY_DISABLE_MANAGED_COMPONENTS
                            Scene dstScene = CompanionGameObjectUtility.GetCompanionScene(false);
                            #else
                            Scene dstScene = default(Scene);
                            #endif
                            loadingOperation = UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForgetAsync(loadPath, 1, 0UL, -1L, dstScene);
                        }
                    }
                    else
                    {
                        loadingOperation = AssetDatabase.LoadObjectAsync(AssetDatabase.GUIDToAssetPath(refId.GlobalId.AssetGUID), refId.GlobalId.SceneObjectIdentifier0);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Caught exception while loading id {id}:\n{e}");
                }
            }

            public bool WaitForCompletion(int timeoutMs)
            {
                if (IsDone)
                    return true;
                if (refId.GenerationType == WeakReferenceGenerationType.SubSceneObjectReferences)
                    return (loadingOperation as UnityEditorInternal.LoadFileAndForgetOperation).Result != null;
                else
                    return (loadingOperation as AssetDatabaseLoadOperation).LoadedObject != null;
            }

            public object GetResult()
            {
                if (loadingOperation == null)
                    return null;
                if (refId.GenerationType == WeakReferenceGenerationType.SubSceneObjectReferences)
                    return (loadingOperation as UnityEditorInternal.LoadFileAndForgetOperation).Result;
                return (loadingOperation as AssetDatabaseLoadOperation).LoadedObject;
            }

            internal void Unload()
            {
                //in the editor, it is best to not unload anything as it may invalidate other direct references.
                //in a player build, any direct references that get pulled into player data will be duplicated
            }
        }

        Dictionary<UntypedWeakReferenceId, LoadState> _LoadingStates;
        Dictionary<UntypedWeakReferenceId, LoadState> LoadingStates
        {
            get
            {
                if (_LoadingStates == null)
                    _LoadingStates = new Dictionary<UntypedWeakReferenceId, LoadState>(100);
                return _LoadingStates;
            }
        }
        Dictionary<RuntimeContentManager.InstanceHandle, LoadState> _Instances;
        Dictionary<RuntimeContentManager.InstanceHandle, LoadState> Instances
        {
            get
            {
                if (_Instances == null)
                    _Instances = new Dictionary<RuntimeContentManager.InstanceHandle, LoadState>();
                return _Instances;
            }
        }

        [UnityEditor.InitializeOnLoadMethod]
        static void EditorInitializeOnLoadMethod()
        {
            RuntimeContentManager.OverrideLoader = new EditorPlayModeLoader();
        }

        public bool IsCreated => LoadingStates != null;

        public bool WaitForCompletion(UntypedWeakReferenceId objectId, int timeoutMs)
        {
            if (!LoadingStates.TryGetValue(objectId, out var state))
                throw new Exception($"IsLoaded should never be called when LoadingStates does not contain entry - id: {objectId}");
            return state.WaitForCompletion(timeoutMs);
        }

        public bool LoadObject(UntypedWeakReferenceId objectId)
        {
            if (LoadingStates.ContainsKey(objectId))
                throw new Exception($"Load should never be called when LoadingStates alread contains and entry - id: {objectId}");
            var ls = new LoadState(objectId);
            if (!ls.IsValid)
                return false;
            LoadingStates.Add(objectId, ls);
            return true;
        }

        public ObjectLoadingStatus GetObjectLoadStatus(UntypedWeakReferenceId objectId)
        {
            if (!LoadingStates.TryGetValue(objectId, out var state))
                throw new Exception($"IsLoaded should never be called when LoadingStates does not contain entry - id: {objectId}");
            return state.IsDone ? ObjectLoadingStatus.Completed : ObjectLoadingStatus.Loading;
        }

        public object GetObject(UntypedWeakReferenceId objectId)
        {
            if (!LoadingStates.TryGetValue(objectId, out var state))
                throw new Exception($"GetObject should never be called when LoadingStates does not contain entry - id: {objectId}");
            return state.GetResult();
        }

        public void UnloadScene(ref Scene scene)
        {
            SceneManager.UnloadSceneAsync(scene, UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
            scene = default;
        }

        public Scene LoadScene(UntypedWeakReferenceId sceneReferenceId, ContentSceneParameters loadParams)
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(AssetDatabase.GUIDToAssetPath(sceneReferenceId.GlobalId.AssetGUID), new LoadSceneParameters { loadSceneMode = loadParams.loadSceneMode, localPhysicsMode = loadParams.localPhysicsMode });
            return SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
        }

        public void Unload(UntypedWeakReferenceId objectId)
        {
            if (!LoadingStates.TryGetValue(objectId, out var state))
                throw new Exception($"Unload should never be called when LoadingStates does not contain entry - id: {objectId}");
            state.Unload();
            LoadingStates.Remove(objectId);
        }

        public void Dispose()
        {
            foreach(var s in LoadingStates)
                s.Value.Unload();
            _LoadingStates = null;
        }

        public bool LoadInstance(RuntimeContentManager.InstanceHandle handle)
        {
            var ls = new LoadState(handle.ObjectId);
            if (!ls.IsValid)
                return false;
            return Instances.TryAdd(handle, ls);
        }

        public bool WaitForCompletion(RuntimeContentManager.InstanceHandle handle, int timeoutMs)
        {
            if (!Instances.TryGetValue(handle, out var ls))
                return false;
            return ls.WaitForCompletion(timeoutMs);
        }

        public ObjectLoadingStatus GetInstanceLoadStatus(RuntimeContentManager.InstanceHandle handle)
        {
            if (!Instances.TryGetValue(handle, out var state))
                return ObjectLoadingStatus.None;
            return state.IsDone? ObjectLoadingStatus.Completed: ObjectLoadingStatus.Loading;
        }

        public Object GetInstance(RuntimeContentManager.InstanceHandle handle)
        {
            if (!Instances.TryGetValue(handle, out var state))
                return null;
            return state.GetResult() as Object;
        }

        public void ReleaseInstance(RuntimeContentManager.InstanceHandle handle)
        {
            if (Instances.Remove(handle, out var state))
                state.Unload();
        }
    }
}
