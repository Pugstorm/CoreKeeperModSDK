using System;
using System.Diagnostics;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Serialization;

namespace Unity.Scenes
{
    internal struct ResolveSceneSectionArchetypes
    {
        public EntityArchetype SectionEntityRequestLoad;
        public EntityArchetype SectionEntityNoLoad;
    }

    internal static class ResolveSceneSectionUtility
    {
        internal static ResolveSceneSectionArchetypes CreateResolveSceneSectionArchetypes(EntityManager manager)
        {
            return new ResolveSceneSectionArchetypes
            {
                SectionEntityRequestLoad = manager.CreateArchetype(
                    typeof(RequestSceneLoaded),
                     typeof(SceneSectionData),
                    typeof(SceneBoundingVolume),
                    typeof(SceneEntityReference),
                    typeof(ResolvedSectionPath)),
                SectionEntityNoLoad = manager.CreateArchetype(
                    typeof(SceneSectionData),
                    typeof(SceneBoundingVolume),
                    typeof(SceneEntityReference),
                    typeof(ResolvedSectionPath))
            };
        }

        internal static bool ResolveSceneSections(EntityManager manager, Entity sceneEntity, Hash128 sceneGUID, RequestSceneLoaded requestSceneLoaded, Hash128 artifactHash, string sceneLoadDir)
        {
            var bufLen = manager.AddBuffer<ResolvedSectionEntity>(sceneEntity).Length;
            manager.AddComponentData(sceneEntity, new ResolvedSceneHash { ArtifactHash = artifactHash });

#if UNITY_EDITOR
            AssetDatabaseCompatibility.GetArtifactPaths(artifactHash, out var paths);
            var sceneHeaderPath = EntityScenesPaths.GetLoadPathFromArtifactPaths(paths, EntityScenesPaths.PathType.EntitiesHeader);
#else
            var sceneHeaderPath = EntityScenesPaths.FullPathForFile(sceneLoadDir, EntityScenesPaths.RelativePathForSceneFile(sceneGUID, EntityScenesPaths.PathType.EntitiesHeader, -1));
#endif
            if (!ReadHeader(sceneHeaderPath, out var sceneMetaData, sceneGUID, out var headerBlobOwner))
                return false;
            var sectionPaths = new UnsafeList<ResolvedSectionPath>(sceneMetaData.Value.Sections.Length, Allocator.Temp);

#if UNITY_EDITOR
            SceneHeaderUtility.BuildSectionPaths(ref sectionPaths, ref sceneMetaData.Value, sceneGUID, paths, artifactHash);
#else
            SceneHeaderUtility.BuildSectionPathsForContentArchives(ref sectionPaths, ref sceneMetaData.Value, sceneGUID, sceneLoadDir);
#endif

            var resolveSceneSectionArchetypes = CreateResolveSceneSectionArchetypes(manager);

            return ResolveSceneSections(manager, sceneEntity, requestSceneLoaded, ref sceneMetaData.Value, resolveSceneSectionArchetypes, sectionPaths, headerBlobOwner);
        }

        internal static unsafe bool ResolveSceneSections(EntityManager entityManager, Entity sceneEntity, RequestSceneLoaded requestSceneLoaded, ref SceneMetaData sceneMetaData, ResolveSceneSectionArchetypes sectionArchetypes, UnsafeList<ResolvedSectionPath> sectionPaths, BlobAssetOwner headerBlobOwner)
        {
#if UNITY_EDITOR
            var sceneName = sceneMetaData.SceneName.ToString();
            var sceneNameFs = new FixedString64Bytes();
            FixedStringMethods.CopyFromTruncated(ref sceneNameFs, $"Scene: {sceneName}"); // Long scene names are truncated.
            entityManager.SetName(sceneEntity, sceneNameFs);
#endif
            // Resolve first (Even if the file doesn't exist we want to stop continuously trying to load the section)
             entityManager.AddComponent(sceneEntity, new ComponentTypeSet(typeof(ResolvedSectionEntity), typeof(LinkedEntityGroup)));
             entityManager.GetBuffer<LinkedEntityGroup>(sceneEntity).Add(sceneEntity);

            // If auto-load is enabled
            var loadSections = (requestSceneLoaded.LoadFlags & SceneLoadFlags.DisableAutoLoad) == 0;

            var sectionCount = sceneMetaData.Sections.Length;
            var sectionEntities = new NativeArray<Entity>(sectionCount, Allocator.Temp);
            entityManager.CreateEntity(loadSections ? sectionArchetypes.SectionEntityRequestLoad : sectionArchetypes.SectionEntityNoLoad, sectionEntities);
            var sectionEntitiesBuffer = entityManager.GetBuffer<ResolvedSectionEntity>(sceneEntity);
            sectionEntitiesBuffer.AddRange(sectionEntities.Reinterpret<ResolvedSectionEntity>());

            for (int i = 0; i != sceneMetaData.Sections.Length; i++)
            {
                var sectionEntity = sectionEntities[i];
                var sectionIndex = sceneMetaData.Sections[i].SubSectionIndex;
#if UNITY_EDITOR
                var sceneNameFs32 = new FixedString32Bytes(); // Ensure the sectionIndex fits too.
                FixedStringMethods.CopyFromTruncated(ref sceneNameFs32, sceneName);
                entityManager.SetName(sectionEntity, $"SceneSection: {sceneNameFs32} ({sectionIndex})");
#endif

                if (loadSections)
                {
                    entityManager.SetComponentData(sectionEntity, requestSceneLoaded);
                }

                entityManager.SetComponentData(sectionEntity, sceneMetaData.Sections[i]);
                entityManager.SetComponentData(sectionEntity, new SceneBoundingVolume { Value = sceneMetaData.Sections[i].BoundingVolume });
                entityManager.SetComponentData(sectionEntity, new SceneEntityReference { SceneEntity = sceneEntity });
                entityManager.SetComponentData(sectionEntity, sectionPaths[i]);

                entityManager.AddSharedComponent(sectionEntity, headerBlobOwner);

                if (entityManager.HasComponent<SceneTag>(sceneEntity))
                    entityManager.AddSharedComponent(sectionEntity, entityManager.GetSharedComponent<SceneTag>(sceneEntity));

                AddSectionMetadataComponents(sectionEntity, ref sceneMetaData.SceneSectionCustomMetadata[i], entityManager);

                var linkedEntityGroup = entityManager.GetBuffer<LinkedEntityGroup>(sceneEntity);
                linkedEntityGroup.Add(new LinkedEntityGroup { Value = sectionEntity });
            }
            sectionEntities.Dispose();
            return true;
        }

        internal static unsafe bool ReadHeader(string sceneHeaderPath, out BlobAssetReference<SceneMetaData> sceneMetaDataRef, Hash128 sceneGUID) =>
            ReadHeader(sceneHeaderPath, out sceneMetaDataRef, sceneGUID, out _, false);

        internal static unsafe bool ReadHeader(string sceneHeaderPath, out BlobAssetReference<SceneMetaData> sceneMetaDataRef, Hash128 sceneGUID, out BlobAssetOwner headerBlobOwner, bool readBlobAssetHeader = true)
        {
            headerBlobOwner = default;
            if (string.IsNullOrEmpty(sceneHeaderPath))
            {
                sceneMetaDataRef = default;
                SceneHeaderUtility.LogHeaderLoadError(SceneHeaderUtility.HeaderLoadStatus.MissingFile, sceneGUID);
                return false;
            }

            using (var binaryReader = new StreamBinaryReader(sceneHeaderPath, 16*1024))
            {
                if (!BlobAssetReference<SceneMetaData>.TryRead(binaryReader, SceneMetaDataSerializeUtility.CurrentFileFormatVersion, out sceneMetaDataRef))
                {
#if UNITY_EDITOR
                    Debug.LogError($"Loading Entity Scene failed because the entity header file was an old version or doesn't exist: guid={sceneGUID} path={sceneHeaderPath}");
#else
                    Debug.LogError($"Loading Entity Scene failed because the entity header file was an old version or doesn't exist: {sceneGUID}\nNOTE: In order to load SubScenes in the player you have to use the new BuildConfiguration asset based workflow to build & run your player.\n{sceneHeaderPath}");
#endif
                }

                if (readBlobAssetHeader)
                {
                    //Load header blob batch
                    var dataSize = sceneMetaDataRef.Value.HeaderBlobAssetBatchSize;
                    void* blobAssetBatch = Memory.Unmanaged.Allocate(dataSize, 16, Allocator.Persistent);
                    binaryReader.ReadBytes(blobAssetBatch, dataSize);
                    headerBlobOwner =  new BlobAssetOwner(blobAssetBatch, dataSize);
                }
            }


            if (readBlobAssetHeader)
            {
                for (int i = 0; i < sceneMetaDataRef.Value.Sections.Length; ++i)
                {
                    sceneMetaDataRef.Value.Sections[i].BlobHeader =
                        sceneMetaDataRef.Value.Sections[i].BlobHeader.Resolve(headerBlobOwner);
                }
            }
            return true;
        }
        internal static unsafe void AddSectionMetadataComponents(Entity sectionEntity, ref BlobArray<SceneSectionCustomMetadata> sectionMetaDataArray, EntityManager entityManager)
        {
            // Deserialize the SceneSection custom metadata
            for (var i = 0; i < sectionMetaDataArray.Length; i++)
            {
                ref var metadata = ref sectionMetaDataArray[i];
                var customTypeIndex = TypeManager.GetTypeIndexFromStableTypeHash(metadata.StableTypeHash);

                // Couldn't find the type...
                if (customTypeIndex == TypeIndex.Null)
                {
                    UnityEngine.Debug.LogError(
                        $"Couldn't import SceneSection metadata, couldn't find the type to deserialize with stable hash {metadata.StableTypeHash}");
                    continue;
                }

                entityManager.AddComponent(sectionEntity, ComponentType.FromTypeIndex(customTypeIndex));

                if (TypeManager.IsZeroSized(customTypeIndex))
                    continue;

                void* componentPtr = entityManager.GetComponentDataRawRW(sectionEntity, customTypeIndex);
                UnsafeUtility.MemCpy(componentPtr, metadata.Data.GetUnsafePtr(), metadata.Data.Length);
            }
        }
    }
}
